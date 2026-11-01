using System;
using System.Text;
using System.Threading.Tasks;

namespace EdrLocal;

public static class SepeOperationCompletion
{
	public static async Task Wait(StopForecastRequest request, Func<string, Task<string>> script, Func<Task> pause, Func<string, Task<bool>> confirmForecast)
	{
		bool answered = false;
		for (int i = 0; i < 45; i++)
		{
			await pause();
			string text = Json.Read<string>(Encoding.UTF8.GetBytes(await script(request.IsStopChange ? SepeTimeTransfer.StopProgress(request.Identity, request.Stop) : SepeTimeTransfer.OperationProgress(request.Identity))));
			string progress = text;
			if (progress == "closed")
			{
				return;
			}
			if (progress != null && progress.StartsWith("forecast-warning: "))
			{
				if (!answered)
				{
					string message = progress.Substring("forecast-warning: ".Length);
					if (!StopForecastRequest.IsForecastQuestion(message))
					{
						throw new InvalidOperationException("SEPE: " + SepeDiagnostics.Clean(message));
					}
					int num = ((request.Operation == StopForecastOperation.Forecast || (confirmForecast != null && await confirmForecast(message))) ? 1 : 0);
					bool accept = (byte)num != 0;
					string text2 = Json.Read<string>(Encoding.UTF8.GetBytes(await script(SepeTimeTransfer.ForecastReply(request.Identity, message, accept))));
					string reply = text2;
					if (reply != (accept ? "accepted" : "declined"))
					{
						throw new InvalidOperationException("Zmieniono pytanie SEPE lub wybrany pociąg. Zapis wstrzymany.");
					}
					answered = true;
					if (!accept)
					{
						throw new ForecastDeclinedException();
					}
				}
			}
			else
			{
				switch (progress)
				{
				case "different":
					throw new InvalidOperationException("SEPE zmieniło zaznaczenie pociągu lub dane otwartego formularza. Nie ponowiono kliknięcia zapisu.");
				default:
					throw new InvalidOperationException("SEPE nie zakończyło zapisu " + request.Description + ": " + SepeDiagnostics.Clean(progress));
				case "waiting":
				case "saved":
					break;
				}
			}
		}
		throw new InvalidOperationException("SEPE pozostawiło formularz otwarty po operacji " + request.Description + ". Nie ponowiono kliknięcia zapisu.");
	}
}
