import sys, os
sys.argv = [sys.argv[0], "x"]
src = open("ikony.py", encoding="utf-8").read().split("ikony = {")[0]
exec(src)
OUT = "ikony"

def ikona_mix():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    # monitor terminala (A)
    d.rounded_rectangle([40, 110, 984, 790], radius=90, fill=RAMKA)
    d.rounded_rectangle([80, 150, 944, 750], radius=60, fill=NAVY)
    # znaczek EZP (C) z paskiem kolorów – lewy górny róg ekranu
    d.rounded_rectangle([130, 180, 520, 380], radius=26, fill=(91, 100, 120, 255), outline=(200, 205, 215, 255), width=10)
    for i, k in enumerate((CZERW, ZIELONY, NIEB)):
        d.rectangle([165 + i * 107, 205, 165 + (i + 1) * 107, 245], fill=k)
    d.text((325, 318), "EZP", font=font(120), fill=BIALY, anchor="mm")
    # tory z rozjazdem i numer pociągu (A)
    lw = 28
    d.line([(120, 600), (900, 600)], fill=BIALY, width=lw)
    d.line([(280, 600), (400, 440), (900, 440)], fill=BIALY, width=lw, joint="curve")
    d.rectangle([520, 552, 820, 648], fill=CZERW)
    d.text((670, 600), "7321", font=font(96), fill=BIALY, anchor="mm")
    d.line([(130, 690), (880, 690)], fill=ZIELONY, width=20)
    # podstawka
    d.polygon([(430, 790), (594, 790), (636, 880), (388, 880)], fill=RAMKA)
    d.rounded_rectangle([320, 870, 704, 930], radius=25, fill=RAMKA)
    # telefon dyżurnego (B) – lewy dolny róg, na krążku
    d.ellipse([0, 600, 400, 1000], fill=NAVY, outline=POMAR, width=22)
    tel = ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", "seguisym.ttf"), 250)
    d.text((200, 805), "☎", font=tel, fill=POMAR, anchor="mm")
    # dymek czatu (A/B) – prawy górny róg
    dymek(d, 640, 0, 380, 230, CYJAN, "lewy")
    return im

im = ikona_mix()
im.save(os.path.join(OUT, "MIX.png"))
im.save(os.path.join(OUT, "MIX.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
pod = Image.new("RGBA", (560, 300), (245, 246, 250, 255))
pod.alpha_composite(im.resize((256, 256), Image.LANCZOS), (20, 20))
x = 300
for r in (64, 48, 32, 24, 16):
    pod.alpha_composite(im.resize((r, r), Image.LANCZOS), (x, 150 - r // 2)); x += r + 14
ciemny = Image.new("RGBA", (560, 60), (32, 32, 36, 255))
x = 300
for r in (48, 32, 16):
    ciemny.alpha_composite(im.resize((r, r), Image.LANCZOS), (x, 30 - r // 2)); x += r + 20
out = Image.new("RGBA", (560, 360)); out.alpha_composite(pod, (0, 0)); out.alpha_composite(ciemny, (0, 300))
out.save(os.path.join(OUT, "MIX_podglad.png"))
print("ok")
