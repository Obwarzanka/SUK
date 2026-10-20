using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using LcsWidzew.UI;

namespace LcsWidzew;

public partial class MainWindow
{
    /// <summary>Jawna próba rzeczywistego okna: normalny OnTimer, przyciski tempa i restart. Wyłącznie po #localtest w trybie zrzutu.</summary>
    void TestLocalAdapter(string output)
    {
        var report = new StringBuilder(); int errors = 0;
        void Check(bool value, string name) { report.AppendLine((value ? "OK: " : "BŁĄD: ") + name); if (!value) errors++; }
        string shot = ShotPath; var original = localWorld; var oldSession = ses;
        var originalEpoch = original.Status.WorldRunId;
        try
        {
            Check(localWorld != null && ses.IsHost && sim.T == 0, "lokalny świat przy T=0 bez konta serwera");
            Check(!localWorld.Status.Ready, "gotowość nie jest nadawana przed pierwszym krokiem");
            // Normalny pulpit używa tego samego OnTimer; zrzut pozostaje wyłącznie otoczką testu.
            ShotPath = null; paused = true;
            Thread.Sleep(120); OnTimer(null, EventArgs.Empty);
            Check(sim.T == 0, "pauza zatrzymuje czas");
            Speed_Click(new Button { Tag = "1" }, null); Thread.Sleep(130); OnTimer(null, EventArgs.Empty);
            Check(sim.T >= 0.1 && localWorld.Status.Ready, "przycisk 1× i normalny timer uruchamiają WorldLoop");
            Speed_Click(new Button { Tag = "0" }, null); double stoppedAt = sim.T;
            Thread.Sleep(130); OnTimer(null, EventArgs.Empty);
            Check(sim.T == stoppedAt, "przycisk pauzy nie dodaje kroków");
            Speed_Click(new Button { Tag = "10" }, null); Thread.Sleep(130); OnTimer(null, EventArgs.Empty);
            Check(sim.T - stoppedAt >= 0.9, "przycisk 10× przyspiesza ten sam cykl");
            paused = true;
            string point = sim.L.Switches.Keys.First(); double beforeCommand = sim.T;
            Check(Run(point + " Minus").Ok && sim.T == beforeCommand, "polecenie dyżurnego w pauzie ma wynik bez kroku czasu");
            var oldInspector = new InstructorWindow(sim, _ => { }) { Owner = this }; oldInspector.Show();
            var oldRoutes = new RoutesWindow(ses.RouteTable, sim) { Owner = this }; oldRoutes.Show();
            ShotPath = shot;
            Start(scenario, new User { Name = "Test", Role = "dyżurny ruchu" });
            Check(original.IsDisposed && original.Status.State == Lcs.WorldHost.WorldState.Stopped, "restart zamyka poprzednią epokę");
            Check(!oldInspector.IsVisible && !oldRoutes.IsVisible, "restart zamyka okna przechowujące poprzedni świat");
            bool staleRejected = false; try { oldSession.Exec("KW"); } catch (ObjectDisposedException) { staleRejected = true; }
            Check(staleRejected, "stara sesja nie może wykonać polecenia po resecie");
            Check(localWorld.Status.WorldRunId != originalEpoch && sim.T == 0, "nowy świat ma odrębną epokę i nie dziedziczy długu czasu");
            localWorld.AdvanceTo(1);
            Check(localWorld.Status.Ready && localWorld.Status.ServerTick == 11, "tryb zrzutu zachowuje historyczny warunek czasu double i wspólny krok 0,1 s");
            netWin = new NetWindow(this) { Owner = this }; netWin.Show();
            static IEnumerable<Button> Buttons(DependencyObject root)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                {
                    if (child is Button button) yield return button;
                    foreach (var nested in Buttons(child)) yield return nested;
                }
            }
            var oldControl = Buttons(netWin).First(b => (string)b.Content == "Oddaj");
            Start(scenario, new User { Name = "Test", Role = "dyżurny ruchu" });
            var newControl = Buttons(netWin).First(b => (string)b.Content == "Oddaj");
            Check(netWin.IsVisible && !ReferenceEquals(oldControl, newControl), "okno sieci od razu przebudowuje przyciski po restarcie tej samej obsady");
            newControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(localWorld.Status.State != Lcs.WorldHost.WorldState.Faulted, "przycisk sterowania korzysta z nowej sesji");
            netWin.Close(); netWin = null;
        }
        catch (Exception ex) { report.AppendLine("BŁĄD WYKONANIA: " + ex.GetType().Name + ": " + ex.Message); errors++; }
        finally
        {
            ShotPath = shot; paused = true;
            report.AppendLine($"błędów: {errors}"); File.WriteAllText(output, report.ToString());
        }
    }
}
