"""Generate oil sprites into ValleyExchange/assets (pure stdlib, no PIL):
  pumpjack.png  32x32  two 16x32 big-craftable frames (beam down / beam up)
  refinery.png  16x32  big craftable
  oil.png       32x16  objects: 0 = crude oil barrel, 1 = gasoline can
  (hired hands: see make_crew_sprites.py)
Run: python -I tools/make_oil_sprites.py [--preview DIR]   (preview writes 8x upscaled copies)"""
import math
import struct
import sys
import zlib
from pathlib import Path

OUT = Path(__file__).parent.parent / "ValleyExchange" / "assets"
OUTLINE = (40, 32, 36, 255)
P = {
    "y": (236, 186, 44, 255), "Y": (178, 126, 30, 255),       # beam yellow
    "g": (150, 152, 160, 255), "G": (96, 98, 108, 255),       # steel
    "r": (190, 52, 40, 255), "R": (130, 30, 26, 255),         # red
    "c": (176, 170, 158, 255), "C": (128, 122, 114, 255),     # concrete
    "k": (24, 22, 26, 255), "b": (52, 54, 66, 255), "B": (82, 86, 102, 255),  # black, barrel
    "o": (255, 150, 30, 255), "O": (255, 226, 90, 255),       # flame
    "s": (205, 210, 216, 255), "S": (160, 166, 176, 255),     # silver
}


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [[None] * w for _ in range(h)]

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = P[c] if isinstance(c, str) else c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(n + 1):
            self.put(round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n), c)

    def outline(self):
        """Stardew-style dark outline around every filled shape."""
        filled = {(x, y) for y in range(self.h) for x in range(self.w) if self.px[y][x]}
        for x, y in filled:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if (x + dx, y + dy) not in filled:
                    self.put(x + dx, y + dy, OUTLINE)
        return self

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                if other.px[y][x]:
                    self.put(ox + x, oy + y, other.px[y][x])

    def png(self, scale=1):
        rows = []
        for y in range(self.h * scale):
            row = b"".join(bytes(self.px[y // scale][x // scale] or (0, 0, 0, 0)) for x in range(self.w * scale))
            rows.append(b"\x00" + row)
        chunk = lambda tag, data: struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        ihdr = struct.pack(">IIBBBBB", self.w * scale, self.h * scale, 8, 6, 0, 0, 0)
        return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b"")


def pumpjack(tilt):
    """tilt > 0: horsehead down. Pivot at (8,10); horsehead on the left over the wellhead."""
    c = Canvas(16, 32)
    c.rect(1, 28, 14, 30, "c")                     # concrete pad
    c.rect(1, 30, 14, 30, "C")
    c.rect(1, 25, 3, 27, "g")                       # wellhead
    c.rect(1, 27, 3, 27, "G")
    c.rect(7, 11, 8, 27, "G")                       # samson post
    c.rect(7, 11, 7, 27, "g")
    c.rect(11, 25, 14, 27, "G")                     # gearbox
    c.rect(11, 25, 14, 25, "g")
    a = math.radians(tilt)
    lx, ly = 8 - 6 * math.cos(a), 10 + 6 * math.sin(a)   # left (horsehead) end
    rx, ry = 8 + 6 * math.cos(a), 10 - 6 * math.sin(a)   # right (crank) end
    crank_y = 20 if tilt < 0 else 23                    # counterweight swings opposite the horsehead
    c.line(round(rx), round(ry) + 1, 13, crank_y, "k")  # pitman arm
    c.rect(11, crank_y - 1, 14, crank_y + 1, "r")       # counterweight
    c.rect(11, crank_y + 1, 14, crank_y + 1, "R")
    c.line(round(lx), round(ly), round(rx), round(ry), "y")         # walking beam
    c.line(round(lx), round(ly) + 1, round(rx), round(ry) + 1, "Y")
    hx, hy = round(lx), round(ly)
    c.rect(hx - 2, hy - 2, hx, hy + 4, "y")          # horsehead
    c.rect(hx - 2, hy + 4, hx, hy + 4, "Y")
    c.line(hx - 1, hy + 5, 2, 24, "k")               # polished rod down to the wellhead
    c.rect(7, 9, 9, 11, "G")                         # pivot bearing
    c.put(8, 10, "g")
    return c.outline()


def refinery():
    c = Canvas(16, 32)
    c.rect(0, 28, 15, 30, "c")
    c.rect(0, 30, 15, 30, "C")
    c.rect(3, 6, 8, 27, "s")                         # distillation tower
    c.rect(7, 6, 8, 27, "S")
    for y in (10, 15, 20, 25):
        c.rect(3, y, 8, y, "G")                      # tray bands
    c.rect(4, 3, 7, 5, "S")                          # tower cap
    c.rect(10, 19, 14, 27, "s")                      # storage tank
    c.rect(13, 19, 14, 27, "S")
    c.rect(10, 22, 14, 22, "r")
    c.rect(11, 18, 13, 18, "S")
    c.line(9, 13, 12, 13, "G")                       # pipe to the flare stack
    c.rect(12, 7, 12, 17, "G")                       # flare stack
    c.put(12, 6, "o"); c.put(11, 5, "o"); c.put(12, 5, "O"); c.put(13, 5, "o"); c.put(12, 4, "O"); c.put(12, 3, "o")
    c.line(9, 24, 9, 24, "G")
    return c.outline()


def barrel():
    c = Canvas(16, 16)
    c.rect(4, 3, 11, 14, "b")
    c.rect(4, 3, 5, 14, "B")                         # highlight
    for y in (5, 9, 13):
        c.rect(4, y, 11, y, "G")                     # ribs
    c.rect(5, 2, 10, 2, "k")                         # lid with oil
    c.put(9, 3, "k"); c.put(9, 4, "k")               # drip
    c.rect(7, 7, 8, 7, "y")                          # label
    return c.outline()


def gas_can():
    c = Canvas(16, 16)
    c.rect(3, 5, 12, 14, "r")
    c.rect(3, 5, 4, 14, "o")                         # highlight
    c.rect(3, 14, 12, 14, "R")
    c.line(5, 7, 10, 12, "R"); c.line(10, 7, 5, 12, "R")   # pressed X
    c.rect(5, 3, 8, 4, "R")                          # handle
    c.rect(10, 2, 11, 4, "y")                        # spout
    c.put(12, 1, "y")
    return c.outline()


def main():
    preview = Path(sys.argv[sys.argv.index("--preview") + 1]) if "--preview" in sys.argv else None
    sheets = {}
    pj = Canvas(32, 32)
    pj.paste(pumpjack(18), 0, 0)
    pj.paste(pumpjack(-18), 16, 0)
    sheets["pumpjack.png"] = pj
    sheets["refinery.png"] = refinery()
    items = Canvas(32, 16)
    items.paste(barrel(), 0, 0)
    items.paste(gas_can(), 16, 0)
    sheets["oil.png"] = items
    for name, canvas in sheets.items():
        (OUT / name).write_bytes(canvas.png())
        if preview:
            preview.mkdir(parents=True, exist_ok=True)
            (preview / name).write_bytes(canvas.png(8))
        print("wrote", name)


if __name__ == "__main__":
    main()
