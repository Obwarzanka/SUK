namespace Lcs.Tabor;

/// <summary>Plik taboru otwarty w edytorze: zmiany z cofaniem (migawki JSON, jak w edytorach UT i zobrazowania), zapis z <c>.bak</c>.</summary>
public class TaborDokument
{
    public const int UndoLimit = 200;
    public TaborPlik P { get; private set; } = new();
    public string Path { get; private set; }
    public bool Dirty { get; private set; }
    public event Action Changed;
    readonly Stack<(string What, string Json)> undo = new(), redo = new();
    public string UndoName => undo.Count > 0 ? undo.Peek().What : null;
    public string RedoName => redo.Count > 0 ? redo.Peek().What : null;

    public static TaborDokument Nowy() => new() { P = new TaborPlik() };
    public static TaborDokument Load(string path) => new() { P = TaborPlik.Load(path), Path = System.IO.Path.GetFullPath(path) };

    public void Save(string path = null)
    {
        path = System.IO.Path.GetFullPath(path ?? Path ?? throw new InvalidOperationException("Brak nazwy pliku"));
        P.Save(path);
        Path = path; Dirty = false;
        Changed?.Invoke();
    }

    /// <summary>Zmiana z możliwością cofnięcia; zmiana bez skutku nie trafia na listę.</summary>
    public bool Change(string what, Action<TaborPlik> act)
    {
        var before = TaborPlik.Serialize(P);
        act(P);
        if (TaborPlik.Serialize(P) == before) return false;
        undo.Push((what, before));
        if (undo.Count > UndoLimit) { var keep = undo.Take(UndoLimit).Reverse().ToList(); undo.Clear(); foreach (var x in keep) undo.Push(x); }
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
        to.Push((what, TaborPlik.Serialize(P)));
        P = TaborPlik.Deserialize(json);
        Dirty = true;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Zmiana oznaczenia serii razem z odwołaniami w składach; null – zmieniono, inaczej powód odmowy.</summary>
    public string ZmienSerie(int idx, string nowa)
    {
        nowa = nowa?.Trim() ?? "";
        if (nowa.Length == 0) return "Oznaczenie serii nie może być puste";
        var stara = P.Pojazdy[idx].Seria;
        if (nowa == stara) return null;
        if (P.Pojazdy.Any(p => p.Seria == nowa)) return $"Seria {nowa} jest już w katalogu";
        Change($"Seria {stara} → {nowa}", p =>
        {
            p.Pojazdy[idx].Seria = nowa;
            foreach (var x in p.Sklady.SelectMany(s => s.Pojazdy).Where(x => x.Seria == stara)) x.Seria = nowa;
        });
        return null;
    }
}
