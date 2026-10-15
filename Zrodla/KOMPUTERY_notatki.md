# KOMPUTERY.pdf – notatki do symulatora

Źródło: podręcznik UTK (Fundusze Europejskie), str. 120–175, rozdz. 1.5–1.6.
Tematy: komputerowe urządzenia srk, LCS, blokada stacyjna, blokady liniowe.

## Warstwy systemu (Tabela 23) – podstawa architektury symulatora
1. **Interfejs użytkownika** (elektroniczny pulpit nastawczy): obsługa i wizualizacja. Po każdym działaniu operatora jest reakcja optyczna lub akustyczna.
2. **System zależnościowy**: bezpieczeństwo nastawiania i zwalniania przebiegów, kontrola stanu urządzeń. Komunikuje się z urządzeniami przytorowymi (wejścia/wyjścia) i z pulpitem.
3. **Urządzenia przytorowe**: napędy zwrotnicowe, obwody torowe, liczniki osi, sygnalizatory.

Komputer zależności: układ „2z2”. Dwa komputery z różnym kodem, a wyjścia porównuje bezpieczny komparator. Przy niezgodności przechodzi w stan bezpieczny (zatrzymanie ruchu). Pulpit ma 2 monitory: podstawowy i rezerwowy.

## Pulpit (MOR-2zs, EbiScreen)
- Elementami są urządzenia: zwrotnice, semafory, tarcze, urządzenia kontroli zajętości, tory stacyjne i szlakowe. Adresy to numery zwrotnic oraz oznaczenia odcinków i sygnalizatorów.
- Pulpit pokazuje planowaną drogę przebiegu z informacją, czy da się go nastawić, ostrzega o przeszkodach i podpowiada dostępne polecenia.
- Na ekranie (Rys. 77):
  - stan sygnalizatorów;
  - położenie i stan zwrotnic oraz wykolejnic;
  - nastawione przebiegi;
  - niezajętość torów i rozjazdów;
  - blokada stacyjna;
  - stany awaryjne, np. rozprucie;
  - zasilanie;
  - stan szlaków i sąsiednich posterunków.
- Obszar zdalnego prowadzenia ruchu dzieli się na **obszary obsługi**, po jednej stacji lub posterunku na obszar.
- Polecenia związane z numerami pociągów (Rys. 73):
  - nadanie numeru (utworzenie pociągu);
  - usunięcie numeru;
  - dodatkowe informacje i ostrzeżenia przy pociągu;
  - **przesunięcie numeru między torami** (tryb awaryjny);
  - wpis do rejestratora przy torze.
- Polecenia LCS (Tabela 25), między innymi:
  - przebiegi pociągowe i manewrowe;
  - indywidualne nastawianie zwrotnic i sygnalizatorów;
  - **elektryczne ogrzewanie rozjazdów (EOR)**;
  - rejestracja poleceń i stanów, odtwarzanie rejestratora;
  - współpraca z PIP;
  - obsługa przejazdów (SSP, powiadamianie dróżników);
  - rozkazy pisemne;
  - praca przy uszkodzonych urządzeniach zdalnego sterowania.
- **Awaryjny terminal** (Tabela 25):
  - przestawianie zwrotnic i wykolejnic;
  - kasowanie rozprucia;
  - doraźne zwalnianie przebiegów;
  - EOR, oświetlenie;
  - wyłączanie i włączanie napięcia nastawczego;
  - kontrola zajętości odcinków izolowanych i położenia zwrotnic.
- Zrzuty EbiScreen (str. 146–148, Psary):
  - nagłówek: „Zalogowany”, MIEJSCOWY lub ZDALNY, nazwa stacji, „Liczba osi”;
  - przełączniki ZG / PIP / STR;
  - po lewej kolumna numerów pociągów na szlaku;
  - u dołu: SERWER EBISCREEN 1/2, Komputer zależnościowy, zegar, ZDARZENIA (Czas, Obszar, Stacja, Typ obiektu, Obiekt, Kategoria, Zdarzenie, Operator, Notatka, Źródło);
  - przy szlakach: WY/WJ (powtarzacze), ToB/ToA, ISpA (tarcze), JT1D/JT2D;
  - przejazd jest opisany kilometrem (np. 20.722).

## Kontrola niezajętości
- Rodzaje: izolowane obwody torowe, bezzłączowe obwody torowe, **liczniki osi** (np. UNI AS1, z protokołem cyfrowym).
- W SBL obwody torowe mają zasilanie na środku i przekaźniki na obu końcach odcinka. Każdy posterunek odstępowy kontroluje oba sąsiednie odstępy.

## Blokada stacyjna (gdy stacja ma ≥ 2 okręgi nastawcze)
- **Nastawnia dysponująca:** Dn (danie nakazu), Oz (otrzymanie zgody).
- **Nastawnia wykonawcza:** On (otrzymanie nakazu), Dz (danie zgody).
- Pary: Dn→On, Dz→Oz. Zgodę poprzedza żądanie (Żz). Nakaz sygnału zastępczego: DnSz/OnSz.
- Warunki (Tabela 26):
  - sygnał zezwalający w nastawni wykonawczej dopiero po otrzymaniu nakazu;
  - jeśli inna nastawnia przygotowuje część drogi, dopiero po jej zgodzie;
  - zwrot niewykorzystanego nakazu lub zgody jest możliwy;
  - możliwe doraźne uchylenie zamknięcia zwrotnic, z rejestracją;
  - nastawnia dająca nakaz może nastawić „Stój” na semaforze, którego nakaz dotyczy;
  - zwrot po zwolnieniu przebiegu, samoczynnie albo przez obsługę;
  - **nakaz lub zgoda tylko na jedno użycie** (przeciwwtórność), chyba że zgoda nie jest zgłoszeniem niezajętości toru.
- Danie nakazu lub zgody wymaga przygotowania własnej części drogi i wyklucza przebiegi sprzeczne.
- W stanie zasadniczym tarczki bloków są czerwone, a podczas przebiegu białe.

## Blokady liniowe
- **Podział:**
  - samoczynna (SBL): wieloodstępowa, dwu-, trzy- lub czterostawna;
  - półsamoczynna: jednoodstępowa, dwustawna;
  - wariant szlaku: jedno- lub dwutorowy; wariant toru: jedno- lub dwukierunkowy.
- **SBL:**
  - przyciski Wbl, Pzk, Zwbl, opcjonalnie STOP i A;
  - Wbl na stacji wyprawiającej powoduje, że strzałka miga na biało na obu stacjach;
  - Pzk na drugiej stacji: strzałki świecą białym światłem ciągłym, a semafory odstępowe pokazują sygnały;
  - Zwbl (z drugiej strony) zwalnia kierunek, gdy szlak jest wolny i nie ma przebiegu wyjazdowego;
  - Wbl można odwołać przed Pzk;
  - zmiana kierunku jest możliwa tylko przy wolnym szlaku;
  - semafory odstępowe przeciwnego kierunku są ciemne;
  - na pulpicie są dwie strzałki kierunkowe i szczeliny zajętości odstępów; gdy nie wszystkie odstępy są pokazane, jest szczelina kTor (czerwona przy zajętości lub braku sygnału zezwalającego).
- **Stawność:**
  - dwustawna: S1 i S2, odstęp ≥ 2× droga hamowania (lh), tarcze ostrzegawcze, do 50 km/h;
  - trzystawna: S1, S2, S5, odstęp ≥ lh (zwykle 1000 m), V ≤ 120;
  - czterostawna: S1, S5, S4/S2 migające (dwa wolne odstępy), S2 (≥ 3 wolne), odstęp ≥ ½ lh, V ≤ 160.
  - Wzory: następstwo ln³ = lr + 2lB + lo + lt + lp; ln⁴ = lr + 1,5·lh + lo + lt + lp.
- **Półsamoczynna jednoodstępowa (Tabela 30):**
  - sygnał zezwalający dopiero po zwolnieniu blokady i ustaleniu kierunku, tylko raz do czasu zwolnienia;
  - „Stój” samoczynnie po najechaniu pierwszą osią za semaforem;
  - zwolnienie: zajęcie odcinka za wjazdowym, „Stój” na wjazdowym, zwolnienie odcinka szlakowego, zwolnienie odcinka za wjazdowym;
  - przy Sz wszystkie warunki muszą być spełnione i skontrolowane.
  - Przyciski: **Po** (blok początkowy), **Ko** (blok końcowy), **Poz** (blok pozwolenia), doraźne **dPo** i **dKo** (wyciągane, z licznikami).
  - Lampka **Pwl** (przeciwwtórność liniowa): czerwona po podaniu sygnału wyjazdowego, a do czasu Po nie da się podać sygnału ponownie ani zmienić pozwolenia.
  - Przebieg: Wbl → strzałka miga → sygnał wyjazdowy (Pwl czerwona) → po wygaszeniu Po, co odblokowuje Ko na sąsiedniej stacji → wjazd stwierdzony czujnikami lub obwodem, biała lampka przy Ko → Ko → blok początkowy sąsiada odblokowany, stan zasadniczy.
  - Zmiana kierunku: Poz na stacji mającej pozwolenie.
  - Sz lub rozkaz pisemny: dPo przed Po (odwzbudza Pwl); dKo przed wjazdem na Sz, potem po wjeździe Ko.
- **Komputerowa blokada liniowa:**
  - Ea: półsamoczynna Eap i samoczynna Eac;
  - blokada o zasilaniu impulsowym: odcinki izolowane (kodowa) albo liczniki osi;
  - włączenie, zwolnienie i zmiana kierunku tylko przy wolnym szlaku i bez utwierdzonego przebiegu wyjazdowego;
  - przy SBL kolejne pociągi po zwolnieniu pierwszego odstępu.
