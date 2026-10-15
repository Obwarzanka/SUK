"""
Maska planu schematycznego (obraz PNG) dla generatora układu: piksele ciemne (linie torów) jako bity, wiersze w hex.
Generator (gen_uklad.py) prowadzi po masce tory między kotwicami (zwrotnicami) – geometria jak na planie.

Użycie: python raster_maska.py <plan.png> <maska.json> [próg=150]
"""
import json
import sys

from PIL import Image

GAP = 0


def main(src, dst, thr='150'):
    im = Image.open(src).convert('L')
    w, h = im.size
    px = im.load()
    t = int(thr)
    rows = []
    for y in range(h):
        bits = 0
        for x in range(w):
            if px[x, y] < t:
                bits |= 1 << x
        # domknięcie poziomych przerw (napisy w przerwie linii toru: numer, długość) – do 2·GAP pikseli
        full = (1 << w) - 1
        dil = bits
        for s in range(1, GAP + 1):
            dil |= (bits << s) | (bits >> s)
        dil &= full
        ero = dil
        for s in range(1, GAP + 1):
            ero &= ((dil << s) | ((1 << s) - 1)) & ((dil >> s) | (full ^ (full >> s)))
        rows.append(format(bits | (ero & full), 'x'))
    json.dump(dict(w=w, h=h, rows=rows), open(dst, 'w'))
    print('maska', w, 'x', h)


if __name__ == '__main__':
    main(*sys.argv[1:])
