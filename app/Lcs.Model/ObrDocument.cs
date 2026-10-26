using System.Text.Json;

namespace Lcs.Model;

/// <summary>
/// Otwarte zobrazowanie w wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 4): plik <c>*.zob.json</c>, wczytany projekt UT (tylko do odczytu),
/// zmiany z cofaniem jak w <see cref="ZobDocument"/>. Plik wersji 1 (stary edytor) – przekształcany do wersji 2 (<see cref="ObrZWersji1"/>), zapis pod nową nazwą.
/// </summary>
public class ObrDocument
{
    public ObrProject P { get; private set; }
    /// <summary>Plik zobrazowania; null – jeszcze niezapisane (<see cref="DefaultPath"/> – propozycja nazwy).</summary>
    public string Path { get; private set; }
    /// <summary>Projekt UT – tylko do odczytu, edytor zobrazowania go nie zapisuje.</summary>
    public UtProject Ut { get; private set; }
    /// <summary>Pełna ścieżka pliku UT.</summary>
    public string UtPath { get; private set; }
    /// <summary>UT zapisany po ostatnim zapisie zobrazowania (czas pliku nowszy niż <see cref="ObrProject.UtZapis"/>).</summary>
    public bool UtChanged { get; private set; }
    public bool Dirty { get; private set; }
    public event Action Changed;

    readonly Stack<(string What, string Json)> undo = new(), redo = new();
    public string UndoName => undo.Count > 0 ? undo.Peek().What : null;
    public string RedoName => redo.Count > 0 ? redo.Peek().What : null;

    ObrDocument(ObrProject p, string path, UtProject ut, string utPath) { P = p; Path = path; Ut = ut; UtPath = utPath; }

    public static string Serialize(ObrProject p) => JsonSerializer.Serialize(p, UtDocument.Json);
    public static ObrProject Deserialize(string json) => JsonSerializer.Deserialize<ObrProject>(json, UtDocument.Json) ?? new ObrProject();

    /// <summary>Plik zapisany starym edytorem (wersja 1) – odczyt i przekształcenie do wersji 2 w etapie E5.</summary>
    public class StaryFormatException(string m) : IOException(m);

    /// <summary>Plik wersji 1, z którego przekształcono ten dokument (null – plik wersji 2 albo nowy); <see cref="UwagiKonwersji"/> – co pominięto.</summary>
    public string ZWersji1 { get; private set; }
    public List<string> UwagiKonwersji { get; private set; } = new();

    /// <summary>Propozycja nazwy dla obrazu przekształconego z wersji 1: <c>x.zob.json</c> → <c>x.v2.zob.json</c> (plik starego edytora zostaje).</summary>
    public static string NazwaV2(string v1Path)
    {
        var n = System.IO.Path.GetFileName(v1Path);
        n = n.EndsWith(".zob.json", StringComparison.OrdinalIgnoreCase) ? n[..^9] : System.IO.Path.GetFileNameWithoutExtension(n);
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(v1Path)!, n + ".v2.zob.json");
    }

    /// <summary>Wersja pliku zobrazowania (pole <c>version</c>); 0 – brak pola.</summary>
    public static int WersjaPliku(string json)
    {
        using var d = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : 0;
    }

    public string DefaultPath => Path ?? (ZWersji1 != null ? NazwaV2(ZWersji1) : ZobDocument.ZobPathFor(UtPath));

    /// <summary>Nowe zobrazowanie do projektu UT: jeden obraz szczegółowy ze wszystkimi posterunkami UT i domyślnymi strefami.</summary>
    public static ObrDocument New(string utPath)
    {
        utPath = System.IO.Path.GetFullPath(utPath);
        var ut = UtDocument.Load(utPath).P;
        var p = new ObrProject { Ut = utPath };
        var o = new ObrObraz
        {
            Nazwa = ut.Posts.Count > 0 ? ut.Posts[0].Nazwa : ut.Name,
            Posterunki = ut.Posts.Select(x => x.Id).ToList(),
        };
        o.Strefy = ObrObraz.StrefyDomyslne(o.Szer);
        p.Obrazy.Add(o);
        return new ObrDocument(p, null, ut, utPath);
    }

    public static ObrDocument Load(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        var json = File.ReadAllText(path);
        int v = WersjaPliku(json);
        if (v == 1) return ZWersji1Load(path, json);
        if (v != ObrProject.Wersja) throw new InvalidDataException($"Nieznana wersja pliku zobrazowania ({v})");
        var p = Deserialize(json);
        p.Obrazy ??= new();
        foreach (var o in p.Obrazy) Normalize(o);
        if (string.IsNullOrEmpty(p.Ut)) throw new InvalidDataException("W pliku zobrazowania brak ścieżki projektu UT");
        var utPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, p.Ut));
        if (!File.Exists(utPath)) throw new FileNotFoundException($"Nie ma projektu UT „{p.Ut}” (szukano: {utPath})", utPath);
        var ut = UtDocument.Load(utPath).P;
        var d = new ObrDocument(p, path, ut, utPath);
        d.UtChanged = p.UtZapis is DateTime t && File.GetLastWriteTime(utPath) > t.AddSeconds(1);
        return d;
    }

    /// <summary>Plik starego edytora: odczyt jak w <see cref="ZobDocument"/>, przekształcenie do wersji 2; dokument niezapisany (propozycja nazwy *.v2.zob.json).</summary>
    static ObrDocument ZWersji1Load(string path, string json)
    {
        var v1 = ZobDocument.Load(path);
        var w = ObrZWersji1.Przeksztalc(v1.P, v1.Ut);
        foreach (var o in w.P.Obrazy) Normalize(o);
        var d = new ObrDocument(w.P, null, v1.Ut, v1.UtPath) { ZWersji1 = path, UwagiKonwersji = w.Uwagi };
        d.P.Ut = v1.P.Ut;
        return d;
    }

    static void Normalize(ObrObraz o)
    {
        o.Posterunki ??= new(); o.Rzedy ??= new(); o.Strefy ??= new(); o.Wezly ??= new(); o.Odcinki ??= new(); o.Elementy ??= new(); o.Przypiecia ??= new();
        foreach (var e in o.Elementy) e.Opisy ??= new();
        if (o.Szkic != null) { o.Szkic.Wezly ??= new(); o.Szkic.Odcinki ??= new(); o.Szkic.Elementy ??= new(); o.Szkic.Przypiecia ??= new(); }
        foreach (var e in o.Odcinki) e.Pkt ??= new();
        o.Rzedy.Sort();
    }

    /// <summary>Zapis przez plik tymczasowy, poprzednia wersja w <c>.bak</c>; ścieżka UT względem pliku, czas zapisu UT.</summary>
    public void Save(string path = null)
    {
        path = System.IO.Path.GetFullPath(path ?? Path ?? DefaultPath);
        var rel = System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(path)!, UtPath);
        P.Ut = System.IO.Path.IsPathRooted(rel) ? UtPath : rel.Replace('\\', '/');
        P.UtZapis = File.GetLastWriteTime(UtPath);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(P));
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
        else File.Move(tmp, path);
        Path = path; Dirty = false; UtChanged = false;
        Changed?.Invoke();
    }

    /// <summary>Wczytanie UT od nowa (po zmianach w edytorze UT); obraz bez zmian.</summary>
    public void ReloadUt()
    {
        Ut = UtDocument.Load(UtPath).P;
        UtChanged = P.UtZapis is DateTime t && File.GetLastWriteTime(UtPath) > t.AddSeconds(1);
        Changed?.Invoke();
    }

    /// <summary>Zmiana zobrazowania z możliwością cofnięcia; zmiana bez skutku nie trafia na listę.</summary>
    public bool Change(string what, Action<ObrProject> act)
    {
        var before = Serialize(P);
        act(P);
        if (Serialize(P) == before) return false;
        undo.Push((what, before));
        if (undo.Count > UtDocument.UndoLimit) { var keep = undo.Take(UtDocument.UndoLimit).Reverse().ToList(); undo.Clear(); foreach (var x in keep) undo.Push(x); }
        redo.Clear();
        Dirty = true;
        Changed?.Invoke();
        return true;
    }

    public bool Undo() => Swap(undo, redo);
    public bool Redo() => Swap(redo, undo);

    bool Swap(Stack<(string What, string Json)> from, Stack<(string What, string Json)> to)
    {
        if (from.Count == 0) return false;
        var (what, json) = from.Pop();
        to.Push((what, Serialize(P)));
        P = Deserialize(json);
        Dirty = true;
        Changed?.Invoke();
        return true;
    }
}
