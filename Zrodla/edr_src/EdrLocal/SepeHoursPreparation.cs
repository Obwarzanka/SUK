using System;
using System.Text;
using System.Threading.Tasks;

namespace EdrLocal;

public static class SepeHoursPreparation
{
	private static bool Same(SepeWriteRequest a, SepeWriteRequest b)
	{
		if (a.TrainId == b.TrainId && a.ExternalNumber == b.ExternalNumber && a.PointIndex == b.PointIndex && a.Number == b.Number && a.PlanArrival == b.PlanArrival && a.PlanDeparture == b.PlanDeparture && a.Origin == b.Origin && a.Destination == b.Destination && a.ArrivalChanged == b.ArrivalChanged && a.DepartureChanged == b.DepartureChanged && a.AdjustDepartureForArrival == b.AdjustDepartureForArrival && a.DepartureCorrection == b.DepartureCorrection && (!a.ArrivalChanged || SepeWriteRequest.SameMinute(a.Arrival, b.Arrival)))
		{
			if (a.DepartureChanged)
			{
				return SepeWriteRequest.SameMinute(a.Departure, b.Departure);
			}
			return true;
		}
		return false;
	}

	public static async Task Submit(Func<SepeWriteRequest> latest, Func<string, Task<string>> script, Func<int, Task> pause, Func<DateTime> clock, Action<bool> submission)
	{
		int attempt = 0;
		int num = default;
		int[] array2 = default;
		int num2 = default;
		int field = default;
		while (attempt < 3)
		{
			SepeWriteRequest request = latest();
			if ((request.ArrivalChanged && !request.Arrival.HasValue) || (request.DepartureChanged && !request.Departure.HasValue))
			{
				throw new InvalidOperationException("Usunięcie godziny wymaga zmiany bezpośrednio w oficjalnym SEPE.");
			}
			if (request.AdjustDepartureForArrival && request.ArrivalChanged && request.Arrival.HasValue && (!request.DepartureChanged || request.DepartureCorrection))
			{
				if (await script(SepeTimeTransfer.Identity(request)) != "\"matched\"")
				{
					throw new InvalidOperationException("SEPE zmieniło wybrany pociąg. Zapis wstrzymany.");
				}
				string[] array = Json.Read<string[]>(Encoding.UTF8.GetBytes(await script(SepeTimeTransfer.ReadTimes(clock()))));
				string[] shown = array;
				if (shown == null || shown.Length != 2)
				{
					throw new InvalidOperationException("Nie odczytano godzin formularza SEPE.");
				}
				DateTime? departure = (string.IsNullOrWhiteSpace(shown[1]) ? ((DateTime?)null) : SimRailProvider.Time(shown[1]));
				if (request.DepartureCorrection || (departure.HasValue && !SepeWriteRequest.AtOrAfterMinute(departure, request.Arrival)))
				{
					request.Departure = (SepeWriteRequest.AtOrAfterMinute(departure, request.Arrival) ? departure : request.Arrival);
					request.DepartureChanged = true;
					request.DepartureCorrection = true;
				}
			}
			DateTime? a = (request.ArrivalChanged ? request.Arrival : ((DateTime?)null));
			DateTime? d = (request.DepartureChanged ? request.Departure : ((DateTime?)null));
			int[] fields = ((!request.DepartureCorrection) ? new int[6] { 0, 1, 2, 3, 4, 5 } : new int[6] { 3, 4, 5, 0, 1, 2 });
			string prepared;
			switch (num)
			{
			default:
				array2 = fields;
				num2 = 0;
				goto IL_059f;
			case 2:
				num = -1;
				goto IL_0431;
			case 3:
				num = -1;
				goto IL_04d7;
			case 4:
				{
					num = -1;
					goto IL_0582;
				}
				IL_059f:
				if (num2 < array2.Length)
				{
					field = array2[num2];
					goto IL_0431;
				}
				await pause(500);
				if (!(await script(SepeTimeTransfer.Verify(a, d, clock())) != "\"verified\"") && Same(request, latest()))
				{
					submission(obj: true);
					string result = await script(SepeTimeTransfer.SubmitRequest(request, a, d, clock()));
					if (result == "\"submitted\"")
					{
						return;
					}
					submission(obj: false);
					string rejection = Json.Read<string>(Encoding.UTF8.GetBytes(result));
					if (!(rejection == "changed"))
					{
						if (rejection != null && rejection.StartsWith("validation: "))
						{
							throw SepeChronologyException.FromPage("Walidacja formularza SEPE: " + rejection.Substring(12));
						}
						throw new InvalidOperationException("Zmieniono pociąg lub godziny formularza. Zapis wstrzymany.");
					}
				}
				attempt++;
				break;
				IL_0582:
				await pause(250);
				num2++;
				goto IL_059f;
				IL_04d7:
				prepared = await script(SepeTimeTransfer.Field(a, d, clock(), field));
				if (prepared != "\"prepared\"" && prepared != "\"skipped\"")
				{
					throw new InvalidOperationException("SEPE nie przyjęło daty lub pola godziny. Sprawdź uprawnienia i datę serwera.");
				}
				goto IL_0582;
				IL_0431:
				if (await script(SepeTimeTransfer.Identity(request)) != "\"matched\"")
				{
					throw new InvalidOperationException("SEPE zmieniło wybrany pociąg. Zapis wstrzymany.");
				}
				goto IL_04d7;
			}
		}
		throw new InvalidOperationException("Formularz SEPE nie zachował wpisanych godzin. Zapis wstrzymany.");
	}
}
