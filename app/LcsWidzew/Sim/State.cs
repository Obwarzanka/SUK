namespace LcsWidzew.Sim;

public enum Aspect { Stop, Train, Shunt, Sz }

public class SecState
{
    public string Id;
    /// <summary>Status z karty odcinka (KartaOdcinka.Publikuj): zajętość przez pojazd i stan urządzenia kontroli niezajętości.</summary>
    public bool TrainOcc;
    public string Fault;          // null | "occ" (zajętość bez pojazdu) | "reset" (wymagane zerowanie) | "await" (oczekiwanie na 1. przejazd)
    public bool Closed, NoData;
    public string Lock, LockType; // przebieg utwierdzający; "train" | "shunt" | "overlap"
    public double? TimeRel;
}

public class MoveInfo { public string Target, From; public double Until, Started; public bool Alarmed; }

public class SwState
{
    public string Num, Pos = "+", Lock;
    /// <summary>Przebiegi, których ochronę boczną zapewnia zwrotnica (utwierdzona w położeniu ochronnym).</summary>
    public HashSet<string> Flank = new();
    /// <summary>Przestawianie zlecone karcie zwrotnicy (do meldunku o kontroli w położeniu docelowym albo o braku kontroli).</summary>
    public MoveInfo Moving;
    /// <summary>Frozen – karta melduje przekroczony czas przekładania (nadzór czasu, np. zamarznięcie).</summary>
    public bool Stopped, Rozpr, NoCtrl, Frozen, NoData, Local;
    /// <summary>Rozprucie skasowane (KSR) w trwającym braku kontroli – dalszy brak kontroli to już nie rozprucie.</summary>
    public bool KsrDone;
}

public class SigState
{
    public string Name, Route, RouteEnd;
    /// <summary>Przebiegi, w których ochronie bocznej sygnalizator jest utwierdzony przy sygnale „Stój”.</summary>
    public HashSet<string> Flank = new();
    public Aspect Aspect = Aspect.Stop;
    public bool Closed, LampFault, NoData, FailAlarm, Dark;
    /// <summary>Sygnał na semaforze odstępowym: S1, S2, S4 (zielone migające), S5 (pomarańczowe); null – inne.</summary>
    public string Sig;
    public double SzUntil;
}

public class DerState { public string Name, Lock; public bool On = true; public bool? MovingTo; public double MoveUntil; public HashSet<string> Flank = new(); }

/// <summary>Żądanie ustawienia kierunku WYJAZD od końca EndIdx (-1 – od posterunku spoza obszaru LCS).</summary>
public class BlockReq { public int EndIdx; public double At; public bool Alarmed; public string ForTrain; }

/// <summary>Stan elektronicznej blokady samoczynnej (Ie-104 § 8 pkt 17).</summary>
public class BlockState
{
    public string Id;
    public int Dir;                 // 0 – neutralny, ±1 – ustawiony kierunek ruchu (na wschód / zachód)
    public BlockReq Req;            // żądanie pozwolenia (Wbl ↔ Pzk)
    public double? ReleaseUntil;    // zwalnianie kierunku (Zwbl)
    public double? AzkUntil;        // awaryjna zmiana kierunku (AZK)
    public int AzkDir;
    public bool[] StopEnd = new bool[2];  // indywidualne zamknięcie wyjazdu na szlak (Stop) od danego końca
    public bool NbrSilent, NbrStop;
    /// <summary>Blokada półsamoczynna: po wjeździe pociągu na szlak (blok początkowy) – posterunek, który musi dać blok końcowy (-1 – sąsiedni spoza LCS).</summary>
    public int? AwaitEnd; public double FreeSince; public bool EndAlarmed;
    /// <summary>
    /// Półsamoczynna (KOMPUTERY.pdf 1.6.2), dla każdego końca: Pwl – przeciwwtórność liniowa (po sygnale zezwalającym na wyjazd,
    /// do bloku początkowego Po); PoReady – można dać Po (sygnał wygaszony albo dPo); EntryOk – semafor wjazdowy wyświetlił sygnał
    /// zezwalający dla przyjmowanego pociągu (warunek Ko; dKo go zastępuje przy wjeździe na Sz / rozkaz pisemny).
    /// </summary>
    public bool[] Pwl = new bool[2], PoReady = new bool[2], EntryOk = new bool[2], DKo = new bool[2];
    /// <summary>Liczniki użyć przycisków doraźnych dPo / dKo.</summary>
    public int DPoCount, DKoCount;
    public bool Busy => ReleaseUntil != null || AzkUntil != null;
}

public class PowerState
{
    public bool Mains = true, Ups = true, Gen, GenAuto = true, GenFail, NN = true;
    public double? OutageAt;
}

public class RouteDef
{
    public string Id, Start, End, EndKind, Type, EndNode, Overlap;
    public List<string> Edges = new(), Secs = new(), Ders = new();
    public List<(string Num, string Pos)> Sw = new();
    /// <summary>Ochrona boczna: Kind „sw” (zwrotnica w położeniu Pos), „der” (wykolejnica założona), „sig” (sygnalizator „Stój”); For – zwrotnica przebiegu, którą chroni.</summary>
    public List<(string Kind, string Id, string Pos, string For)> Flank = new();
    public int Minus, Variant;
    public string FlankText => string.Join(" ", Flank.Select(f => f.Kind switch { "sw" => Model.Station.Lbl(f.Id) + f.Pos, "der" => Model.Station.Lbl(f.Id), _ => Model.Station.Lbl(f.Id) + "(S1)" }));
    public string SwText => string.Join(" ", Sw.Select(q => Model.Station.Lbl(q.Num) + q.Pos));
    /// <summary>Klucz przebiegu w regułach edytora: położenia zwrotnic (posortowane).</summary>
    public string SwKey => string.Join(" ", Sw.Select(q => q.Num + q.Pos).OrderBy(x => x, StringComparer.Ordinal));
    public bool Off;
    /// <summary>Prędkość przebiegu w granicach stacji [km/h]: z torów i zwrotnic (VCalc) albo ustalona w edytorze (VSet).</summary>
    public double VCalc, VSet;
    public double VMax => VSet > 0 ? VSet : VCalc;
}

public class Route : RouteDef
{
    public string Uid, State = "setting";
    public int Rel;
    public HashSet<string> WasOcc = new();
    public bool Passed, SlowAlarm, ManualStop;
    public double T0;
    public double? TimeRelUntil, DoneAt;
}

public class Pending
{
    public string Code, Arg, Arg2, Line, Desc;
    /// <summary>Stanowisko, które zainicjowało polecenie.</summary>
    public string Desk;
    public List<string> Targets = new();
    public double Until, NotBefore;
    public List<string> Notes = new();
}

/// <summary>Wpis rejestratora zdarzeń – kolumny jak na pulpicie LCS.</summary>
public class EventRecord : System.ComponentModel.INotifyPropertyChanged
{
    public double T;
    /// <summary>Numer kolejny wpisu (synchronizacja rejestratora na stanowiskach sieciowych).</summary>
    public long Id;
    public string Czas { get; set; }
    public string Obszar { get; set; } = "LCS";
    public string Stacja { get; set; } = "";
    public string TypObiektu { get; set; }
    public string Obiekt { get; set; }
    public string Kategoria { get; set; }
    public string Zdarzenie { get; set; }
    public string Operator { get; set; }
    public string Notatka { get; set; } = "";
    public string Zrodlo { get; set; } = "System";
    bool ack, active = true;
    public bool Ack { get => ack; set { ack = value; Changed(nameof(Ack)); Changed(nameof(Stan)); } }
    /// <summary>Czy utrzymuje się stan alarmowy (Ie-104 § 7 ust. 5).</summary>
    public bool Active { get => active; set { active = value; Changed(nameof(Active)); Changed(nameof(Stan)); } }
    public string Stan => (Ack ? "potw." : "NIEPOTW.") + (Active ? " / trwa" : " / ustąpił");
    public string Key;
    [System.Text.Json.Serialization.JsonIgnore] public Func<bool> StillActive;
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    void Changed(string n) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
}

public class BookEntry
{
    public string Czas { get; set; }
    public string Uzytkownik { get; set; }
    public string Tresc { get; set; }
    public string Automatyk { get; set; }
    public string ForCode;
}

public class User { public string Name, Role; }

public class Stats
{
    public int Cmds, Special, Rejected, TrainsIn, TrainsOut, WrongDir, Spad;
    public double DelaySum;
}
