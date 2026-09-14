# 액티브 스킬 아이콘 생성 명세 (32종)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/Skills/` — 32개 PNG  
해상도: **128×128** (프로젝트 전체 아이콘을 128로 올리는 중이다)  
짝 문서: `Docs/Icon_Regeneration_Spec.md`(묶음 개요) · `Docs/RelicTree_Icon_Spec.md`(유물 트리)

`ActiveSkillId` 는 33개지만 **파일은 32개**다 — `BossEnrage` 가 `skill_berserker` 를 재사용한다.
그 항목은 표에 있지만 새 파일을 만들지 않는다.

## 공통 시각 언어

| 요소 | 방향 | 의도 |
|---|---|---|
| 배경 | 완전 투명. 원형·사각 배경판을 그리지 말 것 | UI 쪽이 이미 어두운 카드/슬롯을 깔고 그 위에 아이콘만 얹는다 |
| 주제 | 한 아이콘에 하나의 읽히는 행위·도구·상태 | 실제 표시 크기가 작다 — 두 개를 넣으면 뭉갠다 |
| 여백 | 중앙 주제 88~104px, 외곽 여백 12~20px | 슬롯 안쪽 패딩과 겹쳐도 잘리지 않는다 |
| 외곽선 | 어두운 2~3px 실루엣 외곽선 | 어떤 배경 위에서도 형태가 분리된다 |
| 광원 | 좌상단 단일 광원, 플랫 셰이딩 | 수십 장이 한 화면에 같이 보인다 — 광원이 흔들리면 잡동사니가 된다 |
| 색 | 아래 색 규칙을 따르고 강조는 1색만 | 색으로 먼저 분류하고 형태로 확인한다 |
| 금지 | 문자·숫자·워터마크·프레임·테두리 장식·드롭섀도 | 프레임은 UI 가 그린다. 겹치면 두 겹이 된다 |

### 색 규칙

`Docs/Icon_Regeneration_Spec.md` 의 색 규칙을 그대로 잇는다.

| 의미 | 색 | 프롬프트에 넣을 영문 |
|---|---|---|
| 피해 | 주황·적색 | ember orange-red |
| 회복 | 녹색 | healing green |
| 제어·둔화 | 청록·빙청 | icy cyan |
| 마법 | 보라 | arcane violet |
| 소환 | 뼈색·보라 | bone white with violet |
| 강화·버프 | 금색 | warm gold |
| 적 전용(보스) | 혈색 | blood red |

### ⚠ 의미 뱃지를 그림으로 대체한다

기존 아이콘은 `IconArt.cs` 가 우하단에 작은 **의미 뱃지**를 직접 그렸다
(피해=번개, 회복=`+`, 소환=별, 강화=상승 화살표, 약화=하락 화살표, 제어=시계).
이미지 모델은 이 뱃지를 일관되게 못 그린다 — **뱃지를 그리라고 요구하지 말 것.**
대신 같은 구분을 **주제 자체의 차이**로 낸다. 위 프롬프트에 화살표·시계 같은 표식이
들어간 항목은 그것이 주제의 일부라서 넣은 것이다.

## 공통 프롬프트

각 항목 프롬프트 앞에 그대로 붙인다. `{ACCENT}` 자리에 위 표의 영문 색을 넣는다.

```
128x128 game icon, dark fantasy, single centered subject,
flat shading with one light source from the upper left,
crisp dark outline, {ACCENT} as the dominant color,
fully transparent background, no background plate, no frame, no border,
no text, no numbers, no watermark, no signature,
centered composition with even margin on all four sides
```

### 네거티브 프롬프트

```
text, letters, numbers, watermark, signature, logo,
multiple subjects, cluttered composition, busy background,
background plate, circular badge, frame, border, UI chrome,
photorealistic, 3D render, depth of field, drop shadow on background,
cropped subject, subject touching the edge
```

### 생성 해상도

모델에 128×128 를 직접 요구하지 말 것 — 뭉개진다.
**1024×1024 로 생성한 뒤 128×128 로 축소**하고, 축소 후 알파 가장자리를 정리한다.
축소 필터는 Lanczos/Area 를 쓰고, 반투명으로 번진 외곽 픽셀은 알파 임계값 0.35 로 자른다.

### 등급 표현

희귀 스킬(21~30)은 같은 주제라도 **한 단계 더 화려하게** 그린다 — 광원을 하나 더 주거나
금색 포인트를 넣는다. 보스 패턴(31~33)은 혈색을 주조로 써 아군 스킬과 분리한다.
등급 테두리는 그리지 말 것 — 스킬 슬롯 UI 가 이미 그린다.

## 스킬별 프롬프트

### 일반 스킬 (1~20)

| 파일명 | 스킬 | 직업 | 효과 | 주제 | Prompt |
|---|---|---|---|---|---|
| `skill_heavy_strike.png` | **강타** | 방패·전사 | 단일 돌진 타격 + 넉백 | 내리꽂는 대검과 충돌 파편 | a greatsword smashing down, impact burst and shards at the point of contact |
| `skill_volley_fire.png` | **일제 사격** | 궁수·법사 | 전체 즉시 일반 공격 | 같은 방향으로 나가는 화살 세 발 | three arrows loosed in the same direction with parallel trails |
| `skill_leap_strike.png` | **도약 강타** | 방패·전사 | 전방 도약 + AoE 타격 + 넉백 | 도약 궤적과 착지 충격파 | a leaping arc trajectory ending in a ground shockwave |
| `skill_heal_aura.png` | **치유 오라** | 공통 | 피해 입은 아군 장군 랜덤 1명+휘하 병사 회복 | 아군을 감싸는 초록 회복 고리 | a ring of green healing light expanding around a small figure |
| `skill_target_heal.png` | **집중 치유** | 공통 | 아군 장군 중 HP 가장 낮은 1명 집중 회복 | 한 대상에 모이는 치유 광선 | a concentrated beam of healing light converging on one figure |
| `skill_charge_soldier.png` | **돌격 병사 소환** | 방패 | 적 밀치며 데미지, 전투 참여 | 방패를 든 돌격병과 밀어내는 압력선 | a shield-bearing soldier charging forward with push lines behind |
| `skill_summon_skeleton.png` | **스켈레톤 소환** | 공통 | 사망 병사 공·체 일부로 소환 | 무덤에서 솟은 해골 손 | a skeletal hand bursting out of a grave mound |
| `skill_poison_zone.png` | **독성 지대** | 법사·궁수 | 이속 감소 + 지속 피해 영역 | 기포가 이는 독 웅덩이 | a bubbling green poison pool with rising toxic vapor |
| `skill_meteor.png` | **메테오** | 법사 | 강력한 AoE 피해 + 넉백 | 불꼬리를 끌며 낙하하는 운석 | a burning meteor falling with a fiery tail toward impact |
| `skill_blizzard.png` | **블리자드** | 법사 | 공·이속 감소 + 지속 피해 영역 | 얼음 파편이 도는 눈보라 소용돌이 | a swirling snowstorm vortex with ice shards |
| `skill_sacrifice_soldier.png` | **병사 희생** | 공통 | 병사 즉사, 그 공·체 일부 흡수 | 생명력이 빨려 올라가는 병사 실루엣 | a soldier silhouette dissolving as its life force is drawn upward |
| `skill_bind.png` | **속박** | 공통 | 단일 완전 행동불능 + 지속 피해 | 팽팽하게 잠긴 속박 사슬 | taut binding chains locked around an unseen target |
| `skill_suicide_soldier.png` | **자폭 병사** | 법사 | 병사가 적에게 달려 폭발 | 폭발 직전의 돌격병 | a soldier silhouette an instant before detonation, blast cracks radiating |
| `skill_berserker.png` | **광전사** | 전사 | 공격속도 대폭 증가 (일시) | 붉은 전투 광기에 휩싸인 전사 | a raging warrior wreathed in red battle fury |
| `skill_iron_shield.png` | **철벽 방어** | 방패 | 방어율 대폭 증가 (일시) | 보강대를 덧댄 철 방패 | a heavy iron shield braced with reinforcing bands |
| `skill_arrow_rain.png` | **화살 비** | 궁수 | 범위 지속 피해 | 수직으로 쏟아지는 화살 | arrows falling vertically in dense rows onto the ground |
| `skill_battle_cry.png` | **전투 함성** | 전사·방패 | 주변 아군 공격력 증가 (일시) | 함성이 퍼지는 외치는 투구 | a shouting helm with sound waves rippling outward |
| `skill_shockwave.png` | **충격파** | 전사 | 전방 부채꼴 넉백 | 전방으로 퍼지는 부채꼴 압력파 | a fan-shaped pressure wave sweeping forward |
| `skill_swift_strike.png` | **신속 연격** | 궁수 | 자신·병사 공격속도 대폭 증가 | 잔상이 남는 연속 타격 | rapid consecutive strikes leaving afterimages |
| `skill_summon_elite.png` | **정예 소환** | 법사 | 강화된 병사 분대 소환 | 깃발을 든 정예 분대 실루엣 | an elite squad silhouette rising with a raised banner |

### 희귀 · 직업 전용 (21~24)

| 파일명 | 스킬 | 직업 | 효과 | 주제 | Prompt |
|---|---|---|---|---|---|
| `skill_bisect.png` | **일도양단** | 기사 | 전방 직선 경직 후 참격 | 화면을 한 줄로 가르는 참격 | a single straight cut splitting the frame cleanly in two |
| `skill_arrow_storm.png` | **화살 폭풍** | 궁수 | 3연타 광역 낙하 | 세 번에 걸쳐 쏟아지는 화살 폭풍 | a swirling storm of arrows falling in three waves |
| `skill_gravity_collapse.png` | **중력 붕괴** | 법사 | 흡입 구속 후 붕괴 폭발 | 안쪽으로 휘어드는 파편과 붕괴점 | debris curving inward toward a dark collapsing point |
| `skill_bulwark.png` | **불멸의 방벽** | 방패 | 아군 보호막 후 폭발 + 치유 | 아군을 감싼 거대 보호막 돔 | a vast translucent barrier dome shielding a small figure |

### 희귀 · 공통 (25~30)

| 파일명 | 스킬 | 직업 | 효과 | 주제 | Prompt |
|---|---|---|---|---|---|
| `skill_chain_lightning.png` | **연쇄 번개** | 공통 | 적 사이를 튀며 피해 누적 | 세 지점을 잇는 연쇄 번개 | a lightning bolt arcing between three points in a chain |
| `skill_death_sentence.png` | **사형 선고** | 공통 | 낙인 후 일제 처형(즉사) | 적에게 찍힌 처형 낙인 | a glowing execution brand burned onto an enemy silhouette |
| `skill_blood_price.png` | **피의 대가** | 공통 | 자기 체력을 태워 전방 광역 | 불타는 심장과 전방으로 터지는 핏빛 파도 | a burning heart with a blood wave bursting forward |
| `skill_piercing_dash.png` | **관통 돌진** | 근거리 | 1초 쿨 평타형 돌진 관통 | 적을 관통하는 직선 돌진 자취 | a straight dash trail piercing through an enemy silhouette |
| `skill_war_banner.png` | **군기 강림** | 공통 | 주변 아군 공·공속·이속 강화 | 빛을 뿌리며 강림하는 군기 | a radiant war banner descending, light spreading at its base |
| `skill_gravestone.png` | **비석 강림** | 공통 | 비석 낙하 피해 + 스켈레톤 소환 | 갈라진 땅에 내리꽂힌 비석 | a gravestone slamming down into cracked earth with a summoning rift |

### 우두머리 패턴 (31~33, 적 전용)

| 파일명 | 스킬 | 직업 | 효과 | 주제 | Prompt |
|---|---|---|---|---|---|
| `skill_boss_charge.png` | **돌진** | 보스/엘리트 | 적을 관통하며 몸통박치기 | 먼지를 뚫고 돌진하는 붉은 거구 | a massive red silhouette barreling forward through dust |
| `skill_boss_slam.png` | **분쇄 강타** | 보스 | 예고 후 제자리 대반경 강타 | 지면을 부수는 거대한 주먹 | a giant fist smashing the ground with radial fractures |
| `skill_berserker.png` ⟳ | **광폭화** | 보스 | 1분마다 공격력·방어관통·몸집 영구 중첩 | — (`skill_berserker` 재사용, 새로 만들지 않는다) | — |

⟳ = 다른 아이콘을 재사용하는 항목. 파일을 새로 만들지 않는다.

## 생성 후 절차

1. 1024×1024 로 생성 → 128×128 축소 → 알파 임계값 정리.
2. `Assets/_project/3.Textures/Icons/Skills/` 의 **기존 파일을 같은 이름으로 덮어쓴다**.
   경로·파일명·`.meta` 를 유지하면 SO 참조와 아틀라스 연결이 그대로 살아 있다.
3. 임포트 설정: `Texture Type = Sprite (2D and UI)`, `Alpha Is Transparency = ON`,
   `Filter Mode = Bilinear`, `Compression = None`, `Max Size = 128`, `Mesh Type = Full Rect`.
4. `Atlas_General.spriteatlas` 재패킹 — `Tools > Project K > 데이터 생성 > SpriteManager + SpriteAtlas`.

> ⚠ 원본 PNG 와 `.meta` 를 먼저 백업할 것  
> 덮어쓰기라 되돌릴 방법이 파일 백업뿐이다.

## 검수 체크리스트

- [ ] 파일명이 기존과 정확히 같다 (새 이름을 만들지 않았다)
- [ ] 배경이 완전 투명하다 (배경판·원형 뱃지가 없다)
- [ ] 128px 에서 주제가 하나로 읽히고, 48px 로 줄여도 형태가 남는다
- [ ] 문자·숫자·워터마크가 없다
- [ ] 색 규칙과 맞다 — 같은 분류끼리 나란히 놓고 확인한다
- [ ] 희귀 스킬 10종이 일반 스킬보다 확실히 화려하다
- [ ] 보스 패턴 2종이 혈색이라 아군 스킬과 한눈에 구분된다
- [ ] `skill_berserker.png` 를 두 번 만들지 않았다

