# SUK i SimRail – pulpity zdalne posterunków gry

SUK działa na dwa sposoby i **nie mieszają się**:

| | SUK z UT | SUK z SimRailem (pulpit zdalny) |
|---|---|---|
| Układ | `app/LcsWidzew/Data/station.json`, `Data/projekty/*.json` | `<stacje>\<układ>\<układ>_zob.json` |
| Skąd rysunek | Edytor UT → Edytor zobrazowania → „Do symulatora” | zrzut pulpitu EBI z gry → `pulpit_do_zob.py` → Edytor zobrazowania → `zob_do_suk.py` |
| Zależności | własny komputer zależności SUK | gra (mod SimRailMost); SUK tylko pokazuje stany i wysyła polecenia |
| Start | `uruchom.bat` (okno wyboru: część „SUK – układ torowy”) | `posterunek.cmd uruchom <układ>` (`--simrail`) albo okno wyboru: część „SimRail – pulpit zdalny” |
| Kod w SUK | `app/LcsWidzew/Sim`, `Model`, `Net` | `app/LcsWidzew/SimRail` (`LcsWidzew.SimRail`), w edytorze – zakładka „Gra” |

Układ SUK z UT może też mieć mapę SimRail (`<układ>.simrail.json`) i działać z grą z okna „SimRail” – to wyjątek do prób, nie zwykła droga.

## Katalog stacji

Ten sam w paczce wydania (`stacje\`) i u autora (`<SimRail>\UserData\SimRailMost\suk\stacje`):

```
stacje\posterunki.json                    rejestr: posterunek gry → kod SUK → układ
stacje\<układ>\edytor\<układ>.zob.json     zobrazowanie – projektuje człowiek (Edytor zobrazowania)
stacje\<układ>\edytor\<układ>.simrail.json mapa: obraz → element / odcinek → ID obiektu gry (zakładka Gra)
stacje\<układ>\edytor\<układ>.ut.json      UT z wpisami posterunków (do pulpitu zdalnego niepotrzebny)
stacje\<układ>\edytor\<układ>.przypiecia_gry.json  stan przypięć (konwerter; tylko do odczytu)
stacje\<układ>\<układ>_zob.json            pulpit SUK – budowany od nowa przy każdym „uruchom”
stacje\<układ>\<układ>_zob.simrail.json    ID gry → obiekt SUK (dla pulpitu), .przebiegi_gry.json, .nieprzypisane.txt, dzienniki EDR
```

**Rejestr** (`posterunki.json`): `nazwaGry` (jak w grze), `kod` (prefiks id w SUK – `KO:…`; nie zmienia się, bo od niego zależą id
odcinków i dzienniki EDR), `uklad` (katalog; kilka posterunków z tym samym układem = jeden pulpit), `pulpit` (`ebi` / `domino`),
opcjonalnie `skalaX` (rozciągnięcie rysunku w poziomie; domyślnie 1,5). Przy pierwszym `nowy` kod jest z ekranu pulpitu gry
(`EBI300ScreenStatus.prefix`), potem tylko z rejestru.

## Polecenie `posterunek`

`tools\simrail\posterunek.cmd` (w paczce `stacje\posterunek.cmd`; stare `nowa_stacja.cmd`, `uruchom_pulpit.cmd`, `edytuj_*.cmd` wołają to samo):

| polecenie | co robi |
|---|---|
| `posterunek lista` | rejestr i stan układów: zrzut z gry, nieprzypisane, czy pulpit aktualny; zrzuty bez układu |
| `posterunek nowy "Łazy" [KOD] [--uklad X]` | najnowszy zrzut pulpitu → wpis w rejestrze, UT z posterunkiem, zobrazowanie, mapa, pulpit SUK. Gdy zobrazowanie już jest – wersja z gry obok (`_z_gry_nowy.zob.json`), poprawek nie rusza |
| `posterunek odswiez <układ>` | nowy zrzut obok i `<układ>_zmiany_z_gry.txt`: obiekty gry nowe / usunięte względem mapy |
| `posterunek buduj <układ>` / `--wszystkie` | pulpit SUK z zobrazowania |
| `posterunek uruchom <układ>` | buduj + SUK od razu jako pulpit zdalny SimRail |
| `posterunek edytuj <układ> [ut]` | stan przypięć + Edytor zobrazowania (albo Edytor UT) |
| `posterunek przypiecia <układ>` | sam stan przypięć do zakładki Gra |

Ścieżki: `SIMRAIL_DIR` (inaczej Steam), `SUK_STACJE`, `SUK_ZRZUTY`, `SUK_PROGRAMY` (paczka). Bez nich – programy z `app\*\bin\Release`.

## Mapowanie obiektów gry – zakładka „Gra” w Edytorze zobrazowania

Widoczna tylko przy zobrazowaniu z mapą gry (`<układ>.simrail.json` obok). Stan każdego elementu i odcinka z konwertera
(jedno źródło reguł: `zob_do_suk.py --tylko-przypiecia`): z mapy / po numerze / po położeniu / po sąsiadach / bez obiektu gry / brak.

- **Przelicz (F5)** – zapisuje zobrazowanie i liczy stan od nowa (potrzebny Python 3).
- Zaznacz element (semafor, tarcza, zwrotnica – także klik w rozjazd) albo tor → pole ID gry z kandydatami (najpierw z konwertera,
  potem wszystkie znane z mapy) → **Przypnij** / **Bez obiektu gry** (`-` w mapie: świadomie, bez wpisu w `nieprzypisane.txt`) /
  **Usuń** (konwerter znów zaproponuje).
- Lista **Do przypięcia** (klik – zaznaczenie na płótnie), **Obiekty gry bez miejsca na rysunku**; na płótnie pomarańczowe znaczniki.
- Przypięcie zapisuje się od razu w mapie (poprzednia wersja `.bak`); cofnij / ponów zobrazowania go nie dotyczy.

## Pliki

| plik | |
|---|---|
| `posterunek.py`, `posterunek.cmd` | jedno polecenie (wyżej) |
| `pulpit_do_zob.py` | zrzut pulpitu gry → zobrazowanie + mapa |
| `zob_do_suk.py` | zobrazowanie + mapa → pulpit SUK; `--tylko-przypiecia`, `--zrzuty`, `--rejestr` |
| `pulpit_do_suk.py`, `pulpit_png.py` | starszy pulpit z siatki EBI (bez edytora), podgląd PNG |
| `pusty_ut.py` | pusty UT (zastąpiony przez `posterunek nowy`; zostaje dla starych skryptów) |
| `diagnostyka\` | `zwrotnica.cmd`, `sygnal.cmd` (polecenia do gry przez mod), `przejazdy_*` (podgląd przejazdów, P0) |

Konwertery były w `SimRailMost\narzedzia` (poza gitem) – od 2026-11-28 jedynym źródłem jest ten katalog; paczka wydania kopiuje je stąd.
W `SimRailMost` zostaje sam mod; jego stare skrypty `.cmd` wołają `posterunek.cmd`.
