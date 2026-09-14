# 패시브 스킬 아이콘 생성 명세 (40종)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/Passives/` — 40개 PNG  
해상도: **128×128** (프로젝트 전체 아이콘을 128로 올리는 중이다)  
짝 문서: `Docs/Icon_Regeneration_Spec.md`(묶음 개요) · `Docs/RelicTree_Icon_Spec.md`(유물 트리)

파일명은 `passive_<PassiveSkillType 이름>.png` — enum 이름을 그대로 쓴다(`PassiveIconGenerator.cs`).
**대소문자까지 그대로 유지할 것.** 이름이 곧 키다.

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
| 병사 관련 | 청동·황토 | bronze |
| 장군 관련 | 금색 | warm gold |
| 교환·대가 | 자주·적색 | crimson |
| 회복 | 녹색 | healing green |
| 방어 | 청록 | steel cyan |
| 처치·피해 | 주황·적색 | ember orange-red |
| 스킬 발동 | 보라 | arcane violet |

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

### ⚠ 40종이 서로 구분돼야 한다 — 트리거를 그림에 넣는다

패시브는 대부분 '스탯이 오른다' 라서 주제를 안 나누면 40장이 전부 상승 화살표가 된다.
**언제 발동하는지**를 주제에 넣어 분류가 보이게 한다:

| 트리거 | 그림에 들어갈 것 |
|---|---|
| 공격 시 | 칼날·주먹이 나가는 순간 |
| 피격 시 | 방패·투구에 충격이 닿는 순간 |
| 적 처치 시 | 쓰러진 적의 해골 |
| 병사 사망 시 | 쓰러진 병사의 투구와 불씨 |
| 스킬 사용 시 | 마법 문양(시길) |
| 전투 시작 시 | 환산되는 두 사물이 맞물린 형태 |

## 패시브별 프롬프트

### 병사 강화

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_ExtraSoldiers.png` | **병사 수 +5명** | 장군 소속 병사 수를 5명 추가합니다. | 창을 든 소대와 증원 표식 | a small squad of spear silhouettes with a plus mark above them |
| `passive_SoldierCombatBoost.png` | **병사 전투 강화** | 병사의 공격력과 이동속도를 각각 20%·10% 증가시킵니다. | 상승 화살표가 붙은 병사 | a soldier silhouette with an upward arrow and speed lines |
| `passive_SoldierHorde.png` | **군중 전술** | 병사 수 +10명. 단, 병사 공격력·체력 -10%. | 빽빽하게 몰린 병사 무리 | a dense crowd of small soldier silhouettes packed tightly together |
| `passive_VanguardAura.png` | **선봉 오라** | 병사 방어율 +10%. | 발밑에 방어 오라가 도는 선봉병 | a front-line soldier with a glowing defensive aura ring at his feet |

### 교환 (한쪽을 깎아 다른 쪽을 올린다)

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_WeakGeneralStrongSoldier.png` | **약한 장군, 강한 병사** | 장군 공격력·체력 -20%. 병사 공격력·체력 +30%. | 병사 쪽으로 기운 저울 | a balance scale tipping toward the soldier side, the general side dimmed |
| `passive_StrongGeneralWeakSoldier.png` | **강한 장군, 약한 병사** | 병사 공격력·체력 -20%. 장군 공격력·체력 +30%. | 장군 쪽으로 기운 저울 | a balance scale tipping toward a single general, the soldier side dimmed |
| `passive_WeakGeneralMoreSoldiers.png` | **희생의 지휘** | 장군 공격력·체력 -15%. 병사 수 +8명. | 자기 힘을 병사에게 넘기는 장군 | a general handing his strength to a growing line of soldiers |
| `passive_BerserkerPact.png` | **광전사의 맹약** | 전체 공격력·공격속도 +25%. 방어율 -15%. | 붉게 타는 갈라진 맹약 인장과 부서진 방패 | a cracked pact seal burning red over a broken shield |

### 장군 강화

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_GeneralCombatBoost.png` | **장군 전투 강화** | 장군 공격력·이동속도 +15%. | 상승 화살표와 잔상이 붙은 지휘관 | a commander silhouette with an upward arrow and a motion streak |
| `passive_TitanGeneral.png` | **거인 장군** | 장군 체력·공격력 +30%·+20%. 공격·이동속도 -15%. 크기 +30%. | 작은 병사들 위에 우뚝 선 거구 | an oversized armored figure towering over tiny soldiers |
| `passive_CommanderFury.png` | **지휘관의 분노** | 장군 크리티컬 확률 +15%, 크리티컬 배율 +0.5. | 관자놀이에 치명 섬광이 터지는 지휘관 투구 | a commander helm with a critical starburst at the temple |

### 시너지 (병사 수·사망이 장군으로)

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_SoldierEmpowerGeneral.png` | **병력의 힘** | 병사 1명당 장군 공격력·체력 +1%. | 여러 병사에게서 장군으로 흐르는 빛 | streams of light flowing from many soldiers into one general |
| `passive_UnityStrength.png` | **결속의 힘** | 병사 1명당 장군 공격력·체력 +1.5%. | 빛나는 사슬로 이어진 병사와 중앙의 장군 | soldiers linked by a glowing chain feeding a central figure |
| `passive_SoldierDeathEmpower.png` | **병사의 유산** | 병사 사망 1명당 장군 공격력 +2%, 체력 +1%. | 쓰러진 병사의 불씨가 장군에게 오른다 | a fallen soldier ember rising into a standing general |
| `passive_SacrificeRitual.png` | **희생 의식** | 병사 5명 희생 → 장군 공격력·체력 +20%. | 병사 표식 다섯이 타 사라지는 의식진 | a ritual circle with five soldier marks burning away |

### 조건부 (체력·병사 수가 조건)

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_BloodPact.png` | **피의 계약** | 장군 체력이 낮을수록 공격력 최대 +50%. | 금 간 심장과 치솟는 피해 화살표 | a cracked heart with a rising damage arrow beside a low health bar |
| `passive_IronWill.png` | **강철 의지** | 장군 HP 50% 이하 시 공격력·체력 +20%·+10% (1회). | 마지막 한 칸에서 버티는 강철 심장 | a steel-banded heart holding at the last sliver of a health bar |
| `passive_LastStand.png` | **최후의 항전** | 병사 수가 초기의 50% 이하 시 남은 병사 공격력·체력 +30%·+20% (1회). | 등을 맞대고 좁혀진 전선의 병사들 | a handful of soldiers back to back on a shrinking line |

### 공격 시 발동

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_VampiricStrike.png` | **흡혈 공격** | 공격 시 가한 피해의 10%를 즉시 체력 회복. | 핏방울을 심장으로 빨아올리는 칼날 | a blade drawing red droplets upward into a heart |
| `passive_StrengthStack.png` | **연격 스택** | 연속 공격마다 공격력 +8 누적 (최대 5스택). | 주먹 옆에 쌓이는 다섯 칸의 스택 | five stacked bars rising beside a striking fist |
| `passive_SoldierMorale.png` | **병사 고무** | 공격 시 30% 확률로 병사 전체 공격력 +12를 3초 동안 버프. | 뿔나팔에 맞춰 들리는 병사들의 창 | a raised war horn with soldier spears lifting in response |

### 피격 시 발동

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_DefenseShield.png` | **방어 강화** | 피격 시 방어율 +10%를 3초 동안 버프. | 피격 순간 번쩍이는 방패 | a shield flashing at the moment of impact |
| `passive_QuickRecovery.png` | **긴급 회복** | 피격 시 최대체력의 0.5%를 즉시 회복. | 맞은 투구 위로 번지는 초록 회복 표식 | a green cross pulse over a struck helm |
| `passive_CounterStrike.png` | **피격 반격** | 피격 시 40% 확률로 공격력 +20%를 5초 동안 버프. | 받아친 힘이 되돌아가는 반격 화살표 | a blow deflected into a returning strike arrow |

### 적 처치 시 발동

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_KillMomentum.png` | **처치 가속** | 처치마다 이동속도 +0.15 누적 (최대 5스택). | 해골을 지나 빨라지는 발자국 자취 | a boot print trail accelerating past a fallen skull |
| `passive_KillEmpower.png` | **처치 강화** | 처치마다 공격력 +10 누적 (최대 5스택). | 해골 더미 위에서 밝아지는 검 | a sword growing brighter over a pile of skulls |
| `passive_KillHeal.png` | **처치 회복** | 처치 시 최대체력의 5%를 즉시 회복. | 해골의 빛을 흡수하는 초록 심장 | a green heart absorbing light from a fallen skull |
| `passive_SoldierVigor.png` | **병사 결의** | 처치 시 병사 전체 공격력 +15를 4초 동안 버프. | 적이 쓰러지자 빛나는 병사들의 창 | soldier spears glowing as an enemy skull falls |
| `passive_LootHunter.png` | **전리품 사냥** | 적 처치 시 골드 +15 획득. | 쓰러진 적에게서 쏟아지는 금화 | gold coins spilling from a fallen enemy |
| `passive_Slaughterer.png` | **도살자** | 처치마다 현재 공격력의 3% 추가 누적 (최대 10스택). | 해골 더미 위의 육중한 도살 칼 | a heavy cleaver over a stack of enemy skulls |

### 병사 사망 시 발동

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_SacrificeAbsorb.png` | **희생 흡수** | 병사 사망 시 사망 1명당 즉시 체력 +30 회복. | 쓰러진 병사의 빛이 심장으로 흡수된다 | a fallen soldier light absorbed into a healing heart |

### 스킬 사용 시 발동

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_SkillAdrenaline.png` | **스킬 아드레날린** | 스킬 사용 시 공격력 +20·공격속도 +0.3을 5초 동안 버프. | 주먹 둘레에 속도선이 도는 스킬 문양 | a spell sigil flaring with speed lines around a fist |
| `passive_SkillInstinct.png` | **생존 본능** | 스킬 사용 시 50% 확률로 최대체력의 8%를 즉시 회복. | 심장 위에서 초록으로 맥동하는 스킬 문양 | a spell sigil with a green pulse over a heart |
| `passive_SkillRally.png` | **전투 집결** | 스킬 사용 시 병사 전체 공격력 +20·이동속도 +0.5를 6초 동안 버프. | 집결하는 병사 대열 위의 스킬 문양 | a spell sigil above a rallying line of soldiers |

### 전투 시작 시 환산

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_GoldenPower.png` | **황금의 힘** | 전투 시작 시 보유 골드 300당 공격력·최대체력 +1%. | 금화 더미가 힘을 불어넣는 갑옷 팔 | a coin stack feeding a glowing armored arm |
| `passive_SwiftAssault.png` | **속전속결** | 이동속도 증가분만큼 공격속도도 동일하게 증가. | 같은 잔상을 공유하는 군화와 칼날 | a boot and a blade sharing the same motion streak |
| `passive_SteelBody.png` | **강철 체력** | 전투 시작 시 최대체력의 3%를 공격력으로 전환. | 칼날로 변해 가는 강철 심장 | a steel heart converting into a blade edge |
| `passive_ShieldEdge.png` | **방패의 날** | 방어율 10%당 공격력 +4%. | 테두리를 날로 갈아낸 방패 | a shield whose rim has been sharpened into a blade |
| `passive_FocusedFire.png` | **집중 사격** | 공격속도 0.1당 치명타 확률 +0.5%. | 연사 눈금과 함께 좁혀지는 조준선 | a crosshair tightening with rapid-fire tick marks |

### 즉시 적용

| 파일명 | 패시브 | 효과 | 주제 | Prompt |
|---|---|---|---|---|
| `passive_WideRange.png` | **사거리 확장** | 사정거리 +10%. | 바깥으로 넓어지는 사거리 호 | a widening range arc with an outward arrow |

## 생성 후 절차

1. 1024×1024 로 생성 → 128×128 축소 → 알파 임계값 정리.
2. `Assets/_project/3.Textures/Icons/Passives/` 의 **기존 파일을 같은 이름으로 덮어쓴다**.
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
- [ ] 트리거가 다른 패시브끼리 그림이 겹치지 않는다
- [ ] '상승 화살표만 있는' 아이콘이 두 장 이상 나오지 않았다
- [ ] 파일명 대소문자가 enum 과 정확히 같다

