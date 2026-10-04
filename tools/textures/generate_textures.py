#!/usr/bin/env python3
"""Generates the tileable environment textures of Poing Mort (matte, warm, desaturated palette).

All textures are procedural and created for the project (no third-party image).
Usage: python3 tools/textures/generate_textures.py
Output: Assets/PoingMort/Art/Textures/T_*.png (albedo) and T_*_N.png (normal maps)
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "PoingMort", "Art", "Textures")
FONT_DIR = "/usr/share/fonts"
rng = np.random.default_rng(20261004)


def font(name, size):
    paths = {
        "bebas": "opentype/bebas-neue/BebasNeue-Bold.otf",
        "inter": "opentype/inter/Inter-Bold.otf",
        "inter_reg": "opentype/inter/Inter-Medium.otf",
        "marker": "truetype/yusei-magic/YuseiMagic-Regular.ttf",
        "spartan": "opentype/league-spartan/LeagueSpartan-Black.otf",
    }
    return ImageFont.truetype(os.path.join(FONT_DIR, paths[name]), size)


def tileable_noise(size, scale, octaves=4, persistence=0.5):
    """Fractal value noise that tiles (wrap-around interpolation)."""
    total = np.zeros((size, size), dtype=np.float32)
    amp, norm = 1.0, 0.0
    freq = scale
    for _ in range(octaves):
        g = rng.random((freq, freq)).astype(np.float32)
        x = np.linspace(0, freq, size, endpoint=False)
        xi = np.floor(x).astype(int)
        xf = x - xi
        xf = xf * xf * (3 - 2 * xf)
        x0, x1 = xi % freq, (xi + 1) % freq
        a = g[np.ix_(x0, x0)]
        b = g[np.ix_(x0, x1)]
        c = g[np.ix_(x1, x0)]
        d = g[np.ix_(x1, x1)]
        fy = xf[:, None]
        fx = xf[None, :]
        layer = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
        total += layer * amp
        norm += amp
        amp *= persistence
        freq *= 2
    return total / norm


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    img.save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT), img.size)


def to_img(arr):
    return Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8))


def normal_from_height(h, strength=2.0):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    n = np.stack([-dx, dy, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return to_img(n * 0.5 + 0.5)


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


# --------------------------------------------------------------------------- bricks

def bricks(name, base, mortar, rows=32, cols=8, size=1024, variation=0.12, soot=0.25):
    """Running bond bricks. One tile = 2 m wide (cols bricks of 25 cm) x 2.4 m high (rows of 7.5 cm)."""
    img = np.zeros((size, size, 3), dtype=np.float32)
    height = np.zeros((size, size), dtype=np.float32)
    bh = size / rows
    bw = size / cols
    m = max(2, int(size / 180))
    noise = tileable_noise(size, 16, 4)
    grain = tileable_noise(size, 128, 2)
    # Mostly luminance variation, with a very small warm/cool shift: desaturated bricks.
    lum = rng.normal(0, variation, (rows, cols + 1, 1)).astype(np.float32)
    hue = rng.normal(0, variation * 0.18, (rows, cols + 1, 1)).astype(np.float32) * np.array([1.0, 0.2, -0.8], dtype=np.float32)
    tones = lum + hue
    for r in range(rows):
        y0, y1 = int(r * bh), int((r + 1) * bh)
        offset = (bw / 2) if r % 2 else 0
        for c in range(-1, cols + 1):
            x0 = int(c * bw + offset)
            x1 = int((c + 1) * bw + offset)
            tone = base * (1 + tones[r, c % (cols + 1)])
            xs = np.arange(x0, x1) % size
            img[y0:y1, :][:, xs] = tone
            height[y0:y1, :][:, xs] = 1.0
            # mortar joints
            img[y0:y0 + m, :][:, xs] = mortar
            height[y0:y0 + m, :][:, xs] = 0.0
            xm = np.arange(x0, x0 + m) % size
            img[y0:y1, :][:, xm] = mortar
            height[y0:y1, :][:, xm] = 0.0
    img *= (0.88 + 0.24 * noise)[..., None]
    img *= (0.95 + 0.1 * grain)[..., None]
    # Soot and wash streaks (darker toward the top and in vertical streaks)
    streak = tileable_noise(size, 6, 3)
    img *= (1 - soot * np.clip(streak - 0.45, 0, 1))[..., None]
    save(to_img(img), name + ".png")
    save(normal_from_height(height * 0.6 + grain * 0.4, 3.0), name + "_N.png")


# --------------------------------------------------------------------------- simple materials

def stucco(name, base, size=1024, dirt=0.18):
    n1 = tileable_noise(size, 8, 5)
    n2 = tileable_noise(size, 64, 3)
    img = base[None, None, :] * (0.9 + 0.2 * n1[..., None]) * (0.96 + 0.08 * n2[..., None])
    stains = tileable_noise(size, 4, 3)
    img *= (1 - dirt * np.clip(stains - 0.5, 0, 1) * 2)[..., None]
    save(to_img(img), name + ".png")
    save(normal_from_height(n2, 1.2), name + "_N.png")


def concrete(name, base, size=1024):
    n1 = tileable_noise(size, 6, 5)
    n2 = tileable_noise(size, 96, 2)
    pits = (rng.random((size, size)) > 0.996).astype(np.float32)
    pits = np.asarray(Image.fromarray((pits * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)), dtype=np.float32) / 255
    img = base[None, None, :] * (0.88 + 0.22 * n1[..., None]) * (0.95 + 0.1 * n2[..., None]) * (1 - 0.35 * pits[..., None])
    save(to_img(img), name + ".png")
    save(normal_from_height(n2 * 0.7 - pits, 1.5), name + "_N.png")


def asphalt(name, size=1024):
    base = hex_rgb("#3b3b3d")
    n1 = tileable_noise(size, 4, 4)
    n2 = tileable_noise(size, 160, 2)
    agg = rng.random((size, size)).astype(np.float32)
    agg = (agg > 0.93) * rng.random((size, size)).astype(np.float32) * 0.35
    img = base[None, None, :] * (0.85 + 0.3 * n1[..., None]) * (0.92 + 0.16 * n2[..., None]) + agg[..., None] * 0.25
    # Tar patches
    patch = tileable_noise(size, 3, 2)
    img *= (1 - 0.18 * np.clip((patch - 0.62) * 6, 0, 1))[..., None]
    save(to_img(img), name + ".png")
    save(normal_from_height(n2 + agg, 1.0), name + "_N.png")


def slabs(name, base, slabs_per_tile=2, size=1024):
    """Sidewalk concrete slabs (1 m) with joints. One tile = 2 m."""
    n1 = tileable_noise(size, 8, 4)
    n2 = tileable_noise(size, 96, 2)
    img = base[None, None, :] * (0.88 + 0.22 * n1[..., None]) * (0.95 + 0.1 * n2[..., None])
    height = np.ones((size, size), dtype=np.float32)
    step = size // slabs_per_tile
    tones = rng.normal(0, 0.05, (slabs_per_tile, slabs_per_tile))
    for i in range(slabs_per_tile):
        for j in range(slabs_per_tile):
            img[i * step:(i + 1) * step, j * step:(j + 1) * step] *= (1 + tones[i, j])
    j = max(3, size // 220)
    for k in range(slabs_per_tile):
        img[k * step:k * step + j, :] *= 0.6
        img[:, k * step:k * step + j] *= 0.6
        height[k * step:k * step + j, :] = 0
        height[:, k * step:k * step + j] = 0
    save(to_img(img), name + ".png")
    save(normal_from_height(height * 0.5 + n2 * 0.5, 2.0), name + "_N.png")


def roller_door(name, size=512):
    base = hex_rgb("#7c7a76")
    img = np.zeros((size, size, 3), dtype=np.float32)
    height = np.zeros((size, size), dtype=np.float32)
    ribs = 16
    for y in range(size):
        t = (y % (size // ribs)) / (size / ribs)
        shade = 0.82 + 0.25 * math.sin(t * math.pi)
        img[y, :] = base * shade
        height[y, :] = math.sin(t * math.pi)
    n = tileable_noise(size, 8, 3)
    img *= (0.9 + 0.2 * n)[..., None]
    rust = tileable_noise(size, 5, 3)
    img = img * (1 - 0.3 * np.clip(rust - 0.6, 0, 1)[..., None]) + np.clip(rust - 0.6, 0, 1)[..., None] * hex_rgb("#6b3f22") * 0.6
    save(to_img(img), name + ".png")
    save(normal_from_height(height, 2.5), name + "_N.png")


def foliage(name, size=512):
    n1 = tileable_noise(size, 12, 4)
    n2 = tileable_noise(size, 48, 3)
    dark, light = hex_rgb("#2f3d24"), hex_rgb("#5d6b39")
    t = np.clip(n1 * 0.7 + n2 * 0.5 - 0.15, 0, 1)
    img = dark * (1 - t[..., None]) + light * t[..., None]
    # warm sunset tint on highlights
    img = img * (1 + 0.08 * np.clip(n2 - 0.6, 0, 1)[..., None] * np.array([1.4, 1.0, 0.6]))
    save(to_img(img), name + ".png")


def bark(name, size=256):
    n = tileable_noise(size, 6, 4)
    stripes = np.sin(np.linspace(0, 18 * math.pi, size))[None, :] * 0.5 + 0.5
    img = hex_rgb("#4a3a2e") * (0.75 + 0.3 * (n * 0.6 + stripes * 0.4))[..., None]
    save(to_img(img), name + ".png")


# --------------------------------------------------------------------------- signs and windows

def sign(name, text, bg, fg, w=1024, h=256, font_name="bebas", sub=None, accent=None):
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    border = max(6, h // 24)
    d.rectangle([border, border, w - border - 1, h - border - 1], outline=fg if accent is None else accent, width=border // 2)
    f = font(font_name, int(h * (0.62 if sub is None else 0.52)))
    tw = d.textlength(text, font=f)
    y = h * (0.16 if sub is None else 0.1)
    d.text(((w - tw) / 2, y), text, font=f, fill=fg)
    if sub:
        f2 = font("inter", int(h * 0.17))
        sw = d.textlength(sub, font=f2)
        d.text(((w - sw) / 2, h * 0.7), sub, font=f2, fill=fg if accent is None else accent)
    # Subtle wear
    arr = np.asarray(img, dtype=np.float32) / 255
    n = tileable_noise(max(w, h), 16, 3)[:h, :w]
    arr *= (0.9 + 0.12 * n)[..., None]
    save(to_img(arr), name + ".png")


def shop_interior(name, warm, size=512):
    """Blurred lit interior seen through a shop window (shelves, lights)."""
    img = Image.new("RGB", (size, size), tuple(int(c * 255) for c in warm * 0.35))
    d = ImageDraw.Draw(img)
    for row in range(4):
        y = int(size * (0.25 + row * 0.18))
        d.rectangle([0, y, size, y + 10], fill=tuple(int(c * 255) for c in warm * 0.2))
        x = 0
        while x < size:
            w = int(rng.integers(12, 34))
            hgt = int(rng.integers(20, 60))
            col = np.clip(warm * rng.uniform(0.5, 1.1) + rng.normal(0, 0.12, 3), 0, 1)
            d.rectangle([x, y - hgt, x + w, y], fill=tuple(int(c * 255) for c in col))
            x += w + int(rng.integers(2, 8))
    for k in range(3):
        cx = int(size * (0.2 + 0.3 * k))
        d.ellipse([cx - 30, 10, cx + 30, 40], fill=(255, 238, 200))
    img = img.filter(ImageFilter.GaussianBlur(6))
    save(img, name + ".png")


def windows_lit(name, size=512):
    """Mostly dark window interiors with a few warm lit rooms (for distant buildings)."""
    img = np.zeros((size, size, 3), dtype=np.float32)
    cells = 8
    s = size // cells
    for i in range(cells):
        for j in range(cells):
            lit = rng.random() < 0.28
            col = hex_rgb("#ffcf8a") * rng.uniform(0.55, 1.0) if lit else hex_rgb("#1b2028") * rng.uniform(0.7, 1.2)
            img[i * s + 4:(i + 1) * s - 4, j * s + 6:(j + 1) * s - 6] = col
    save(to_img(img), name + ".png")


def title_logo(name):
    """POING MORT logo: marker lettering, slight slant, red brush stroke. Graffiti is reserved for the title."""
    W, H = 1400, 700
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f_big = font("marker", 300)
    # Red brush stroke under MORT
    stroke = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(stroke)
    pts = [(170 + i * 9, 590 - 40 * math.sin(i / 120 * math.pi) + rng.normal(0, 3)) for i in range(120)]
    for i, (x, y) in enumerate(pts):
        r = 26 * (1 - abs(i / 119 - 0.45) ** 1.6)
        sd.ellipse([x - r * 1.6, y - r * 0.6, x + r * 1.6, y + r * 0.6], fill=255)
    stroke = stroke.filter(ImageFilter.GaussianBlur(2))
    red = Image.new("RGBA", (W, H), (205, 38, 34, 255))
    img.paste(red, (0, 0), stroke)
    # Lettering with a dark outline for readability over the 3D backdrop
    for dx, dy in ((5, 6), (6, 4), (3, 7)):
        d.text((150 + dx, 30 + dy), "POING", font=f_big, fill=(10, 10, 12, 200))
        d.text((300 + dx, 290 + dy), "MORT", font=f_big, fill=(10, 10, 12, 200))
    d.text((150, 30), "POING", font=f_big, fill=(244, 241, 235, 255))
    d.text((300, 290), "MORT", font=f_big, fill=(244, 241, 235, 255))
    # Small crown above the P, drawn with strokes like a tag
    cx, cy = 210, 40
    crown = [(cx - 55, cy + 40), (cx - 45, cy - 30), (cx - 15, cy + 10), (cx, cy - 45), (cx + 15, cy + 10), (cx + 45, cy - 30), (cx + 55, cy + 40)]
    d.line(crown + [crown[0]], fill=(244, 241, 235, 255), width=9, joint="curve")
    img = img.rotate(4, resample=Image.BICUBIC, expand=False)
    # Rough edges: erode alpha with noise
    a = np.asarray(img.split()[-1], dtype=np.float32) / 255
    n = tileable_noise(1400, 64, 2)[:H, :W]
    a = np.clip(a * (0.85 + 0.3 * n), 0, 1)
    img.putalpha(Image.fromarray((a * 255).astype(np.uint8)))
    os.makedirs(os.path.join(ROOT, "Assets", "PoingMort", "Art", "UI"), exist_ok=True)
    path = os.path.join(ROOT, "Assets", "PoingMort", "Art", "UI", name + ".png")
    img.save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT))


def main():
    bricks("T_Brick_Red", hex_rgb("#8a5444"), hex_rgb("#a39a8f"))
    bricks("T_Brick_Dark", hex_rgb("#5f4a42"), hex_rgb("#8c847b"), variation=0.1, soot=0.35)
    stucco("T_Plaster_Warm", hex_rgb("#c7b49a"))
    stucco("T_Plaster_Grey", hex_rgb("#9c968e"), dirt=0.25)
    concrete("T_Concrete", hex_rgb("#8e8a84"))
    asphalt("T_Asphalt")
    slabs("T_Sidewalk", hex_rgb("#9a958d"))
    concrete("T_Curb", hex_rgb("#a8a39b"))
    roller_door("T_RollerDoor")
    foliage("T_Foliage")
    bark("T_Bark")
    concrete("T_Gravel", hex_rgb("#77716a"))
    # Shop fascias: 12.8:1 so a 0.6 m high band can span ~7.7 m without stretching the letters.
    sign("T_Sign_Epicerie", "ÉPICERIE  ·  OUVERT 24/7", (32, 86, 52), (238, 233, 220), w=2048, h=160, accent=(220, 70, 52))
    sign("T_Sign_Laverie", "LAVERIE  ·  LAVAGE SÉCHAGE", (206, 196, 176), (38, 64, 96), w=2048, h=160)
    sign("T_Sign_Atelier", "ATELIER MÉCANIQUE", (34, 34, 36), (226, 214, 190), w=2048, h=160, accent=(196, 46, 40))
    sign("T_Sign_Cafe", "CAFÉ DU COIN", (118, 32, 28), (240, 226, 200), w=2048, h=160)
    sign("T_Sign_SensUnique", "SENS UNIQUE", (24, 64, 140), (240, 240, 240), w=512, h=128)
    shop_interior("T_Shop_Interior_Warm", hex_rgb("#f2c98a"))
    shop_interior("T_Shop_Interior_Cool", hex_rgb("#c8d8e0"))
    windows_lit("T_Windows_Lit")
    title_logo("T_Title_PoingMort")


if __name__ == "__main__":
    main()
