Pierwsze wydanie SUK – symulatora urządzeń komputerowych LCS współpracującego z SimRail.

## W paczce
- **Pulpit LCS** (`programy\SUK\LcsWidzew.exe`) – symulator, pulpit zdalny SimRail, EDR.
- **Edytor zobrazowania** (`programy\EdytorZobrazowania2`) – obrazy pulpitu wg Ie-104.
- **Edytor UT** (`programy\EdytorUT`) – układ torowy w PUWG 1992, symbole Ig-10.
- **Łączność / EZP** (`programy\Lacznosc`) – czat dyżurnych SimRail, EZP z szablonów gry, podpowiedzi odpowiedzi, plan ruchu z API SimRail.
- **Mod SimRailMost 0.11.2** (`mod\SimRailMost.dll`, MelonLoader 0.7.x) – stany z gry, polecenia, czat, zrzut pulpitu EBI.
- **Skrypty stacji** (`stacje\`) – nowa stacja z pulpitu gry jednym poleceniem: `nowa_stacja.cmd "<Posterunek>"`,
  potem `edytuj_zobrazowanie.cmd`, `edytuj_ut.cmd`, `uruchom_pulpit.cmd`. Katalog gry wykrywany ze Steam.

## Wymagania
Windows 10/11 x64, .NET Desktop Runtime 10.0 (≥ 10.0.13), Python 3 (skrypty stacji), SimRail + MelonLoader 0.7.x.

Instrukcja: `CZYTAJ.md` w paczce. Wersja rozwojowa.

Paczka nie zawiera rysunków stacji z gry – każdy buduje je u siebie ze swojego zrzutu pulpitu.
