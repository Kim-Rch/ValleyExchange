"""Generate assets/terminal.png (16x32 big craftable): a CRT monitor showing a stock chart on a small desk.
Run: python -I tools/make_sprite.py  (pure stdlib, no PIL)."""
import struct
import sys
import zlib
from pathlib import Path

W, H = 16, 32
C = {
    ".": (0, 0, 0, 0),
    "o": (59, 38, 24, 255),     # outline
    "b": (214, 203, 172, 255),  # monitor beige
    "d": (176, 163, 130, 255),  # beige shadow
    "s": (12, 40, 20, 255),     # screen
    "g": (90, 255, 110, 255),   # chart line
    "w": (140, 90, 46, 255),    # desk wood
    "x": (98, 60, 30, 255),     # desk shadow
    "y": (255, 214, 64, 255),   # gold LED
}

ROWS = [
    "................",  # 0
    "................",
    "................",
    "................",
    "................",
    "................",  # 5
    ".oooooooooooooo.",
    ".obbbbbbbbbbbbo.",
    ".obssssssssssbo.",
    ".obsssssssssgbo.",
    ".obssssssssgsbo.",  # 10
    ".obsssssgssgsbo.",
    ".obssssgsgsgsbo.",
    ".obssgsgssgssbo.",
    ".obsgsgssssssbo.",
    ".obgsssssssssbo.",  # 15
    ".obssssssssssbo.",
    ".obbbbbbbbbbbbo.",
    ".obbbbbbbbbbybo.",
    ".oddddddddddddo.",
    ".oooooooooooooo.",  # 20
    "......obbo......",
    ".....oddddo.....",
    "oooooooooooooooo",
    "owwwwwwwwwwwwwwo",
    "oxxxxxxxxxxxxxxo",  # 25
    "oooooooooooooooo",
    ".ox..........xo.",
    ".ox..........xo.",
    ".ox..........xo.",
    ".ox..........xo.",  # 30
    ".oo..........oo.",
]


def main() -> None:
    assert len(ROWS) == H and all(len(r) == W for r in ROWS), [len(r) for r in ROWS]
    raw = b"".join(b"\x00" + b"".join(bytes(C[ch]) for ch in row) for row in ROWS)
    chunk = lambda tag, data: struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    out = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).parent.parent / "ValleyExchange" / "assets" / "terminal.png")
    out.write_bytes(png)
    print("wrote", out, len(png), "bytes")


if __name__ == "__main__":
    main()
