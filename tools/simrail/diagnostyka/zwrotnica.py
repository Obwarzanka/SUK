"""Prosty kontroler zwrotnic SimRail przez mod SimRailMost (UDP 127.0.0.1:47131).

Użycie:
  python zwrotnica.py              – tryb interaktywny
  python zwrotnica.py 71ab +       – przełóż 71ab na plus (+ / plus / p)
  python zwrotnica.py 71ab -       – przełóż 71ab na minus (- / minus / m)
  python zwrotnica.py lista [71]   – lista zwrotnic (opcjonalnie filtr)
  python zwrotnica.py stacje       – stacje, których pulpit jest zapisany
  python zwrotnica.py -s Ła 12 +   – inna stacja (nazwa lub skrót z pulpitu, np. Ła, Łazy, ko)

Domyślna stacja: ta, której pulpit zapisano ostatnio (czyli obsadzana teraz). Gra przyjmie polecenie tylko
dla obiektów pulpitu, który jest teraz wczytany – zapis innej stacji służy tylko do listy numerów.

Zwrotnicę podajesz numerem z pulpitu (visibleName, np. 71ab, 5) albo nazwą w sieci gry (np. z2029cd).
Numery bierzemy z najnowszego zrzutu pulpitu: UserData/SimRailMost/pulpit_*.json (robi go mod po wczytaniu posterunku).
Wynik: z logu MelonLoadera (linia „[polecenie z SUK] wysłano/odrzucono”), a gdy SUK nie zajmuje portu 47130 –
także odpowiedź moda i aktualne położenie zwrotnicy.
"""
import glob
import json
import os
import re
import socket
import sys
import time
import unicodedata

GRA = r"D:\SteamLibrary\steamapps\common\SimRail"
KATALOG = os.path.join(GRA, "UserData", "SimRailMost")
LOG = os.path.join(GRA, "MelonLoader", "Latest.log")
PORT_POLECEN = 47131
PORT_STANOW = 47130
KLASY = ("Junction", "Derailer")  # zwrotnice i wykolejnice


def bez_ogonkow(t):
    t = unicodedata.normalize("NFKD", (t or "").replace("ł", "l").replace("Ł", "L"))
    return "".join(c for c in t if not unicodedata.combining(c)).lower().strip()


def wczytaj_json(plik):
    try:
        return json.load(open(plik, encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None


def numer_z_nazwy(rnn):
    """Numer i skrót stacji z nazwy w sieci gry: z_la_50 → 50 (la), la_wk11 → Wk11 (la), z2029cd → z2029cd."""
    m = re.match(r"^z_([a-z]+)_(.+)$", rnn, re.I)
    if m:
        return m.group(2), m.group(1)
    m = re.match(r"^([a-z]+)_wk(.+)$", rnn, re.I)
    if m:
        return "Wk" + m.group(2), m.group(1) if len(m.group(1)) <= 3 else ""
    m = re.match(r"^\d+_([a-z]+)_(wk.+)$", rnn, re.I)  # 1803_KO_Wk109
    if m:
        return m.group(2)[:1].upper() + m.group(2)[1:], m.group(1).lower()
    return rnn, ""


def zrzuty():
    """Stacje z zwrotnicami: {posterunek: {"zw": {rnn: {...}}, "prefiksy": {...}, "czas": mtime}}.

    Lista zwrotnic – z najnowszego zrzutu stanów serwera każdej stacji (zrzut_serwer_*.json; ma go każdy posterunek,
    także pulpity Domino). Numery – z zapisu pulpitu EBI (pulpit_*.json), gdy jest; inaczej z nazwy obiektu.
    """
    # numery z pulpitów EBI: najnowszy zapis każdego ekranu
    ebi, ebi_czas = {}, {}
    for plik in sorted(glob.glob(os.path.join(KATALOG, "pulpit_*.json")), key=os.path.getmtime):
        d = wczytaj_json(plik)
        if not d:
            continue
        for e in d.get("elementy", []):
            p = e.get("pola", {})
            if any(k in e.get("klasa", "") for k in KLASY) and p.get("railNetworkName") and p.get("visibleName"):
                ebi[p["railNetworkName"].lower()] = (p["visibleName"], p.get("prefix") or "", d.get("posterunek", ""))
        ebi_czas[d.get("posterunek", "")] = os.path.getmtime(plik)

    stacje = {}
    for plik in sorted(glob.glob(os.path.join(KATALOG, "zrzut_serwer_*.json")), key=os.path.getmtime):
        d = wczytaj_json(plik)
        if not d:
            continue
        nowe = {}
        for o in d.get("obiekty", []):
            if o.get("rodzaj") in ("zwrotnica", "wykolejnica") and o.get("nazwa"):
                nowe.setdefault(o.get("stacja") or "?", []).append(o)
        for post, lista in nowe.items():  # późniejszy plik zastępuje wcześniejszy
            zw, prefiksy = {}, set()
            for o in lista:
                rnn = o["nazwa"]
                if rnn.lower() in ebi:
                    nr, pref, _ = ebi[rnn.lower()]
                else:
                    nr, pref = numer_z_nazwy(rnn)
                if pref:
                    prefiksy.add(pref)
                zw[rnn.lower()] = {"nr": nr, "rnn": rnn, "prefix": pref, "posterunek": post}
            stacje[post] = {"zw": zw, "prefiksy": prefiksy, "czas": os.path.getmtime(plik)}

    # stacje znane tylko z pulpitu EBI (bez zrzutu serwera)
    for rnn, (nr, pref, post) in ebi.items():
        if post in stacje and not stacje[post].get("tylko_ebi"):
            continue
        s = stacje.setdefault(post, {"zw": {}, "prefiksy": set(), "czas": ebi_czas.get(post, 0), "tylko_ebi": True})
        s["zw"][rnn] = {"nr": nr, "rnn": rnn, "prefix": pref, "posterunek": post}
        if pref:
            s["prefiksy"].add(pref)
    # stare zrzuty (wcześniejsze wersje moda): stacja „?” albo nazwa ekranu („LEWA”) – te same zwrotnice co nowsza stacja
    for post in sorted(stacje, key=lambda p: stacje[p]["czas"]):
        klucze = set(stacje[post]["zw"])
        if post == "?" or any(inna != post and klucze <= set(s["zw"]) and s["czas"] >= stacje[post]["czas"]
                              for inna, s in stacje.items()):
            del stacje[post]
    for s in stacje.values():  # skrót stacji: najczęstszy przedrostek (pojedyncze obce zwrotnice nie psują)
        licz = {}
        for z in s["zw"].values():
            if z["prefix"]:
                licz[z["prefix"]] = licz.get(z["prefix"], 0) + 1
        s["prefiksy"] = {max(licz, key=licz.get)} if licz else set()
    return stacje


def wybierz_stacje(stacje, nazwa=None):
    if not stacje:
        sys.exit(f"Brak zrzutu stanów w {KATALOG} – obsadź posterunek w grze z modem SimRailMost (zrzut robi się sam).")
    if not nazwa:  # domyślnie stacja, na której ostatnio dyżurowałeś
        return max(stacje, key=lambda p: stacje[p]["czas"])
    n = bez_ogonkow(nazwa)
    for p in stacje:  # dokładna nazwa (np. „Łazy” obok „Łazy Ła”)
        if bez_ogonkow(p) == n:
            return p
    for p, s in stacje.items():  # skrót z nazw obiektów (np. Ła, ko)
        if n in {bez_ogonkow(x) for x in s["prefiksy"]}:
            return p
    pasuje = [p for p in stacje if bez_ogonkow(p).startswith(n) or n in bez_ogonkow(p)]
    if len(pasuje) == 1:
        return pasuje[0]
    return None


def wczytaj_zwrotnice(stacje, posterunek):
    return stacje[posterunek]["zw"]


def opis_stacji(stacje, p):
    s = stacje[p]
    kiedy = time.strftime("%Y-%m-%d %H:%M", time.localtime(s["czas"]))
    return f"{p} ({', '.join(sorted(s['prefiksy'])) or '-'}), zrzut {kiedy}"


def pokaz_stacje(stacje):
    for p in sorted(stacje, key=lambda p: -stacje[p]["czas"]):
        print("  " + opis_stacji(stacje, p))


def znajdz(zw, tekst):
    t = tekst.strip().lower()
    if t in zw:
        return [zw[t]]
    dokladne = [z for z in zw.values() if z["nr"].lower() == t]
    if dokladne:
        return dokladne
    return sorted((z for z in zw.values() if z["nr"].lower().startswith(t)), key=lambda z: z["nr"])


def gniazdo_stanow():
    """Port stanów 47130 tylko gdy wolny (wyłączny – nie podkradamy pakietów SUK)."""
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        if hasattr(socket, "SO_EXCLUSIVEADDRUSE"):
            s.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        s.bind(("127.0.0.1", PORT_STANOW))
        s.settimeout(0.2)
        return s
    except OSError:
        s.close()
        return None


def rozmiar_logu():
    try:
        return os.path.getsize(LOG)
    except OSError:
        return 0


def nowe_linie_logu(od):
    try:
        with open(LOG, "rb") as f:
            f.seek(od)
            return f.read().decode("utf-8", "replace").splitlines()
    except OSError:
        return []


def przestaw(z, order):
    id_ = int(time.time() * 1000) % 1_000_000_000
    pakiet = {"v": 1, "typ": "polecenie", "id": id_, "kod": "IndividualOrder", "nazwa": z["rnn"], "order": order}
    stany = gniazdo_stanow()
    log_od = rozmiar_logu()
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
        s.sendto(json.dumps(pakiet).encode("utf-8"), ("127.0.0.1", PORT_POLECEN))
    print(f"→ {order.upper()} {z['prefix']}{z['nr']} ({z['rnn']})")

    odp, polozenie, koniec = None, None, time.time() + 3
    while time.time() < koniec and (odp is None or (stany and polozenie is None)):
        if stany:
            try:
                dane, _ = stany.recvfrom(65535)
                m = json.loads(dane)
                if m.get("typ") == "odp" and m.get("id") == id_:
                    odp = m
                for o in m.get("obiekty", []) + ([m] if m.get("typ") == "zmiana" else []):
                    if (o.get("n") or "").lower() == z["rnn"].lower() and o.get("zw"):
                        polozenie = o["zw"]
            except (socket.timeout, ValueError):
                pass
            except OSError:
                pass
        else:
            time.sleep(0.2)
            if any("polecenie z SUK" in l for l in nowe_linie_logu(log_od)):
                break
    if stany:
        stany.close()

    if odp is not None:
        print(("  OK: " if odp.get("ok") else "  ODMOWA: ") + odp.get("msg", ""))
    for l in nowe_linie_logu(log_od):
        if "polecenie z SUK" in l:
            print("  log:", l.split("] ", 2)[-1] if "] " in l else l)
    if odp is None and not any("polecenie z SUK" in l for l in nowe_linie_logu(log_od)):
        print("  brak odpowiedzi – czy gra z modem SimRailMost jest uruchomiona i posterunek obsadzony?")
    if polozenie:
        print(f"  położenie teraz: {polozenie} (zmiana może przyjść po kilku sekundach przekładania)")


ORDERY = {"+": "Plus", "plus": "Plus", "p": "Plus", "-": "Minus", "minus": "Minus", "m": "Minus"}


def lista(zw, filtr=""):
    pasujace = sorted((z for z in zw.values() if z["nr"].lower().startswith(filtr.lower())),
                      key=lambda z: (len(z["nr"]), z["nr"]))
    for z in pasujace:
        print(f"  {z['nr']:>8}  {z['rnn']}")
    print(f"  ({len(pasujace)} zwrotnic)")


def polecenie(zw, slowa):
    if not slowa:
        return
    if slowa[0].lower() in ("lista", "l", "?"):
        return lista(zw, slowa[1] if len(slowa) > 1 else "")
    if len(slowa) < 2 or slowa[1].lower() not in ORDERY:
        print("Podaj: <numer> + | <numer> -   (np. 71ab +), albo: lista [filtr]")
        return
    znalezione = znajdz(zw, slowa[0])
    if not znalezione:
        print(f"Nie ma zwrotnicy '{slowa[0]}' na pulpicie. Wpisz: lista")
        return
    if len(znalezione) > 1:
        print("Niejednoznaczne, pasują: " + ", ".join(z["nr"] for z in znalezione))
        return
    przestaw(znalezione[0], ORDERY[slowa[1].lower()])


def main():
    arg = sys.argv[1:]
    stacje = zrzuty()
    nazwa = None
    if len(arg) >= 2 and arg[0] in ("-s", "--stacja"):
        nazwa, arg = arg[1], arg[2:]
    if arg and arg[0].lower() == "stacje":
        return pokaz_stacje(stacje)
    post = wybierz_stacje(stacje, nazwa)
    if post is None:
        print(f"Brak zrzutu pulpitu stacji '{nazwa}'. Obsadź ją w grze (mod zapisze pulpit sam), dostępne:")
        return pokaz_stacje(stacje)
    zw = wczytaj_zwrotnice(stacje, post)
    if arg:
        return polecenie(zw, arg)
    print(f"Kontroler zwrotnic SimRail – {opis_stacji(stacje, post)}, {len(zw)} zwrotnic")
    print("Wpisz np. '71ab +', '5 -', 'lista', 'lista 7', 'stacje', 'stacja Ła'; pusta linia = koniec.")
    print("Uwaga: gra wykona polecenie tylko dla stacji, którą teraz obsadzasz.")
    while True:
        try:
            linia = input(f"{post}> ").strip()
        except (EOFError, KeyboardInterrupt):
            break
        if not linia:
            break
        slowa = linia.split()
        if slowa[0].lower() == "stacje":
            stacje = zrzuty(); pokaz_stacje(stacje); continue
        if slowa[0].lower() == "stacja" and len(slowa) > 1:
            stacje = zrzuty()
            nowa = wybierz_stacje(stacje, " ".join(slowa[1:]))
            if nowa is None:
                print("Nie ma zrzutu takiej stacji – obsadź ją w grze. Dostępne:"); pokaz_stacje(stacje)
            else:
                post, zw = nowa, wczytaj_zwrotnice(stacje, nowa)
                print(f"  {opis_stacji(stacje, post)}, {len(zw)} zwrotnic")
            continue
        # „71ab+” / „71ab-” bez spacji też działa
        polecenie(zw, re.sub(r"\s*([+-])\s*$", r" \1", linia).split())


if __name__ == "__main__":
    main()
