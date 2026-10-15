"""
Import rzeczywistego rozkładu PKP Intercity dla obszaru LCS Łódź Widzew (ŁW, ŁF, ŁN).

Źródło: https://ptrc.gay/pkpic/ (dane: output/<RRRR-MM-DD>.json – przebiegi pociągów PKP IC z posterunkami,
torami i krawędziami peronowymi). Źródło obejmuje wyłącznie pociągi PKP Intercity (IC, TLK, EIC, EIP) –
bez ŁKA i Polregio.

Użycie:
    python pkpic_import.py 2026-10-14 ../app/LcsWidzew/Data/rozklad_ic.json
    python pkpic_import.py plik.json ../app/LcsWidzew/Data/rozklad_ic.json   (plik pobrany wcześniej)

Wynik: lista wpisów dla symulatora. Każdy wpis to jeden pociąg w obszarze LCS:
  wjazd ze szlaku (from) albo z toru, postoje (stops: posterunek → przyjazd/odjazd/tor/peron),
  zmiany czoła (ŁF zawsze – stacja czołowa), zjazd próżny do zaplecza Olechów, zjazdy luzem lokomotyw z ŁF.
"""
import json
import sys
import urllib.request

# szlaki modelu (tools/lcs_panel.py): wjazd do ŁW / wyjazd z ŁW
IN_BLOCK = {'A': '2A', 'C': '2C', 'O': '2O', 'M': '1M'}
OUT_BLOCK = {'A': '1A', 'C': '1C', 'O': '1O', 'M': '1M'}
UNITS = {'ED161': 190, 'ED160': 125, 'ED250': 187, 'ED74': 85}   # długość składu [m]; pozostałe – skład wagonowy z lokomotywą


def side_of(name):
    """Kierunek względem ŁW: A – Andrzejów (wsch.), O – Olechów / zaplecze IC (wsch.), C – Chojny/Kaliska/Dąbrowa (zach.), M – Marysin, F – Fabryczna."""
    n = name
    if 'Andrzej' in n:
        return 'A'
    if 'Olech' in n or n.endswith('Widzew R56') or n.endswith('Widzew R50'):
        return 'O'
    if 'Marysin' in n:
        return 'M'
    if 'Fabryczna' in n or 'Niciarn' in n or 'Widzew R3' in n or 'PZS R3' in n:
        return 'F'
    if 'Chojny' in n or 'Kaliska' in n or 'Dąbrowa' in n or 'Widzew R9' in n or 'Lublinek' in n or 'Żabieniec' in n:
        return 'C'
    return None


def post_of(name):
    if name == 'Łódź Widzew':
        return 'LW'
    if name == 'Łódź Fabryczna':
        return 'LF'
    if name == 'Łódź Niciarniana':
        return 'LN'
    return None


def tod(hms):
    h, m, s = (int(x) for x in hms.split(':'))
    return h * 3600 + m * 60 + s


def in_area(name):
    return name.startswith('Łódź Widzew') or name in ('Łódź Fabryczna', 'Łódź Niciarniana')


def convert(train):
    S = train['stops']
    idx = [i for i, s in enumerate(S) if in_area(s['name'])]
    if not idx:
        return []
    out = []
    # podział na odcinki biegu w obszarze (pociąg może wjechać kilka razy – rzadkie)
    groups, cur = [], [idx[0]]
    for i in idx[1:]:
        if i == cur[-1] + 1:
            cur.append(i)
        else:
            groups.append(cur); cur = [i]
    groups.append(cur)
    for g in groups:
        a, b = g[0], g[-1]
        prev = S[a - 1]['name'] if a > 0 else None
        nxt = S[b + 1]['name'] if b + 1 < len(S) else None
        ent = side_of(S[a]['name']) if S[a]['name'] != 'Łódź Widzew' else None
        if ent in (None, 'F') or S[a]['name'] == 'Łódź Widzew':
            ent = side_of(prev) if prev else None
        ext = side_of(S[b]['name']) if S[b]['name'] != 'Łódź Widzew' else None
        if ext in (None, 'F') or S[b]['name'] == 'Łódź Widzew':
            ext = side_of(nxt) if nxt else None
        # postoje i zmiany czoła
        stops = []
        for i in g:
            s = S[i]
            p = post_of(s['name'])
            if not p:
                continue
            stop = s['passenger'] or s['arrival'] != s['departure']
            stops.append(dict(post=p, arr=tod(s['arrival']), dep=tod(s['departure']), stop=stop,
                              track=s.get('track'), platform=s.get('platform'), passenger=s['passenger']))
        appear = tod(S[a - 1]['departure']) if a > 0 else tod(S[a]['arrival']) - 120
        veh = S[a].get('vehicle') or ''
        unit = veh in UNITS
        e = dict(num=train['number'], name=train['name'], cat=train['category'], veh=veh, origin=train['origin'], destination=train['destination'],
                 appear=appear, fromSide=ent, toSide=ext, len=UNITS.get(veh, 260), unit=unit, stops=stops,
                 emptyIn=train['origin'] in ('Łódź Fabryczna',) and ent == 'O',
                 emptyOut=train['destination'] in ('Łódź Fabryczna',) and ext == 'O')
        e['from'] = IN_BLOCK.get(ent) if ent else None
        e['to'] = OUT_BLOCK.get(ext) if ext else None
        out.append(e)
    return out


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    src, dst = sys.argv[1], sys.argv[2]
    if src.endswith('.json'):
        data = json.load(open(src, encoding='utf-8'))
        date = src
    else:
        date = src
        url = f'https://ptrc.gay/pkpic/output/{src}.json'
        with urllib.request.urlopen(url, timeout=60) as r:
            data = json.loads(r.read().decode('utf-8'))
    entries = []
    for t in data.values():
        entries += convert(t)
    entries.sort(key=lambda e: e['appear'])
    json.dump(dict(date=date, source='https://ptrc.gay/pkpic/ (PKP Intercity)', trains=entries), open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(len(entries), 'pociągów IC w obszarze LCS ->', dst)
    for e in entries[:8]:
        print(e['num'], e['name'], e['from'], '->', e['to'], [(s['post'], s['arr'], s['dep'], s['track']) for s in e['stops']])


if __name__ == '__main__':
    main()
