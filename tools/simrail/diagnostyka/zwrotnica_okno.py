"""Okno kontrolera zwrotnic SimRail (przez mod SimRailMost, UDP 127.0.0.1:47131).

Kafelki zwrotnic obsadzonej stacji z przyciskami + / −, wyszukiwanie po numerze, wybór stacji, wyniki z gry.
„Położenia na żywo” słuchają stanów moda na porcie 47130 – tego samego, którego używa SUK, więc działają tylko
przy wyłączonym SUK (włączony SUK: przełączanie działa, położeń nie widać).
Logika wspólna z wersją konsolową: zwrotnica.py.
"""
import json
import os
import queue
import re
import socket
import sys
import threading
import time
import tkinter as tk
from tkinter import ttk

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zwrotnica as zr  # noqa: E402

KOLUMNY = 6
KOLOR = {"Plus": "#cfe8cf", "Minus": "#f3e0a8"}
TLO = "#eceff3"


class Okno:
    def __init__(self, root):
        self.root = root
        self.kolejka = queue.Queue()
        self.polozenia = {}          # rnn (małe litery) -> "Plus" / "Minus" / …
        self.czekaja = {}            # id polecenia -> (zwrotnica, order)
        self.nasluch = None          # gniazdo stanów, gdy „na żywo”
        self.kafelki = {}            # rnn -> (ramka, etykieta położenia)
        self.stacje = {}
        self.post = None
        self.zw = {}

        root.title("Zwrotnice – SimRail")
        root.geometry("980x700")
        root.minsize(640, 420)
        styl = ttk.Style()
        try:
            styl.theme_use("clam")
        except tk.TclError:
            pass
        styl.configure("Nr.TLabel", font=("Segoe UI", 15, "bold"))
        styl.configure("Btn.TButton", font=("Segoe UI", 12, "bold"), padding=(4, 2))

        gora = ttk.Frame(root, padding=(10, 8))
        gora.pack(fill="x")
        ttk.Label(gora, text="Stacja:").pack(side="left")
        self.wybor_stacji = ttk.Combobox(gora, state="readonly", width=28)
        self.wybor_stacji.pack(side="left", padx=(4, 12))
        self.wybor_stacji.bind("<<ComboboxSelected>>", lambda e: self.zmien_stacje(self.wybor_stacji.get().split(" (")[0]))
        ttk.Label(gora, text="Szukaj / polecenie:").pack(side="left")
        self.szukaj = tk.StringVar()
        pole = ttk.Entry(gora, textvariable=self.szukaj, width=16, font=("Segoe UI", 12))
        pole.pack(side="left", padx=4)
        pole.bind("<Return>", self.enter)
        self.szukaj.trace_add("write", lambda *a: self.rysuj())
        ttk.Label(gora, text="np. 71ab albo 71ab+ i Enter", foreground="#666").pack(side="left", padx=(2, 12))
        self.na_zywo = tk.BooleanVar(value=False)
        ttk.Checkbutton(gora, text="Położenia na żywo (bez SUK)", variable=self.na_zywo,
                        command=self.przelacz_nasluch).pack(side="right")
        ttk.Button(gora, text="Odśwież", command=self.odswiez).pack(side="right", padx=8)

        srodek = ttk.Frame(root)
        srodek.pack(fill="both", expand=True, padx=10)
        self.plotno = tk.Canvas(srodek, background=TLO, highlightthickness=0)
        pasek = ttk.Scrollbar(srodek, orient="vertical", command=self.plotno.yview)
        self.plotno.configure(yscrollcommand=pasek.set)
        pasek.pack(side="right", fill="y")
        self.plotno.pack(side="left", fill="both", expand=True)
        self.siatka = tk.Frame(self.plotno, background=TLO)
        self.okno_siatki = self.plotno.create_window((0, 0), window=self.siatka, anchor="nw")
        self.siatka.bind("<Configure>", lambda e: self.plotno.configure(scrollregion=self.plotno.bbox("all")))
        self.plotno.bind("<Configure>", lambda e: self.plotno.itemconfigure(self.okno_siatki, width=e.width))
        root.bind_all("<MouseWheel>", lambda e: self.plotno.yview_scroll(int(-e.delta / 120), "units"))

        dol = ttk.Frame(root, padding=(10, 6))
        dol.pack(fill="x")
        self.log = tk.Text(dol, height=7, font=("Consolas", 10), state="disabled", wrap="word")
        self.log.pack(fill="x")
        self.log.tag_configure("ok", foreground="#1b6e1b")
        self.log.tag_configure("zle", foreground="#b00020")
        self.log.tag_configure("info", foreground="#555")

        self.odswiez()
        pole.focus_set()
        root.after(100, self.petla)
        root.protocol("WM_DELETE_WINDOW", self.zamknij)

    # ------------------------------------------------------------------ stacje i kafelki
    def odswiez(self):
        self.stacje = zr.zrzuty()
        if not self.stacje:
            self.pisz("Brak zapisu pulpitu – obsadź posterunek w grze z modem SimRailMost i kliknij Odśwież.", "zle")
            return
        opisy = [zr.opis_stacji(self.stacje, p) for p in sorted(self.stacje, key=lambda p: -self.stacje[p]["czas"])]
        self.wybor_stacji["values"] = opisy
        domyslna = zr.wybierz_stacje(self.stacje)  # ostatnio obsadzana
        self.zmien_stacje(domyslna)

    def zmien_stacje(self, post):
        if post not in self.stacje:
            return
        self.post = post
        self.zw = zr.wczytaj_zwrotnice(self.stacje, post)
        self.wybor_stacji.set(zr.opis_stacji(self.stacje, post))
        self.pisz(f"{post}: {len(self.zw)} zwrotnic. Gra przełoży tylko zwrotnice stacji, którą teraz obsadzasz.", "info")
        self.rysuj()

    @staticmethod
    def klucz(z):
        m = re.match(r"([A-Za-z]*)(\d*)(.*)", z["nr"])
        return (m.group(1).lower(), int(m.group(2) or 0), m.group(3).lower())

    def rysuj(self):
        for w in self.siatka.winfo_children():
            w.destroy()
        self.kafelki.clear()
        filtr = re.sub(r"^\s*[+-]|[+-]\s*$", "", self.szukaj.get()).strip().lower()
        lista = [z for z in self.zw.values() if not filtr or z["nr"].lower().startswith(filtr) or z["rnn"].lower().startswith(filtr)]
        lista.sort(key=self.klucz)
        for c in range(KOLUMNY):
            self.siatka.columnconfigure(c, weight=1, uniform="k")
        for i, z in enumerate(lista):
            self.kafelek(z, i // KOLUMNY, i % KOLUMNY)
        if not lista:
            tk.Label(self.siatka, text="Brak zwrotnic pasujących do wyszukiwania", background=TLO,
                     foreground="#666", font=("Segoe UI", 11)).grid(row=0, column=0, columnspan=KOLUMNY, pady=30)
        self.plotno.yview_moveto(0)

    def kafelek(self, z, r, c):
        klucz = z["rnn"].lower()
        poz = self.polozenia.get(klucz)
        ramka = tk.Frame(self.siatka, background=KOLOR.get(poz, "white"), highlightbackground="#c5ccd6",
                         highlightthickness=1, padx=6, pady=4)
        ramka.grid(row=r, column=c, sticky="nsew", padx=4, pady=4)
        tk.Label(ramka, text=z["nr"], font=("Segoe UI", 15, "bold"), background=ramka["background"]).pack()
        et = tk.Label(ramka, text=self.opis_polozenia(poz), font=("Segoe UI", 9), foreground="#444",
                      background=ramka["background"])
        et.pack()
        przyciski = tk.Frame(ramka, background=ramka["background"])
        przyciski.pack(pady=(3, 0))
        ttk.Button(przyciski, text="+", width=3, style="Btn.TButton",
                   command=lambda: self.wyslij(z, "Plus")).pack(side="left", padx=2)
        ttk.Button(przyciski, text="−", width=3, style="Btn.TButton",
                   command=lambda: self.wyslij(z, "Minus")).pack(side="left", padx=2)
        self.kafelki[klucz] = (ramka, et)

    @staticmethod
    def opis_polozenia(poz):
        if not poz:
            return "położenie: ?"
        return {"Plus": "na plus (+)", "Minus": "na minus (−)"}.get(poz, poz)

    def ustaw_polozenie(self, klucz, poz):
        if self.polozenia.get(klucz) == poz:
            return
        self.polozenia[klucz] = poz
        if klucz in self.kafelki:
            ramka, et = self.kafelki[klucz]
            kolor = KOLOR.get(poz, "white")
            for w in [ramka, et] + list(ramka.winfo_children()):
                if isinstance(w, (tk.Frame, tk.Label)):
                    w.configure(background=kolor)
            et.configure(text=self.opis_polozenia(poz))

    # ------------------------------------------------------------------ polecenia
    def enter(self, _=None):
        t = self.szukaj.get().strip()
        m = re.match(r"^(.+?)\s*([+-])$", t)
        p = re.match(r"^([+-])\s*(.+)$", t)  # „+402” / „- 402”
        if p and not m:
            m = re.match(r"^(.+?)\s*([+-])$", f"{p.group(2)}{p.group(1)}")
        if not m:
            pasujace = zr.znajdz(self.zw, t) if t else []
            if len(pasujace) == 1:
                self.pisz(f"{pasujace[0]['nr']}: dopisz + albo - i Enter, albo kliknij przycisk.", "info")
            return
        znalezione = zr.znajdz(self.zw, m.group(1))
        if not znalezione:
            self.pisz(f"Nie ma zwrotnicy '{m.group(1)}' na pulpicie {self.post}.", "zle")
        elif len(znalezione) > 1:
            self.pisz("Niejednoznaczne, pasują: " + ", ".join(z["nr"] for z in znalezione), "zle")
        else:
            self.wyslij(znalezione[0], "Plus" if m.group(2) == "+" else "Minus")
            self.szukaj.set("")

    def wyslij(self, z, order):
        threading.Thread(target=self.wyslij_w_tle, args=(z, order), daemon=True).start()

    def wyslij_w_tle(self, z, order):
        id_ = int(time.time() * 1000) % 1_000_000_000
        pakiet = {"v": 1, "typ": "polecenie", "id": id_, "kod": "IndividualOrder", "nazwa": z["rnn"], "order": order}
        nazwa = f"{'+' if order == 'Plus' else '−'} {z['nr']}"
        self.kolejka.put(("log", f"→ {nazwa} ({z['rnn']})", "info"))
        wlasne = None if self.nasluch else zr.gniazdo_stanow()  # odpowiedź moda, gdy port wolny
        self.czekaja[id_] = nazwa
        log_od = zr.rozmiar_logu()
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
                s.sendto(json.dumps(pakiet).encode("utf-8"), ("127.0.0.1", zr.PORT_POLECEN))
        except OSError as ex:
            self.kolejka.put(("log", f"   nie wysłano: {ex}", "zle"))
            return
        koniec, z_logu = time.time() + 3, []
        while time.time() < koniec and id_ in self.czekaja:
            if wlasne:
                try:
                    dane, _ = wlasne.recvfrom(65535)
                    self.kolejka.put(("pakiet", dane))
                except (socket.timeout, OSError):
                    pass
            else:
                time.sleep(0.2)
            z_logu = [l for l in zr.nowe_linie_logu(log_od) if "polecenie z SUK" in l]
            if z_logu and (wlasne is None and self.nasluch is None):
                break
        if wlasne:
            wlasne.close()
        for l in z_logu:
            tresc = l.split("] ", 2)[-1]
            self.kolejka.put(("log", "   gra: " + tresc, "zle" if "odrzucono" in tresc else "ok"))
        if self.czekaja.pop(id_, None) and not z_logu:
            self.kolejka.put(("log", f"   {nazwa}: brak odpowiedzi – czy gra z modem jest uruchomiona i posterunek obsadzony?", "zle"))

    # ------------------------------------------------------------------ nasłuch stanów (bez SUK)
    def przelacz_nasluch(self):
        if self.na_zywo.get():
            s = zr.gniazdo_stanow()
            if s is None:
                self.na_zywo.set(False)
                self.pisz("Port 47130 zajęty (pewnie SUK jest włączony) – położeń nie pokażę, przełączanie działa.", "zle")
                return
            self.nasluch = s
            threading.Thread(target=self.petla_nasluchu, args=(s,), daemon=True).start()
            self.pisz("Nasłuch stanów włączony – położenia odświeżają się co ~2 s. Wyłącz przed uruchomieniem SUK.", "info")
        elif self.nasluch:
            s, self.nasluch = self.nasluch, None
            s.close()
            self.pisz("Nasłuch stanów wyłączony (port 47130 wolny dla SUK).", "info")

    def petla_nasluchu(self, s):
        while self.nasluch is s:
            try:
                dane, _ = s.recvfrom(65535)
                self.kolejka.put(("pakiet", dane))
            except socket.timeout:
                continue
            except OSError:
                break

    def pakiet(self, dane):
        try:
            m = json.loads(dane)
        except ValueError:
            return
        if m.get("typ") == "odp":
            nazwa = self.czekaja.pop(m.get("id"), None)
            if nazwa:
                self.pisz(f"   {'OK' if m.get('ok') else 'ODMOWA'}: {m.get('msg', '')}", "ok" if m.get("ok") else "zle")
            return
        obiekty = m.get("obiekty", []) + ([m] if m.get("typ") == "zmiana" else [])
        for o in obiekty:
            if o.get("zw"):
                self.ustaw_polozenie((o.get("n") or "").lower(), o["zw"])

    # ------------------------------------------------------------------ wspólne
    def petla(self):
        try:
            while True:
                w = self.kolejka.get_nowait()
                if w[0] == "log":
                    self.pisz(w[1], w[2])
                elif w[0] == "pakiet":
                    self.pakiet(w[1])
        except queue.Empty:
            pass
        self.root.after(100, self.petla)

    def pisz(self, tekst, tag="info"):
        self.log.configure(state="normal")
        self.log.insert("end", time.strftime("%H:%M:%S ") + tekst + "\n", tag)
        self.log.see("end")
        self.log.configure(state="disabled")

    def zamknij(self):
        if self.nasluch:
            s, self.nasluch = self.nasluch, None
            s.close()
        self.root.destroy()


if __name__ == "__main__":
    root = tk.Tk()
    Okno(root)
    root.mainloop()
