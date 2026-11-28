"""Szybki podgląd zrzutu pulpitu SimRail (pulpit_*.json) jako PNG – czysty Python (bez PIL)."""
import json, sys, zlib, struct

d = json.load(open(sys.argv[1], encoding="utf-8"))
wyj = sys.argv[2]
C = 9  # piksele na komórkę
el = d["elementy"]
W, H = 120 * C, 33 * C
zakladki = sorted({e["pola"].get("virtualTab", 0) for e in el})
obraz = [[(18, 18, 18)] * W for _ in range(H * len(zakladki))]

def px(x, y, k):
    if 0 <= x < W and 0 <= y < H * len(zakladki) and y >= 0:
        obraz[y][x] = k

def linia(x0, y0, x1, y1, k, g=2):
    n = max(abs(x1 - x0), abs(y1 - y0), 1)
    for i in range(n + 1):
        x = round(x0 + (x1 - x0) * i / n); y = round(y0 + (y1 - y0) * i / n)
        for dx in range(-g // 2, g - g // 2):
            for dy in range(-g // 2, g - g // 2):
                px(x + dx, y + dy, k)

SZARY, ZOLTY, BIAL, ZIEL, CZER, NIEB = (150, 150, 150), (220, 200, 60), (230, 230, 230), (60, 200, 90), (220, 60, 60), (80, 140, 230)
for e in el:
    p = e["pola"]; k = e["klasa"]
    zi = zakladki.index(p.get("virtualTab", 0))
    oy = zi * H
    pos = p.get("position") or [0, 0]
    cx = lambda x: int(x * C + C / 2)
    cy = lambda y: int(oy + y * C + C / 2)
    if k == "EBI300StraightTrack" and p.get("cells"):
        cs = p["cells"]
        kol = ZOLTY if "kol_it" in p.get("railNetworkName", "") else SZARY
        if p.get("isHorizontal", True):
            linia(cs[0][0] * C, cy(cs[0][1]), (cs[-1][0] + 1) * C, cy(cs[-1][1]), kol)
        else:
            linia(cx(cs[0][0]), oy + cs[0][1] * C, cx(cs[-1][0]), oy + (cs[-1][1] + 1) * C, kol)
    elif k == "EBI300CornerTrack":
        x, y = pos; r = p.get("rotation", 0) % 4
        # przybliżenie: łuk jako ukos przez komórkę
        if r in (0, 2): linia(x * C, oy + (y + 1) * C, (x + 1) * C, oy + y * C, SZARY)
        else: linia(x * C, oy + y * C, (x + 1) * C, oy + (y + 1) * C, SZARY)
    elif k in ("EBI300HorizontalJunction", "EBI300VerticalJunction"):
        x, y = pos
        linia(x * C, cy(y), (x + 1) * C, cy(y), BIAL)
        dy = -C if p.get("isUp") else C
        linia(cx(x), cy(y), cx(x) + (-C if p.get("isLeft") else C), cy(y) + dy // 2, BIAL)
    elif k == "EBI300Signal":
        x, y = pos
        kol = ZIEL if p.get("signalType", "") != "ShuntingSignal" else NIEB
        for i in range(4):
            s = i if p.get("direction") == "Right" else 3 - i
            linia(x * C + 2 + i, cy(y) - 6 + s, x * C + 2 + i, cy(y) - 6 + (6 - s), kol, 1)
    elif k == "EBI300Platform":
        x, y = pos; w = max(1, int(p.get("width", 50) / 32 * C / 3));
        for i in range(w): px(x * C + i, oy + y * C, BIAL); px(x * C + i, oy + y * C + 2, BIAL)
    elif k == "EBI300LineBlock":
        x, y = pos; linia(x * C, cy(y), x * C + C, cy(y), CZER, 3)

# PNG
raw = b"".join(b"\x00" + bytes(c for k in row for c in k) for row in obraz)
def chunk(t, dane): return struct.pack(">I", len(dane)) + t + dane + struct.pack(">I", zlib.crc32(t + dane) & 0xffffffff)
png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, len(obraz), 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b"")
open(wyj, "wb").write(png)
print("zapisano", wyj, W, "x", len(obraz), "zakładki", zakladki)
