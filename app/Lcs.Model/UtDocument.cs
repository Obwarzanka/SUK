using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lcs.Model;

/// <summary>
/// Otwarty projekt UT: plik, zmiany z cofaniem i ponawianiem.
/// Każda zmiana idzie przez <see cref="Change"/> – zapamiętuje stan sprzed zmiany (zrzut JSON), więc Ctrl+Z przywraca go dokładnie.
/// </summary>
public class UtDocument
{
    public UtProject P { get; private set; }
    public string Path { get; private set; }
    public bool Dirty { get; private set; }
    public event Action Changed;

    readonly Stack<(string What, string Json)> undo = new(), redo = new();
    public string UndoName => undo.Count > 0 ? undo.Peek().What : null;
    public string RedoName => redo.Count > 0 ? redo.Peek().What : null;
    public const int UndoLimit = 200;

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public UtDocument() : this(new UtProject(), null) { }
    UtDocument(UtProject p, string path) { P = p; Path = path; }

    public static string Serialize(UtProject p) => JsonSerializer.Serialize(p, Json);
    public static UtProject Deserialize(string json) => JsonSerializer.Deserialize<UtProject>(json, Json) ?? new UtProject();
    /// <summary>Stan projektu bez widoku – do porównań i cofania.</summary>
    static string Snapshot(UtProject p) { var v = p.View; p.View = null; try { return Serialize(p); } finally { p.View = v; } }

    public static UtDocument Load(string path)
    {
        var p = Deserialize(File.ReadAllText(path));
        if (p.Crs != "EPSG:2180") throw new InvalidDataException($"Nieznany układ współrzędnych „{p.Crs}” – edytor obsługuje PUWG 1992 (EPSG:2180)");
        p.View ??= new UtViewState();
        if (!UtProject.DrawingScales.Contains(p.DrawingScale)) p.DrawingScale = 1000;
        return new UtDocument(p, System.IO.Path.GetFullPath(path));
    }

    /// <summary>Zapis przez plik tymczasowy; poprzednia wersja zostaje jako <c>.bak</c>.</summary>
    public void Save(string path = null)
    {
        path = System.IO.Path.GetFullPath(path ?? Path ?? throw new InvalidOperationException("Brak nazwy pliku"));
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(P));
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
        else File.Move(tmp, path);
        Path = path; Dirty = false;
        Changed?.Invoke();
    }

    /// <summary>Zmiana projektu z możliwością cofnięcia. Zmiana, która nic nie zmienia, nie trafia na listę.</summary>
    public bool Change(string what, Action<UtProject> act)
    {
        var before = Snapshot(P);
        act(P);
        if (Snapshot(P) == before) return false;
        undo.Push((what, before));
        if (undo.Count > UndoLimit) Trim(undo);
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
        to.Push((what, Snapshot(P)));
        var view = P.View;
        P = Deserialize(json); P.View = view;
        Dirty = true;
        Changed?.Invoke();
        return true;
    }

    static void Trim(Stack<(string, string)> s)
    {
        var keep = s.Take(UndoLimit).Reverse().ToList();
        s.Clear(); foreach (var x in keep) s.Push(x);
    }
}
