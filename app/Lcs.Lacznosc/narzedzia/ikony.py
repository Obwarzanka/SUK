import sys, os
from PIL import Image, ImageDraw, ImageFont

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
S = 1024  # rysujemy w 4x i zmniejszamy (wygładzanie)

NAVY = (11, 16, 32, 255); PANEL = (24, 34, 60, 255); RAMKA = (70, 86, 120, 255)
CYJAN = (79, 211, 230, 255); POMAR = (255, 138, 61, 255); BIALY = (235, 238, 245, 255)
ZIELONY = (107, 255, 107, 255); CZERW = (240, 80, 60, 255); NIEB = (60, 120, 230, 255)

def font(size, bold=True):
    for n in (["consolab.ttf", "arialbd.ttf"] if bold else ["consola.ttf", "arial.ttf"]):
        try: return ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", n), size)
        except OSError: pass
    return ImageFont.load_default()

def dymek(d, x, y, w, h, kolor, ogon="lewy"):
    d.rounded_rectangle([x, y, x + w, y + h], radius=h * 0.28, fill=kolor)
    if ogon == "lewy":
        d.polygon([(x + w * 0.18, y + h - 4), (x + w * 0.10, y + h + h * 0.38), (x + w * 0.38, y + h - 4)], fill=kolor)
    else:
        d.polygon([(x + w * 0.62, y + h - 4), (x + w * 0.90, y + h + h * 0.38), (x + w * 0.82, y + h - 4)], fill=kolor)
    # trzy kropki „pisze…”
    r = h * 0.09
    for i in range(3):
        cx = x + w * (0.30 + 0.20 * i); cy = y + h / 2
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=NAVY)

# ------------------------------------------------------------------ A: terminal stacji stycznej
def ikona_a():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([40, 90, 984, 800], radius=90, fill=RAMKA)
    d.rounded_rectangle([80, 130, 944, 760], radius=60, fill=NAVY)
    d.rectangle([80, 130, 944, 210], fill=(233, 236, 242, 255))
    d.rounded_rectangle([80, 130, 944, 210], radius=60, fill=(233, 236, 242, 255))
    d.rectangle([80, 170, 944, 210], fill=(233, 236, 242, 255))
    # tory z rozjazdem jak na terminalu
    lw = 26
    d.line([(120, 560), (900, 560)], fill=BIALY, width=lw)
    d.line([(300, 560), (420, 400), (900, 400)], fill=BIALY, width=lw, joint="curve")
    # numer pociągu
    d.rectangle([470, 515, 760, 605], fill=CZERW)
    d.text((615, 560), "7321", font=font(92), fill=BIALY, anchor="mm")
    # stopka z liniami jak plan ruchu
    for i, y in enumerate((650, 700)):
        d.line([(130, y), (880, y)], fill=ZIELONY if i == 0 else (60, 160, 70, 255), width=18)
    # podstawka
    d.polygon([(420, 800), (604, 800), (650, 900), (374, 900)], fill=RAMKA)
    d.rounded_rectangle([300, 890, 724, 950], radius=25, fill=RAMKA)
    # dymek
    dymek(d, 610, 20, 380, 240, CYJAN, "lewy")
    return im

# ------------------------------------------------------------------ B: słuchawka dyżurnego nad torami
def ikona_b():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([30, 30, 994, 994], radius=200, fill=NAVY)
    # tory w perspektywie
    d.polygon([(330, 994), (450, 560), (480, 560), (390, 994)], fill=(150, 160, 180, 255))
    d.polygon([(634, 994), (544, 560), (574, 560), (694, 994)], fill=(150, 160, 180, 255))
    for i, y in enumerate(range(600, 994, 70)):
        t = (y - 560) / 434
        x0 = 455 - 140 * t; x1 = 569 + 140 * t
        d.rectangle([x0 - 30 * t - 10, y, x1 + 30 * t + 10, y + 14 + 16 * t], fill=(110, 80, 60, 255))
    # słuchawka (łuk + dwie muszle)
    d.arc([180, 120, 844, 640], start=200, end=340, fill=POMAR, width=110)
    for cx in (250, 774):
        d.rounded_rectangle([cx - 120, 330, cx + 120, 520], radius=60, fill=POMAR)
    d.rounded_rectangle([205, 470, 295, 500], radius=10, fill=NAVY)
    d.rounded_rectangle([729, 470, 819, 500], radius=10, fill=NAVY)
    # dymek
    dymek(d, 560, 540, 400, 230, CYJAN, "prawy")
    return im

# ------------------------------------------------------------------ C: wskaźnik EZP
def ikona_c():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([30, 30, 994, 994], radius=200, fill=(91, 100, 120, 255))
    d.rounded_rectangle([30, 30, 994, 994], radius=200, outline=(200, 205, 215, 255), width=24)
    # pasek kolorów jak w rogu terminala
    for i, k in enumerate((CZERW, ZIELONY, NIEB)):
        d.rectangle([180 + i * 222, 120, 180 + (i + 1) * 222, 230], fill=k)
    d.rectangle([180, 250, 846, 290], fill=BIALY)
    d.text((512, 500), "EZP", font=font(300), fill=BIALY, anchor="mm")
    d.text((512, 760), "O W", font=font(190), fill=CYJAN, anchor="mm")
    # strzałki tam / z powrotem
    d.polygon([(120, 880), (230, 830), (230, 930)], fill=POMAR)
    d.polygon([(904, 880), (794, 830), (794, 930)], fill=POMAR)
    return im

ikony = {"A_terminal": ikona_a(), "B_sluchawka": ikona_b(), "C_ezp": ikona_c()}
podglad = Image.new("RGBA", (3 * 300 + 40, 360 + 150), (245, 246, 250, 255))
dp = ImageDraw.Draw(podglad)
for i, (n, im) in enumerate(ikony.items()):
    im.save(os.path.join(OUT, n + ".png"))
    im.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, n + "_256.png"))
    im.save(os.path.join(OUT, n + ".ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    x = 20 + i * 300
    podglad.alpha_composite(im.resize((256, 256), Image.LANCZOS), (x + 22, 20))
    # małe rozmiary – jak na pasku zadań i w tytule okna
    podglad.alpha_composite(im.resize((48, 48), Image.LANCZOS), (x + 60, 300))
    podglad.alpha_composite(im.resize((32, 32), Image.LANCZOS), (x + 130, 308))
    podglad.alpha_composite(im.resize((16, 16), Image.LANCZOS), (x + 185, 316))
    dp.text((x + 150, 380), n.replace("_", " – "), font=font(26), fill=(30, 30, 40, 255), anchor="mm")
podglad.save(os.path.join(OUT, "propozycje.png"))
print("ok")
