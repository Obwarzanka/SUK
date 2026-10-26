"""Ikony programów: tools/ikony/svg/*.svg -> app/<projekt>/app.ico.

Każdy rozmiar jest rysowany z wektora osobno (Chrome bez okna), żeby małe
ikony były ostre, a nie pomniejszone z 256 px. Uruchom po zmianie SVG:
    python tools/ikony/generuj_ikony.py
"""
import os
import subprocess
import sys
import tempfile

from PIL import Image

TU = os.path.dirname(os.path.abspath(__file__))
APP = os.path.normpath(os.path.join(TU, "..", "..", "app"))
ROZMIARY = [256, 64, 48, 40, 32, 24, 20, 16]
PROGRAMY = {
    "symulator": "LcsWidzew",
    "edytor_ut": "Lcs.EdytorUT",
    "edytor_zobrazowania2": "Lcs.EdytorZobrazowania2",
    "edytor_taboru": "Lcs.EdytorTaboru",
    "komputerownia": "Lcs.Komputerownia",
    "przechylka": "Lcs.Przechylka",
    "host_swiata": "Lcs.WorldHost",
}
CHROME = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
]


def przegladarka():
    for p in CHROME:
        if os.path.exists(p):
            return p
    sys.exit("Brak Chrome/Edge do rysowania SVG.")


def rysuj(svg, katalog):
    """Jedna strona z ikoną we wszystkich rozmiarach obok siebie -> zrzut -> wycinki."""
    with open(svg, encoding="utf-8") as f:
        kod = f.read()
    x, ramki, html = 0, [], []
    for r in ROZMIARY:
        html.append(f'<div style="position:absolute;left:{x}px;top:0;width:{r}px;height:{r}px">'
                    + kod.replace("<svg ", f'<svg width="{r}" height="{r}" ', 1) + "</div>")
        ramki.append((x, r))
        x += r + 8
    strona = os.path.join(katalog, "ikona.html")
    with open(strona, "w", encoding="utf-8") as f:
        f.write('<html><body style="margin:0;background:transparent">' + "".join(html) + "</body></html>")
    zrzut = os.path.join(katalog, "zrzut.png")
    subprocess.run([przegladarka(), "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    "--force-device-scale-factor=1", "--default-background-color=00000000",
                    f"--window-size={x},{ROZMIARY[0]}", f"--screenshot={zrzut}",
                    "file:///" + strona.replace("\\", "/")],
                   check=True, capture_output=True)
    obraz = Image.open(zrzut).convert("RGBA")
    return [obraz.crop((x0, 0, x0 + r, r)) for x0, r in ramki]


def main():
    with tempfile.TemporaryDirectory() as tmp:
        for nazwa, projekt in PROGRAMY.items():
            obrazy = rysuj(os.path.join(TU, "svg", nazwa + ".svg"), tmp)
            cel = os.path.join(APP, projekt, "app.ico")
            obrazy[0].save(cel, format="ICO", sizes=[(r, r) for r in ROZMIARY],
                           append_images=obrazy[1:])
            obrazy[0].save(os.path.join(TU, nazwa + ".png"))
            print(f"{nazwa}: {cel}")


if __name__ == "__main__":
    main()
