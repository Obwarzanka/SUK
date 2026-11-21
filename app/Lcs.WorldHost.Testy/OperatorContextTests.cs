using LcsWidzew.Model;
using LcsWidzew.Sim;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>S02A.04: jawny kontekst operatora, jeden parser i obsługa poleceń specjalnych dla właściwego stanowiska.</summary>
public sealed class OperatorContextTests
{
    static Simulator New()
    {
        var s = new Simulator(Station.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json")));
        s.User = new User { Name = "Operator głównego stanowiska", Role = "instruktor" };
        return s;
    }

    static Desk AddDesk(Simulator s, string id, string post, string? name = null)
    {
        var d = s.AddDesk(id, "Stanowisko " + id, false);
        d.Page = s.L.Pages.First(p => p.Posts.Contains(post)).Id;
        d.User = new User { Name = name ?? "Operator " + id, Role = "dyżurny ruchu" };
        s.Control[post] = d.Id;
        return d;
    }

    static SignalDef TrainSignal(Simulator s) => s.L.Signals.Values.First(x => x.IsTrain && x.Type != "odstepowy");
    static List<EventRecord> PcEvents(Simulator s) => s.Events.Where(e => e.Zrodlo == "PC").ToList();

    [Fact]
    public void ExplicitContextIsSnapshotOfDeskValuesAndDefaultContextRemainsLocal()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        var originalUser = s.User;
        string originalPage = s.Page;
        var context = s.Nadrzedny.Kontekst(d);

        d.User.Name = "Zmieniony operator";
        d.User.Role = "obserwator";
        d.Page = "inny-obraz";
        d.Id = "zmieniony-identyfikator";

        Assert.Equal("A", context.DeskId);
        Assert.Equal("Operator A", context.Nazwa);
        Assert.Equal("dyżurny ruchu", context.Rola);
        Assert.True(context.Zalogowany);
        Assert.Equal(s.L.Pages.First(p => p.Posts.Contains(sig.Post)).Id, context.Page);
        var local = s.Nadrzedny.Kontekst();
        Assert.Null(local.DeskId);
        Assert.Equal(originalPage, local.Page);
        Assert.Equal(originalUser.Name, local.Nazwa);
        Assert.Equal(originalUser.Role, local.Rola);
        Assert.Same(originalUser, s.User);
    }

    [Fact]
    public void ExecAsNeverReplacesGlobalUserOrPageEvenInsideEventCallback()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        var localUser = s.User;
        string localPage = s.Page;
        int callbacks = 0;
        s.EventAdded += e =>
        {
            if (e.Zrodlo != "PC") return;
            callbacks++;
            Assert.Same(localUser, s.User);
            Assert.Equal(localPage, s.Page);
            Assert.Equal(d.User.Name, e.Operator);
        };

        Assert.True(s.ExecAs(d, sig.Name + " STOP").Ok);

        Assert.Equal(3, callbacks);
        Assert.Same(localUser, s.User);
        Assert.Equal(localPage, s.Page);
        Assert.True(s.Sigs[sig.Name].Closed);
    }

    [Fact]
    public void NestedCommandFromOtherDeskDoesNotChangeRemainingOuterLogOperators()
    {
        var s = New();
        var first = TrainSignal(s);
        var second = s.L.Signals.Values.First(x => x.IsTrain && x.Type != "odstepowy" && x.Post != first.Post);
        var a = AddDesk(s, "A", first.Post);
        var b = AddDesk(s, "B", second.Post);
        var localUser = s.User;
        string localPage = s.Page;
        bool nested = false;
        s.EventAdded += e =>
        {
            Assert.Same(localUser, s.User);
            Assert.Equal(localPage, s.Page);
            if (nested || e.Zrodlo != "PC" || e.Operator != a.User.Name) return;
            nested = true;
            Assert.True(s.ExecAs(b, second.Name + " STOP").Ok);
        };

        Assert.True(s.ExecAs(a, first.Name + " STOP").Ok);

        Assert.True(nested);
        Assert.Equal(new[] { "Operator A", "Operator B", "Operator B", "Operator B", "Operator A", "Operator A" },
            PcEvents(s).Select(e => e.Operator));
        Assert.True(s.Sigs[first.Name].Closed);
        Assert.True(s.Sigs[second.Name].Closed);
    }

    [Fact]
    public void DeskMutationDuringCallbackAffectsNextCommandOnly()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        string initialPage = d.Page;
        bool changed = false;
        s.EventAdded += e =>
        {
            if (changed || e.Zrodlo != "PC") return;
            changed = true;
            d.User.Name = "Operator po zmianie";
            d.User.Role = "instruktor";
            d.Page = s.L.Pages.First(p => p.Id != initialPage).Id;
        };

        Assert.True(s.ExecAs(d, sig.Name + " STOP").Ok);
        Assert.All(PcEvents(s), e => Assert.Equal("Operator A", e.Operator));
        Assert.True(s.ExecAs(d, sig.Name + " OSTOP").Ok);

        Assert.Equal(6, PcEvents(s).Count);
        Assert.All(PcEvents(s).Skip(3), e => Assert.Equal("Operator po zmianie", e.Operator));
        Assert.False(s.Sigs[sig.Name].Closed);
    }

    [Fact]
    public void CallbackExceptionDoesNotChangeGlobalContextOrNextCommandOperator()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        var localUser = s.User;
        string localPage = s.Page;
        Action<EventRecord> fail = _ => throw new InvalidOperationException("Błąd kontrolny odbiorcy zdarzenia");
        s.EventAdded += fail;

        Assert.Throws<InvalidOperationException>(() => s.ExecAs(d, sig.Name + " STOP"));

        s.EventAdded -= fail;
        Assert.Same(localUser, s.User);
        Assert.Equal(localPage, s.Page);
        Assert.True(s.ExecAs(d, sig.Name + " STOP").Ok);
        Assert.All(PcEvents(s), e => Assert.Equal("Operator A", e.Operator));
    }

    [Fact]
    public void SameLocalSignalNameUsesExplicitPageAndControlOfEachDesk()
    {
        var s = New();
        var duplicate = s.L.Signals.Values.Where(x => x.IsTrain && x.Type != "odstepowy")
            .GroupBy(x => x.Label).First(g => g.Select(x => x.Post).Distinct().Count() > 1).ToList();
        var first = duplicate[0];
        var second = duplicate.First(x => x.Post != first.Post);
        var a = AddDesk(s, "A", first.Post);
        var b = AddDesk(s, "B", second.Post);
        var ca = s.Nadrzedny.Kontekst(a);
        var cb = s.Nadrzedny.Kontekst(b);
        s.Page = b.Page;

        Assert.Equal(first.Name, s.Nadrzedny.Resolve(ca, first.Label, "sig"));
        Assert.Equal(second.Name, s.Nadrzedny.Resolve(cb, second.Label, "sig"));
        Assert.True(s.Nadrzedny.Exec(ca, first.Label + " STOP").Ok);
        Assert.True(s.Sigs[first.Name].Closed);
        Assert.False(s.Sigs[second.Name].Closed);
        Assert.True(s.Nadrzedny.Exec(cb, second.Label + " STOP").Ok);
        Assert.True(s.Sigs[second.Name].Closed);
        Assert.False(s.Nadrzedny.Exec(ca, second.Name + " OSTOP").Ok);
        Assert.True(s.Sigs[second.Name].Closed);
    }

    [Fact]
    public void InternalBlockCommandsChooseEndFromExplicitDeskContext()
    {
        var s = New();
        var block = s.L.Blocks.Values.First(b => b.Internal && b.Ends.Count == 2 && b.Ends[0].Post != b.Ends[1].Post);
        var a = AddDesk(s, "A", block.Ends[0].Post);
        var b = AddDesk(s, "B", block.Ends[1].Post);

        Assert.True(s.ExecAs(a, block.Id + " STOP").Ok);
        Assert.Equal(new[] { true, false }, s.Blocks[block.Id].StopEnd);
        Assert.True(s.ExecAs(b, block.Id + " STOP").Ok);
        Assert.Equal(new[] { true, true }, s.Blocks[block.Id].StopEnd);
        Assert.True(s.ExecAs(a, block.Id + " OSTOP").Ok);
        Assert.Equal(new[] { false, true }, s.Blocks[block.Id].StopEnd);
    }

    [Fact]
    public void OnePendingCommandCannotBeConfirmedOrCancelledByOtherDesk()
    {
        var s = New();
        var sig = TrainSignal(s);
        var a = AddDesk(s, "A", sig.Post);
        var b = AddDesk(s, "B", sig.Post);
        s.Control[sig.Post] = a.Id;
        var ca = s.Nadrzedny.Kontekst(a);
        var cb = s.Nadrzedny.Kontekst(b);

        Assert.True(s.Nadrzedny.Exec(ca, sig.Name + " SZ").Ok);
        var pending = s.Pending;
        Assert.NotNull(pending);
        Assert.Equal(a.Id, pending.Desk);
        Assert.False(s.Nadrzedny.ConfirmPending(ca, sig.Name).Ok);
        s.T = Simulator.Cfg.SpecialMinWait;
        Assert.False(s.Nadrzedny.ConfirmPending(cb, sig.Name).Ok);
        s.Nadrzedny.CancelPending(cb);
        Assert.Same(pending, s.Pending);
        Assert.Empty(s.BookDrafts);
        Assert.True(s.Nadrzedny.ConfirmPending(ca, sig.Name).Ok);

        Assert.Null(s.Pending);
        Assert.Equal(1, s.Stats.Cmds);
        Assert.Equal(1, s.Stats.Special);
        Assert.Equal(a.Id, Assert.Single(s.BookDrafts).Desk);
        Assert.False(s.Nadrzedny.ConfirmPending(ca, sig.Name).Ok);
        Assert.Single(s.BookDrafts);
    }

    [Fact]
    public void InitiatingDeskCanCancelPendingWithItsExplicitContext()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        var context = s.Nadrzedny.Kontekst(d);
        Assert.True(s.Nadrzedny.Exec(context, sig.Name + " SZ").Ok);

        s.Nadrzedny.CancelPending(context);

        Assert.Null(s.Pending);
        Assert.Empty(s.BookDrafts);
        Assert.Equal(0, s.Stats.Special);
        Assert.Equal("Operator A", s.Events.Last(e => e.Zrodlo == "PC").Operator);
    }

    [Fact]
    public void SpecialCompletionIsAddressedToInitiatingLocalSessionOnly()
    {
        var s = New();
        var sig = TrainSignal(s);
        var a = AddDesk(s, "A", sig.Post);
        var b = AddDesk(s, "B", sig.Post);
        s.Control[sig.Post] = a.Id;
        var first = new LocalSession(s, a);
        var second = new LocalSession(s, b);
        int firstNotifications = 0, secondNotifications = 0, legacyNotifications = 0;
        var addressed = new List<(string Desk, string Line)>();
        first.SpecialExecuted += () => firstNotifications++;
        second.SpecialExecuted += () => secondNotifications++;
        s.SpecialExecuted += _ => legacyNotifications++;
        s.SpecialExecutedForDesk += (id, line) => addressed.Add((id, line));
        Assert.True(first.Exec(sig.Name + " SZI").Ok);
        s.T = Simulator.Cfg.SpecialMinWait;

        Assert.True(first.Exec(sig.Name + " SZ").Ok);

        Assert.Equal(1, firstNotifications);
        Assert.Equal(0, secondNotifications);
        Assert.Equal(1, legacyNotifications);
        Assert.Equal((a.Id, sig.Name + " SZ"), Assert.Single(addressed));
        Assert.Equal(a.Id, Assert.Single(s.BookDrafts).Desk);
    }

    [Fact]
    public void BookPhoneAndLoginUseTheirDeskOperatorWithoutChangingLocalUser()
    {
        var s = New();
        var d = AddDesk(s, "A", TrainSignal(s).Post);
        var localUser = s.User;
        string localPage = s.Page;

        s.Phone(d, "*", "Wiadomość kontrolna");
        Assert.Equal("Operator A", s.Events.Last().Operator);
        s.AddBook(d, "Wpis kontrolny", "Automatyk próby");
        Assert.Equal("Operator A", Assert.Single(s.Book).Uzytkownik);
        Assert.Equal("Operator A", s.Events.Last().Operator);
        s.Login(d, new User { Name = "Nowy operator A", Role = "dyżurny ruchu" });
        Assert.Equal("Nowy operator A", s.Events.Last().Operator);
        Assert.Equal("Nowy operator A", d.User.Name);
        Assert.Same(localUser, s.User);
        Assert.Equal(localPage, s.Page);
    }

    [Fact]
    public void SzAssessmentEffectsKeepCommandOperatorAndAutomaticEventsStayUnattributed()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        Assert.True(s.ExecAs(d, sig.Name + " SZ").Ok);
        s.T = Simulator.Cfg.SpecialMinWait;
        int before = s.Events.Count;

        Assert.True(s.ConfirmAs(d, sig.Name).Ok);

        var assessments = s.Events.Skip(before).Where(e => e.Kategoria is "Uwaga" or "Ocena").ToList();
        Assert.NotEmpty(assessments);
        Assert.All(assessments, e => Assert.Equal("Operator A", e.Operator));
        s.Log("Zdarzenie", "Samoczynna zmiana kontrolna", "Zwrotnica", s.L.Switches.Keys.First());
        Assert.Equal("", s.Events.Last().Operator);
    }

    [Fact]
    public void LoggedOutExplicitContextCannotBorrowHostLogin()
    {
        var s = New();
        var sig = TrainSignal(s);
        var d = AddDesk(s, "A", sig.Post);
        d.User = null!;
        var context = s.Nadrzedny.Kontekst(d);

        Assert.False(context.Zalogowany);
        Assert.False(s.Nadrzedny.Exec(context, sig.Name + " STOP").Ok);
        Assert.False(s.Sigs[sig.Name].Closed);
        Assert.Equal("", PcEvents(s).Last().Operator);
        Assert.NotNull(s.User);
    }
}
