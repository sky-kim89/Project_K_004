import re,os,struct,collections
S='.'
T='../3.Textures/Icons'
def rd(p): return open(os.path.join(S,p),encoding='utf-8').read()
def switch(src, fn, enum):
    # find "fn(... ) => x switch {" block
    m=re.search(re.escape(fn)+r'\([^)]*\)\s*=>\s*\w+\s+switch\s*\{',src)
    if not m: return {}
    i=m.end(); depth=1; j=i
    while depth and j<len(src):
        if src[j]=='{': depth+=1
        elif src[j]=='}': depth-=1
        j+=1
    body=src[i:j]
    out={}
    for mm in re.finditer(enum+r'\.(\w+)\s*=>\s*(.*?)(?=,\s*\n\s*(?:'+enum+r'\.|_\s*=>|//|\n)|\n\s*\};)',body,re.S):
        k,v=mm.group(1),mm.group(2)
        strs=re.findall(r'"([^"]*)"',v)
        out[k]=" ".join(s for s in strs if s.strip() and not s.startswith(' · ')) if strs else ''
    return out
def files(d):
    p=os.path.join(T,d)
    return sorted(f[:-4] for f in os.listdir(p) if f.endswith('.png'))
def dims(d,f):
    h=open(os.path.join(T,d,f+'.png'),'rb').read(24); return "%dx%d"%struct.unpack('>II',h[16:24])
def clean(s): return s.replace('|','/').replace('\n',' ').strip()
out=[]
def table(title, d, rows, note=''):
    out.append(f"\n### {title}\n")
    if note: out.append(note+"\n")
    out.append("| 파일 | 현재 | 이름 | 의미(코드 원문) |\n|---|---|---|---|")
    for key,name,desc in rows:
        dm = dims(d,key) if os.path.exists(os.path.join(T,d,key+'.png')) else '**없음**'
        out.append(f"| `{d}/{key}.png` | {dm} | {clean(name)} | {clean(desc)} |")

# Species passives
sp=rd('InGame/Summon/SpeciesPassive.cs')
n=switch(sp,'ToKorean','SpeciesPassive'); ds=switch(sp,'Describe','SpeciesPassive')
rows=[];tier=[]
for f in files('SpeciesPassives'):
    k=f[len('passive_'):]
    if re.search(r'Up\d$',k): tier.append(f); continue
    rows.append((f,n.get(k,'?'),ds.get(k,'')))
aw=rd('InGame/Summon/PassiveAwakening.cs')
pair={b:a for a,b in re.findall(r'\(SpeciesPassive\.(\w+),\s*SpeciesPassive\.(\w+)\)',aw)}
val={m.group(1):int(m.group(2)) for m in re.finditer(r'(\w+)\s*=\s*(\d+)',sp[sp.index('enum SpeciesPassive'):sp.index('}',sp.index('enum SpeciesPassive'))])}
def kind(k):
    if k in pair: return '각성 ← '+n.get(pair[k],pair[k])
    v=val.get(k,0)
    return '특이 (장비)' if 40<=v<60 else ('계보 (종족)' )
rows=[(f,nm+' · '+kind(f[len('passive_'):]),d) for f,nm,d in rows]
table('종족 패시브 (계보·특이·각성)','SpeciesPassives',rows,note='이름 뒤의 분류: 계보 = 종족이 타고남 · 특이 = 장비 패시브 · 각성 ← 원본 = 같은 패시브 둘이 모이면 바뀌는 상위판')
fam=collections.OrderedDict()
for f in tier:
    base=re.sub(r'\d$','',f[len('passive_'):]); fam.setdefault(base,[]).append(f)
out.append("\n**단계 패시브 (장비 Lv4·Lv5 가 여는 스탯 I·II·III)** — 한 계열 세 장은 같은 그림에 단계 표식만 다르다\n")
out.append("| 계열 | 파일 |\n|---|---|")
for b,fs in fam.items(): out.append(f"| {b} | "+", ".join(f"`{x}`" for x in fs)+" |")

rp=rd('InGame/Summon/RunPerk.cs')
n=switch(rp,'ToKorean','RunPerk'); ds=switch(rp,'Describe','RunPerk')
table('런 특성','RunPerks',[(f,n.get(f[5:],'?'),ds.get(f[5:],'')) for f in files('RunPerks') if f.startswith('perk_')])
spk=rd('InGame/Summon/SummonerPerk.cs')
n=switch(spk,'ToKorean','SummonerPerk'); ds=switch(spk,'Describe','SummonerPerk')
used=set(re.findall(r'SummonerPerk\.(\w+)',rd('InGame/Summon/Editor/SummonerCreator.cs')))
table('소환사 개성','RunPerks',[(f,n.get(f[6:],'?')+('' if f[6:] in used else ' **(쓰는 소환사 없음 — 만들지 말 것)**'),ds.get(f[6:],'')) for f in files('RunPerks') if f.startswith('sperk_')])
sy=rd('InGame/Summon/MonsterSynergyRule.cs')
n=switch(sy,'NameOf','MonsterTag'); g=switch(sy,'GoldAbility','MonsterTag')
table('시너지','Synergies',[(f,n.get(f[8:],'?'),'금: '+g.get(f[8:],'')) for f in files('Synergies')])
rn=rd('InGame/Summon/RunNode.cs')
n=switch(rn,'ToKorean','RunNodeKind'); ds=switch(rn,'Describe','RunNodeKind')
table('갈림길 칸','RunNodes',[(f,n.get(f[5:],'?'),ds.get(f[5:],'')) for f in files('RunNodes')])
ev=rd('InGame/Summon/RunEvent.cs')
evs={m.group(1):(m.group(2),m.group(3)) for m in re.finditer(r'new\(RunEventId\.(\w+),\s*"([^"]+)",\s*"([^"]+)"',ev)}
table('갈림길 이벤트','RunEvents',[(f,evs.get(f[6:],('?',''))[0],evs.get(f[6:],('?',''))[1]) for f in files('RunEvents')])
cat=rd('Relic/Tree/RelicTreeCatalog.cs')
def snake(x): return 'node_'+re.sub(r'(?<!^)(?=[A-Z])','_',x).lower()
rel={}
for m in re.finditer(r'(?:Stat|Sys|Root)\(RelicNodeId\.N_(\w+),\s*RelicNodeId\.\w+,\s*"([^"]+)"([^;]*);',cat):
    br=re.search(r'RelicBranch\.(\w+)',m.group(3)); ef=re.search(r'RelicSystemEffect\.(\w+)',m.group(3)); st=re.findall(r'StatType\.(\w+)',m.group(3))
    rel[snake(m.group(1))]=(m.group(2),(br.group(1) if br else '?')+' · '+(ef.group(1) if ef else '+'.join(st)))
table('유물 트리 노드','RelicTree',[(f,rel.get(f,('?',''))[0],rel.get(f,('?','?'))[1]) for f in files('RelicTree')],
      note='효과 문구는 `RelicTreeCatalog.cs` 의 해당 줄과 `Docs/RelicTree.md` 를 본다.')
ac=rd('InGame/Battle/Editor/ActiveSkillCreator.cs')
ak={}
for m in re.finditer(r'id\s*:\s*ActiveSkillId\.(\w+),.*?skillName\s*:\s*"([^"]+)",\s*description\s*:\s*"([^"]+)"',ac,re.S):
    ak[m.group(1)]=(m.group(2),m.group(3))
ik=rd('InGame/Skill/ActiveSkillData.cs')
keymap=collections.defaultdict(list)
for m in re.finditer(r'ActiveSkillId\.(\w+)\s*=>\s*"(skill_\w+)"',ik): keymap[m.group(2)].append(m.group(1))
rows=[]
for f in files('Skills'):
    ids=keymap.get(f,[])
    nm=" / ".join(ak.get(i,(i,''))[0] for i in ids); de=" / ".join(ak.get(i,('',''))[1] for i in ids)
    rows.append((f,nm or '?',de))
table('액티브 스킬','Skills',rows,note='한 파일을 여러 스킬이 빌려 쓰면 이름이 `/` 로 이어진다.')
ga=rd('InGame/Battle/Editor/GameAssetCreator.cs')
pv={m.group(1):(m.group(2),m.group(3)) for m in re.finditer(r'PassiveSkillType\.(\w+),\s*"\w+",\s*"([^"]+)",\s*"([^"]+)"',ga)}
table('원작 패시브 (카드 레벨 패시브 · 용사 패시브)','Passives',[(f,pv.get(f[8:],('?',''))[0],pv.get(f[8:],('?',''))[1]) for f in files('Passives')])
gm=rd('InGame/MonsterGear/MonsterGearData.cs'); n=switch(gm,'NameOf','MonsterGearPart')
table('몬스터 장비 부위','Gear',[(f,n.get(f[5:],'?'),'') for f in files('Gear')])
de=rd('InGame/Difficulty/DifficultyEnums.cs')
tl=switch(de,'Label','DifficultyTier'); ts=switch(de,'Summary','DifficultyTier'); dl=switch(de[de.index('Label(this DifficultyDebuff'):],'Label','DifficultyDebuff')
rows=[]
for f in files('Difficulty'):
    k=f.split('_',1)[1]
    if f.startswith('difficulty_'):
        kk=[x for x in tl if x.lower()==k][0]; rows.append((f,tl[kk],ts.get(kk,'')))
    else:
        kk=[x for x in dl if x.lower()==k][0]; rows.append((f,dl[kk],'난이도 제약'))
table('난이도','Difficulty',rows)
print("\n".join(out))

gc=rd('InGame/MonsterGear/Editor/MonsterGearCreator.cs')
out2=["\n### (확장) 장비 개별 아이콘 — 지금은 부위 아이콘 8장을 나눠 쓴다\n","| 새 파일 | 이름 | 등급 | 몸 | 부위 | 설명 |","|---|---|---|---|---|---|"]
for m in re.finditer(r'new Spec \{ Id = "(\w+)", Name = "([^"]+)", Grade = UnitGrade\.(\w+),\s*Body = MonsterGearBody\.(\w+), Part = MonsterGearPart\.(\w+),.*?Desc = "([^"]*)"',gc,re.S):
    out2.append(f"| `Gear/Items/{m.group(1)}.png` | {m.group(2)} | {m.group(3)} | {m.group(4)} | {m.group(5)} | {m.group(6)} |")
print("\n".join(out2))
