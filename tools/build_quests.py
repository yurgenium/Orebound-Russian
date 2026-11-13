# -*- coding: utf-8 -*-
"""Собирает translations/quests.ru_ru.snbt.

Источники:
  translations/src/quests/*.txt   — значения (строка или список строк) по одному на строку,
                                    в том же порядке, что и строки en_us.snbt с отступом-табуляцией;
  translations/src/quests/*.json  — переводы по ключу для строк en_us.snbt с отступом из пробелов
                                    (глава «Mining Help» записана в оригинале именно так);
  translations/src/quest_hints.py — названия заданий-«Продвижений» и абзац «Как выполнить».
Запуск: python tools/build_quests.py <путь к en_us.snbt>
Главы читаются из папки chapters рядом с lang.
"""
import json, re, sys, os, glob

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'translations', 'src'))
import quest_hints

en_path = sys.argv[1]
chapters_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(en_path))), 'chapters')
src = open(en_path, encoding='utf-8').read().split('\n')
tab_lines = [l for l in src if l.startswith('\t')]
space_lines = [l for l in src if l.startswith('    ') and not l.startswith('\t')]
vals = []
for f in sorted(glob.glob(os.path.join(ROOT, 'translations', 'src', 'quests', '*.txt'))):
    for n, l in enumerate(open(f, encoding='utf-8').read().split('\n')):
        if l.strip() == '':
            continue
        vals.append((os.path.basename(f), n + 1, l))
by_key = {}
for f in sorted(glob.glob(os.path.join(ROOT, 'translations', 'src', 'quests', '*.json'))):
    for k, v in json.load(open(f, encoding='utf-8')).items():
        by_key[k] = (os.path.basename(f), v)


def tokens(s):
    return sorted(re.findall(r'\{[^{}]*\}|&[0-9a-fk-or]|\n', s))


err = 0
result = {}  # ключ -> значение (python-объект), в порядке en_us
order = []


def check(where, k, ev, tv):
    global err
    if type(ev) != type(tv) or (isinstance(ev, list) and len(ev) != len(tv)):
        print('SHAPE', where, k, 'en', str(ev)[:50], '| ru', str(tv)[:50])
        err += 1
        return False
    for a, b in zip(ev if isinstance(ev, list) else [ev], tv if isinstance(tv, list) else [tv]):
        if tokens(a) != tokens(b):
            print('TOKENS', where, k, tokens(a), tokens(b))
            err += 1
        if not re.search(r'[А-Яа-яЁё]', b) and re.search(r'[a-z]{4,} [a-z]{3,}', b):
            print('UNTRANSLATED?', where, b[:60])
    return True


for i, l in enumerate(tab_lines):
    k, v = l.strip().split(': ', 1)
    if i >= len(vals):
        break
    f, n, t = vals[i]
    try:
        ev, tv = json.loads(v), json.loads(t)
    except Exception as e:
        print('PARSE', f, n, e, t[:60])
        err += 1
        continue
    if check('%s:%d' % (f, n), k, ev, tv):
        result[k] = tv
        order.append(k)

for l in space_lines:
    k, v = l.strip().split(': ', 1)
    if k not in by_key:
        print('MISSING', k)
        err += 1
        continue
    f, tv = by_key.pop(k)
    if check(f, k, json.loads(v), tv):
        result[k] = tv
        order.append(k)
for k in by_key:
    print('UNKNOWN KEY', k)
    err += 1

# --- задания-«Продвижения» ---
task_titles = {}
hinted = 0
for cf in sorted(glob.glob(os.path.join(chapters_dir, '*.snbt'))):
    text = open(cf, encoding='utf-8').read()
    for qm in re.finditer(r'\n\t\t\{\n(.*?)\n\t\t\}', text, re.S):
        qb = qm.group(1)
        qid = re.search(r'\n\t\t\tid: "([0-9A-F]+)"', '\n' + qb)
        if not qid:
            continue
        qid = qid.group(1)
        hints = []
        for tb in re.findall(r'\{([^{}]*?type: "advancement"[^{}]*?)\}', qb, re.S):
            adv = re.search(r'advancement: "([^"]+)"', tb).group(1)
            tid = re.search(r'id: "([0-9A-F]+)"', tb).group(1)
            key = adv[len('orebound:quest/'):] if adv.startswith('orebound:quest/') else adv
            if key not in quest_hints.ADV:
                if adv.startswith('orebound:'):
                    print('NO HINT', qid, adv)
                    err += 1
                continue
            title, hint = quest_hints.ADV[key]
            task_titles[tid] = title
            if hint and hint not in hints:
                hints.append(hint)
        if qid in quest_hints.QUEST:
            hints = [quest_hints.QUEST[qid]]
        if not hints:
            continue
        dk = 'quest.%s.quest_desc' % qid
        if dk not in result:
            result[dk] = []
            order.append(dk)
        result[dk] = result[dk] + [quest_hints.PREFIX + ' '.join(hints)]
        hinted += 1

for k, v in quest_hints.EXTRA.items():
    if k in result:
        print('EXTRA OVERRIDES', k)
        err += 1
    order.append(k)
    result[k] = v

for tid, title in task_titles.items():
    k = 'task.%s.title' % tid
    result[k] = title
    order.append(k)

out = ['{']
seen = set()
for k in order:
    if k in seen:
        continue
    seen.add(k)
    out.append('\t' + k + ': ' + json.dumps(result[k], ensure_ascii=False))
out.append('}')
total = len(tab_lines) + len(space_lines)
print('translated', min(len(vals), len(tab_lines)) + len(space_lines), 'of', total,
      'EXTRA LINES' if len(vals) > len(tab_lines) else '', '| hints', hinted, '| task titles', len(task_titles),
      '| errors', err)
if not err and len(vals) == len(tab_lines):
    open(os.path.join(ROOT, 'translations', 'quests.ru_ru.snbt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
    print('written')
