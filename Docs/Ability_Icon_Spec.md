# 어빌리티 아이콘 생성 명세 (46종)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/Abilities/` — 46개 PNG (현재 48px, **256px 로 교체**)  
짝 문서: `Docs/Item_Icon_Spec.md`(아이템) · `Docs/RelicTree_Icon_Spec.md`(유물) ·
`Docs/ActiveSkill_Icon_Spec.md` · `Docs/Passive_Icon_Spec.md` · `Docs/Trait_Icon_Spec.md`

파일명은 `AbilityId` 를 소문자로 쓴 것이다 — `A01` → `ability_a01.png`.
`AbilityCreator` 가 이 이름으로 SO 에 자동 연결하므로 **파일명을 바꾸면 안 된다**.

---

## 1. 어디에 어떻게 보이는가 — 이게 모든 결정의 기준이다

원본 해상도가 아니라 **실제 칸 크기**를 기준으로 그린다. 아래가 그 칸 전부다.

| 화면 | 칸 크기 | 캔버스 | 상황 |
|---|---:|---|---|
| `AbilitySelectPopup` 선택 카드 | **124** | Lobby 1920×1080 | 가장 크게 보인다. 세로 카드(328×760) 맨 위, 어두운 보라 패드 위 |
| `RewardCard` (보상 카드) | **128** | InGame 1080×1920 | 어빌리티 보상(`eItem.Ability`). 카드 전면을 채운다 |
| `AbilityListPopup` 정보 카드 | 96 | Lobby | 우측 상세 패널 |
| `AbilityListPopup` 보유 목록 행 | 72 | Lobby | 2열 그리드, 행 높이 104 |
| `GeneralPanelUI` 버프 슬롯 | **36** | InGame | 최소 크기. 전장 배경 위에 배경판 없이 얹힌다 |

**결론 두 개 — 프롬프트가 이 두 줄을 만족시켜야 한다.**

1. **최대 노출 124 → 파일은 256×256.** (노출 크기 × 2, 2의 거듭제곱 올림)
   1024 원본을 남길 이유가 없다. 생성은 512로 충분하고, 최종 산출은 256이다.
2. **최소 노출 36 → 36px 로 줄여도 무엇인지 읽혀야 한다.**
   검수는 반드시 36px 축소본으로 한다. 여기서 뭉개지면 선이 많은 것이다 — 다시 만든다.

### 겹치는 UI (반드시 피할 것)

- **보상 카드 우하단에 수량 배지가 깔린다** — 카드 폭의 62% × 높이 40 (128 카드 기준
  가로 79 × 세로 40). 아이콘의 **우하단 1/4 에 판독에 필요한 형태를 두지 말 것.**
  중심에서 살짝 좌상단으로 무게를 실으면 안전하다.
- 선택 카드·정보 카드는 아이콘 뒤에 **어두운 보라 패드**(`#3D3660`)를 깐다.
  보라를 주조색으로 쓰면 패드에 묻는다 → 유틸 계열은 **밝은 라벤더 + 금색 포인트**로 대비를 만든다.
- 버프 슬롯은 배경판이 **없다**. 전장(밝은 지면·불빛) 위에 바로 얹히므로
  **어두운 외곽선이 없으면 사라진다.**

---

## 2. 공통 시각 언어

| 요소 | 방향 | 의도 |
|---|---|---|
| 배경 | 완전 투명. 원형·사각 배경판을 그리지 말 것 | UI 가 이미 패드·테두리를 그린다. 겹치면 두 겹이 된다 |
| 주제 | 한 아이콘에 **하나**의 도구·행위·상태 | 36px 에서 두 개는 얼룩이 된다 |
| 여백 | 중앙 주제 176~208px, 외곽 여백 24~40px (256 기준) | 어느 칸에서도 잘리지 않는다 |
| 외곽선 | 어두운 4~6px 실루엣 외곽선 (256 기준) | 배경판 없는 버프 슬롯에서 형태를 살린다 |
| 광원 | 좌상단 단일 광원, 플랫 셰이딩 | 46장이 한 화면에 같이 뜬다 — 광원이 흔들리면 잡동사니가 된다 |
| 색 | 계열 주조색 1 + 강조 1. 그 이상 쓰지 말 것 | 색으로 먼저 분류하고 형태로 확인한다 |
| 금지 | 문자·숫자·워터마크·프레임·테두리 장식·드롭섀도 | 등급 테두리·수량 배지는 UI 가 그린다 |

### 계열 색 규칙

`Docs/RelicTree_Icon_Spec.md` 의 계열 색과 같은 축을 쓴다 — 두 화면이 나란히 보이기 때문이다.

| 계열 | 프롬프트에 넣을 영문 색 | 해당 어빌리티 |
|---|---|---|
| 공격 (공격력·공속·치명) | `ember orange-red` | A02 A03 A10 A14 B02 B03 B09 |
| 방어 (체력·방어) | `steel blue` | A01 A05 B01 |
| 병사·지휘 | `bronze` | A13 A16 A17 B12 B13 B14 |
| 유틸 (이속·사거리·쿨감·자원) | `pale lavender with gold accents` | A04 A15 B04 |
| 기사 전용 | `crimson red` | A06 B05 D01 |
| 궁수 전용 | `verdant green` | A07 A11 B06 B10 D02 |
| 마법사 전용 | `arcane violet` | A08 B07 D03 |
| 방패병 전용 | `icy cyan` | A09 B08 D04 |
| 장군 전용 | `warm gold` | A12 B11 |

### 등급 표현

등급 테두리는 그리지 말 것 — 카드가 이미 등급 색 테두리를 그린다. 등급은 **화려함의 단계**로만 낸다.

| 등급 | UI 색 | 그림의 단계 |
|---|---|---|
| 일반 (A01~A17) | 연회색 `#B3B3BF` | 담백한 상징 하나. 광채 없음, 재질만 |
| 고급 (B01~B14) | 하늘색 `#66B8FF` | 같은 축의 강화판. 광원 하나 추가, 재질이 더 무겁다 |
| 특수 (C01~C11) | 금색 `#FFCC33` | **발동 메커니즘을 직접 묘사**. 금색 포인트 1점 |
| 달인 (D01~D04) | 연녹 `#99FF99` | 직업 상징 + 연녹 광채. 가장 화려하다 |

### 대상 표현 규칙

같은 스탯이라도 **대상이 다르면 실루엣이 달라야 한다.** 이름을 못 읽어도 누구에게 붙는지 보인다.

| 대상 | 실루엣 |
|---|---|
| 전체 (All) | 무기·갑옷 그 자체. 사람 형태 없음 |
| 직업 전용 | 그 직업의 도구 (기사 투구 / 활 / 지팡이 / 타워 실드) |
| 근접 / 원거리 | 밀어붙이는 날붙이 / 조준되는 화살촉 |
| 장군 | 깃털 장식 투구 · 월계관 — 장식이 붙은 것 |
| 병사 | 평범한 창 · 천을 감은 자루 — 장식이 없는 것 |

---

## 3. 공통 프롬프트

각 항목 프롬프트 앞에 그대로 붙인다. `{ACCENT}` 에 위 계열 색 영문을 넣는다.

```
256x256 game icon, dark fantasy, single centered subject,
flat shading with one light source from the upper left,
crisp dark outline that reads at 36 pixels,
{ACCENT} as the dominant color with a single accent hue,
visual weight slightly toward the upper left, bottom-right quadrant kept simple,
fully transparent background, no background plate, no frame, no border,
no text, no numbers, no watermark, no signature,
centered composition with even margin on all four sides
```

### 네거티브 프롬프트

```
text, letters, numbers, watermark, signature, logo,
multiple subjects, cluttered composition, busy background, fine filigree detail,
background plate, circular badge, frame, border, UI chrome,
photorealistic, 3D render, depth of field, drop shadow on background,
cropped subject, subject touching the edge
```

### 생성 크기

**512×512 로 생성 → 256×256 축소.** 1024 는 필요 없다 — 실제로 124px 아래로만 보이므로
디테일을 더 넣을수록 축소에서 뭉개지기만 한다.
축소 필터는 Lanczos/Area, 반투명으로 번진 외곽 픽셀은 알파 임계값 0.35 로 자른다.

---

## 4. 항목별 프롬프트

`효과` 는 그림이 무엇을 뜻해야 하는지 판단하는 근거다. 그림이 효과와 어긋나면 다시 만든다.

### 일반 (Normal) — 17종

| 파일명 | 이름 | 대상 | 효과 | 색 | 주제 | Prompt |
|---|---|---|---|---|---|---|
| `ability_a01.png` | **강인한 체력** | 전체 | 최대체력 +8% | steel blue | 정면을 향한 두꺼운 판금 흉갑 | a solid steel breastplate seen head-on, a faint warm glow at its center |
| `ability_a02.png` | **예리한 검격** | 전체 | 공격력 +8% | ember orange-red | 날이 선 검 한 자루 | a keen straight sword angled upward, one bright highlight running down the edge |
| `ability_a03.png` | **신속한 연격** | 전체 | 공격속도 +8% | ember orange-red | 겹친 두 줄의 초승달 참격 | two overlapping crescent slash arcs, the leading one brighter |
| `ability_a04.png` | **민첩한 기동** | 전체 | 이동속도 +8% | pale lavender with gold accents | 날개 달린 전투화 | a winged boot caught mid-stride with short motion streaks behind the heel |
| `ability_a05.png` | **단단한 방어** | 전체 | 방어율 +6% | steel blue | 리벳 박힌 둥근 방패 | a round shield faced toward the viewer, riveted iron bands across its front |
| `ability_a06.png` | **기사의 용맹** | 기사 | 공격력 +8%, 체력 +6% | crimson red | 볏이 선 기사 투구 | a knight helm with a raised crest, visor closed, jaw set forward |
| `ability_a07.png` | **명궁의 직관** | 궁수 | 사거리 +10%, 공속 +6% | verdant green | 시위를 끝까지 당긴 활 | a longbow drawn to full tension with a nocked arrow, string pulled taut |
| `ability_a08.png` | **마법사의 집중** | 마법사 | 공격력 +10%, 쿨감 +8% | arcane violet | 룬이 도는 지팡이 머리의 보석 | a staff head crowned with a focusing gem, small runes orbiting close around it |
| `ability_a09.png` | **방패병의 수호** | 방패병 | 방어율 +12%, 체력 +6% | icy cyan | 땅에 박아 세운 타워 실드 | a tall tower shield planted into the ground, braced and immovable |
| `ability_a10.png` | **전사의 돌격** | 근접 | 공격력 +8%, 이속 +6% | ember orange-red | 앞으로 내지른 도끼날 | a broad axe head thrust forward with speed lines trailing behind the haft |
| `ability_a11.png` | **원거리 집중** | 원거리 | 사거리 +8%, 공격력 +6% | verdant green | 조준선이 겹친 화살촉 | a sharp arrowhead with a thin aiming reticle aligned just ahead of its tip |
| `ability_a12.png` | **장군의 위엄** | 장군 | 체력 +10%, 방어율 +6% | warm gold | 깃털 장식이 달린 장군 투구 | an ornate commander helm with a plumed crest, chin lifted |
| `ability_a13.png` | **병사의 투지** | 병사 | 공격력 +10%, 이속 +6% | bronze | 천을 묶은 평범한 병사 창 | a plain soldier spear raised upright, a cloth band knotted below the tip |
| `ability_a14.png` | **치명의 감각** | 전체 | 치명확률 +6%p | ember orange-red | 조준선이 비친 맹수의 눈 | a narrowed predatory eye with a thin crosshair glinting in the pupil |
| `ability_a15.png` | **넓은 시야** | 전체 | 사거리 +8% | pale lavender with gold accents | 길게 뽑은 놋쇠 망원경 | an extended brass spyglass with a bright lens flare at the far end |
| `ability_a16.png` | **병사 추가** | 장군 | 병사 수 +1 | bronze | 나란히 꽂힌 창들, 앞의 하나가 새로 박힌다 | a row of spears driven into the ground, the nearest one freshly planted and still glowing at the base |
| `ability_a17.png` | **지휘력 강화** | 장군 | 지휘력 +10 | bronze | 신호를 보내는 지휘봉 | a commander baton with a wrapped grip, raised in a signal gesture |

### 고급 (Advanced) — 14종

같은 축의 강화판이다. **일반판과 같은 물체를 더 무겁게** 그린다 — 나란히 놓으면 계보가 보여야 한다.

| 파일명 | 이름 | 대상 | 효과 | 색 | 주제 | Prompt |
|---|---|---|---|---|---|---|
| `ability_b01.png` | **철벽 체력** | 전체 | 최대체력 +15% | steel blue | 겹판을 덧댄 중장 흉갑 | a heavy reinforced breastplate of layered plates, a glowing rune set at the sternum |
| `ability_b02.png` | **강철 검격** | 전체 | 공격력 +15% | ember orange-red | 잔불이 서린 대검 | a massive greatsword blade wreathed in a thin ember glow along its edge |
| `ability_b03.png` | **폭풍 연격** | 전체 | 공격속도 +12% | ember orange-red | 소용돌이로 감기는 세 겹 참격 | three stacked crescent slashes spiraling into a small blade storm |
| `ability_b04.png` | **질풍 기동** | 전체 | 이동속도 +12% | pale lavender with gold accents | 돌풍을 남기는 날개 부츠 | a winged boot leaving a spiraling gust of wind twisting behind it |
| `ability_b05.png` | **기사의 분노** | 기사 | 공격력 +15%, 체력 +10% | crimson red | 볏이 타오르는 기사 투구 | a knight helm with its crest burning, the visor slit glowing red |
| `ability_b06.png` | **독수리의 눈** | 궁수 | 사거리 +15%, 공속 +12% | verdant green | 옆얼굴의 독수리 눈 | an eagle head in sharp profile, its golden eye locked on something distant |
| `ability_b07.png` | **마법의 각성** | 마법사 | 공격력 +18%, 쿨감 +15% | arcane violet | 갈라지며 빛이 터지는 지팡이 보석 | a staff gem cracking open, arcane light bursting through the fissures |
| `ability_b08.png` | **철옹성** | 방패병 | 방어율 +20%, 체력 +12% | icy cyan | 성벽 총안과 하나가 된 타워 실드 | a tower shield fused into a fortress crenellation, utterly immovable |
| `ability_b09.png` | **광전사의 기세** | 근접 | 공격력 +15%, 이속 +10% | ember orange-red | 붉은 기세에 휩싸인 전투 도끼 | a battle axe wrapped in a red aura of rage, haft gripped tight |
| `ability_b10.png` | **정밀 사격** | 원거리 | 사거리 +12%, 공격력 +12% | verdant green | 과녁 정중앙에 꽂힌 화살 | an arrow struck dead center of a target ring, its fletching still quivering |
| `ability_b11.png` | **영웅의 기상** | 장군 | 체력 +18%, 방어율 +12% | warm gold | 월계관을 두른 장군 투구 | a commander helm crowned with a laurel wreath, warm light rising behind it |
| `ability_b12.png` | **병사의 맹세** | 병사 | 공격력 +18%, 이속 +12% | bronze | 맹세하듯 곧게 세운 병사의 검 | a soldier sword held upright in an oath grip, a knotted vow cloth tied at the crossguard |
| `ability_b13.png` | **병사 대규모** | 장군 | 병사 수 +2 | bronze | 빽빽하게 솟은 창의 숲 | a dense forest of raised spears seen as a single mass, tips catching light |
| `ability_b14.png` | **완벽한 지휘** | 장군 | 지휘력 +20 | bronze | 펼쳐진 군기 | a command banner unfurled on its staff, gold trim catching the light |

### 특수 (Special) — 11종

스탯이 아니라 **발동 메커니즘**을 그린다. 금색 포인트를 1점만 넣는다.

| 파일명 | 이름 | 발동 | 효과 | 색 | 주제 | Prompt |
|---|---|---|---|---|---|---|
| `ability_c01.png` | **흡혈 강습** | 공격 시 | 준 피해의 15% 회복 | blood red with gold accent | 칼날을 거슬러 손잡이로 흐르는 핏방울 | a fanged blade with blood droplets running backward up the edge toward the grip |
| `ability_c02.png` | **철갑 반응** | 피격 시 | 맞을수록 방어율 누적 | steel blue with gold accent | 맞은 자리에서 판금이 솟는 방패 | a shield sprouting extra armor plates outward from a fresh impact dent |
| `ability_c03.png` | **처치 연쇄** | 적 처치 시 | 처치마다 공격력 +5% 누적 | ember orange-red with gold accent | 칼자국이 새겨진 검신 | a blade whose flat is scored with tally notches, each notch glowing brighter toward the tip |
| `ability_c04.png` | **희생의 힘** | 병사 사망 시 | 공격력·최대체력 +5% 누적 | bronze with gold accent | 쓰러진 투구에서 솟는 붉은 기운 | a fallen helm on the ground with a rising ribbon of red force drawn upward out of it |
| `ability_c05.png` | **고통의 계약** | 전투 시작 | 체력 70% 지불 → 공 +35%·공속 +20%·방어 +20% | blood red with gold accent | 가시 문양이 낙인된 손바닥 | an open palm branded with a thorned sigil, blood beading along the burned mark |
| `ability_c06.png` | **거울 방어** | 전투 시작 | 받은 피해 25% 반사 | icy cyan with gold accent | 화살을 되쏘는 거울 방패 | a mirror-polished shield reflecting an incoming arrow straight back outward |
| `ability_c07.png` | **혼령 집결** | 병사 사망 시 | 사망 누적당 능력 +5% | ghostly white-violet with gold accent | 낡은 투구 둘레로 모이는 혼불 | pale spirit flames gathering into a ring around a battered helm |
| `ability_c08.png` | **황금 탐욕** | 상시 | 골드 획득 +30% | warm gold | 금화가 넘치는 주머니 | a bulging coin pouch spilling gold coins over its loosened drawstring |
| `ability_c09.png` | **성장 촉진** | 상시 | 경험치 획득 +30% | warm gold | 빛의 화살이 솟는 펼친 책 | an open book with a rising arrow of light lifting off its glowing pages |
| `ability_c10.png` | **시간 왜곡** | 전투 시작 | 쿨감 +35%, 공격력 −10% | pale lavender with gold accents | 바늘이 앞으로 튀는 뒤틀린 회중시계 | a pocket watch with its hands spinning forward, the glass face warping outward |
| `ability_c11.png` | **쌍신 공격** | 전투 시작 | 2회 타격, 공격력 −40% | ember orange-red with gold accent | 동시에 그어진 두 줄의 참격 | twin parallel slash arcs cut in the same instant, the second a faint echo of the first |

### 달인 (Mastery) — 4종

직업 상징 + **연녹 광채**(`pale green`)를 반드시 넣는다. 이 계열만 광채 색이 다르다.

| 파일명 | 이름 | 효과 | 색 | 주제 | Prompt |
|---|---|---|---|---|---|
| `ability_d01.png` | **기사 달인** | 6초마다 자동 돌진 | crimson red with pale green glow | 앞으로 쏘아지는 기사 투구 | a knight helm charging forward, momentum lines streaming behind it, pale green light at the crest |
| `ability_d02.png` | **궁수 달인** | 다중 사격 | verdant green with pale green glow | 한 발이 세 갈래로 갈라지는 활 | a bow loosing a single shot that fans into three arrows, pale green trails |
| `ability_d03.png` | **마법사 달인** | 스킬 추가 발동 | arcane violet with pale green glow | 두 번째 고리가 겹쳐 도는 마법진 | an arcane circle with a second ring igniting over the first, sigils doubling in pale green |
| `ability_d04.png` | **방패병 달인** | 반경 2 광역 치유 오라 (최대체력 2%/초) | icy cyan with healing green | 치유 고리를 퍼뜨리는 타워 실드 | a tower shield emanating a wide ring of restorative green light around its base |

---

## 5. 저장·임포트 절차

1. 512×512 로 46장 생성 → **256×256** 축소 → 알파 임계값 0.35 정리.
2. `Assets/_project/3.Textures/Icons/Abilities/` 에 **기존 파일명 그대로** 덮어쓴다.
3. Unity 임포트 설정: `Texture Type = Sprite (2D and UI)`, `Alpha Is Transparency = ON`,
   `Filter Mode = Bilinear`, `Compression = None`, **`Max Size = 256`**, `Mesh Type = Full Rect`.
   — 기존 `.meta` 는 48px 시절 설정이라 **Max Size 를 반드시 올려야 한다.**
4. `Tools > Project K > 데이터 생성 > SpriteManager + SpriteAtlas` 로 `Atlas_Abilities` 재패킹.
5. `Tools > Project K > 데이터 생성 > 어빌리티` 로 SO 재연결 (아이콘 참조가 이름 기반이라 안전하다).

---

## 6. 검수 체크리스트

- [ ] 46장 전부 있고 파일명이 `ability_{AbilityId 소문자}.png` 와 일치한다
- [ ] 배경이 완전 투명하다 (배경판·원형 뱃지가 없다)
- [ ] **36px 로 줄여도 무엇인지 읽힌다** — 버프 슬롯 기준, 이 항목이 가장 많이 걸린다
- [ ] 우하단 1/4 이 비교적 비어 있다 (보상 카드 수량 배지와 겹치지 않는다)
- [ ] 어두운 보라 패드(`#3D3660`) 위에 올려도 주제가 분리된다
- [ ] 등급이 화려함의 단계로 읽힌다 (A → B → C → D 순으로 올라간다)
- [ ] 같은 스탯이라도 대상이 다르면 실루엣이 다르다 (A12 장군 vs A13 병사)
- [ ] 문자·숫자·워터마크가 없다
- [ ] 효과와 그림이 어긋나는 항목이 없다 (위 표의 `효과` 열 대조)

## 참조

- 어빌리티 정본: `Assets/_project/1.Script/InGame/Battle/Editor/AbilityCreator.cs`
- 등급 색: `Assets/_project/1.Script/UI/Popup/AbilityUIHelper.cs`
- 표시 칸 크기: `UI/Popup/Editor/AbilitySelectPopupCreator.cs`(124) ·
  `UI/Popup/Editor/AbilityListPopupCreator.cs`(96·72) ·
  `InGame/Battle/Editor/UISetupTool.cs`(보상 카드 128 · 버프 슬롯 36)
