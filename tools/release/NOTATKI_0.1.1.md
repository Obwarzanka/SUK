Poprawka skryptów stacji. Programy i mod SimRailMost 0.11.2 – bez zmian względem 0.1.0.

## Zmiany
- `stacje\nowa_stacja.cmd` – gdy lista zrzutów pulpitów jest pusta, skrypt pokazuje, gdzie szuka, i sprawdza po kolei:
  czy mod jest w `Mods`, czy mod się uruchomił (`UserData\SimRailMost`), czy MelonLoader działa; wypisuje ostatnie
  wpisy moda z `MelonLoader\Latest.log`. Wcześniej pokazywał tylko pustą listę.
- `nowa_stacja.cmd`, `edytuj_zobrazowanie.cmd`, `edytuj_ut.cmd`, `uruchom_pulpit.cmd` – dwuklik bez argumentu pokazuje listę stacji i pyta o nazwę.
- `INSTRUKCJA.md` – instrukcja krok po kroku. Zrzut pulpitu działa tylko na stacjach z komputerowym pulpitem EBI
  (np. Koluszki, Łazy); nastawnie przekaźnikowe Domino (np. Łazy Ła) i mechaniczne się nie zapisują.

## Wymagania
Windows 10/11 x64, .NET Desktop Runtime 10.0 (≥ 10.0.13), Python 3 (skrypty stacji), SimRail + MelonLoader 0.7.x.

Instrukcja: `INSTRUKCJA.md` w paczce. Wersja rozwojowa.

Paczka nie zawiera rysunków stacji z gry – każdy buduje je u siebie ze swojego zrzutu pulpitu.
