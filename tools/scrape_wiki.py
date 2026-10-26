"""Builds eid_data.json for the External Item Descriptions mod from the Enter the Gungeon wiki.

Usage:  python -I scrape_wiki.py [--cache DIR] [--out PATH]

Pulls the wikitext of the Guns, Items and Synergies pages from enterthegungeon.wiki.gg through the
MediaWiki API, parses their list tables, and maps every wiki name to Gungeon pickup IDs using
ids.txt (the ID/console-name table shipped with Mod the Gungeon API). Names that can't be mapped
are printed so they can be added to ALIASES below.
"""
import argparse
import html
import json
import os
import re
import sys
import unicodedata
import urllib.request
from datetime import date

WIKI = "https://enterthegungeon.wiki.gg"
HERE = os.path.dirname(os.path.abspath(__file__))

# wiki name (normalized) -> console names from ids.txt, for names that don't normalize to a match
ALIASES = {
    "masterround": ["master_round_1", "master_round_2", "master_round_3", "master_round_4", "master_round_5"],
    "masterroundi": ["master_round_1"],
    "masterroundii": ["master_round_2"],
    "masterroundiii": ["master_round_3"],
    "masterroundiv": ["master_round_4"],
    "masterroundv": ["master_round_5"],
    "alphabullet": ["alpha_bullets"],
    "infuriatingnote": ["infuriating_note_1", "infuriating_note_2", "infuriating_note_3",
                        "infuriating_note_4", "infuriating_note_5", "infuriating_note_6"],
    "rubeadynemkii": ["rube_adyne_mk2"],
    "serjunkan": ["junkan"],
    "shadessrevolver": ["shades_revolver"],
    "thompsonsubmachinegun": ["thompson"],
}


# ---------------------------------------------------------------- fetching

def fetch(page, cache):
    path = os.path.join(cache, f"wikigg-{page}.wikitext") if cache else None
    if path and os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return f.read()
    url = f"{WIKI}/api.php?action=parse&page={page}&prop=wikitext&format=json"
    req = urllib.request.Request(url, headers={"User-Agent": "GungeonEID-data-builder/1.0"})
    with urllib.request.urlopen(req, timeout=60) as r:
        text = json.load(r)["parse"]["wikitext"]["*"]
    if path:
        os.makedirs(cache, exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)
    return text


# ---------------------------------------------------------------- wikitext helpers

def split_top(s, sep):
    """Split s on sep, ignoring separators nested inside [[...]] or {{...}}."""
    parts, depth, cur, i = [], 0, [], 0
    while i < len(s):
        two = s[i:i + 2]
        if two in ("[[", "{{"):
            depth += 1; cur.append(two); i += 2; continue
        if two in ("]]", "}}"):
            depth = max(0, depth - 1); cur.append(two); i += 2; continue
        if depth == 0 and s.startswith(sep, i):
            parts.append("".join(cur)); cur = []; i += len(sep); continue
        cur.append(s[i]); i += 1
    parts.append("".join(cur))
    return parts


def templates(s, name):
    """Return the argument lists of every top-level {{name|...}} in s."""
    out, i = [], 0
    pat = re.compile(r"\{\{\s*" + re.escape(name) + r"\s*\|", re.I)
    while True:
        m = pat.search(s, i)
        if not m:
            return out
        depth, j = 0, m.start()
        while j < len(s):
            if s.startswith("{{", j): depth += 1; j += 2; continue
            if s.startswith("}}", j):
                depth -= 1; j += 2
                if depth == 0: break
                continue
            j += 1
        args = [re.sub(r"^\d+\s*=\s*", "", a.strip()) for a in split_top(s[m.end():j - 2], "|")]
        out.append(args)
        i = j


def render_template(body):
    args = [a.strip() for a in split_top(body, "|")]
    name = args[0].strip().lower()
    pos = [a for a in args[1:] if "=" not in a.split("|")[0]]
    if name in ("game", "check"):  # game-of-origin badges and checkmarks
        return ""
    if name == "infinity":
        return "Infinite"
    if name == "itemlist":
        return ", ".join(pos)
    if name in ("quality", "synergy", "hover", "color", "tooltip") and pos:
        return pos[0]
    if pos:
        return pos[0]
    return args[0].strip()


PUNCT = {"‘": "'", "’": "'", "“": '"', "”": '"', "–": "-", "—": "-",
         "…": "...", "×": "x", " ": " ", "‎": "", "∞": "Infinite", "°": " degrees"}


def to_ascii(s):
    """The game's pixel font only has ASCII glyphs."""
    for k, v in PUNCT.items():
        s = s.replace(k, v)
    s = unicodedata.normalize("NFKD", s)
    return s.encode("ascii", "ignore").decode("ascii")


def clean(s):
    """Convert a wikitext fragment to plain text."""
    s = re.sub(r"<ref[^>]*/>", "", s)
    s = re.sub(r"<ref[^>]*>.*?</ref>", "", s, flags=re.S)
    s = re.sub(r"<!--.*?-->", "", s, flags=re.S)
    # templates, innermost first
    for _ in range(10):
        new = re.sub(r"\{\{([^{}]*)\}\}", lambda m: render_template(m.group(1)), s)
        if new == s:
            break
        s = new
    s = re.sub(r"\[\[(?:File|Image):[^\[\]]*(?:\[\[[^\]]*\]\][^\[\]]*)*\]\]", "", s, flags=re.I)
    s = re.sub(r"\[\[([^\]|]*)\|([^\]]*)\]\]", r"\2", s)
    s = re.sub(r"\[\[([^\]]*)\]\]", r"\1", s)
    s = re.sub(r"\[https?://\S+\s+([^\]]*)\]", r"\1", s)
    s = re.sub(r"<br\s*/?>", "\n", s, flags=re.I)
    s = re.sub(r"</?[a-zA-Z][^<>]*>", "", s)  # HTML tags only, so text like "<3--<<<" survives
    s = re.sub(r"'''?", "", s)
    s = to_ascii(html.unescape(s))
    s = re.sub(r"[ \t]+", " ", s)
    s = re.sub(r"\s*\n\s*", "\n", s)
    return s.strip()


def one_line(s):
    return re.sub(r"\s*\n\s*", " ", s).strip()


def strip_attrs(cell):
    """Drop a leading 'attr="x" |' section from a table cell."""
    parts = split_top(cell, "|")
    while len(parts) > 1 and re.search(r"^\s*[\w-]+\s*=", parts[0]):
        parts = parts[1:]
    return "|".join(parts)


def parse_tables(text):
    """Yield the rows (lists of raw cell strings, header rows skipped) of every table in text."""
    tables, rows, row, cur = [], None, None, None
    for line in text.split("\n"):
        st = line.strip()
        if st.startswith("{|"):
            rows, row = [], None
            continue
        if rows is None:
            continue
        if st.startswith("|}"):
            if row is not None: rows.append(row)
            tables.append([r for r in rows if r])
            rows = None
            continue
        if st.startswith("|-"):
            if row is not None: rows.append(row)
            row = []
            continue
        if st.startswith("!"):
            row = None  # header row: ignore
            continue
        if row is None:
            continue
        if st.startswith("|"):
            body = st[2:] if st.startswith("||") else st[1:]
            for c in split_top(body, "||"):
                row.append(strip_attrs(c).rstrip().rstrip("|"))
        elif row:
            row[-1] += "\n" + line
    return tables


def biggest(tables):
    return max(tables, key=len)


# ---------------------------------------------------------------- name -> id

def norm(name):
    return re.sub(r"[^a-z0-9]", "", name.lower())


def load_ids():
    ids = {}
    with open(os.path.join(HERE, "ids.txt"), encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            pid, cname = line.split(" ", 1)
            ids[cname] = int(pid)
    by_norm = {}
    for cname, pid in ids.items():
        if "+" in cname and cname != "+1_bullets":
            continue  # synergy forms of guns
        by_norm.setdefault(norm(cname), []).append(pid)
    return ids, by_norm


def resolve(name, ids, by_norm, missing):
    n = norm(name)
    if n in ALIASES:
        return [ids[c] for c in ALIASES[n] if c in ids]
    if n in by_norm:
        return sorted(by_norm[n])
    # wiki often prefixes "The"; console names usually keep it, but not always
    if n.startswith("the") and n[3:] in by_norm:
        return sorted(by_norm[n[3:]])
    if "the" + n in by_norm:
        return sorted(by_norm["the" + n])
    missing.add(name)
    return []


# ---------------------------------------------------------------- pages

# The name cell holds the name, then the quote on a second line: [[Name]]<br><small>''"Quote"''</small>
GUN_COLS = ["icon", "name", "notes", "quality", "type", "dps", "magazine", "ammo", "damage",
            "fireRate", "reload", "shotSpeed", "range", "force", "spread", "gunClass", "unlockable"]
GUN_STATS = GUN_COLS[5:16]
ITEM_COLS = ["icon", "name", "type", "quote", "quality", "effect"]


def parse_guns(text, ids, by_norm, missing, warn):
    out = []
    for raw in biggest(parse_tables(text)):
        if len(raw) == len(GUN_COLS) - 1:
            raw = raw + [""]  # some rows leave out the last (Unlockable) cell
        if len(raw) != len(GUN_COLS):
            warn.append(f"Guns: row with {len(raw)} cells: {clean(raw[1] if len(raw) > 1 else raw[0])[:40]!r}")
            continue
        g = {k: clean(v) for k, v in zip(GUN_COLS, raw)}
        del g["icon"], g["unlockable"]
        name, _, quote = g["name"].partition("\n")
        g["name"] = name.strip()
        g["quote"] = one_line(quote).strip('"')
        g["notes"] = one_line(g["notes"])
        for k in GUN_STATS:
            g[k] = re.sub(r"\n", " / ", g[k])
        g["ids"] = resolve(g["name"], ids, by_norm, missing)
        out.append(g)
    return out


def parse_items(text, ids, by_norm, missing, warn):
    out = []
    for raw in biggest(parse_tables(text)):
        if len(raw) != len(ITEM_COLS):
            warn.append(f"Items: row with {len(raw)} cells: {clean(raw[1] if len(raw) > 1 else raw[0])[:40]!r}")
            continue
        it = {k: clean(v) for k, v in zip(ITEM_COLS, raw)}
        del it["icon"]
        for k in it:
            it[k] = one_line(it[k]) if k != "effect" else it[k]
        it["ids"] = resolve(it["name"], ids, by_norm, missing)
        out.append(it)
    # Master Round I..V share a name; keep one entry
    seen, dedup = set(), []
    for it in out:
        if it["name"] in seen:
            continue
        seen.add(it["name"]); dedup.append(it)
    return dedup


NUMBER_WORDS = {"one": 1, "two": 2, "three": 3, "four": 4, "five": 5}


def parse_synergies(text, ids, by_norm, missing, warn):
    out = []
    # The page also has a table of internal synergy codes; use the one whose rows list items.
    tables = parse_tables(text)
    synergy_table = max(tables, key=lambda t: sum(1 for row in t if any(templates(c, "ItemList") for c in row)))
    for raw in synergy_table:
        cells = list(raw)
        if len(cells) < 3:
            warn.append(f"Synergies: short row {cells!r:.80}")
            continue
        name = one_line(clean(cells[0])) or "(Unnamed synergy)"
        # cells: name, items (1 or 2 cells), effect, [sprite]
        item_cells = [c for c in cells[1:] if templates(c, "ItemList")]
        rest = [c for c in cells[1:] if not templates(c, "ItemList")]
        effect_cells = [c for c in rest if clean(c)]
        if not item_cells or not effect_cells:
            warn.append(f"Synergies: could not split row {name!r}")
            continue
        mandatory, optional, need = [], [], 0
        for c in item_cells:
            lists = [to_ascii(n) for args in templates(c, "ItemList") for n in args if n]
            m = re.search(r"(\w+) of the following", c, re.I)
            if m:
                optional += lists
                need = NUMBER_WORDS.get(m.group(1).lower(), 1)
            else:
                mandatory += lists
        sy = {
            "name": name,
            "effect": clean(effect_cells[0]),
            "mandatory": [{"name": n, "ids": resolve(n, ids, by_norm, missing)} for n in mandatory],
            "optional": [{"name": n, "ids": resolve(n, ids, by_norm, missing)} for n in optional],
            "optionalNeeded": need,
        }
        out.append(sy)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", help="directory to cache/reuse downloaded wikitext")
    ap.add_argument("--out", default=os.path.join(HERE, "..", "data", "eid_data.json"))
    a = ap.parse_args()

    ids, by_norm = load_ids()
    missing, warn = set(), []
    guns = parse_guns(fetch("Guns", a.cache), ids, by_norm, missing, warn)
    items = parse_items(fetch("Items", a.cache), ids, by_norm, missing, warn)
    synergies = parse_synergies(fetch("Synergies", a.cache), ids, by_norm, missing, warn)

    data = {
        "source": "https://enterthegungeon.wiki.gg/wiki/ (Guns, Items, Synergies) - CC BY-SA",
        "fetched": date.today().isoformat(),
        "guns": guns,
        "items": items,
        "synergies": synergies,
    }
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)

    print(f"guns={len(guns)} items={len(items)} synergies={len(synergies)} -> {os.path.abspath(a.out)}")
    for w in warn:
        print("WARN", w)
    for n in sorted(missing):
        print("UNMATCHED", n)


if __name__ == "__main__":
    sys.exit(main())
