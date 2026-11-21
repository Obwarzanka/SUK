using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace LcsWidzew.Sim;

/// <summary>Jedna istniejąca faza algorytmu C#; nie jest publicznym protokołem poleceń ani ABI Ada.</summary>
public enum KrokKz { Statusy, Blokady, Sygnaly, Przebiegi, KodySygnalow, Polecenie, WszystkieStoj }

public enum RodzajEfektu { Zwrotnica, Wykolejnica, Sygnal, NapiecieNastawcze, Agregat, ZeroLO, Zdarzenie, Alarm, Uwaga, Status }
public enum WarunekAlarmu { Brak, BrakSieci, ZwrotnicaWRuchu, Rozprucie, BrakKontroli, BrakDanychSygnalu, BrakSygnaluPrzebiegu, NastawianiePrzebiegu, BrakBlokuKoncowego, ZadanieBlokady, AktualneZadanieBlokady }

/// <summary>Wyłącznie wartości; warunek alarmu jest danymi, nigdy delegatem zamykającym stan kanału.</summary>
public sealed record EfektWyjscia
{
    public string EffectId { get; init; }
    public RodzajEfektu Rodzaj { get; init; }
    public string Obiekt { get; init; } = "";
    public string Wartosc { get; init; } = "";
    public string Typ { get; init; } = "";
    public string Kategoria { get; init; } = "";
    public string Zrodlo { get; init; } = "System";
    public string KluczAlarmu { get; init; }
    public WarunekAlarmu Warunek { get; init; }
    public string Przebieg { get; init; }
    public long Zadanie { get; init; }
}

/// <summary>
/// Bierny OutputPlan S02A.03. Nie zawiera kart, KZ, terenu, delegatów ani proponowanego mutable stanu.
/// Stan proponowany przechowuje prywatnie dotychczasowy KZ, poza kontraktem planu. Przyjmuje go dopiero bramka.
/// </summary>
public sealed class PlanWyjscia
{
    public const string Kontrakt = "kz.plan_wyjscia";
    public string Swiat { get; }
    public long Numer { get; }
    public long Cykl { get; }
    public long CzasSymulacjiMs { get; }
    public KrokKz Krok { get; }
    public string InputHash { get; }
    public ImmutableArray<EfektWyjscia> Efekty { get; }
    public CmdResult Wynik { get; }
    public string PlanId => $"KZ-{Numer}";
    public string ContentHash { get; }

    internal PlanWyjscia(string swiat, long numer, long cykl, long czasMs, KrokKz krok, string inputHash,
        ImmutableArray<EfektWyjscia> efekty, CmdResult wynik)
    {
        Swiat = swiat; Numer = numer; Cykl = cykl; CzasSymulacjiMs = czasMs; Krok = krok; InputHash = inputHash;
        Efekty = efekty; Wynik = wynik;
        ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Kanoniczny())));
    }

    public string Kanoniczny()
    {
        var b = new StringBuilder(256 + 200 * Efekty.Length).Append('{');
        KanonJson.Liczba(b, "schemaVersion", 1, pierwsze: true); KanonJson.Tekst(b, "kontrakt", Kontrakt);
        KanonJson.Liczba(b, "numer", Numer); KanonJson.Liczba(b, "cykl", Cykl);
        KanonJson.Liczba(b, "czasSymulacjiMs", CzasSymulacjiMs); KanonJson.Tekst(b, "krok", Krok.ToString());
        KanonJson.Tekst(b, "inputHash", InputHash); KanonJson.Logiczna(b, "ok", Wynik?.Ok);
        KanonJson.Tekst(b, "wynik", Wynik?.Msg); KanonJson.Pole(b, "efekty").Append('[');
        for (int i = 0; i < Efekty.Length; i++)
        {
            if (i > 0) b.Append(','); var e = Efekty[i]; b.Append('{');
            KanonJson.Pole(b, "effectId", pierwsze: true); KanonJson.Tekst(b, e.EffectId);
            KanonJson.Tekst(b, "rodzaj", e.Rodzaj.ToString()); KanonJson.Tekst(b, "obiekt", e.Obiekt);
            KanonJson.Tekst(b, "wartosc", e.Wartosc); KanonJson.Tekst(b, "typ", e.Typ); KanonJson.Tekst(b, "kategoria", e.Kategoria);
            KanonJson.Tekst(b, "zrodlo", e.Zrodlo); KanonJson.Tekst(b, "kluczAlarmu", e.KluczAlarmu);
            KanonJson.Tekst(b, "warunek", e.Warunek.ToString()); KanonJson.Tekst(b, "przebieg", e.Przebieg);
            KanonJson.Liczba(b, "zadanie", e.Zadanie); b.Append('}');
        }
        return b.Append("]}").ToString();
    }
}

/// <summary>Uporządkowane rozstrzygnięcie świata; Executed oznacza zakończenie adaptera, nie potwierdzenie karty.</summary>
public sealed record RozstrzygniecieWyjscia(long NrDecyzji, string PlanId, string ContentHash, string Stan, string Powod, int Wykonane);
