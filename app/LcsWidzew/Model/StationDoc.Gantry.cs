using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Konstrukcja sygnalizatora na planie schematycznym (Ig-10 / poradnik UTK, wykaz znaków 9.8): maszt prosty, maszt
/// z wysięgnikiem, wysięgnik nad torem, bramka sygnałowa, sygnalizator karzełkowy. Bramka – osobny obiekt planu
/// („gantries”: oznaczenie, x, y0, y1) w poprzek torów; sygnalizatory przypięte mają „mount”: „bramka” i „gantry”.
/// Tylko układ torowy i edytor – komputer zależności i pulpit nie zależą od konstrukcji.
/// </summary>
public partial class StationDoc
{
    public JsonObject Gantries { get { if (Root["gantries"] is not JsonObject g) Root["gantries"] = g = new JsonObject(); return g; } }

    public static readonly (string Val, string Text)[] Mounts =
    {
        ("", "maszt prosty (znak 9.8.3)"),
        ("wysiegnik", "maszt z wysięgnikiem (9.8.7)"),
        ("nad", "wysięgnik nad torem (9.8.8)"),
        ("bramka", "bramka sygnałowa (9.8.8)"),
        ("karzel", "karzełkowy (9.8.5)"),
    };

    public string AddGantry(string post, string label, double x, double y0, double y1)
    {
        var key = $"{post}:{label}";
        if (Gantries.ContainsKey(key)) throw new InvalidOperationException($"Bramka {label} już istnieje");
        Gantries[key] = new JsonObject { ["label"] = label, ["post"] = post, ["x"] = Math.Round(x, 1), ["y0"] = Math.Round(Math.Min(y0, y1), 1), ["y1"] = Math.Round(Math.Max(y0, y1), 1) };
        return key;
    }

    public IEnumerable<string> GantrySignals(string gid) => Signals.Where(s => Sv(s.Value["gantry"]) == gid).Select(s => s.Key);

    /// <summary>Węzeł sygnalizatora można przesunąć do x bez zmiany kolejności na torach (kierunek jazdy torów się nie zmienia).</summary>
    bool CanShiftX(string nid, double x)
    {
        var (ox, _) = Pos(nid);
        foreach (var e in EdgesAt(nid))
        {
            var o = Sv(e["a"]) == nid ? Sv(e["b"]) : Sv(e["a"]); var (px, _) = Pos(o);
            if (Math.Abs(px - ox) >= 0.5 && (Math.Sign(px - x) != Math.Sign(px - ox) || Math.Abs(px - x) < 0.5)) return false;
        }
        return true;
    }

    /// <summary>Przypięcie sygnalizatora do bramki: węzeł dosuwa się w poziomie do bramki (gdy nie przeskakuje sąsiednich węzłów). Zwraca, czy dosunięto.</summary>
    public bool AttachToGantry(string sigKey, string gid)
    {
        var s = (JsonObject)Signals[sigKey]; var g = (JsonObject)Gantries[gid];
        s["mount"] = "bramka"; s["gantry"] = gid;
        var nid = Sv(s["node"]); var (nx, ny) = Pos(nid); double gx = D(g["x"]);
        if (Math.Abs(nx - gx) < 0.05) return true;
        if (!CanShiftX(nid, gx)) return false;
        MoveNode(nid, gx, ny); return true;
    }

    /// <summary>Sygnalizatory w pobliżu bramki (w poziomie ≤ tol, w pionie w jej zasięgu) – przypięte.</summary>
    public (int Attached, int NotMoved) AttachNear(string gid, double tol)
    {
        var g = (JsonObject)Gantries[gid]; double x = D(g["x"]), y0 = D(g["y0"]), y1 = D(g["y1"]);
        int a = 0, nm = 0;
        foreach (var (k, v) in Signals.ToList())
        {
            var s = (JsonObject)v; if (Sv(s["type"]) == "pk") continue;   // PK – bez konstrukcji
            var (nx, ny) = Pos(Sv(s["node"]));
            if (Math.Abs(nx - x) > tol || ny < y0 - 2 || ny > y1 + 2) continue;
            a++; if (!AttachToGantry(k, gid)) nm++;
        }
        return (a, nm);
    }

    public void SetMount(string sigKey, string mount)
    {
        var s = (JsonObject)Signals[sigKey];
        if (string.IsNullOrEmpty(mount)) s.Remove("mount"); else s["mount"] = mount;
        if (mount != "bramka") s.Remove("gantry");
    }

    /// <summary>Przesunięcie bramki w poziomie razem z przypiętymi sygnalizatorami (tylko te, które nie przeskoczą sąsiednich węzłów).</summary>
    public int MoveGantry(string gid, double x)
    {
        var g = (JsonObject)Gantries[gid]; g["x"] = Math.Round(x, 1); int stuck = 0;
        foreach (var k in GantrySignals(gid).ToList()) if (!AttachToGantry(k, gid)) stuck++;
        return stuck;
    }

    public void DeleteGantry(string gid)
    {
        foreach (var k in GantrySignals(gid).ToList()) SetMount(k, "");
        Gantries.Remove(gid);
        if (Gantries.Count == 0) Root.Remove("gantries");
    }
}
