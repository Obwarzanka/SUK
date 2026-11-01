using LcsWidzew.Model;
using LcsWidzew.Sim;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>S02A.04: zaufane wewnętrzne wrappery zachowują dawną obsługę Pending bez stanowiska.</summary>
public sealed class PendingInternalCompatibilityTests
{
    static (Simulator Sim, Desk Desk, string Signal) Initiated()
    {
        var s = new Simulator(Station.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json")));
        s.User = new User { Name = "Operator wewnętrzny", Role = "instruktor" };
        var sig = s.L.Signals.Values.First(x => x.IsTrain && x.Type != "odstepowy");
        var desk = s.AddDesk("A", "Stanowisko A", false);
        desk.User = new User { Name = "Operator stanowiska A", Role = "dyżurny ruchu" };
        desk.Page = s.L.Pages.First(p => p.Posts.Contains(sig.Post)).Id;
        s.Control[sig.Post] = desk.Id;
        Assert.True(s.ExecAs(desk, sig.Name + " SZ").Ok);
        Assert.Equal(desk.Id, s.Pending.Desk);
        return (s, desk, sig.Name);
    }

    [Theory]
    [InlineData("OPS")]
    [InlineData("CancelPending")]
    public void DefaultInternalCancellationRemovesDeskPendingWithoutReplacingLocalContext(string operation)
    {
        var (s, desk, _) = Initiated();
        var localUser = s.User;
        string localPage = s.Page;
        Assert.Null(s.Nadrzedny.Kontekst().DeskId);

        if (operation == "OPS") Assert.True(s.Exec("OPS").Ok);
        else s.CancelPending();

        Assert.Null(s.Pending);
        Assert.Empty(s.BookDrafts);
        Assert.Equal(0, s.Stats.Special);
        Assert.Equal(localUser.Name, s.Events.Last(e => e.Zrodlo == "PC").Operator);
        Assert.Same(localUser, s.User);
        Assert.Equal(localPage, s.Page);
        Assert.Equal("Operator stanowiska A", desk.User.Name);
    }

    [Fact]
    public void DefaultInternalConfirmationCanCompleteDeskPendingWithItsOwnExplicitSnapshot()
    {
        var (s, desk, signal) = Initiated();
        var localUser = s.User;
        string localPage = s.Page;
        s.T = Simulator.Cfg.SpecialMinWait;
        int before = s.Events.Count;

        Assert.True(s.ConfirmPending(signal).Ok);

        Assert.Null(s.Pending);
        Assert.Equal(1, s.Stats.Special);
        Assert.Equal(1, s.Stats.Cmds);
        Assert.Null(Assert.Single(s.BookDrafts).Desk); // Stare wewnętrzne wywołanie nie wskazywało CurDesk.
        Assert.All(s.Events.Skip(before).Where(e => e.Zrodlo == "PC" || e.Kategoria is "Uwaga" or "Ocena"),
            e => Assert.Equal(localUser.Name, e.Operator));
        Assert.Same(localUser, s.User);
        Assert.Equal(localPage, s.Page);
        Assert.Equal("Operator stanowiska A", desk.User.Name);
        Assert.False(s.ConfirmPending(signal).Ok);
        Assert.Single(s.BookDrafts);
    }
}
