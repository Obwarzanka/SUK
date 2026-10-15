using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Elektroniczna blokada samoczynna dwukierunkowa (Ie-20 § 20, Ie-104 § 8 pkt 17, § 12 pkt 3–4).
/// Polecenia: Wbl (żądanie kierunku WYJAZD), oWbl, Pzk (pozwolenie – kierunek PRZYJAZD), Zwbl (zwolnienie kierunku),
/// Stop / oStop (zamknięcie wyjazdu na szlak), AZK (awaryjna zmiana kierunku – polecenie specjalne).
/// </summary>
public partial class Simulator
{
    /// <summary>Koniec blokady obsługiwany z danego obrazu (dla szlaków wewnętrznych – ten po stronie wyświetlanego posterunku).</summary>
    public int EndIdx(BlockDef b, string page)
    {
        var pg = L.Pages.FirstOrDefault(p => p.Id == page);
        for (int i = 0; i < b.Ends.Count; i++) if (pg != null && pg.Posts.Contains(b.Ends[i].Post)) return i;
        return 0;
    }
    public int EndIdxForDir(BlockDef b, int dir) { for (int i = 0; i < b.Ends.Count; i++) if (b.Ends[i].Outward == dir) return i; return -1; }

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
        if (Trains != null && Trains.OnBlock(b.Id)) why.Add("pociąg na szlaku " + b.Id);
        return why;
    }

    /// <summary>Lampka TOR – szlak wolny, kierunek ustawiony, wszystkie sygnały odstępowe „wolna droga”.</summary>
    public bool TorLamp(BlockDef b)
    {
        var bs = Blocks[b.Id];
        if (!b.Permission) return false;
        if (bs.Dir == 0 || bs.Busy) return false;
        if (b.SecsWE.Any(SecOcc)) return false;
        return L.Signals.Values.Where(s => s.Szlak == b.Id && s.Type == "odstepowy" && s.Dir == bs.Dir).All(s => Sigs[s.Name].Aspect == Aspect.Train);
    }

    void UpdateBlocks()
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
                if (bs.AwaitEnd == null && occ && bs.Dir != 0)
                {
                    int recv = EndIdxForDir(b, -bs.Dir);
                    bs.AwaitEnd = recv; bs.FreeSince = 0; bs.EndAlarmed = false;
                    Log("Zdarzenie", "Blok początkowy – szlak zablokowany do czasu bloku końcowego" + (recv < 0 ? $" ({b.Nbr})" : ""), "Blokada liniowa", id);
                }
                if (bs.AwaitEnd is int ae)
                {
                    if (occ) bs.FreeSince = 0;
                    else
                    {
                        if (bs.FreeSince == 0) { bs.FreeSince = T; if (ae >= 0) Log("Zdarzenie", $"Szlak {id} wolny – pociąg przybył; daj blok końcowy ({id} Bkon)", "Blokada liniowa", id); }
                        if (ae < 0 && T - bs.FreeSince > Cfg.NbrDelay) { bs.AwaitEnd = null; Log("Zdarzenie", $"{b.Nbr}: blok końcowy – szlak {id} wolny", "Blokada liniowa", id); }
                        else if (ae >= 0 && !bs.EndAlarmed && T - bs.FreeSince > Cfg.ReqAlarmAfter)
                        { bs.EndAlarmed = true; var b2 = bs; Alarm($"Blokada {id}: pociąg przybył, brak bloku końcowego – szlak zablokowany ({id} Bkon)", "Blokada liniowa", id, "bk" + id, () => b2.AwaitEnd != null); }
                    }
                }
            }
            var rq = bs.Req;
            if (rq == null) continue;
            // odpowiedź posterunku spoza obszaru LCS na nasze żądanie (Wbl)
            if (!b.Internal && rq.EndIdx == 0 && T - rq.At > Cfg.NbrDelay)
            {
                if (bs.NbrSilent)
                {
                    if (T - rq.At > 60 && !rq.Alarmed) { rq.Alarmed = true; Alarm($"Blokada {id}: brak odpowiedzi posterunku {b.Nbr} na żądanie kierunku – zmiana kierunku w trybie normalnym niemożliwa (§20 ust. 5)", "Blokada liniowa", id, "nbrsilent" + id, () => bs.Req != null); }
                }
                else if (Trains != null && Trains.NbrPending(id, 60))
                { bs.Req = null; Log("Ruch", $"{b.Nbr}: odmowa pozwolenia na szlak {id} – pociąg w drodze do ŁW", "Blokada liniowa", id); }
                else if (BlockConds(b).Count == 0)
                { bs.Dir = b.Ends[0].Outward; bs.Req = null; Log("Zdarzenie", $"{b.Nbr}: dano pozwolenie – kierunek WYJAZD na szlak {id}", "Blokada liniowa", id); }
            }
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
        if (e < 0) return new(false, $"Szlak {id} wewnętrzny – podaj kierunek wyjazdu: „{id} {Codes[code].Code} E” (→ na wschód) albo „{id} {Codes[code].Code} W” (← na zachód)");
        var b = L.Blocks[id]; var bs = Blocks[id]; var my = b.Ends[e];
        if (code is "WBL" or "OWBL" or "PZK" or "ZWBL" or "AZK" && !b.Permission)
            return new(false, b.NoBlock ? $"Szlak {id}: telefoniczne zapowiadanie – brak blokady liniowej i kierunku"
                                        : $"Szlak {id}: {b.TypeShort} – bez bloku pozwolenia, kierunek stały ({(b.DefDir > 0 ? "→ na wschód" : "← na zachód")})");
        if (code == "BKON")
        {
            if (!b.SemiAuto) return new(false, $"Szlak {id}: {b.TypeShort} – blok końcowy dotyczy blokady półsamoczynnej");
            if (bs.AwaitEnd == null) return new(false, "Szlak nie jest zablokowany blokiem początkowym");
            if (bs.AwaitEnd != e) return new(false, "Blok końcowy daje posterunek przyjmujący pociąg");
            if (b.SecsWE.Any(SecOcc)) return new(false, "Szlak zajęty – pociąg nie przybył w całości (sprawdź koniec pociągu)");
            bs.AwaitEnd = null; return new(true, "blok końcowy – szlak wolny");
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
                    return new(true, $"kierunek {(my.Outward > 0 ? "→ na wschód" : "← na zachód")}: wyjazd z {L.Posts.GetValueOrDefault(my.Post) ?? my.Post} do {L.Posts.GetValueOrDefault(to) ?? to} (szlak wewnętrzny LCS)");
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
