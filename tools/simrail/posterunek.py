"""
Posterunki SimRail w SUK – jedno polecenie zamiast skryptów na każdą stację (PLAN_POSTERUNKI_SIMRAIL.md, P1–P2).

Katalog stacji (SUK_STACJE) – ten sam układ w paczce wydania i u autora:

  <stacje>\\posterunki.json                     rejestr: posterunek gry → kod SUK → układ (katalog)
  <stacje>\\<układ>\\edytor\\<układ>.zob.json      zobrazowanie (Edytor zobrazowania) – projektuje człowiek
  <stacje>\\<układ>\\edytor\\<układ>.simrail.json  mapa: obraz → element / odcinek rysunku → ID obiektu gry (zakładka Gra)
  <stacje>\\<układ>\\edytor\\<układ>.ut.json       UT (Edytor UT) – niepotrzebny do pulpitu zdalnego
  <stacje>\\<układ>\\<układ>_zob.json             pulpit SUK (budowany od nowa przy każdym „uruchom”) + .simrail.json,
                                                .przebiegi_gry.json, .nieprzypisane.txt, dzienniki EDR

Polecenia:
  posterunek lista                         rejestr i stan każdego układu
  posterunek nowy "<posterunek gry>" [KOD] [--uklad <katalog>]
                                           układ z najnowszego zrzutu pulpitu (mod SimRailMost); wpis w rejestrze
  posterunek odswiez <układ>               nowy zrzut z gry obok (<układ>_z_gry_nowy.zob.json) i raport zmian obiektów gry;
                                           poprawionego zobrazowania nie rusza
  posterunek buduj <układ>|--wszystkie     pulpit SUK z zobrazowania (zob_do_suk.py)
  posterunek uruchom <układ>               buduj + pulpit SUK (LcsWidzew --station …)
  posterunek edytuj <układ> [ut]           Edytor zobrazowania (albo Edytor UT)
  posterunek przypiecia <układ>            stan przypięć do obiektów gry (dla edytora, zakładka Gra)

Ścieżki: zmienne SUK_STACJE, SUK_ZRZUTY (UserData\\SimRailMost), SUK_PROGRAMY (paczka: programy\\…), SIMRAIL_DIR;
bez nich – katalog gry z Steam, stacje w <gra>\\UserData\\SimRailMost\\suk\\stacje, programy z repozytorium (bin\\Release).
"""
import datetime, json, os, re, subprocess, sys, unicodedata

sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
NARZ = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(NARZ, "..", ".."))


# ------------------------------------------------------------------ ścieżki
def katalog_gry():
    if os.environ.get("SIMRAIL_DIR"): return os.environ["SIMRAIL_DIR"]
    kand = []
    try:
        import winreg
        for gal, klucz, wart in ((winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath"),
                                 (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")):
            try:
                with winreg.OpenKey(gal, klucz) as k: steam = winreg.QueryValueEx(k, wart)[0]
            except OSError: continue
            kand.append(os.path.join(steam, "steamapps", "common", "SimRail"))
            vdf = os.path.join(steam, "steamapps", "libraryfolders.vdf")
            if os.path.exists(vdf):
                for m in re.finditer(r'"path"\s+"([^"]+)"', open(vdf, encoding="utf-8", errors="replace").read()):
                    kand.append(os.path.join(m.group(1).replace("\\\\", "\\"), "steamapps", "common", "SimRail"))
    except ImportError: pass
    kand.append(r"C:\Program Files (x86)\Steam\steamapps\common\SimRail")
    return next((k for k in kand if os.path.exists(os.path.join(k, "SimRail.exe"))), None)


GRA = katalog_gry()
ZRZUTY = os.environ.get("SUK_ZRZUTY") or (os.path.join(GRA, "UserData", "SimRailMost") if GRA else None)
STACJE = os.environ.get("SUK_STACJE") or (os.path.join(ZRZUTY, "suk", "stacje") if ZRZUTY else None)
REJESTR = os.path.join(STACJE, "posterunki.json") if STACJE else None


def program(nazwa):
    """LcsWidzew / EdytorZobrazowania2 / EdytorUT: z paczki (SUK_PROGRAMY) albo z repozytorium (bin\\Release)."""
    pacz = {"LcsWidzew": ("SUK", "LcsWidzew.exe"), "EdytorZobrazowania2": ("EdytorZobrazowania2", "EdytorZobrazowania2.exe"),
            "EdytorUT": ("EdytorUT", "EdytorUT.exe")}[nazwa]
    repo = {"LcsWidzew": "LcsWidzew", "EdytorZobrazowania2": "Lcs.EdytorZobrazowania2", "EdytorUT": "Lcs.EdytorUT"}[nazwa]
    if os.environ.get("SUK_PROGRAMY"): p = os.path.join(os.environ["SUK_PROGRAMY"], *pacz)
    else: p = os.path.join(REPO, "app", repo, "bin", "Release", "net10.0-windows", pacz[1])
    if not os.path.exists(p): raise SystemExit(f"Brak programu: {p}" + ("" if os.environ.get("SUK_PROGRAMY") else
                                                                      f" – zbuduj: dotnet build -c Release w app\\{repo}"))
    return p


# ------------------------------------------------------------------ rejestr
def klucz(s):
    """Jak SimRailMapa.Klucz w SUK: bez wielkości liter, polskich znaków i odstępów."""
    s = s.replace("ł", "l").replace("Ł", "L")
    s = "".join(c for c in unicodedata.normalize("NFKD", s) if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]", "", s.lower())


def plik_z_nazwy(nazwa):
    """Nazwa katalogu układu dla nowego posterunku: bez polskich znaków, odstępy → „_” (Łódź Widzew → Lodz_Widzew)."""
    s = nazwa.replace("ł", "l").replace("Ł", "L")
    s = "".join(c for c in unicodedata.normalize("NFKD", s) if not unicodedata.combining(c))
    return re.sub(r"[^A-Za-z0-9]+", "_", s).strip("_") or "posterunek"


def wczytaj_rejestr():
    if REJESTR and os.path.exists(REJESTR):
        return json.load(open(REJESTR, encoding="utf-8"))
    return {"opis": "Rejestr posterunków SimRail w SUK (posterunek.py). nazwaGry – jak w grze (pole „stacja” moda); kod – "
                    "prefiks id obiektów SUK (KO:…; zmiana kodu zmienia id odcinków i dzienniki EDR); uklad – katalog układu "
                    "(kilka posterunków z tym samym układem = jeden pulpit SUK); pulpit – „ebi” albo „domino”.",
            "posterunki": []}


def zapisz_rejestr(r):
    os.makedirs(STACJE, exist_ok=True)
    r["posterunki"].sort(key=lambda p: (p.get("uklad", ""), p.get("nazwaGry", "")))
    json.dump(r, open(REJESTR, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def wpis(r, nazwa):
    return next((p for p in r["posterunki"] if klucz(p.get("nazwaGry", "")) == klucz(nazwa)), None)


def uklady(r):
    """Układy z rejestru i katalogi z zobrazowaniem (także spoza rejestru)."""
    u = {p["uklad"] for p in r["posterunki"] if p.get("uklad")}
    if STACJE and os.path.isdir(STACJE):
        u |= {d for d in os.listdir(STACJE) if os.path.exists(os.path.join(STACJE, d, "edytor", d + ".zob.json"))}
    return sorted(u, key=str.lower)


def znajdz_uklad(r, nazwa):
    """Układ po nazwie katalogu albo po nazwie posterunku gry; bez nazwy – pytanie (dwuklik skryptu w paczce)."""
    if not nazwa:
        u = uklady(r)
        if not u: raise SystemExit("Nie ma jeszcze żadnej stacji – najpierw posterunek nowy (nowa_stacja.cmd).")
        print("Twoje stacje:\n   " + "\n   ".join(u))
        nazwa = input("\nWpisz nazwę stacji z listy i naciśnij Enter: ").strip()
        if not nazwa: raise SystemExit(1)
    for u in uklady(r):
        if u.lower() == nazwa.lower() or klucz(u) == klucz(nazwa): return u
    p = wpis(r, nazwa)
    if p: return p["uklad"]
    raise SystemExit(f"Nie ma układu „{nazwa}”. Układy: {', '.join(uklady(r)) or '(brak)'} – nowy: posterunek nowy \"<posterunek gry>\"")


# ------------------------------------------------------------------ zrzuty pulpitu z gry
def zrzuty(nazwa=None):
    """Zrzuty pulpitu EBI (pulpit_<posterunek>_<ekran>_<data>.json z moda), najnowszy pierwszy: [(plik, posterunek)]."""
    if not ZRZUTY or not os.path.isdir(ZRZUTY): return []
    w = []
    for f in os.listdir(ZRZUTY):
        if not re.match(r"^pulpit_.+_\d{8}_\d{6}\.json$", f): continue
        try:
            with open(os.path.join(ZRZUTY, f), encoding="utf-8") as h: post = json.load(h).get("posterunek") or ""
        except (OSError, ValueError): continue
        if nazwa is None or klucz(post) == klucz(nazwa): w.append((f, post))
    return sorted(w, key=lambda x: x[0][-20:], reverse=True)   # nazwa kończy się datą_godziną


def kod_ze_zrzutu(plik):
    d = json.load(open(os.path.join(ZRZUTY, plik), encoding="utf-8"))
    return next((e["pola"].get("prefix", "").upper() for e in d.get("elementy", []) if e.get("klasa") == "EBI300ScreenStatus"), "")


# ------------------------------------------------------------------ pliki układu
def sciezki(u):
    k = os.path.join(STACJE, u); ed = os.path.join(k, "edytor")
    return {"kat": k, "ed": ed, "zob": os.path.join(ed, u + ".zob.json"), "mapa": os.path.join(ed, u + ".simrail.json"),
            "ut": os.path.join(ed, u + ".ut.json"), "pulpit": os.path.join(k, u + "_zob.json"),
            "nieprzyp": os.path.join(k, u + "_zob.nieprzypisane.txt"), "przyp": os.path.join(ed, u + ".przypiecia_gry.json")}


def py(skrypt, *args):
    r = subprocess.run([sys.executable, "-I", os.path.join(NARZ, skrypt), *args])
    if r.returncode: raise SystemExit(f"Błąd: {skrypt} (kod {r.returncode})")


def pusty_ut(sciezka, posterunki):
    """Pusty UT z wpisami posterunków (kod, nazwa) – edytor zobrazowania wie, czyj to obraz."""
    ut = {"version": 1, "name": posterunki[0]["nazwaGry"], "crs": "EPSG:2180", "drawingScale": 2000,
          "notes": f"Pusty projekt UT ({', '.join(p['nazwaGry'] for p in posterunki)}) do zobrazowania z pulpitu SimRail – geometrię "
                   "torów (PUWG 1992) zbuduj w Edytorze UT, potem „Przypnij do UT” w edytorze zobrazowania.",
          "backgrounds": [], "tracks": [], "turnouts": [], "crossings": [], "devices": [], "sections": [], "platforms": [],
          "levelCrossings": [], "kmPoints": [], "lines": [],
          "posts": [{"id": klucz(p["nazwaGry"])[:8] or p["kod"].lower(), "kod": p["kod"], "nazwa": p["nazwaGry"]} for p in posterunki]}
    json.dump(ut, open(sciezka, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def uzupelnij_posterunki(s, posterunki):
    """UT: wpisy posterunków z rejestru (dopisuje brakujące); zobrazowanie: obrazy bez posterunków dostają posterunki UT."""
    if os.path.exists(s["ut"]):
        ut = json.load(open(s["ut"], encoding="utf-8"))
        posts = ut.setdefault("posts", []); zm = False
        for p in posterunki:
            if not any(klucz(x.get("nazwa", "")) == klucz(p["nazwaGry"]) for x in posts):
                posts.append({"id": klucz(p["nazwaGry"])[:8] or p["kod"].lower(), "kod": p["kod"], "nazwa": p["nazwaGry"]}); zm = True
        if zm: json.dump(ut, open(s["ut"], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        ids = [x["id"] for x in posts if any(klucz(x.get("nazwa", "")) == klucz(p["nazwaGry"]) for p in posterunki)]
    else: ids = []
    if ids and os.path.exists(s["zob"]):
        zob = json.load(open(s["zob"], encoding="utf-8")); zm = False
        for o in zob.get("obrazy", []):
            if not o.get("posterunki"): o["posterunki"] = list(ids); zm = True
        if zm: json.dump(zob, open(s["zob"], "w", encoding="utf-8"), ensure_ascii=False, indent=1)


# ------------------------------------------------------------------ polecenia
def lista(_):
    r = wczytaj_rejestr()
    print(f"Stacje:  {STACJE}\nZrzuty:  {ZRZUTY}\nRejestr: {REJESTR}" + ("" if os.path.exists(REJESTR or "") else " (brak)"))
    print()
    for u in uklady(r):
        s = sciezki(u); posty = [p for p in r["posterunki"] if p.get("uklad") == u]
        opis = ", ".join(f"{p['nazwaGry']} ({p.get('kod', '?')})" for p in posty) or "(spoza rejestru)"
        st = []
        if not os.path.exists(s["zob"]): st.append("brak zobrazowania")
        if not os.path.exists(s["pulpit"]): st.append("pulpit SUK niezbudowany")
        elif os.path.getmtime(s["pulpit"]) < os.path.getmtime(s["zob"]): st.append("pulpit SUK starszy niż zobrazowanie")
        if os.path.exists(s["nieprzyp"]):
            n = max(0, sum(1 for _ in open(s["nieprzyp"], encoding="utf-8")) - 1)
            st.append(f"nieprzypisane {n}" if n else "wszystko przypięte")
        z = [zr for p in posty for zr in zrzuty(p["nazwaGry"])[:1]]
        if z: st.append("zrzut z gry " + max(f for f, _ in z)[-20:-5].replace("_", " "))
        print(f"  {u:<14} {opis}\n  {'':<14} {' · '.join(st)}")
    poza = sorted({post for _, post in zrzuty()} - {p["nazwaGry"] for p in r["posterunki"]}, key=klucz)
    if poza: print(f"\nZrzuty pulpitu bez układu (posterunek nowy \"…\"): {', '.join(poza)}")


def nowy(a):
    if not a.nazwa:
        z = sorted({post for _, post in zrzuty()}, key=klucz)
        if not z: raise SystemExit(f"Brak zrzutów pulpitu w {ZRZUTY} – wejdź w grze na posterunek z pulpitem EBI jako dyżurny i odczekaj 10 s.")
        print("Posterunki, których pulpit zapisał mod:\n   " + "\n   ".join(z))
        a.nazwa = input("\nWpisz nazwę posterunku z listy i naciśnij Enter: ").strip()
        if not a.nazwa: return
    z = zrzuty(a.nazwa)
    if not z: raise SystemExit(f"Brak zrzutu pulpitu „{a.nazwa}” w {ZRZUTY} – nazwa jak na liście (posterunek nowy bez nazwy).")
    plik, post = z[0]
    print(f"Zrzut pulpitu: {plik}")
    r = wczytaj_rejestr(); w = wpis(r, post)
    if w:
        if a.kod and a.kod.upper() != w["kod"]:
            raise SystemExit(f"„{post}” ma w rejestrze kod {w['kod']} – kodu nie zmienia się (od niego zależą id odcinków i dzienniki EDR). "
                             f"Zmiana tylko ręcznie w {REJESTR}.")
        if a.uklad and a.uklad != w["uklad"]: raise SystemExit(f"„{post}” jest już w układzie {w['uklad']}.")
    else:
        kod = (a.kod or kod_ze_zrzutu(plik) or plik_z_nazwy(post)[:2]).upper()
        # katalog sprzed rejestru (nowa_stacja.cmd do 0.1.3: „Łódź_Widzew”) – ten sam układ, nie nowy
        dawny = next((d for d in uklady(r) if klucz(d) == klucz(post)), None)
        w = {"nazwaGry": post, "kod": kod, "uklad": a.uklad or dawny or plik_z_nazwy(post), "pulpit": "ebi"}
        if dawny and not a.kod and os.path.exists(sciezki(dawny)["mapa"]):
            w["kod"] = kod = (json.load(open(sciezki(dawny)["mapa"], encoding="utf-8")).get("kod") or kod).upper()
        inny = next((p for p in r["posterunki"] if p["kod"] == kod and p["uklad"] != w["uklad"]), None)
        if inny: print(f"Uwaga: kod {kod} ma już {inny['nazwaGry']} (układ {inny['uklad']}) – podaj inny: posterunek nowy \"{post}\" <KOD>")
        r["posterunki"].append(w); zapisz_rejestr(r)
        print(f"Rejestr: {post} → kod {kod}, układ {w['uklad']}")
    u = w["uklad"]; s = sciezki(u); posty = [p for p in r["posterunki"] if p["uklad"] == u]
    os.makedirs(s["ed"], exist_ok=True)
    if not os.path.exists(s["ut"]): pusty_ut(s["ut"], posty)
    if os.path.exists(s["zob"]):
        nowy_zob = os.path.join(s["ed"], u + "_z_gry_nowy.zob.json")
        py("pulpit_do_zob.py", os.path.join(ZRZUTY, plik), nowy_zob, u + ".ut.json")
        print(f"Zobrazowanie już jest – nowa wersja z gry obok: {os.path.basename(nowy_zob)} (Twoich poprawek nie ruszam)")
    else:
        py("pulpit_do_zob.py", os.path.join(ZRZUTY, plik), s["zob"], u + ".ut.json")
    uzupelnij_posterunki(s, posty)
    buduj_uklad(u)
    print(f"\nGotowe: {s['kat']}\nDalej: posterunek edytuj {u}   albo   posterunek uruchom {u}")
    print("Rysunek pochodzi z gry SimRail – tylko do własnego użytku, nie rozpowszechniaj plików stacji.")


def odswiez(a):
    r = wczytaj_rejestr(); u = znajdz_uklad(r, a.uklad); s = sciezki(u)
    posty = [p for p in r["posterunki"] if p["uklad"] == u] or [{"nazwaGry": u}]
    z = [zr for p in posty for zr in zrzuty(p["nazwaGry"])[:1]]
    if not z: raise SystemExit(f"Brak zrzutu pulpitu dla {', '.join(p['nazwaGry'] for p in posty)} w {ZRZUTY}.")
    plik = max(z)[0]; print(f"Zrzut pulpitu: {plik}")
    nowy_zob = os.path.join(s["ed"], u + "_z_gry_nowy.zob.json")
    py("pulpit_do_zob.py", os.path.join(ZRZUTY, plik), nowy_zob, u + ".ut.json")
    # porównanie obiektów gry: mapa układu ↔ mapa z nowego zrzutu (po railNetworkName)
    def obiekty(mp):
        if not os.path.exists(mp): return set()
        m = json.load(open(mp, encoding="utf-8"))
        return {g for o in m.get("obrazy", {}).values() for d in ("elementy", "odcinki") for g in o.get(d, {}).values() if g and g != "-"}
    stare, nowe = obiekty(s["mapa"]), obiekty(nowy_zob[:-len(".zob.json")] + ".simrail.json")
    dodane, usuniete = sorted(nowe - stare), sorted(stare - nowe)
    raport = os.path.join(s["kat"], u + "_zmiany_z_gry.txt")
    with open(raport, "w", encoding="utf-8") as f:
        f.write(f"Zmiany obiektów gry: {os.path.basename(s['mapa'])} ↔ zrzut {plik} ({datetime.datetime.now():%Y-%m-%d %H:%M})\n")
        f.write(f"\nNowe w grze ({len(dodane)}) – dorysuj w edytorze i przypnij (zakładka Gra):\n" + "".join(f"  {g}\n" for g in dodane))
        f.write(f"\nNie ma ich już w grze ({len(usuniete)}) – sprawdź elementy w edytorze:\n" + "".join(f"  {g}\n" for g in usuniete))
    print(f"Obiekty gry: nowe {len(dodane)}, usunięte {len(usuniete)} – {raport}")
    print(f"Twoje zobrazowanie bez zmian; rysunek z gry do porównania: {os.path.basename(nowy_zob)}")


def buduj_uklad(u, tylko_przyp=False):
    s = sciezki(u)
    if not os.path.exists(s["zob"]): raise SystemExit(f"Brak zobrazowania {s['zob']} – najpierw posterunek nowy \"<posterunek gry>\"")
    posty = [p for p in wczytaj_rejestr()["posterunki"] if p.get("uklad") == u]
    if posty: uzupelnij_posterunki(s, posty)      # UT i obrazy wiedzą, czyje są (tylko gdy brak)
    arg = ["--rejestr", REJESTR] if os.path.exists(REJESTR) else []
    if ZRZUTY and os.path.isdir(ZRZUTY): arg += ["--zrzuty", ZRZUTY]
    if tylko_przyp: py("zob_do_suk.py", s["zob"], "--tylko-przypiecia", *arg)
    else: py("zob_do_suk.py", s["zob"], s["pulpit"], *arg)


def buduj(a):
    r = wczytaj_rejestr()
    if not a.uklad and not a.wszystkie: raise SystemExit("posterunek buduj <układ>   albo   posterunek buduj --wszystkie")
    for u in (uklady(r) if a.wszystkie else [znajdz_uklad(r, a.uklad)]):
        print(f"== {u}"); buduj_uklad(u)


def uruchom(a):
    r = wczytaj_rejestr(); u = znajdz_uklad(r, a.uklad); buduj_uklad(u)
    # --simrail: pulpit od razu jako pulpit zdalny SimRail (bez gry jednoosobowej na własnym komputerze zależności)
    subprocess.Popen([program("LcsWidzew"), "--station", sciezki(u)["pulpit"], "--simrail"], close_fds=True)


def edytuj(a):
    r = wczytaj_rejestr(); u = znajdz_uklad(r, a.uklad); s = sciezki(u)
    if a.co == "ut": subprocess.Popen([program("EdytorUT"), s["ut"]], close_fds=True)
    else:
        buduj_uklad(u, tylko_przyp=True)        # stan przypięć do zakładki Gra
        subprocess.Popen([program("EdytorZobrazowania2"), s["zob"]], close_fds=True)


def przypiecia(a):
    r = wczytaj_rejestr(); buduj_uklad(znajdz_uklad(r, a.uklad), tylko_przyp=True)


def main():
    import argparse
    ap = argparse.ArgumentParser(prog="posterunek", description="Posterunki SimRail w SUK (tools/simrail/posterunek.py)")
    sp = ap.add_subparsers(dest="pol")
    sp.add_parser("lista").set_defaults(f=lista)
    p = sp.add_parser("nowy"); p.add_argument("nazwa", nargs="?"); p.add_argument("kod", nargs="?"); p.add_argument("--uklad"); p.set_defaults(f=nowy)
    for n, f in (("odswiez", odswiez), ("uruchom", uruchom), ("przypiecia", przypiecia)):
        p = sp.add_parser(n); p.add_argument("uklad", nargs="?"); p.set_defaults(f=f)
    p = sp.add_parser("buduj"); p.add_argument("uklad", nargs="?"); p.add_argument("--wszystkie", action="store_true"); p.set_defaults(f=buduj)
    p = sp.add_parser("edytuj"); p.add_argument("uklad", nargs="?"); p.add_argument("co", nargs="?", choices=["zob", "ut"], default="zob"); p.set_defaults(f=edytuj)
    a = ap.parse_args(sys.argv[1:] if len(sys.argv) > 1 else ["lista"])
    if not STACJE: raise SystemExit("Nie znaleziono katalogu SimRail. Ustaw SIMRAIL_DIR (albo SUK_STACJE i SUK_ZRZUTY).")
    a.f(a)


if __name__ == "__main__":
    main()
