using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public sealed class AccidentCounterClient
{
	public const long DefaultResetEpoch = 1792274400L;

	private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	private readonly IAccidentCounterFeed feed;

	private readonly string cachePath;

	private readonly Func<DateTime> utc;

	private readonly Func<TimeSpan> monotonic;

	private readonly object gate = new object();

	private int polling;

	private TimeSpan nextPoll;

	private TimeSpan anchorElapsed;

	private DateTime anchorUtc;

	private bool serverClock;

	private AccidentCounterReading reading;

	private string source = "Data domyślna";

	public string Simulator { get; private set; }

	public string LastError { get; private set; }

	public bool HasReading
	{
		get
		{
			lock (gate)
			{
				return reading != null;
			}
		}
	}

	public long Revision
	{
		get
		{
			lock (gate)
			{
				return (reading == null) ? (-1) : reading.Revision;
			}
		}
	}

	public string Source
	{
		get
		{
			lock (gate)
			{
				return source;
			}
		}
	}

	public DateTime ResetUtc
	{
		get
		{
			lock (gate)
			{
				return Epoch.AddSeconds((reading == null) ? 1792274400 : reading.LastResetEpochSeconds);
			}
		}
	}

	public DateTime CurrentUtc
	{
		get
		{
			lock (gate)
			{
				return serverClock ? SafeAdd(anchorUtc, monotonic() - anchorElapsed) : utc();
			}
		}
	}

	public TimeSpan Duration
	{
		get
		{
			lock (gate)
			{
				DateTime dateTime = (serverClock ? SafeAdd(anchorUtc, monotonic() - anchorElapsed) : utc());
				TimeSpan timeSpan = dateTime - Epoch.AddSeconds((reading == null) ? 1792274400 : reading.LastResetEpochSeconds);
				return (timeSpan < TimeSpan.Zero) ? TimeSpan.Zero : timeSpan;
			}
		}
	}

	public AccidentCounterClient(string simulator, IAccidentCounterFeed provider, string path)
		: this(simulator, provider, path, () => DateTime.UtcNow, Clock())
	{
	}

	public AccidentCounterClient(string simulator, IAccidentCounterFeed provider, string path, Func<DateTime> realUtc, Func<TimeSpan> elapsed)
	{
		CheckSimulator(simulator);
		Simulator = simulator;
		feed = provider;
		cachePath = path;
		utc = realUtc;
		monotonic = elapsed;
		LoadCache();
	}

	private static Func<TimeSpan> Clock()
	{
		Stopwatch clock = Stopwatch.StartNew();
		return () => clock.Elapsed;
	}

	private static DateTime SafeAdd(DateTime date, TimeSpan elapsed)
	{
		try
		{
			return date.Add(elapsed);
		}
		catch (ArgumentOutOfRangeException)
		{
			return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
		}
	}

	public static void CheckSimulator(string simulator)
	{
		if (simulator != "simrail" && simulator != "td2")
		{
			throw new ArgumentException("Nieznany symulator licznika.");
		}
	}

	public static AccidentCounterReading Parse(byte[] bytes)
	{
		using MemoryStream stream = new MemoryStream(bytes);
		return (AccidentCounterReading)new DataContractJsonSerializer(typeof(AccidentCounterReading)).ReadObject((Stream)stream);
	}

	public static void Validate(AccidentCounterReading value, string simulator)
	{
		if (value == null || value.SchemaVersion != 1 || value.Simulator != simulator || value.Revision < 0 || value.ServerEpochSeconds < 1 || value.ServerEpochSeconds > 253402300799L || value.LastResetEpochSeconds < 0 || value.LastResetEpochSeconds > 253402300799L || value.LastResetEpochSeconds > value.ServerEpochSeconds + 300)
		{
			throw new InvalidDataException("Nieprawidłowe dane licznika.");
		}
	}

	private void LoadCache()
	{
		if (!LocalPersistence.Enabled || string.IsNullOrEmpty(cachePath))
		{
			return;
		}
		try
		{
			if (File.Exists(cachePath) && new FileInfo(cachePath).Length <= 65536)
			{
				AccidentCounterReading value = Parse(File.ReadAllBytes(cachePath));
				Validate(value, Simulator);
				reading = value;
				source = "Zapamiętana data";
			}
		}
		catch
		{
			LastError = "Nie można odczytać zapisanej daty licznika.";
		}
	}

	private void SaveCache(AccidentCounterReading value)
	{
		if (!LocalPersistence.Enabled || string.IsNullOrEmpty(cachePath))
		{
			return;
		}
		string text = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cachePath)));
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				new DataContractJsonSerializer(typeof(AccidentCounterReading)).WriteObject((Stream)fileStream, (object)value);
				fileStream.Flush(flushToDisk: true);
			}
			if (File.Exists(cachePath))
			{
				File.Replace(text, cachePath, null);
			}
			else
			{
				File.Move(text, cachePath);
			}
		}
		catch
		{
			LastError = "Data licznika została pobrana, ale nie można zapisać jej na dysku.";
		}
		finally
		{
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
		}
	}

	public async Task<bool> PollIfDueAsync(CancellationToken token)
	{
		if (token.IsCancellationRequested || Interlocked.CompareExchange(ref polling, 1, 0) != 0)
		{
			return false;
		}
		try
		{
			int num = default;
			_ = num;
			_ = 0;
			try
			{
				if (monotonic() < nextPoll)
				{
					return false;
				}
				nextPoll = monotonic() + TimeSpan.FromSeconds(60.0);
				AccidentCounterReading value = await feed.FetchAsync(Simulator, token).ConfigureAwait(continueOnCapturedContext: false);
				token.ThrowIfCancellationRequested();
				Validate(value, Simulator);
				AccidentCounterReading accepted = new AccidentCounterReading
				{
					SchemaVersion = value.SchemaVersion,
					Simulator = value.Simulator,
					ServerEpochSeconds = value.ServerEpochSeconds,
					LastResetEpochSeconds = value.LastResetEpochSeconds,
					Revision = value.Revision
				};
				lock (gate)
				{
					reading = accepted;
					anchorUtc = Epoch.AddSeconds(accepted.ServerEpochSeconds);
					anchorElapsed = monotonic();
					serverClock = true;
					source = "API";
					LastError = null;
				}
				SaveCache(accepted);
				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			catch
			{
				if (!token.IsCancellationRequested)
				{
					LastError = "Brak połączenia z licznikiem. Zachowano ostatnią poprawną datę.";
				}
				return false;
			}
		}
		finally
		{
			Interlocked.Exchange(ref polling, 0);
		}
	}
}
