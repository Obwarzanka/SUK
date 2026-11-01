using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public sealed class SimRailProvider : ISimulatorFeed
{
	public const string OpenBase = "https://api.simrail.eu:8084/";

	public const string TimetableBase = "https://api.simrail.eu:8082/api/";

	private readonly VehicleObservations vehicleObservations = new VehicleObservations();

	public string ProviderId => "SimRail";

	public static T Parse<T>(byte[] bytes)
	{
		using MemoryStream stream = new MemoryStream(bytes);
		return (T)new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
		{
			MaxItemsInObjectGraph = 4000000
		}).ReadObject((Stream)stream);
	}

	public static byte[] Get(string url, CancellationToken token)
	{
		return Get(url, token, 30000);
	}

	public static byte[] Get(string url, CancellationToken token, int timeout)
	{
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
		request.Method = "GET";
		request.UserAgent = "EDRLocal/1.0.10.199";
		request.Timeout = timeout;
		request.ReadWriteTimeout = timeout;
		request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
		Action callback = () =>
		{
			request.Abort();
		};
		using (token.Register(callback))
		{
			using HttpWebResponse httpWebResponse = (HttpWebResponse)request.GetResponse();
			using Stream stream = httpWebResponse.GetResponseStream();
			using MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[32768];
			int count;
			while ((count = stream.Read(array, 0, array.Length)) > 0)
			{
				token.ThrowIfCancellationRequested();
				memoryStream.Write(array, 0, count);
				if (memoryStream.Length > 67108864)
				{
					throw new InvalidDataException("Odpowiedź SimRail jest zbyt duża.");
				}
			}
			return memoryStream.ToArray();
		}
	}

	public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
	{
		return Task.Run(() =>
		{
			SimRailEnvelope<SimRailServer> simRailEnvelope = Parse<SimRailEnvelope<SimRailServer>>(Get("https://api.simrail.eu:8084/servers-open", token));
			if (!simRailEnvelope.result || simRailEnvelope.data == null)
			{
				throw new InvalidDataException("API nie zwróciło listy serwerów.");
			}
			return (from x in simRailEnvelope.data
				where x.IsActive
				orderby x.ServerCode
				select x).ToList();
		}, token);
	}

	public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
	{
		return Task.Run(() =>
		{
			if (string.IsNullOrWhiteSpace(server) || server.Any((char x) => !char.IsLetterOrDigit(x) && x != '-' && x != '_'))
			{
				throw new ArgumentException("Nieprawidłowy serwer.");
			}
			string query = "?serverCode=" + Uri.EscapeDataString(server);
			List<SimRailTrain> list = Parse<List<SimRailTrain>>(Get("https://api.simrail.eu:8082/api/getEDRTimetables" + query, token));
			if (list == null || list.Count == 0 || list.Any((SimRailTrain x) => x.timetable == null))
			{
				throw new InvalidDataException("API nie zwróciło kompletnego rozkładu.");
			}
			ConsistPlan.TryLoad(list, token, () => Get("https://api.simrail.eu:8082/api/getAllTimetables" + query, token, 8000));
			SimRailEnvelope<SimRailDispatcherStation> simRailEnvelope = Parse<SimRailEnvelope<SimRailDispatcherStation>>(Get("https://api.simrail.eu:8084/stations-open" + query, token));
			if (!simRailEnvelope.result || simRailEnvelope.data == null)
			{
				throw new InvalidDataException("API nie zwróciło posterunków serwera.");
			}
			LiveVehicles.TryLoad(server, list, token, () => Get("https://api.simrail.eu:8084/trains-open" + query, token, 8000));
			vehicleObservations.Apply(server, list);
			string s = Encoding.UTF8.GetString(Get("https://api.simrail.eu:8082/api/getTime" + query, token)).Trim();
			if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				throw new InvalidDataException("Nieprawidłowy czas SimRail.");
			}
			int.TryParse(Encoding.UTF8.GetString(Get("https://api.simrail.eu:8082/api/getTimeZone" + query, token)), out var result2);
			return new SimRailSnapshot
			{
				ServerCode = server,
				Trains = list,
				DispatcherNames = new HashSet<string>((simRailEnvelope.data ?? new List<SimRailDispatcherStation>()).Select((SimRailDispatcherStation x) => x.Name), StringComparer.CurrentCultureIgnoreCase),
				GameTime = new DateTime(1970, 1, 1).AddMilliseconds(result),
				UtcOffset = result2,
				DownloadedUtc = DateTime.UtcNow
			};
		}, token);
	}

	public static DateTime? Time(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		if (!DateTime.TryParseExact(text, new string[4] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			throw new InvalidDataException("Nieprawidłowa data rozkładu SimRail.");
		}
		return result;
	}

	public static string Value(object value)
	{
		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
	}
}
