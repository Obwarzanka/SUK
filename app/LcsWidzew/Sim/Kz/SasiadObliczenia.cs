using LcsWidzew.Model;

namespace LcsWidzew.Sim;

public partial class KomputerZaleznosci
{
    // Dotychczasowe odpowiedzi automatycznego sąsiada (Otoczenie), na prywatnym stanie proponowanym.
    // Adapter zamraża tylko jego meldunek i rozkład. Nie jest to jeszcze kontrakt stycznej KZ–KZ.
    void BlokPoczatkowySasiada(string id, BlockDef b, BlockState bs, bool occ)
    {
        if (bs.AwaitEnd == null && occ && bs.Dir != 0 && !b.Internal && EndIdxForDir(b, -bs.Dir) >= 0)
        {
            int recv = EndIdxForDir(b, -bs.Dir);
            bs.AwaitEnd = recv; bs.FreeSince = 0; bs.EndAlarmed = false; bs.EntryOk[recv] = false; bs.DKo[recv] = false;
            Log("Zdarzenie", $"{b.Nbr}: blok początkowy – szlak {id} zablokowany do czasu bloku końcowego", "Blokada liniowa", id);
        }
    }

    void BlokKoncowySasiada(string id, BlockDef b, BlockState bs)
    {
        if (T - bs.FreeSince > Simulator.Cfg.NbrDelay) { bs.AwaitEnd = null; Log("Zdarzenie", $"{b.Nbr}: blok końcowy – szlak {id} wolny", "Blokada liniowa", id); }
    }

    void OdpowiedzSasiada(string id, BlockDef b, BlockState bs, BlockReq rq)
    {
        if (T - rq.At <= Simulator.Cfg.NbrDelay) return;
        if (bs.NbrSilent)
        {
            if (T - rq.At > 60 && !rq.Alarmed)
            {
                rq.Alarmed = true;
                Alarm($"Blokada {id}: brak odpowiedzi posterunku {b.Nbr} na żądanie kierunku – zmiana kierunku w trybie normalnym niemożliwa (§20 ust. 5)",
                    "Blokada liniowa", id, "nbrsilent" + id, warunek: WarunekAlarmu.ZadanieBlokady);
            }
        }
        else if (ruchObliczenia && oczekujacySasiad[id])
        {
            bs.Req = null; Log("Ruch", $"{b.Nbr}: odmowa pozwolenia na szlak {id} – pociąg w drodze do {S.PostName(b.Ends[0].Post)}", "Blokada liniowa", id);
        }
        else if (BlockConds(b).Count == 0)
        {
            bs.Dir = b.Ends[0].Outward; bs.Req = null;
            Log("Zdarzenie", $"{b.Nbr}: dano pozwolenie – kierunek WYJAZD na szlak {id}", "Blokada liniowa", id);
        }
    }
}
