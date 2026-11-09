"""Generate Jerma Rats placeholder art and sound (Python standard library only).

Run from anywhere:  python tools\\make_placeholders.py

Writes:
  Textures/Items/big_<s|m|l|xl|xxl>_<0..3>.png   champion frames (shipped)
  art-src/placeholders/rat_<size>_<m|f>_<0..3>.png  optional normal-rat frames (NOT shipped: they would hide the
                                                    real rat art; copy real frames into Textures/Items/ instead)
  Sounds/rat_pickup.wav, rat_drop.wav, big_pickup.wav, big_drop.wav, rat_tune.wav

Your files are safe: art-src/placeholders.sha256 records the hash of every file this script wrote, and a file is only
rewritten if it is missing or still byte-identical to the placeholder. Anything you replaced is left alone.
"""
import hashlib
import math
import os
import struct
import wave
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HASHES = os.path.join(ROOT, "art-src", "placeholders.sha256")

# Exact vanilla rat sprite sizes (width, height) per size class, measured from the game files.
SIZES = {"s": (32, 16), "m": (48, 16), "l": (48, 16), "xl": (48, 32), "xxl": (64, 32)}
FRAMES = 4
RATE = 44100

MAGENTA = (255, 0, 255, 255)
BLACK = (0, 0, 0, 255)
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 3)  # the game's rat tiles use alpha 3, not 0: the whole tile stays clickable

DIGITS = {  # 3x5 pixel font
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"],
    "2": ["111", "001", "111", "100", "111"], "3": ["111", "001", "111", "001", "111"],
    "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"],
    "8": ["111", "101", "111", "101", "111"], "9": ["111", "101", "111", "001", "111"],
}


def load_hashes():
    known = {}
    if os.path.exists(HASHES):
        with open(HASHES, encoding="utf-8") as fh:
            for line in fh:
                parts = line.strip().split("  ", 1)
                if len(parts) == 2:
                    known[parts[1]] = parts[0]
    return known


def write_if_placeholder(rel, data, known, written):
    path = os.path.join(ROOT, rel)
    digest = hashlib.sha256(data).hexdigest()
    if os.path.exists(path):
        with open(path, "rb") as fh:
            current = hashlib.sha256(fh.read()).hexdigest()
        if current != known.get(rel) and current != digest:
            print("keep (yours)  ", rel)
            return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as fh:
        fh.write(data)
    written[rel] = digest
    print("placeholder   ", rel)


# ------------------------------------------------------------------ images

def png(width, height, pixels):
    raw = b"".join(b"\x00" + b"".join(bytes(p) for p in pixels[y * width:(y + 1) * width]) for y in range(height))

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def rat_frame(width, height, frame, big):
    """A generic rat-ish blob facing left, feet on the bottom edge, checkered, with the frame number."""
    px = [CLEAR] * (width * height)
    bob = (0, 1, 0, -1)[frame % 4] if height >= 32 else (0, 1, 0, 0)[frame % 4]
    body_cx, body_cy = width * 0.58, height * 0.55 + bob * 0.5
    body_rx, body_ry = width * 0.36, height * 0.40
    head_cx, head_cy, head_r = width * 0.20, height * 0.58 + bob * 0.5, min(width * 0.16, height * 0.36)

    def inside(x, y):
        fx, fy = x + 0.5, y + 0.5
        in_body = ((fx - body_cx) / body_rx) ** 2 + ((fy - body_cy) / body_ry) ** 2 <= 1.0
        in_head = (fx - head_cx) ** 2 + (fy - head_cy) ** 2 <= head_r ** 2
        tail_y = height * 0.80 + (frame % 2)
        in_tail = x >= width * 0.90 and abs(fy - tail_y) <= 0.6
        return in_body or in_head or in_tail

    for y in range(height):
        for x in range(width):
            if inside(x, y):
                edge = not (inside(x - 1, y) and inside(x + 1, y) and inside(x, y - 1) and inside(x, y + 1))
                checker = ((x // 2) + (y // 2)) % 2 == 0
                px[y * width + x] = BLACK if edge else (MAGENTA if checker else BLACK if not big else (128, 0, 160, 255))
    # frame number in white, centred on the body
    glyph = DIGITS[str(frame % 10)]
    gx, gy = int(body_cx) - 1, int(body_cy) - 2
    for row, bits in enumerate(glyph):
        for col, bit in enumerate(bits):
            x, y = gx + col, gy + row
            if bit == "1" and 0 <= x < width and 0 <= y < height:
                px[y * width + x] = WHITE
    return png(width, height, px)


# ------------------------------------------------------------------ sounds

def wav(samples):
    import io
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", max(-32767, min(32767, int(s * 32767)))) for s in samples))
    return buf.getvalue()


def tone(freq, seconds, volume=0.25, square=False):
    n = int(RATE * seconds)
    fade = max(1, int(RATE * 0.01))
    out = []
    for i in range(n):
        phase = math.sin(2 * math.pi * freq * i / RATE)
        v = (1.0 if phase >= 0 else -1.0) * 0.6 if square else phase
        env = min(1.0, i / fade, (n - i) / fade)
        out.append(v * volume * env)
    return out


def blip(f1, f2):
    return tone(f1, 0.07) + tone(f2, 0.09)


def tune():
    # Placeholder: an original 8-note chiptune jingle (C major): C5 E5 G5 C6 A5 F5 G5 C6.
    notes = [523.25, 659.25, 783.99, 1046.50, 880.00, 698.46, 783.99, 1046.50]
    lengths = [0.15, 0.15, 0.15, 0.30, 0.15, 0.15, 0.15, 0.45]
    out = []
    for f, d in zip(notes, lengths):
        out += tone(f, d, volume=0.18, square=True) + [0.0] * int(RATE * 0.02)
    return out


def main():
    known = load_hashes()
    written = dict(known)

    for size, (w, h) in SIZES.items():
        for n in range(FRAMES):
            write_if_placeholder(f"Textures/Items/big_{size}_{n}.png", rat_frame(w, h, n, True), known, written)
            for sex in ("m", "f"):
                write_if_placeholder(f"art-src/placeholders/rat_{size}_{sex}_{n}.png",
                                     rat_frame(w, h, n, False), known, written)

    write_if_placeholder("Sounds/rat_pickup.wav", wav(blip(660, 880)), known, written)
    write_if_placeholder("Sounds/rat_drop.wav", wav(blip(880, 660)), known, written)
    write_if_placeholder("Sounds/big_pickup.wav", wav(blip(330, 440)), known, written)
    write_if_placeholder("Sounds/big_drop.wav", wav(blip(440, 330)), known, written)
    if not os.path.exists(os.path.join(ROOT, "Sounds", "rat_tune.ogg")):
        write_if_placeholder("Sounds/rat_tune.wav", wav(tune()), known, written)
    else:
        print("skip          Sounds/rat_tune.wav (your .ogg exists; one file per name)")

    os.makedirs(os.path.dirname(HASHES), exist_ok=True)
    with open(HASHES, "w", encoding="utf-8", newline="\n") as fh:
        for rel in sorted(written):
            fh.write(f"{written[rel]}  {rel}\n")


if __name__ == "__main__":
    main()
