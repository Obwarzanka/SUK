using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace EdrLocal;

internal sealed class ConfigurationSessionLock : IDisposable
{
	private readonly Mutex mutex;

	private bool owned;

	public static string Name(ConfiguratorRepository repository)
	{
		if (!Ui.Testing)
		{
			return "Local\\SimKol_EDR_Configurator_simrail";
		}
		using SHA256 sHA = SHA256.Create();
		return "Local\\SimKol_EDR_ConfigTest_" + BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(repository.Root.ToUpperInvariant()))).Replace("-", "");
	}

	public ConfigurationSessionLock(ConfiguratorRepository repository)
	{
		mutex = new Mutex(initiallyOwned: false, Name(repository));
		try
		{
			owned = mutex.WaitOne(0);
		}
		catch (AbandonedMutexException)
		{
			owned = true;
		}
		if (!owned)
		{
			mutex.Dispose();
			throw new InvalidOperationException("Zamknij osobny konfigurator struktury przed zmianą przypisania IZ / ISE.");
		}
	}

	public void Dispose()
	{
		if (owned)
		{
			mutex.ReleaseMutex();
			owned = false;
		}
		mutex.Dispose();
	}
}
