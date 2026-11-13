# -*- coding: utf-8 -*-
"""Собирает translations/orebound.ru_ru.json из translations/src/mod_lang_values.py и en_us.json мода."""
import json,re,sys,os,zipfile
sys.stdout.reconfigure(encoding='utf-8')
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0,os.path.join(ROOT,'translations','src'))
from mod_lang_values import V,K
jar=sys.argv[1]
en=json.loads(zipfile.ZipFile(jar).read('assets/orebound/lang/en_us.json').decode('utf-8'))
def args(s):
    n=0;out=[]
    for m in re.finditer(r'%(?:(\d+)\$)?([sd%])',s):
        if m.group(2)=='%': continue
        if m.group(1): out.append(int(m.group(1)))
        else: n+=1; out.append(n)
    return sorted(out)
ru={};miss=[];bad=[]
for k,v in en.items():
    t=K.get(k,V.get(v))
    if t is None: miss.append((k,v)); continue
    if args(v)!=args(t): bad.append((k,v,t))
    ru[k]=t
unused=[v for v in V if v not in en.values()]
print('keys',len(en),'missing',len(miss),'bad args',len(bad),'unused',len(unused))
for m in miss: print('MISSING',m)
for b in bad: print('BADARGS',b)
for u in unused: print('UNUSED',u)
if miss or bad: sys.exit(1)
json.dump(ru,open(os.path.join(ROOT,'translations','orebound.ru_ru.json'),'w',encoding='utf-8',newline='\n'),ensure_ascii=False,indent=2)
