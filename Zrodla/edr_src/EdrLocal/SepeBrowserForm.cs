using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace EdrLocal;

public sealed class SepeBrowserForm : ReferenceForm
{
	private readonly SimRailSession session;

	private readonly WebView2 browser;

	private readonly Label state;

	private string number;

	private DateTime? pendingArrival;

	private DateTime? pendingDeparture;

	private bool preparePending;

	private bool opening;

	private bool completed;

	private SepeWriteRequest automaticWrite;

	private bool ready;

	public event Action<bool, string> WriteCompleted;

	public SepeBrowserForm(SimRailSession value, string train)
	{
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Expected Obj, but got Unknown
		session = value;
		number = train;
		Text = "SimRail - oficjalne SEPE";
		Size = new Size(1300, 830);
		MinimumSize = new Size(850, 550);
		StartPosition = FormStartPosition.CenterParent;
		Font = ReferenceTheme.Dialog;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 42
		};
		flowLayoutPanel.Controls.Add(Ui.Button("Wklej link SEPE z gry…", PasteLink));
		Control.ControlCollection controls = flowLayoutPanel.Controls;
		Action action = () =>
		{
			SelectTrain(number);
		};
		controls.Add(Ui.Button("Wprowadzanie godzin…", action));
		flowLayoutPanel.Controls.Add(Ui.Button("Odśwież stronę", () =>
		{
			if (ready)
			{
				browser.Reload();
			}
		}));
		flowLayoutPanel.Controls.Add(Ui.Button("Otwórz w przeglądarce", OpenExternal));
		state = new Label
		{
			Dock = DockStyle.Bottom,
			Height = 38,
			Text = "Edytuj i zatwierdź godziny przyciskiem Zapisz w oficjalnym SEPE. Po zamknięciu tego okna EDR pobierze dane ponownie."
		};
		WebView2 val = new WebView2();
		((Control)(object)val).Dock = DockStyle.Fill;
		browser = val;
		Controls.Add((Control)(object)browser);
		Controls.Add(flowLayoutPanel);
		Controls.Add(state);
		Shown += async (object s, EventArgs e) =>
		{
			await Initialize();
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			((Component)(object)browser).Dispose();
			if (automaticWrite != null && !completed)
			{
				CompleteWrite(ok: false, "Okno SEPE zamknięto przed potwierdzeniem zapisu.");
			}
		};
	}

	private async Task Initialize()
	{
		try
		{
			if (automaticWrite != null && !session.HasWriteLink)
			{
				PasteLink();
				if (!session.HasWriteLink)
				{
					CompleteWrite(ok: false, "Do zapisu godzin potrzebny jest aktualny link SEPE z gry.");
					return;
				}
			}
			SepeWebView.ConfigureLoader();
			WebView2 val = browser;
			CoreWebView2CreationProperties val2 = new CoreWebView2CreationProperties();
			val2.IsInPrivateModeEnabled = true;
			val2.UserDataFolder = SepeWebView.UserDataFolder;
			val.CreationProperties = val2;
			await browser.EnsureCoreWebView2Async((CoreWebView2Environment)null, (CoreWebView2ControllerOptions)null);
			if (IsDisposed)
			{
				return;
			}
			ready = true;
			browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
			browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
			browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
			browser.CoreWebView2.NavigationStarting += (object s, CoreWebView2NavigationStartingEventArgs e) =>
			{
				if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var result) || !SepeLink.Trusted(result))
				{
					e.Cancel = true;
					state.Text = "Nawigacja poza oficjalne SEPE została zatrzymana.";
				}
			};
			browser.CoreWebView2.ProcessFailed += (object s, CoreWebView2ProcessFailedEventArgs e) =>
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Invalid comparison between Unknown and I4
				if ((int)e.ProcessFailedKind == 0 || (int)e.ProcessFailedKind == 1)
				{
					ready = false;
					state.Text = "Proces przeglądarki został przerwany. Użyj Otwórz w przeglądarce lub otwórz to okno ponownie.";
					if (automaticWrite != null)
					{
						CompleteWrite(ok: false, state.Text);
					}
				}
			};
			browser.CoreWebView2.NewWindowRequested += (object s, CoreWebView2NewWindowRequestedEventArgs e) =>
			{
				e.Handled = true;
			};
			browser.CoreWebView2.NavigationCompleted += async (object s, CoreWebView2NavigationCompletedEventArgs e) =>
			{
				if (!e.IsSuccess)
				{
					state.Text = "Nie udało się wczytać SEPE. Sprawdź połączenie lub wklej aktualny link z gry.";
				}
				else
				{
					await OpenTimes();
				}
			};
			Navigate();
		}
		catch (Exception)
		{
			if (!IsDisposed)
			{
				state.Text = "Przeglądarka wymaga Microsoft Edge WebView2 Runtime. Możesz użyć przycisku Otwórz w przeglądarce.";
				((Control)(object)browser).Visible = false;
				if (automaticWrite != null)
				{
					CompleteWrite(ok: false, state.Text);
				}
			}
		}
	}

	private void OpenExternal()
	{
		try
		{
			Process.Start((session.AccessUrl ?? session.PublicUrl).AbsoluteUri);
		}
		catch
		{
			throw new Exception("Nie można otworzyć przeglądarki systemowej. Sprawdź ustawienie domyślnej przeglądarki Windows.");
		}
	}

	private void Navigate()
	{
		if (ready)
		{
			browser.CoreWebView2.Navigate((session.AccessUrl ?? session.PublicUrl).AbsoluteUri);
		}
	}

	private void PasteLink()
	{
		using Editor editor = new Editor("Aktualny link SEPE z gry", 720, 260);
		TextBox text = editor.Add("Link SEPE", Ui.Text("", multiline: false), 35);
		text.UseSystemPasswordChar = true;
		editor.Info("Skopiuj link z SimRail dla bieżącego serwera i posterunku. Link nie będzie zapisany w bazie EDR.", 60);
		editor.Save(() =>
		{
			session.SetAccessLink(text.Text);
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			Navigate();
		}
	}

	public void PrepareHours(Train train)
	{
		pendingArrival = train.Arrival;
		pendingDeparture = train.Departure;
		preparePending = true;
		SelectTrain(train.Number);
	}

	public async void SelectTrain(string train)
	{
		number = train;
		if (ready)
		{
			await OpenTimes();
		}
	}

	public void SubmitHours(SepeWriteRequest request)
	{
		automaticWrite = request;
		number = request.Number;
		pendingArrival = (request.ArrivalChanged ? request.Arrival : ((DateTime?)null));
		pendingDeparture = (request.DepartureChanged ? request.Departure : ((DateTime?)null));
		preparePending = true;
		Text = "Zapis godzin w SEPE - " + request.Number;
	}

	private void CompleteWrite(bool ok, string text)
	{
		if (!completed)
		{
			completed = true;
			state.Text = text;
			WriteCompleted?.Invoke(ok, text);
		}
	}

	private async Task OpenTimes()
	{
		if (opening || string.IsNullOrWhiteSpace(number) || !ready || IsDisposed)
		{
			return;
		}
		opening = true;
		try
		{
			for (int attempt = 0; attempt < 60; attempt++)
			{
				if (IsDisposed)
				{
					break;
				}
				if (await browser.ExecuteScriptAsync(SelectionScript(number)) == "\"opened\"")
				{
					if (preparePending)
					{
						if (automaticWrite != null && ((automaticWrite.ArrivalChanged && !automaticWrite.Arrival.HasValue) || (automaticWrite.DepartureChanged && !automaticWrite.Departure.HasValue)))
						{
							CompleteWrite(ok: false, "Usunięcie godziny wymaga zmiany pola bezpośrednio w oficjalnym SEPE.");
							return;
						}
						bool stable = false;
						for (int retry = 0; retry < 4; retry++)
						{
							if (IsDisposed)
							{
								break;
							}
							if (await browser.ExecuteScriptAsync(SepeTimeTransfer.Script(pendingArrival, pendingDeparture, session.Now)) != "\"prepared\"")
							{
								await Task.Delay(400);
								continue;
							}
							await Task.Delay(500);
							if (await browser.ExecuteScriptAsync(SepeTimeTransfer.Verify(pendingArrival, pendingDeparture, session.Now)) == "\"verified\"")
							{
								await Task.Delay(300);
								if (await browser.ExecuteScriptAsync(SepeTimeTransfer.Verify(pendingArrival, pendingDeparture, session.Now)) == "\"verified\"")
								{
									stable = true;
									break;
								}
							}
						}
						preparePending = false;
						if (!stable)
						{
							string text = "Nie uzupełniono godzin: brak zgodnej daty, uprawnień albo formularz nie zachował wprowadzonych wartości.";
							state.Text = text;
							if (automaticWrite != null)
							{
								CompleteWrite(ok: false, text);
							}
							return;
						}
						if (automaticWrite == null)
						{
							state.Text = "Godziny uzupełnione. Sprawdź pociąg i daty, następnie wybierz Zapisz.";
							return;
						}
						state.Text = "Zapisywanie godzin w oficjalnym SEPE…";
						if (await browser.ExecuteScriptAsync(SepeTimeTransfer.Submit(number, pendingArrival, pendingDeparture, session.Now)) != "\"submitted\"")
						{
							CompleteWrite(ok: false, "SEPE nie udostępniło poprawnego formularza zapisu godzin.");
							return;
						}
						for (int n = 0; n < 24; n++)
						{
							if (IsDisposed)
							{
								break;
							}
							await Task.Delay(500);
							if (await browser.ExecuteScriptAsync(SepeTimeTransfer.State()) == "\"closed\"")
							{
								CompleteWrite(ok: true, "Formularz SEPE zamknięty. Sprawdzanie potwierdzenia w API…");
								return;
							}
						}
						CompleteWrite(ok: false, "Formularz SEPE pozostał otwarty. Sprawdź komunikat walidacji i uprawnienia na stronie.");
					}
					else
					{
						state.Text = "Pociąg " + number + " - edytuj godziny i wybierz Zapisz w SEPE.";
					}
					return;
				}
				await Task.Delay(500);
			}
			if (!IsDisposed)
			{
				state.Text = "Nie udało się wybrać pociągu i otworzyć formularza godzin. Wybierz go bezpośrednio na stronie.";
				if (automaticWrite != null)
				{
					CompleteWrite(ok: false, state.Text);
				}
			}
		}
		catch
		{
			if (!IsDisposed)
			{
				state.Text = "Nie udało się zapisać godzin w SEPE. Lokalny wpis pozostaje zachowany.";
				if (automaticWrite != null)
				{
					CompleteWrite(ok: false, state.Text);
				}
			}
		}
		finally
		{
			opening = false;
		}
	}

	public static string SelectionScript(string train)
	{
		return "(function(){var n=" + Json.Text(train) + ";var modal=document.getElementById('enterTimeModal');if(modal&&(modal.classList.contains('show')||modal.classList.contains('in'))){var title=document.getElementById('enterTimeModal-body-title');var parts=title?title.textContent.split(':'):[];var displayed=parts.length>1?parts[1].split('/')[0].trim():'';return displayed===n?'opened':'different';}var row=Array.from(document.querySelectorAll('tr')).find(function(r){var c=r.querySelectorAll('td');return c.length>=20&&(c[6].textContent.trim()===n||c[8].textContent.trim()===n);});if(!row)return 'waiting';if(!row.classList.contains('selectedRow')){row.click();return 'selected';}var button=document.querySelector('[data-target=\"#enterTimeModal\"]');if(!button||button.disabled)return 'selected';button.click();return 'selected';})()";
	}
}
