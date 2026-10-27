#!/usr/bin/env python3
"""Sprawdzenie kontraktów SUK (S00.03).

Uruchomienie z katalogu repo:  python3 kontrakty/testy/sprawdz_kontrakty.py [--wynik plik.txt] [--jsonl ścieżka ...] [--mapa plik.json ...] [--wejscia katalog ...]
--jsonl (S01.01): dodatkowo sprawdza pliki JSONL zapisane przez programy (plik albo katalog – rekurencyjnie *.jsonl):
każda linia = jeden obiekt JSON zgodny ze schematem v1 i katalogiem zdarzeń, ≤ 8 KiB, bez znacznika testowego sekretu
„SEKRET_TESTOWY”, recordSeq bez powtórzeń w obrębie processRunId.
--mapa (S02A.01): dodatkowo sprawdza mapy własności KZ zapisane przez program (Lcs.Core.TestHost --mapa-wlasnosci):
schemat kz/mapa_wlasnosci_v1, reguła kzId, spójność odwołań, liczników i kanonicznej kolejności.
--wejscia (S02A.02): dodatkowo sprawdza katalogi zapisane przez Lcs.Core.TestHost --wejscia-cyklu: tabela rozsyłania
i początki strumieni wejść KZ (schemat kz/wejscie_cyklu_v1, postać kanoniczna bajt w bajt, skrót tabeli, numeracja bez luk,
odbiorcy zgodni z tabelą, porządek kanoniczny, raport.json zgodne=true). Zawsze: wektory postaci kanonicznej
kontrakty/testy/wejscie_cyklu_kanon.json (bajty i SHA-256 jak w C#, ciągi wg RFC 8785).
Wymaga: Python >= 3.8, pakiet jsonschema >= 3.2 (walidator Draft-07). Skrypt tylko czyta pliki repo;
nie łączy się z siecią i niczego nie zapisuje poza opcjonalnym plikiem --wynik.
Kod wyjścia 0 = brak błędów, 1 = co najmniej jeden błąd, 2 = brak wymaganego pakietu.
"""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

try:
    import jsonschema
    from jsonschema import Draft7Validator
except ImportError:  # pragma: no cover
    print("BŁĄD: brak pakietu jsonschema (pip install jsonschema)")
    sys.exit(2)

REPO = Path(__file__).resolve().parents[2]
K = REPO / "kontrakty"
wyniki = []
bledy = 0


def ok(msg):
    wyniki.append("OK    " + msg)


def blad(msg):
    global bledy
    bledy += 1
    wyniki.append("BŁĄD  " + msg)


def wczytaj(p):
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def sha256_lf(p):
    return hashlib.sha256(Path(p).read_bytes().replace(b"\r\n", b"\n")).hexdigest()


# ------------------------------------------------------------------ 1. poprawność JSON
for p in sorted(K.rglob("*.json")):
    try:
        wczytaj(p)
        ok(f"JSON poprawny: {p.relative_to(REPO).as_posix()}")
    except Exception as e:  # noqa: BLE001
        blad(f"JSON niepoprawny: {p.relative_to(REPO).as_posix()}: {e}")

schema = wczytaj(K / "logi" / "rekord_logu_v1.schema.json")
katalog = wczytaj(K / "logi" / "katalog_zdarzen_v1.json")
budzety = wczytaj(K / "logi" / "budzety_logow_v1.json")
stos = wczytaj(K / "stos" / "wersje_stosu.json")
kontrakty = wczytaj(K / "katalog_kontraktow.json")

# ------------------------------------------------------------------ 2. schemat rekordu
try:
    Draft7Validator.check_schema(schema)
    ok("schemat rekord_logu_v1 zgodny z metaschematem Draft-07")
except jsonschema.exceptions.SchemaError as e:
    blad(f"schemat rekord_logu_v1 niezgodny z Draft-07: {e.message}")
walidator = Draft7Validator(schema)
LIMIT = schema["x-suk-limity"]["maxRecordBytesUtf8"]

# ------------------------------------------------------------------ 3. katalog zdarzeń
PREF = re.compile(r"^[A-Z][A-Z0-9]{1,15}$")
podzakresy = []
poprzedni_koniec = 999
for o in katalog["obszary"]:
    if o["od"] != poprzedni_koniec + 1:
        blad(f"katalog: obszar {o['od']}-{o['do']} nie przylega do poprzedniego ({poprzedni_koniec})")
    poprzedni_koniec = o["do"]
    pk = o["od"] - 1
    for s in o["podzakresy"]:
        if s["od"] != pk + 1 or s["do"] < s["od"] or s["do"] > o["do"]:
            blad(f"katalog: podzakres {s['od']}-{s['do']} ({s['prefiks']}) poza kolejnością lub obszarem")
        if not PREF.match(s["prefiks"]):
            blad(f"katalog: zły prefiks {s['prefiks']}")
        pk = s["do"]
        podzakresy.append(s)
    if pk != o["do"]:
        blad(f"katalog: podzakresy obszaru {o['od']}-{o['do']} nie pokrywają go w całości")
if poprzedni_koniec != 9999:
    blad("katalog: obszary nie kończą się na 9999")
else:
    ok(f"katalog zdarzeń: {len(katalog['obszary'])} obszarów, {len(podzakresy)} podzakresów, ciągłe 1000-9999")


def podzakres(event_id):
    for s in podzakresy:
        if s["od"] <= event_id <= s["do"]:
            return s
    return None


kody_id = {}
kody_code = {}
for c in katalog["kody"]:
    s = podzakres(c["eventId"])
    pref = c["eventCode"].split(".")[0]
    if c["eventId"] in kody_id or c["eventCode"] in kody_code:
        blad(f"katalog: powtórzony kod {c['eventId']} {c['eventCode']}")
    kody_id[c["eventId"]] = c
    kody_code[c["eventCode"]] = c
    if s is None or s["prefiks"] != pref or s["prefiks"] == "REZERWA":
        blad(f"katalog: {c['eventCode']} ({c['eventId']}) poza podzakresem z prefiksem {pref}")
    elif c["severity"] not in schema["properties"]["severity"]["enum"]:
        blad(f"katalog: {c['eventCode']} ma nieznany poziom {c['severity']}")
    else:
        ok(f"katalog: kod {c['eventId']} {c['eventCode']} w podzakresie {s['od']}-{s['do']}")


def sprawdz_katalog(r):
    """Zwraca opis błędu zgodności rekordu z katalogiem albo None."""
    eid, code = r.get("eventId"), r.get("eventCode")
    if not isinstance(eid, int) or not isinstance(code, str):
        return "brak eventId/eventCode"
    s = podzakres(eid)
    pref = code.split(".")[0]
    if s is None or s["prefiks"] == "REZERWA":
        return f"eventId {eid} w rezerwie/poza katalogiem"
    if s["prefiks"] != pref:
        return f"prefiks {pref} niezgodny z podzakresem {s['od']}-{s['do']} ({s['prefiks']})"
    if code in kody_code and kody_code[code]["eventId"] != eid:
        return f"{code} ma w katalogu eventId {kody_code[code]['eventId']}, nie {eid}"
    if eid in kody_id and kody_id[eid]["eventCode"] != code:
        return f"eventId {eid} należy w katalogu do {kody_id[eid]['eventCode']}"
    return None


def ocen(r, linia=None):
    powody = {}
    bl = sorted(walidator.iter_errors(r), key=lambda e: list(e.path))
    if bl:
        powody["schemat"] = "; ".join(e.message[:120] for e in bl[:3])
    if not bl:
        k = sprawdz_katalog(r)
        if k:
            powody["katalog"] = k
    tekst = linia if linia is not None else json.dumps(r, ensure_ascii=False, separators=(",", ":"))
    n = len(tekst.encode("utf-8"))
    if n > LIMIT:
        powody["rozmiar"] = f"{n} B > {LIMIT} B"
    return powody


# ------------------------------------------------------------------ 4. rekordy poprawne (JSONL)
plik = K / "testy" / "rekordy_poprawne.jsonl"
for i, linia in enumerate(plik.read_text(encoding="utf-8").split("\n"), 1):
    if not linia:
        continue
    try:
        r = json.loads(linia)
    except json.JSONDecodeError as e:
        blad(f"rekordy_poprawne.jsonl:{i}: nie jest jedną linią JSON: {e}")
        continue
    if not isinstance(r, dict):
        blad(f"rekordy_poprawne.jsonl:{i}: nie jest obiektem")
        continue
    p = ocen(r, linia)
    if p:
        blad(f"rekordy_poprawne.jsonl:{i} {r.get('eventCode')}: odrzucony: {p}")
    else:
        ok(f"rekordy_poprawne.jsonl:{i} {r.get('eventCode')}: przyjęty ({len(linia.encode('utf-8'))} B)")

# ------------------------------------------------------------------ 5. przypadki błędne
for i, c in enumerate(wczytaj(K / "testy" / "przypadki_bledne.json")["przypadki"], 1):
    p = ocen(c["rekord"])
    oczek = c["oczekiwane"]
    if oczek in p and (oczek == "schemat" or "schemat" not in p):
        ok(f"błędny #{i} „{c['opis']}”: odrzucony ({oczek}: {p[oczek][:90]})")
    elif not p:
        blad(f"błędny #{i} „{c['opis']}”: PRZYJĘTY, oczekiwano odrzucenia ({oczek})")
    else:
        blad(f"błędny #{i} „{c['opis']}”: odrzucony z innego powodu {sorted(p)}, oczekiwano {oczek}")

# ------------------------------------------------------------------ 5a. pliki JSONL z programów (S01.01, opcja --jsonl)
if "--jsonl" in sys.argv:
    i = sys.argv.index("--jsonl") + 1
    sciezki = []
    while i < len(sys.argv) and not sys.argv[i].startswith("--"):
        sciezki.append(Path(sys.argv[i]))
        i += 1
    pliki = []
    for sc in sciezki:
        if sc.is_dir():
            pliki += sorted(sc.rglob("*.jsonl"))
        elif sc.is_file():
            pliki.append(sc)
        else:
            blad(f"jsonl: brak ścieżki {sc}")
    razem = 0
    for p in pliki:
        dane = p.read_bytes()
        nazwa = p.as_posix()
        if b"SEKRET_TESTOWY" in dane:
            blad(f"jsonl: {nazwa}: zawiera znacznik testowego sekretu SEKRET_TESTOWY")
        n = bl = 0
        seq = {}
        for nr, linia in enumerate(dane.decode("utf-8").split("\n"), 1):
            linia = linia[:-1] if linia.endswith("\r") else linia
            if not linia:
                continue
            n += 1
            try:
                r = json.loads(linia)
            except json.JSONDecodeError as e:
                bl += 1
                if bl <= 5:
                    blad(f"jsonl: {nazwa}:{nr}: nie jest jedną linią JSON: {e}")
                continue
            if not isinstance(r, dict):
                bl += 1
                if bl <= 5:
                    blad(f"jsonl: {nazwa}:{nr}: nie jest obiektem")
                continue
            pw = ocen(r, linia)
            klucz = (r.get("processRunId"), r.get("recordSeq"))
            if klucz in seq:
                pw["recordSeq"] = f"powtórzony recordSeq {klucz[1]} (linie {seq[klucz]} i {nr})"
            seq[klucz] = nr
            if pw:
                bl += 1
                if bl <= 5:
                    blad(f"jsonl: {nazwa}:{nr} {r.get('eventCode')}: odrzucony: {pw}")
        if bl > 5:
            blad(f"jsonl: {nazwa}: łącznie {bl} błędnych rekordów (pokazano 5)")
        if bl == 0:
            ok(f"jsonl: {nazwa}: {n} rekordów zgodnych ze schematem v1 i katalogiem")
        razem += n
    ok(f"jsonl: {len(pliki)} plików, {razem} rekordów")

# ------------------------------------------------------------------ 6a. mapa własności KZ (S02A.01)
schemat_mapy = wczytaj(K / "kz" / "mapa_wlasnosci_v1.schema.json")
try:
    Draft7Validator.check_schema(schemat_mapy)
    ok("schemat kz/mapa_wlasnosci_v1 zgodny z metaschematem Draft-07")
except jsonschema.exceptions.SchemaError as e:
    blad(f"schemat kz/mapa_wlasnosci_v1 niezgodny z Draft-07: {e.message}")
walidator_mapy = Draft7Validator(schemat_mapy)


def oceń_mape(m):
    """Błędy mapy: schemat + reguły krzyżowe, których JSON Schema nie wyraża (lista komunikatów, pusta = poprawna)."""
    e = [x.message[:160] for x in list(walidator_mapy.iter_errors(m))[:5]]
    if e:
        return e
    kz = {}
    for p in m["posterunki"]:
        if p["kzId"] != "KZ-" + p["postId"]:
            e.append(f"posterunek {p['postId']}: kzId {p['kzId']} niezgodny z regułą KZ-{{postId}}")
        if p["postId"] in kz:
            e.append(f"posterunek {p['postId']} powtórzony")
        kz[p["postId"]] = p["kzId"]
    wszystkie = set(kz.values())
    def zgodny(w, opis):
        if w["kzId"] is not None and (w["postId"] not in kz or kz[w["postId"]] != w["kzId"]):
            e.append(f"{opis}: kzId {w['kzId']} nie wynika z posterunku {w['postId']}")
    for o in m["obiekty"]:
        zgodny(o, f"obiekt {o['rodzaj']} {o['id']}")
        if o["kzId"] is not None and o["kzId"] not in o["odbiorcy"]:
            e.append(f"obiekt {o['rodzaj']} {o['id']}: właściciel nie jest odbiorcą")
        if not set(o["odbiorcy"]) <= wszystkie or not set(o["kandydaci"]) <= set(kz):
            e.append(f"obiekt {o['rodzaj']} {o['id']}: odbiorca/kandydat spoza posterunków")
    for k in m["konceBlokad"]:
        zgodny(k, f"koniec {k['blokadaId']}#{k['indeks']}")
    wlasciciel = {(o["rodzaj"], o["id"]): o["kzId"] for o in m["obiekty"]}
    for g in m["granice"]:
        if g["zKz"] == g["doKz"] or g["zKz"] not in wszystkie or g["doKz"] not in wszystkie:
            e.append(f"granica {g['rodzaj']} {g['obiektId']}: zKz/doKz niepoprawne")
        if g["obiektRodzaj"] != "blokada" and wlasciciel.get((g["obiektRodzaj"], g["obiektId"])) != g["doKz"]:
            e.append(f"granica {g['rodzaj']} {g['obiektId']}: doKz nie jest właścicielem obiektu")
        if g["liczbaOdwolan"] < len(g["konteksty"]):
            e.append(f"granica {g['rodzaj']} {g['obiektId']}: mniej odwołań niż kontekstów")
    s = m["podsumowanie"]
    oczek = {"posterunki": len(m["posterunki"]), "obiekty": len(m["obiekty"]), "konceBlokad": len(m["konceBlokad"]), "szlaki": len(m["szlaki"]),
             "granice": len(m["granice"]), "problemy": len(m["problemy"]),
             "przypisane": sum(o["status"] == "PRZYPISANY" for o in m["obiekty"]),
             "niejednoznaczne": sum(o["status"] == "NIEJEDNOZNACZNY" for o in m["obiekty"]),
             "nieznane": sum(o["status"] == "NIEZNANY" for o in m["obiekty"])}
    for k, v in oczek.items():
        if s[k] != v:
            e.append(f"podsumowanie.{k} = {s[k]}, a jest {v}")
    def u16(t):
        return t.encode("utf-16-be")   # porządek ordinal .NET (jednostki UTF-16)
    for nazwa, klucz in [("posterunki", lambda x: u16(x["postId"])), ("obiekty", lambda x: (u16(x["rodzaj"]), u16(x["id"]))),
                         ("konceBlokad", lambda x: (u16(x["blokadaId"]), x["indeks"])), ("szlaki", lambda x: u16(x["blokadaId"])),
                         ("granice", lambda x: tuple(u16(x[k]) for k in ("rodzaj", "zKz", "doKz", "obiektRodzaj", "obiektId"))),
                         ("problemy", lambda x: (u16(x["kod"]), u16(x["rodzaj"]), u16(x["id"])))]:
        if [klucz(x) for x in m[nazwa]] != sorted(klucz(x) for x in m[nazwa]):
            e.append(f"{nazwa}: kolejność niekanoniczna")
    return e


przyklad = wczytaj(K / "testy" / "mapa_wlasnosci_przyklad.json")
pw = oceń_mape(przyklad)
if pw:
    blad(f"mapa_wlasnosci_przyklad.json: odrzucony: {pw}")
else:
    ok("mapa_wlasnosci_przyklad.json: zgodny ze schematem i regułami krzyżowymi")
import copy as _copy
def _mutacja(opis, f):
    m = _copy.deepcopy(przyklad)
    f(m)
    if oceń_mape(m):
        ok(f"mapa – przypadek błędny odrzucony: {opis}")
    else:
        blad(f"mapa – przypadek błędny PRZYJĘTY: {opis}")
_mutacja("PRZYPISANY bez kzId", lambda m: m["obiekty"][1].update(kzId=None))
_mutacja("NIEJEDNOZNACZNY z kzId (domyślny właściciel)", lambda m: m["obiekty"][0].update(kzId="KZ-A", postId="A"))
_mutacja("kzId niezgodny z regułą posterunku", lambda m: m["posterunki"][0].update(kzId="KZ-B"))
_mutacja("obiekt z kzId innego posterunku", lambda m: m["obiekty"][1].update(kzId="KZ-B"))
_mutacja("granica do samego siebie", lambda m: m["granice"][2].update(doKz="KZ-A"))
_mutacja("granica do nie-właściciela obiektu", lambda m: m["granice"][2].update(obiektId="A:W"))
_mutacja("nieznane pole", lambda m: m.update(dodatkowe=1))
_mutacja("niezgodne podsumowanie", lambda m: m["podsumowanie"].update(problemy=0))
_mutacja("kolejność niekanoniczna", lambda m: m["obiekty"].reverse())
_mutacja("identyfikator posterunku ze spacją", lambda m: m["posterunki"][0].update(postId="A B", kzId="KZ-A B"))
if "--mapa" in sys.argv:
    i = sys.argv.index("--mapa") + 1
    while i < len(sys.argv) and not sys.argv[i].startswith("--"):
        sc = Path(sys.argv[i])
        i += 1
        try:
            pw = oceń_mape(wczytaj(sc))
        except Exception as ex:  # noqa: BLE001
            pw = [f"nie można wczytać: {ex}"]
        if pw:
            blad(f"mapa: {sc.as_posix()}: odrzucona: {pw}")
        else:
            ok(f"mapa: {sc.as_posix()}: zgodna ze schematem kz/mapa_wlasnosci_v1 i regułami krzyżowymi")

# ------------------------------------------------------------------ 5b. wejścia cyklu KZ (S02A.02)
schemat_wejsc = wczytaj(K / "kz" / "wejscie_cyklu_v1.schema.json")
try:
    Draft7Validator.check_schema(schemat_wejsc)
    ok("schemat kz/wejscie_cyklu_v1 zgodny z metaschematem Draft-07")
except jsonschema.exceptions.SchemaError as e:
    blad(f"schemat kz/wejscie_cyklu_v1 niezgodny z Draft-07: {e.message}")
_defs = dict(schemat_wejsc, oneOf=None)
walidator_wejscia = Draft7Validator({"$ref": "#/definitions/wejscie", "definitions": schemat_wejsc["definitions"]})
walidator_trasy = Draft7Validator({"$ref": "#/definitions/trasa", "definitions": schemat_wejsc["definitions"]})


def kanon_trasy(t):
    """Postać kanoniczna tabeli: JSON z wcięciem 2, LF, minimalne ucieczki, znak nowej linii na końcu (bajty do SHA-256)."""
    return (json.dumps(t, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def kanon_wejscia(w):
    """Postać kanoniczna wejścia: jedna linia, separatory bez spacji, ciągi wg RFC 8785 (jak KanonJson w C#)."""
    return json.dumps(w, ensure_ascii=False, separators=(",", ":"))


def samotny_surogat(t):
    """Identyfikator z samotnym surogatem UTF-16 nie ma postaci UTF-8 – kontrakt go odrzuca."""
    return isinstance(t, str) and any(0xD800 <= ord(c) <= 0xDFFF for c in t)


def oceń_wejscia(trasa, wejscia, skrot_trasy=None, pelne=True):
    """Błędy tabeli i strumieni wejść (lista komunikatów, pusta = poprawne). wejscia: lista dokumentów (dowolne KZ, w kolejności)."""
    e = [f"trasa: {x.message[:160]}" for x in list(walidator_trasy.iter_errors(trasa))[:5]]
    if e:
        return e
    def u16(t):
        return t.encode("utf-16-be")
    klucze = [(u16(w["rodzaj"]), u16(w["id"])) for w in trasa["wpisy"]]
    if klucze != sorted(klucze) or len(set(klucze)) != len(klucze):
        e.append("trasa: wpisy w kolejności niekanonicznej albo powtórzone")
    if [u16(k) for k in trasa["kz"]] != sorted(u16(k) for k in trasa["kz"]):
        e.append("trasa: kz w kolejności niekanonicznej")
    kz = set(trasa["kz"])
    poz = {}
    for i, w in enumerate(trasa["wpisy"]):
        poz[(w["rodzaj"], w["id"])] = (i, set(w["odbiorcy"]))
        if samotny_surogat(w["id"]):
            e.append(f"trasa: {w['rodzaj']}: identyfikator z samotnym surogatem")
        if not set(w["odbiorcy"]) <= kz:
            e.append(f"trasa: {w['rodzaj']} {w['id']}: odbiorca spoza kz")
        if [u16(k) for k in w["odbiorcy"]] != sorted(u16(k) for k in w["odbiorcy"]):
            e.append(f"trasa: {w['rodzaj']} {w['id']}: odbiorcy w kolejności niekanonicznej")
        if w["powod"] == "NIEZNANY_WSZYSTKIE_KZ" and set(w["odbiorcy"]) != kz:
            e.append(f"trasa: {w['rodzaj']} {w['id']}: NIEZNANY_WSZYSTKIE_KZ bez wszystkich KZ")
    sk = skrot_trasy or hashlib.sha256(kanon_trasy(trasa)).hexdigest()
    ostatni = {}
    for w in wejscia:
        we = [x.message[:160] for x in list(walidator_wejscia.iter_errors(w))[:3]]
        if we:
            e.append(f"wejście {w.get('kzId')}#{w.get('nrWejscia')}: {we}")
            continue
        opis = f"wejście {w['kzId']}#{w['nrWejscia']}"
        if w["kzId"] not in kz:
            e.append(f"{opis}: kzId spoza tabeli")
        if w["skrotTrasy"] != sk:
            e.append(f"{opis}: skrotTrasy niezgodny z tabelą")
        p = ostatni.get(w["kzId"])
        if p is None and pelne and w["nrWejscia"] != 1:
            e.append(f"{opis}: strumień nie zaczyna się od 1")
        if p is not None:
            if w["nrWejscia"] != p["nrWejscia"] + 1:
                e.append(f"{opis}: luka w numeracji po {p['nrWejscia']}")
            if w["nrPartii"] <= p["nrPartii"] or w["cykl"] < p["cykl"] or w["czasSymulacjiMs"] < p["czasSymulacjiMs"]:
                e.append(f"{opis}: partia/cykl/czas nie rosną")
        ostatni[w["kzId"]] = w
        if w["faza"] in ("ROZKAZ", "USTERKA", "ZDARZENIE_KARTY", "POZA_FAZA") and not w["statusy"]:
            e.append(f"{opis}: faza natychmiastowa bez statusów")
        pp = -1
        for st in w["statusy"]:
            if samotny_surogat(st["id"]):
                e.append(f"{opis}: {st['rodzaj']}: identyfikator z samotnym surogatem")
            q = poz.get((st["rodzaj"], st["id"]))
            if q is None:
                e.append(f"{opis}: {st['rodzaj']} {st['id']} spoza tabeli")
                continue
            if w["kzId"] not in q[1]:
                e.append(f"{opis}: {st['rodzaj']} {st['id']} – KZ nie jest odbiorcą w tabeli")
            if q[0] < pp:
                e.append(f"{opis}: porządek statusów niekanoniczny")
            pp = q[0]
    # ta sama partia: status obiektu u wszystkich odbiorców z tabeli (gdy partia jest w całości w danych)
    partie = {}
    for w in wejscia:
        partie.setdefault(w.get("nrPartii"), []).append(w)
    for nr, ws in partie.items():
        if len({(w.get("faza"), w.get("cykl"), w.get("czasSymulacjiMs")) for w in ws}) != 1:
            e.append(f"partia {nr}: różne fazy/cykle/czasy w wejściach")
        obecne = {w.get("kzId") for w in ws}
        for w in ws:
            for st in w.get("statusy", []):
                q = poz.get((st.get("rodzaj"), st.get("id")))
                if q is not None and q[1] <= obecne:
                    for k in q[1]:
                        inne = next(x for x in ws if x["kzId"] == k)
                        if sum(1 for y in inne["statusy"] if y == st) != sum(1 for y in w["statusy"] if y == st):
                            e.append(f"partia {nr}: {st['rodzaj']} {st['id']} nie dotarł jednakowo do {k}")
    return e


przyklad_w = wczytaj(K / "testy" / "wejscie_cyklu_przyklad.json")
pw = oceń_wejscia(przyklad_w["trasa"], przyklad_w["wejscia"])
if pw:
    blad(f"wejscie_cyklu_przyklad.json: odrzucony: {pw}")
else:
    ok("wejscie_cyklu_przyklad.json: zgodny ze schematem i regułami krzyżowymi")
def _mutacja_w(opis, f):
    m = _copy.deepcopy(przyklad_w)
    f(m)
    if oceń_wejscia(m["trasa"], m["wejscia"]):
        ok(f"wejścia – przypadek błędny odrzucony: {opis}")
    else:
        blad(f"wejścia – przypadek błędny PRZYJĘTY: {opis}")
_mutacja_w("luka w numeracji KZ", lambda m: m["wejscia"][2].update(nrWejscia=5))
_mutacja_w("status u KZ spoza odbiorców", lambda m: m["wejscia"][3]["statusy"].insert(0, {"rodzaj": "odcinek", "id": "A:1", "pojazd": False, "stan": None}))
_mutacja_w("status pominięty u jednego z odbiorców", lambda m: m["wejscia"][3]["statusy"].pop())
_mutacja_w("porządek niekanoniczny", lambda m: m["wejscia"][0]["statusy"].reverse())
_mutacja_w("referencja/pole spoza kontraktu (wywołanie zwrotne)", lambda m: m["wejscia"][0].update(odbiorcaKz="callback"))
_mutacja_w("nieznany stan odcinka", lambda m: m["wejscia"][2]["statusy"][0].update(stan="cokolwiek"))
_mutacja_w("faza natychmiastowa bez statusów", lambda m: m["wejscia"][4].update(statusy=[]))
_mutacja_w("skrót trasy niezgodny", lambda m: m["wejscia"][1].update(skrotTrasy="f" * 64))
_mutacja_w("zmiana tabeli bez zmiany skrótu w wejściach", lambda m: m["trasa"]["wpisy"][3].update(odbiorcy=["KZ-A", "KZ-B"]))
_mutacja_w("obiekt bez odbiorców w tabeli", lambda m: m["trasa"]["wpisy"][0].update(odbiorcy=[]))
_mutacja_w("tabela bez KZ", lambda m: m["trasa"].update(kz=[]))
_mutacja_w("samotny surogat w identyfikatorze", lambda m: m["wejscia"][0]["statusy"][0].update(id="A\ud800"))

# wektory postaci kanonicznej (S02A.02): te same bajty i SHA-256 co WejscieCyklu.Kanoniczny() w C# (test WejscieCykluTests)
kanon = wczytaj(K / "testy" / "wejscie_cyklu_kanon.json")
for v in kanon["wektory"]:
    bl = [x.message[:120] for x in list(walidator_wejscia.iter_errors(v["wejscie"]))[:3]]
    linia = kanon_wejscia(v["wejscie"])
    if linia != v["kanoniczny"]:
        bl.append("postać kanoniczna różna od wektora")
    if hashlib.sha256(linia.encode("utf-8")).hexdigest() != v["sha256"]:
        bl.append("SHA-256 różny od wektora")
    if bl:
        blad(f"wektor kanoniczny {v['opis']}: {bl}")
    else:
        ok(f"wektor kanoniczny {v['opis']}: zgodny ze schematem, bajty i SHA-256 jak w C#")
if "--wejscia" in sys.argv:
    i = sys.argv.index("--wejscia") + 1
    while i < len(sys.argv) and not sys.argv[i].startswith("--"):
        kat = Path(sys.argv[i])
        i += 1
        pw = []
        try:
            surowa = (kat / "trasa.json").read_bytes()
            trasa = json.loads(surowa.decode("utf-8"))
            bt = [x.message[:160] for x in list(walidator_trasy.iter_errors(trasa))[:3]]
            if bt:
                raise ValueError(f"trasa.json niezgodna ze schematem: {bt}")   # nazwy plików z trasa["kz"] dopiero po walidacji
            if any(not re.fullmatch(r"KZ-[0-9A-Za-z_À-ÖØ-öø-ɏ]{1,32}", k) for k in trasa["kz"]):   # „$” wzorca przepuszcza końcowe LF
                raise ValueError("trasa.json: kzId spoza wzorca")
            if kanon_trasy(trasa) != surowa:
                pw.append("trasa.json: postać niekanoniczna")
            sk = hashlib.sha256(surowa).hexdigest()
            raport = wczytaj(kat / "raport.json")
            if raport.get("skrotTrasy") != sk or raport.get("zgodne") is not True:
                pw.append("raport.json: skrotTrasy niezgodny albo zgodne != true")
            wej = []
            for kz in trasa["kz"]:
                tekst = (kat / f"wejscia-{kz}.jsonl").read_text(encoding="utf-8")
                # tylko LF dzieli rekordy (splitlines dzieliłby też U+2028/U+0085 w treści)
                for nr, linia in enumerate(tekst.split("\n")[:-1] if tekst.endswith("\n") else tekst.split("\n"), 1):
                    d = json.loads(linia)
                    if kanon_wejscia(d) != linia:
                        pw.append(f"wejscia-{kz}.jsonl:{nr}: postać niekanoniczna")
                    if d.get("kzId") != kz:
                        pw.append(f"wejscia-{kz}.jsonl:{nr}: kzId {d.get('kzId')}")
                    wej.append(d)
            wej.sort(key=lambda d: (d.get("nrPartii", 0), d.get("kzId", "")))
            pw += oceń_wejscia(trasa, wej, sk, pelne=True)
            # próbka obejmuje pierwsze wejścia każdego KZ – partie spoza wspólnego zakresu pomijamy w porównaniu partii
        except Exception as ex:  # noqa: BLE001
            pw.append(f"nie można wczytać: {ex}")
        if pw:
            blad(f"wejścia: {kat.as_posix()}: odrzucone: {pw[:5]}")
        else:
            ok(f"wejścia: {kat.as_posix()}: zgodne ze schematem kz/wejscie_cyklu_v1, postacią kanoniczną i tabelą rozsyłania ({len(wej)} wejść)")

# ------------------------------------------------------------------ 6. budżety a schemat
ids = [b["id"] for b in budzety["budzety"]]
if len(ids) != len(set(ids)):
    blad("budżety: powtórzone identyfikatory")
b01 = next((b for b in budzety["budzety"] if b["id"] == "B01"), None)
lim = schema["x-suk-limity"]
if b01 and all(b01["wartosci"].get(k) == lim[k] for k in ("maxRecordBytesUtf8", "maxStringChars", "maxProperties", "maxExternalIdChars")) \
        and b01["wartosci"].get("maxExceptionCharsPoRedakcji") == lim["maxExceptionChars"] \
        and schema["maxProperties"] == lim["maxProperties"] and schema["properties"]["@x"]["maxLength"] == lim["maxExceptionChars"] \
        and schema["definitions"]["tekst"]["maxLength"] == lim["maxStringChars"]:
    ok(f"budżety: {len(ids)} pozycji; B01 zgodny z limitami schematu")
else:
    blad("budżety: B01 niezgodny z x-suk-limity/ograniczeniami schematu")

# ------------------------------------------------------------------ 7. stos: global.json, Directory.Packages.props, nuget.config
app = REPO / "app"
g = wczytaj(app / "global.json")
sdk = stos["dotnet"]["sdk"]
if g.get("sdk", {}).get("version") == sdk["wersja"] and g["sdk"].get("rollForward") == sdk["rollForward"] \
        and g["sdk"].get("allowPrerelease") is False and g.get("test", {}).get("runner") == "Microsoft.Testing.Platform":
    ok(f"app/global.json: SDK {sdk['wersja']} ({sdk['rollForward']}), bez prerelease, test.runner MTP")
else:
    blad("app/global.json niezgodny z kontrakty/stos/wersje_stosu.json")

props = ET.parse(app / "Directory.Packages.props").getroot()
wl = {e.tag: (e.text or "").strip() for e in props.iter() if e.tag in ("ManagePackageVersionsCentrally", "CentralPackageTransitivePinningEnabled")}
if wl.get("ManagePackageVersionsCentrally") == "true" and wl.get("CentralPackageTransitivePinningEnabled") == "true":
    ok("Directory.Packages.props: CPM i przypinanie przechodnie włączone")
else:
    blad("Directory.Packages.props: brak ManagePackageVersionsCentrally/CentralPackageTransitivePinningEnabled = true")
w_props = {e.get("Include"): e.get("Version") for e in props.iter("PackageVersion")}
w_stos = {p["id"]: p["wersja"] for p in stos["nuget"]["pakiety"]}
w_stos.update({p["id"]: p["wersja"] for p in stos["nuget"]["przypiete_przechodnie"]})
if w_props == w_stos:
    ok(f"Directory.Packages.props = wersje_stosu.json ({len(w_props)} pakietów)")
else:
    rozn = sorted(set(w_props.items()) ^ set(w_stos.items()))
    blad(f"Directory.Packages.props różni się od wersje_stosu.json: {rozn}")
for pid, v in w_props.items():
    if not re.match(r"^\d+\.\d+\.\d+$", v or ""):
        blad(f"Directory.Packages.props: {pid} ma wersję niestabilną/zakres: {v}")

nc = ET.parse(app / "nuget.config").getroot()
zrodla = nc.find("packageSources")
adds = zrodla.findall("add") if zrodla is not None else []
mapa = nc.find("packageSourceMapping")
wzorce = [p.get("pattern") for p in mapa.iter("package")] if mapa is not None else []
if zrodla is not None and zrodla.find("clear") is not None and len(adds) == 1 \
        and adds[0].get("value") == "https://api.nuget.org/v3/index.json" and wzorce == ["*"] \
        and nc.find("packageSourceCredentials") is None:
    ok("app/nuget.config: tylko nuget.org, <clear/>, mapowanie *, bez poświadczeń")
else:
    blad("app/nuget.config: oczekiwano <clear/>, jednego źródła nuget.org, mapowania * i braku poświadczeń")

# ------------------------------------------------------------------ 8. katalog kontraktów i sumy
STATUSY = {"wstepny", "zamrozony", "kandydat", "planowany", "nie_istnieje"}
JOB = re.compile(r"^S\d\d[A-Z]?\d?(\.\d\d)?$")
seen = set()
for c in kontrakty["kontrakty"]:
    cid = c["id"]
    if cid in seen:
        blad(f"katalog kontraktów: powtórzone id {cid}")
    seen.add(cid)
    if c["status"] not in STATUSY:
        blad(f"katalog kontraktów: {cid} ma nieznany status {c['status']}")
    for j in [c["tworzy"], c.get("zamraza")] + c.get("konsumenci", []):
        if j and not JOB.match(j):
            blad(f"katalog kontraktów: {cid} – zły identyfikator zadania {j}")
    for f in c.get("pliki", []):
        p = REPO / f["plik"]
        if not p.is_file():
            blad(f"katalog kontraktów: {cid} – brak pliku {f['plik']}")
        elif sha256_lf(p) != f["sha256_lf"]:
            blad(f"katalog kontraktów: {cid} – suma {f['plik']} niezgodna (zaktualizuj katalog po zmianie)")
        else:
            ok(f"katalog kontraktów: {cid} – {f['plik']} sha256 zgodne")
for f in kontrakty.get("narzedzia", []):
    p = REPO / f["plik"]
    if not p.is_file() or sha256_lf(p) != f["sha256_lf"]:
        blad(f"katalog kontraktów: narzędzie {f['plik']} – brak pliku lub suma niezgodna")
    else:
        ok(f"katalog kontraktów: narzędzie {f['plik']} sha256 zgodne")
ok(f"katalog kontraktów: {len(seen)} pozycji")

# ------------------------------------------------------------------ 9. brak kluczy prywatnych w kontraktach
for p in sorted(K.rglob("*")):
    if p.is_file() and re.search(rb"-----BEGIN [A-Z ]*PRIVATE KEY-----", p.read_bytes()):
        blad(f"klucz prywatny w {p.relative_to(REPO).as_posix()}")
ok("brak bloków kluczy prywatnych w kontrakty/")

try:
    from importlib import metadata as _md
    _wersja_js = _md.version("jsonschema")
except Exception:  # noqa: BLE001
    _wersja_js = "?"
wyniki.append(f"jsonschema {_wersja_js}, Python {sys.version.split()[0]}")
wyniki.append(f"błędów: {bledy}")
tekst = "\n".join(wyniki) + "\n"
if "--wynik" in sys.argv:
    Path(sys.argv[sys.argv.index("--wynik") + 1]).write_text(tekst, encoding="utf-8")
sys.stdout.write(tekst)
sys.exit(1 if bledy else 0)
