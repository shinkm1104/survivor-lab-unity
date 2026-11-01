"""Procedural pixel art for Survivor Lab.

Every image is drawn by this script from hand-written 16x16 templates and seeded noise.
The script uses hand-authored pixel templates and deterministic noise.
Outputs go to Assets/StreamingAssets/Art so the built game can load (and mod) them at runtime.

Run: python -I ArtSource/generate_art.py
"""
import json
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets" / "StreamingAssets" / "Art"
SCALE = 2  # 16px templates -> 32px textures
TILE = 32  # background pixels per world unit
SEED = 20261101


def hexrgb(value, alpha=255):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def render(rows, palette, scale=SCALE):
    width = len(rows[0])
    assert all(len(r) == width for r in rows), [len(r) for r in rows]
    img = Image.new("RGBA", (width, len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), palette[ch])
    return img.resize((width * scale, len(rows) * scale), Image.NEAREST)


def mirror(half_rows):
    return [r + r[::-1] for r in half_rows]


def bob(rows):
    """Second animation frame: whole sprite one pixel higher (row 0 must be empty)."""
    assert set(rows[0]) == {"."}
    return rows[1:] + ["." * len(rows[0])]


def save(img, *parts):
    path = OUT.joinpath(*parts)
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    return path


# ---------------------------------------------------------------- characters
WIZARD = [
    "................",
    ".......oo.......",
    "......oHHo......",
    ".....oHHhHo.....",
    "....oHHHHhho....",
    "...ooBBBBBBoo...",
    "....osssssso.og.",
    "....ossseseo.oGo",
    "....osssssso..w.",
    "...oRRRRRRRRo.w.",
    "..oRRRARRRRrRow.",
    "..osRRARRRRrRsw.",
    "...oRRARRRRrro..",
    "...oRRARRRRrro..",
    "....obbo.obbo...",
    "....ooo...ooo...",
]
WIZARD_STEP = WIZARD[:14] + [
    "...obbo...obbo..",
    "...ooo.....ooo..",
]
CLASSES = {
    # robe, robe shade, hat, hat shade, band/trim accent, staff gem
    "pulse": ("#2bb3a3", "#1b7a73", "#f2c14e", "#c8922d", "#fff1a8", "#ffd34d"),
    "orb": ("#4a5bd6", "#2f3a96", "#7b4fd6", "#55349e", "#b9c6ff", "#9fe6ff"),
    "trail": ("#7a8f3a", "#55662a", "#8a5a33", "#5f3d22", "#d9c08a", "#c9a06a"),
    "slam": ("#c2413b", "#86292a", "#5b3a80", "#3e2759", "#f7a35c", "#ff7a3d"),
}


def wizard_palette(robe, robe_shade, hat, hat_shade, accent, gem):
    return {
        "o": hexrgb("#1a1424"), "H": hexrgb(hat), "h": hexrgb(hat_shade), "B": hexrgb(accent),
        "s": hexrgb("#f1c7a0"), "e": hexrgb("#1a1424"), "R": hexrgb(robe), "r": hexrgb(robe_shade),
        "A": hexrgb(accent), "w": hexrgb("#7a4e2b"), "g": hexrgb(gem), "G": hexrgb("#ffffff"),
        "b": hexrgb("#3b2a20"),
    }


# ---------------------------------------------------------------- monsters
SLIME = [
    "................",
    "................",
    "................",
    "................",
    "......oooo......",
    "....ooGGGGoo....",
    "...oGGGGGGGGo...",
    "..oGLLGGGGGGGo..",
    "..oGLGGGGGGGGo..",
    ".oGGGGeGGGeGGGo.",
    ".oGGGGeGGGeGGGo.",
    ".oGGGGGGGGGGGGo.",
    ".oGGGGGmmmGGGgo.",
    ".ogGGGGGGGGGggo.",
    "..oggggggggggo..",
    "...oooooooooo...",
]
SLIME_SQUASH = [
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    ".....oooooo.....",
    "...ooGGGGGGoo...",
    "..oGLLGGGGGGGo..",
    ".oGLGGeGGGeGGGo.",
    "oGGGGGeGGGeGGGGo",
    "oGGGGGGGGGGGGGGo",
    "oGGGGGGmmmGGGGgo",
    "ogGGGGGGGGGGGggo",
    ".oggggggggggggo.",
    "..oooooooooooo..",
]
BAT_UP = mirror([
    "........",
    "........",
    "o.......",
    "oo....o.",
    "oWo..oPo",
    "oWWooPPP",
    "oWWWoPeP",
    "oWWWWPPP",
    "oWWWoPPm",
    "oWoWoPPP",
    "oo.o.oPP",
    "......oo",
    "........",
    "........",
    "........",
    "........",
])
BAT_DOWN = mirror([
    "........",
    "........",
    "........",
    "......o.",
    ".....oPo",
    "....oPPP",
    "oooooPeP",
    "oWWWWPPP",
    "oWWWWPPm",
    ".oWWWoPP",
    "..oWWoPP",
    "...oo.oo",
    "........",
    "........",
    "........",
    "........",
])
GHOST = [
    "................",
    "................",
    ".....oooooo.....",
    "....oWWWWWWo....",
    "...oWWWWWWWWo...",
    "..oWWWWWWWWWWo..",
    "..oWWeeWWeeWWo..",
    "..oWWeeWWeeWWo..",
    "..oWWWWWWWWWWo..",
    "..oWWWWmmWWWWo..",
    "..oWWWWWWWWWWo..",
    "..oWWWWWWWWWSSo.",
    "..oWWWWWWWWWSSo.",
    "..oWSWWSWWSWWSo.",
    "..oo.oo.oo.oo.o.",
    "................",
]
GHOST_WAVE = GHOST[:13] + [
    "..oWWSWWSWWSWSo.",
    "...oo.oo.oo.oo..",
    "................",
]
MONSTERS = {
    "slime": ([SLIME, SLIME_SQUASH], {
        "o": hexrgb("#14261a"), "G": hexrgb("#5fcf5a"), "g": hexrgb("#379a3f"), "L": hexrgb("#c8ffb8"),
        "e": hexrgb("#14261a"), "m": hexrgb("#1f4d2a")}),
    "bat": ([BAT_UP, BAT_DOWN], {
        "o": hexrgb("#1b1028"), "W": hexrgb("#5a3a7a"), "P": hexrgb("#8e5bb8"), "e": hexrgb("#ff5a5a"),
        "m": hexrgb("#f0e6ff")}),
    "ghost": ([GHOST, GHOST_WAVE], {
        "o": hexrgb("#2a2440"), "W": hexrgb("#e6e9ff"), "S": hexrgb("#a9b0e0"), "e": hexrgb("#2a2440"),
        "m": hexrgb("#6c6a9a")}),
}

# ---------------------------------------------------------------- pickups and effects
XP_GEM = [
    "................",
    "................",
    ".......oo.......",
    "......oLLo......",
    ".....oLBBBo.....",
    "....oLBBBBBo....",
    "...oLBBBBBBDo...",
    "..oLBBBBBBBDDo..",
    "..oBBBBBBBBDDo..",
    "...oBBBBBBDDo...",
    "....oBBBBDDo....",
    ".....oBBDDo.....",
    "......oDDo......",
    ".......oo.......",
    "................",
    "................",
]
POTION = [
    "................",
    "......oooo......",
    "......oCCo......",
    ".....oooooo.....",
    "......oGGo......",
    "......oGGo......",
    ".....oGGGGo.....",
    "....oGLGGGGo....",
    "...oGLGGGGGGo...",
    "...oGLGGGGGgo...",
    "...oGGGGGGGgo...",
    "...oGGGGGGggo...",
    "....oGGGGggo....",
    ".....oooooo.....",
    "................",
    "................",
]
ROCK = [
    "................",
    "................",
    "................",
    "................",
    "......oooo......",
    "....ooLLSSoo....",
    "...oLLSSSSSSo...",
    "..oLSSSSSSSDDo..",
    "..oSSSSDSSSSDo..",
    ".oSSSSSSSSSSDDo.",
    ".oSSDSSSSSSDDDo.",
    ".oDSSSSSSSDDDDo.",
    "..oDDDDDDDDDDo..",
    "...oooooooooo...",
    "................",
    "................",
]
BOLT = [  # grayscale so the game can tint it with the active element colour
    "................",
    "................",
    "................",
    "................",
    "......aaaa......",
    ".....abbbba.....",
    "....abccccba....",
    "....abcWWcba....",
    "....abcWWcba....",
    "....abccccba....",
    ".....abbbba.....",
    "......aaaa......",
    "................",
    "................",
    "................",
    "................",
]
ITEMS = {
    "xp": (XP_GEM, {"o": hexrgb("#10204a"), "L": hexrgb("#d6ecff"), "B": hexrgb("#4f9dff"), "D": hexrgb("#2a5fc4")}),
    "potion": (POTION, {"o": hexrgb("#1a1424"), "C": hexrgb("#9a6a3a"), "G": hexrgb("#5fe07a"),
                        "g": hexrgb("#2f9a48"), "L": hexrgb("#e4ffe6")}),
    "rock": (ROCK, {"o": hexrgb("#2b2118"), "L": hexrgb("#c9b08a"), "S": hexrgb("#9c8462"), "D": hexrgb("#6d5a42")}),
    "bolt": (BOLT, {"a": hexrgb("#ffffff", 70), "b": hexrgb("#ffffff", 150), "c": hexrgb("#ffffff", 220),
                    "W": hexrgb("#ffffff")}),
}


def shadow():
    img = Image.new("RGBA", (32, 16), (0, 0, 0, 0))
    for y in range(16):
        for x in range(32):
            d = ((x - 15.5) / 15.5) ** 2 + ((y - 7.5) / 7.5) ** 2
            if d <= 1:
                img.putpixel((x, y), (0, 0, 0, int(110 * (1 - d * 0.6))))
    return img


# ---------------------------------------------------------------- background
GRASS = ["#3f6b3a", "#447340", "#3b6436", "#4a7a44"]
DIRT = ["#8a6a45", "#7f6140", "#93724b", "#76593a"]
STONE = ["#7a7f86", "#6e737a", "#858a91"]
WALL = ["#3a3440", "#433c4a", "#342f3a"]


def tile_image(kind, rng):
    img = Image.new("RGBA", (16, 16))
    base = {"grass": GRASS, "dirt": DIRT, "stone": STONE, "wall": WALL}[kind]
    for y in range(16):
        for x in range(16):
            img.putpixel((x, y), hexrgb(rng.choice(base[:2]) if rng.random() < 0.85 else rng.choice(base)))
    if kind == "grass":
        for _ in range(rng.randint(2, 5)):  # grass tufts
            x, y = rng.randint(1, 14), rng.randint(2, 14)
            img.putpixel((x, y), hexrgb("#5c9150")); img.putpixel((x, y - 1), hexrgb("#6ea85e"))
    elif kind == "dirt":
        for _ in range(rng.randint(1, 3)):
            img.putpixel((rng.randint(0, 15), rng.randint(0, 15)), hexrgb("#5e4630"))
    elif kind == "stone":  # flagstone seams
        for i in range(16):
            img.putpixel((i, 0), hexrgb("#55595f")); img.putpixel((0, i), hexrgb("#55595f"))
            img.putpixel((i, 8), hexrgb("#5d6168")) if i < 8 else None
    elif kind == "wall":  # brick courses
        for x in range(16):
            for y in (0, 5, 10):
                img.putpixel((x, y), hexrgb("#1f1b24"))
        for y in range(16):
            for x in ((4, 12) if (y // 5) % 2 else (0, 8)):
                img.putpixel((x, y), hexrgb("#1f1b24"))
    return img.resize((TILE, TILE), Image.NEAREST)


DECOR = {
    "flower_red": ([
        "........", "..r.r...", ".rrYrr..", "..rrr...", "...g....", "..gg....", "...g....", "........"],
        {"r": hexrgb("#e0566a"), "Y": hexrgb("#ffe066"), "g": hexrgb("#2f6a2c")}),
    "flower_blue": ([
        "........", "...b.b..", "..bbYbb.", "...bbb..", "....g...", "....gg..", "....g...", "........"],
        {"b": hexrgb("#7ab6ff"), "Y": hexrgb("#fff3b0"), "g": hexrgb("#2f6a2c")}),
    "mushroom": ([
        "........", "..oooo..", ".oRRwRo.", "oRwRRRRo", "oooooooo", "...ww...", "...ww...", "..oooo.."],
        {"o": hexrgb("#2b1a1a"), "R": hexrgb("#c8423e"), "w": hexrgb("#f4ead8")}),
    "pebbles": ([
        "........", "........", ".oo.....", "oSSo..o.", ".oo..oSo", "......o.", "..oo....", ".oSSo..."],
        {"o": hexrgb("#3a342c"), "S": hexrgb("#a69a86")}),
    "bones": ([
        "........", "w......w", "ww....ww", "..wwww..", "..wwww..", "ww....ww", "w......w", "........"],
        {"w": hexrgb("#e8e2cc")}),
    "bush": ([
        "..oooo..", ".oGGGGo.", "oGLGGGGo", "oGGGGgGo", "oGGGggGo", ".oggggo.", "..oooo..", "........"],
        {"o": hexrgb("#1c3a1c"), "G": hexrgb("#4f8f45"), "g": hexrgb("#3a6d34"), "L": hexrgb("#7cc06a")}),
}


def build_background(half_extent):
    rng = random.Random(SEED)
    border = 4
    size = (half_extent + border) * 2
    origin = -(half_extent + border)
    tiles = [["grass"] * size for _ in range(size)]

    def world(i):
        return origin + i + 0.5

    for j in range(size):
        for i in range(size):
            x, y = world(i), world(j)
            if abs(x) > half_extent or abs(y) > half_extent:
                tiles[j][i] = "wall"
            elif x * x + y * y < 5.5 ** 2:
                tiles[j][i] = "stone"  # central ruin plaza = player spawn
            elif abs(x) < 1.6 or abs(y) < 1.6:
                tiles[j][i] = "dirt"  # cross roads
    for _ in range(14):  # dirt patches
        cx, cy, r = rng.uniform(-28, 28), rng.uniform(-28, 28), rng.uniform(1.5, 3.5)
        for j in range(size):
            for i in range(size):
                x, y = world(i), world(j)
                if tiles[j][i] == "grass" and (x - cx) ** 2 + (y - cy) ** 2 < r * r * rng.uniform(0.7, 1.0):
                    tiles[j][i] = "dirt"

    variants = {k: [tile_image(k, rng) for _ in range(4)] for k in ("grass", "dirt", "stone", "wall")}
    img = Image.new("RGBA", (size * TILE, size * TILE))
    for j in range(size):
        for i in range(size):
            # Image rows run top-down while world y runs up, so row 0 is the northernmost tile.
            img.paste(rng.choice(variants[tiles[j][i]]), (i * TILE, (size - 1 - j) * TILE))

    decorations = []
    kinds = list(DECOR)
    while len(decorations) < 170:
        x, y = rng.uniform(-half_extent + 0.6, half_extent - 0.6), rng.uniform(-half_extent + 0.6, half_extent - 0.6)
        i, j = int(x - origin), int(y - origin)
        if tiles[j][i] == "stone":
            continue
        kind = rng.choice(kinds[:4] if tiles[j][i] == "grass" else ["pebbles", "bones"])
        if kind == "bush" or rng.random() < 0.15:
            kind = "bush" if tiles[j][i] == "grass" else kind
        decorations.append({"type": kind, "x": round(x, 2), "y": round(y, 2)})
    for d in decorations:
        rows, palette = DECOR[d["type"]]
        sprite = render(rows, palette, scale=3)
        px = int((d["x"] - origin) * TILE - sprite.width / 2)
        py = int((size - (d["y"] - origin)) * TILE - sprite.height / 2)
        img.alpha_composite(sprite, (px, py))
    # Plaza ring: darker flagstones on the spawn circle edge.
    for j in range(size):
        for i in range(size):
            x, y = world(i), world(j)
            if 4.6 ** 2 < x * x + y * y < 5.5 ** 2:
                ring = Image.new("RGBA", (TILE, TILE), (40, 36, 48, 70))
                img.alpha_composite(ring, (i * TILE, (size - 1 - j) * TILE))

    legend = {"grass": 0, "dirt": 1, "stone": 2, "wall": 3}
    data = {
        "version": 1,
        "note": "Generated by ArtSource/generate_art.py (procedural, seed %d). Visual only: the simulation does not read obstacles." % SEED,
        "seed": SEED,
        "tileSize": TILE,
        "width": size,
        "height": size,
        "originX": origin,
        "originY": origin,
        "arenaHalfExtent": half_extent,
        "image": "background/arena.png",
        "legend": list(legend),
        "rows": ["".join(str(legend[t]) for t in row) for row in tiles],
        "decorations": decorations,
    }
    return img, data


def main():
    balance = json.loads((ROOT / "Assets" / "StreamingAssets" / "balance.json").read_text(encoding="utf-8"))
    sheet = []
    for cls, colors in CLASSES.items():
        palette = wizard_palette(*colors)
        for f, rows in enumerate([WIZARD, WIZARD_STEP]):
            sheet.append(save(render(rows, palette), "characters", f"{cls}_{f}.png"))
    for name, (frames, palette) in MONSTERS.items():
        for f, rows in enumerate(frames):
            sheet.append(save(render(rows, palette), "monsters", f"{name}_{f}.png"))
    for name, (rows, palette) in ITEMS.items():
        sheet.append(save(render(rows, palette), "items", f"{name}.png"))
    save(shadow(), "items", "shadow.png")

    background, data = build_background(int(balance["arenaHalfExtent"]))
    save(background, "background", "arena.png")
    (OUT / "background" / "background-map.json").write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")

    # Contact sheet for documentation (not loaded by the game).
    cell = 96
    preview = Image.new("RGBA", (cell * 8, cell * 3 + 256), hexrgb("#16202c"))
    for n, path in enumerate(sheet):
        sprite = Image.open(path).resize((cell - 16, cell - 16), Image.NEAREST)
        preview.alpha_composite(sprite, ((n % 8) * cell + 8, (n // 8) * cell + 8))
    crop = background.crop((background.width // 2 - 512, background.height // 2 - 128,
                            background.width // 2 + 256, background.height // 2 + 128))
    preview.alpha_composite(crop, (0, cell * 3))
    preview.save(ROOT / "ArtSource" / "preview.png")
    print("sprites", len(sheet), "background", background.size, "decorations", len(data["decorations"]))


if __name__ == "__main__":
    main()
