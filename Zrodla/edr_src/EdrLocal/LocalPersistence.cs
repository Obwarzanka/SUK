using System;

namespace EdrLocal;

public static class LocalPersistence
{
	public static bool Enabled { get; set; }

	public static void RequireEnabled()
	{
		if (!Enabled)
		{
			throw new InvalidOperationException("Zapis i wczytywanie danych EDR są tymczasowo wyłączone.");
		}
	}
}
