# -*- coding: utf-8 -*-
"""Перерисовывает подписи на обучающих картинках квестов.

Запуск: python tools/render_images.py <orebound-0.2.2.jar (оригинал)> <папка для результата>
Подписи берутся из translations/image-labels.ru.json:
  rect  — область [x0, y0, x1, y1], которая закрашивается фоном и в которую вписывается текст;
  size  — желаемый кегль (уменьшается автоматически, если текст не помещается);
  style — title | sub | text | bold | cell | empty | banner1 | banner2 | banner3;
  lines — для многострочных блоков: строки ставятся с равным шагом по высоте rect.
Нужен Pillow и шрифты Verdana (есть в Windows).
"""
import json, os, sys, zipfile, io
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_DIR = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts')
REG, BOLD = os.path.join(FONT_DIR, 'verdana.ttf'), os.path.join(FONT_DIR, 'verdanab.ttf')
ARROW = (255, 196, 61)


def font(bold, size):
    return ImageFont.truetype(BOLD if bold else REG, size)


def fit(draw, text, bold, size, width):
    while size > 8:
        f = font(bold, size)
        if draw.textlength(text, font=f) <= width:
            return f
        size -= 1
    return font(bold, size)


def median_border(img, rect):
    x0, y0, x1, y1 = rect
    px = img.load()
    pts = [px[x, y0] for x in range(x0, x1, 3)] + [px[x, y1 - 1] for x in range(x0, x1, 3)]
    pts += [px[x0, y] for y in range(y0, y1, 3)] + [px[x1 - 1, y] for y in range(y0, y1, 3)]
    pts.sort(key=lambda c: c[0] + c[1] + c[2])
    return pts[len(pts) // 2][:3]


def near(c, ref, tol=70):
    return sum(abs(a - b) for a, b in zip(c[:3], ref)) < tol


STYLES = {
    'title': dict(bold=True, color=(255, 255, 255), align='left'),
    'sub': dict(bold=False, color=(226, 229, 235), align='left'),
    'text': dict(bold=False, color=(232, 234, 240), align='left'),
    'bold': dict(bold=True, color=(255, 255, 255), align='left'),
    'cell': dict(bold=True, color=(255, 255, 255), align='center', fill=(0, 0, 0)),
    'empty': dict(bold=True, color=(128, 133, 142), align='center'),
    'banner1': dict(bold=True, color=(245, 245, 245), align='center', stroke=(20, 20, 24)),
    'banner2': dict(bold=True, color=(255, 160, 20), align='center', stroke=(20, 20, 24)),
    'banner3': dict(bold=False, color=(225, 228, 235), align='center'),
}


def draw_label(img, lab):
    st = dict(STYLES[lab['style']])
    if 'color' in lab:
        st['color'] = tuple(lab['color'])
    x0, y0, x1, y1 = lab['rect']
    draw = ImageDraw.Draw(img)
    bg = st.get('fill') or median_border(img, lab['rect'])
    src = img.copy().load()
    draw.rectangle([x0, y0, x1 - 1, y1 - 1], fill=bg)
    lines = lab.get('lines') or [lab['text']]
    pitch = (y1 - y0) / len(lines)
    pad = 6 if st['align'] == 'center' else 0
    for i, line in enumerate(lines):
        f = fit(draw, line, st['bold'], lab['size'], x1 - x0 - 2 * pad)
        cy = y0 + pitch * (i + 0.5)
        if st['align'] == 'center':
            pos, anchor = ((x0 + x1) / 2, cy), 'mm'
        else:
            pos, anchor = (x0, cy), 'lm'
        kw = {}
        if st.get('stroke'):
            kw = dict(stroke_width=3, stroke_fill=st['stroke'])
        draw.text(pos, line, font=f, fill=st['color'], anchor=anchor, **kw)
    if lab.get('keep_arrow'):
        px = img.load()
        for y in range(y0, y1):
            for x in range(x0, x1):
                if near(src[x, y], ARROW):
                    px[x, y] = src[x, y]


def main():
    jar, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    spec = json.load(open(os.path.join(ROOT, 'translations', 'image-labels.ru.json'), encoding='utf-8'))
    z = zipfile.ZipFile(jar)
    for entry in spec:
        data = z.read(entry['path'])
        img = Image.open(io.BytesIO(data))
        mode = img.mode
        work = img.convert('RGBA') if mode == 'RGBA' else img.convert('RGB')
        assert list(work.size) == entry['size'], entry['path']
        for lab in entry['labels']:
            draw_label(work, lab)
        dst = os.path.join(out, os.path.basename(entry['path']))
        work.save(dst, optimize=True)
        print(os.path.basename(dst), len(entry['labels']))


if __name__ == '__main__':
    main()
