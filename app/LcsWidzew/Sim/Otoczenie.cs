using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Otoczenie obszaru LCS: posterunki sąsiednie spoza LCS – druga strona blokady liniowej (odpowiedź na Wbl, blok początkowy
/// i końcowy półsamoczynnej), żądanie pozwolenia (Pzk) i wyprawianie pociągów z rozkładu na szlak.
/// Komputer zależności wymienia z nim komunikaty blokady; o pociągach wie tylko otoczenie.
/// </summary>
public class Otoczenie
{
    readonly Simulator S;
    public Otoczenie(Simulator s) { S = s; }

    /// <summary>
    /// Posterunek sąsiedni ma wyprawić pociąg z rozkładu na szlak: żąda pozwolenia (Pzk), gdy kierunek jest przeciwny,
    /// i wyprawia pociąg, gdy szlak jest wolny i blokada pozwala. Zwraca true, gdy pociąg wyprawiono.
    /// </summary>
    public bool Wyprawienie(TimetableEntry q)
    {
        var b = S.L.Blocks[q.From]; var bs = S.Blocks[q.From]; int inward = -b.Ends[0].Outward;
        bool ok = (b.NoBlock ? !b.SecsWE.Any(S.SecOcc) : bs.Dir == inward) && bs.AwaitEnd == null && !bs.Busy && !S.SecOcc(FarSec(b)) && !EmergingAtFar(q.From) && !bs.StopEnd[0];
        if (b.Permission && bs.Dir != inward && bs.Req == null && !bs.Busy)
        {
            bs.Req = new BlockReq { EndIdx = -1, At = S.T, ForTrain = q.Num };
            S.Log("Ruch", $"{b.Nbr}: żądanie pozwolenia na szlak {q.From} dla pociągu {q.Num} (Pzk {q.From})", "Blokada liniowa", q.From);
        }
        if (!ok) return false;
        S.Trains.SpawnFromSzlak(q);
        return true;
    }

    /// <summary>
    /// Pociąg wyprawiony przez sąsiada jest na części szlaku poza obszarem LCS (przed granicą) – sąsiad melduje szlak zajęty
    /// (blokada liniowa). Odstępy w obszarze LCS komputer zależności zna z kart odcinków.
    /// </summary>
    public bool SzlakZajetyUSasiada(string id)
    {
        var b = S.L.Blocks[id];
        return b.Far != null && S.Trains != null && S.Trains.List.Any(t => t.Segs[0].E == null && t.Segs[0].To == b.Far && t.Head - t.Len < t.Segs[0].Len);
    }

    bool EmergingAtFar(string id)
    {
        var b = S.L.Blocks[id];
        return S.Trains != null && S.Trains.List.Any(t => t.Segs[0].E == null && t.Segs[0].To == b.Far && t.Head - t.Len < t.Segs[0].Len + 50);
    }
    /// <summary>Czy posterunek sąsiedni ma w najbliższym czasie pociąg do wyprawienia na ten szlak.</summary>
    public bool NbrPending(string id, double within) => S.Rozklad.Queue.Any(q => q.From == id && !q.Done && q.T <= S.T + within && q.StartSec == null);

    string FarSec(BlockDef b) => S.L.Nodes[b.Far].X < S.L.Nodes[b.Ends[0].Boundary].X ? b.SecsWE[0] : b.SecsWE[^1];

    // ------------------------------------------------------------------ komunikaty blokady liniowej od posterunku sąsiedniego
    /// <summary>Blokada półsamoczynna: pociąg wyprawiony przez sąsiada wjechał na szlak – blok początkowy sąsiada.</summary>
    public void BlokPoczatkowy(string id, BlockDef b, BlockState bs, bool occ)
    {
        if (bs.AwaitEnd == null && occ && bs.Dir != 0 && !b.Internal && S.EndIdxForDir(b, -bs.Dir) >= 0)
        {
            int recv = S.EndIdxForDir(b, -bs.Dir);
            bs.AwaitEnd = recv; bs.FreeSince = 0; bs.EndAlarmed = false; bs.EntryOk[recv] = false; bs.DKo[recv] = false;
            S.Log("Zdarzenie", $"{b.Nbr}: blok początkowy – szlak {id} zablokowany do czasu bloku końcowego", "Blokada liniowa", id);
        }
    }

    /// <summary>Blokada półsamoczynna: pociąg wyprawiony przez LCS przybył do sąsiada (szlak wolny) – blok końcowy sąsiada.</summary>
    public void BlokKoncowy(string id, BlockDef b, BlockState bs)
    {
        if (S.T - bs.FreeSince > Simulator.Cfg.NbrDelay) { bs.AwaitEnd = null; S.Log("Zdarzenie", $"{b.Nbr}: blok końcowy – szlak {id} wolny", "Blokada liniowa", id); }
    }

    /// <summary>Odpowiedź posterunku spoza obszaru LCS na żądanie kierunku (Wbl): pozwolenie, odmowa albo brak odpowiedzi (usterka).</summary>
    public void OdpowiedzNaWbl(string id, BlockDef b, BlockState bs, BlockReq rq)
    {
        if (S.T - rq.At <= Simulator.Cfg.NbrDelay) return;
        if (bs.NbrSilent)
        {
            if (S.T - rq.At > 60 && !rq.Alarmed) { rq.Alarmed = true; S.Alarm($"Blokada {id}: brak odpowiedzi posterunku {b.Nbr} na żądanie kierunku – zmiana kierunku w trybie normalnym niemożliwa (§20 ust. 5)", "Blokada liniowa", id, "nbrsilent" + id, () => bs.Req != null); }
        }
        else if (S.Trains != null && NbrPending(id, 60))
        { bs.Req = null; S.Log("Ruch", $"{b.Nbr}: odmowa pozwolenia na szlak {id} – pociąg w drodze do {S.PostName(b.Ends[0].Post)}", "Blokada liniowa", id); }
        else if (S.BlockConds(b).Count == 0)
        { bs.Dir = b.Ends[0].Outward; bs.Req = null; S.Log("Zdarzenie", $"{b.Nbr}: dano pozwolenie – kierunek WYJAZD na szlak {id}", "Blokada liniowa", id); }
    }
}
