using Lcs.Tabor;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Jazda wg fizyki (PLAN_EDYTOR_TABORU.md, etapy B2, B4 – maszynista wspólny z przejazdem próbnym edytora taboru, <see cref="Maszynista"/>): pociąg ze składem z pliku taboru – siła pociągowa, opory, pochylenia toru pod składem
/// (<c>poch</c> z UT), łuki (<c>Edge.Radius</c>), sieć trakcyjna (<c>elec</c>), hamowanie z masy hamującej. Prowadzi automatyczny maszynista:
/// prędkość dozwolona z zapasem, krzywe hamowania do ograniczeń i do sygnału „Stój” z pochyleniem na drodze hamowania.
/// Kroki stałe 0,1 s (<see cref="Pociag.Krok"/>) – ruch nie zależy od długości klatki.
/// </summary>
public partial class Trains
{
    TaborPlik tabor;
    bool? elecDane;

    /// <summary>Plik taboru: wspólny <c>Data/tabor.json</c> uzupełniony <c>&lt;układ&gt;.tabor.json</c> (wczytywany przy pierwszym składzie).</summary>
    public TaborPlik Tabor
    {
        get
        {
            if (tabor != null) return tabor;
            try { tabor = TaborPlik.Wczytaj(S.L.SourcePath); }
            catch (Exception ex) { tabor = new TaborPlik(); S.Remark("Plik taboru nie wczytany: " + ex.Message); }
            return tabor;
        }
        set => tabor = value;
    }

    /// <summary>Skład z rozkładu → fizyka pociągu (długość i Vmax ze składu). Nieznany skład – uwaga i ruch uproszczony.</summary>
    void Fizyka(Train t, string sklad)
    {
        if (sklad == null) return;
        var sk = Tabor.Sklad(sklad);
        if (sk == null) { S.Remark($"Pociąg {t.Num}: nie ma składu „{sklad}” w pliku taboru – jedzie w ruchu uproszczonym"); return; }
        try
        {
            t.Fiz = new Pociag(sk, Tabor);
            t.Jaz = new StanJazdy();
            t.Len = t.Fiz.Dlugosc; t.VMax = t.Fiz.VMax / 3.6;
        }
        catch (Exception ex) { t.Fiz = null; t.Jaz = null; S.Remark($"Pociąg {t.Num}, skład {sklad}: {ex.Message} – ruch uproszczony"); }
    }

    /// <summary>Sieć trakcyjna na torze; układ bez danych o elektryfikacji (żaden tor bez <c>elec</c>) – sieć wszędzie.</summary>
    bool Siec(string eid)
    {
        elecDane ??= S.L.Edges.Values.Any(e => e.Elec);
        return elecDane == false || (eid != null && S.L.Edges[eid].Elec);
    }

    /// <summary>Pochylenie kawałka toru w kierunku jazdy [‰] (dodatnie – wzniesienie); brak danych – 0.</summary>
    double Poch(string eid, string from)
    {
        var e = S.L.Edges[eid];
        if (double.IsNaN(e.Grad)) return 0;
        return e.A == from ? e.Grad : -e.Grad;
    }

    /// <summary>Opór łuku [N/kN] (wzór Röckla 650 / (R − 55), do potwierdzenia); tor prosty / bez danych – 0.</summary>
    double Luk(string eid) { var r = S.L.Edges[eid].Radius; return r > 150 ? 650 / (r - 55) : r > 0 ? 650 / 95.0 : 0; }

    /// <summary>Średnie pochylenie [‰] i opór łuku [N/kN] pod składem (ważone długością pociągu na kawałkach toru).</summary>
    (double I, double R) PodSkladem(Train t)
    {
        double pos = 0, tail = t.Head - t.Len, i = 0, r = 0, len = 0;
        foreach (var s in t.Segs)
        {
            double a = pos, b = pos + s.Len; pos = b;
            double ov = Math.Min(b, t.Head) - Math.Max(a, tail);
            if (ov <= 0) continue;
            len += ov;
            if (s.E == null) continue;
            i += ov * Poch(s.E, s.From); r += ov * Luk(s.E);
        }
        return len > 0 ? (i / len, r / len) : (0, 0);
    }

    /// <summary>Tor przed czołem: kawałki (początek od czoła [m], długość, pochylenie ‰, ograniczenie prędkości [m/s]) do ok. 2,5 km.</summary>
    List<(double Od, double Len, double I, double Lim)> Przed(Train t)
    {
        var l = new List<(double, double, double, double)>();
        var last = t.Segs[^1];
        double rest = Total(t) - t.Head;
        if (last.E != null && rest > 0) l.Add((0, rest, Poch(last.E, last.From), 1e4));
        if (last.Sink || last.E == null) return l;
        double dist = rest; string node = last.To, from = last.E; int dir = last.Dir;
        for (int k = 0; k < 40 && dist < 2500; k++)
        {
            var nx = S.NextInTerrain(node, dir, from); if (nx == null) break;
            var len = S.L.Edges[nx.Edge].Len;
            l.Add((dist, len, Poch(nx.Edge, node), EdgeLimit(nx.Edge)));
            dist += len; from = nx.Edge; node = S.L.Other(nx.Edge, node);
        }
        return l;
    }

    /// <summary>Prędkość docelowa maszynisty [m/s] (<see cref="Maszynista.Docelowa"/>): dozwolona pod składem, ograniczenia przed czołem, miejsce zatrzymania.</summary>
    double Docelowa(Train t, (double D, string Sig, bool Dwell, bool End) la, bool siec)
    {
        double vmax = t.VMax;
        if (t.SzLimit) vmax = Math.Min(vmax, VSz / 3.6);
        if (t.Shunt) vmax = Math.Min(vmax, VShunt / 3.6);
        double pod = 1e4, pos = 0, tail = t.Head - t.Len;
        foreach (var sg in t.Segs) { double a = pos, b = pos + sg.Len; pos = b; if (sg.E != null && b > tail && a < t.Head) pod = Math.Min(pod, EdgeLimit(sg.E)); }
        var przed = Przed(t).Select(k => new Maszynista.Przed(k.Od, k.Len, k.I, k.Lim)).ToList();
        return Maszynista.Docelowa(t.Fiz, t.Jaz.V, Math.Min(vmax, pod), przed, la.D, siec);
    }

    /// <summary>Ruch pociągu ze składem przez dt [s] w krokach 0,1 s; zwraca stan przed czołem po ruchu.</summary>
    (double D, string Sig, bool Dwell, bool End) MoveFiz(Train t, double dt, (double D, string Sig, bool Dwell, bool End) la)
    {
        if (t.V == 0) t.Jaz.V = 0;   // postój, zmiana czoła (Reverse) – prędkość zerowana poza fizyką
        t.Zapas += dt;
        while (t.Zapas >= Pociag.Krok - 1e-9 && !t.Gone)
        {
            t.Zapas -= Pociag.Krok;
            la = Lookahead(t);
            var headE = t.Segs.LastOrDefault(s => s.E != null)?.E;
            bool siec = Siec(headE);
            var (ip, rp) = PodSkladem(t);
            double cel = Docelowa(t, la, siec);
            t.IPod = ip; t.VCel = cel;
            var u = new Sterowanie { Pochylenie = ip, OporLuku = rp, Siec = siec, Warunki = 1 };
            Maszynista.Steruj(ref u, t.Jaz.V, cel, la.D);
            double d = Jazda.Krok(t.Fiz, t.Jaz, u, Pociag.Krok);
            // miejsce zatrzymania (sygnał „Stój”, postój, koniec drogi) – pociąg go nie mija
            if (double.IsFinite(la.D) && d >= la.D) { d = Math.Max(0, la.D); t.Jaz.V = 0; }
            t.V = t.Jaz.V;
            if (t.Jaz.NieRusza && !t.LoggedNieRusza)
            {
                t.LoggedNieRusza = true;
                S.Log("Ruch", t.Jaz.BrakZasilania ? $"Pociąg {t.Num} nie może ruszyć – brak sieci trakcyjnej dla pojazdów elektrycznych"
                    : $"Pociąg {t.Num} nie może ruszyć – za mała siła pociągowa na wzniesieniu {ip:0.#} ‰", "Pociąg", t.Num);
            }
            if (!t.Jaz.NieRusza) t.LoggedNieRusza = false;
            if (d > 0)
            {
                t.StoppedSince = 0; t.WaitingAt = null;
                Advance(t, d);
                if (t.V == 0) t.Jaz.V = 0;
            }
            if (t.V < 0.05 && la.D < 1) { t.Zapas = 0; break; }
        }
        return t.Gone ? la : Lookahead(t);
    }
}
