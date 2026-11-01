# EDR – Elektroniczny Dziennik Ruchu (notatki ze zdjęć)

Źródło: `Zrodla/EDR/` – 59 zdjęć wydruku **„EDR – Podręcznik użytkownika”** (PKP PLK S.A., autor Piotr Marszewski,
wydruk 2017-06-14, ok. 55 stron) oraz film `20261027_111725.mp4` (75 s, EDR na stanowisku ISDR Anastazów).
Film to ten sam plik, który opisano w `ISDR_zdjecia.md` § 5 i § 7.7. Tutaj są tylko rzeczy nowe z podręcznika.
Przykłady w podręczniku dotyczą posterunku Warszawa Zachodnia (konfiguracja: Kluczbork, wyciąg: Wałbrzych Miasto).

## 1. Po co jest EDR (rozdz. 1)

- Zastępuje papierowy **dziennik ruchu R-146/R-147** (§ 36 Ir-1) i **dziennik telefoniczny**, osobny dla każdego szlaku.
- Dane o wykonaniu rozkładu trafiają od razu do SEPE / SWDR. Dyżurny nie musi wpisywać tego samego dwa razy.
- Wiersze dziennika wypełniają się same z wewnętrznego rozkładu jazdy (WRJ). Telefonogramy pisze się z wzorów.

## 2. Uruchomienie i użytkownicy (rozdz. 3–5)

- Logowanie: identyfikator PLK (np. `PLK012345`) i hasło. Przy pierwszym uruchomieniu trzeba wskazać lokalizację:
  „Znajduję się na posterunku ruchu: [lista]” albo „poza posterunkiem ruchu”.
- Role: **Operator** (dyżurny) i **Koordynator** (spółki, zakładu, sekcji, posterunku). Do tego **obserwatorzy**
  spółki, zakładu i sekcji: mają tylko podgląd dzienników, zajętości torów i konfiguracji i mogą robić wyciągi.
- Struktura: spółka → zakład → sekcja eksploatacji → posterunek. Koordynator przypisuje ludzi do posterunków.

## 3. Konfiguracja posterunku (rozdz. 5.2)

Okno ma zakładki: **Szlaki przyległe – Dzienniki ruchu**, Układ dzienników, Tory stacyjne, Wymagane sprawdzenia,
Podgląd WRJ, Inne. Na pierwszej zakładce są trzy listy:

1. **Szlaki przyległe**: linia (nr, nazwa), szlak lub odstęp Z/DO (posterunek końcowy), liczba torów,
   kierunek parzysty, nazwa dziennika ruchu (np. „Warszawa Aleje Jerozolimskie L. 445”). Jeden dziennik = jeden szlak,
   a przy kilku liniach do tego samego sąsiada każda ma osobny dziennik.
2. **Tory szlakowe** wybranego szlaku: tor lewy/prawy, numer, status (Otwarty…), zasadniczy rodzaj ruchu
   (jednokierunkowy / dwukierunkowy), zasadniczy kierunek (Z / DO posterunku sąsiedniego), kierunek parzysty,
   rodzaj urządzenia srk (np. „Półsamoczynna przekaźnikowa blokada liniowa typu Eap-94”),
   podstawa prowadzenia ruchu (np. „Jednoodstępowa (półsamoczynna) liniowa”).
3. **Posterunki dróżników przejazdowych** na szlaku: km, nazwa, sposób powiadamiania
   (jak na linii jednotorowej = godzina, jak na wielotorowej = ukośnik).

Jest **stan zasadniczy** szlaku (zapisany w konfiguracji) i **stan bieżący**. Stan bieżący zmieniają telefonogramy,
np. zapowiadanie telefoniczne, ruch dwukierunkowy na jednym torze albo zamknięcie toru.

## 4. Główne okno (rozdz. 6.3, 7.1)

- **Górna część: „Wykaz pociągów”** (inne zakładki: Opis pociągu, Trasa pociągu, Kody opóźnień, Przesyłki nadzwyczajne
  i TWR, Zarządzanie RJ, Skomunikowania, Towary niebezpieczne). To lista pociągów z WRJ odświeżana co N minut,
  z okresem ±N godzin. Kolumny są w `ISDR_zdjecia.md` § 5. Kolumna **PT** to tor stacyjny z rozkładu.
- **Prawy górny róg**: data i godzina oraz do 4 przycisków **cyklicznych sprawdzeń** (np. łączność z dróżnikami,
  oświetlenie semaforów). Przyciski są kolorowe i pokazują godzinę ostatniego/następnego sprawdzenia.
- **Dolna część**: dzienniki ruchu (każdy szlak na zakładce, po dwa obok siebie) i ostatnia zakładka „Dziennik telefoniczny”.
- **Pod dziennikami**: kontrolka zajętości torów stacyjnych, czyli rząd ponumerowanych kwadratów: zielony = wolny,
  czerwony = zajęty. To wyjaśnia pasek „1–6” z filmu, który w `ISDR_zdjecia.md` § 7.7 był opisany jako „znaczenie nieznane”.
- Na stopce: użytkownik, jednostka organizacyjna, „Zalogowany od” i „TRYB OPERATORA”.

## 5. Przyjęcie i wyprawienie pociągu (rozdz. 7.1.1–7.1.2)

- PPM na pociągu daje **Przyjmij pociąg** (z szlaku wynikającego z rozkładu, kolumna „Z kierunku posterunku”)
  albo **Przyjmij z innego szlaku**. To drugie otwiera okno „Szlaki posterunku” z wyborem szlaku.
  Tak samo działa **Wypraw pociąg** i **Wypraw na inny szlak**.
- Okno Przyjęcie/Wyprawienie ma pola: Nr pociągu, Tor stacyjny (lista), Droga wolna, Pociąg odjechał, Pociąg przyjechał
  (każde z polem wyboru), „Po torze lewym”, „Powrót ze szlaku”, Uwagi, Zapisz/Anuluj.
- Domyślne wartości:
  - **Nr pociągu**: z rozkładu, a **parzystość wymusza się wg kierunku szlaku**: aplikacja sama zmienia ostatnią cyfrę.
  - **Tor stacyjny**: przy przyjęciu z kolumny PT, przy wyprawieniu tor, na który pociąg przyjęto.
  - **Pociąg odjechał**: bieżąca godzina i minuta.
- Pola, które trzeba jeszcze wypełnić, mają **niebieskie tło**, a wiersz jest wtedy niekompletny. Które pola są wymagane,
  zależy od kierunku, toru szlakowego i bieżącej podstawy prowadzenia ruchu.
- Inne sposoby dodania wiersza: **dwuklik** na numerze pociągu w wykazie (EDR sam wybiera dziennik)
  albo **przeciąganie** numeru na zakładkę dziennika.

## 6. Dziennik ruchu (rozdz. 7.2)

- Kolumny: 1 Nr nieparzysty, 2 Nr parzysty, 3 Tor (stacyjny), 4 Droga wolna, 5 Pociąg odjechał, 6 Pociąg przyjechał,
  7 Uwagi, 8 „O jeździe pociągu zawiadomiono dróżników przejazdowych” (podkolumna dla każdego posterunku, z km w nagłówku).
- Nagłówek ma **ikony torów szlakowych** (lewy i prawy względem naszego posterunku):
  czerwona = tor zajęty, niebieska strzałka = kierunek jazdy pociągu (w dół = DO nas), szare strzałki = zasadniczy kierunek.
  Zamknięcie toru telefonogramem dodaje na ikonie osobny znak.
- Przyciski nad dziennikiem: pusty wiersz przyjęcia, pusty wiersz wyprawienia, powiadomienie dróżników,
  telefonogram, wpis o dowolnej treści (i) oraz stan bieżący szlaku (koło zębate).
- Strona dziennika = jedna doba. O północy dodaje się nowa strona, a jej pierwszy wpis mówi o zmianie doby.
  Nawigacja: |< < [data▾] > >| oraz ^ v (pierwszy i ostatni wpis). Nie da się przejść w przyszłość.
- Przy objęciu dyżuru wpis „Dyżur objął Jan Kowalski dnia … o godzinie …” pojawia się sam. „Prowadzi:” pokazuje nazwisko.
- Zapis do bazy idzie sam przy przejściu do innego wiersza. Przycisk „Zapisz” jest potrzebny tylko wtedy, gdy na stronie jest jeden wiersz.
- W filmie wiersz przed zatwierdzeniem jest **pomarańczowy**, a po zatwierdzeniu szary.

## 7. Telefonogram (rozdz. 7.3)

1. Klik ikony telefonogramu, potem **Nadaję / Odbieram**, data i godzina nadania, Nadał (zalogowany), Odebrał.
2. **Wzór telefonogramu** z listy, np. „[21] Zamknięcie toru szlakowego”, „[9] Żądanie wstrzymania wyprawiania pociągów”.
3. Szablon pojawia się na żółtym tle i ma pola wypełnione automatycznie, np.
   „Tor numer [3R] od [Warszawa Zachodnia] do [Warszawa Aleje Jerozolimskie] zamknięty od [23:20] z powodu (lub celem) [ ]”.
4. „Dodatkowa notatka” wpisuje się pod treścią. Po Zapisz telefonogram trafia na koniec dziennika **na całą szerokość**
   (pomarańczowe tło, pogrubiona treść, pod nią „Nadał … , odebrał … godz. …”).
5. Telefonogram może **zmienić stan bieżący szlaku**, np. zamknięty tor dostaje znak na ikonie toru szlakowego.

## 8. Powiadomienie dróżników przejazdowych (rozdz. 7.4)

- Dyżurny klika komórkę w kolumnie 8 przy pociągu. Jeśli posterunek ma sposób „jak na linii dwutorowej”, wpisuje ukośnik „\”.
  Jeśli „jak na linii jednotorowej”, wpisuje godzinę (np. 07:53). Niewypełniona komórka ma niebieskie tło.

## 9. Jazda do km i z powrotem (rozdz. 7.5)

- Ikona wyprawienia, potem w oknie Nr pociągu (np. „Rob. 1”), tor, droga wolna, odjechał i zaznaczone **„Powrót ze szlaku”**.
- W dzienniku powstają **dwa wiersze spięte klamrą**: wyjazd z uwagą „do km 57,987 i z powrotem” i powrót,
  w którym niebieskie pole „Pociąg przyjechał” wypełnia się po powrocie. Klamra jest traktowana jako jeden zapis.

## 10. Wyciągi (rozdz. 8)

- Wyciąg z dziennika ruchu, dziennika telefonicznego albo zajętości torów stacyjnych za podany okres, wydrukowany jak papierowy R-146:
  nagłówek z posterunkiem, szlakiem i okresem, kolumny z godzinami i minutami, a na dole „Wyciąg sporządził, stanowisko, jednostka, tel., data”.
- Są dwie wersje: **bez historii** albo **z historią zmian**. W wersji z historią pod każdym wierszem są jego wcześniejsze wersje na szaro,
  a kolumna 9 „Data i godzina zmiany” ma dokładność do sekundy. EDR nie kasuje wpisów, tylko je wersjonuje.

## 11. Wnioski dla symulatora (propozycje – do decyzji właściciela)

EDR jest w `ROZWOJ.md` jako osobny etap na później. Gdy przyjdzie jego kolej:

1. **Dziennik szlaku** jako tabela 1–8 wg § 6, osobna dla każdego szlaku z konfiguracji stacji (bez nazw stacji w kodzie).
   Wiersze tworzy się z wykazu pociągów przez PPM, dwuklik albo przeciąganie, a brakujące pola są na niebiesko.
2. **Parzystość numeru wg kierunku szlaku**: w modelu szlaku trzeba zapisać, który kierunek jest parzysty.
3. **Kontrolka zajętości torów stacyjnych** (rząd kwadratów) – może brać zajętość prosto z symulacji.
4. **Telefonogramy z wzorów** (lista wzorów z numerami jak [9], [21] – treść podaje właściciel) i ich wpływ na stan bieżący szlaku
   (zamknięcie toru, zapowiadanie telefoniczne, ruch dwukierunkowy).
5. **Powiadomienie dróżników** w kolumnie 8 (godzina albo ukośnik) wg konfiguracji przejazdu.
6. **Jazda do km i z powrotem** jako para wierszy w klamrze.
7. **Wyciąg z historią zmian** jako podstawa oceny ćwiczenia (kto, co i kiedy zmienił).
8. Cykliczne sprawdzenia (przyciski w prawym górnym rogu) jako zadania z terminem w scenariuszu.

Do potwierdzenia: lista wzorów telefonogramów i ich numery, zawartość zakładek „Wymagane sprawdzenia” i „Układ dzienników”
(strony 30–38 podręcznika są tylko częściowo na zdjęciach).
