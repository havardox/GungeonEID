import sys
from PIL import Image, ImageDraw

N = 64  # logo is drawn on a 64x64 pixel grid, then scaled up with nearest-neighbour

# Palette, taken from the mod's own Design.cs colors where possible.
BG_BOT   = (18, 13, 26)
GLOW     = (64, 46, 84)
PANEL    = (13, 10, 20)
BORDER   = (88, 72, 110)
BORDER_HI= (128, 108, 150)
OUTLINE  = (10, 8, 15)
ACCENT   = (255, 209, 82)    # Design.Accent
ACCENT_D = (196, 140, 40)
QUOTE    = (237, 219, 158)   # Design.Quote
TEXT     = (242, 242, 242)
DIM      = (158, 158, 158)
DPS      = (255, 158, 64)    # Design.Dps
OWNED    = (77, 166, 255)    # default OwnedColor
Q_S      = (255, 217, 51)
Q_B      = (115, 230, 115)
Q_A      = (255, 107, 97)
M_L, M_M, M_D = (206, 212, 224), (140, 148, 166), (82, 88, 108)
W_L, W_M, W_D = (186, 110, 62), (140, 74, 40), (94, 48, 28)
BRASS    = (230, 180, 70)

img = Image.new("RGB", (N, N))
px = img.load()

# Background: flat dark purple with a dithered glow behind the gun.
bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
for y in range(N):
    for x in range(N):
        px[x, y] = BG_BOT
        dx, dy = (x - 32) / 30, (y - 48) / 16
        d = dx * dx + dy * dy
        if d < 1 and (1 - d) * 24 > bayer[y % 4][x % 4]:
            px[x, y] = GLOW

def rect(d, x0, y0, x1, y1, c):
    d.rectangle([x0, y0, x1, y1], fill=c)

# ---- Sprite layer (gets a 1px dark outline, like the mod's text and sprites) ----
spr = Image.new("RGBA", (N, N), (0, 0, 0, 0))
d = ImageDraw.Draw(spr)

# Tooltip panel with a pointer down to the gun.
P0X, P0Y, P1X, P1Y = 6, 4, 57, 37
rect(d, P0X, P0Y, P1X, P1Y, BORDER)
rect(d, P0X + 1, P0Y + 1, P1X - 1, P1Y - 1, PANEL)
rect(d, P0X + 1, P0Y, P1X - 1, P0Y, BORDER_HI)          # top highlight
d.polygon([(28, P1Y), (35, P1Y), (31.5, P1Y + 4)], fill=BORDER)
d.polygon([(29, P1Y - 1), (34, P1Y - 1), (31.5, P1Y + 2)], fill=PANEL)
# Rounded corners.
for (cx, cy) in [(P0X, P0Y), (P1X, P0Y), (P0X, P1Y), (P1X, P1Y)]:
    d.point((cx, cy), fill=(0, 0, 0, 0))

# Chunky 5x7 glyphs drawn at 2x.
G = {
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "I": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
}
def glyph(ch, ox, oy, s, top, bot):
    for gy, row in enumerate(G[ch]):
        for gx, v in enumerate(row):
            if v == "#":
                c = top if gy < 4 else bot
                rect(d, ox + gx * s, oy + gy * s, ox + gx * s + s - 1, oy + gy * s + s - 1, c)

S = 2
word = "EID"
w = len(word) * 5 * S + (len(word) - 1) * 2 * S
ox = (N - w) // 2
for i, ch in enumerate(word):
    x = ox + i * (5 * S + 2 * S)
    # drop shadow
    for gy, row in enumerate(G[ch]):
        for gx, v in enumerate(row):
            if v == "#":
                rect(d, x + gx * S + 1, 9 + gy * S + 1, x + gx * S + S, 9 + gy * S + S, ACCENT_D)
    glyph(ch, x, 9, S, ACCENT, ACCENT)

# "Text" lines under the title: quality pips, a quote, a stat row and a description line.
y = 27
rect(d, 11, y, 12, y + 1, Q_S); rect(d, 15, y, 31, y + 1, QUOTE)
rect(d, 35, y, 39, y + 1, DIM);  rect(d, 41, y, 52, y + 1, DPS)
y = 31
rect(d, 11, y, 26, y + 1, TEXT)
rect(d, 29, y, 40, y + 1, OWNED)
rect(d, 43, y, 52, y + 1, TEXT)

# Revolver (original pixel art), facing right, sitting under the tooltip.
GUN = [
    "...dd.........................",
    "..dmmd....................l...",
    "..dmmmllllllllllllllllllllll..",
    "...dmmmmmmmmmmmmmmmmmmmmmmmmd.",
    "....dmddlmlmlmddddddddddddddd.",
    "....dmmdmdmdmdmd..............",
    "...dwwmlmlmlmlmd..............",
    "..dwwWdmdmdmdmd...............",
    ".dwwwWdddddddd................",
    ".dwwWWd.d..d..................",
    "dwwwWd..d..d..................",
    "dwwWWd...dd...................",
    "dwwWd.........................",
    "dwwWd.........................",
    ".ddd..........................",
]
COL = {"d": M_D, "m": M_M, "l": M_L, "w": W_L, "W": W_M}
gx0, gy0 = 18, 44
for yy, row in enumerate(GUN):
    for xx, v in enumerate(row):
        if v in COL:
            d.point((gx0 + xx, gy0 + yy), fill=COL[v])

# Outline every opaque sprite pixel (1px, 4-neighbour), drawn beneath the sprite.
a = spr.load()
out = Image.new("RGBA", (N, N), (0, 0, 0, 0))
o = out.load()
for yy in range(N):
    for xx in range(N):
        if a[xx, yy][3] == 0 and any(
            0 <= xx + ex < N and 0 <= yy + ey < N and a[xx + ex, yy + ey][3]
            for ex, ey in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1))
        ):
            o[xx, yy] = OUTLINE + (255,)
img = img.convert("RGBA")
img.alpha_composite(out)
img.alpha_composite(spr)

# Usage: python3 logo.py out.png [size]
size = int(sys.argv[2]) if len(sys.argv) > 2 else 256
img.resize((size, size), Image.NEAREST).convert("RGB").save(sys.argv[1])
