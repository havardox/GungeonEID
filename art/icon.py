import sys, math
# Draws the mod icon on a 64x64 pixel grid. Usage: python3 icon.py out.png 256
from PIL import Image

N = 64
K  = (20, 12, 28)
R1, R2, R3 = (104, 22, 38), (170, 38, 50), (218, 72, 70)
G1, G2, G3 = (166, 98, 26), (238, 170, 52), (255, 226, 126)
P0, P1, P2, P3 = (164, 132, 96), (204, 176, 128), (238, 218, 172), (255, 246, 220)
INK, INK2 = (60, 42, 60), (128, 100, 110)
OWN = (77, 166, 255)
BG0, BG1, BG2, BG3 = (20, 22, 50), (28, 34, 74), (36, 48, 98), (48, 66, 128)
W = (255, 255, 255)
M1, M2, M3 = (78, 84, 110), (146, 154, 178), (214, 220, 236)
WD1, WD2 = (104, 52, 30), (168, 92, 48)

img = Image.new("RGBA", (N, N))
px = img.load()

# Background: navy with stepped light rings around the centre.
for y in range(N):
    for x in range(N):
        r = math.hypot(x - 31.5, y - 30.5)
        px[x, y] = (BG3 if r < 22 else BG2 if r < 28 else BG1 if r < 35 else BG0) + (255,)
# Soft shadow under the book.
for y in range(50, 58):
    for x in range(N):
        if ((x - 31.5) / 27) ** 2 + ((y - 53.5) / 3.6) ** 2 < 1:
            px[x, y] = BG0 + (255,)

spr = {}
def put(x, y, c): spr[(x, y)] = c
def rect(x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(x, y, c)

L0, L1, R0, R1x = 8, 30, 33, 55        # page columns; gutter is 31..32
def dip(x):
    d = 31 - x if x <= 31 else x - 32
    return 3 if d <= 0 else 2 if d <= 2 else 1 if d <= 6 else 0
def top(x): return 14 + dip(x)
def bot(x): return 44 + dip(x)

# Cover: sides and bottom show around the pages.
for x in range(6, 58):
    for y in range(18, 50 + dip(min(max(x, 8), 55)) + 1):
        put(x, y, R2)
    put(x, 50 + dip(min(max(x, 8), 55)), R1)
for y in range(18, 50): put(6, y, R3); put(57, y, R1)

# Pages, with a two-line page stack underneath.
for x in range(L0, R1x + 1):
    for y in range(top(x), bot(x) + 1):
        put(x, y, P2)
    put(x, top(x), P3)
    put(x, bot(x) + 1, P1)
    put(x, bot(x) + 2, P0)
    put(x, bot(x) + 3, K)
# Gutter shading.
for x, c in ((29, P1), (30, P1), (31, P0), (32, P0), (33, P1), (34, P1)):
    for y in range(top(x) + 1, bot(x) + 1):
        put(x, y, c)
for y in range(top(31) + 1, bot(31) + 1):
    put(31, y, INK2); put(32, y, P0)
# Outer page edges.
for y in range(top(L0) + 1, bot(L0) + 1): put(L0, y, P1)
for y in range(top(R1x) + 1, bot(R1x) + 1): put(R1x, y, P1)

# Left page: the item's portrait, a small revolver, plus quality pips and a line of text.
gun = {}
def g(x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            gun[(x, y)] = c
g(7, 2, 18, 2, M3); g(7, 3, 18, 3, M2); g(7, 4, 18, 4, M1)      # barrel
g(17, 1, 17, 1, M2)                                              # front sight
g(3, 1, 9, 6, M2); g(3, 1, 8, 1, M3)                             # frame
for x in range(4, 9):                                            # fluted cylinder
    g(x, 3, x, 5, M3 if x % 2 == 0 else M1)
g(2, 0, 3, 0, M1); g(2, 1, 2, 1, M1)                             # hammer
g(7, 7, 7, 8, M1)                                                # trigger
for y, (a, b) in enumerate(((2, 5), (1, 5), (1, 4), (0, 4), (0, 3), (0, 3)), start=6):
    g(a, y, b, y, WD2); g(b, y, b, y, WD1)                       # grip
g(3, 8, 3, 8, G2)
GX, GY = 10, 21
for (x, y) in gun:
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            q = (x + dx, y + dy)
            if q not in gun: put(GX + q[0], GY + q[1], K)
for (x, y), c in gun.items(): put(GX + x, GY + y, c)
for i in range(5):
    put(11 + i * 3, 35, G2); put(12 + i * 3, 35, G2 if i < 4 else P1)
rect(11, 38, 26, 38, INK2)
rect(11, 41, 22, 41, INK2)

# Right page: a red title, body text, one line in the "owned" blue.
rect(36, 19, 48, 19, R2)
for y, b in ((23, 52), (26, 50), (29, 52), (35, 49), (38, 52), (41, 46)):
    rect(36, y, b, y, INK2)
rect(36, 32, 44, 32, OWN)

# Gold corner caps on the cover.
for (x, y) in ((6, 18), (7, 18), (8, 18), (6, 19), (6, 20),
               (57, 18), (56, 18), (55, 18), (57, 19), (57, 20)):
    put(x, y, G2)
for (x, y) in ((6, 48), (6, 49), (6, 50), (7, 50), (8, 50),
               (57, 48), (57, 49), (57, 50), (56, 50), (55, 50)):
    put(x, y, G1)

# Ribbon bookmark hanging from the spine.
for y in range(50, 59):
    put(33, y, G3 if y < 52 else G2); put(34, y, G2); put(35, y, G1)
put(34, 59, None); put(33, 59, G2); put(35, 59, G1)

spr = {k: v for k, v in spr.items() if v is not None}

# 1px dark outline around the sprite.
outline = set()
for (x, y) in spr:
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            q = (x + dx, y + dy)
            if q not in spr and 0 <= q[0] < N and 0 <= q[1] < N:
                outline.add(q)
for q in outline: px[q] = K + (255,)
for (x, y), c in spr.items(): px[x, y] = c + (255,)

# Sparkles.
def sparkle(x, y, n, c):
    px[x, y] = W + (255,)
    for i in range(1, n + 1):
        for dx, dy in ((i, 0), (-i, 0), (0, i), (0, -i)):
            px[x + dx, y + dy] = (W if i == 1 else c) + (255,)
sparkle(54, 8, 3, G3)
sparkle(9, 7, 2, G3)
sparkle(59, 27, 1, W)

size = int(sys.argv[2])
img.resize((size, size), Image.NEAREST).convert("RGB").save(sys.argv[1])
