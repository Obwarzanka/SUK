using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace EdrLocal;

public sealed class SepeBackgroundWriter : ISepeHoursWriter, IDisposable
{
	private readonly SimRailSession session;

	private readonly WebView2 browser;

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private Uri loaded;

	private bool failed;

	private bool disposed;

	private string lastDialog;

	private StopForecastRequest activeOperation;

	private Func<string, Task<bool>> forecastConfirmation;

	private bool forecastDeclined;

	public bool SubmissionAttempted { get; private set; }

	public SepeBackgroundWriter(Control owner, SimRailSession value)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected Obj, but got Unknown
		session = value;
		WebView2 val = new WebView2();
		((Control)(object)val).Bounds = new Rectangle(-4, -4, 1, 1);
		((Control)(object)val).Visible = false;
		((Control)(object)val).TabStop = false;
		browser = val;
		owner.Controls.Add((Control)(object)browser);
		_ = ((Control)(object)browser).Handle;
	}

	public static Uri OperationUrl(SimRailSession session, StopForecastOperation operation)
	{
		if (!session.HasWriteLink)
		{
			throw new InvalidOperationException("Wklej aktualny link SEPE z gry.");
		}
		if (operation != StopForecastOperation.Forecast)
		{
			return session.AccessUrl;
		}
		return session.PublicUrl;
	}

	private async Task Pause(int milliseconds)
	{
		await Task.Delay(milliseconds, lifetime.Token);
		if (disposed || failed)
		{
			throw new InvalidOperationException("Przerwano połączenie SEPE.");
		}
	}

	private async Task Load(Uri target = null)
	{
		target = target ?? session.AccessUrl;
		if (!session.HasWriteLink)
		{
			throw new InvalidOperationException("Wklej aktualny link z gry w menu SimRail → Link SEPE z gry, a następnie wyślij oczekujące godziny.");
		}
		if (browser.CoreWebView2 == null)
		{
			SepeWebView.ConfigureLoader();
			WebView2 val = browser;
			CoreWebView2CreationProperties val2 = new CoreWebView2CreationProperties();
			val2.IsInPrivateModeEnabled = true;
			val2.UserDataFolder = SepeWebView.UserDataFolder;
			val.CreationProperties = val2;
			Task init = browser.EnsureCoreWebView2Async((CoreWebView2Environment)null, (CoreWebView2ControllerOptions)null);
			if (await Task.WhenAny(init, Task.Delay(30000, lifetime.Token)) != init)
			{
				throw new InvalidOperationException("Nie udało się uruchomić połączenia SEPE w tle. Sprawdź WebView2 Runtime.");
			}
			await init;
			lifetime.Token.ThrowIfCancellationRequested();
			CoreWebView2 core = browser.CoreWebView2;
			core.Settings.AreDevToolsEnabled = false;
			core.Settings.AreDefaultScriptDialogsEnabled = false;
			core.ScriptDialogOpening += ScriptDialog;
			core.Settings.IsPasswordAutosaveEnabled = false;
			core.Settings.IsGeneralAutofillEnabled = false;
			core.NewWindowRequested += (object s, CoreWebView2NewWindowRequestedEventArgs e) =>
			{
				e.Handled = true;
			};
			core.NavigationStarting += (object s, CoreWebView2NavigationStartingEventArgs e) =>
			{
				if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var result) || !SepeLink.Trusted(result))
				{
					e.Cancel = true;
					failed = true;
				}
			};
			core.ProcessFailed += (object s, CoreWebView2ProcessFailedEventArgs e) =>
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Invalid comparison between Unknown and I4
				if ((int)e.ProcessFailedKind == 0 || (int)e.ProcessFailedKind == 1)
				{
					failed = true;
				}
			};
		}
		if (loaded == target && !failed)
		{
			return;
		}
		failed = false;
		loaded = null;
		TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
		EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = (object s, CoreWebView2NavigationCompletedEventArgs e) =>
		{
			done.TrySetResult(e.IsSuccess);
		};
		browser.CoreWebView2.NavigationCompleted += handler;
		try
		{
			browser.CoreWebView2.Navigate(target.AbsoluteUri);
			if (await Task.WhenAny(done.Task, Task.Delay(30000, lifetime.Token)) != done.Task || !(await done.Task))
			{
				throw new InvalidOperationException("Nie udało się wczytać oficjalnego SEPE. Sprawdź aktualność linku z gry.");
			}
			loaded = target;
		}
		finally
		{
			browser.CoreWebView2.NavigationCompleted -= handler;
		}
	}

	private async Task<string> Script(string code)
	{
		lifetime.Token.ThrowIfCancellationRequested();
		if (disposed || failed)
		{
			throw new InvalidOperationException("Przerwano połączenie SEPE.");
		}
		string result = await browser.ExecuteScriptAsync(code);
		if (forecastDeclined)
		{
			throw new ForecastDeclinedException();
		}
		if (!string.IsNullOrWhiteSpace(lastDialog))
		{
			string text = lastDialog;
			lastDialog = null;
			if (!Regex.IsMatch(text, "(?i)^(zapisano|saved|successfully)"))
			{
				throw SepeChronologyException.FromPage("SEPE: " + text);
			}
		}
		return result;
	}

	private async void ScriptDialog(object sender, CoreWebView2ScriptDialogOpeningEventArgs e)
	{
		if ((int)e.Kind == 1 && activeOperation != null && StopForecastRequest.IsForecastQuestion(e.Message))
		{
			CoreWebView2Deferral deferred = e.GetDeferral();
			try
			{
				int num = default;
				_ = num;
				_ = 0;
				try
				{
					if (activeOperation.Operation == StopForecastOperation.Forecast || (forecastConfirmation != null && await forecastConfirmation(e.Message)))
					{
						e.Accept();
					}
					else
					{
						forecastDeclined = true;
					}
					return;
				}
				catch (Exception error)
				{
					lastDialog = SepeDiagnostics.Reason(error);
					return;
				}
			}
			finally
			{
				deferred.Complete();
			}
		}
		lastDialog = SepeDiagnostics.Clean(e.Message);
		if ((int)e.Kind == 0)
		{
			e.Accept();
		}
	}

	private async Task CheckPageErrors()
	{
		string json = await Script(SepeTimeTransfer.Errors());
		string error = Json.Read<string>(Encoding.UTF8.GetBytes(json));
		if (!string.IsNullOrWhiteSpace(error))
		{
			throw SepeChronologyException.FromPage("SEPE: " + SepeDiagnostics.Clean(error));
		}
	}

	public Task Send(SepeWriteRequest request)
	{
		return Send(request, null);
	}

	internal async Task Send(SepeWriteRequest request, Func<SepeWriteRequest> latestHours)
	{
		lastDialog = null;
		SubmissionAttempted = false;
		request.Validate(session);
		if ((request.ArrivalChanged && !request.Arrival.HasValue) || (request.DepartureChanged && !request.Departure.HasValue))
		{
			throw new InvalidOperationException("Usunięcie godziny wymaga zmiany bezpośrednio w oficjalnym SEPE.");
		}
		await Load();
		bool opened = false;
		int i = 0;
		while (i < 60)
		{
			await CheckPageErrors();
			switch (await Script(SepeTimeTransfer.Select(request)))
			{
			case "\"opened\"":
				break;
			case "\"different\"":
			case "\"ambiguous\"":
				throw new InvalidOperationException("Formularz SEPE nie wskazuje jednoznacznie wybranego pociągu i jego daty. Zapis wstrzymany.");
			default:
				await Pause(500);
				i++;
				continue;
			}
			opened = true;
			break;
		}
		if (!opened)
		{
			throw new InvalidOperationException("Nie znaleziono wybranego pociągu z właściwą datą w oficjalnym SEPE. Zapis wstrzymany.");
		}
		await SepeHoursPreparation.Submit(() =>
		{
			SepeWriteRequest sepeWriteRequest = ((latestHours == null) ? request : latestHours());
			if (sepeWriteRequest.TrainId != request.TrainId)
			{
				throw new InvalidOperationException("Zmieniono adresata godzin SEPE.");
			}
			sepeWriteRequest.Validate(session);
			return sepeWriteRequest;
		}, Script, Pause, () => session.Now, (bool value) =>
		{
			SubmissionAttempted = value;
		});
		for (int i2 = 0; i2 < 30; i2++)
		{
			await Pause(400);
			await CheckPageErrors();
			if (await Script(SepeTimeTransfer.State()) == "\"closed\"")
			{
				return;
			}
		}
		throw new InvalidOperationException("SEPE pozostawiło formularz otwarty. Sprawdź poprawność godzin i aktualność linku.");
	}

	public async Task SendOperation(StopForecastRequest request, Func<string, Task<bool>> confirmForecast = null)
	{
		lastDialog = null;
		forecastDeclined = false;
		SubmissionAttempted = false;
		request.Validate(session);
		await Load(OperationUrl(session, request.Operation));
		bool opened = false;
		int i = 0;
		while (i < 60)
		{
			await CheckPageErrors();
			switch (await Script(SepeTimeTransfer.Select(request.Identity)))
			{
			case "\"opened\"":
				break;
			case "\"different\"":
			case "\"ambiguous\"":
				throw new InvalidOperationException("Nie można jednoznacznie wskazać pociągu i daty w SEPE.");
			default:
				await Pause(500);
				i++;
				continue;
			}
			opened = true;
			break;
		}
		if (!opened)
		{
			throw new InvalidOperationException("Nie znaleziono pociągu z właściwą datą w formularzu SEPE.");
		}
		if (request.Operation != StopForecastOperation.CancelStop)
		{
			bool prepared = false;
			for (int attempt = 0; attempt < 3; attempt++)
			{
				for (int field = 0; field < 6; field++)
				{
					if (await Script(SepeTimeTransfer.Identity(request.Identity)) != "\"matched\"")
					{
						throw new InvalidOperationException("Zmieniono wybrany pociąg w SEPE.");
					}
					if (await Script(SepeTimeTransfer.Field(request.Arrival, request.Departure, session.Now, field)) != "\"prepared\"")
					{
						throw new InvalidOperationException("SEPE nie przyjęło daty lub godziny prognozy.");
					}
					await Pause(250);
				}
				await Pause(500);
				if (await Script(SepeTimeTransfer.Verify(request.Arrival, request.Departure, session.Now)) == "\"verified\"")
				{
					prepared = true;
					break;
				}
			}
			if (!prepared)
			{
				throw new InvalidOperationException("Formularz SEPE nie zachował prognozy.");
			}
		}
		request.Validate(session);
		SubmissionAttempted = true;
		activeOperation = request;
		forecastConfirmation = confirmForecast;
		try
		{
			if (await Script(request.IsStopChange ? SepeTimeTransfer.StopRequest(request.Identity, request.Stop, request.Stop ? new DateTime?(request.Arrival) : ((DateTime?)null), request.Stop ? new DateTime?(request.Departure) : ((DateTime?)null), session.Now) : SepeTimeTransfer.SubmitRequest(request.Identity, request.Arrival, request.Departure, session.Now)) != "\"submitted\"")
			{
				SubmissionAttempted = false;
				throw new InvalidOperationException("SEPE nie przyjęło operacji. Zmieniono pociąg, formularz albo uprawnienia.");
			}
			await SepeOperationCompletion.Wait(request, Script, async () =>
			{
				await Pause(300);
				await CheckPageErrors();
			}, confirmForecast);
		}
		finally
		{
			activeOperation = null;
			forecastConfirmation = null;
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			lifetime.Cancel();
			((Component)(object)browser).Dispose();
			lifetime.Dispose();
		}
	}
}
