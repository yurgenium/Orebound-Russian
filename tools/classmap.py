# -*- coding: utf-8 -*-
"""Строит translations/classes.ru.json: {класс: {исходная строка: перевод}} и проверяет покрытие."""
import json,re,sys,os,zipfile,struct
sys.stdout.reconfigure(encoding='utf-8')
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0,os.path.join(ROOT,'translations','src'))
from class_strings import G,SINGLE,SKIP
from class_lore import LORE
def strings(b):
    n=struct.unpack('>H',b[8:10])[0];p=10;utf={};refs=[];k=1
    while k<n:
        t=b[p];p+=1
        if t==1:
            l=struct.unpack('>H',b[p:p+2])[0];p+=2;utf[k]=b[p:p+l].decode('utf-8','replace');p+=l
        elif t==8: refs.append(struct.unpack('>H',b[p:p+2])[0]);p+=2
        elif t in(3,4,9,10,11,12,17,18):p+=4
        elif t in(5,6):p+=8;k+=1
        elif t in(7,16,19,20):p+=2
        elif t==15:p+=3
        else: raise Exception(t)
        k+=1
    return [utf[r] for r in refs]
def build(jar,verbose=True):
    z=zipfile.ZipFile(jar);out={};unhandled=[];usedG=set();usedS=set()
    for n in z.namelist():
        if not n.endswith('.class'): continue
        short=n[len('com/orebound/'):-6]; ss=strings(z.read(n)); m={}
        if short=='lore/OreboundArchiveCatalog':
            lore=[s for s in ss if s in ss[:73]]
            assert len(ss)==74 and ss[73].startswith('Invalid archive id'), len(ss)
            for en,ru in zip(ss[:73],LORE):
                assert en.count('\n')==ru.count('\n'),(en[:30],en.count('\n'),ru.count('\n'))
                m[en]=ru
            ss=ss[73:]
        for s in ss:
            single=not re.search(r'[ \n]',s)
            if single:
                t=SINGLE.get(short,{}).get(s)
                if t is not None: m[s]=t; usedS.add((short,s))
                continue
            if s in G:
                t=G[s]; assert s.count('\x01')==t.count('\x01'),s
                if t!=s: m[s]=t
                usedG.add(s)
            elif s not in SKIP and re.search(r'[A-Za-z]{3}',s) and ';' not in s:
                unhandled.append((short,s))
        if m: out[n]=m
    if verbose:
        for u in unhandled: print('UNHANDLED',u[0],json.dumps(u[1],ensure_ascii=False)[:150])
        for g in G:
            if g not in usedG: print('UNUSED G',json.dumps(g,ensure_ascii=False)[:100])
        for c,d in SINGLE.items():
            for s in d:
                if (c,s) not in usedS: print('UNUSED SINGLE',c,s)
        print('classes',len(out),'strings',sum(len(v) for v in out.values()),'unhandled',len(unhandled))
    return out,unhandled
if __name__=='__main__':
    out,un=build(sys.argv[1])
    json.dump(out,open(os.path.join(ROOT,'translations','classes.ru.json'),'w',encoding='utf-8',newline='\n'),ensure_ascii=False,indent=1)
    sys.exit(1 if un else 0)
