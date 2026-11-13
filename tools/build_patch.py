# -*- coding: utf-8 -*-
"""Собирает data/patch.json.gz для установщика из файлов translations/.

Запуск: python tools/build_patch.py <оригинальный orebound-0.2.2.jar> <config/ftbquests/quests/lang/en_us.snbt>
Перед этим: build_mod_lang.py, build_quests.py, make_image_labels.py, render_images.py (в build/images).
"""
import base64, gzip, hashlib, io, json, os, struct, sys, zipfile
from PIL import Image

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import classmap

VERSION = '2.1.0'
T = lambda *p: os.path.join(ROOT, 'translations', *p)
sha = lambda b: hashlib.sha256(b).hexdigest()
b64 = lambda b: base64.b64encode(b).decode('ascii')


def pool_end(b):
    n = struct.unpack('>H', b[8:10])[0]
    p = 10
    k = 1
    while k < n:
        t = b[p]
        p += 1
        if t == 1:
            p += 2 + struct.unpack('>H', b[p:p + 2])[0]
        elif t in (3, 4, 9, 10, 11, 12, 17, 18):
            p += 4
        elif t in (5, 6):
            p += 8
            k += 1
        elif t in (7, 8, 16, 19, 20):
            p += 2
        elif t == 15:
            p += 3
        else:
            raise Exception(t)
        k += 1
    return p


def snbt(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def main():
    jar_path, en_path = sys.argv[1], sys.argv[2]
    jar = open(jar_path, 'rb').read()
    z = zipfile.ZipFile(io.BytesIO(jar))
    assert 'META-INF/orebound-ru.json' not in z.namelist(), 'нужен оригинальный jar без перевода'
    entry_hashes = {}
    for n in z.namelist():
        b = z.read(n)
        entry_hashes[n] = sha(b[pool_end(b):] if n.endswith('.class') else b)
    classes, unhandled = classmap.build(jar_path, verbose=False)
    assert not unhandled, unhandled

    spec = json.load(open(T('image-labels.ru.json'), encoding='utf-8'))
    image_patches = {}
    for e in spec:
        img = Image.open(os.path.join(ROOT, 'build', 'images', os.path.basename(e['path'])))
        patches = []
        for lab in e['labels']:
            x0, y0, x1, y1 = lab['rect']
            buf = io.BytesIO()
            img.crop((x0, y0, x1, y1)).save(buf, 'PNG', optimize=True)
            patches.append(dict(x=x0, y=y0, width=x1 - x0, height=y1 - y0, png=b64(buf.getvalue())))
        image_patches[e['path']] = dict(width=e['size'][0], height=e['size'][1], patches=patches)

    ch = json.load(open(T('chapter-books.ru.json'), encoding='utf-8'))
    names = [snbt(json.dumps({'italic': False, 'text': n}, ensure_ascii=False, separators=(',', ':'))) for n in ch['names']]
    pages = [snbt(json.dumps(p, ensure_ascii=False)) for p in ch['pages']]
    raws = pages + [snbt(ch['title'])]

    data = dict(
        version=VERSION, minecraft='1.21.1', modpack='0.2.2',
        original_mod_sha256=sha(jar), supported_mod_hashes=[sha(jar)],
        quest_source_sha256=sha(open(en_path, 'rb').read()),
        quest_language=b64(open(T('quests.ru_ru.snbt'), 'rb').read()),
        mod_language=b64(open(T('orebound.ru_ru.json'), 'rb').read()),
        classes=classes, entry_hashes=entry_hashes, image_patches=image_patches,
        chapter_path='config/ftbquests/quests/chapters/64d8014b4a521eab.snbt',
        chapter_names=names, chapter_raws=raws + raws,
        lock_message=ch['lock_message'],
        signs=json.load(open(T('signs.ru.json'), encoding='utf-8')),
    )
    os.makedirs(os.path.join(ROOT, 'data'), exist_ok=True)
    raw = json.dumps(data, ensure_ascii=False).encode('utf-8')
    with open(os.path.join(ROOT, 'data', 'patch.json.gz'), 'wb') as f:
        with gzip.GzipFile(fileobj=f, mode='wb', mtime=0) as g:
            g.write(raw)
    print('patch', len(raw), '->', os.path.getsize(os.path.join(ROOT, 'data', 'patch.json.gz')),
          'classes', len(classes), 'images', len(image_patches))


if __name__ == '__main__':
    main()
