using System.Text.Json;

namespace Lcs.Model;

/// <summary>
/// Otwarte zobrazowanie: plik <c>*.zob.json</c>, wczytany projekt UT (tylko do odczytu), zmiany z cofaniem jak w <see cref="UtDocument"/>.
/// </summary>
public class ZobDocument
{
    public ZobProject P { get; private set; }
    /// <summary>Plik zobrazowania; null – jeszcze niezapisane (<see cref="DefaultPath"/> – propozycja nazwy).</summary>
    public string Path { get; private set; }
    /// <summary>Projekt UT – tylko do odczytu, edytor zobrazowania go nie zapisuje.</summary>
    public UtProject Ut { get; private set; }
    /// <summary>Pełna ścieżka pliku UT.</summary>
    public string UtPath { get; private set; }
    /// <summary>UT zapisany po ostatnim zapisie zobrazowania (czas pliku nowszy niż <see cref="ZobProject.UtZapis"/>).</summary>
    public bool UtChanged { get; private set; }
    public bool Dirty { get; private set; }
    public event Action Changed;

    readonly Stack<(string What, string Json)> undo = new(), redo = new();
    public string UndoName => undo.Count > 0 ? undo.Peek().What : null;
    public string RedoName => redo.Count > 0 ? redo.Peek().What : null;

    ZobDocument(ZobProject p, string path, UtProject ut, string utPath) { P = p; Path = path; Ut = ut; UtPath = utPath; }

    public static string Serialize(ZobProject p) => JsonSerializer.Serialize(p, UtDocument.Json);
    public static ZobProject Deserialize(string json) => JsonSerializer.Deserialize<ZobProject>(json, UtDocument.Json) ?? new ZobProject();

    /// <summary>Propozycja pliku zobrazowania obok UT: <c>widzew.ut.json</c> → <c>widzew.zob.json</c>.</summary>
    public static string ZobPathFor(string utPath)
    {
        var name = System.IO.Path.GetFileName(utPath);
        name = name.EndsWith(".ut.json", StringComparison.OrdinalIgnoreCase) ? name[..^8] : System.IO.Path.GetFileNameWithoutExtension(name);
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(utPath))!, name + ".zob.json");
    }
    public string DefaultPath => Path ?? ZobPathFor(UtPath);

    /// <summary>Nowe zobrazowanie do projektu UT: jeden obraz szczegółowy ze wszystkimi posterunkami UT.</summary>
    public static ZobDocument New(string utPath)
    {
        utPath = System.IO.Path.GetFullPath(utPath);
        var ut = UtDocument.Load(utPath).P;
        var p = new ZobProject { Ut = utPath };
        p.Obrazy.Add(new ZobObraz
        {
            Nazwa = ut.Posts.Count > 0 ? ut.Posts[0].Nazwa : ut.Name,
            Posterunki = ut.Posts.Select(x => x.Id).ToList(),
        });
        return new ZobDocument(p, null, ut, utPath);
    }

    public static ZobDocument Load(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        var p = Deserialize(File.ReadAllText(path));
        if (p.Version != 1) throw new InvalidDataException($"Nieznana wersja pliku zobrazowania ({p.Version})");
        p.Obrazy ??= new();
        foreach (var o in p.Obrazy) Normalize(o);
        if (string.IsNullOrEmpty(p.Ut)) throw new InvalidDataException("W pliku zobrazowania brak ścieżki projektu UT");
        var utPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, p.Ut));
        if (!File.Exists(utPath)) throw new FileNotFoundException($"Nie ma projektu UT „{p.Ut}” (szukano: {utPath})", utPath);
        var ut = UtDocument.Load(utPath).P;
        var d = new ZobDocument(p, path, ut, utPath);
        d.UtChanged = p.UtZapis is DateTime t && File.GetLastWriteTime(utPath) > t.AddSeconds(1);
        return d;
    }

    static void Normalize(ZobObraz o)
    {
        o.Posterunki ??= new(); o.Tory ??= new(); o.Zwrotnice ??= new(); o.Sygnalizatory ??= new(); o.Wykolejnice ??= new();
        o.Kozly ??= new(); o.Przerwy ??= new(); o.Odcinki ??= new(); o.NumeryTorow ??= new(); o.Perony ??= new();
        o.Przejazdy ??= new(); o.Szlaki ??= new(); o.Napisy ??= new();
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
    public bool Change(string what, Action<ZobProject> act)
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
