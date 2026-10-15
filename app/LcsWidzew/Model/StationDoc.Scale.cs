using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Skala planu schematycznego (Ig-10 § 3): podłużna – długości torów (1:2000, za zgodą 1:4000), poprzeczna – odstępy torów
/// (1:500, za zgodą 1:1000). Rysunek planu ma 2,5 jednostki na 1 mm arkusza, więc jednostki na metr: wzdłuż Ux, w poprzek Uy.
/// Zapis w station.json: „planScale” { along, across, auto }. auto = true – długość toru (len) liczona z rysunku planu
/// (rzut w skali podłużnej + skos w skali poprzecznej); wyjątki: szlaki (rysowane w skróceniu, Ig-10 § 1 ust. 8),
/// łączniki obrazów i tory z długością wpisaną ręcznie („lenFix”).
/// </summary>
public partial class StationDoc
{
    /// <summary>Jednostki rysunku planu na 1 mm arkusza (styl Ig-10 w edytorze).</summary>
    public const double PlanMm = 2.5;
    public static readonly (int Along, int Across, string Name)[] PlanScales =
    {
        (2000, 500, "1:2000 wzdłuż / 1:500 w poprzek (Ig-10 § 3 ust. 1)"),
        (4000, 1000, "1:4000 wzdłuż / 1:1000 w poprzek (Ig-10 § 3 ust. 2)"),
        (2000, 2000, "1:2000 w obu kierunkach – skala rzeczywista (podkład ze zdjęć / mapy bez zniekształceń)"),
        (1000, 1000, "1:1000 w obu kierunkach – skala rzeczywista, większa"),
    };
    /// <summary>Skala rzeczywista: ta sama wzdłuż i w poprzek – zdjęcie lotnicze / mapa leży na planie bez zniekształceń.</summary>
    public bool RealScale => Math.Abs(ScaleAlong - ScaleAcross) < 0.5;

    public JsonObject PlanScaleObj => Root["planScale"] as JsonObject;
    public double ScaleAlong => PlanScaleObj?["along"] is JsonNode n ? Math.Max(10, D(n)) : 2000;
    public double ScaleAcross => PlanScaleObj?["across"] is JsonNode n ? Math.Max(10, D(n)) : 500;
    /// <summary>Jednostki planu na 1 m wzdłuż toru (1:2000 → 1,25).</summary>
    public double Ux => PlanMm * 1000 / ScaleAlong;
    /// <summary>Jednostki planu na 1 m w poprzek (1:500 → 5).</summary>
    public double Uy => PlanMm * 1000 / ScaleAcross;
    /// <summary>Długości torów z rysunku planu.</summary>
    public bool LenAuto => PlanScaleObj?["auto"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public void SetPlanScale(double along, double across, bool auto) =>
        Root["planScale"] = new JsonObject { ["along"] = Math.Round(along, 1), ["across"] = Math.Round(across, 1), ["auto"] = auto };

    /// <summary>
    /// Plik bez „planScale”: skala odczytana z rysunku (mediana rzutu poziomego / len torów stacyjnych), dopasowana do skal
    /// Ig-10. Istniejące długości są danymi (np. z OSM) – auto wyłączone. Pusty układ: 1:2000 / 1:500, długości z rysunku.
    /// </summary>
    void EnsurePlanScale()
    {
        if (Root["planScale"] is JsonObject) return;
        var r = new List<double>();
        foreach (var e in Edges.OfType<JsonObject>())
        {
            if (IsSzlak(e) || IsLink(e) || e["pts"] is not JsonArray) continue;
            var p = Pts(e); double dx = 0, len = D(e["len"]);
            for (int i = 0; i < p.Count - 1; i++) dx += Math.Abs(p[i + 1].X - p[i].X);
            if (dx > 5 && len > 1) r.Add(dx / len);
        }
        if (r.Count == 0) { SetPlanScale(2000, 500, true); return; }
        r.Sort(); double ux = r[r.Count / 2], along = PlanMm * 1000 / ux;
        foreach (var (a, c, _) in PlanScales)
            if (Math.Abs(along / a - 1) < 0.15) { SetPlanScale(a, c, false); return; }
        along = Math.Round(along / 10) * 10;
        SetPlanScale(along, along / 4, false);
    }

    /// <summary>Długość łamanej planu w metrach: składowa pozioma w skali podłużnej, pionowa w poprzecznej.</summary>
    public double PlanLen(IReadOnlyList<(double X, double Y)> p)
    {
        double s = 0, ux = Ux, uy = Uy;
        for (int i = 0; i < p.Count - 1; i++) { double dx = (p[i + 1].X - p[i].X) / ux, dy = (p[i + 1].Y - p[i].Y) / uy; s += Math.Sqrt(dx * dx + dy * dy); }
        return s;
    }
    public double PlanLen(double x0, double y0, double x1, double y1) => PlanLen(new[] { (x0, y0), (x1, y1) });

    public static bool SzlakEdge(JsonObject e) => IsSzlak(e);
    /// <summary>Tor z długością wpisaną (fix) albo z rysunku.</summary>
    public void SetLenFix(JsonObject e, bool fix) { if (fix) e["lenFix"] = true; else { e.Remove("lenFix"); AutoLen(e); } }
    public static bool LenFixed(JsonObject e) => e["lenFix"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    /// <summary>Długość toru wynika z rysunku (auto, nie szlak, nie łącznik obrazów, nie wpisana ręcznie).</summary>
    public bool LenFromPlan(JsonObject e) => LenAuto && !IsSzlak(e) && !IsLink(e) && !LenFixed(e);

    /// <summary>Długość nowego toru – z rysunku w skali planu.</summary>
    double NewLen(List<(double X, double Y)> p) => Math.Round(Math.Max(1, PlanLen(p)), 1);

    /// <summary>Po zmianie rysunku toru: długość z rysunku (gdy włączone). Bez auto długość jest daną i się nie zmienia.</summary>
    void AutoLen(JsonObject e) { if (LenFromPlan(e)) e["len"] = NewLen(Pts(e)); }

    /// <summary>Przeliczenie długości wszystkich torów z rysunku (pomija szlaki, łączniki obrazów i tory „lenFix”). Raport.</summary>
    public string RecalcLens(bool includeFixed = false)
    {
        int n = 0, skip = 0; double maxD = 0; string maxId = null;
        foreach (var e in EdgeObjs)
        {
            if (IsSzlak(e) || IsLink(e) || (!includeFixed && LenFixed(e))) { skip++; continue; }
            double old = D(e["len"]), nw = NewLen(Pts(e));
            if (Math.Abs(nw - old) >= 0.05) { n++; if (Math.Abs(nw - old) > maxD) { maxD = Math.Abs(nw - old); maxId = Sv(e["id"]); } }
            e["len"] = nw;
            if (includeFixed) e.Remove("lenFix");
        }
        return $"zmieniono {n} torów" + (maxId != null ? $" (największa zmiana {maxD:0} m – tor {maxId})" : "") + $", pominięto {skip} (szlaki, łączniki obrazów, długości wpisane)";
    }

    /// <summary>Podkłady planu (od dołu): obrazy i mozaiki kafelków. Zapis „planBgs”; dawny pojedynczy „planBg” – przenoszony.</summary>
    public JsonArray PlanBgs { get { if (Root["planBgs"] is not JsonArray a) Root["planBgs"] = a = new JsonArray(); return a; } }
    void MigrateBg()
    {
        if (Root["planBg"] is not JsonObject one) return;
        Root.Remove("planBg"); PlanBgs.Add(one);
    }

    /// <summary>Zmiana skali planu z przeskalowaniem rysunku (węzły, tory, napisy, perony, przejazdy) – długości w metrach bez zmian.</summary>
    public void RescalePlan(double along, double across)
    {
        double kx = ScaleAlong / along, ky = ScaleAcross / across;
        bool auto = LenAuto;
        foreach (var (_, n) in Nodes) { var o = (JsonObject)n; o["x"] = Math.Round(D(o["x"]) * kx, 1); o["y"] = Math.Round(D(o["y"]) * ky, 1); }
        foreach (var e in EdgeObjs) SetPts(e, Pts(e).Select(p => (p.X * kx, p.Y * ky)).ToList());
        foreach (var l in ((JsonArray)Root["labels"]).OfType<JsonObject>()) { l["x"] = Math.Round(D(l["x"]) * kx, 1); l["y"] = Math.Round(D(l["y"]) * ky, 1); }
        foreach (var p in ((JsonArray)Root["platforms"]).OfType<JsonObject>()) foreach (var (k, f) in new[] { ("x", kx), ("y", ky), ("w", kx), ("h", ky) }) p[k] = Math.Round(D(p[k]) * f, 1);
        foreach (var c in ((JsonArray)Root["crossings"]).OfType<JsonObject>()) foreach (var (k, f) in new[] { ("x", kx), ("y0", ky), ("y1", ky) }) c[k] = Math.Round(D(c[k]) * f, 1);
        foreach (var bg in (Root["planBgs"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            // podkład zostaje na swoim miejscu względem planu: skala obrazu ×kx, rozciągnięcie pionowe ×ky/kx
            bg["x"] = Math.Round(D(bg["x"]) * kx, 2); bg["y"] = Math.Round(D(bg["y"]) * ky, 2);
            if (bg["w"] != null) bg["w"] = Math.Round(D(bg["w"]) * kx, 2);
            bg["sy"] = Math.Round((bg["sy"] != null ? D(bg["sy"]) : 1) * ky / kx, 4);
        }
        SetPlanScale(along, across, auto);
    }
}
