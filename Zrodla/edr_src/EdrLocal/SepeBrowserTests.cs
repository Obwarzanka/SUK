using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace EdrLocal;

public static class SepeBrowserTests
{
	public static bool Run(string folder)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected Obj, but got Unknown
		Directory.CreateDirectory(folder);
		List<string> log = new List<string>();
		bool ok = false;
		ReferenceForm form = new ReferenceForm
		{
			Text = "Test SEPE",
			Width = 1100,
			Height = 700
		};
		try
		{
			WebView2 val = new WebView2();
			((Control)(object)val).Dock = DockStyle.Fill;
			WebView2 browser = val;
			try
			{
				form.Controls.Add((Control)(object)browser);
				form.Shown += async (object s, EventArgs e) =>
				{
					try
					{
						CoreWebView2Environment.SetLoaderDllFolderPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", Environment.Is64BitProcess ? "x64" : "x86"));
						WebView2 val2 = browser;
						CoreWebView2CreationProperties val3 = new CoreWebView2CreationProperties();
						val3.IsInPrivateModeEnabled = true;
						val3.UserDataFolder = Path.GetFullPath(Path.Combine(folder, "browser-profile"));
						val2.CreationProperties = val3;
						await browser.EnsureCoreWebView2Async((CoreWebView2Environment)null, (CoreWebView2ControllerOptions)null);
						log.Add("PASS: Uruchomienie WebView2 w trybie InPrivate.");
						browser.CoreWebView2.ProcessFailed += (object a, CoreWebView2ProcessFailedEventArgs b) =>
						{
							//IL_001d: Unknown result type (might be due to invalid IL or missing references)
							//IL_0033: Unknown result type (might be due to invalid IL or missing references)
							log.Add(string.Concat("PROCESS FAILED: ", b.ProcessFailedKind, " ", b.Reason));
						};
						TaskCompletionSource<bool> loaded = new TaskCompletionSource<bool>();
						browser.NavigationCompleted += (object a, CoreWebView2NavigationCompletedEventArgs b) =>
						{
							loaded.TrySetResult(b.IsSuccess);
						};
						string cells = "";
						for (int num = 0; num < 20; num++)
						{
							cells = cells + "<td>" + ((num == 6) ? "123" : "") + "</td>";
						}
						browser.NavigateToString("<html><body><table><tr onclick=\"window.picked=true;this.className='selectedRow'\">" + cells + "</tr></table><button data-target=\"#enterTimeModal\" onclick=\"if(window.picked){document.getElementById('enterTimeModal').className='show';}\">Wprowadzanie godzin</button><div id='enterTimeModal'><div id='enterTimeModal-body-title'>Pociąg: 123 / Relacja: Test</div><button onclick='window.saved=true'>Zapisz</button></div></body></html>");
						await loaded.Task;
						await Task.Delay(1000);
						log.Add("CORE: " + (browser.CoreWebView2 != null));
						string script = SepeBrowserForm.SelectionScript("123");
						await browser.ExecuteScriptAsync(script);
						await browser.ExecuteScriptAsync(script);
						if (await browser.ExecuteScriptAsync(script) != "\"opened\"")
						{
							throw new Exception("Nie otwarto poprawnego formularza.");
						}
						log.Add("PASS: Wybór numeru pociągu i otwarcie formularza godzin w DOM.");
						if (await browser.ExecuteScriptAsync("window.saved===true") != "false")
						{
							throw new Exception("Nieuprawnione automatyczne zatwierdzenie.");
						}
						log.Add("PASS: Godziny wymagają świadomego Zapisz na stronie SEPE.");
						Ui.Capture(form, Path.Combine(folder, "webview2.png"));
						ok = true;
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						form.Close();
					}
				};
				Application.Run(form);
			}
			finally
			{
				if (browser != null)
				{
					((IDisposable)browser).Dispose();
				}
			}
		}
		finally
		{
			if (form != null)
			{
				((IDisposable)form).Dispose();
			}
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-webview2.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		return ok;
	}
}
