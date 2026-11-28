# SUK – instrukcja krok po kroku

Rób po kolei. Nie pomijaj kroków. Każdy krok robisz **tylko raz**, chyba że jest napisane inaczej.

---

## CZĘŚĆ 1. Instalacja (raz na komputer)

### Krok 1. Zainstaluj .NET
1. Wejdź na: https://dotnet.microsoft.com/download/dotnet/10.0
2. Znajdź napis **.NET Desktop Runtime**.
3. Przy **Windows** kliknij **x64** (w kolumnie „Installers”).
4. Otwórz pobrany plik, kliknij **Zainstaluj**, potem **Zamknij**.

### Krok 2. Zainstaluj Pythona
1. Wejdź na: https://www.python.org/downloads/
2. Kliknij duży żółty przycisk **Download Python**.
3. Otwórz pobrany plik.
4. **WAŻNE:** na dole okna zaznacz ptaszkiem **„Add python.exe to PATH”**.
5. Kliknij **Install Now**. Na końcu **Close**.

### Krok 3. Zainstaluj MelonLoader w SimRail
1. Wejdź na: https://github.com/LavaGang/MelonLoader/releases
2. Pobierz plik **MelonLoader.Installer.exe** i go otwórz.
3. Wybierz grę **SimRail** (albo wskaż plik `SimRail.exe` w folderze gry).
4. Wersja: **0.7.x**. Kliknij **Install**.
5. Uruchom SimRail **raz**, poczekaj, aż dojdzie do menu, i **zamknij grę**.

> Folder gry znajdziesz tak: Steam → prawy klik na SimRail → Zarządzaj → Przeglądaj pliki lokalne.

### Krok 4. Pobierz SUK
1. Wejdź na: https://github.com/Obwarzanka/SUK/releases
2. Pobierz plik **SUK-…-win-x64.zip**.
3. Prawy klik na pobrany plik → **Właściwości** → na dole zaznacz **Odblokuj** (jeśli jest) → **OK**.
4. Prawy klik na plik → **Wyodrębnij wszystkie…** → wpisz `C:\SUK` → **Wyodrębnij**.

> Nie wypakowuj do „Program Files” ani na Pulpit w OneDrive. `C:\SUK` jest dobre.

### Krok 5. Zainstaluj mod
1. **Gra musi być wyłączona.**
2. Otwórz folder `C:\SUK\SUK-…-win-x64`.
3. Dwuklik **`zainstaluj_mod.cmd`**.
4. Ma się pojawić: **„Mod SimRailMost zainstalowany”**. Naciśnij dowolny klawisz.

---

## CZĘŚĆ 2. Robisz nową stację (raz na stację)

> **Koluszki są już w paczce** (`stacje\Koluszki`). Dla Koluszek pomiń Kroki 6 i 7 – idź od razu do **Kroku 8**.

### Krok 6. Zapisz pulpit stacji z gry
1. Uruchom SimRail.
2. Wejdź na wybraną stację **jako dyżurny ruchu**.
3. Poczekaj **10 sekund**. Mod sam zapisuje rysunek pulpitu. Nic nie klikasz.
   Działa tylko na stacjach z **komputerowym pulpitem (ekrany EBI)**, np. Koluszki, Łazy. Nastawnie przekaźnikowe Domino (np. Łazy Ła) i mechaniczne się nie zapisują.
4. Możesz grać dalej albo wyjść.

### Krok 7. Zrób stację w SUK
1. Otwórz folder `stacje`.
2. Dwuklik **`nowa_stacja.cmd`**.
3. Pokaże się lista stacji (np. `Koluszki`). **Wpisz nazwę** dokładnie jak na liście i naciśnij **Enter**.
4. Na końcu ma być napis **„Gotowe”**. Naciśnij dowolny klawisz.

Powstał folder `stacje\<nazwa stacji>` – to Twoja stacja.

---

## CZĘŚĆ 3. Grasz (za każdym razem)

### Krok 8. Pulpit SUK połączony z grą
1. Uruchom SimRail i wejdź na swoją stację jako dyżurny.
2. W folderze `stacje` dwuklik **`uruchom_pulpit.cmd`**, wpisz nazwę stacji, **Enter**.
3. Otworzy się pulpit SUK – od razu połączony z grą (pulpit zdalny SimRail). Zaloguj się jako dyżurny.
4. Gotowe – pulpit SUK pokazuje stan z gry, a polecenia z SUK idą do gry.
   (Gdyby pulpit nie połączył się z grą: przycisk **„SimRail”** → **„Pulpit zdalny SimRail”**.)

### Krok 9. Łączność z innymi dyżurnymi (EZP)
1. Gra musi działać, Ty na stacji jako dyżurny.
2. Dwuklik `programy\Lacznosc\Lacznosc.exe`.
3. Po lewej klikasz stację sąsiednią, po prawej wybierasz komunikat w **EZP**, wpisujesz pociąg i tor, klikasz **Wyślij**.
4. Na dole: wybierz serwer i kliknij **„Pobierz rozkład”** – zobaczysz pociągi.

---

## CZĘŚĆ 4. Poprawianie wyglądu stacji (nieobowiązkowe)

- Dwuklik `stacje\edytuj_zobrazowanie.cmd` → wpisz nazwę stacji → otwiera się edytor rysunku pulpitu. Zapisz (**Ctrl+S**) i zamknij.
- W edytorze zakładka **„Gra”** (po prawej): pomarańczowe znaczniki to tory i elementy bez obiektu w grze. Kliknij pozycję z listy „Do przypięcia”, wybierz ID z listy i **„Przypnij”** albo **„Bez obiektu gry”**.
- Następne `uruchom_pulpit.cmd` pokaże już poprawiony rysunek.
- Jak zepsujesz rysunek: usuń plik `stacje\<stacja>\edytor\<stacja>.zob.json` i zrób **Krok 7** jeszcze raz.

---

## Coś nie działa?

| Co widzisz | Co zrobić |
|---|---|
| „Windows ochronił ten komputer” | Kliknij **Więcej informacji** → **Uruchom mimo to**. |
| Program się nie otwiera albo pisze o .NET | Zrób jeszcze raz **Krok 1**. |
| „Brak Pythona 3” | Zrób jeszcze raz **Krok 2** i **pamiętaj o ptaszku „Add python.exe to PATH”**. Potem uruchom komputer ponownie. |
| „Nie znaleziono katalogu SimRail” | Otwórz `stacje\ustawienia.cmd` w Notatniku. W linii `rem set "SIMRAIL_DIR=..."` usuń `rem ` na początku i wpisz po `=` swój folder gry. Zapisz. |
| „W … nie ma MelonLoadera” | Zrób jeszcze raz **Krok 3**. |
| „Zamknij grę przed instalacją moda” | Wyłącz SimRail i zrób **Krok 5** jeszcze raz. |
| Lista stacji w Kroku 7 jest pusta | `nowa_stacja.cmd` wypisze pod spodem, co jest nie tak (`[X]`). Zrób to, co podpowiada; jeśli nie pomaga, wyślij autorowi cały tekst z okna. |
| „Brak zrzutu pulpitu” | Źle wpisana nazwa. Wpisz dokładnie jak na liście (np. `Lodz_Widzew`). |
| Pulpit SUK szary, nic się nie zmienia | Gra musi działać, Ty na tej samej stacji jako dyżurny, a w SUK włączony **„Pulpit zdalny SimRail”** (Krok 8). |
| Okno z błędem w programie | Wyślij autorowi plik `bledy.log` z folderu tego programu (`programy\…`). |

---

**Ważne:**
- Na **publicznych serwerach** SimRail wysyłanie poleceń z SUK może łamać regulamin. Graj na serwerze prywatnym albo w grze jednoosobowej.
- Rysunki stacji (`stacje\<stacja>`) pochodzą z gry SimRail – tylko do własnego użytku.
