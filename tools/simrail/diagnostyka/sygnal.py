"""Sygnał zastępczy na semaforze SimRail przez mod SimRailMost (UDP 127.0.0.1:47131) – odpowiednik zwrotnica.py.

Użycie:
  python sygnal.py W63 SZ     – sygnał zastępczy (inicjacja, po 1,5 s potwierdzenie)
  python sygnal.py W63 SZI    – tylko inicjacja
  python sygnal.py W63 NSZ    – sygnał zastępczy z W24 (wyjazd pod prąd)
  python sygnal.py W63 OSZ    – odwołanie sygnału zastępczego (też oSZ)
  python sygnal.py ko_w63 SZ  – można podać od razu nazwę w sieci gry

Nazwę semafora w grze bierzemy z najnowszego zrzutu w UserData/SimRailMost (pulpit_*.json, zrzut_serwer_*.json):
„W63” pasuje do nazwy „w63” albo kończącej się na „_w63”. Gra przyjmie polecenie tylko na wczytanym posterunku.
"""
import glob
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zwrotnica as zw  # noqa: E402  (wysyłanie i odczyt odpowiedzi jak dla zwrotnic)

KODY = {
    "SZ": ["SubsidiarySignalInit", "SubsidiarySignalConfirm"],
    "SZI": ["SubsidiarySignalInit"],
    "NSZ": ["SubsidiarySignalWithW24Init", "SubsidiarySignalWithW24Confirm"],
    "OSZ": ["RevokeSubsidiarySignal"],
}
# Pulpit Domino (Katowice, Łazy): podsłuch kliknięć 26.11.2026 – wyciągnięcie przycisku Sz wysyła samo
# SubsidiarySignalConfirm (25), wciśnięcie RevokeSubsidiarySignal (28); inicjacji (24) pulpit nie wysyła.
KODY_DOMINO = {
    "SZ": ["SubsidiarySignalConfirm"],
    "SZI": ["SubsidiarySignalConfirm"],
    "NSZ": ["SubsidiarySignalWithW24Confirm"],
    "OSZ": ["RevokeSubsidiarySignal"],
}


def znajdz_nazwe(tekst):
    t = tekst.lower()
    pliki = glob.glob(os.path.join(zw.KATALOG, "pulpit_*.json")) + glob.glob(os.path.join(zw.KATALOG, "zrzut_serwer_*.json"))
    for plik in sorted(pliki, key=os.path.getmtime, reverse=True):
        try:
            dane = open(plik, encoding="utf-8").read()
        except OSError:
            continue
        nazwy = {n for n in re.findall(r'"([A-Za-z0-9_/]+)"', dane) if n.lower() == t or n.lower().endswith("_" + t)}
        if nazwy:
            return sorted(nazwy, key=len)[0], os.path.basename(plik), "DominoPanelButtonRenderer" in dane
    return (t, None, False) if "_" in t else (None, None, False)


def main():
    arg = sys.argv[1:]
    if len(arg) != 2 or arg[1].upper() not in KODY:
        print(__doc__)
        return 1
    nazwa, plik, domino = znajdz_nazwe(arg[0])
    if not nazwa:
        print(f"Nie znalazłem semafora „{arg[0]}” w zrzutach {zw.KATALOG} – podaj nazwę z gry, np. ko_w63.")
        return 1
    if plik:
        print(f"{arg[0]} = {nazwa} (z {plik}{', pulpit Domino' if domino else ''})")
    ordery = (KODY_DOMINO if domino else KODY)[arg[1].upper()]
    for i, order in enumerate(ordery):
        if i:
            time.sleep(1.5)
        zw.przestaw({"rnn": nazwa, "prefix": "", "nr": nazwa}, order)
    return 0


if __name__ == "__main__":
    sys.exit(main())
