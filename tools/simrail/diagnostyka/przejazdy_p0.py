"""P0 planu przejazdów (SUK/PLAN_PRZEJAZDY_SSP_UZK.md) – zestawienie tego, co mod zebrał w grze.

Czyta z katalogu UserData/SimRailMost (domyślnie w folderze gry, albo podany jako argument):
  1. pulpit_*.json        – przejazdy z każdego zrzuconego pulpitu EBI i ich flagi (automatic, remote, lights, hasTOP …),
  2. stany_diag_*.jsonl   – rodzaj „przejazdP0” (pulpit) i „przejazdP0swiat” (świat 3D): kolejne stany każdego przejazdu,
  3. polecenia_*.jsonl    – polecenia przejazdów wysłane z pulpitu gry (podsłuch SendRPCCall).
Wynik: przejazdy_P0.md w tym samym katalogu (i na ekran).

Użycie:  python przejazdy_p0.py [katalog_UserData_SimRailMost]
"""
import glob
import json
import os
import sys
from collections import OrderedDict, defaultdict

DOMYSLNY = r"D:\SteamLibrary\steamapps\common\SimRail\UserData\SimRailMost"
FLAGI = ("automatic", "remote", "lights", "hasTOP", "hideOrders", "hideAuth")
POLECENIA_PRZEJAZDU = ("Crossing", "Przejazd", "TOP", "Barrier", "74)", "75)", "76)", "77)", "78)", "93)", "94)", "95)", "96)", "97)")


def jsonl(wzorzec):
    for plik in sorted(glob.glob(wzorzec)):
        with open(plik, encoding="utf-8", errors="replace") as f:
            for linia in f:
                linia = linia.strip()
                if not linia:
                    continue
                try:
                    yield json.loads(linia)
                except ValueError:
                    pass


def main():
    kat = sys.argv[1] if len(sys.argv) > 1 else DOMYSLNY
    wyj = []
    p = wyj.append

    # 1. pulpity
    p("# P0 – przejazdy w grze\n")
    p("## 1. Przejazdy na zrzuconych pulpitach EBI\n")
    p("| posterunek / ekran | nazwa w sieci | km | " + " | ".join(FLAGI) + " |")
    p("|---|---|---|" + "---|" * len(FLAGI))
    przejazdy = OrderedDict()
    for plik in sorted(glob.glob(os.path.join(kat, "pulpit_*.json"))):
        try:
            d = json.load(open(plik, encoding="utf-8"))
        except (ValueError, OSError):
            continue
        ekran = f"{d.get('posterunek', '?')} / {d.get('ekran', '?')}"
        for e in d.get("elementy", []):
            if e.get("klasa") != "EBI300RoadCrossing":
                continue
            pola = e.get("pola", {})
            przejazdy[(ekran, (pola.get("railNetworkName") or "").lower())] = pola
    for (ekran, nazwa), pola in przejazdy.items():
        p(f"| {ekran} | {nazwa} | {pola.get('visibleName', '')} | " + " | ".join("**tak**" if pola.get(f) else "–" for f in FLAGI) + " |")
    ciekawe = [k for k, v in przejazdy.items() if v.get("remote") or v.get("automatic") or v.get("hasTOP")]
    p(f"\nPrzejazdów: {len(przejazdy)}; z remote / automatic / hasTOP: **{len(ciekawe)}**"
      + ("" if ciekawe else " – trzeba wejść na posterunek z przejazdem SSP (mod sam zrzuci pulpit)."))

    # 2. stany
    p("\n## 2. Stany z pulpitu (przejazdP0) – każdy różny podpis\n")
    stany = defaultdict(list)
    swiat = defaultdict(list)
    trzyd = defaultdict(list)
    for r in jsonl(os.path.join(kat, "stany_diag_*.jsonl")):
        if r.get("rodzaj") == "przejazdP0":
            stany[r.get("nazwa")].append(r)
        elif r.get("rodzaj") == "przejazdP0swiat":
            swiat[r.get("nazwa")].append(r)
        elif r.get("rodzaj") == "przejazdP0_3d":
            trzyd[r.get("nazwa")].append(r)
    if not stany:
        p("Brak wpisów – mod 0.11.2 jeszcze nie działał w grze.")
    for nazwa, lista in stany.items():
        p(f"### {nazwa} ({len(lista)} zmian)\n")
        for r in lista:
            s = r.get("stan", {})
            if "blad" in s:
                p(f"- `{r.get('czasPc')}` BŁĄD odczytu: {s['blad']} (mod sprzed poprawki 0.11.2b)")
                continue
            st = s.get("stan") or {}
            p(f"- `{r.get('czasPc')}` ogólny {st.get('ogolny')}, sygnały {st.get('sygnaly')}, rogatki wj {st.get('rogatkiWj')} / wy {st.get('rogatkiWy')}, "
              f"tory wył. {st.get('toryWyl')}; confirmed {s.get('confirmed')}, manualReleased {s.get('manualReleased')}, obszar {s.get('obszar')} "
              f"„{s.get('napisObszaru')}”, licznik {s.get('licznikWidoczny')} {s.get('licznikS')} s „{s.get('licznikNapis')}”")
            p(f"  - barwy: rogatki {s.get('rogatki')}, światła {s.get('swiatla')}, korpus {s.get('korpus')}, maska {s.get('maska')}, "
              f"licznik {s.get('barwaLicznika')}, tło {s.get('tlo')}")
        p("")

    p("## 3. Stany ze świata 3D (przejazdP0swiat)\n")
    if not swiat:
        p("Brak wpisów (w roli dyżurnego RailNetwork bywa puste).")
    else:
        p("| przejazd | stacja | zmian | stany ogólne |")
        p("|---|---|---|---|")
        for nazwa, lista in sorted(swiat.items()):
            ogolne = sorted({(r.get("stan", {}).get("stan") or {}).get("ogolny", "?") for r in lista})
            p(f"| {nazwa} | {lista[-1].get('stan', {}).get('stacja', '')} | {len(lista)} | {', '.join(ogolne)} |")

    p("\n## 3a. Przejazdy wczytane w świecie 3D (CrossingNetworkHolder) – także spoza pulpitu\n")
    if not trzyd:
        p("Brak wpisów (mod sprzed poprawki albo gra jeszcze nie wczytała świata).")
    else:
        p("| przejazd | obiekt 3D | dróżnik | xyz | zmian | stany ogólne | ostatni stan |")
        p("|---|---|---|---|---|---|---|")
        for nazwa, lista in sorted(trzyd.items()):
            s0 = lista[-1].get("stan", {})
            ogolne = sorted({str((r.get("stan", {}).get("stan") or {}).get("ogolny", "?")) for r in lista})
            st = s0.get("stan") or {}
            p(f"| {nazwa} | {s0.get('przejazd') or s0.get('obiekt')} | {s0.get('dróżnik')} | {s0.get('xyz')} | {len(lista)} | {', '.join(ogolne)} | "
              f"sygn. {st.get('sygnaly')}, wj {st.get('rogatkiWj')}, wy {st.get('rogatkiWy')}, tory wył. {st.get('toryWyl')} |")
        p(f"\nPrzejazdów 3D: **{len(trzyd)}**")

    # 3. polecenia
    p("\n## 4. Polecenia przejazdów z pulpitu gry (podsłuch)\n")
    n = 0
    for r in jsonl(os.path.join(kat, "polecenia_*.jsonl")):
        a = r.get("arg") or {}
        order = str(a.get("order", ""))
        if any(k in order for k in POLECENIA_PRZEJAZDU):
            n += 1
            p(f"- `{r.get('czasPc')}` {r.get('metoda')} {a.get('code')} **{order}** na `{a.get('name')}` – historia „{a.get('orderHistoryString')}”")
    if n == 0:
        p("Brak.")

    tekst = "\n".join(wyj) + "\n"
    with open(os.path.join(kat, "przejazdy_P0.md"), "w", encoding="utf-8") as f:
        f.write(tekst)
    sys.stdout.reconfigure(encoding="utf-8")
    print(tekst)


if __name__ == "__main__":
    main()
