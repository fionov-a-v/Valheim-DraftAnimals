"""Иконка мода 256×256, баннер 1300×372 и превью 1132×636: олень с дугой и колокольчиком тянет тележку (на баннере —
гонка: за ним кабан и волк со своими тележками; на превью — одна крупная упряжка с возницей на козлах и выглядывающий
снизу слева заяц, читается и в уменьшенном вчетверо виде). Плоская картинка в духе вышивки, рисуется кодом без текстур:

python3 tests/icon.py package/icon.png                 иконка 256×256
python3 tests/icon.py media/banner.png --banner        баннер 1300×372 (шапка страницы мода)
python3 tests/icon.py media/preview.png --preview      превью 1132×636 (главная картинка мода на Nexus)
"""
import math
import sys

from PIL import Image, ImageDraw, ImageFilter

SS = 4  # рисуем крупнее и уменьшаем — сглаживание

WOOD = (112, 74, 42)
WOOD_DARK = (74, 47, 27)
WOOD_LIGHT = (150, 104, 62)
IRON = (52, 54, 60)
LEATHER = (60, 38, 24)
BRONZE = (214, 160, 70)
BRONZE_DARK = (150, 100, 40)


class Canvas:
    """Рисование в «единицах сцены»: (x, y) * k + (ox, oy), y — вниз."""

    def __init__(self, img, k, ox, oy):
        self.d = ImageDraw.Draw(img)
        self.k, self.ox, self.oy = k, ox, oy

    def p(self, x, y):
        return (self.ox + x * self.k, self.oy + y * self.k)

    def poly(self, pts, fill):
        self.d.polygon([self.p(x, y) for x, y in pts], fill=fill)

    def line(self, pts, fill, w):
        self.d.line([self.p(x, y) for x, y in pts], fill=fill, width=max(1, int(w * self.k)), joint="curve")

    def ellipse(self, cx, cy, rx, ry, fill, outline=None, w=0):
        a, b = self.p(cx - rx, cy - ry), self.p(cx + rx, cy + ry)
        self.d.ellipse([a, b], fill=fill, outline=outline, width=max(0, int(w * self.k)))

    def arc(self, cx, cy, rx, ry, start, end, fill, w):
        a, b = self.p(cx - rx, cy - ry), self.p(cx + rx, cy + ry)
        self.d.arc([a, b], start, end, fill=fill, width=max(1, int(w * self.k)))


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c[:3])


def smooth(pts, steps=6):
    """Сглаженная замкнутая кривая через точки (Catmull-Rom) — силуэты зверей без углов."""
    out = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            out.append(tuple(
                0.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                       + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    return out


def leg(c, top, knee, hoof, w, color):
    """Нога сужается от бедра к копыту: два четырёхугольника и копыто."""
    def seg(a, b, wa, wb):
        dx, dy = b[0] - a[0], b[1] - a[1]
        n = math.hypot(dx, dy) or 1.0
        px, py = -dy / n, dx / n
        c.poly([(a[0] + px * wa, a[1] + py * wa), (b[0] + px * wb, b[1] + py * wb),
                (b[0] - px * wb, b[1] - py * wb), (a[0] - px * wa, a[1] - py * wa)], color)
        c.ellipse(b[0], b[1], wb, wb, color)
    seg(top, knee, w * 0.95, w * 0.5)
    seg(knee, hoof, w * 0.5, w * 0.36)
    c.ellipse(hoof[0] + 0.012, hoof[1] - 0.01, w * 0.5, w * 0.32, shade(color, 0.55))


# ===================================================================== звери (смотрят вправо, земля — y = 0)

def deer(c, gallop, color=(158, 102, 60)):
    """Олень: ноги на земле y = 0, холка около (0.28, -1.0). Возвращает точку дуги на холке и куда цеплять постромки."""
    far = shade(color, 0.7)
    if gallop:
        legs_far = [((-0.40, -0.66), (-0.66, -0.40), (-0.84, -0.14)), ((0.30, -0.66), (0.56, -0.44), (0.74, -0.22))]
        legs_near = [((-0.30, -0.66), (-0.42, -0.32), (-0.62, -0.02)), ((0.22, -0.66), (0.40, -0.36), (0.54, -0.10))]
    else:
        legs_far = [((-0.42, -0.66), (-0.50, -0.32), (-0.46, 0.0)), ((0.30, -0.66), (0.32, -0.32), (0.34, 0.0))]
        legs_near = [((-0.30, -0.66), (-0.38, -0.32), (-0.34, 0.0)), ((0.20, -0.66), (0.20, -0.32), (0.22, 0.0))]
    for top, knee, hoof in legs_far:
        leg(c, top, knee, hoof, 0.09, far)
    c.poly([(-0.58, -0.92), (-0.72, -1.00), (-0.66, -0.84)], shade(color, 0.9))  # хвост
    body = smooth([(-0.58, -0.90), (-0.30, -1.00), (0.10, -1.02), (0.36, -1.04), (0.52, -0.92), (0.50, -0.70),
                   (0.30, -0.56), (-0.10, -0.56), (-0.46, -0.60), (-0.64, -0.74)])
    c.poly(body, color)
    # Шея вперёд-вверх и голова.
    neck = smooth([(0.26, -1.00), (0.44, -1.26), (0.58, -1.42), (0.74, -1.40), (0.70, -1.22), (0.56, -0.92), (0.46, -0.78)], 5)
    c.poly(neck, color)
    head = smooth([(0.58, -1.46), (0.72, -1.52), (0.92, -1.40), (0.98, -1.30), (0.86, -1.27), (0.66, -1.30)], 5)
    c.poly(head, color)
    c.poly([(0.62, -1.48), (0.50, -1.64), (0.68, -1.52)], shade(color, 0.85))  # ухо
    c.ellipse(0.76, -1.41, 0.024, 0.024, (20, 14, 10))
    c.ellipse(0.97, -1.31, 0.03, 0.025, (40, 28, 20))  # нос
    # Рога.
    horn = (214, 196, 156)
    c.line([(0.68, -1.50), (0.66, -1.74), (0.58, -1.92)], horn, 0.03)
    c.line([(0.66, -1.74), (0.78, -1.86)], horn, 0.024)
    c.line([(0.62, -1.84), (0.54, -1.80)], horn, 0.02)
    c.poly(smooth([(-0.40, -0.62), (0.24, -0.60), (0.34, -0.66), (-0.30, -0.70)], 3), (198, 160, 118))  # светлое брюхо
    for top, knee, hoof in legs_near:
        leg(c, top, knee, hoof, 0.095, color)
    return (0.30, -1.02), (0.36, -0.84)


def boar(c, gallop, color=(92, 70, 58)):
    far = shade(color, 0.7)
    stride = 0.12 if gallop else 0.0
    for x in (-0.38, 0.22):
        leg(c, (x, -0.40), (x - stride, -0.20), (x - stride * 1.6, 0.0), 0.11, far)
    body = smooth([(-0.55, -0.62), (-0.10, -0.80), (0.30, -0.82), (0.52, -0.66), (0.50, -0.42), (0.10, -0.34),
                   (-0.40, -0.36), (-0.62, -0.48)])
    c.poly(body, color)
    head = smooth([(0.40, -0.80), (0.62, -0.70), (0.86, -0.52), (0.86, -0.42), (0.62, -0.38), (0.44, -0.46)], 5)
    c.poly(head, color)
    c.poly([(0.52, -0.74), (0.50, -0.92), (0.62, -0.76)], shade(color, 0.85))  # ухо
    c.ellipse(0.66, -0.62, 0.02, 0.02, (20, 14, 10))
    c.line([(0.78, -0.44), (0.86, -0.56)], (235, 225, 200), 0.025)  # клык
    # Щетина по хребту.
    for i in range(7):
        x = -0.30 + i * 0.11
        c.poly([(x - 0.05, -0.77 + abs(x) * 0.05), (x, -0.90 + abs(x) * 0.08), (x + 0.05, -0.77 + abs(x) * 0.05)], shade(color, 0.7))
    c.line([(-0.60, -0.54), (-0.70, -0.62)], color, 0.03)  # хвостик
    for x in (-0.30, 0.30):
        leg(c, (x, -0.40), (x + stride, -0.20), (x + stride * 1.6, 0.0), 0.115, color)
    return (0.22, -0.82), (0.22, -0.62)


def wolf(c, gallop, color=(150, 150, 158)):
    far = shade(color, 0.72)
    if gallop:
        legs_far = [((-0.42, -0.50), (-0.62, -0.30), (-0.78, -0.10)), ((0.30, -0.52), (0.52, -0.32), (0.66, -0.14))]
        legs_near = [((-0.34, -0.50), (-0.44, -0.24), (-0.60, 0.0)), ((0.22, -0.52), (0.36, -0.26), (0.48, -0.06))]
    else:
        legs_far = [((-0.42, -0.50), (-0.46, -0.24), (-0.44, 0.0)), ((0.30, -0.52), (0.30, -0.26), (0.32, 0.0))]
        legs_near = [((-0.32, -0.50), (-0.36, -0.24), (-0.32, 0.0)), ((0.20, -0.52), (0.20, -0.26), (0.22, 0.0))]
    for top, knee, hoof in legs_far:
        leg(c, top, knee, hoof, 0.085, far)
    c.poly(smooth([(-0.52, -0.70), (-0.80, -0.70), (-1.00, -0.56), (-0.84, -0.60), (-0.56, -0.60)], 4), shade(color, 0.92))  # хвост
    body = smooth([(-0.58, -0.74), (-0.10, -0.82), (0.30, -0.90), (0.50, -0.78), (0.46, -0.52), (0.04, -0.44),
                   (-0.48, -0.48), (-0.64, -0.60)])
    c.poly(body, color)
    head = smooth([(0.36, -0.86), (0.56, -0.98), (0.70, -0.94), (0.92, -0.84), (0.88, -0.78), (0.62, -0.76), (0.44, -0.66)], 5)
    c.poly(head, color)
    c.poly([(0.54, -0.96), (0.56, -1.12), (0.64, -0.96)], shade(color, 0.85))  # ухо
    c.ellipse(0.70, -0.89, 0.018, 0.018, (20, 14, 10))
    c.poly(smooth([(0.40, -0.58), (0.56, -0.66), (0.60, -0.76), (0.48, -0.70)], 3), shade(color, 1.15))  # светлая грудь
    for top, knee, hoof in legs_near:
        leg(c, top, knee, hoof, 0.09, color)
    return (0.26, -0.90), (0.30, -0.70)


# ===================================================================== тележка и упряжь

def cart(c, x, wheel_spin=0.0, load=True):
    """Тележка: колесо на земле, оглобля смотрит вправо и кончается у x. Возвращает конец оглобли."""
    wx, wr = x - 1.05, 0.34
    bed_y = -0.62
    # Дальнее колесо выглядывает из-за кузова.
    c.ellipse(wx + 0.10, -wr - 0.02, wr, wr, shade(WOOD_DARK, 0.6))
    # Груз.
    if load:
        c.poly([(wx - 0.52, bed_y - 0.20), (wx - 0.12, bed_y - 0.20), (wx - 0.12, bed_y - 0.48), (wx - 0.52, bed_y - 0.48)], WOOD_LIGHT)
        c.line([(wx - 0.52, bed_y - 0.34), (wx - 0.12, bed_y - 0.34)], WOOD_DARK, 0.02)
        c.ellipse(wx + 0.12, bed_y - 0.30, 0.17, 0.12, (100, 104, 112))  # руда горкой
        c.ellipse(wx + 0.30, bed_y - 0.26, 0.13, 0.09, (122, 92, 70))
        c.ellipse(wx + 0.02, bed_y - 0.24, 0.12, 0.08, (136, 110, 80))
    # Кузов из досок.
    c.poly([(wx - 0.64, bed_y), (wx + 0.56, bed_y), (wx + 0.60, bed_y - 0.28), (wx - 0.68, bed_y - 0.28)], WOOD)
    for i in range(1, 3):
        y = bed_y - 0.28 * i / 3
        c.line([(wx - 0.66, y), (wx + 0.58, y)], WOOD_DARK, 0.018)
    c.line([(wx - 0.68, bed_y - 0.28), (wx + 0.60, bed_y - 0.28)], WOOD_LIGHT, 0.025)
    # Оглобля к зверю.
    c.line([(wx + 0.50, bed_y - 0.06), (x, bed_y - 0.20)], WOOD_DARK, 0.05)
    # Ближнее колесо со спицами.
    c.ellipse(wx, -wr, wr, wr, None, WOOD_DARK, 0.06)
    for i in range(8):
        a = wheel_spin + i * math.pi / 4
        c.line([(wx, -wr), (wx + math.cos(a) * wr * 0.92, -wr + math.sin(a) * wr * 0.92)], WOOD, 0.03)
    c.ellipse(wx, -wr, 0.06, 0.06, IRON)
    c.ellipse(wx, -wr, wr + 0.01, wr + 0.01, None, IRON, 0.02)
    return (x, bed_y - 0.20)


def harness(c, withers, trace_at, shaft_end, bell=1.0, arch=1.0):
    """Дуга над холкой (видна чуть сбоку — узкий обод), колокольчик под верхом, постромка от оглобли, подпруга.
    arch > 1 — дуга крупнее и светлее (для превью, где она должна читаться и в миниатюре)."""
    ax, ay = withers
    rx, ry = 0.16 * arch, 0.46 * arch   # полуширина обода в проекции и высота над холкой
    cy = ay + 0.08                      # концы дуги — чуть ниже холки, по бокам
    wood = (188, 136, 80) if arch <= 1.0 else (226, 176, 104)
    c.line([shaft_end, trace_at], LEATHER, 0.035)
    c.line([(ax - 0.10, ay + 0.02), (ax - 0.13, ay + 0.44)], LEATHER, 0.06)
    c.arc(ax, cy, rx, ry, 180, 270, shade(WOOD, 0.6), 0.06 * arch)
    c.arc(ax, cy, rx, ry, 270, 360, WOOD_DARK, 0.095 * arch)
    c.arc(ax, cy, rx, ry, 270, 360, wood, 0.065 * arch)
    top = (ax, cy - ry)
    b = bell
    bx, by = top[0] - 0.01, top[1] + 0.05 + 0.06 * b
    c.line([(top[0], top[1] + 0.02), (bx, by - 0.05 * b)], IRON, 0.014 * b)
    c.poly([(bx - 0.035 * b, by - 0.06 * b), (bx + 0.035 * b, by - 0.06 * b), (bx + 0.075 * b, by + 0.05 * b),
            (bx - 0.075 * b, by + 0.05 * b)], BRONZE)
    c.ellipse(bx, by + 0.05 * b, 0.075 * b, 0.02 * b, BRONZE_DARK)
    c.ellipse(bx, by + 0.075 * b, 0.018 * b, 0.018 * b, BRONZE_DARK)
    c.ellipse(ax + rx, cy, 0.035, 0.035, None, IRON, 0.015)  # кольцо постромки


def driver(c, wx, reins_to):
    """Возница на передке тележки (wx — ось колеса): шлем, синий плащ, вожжи в руках тянутся к морде зверя."""
    cape = (44, 74, 138)
    tunic = (96, 72, 50)
    skin = (222, 184, 152)
    helmet = (118, 124, 134)
    hip = (wx + 0.40, -0.95)
    shoulder = (wx + 0.50, -1.40)
    hands = (wx + 0.80, -1.18)
    # Плащ за спиной, ноги свешиваются вперёд с передка.
    c.poly([(shoulder[0] - 0.06, shoulder[1] - 0.02), (shoulder[0] + 0.08, shoulder[1]), (hip[0] - 0.02, hip[1] + 0.04),
            (hip[0] - 0.24, hip[1] + 0.06)], cape)
    leg(c, (hip[0] + 0.02, hip[1]), (wx + 0.66, -0.93), (wx + 0.70, -0.66), 0.11, (70, 56, 44))
    c.poly(smooth([(hip[0] - 0.10, hip[1] + 0.02), (hip[0] + 0.12, hip[1] + 0.02), (shoulder[0] + 0.12, shoulder[1] + 0.06),
                   (shoulder[0] - 0.06, shoulder[1] - 0.02)], 3), tunic)
    c.line([shoulder, (wx + 0.66, -1.24), hands], tunic, 0.075)  # рука
    c.ellipse(hands[0], hands[1], 0.045, 0.045, skin)
    hx, hy = shoulder[0] + 0.03, shoulder[1] - 0.17
    c.ellipse(hx, hy, 0.105, 0.115, skin)  # голова
    c.poly([(hx - 0.02, hy + 0.04), (hx + 0.12, hy + 0.04), (hx + 0.04, hy + 0.20)], (206, 160, 90))  # борода
    c.poly(smooth([(hx - 0.12, hy - 0.01), (hx - 0.10, hy - 0.12), (hx, hy - 0.17), (hx + 0.10, hy - 0.12),
                   (hx + 0.13, hy - 0.01)], 4), helmet)  # шлем
    c.line([(hx + 0.07, hy - 0.04), (hx + 0.07, hy + 0.06)], helmet, 0.025)  # наносник
    # Вожжи: от рук к морде, с лёгким провисом.
    mx, my = (hands[0] + reins_to[0]) / 2, (hands[1] + reins_to[1]) / 2 + 0.10
    c.line([hands, (mx, my), reins_to], LEATHER, 0.022)


def team(c, animal, x, gallop, spin, scale=1.0, rider=False, bell=1.0, arch=1.0):
    """Зверь с упряжью и тележкой: зверь стоит в x (центр корпуса), тележка — позади."""
    sub = Canvas.__new__(Canvas)
    sub.d, sub.k, sub.ox, sub.oy = c.d, c.k * scale, c.ox + x * c.k, c.oy
    shaft_end = cart(sub, -0.42, spin)
    withers, trace = animal(sub, gallop)
    harness(sub, withers, trace, shaft_end, bell, arch)
    if rider:
        driver(sub, -0.42 - 1.05, (0.86, -1.31))


# ===================================================================== фон

def background(w, h, horizon):
    img = Image.new("RGB", (w, h))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        if y < horizon:
            a = y / max(1, horizon)
            col = (int(18 + 60 * a), int(40 + 70 * a), int(58 + 60 * a))  # вечернее небо к горизонту светлее
        else:
            a = (y - horizon) / max(1, h - horizon)
            col = (int(52 - 22 * a), int(78 - 30 * a), int(40 - 16 * a))  # луг
        draw.line([(0, y), (w, y)], fill=col)
    return img


def hills(img, horizon, seed_shift):
    d = ImageDraw.Draw(img)
    w, h = img.size
    for layer, (col, amp, base) in enumerate([((38, 62, 70), 0.10, 0.02), ((32, 56, 46), 0.06, 0.0)]):
        pts = [(0, horizon)]
        for i in range(0, w + 1, max(1, w // 60)):
            x = i
            y = horizon - h * (base + amp * (0.5 + 0.5 * math.sin(x / w * (5 + layer * 3) + seed_shift + layer)))
            pts.append((x, y))
        pts.append((w, horizon + 2))
        pts.append((0, horizon + 2))
        d.polygon(pts, fill=col)


def stars(img, horizon, count, seed):
    d = ImageDraw.Draw(img)
    w, _ = img.size
    rnd = (seed * 7919) % 104729
    for i in range(count):
        rnd = (rnd * 1103515245 + 12345) % 2147483648
        x = rnd % w
        rnd = (rnd * 1103515245 + 12345) % 2147483648
        y = rnd % max(1, int(horizon * 0.8))
        r = SS * (0.6 + (i % 3) * 0.35)
        v = 150 + (i * 37) % 90
        d.ellipse([x - r, y - r, x + r, y + r], fill=(v, v + 10 if v < 245 else 255, 255))


def moon(img, cx, cy, r):
    glow = Image.new("RGBA", img.size)
    ImageDraw.Draw(glow).ellipse([cx - r * 2.2, cy - r * 2.2, cx + r * 2.2, cy + r * 2.2], fill=(200, 230, 255, 60))
    glow = glow.filter(ImageFilter.GaussianBlur(r))
    img.paste(Image.alpha_composite(img.convert("RGBA"), glow).convert("RGB"))
    ImageDraw.Draw(img).ellipse([cx - r, cy - r, cx + r, cy + r], fill=(232, 240, 246))


def shadow(img, cx, cy, rx, ry):
    layer = Image.new("RGBA", img.size)
    ImageDraw.Draw(layer).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(10, 20, 10, 110))
    layer = layer.filter(ImageFilter.GaussianBlur(ry * 0.6))
    img.paste(Image.alpha_composite(img.convert("RGBA"), layer).convert("RGB"))


def road(img, horizon):
    d = ImageDraw.Draw(img)
    w, h = img.size
    d.polygon([(0, h * 0.86), (w, h * 0.78), (w, h * 0.98), (0, h)], fill=(96, 84, 60))
    d.polygon([(0, h * 0.90), (w, h * 0.83), (w, h * 0.85), (0, h * 0.92)], fill=(112, 98, 70))


def make_icon(path):
    size = 256 * SS
    horizon = int(size * 0.66)
    img = background(size, size, horizon)
    stars(img, horizon, 25, 3)
    moon(img, int(size * 0.22), int(size * 0.20), int(size * 0.06))
    hills(img, horizon, 1.3)
    ground = int(size * 0.88)
    k = size * 0.30
    cx = size * 0.58
    shadow(img, int(cx - 0.35 * k), ground, int(1.3 * k), int(0.08 * k))
    c = Canvas(img, k, cx, ground)
    team(c, deer, 0.0, False, 0.3)
    img.resize((256, 256), Image.LANCZOS).save(path)


def make_banner(path):
    w, h = 1300 * SS, 372 * SS
    horizon = int(h * 0.60)
    img = background(w, h, horizon)
    stars(img, horizon, 90, 5)
    moon(img, int(w * 0.88), int(h * 0.20), int(h * 0.07))
    hills(img, horizon, 0.4)
    road(img, horizon)
    # Гонка: впереди олень, за ним кабан и волк (дальше — мельче и выше на дороге).
    for animal, x, ground, k, spin in [
        (boar, 0.20, 0.82, 0.30, 0.6),
        (wolf, 0.84, 0.84, 0.31, 1.1),
        (deer, 0.54, 0.95, 0.40, 0.2),
    ]:
        gx, gy, kk = w * x, h * ground, h * k
        shadow(img, int(gx - 0.35 * kk), int(gy), int(1.35 * kk), int(0.07 * kk))
        c = Canvas(img, kk, gx, gy)
        team(c, animal, 0.0, True, spin)
    img.resize((1300, 372), Image.LANCZOS).save(path)


def outlined(img, draw_fn, width, color=(14, 18, 22)):
    """Рисует сцену на прозрачном слое и кладёт под неё тёмную обводку — силуэты читаются и на маленькой картинке."""
    layer = Image.new("RGBA", img.size)
    draw_fn(layer)
    alpha = layer.split()[3]
    size = width * 2 + 1
    halo = alpha.filter(ImageFilter.MaxFilter(size))
    outline = Image.new("RGBA", img.size, color + (255,))
    outline.putalpha(halo)
    base = img.convert("RGBA")
    base.alpha_composite(outline)
    base.alpha_composite(layer)
    img.paste(base.convert("RGB"))


def dust(img, points, r):
    """Пыль из-под колёс и копыт: размытые полупрозрачные клубы."""
    layer = Image.new("RGBA", img.size)
    d = ImageDraw.Draw(layer)
    for x, y, s in points:
        d.ellipse([x - r * s, y - r * s * 0.6, x + r * s, y + r * s * 0.6], fill=(170, 150, 112, 90))
    layer = layer.filter(ImageFilter.GaussianBlur(r * 0.35))
    img.paste(Image.alpha_composite(img.convert("RGBA"), layer).convert("RGB"))


# Заяц Туманных земель из игры: тёмная чешуйчатая шкура, серые клочья меха на щеках, голая розоватая кожа в ушах
# и у носа, оранжевые глаза (цвета — с его текстуры hare_d).
HARE_SKIN = (60, 54, 50)
HARE_SCALE = (92, 84, 78)
HARE_FUR = (140, 134, 128)
HARE_BARE = (156, 108, 94)
HARE_EYE = (232, 122, 46)


def hare_face(layer, k, cx, cy):
    """Морда игрового зайца анфас: уши торчком, смотрит прямо на зрителя исподлобья. Единица k — радиус головы,
    (cx, cy) — её центр в пикселях слоя."""
    c = Canvas(layer, k, cx, cy)
    ears = []
    for side in (-1, 1):
        tilt = 0.10 * side
        ear = [(0.18, -0.60), (0.30, -1.40), (0.38, -2.20), (0.56, -2.56), (0.78, -2.26), (0.88, -1.40), (0.74, -0.52)]
        ears.append([(side * x + tilt * (-y - 0.6), y) for x, y in ear])
    head = [(0.0, -0.98), (0.66, -0.86), (0.98, -0.30), (1.00, 0.36), (0.66, 0.92), (0.0, 1.06), (-0.66, 0.92), (-1.00, 0.36),
            (-0.98, -0.30), (-0.66, -0.86)]
    # Клочья меха на щеках торчат в стороны.
    for side in (-1, 1):
        tuft = [(0.62, 0.02), (1.22, 0.10), (0.98, 0.26), (1.34, 0.42), (1.02, 0.54), (1.24, 0.74), (0.86, 0.76), (0.98, 0.98),
                (0.50, 0.92)]
        c.poly([(side * x, y) for x, y in tuft], HARE_FUR)
    for ear in ears:
        c.poly(smooth(ear), HARE_SKIN)
    c.poly(smooth(head), HARE_SKIN)

    # Чешуя: полукружия рядами по шкуре (только внутри головы и ушей).
    size = layer.size
    mask = Image.new("L", size)
    md = ImageDraw.Draw(mask)
    for shape in ears + [head]:
        md.polygon([c.p(x, y) for x, y in smooth(shape)], fill=255)
    scales = Image.new("RGBA", size)
    sc = Canvas(scales, k, cx, cy)
    step = 0.17
    row = 0
    y = -2.7
    while y < 1.1:
        x = -1.3 + (step / 2 if row % 2 else 0.0)
        while x < 1.3:
            sc.arc(x, y, step * 0.55, step * 0.45, 20, 160, HARE_SCALE + (255,), 0.028)
            x += step
        y += step * 0.62
        row += 1
    layer.paste(scales, (0, 0), Image.composite(scales, Image.new("RGBA", size), mask).split()[3])

    for ear in ears:
        cxe = sum(x for x, _ in ear) / len(ear)
        inner = [(cxe + (x - cxe) * 0.5, y * 0.94 - 0.05) for x, y in ear]
        c.poly(smooth(inner), HARE_BARE)
    # Серый мех на морде и подбородке, голая кожа вокруг носа.
    for side in (-1, 1):
        c.ellipse(side * 0.26, 0.54, 0.34, 0.28, HARE_FUR)
    c.ellipse(0.0, 0.84, 0.30, 0.20, HARE_FUR)
    c.ellipse(0.0, 0.30, 0.17, 0.12, HARE_BARE)
    c.poly([(-0.12, 0.24), (0.12, 0.24), (0.0, 0.36)], (70, 44, 42))
    c.line([(0.0, 0.36), (0.0, 0.52)], (40, 30, 28), 0.035)
    c.arc(-0.10, 0.52, 0.10, 0.08, 0, 160, (40, 30, 28), 0.035)
    c.arc(0.10, 0.52, 0.10, 0.08, 20, 180, (40, 30, 28), 0.035)
    # Глаза — оранжевые, по бокам головы, под тяжёлыми надбровьями: смотрят прямо на зрителя.
    for side in (-1, 1):
        ex, ey = side * 0.52, -0.14
        c.ellipse(ex, ey, 0.21, 0.19, (22, 16, 14))
        c.ellipse(ex, ey + 0.01, 0.16, 0.14, HARE_EYE)
        c.ellipse(ex, ey + 0.01, 0.07, 0.11, (18, 12, 10))
        c.ellipse(ex - side * 0.05, ey - 0.05, 0.035, 0.035, (255, 236, 200))
        # Верхнее веко-надбровье скошено к носу — взгляд исподлобья.
        inner, outer = ex - side * 0.24, ex + side * 0.24
        c.poly([(outer + side * 0.05, ey - 0.26), (inner - side * 0.05, ey - 0.26), (inner - side * 0.02, ey - 0.01),
                (outer, ey - 0.13)], HARE_SKIN)
    for side in (-1, 1):
        for dy, ty in ((0.42, 0.30), (0.52, 0.52), (0.60, 0.74)):
            c.line([(side * 0.46, dy), (side * 1.36, ty)], (170, 164, 156), 0.018)


def make_preview(path):
    """Превью 1132×636: одна крупная упряжка — олень с дугой и колокольчиком на скаку, тележка с грузом и возница;
    голова оленя с рогами — на фоне луны. Уменьшенное вчетверо (283×159) остаётся понятным."""
    W, H = 1132, 636
    w, h = W * SS, H * SS
    horizon = int(h * 0.62)
    img = background(w, h, horizon)
    stars(img, horizon, 70, 9)
    hills(img, horizon, 2.1)
    road(img, horizon)
    k = h * 0.40
    gy = h * 0.93
    # Упряжка от задка тележки (-2.15) до морды оленя (0.98) — по центру кадра.
    ox = w * 0.5 + (2.15 - 0.98) * 0.5 * k + 70 * SS  # правее центра: слева внизу выглядывает заяц
    moon(img, int(ox + 0.70 * k), int(gy - 1.55 * k), int(h * 0.15))
    shadow(img, int(ox - 0.6 * k), int(gy), int(1.9 * k), int(0.08 * k))
    dust(img, [(ox - 1.80 * k, gy - 0.10 * k, 1.0), (ox - 2.15 * k, gy - 0.18 * k, 1.3), (ox - 0.95 * k, gy - 0.06 * k, 0.8),
               (ox - 0.55 * k, gy - 0.08 * k, 0.6)], 0.30 * k)

    def draw(layer):
        team(Canvas(layer, k, ox, gy), deer, 0.0, True, 0.4, rider=True, bell=1.3, arch=1.3)

    outlined(img, draw, int(0.022 * k))

    # Заяц выглядывает из-за нижнего края слева: подбородок за кадром, уши торчком.
    def draw_hare(layer):
        hare_face(layer, 86 * SS, 122 * SS, h - 66 * SS)

    outlined(img, draw_hare, int(0.022 * k))
    img.resize((W, H), Image.LANCZOS).save(path)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    out = sys.argv[1]
    if "--banner" in sys.argv[2:]:
        make_banner(out)
    elif "--preview" in sys.argv[2:]:
        make_preview(out)
    else:
        make_icon(out)
    print(out)
