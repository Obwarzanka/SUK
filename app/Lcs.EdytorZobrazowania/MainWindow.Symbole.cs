using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Symbole na torach w oknie (etap Z3, PLAN_EDYTOR_ZOBRAZOWANIA.md § 4): narzędzia Zwrotnica (Z), Sygnalizator (S), Wykolejnica (W),
/// Kozioł (K) – obiekt UT wybrany w panelu UT albo następny nieumieszczony, klik na linii jego toru obrazu; panel symbolu; „Sprawdź” (F7).
/// </summary>
public partial class MainWindow
{
    /// <summary>Obiekt UT do postawienia narzędziem symbolu.</summary>
    public string SymCurrent { get; private set; }
    /// <summary>Wynik ostatniego „Sprawdź” (null – nie uruchomione).</summary>
    public List<ZobUwaga> Uwagi { get; private set; }
    public readonly StackPanel CheckPanel = new() { Margin = new Thickness(0, 4, 0, 8) };

    static ZobRodzaj? RodzajNarzedzia(ZobTool t) => t switch
    {
        ZobTool.Zwrotnica => ZobRodzaj.Zwrotnica, ZobTool.Sygnalizator => ZobRodzaj.Sygnalizator,
        ZobTool.Wykolejnica => ZobRodzaj.Wykolejnica, ZobTool.Koziol => ZobRodzaj.Kozioł, ZobTool.Przerwa => ZobRodzaj.Przerwa,
        ZobTool.NumerToru => ZobRodzaj.NumerToru, ZobTool.Przejazd => ZobRodzaj.Przejazd, ZobTool.Szlak => ZobRodzaj.Szlak,
        ZobTool.OpisOdcinka => ZobRodzaj.OpisOdcinka, _ => null,
    };
    static string Grupa(ZobRodzaj r) => r switch
    {
        ZobRodzaj.Zwrotnica => ZobUt.GRozjazdy, ZobRodzaj.Sygnalizator => ZobUt.GSygnalizatory, ZobRodzaj.Wykolejnica => ZobUt.GWykolejnice,
        ZobRodzaj.Kozioł => ZobUt.GKozly, ZobRodzaj.Przerwa => ZobUt.GPrzerwy, ZobRodzaj.NumerToru => ZobUt.GNumery,
        ZobRodzaj.Przejazd => ZobUt.GPrzejazdy, ZobRodzaj.Szlak => ZobUt.GSzlaki, _ => ZobUt.GOdcinki,
    };
    static string NazwaNarzedzia(ZobRodzaj r) => r switch
    {
        ZobRodzaj.Zwrotnica => "Zwrotnica", ZobRodzaj.Sygnalizator => "Sygnalizator", ZobRodzaj.Wykolejnica => "Wykolejnica", ZobRodzaj.Kozioł => "Kozioł",
        ZobRodzaj.Przerwa => "Przerwa", ZobRodzaj.NumerToru => "Numer toru", ZobRodzaj.Przejazd => "Przejazd", ZobRodzaj.Szlak => "Szlak", _ => "Opis odcinka",
    };

    void InitSymbole()
    {
        View.SymbolClick += OnSymbolClick;
        // opis odcinka – sam napis: przeciągnięcie przesuwa jego punkt
        View.OpisMoved += (r, u, pt) => ChSym(r, u, "Przesunięcie opisu", s => { if (r == ZobRodzaj.OpisOdcinka) s.P = pt; else s.Opis = pt; });
        InitZ4();
    }

    void ChSym(ZobRodzaj r, string ut, string what, Action<ZobSymbol> act) =>
        Ch(what, o => { var s = ZobSymbole.Lista(o, r).FirstOrDefault(x => x.Ut == ut); if (s != null) act(s); });

    /// <summary>Opis obiektu do komunikatów: nazwa, tor i km z UT.</summary>
    string SymOpis(string ut)
    {
        var n = ZobSymbole.Nazwa(Doc.Ut, ut);
        if (ZobSymbole.RodzajUt(Doc.Ut, ut) is ZobRodzaj.NumerToru or ZobRodzaj.Szlak) return n;
        return ZobSymbole.Miejsce(Doc.Ut, ut) is var (tr, km) ? $"{n} (tor {Doc.Ut.Tracks.FirstOrDefault(t => t.Id == tr)?.Number ?? "?"}" + (double.IsNaN(km) ? ")" : $" km {km:0})") : n;
    }

    /// <summary>Narzędzie symbolu: obiekt UT – wybrany w panelu UT (gdy tego rodzaju), inaczej następny nieumieszczony.</summary>
    void PickSym(ZobRodzaj r, string prefer = null)
    {
        var all = ZobUt.Obiekty(Doc.Ut, Obraz, true).Where(x => x.Grupa == Grupa(r) && ZobSymbole.RodzajUt(Doc.Ut, x.Id) == r).ToList();
        // następny nieumieszczony – tylko z posterunków obrazu; obiekt innego posterunku – gdy wybrany w panelu UT
        SymCurrent = all.FirstOrDefault(x => x.Id == prefer)?.Id ?? all.FirstOrDefault(x => !x.NaObrazie && !x.Obcy)?.Id;
        var nazwa = NazwaNarzedzia(r);
        if (SymCurrent == null)
            Message(all.Count == 0 ? $"{nazwa}: w UT obrazu nie ma takich obiektów" : $"{nazwa}: wszystkie są już na obrazie (wybierz w panelu UT, żeby przestawić)");
        else
            Message($"{nazwa}: {SymOpis(SymCurrent)}{(all.First(x => x.Id == SymCurrent).NaObrazie ? " – jest na obrazie, klik przestawi" : "")} – kliknij na linii jego toru obrazu" +
                (r switch
                {
                    ZobRodzaj.Kozioł => " (przy końcu toru)", ZobRodzaj.Zwrotnica => " (w miejscu, gdzie odchodzi tor odgałęźny)",
                    ZobRodzaj.Szlak => " (za semaforem wjazdowym, od strony szlaku)", ZobRodzaj.OpisOdcinka => " – w miejscu opisu, przy torze odcinka",
                    ZobRodzaj.NumerToru => " – w dowolnym miejscu toru", _ => "",
                }));
    }

    void OnSymbolClick(int[] p, string tor)
    {
        if (RodzajNarzedzia(View.Tool) is not ZobRodzaj r || Obraz == null) return;
        if (SymCurrent == null) { PickSym(r); return; }
        PlaceSym(r, SymCurrent, p, tor);
    }

    /// <summary>Postawienie (albo przestawienie) symbolu obiektu UT w punkcie na torze obrazu.</summary>
    public bool PlaceSym(ZobRodzaj r, string ut, int[] p, string tor)
    {
        int? koniec = null;
        if (r == ZobRodzaj.Szlak) { koniec = KoniecSzlaku(ut, p); if (koniec == null) return false; }
        if (r == ZobRodzaj.OpisOdcinka) return PlaceOpis(ut, p);
        if (ZobSymbole.Miejsce(Doc.Ut, ut, koniec ?? 0) is not var (track, km)) { Message("Obiektu nie ma w UT"); return false; }
        var kaw = ZobSymbole.Kawalki(Obraz, track, km).ToList();
        string trNr = Doc.Ut.Tracks.FirstOrDefault(t => t.Id == track)?.Number ?? "?";
        if (kaw.Count == 0) { Message($"{SymOpis(ut)}: najpierw narysuj tor {(trNr.Length > 0 ? trNr : TorName(track))} z km {km:0} (narzędzie Tor)"); return false; }
        var t = kaw.FirstOrDefault(x => x.Id == tor);
        int g = Doc.P.Siatka;
        if (t == null)
        {
            // kursor przyciągnięty do innego toru (np. węzeł łącznika) – linia toru obiektu w pobliżu kliknięcia (do 10 px)
            var near = kaw.Select(x => (T: x, R: ZobTory.Rzut(x.Pkt, p[0], p[1]))).Where(x => x.R.Dist * View.Zoom <= 10).OrderBy(x => x.R.Dist).FirstOrDefault();
            if (near.T != null) { t = near.T; p = ZobTory.NaOdcinku(t.Pkt[near.R.Seg], t.Pkt[near.R.Seg + 1], p[0], p[1], g); }
        }
        if (t == null) { Message($"{SymOpis(ut)} stoi na torze {(trNr.Length > 0 ? trNr : TorName(track))} w km {km:0} – kliknij na linii tego toru obrazu"); return false; }
        if (r == ZobRodzaj.Kozioł)
        {
            // koniec kawałka toru obrazu, którego km jest bliżej km kozła
            p = Math.Abs(t.Ut.S0 - km) <= Math.Abs(t.Ut.S1 - km) ? t.Pkt[0] : t.Pkt[^1];
            p = new[] { p[0], p[1] };
        }
        else if (r == ZobRodzaj.Zwrotnica && Doc.Ut.Turnouts.FirstOrDefault(z => z.Id == ut) is UtTurnout z)
        {
            // przyciąganie do miejsca, gdzie od toru zasadniczego odchodzi narysowany tor odgałęźny (do 3 kroków siatki)
            var ends = Obraz.Tory.Where(x => x.Ut?.Tor == z.Branch && x.Pkt.Count >= 2).SelectMany(x => new[] { x.Pkt[0], x.Pkt[^1] })
                .Where(q => ZobSymbole.NaTorze(t, q)).OrderBy(q => Math.Abs(q[0] - p[0]) + Math.Abs(q[1] - p[1])).ToList();
            if (ends.Count > 0 && Math.Abs(ends[0][0] - p[0]) + Math.Abs(ends[0][1] - p[1]) <= 3 * g) p = new[] { ends[0][0], ends[0][1] };
        }
        else if (r == ZobRodzaj.Zwrotnica && Krzyzowe.Parse(Doc.Ut, ut) is var (kc, kk) && Krzyzowe.Wezly(Doc.Ut, kc, kk) is { Count: 2 } w)
        {
            // rozjazd krzyżowy: przyciąganie do końca narysowanego toru B w km tego końca (do 3 kroków siatki)
            var ends = Obraz.Tory.Where(x => x.Ut?.Tor == w[1].Track && x.Pkt.Count >= 2)
                .SelectMany(x => new[] { (x.Ut.S0, x.Pkt[0]), (x.Ut.S1, x.Pkt[^1]) }).Where(q => Math.Abs(q.Item1 - w[1].S) < 0.5 && ZobSymbole.NaTorze(t, q.Item2))
                .Select(q => q.Item2).OrderBy(q => Math.Abs(q[0] - p[0]) + Math.Abs(q[1] - p[1])).ToList();
            if (ends.Count > 0 && Math.Abs(ends[0][0] - p[0]) + Math.Abs(ends[0][1] - p[1]) <= 3 * g) p = new[] { ends[0][0], ends[0][1] };
        }
        else if (r == ZobRodzaj.Szlak && Doc.Ut.Lines.FirstOrDefault(l => l.Id == ut) is UtLine line && line.Konce[koniec.Value].Semafor is string semId
                 && Doc.Ut.Devices.FirstOrDefault(d => d.Id == semId) is UtDevice sd)
        {
            // strzałka blokady za semaforem wjazdowym, od strony szlaku (ROZWOJ § 2a)
            // strona szlaku względem symbolu semafora na obrazie (symbole stoją tam, gdzie postawił je człowiek, nie liniowo wg km)
            var semSym = Obraz.Sygnalizatory.FirstOrDefault(x => x.Ut == semId && ZobSymbole.NaTorze(t, x.P));
            bool zlaStrona;
            if (semSym != null) { var (ox, oy) = ZobSymbole.Kierunek(t, semSym.P, -sd.Dir); zlaStrona = (p[0] - semSym.P[0]) * ox + (p[1] - semSym.P[1]) * oy <= 0; }
            else zlaStrona = (ZobSymbole.KmNa(t, p) - sd.S) * -sd.Dir <= 0.5;
            if (zlaStrona) { Message($"Strzałka szlaku {line.Numer} ma stać za semaforem wjazdowym {sd.Name}, od strony szlaku"); return false; }
        }
        var sym = new ZobSymbol { Ut = ut, P = p, Koniec = koniec };
        var old = ZobSymbole.Lista(Obraz, r).FirstOrDefault(x => x.Ut == ut && x.Koniec == koniec);
        bool was = old != null;
        if (was) { sym.Opis = r == ZobRodzaj.Szlak ? null : old.Opis; sym.Zasieg = old.Zasieg; }
        Ch(was ? "Przestawienie symbolu" : "Symbol na obrazie", o => { var l = ZobSymbole.Lista(o, r); l.RemoveAll(x => x.Ut == ut && x.Koniec == koniec); l.Add(sym); });
        var placed = SymOpis(ut);
        var kolej = ZobSymbole.Sprawdz(Doc.Ut, Obraz).FirstOrDefault(u => u.Ut == ut && u.Error);
        PickSym(r);
        Message($"{placed} na obrazie ({p[0]}, {p[1]})" + (kolej != null ? $" – uwaga: {kolej.Text}" : "") + (SymCurrent != null ? $". Następny: {SymOpis(SymCurrent)}" : ". Wszystkie tego rodzaju są na obrazie"));
        return true;
    }

    /// <summary>Panel UT – klik na obiekcie z symbolem: w narzędziu tego rodzaju – do postawienia, inaczej – zaznaczenie na obrazie.</summary>
    bool UtSymClicked(ZobUtObj o)
    {
        if (ZobSymbole.RodzajUt(Doc.Ut, o.Id) is not ZobRodzaj r) return false;
        if (RodzajNarzedzia(View.Tool) == r) { PickSym(r, o.Id); return true; }
        if (ZobSymbole.Lista(Obraz, r).Any(x => x.Ut == o.Id)) { View.SelectSym(r, o.Id); Message($"{SymOpis(o.Id)} – na obrazie"); return true; }
        Message($"{SymOpis(o.Id)} – nie ma go jeszcze na obrazie (narzędzie {NazwaNarzedzia(r)}{(Klawisz(r) is string k ? " – " + k : "")})");
        return true;
    }

    public void DeleteSym()
    {
        if (View.SelectedSym is not var (r, ut)) return;
        var n = SymOpis(ut);
        Ch("Zdjęcie symbolu z obrazu", o => ZobSymbole.Lista(o, r).RemoveAll(x => x.Ut == ut));
        View.SelectSym(null, null);
        Message($"{n} zdjęty z obrazu (w UT zostaje; Ctrl+Z przywraca)");
    }

    /// <summary>Panel zaznaczonego symbolu: dane z UT (tylko do odczytu), opis, zdjęcie z obrazu.</summary>
    void BuildSymPanel()
    {
        if (View.SelectedSym is not var (r, ut) || ZobSymbole.Lista(Obraz, r).FirstOrDefault(x => x.Ut == ut) is not ZobSymbol s) return;
        var P = ImgPanel;
        P.Children.Add(Header("Symbol na obrazie"));
        P.Children.Add(new TextBlock { Text = SymOpis(ut), TextWrapping = TextWrapping.Wrap });
        var lines = new List<string>();
        if (Doc.Ut.Devices.FirstOrDefault(d => d.Id == ut) is UtDevice d)
        {
            if (d.Kind == UtDevice.Semafor) lines.Add("funkcja: " + (string.IsNullOrEmpty(d.Funkcja) ? "nie określona (popraw w edytorze UT)" : UtDevice.FunkcjaName(d.Funkcja)));
            if (d.Kind is UtDevice.Semafor or UtDevice.TarczaMan or UtDevice.TarczaOstrz) lines.Add($"komory: {d.LightsOrDefault}" + (d.Kind == UtDevice.Semafor ? (d.LightsOrDefault.Contains('B') ? " – z sygnalizacją manewrową (grot za trójkątem)" : " – bez sygnalizacji manewrowej") : ""));
            if (d.Kind != UtDevice.Koziol) lines.Add($"kierunek działania w UT: {(d.Dir > 0 ? "w stronę rosnącego km" : "w stronę malejącego km")} toru");
        }
        if (Doc.Ut.Turnouts.FirstOrDefault(z => z.Id == ut) is UtTurnout z)
            lines.Add($"położenie zasadnicze: {(z.Zas == "-" ? "na zwrot (−)" : "na wprost (+)")}" + (z.Manual ? ", zwrotnica ręczna" : ""));
        if (Krzyzowe.Parse(Doc.Ut, ut) is var (kc, kk))
        {
            var kind = Krzyzowe.Rodzaj(kc);
            lines.Add($"{kc.Type}; koniec {kk} – w symulatorze zwrotnica {Krzyzowe.Numer(kc, kk)}");
            lines.Add("dwie zwrotnice zwrócone ostrzami (Ie-104 § 8 pkt 9) – tor B rysuj do punktu zwrotnicy ab i od punktu cd (np. nawiasami „[” „]”)"
                + (kind == RozjazdTyp.Rkp ? "; Rkp – jeden przejazd, jazdy przez brakujący komputer zależności nie dopuszcza" : ""));
        }
        if (Doc.Ut.Lines.FirstOrDefault(l => l.Id == ut) is UtLine line)
            lines.Add($"koniec szlaku: {Szlaki.Opis(Doc.Ut, line.Konce[Math.Clamp(s.Koniec ?? 0, 0, line.Konce.Count - 1)])}; blokada: {UtLine.Typy.FirstOrDefault(x => x.Key == line.Typ).Name}");
        if (r is not (ZobRodzaj.OpisOdcinka or ZobRodzaj.NumerToru)) lines.Add("kierunek symbolu wynika z UT i z kierunku km toru obrazu");
        P.Children.Add(new TextBlock { Text = string.Join("\n", lines), Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
        P.Children.Add(new TextBlock
        {
            Text = r is ZobRodzaj.NumerToru or ZobRodzaj.Przerwa ? $"Symbol w ({s.P[0]}, {s.P[1]}) – przestawiasz go narzędziem {NazwaNarzedzia(r)}."
                : r == ZobRodzaj.OpisOdcinka ? $"Opis w ({s.P[0]}, {s.P[1]}) – przeciągasz go myszą (Wskaż)."
                : $"Symbol w ({s.P[0]}, {s.P[1]}); opis {(s.Opis == null ? "w miejscu domyślnym (jak na pulpicie)" : $"przesunięty – ({s.Opis[0]}, {s.Opis[1]})")}. Opis przeciągasz myszą (Wskaż).",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 6),
        });
        if (r == ZobRodzaj.Przejazd) PrzejazdZasieg(P, s);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        if (s.Opis != null) row.Children.Add(Btn("Opis w miejscu domyślnym", (a, e) => ChSym(r, ut, "Opis w miejscu domyślnym", x => x.Opis = null), "Opis tam, gdzie stawia go pulpit"));
        var del = Btn("Zdejmij z obrazu", (a, e) => DeleteSym(), "Zdejmij symbol z obrazu (Del); w UT zostaje"); del.Margin = new Thickness(s.Opis != null ? 6 : 0, 0, 0, 0);
        row.Children.Add(del);
        P.Children.Add(row);
        PrzypnijSym(P, r, s);
        P.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) });
    }

    /// <summary>„Sprawdź” (F7): uwagi obrazu w panelu, klik – zaznaczenie obiektu.</summary>
    public List<ZobUwaga> Sprawdz()
    {
        if (Doc == null || Obraz == null) return new();
        Uwagi = ZobSymbole.Sprawdz(Doc.Ut, Obraz);
        RefreshCheck();
        Message(Uwagi.Count == 0 ? "Sprawdź: bez uwag" : $"Sprawdź: błędów {Uwagi.Count(u => u.Error)}, uwag {Uwagi.Count(u => !u.Error)}");
        return Uwagi;
    }

    void RefreshCheck()
    {
        CheckPanel.Children.Clear();
        if (Uwagi == null) return;
        if (Doc != null && Obraz != null) Uwagi = ZobSymbole.Sprawdz(Doc.Ut, Obraz);
        CheckPanel.Children.Add(new TextBlock { Text = Uwagi.Count == 0 ? "✔ Bez uwag" : $"Błędów {Uwagi.Count(u => u.Error)}, uwag {Uwagi.Count(u => !u.Error)}", Foreground = Uwagi.Count == 0 ? OkFg : MissFg, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var u in Uwagi.Take(200))
        {
            var b = new Button { Content = new TextBlock { Text = u.Text, TextWrapping = TextWrapping.Wrap }, HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = u.Error ? MissFg : PartFg, Margin = new Thickness(0, 0, 0, 2), Padding = new Thickness(4, 1, 4, 1) };
            var ut = u.Ut;
            b.Click += (s, e) =>
            {
                if (ut == null) return;
                if (ZobSymbole.RodzajUt(Doc.Ut, ut) is ZobRodzaj r && ZobSymbole.Lista(Obraz, r).Any(x => x.Ut == ut)) View.SelectSym(r, ut);
                else { utSelected = ut; Refresh(); }
            };
            CheckPanel.Children.Add(b);
        }
    }
}
