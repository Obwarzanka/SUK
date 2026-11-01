using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace EdrLocal;

public static class SepeWebView
{
	private static bool configured;

	private static readonly string temporaryProfile = Path.Combine(Path.GetTempPath(), "EDR-WebView2-" + Guid.NewGuid().ToString("N"));

	public static string UserDataFolder
	{
		get
		{
			if (!LocalPersistence.Enabled)
			{
				return temporaryProfile;
			}
			return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dane", "browser");
		}
	}

	private static void CleanupTemporaryProfile()
	{
		string value = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(temporaryProfile);
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fullPath).StartsWith("EDR-WebView2-", StringComparison.Ordinal))
		{
			return;
		}
		try
		{
			if (Directory.Exists(fullPath))
			{
				Directory.Delete(fullPath, recursive: true);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	public static void ConfigureLoader()
	{
		if (!configured)
		{
			Application.ApplicationExit += (object s, EventArgs e) =>
			{
				CleanupTemporaryProfile();
			};
			CoreWebView2Environment.SetLoaderDllFolderPath(BundledWebView2.LoaderFolder());
			configured = true;
		}
	}
}
