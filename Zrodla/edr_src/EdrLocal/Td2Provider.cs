using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public sealed class Td2Provider : ISimulatorFeed
{
	public const string ApiBase = "https://api.td2.info.pl/";

	private readonly Func<string, CancellationToken, byte[]> download;

	private readonly object gate = new object();

	private readonly Dictionary<string, Dictionary<string, Td2Record>> sessions = new Dictionary<string, Dictionary<string, Td2Record>>();

	public string ProviderId => "TD2";

	public Td2Provider()
		: this(Get)
	{
	}

	internal Td2Provider(Func<string, CancellationToken, byte[]> transport)
	{
		download = transport;
	}

	public static string Region(string value)
	{
		value = (value ?? "").Trim().ToLowerInvariant();
		switch (value)
		{
		case "pl1":
			value = "eu";
			break;
		case "pl2":
			value = "cae";
			break;
		case "de":
			value = "usw";
			break;
		case "cz":
			value = "us";
			break;
		case "eng":
			value = "ru";
			break;
		}
		if (!Regex.IsMatch(value, "^[a-z0-9_-]{1,24}$"))
		{
			throw new ArgumentException("Wpisz region TD2, np. eu.");
		}
		return value;
	}

	public static string ServerName(string code)
	{
		return Region(code) switch
		{
			"eu" => "PL1", 
			"cae" => "PL2", 
			"usw" => "DE", 
			"us" => "CZ", 
			"ru" => "ENG", 
			_ => Region(code).ToUpperInvariant(), 
		};
	}

	public static string Address(string region)
	{
		return "https://api.td2.info.pl/?method=readFromSWDR&value=getTimetables;" + Uri.EscapeDataString(Region(region));
	}

	internal static HttpWebRequest CreateRequest(string address)
	{
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(address);
		httpWebRequest.Method = "GET";
		httpWebRequest.Timeout = 25000;
		httpWebRequest.ReadWriteTimeout = 25000;
		httpWebRequest.AllowAutoRedirect = false;
		httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
		return httpWebRequest;
	}

	private static byte[] Get(string address, CancellationToken token)
	{
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		HttpWebRequest request = CreateRequest(address);
		if (address == "https://api.td2.info.pl/?method=getTrainsOnline")
		{
			request.Timeout = 8000;
			request.ReadWriteTimeout = 8000;
		}
		using (token.Register(() =>
		{
			request.Abort();
		}))
		{
			using HttpWebResponse httpWebResponse = (HttpWebResponse)request.GetResponse();
			using Stream stream = httpWebResponse.GetResponseStream();
			using MemoryStream memoryStream = new MemoryStream();
			if (httpWebResponse.StatusCode != HttpStatusCode.OK)
			{
				throw new InvalidDataException("API TD2 nie zwróciło pełnego rozkładu.");
			}
			byte[] array = new byte[32768];
			int count;
			while ((count = stream.Read(array, 0, array.Length)) > 0)
			{
				token.ThrowIfCancellationRequested();
				memoryStream.Write(array, 0, count);
				if (memoryStream.Length > 67108864)
				{
					throw new InvalidDataException("Rozkład TD2 jest zbyt duży.");
				}
			}
			return memoryStream.ToArray();
		}
	}

	public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		return Task.FromResult(new string[5] { "eu", "cae", "usw", "us", "ru" }.Select((string code) => new SimRailServer
		{
			ServerCode = code,
			ServerName = ServerName(code),
			IsActive = true
		}).ToList());
	}

	public async Task<SimRailSnapshot> SnapshotAsync(string region, CancellationToken token)
	{
		region = Region(region);
		Task<byte[]> timetable = Task.Run(() => download(Address(region), token), token);
		Task<List<Td2OnlineTrain>> online = Task.Run(() => Td2Online.Read(download, token), token);
		await Task.WhenAll(timetable, online).ConfigureAwait(continueOnCapturedContext: false);
		token.ThrowIfCancellationRequested();
		return Accept(timetable.Result, region, DateTimeOffset.UtcNow, online.Result);
	}

	internal SimRailSnapshot Accept(byte[] bytes, string region, DateTimeOffset downloaded, List<Td2OnlineTrain> online = null)
	{
		region = Region(region);
		if (bytes == null || bytes.Length == 0 || bytes.Length > 67108864)
		{
			throw new InvalidDataException("Brak pełnej odpowiedzi TD2.");
		}
		Td2Envelope td2Envelope = SimRailProvider.Parse<Td2Envelope>(bytes);
		if (td2Envelope == null || !td2Envelope.success || td2Envelope.message == null)
		{
			throw new InvalidDataException("API TD2 nie potwierdziło pobrania rozkładu.");
		}
		Dictionary<string, Td2Record> dictionary = new Dictionary<string, Td2Record>();
		foreach (Td2Schedule item in td2Envelope.message)
		{
			Validate(item);
			if (string.Equals(item.trainInfo.region, region, StringComparison.OrdinalIgnoreCase))
			{
				Td2Record td2Record = new Td2Record();
				td2Record.Schedule = item;
				Td2Record td2Record2 = td2Record;
				if (dictionary.ContainsKey(td2Record2.Id))
				{
					throw new InvalidDataException("API TD2 powtórzyło identyfikator rozkładu.");
				}
				dictionary.Add(td2Record2.Id, td2Record2);
			}
		}
		if (td2Envelope.message.Count <= 0 || dictionary.Count != 0)
		{
			lock (gate)
			{
				if (!sessions.TryGetValue(region, out var value))
				{
					value = new Dictionary<string, Td2Record>();
				}
				Dictionary<string, Td2Record> dictionary2 = new Dictionary<string, Td2Record>();
				foreach (KeyValuePair<string, Td2Record> item2 in value)
				{
					Dictionary<string, Td2Record> dictionary3 = dictionary2;
					string key = item2.Key;
					Td2Record value2;
					if (item2.Value.Removed)
					{
						value2 = new Td2Record
						{
							Schedule = item2.Value.Schedule,
							Removed = true,
							RuntimeConsist = item2.Value.RuntimeConsist
						};
					}
					else
					{
						value2 = (dictionary.ContainsKey(item2.Key) ? new Td2Record
						{
							Schedule = dictionary[item2.Key].Schedule,
							Removed = item2.Value.Removed,
							RuntimeConsist = item2.Value.RuntimeConsist
						} : new Td2Record
						{
							Schedule = item2.Value.Schedule,
							Removed = true,
							RuntimeConsist = item2.Value.RuntimeConsist
						});
					}
					dictionary3[key] = value2;
				}
				foreach (KeyValuePair<string, Td2Record> item3 in dictionary)
				{
					if (!dictionary2.ContainsKey(item3.Key))
					{
						dictionary2.Add(item3.Key, item3.Value);
					}
				}
				List<Td2Record> records = dictionary2.Values.ToList();
				Td2Online.Attach(records, region, online);
				Td2SnapshotData td2SnapshotData = new Td2SnapshotData();
				td2SnapshotData.Region = region;
				td2SnapshotData.Records = records;
				Td2SnapshotData td2SnapshotData2 = td2SnapshotData;
				SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
				simRailSnapshot.Td2 = td2SnapshotData2;
				simRailSnapshot.ServerCode = region;
				simRailSnapshot.Trains = new List<SimRailTrain>();
				simRailSnapshot.DispatcherNames = new HashSet<string>(from x in td2SnapshotData2.Locations()
					select x.Name);
				simRailSnapshot.GameTime = PolandTime(downloaded);
				simRailSnapshot.DownloadedUtc = downloaded.UtcDateTime;
				SimRailSnapshot result = simRailSnapshot;
				sessions[region] = dictionary2;
				return result;
			}
		}
		throw new InvalidDataException("API TD2 zwróciło rozkład innego regionu.");
	}

	private static void Validate(Td2Schedule schedule)
	{
		if (schedule == null || schedule.trainInfo == null || !Regex.IsMatch(Text(schedule.trainInfo.timetableId), "^\\d+$") || !Regex.IsMatch(Text(schedule.trainInfo.trainNo), "^\\d+$") || string.IsNullOrWhiteSpace(schedule.trainInfo.region) || schedule.stopPoints == null || schedule.stopPoints.Count == 0)
		{
			throw new InvalidDataException("Niekompletny rozkład TD2.");
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (Td2Stop stopPoint in schedule.stopPoints)
		{
			if (stopPoint == null || string.IsNullOrWhiteSpace(stopPoint.pointNameRAW ?? stopPoint.pointName) || !Regex.IsMatch(stopPoint.pointId ?? "", "^\\d{1,20}$") || !Regex.IsMatch(Text(stopPoint.entryId), "^\\d+$") || !hashSet.Add(Text(stopPoint.entryId)))
			{
				throw new InvalidDataException("Niekompletny lub powtórzony punkt trasy TD2.");
			}
			DateTime? dateTime = Time(stopPoint.arrivalTime);
			DateTime? dateTime2 = Time(stopPoint.departureTime);
			Time(stopPoint.arrivalRealTime);
			Time(stopPoint.departureRealTime);
			if (!dateTime.HasValue && !dateTime2.HasValue)
			{
				throw new InvalidDataException("Punkt TD2 nie ma planowego czasu.");
			}
			if (dateTime.HasValue && dateTime2.HasValue && dateTime2 < dateTime)
			{
				throw new InvalidDataException("Odwrócone planowe czasy TD2.");
			}
		}
	}

	internal static string Text(object value)
	{
		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
	}

	internal static bool Flag(object value)
	{
		string text = Text(value);
		if (!(text == "1"))
		{
			return text.Equals("true", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static double? Number(object value)
	{
		if (!double.TryParse(Text(value).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || double.IsNaN(result) || double.IsInfinity(result))
		{
			return null;
		}
		return result;
	}

	internal static DateTime PolandTime(DateTimeOffset value)
	{
		return DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")).DateTime, DateTimeKind.Unspecified);
	}

	internal static DateTime? Time(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		if (!Regex.IsMatch(value, "(?:Z|[+\\-]\\d{2}:\\d{2})$") || !DateTimeOffset.TryParseExact(value, new string[2] { "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			throw new InvalidDataException("Nieprawidłowa data TD2.");
		}
		return PolandTime(result);
	}
}
