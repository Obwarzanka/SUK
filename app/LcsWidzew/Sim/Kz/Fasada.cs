using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Fasada: stan i zapytania komputera zależności dla okien, sieci, ruchu pociągów i testów (dawne pola <see cref="Simulator"/>).
/// Moduły rozmawiają z komputerem zależności przez <see cref="Kz"/>: karty – statusy, system nadrzędny – polecenia.
/// </summary>
public partial class Simulator
{
    /// <summary>Komputer zależności (Sim/Kz/).</summary>
    public readonly KomputerZaleznosci Kz;

    public Dictionary<string, SecState> Secs => Kz.Secs;
    public Dictionary<string, SwState> Sws => Kz.Sws;
    public Dictionary<string, SigState> Sigs => Kz.Sigs;
    public Dictionary<string, DerState> Ders => Kz.Ders;
    public Dictionary<string, BlockState> Blocks => Kz.Blocks;
    public Dictionary<string, Route> Routes => Kz.Routes;
    public Dictionary<string, List<RouteDef>> RouteTable => Kz.RouteTable;
    public PowerState Power => Kz.Power;

    public bool SecOcc(string id) => Kz.SecOcc(id);
    public bool SwControlled(string n) => Kz.SwControlled(n);
    public double SzLeft(string id) => Kz.SzLeft(id);
    public double SignalSpeed(RouteDef r) => Kz.SignalSpeed(r);
    public (int Klasa, int W21, double V, string Uwaga) Sygnalizowana(RouteDef r) => Kz.Sygnalizowana(r);
    public double RouteSpeed(RouteDef r) => Kz.RouteSpeed(r);
    public List<string> WhyNot(RouteDef rd) => Kz.WhyNot(rd);
    /// <summary>Przeliczenie tablicy przebiegów po zmianie zależności specjalnych – tylko bez nastawionych przebiegów (null – przeliczona).</summary>
    public string PrzeliczTablice()
    {
        if (Routes.Count > 0) return "Są nastawione przebiegi – zwolnij je i naciśnij „Przelicz tablicę” (albo uruchom symulację ponownie).";
        Kz.RouteTable.Clear(); Kz.BuildRouteTable();
        return null;
    }
    public List<string> SignalConditions(Route r) => Kz.SignalConditions(r);
    public RouteDef FindRoute(string start, string end, string type, IList<string> via = null) => Kz.FindRoute(start, end, type, via);
    public List<RouteDef> RawRoutes(string sigName) => Kz.RawRoutes(sigName);
    public List<RouteDef> ApplyRules(List<RouteDef> raw) => Kz.ApplyRules(raw);
    public Fwd NextByState(string nid, int dir, string fromEdge) => Kz.NextByState(nid, dir, fromEdge);
    public bool BlockAllowsExit(string sz, int dir) => Kz.BlockAllowsExit(sz, dir);
    public List<string> BlockConds(BlockDef b) => Kz.BlockConds(b);
    public bool TrainOnBlock(string id) => Kz.TrainOnBlock(id);
    public bool KTor(BlockDef b) => Kz.KTor(b);
    public bool KoReady(BlockDef b, int e) => Kz.KoReady(b, e);
    public int EndIdx(BlockDef b, string page) => Kz.EndIdx(b, page);
    public int EndIdxForDir(BlockDef b, int dir) => Kz.EndIdxForDir(b, dir);
    public string DirText(BlockDef b, int dir) => Kz.DirText(b, dir);
}
