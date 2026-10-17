using LcsWidzew.Model;

using Cfg = LcsWidzew.Sim.Simulator.Cfg;

namespace LcsWidzew.Sim;

/// <summary>
/// Elektroniczna blokada samoczynna dwukierunkowa (Ie-20 § 20, Ie-104 § 8 pkt 17, § 12 pkt 3–4).
/// Polecenia: Wbl (żądanie kierunku WYJAZD), oWbl, Pzk (pozwolenie – kierunek PRZYJAZD), Zwbl (zwolnienie kierunku),
/// Stop / oStop (zamknięcie wyjazdu na szlak), AZK (awaryjna zmiana kierunku – polecenie specjalne).
/// </summary>
public partial class KomputerZaleznosci
{
    /// <summary>Koniec blokady obsługiwany z danego obrazu (dla szlaków wewnętrznych – ten po stronie wyświetlanego posterunku).</summary>
    public int EndIdx(BlockDef b, string page)
    {
        var pg = L.Pages.FirstOrDefault(p => p.Id == page);
        for (int i = 0; i < b.Ends.Count; i++) if (pg != null && pg.Posts.Contains(b.Ends[i].Post)) return i;
        return 0;
    }
    /// <summary>Kierunek blokady słownie: „wyjazd z X” (koniec o tym kierunku na zewnątrz) albo „przyjazd do X” (szlak jednostronny).</summary>
    public string DirText(BlockDef b, int dir)
    {
        if (dir == 0) return "neutralny";
        var i = EndIdxForDir(b, dir); if (i >= 0) return "wyjazd z " + (L.Posts.GetValueOrDefault(b.Ends[i].Post) ?? b.Ends[i].Post);
        var j = EndIdxForDir(b, -dir); return j >= 0 ? "przyjazd do " + (L.Posts.GetValueOrDefault(b.Ends[j].Post) ?? b.Ends[j].Post) : (dir > 0 ? "A→B" : "B→A");
    }
    public int EndIdxForDir(BlockDef b, int dir) { for (int i = 0; i < b.Ends.Count; i++) if (b.Ends[i].Outward == dir) return i; return -1; }

    /// <summary>Szczelina kontrolna kTor (SBL): czerwona, gdy przy ustawionym kierunku dowolny odstęp zajęty albo semafor odstępowy nie zezwala.</summary>
    public bool KTor(BlockDef b)
    {
        var bs = Blocks[b.Id];
        if (b.NoBlock || b.SemiAuto || b.SecsWE.Count < 2 || bs.Dir == 0) return false;
        return b.SecsWE.Any(SecOcc) || L.Signals.Values.Any(s => s.Szlak == b.Id && s.Type == "odstepowy" && s.Dir == bs.Dir && Sigs[s.Name].Aspect != Aspect.Train);
    }

    /// <summary>Półsamoczynna: blok końcowy możliwy na końcu e (pociąg przybył w całości, wjazd stwierdzony).</summary>
    public bool KoReady(BlockDef b, int e)
    {
        if (!b.SemiAuto || e >= b.Ends.Count) return false;
        var bs = Blocks[b.Id];
        if (bs.AwaitEnd != e || b.SecsWE.Any(SecOcc) || bs.FreeSince == 0) return false;
        // semafor wjazdowy wrócił do „Stój” po najechaniu pociągu (samoczynnie) – warunek spełniony przez przejazd
        return bs.EntryOk[e] || bs.DKo[e];
    }

    /// <summary>Przeciwwtórność liniowa: po sygnale zezwalającym na wyjazd kolejny sygnał dopiero po bloku początkowym (Po).</summary>
    public bool PwlBlocks(string sz, int dir)
    {
        var b = L.Blocks[sz]; if (!b.SemiAuto) return false;
        int e = EndIdxForDir(b, dir);
        return e >= 0 && Blocks[sz].Pwl[e];
    }

    public bool BlockAllowsExit(string sz, int dir)
    {
        var b = L.Blocks[sz]; var bs = Blocks[sz];
        int e = EndIdxForDir(b, dir);
        if (e < 0) return false;
        if (b.NoBlock) return !bs.StopEnd[e] && !b.SecsWE.Any(SecOcc);                  // telefoniczne zapowiadanie – szlak wolny (zapowiedź telefoniczna)
        if (bs.AwaitEnd != null) return false;                                          // półsamoczynna – brak bloku końcowego
        return bs.Dir == dir && !bs.Busy && !bs.StopEnd[e] && !(b.Internal ? bs.StopEnd[1 - e] : bs.NbrStop) && !SecOcc(b.FirstSec(dir));
    }

    public string DirText(BlockDef b, int dir, int endIdx)
    {
        if (dir == 0) return "stan neutralny";
        return dir == b.Ends[endIdx].Outward ? "kierunek WYJAZD" : "kierunek PRZYJAZD";
    }

    public List<string> BlockConds(BlockDef b)
    {
        var why = new List<string>();
        foreach (var sc in b.SecsWE) if (SecOcc(sc)) why.Add($"odstęp {sc} zajęty");
        foreach (var en in b.Ends) if (Sigs.TryGetValue(en.EntrySig, out var esg) && esg.Aspect != Aspect.Stop) why.Add($"semafor {en.EntrySig} nie wskazuje „Stój”");
        if (Routes.Values.Any(r => r.End == b.Id)) why.Add("nastawiony przebieg na szlak " + b.Id);
        if (Trains != null && TrainOnBlock(b.Id)) why.Add("pociąg na szlaku " + b.Id);
        return why;
    }

    /// <summary>Lampka TOR – szlak wolny, kierunek ustawiony, wszystkie sygnały odstępowe „wolna droga”.</summary>
    /// <summary>Pociąg na szlaku: odstęp w obszarze LCS zajęty przez pojazd (karty odcinków) albo meldunek sąsiada o pociągu przed granicą.</summary>
    public bool TrainOnBlock(string id) => L.Blocks[id].SecsWE.Any(sc => Secs[sc].TrainOcc) || Otoczenie.SzlakZajetyUSasiada(id);

    public bool TorLamp(BlockDef b)
    {
        var bs = Blocks[b.Id];
        if (!b.Permission) return false;
        if (bs.Dir == 0 || bs.Busy) return false;
        if (b.SecsWE.Any(SecOcc)) return false;
        return L.Signals.Values.Where(s => s.Szlak == b.Id && s.Type == "odstepowy" && s.Dir == bs.Dir).All(s => Sigs[s.Name].Aspect == Aspect.Train);
    }

    internal void UpdateBlocks()
    {
        foreach (var (id, bs) in Blocks)
        {
            var b = L.Blocks[id];
            if (bs.ReleaseUntil is double ru && T >= ru) { bs.ReleaseUntil = null; bs.Dir = 0; Log("Zdarzenie", "Kierunek blokady zwolniony – stan neutralny", "Blokada liniowa", id); }
            if (bs.AzkUntil is double au && T >= au) { bs.AzkUntil = null; bs.Dir = bs.AzkDir; bs.Req = null; Log("Zdarzenie", "Awaryjna zmiana kierunku zakończona", "Blokada liniowa", id); }
            // blokada półsamoczynna: blok początkowy po wjeździe pociągu, blok końcowy po przyjeździe (posterunek przyjmujący)
            if (b.SemiAuto)
            {
                bool occ = b.SecsWE.Any(SecOcc);
                // Pwl: sygnał zezwalający na wyjazd na szlak; po jego wygaszeniu możliwy blok początkowy Po
                for (int e = 0; e < b.Ends.Count; e++)
                {
                    int outw = b.Ends[e].Outward;
                    bool exitOpen = Routes.Values.Any(r => r.End == id && r.EndKind == "szlak" && L.Signals[r.Start].Dir == outw && Sigs[r.Start].Aspect == Aspect.Train);
                    if (exitOpen && !bs.Pwl[e]) { bs.Pwl[e] = true; bs.PoReady[e] = false; Log("Zdarzenie", $"Szlak {id}: przeciwwtórność liniowa (Pwl) – po wyprawieniu pociągu daj blok początkowy ({id} Po)", "Blokada liniowa", id); }
                    if (bs.Pwl[e] && !exitOpen && !bs.PoReady[e]) bs.PoReady[e] = true;
                    // semafor wjazdowy wyświetlił sygnał zezwalający dla pociągu przyjmowanego z tego szlaku
                    if (bs.AwaitEnd == e && Sigs.TryGetValue(b.Ends[e].EntrySig ?? "", out var es) && es.Aspect == Aspect.Train) bs.EntryOk[e] = true;
                }
                // pociąg od posterunku sąsiedniego spoza LCS – jego blok początkowy przychodzi samoczynnie
                Otoczenie.BlokPoczatkowy(id, b, bs, occ);
                if (bs.AwaitEnd is int ae)
                {
                    if (occ) bs.FreeSince = 0;
                    else
                    {
                        if (bs.FreeSince == 0) { bs.FreeSince = T; if (ae >= 0) Log("Zdarzenie", $"Szlak {id} wolny – pociąg przybył; daj blok końcowy ({id} Ko)", "Blokada liniowa", id); }
                        if (ae < 0) Otoczenie.BlokKoncowy(id, b, bs);
                        else if (!bs.EndAlarmed && T - bs.FreeSince > Cfg.ReqAlarmAfter)
                        { bs.EndAlarmed = true; var b2 = bs; Alarm($"Blokada {id}: pociąg przybył, brak bloku końcowego – szlak zablokowany ({id} Ko)", "Blokada liniowa", id, "bk" + id, () => b2.AwaitEnd != null); }
                    }
                }
            }
            var rq = bs.Req;
            if (rq == null) continue;
            // odpowiedź posterunku spoza obszaru LCS na nasze żądanie (Wbl)
            if (!b.Internal && rq.EndIdx == 0) Otoczenie.OdpowiedzNaWbl(id, b, bs, rq);
            // żądanie skierowane do dyżurnego LCS – alarm dopiero po dłuższym braku reakcji (Ie-104 § 7 ust. 3)
            bool toUs = rq.EndIdx == -1 || b.Internal;
            if (toUs && !rq.Alarmed && T - rq.At > Cfg.ReqAlarmAfter)
            {
                rq.Alarmed = true;
                var from = rq.EndIdx == -1 ? b.Nbr : Station.Lbl(b.Ends[rq.EndIdx].EntrySig) + " (" + (L.Posts.GetValueOrDefault(b.Ends[rq.EndIdx].Post) ?? "") + ")";
                Alarm($"Blokada {id}: żądanie pozwolenia od {from} oczekuje od {(T - rq.At) / 60:0} min{(rq.ForTrain != null ? " (pociąg " + rq.ForTrain + ")" : "")} – Pzk {id}", "Blokada liniowa", id, "req" + id, () => bs.Req == rq);
            }
        }
    }

    CmdResult BlockCmd(string code, string id, int e)
    {
        if (e < 0) { var bb = L.Blocks[id]; return new(false, $"Szlak {id} wewnętrzny – podaj, z którego posterunku wyjazd: " + string.Join(" albo ", bb.Ends.Select(x => $"„{id} {SystemNadrzedny.Codes[code].Code} {x.Post}”")) + " (albo E / W – w prawo / w lewo na obrazie)"); }
        var b = L.Blocks[id]; var bs = Blocks[id]; var my = b.Ends[e];
        if (code is "WBL" or "OWBL" or "PZK" or "ZWBL" or "AZK" && !b.Permission)
            return new(false, b.NoBlock ? $"Szlak {id}: telefoniczne zapowiadanie – brak blokady liniowej i kierunku"
                                        : $"Szlak {id}: {b.TypeShort} – bez bloku pozwolenia, kierunek stały ({DirText(b, b.DefDir)})");
        if (code is "BKON" or "PO" or "POZ" or "DPO" or "DKO" && !b.SemiAuto)
            return new(false, $"Szlak {id}: {b.TypeShort} – przyciski Po, Ko, Poz, dPo, dKo dotyczą blokady półsamoczynnej");
        if (code == "BKON")
        {
            if (bs.AwaitEnd == null) return new(false, "Szlak nie jest zablokowany blokiem początkowym (Po)");
            if (bs.AwaitEnd != e) return new(false, "Blok końcowy (Ko) daje posterunek przyjmujący pociąg");
            if (b.SecsWE.Any(SecOcc)) return new(false, "Szlak zajęty – pociąg nie przybył w całości (sprawdź sygnały końca pociągu)");
            if (bs.FreeSince == 0) return new(false, "Nie stwierdzono przejazdu pociągu przez szlak");
            if (!bs.EntryOk[e] && !bs.DKo[e]) return new(false, "Nie stwierdzono wjazdu na sygnał zezwalający – przy wjeździe na sygnał zastępczy lub rozkaz pisemny użyj dKo przed wjazdem pociągu");
            bs.AwaitEnd = null; bs.EntryOk[e] = false; bs.DKo[e] = false;
            return new(true, "blok końcowy (Ko) – szlak wolny, blok początkowy sąsiada odblokowany");
        }
        if (code == "PO")
        {
            if (bs.AwaitEnd != null) return new(false, "Szlak już zablokowany blokiem początkowym");
            if (bs.Dir != my.Outward) return new(false, "Brak pozwolenia (kierunku) na wyprawienie pociągu z tej strony");
            if (!bs.PoReady[e])
                return new(false, bs.Pwl[e] ? "Sygnał zezwalający na wyjazd jeszcze wyświetlony – Po po jego wygaszeniu" : "Nie wyprawiono pociągu na sygnał zezwalający – przy wyjeździe na sygnał zastępczy lub rozkaz pisemny użyj dPo");
            int recv = b.Ends.Count > 1 ? 1 - e : -1;
            bs.Pwl[e] = false; bs.PoReady[e] = false; bs.AwaitEnd = recv; bs.FreeSince = 0; bs.EndAlarmed = false;
            if (recv >= 0) { bs.EntryOk[recv] = false; bs.DKo[recv] = false; }
            return new(true, "blok początkowy (Po) – szlak zablokowany do bloku końcowego (Ko) " + (recv < 0 ? b.Nbr : "na drugim końcu"));
        }
        if (code == "DPO")
        {
            if (bs.AwaitEnd != null) return new(false, "Szlak już zablokowany blokiem początkowym");
            bs.Pwl[e] = false; bs.PoReady[e] = true; bs.DPoCount++;
            return new(true, $"dPo – przeciwwtórność odwzbudzona, możliwy blok początkowy (licznik dPo: {bs.DPoCount})");
        }
        if (code == "DKO")
        {
            if (bs.AwaitEnd != e) return new(false, "dKo przed wjazdem pociągu, dla którego szlak zablokowano blokiem początkowym");
            bs.DKo[e] = true; bs.DKoCount++;
            return new(true, $"dKo – wjazd na sygnał zastępczy / rozkaz pisemny; po wjeździe pociągu daj Ko (licznik dKo: {bs.DKoCount})");
        }
        if (code == "POZ")
        {
            // blok pozwolenia: stacja mająca pozwolenie oddaje je sąsiedniej (odpowiedź na Wbl albo z własnej inicjatywy)
            if (bs.AwaitEnd != null) return new(false, "Szlak zablokowany – pociąg w drodze");
            if (bs.Pwl[e]) return new(false, "Przeciwwtórność liniowa (Pwl) – najpierw blok początkowy");
            if (bs.Dir != 0 && bs.Dir != my.Outward && bs.Req == null) return new(false, "Pozwolenie ma druga strona");
            if (b.Ends.Count < 2 && !b.Internal && bs.Dir != my.Outward && bs.Req == null) return new(false, "Brak pozwolenia do oddania");
            { var why = BlockConds(b); if (why.Count > 0) return new(false, why[0]); }
            bs.Dir = -my.Outward; bs.Req = null;
            return new(true, "blok pozwolenia (Poz) – pozwolenie oddane, kierunek PRZYJAZD");
        }
        switch (code)
        {
            case "WBL":
                if (bs.Dir == my.Outward && !bs.Busy) return new(false, "Kierunek WYJAZD już ustawiony");
                if (b.Internal)
                {
                    // szlak między posterunkami LCS – oba końce obsługuje ten sam dyżurny: kierunek ustawiany jednym poleceniem
                    if (bs.Busy) return new(false, "Trwa zmiana stanu blokady");
                    { var why = BlockConds(b); if (why.Count > 0) return new(false, why[0] + " (§20 ust. 4)"); }
                    bs.Dir = my.Outward; bs.Req = null;
                    var to = b.Ends.Count > 1 ? b.Ends[1 - e].Post : "";
                    return new(true, $"kierunek: wyjazd z {L.Posts.GetValueOrDefault(my.Post) ?? my.Post} do {L.Posts.GetValueOrDefault(to) ?? to} (szlak wewnętrzny LCS)");
                }
                if (bs.Req != null) return new(false, bs.Req.EndIdx == e ? "Żądanie już wysłane" : "Oczekuje żądanie z przeciwnej strony – Pzk");
                if (bs.Busy) return new(false, "Trwa zmiana stanu blokady");
                { var why = BlockConds(b); if (why.Count > 0) return new(false, why[0] + " (§20 ust. 4)"); }
                bs.Req = new BlockReq { EndIdx = e, At = T };
                return new(true, b.Internal ? $"żądanie kierunku WYJAZD – oczekuje na pozwolenie (Pzk) od strony {Station.Lbl(b.Ends[1 - e].EntrySig)}" : "żądanie pozwolenia wysłane do " + b.Nbr);
            case "OWBL":
                if (bs.Req == null || bs.Req.EndIdx != e) return new(false, "Brak własnego żądania");
                bs.Req = null; return new(true, "żądanie odwołane");
            case "PZK":
                if (bs.Req == null || bs.Req.EndIdx == e) return new(false, "Brak żądania pozwolenia ze strony przeciwnej");
                { var why = BlockConds(b); if (why.Count > 0) return new(false, why[0] + " (§20 ust. 4)"); }
                bs.Dir = -my.Outward; bs.Req = null;
                return new(true, "dano pozwolenie – kierunek PRZYJAZD");
            case "ZWBL":
                if (bs.Dir == 0) return new(false, "Blokada w stanie neutralnym");
                { var why = BlockConds(b); if (why.Count > 0) return new(false, why[0]); }
                bs.ReleaseUntil = T + Cfg.BlockRelease; return new(true, "zwalnianie kierunku");
            case "STOP": bs.StopEnd[e] = true; return new(true, "wyjazd na szlak zamknięty (lampka STOP)");
            case "OSTOP": bs.StopEnd[e] = false; return new(true, "odwołano zamknięcie wyjazdu");
            case "AZK":
                bs.AzkUntil = T + Cfg.AzkTime; bs.AzkDir = -my.Outward; bs.Req = null;
                return new(true, "awaryjna zmiana kierunku na PRZYJAZD");
        }
        return new(false, "Nieobsługiwane polecenie blokady");
    }
}
