using System;
using System.IO;

namespace EdrLocal;

public sealed class SepeEnvironmentException : InvalidOperationException
{
	private SepeEnvironmentException(string message, Exception inner)
		: base(message, inner)
	{
	}

	public static SepeEnvironmentException From(Exception error)
	{
		for (Exception ex = error; ex != null; ex = ex.InnerException)
		{
			if (ex is SepeEnvironmentException)
			{
				return (SepeEnvironmentException)ex;
			}
			if (ex.GetType().FullName == "Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException")
			{
				return new SepeEnvironmentException("Na tym komputerze nie znaleziono Microsoft Edge WebView2 Runtime. Zainstaluj WebView2 Runtime i uruchom EDR ponownie. Godziny pozostają lokalnie.", error);
			}
			FileNotFoundException ex2 = ex as FileNotFoundException;
			FileLoadException ex3 = ex as FileLoadException;
			string text = ((ex2 != null) ? ex2.FileName : ex3?.FileName);
			if ((ex2 != null || ex3 != null) && ((text ?? "") + ex.Message).IndexOf("WebView2", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return new SepeEnvironmentException("Nie udało się załadować składnika WebView2. Użyj kompletnego EDR w wersji 1.0.10.199 lub nowszej. Składniki są dołączone do EXE; jeśli błąd pozostaje, pobierz program ponownie. Godziny pozostają lokalnie.", error);
			}
			if (ex is DllNotFoundException || ex is BadImageFormatException)
			{
				return new SepeEnvironmentException("Nie udało się uruchomić składnika WebView2 dla tego systemu. Pobierz EDR ponownie i sprawdź instalację Microsoft Edge WebView2 Runtime. Godziny pozostają lokalnie.", error);
			}
		}
		return null;
	}
}
