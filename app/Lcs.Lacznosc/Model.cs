using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lcs.Lacznosc;

public abstract class Obserwowany : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void Zmiana([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    protected void Ustaw<T>(ref T pole, T v, [CallerMemberName] string n = null)
    {
        if (EqualityComparer<T>.Default.Equals(pole, v)) return;
        pole = v; Zmiana(n);
    }
}

/// <summary>Posterunek sąsiedni (z DispatcherPlayerAuth.Neighbours w grze).</summary>
public sealed class Sasiad : Obserwowany
{
    public string Nazwa { get; init; }
    bool _gracz; int _nieprzeczytane; int _oczekujace;
    public bool Gracz { get => _gracz; set { Ustaw(ref _gracz, value); Zmiana(nameof(Obsada)); } }
    public string Obsada => Gracz ? "gracz" : "AI";
    public int Nieprzeczytane { get => _nieprzeczytane; set { Ustaw(ref _nieprzeczytane, value); Zmiana(nameof(Licznik)); } }
    public int Oczekujace { get => _oczekujace; set { Ustaw(ref _oczekujace, value); Zmiana(nameof(Licznik)); } }
    public string Licznik => Nieprzeczytane > 0 ? $"O:{Nieprzeczytane}" : Oczekujace > 0 ? $"W:{Oczekujace}" : "";
}

/// <summary>Wpis rozmowy (komunikat czatu dyżurnych).</summary>
public sealed class Wpis : Obserwowany
{
    public string Czas { get; init; }
    public bool Wychodzacy { get; init; }
    public string Od { get; init; }
    public string Do { get; init; }
    public bool Ai { get; init; }
    public string Klasa { get; init; }
    public string Pociag { get; init; }
    public int? Tor { get; init; }
    string _tekst, _status;
    public string Tekst { get => _tekst; set => Ustaw(ref _tekst, value); }
    public string Status { get => _status; set => Ustaw(ref _status, value); }
    public long Id { get; init; }
    public DateTime Utworzony { get; init; } = DateTime.Now;
    public string Naglowek => $"{Czas}  {(Wychodzacy ? $"→ {Do}" : $"← {Od}{(Ai ? " (AI)" : "")}")}  {Teksty.Etykieta(Klasa)}";
    public string Sasiad => Wychodzacy ? Do : Od;
}

/// <summary>Rodzaj komunikatu (szablon gry). Arg – argumenty z gry: Train, Time, DenialReason, TrackNumber.</summary>
public sealed class Rodzaj
{
    public string Klasa { get; init; }
    public string Szablon { get; init; }
    public string[] Arg { get; init; } = Array.Empty<string>();
    public bool ZGry { get; init; }
    public string Etykieta => Teksty.Etykieta(Klasa);
    public override string ToString() => Etykieta;
}

/// <summary>Podpowiedź numeru pociągu.</summary>
public sealed class Podpowiedz
{
    public string Nr { get; init; }
    public string Opis { get; init; }
    public int? Tor { get; init; }
    public DateTime? Czas { get; init; }
    public int Waga { get; init; }
    public override string ToString() => Nr;
}

/// <summary>Wiersz planu ruchu posterunku (z rozkładu SimRail).</summary>
public sealed class PozycjaPlanu : Obserwowany
{
    public string Pociag { get; init; }
    public string Kategoria { get; init; }
    public string Przyjazd { get; init; }
    public string Odjazd { get; init; }
    public DateTime Czas { get; init; }
    public string PO { get; init; }
    public string Tor { get; init; }
    public string Peron { get; init; }
    public string Skad { get; init; }
    public string Dokad { get; init; }
    public string Relacja { get; init; }
    public string Lokomotywa { get; init; }
    public string Dlugosc { get; init; }
    public string Brutto { get; init; }
    string _zgoda; bool _wStacji;
    public string Zgoda { get => _zgoda; set => Ustaw(ref _zgoda, value); }
    public bool WStacji { get => _wStacji; set => Ustaw(ref _wStacji, value); }
}

public static class Teksty
{
    public static string Etykieta(string klasa) => klasa switch
    {
        "TrainRequestMessage" => "Zapytanie: czy droga wolna?",
        "TrainPermissionMessage" => "Droga wolna (zgoda)",
        "TrainDenialMessage" => "Odmowa przyjęcia",
        "TrainDepartedMessage" => "Odjazd pociągu",
        "TrainArrivedMessage" => "Przyjazd pociągu",
        "TrainCancelledMessage" => "Odwołanie pociągu",
        "TrainQuestionMessage" => "Co odjechało?",
        "LockLineblockMessage" => "Zablokuj blok początkowy",
        "CustomTextMessage" => "Tekst własny",
        "RepeatMessage" => "Powtórz",
        null or "" => "",
        _ => klasa.Replace("Message", ""),
    };

    /// <summary>Argumenty, gdy gra ich nie podała (program bez połączenia) – do potwierdzenia szablonami z gry.</summary>
    public static string[] DomyslneArg(string klasa) => klasa switch
    {
        // jak w grze (szablony odczytane z SimRail 2026-11-21)
        "TrainRequestMessage" => new[] { "Train", "TrackNumber" },
        "TrainPermissionMessage" or "TrainDepartedMessage" => new[] { "Train", "Time", "TrackNumber" },
        "TrainDenialMessage" => new[] { "Train", "DenialReason", "TrackNumber" },
        "TrainArrivedMessage" => new[] { "Train", "Time" },
        "CustomTextMessage" or "TrainQuestionMessage" or "LockLineblockMessage" => Array.Empty<string>(),
        _ => new[] { "Train" },
    };

    public static readonly string[] Klasy =
    {
        "TrainRequestMessage", "TrainPermissionMessage", "TrainDenialMessage", "TrainDepartedMessage", "TrainArrivedMessage",
        "TrainCancelledMessage", "TrainQuestionMessage", "LockLineblockMessage", "CustomTextMessage",
    };

    public static readonly (string Kod, string Opis)[] Powody =
    {
        ("TrackOccupied", "tor zajęty"),
        ("NoTracksAvailable", "brak wolnych torów"),
        ("TrackIsClosed", "tor zamknięty"),
        ("ThisWouldBlockMyStation", "zablokowałby moją stację"),
        ("IHaveHigherPriority", "mam pociąg o wyższym pierwszeństwie"),
        ("IHavePermission", "mam pozwolenie w drugą stronę"),
        ("IHaveToAskFurther", "muszę zapytać dalej"),
        ("TrainDoesntExist", "nie ma takiego pociągu"),
        ("UnknownLine", "nieznana linia"),
    };
}
