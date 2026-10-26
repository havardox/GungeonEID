"""Writes package/icon.png (256x256, Thunderstore's required size).

The icon shows The Exotic's gun sprite (the_exotic.png, from the Enter the Gungeon wiki) as the
hologram The Exotic projects over chests: a cyan, scanlined image floating above a light beam.
Three of the gun's stats are written underneath in the same hologram colors.
"""
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
SIZE = 64   # canvas, in art pixels
SCALE = 4   # 64 * 4 = 256

BG = (22, 18, 30)
HALO = (26, 58, 80)
BEAM = (32, 78, 104)
BEAM_EDGE = (56, 142, 186)
EMITTER = (200, 250, 255)
DARK = (22, 96, 136)
MID = (70, 190, 235)
LIGHT = (200, 252, 255)
LABEL = (70, 160, 200)

STATS = [("DPS", "44.5"), ("RELOAD", "1.5"), ("AMMO", "50")]

# 3x5 pixel font for the stat lines.
FONT = {
    "A": ["010", "101", "111", "101", "101"], "D": ["110", "101", "101", "101", "110"],
    "E": ["111", "100", "110", "100", "111"], "L": ["100", "100", "100", "100", "111"],
    "M": ["101", "111", "111", "101", "101"], "O": ["010", "101", "101", "101", "010"],
    "P": ["110", "101", "110", "100", "100"], "R": ["110", "101", "110", "101", "101"],
    "S": ["011", "100", "010", "001", "110"],
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"],
    "2": ["110", "001", "010", "100", "111"], "3": ["110", "001", "010", "001", "110"],
    "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "110", "001", "110"],
    "6": ["011", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"],
    "8": ["111", "101", "111", "101", "111"], "9": ["111", "101", "111", "001", "110"],
    ".": ["0", "0", "0", "0", "1"], " ": ["0", "0", "0", "0", "0"],
}


def read_png(path):
    """Decodes an 8-bit, non-interlaced PNG (palette, RGB or RGBA) into rows of RGBA tuples."""
    with open(path, "rb") as f:
        data = f.read()
    pos, chunks = 8, {}
    while pos < len(data):
        length, tag = struct.unpack(">I4s", data[pos:pos + 8])
        chunks.setdefault(tag, []).append(data[pos + 8:pos + 8 + length])
        pos += 12 + length
    width, height, depth, color_type = struct.unpack(">IIBB", chunks[b"IHDR"][0][:10])
    if depth != 8:
        raise ValueError("only 8-bit PNGs are supported")
    channels = {3: 1, 2: 3, 6: 4}[color_type]
    raw = zlib.decompress(b"".join(chunks[b"IDAT"]))
    stride = width * channels
    rows, prev = [], bytearray(stride)
    for y in range(height):
        kind = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            left = line[i - channels] if i >= channels else 0
            up = prev[i]
            up_left = prev[i - channels] if i >= channels else 0
            if kind == 1:
                line[i] = (line[i] + left) & 255
            elif kind == 2:
                line[i] = (line[i] + up) & 255
            elif kind == 3:
                line[i] = (line[i] + (left + up) // 2) & 255
            elif kind == 4:
                p = left + up - up_left
                pa, pb, pc = abs(p - left), abs(p - up), abs(p - up_left)
                pred = left if pa <= pb and pa <= pc else up if pb <= pc else up_left
                line[i] = (line[i] + pred) & 255
        rows.append(line)
        prev = line
    if color_type == 3:
        plte = chunks[b"PLTE"][0]
        trns = chunks.get(b"tRNS", [b""])[0]
        palette = [tuple(plte[i:i + 3]) + ((trns[i // 3] if i // 3 < len(trns) else 255),)
                   for i in range(0, len(plte), 3)]
        return [[palette[v] for v in row] for row in rows]
    if color_type == 2:
        return [[tuple(row[i:i + 3]) + (255,) for i in range(0, len(row), 3)] for row in rows]
    return [[tuple(row[i:i + 4]) for i in range(0, len(row), 4)] for row in rows]


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def hologram(rgba, row):
    """Recolors a sprite pixel the way the hologram looks: a cyan ramp by brightness, with scanlines."""
    r, g, b, _ = rgba
    lum = (0.3 * r + 0.59 * g + 0.11 * b) / 255
    color = lerp(DARK, MID, lum * 2) if lum < 0.5 else lerp(MID, LIGHT, (lum - 0.5) * 2)
    return lerp(color, BG, 0.3) if row % 2 else color


def draw():
    img = [[BG] * SIZE for _ in range(SIZE)]

    def put(x, y, color):
        if 0 <= x < SIZE and 0 <= y < SIZE:
            img[y][x] = color

    sprite = read_png(os.path.join(HERE, "the_exotic.png"))
    sw, sh = len(sprite[0]), len(sprite)
    sx, sy = (SIZE - sw) // 2, 8
    solid = {(x, y) for y in range(sh) for x in range(sw) if sprite[y][x][3] > 0}

    # Beam: a dithered cone from the emitter up to the bottom of the hologram, with brighter edges.
    emit_x, emit_y = SIZE // 2, 58
    top_y = sy + sh + 1
    for y in range(top_y, emit_y):
        t = (emit_y - y) / (emit_y - top_y)
        left = round(emit_x - 1 - t * (sw / 2 - 2))
        right = round(emit_x + t * (sw / 2 - 2))
        for x in range(left + 1, right):
            if (x + y) % 2 == 0:
                put(x, y, BEAM)
        put(left, y, BEAM_EDGE)
        put(right, y, BEAM_EDGE)
    for dx in (-1, 0):
        put(emit_x + dx, emit_y, EMITTER)
    put(emit_x - 2, emit_y, BEAM_EDGE)
    put(emit_x + 1, emit_y, BEAM_EDGE)

    # Faint halo around the gun, then the gun itself in hologram colors.
    for x, y in solid:
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if (nx, ny) not in solid:
                put(sx + nx, sy + ny, HALO)
    for x, y in solid:
        put(sx + x, sy + y, hologram(sprite[y][x], sy + y))

    # Stat lines under the gun: dim labels, bright values.
    y = sy + sh + 6
    for label, value in STATS:
        x = sx
        for text, color in ((label + " ", LABEL), (value, LIGHT)):
            for ch in text:
                glyph = FONT[ch]
                for gy, line in enumerate(glyph):
                    for gx, bit in enumerate(line):
                        if bit == "1":
                            put(x + gx, y + gy, color)
                x += len(glyph[0]) + 1
        y += 7
    return img


def write_png(path, img):
    size = SIZE * SCALE
    rows = []
    for y in range(size):
        line = img[y // SCALE]
        row = bytearray([0])
        for x in range(size):
            row += bytes(line[x // SCALE]) + b"\xff"
        rows.append(bytes(row))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(b"".join(rows), 9)))
        f.write(chunk(b"IEND", b""))


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "package", "icon.png")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    write_png(out, draw())
    print(os.path.abspath(out))
