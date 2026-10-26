"""Writes package/icon.png (256x256, Thunderstore's required size).

A revolver hologram (revolver.png) above stat bars, on a Pip-Boy style screen.
"""
import os
import struct
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
SIZE = 64   # canvas, in art pixels
SCALE = 4   # 64 * 4 = 256

BG = (22, 18, 30)
CASE_SHADOW = (78, 78, 82)
CASE = (132, 132, 136)
CASE_LIGHT = (172, 172, 176)
SCREW = (196, 196, 200)
SCREEN = (8, 14, 32)
HALO = (18, 36, 80)
DARK = (22, 72, 150)
MID = (70, 150, 255)
LIGHT = (200, 230, 255)
LABEL = (40, 90, 170)

SCREEN_BOX = (5, 5, 58, 58)  # left, top, right, bottom (inclusive)

# Stat table: two columns of (label bar length, value bar length), like the panel's stat cells.
STATS = [[(9, 4), (7, 5), (10, 3)], [(8, 4), (11, 3), (6, 5)]]


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


def hologram(rgba):
    """Recolors a sprite pixel the way the hologram looks: a blue ramp by brightness."""
    r, g, b, _ = rgba
    lum = (0.3 * r + 0.59 * g + 0.11 * b) / 255
    return lerp(DARK, MID, lum * 2) if lum < 0.5 else lerp(MID, LIGHT, (lum - 0.5) * 2)


def rounded(x, y, box, radius):
    """Whether (x, y) is inside box with its corners cut to the given radius."""
    left, top, right, bottom = box
    if not (left <= x <= right and top <= y <= bottom):
        return False
    cx = left + radius if x < left + radius else right - radius if x > right - radius else x
    cy = top + radius if y < top + radius else bottom - radius if y > bottom - radius else y
    return (x - cx) ** 2 + (y - cy) ** 2 <= radius * radius + radius


def draw():
    img = [[BG] * SIZE for _ in range(SIZE)]

    def put(x, y, color):
        if 0 <= x < SIZE and 0 <= y < SIZE:
            img[y][x] = color

    # Casing: platinum body, lit from the top left, with a screw in each corner.
    case = (1, 1, SIZE - 2, SIZE - 2)
    for y in range(SIZE):
        for x in range(SIZE):
            if rounded(x, y, case, 5):
                inside_x = rounded(x - 1, y, case, 5) and rounded(x + 1, y, case, 5)
                inside_y = rounded(x, y - 1, case, 5) and rounded(x, y + 1, case, 5)
                if not rounded(x - 1, y - 1, case, 5):
                    put(x, y, CASE_LIGHT)
                elif not rounded(x + 1, y + 1, case, 5) or not (inside_x and inside_y):
                    put(x, y, CASE_SHADOW)
                else:
                    put(x, y, CASE)
    for sx in (3, SIZE - 4):
        for sy in (3, SIZE - 4):
            put(sx, sy, SCREW)
            put(sx + 1, sy + 1, CASE_SHADOW)

    # Screen: recessed (shadow on the top and left edges of the bezel, light on the bottom and right).
    left, top, right, bottom = SCREEN_BOX
    bezel = (left - 1, top - 1, right + 1, bottom + 1)
    for y in range(SIZE):
        for x in range(SIZE):
            if rounded(x, y, SCREEN_BOX, 3):
                put(x, y, SCREEN)
            elif rounded(x, y, bezel, 4):
                put(x, y, CASE_SHADOW if x + y < left + top + right + bottom - x - y else CASE_LIGHT)

    # The revolver, scaled up by a whole number so it fills most of the screen's width.
    sprite = read_png(os.path.join(HERE, "revolver.png"))
    width = right - left + 1
    scale = max(1, min(round((width - 10) / len(sprite[0])), (width - 4) // len(sprite[0])))
    sw, sh = len(sprite[0]) * scale, len(sprite) * scale
    gx, gy = left + (width - sw) // 2, top + 3
    solid = {(x, y) for y in range(sh) for x in range(sw) if sprite[y // scale][x // scale][3] > 0}
    for x, y in solid:
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if (nx, ny) not in solid:
                put(gx + nx, gy + ny, HALO)
    for x, y in solid:
        put(gx + x, gy + y, hologram(sprite[y // scale][x // scale]))

    # Divider, then the stat bars: dim labels, bright values.
    divider = gy + sh + 2
    for x in range(left + 2, right - 1):
        put(x, divider, LABEL)
    column_x = (left + 3, left + (width + 1) // 2 + 1)
    for column, cells in zip(column_x, STATS):
        y = divider + 3
        for label, value in cells:
            for dy in range(3):
                for dx in range(label):
                    put(column + dx, y + dy, LABEL)
                for dx in range(value):
                    put(column + label + 2 + dx, y + dy, LIGHT)
            y += 6

    # Scanlines and darkened corners across the whole screen.
    for y in range(top, bottom + 1):
        for x in range(left, right + 1):
            if rounded(x, y, SCREEN_BOX, 3):
                if y % 2:
                    put(x, y, lerp(img[y][x], SCREEN, 0.3))
                if not rounded(x, y, (left + 1, top + 1, right - 1, bottom - 1), 5):
                    put(x, y, lerp(img[y][x], SCREEN, 0.4))

    # Light from the screen reflecting off the bezel: each casing pixel near the screen is lit by the
    # bright screen pixels around it, so it glows brightest beside the revolver and the value bars.
    for y in range(SIZE):
        for x in range(SIZE):
            if rounded(x, y, SCREEN_BOX, 3):
                continue
            ring = next((d for d in (1, 2, 3) if rounded(x, y, (left - d, top - d, right + d, bottom + d), 3 + d)), 0)
            if not ring:
                continue
            light = [0, 0, 0]
            for sy in range(y - 10, y + 11):
                for sx in range(x - 10, x + 11):
                    if rounded(sx, sy, SCREEN_BOX, 3):
                        falloff = 1 / ((sx - x) ** 2 + (sy - y) ** 2)
                        light = [t + max(0, c - b) * falloff for t, c, b in zip(light, img[sy][sx], SCREEN)]
            strength = (0.4, 0.22, 0.1)[ring - 1]
            put(x, y, tuple(min(255, round(c + t * strength)) for c, t in zip(img[y][x], light)))
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
