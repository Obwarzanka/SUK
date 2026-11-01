# Załącznik – wskaźniki wg Ie-1 (baza opisowa)

Załącznik do dokumentacji symulatora SUK. Spis wszystkich wskaźników z instrukcji sygnalizacji **Ie-1** z opisem: co oznacza, jak wygląda,
gdzie się go ustawia i czy symulator go obsługuje. Opisy są streszczeniem – przy projektowaniu i w razie wątpliwości rozstrzyga tekst Ie-1.

- **Źródło:** `Zrodla/Ie-1_obowiazuje_od_2026-01-17__uz_1041_25__WCAG.pdf` (uchwała nr 1041/2025, obowiązuje od 2026-01-17).
  Numery stron („s.”) – strony tego PDF.
- **Zakres:** § 17 (wskaźniki zwrotnicowe Wz, ogólnoeksploatacyjne W, sieciowe We, ETCS), § 18 (wskaźniki na wyznaczonych liniach),
  § 19 (obrazy stosowane do odwołania), § 20 (wskaźniki dla pracowników), zał. 6 (wskaźniki TSI OPE – jeszcze nie obowiązują).
- **Sygnały semaforów** (S1–S13a, Sz, Ms1, Ms2) są w osobnej bazie `app/LcsWidzew/Data/sygnaly_ie1.json` (§ 4, § 8, zał. 1).
- **Znaki na planie UT** rysuje się wg Ig-10 zał. 8 (`app/Lcs.EdytorUT/Dane/symbole_ig10.json`) – ten załącznik opisuje znaczenie
  wskaźników w ruchu, nie ich znak na planie schematycznym.

Kolumna / pole **Symulator**:

| Oznaczenie | Znaczenie |
|---|---|
| **obsługiwany** | jest w modelu i działa w logice (komputer zależności, sygnał, ruch) |
| **tylko UT** | można postawić w edytorze UT, logika go nie używa |
| **brak** | nie ma w programie; ewentualne użycie – w propozycjach (rozdz. 9), decyzja właściciela |

---

## 1. Zasady ogólne (§ 17 ust. 1–3, 14)

- Wskaźnik przekazuje polecenie, nakaz albo informację napisem, symbolem, światłem albo samym kształtem.
- Tablice zaleca się robić odblaskowe; tablic się nie oświetla, chyba że Ie-1 mówi inaczej.
- Wskaźniki świetlne (W 2, W 10, W 19, W 20, W 21, W 21a, W 24, W 25, W 26, W 30) **zapalają się razem z sygnałem** albo ze zdarzeniem
  (np. ogrzewanie, ważenie) – w symulatorze to stan, nie tylko rysunek.
- **Gdzie stoi wskaźnik ogólnoeksploatacyjny** (ust. 14), patrząc w kierunku jazdy:
  - stacja – z prawej strony toru, do którego się odnosi;
  - szlak jednotorowy – z prawej strony toru dla każdego kierunku;
  - szlak dwutorowy (i dwa równoległe tory dwóch linii jednotorowych) – po zewnętrznej stronie: tor prawy – z prawej, tor lewy – z lewej;
  - szlak wielotorowy (> 2 tory) – tory skrajne po zewnętrznej stronie, nieskrajne z prawej strony.
- Gdy międzytorze nie mieści tablicy typowej, stosuje się tablicę zmniejszoną, umieszczoną nisko (dotyczy m.in. W 8, W 9, W 14, W 27a,
  W 28, W 33, W 34, W 35, W 36, W ETCS).

## 2. Wskaźniki zwrotnicowe Wz (§ 17 ust. 4–13, s. 112–117)

Pokazują położenie zwrotnicy, tak samo w dzień i w nocy. Postać: latarnia mechaniczna (szkło mleczne), latarnia elektryczna (białe światła)
albo tarcza nieoświetlona (zalecana odblaskowa). Przy rozjazdach zwyczajnych, krzyżowych pojedynczych, łukowych i skupionych stoją obok
rozjazdu na początku każdej zwrotnicy, przy rozjazdach krzyżowych podwójnych – z boku, w środku rozjazdu.

**Kiedy można ich nie stosować:** zwrotnice scentralizowane, po których jeździ się tylko w przebiegach utwierdzanych lub zamykanych;
rozjazdy, na których po zmroku manewruje się rzadko albo jest dobre oświetlenie (wtedy też tarcze nieoświetlone zamiast latarń).
Pora podświetlania – w regulaminie technicznym posterunku.

**Iglica nieprzylegająca** (rozjazd krzyżowy podwójny): w latarni mechanicznej częściowo widać trzecią strzałę, w elektrycznej miga
środkowe światło – wjazd na zwrotnicę zabroniony.

| Wskaźnik | Nazwa | Wygląd | Znaczenie |
|---|---|---|---|
| Wz 1 | Jazda na wprost | biały prostokąt pionowo na czarnym tle; latarnia el.: dwa białe światła w pionie; widać od ostrza i od krzyżownicy | zwrotnica na kierunek prosty (rozjazd łukowy jednostronny – na łuk o większym promieniu) |
| Wz 2 | Jazda na ostrze | biała strzała lub kresa skośnie w górę w prawo / w lewo, widoczna od ostrza; latarnia el.: dwa białe światła w poziomie | zwrotnica na kierunek zwrotny (łukowy jednostronny – na łuk o mniejszym promieniu; łukowy dwustronny – na jeden z łuków, strzała w jego stronę) |
| Wz 3 | Jazda z ostrza | biała okrągła tarcza na czarnym tle, widoczna od krzyżownicy; latarnia el.: dwa białe światła w poziomie | zwrotnica na kierunek zwrotny (jazda z ostrza) |
| Wz 4 | Jazda z ostrza (łukowy dwustronny) | biała okrągła tarcza z czarnym łukiem wklęsłym w stronę nastawionego łuku | rozjazd łukowy dwustronny nastawiony z lewego albo prawego toru |
| Wz 5 | Jazda po prostej w prawo | dwie białe strzały ostrzami do siebie / dwie kresy / trzy światła w linii wznoszącej się w prawo | rozjazd krzyżowy podwójny: prosto z lewego toru na prawy |
| Wz 6 | Jazda po prostej w lewo | jak Wz 5, linia wznosząca się w lewo | prosto z prawego toru na lewy |
| Wz 7 | Jazda po łuku w lewo | dwie strzały / kresy / trzy światła tworzące kąt prosty otwarty w lewo | zwrotnie z lewego toru na lewy |
| Wz 8 | Jazda po łuku w prawo | jak Wz 7, kąt otwarty w prawo | zwrotnie z prawego toru na prawy |

**Symulator:** brak. Zwrotnice w symulatorze są scentralizowane, więc Wz nie są potrzebne (wyjątek z ust. 7 pkt 1); położenie zwrotnicy
pokazuje zobrazowanie wg Ie-104.

## 3. Wskaźniki ogólnoeksploatacyjne W (§ 17 ust. 15, s. 118–170)

### W 1 „Wskaźnik usytuowania” (s. 118–119)
- **Znaczenie:** zwraca uwagę na tarczę ostrzegawczą (semaforową lub przejazdową), a przy czterostawnej SBL – na przedostatni semafor
  odstępowy przed semaforem wjazdowym.
- **Wygląd:** biała prostokątna tablica z czarną ramką, dwa czarne kąty jeden nad drugim stykające się wierzchołkami w środku.
- **Ustawienie:** tuż przed tarczą / semaforem albo nisko na jego maszcie.
- **Symulator:** brak.

### W 2 „Wskaźnik kierunku jazdy” oraz W 26a, W 26b (s. 119–120)
- **Znaczenie:** W 2 – kierunek wyjazdu pociągu; W 26a – przejazd z grupy torów dalekobieżnych na podmiejskie; W 26b – z podmiejskich
  na dalekobieżne.
- **Wygląd:** świetlny, matowobiała litera na czarnym tle. W 2 – skrót stacji końcowej, węzłowej albo innego punktu docelowego;
  W 26a – zawsze „P”, W 26b – zawsze „D”.
- **Ustawienie:** W 2 na maszcie semafora albo osobnym maszcie; W 26a/b na semaforze przed połączeniem grup torów.
- **Stan:** świeci tylko przy sygnale zezwalającym na wyróżniony kierunek; W 26a/b świeci też przy sygnale zastępczym.
- **Symulator:** brak.

### W 3 „Wskaźnik unieważnienia” (s. 120–121)
- **Znaczenie:** semafor lub tarcza zaporowa stojące z prawej strony toru przy tym wskaźniku **nie dotyczą** toru, przy którym stoi wskaźnik.
- **Wygląd:** dzienny – maszt semafora bez ramion; nocny – białe światło u wierzchołka masztu; albo wskaźnik świetlny z białym światłem.
- **Ustawienie:** z prawej strony toru, przy semaforze lub tarczy zaporowej, której dotyczy.
- **Symulator:** brak.

### W 4 „Wskaźnik zatrzymania” (s. 121–123)
- **Znaczenie:** miejsce, do którego może dojechać czoło pociągu zatrzymującego się na stacji lub przystanku; pociąg z postojem staje
  tak, by ruch podróżnych był najdogodniejszy.
- **Wygląd:** prosty biały krzyż na czarnym prostokącie; latarnia ze szkłem mlecznobiałym albo tarcza nieoświetlona.
- **Ustawienie:** przy końcu peronu albo przed ukresem, z prawej strony toru (wyjątkowo z lewej, np. brak skrajni). Przy końcu peronu,
  gdy nie jest końcem przebiegu – dotyczy tylko pociągów z postojem przy tym peronie. Nie stawia się go, gdy do 25 m za końcem peronu
  jest semafor.
- **Symulator:** obsługiwany (2026-10-29) – urządzenie „Wskaźnik W4” (`UtDevice.W4`, Ig-10 zał. 8 s. 33), z kierunkiem działania.
  Pociąg mający postój przy peronie staje czołem 5 m przed W4 swojego kierunku (bez W4 – 15 m przed semaforem na końcu toru peronowego).
  W4 zwrócony w drugą stronę nie zatrzymuje (`Sim/Trains.cs`, `Lookahead`; dane „w4” z „Do symulatora”).

### W 5 „Wskaźnik przetaczania” (s. 123)
- **Znaczenie:** granica przetaczania (manewrów). Dalej tylko za zezwoleniem dyżurnego ruchu.
- **Wygląd:** biała tablica zaokrąglona u góry, z czarną i białą ramką.
- **Ustawienie:** co najmniej 100 m przed semaforem wjazdowym (patrząc w stronę szlaku); linia dwutorowa – przy torze wjazdowym po stronie
  semafora wjazdowego; linia jednotorowa – z prawej strony toru głównego zasadniczego, patrząc w stronę szlaku.
- **Symulator:** obsługiwany – urządzenie „Wskaźnik W5” w UT, w stacji pole `w5` (węzeł → kierunek, nazwa); komputer zależności kończy
  wyjazd manewrowy za semafor wjazdowy na W5 (`Sim/Kz/Blocks.cs`, `KomputerZaleznosci.cs`).

### W 6 „Wskaźnik ostrzegania” (s. 124)
- **Znaczenie:** podać sygnał Rp 1 „Baczność”.
- **Wygląd:** biały trójkąt równoboczny wierzchołkiem w górę, czarna i biała ramka.
- **Ustawienie:** tam, gdzie maszynista ma dać Rp 1.
- **Symulator:** brak.

### W 6a „Wskaźnik przejazdowy” (s. 124–126)
- **Znaczenie:** dalej jest przejazd albo przejście z systemem przejazdowym.
- **Wygląd:** jak W 6 z symbolem pojazdu drogowego.
- **Ustawienie:** przed przejazdami z półsamoczynnym lub samoczynnym systemem (powiązanym z urządzeniami stacyjnymi, z tarczami
  ostrzegawczymi przejazdowymi, z sygnalizatorami drogowymi i rogatkami albo z samymi sygnalizatorami) – wg przepisów o skrzyżowaniach
  linii kolejowych z drogami.
- **Symulator:** brak.

### W 6b „Wskaźnik ostrzegania przed niezabezpieczonym przejazdem kolejowo-drogowym lub przejściem” (s. 126–128)
- **Znaczenie:** podać Rp 1 „Baczność” przed przejazdem bez systemu przejazdowego (albo z systemem niepowiązanym, z ruchem kierowanym
  przez pracownika, z rogatkami stale zamkniętymi otwieranymi przez użytkowników).
- **Wygląd:** dwa trójkąty jak W 6a, jeden pod drugim.
- **Symulator:** brak.

### W 7 „Wskaźnik robót torowych” (s. 128–129)
- **Znaczenie:** podać Rp 1 „Baczność” ze względu na ludzi pracujących na torze.
- **Wygląd:** przenośny biały trójkąt (wierzchołek w górę) z czarną literą „R”.
- **Ustawienie:** z obu stron miejsca robót, 300–500 m od niego.
- **Symulator:** brak.

### W 8 „Wskaźnik ograniczenia prędkości” (s. 129–131)
- **Znaczenie:** zmniejszyć prędkość (zapowiedź odcinka ograniczonej prędkości).
- **Wygląd:** biały trójkąt wierzchołkiem w dół, czarna ramka, czarna liczba = 1/10 prędkości w km/h (prędkość co 5 km/h, np. „4” =
  40 km/h, „2,5” = 25 km/h). Gdy brak skrajni – trójkąt wierzchołkiem w górę, nisko przy główce szyny.
- **Ustawienie:** dla ostrzeżeń stałych (wykaz ostrzeżeń stałych), w odległości drogi hamowania przed początkiem odcinka; za nim W 9.
  W stacji – jak na szlaku jednotorowym. Ograniczenie na całą stację – przy tarczy ostrzegawczej semafora wjazdowego (obowiązuje do
  minięcia stacji). Na torach głównych dodatkowych i rozjazdach poza torami głównymi zasadniczymi W 8 nie jest wymagany – wystarczy W 9.
  Służy też jako zapowiedź obniżenia prędkości drogowej przed W 27a.
- **Symulator:** obsługiwany (2026-10-29) – urządzenie „Wskaźnik W8” z prędkością (`UtDevice.W8`, `Vw`). Z W9 za nim (do 3 km) – tylko
  zapowiedź (maszynista hamuje do W9 z wyprzedzeniem). Bez W9 – ograniczenie na wszystkich torach stacyjnych za W8 do minięcia stacji
  (lit. e), z uwagą w „Do symulatora”. Znak na planie: słupek Ig-10 s. 34, trójkąt wg Ie-1 – propozycja do akceptacji.

### W 9 i W 14 „Wskaźniki odcinka ograniczonej prędkości” (s. 131–134)
- **Znaczenie:** początek albo koniec odcinka ograniczonej prędkości. W 9 (biały) – ograniczenie stałe, razem z W 8; W 14 (pomarańczowy)
  – ograniczenie doraźne, razem z tarczą D 6 „Zwolnić bieg”.
- **Wygląd:** prostokątna tablica (W 9 biała, W 14 pomarańczowa), czarna ramka. Strona „początek” – czarny kąt wierzchołkiem w dół
  z liczbą prędkości (1/10 km/h). Strona „koniec” – kąt wierzchołkiem w górę, czasem z literą **C** = ograniczenie dotyczy tylko
  **czoła** pociągu (bez litery – do minięcia odcinka całym pociągiem).
- **Ustawienie:** za W 8 / D 6 na początku i końcu odcinka; ograniczenie na całą stację – przy semaforze wjazdowym. Koniec odcinka
  maszynista odczytuje z **odwrotnej strony** wskaźnika postawionego dla przeciwnego kierunku (także gdy stoi on z lewej strony).
  Tło odblaskowe; przy wąskim międzytorzu – zmniejszony, nisko.
- **Symulator:** W 9 obsługiwany (2026-10-29), W 14 – brak. Urządzenie „Wskaźnik W9” (`UtDevice.W9`): `Vw` – prędkość na stronie początku
  (puste – tablica tylko z końcem), `W9C` – litera C na stronie końca. Jazda w kierunku wskaźnika – od niego ograniczenie aż do W9
  zwróconego przeciwnie (jego strona końca); bez końca w 5 km – uwaga. „Do symulatora” zapisuje ograniczenie na torach z węzłem wejścia
  („vlim”, „vlimC”), maszynista ma je w profilu prędkości; z literą C pociąg przyspiesza, gdy czoło minie koniec (`EdgeLimit`).

### W 10a, W 10b „Wskaźniki odcinka z popychaniem” (s. 135)
- **Znaczenie:** W 10a – rozpocząć popychanie, W 10b – zakończyć popychanie.
- **Wygląd:** latarnia, mlecznobiały kąt na czarnym tle – wierzchołkiem w górę (W 10a) albo w dół (W 10b).
- **Ustawienie:** W 10a 100 m przed miejscem rozpoczęcia popychania, W 10b w miejscu zakończenia; z prawej strony toru.
- **Symulator:** brak.

### W 11a, W 11b, W 11c „Wskaźniki uprzedzające” (s. 136–142)
- **Znaczenie:** dalej jest tarcza ostrzegawcza semafora wjazdowego lub odstępowego albo semafor słabo widoczny (W 11a); tarcza
  ostrzegawcza semafora wjazdowego posterunku, od którego zaczyna się sieć 3 kV DC (W 11b) albo 25 kV AC (W 11c).
- **Wygląd:** seria białych tablic z czarnymi pasami ukośnymi (z lewa w prawo w górę; 30° na prostokątnych, 45° na kwadratowych):
  przed tarczą ostrzegawczą 3 tablice (3, 2, 1 pas), przed semaforem słabo widocznym 4 tablice (4, 3, 2, 1 pas). W 11b – na pasach
  czerwona błyskawica ostrzem w dół; W 11c – błyskawica oraz czerwone „25” z sinusoidą u góry.
- **Ustawienie:** po tej samej stronie toru co tarcza / semafor. Przed tarczą co 100 m (wyjątkowo mniej, najmniej 60 m, równe odstępy).
  Przed semaforem – pierwsza tablica tam, skąd semafor powinien być widoczny, pozostałe równo do semafora. Na liniach drugorzędnych
  i lokalnych (stacje pośrednie, niewęzłowe) i przy V ≤ 40 km/h przed tarczą można W 11a pominąć. W 11b, W 11c – tylko na szlakach
  niezelektryfikowanych, zawsze.
- **Symulator:** brak.

### W 11p „Wskaźnik przejazdowy” (s. 142–143)
- **Znaczenie:** dalej jest tarcza ostrzegawcza przejazdowa.
- **Wygląd:** pomarańczowe prostokątne tablice z czarnymi trójkątami (wierzchołek w górę): jedna tablica z 1 trójkątem; przy tarczy
  słabo widocznej dwie tablice – z 2 i z 1 trójkątem.
- **Ustawienie:** z 1 trójkątem 200 m, z 2 trójkątami 400 m przed tarczą, po jej stronie toru.
- **Symulator:** brak.

### W 12 „Wskaźnik parowozowy” (s. 143–144)
- **Znaczenie:** zakropić popielnik parowozu i zamknąć jego klapy.
- **Wygląd:** biała tablica w kształcie ukośnika (romboidu) z czarną i białą ramką.
- **Ustawienie:** 200 m przed mostem, wiaduktem lub innym obiektem.
- **Symulator:** brak (tabor parowy poza zakresem).

### W 13 „Wskaźnik torowy” (s. 144–146)
- **Znaczenie:** podnieść noże i zamknąć skrzydła pługa odśnieżnego; ostrożnie z podbijarką, oczyszczarką i innymi maszynami torowymi.
- **Wygląd:** czarno-biała krata (dwie pary ukośników) albo dwie kraty jedna nad drugą.
- **Ustawienie:** 50 m przed osłanianym miejscem (przejazd, most, rozjazd, urządzenia tor–pojazd, czujniki szynowe, detektory gorących
  maźnic i płaskich miejsc), z obu stron, przy każdym torze. Przeszkody bliżej niż 150 m od siebie – jedna przeszkoda, wskaźnik z dwiema
  kratami.
- **Symulator:** brak.

### W 15 „Wskaźnik zmiany lokalizacji” (s. 146)
- **Znaczenie:** semafor, sygnalizator powtarzający lub tarcza ostrzegawcza stoi gdzie indziej niż powinien, ale dotyczy toru, przy którym
  stoi wskaźnik.
- **Wygląd:** biała kwadratowa tablica z czarnym trójkątem ostrzem w stronę sygnalizatora.
- **Ustawienie:** w miejscu, gdzie powinien stać sygnalizator.
- **Symulator:** brak.

### W 16 „Wskaźnik przystanku osobowego” (s. 146–147)
- **Znaczenie:** w odległości drogi hamowania jest przystanek osobowy.
- **Wygląd:** biała pozioma tablica z trzema czarnymi pasami ukośnymi (z lewa w prawo w górę).
- **Ustawienie:** skośnie do toru, z prawej strony, przed przystankiem bez semaforów, w odległości drogi hamowania od W 4 tego przystanku.
- **Symulator:** brak.

### W 17 „Wskaźnik ukresu” (s. 147–148)
- **Znaczenie:** granica, do której wolno zająć tor taborem przy zbiegających się torach.
- **Wygląd:** biało-czerwony słupek (słupek ukresowy).
- **Ustawienie:** wg skrajni i warunków (przechyłka, poszerzenie w łuku).
- **Symulator:** obsługiwany (2026-10-29) – urządzenie „Ukres (W17)” (`UtDevice.Ukres`, Ig-10 zał. 8 s. 24), bez kierunku. Gdy koniec
  drogi (koniec przebiegu manewrowego, PK, koniec toru) wypadałby między ukresem a zwrotnicą za nim, czoło staje 3 m przed ukresem
  (`Sim/Trains.cs`). Zajętość odcinków dalej wyznaczają liczniki osi i złącza – stawiaj je przy ukresie.

### W 18 „Wskaźnik samoczynnej blokady liniowej” (s. 148–149)
- **Znaczenie:** ostatni semafor odstępowy SBL przed semaforem wjazdowym. Gdy ten semafor jest ciemny lub unieważniony – jazda z prędkością
  pozwalającą zatrzymać się przed przeszkodą albo semaforem wjazdowym „Stój”.
- **Wygląd:** biała kwadratowa tablica z czarną ramką, czarny pierścień z czarnym kołem w środku.
- **Ustawienie:** na maszcie ostatniego semafora odstępowego.
- **Symulator:** brak.

### W 19 „Wskaźnik uprzedzający o braku drogi hamowania” (s. 149–151)
- **Znaczenie:** za następnym semaforem lub tarczą ostrzegawczą odstęp jest krótszy od drogi hamowania – szczególna ostrożność w regulowaniu
  prędkości.
- **Wygląd:** biała strzała ostrzem w dół na czarnym tle; zasadniczo świetlny.
- **Ustawienie / stan:** na maszcie semafora lub tarczy ostrzegawczej poprzedzającej; świeci z sygnałem nakazującym zatrzymanie lub
  zmniejszenie prędkości przy kolejnych dwóch semaforach (dla przebiegu na skrócony odstęp). Tablica odblaskowa dopuszczona, gdy dotyczy
  każdego sygnału zezwalającego albo stoi na semaforze czterostawnej SBL (tam: ostatni odstęp krótszy od drogi hamowania).
- **Symulator:** brak.

### W 20 „Wskaźnik braku drogi hamowania” (s. 151–153)
- **Znaczenie:** odległość od tego sygnalizatora do następnego semafora jest mniejsza od drogi hamowania.
- **Wygląd:** dwie równoległe białe strzały ostrzem w dół na czarnym tle; zasadniczo świetlny.
- **Ustawienie / stan:** na maszcie tarczy ostrzegawczej lub semafora na początku skróconego odstępu; świeci z sygnałem nakazującym
  zatrzymanie lub zmniejszenie prędkości przy następnym semaforze. Poprzedza go W 19. Tablica – na tych samych warunkach co W 19.
- **Symulator:** brak.

### W 21 „Wskaźnik podwyższenia prędkości” (s. 153–154)
- **Znaczenie:** od tego semafora wolno jechać z prędkością ze wskaźnika (wyższą niż 40, 60 lub 100 km/h z sygnału).
- **Wygląd:** czarna tablica (kwadrat lub prostokąt) z białą liczbą w dziesiątkach km/h (np. „5” = 50 km/h, „10” = 100 km/h).
- **Postać:** zasadniczo świetlny – liczba zapala się z sygnałem zezwalającym, gdy trzeba podnieść prędkość ponad sygnał. Tablica
  odblaskowa – gdy dotyczy wszystkich sygnałów ze zmniejszoną prędkością danego semafora (układ wszędzie pozwala na więcej) albo wszystkich
  sygnałów 40 km/h, gdy każdy taki przebieg pozwala na 50 km/h.
- **Symulator:** obsługiwany – pole semafora `W21`: „S” = świetlny (liczba z prędkości przebiegu), cyfra = tablica, puste = brak
  (`Model/Station.cs`, `Dobor`); w UT właściwości `W21` i `W21Swietlny`. Stan „liczba na W21” – `Sim/State.cs`. Prędkość przebiegu
  wpisuje projektant w tablicy przebiegów.

### W 21a „Wskaźnik uprzedzający podwyższenie prędkości na następnym semaforze” (s. 155–156)
- **Znaczenie:** od **następnego** semafora wolno jechać z prędkością ze wskaźnika (zapowiedź W 21).
- **Wygląd:** czarna kwadratowa tablica z pomarańczową liczbą (dziesiątki km/h), świetlny, nad latarnią sygnalizatora.
- **Stan:** świeci, gdy następny semafor daje sygnał zezwalający i jego W 21 pokazuje liczbę.
- **Uwaga:** tylko w projektach pilotażowych albo na zarządzenie zarządcy.
- **Symulator:** brak.

### W 22 „Wskaźnik jazdy pociągu towarowego” (s. 156–157)
- **Znaczenie:** ciężki pociąg towarowy może minąć bez zatrzymania semafor odstępowy SBL wskazujący „Stój”, z prędkością ≤ 20 km/h,
  gotów zatrzymać się przed przeszkodą.
- **Wygląd:** czarny kwadrat postawiony na wierzchołku z białą odblaskową literą „T”.
- **Ustawienie:** na semaforach odstępowych SBL na wzniesieniu miarodajnym ponad 6 ‰ na długości drogi hamowania.
- **Symulator:** brak.

### W 23 „Wskaźnik odcinka izolowanego” (s. 157–158)
- **Znaczenie:** początek odcinka torowego lub zwrotnicowego z kontrolą niezajętości; przetaczany tabor ma się przed nim zatrzymać, by
  można było przestawić zwrotnicę.
- **Wygląd:** żółty słupek przy torze, nieoświetlony.
- **Symulator:** brak (granice odcinków – złącza izolowane i liczniki osi w UT).

### W 24 „Wskaźnik kierunku przeciwnego” (s. 158)
- **Znaczenie:** wyjazd na tor szlaku dwu- lub wielotorowego w kierunku przeciwnym do zasadniczego.
- **Wygląd:** świetlny, matowobiała kresa na czarnym kwadracie, wznosząca się z prawa na lewo.
- **Ustawienie / stan:** na maszcie semafora albo osobnym maszcie; świeci z sygnałem zezwalającym i z sygnałem zastępczym Sz. Przy
  zamknięciach torowych może być przenośną tablicą (dzień).
- **Symulator:** obsługiwany (2026-10-29) – właściwość semafora `W24` w UT („w24” w danych symulatora). Świeci z sygnałem zezwalającym,
  gdy przebieg pociągowy prowadzi na tor szlaku w kierunku przeciwnym do zasadniczego (`KomputerZaleznosci.UpdateSignals`), oraz z Sz
  po NSZ (`Sim/Kz/Commands.cs`). Pulpit jeszcze go nie rysuje (stan `SigInd.W24`).

### W 25 „Wskaźnik ogrzewania” (s. 159)
- **Znaczenie:** stanowisko elektrycznego ogrzewania wagonów; trwa ogrzewanie – nie zbliżać się ani nie dojeżdżać do składu.
- **Wygląd:** świetlny, jedna lub dwie latarnie: czerwony numer toru ze strzałką w stronę toru i błyskawica ostrzem w dół.
- **Ustawienie / stan:** na międzytorzu przy torach postojowych; świeci od rozpoczęcia ogrzewania.
- **Symulator:** brak.

### W 26a, W 26b – opisane przy W 2.

### W 27a „Wskaźnik zmiany prędkości” (s. 159–160)
- **Znaczenie:** od tego miejsca obowiązuje nowa największa prędkość drogowa linii.
- **Wygląd:** biała kwadratowa tablica z czarną obwódką i czarną liczbą (1/10 km/h, co 5 km/h); dwustronny.
- **Ustawienie:** przy torach szlakowych i głównych zasadniczych, poza drogami rozjazdowymi; na szlaku jednotorowym z prawej patrząc
  w stronę rosnącego kilometrażu, inaczej wg ust. 14. Obniżenie prędkości zapowiada W 8 w drodze hamowania.
- **Symulator:** brak.

### W 28 „Wskaźnik kanału radiowego” (s. 161–163)
- **Znaczenie:** od tego miejsca obowiązuje wskazany kanał radiołączności pociągowej; maszynista przełącza radiotelefon i nawiązuje łączność
  z najbliższym posterunkiem na tym kanale. Ważny do następnego W 28.
- **Wygląd:** okrągła czarna tablica z żółtym oznaczeniem: litera zarządcy (PLK – „R”) i numer kanału, np. „R1”.
- **Ustawienie:** na stacji 30–70 m za ostatnią zwrotnicą wyjazdową, na posterunku odgałęźnym 100–150 m za ostatnią zwrotnicą (w stronę
  szlaku z danym kanałem); na dojeździe ze szlaku bez radiołączności – 300 m przed semaforem wjazdowym.
- **Symulator:** brak.

### W 29 „Wskaźnik nawiązania łączności” (s. 163)
- **Znaczenie:** nawiązać łączność radiową z dyżurnym ruchu odcinkowym (ruch na podstawie radiotelefonicznego porozumiewania się).
- **Wygląd:** pomarańczowa pozioma tablica, dwa czarne kąty obok siebie stykające się wierzchołkami w środku.
- **Ustawienie:** skośnie, z prawej strony, przed mijanką bezobsługową, co najmniej 1600 m przed samoczynnym semaforem wjazdowym.
- **Symulator:** brak.

### W 30 „Wskaźnik ważenia składu” (s. 164)
- **Znaczenie:** skład będzie ważony – przejeżdżać przez wagę z podaną prędkością.
- **Wygląd:** świetlny, matowobiałe koło na jasnoniebieskim tle z napisem „Waga x km/h”.
- **Ustawienie:** z obu stron wagi, wg dokumentacji wagi.
- **Symulator:** brak.

### W 31 „Wskaźnik kasowania” (s. 164–165)
- **Znaczenie:** sygnalizator jest nieczynny, nieoddany do użytku albo unieważniony; sygnalizator świetlny jest ciemny.
- **Wygląd:** biały ukośny krzyż z czarną obwódką.
- **Ustawienie:** na sygnalizatorze (na latarni sygnałowej).
- **Symulator:** brak (semafor ciemny jest w bazie sygnałów jako obraz bez świateł).

### W 32 „Wskaźnik czoła pociągu” (s. 165–166)
- **Znaczenie:** miejsce zatrzymania czoła pociągu o podanej długości.
- **Wygląd:** biała pięciokątna tablica (prostokąt ze ściętym narożnikiem od strony toru), czarna obwódka i liczba – długość pociągu w m.
- **Ustawienie:** na stacjach i przystankach, w razie potrzeby kilka dla różnych długości; wykonanie lewo- i prawostronne.
- **Symulator:** brak.

### W 33 „Wskaźnik sieci GSM-R” (s. 166–167)
- **Znaczenie:** wybrać ręcznie wskazaną sieć GSM-R w radiotelefonie (chyba że wybiera się sama).
- **Wygląd:** biała prostokątna tablica z czarną ramką: słuchawka, napis „GSM-R”, pod spodem w elipsie skrót państwa.
- **Ustawienie:** przed semaforem wjazdowym posterunku na linii z GSM-R, w odległości wystarczającej do zalogowania i rejestracji numeru
  przy największej prędkości.
- **Symulator:** brak.

### W 34 „Wskaźnik końca obowiązywania systemu ERTMS/GSM-R” (s. 167–169)
- **Znaczenie:** koniec ERTMS/GSM-R, dalej radiołączność 150 MHz; przełączyć radio i nawiązać łączność.
- **Wygląd:** jak W 33, przekreślony czerwonym pasem (z lewego dolnego do prawego górnego rogu). Nad nim W 28 z kanałem 150 MHz.
- **Ustawienie:** na stacji 30–70 m za ostatnią zwrotnicą wyjazdową, na posterunku odgałęźnym 100–150 m za ostatnim rozjazdem; na dojeździe
  do posterunku bez GSM-R – w drodze hamowania przed semaforem wjazdowym.
- **Symulator:** brak.

### W 35, W 36 „Wskaźniki ograniczenia prędkości na kierunku zwrotnym” (s. 169–170)
- **Znaczenie:** ograniczenie prędkości na rozjeździe dotyczy tylko jazdy na kierunek zwrotny. W 35 (biały) – ograniczenie stałe,
  W 36 (pomarańczowy) – doraźne.
- **Wygląd:** prostokątna tablica z czarną obwódką i czarną poziomą strzałą w stronę, w którą odgałęzia się tor zwrotny.
- **Ustawienie:** W 35 nad W 8 i nad W 9 na początku odcinka; W 36 nad tarczą D 6 i nad W 14 na początku odcinka.
- **Symulator:** brak.

## 4. Wskaźniki sieci trakcyjnej We (§ 17 ust. 16–20, s. 171–184)

Określają sposób prowadzenia elektrycznego pojazdu trakcyjnego wynikający z zasilania i stanu sieci. Stałe albo przenośne. Stałe wiszą nad
torem na konstrukcji wsporczej (co najmniej 4,5 m nad główką szyny) albo na wysięgniku; przenośne – tak samo albo obok toru wg ust. 14.
Postać: niebieski kwadrat postawiony na wierzchołku, czarna i biała obwódka, biały symbol.

Odmiany **b / c** (We 1, We 2, We 4, We 8) dotyczą toru odgałęziającego się w prawo / w lewo – mają dodatkową małą czarną tablicę z białym
trójkątem ostrzem w prawo / w lewo. Odmiany We 3 i We 9 różnią się dodatkową tabliczką: **a** – bez tabliczki (EZT i pociągi z pantografem
30–200 m od czoła), **b** – tabliczka „L” (lokomotywy), **c** – tabliczka „400” (pociągi dłuższe niż 200 m, pantograf dalej niż 200 m od
czoła, m.in. push-pull z lokomotywą z tyłu).

**Odległość zapowiedzi** (We 1 przed We 2, We 10b/d/f przed We 2a / We 8a): co najmniej 400 m przy V ≤ 60 km/h, 600 m przy 60 < V ≤ 100 km/h,
800 m przy V > 100 km/h.

| Wskaźnik | Nazwa | Wygląd (biały symbol) | Znaczenie | Ustawienie |
|---|---|---|---|---|
| We 1a/b/c | opuszczania pantografu | dwa poziome paski przesunięte schodkowo | opuścić pantografy przed następnym wskaźnikiem (We 2) | przed We 2, odległość zapowiedzi |
| We 2a/b/c | opuszczonego pantografu | jeden poziomy pasek | od tego miejsca jazda z opuszczonym pantografem (sieć wyłączona, jazda z rozpędu przez odcinek bez sieci lub z siecią niesprawną) | 50–150 m przed początkiem odcinka |
| We 3a/b/c | podniesienia pantografu | jeden pionowy pasek (+ „L” / „400”) | podnieść pantografy | za miejscem, gdzie wolno podnieść: a – 200–250 m, b – 30–100 m, c – 400–450 m |
| We 4a/b/c | zakazu wjazdu elektrycznych pojazdów trakcyjnych | dwa kwadraty jeden w drugim | zakaz wjazdu EPT (uszkodzenie sieci, praca przy sieci, koniec sieci) | 15–65 m przed miejscem zakazu |
| We 8a/b/c | jazdy bezprądowej | dwa pionowe paski, pod nimi poziomy, niestykający | przejechać bez poboru prądu (AC – także z wyłączonym wyłącznikiem głównym) | 30–80 m przed elementem sekcjonowania (odcinek neutralny, izolowane przęsło, przerwa powietrzna, izolator sekcyjny) |
| We 9a/b/c | jazdy pod prądem | dwa pionowe paski stykające się z poziomym (+ „L” / „400”) | od tego miejsca wolno pobierać prąd | za miejscem jazdy bezprądowej: a – 200–250 m, b – 30–100 m, c – 400–450 m |
| We 10a / We 10b | zmiany systemu zasilania – 25 kV AC | „25” z sinusoidą; We 10b przekreślony czerwonym pasem | wjazd do / wyjazd z obszaru 25 kV AC | wjazd – w sekcji separacji systemów, przed We 3b / We 9b; wyjazd – przed We 2a / We 8a, odległość zapowiedzi |
| We 10c / We 10d | j.w. – 15 kV AC | „15” z sinusoidą; We 10d przekreślony | wjazd do / wyjazd z obszaru 15 kV AC | j.w. |
| We 10e / We 10f | j.w. – 3 kV DC | „3” z dwoma poziomymi paskami; We 10f przekreślony | wjazd do / wyjazd z obszaru 3 kV DC | j.w. |

We 10 stosuje się tylko w strefach separacji dwóch systemów zasilania, nie na granicy obszaru zelektryfikowanego i niezelektryfikowanego.
Przykłady osygnalizowania – zał. 4 Ie-1.

**Symulator:** brak. Tabor w edytorze taboru ma trakcję (BZT, bitrakcyjne – `PLAN_EDYTOR_TABORU.md`), ale sieć trakcyjna i jej sekcje nie są
jeszcze modelowane, więc We nie mają na co działać.

## 5. Wskaźniki ERTMS/ETCS (§ 17 ust. 21–23, s. 184–201)

Ustawia się je wg ust. 14, z materiałów odblaskowych. W ETCS 10 i W ETCS 11 mogą stać po drugiej stronie toru lub nad torem, gdy nie ma
miejsca; przy semaforze na bramce – nad torem.

| Wskaźnik | Nazwa | Wygląd | Ustawienie |
|---|---|---|---|
| W ETCS 1 | Zapowiedź wjazdu w obszar ETCS poziomu 1 LS | żółty kwadrat, czarna ramka, „ETCS / L1LS” | w miejscu zapowiedzi, nie wewnątrz obszaru |
| W ETCS 2 | Wjazd w obszar ETCS poziomu 1 LS | biały kwadrat, czarna ramka, „ETCS / L1LS” | na granicy wjazdu albo przy każdej grupie balis zmieniającej poziom |
| W ETCS 3 | Wyjazd z obszaru ETCS poziomu 1 LS | jak W ETCS 2, przekreślony czerwonym pasem | na granicy wyjazdu; nie na granicy L1LS z L1 lub L2 |
| W ETCS 4 | Zapowiedź wjazdu w obszar ETCS poziomu 1 | żółty, „ETCS / L1” | jak W ETCS 1 |
| W ETCS 5 | Wjazd w obszar ETCS poziomu 1 | biały, „ETCS / L1” | jak W ETCS 2 |
| W ETCS 6 | Wyjazd z obszaru ETCS poziomu 1 | biały, „ETCS / L1”, przekreślony | na granicy wyjazdu; nie na granicy L1 z L1LS lub L2 |
| W ETCS 7 | Zapowiedź wjazdu w obszar ETCS poziomu 2 | żółty, „ETCS / L2” | jak W ETCS 1 |
| W ETCS 8 | Wjazd w obszar ETCS poziomu 2 | biały, „ETCS / L2” | jak W ETCS 2 |
| W ETCS 9 | Wyjazd z obszaru ETCS poziomu 2 | biały, „ETCS / L2”, przekreślony | na granicy wyjazdu; nie na granicy L2 z L1LS lub L1 |

Tablice zmniejszone i nisko – gdy brak skrajni na szlaku; w stacjach zalecane.

### W ETCS 10 „Wskaźnik zatrzymania ETCS” (s. 195–198)
- **Znaczenie:** koniec odstępu (odstępu ETCS, odcinka kontroli niezajętości) jako możliwy koniec zezwolenia na jazdę (MA); miejsce
  zatrzymania w trybach OS i SR.
- **Wygląd:** niebieski kwadrat z żółtą strzałą w białej obwódce, ostrzem w stronę toru, którego dotyczy.
- **Ustawienie:** na końcu odstępu nieosłoniętego semaforem (gdy nie ma tam W ETCS 11) albo na maszcie semafora obsługiwanego zdolnego
  nadać sygnał SE „Jazda zgodnie ze wskazaniami ERTMS/ETCS”.
- **Minięcie dozwolone:** na podstawie MA (pełny nadzór), wskazania semafora (gdy wskaźnik jest na jego maszcie) albo rozkazu pisemnego.
- **Oznaczenie tabliczką** (gdy nie na maszcie semafora): na posterunku – kolejne wielkie litery wg kilometrażu, od lewej do prawej patrząc
  w kierunku jazdy (można dodać numer toru); na szlaku – km i hm (np. „222”), dla kierunku przeciwnego z „N” na końcu, na liniach
  wielotorowych z literą linii (np. „222P”, „222PN”, „222D”, „222DN”).
- **Symulator:** brak.

### W ETCS 11 „Wskaźnik lokalizacji ETCS” (s. 198–201)
- **Znaczenie:** jak W ETCS 10 – możliwy koniec MA, ale zatrzymanie wymagane tylko, gdy kończy się tu MA, wynika to z rozkazu pisemnego
  albo ze wskazania semafora, na którym wisi.
- **Wygląd:** biały kwadrat z czarną ramką i czarną strzałką w stronę toru.
- **Ustawienie:** na końcu odstępu nieosłoniętego semaforem (gdy nie ma W ETCS 10) albo na maszcie samoczynnego semafora odstępowego
  zdolnego nadać sygnał SE. Oznaczenie tabliczką – jak W ETCS 10.
- **Symulator:** brak.

## 6. § 18 – sygnały i wskaźniki tylko na wyznaczonych liniach i stacjach (s. 201–203)

| Oznaczenie | Opis | Gdzie |
|---|---|---|
| S 1a „Stój – absolutny zakaz wjazdu do tunelu” | czerwone **migające** światło razem z S 1 na samoczynnym semaforze odstępowym przed tunelem; dalsza jazda dopiero po zgaśnięciu i po zezwoleniu dyżurnego ruchu (łącznością) lub upoważnionego pracownika (ustnie) | przed tunelami |
| W 21wg | świetlny, matowobiała liczba na ciemnym tle – prędkość wjazdu na automatyczną wagę w dziesiątkach km/h (np. „0,5”); świeci z sygnałem zezwalającym tylko przy przebiegu wjazdowym na tor z wagą; 40 m przed wagą z obu stron W 9 bez W 8 | Hrubieszów Towarowy, Dorohusk |
| Pc 5-LHS „Oznaczenie końca pociągu” | odblaskowa tarcza na tylnej ścianie ostatniego wagonu, z prawej, podzielona na 4 trójkąty: górny i dolny czerwone, boczne białe | linie PKP LHS |

**Symulator:** brak.

## 7. § 19 – obrazy stosowane do odwołania (s. 204–206)

Dopuszczone przejściowo; informacja o nich ma być w dodatku do wewnętrznego rozkładu jazdy.

| Sygnalizator | Obraz (od góry) | Znaczenie |
|---|---|---|
| semafor | Z, Z (dwa zielone w pionie) | „Wolna droga ze zmniejszoną prędkością” lub „… Następny semafor nadaje sygnał zezwalający” |
| semafor | P, Z (pomarańczowe nad zielonym) | „Wolna droga. Następny semafor wskazuje sygnał zezwalający na jazdę ze zmniejszoną prędkością” |
| tarcza ostrzegawcza | P, Z w pionie | semafor wskazuje „Wjazd ze zmniejszoną prędkością. Następny semafor wskazuje Stój” lub „Wolna droga ze zmniejszoną prędkością. Następny semafor wskazuje sygnał zezwalający” |
| sygnalizator powtarzający | P, B, Z w pionie | semafor wskazuje sygnał zezwalający na jazdę ze zmniejszoną prędkością |

**Symulator:** brak – symulator nadaje obrazy wg § 4 (`sygnaly_ie1.json`). Dodanie tych obrazów – tylko na wniosek właściciela.

## 8. § 20 – wskaźniki dla pracowników (s. 206–207)

Nie dotyczą prowadzenia ruchu; są dla pracowników przy robotach torowych.

- **Wskaźnik kierunku zejścia z toru:** czerwony trójkąt równoramienny z białą obwódką na biało-czerwonym słupku, wierzchołkiem w lewo
  lub w prawo – w stronę wyznaczonego zejścia. Stawia się na początku i końcu robót.
- **Symulator:** brak.

## 9. Załącznik 6 Ie-1 – wskaźniki TSI OPE (s. 243–266, **jeszcze nie obowiązują**)

Zmiany wejdą po zmianie rozporządzenia Ministra Infrastruktury z 18 lipca 2005 r.; do tego czasu obowiązuje tekst opisany wyżej.
Najważniejsze różnice do uwzględnienia, gdy wejdą:

- **We** – tablice niebieskie albo białe (symbol biały albo niebieski), nowe lica We 1–We 4, We 8, We 9; We 4 – dwa romby jeden w drugim.
- **We 10a/b/c** – tylko wjazd: „25 kV”, „15 kV”, „3000 V” (bez odmian wyjazdowych d–f).
- **Nowe:** We 11a/b/c – zapowiedź zmiany systemu zasilania (niebieskie „25 kV”, „15 kV”, „3000 V”; odległość zapowiedzi przed
  We 2a / We 8a); We 12 – zapowiedź zakazu wjazdu EPT (dwa niebieskie romby, 30–100 m przed We 4); We 13 – zapowiedź odcinka
  neutralnego (dwa pionowe paski, odległość zapowiedzi przed We 8a).
- **ETCS:** uchylone W ETCS 1–9; dotychczasowe W ETCS 10 i 11 stają się pkt 1 i 2; nowe W ETCS 12 „Zmiana poziomu ETCS” (biała tablica
  „LT / ETCS / poziom”), W ETCS 13 – podać Rp 1 (czarna tablica z białą trąbką), W ETCS 14a/b/c – zapowiedź, początek, koniec bezpiecznego
  miejsca zatrzymania (tunele), W ETCS 15a/b/c – zapowiedź, początek, koniec zakazu hamulców specjalnych (wiroprądowy, magnetyczny,
  odzyskowy).
- **W 33** w wykazie zał. 3 nazywa się „Wskaźnik początku obowiązywania systemu ERTMS/GSM-R”.

## 10. Zestawienie – stan w symulatorze

| Wskaźnik | Stan | Gdzie w kodzie |
|---|---|---|
| W 4 | obsługiwany | `Lcs.Model/UtDevice.cs` (`W4`), `DoSymulatora.cs` („w4”), `Station.cs` (`W4`), `Sim/Trains.cs` (`Lookahead`) |
| W 5 | obsługiwany | `UtDevice.W5`; `LcsWidzew/Model/Station.cs` (`W5`); `Sim/Kz/Blocks.cs`, `Sim/Kz/KomputerZaleznosci.cs` |
| W 8 | obsługiwany | `UtDevice.W8`, `Vw`; `DoSymulatora.cs` (lit. e → „vlim”) |
| W 9 | obsługiwany | `UtDevice.W9`, `Vw`, `W9C`; `DoSymulatora.cs` („vlim”, „vlimC”); `Station.cs` (`Edge.VLim`); `Sim/Trains.cs` (`EdgeLimit`) |
| W 17 | obsługiwany | `UtDevice.Ukres`; „ukres”; `Station.cs` (`Ukres`); `Sim/Trains.cs` (`Lookahead`, `ZwrotnicaPrzed`) |
| W 21 | obsługiwany | `UtDevice.W21`, `W21Swietlny`; `Station.cs` (`W21`, `Dobor`); `Sim/State.cs` |
| W 24 | obsługiwany (na pulpicie jeszcze nie rysowany) | `UtDevice.W24`; „w24”; `Sim/Kz/KomputerZaleznosci.cs`, `Sim/Kz/Commands.cs`, `Sim/State.cs` |
| pozostałe | brak | – |

### Propozycje (do decyzji właściciela, nic nie jest zrobione)

Wg zasad projektu (`CLAUDE.md`) wskaźniki stawia projektant ręcznie; program może tylko podpowiadać. Kandydaci, którzy wpływają na
pracę dyżurnego lub komputera zależności:

1. **W 2 / W 26a / W 26b** – litera przy semaforze drogowskazowym, zapalana z sygnałem (i W 26 z Sz) – jak W 24.
2. **W 18, W 19, W 20** – przy SBL i skróconych odstępach; W 19 / W 20 świetlne zależą od sygnału, więc to stan semafora.
3. **W 3, W 31** – unieważnienie / skasowanie semafora (semafor ciemny).
4. **W 14, W 27a, W 35, W 36** – pozostałe ograniczenia prędkości (W 8 i W 9 – zrobione 2026-10-29).
5. **W 23** – granica odcinka izolowanego dla manewrów (dziś granice wyznaczają złącza i liczniki osi).
6. **We** – dopiero po zamodelowaniu sieci trakcyjnej i jej sekcji.
