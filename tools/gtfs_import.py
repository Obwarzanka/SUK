"""
Import rzeczywistego rozkładu (wszyscy przewoźnicy: ŁKA, Polregio, PKP Intercity) z GTFS „Polish Trains”
(https://mkuran.pl/gtfs/polish_trains.zip – dane PKP PLK, z numerami peronów i torów) dla obszaru LCS Łódź Widzew.

Użycie:
    python gtfs_import.py polish_trains.zip ../app/LcsWidzew/Data/rozklad.json [RRRR-MM-DD] [rozklad_ic.json]

  RRRR-MM-DD  – dzień rozkładu (domyślnie dziś),
  rozklad_ic.json – opcjonalnie dane PKP IC z ptrc.gay/pkpic (tools/pkpic_import.py): pociągi IC o tym samym numerze
                    biorą stamtąd próżne przejazdy do/z zaplecza Olechów i pojazd.

Szlak wjazdu / wyjazdu wynika z sąsiedniego przystanku pasażerskiego (GTFS nie zawiera posterunków ruchu):
  A (Andrzejów, LK17)  – Andrzejów, Andrzejów Szosa, Koluszki i stacje za Koluszkami,
  C (Chojny, LK540)    – Chojny, Zarzew, Kaliska, Dąbrowa, Lublinek, Pabianicka, Retkinia, Żabieniec,
  M (Marysin, LK16)    – Stoki, Arturówek, Marysin, Radogoszcz, Zgierz,
  O (Olechów, LK541)   – przystanki Olechów.
Przystanek spoza listy – przypisanie wg kierunku geograficznego względem Widzewa (wypisane do kontroli).
"""
import csv
import datetime
import io
import json
import math
import sys
import zipfile

AREA = {'Łódź Widzew': 'LW', 'Łódź Fabryczna': 'LF', 'Łódź Niciarniana': 'LN'}
IN_BLOCK = {'A': '2A', 'C': '2C', 'O': '2O', 'M': '1M'}
OUT_BLOCK = {'A': '1A', 'C': '1C', 'O': '1O', 'M': '1M'}
SIDES = {
    'A': ['Andrzejów', 'Koluszki', 'Piotrków', 'Tomaszów', 'Częstochowa', 'Drzewica', 'Skierniewice', 'Warszawa', 'Rogów', 'Słotwiny', 'Gałków', 'Bedoń', 'Kraków', 'Katowice', 'Radom', 'Opoczno', 'Lublin'],
    'C': ['Chojny', 'Zarzew', 'Kaliska', 'Dąbrowa', 'Lublinek', 'Pabianicka', 'Retkinia', 'Żabieniec', 'Pabianice', 'Sieradz', 'Kalisz', 'Wrocław', 'Szczecin', 'Poznań', 'Zduńska'],
    'M': ['Stoki', 'Arturówek', 'Marysin', 'Radogoszcz', 'Zgierz', 'Kutno', 'Ozorków', 'Łowicz', 'Gdynia', 'Bydgoszcz'],
    'O': ['Olechów'],
}
CATS = {'LKA': 'ŁKA', 'PR': 'PR', 'IC': 'IC'}
UNIT_LEN = {'ŁKA': 85, 'PR': 110}


def rows(z, name):
    with z.open(name) as f:
        yield from csv.DictReader(io.TextIOWrapper(f, encoding='utf-8-sig'))


def tod(hms):
    h, m, s = (int(x) for x in hms.split(':'))
    return h * 3600 + m * 60 + s


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    src, dst = sys.argv[1], sys.argv[2]
    day = sys.argv[3] if len(sys.argv) > 3 else datetime.date.today().isoformat()
    ic_path = sys.argv[4] if len(sys.argv) > 4 else None
    ymd = day.replace('-', '')
    z = zipfile.ZipFile(src)
    stops = {r['stop_id']: r for r in rows(z, 'stops.txt')}
    base = {}
    for sid, r in stops.items():
        base[sid] = stops[r['parent_station']]['stop_name'] if r.get('parent_station') in stops else r['stop_name']
    w0 = next(r for r in stops.values() if r['stop_name'] == 'Łódź Widzew' and not r.get('parent_station'))
    lat0, lon0 = float(w0['stop_lat']), float(w0['stop_lon'])
    services = {r['service_id'] for r in rows(z, 'calendar_dates.txt') if r['date'] == ymd and r['exception_type'] == '1'}
    trips = {r['trip_id']: r for r in rows(z, 'trips.txt') if r['service_id'] in services}
    routes = {r['route_id']: r for r in rows(z, 'routes.txt')}
    st = {}
    for r in rows(z, 'stop_times.txt'):
        if r['trip_id'] in trips:
            st.setdefault(r['trip_id'], []).append(r)

    report = {}

    def side_of(name, sid):
        if name in AREA:
            return 'F' if name != 'Łódź Widzew' else None
        for k, keys in SIDES.items():
            if any(x in name for x in keys):
                report[name] = k
                return k
        # kierunek geograficzny względem Widzewa
        s = stops[sid]
        dx = (float(s['stop_lon']) - lon0) * math.cos(math.radians(lat0)); dy = float(s['stop_lat']) - lat0
        k = 'A' if dx > 0 and abs(dy) < dx * 1.5 else 'M' if dy > 0 else 'C'
        report[name] = k + ' (geograficznie – SPRAWDŹ)'
        return k

    ic = {}
    if ic_path:
        for e in json.load(open(ic_path, encoding='utf-8'))['trains']:
            ic.setdefault(e['num'].split('/')[0], []).append(e)

    out = []
    for tid, lst in st.items():
        lst.sort(key=lambda r: int(r['stop_sequence']))
        names = [base[r['stop_id']] for r in lst]
        idx = [i for i, n in enumerate(names) if n in AREA]
        if not idx:
            continue
        a, b = idx[0], idx[-1]
        t = trips[tid]; rt = routes.get(t['route_id'], {})
        agency = rt.get('agency_id') or t['route_id'].split('_')[0]
        if t['route_id'].endswith('_BUS') or rt.get('route_type') == '3':
            continue                                              # komunikacja zastępcza
        num = t.get('plk_train_number') or t['trip_short_name']
        cat = CATS.get(agency, agency)
        if agency == 'IC':
            cat = (rt.get('route_short_name') or 'IC').split()[0]
        # pociąg IC z danymi ptrc (próżne przejazdy, pojazd) – bierzemy tamte
        if agency == 'IC':
            # ten sam pociąg w danych ptrc: numer bywa inny (np. 1510/1 vs 1511/0) – dopasowanie po nazwie i czasie na posterunku
            gname = (t.get('plk_train_name') or '').upper()
            times = {(AREA[names[i]], tod(lst[i]['departure_time'])) for i in idx}
            hit = None
            for key, lst_ic in ic.items():
                for e in lst_ic:
                    same_name = gname and e.get('name', '').upper() == gname
                    near = any(abs(s2['dep'] - d) <= 180 and s2['post'] == p for s2 in e['stops'] for (p, d) in times)
                    if (same_name and near) or key == num.split('/')[0]:
                        hit = key; break
                if hit: break
            if hit:
                for e in ic.pop(hit):
                    e2 = dict(e); e2['src'] = 'pkpic'; out.append(e2)
                continue
        prev = (names[a - 1], lst[a - 1]['stop_id']) if a > 0 else None
        nxt = (names[b + 1], lst[b + 1]['stop_id']) if b + 1 < len(names) else None
        ent = side_of(*prev) if prev else None
        ext = side_of(*nxt) if nxt else None
        stops_out = []
        for i in idx:
            r = lst[i]
            stops_out.append(dict(post=AREA[names[i]], arr=tod(r['arrival_time']), dep=tod(r['departure_time']), stop=True,
                                  track=r.get('track') or None, platform=r.get('platform') or None, passenger=True))
        # zmiany czoła: Fabryczna (stacja czołowa) oraz Widzew, gdy pociąg wjeżdża i wyjeżdża tą samą stroną
        # w ŁF zmiana czoła, gdy pociąg tam przyjechał (nie zaczyna biegu) i jedzie dalej
        rev = [k for k, s in enumerate(stops_out) if s['post'] == 'LF' and (k > 0 or ent) and (k < len(stops_out) - 1 or ext)]
        if ent and ext and ent == ext and 'LF' not in [s['post'] for s in stops_out]:
            rev.append(max(k for k, s in enumerate(stops_out) if s['post'] == 'LW'))
        appear = tod(lst[a - 1]['departure_time']) if a > 0 else stops_out[0]['arr'] - 120
        out.append(dict(num=num, name=t.get('plk_train_name') or t.get('trip_headsign') or '', cat=cat, veh='', origin=names[0], destination=names[-1],
                        appear=appear, fromSide=ent, toSide=ext, len=UNIT_LEN.get(cat, 200), unit=agency != 'IC', stops=stops_out,
                        emptyIn=False, emptyOut=False, revIdx=sorted(set(rev)),
                        startBaza=prev is None and stops_out[0]['post'] == 'LW', endBaza=nxt is None and stops_out[-1]['post'] == 'LW',
                        **{'from': IN_BLOCK.get(ent) if ent else None, 'to': OUT_BLOCK.get(ext) if ext else None}, src='gtfs'))
    # obiegi: pociąg kończący bieg w ŁF / ŁW i najbliższy odjazd tego samego przewoźnika z tego samego toru (4–60 min)
    # to ta sama jednostka – zmiana czoła i numeru zamiast odstawienia
    def carrier(e):
        return 'IC' if e['cat'] not in ('ŁKA', 'PR') else e['cat']
    pairs = 0
    for post in ('LF', 'LW'):
        arrs = sorted([e for e in out if e.get('src') == 'gtfs' and e['to'] is None and e['stops'][-1]['post'] == post], key=lambda e: e['stops'][-1]['arr'])
        deps = [e for e in out if e.get('src') == 'gtfs' and e['from'] is None and e['stops'][0]['post'] == post]
        used = set()
        for ar in arrs:
            la = ar['stops'][-1]; best = None
            for dp in deps:
                if id(dp) in used or dp is ar or carrier(dp) != carrier(ar):
                    continue
                fd = dp['stops'][0]
                if la.get('track') and fd.get('track') and la['track'] != fd['track']:
                    continue
                gap = fd['dep'] - la['arr']
                if 240 <= gap <= 3600 and (best is None or gap < best[0]):
                    best = (gap, dp)
            if not best:
                continue
            dp = best[1]; used.add(id(dp))
            n0 = len(ar['stops'])
            nums = dict(ar.get('numsAt', {})); nums[str(n0 - 1)] = dp['num']
            for k, v in dp.get('numsAt', {}).items():
                nums[str(int(k) + n0)] = v
            ar['stops'] = ar['stops'] + dp['stops']
            # zmiana czoła: w ŁF zawsze (stacja czołowa); w ŁW tylko, gdy pociąg wraca tą samą stroną, z której przyjechał
            WEST = {'C', 'M', 'F'}
            def came_from(e):
                if any(x['post'] in ('LF', 'LN') for x in e['stops'][:-1]): return 'F'
                return e.get('fromSide')
            def goes_to(e):
                if any(x['post'] in ('LF', 'LN') for x in e['stops'][1:]): return 'F'
                return e.get('toSide')
            cf, gt = came_from(ar), goes_to(dp)
            reverse = post == 'LF' or (cf is not None and gt is not None and ((cf in WEST) == (gt in WEST)))
            ar['revIdx'] = sorted(set(ar['revIdx'] + ([n0 - 1] if reverse else []) + [i + n0 for i in dp['revIdx']]))
            ar['renumIdx'] = sorted(set(ar.get('renumIdx', []) + ([] if reverse else [n0 - 1]) + [i + n0 for i in dp.get('renumIdx', [])]))
            ar['numsAt'] = nums
            ar['to'] = dp['to']; ar['toSide'] = dp['toSide']; ar['endBaza'] = dp['endBaza']; ar['destination'] = dp['destination']
            ar['name'] = (ar['name'] + ' / ' + dp['name']).strip(' /')
            out.remove(dp); pairs += 1
    # pociągi IC z ptrc, których nie ma w GTFS (np. tylko przejazd techniczny)
    for lst in ic.values():
        for e in lst:
            e2 = dict(e); e2['src'] = 'pkpic'; out.append(e2)
    out.sort(key=lambda e: e['appear'])
    json.dump(dict(date=day, source='GTFS Polish Trains (mkuran.pl, dane PKP PLK)' + (' + ptrc.gay/pkpic' if ic_path else ''), trains=out),
              open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    by = {}
    for e in out:
        by[e['cat']] = by.get(e['cat'], 0) + 1
    print(f'{day}: {len(out)} pociągów w obszarze LCS – ' + ', '.join(f'{k} {v}' for k, v in sorted(by.items())) + f'; obiegi (przyjazd + odjazd tą samą jednostką): {pairs}')
    print('Przypisanie sąsiednich przystanków do szlaków:')
    for k, v in sorted(report.items()):
        print(f'  {k:32s} → {v}')


if __name__ == '__main__':
    main()
