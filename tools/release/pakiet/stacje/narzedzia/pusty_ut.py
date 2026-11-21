"""Pusty projekt UT (*.ut.json) dla nowej stacji z pulpitu SimRail.
Użycie: python -I pusty_ut.py "Nazwa posterunku" wynik.ut.json
"""
import json, sys

nazwa, wyj = sys.argv[1], sys.argv[2]
ut = {
    "version": 1,
    "name": nazwa,
    "crs": "EPSG:2180",
    "drawingScale": 2000,
    "notes": f"Pusty projekt UT stacji {nazwa} do zobrazowania z pulpitu SimRail – geometrię torów (PUWG 1992) zbuduj "
             "w Edytorze UT, potem „Przypnij do UT” w edytorze zobrazowania.",
    "backgrounds": [], "tracks": [], "turnouts": [], "crossings": [], "devices": [], "sections": [],
    "platforms": [], "levelCrossings": [], "kmPoints": [], "posts": [], "lines": [],
}
with open(wyj, "w", encoding="utf-8") as f:
    json.dump(ut, f, ensure_ascii=False, indent=1)
print("UT:", wyj)
