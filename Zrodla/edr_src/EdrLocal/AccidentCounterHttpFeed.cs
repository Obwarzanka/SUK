using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public sealed class AccidentCounterHttpFeed : IAccidentCounterFeed
{
	public const string ApiBase = "https://edr-licznik.jever.pl/api/v1/counters/";

	public static Uri Address(string simulator)
	{
		AccidentCounterClient.CheckSimulator(simulator);
		return new Uri("https://edr-licznik.jever.pl/api/v1/counters/" + simulator);
	}

	public Task<AccidentCounterReading> FetchAsync(string simulator, CancellationToken token)
	{
		Uri address = Address(simulator);
		return Task.Run(() =>
		{
			token.ThrowIfCancellationRequested();
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(address);
			request.Method = "GET";
			if (simulator != "td2")
			{
				request.UserAgent = "EDRLocal/1.0.10.199";
			}
			request.Accept = "application/json";
			request.AllowAutoRedirect = false;
			request.Timeout = 8000;
			request.ReadWriteTimeout = 8000;
			request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
			using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
			cancellationTokenSource.CancelAfter(8000);
			using (cancellationTokenSource.Token.Register(() =>
			{
				request.Abort();
			}))
			{
				using HttpWebResponse httpWebResponse = (HttpWebResponse)request.GetResponse();
				if (httpWebResponse.StatusCode != HttpStatusCode.OK)
				{
					throw new InvalidDataException("API licznika nie zwróciło poprawnej odpowiedzi.");
				}
				if (httpWebResponse.ContentLength > 65536)
				{
					throw new InvalidDataException("Zbyt duża odpowiedź licznika.");
				}
				using Stream stream = httpWebResponse.GetResponseStream();
				using MemoryStream memoryStream = new MemoryStream();
				byte[] array = new byte[4096];
				int count;
				while ((count = stream.Read(array, 0, array.Length)) > 0)
				{
					token.ThrowIfCancellationRequested();
					memoryStream.Write(array, 0, count);
					if (memoryStream.Length > 65536)
					{
						throw new InvalidDataException("Zbyt duża odpowiedź licznika.");
					}
				}
				return AccidentCounterClient.Parse(memoryStream.ToArray());
			}
		}, token);
	}
}
