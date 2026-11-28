"""Podgląd na żywo przejazdów (SSP / UZK / TOP) w trakcie testów – czyta to, co mod SimRailMost dopisuje do
stany_diag_*.jsonl (rodzaje przejazdP0, przejazdP0swiat, przejazdP0_3d) i polecenia_*.jsonl (ZAMK / OTW …).
Niczego nie wysyła do gry ani SUK – tylko czyta pliki.

Użycie:  python -I przejazdy_podglad.py [katalog_UserData_SimRailMost] [--calosc]
  --calosc   wczytaj też to, co już jest w plikach (domyślnie: tylko od uruchomienia; stan początkowy i tak
             jest odtwarzany z dzisiejszego pliku, ale dziennik zdarzeń zaczyna się pusty)
Wyjście: Ctrl+C.
"""
import glob
import json
import os
import re
import shutil
import sys
import time
from collections import deque
from datetime import datetime

DOMYSLNY = r"D:\SteamLibrary\steamapps\common\SimRail\UserData\SimRailMost"
POLECENIA = {74: "OTW", 75: "ZAMK", 76: "ZAMK AW", 77: "wył. TOP", 78: "zwoln. ręczne", 93: "zamk. rog. wj", 94: "otw. rog. wj",
             95: "zamk. rog. wy", 96: "otw. rog. wy", 97: "STOP rogatek"}
BARWY = {  # nazwa barwy wg Ie-104 → rgb
    "różowa": (0xff, 0x99, 0xff), "szara": (0x66, 0x66, 0x66), "żółta": (0xff, 0xd6, 0x00), "biała": (0xff, 0xff, 0xff),
    "czerwona": (0xff, 0x00, 0x00), "zielona": (0x00, 0xc0, 0x00), "czarna": (0x00, 0x00, 0x00), "niebieska": (0x00, 0x80, 0xff),
}
ANSI = {"różowa": "95", "szara": "90", "żółta": "93", "biała": "97", "czerwona": "91", "zielona": "92", "niebieska": "94", "czarna": "30"}
MIGANIE_S = 3.0  # ≥2 zmiany barwy części w tym oknie = miga


def kolor(tekst, kod):
    return f"\x1b[{kod}m{tekst}\x1b[0m" if kod else tekst


def barwa(hexa):
    """„ff99ff” → „różowa”; „-” → ukryta."""
    if hexa in ("-", "brak", "?", None, ""):
        return "–"
    try:
        r, g, b = int(hexa[0:2], 16), int(hexa[2:4], 16), int(hexa[4:6], 16)
    except ValueError:
        return hexa
    return min(BARWY, key=lambda n: sum((x - y) ** 2 for x, y in zip((r, g, b), BARWY[n])))


def flagi(tekst):
    m = re.search(r"\((\d+)\)", str(tekst or ""))
    return int(m.group(1)) if m else None


def opis_stanu(ogolny):
    f = flagi(ogolny)
    if f is None:
        return "?", ""
    if f & 128:
        return "AWARIA", "91"
    if f & 4:
        return "rogatki w ruchu", "95"
    if f & 8:
        return "zamknięty" + (" (utrzym.)" if f & 32 else ""), "90"
    if f & 2:
        return "otwarty", "95"
    if f & 1:
        return "brak danych", "97"
    return str(ogolny), ""


def czesci(lista):
    """["rog1=ff99ff", …] → [("rog1", "ff99ff"), …]"""
    if not isinstance(lista, list):
        return []
    wyn = []
    for x in lista:
        n, _, h = str(x).partition("=")
        wyn.append((n, h))
    return wyn


class Przejazd:
    def __init__(self, nazwa):
        self.nazwa = nazwa
        self.pulpit = None   # ostatni stan z pulpitu
        self.swiat = None    # z RailNetwork
        self.trzyd = None    # z CrossingNetworkHolder
        self.zmiana = None   # czas ostatniej zmiany (time.time())
        self.historia_barw = {}  # część → deque[(czas, hex)]

    def podpisz_barwy(self, s, teraz):
        for grupa in ("rogatki", "swiatla", "korpus", "maska"):
            for i, (n, h) in enumerate(czesci(s.get(grupa))):
                d = self.historia_barw.setdefault(f"{grupa}{i}", deque(maxlen=8))
                if not d or d[-1][1] != h:
                    d.append((teraz, h))

    def miga(self, grupa, teraz):
        for k, d in self.historia_barw.items():
            if k.startswith(grupa) and sum(1 for t, _ in d if teraz - t <= MIGANIE_S) >= 2:
                return True
        return False


class Ogon:
    """Czyta przyrostowo najnowszy plik pasujący do wzorca (przechodzi na nowy przy zmianie dnia)."""

    def __init__(self, wzorzec, od_konca):
        self.wzorzec, self.plik, self.poz, self.od_konca = wzorzec, None, 0, od_konca

    def nowe(self):
        pliki = sorted(glob.glob(self.wzorzec))
        if not pliki:
            return []
        if pliki[-1] != self.plik:
            self.plik, self.poz = pliki[-1], 0
        try:
            rozm = os.path.getsize(self.plik)
            if rozm < self.poz:
                self.poz = 0
            if rozm == self.poz:
                return []
            with open(self.plik, "rb") as f:
                f.seek(self.poz)
                dane = f.read()
        except OSError:
            return []
        koniec = dane.rfind(b"\n")
        if koniec < 0:
            return []
        self.poz += koniec + 1
        wyn = []
        for linia in dane[:koniec].decode("utf-8", "replace").splitlines():
            try:
                wyn.append(json.loads(linia))
            except ValueError:
                pass
        return wyn


def main():
    arg = [a for a in sys.argv[1:] if not a.startswith("--")]
    calosc = "--calosc" in sys.argv
    kat = arg[0] if arg else DOMYSLNY
    os.system("")  # włącza sekwencje ANSI w konsoli Windows
    sys.stdout.reconfigure(encoding="utf-8")

    przejazdy = {}
    zdarzenia = deque(maxlen=200)
    diag = Ogon(os.path.join(kat, "stany_diag_*.jsonl"), False)
    pol = Ogon(os.path.join(kat, "polecenia_*.jsonl"), False)
    start = True

    def zdarzenie(czas, tekst, kod=""):
        if not start or calosc:
            zdarzenia.append(kolor(f"{(czas or '')[11:19]}  {tekst}", kod))

    while True:
        teraz = time.time()
        for r in diag.nowe():
            rodzaj = r.get("rodzaj", "")
            if not rodzaj.startswith("przejazdP0"):
                continue
            nazwa = (r.get("nazwa") or "?").lower()
            p = przejazdy.setdefault(nazwa, Przejazd(nazwa))
            s = r.get("stan") or {}
            if "blad" in s:
                continue
            stary_opis = opis_stanu(((p.pulpit or {}).get("stan") or {}).get("ogolny"))[0] if p.pulpit else None
            if rodzaj == "przejazdP0":
                p.pulpit = s
                p.podpisz_barwy(s, teraz)
                nowy = opis_stanu((s.get("stan") or {}).get("ogolny"))
                if nowy[0] != stary_opis:
                    zdarzenie(r.get("czasPc"), f"{s.get('widoczna') or nazwa:>10}  {stary_opis or '—'} → {nowy[0]}", nowy[1])
            elif rodzaj == "przejazdP0swiat":
                p.swiat = s
            else:
                p.trzyd = s
            p.zmiana = teraz
        for r in pol.nowe():
            a = r.get("arg") or {}
            nr = flagi(a.get("order"))
            if nr in POLECENIA:
                zdarzenie(r.get("czasPc"), f"{a.get('name')}  polecenie {POLECENIA[nr]} („{a.get('orderHistoryString')}”) {r.get('metoda')}", "96")
        start = False
        rysuj(kat, przejazdy, zdarzenia, teraz)
        try:
            time.sleep(0.5)
        except KeyboardInterrupt:
            break


def rysuj(kat, przejazdy, zdarzenia, teraz):
    szer, wys = shutil.get_terminal_size((140, 40))
    w = []
    w.append(kolor(f"SimRailMost – przejazdy na żywo   {datetime.now():%H:%M:%S}   ({kat})   Ctrl+C – koniec", "1"))
    w.append("")
    w.append(kolor(f"{'km':>9} {'typ':<5} {'stan (gra)':<20} {'sygnały':<10} {'rog. wj/wy':<20} {'symbol na pulpicie':<34} {'licznik':<8} {'zm.':>5}", "4"))
    pulpitowe = sorted((p for p in przejazdy.values() if p.pulpit), key=lambda p: str(p.pulpit.get("widoczna") or p.nazwa))
    for p in pulpitowe:
        s = p.pulpit
        st = s.get("stan") or {}
        typ = ("SSP" if s.get("automatic") or s.get("remote") else "UZK" if s.get("hideOrders") else "obsł.") + ("+T" if s.get("hasTOP") else "")
        opis, kod = opis_stanu(st.get("ogolny"))
        rog = czesci(s.get("rogatki"))
        swi = czesci(s.get("swiatla"))
        sym = []
        for etykieta, grupa, lista in (("rog", "rogatki", rog), ("św", "swiatla", swi)):
            b = sorted({barwa(h) for _, h in lista} - {"–"})
            if b:
                sym.append(f"{etykieta} {'/'.join(b)}" + (" MIGA" if p.miga(grupa, teraz) else ""))
        if s.get("confirmed"):
            sym.append("potw.")
        if s.get("manualReleased"):
            sym.append("zwoln.")
        symtxt = ", ".join(sym) or "–"
        kod_sym = ANSI.get(barwa(rog[0][1]) if rog else "", "")
        licz = f"{s.get('licznikS')} s" if s.get("licznikWidoczny") else ""
        wiek = f"{int(teraz - p.zmiana)}s" if p.zmiana else ""
        w.append(f"{str(s.get('widoczna') or p.nazwa):>9} {typ:<5} {kolor(f'{opis:<20}', kod)} {str(st.get('sygnaly', '')).split(' (')[0]:<10} "
                 f"{(str(st.get('rogatkiWj', '')).split(' (')[0] + '/' + str(st.get('rogatkiWy', '')).split(' (')[0]):<20} "
                 f"{kolor(f'{symtxt[:34]:<34}', kod_sym)} {licz:<8} {wiek:>5}")
    if not pulpitowe:
        w.append("  (brak danych z pulpitu – gra nie działa, nie jesteś na posterunku albo mod sprzed 0.11.2)")

    poza = sorted(p for p in przejazdy if not przejazdy[p].pulpit and (przejazdy[p].trzyd or przejazdy[p].swiat))
    if poza:
        w.append("")
        w.append(kolor(f"Poza pulpitem (świat 3D): {len(poza)}", "4"))
        linia = ""
        for n in poza:
            p = przejazdy[n]
            st = ((p.trzyd or p.swiat or {}).get("stan") or {})
            opis, kod = opis_stanu(st.get("ogolny"))
            el = f"{n[:26]}: {kolor(opis, kod)}   "
            if len(linia) + len(n) + 30 > szer:
                w.append(linia)
                linia = ""
            linia += el
        if linia:
            w.append(linia)

    w.append("")
    w.append(kolor("Zdarzenia (zmiany stanu z pulpitu, polecenia przejazdów):", "4"))
    miejsce = max(3, wys - len(w) - 1)
    lista = list(zdarzenia)[-miejsce:]
    w.extend(lista if lista else ["  (czekam…)"])
    sys.stdout.write("\x1b[H" + "\n".join(x + "\x1b[K" for x in w) + "\x1b[J")
    sys.stdout.flush()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
