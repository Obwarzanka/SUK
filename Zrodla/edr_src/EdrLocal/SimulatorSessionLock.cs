using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EdrLocal;

public sealed class SimulatorSessionLock : IDisposable
{
	private readonly string server;

	private readonly string prefix;

	private readonly Dictionary<string, Mutex> locks = new Dictionary<string, Mutex>();

	private bool disposed;

	public SimulatorSessionLock(string serverCode, IEnumerable<string> points)
		: this(serverCode, points, "simrail")
	{
	}

	public SimulatorSessionLock(string serverCode, IEnumerable<string> points, string provider)
	{
		server = serverCode;
		prefix = ((provider == "td2") ? "TD2" : "SimRail");
		Include(points);
	}

	public void Include(IEnumerable<string> points)
	{
		if (disposed)
		{
			throw new ObjectDisposedException("SimulatorSessionLock");
		}
		List<string> list = new List<string>();
		try
		{
			foreach (string item in points.Distinct().OrderBy((string x) => x, StringComparer.Ordinal))
			{
				if (!locks.ContainsKey(item))
				{
					Mutex mutex = new Mutex(initiallyOwned: true, "Local\\EDRLocal_" + prefix + "_" + server + "_" + item, out var createdNew);
					if (!createdNew)
					{
						mutex.Dispose();
						throw new InvalidOperationException("Jeden z wybranych posterunków jest już otwarty na tym serwerze w EDR.");
					}
					locks.Add(item, mutex);
					list.Add(item);
				}
			}
		}
		catch
		{
			foreach (string item2 in list)
			{
				locks[item2].ReleaseMutex();
				locks[item2].Dispose();
				locks.Remove(item2);
			}
			throw;
		}
	}

	public void ReleaseMutex()
	{
		foreach (Mutex value in locks.Values)
		{
			value.ReleaseMutex();
			value.Dispose();
		}
		locks.Clear();
	}

	public void Dispose()
	{
		if (!disposed)
		{
			ReleaseMutex();
			disposed = true;
		}
	}
}
