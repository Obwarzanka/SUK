# ISDR – zdjęcia stanowiska szkoleniowego (wzór pulpitu, poleceń i dzienników)

Zdjęcia właściciela z 2026-10-20, stanowisko szkoleniowe ISDR, stacje szkoleniowe **Anastazów** i **Brenna**
(linia Dobrociny – Anastazów – Brenna – Cyranka). Oryginały: `../Zrodla/zdjecia symulator/` (poza repozytorium),
zmniejszone kopie (1600 px): `Zrodla/ISDR_zdjecia/`, klatki z filmu wyboru przebiegu: `Zrodla/ISDR_klatki/`.

**Decyzja właściciela 2026-10-20:** panel „Polecenia / Historia” zastępuje menu PPM; opisy (napisy przycisków,
nagłówki, kolejność) mają być **zgodne ze zdjęciami**. Szczegóły w `ROZWOJ.md` § 2a i etapie 4a.

Poniżej opisano tylko to, co widać na zdjęciach. Znaczenie kodów, których nie ma w naszej liście
(`Sim/Pulpit/SystemNadrzedny.cs`, `Codes`), jest do potwierdzenia przez właściciela (`ROZWOJ.md` § 5a).

## 1. Wybór przebiegu – film `20261020_095238.mp4` (Anastazów, przebieg pociągowy R → 1B)

| Klatka | Działanie dyżurnego | Pulpit | Panel „Polecenia” |
|---|---|---|---|
| `1_przed_wyborem.jpg` | – | nic nie wybrane | pusty |
| `2_klik_R_konce.jpg` | klik na semaforze R | R w ramce (w chwili kliknięcia żółtej, potem turkusowej); na **wszystkich możliwych końcach** – trójkątach „koniec przebiegu wyjazdowego” przy semaforach wjazdowych W (2B) i U (1B) – **turkusowe kółko z wypełnionym turkusowym kwadratem** | nagłówek „R”, przyciski **STOP, OSTOP, SZ, SZI, NSZ** (SZI na czerwonym tle) |
| `3_najazd_na_koniec_U.jpg` | najazd myszą na koniec U | kwadrat U **żółty**; przy najeździe widać też żółty obrys odcinka 21 – U | bez zmian |
| `4_wybrany_koniec_menu_R_1B.jpg` | klik na U | kółka znikają; R i trójkąt U w **turkusowych ramkach** | nagłówek „R 1B”, przyciski **POCIĄG, MANEWR, ZD, ZDP, OPS, ZDPP** |
| (7,5 s) | klik POCIĄG | bez zmian | – |
| `5_POCIAG_R_1B_Wyslij.jpg` | – | ramki R i U **zielone** (polecenie przygotowane, niewysłane) | nagłówek „POCIĄG R 1B”, jeden przycisk **Wyślij** |
| `6_po_wyslaniu.jpg` | klik Wyślij | ramki znikają; droga jeszcze szara (film kończy się ok. 1 s później – ułożenia przebiegu nie widać) | pusty |

Zgodne z naszym § 2a: turkusowa ramka początku, turkusowe kółko z kwadratem na końcach, ramka na trójkącie
semafora wjazdowego, szara droga w czasie przekładania. Nowe: wybór rodzaju przebiegu w panelu, zielone ramki
i „Wyślij”, żółty podgląd przy najeździe.

## 2. Panel „Polecenia / Historia” (prawy górny róg obrazu stacji)

- Ramka z dwiema zakładkami: **Polecenia** i **Historia**, niebieskie tło.
- Zakładka Polecenia: w pierwszym wierszu nazwa wybranych obiektów (np. „R”, „R 1B”, „An”), pod nią przyciski poleceń.
  Po wybraniu polecenia – nagłówek „KOD obiekty” (np. „POCIĄG R 1B”) i przycisk **Wyślij**.
- Zestawy przycisków widoczne na zdjęciach:
  - semafor (R): `STOP` `OSTOP` `SZ` `SZI` `NSZ` (zdjęcia `095020`, `095021`);
  - przebieg początek–koniec (R 1B): `POCIĄG` `MANEWR` `ZD` `ZDP` `OPS` `ZDPP` (`095030`);
  - stacja – klik na ikonę nastawni „An” (`An`): `SSS` `OSSS` `ZSS` `OZSS` `OSZ` (`095036`).
- Część przycisków jest jasna, część ciemniejsza (przy „An” jasne SSS i ZSS, ciemne OSSS, OZSS, OSZ; przy „R” jasny STOP,
  ciemne OSTOP, SZ, NSZ, SZI czerwony; przy „R 1B” jasne POCIĄG i MANEWR). **Właściciel 2026-10-20: ciemne = niedostępne w danym stanie.**
  Stąd SZ jest ciemne przed SZI: najpierw inicjalizacja (SZI), po 5 s dostępne wykonanie (SZ) – Ie-104 § 11 pkt 13.
- Zakładka **Historia**: tabela z kolumnami **Czas, Polecenie, Przyjęcie, Realizacja** (`094940`).

## 2a. Kody z panelu a Ie-104 § 12

| Kod ISDR | Gdzie | Ie-104 § 12 | Nasz kod (`SystemNadrzedny.Codes`) | Stan wiedzy |
|---|---|---|---|---|
| STOP | semafor | Stop – zastopowanie sygnalizatora (zwykłe) | STOP | pewne |
| OSTOP | semafor | oStop – odwołanie zastopowania | OSTOP | pewne |
| SZ | semafor | SZ – sygnał zastępczy (specjalne) | SZ | pewne |
| NSZ | semafor | NSZ – SZ przy wyjeździe na tor szlaku dwutorowego w kierunku przeciwnym do zasadniczego (specjalne) | dziś alias SZ | pewne (Ie-104); u nas uproszczone. **Właściciel 2026-10-20:** inicjalizacja taka sama jak dla SZ (SZI, po 5 s NSZ) |
| SZI | semafor | brak (Ie-104 § 11 pkt 13 – inicjacja polecenia specjalnego) | brak | **właściciel 2026-10-20:** inicjalizacja polecenia specjalnego wyświetlenia sygnału zastępczego (1. etap); wykonanie – SZ (2. etap, po 5 s) |
| POCIĄG | przebieg | Pociąg – nastawianie przebiegu pociągowego | POCIAG | pewne |
| MANEWR | przebieg | Manewr – nastawianie przebiegu manewrowego | MANEWR | pewne |
| ZDP | przebieg | ZDP – zwolnienie doraźne natychmiastowe przebiegu pociągowego w całości | ZDP | pewne; **właściciel:** bez zwłoki |
| ZD | przebieg | brak (najbliżej ZDP / ZDM – natychmiastowe) | brak | **właściciel 2026-10-20:** zwolnienie doraźne **bez zwłoki** (natychmiastowe). Wcześniejsze „ZDI” – pomyłka, takiego przycisku nie ma. |
| OPS | przebieg / polecenie specjalne | brak | brak | **właściciel 2026-10-20:** odwołanie polecenia specjalnego (wycofanie zainicjowanego, niewykonanego – Ie-104 § 11 pkt 17) |
| ZDPP | przebieg | odpowiednik ZCZ – zwolnienie doraźne przebiegu pociągowego ze zwłoką czasową | ZCZ | **właściciel 2026-10-20:** zwolnienie doraźne **ze zwłoką 90 s** |
| SSS | stacja („An”) | brak | dziś alias Stój (błędnie) | **właściciel 2026-10-20:** zastopowanie wszystkich sygnalizatorów stacji |
| OSSS | stacja | brak | brak | **właściciel:** odwołanie SSS |
| ZSS | stacja | brak | brak | **właściciel:** zastopowanie wszystkich zwrotnic stacji |
| OZSS | stacja | brak | brak | **właściciel:** odwołanie ZSS |
| OSZ | stacja | oSZ (odwołanie SZ) jest dla sygnalizatora – inne polecenie o tej samej nazwie | OSZ | **właściciel:** odświeżenie stanu komputera zobrazowania (na nastawni „An”, nie na sygnalizatorze) |

Ie-104 § 11 pkt 22–24: system może mieć polecenia dodatkowe (skrót do 8 znaków, różny od kodów § 12); kody § 12 nie mogą
robić nic innego niż opisano. Kody ISDR spoza § 12 są więc dopuszczalne jako dodatkowe – ale ich działanie musi podać właściciel.

## 3. Obraz stacji (pulpit ISDR)

- Nagłówek: „Zalogowany: ISDR” (żółty, mały), pod nim nazwa stacji wielkimi literami (żółta); logo ISDR i przyciski ZDARZENIA / ALARMY na górze.
- Tory jasnoszare, grube; połączenia torów poziomo – pionowo – poziomo z zaokrąglonymi narożnikami.
- Oznaczenia semaforów żółte (A, D…W, ToA, ISpN), tarcz manewrowych i wykolejnic turkusowe (1, 2, 11, 21, 22, 1k, 3k, 4k), numery zwrotnic i torów białe.
- **PIP:** na zajętym torze stacyjnym czerwone pole z numerem pociągu (333012, 3321, 33221, 339012).
- **SBL na szlaku:** odstępy jako osobne kreski z podpisami (it22, it36, it48 / it9, it23, it37); żółta strzałka kierunku blokady przy 2B / 1B; opis „Tor 2B / Tor 1B” z kreską (czerwona przy zajętości szlaku); szary prostokąt obok (pole).
- Ramki „Peron 1”, „Peron 2”; ikona nastawni z nazwą w cudzysłowie („An”, „Br”).
- Zegar symulacji zielony, duży, w lewym dolnym rogu obrazu (08:00:07).
- W prawym dolnym rogu obrazu – ikony (paleta barw, głośnik / dźwięk, czerwona ramka).

## 4. ZDARZENIA i ALARMY (dolna część pulpitu)

- Dwie tabele jedna pod drugą. Nad każdą: etykieta (ZDARZENIA – niebieska, ALARMY – czerwona), **zakres dat od – do**
  (np. „19.10.2026 00:00” – „19.10.2026 23:59”, z rozwijanym kalendarzem).
- Kolumny ZDARZEŃ: **Czas, Obszar, Stacja, Typ obiektu, Obiekt, Kategoria, Zdarzenie, Operator, Notatka, Źródło**;
  przy kolumnach Obszar, Stacja, Typ obiektu, Obiekt, Kategoria – **lejek filtra**.
- Kolumny ALARMÓW: Czas, Obszar, Stacja, Typ obiektu, Obiekt, Kategoria, Zdarzenie, Operator, Źródło (dwie wąskie kolumny na początku – znaczniki).
- Pasek nad tabelą: „Zostaw na wierzchu” (pole wyboru), „Liczba zdarzeń: 69”, „Strona: 1 / 8”, przycisk **Online**, przewijanie stron |◀ ◀ ▶ ▶|; przy alarmach dodatkowo „Wycisz” i „Liczba alarmów”.
- Przykładowy wpis: `2026-10-19 08:00:03 | An | Anastazów | Kontrola niezajętości | IT1 | Zwykłe zdarzenia | zajęty | - | - | PC AB`.

## 5. EDR – elektroniczny dziennik ruchu (osobny monitor; etap na później)

- Logowanie „EDR Produkcja – Logowanie” (Login, Hasło, identyfikator aplikacji, „BRENNA (STACJA)”).
- Lista pociągów: Prz. plan., +/-, Prz. rzecz., Rodz. (ROJ, EIE, TKS, LTE, ZXM, TME), Nr poc. (nieparzysty / parzysty),
  Z kierunku post., W kierunku post., Tor, Nr L., P. zam., P. obl., Typ p., PT, Odj. plan., +/-, Odj. rzecz.,
  Stacja początkowa, Stacja końcowa, Przewoźnik; „Informacje dodatkowe” Z / Do / P; „Czas pobrania danych z bazy”, zegar.
- Dzienniki szlaków (zakładki np. „Anastazów / Cyranka”, „Dziennik telefoniczny”): Nr pociągu (1 nieparzysty, 2 parzysty),
  Tor stacyjny, Droga wolna, Poc. odjechał, Poc. przyjechał, Uwagi (kolumny 1–7); „Prowadzi:”, Zapisz, przewijanie dat.
- Okna: „Wprowadzenie gotowości pociągu do odjazdu” (data, godzina, nazwisko maszynisty, „Wprowadź analizę pociągu”,
  TN / Zapisz / Anuluj), „Wprowadzenie Analizy Pociągu” (brutto i długość – rzeczywiste i planowe, do 3 lokomotyw:
  seria, numer, brutto, długość, VMax, liczba osi, trakcja), „Szlaki posterunku” (Do / Z posterunku, linia, nazwa dziennika ruchu).

## 6. Pozostałe monitory

- **ISDR/SSD – Sieciowy System Diagnostyki Taboru** (`101645`): Alarm / POCIĄG OK, TEST OK, raport z urządzenia
  (np. SD Brenna 1, ASDEK/PM/GM/GH/OK: prędkość, liczba osi, długość, przekroczenia GML/GMP/GMR/GHL/GHP), przyciski F1–F10.
- Panel łączności „DROG.” (wywołanie 1–3, nasłuch, głośność) – nad pulpitem.
- Monitor instruktora (z lewej).
