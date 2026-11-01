using System;

namespace EdrLocal;

public static class TelephoneTemplates
{
	public static readonly string[] Ids = new string[22]
	{
		"9", "10", "11", "12", "16", "16a", "17", "17a", "21", "21a",
		"022", "22", "23", "24", "25", "26", "27", "28", "29", "30",
		"31", "32"
	};

	public static readonly string[] Titles = new string[22]
	{
		"[9] Żądanie wstrzymania wyprawiania pociągów.", "[10] Odwołanie wstrzymania wyprawiania pociągów.", "[11] Żądanie wstrzymania wyprawienia pociągu do czasu przejazdu pociągu z pierwszeństwem.", "[12] Żądanie wstrzymania wyprawienia wszystkich pociągów do czasu przejazdu pociągu z pierwszeństwem.", "[16] Wprowadzenie telefonicznego zapowiadania pociągów dla toru szlakowego.", "[16a] Wprowadzenie radiotelefonicznego zapowiadania pociągów dla toru szlakowego.", "[17] Odwołanie telefonicznego zapowiadania pociągów dla toru szlakowego.", "[17a] Odwołanie radiotelefonicznego zapowiadania pociągów dla toru szlakowego.", "[21] Zamknięcie toru szlakowego.", "[21a] Zamknięcie toru szlakowego po przejeździe pociągu.",
		"[022] Zakończenie robót na torze szlakowym.", "[22] Otwarcie toru szlakowego.", "[23] Wprowadzenie ruchu jednotorowego dwukierunkowego.", "[24] Żądanie wprowadzenia ruchu jednotorowego dwukierunkowego.", "[25] Przywrócenie ruchu dwutorowego.", "[26] Wprowadzenie ruchu dwukierunkowego po torze szlakowym przy czynnych obu torach szlakowych.", "[27] Przywrócenie ruchu jednokierunkowego po torze szlakowym.", "[28] Otwarcie posterunku.", "[29] Zamknięcie posterunku.", "[30] Żądanie zatrzymania pociągów - przyjechał pociąg bez sygnału końca pociągu.",
		"[31] Wstrzymanie ruchu po torze szlakowym dla pociągów z trakcją elektryczną.", "[32] Przywrócenie ruchu po torze szlakowym dla pociągów z trakcją elektryczną."
	};

	public static string Body(TelephoneData data)
	{
		string text = (data.Incoming ? data.NeighbourStation : data.LocalStation);
		string text2 = (data.Incoming ? data.LocalStation : data.NeighbourStation);
		string text3 = "od " + text + " do " + text2;
		string text4 = data.EffectiveAt.ToString("HH:mm");
		switch (data.TemplateId)
		{
		case "OWN":
			return (data.FreeText ?? "").Trim();
		case "9":
			return "Nie wyprawiać pociągów od " + text4 + " aż do odwołania.";
		case "10":
			return "Wstrzymanie wyprawienia pociągów odwołuję o " + text4 + ".";
		case "11":
			return "Nie wyprawiać pociągu " + data.TrainNumber.Trim() + " do czasu przejazdu pociągu " + data.PassingTrainNumber.Trim() + ".";
		case "12":
			return "Nie wyprawiać żadnego pociągu do czasu przejazdu pociągu " + data.PassingTrainNumber.Trim() + ".";
		case "16":
			return "O " + text4 + " wprowadzam telefoniczne zapowiadanie pociągów po torze numer " + data.Track + " " + text3 + " z powodu " + data.Reason.Trim() + ".";
		case "16a":
			return "O " + text4 + " wprowadzam radiotelefoniczne zapowiadanie pociągów po torze numer " + data.Track + " " + text3 + " z powodu " + data.Reason.Trim() + ".";
		case "17":
			return "O " + text4 + " odwołuję telefoniczne zapowiadanie pociągów po torze numer " + data.Track + " " + text3 + ".";
		case "17a":
			return "O " + text4 + " odwołuję radiotelefoniczne zapowiadanie pociągów po torze numer " + data.Track + " " + text3 + ".";
		case "23":
			return "Od " + text + " do " + text2 + " wprowadzam ruch jednotorowy dwukierunkowy po torze numer " + data.Track + ", po którym ostatni pociąg " + data.TrainNumber.Trim() + " odjechał o " + data.LastTrainDeparture.Value.ToString("HH:mm") + ".";
		case "24":
			return "Wprowadzić ruch jednotorowy dwukierunkowy " + text3 + " po torze numer " + data.Track + ".";
		case "25":
			return "Przywracam ruch dwutorowy " + text3 + " o " + text4 + ".";
		case "26":
			return "Przy czynnych obu torach wprowadzam " + text3 + " ruch dwukierunkowy po torze numer " + data.Track + ", po którym ostatni pociąg " + data.TrainNumber.Trim() + " odjechał o " + data.LastTrainDeparture.Value.ToString("HH:mm") + ".";
		case "27":
			return "Przywracam ruch jednokierunkowy po torze numer " + data.Track + " " + text3 + " o " + text4 + ".";
		case "31":
			return "Tor numer " + data.Track + " " + text3 + " od godziny " + data.EffectiveAt.ToString("HH") + " minut " + data.EffectiveAt.ToString("mm") + " nieprzejezdny dla pociągów z trakcją elektryczną.";
		case "32":
			return "Po torze numer " + data.Track + " " + text3 + " od godziny " + data.EffectiveAt.ToString("HH") + " minut " + data.EffectiveAt.ToString("mm") + " przywracam ruch trakcją elektryczną.";
		case "21":
			return "Tor numer " + data.Track + " " + text3 + " zamknięty" + (string.IsNullOrWhiteSpace(data.ClosureMode) ? "" : (" " + data.ClosureMode)) + " od " + text4 + (data.ForPurpose ? " celem " : " z powodu ") + data.Reason.Trim() + ".";
		case "21a":
			return "Tor numer " + data.Track + " " + text3 + " zamknięty" + (string.IsNullOrWhiteSpace(data.ClosureMode) ? "" : (" " + data.ClosureMode)) + " po przejeździe pociągu numer " + data.TrainNumber.Trim() + (data.ForPurpose ? " celem " : " z powodu ") + data.Reason.Trim() + ".";
		case "022":
			return "Roboty na torze " + data.Track + " szlaku " + text + " – " + text2 + " zostały ukończone o godzinie " + data.EffectiveAt.ToString("HH") + " minut " + data.EffectiveAt.ToString("mm") + ". Tor ten jest zdatny i nie ma przeszkód do ruchu pociągów.\nOgraniczenia prędkości: " + data.SpeedRestrictions.Trim() + "\n" + data.WorkSupervisorName.Trim() + ", " + data.WorkSupervisorRole.Trim() + ".";
		case "22":
			return "Tor numer " + data.Track + " " + text3 + " otwarty o " + text4 + ".";
		case "28":
		case "29":
			return "Posterunek " + text + ((data.TemplateId == "28") ? " otwarty" : " zamknięty") + " o godzinie " + data.EffectiveAt.ToString("HH") + " minut " + data.EffectiveAt.ToString("mm") + ".";
		case "30":
			return "Zatrzymać pociągi, pociąg " + data.TrainNumber.Trim() + (data.Passed ? " przejechał" : " przyjechał") + " bez sygnału końca pociągu.";
		default:
			throw new Exception("Nieobsługiwany wzór telefonogramu.");
		}
	}

	public static string Effect(string id)
	{
		switch (id)
		{
		case "16":
		case "16a":
			return "Telefoniczne";
		case "17":
		case "17a":
			return "Normalne";
		case "23":
		case "24":
			return "RuchJednotorowy";
		case "25":
			return "RuchDwutorowy";
		case "26":
			return "RuchDwukierunkowy";
		case "27":
			return "RuchJednokierunkowy";
		case "31":
			return "TrakcjaWylaczona";
		case "32":
			return "TrakcjaPrzywrocona";
		case "21":
			return "Zamknij";
		case "21a":
			return "ZamknijPoPrzejezdzie";
		case "22":
			return "Otwórz";
		default:
			return "";
		}
	}
}
