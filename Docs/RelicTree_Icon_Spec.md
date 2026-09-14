# 유물 트리 아이콘 생성 명세 (46종)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/RelicTree/` — 46개 PNG  
해상도: **128×128** (프로젝트 전체 아이콘을 128로 올리는 중이다)  
짝 문서: `Docs/Icon_Regeneration_Spec.md` — 그쪽이 "유물은 개편 중이라 제외"로 남겨 둔 부분이 이 문서다.

## 왜 새로 필요한가

유물 습득이 카드 그리드에서 **테크트리**로 교체됐다(`Relic/Tree/RelicTreeCatalog.cs`).

> ⚠ **2026-09-07 에 트리를 통째로 갈아 끼웠다 (69 → 46).**
> 원작 트리는 갈래가 공격/체력/병사/유틸이었는데 그 축은 *장수와 병사*의 것이다.
> 이 게임은 진영이 뒤집혀 장수·병사가 전부 **적(용사)** 이라, 69노드 중 40개가
> 사실상 "적을 강화하는 유물" 이었다. 옛 이름으로 구워 둔 PNG 60장은 고아다 —
> `아이콘·텍스처 > 유물 트리 고아 아이콘 정리` 로 걷어낼 것.

구 유물 아이콘 `Icons/Relics/` 29장은 구 `RelicId` 기준이라 트리 노드와 1:1로 맞지 않는다.
현재 `RelicTreePopup` 은 아이콘이 없어 계열 색 타일만 그리고 있다.

> ⚠ `Icons/Relics/` 29장을 재사용하거나 덮어쓰지 말 것  
> 구 `RelicData` SO 가 아직 그 스프라이트를 참조한다. 트리는 **새 폴더**를 쓴다.

## 파일 규칙

| 항목 | 값 |
|---|---|
| 저장 경로 | `Assets/_project/3.Textures/Icons/RelicTree/` |
| 파일명 | `node_<enum 이름 snake_case>.png` (예: `N_Claw` → `node_claw.png`) |
| 해상도 | 128×128 |
| 형식 | PNG, 알파 채널 포함, 배경 완전 투명 |
| 아틀라스 | 신규 `Atlas_RelicTree.spriteatlas` 에 폴더째 등록 |

파일명은 `RelicNodeId` enum 이름에서 `N_` 를 떼고 snake_case 로 바꾼 것이다.

> **변환 규칙의 정본은 `Relic/Tree/RelicIconKey.cs` 다.**
> 런타임 조회·자리표시 생성·이 문서 셋이 같은 규칙을 써야 한다.
> 한 곳이라도 어긋나면 그림이 **에러 없이 조용히 안 붙는다.**

## 공통 시각 언어

| 요소 | 방향 | 의도 |
|---|---|---|
| 배경 | 완전 투명. 원형·사각 배경판을 그리지 말 것 | 노드 Face 가 계열 색 판을 이미 깔고 그 위에 아이콘만 얹는다 |
| 주제 | 한 아이콘에 하나의 읽히는 도구·행위·상태 | 트리에서 52px 로 축소돼 표시된다 — 두 개를 넣으면 뭉갠다 |
| 여백 | 중앙 주제 88~104px, 외곽 여백 12~20px | 마름모(특수 노드)로 45° 회전해도 잘리지 않는다 |
| 외곽선 | 어두운 2~3px 실루엣 외곽선 | 어떤 계열 색 판 위에서도 형태가 분리된다 |
| 광원 | 좌상단 단일 광원, 플랫 셰이딩 | 46장이 한 화면에 같이 보인다 — 광원이 흔들리면 잡동사니가 된다 |
| 색 | 계열 색을 주조로, 강조 1색만 추가 | 계열을 색으로 먼저 읽고 형태로 확인한다 |
| 금지 | 문자·숫자·워터마크·프레임·테두리 장식·드롭섀도 | 프레임은 노드 Face 가 그린다. 겹치면 두 겹이 된다 |

### 계열 색 팔레트

`RelicTreePopup.ColorOf()` 의 값 그대로다. 아이콘 주조색을 여기에 맞춘다.

| 계열 | HEX | 영문 색 이름 (프롬프트에 넣을 것) | 노드 수 |
|---|---|---|---:|
| 뿌리 | `#D9B238` | warm gold | 1 |
| 소환수 (위) | `#E0693D` | ember orange-red | 12 |
| 마왕성 (아래) | `#4F99C4` | steel blue | 12 |
| 마나 (왼쪽) | `#C79438` | bronze | 8 |
| 통솔 (오른쪽) | `#9978D1` | amethyst violet | 13 |

> ⚠ `RelicTreePopup` 의 **색 상수 이름은 옛것 그대로**다 (`AttackColor`·`DefenseColor`·
> `SoldierColor`·`UtilityColor`). 계열이 바뀌며 짝만 옮겼다 — 이름까지 바꾸면
> Creator 와 이 문서를 함께 고쳐야 해서 두었다. 색값이 정본이다.

### 특수 노드 (마름모) — 9종

`MaxLevel == 1` 인 노드는 팝업이 Face 를 45° 회전시켜 마름모로 그린다.
**아이콘은 회전하지 않는다**(`RelicTreePopup` 이 반대로 되돌린다). 다만 마름모 안에 들어가므로
네 모서리로 뻗는 형태는 피하고 원형에 가까운 실루엣을 쓴다.

대상: `node_forge_memory`, `node_demon_horde`, `node_time_reins`, `node_reinforce_seal`, `node_early_bloom`, `node_moment_mastery`, `node_infinite_vessel`, `node_eternal_throne`, `node_doom_prophecy`

## 공통 프롬프트

각 노드 프롬프트 앞에 그대로 붙인다. `{BRANCH}` 자리에 계열 영문 색 이름을 넣는다.

```
128x128 game icon, dark fantasy, single centered subject,
flat shading with one light source from the upper left,
crisp dark outline, {BRANCH} as the dominant color with one accent hue,
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

### 생성 해상도 — 표시 크기가 기준이다

이 아이콘이 실제로 보이는 곳은 **트리 노드 한 곳뿐이고, 그 칸은 52px 이다.**
그래서 최종 파일은 128×128 이면 충분하다 (노출 52 × 2, 2의 거듭제곱 올림).

모델에 128×128 를 직접 요구하지 말 것 — 뭉개진다.
**512×512 로 생성한 뒤 128×128 로 축소**하고, 축소 후 알파 가장자리를 정리한다.
1024 원본을 남길 이유는 없다 — 52px 로 줄어들 그림이라 디테일을 더 넣을수록 축소에서 뭉개지기만 한다.
축소 필터는 Lanczos/Area 를 쓰고, 반투명으로 번진 외곽 픽셀은 알파 임계값 0.35 로 자른다.

**검수는 52px 축소본으로 한다.** 128px 에서 예쁜지가 아니라 52px 에서 계열과 주제가
읽히는지가 합격 기준이다.

## 노드별 프롬프트 (46종)

`주제` 는 검수용 한국어 설명이고, `Prompt` 가 공통 프롬프트 뒤에 붙는 본문이다.
`효과` 는 아이콘이 무엇을 뜻해야 하는지 판단하는 근거다 — 그림이 효과와 어긋나면 다시 만든다.

### 뿌리

주조색 `#D9B238` (warm gold)

| 파일명 | 노드 | T | 효과 (레벨당) | 주제 | Prompt |
|---|---|:-:|---|---|---|
| `node_origin.png` | **마왕의 각인** | 0 | 공격력 +5% · 체력 +5% | 네 갈래로 뻗는 금빛 룬 각인 | an engraved demonic rune sigil pulsing with light, four faint branch lines radiating outward |

### 소환수 (위)

주조색 `#E0693D` (ember orange-red)

| 파일명 | 노드 | T | 효과 (레벨당) | 주제 | Prompt |
|---|---|:-:|---|---|---|
| `node_claw.png` | **날카로운 발톱** | 1 | 공격력 +4% | 휘어진 짐승 발톱 셋 | three curved beast claws splayed outward, chipped keratin edges catching a highlight |
| `node_feral_memory.png` | **야성의 기억** | 2 | 종족 패시브 수치 +10% | 피어오르는 짐승 두개골 잔영 | a beast skull wreathed in drifting spirit vapor, older skull afterimages behind it |
| `node_carapace.png` | **굳은 껍질** | 2 | 체력 +5% | 각질로 굳은 등딱지 조각 | a hardened chitinous carapace plate, overlapping ridged segments |
| `node_split_legacy.png` | **분열의 유산** | 3 | 분열·재조립체 배율 +7% | 둘로 갈라지는 점액 덩이 | a gelatinous blob mid-split into two smaller blobs, a stretching strand between them |
| `node_fang.png` | **사나운 이빨** | 3 | 공격력 +5% | 아래턱에서 솟은 송곳니 한 쌍 | a pair of jagged fangs rising from a lower jaw, saliva glint on the tips |
| `node_thick_hide.png` | **두꺼운 가죽** | 3 | 방어율 +2%p | 두껍게 겹친 짐승 가죽 | a thick layered hide pelt with coarse bristles along the seam |
| `node_forge_memory.png` | **대장간의 기억** | 3 | 런 종료 장비 상자 +1 | 불씨가 남은 대장간 모루 | a blacksmith anvil with embers still glowing in the scale on its face |
| `node_death_toll.png` | **죽음의 대가** | 4 | 사망 발동 패시브 +25% | 금 간 두개골에서 터지는 파동 | a cracked skull bursting outward with a shockwave ring of dark energy |
| `node_predator.png` | **포식자** | 4 | 치명타율 +3%p · 치명타 피해 +12% | 세로 동공의 포식자 눈 | a slit-pupiled predator eye with a crosshair glint in the iris |
| `node_undying_flesh.png` | **불사의 살점** | 4 | 체력 +10% | 아물어 붙는 살점 | a knot of regenerating flesh stitching itself closed, faint sinew threads |
| `node_bespoke.png` | **맞춤 제작** | 4 | 몬스터 장비 스탯 +20% | 짐승 몸에 맞춰 재단한 갑주 조각 | a custom-fitted armor piece shaped for a non-human frame, measuring calipers resting on it |
| `node_demon_horde.png` | **마왕의 군세** | 5 | 공격력 +12% · 체력 +12% | 겹쳐 선 뿔 달린 실루엣 무리 | a massed silhouette of horned monsters standing shoulder to shoulder, the front one in sharpest relief |

### 마왕성 (아래)

주조색 `#4F99C4` (steel blue)

| 파일명 | 노드 | T | 효과 (레벨당) | 주제 | Prompt |
|---|---|:-:|---|---|---|
| `node_thick_gate.png` | **두꺼운 성문** | 1 | 마왕성 체력 +1 | 쇠띠를 두른 두꺼운 성문 | a heavy iron-banded castle gate, thick timbers and riveted straps |
| `node_sealed_wall.png` | **봉인된 성벽** | 2 | 마왕성 체력 +1 | 봉인 룬이 박힌 성벽 | a fortress wall section with a glowing seal rune set into the stone |
| `node_trial_baptism.png` | **시련의 세례** | 2 | 용사 최대 체력 −2.5% | 성수 세례에 갈라지는 방패 | a knightly shield cracking as consecrated water pours over it |
| `node_rebuild.png` | **재건** | 3 | 야영지 회복량 +1 | 무너진 벽을 다시 쌓는 돌덩이 | stone blocks lifting back into a breached wall, mortar fresh at the seams |
| `node_demon_majesty.png` | **마왕의 위엄** | 3 | 소환사 평타 비율 +2%p | 압도하는 마왕의 뿔관 | a demonic crown of horns radiating an oppressive aura |
| `node_slow_march.png` | **느려진 진군** | 3 | 용사 이동속도 −5% | 진창에 잠기는 군화 | a marching boot sinking into thick clinging mire, mud roping off the sole |
| `node_fear_brand.png` | **공포의 각인** | 3 | 용사 공격력 −2.5% | 공포로 뒤틀린 낙인 | a branding iron mark twisted into a screaming face |
| `node_unbroken_keep.png` | **불락의 성** | 4 | 마왕성 체력 +2 | 금 하나 없는 아성 | an unbroken keep tower standing solid, not a single crack in the masonry |
| `node_wither_curse.png` | **쇠약의 저주** | 4 | 용사 최대 체력 −4% | 메말라 오그라드는 손 | a withering hand shriveling to bone, skin flaking away as dust |
| `node_disarm.png` | **무력화** | 4 | 용사 공격력 −4% | 자루째 부러진 검 | a heroic sword snapped clean at the hilt, the blade falling away |
| `node_eternal_throne.png` | **영원한 옥좌** | 5 | 마왕성 체력 +3 | 영원히 식지 않는 옥좌 | an obsidian throne haloed by an unfading dark radiance |
| `node_doom_prophecy.png` | **몰락의 예언** | 5 | 용사 최대 체력 −12% | 파멸을 적은 찢어진 예언서 | a torn prophecy scroll bleeding dark ink, doom glyphs unraveling |

### 마나 (왼쪽)

주조색 `#C79438` (bronze)

| 파일명 | 노드 | T | 효과 (레벨당) | 주제 | Prompt |
|---|---|:-:|---|---|---|
| `node_wide_vessel.png` | **넓은 그릇** | 1 | 최대 마나 +4% | 입이 넓은 마력 그릇 | a wide-mouthed arcane bowl brimming with luminous mana |
| `node_frugality.png` | **절약** | 2 | 소환 비용 −0.5 | 한 방울만 떨구는 마력병 | a mana vial releasing a single careful drop, the rest held back |
| `node_deep_spring.png` | **깊은 샘** | 2 | 최대 마나 +5% | 바닥이 안 보이는 마력 샘 | a deep spring shaft with mana light rising from an unseen bottom |
| `node_patience.png` | **인내** | 3 | 과부하 계수 −0.01 | 느리게 떨어지는 모래시계 | an hourglass with the sand falling unusually slowly, grains suspended |
| `node_flowing_mana.png` | **흐르는 마력** | 3 | 스테이지 마나 회복 +8% | 끊이지 않고 흐르는 마력 줄기 | a continuous ribbon of mana flowing in an unbroken loop |
| `node_meditation_crystal.png` | **명상의 결정** | 4 | 스테이지 마나 회복 +12% | 고요히 떠 있는 명상 결정 | a meditation crystal floating in a serene posture, soft inner glow |
| `node_endless_well.png` | **마르지 않는 우물** | 4 | 최대 마나 +10% | 넘쳐도 마르지 않는 우물 | a stone well overflowing with mana that never drains |
| `node_infinite_vessel.png` | **무한의 그릇** | 5 | 최대 마나 +20% | 안이 무한히 이어지는 그릇 | a vessel whose interior opens into an endless recursive void of mana |

### 통솔 (오른쪽)

주조색 `#9978D1` (amethyst violet)

| 파일명 | 노드 | T | 효과 (레벨당) | 주제 | Prompt |
|---|---|:-:|---|---|---|
| `node_call_horn.png` | **부름의 나팔** | 1 | 라인 배출 속도 +5% | 소리를 뿜는 뿔나팔 | a curved war horn blasting visible sound rings outward |
| `node_insight.png` | **안목** | 2 | 카드 3택 선택지 +1 | 펼쳐진 카드 위의 눈 | an open eye above three fanned cards, seeing what they hold |
| `node_open_gate.png` | **성문 개방** | 2 | 라인 배출 속도 +8% | 활짝 열린 성문 | a portcullis gate thrown fully open, chains slack at the sides |
| `node_plunder_hand.png` | **약탈의 손** | 2 | 런 골드 +12% | 금화를 움켜쥔 손 | a clawed hand clutching a fistful of gold coins, some spilling between the fingers |
| `node_time_reins.png` | **시간의 고삐** | 2 | 전투 배속 해금 +1 | 시간을 당기는 고삐 | reins pulled taut around a clock hand, time held in check |
| `node_attunement.png` | **조율** | 3 | 시너지 동 문턱 −1 | 맞물려 공명하는 톱니 셋 | three interlocking gears clicking into resonant alignment |
| `node_sprint.png` | **질주** | 3 | 이동속도 +8% | 잔상을 남기는 달리는 발 | a monstrous foot mid-stride leaving speed afterimages |
| `node_soul_urn.png` | **영혼의 항아리** | 3 | 환생 포인트 +12% | 영혼이 담긴 항아리 | a sealed urn with wisps of soul light escaping the lid seam |
| `node_moment_mastery.png` | **찰나의 지배** | 4 | 전투 배속 해금 +1 | 멈춰 선 찰나의 모래알 | a single grain of sand frozen in midair inside a stopped hourglass |
| `node_legacy_price.png` | **전승의 대가** | 4 | 특성 선택지 +1 | 대가를 치르고 받는 인장 | an outstretched hand paying a coin for an offered sigil, an exchange implied |
| `node_reinforce_seal.png` | **증원의 인장** | 4 | 카드당 마릿수 +1 | 증원을 부르는 소환 인장 | a summoning seal with extra silhouettes rising from its glyph ring |
| `node_early_bloom.png` | **조기 성장** | 4 | 새 카드 시작 레벨 +1 | 때 이르게 핀 검은 꽃 | a dark flower already in full bloom while its neighbors are still buds |
| `node_resonance_tome.png` | **공명의 서** | 4 | 시너지 중첩 보너스 +2%p | 공명하는 파문이 이는 서책 | an open tome radiating concentric resonance ripples from its pages |

## 작업 절차 (2026-08-25 개정 — 연동 완료 기준)

### 그림을 만드는 쪽

1. 512×512 로 46장 생성 → **256×256** 으로 축소 → 알파 임계값 0.35 로 정리.
2. `Assets/_project/3.Textures/Icons/RelicTree/` 의 같은 이름 파일에 **덮어쓴다.**
   (자리표시 PNG 를 먼저 구우면 46장 전부 그 이름으로 깔린다 — 새 파일을 만들지 말고 덮어쓸 것)
3. Unity 를 켠다. **임포트 설정은 자동으로 붙는다** (`RelicTreeIconImporter`).
4. `Tools > Project K > 데이터 생성 > SpriteManager + 아틀라스` 실행 → 아틀라스에 반영.
5. 유물 화면을 열어 확인한다.

> ⚠ **해상도가 128 → 256 으로 바뀌었다**
> 이 명세를 쓸 당시 노드 아이콘 노출 크기는 52px 이었다. 그 뒤 노드를 키우면서
> 실효 노출이 **약 91px** 이 됐다(아이콘 140 캔버스px × `_zoomInit` 0.65).
> 128 은 확대 배율을 조금만 올려도 부족해진다. 2배 여유를 두어 256 으로 만들 것.
> 임포터의 `maxTextureSize` 도 256 이다.

### 파일명이 어긋나면 그림이 조용히 안 붙는다

에러가 나지 않는다 — 아틀라스에서 못 찾으면 **자리표시가 그대로 남는다.**
헷갈리면 `Tools > Project K > 아이콘·텍스처 > 유물 트리 아이콘 누락 점검` 을 실행한다
(파일이 아예 없는 노드를 콘솔에 나열한다).

### 관련 도구

| 메뉴 | 하는 일 |
|---|---|
| `아이콘·텍스처 > 유물 트리 자리표시 생성 (빈 자리만)` | 파일이 없는 노드만 자리표시로 채운다. **진짜 그림은 안 건드린다** |
| `아이콘·텍스처 > 유물 트리 자리표시 강제 재생성` | 46장 전부 자리표시로 덮어쓴다 (확인 대화상자 있음) |
| `아이콘·텍스처 > 유물 트리 아이콘 임포트 재적용` | 이미 임포트된 파일에 설정을 다시 얹는다 |
| `아이콘·텍스처 > 유물 트리 아이콘 누락 점검` | 파일이 없는 노드를 콘솔에 나열 |
| `데이터 생성 > SpriteManager + 아틀라스` | `Atlas_RelicTree` 재패킹 + SpriteManager 갱신 |

### 자동 적용되는 임포트 설정

`Texture Type = Sprite (2D and UI)` · `Alpha Is Transparency = ON` ·
`Mesh Type = Full Rect` · `Filter Mode = Bilinear` · `Compression = None` ·
`Max Size = 256` · `Mipmap = OFF` · `Wrap = Clamp`

> `Mesh Type = Full Rect` 가 중요하다. `Tight` 는 알파를 따라 불규칙 다각형으로 잘려,
> 특수 노드(마름모)에서 Face 를 45° 돌릴 때 모서리가 날아간다.

> ✅ **연동은 끝났다 (2026-08-25).** 이 폴더에 규칙에 맞는 이름으로 PNG 를 넣으면 그대로 붙는다.
> `RelicTreePopup.BuildNode()` 가 `SpriteManager.Instance.Get(RelicIconKey.Of(id))` 로 찾는다.
> 지금은 46장 전부 **자리표시(계열 색 원반 + 티어 눈금)** 로 채워져 있다 — 그 위에 덮어쓰면 된다.
> 자세한 절차는 아래 '작업 절차' 참고.

## 검수 체크리스트

- [ ] 46장 전부 있고 파일명이 `RelicNodeId` snake_case 와 일치한다
- [ ] 배경이 완전 투명하다 (배경판·원형 뱃지가 없다)
- [ ] 128px 에서 주제가 하나로 읽힌다 — 52px 로 줄여도 형태가 남는다
- [ ] 문자·숫자·워터마크가 없다
- [ ] 계열 주조색이 팔레트와 맞다 — 같은 계열끼리 나란히 놓고 확인한다
- [ ] 특수 노드 9종이 마름모 안에서 잘리지 않는다 (45° 회전 마스크로 확인)
- [ ] 효과와 그림이 어긋나는 노드가 없다 (위 표의 `효과` 열 대조)

## 참조

- 노드 표 정본: `Assets/_project/1.Script/Relic/Tree/RelicTreeCatalog.cs`
- 노드 타입·enum: `Assets/_project/1.Script/Relic/Tree/RelicTreeTypes.cs`
- 계열 색·표시 규칙: `Assets/_project/1.Script/UI/Popup/RelicTreePopup.cs`
- 나머지 아이콘 명세: `Docs/Icon_Regeneration_Spec.md`
