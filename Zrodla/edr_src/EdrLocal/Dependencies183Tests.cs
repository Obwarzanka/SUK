using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace EdrLocal;

public static class Dependencies183Tests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static Exception Error(Action action)
	{
		try
		{
			action();
		}
		catch (Exception result)
		{
			return result;
		}
		throw new Exception("Brak oczekiwanego błędu.");
	}

	private static void Policies(string folder)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		Check(Array.Exists(assemblies, (Assembly x) => x.GetName().Name == "Microsoft.Web.WebView2.Core" && x.Location == "") && Array.Exists(assemblies, (Assembly x) => x.GetName().Name == "Microsoft.Web.WebView2.WinForms" && x.Location == ""), "Obie biblioteki zarządzane WebView2 są dostępne z zasobów EXE.");
		if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Microsoft.Web.WebView2.WinForms.dll")))
		{
			Check(typeof(WebView2).Assembly.Location == "" && typeof(CoreWebView2Environment).Assembly.Location == "", "Bez zewnętrznych DLL kontrolka korzysta wyłącznie z bibliotek w pamięci.");
		}
		Check(typeof(WebView2).Assembly.GetName().Version == new Version(1, 0, 4258, 31), "Wersja biblioteki WinForms odpowiada kompilacji 1.0.4258.31.");
		Assembly assembly = typeof(WebView2).Assembly;
		BundledWebView2.Initialize();
		Check(object.ReferenceEquals(assembly, typeof(WebView2).Assembly), "Ponowne przygotowanie nie ładuje drugiej kopii SDK.");
		Check(BundledWebView2.License.Contains("Copyright (C) Microsoft Corporation") && BundledWebView2.License.Contains("Redistribution"), "Licencja składników jest dostępna także bez plików obok EXE.");
		string[] array = new string[2] { "x64", "x86" };
		foreach (string text in array)
		{
			byte[] array2 = BundledWebView2.Resource(text + ".WebView2Loader.dll");
			string path = BundledWebView2.ExtractLoader(Path.Combine(folder, "loaders"), text);
			string path2 = Path.Combine(path, "WebView2Loader.dll");
			Check(Convert.ToBase64String(File.ReadAllBytes(path2)) == Convert.ToBase64String(array2), "Loader " + text + " ma bajty zgodne z dołączonym zasobem.");
			File.WriteAllText(path2, "Uszkodzony plik testowy");
			BundledWebView2.ExtractLoader(Path.Combine(folder, "loaders"), text);
			Check(Convert.ToBase64String(File.ReadAllBytes(path2)) == Convert.ToBase64String(array2), "Uszkodzony loader " + text + " jest odtwarzany atomowo.");
			int num2 = BitConverter.ToInt32(array2, 60);
			Check(BitConverter.ToUInt16(array2, num2 + 4) == ((text == "x64") ? 34404 : 332), "Dołączony loader ma właściwy format PE: " + text + ".");
		}
		int calls = 0;
		Exception ex = Error(() =>
		{
			SepeRetry.Run((int n) =>
			{
				calls++;
				throw new FileNotFoundException("Brak biblioteki", "Microsoft.Web.WebView2.WinForms, Version=1.0.4258.31");
			}, () => true, null, CancellationToken.None, 0).GetAwaiter().GetResult();
		});
		Check(calls == 1 && ex is SepeEnvironmentException && !ex.Message.Contains("Próba 3"), "Brak biblioteki zwraca jeden czytelny błąd środowiska zamiast trzech prób.");
		Check(SepeEnvironmentException.From(new TargetInvocationException(new FileLoadException("Nie można załadować", "Microsoft.Web.WebView2.Core"))) != null, "Rozpoznano także błąd ładowania wewnątrz wyjątku wywołania.");
		Check(SepeEnvironmentException.From(new IOException("Przejściowa awaria połączenia")) == null, "Zwykła awaria połączenia zachowuje reguły ponawiania.");
		string availableBrowserVersionString = CoreWebView2Environment.GetAvailableBrowserVersionString((string)null, (CoreWebView2EnvironmentOptions)null);
		Check(!string.IsNullOrWhiteSpace(availableBrowserVersionString), "Dołączony loader odczytuje zainstalowane środowisko WebView2: " + availableBrowserVersionString);
	}

	public static bool Run(string folder, bool render = false)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		bool success = false;
		Form host = new Form
		{
			ShowInTaskbar = false,
			Opacity = 0.0,
			Width = 300,
			Height = 200
		};
		try
		{
			host.Shown += async (object s, EventArgs e) =>
			{
				try
				{
					SepeWebView.ConfigureLoader();
					Policies(folder);
					WebView2 val = new WebView2();
					((Control)(object)val).Dock = DockStyle.Fill;
					WebView2 browser = val;
					try
					{
						host.Controls.Add((Control)(object)browser);
						CoreWebView2CreationProperties val2 = new CoreWebView2CreationProperties();
						val2.IsInPrivateModeEnabled = true;
						val2.UserDataFolder = Path.Combine(folder, "local-browser");
						browser.CreationProperties = val2;
						Task initialize = browser.EnsureCoreWebView2Async((CoreWebView2Environment)null, (CoreWebView2ControllerOptions)null);
						if (await Task.WhenAny(initialize, Task.Delay(20000)) != initialize)
						{
							throw new Exception("Timeout uruchomienia lokalnego WebView2.");
						}
						await initialize;
						Check(browser.CoreWebView2 != null, "Utworzono rzeczywistą kontrolkę WebView2 ze składników w EXE.");
						if (render)
						{
							string page = Path.GetFullPath(Path.Combine(folder, "local-page.html"));
							File.WriteAllText(page, "<!doctype html><html><body><p id='check'>LOCAL_ONLY_183</p></body></html>");
							string pageUrl = new Uri(page).AbsoluteUri;
							browser.CoreWebView2.NavigationStarting += (object a, CoreWebView2NavigationStartingEventArgs b) =>
							{
								if (!string.Equals(b.Uri, pageUrl, StringComparison.OrdinalIgnoreCase) && b.Uri != "about:blank")
								{
									b.Cancel = true;
								}
							};
							browser.NavigationCompleted += (object a, CoreWebView2NavigationCompletedEventArgs b) =>
							{
								//IL_002d: Unknown result type (might be due to invalid IL or missing references)
								log.Add("NAV RESULT: " + b.IsSuccess + " " + b.WebErrorStatus);
							};
							TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
							browser.NavigationCompleted += (object a, CoreWebView2NavigationCompletedEventArgs b) =>
							{
								ready.TrySetResult(b.IsSuccess);
							};
							browser.CoreWebView2.ProcessFailed += (object a, CoreWebView2ProcessFailedEventArgs b) =>
							{
								//IL_0017: Unknown result type (might be due to invalid IL or missing references)
								//IL_002d: Unknown result type (might be due to invalid IL or missing references)
								log.Add(string.Concat("PROCESS: ", b.ProcessFailedKind, " ", b.Reason));
							};
							browser.CoreWebView2.Navigate(pageUrl);
							if (await Task.WhenAny(ready.Task, Task.Delay(10000)) != ready.Task || !(await ready.Task))
							{
								log.Add("PAGE: " + await browser.ExecuteScriptAsync("JSON.stringify({url:location.href,text:document.body&&document.body.innerText.slice(0,500)})"));
								throw new Exception("Nie wczytano lokalnej strony testowej.");
							}
							Check(await browser.ExecuteScriptAsync("document.getElementById('check').textContent") == "\"LOCAL_ONLY_183\"", "Lokalna strona i JavaScript działają bez połączenia z SEPE.");
						}
					}
					finally
					{
						((IDisposable)browser)?.Dispose();
					}
					log.Add("INFO: Proces " + (Environment.Is64BitProcess ? "x64" : "x86") + "; folder EXE: " + AppDomain.CurrentDomain.BaseDirectory);
					log.Add("ALL OFFLINE DEPENDENCIES183 TESTS PASSED");
					success = true;
				}
				catch (Exception ex)
				{
					log.Add(ex.ToString());
				}
				finally
				{
					File.WriteAllLines(Path.Combine(folder, "wyniki-zaleznosci183.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
					host.Close();
				}
			};
			Application.Run(host);
		}
		finally
		{
			if (host != null)
			{
				((IDisposable)host).Dispose();
			}
		}
		return success;
	}
}
