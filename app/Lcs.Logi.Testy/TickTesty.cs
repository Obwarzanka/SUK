using System.Diagnostics;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>N43 „brak blokady ticku” i B05: koszt emisji na wątku wywołującym przy działającym i przy zablokowanym zapisie.</summary>
public class TickTesty
{
    [Fact]
    public void TickNieCzekaNaZablokowanyZapis()
    {
        var sink = new SinkTestowy(zablokowany: true);   // symulacja zatrzymanego dysku: worker stoi w sinku
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("tick"), sink));
        var log = s.Modul("Test.Tick", "1.0.0");
        const int ticki = 2000, naTick = 10;
        var emisje = new long[ticki * naTick];
        var tick = new long[ticki];
        int k = 0;
        for (int t = 0; t < ticki; t++)
        {
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < naTick; i++)
            {
                long e0 = Stopwatch.GetTimestamp();
                ZdarzeniaProcesu.Zatrzymano(log, k);
                emisje[k++] = Stopwatch.GetTimestamp() - e0;
            }
            tick[t] = Stopwatch.GetTimestamp() - t0;
        }
        var pe = ProbaLogow.Statystyka(emisje, 0);
        var pt = ProbaLogow.Statystyka(tick, 0);
        var st = s.Stan();
        Pom.Pomiar("B05_tick_zablokowany_zapis", $"emisja: {pe}; tick({naTick} emisji): {pt}; przyjete={st.Przyjete} straty={st.StratyRazem}");
        Assert.Equal(ticki * naTick, st.Przyjete + st.StratyRazem);
        Assert.True(pt.MaxNs < 200_000_000, $"tick czekał {pt.MaxNs} ns");
        Assert.True(pe.P99Ns < 1_000_000, $"p99 emisji {pe.P99Ns} ns");
        sink.Brama.Set();
        s.Zatrzymaj();
    }

    [Fact]
    public void KosztEmisjiPrzyZapisieDoPliku()
    {
        var kat = Pom.NowyKatalog("pomiar");
        var s = SilnikLogow.Uruchom(Pom.Opcje(kat));
        var log = s.Modul("Test.Pomiar", "1.0.0");
        ProbaLogow.Pomiar(log, 20, 100, 1);                 // rozgrzewka (JIT, szablony)
        var p = ProbaLogow.Pomiar(log, 100, 100, 2);
        var st = s.Zatrzymaj();
        Pom.Pomiar("B05_emisja_plik", $"{p}; przyjete={st.Przyjete} straty={st.StratyRazem} max_w_kolejce={st.MaxWKolejce}");
        // rytm 100 rekordów / 2 ms (~50 tys./s) jest powyżej typowego wolumenu; przy wolnym hoście kolejka może się zapełnić –
        // wtedy straty są policzone co do rekordu, a emisja dalej nie czeka
        Assert.Equal(12_000, st.Przyjete + st.StratyRazem);
        Assert.True(p.P99Ns < 1_000_000, $"p99 {p.P99Ns} ns");
    }
}
