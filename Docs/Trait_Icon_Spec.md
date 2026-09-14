# 특성 아이콘 생성 명세 (52종)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/Traits/` — 52개 PNG  
해상도: **128×128** (프로젝트 전체 아이콘을 128로 올리는 중이다)  
짝 문서: `Docs/Icon_Regeneration_Spec.md`(묶음 개요) · `Docs/RelicTree_Icon_Spec.md`(유물 트리)

특성 아이콘은 인게임 HUD·이벤트 팝업·로비 상단바에서 **전부 `TraitIconUI` 로 표시된다.**
표시 크기가 작은 곳이 많아 실루엣이 하나로 읽히는 것이 가장 중요하다.

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
| 기사 | 적색 | crimson red |
| 궁수 | 녹색 | forest green |
| 마법사 | 보라 | arcane violet |
| 방패병 | 청록 | steel cyan |
| 공통·성장 | 금색 | warm gold |
| 전환(환산) | 청동 | bronze |
| 이벤트 · 페널티 | 자주·회녹 | muted crimson / sickly green |
| 시너지 | 은백 + 직업색 | silver with the class hue |

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

### 분류가 색으로 먼저 읽혀야 한다

52종이라 형태만으로는 못 나눈다. **직업 특성은 직업색, 시너지는 은백 바탕에 관련 직업색**을
섞어 한 화면에 늘어놨을 때 묶음이 보이게 한다.

- **전환 특성 7종**은 "A 가 B 로 바뀐다"가 그림에 보여야 한다 — 두 사물이 맞물린 형태로 그린다.
- **이벤트 특성 8종** 중 페널티(부작용·제단의 저주·피의 계약)는 탁하고 어두운 색으로 낮춘다.
- **시너지 특성 13종**은 개수가 곧 의미다 — 3스택은 3개, 5스택은 5개를 실제로 그린다.

## 특성별 프롬프트

### 직업 특성 — 기사

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_knight_command.png` | **지휘관의 기질** | 전장을 호령하는 기사의 타고난 통솔력. | 지휘봉이 교차한 기사 투구 | a knight helm with a commander baton crossed behind it |
| `trait_knight_soldier_rage.png` | **전우의 분노** | 병사가 쓰러질 때마다 장군의 최대 체력이 영구적으로 1% 증가한다. | 전우의 투구가 붉은 심장을 키운다 | a fallen comrade helm feeding a rising red heart |
| `trait_knight_hero_return.png` | **영웅의 귀환** | 장군이 쓰러지는 순간, 마지막 기개로 병사들을 전장에 새롭게 소환한다. | 쓰러지며 병사를 다시 부르는 기사 | a knight rising on one knee as soldier silhouettes reform around him |
| `trait_knight_martyr.png` | **순교** | 쓰러진 병사가 마지막으로 불타오른다. 병사가 사망한 자리에서 폭발이 일어나 | 쓰러진 자리에서 타오르는 순교의 폭발 | a fallen soldier bursting into a martyr flame explosion |

### 직업 특성 — 궁수

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_archer_precision.png` | **정밀 사수** | 숨을 죽이고 조준하는 궁수의 집중력. | 조준선에 고정된 시위와 화살 | an arrow nocked and held steady on a tight crosshair |
| `trait_archer_rain_fire.png` | **폭우 사격** | 공격할 때마다 주변의 적 2명을 추가로 타격한다. 추가 피해는 70%. | 세 갈래로 갈라져 날아가는 화살 | one arrow splitting into three toward separate targets |

### 직업 특성 — 마법사

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_mage_arcane.png` | **마력 집중** | 내면의 마력을 극한까지 끌어올린 마법사의 각성. | 두 손 사이로 모이는 마력의 한 점 | arcane energy converging into a bright point between two hands |
| `trait_mage_attack_cdr.png` | **마법 집중** | 공격할 때마다 액티브 스킬 쿨타임이 1초 감소한다. | 거꾸로 도는 시계바늘과 지팡이 타격 | a staff strike with a clock hand ticking backward |
| `trait_mage_echo_skill.png` | **연속 시전** | 스킬을 사용하면 즉시 동일한 스킬이 40% 위력으로 재발동된다. | 뒤에 옅은 잔상 문양이 겹친 마법진 | a spell sigil with a fainter echo sigil layered behind it |

### 직업 특성 — 방패병

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_shield_fortress.png` | **강철 요새** | 흔들리지 않는 방패병의 육체. | 성벽 블록과 하나가 된 방패 | a shield merged into a fortress wall block |
| `trait_shield_counter_blow.png` | **반격의 달인** | 피격 시 받은 피해를 그대로 공격자에게 반사한다. | 들어온 타격을 가시로 되돌리는 방패 | a shield reflecting an incoming blow back as a spike |
| `trait_shield_rage_build.png` | **분노 축적** | 스테이지를 클리어할 때마다 공격력이 3% 증가한다. 최대 10회 중첩. | 면에 분노 눈금이 쌓이는 방패 | a shield with rage marks stacking up its face |

### 공통 — 배치 슬롯·성장

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_common_expedition.png` | **원정 편성** | 원정을 편성해 더 많은 장수를 전장에 내보낸다. 배치 슬롯은 최대 5칸까지 늘어난다. | 깃발 핀이 하나 더 꽂힌 원정 지도 | a march route map with one extra banner pin added |
| `trait_common_mass_mobilize.png` | **대규모 동원** | 머릿수로 밀어붙이는 총동원령. 배치 슬롯은 최대 5칸까지 늘어난다. | 작은 깃발이 빼곡한 동원 북, 하나는 갈라졌다 | a mobilization drum with many small banners, one of them cracked |
| `trait_common_soldier_supply.png` | **병사 지원령** | 본국에서 병력을 추가로 보내온다. 배치 슬롯은 최대 5칸까지 늘어난다. | 창과 군기를 실어 오는 보급 마차 | a supply wagon delivering spears and a banner |
| `trait_common_forced_levy.png` | **무리한 징집** | 머릿수는 채웠지만 훈련이 부족하다. 배치 슬롯은 최대 5칸까지 늘어난다. | 기둥에 박힌 징집 방문과 끌리는 군화 | a conscription notice nailed to a post, a dragging boot below |
| `trait_common_equip_expand.png` | **중무장 편성** | 보급 마차를 늘려 장비를 더 챙겨 다닌다. | 칸이 하나 더 늘어난 장비 가방 | an expanded gear satchel with one extra equipment slot |
| `trait_common_late_bloom.png` | **대기만성** | 느리게 여무는 그릇. 스테이지를 클리어할 때마다 공격력과 최대 체력이 1%씩 오른다. | 갑주 가지에서 늦게 벌어지는 꽃봉오리 | a slow-opening bud on an armored branch with rising rings |

### 스탯 전환 (A → B 환산)

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_conv_heavy_armor.png` | **중갑** | 두꺼운 갑주 자체가 무기가 된다. 방어율 1%p마다 공격력이 1.5% 오른다. | 끝이 날로 벼려진 두꺼운 어깨 갑옷 | a thick pauldron whose edge sharpens into a blade |
| `trait_conv_titan.png` | **거인** | 거대한 몸집에서 나오는 완력. 최대 체력 1000마다 공격력이 6% 오른다. | 심장 윤곽에서 뻗어 나온 거대한 주먹 | a giant fist formed out of a heart outline |
| `trait_conv_swift.png` | **속공** | 발이 빠른 만큼 손도 빠르다. 이동속도 1마다 공격속도가 12% 오른다. | 자취가 검격으로 이어지는 날개 달린 군화 | a winged boot whose trail becomes a swinging blade arc |
| `trait_conv_sage.png` | **현자** | 마력의 회전이 곧 파괴력이다. 스킬 쿨감 1%p마다 공격력이 2% 오른다. | 마법 구슬로 쏟아져 불붙는 모래시계 | an hourglass pouring into a spell orb that ignites |
| `trait_conv_warlord.png` | **군단장** | 등 뒤의 병사가 많을수록 검이 무거워진다. 병사 1명마다 공격력이 3% 오른다. | 병사 표식만큼 무거워진 지휘검 | a commander sword weighted down by many small soldier marks |
| `trait_conv_marksman.png` | **명사수** | 멀리 볼수록 정확히 꽂힌다. 공격 사거리 1마다 공격력이 6% 오른다. | 긴 화살대와 나란히 맞춘 원시 렌즈 | a far-sight lens aligned with a long arrow shaft |
| `trait_conv_bulwark.png` | **육중** | 단단한 것은 곧 질기다. 방어율 1%p마다 최대 체력이 2% 오른다. | 안에서 심장이 뛰는 육중한 어깨 갑옷 | a heavy anvil-like pauldron with a heart beating inside |

### 치명타

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_crit_assassin.png` | **암살자의 눈** | 급소만 노리는 눈썰미. 치명타 확률과 배율이 함께 오른다. | 동공에 단검 섬광이 비친 후드 쓴 눈 | a hooded eye with a dagger glint in the pupil |
| `trait_crit_executioner.png` | **처형인** | 한 번에 끝낸다. 치명타 배율이 크게 오르는 대신 평타가 무뎌진다. | 처형인의 두건과 이 빠진 육중한 칼 | an executioner hood beside a heavy notched blade |
| `trait_crit_deadeye.png` | **필살** | 노림수가 쌓일수록 일격이 깊어진다. 치명타 확률 1%p마다 치명타 배율이 1.5% 오른다. | 한 점으로 압축되는 조준 고리 | a crosshair whose rings compress into a single lethal point |

### 공격속도

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_haste_frenzy.png` | **광란** | 정확함을 버리고 속도를 택한다. 공격속도가 크게 오르는 대신 공격력이 줄어든다. | 칼 하나가 부러지는 흐릿한 난타 | a blurred flurry of strikes with one blade snapping |
| `trait_haste_rend.png` | **파쇄** | 때릴 때마다 갑주가 갈라진다. 공격이 적중할 때마다 대상 최대 체력의 2%를 | 연타에 쪼개지는 갑주 판 | armor plating splitting apart under repeated hits |

### 이벤트 전용

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_event_battle_will.png` | **전투 의지** | 구해준 부상병이 전한 각오가 부대에 번진다. | 주먹을 내미는 붕대 감은 병사 | a bandaged soldier passing a clenched fist forward |
| `trait_event_potion_buff.png` | **활력의 묘약** | 약장수의 묘약이 몸에 잘 맞았다. | 따뜻하게 빛나는 초록 활력 약병 | a green vitality flask glowing warmly |
| `trait_event_potion_debuff.png` | **부작용** | 묘약의 부작용으로 몸이 무겁다. | 탁한 침전물이 흘러내리는 약병 | a murky flask with a downward sludge drip |
| `trait_event_blood_pact.png` | **피의 계약** | 제단에 피를 바쳐 힘을 얻었다. 그 대가는 자신의 생명력이었다. | 어두운 제단 위의 피 봉헌 그릇 | a blood offering bowl on a dark altar |
| `trait_event_altar_curse.png` | **제단의 저주** | 제단을 부수려다 저주를 뒤집어썼다. | 저주 안개가 새는 갈라진 제단 | a cracked altar leaking a curse mist |
| `trait_event_execution_morale.png` | **처형의 사기** | 첩자를 처형해 군율을 다시 세웠다. | 군율 깃발이 선 처형대 | an executioner block with a raised discipline banner |
| `trait_event_spy_info.png` | **첩자 정보** | 첩자에게서 적진의 정보를 얻어냈다. | 찢긴 적진 지도와 복면 첩자의 증표 | a torn enemy map and a masked informer token |
| `trait_event_veteran_heritage.png` | **노병의 유산** | 방랑 노병이 남긴 행군 요령. | 닳은 노병의 군화와 지팡이 | an old veteran worn boots and walking staff |

### 직업 시너지 — 조합형

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_synergy_vanguard.png` | **선봉대** | 기사와 궁수가 함께 전진하며 화력을 맞물린다. | 교차한 기사의 검과 궁수의 활 | a knight sword and an archer bow crossed in advance |
| `trait_synergy_magic_shield.png` | **마법 방패** | 기사의 방어선 뒤에서 법사가 마법을 펼친다. | 마법 문양이 빛나는 기사 방패 | a knight shield with an arcane sigil glowing on its face |
| `trait_synergy_iron_wall.png` | **철벽진** | 기사와 방패병이 이중 방어선을 형성한다. | 이중으로 겹친 두 개의 방패 | two overlapping shields forming a double line |
| `trait_synergy_balanced.png` | **균형의 군세** | 네 직업이 모두 갖춰진 완전한 군대. | 균형을 이룬 네 직업 문장 | four class emblems arranged in a balanced ring |

### 직업 시너지 — 5스택

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_synergy_knight_order.png` | **기사단** | 다섯 기사의 창이 하나로 모인다. | 한 점으로 모이는 다섯 자루의 창 | five knight lances converging to a single point |
| `trait_synergy_arrow_legion.png` | **화살의 군단** | 하늘을 가리는 화살의 비. | 하늘을 가리는 화살의 비 | a dense sky-filling volley of arrows |
| `trait_synergy_mage_corp.png` | **대법사단** | 다섯 법사의 마력이 공명한다. | 공명하는 다섯 지팡이와 중앙의 마력핵 | five staves resonating around a shared arcane core |
| `trait_synergy_ironclad.png` | **철옹성** | 무너지지 않는 철벽. | 맞물려 무너지지 않는 방패 벽 | an unbreakable wall of interlocked shields |

### 직업 시너지 — 복합·3스택

| 파일명 | 특성 | 설명 | 주제 | Prompt |
|---|---|---|---|---|
| `trait_synergy_ranged_firenet.png` | **원거리 화망** | 궁수와 법사가 원거리 화망을 펼친다. | 그물처럼 얽힌 화살과 마법 궤적 | arrow and spell trajectories weaving into a net |
| `trait_synergy_iron_vanguard.png` | **철벽 전위대** | 기사와 방패병이 함께 전선을 지킨다. | 한 전선을 지키는 기사와 방패병 | a knight and a shieldbearer holding one front line |
| `trait_synergy_knight_squad.png` | **기사 소대** | 세 기사가 대열을 이룬다. | 대열을 이룬 세 자루의 기사창 | three knight lances in formation |
| `trait_synergy_archer_squad.png` | **궁수 소대** | 세 궁수가 일제히 사격한다. | 일제히 시위를 놓는 세 개의 활 | three bows loosing in unison |
| `trait_synergy_mage_squad.png` | **법사 소대** | 세 법사가 마력을 모은다. | 작은 불씨를 함께 모으는 세 지팡이 | three staves gathering a shared spark |
| `trait_synergy_shield_squad.png` | **방패병 소대** | 세 방패병이 방패를 맞댄다. | 가장자리를 맞댄 세 개의 방패 | three shields braced edge to edge |

## 생성 후 절차

1. 1024×1024 로 생성 → 128×128 축소 → 알파 임계값 정리.
2. `Assets/_project/3.Textures/Icons/Traits/` 의 **기존 파일을 같은 이름으로 덮어쓴다**.
   경로·파일명·`.meta` 를 유지하면 SO 참조와 아틀라스 연결이 그대로 살아 있다.
3. 임포트 설정: `Texture Type = Sprite (2D and UI)`, `Alpha Is Transparency = ON`,
   `Filter Mode = Bilinear`, `Compression = None`, `Max Size = 128`, `Mesh Type = Full Rect`.
4. `Atlas_Traits.spriteatlas` 재패킹 — `Tools > Project K > 데이터 생성 > SpriteManager + SpriteAtlas`.

> ⚠ 원본 PNG 와 `.meta` 를 먼저 백업할 것  
> 덮어쓰기라 되돌릴 방법이 파일 백업뿐이다.

## 검수 체크리스트

- [ ] 파일명이 기존과 정확히 같다 (새 이름을 만들지 않았다)
- [ ] 배경이 완전 투명하다 (배경판·원형 뱃지가 없다)
- [ ] 128px 에서 주제가 하나로 읽히고, 48px 로 줄여도 형태가 남는다
- [ ] 문자·숫자·워터마크가 없다
- [ ] 색 규칙과 맞다 — 같은 분류끼리 나란히 놓고 확인한다
- [ ] 직업 특성이 직업색으로 묶여 보인다
- [ ] 전환 특성 7종이 '두 사물이 맞물린' 형태로 통일돼 있다
- [ ] 시너지 3스택/5스택의 개수가 그림과 맞다
- [ ] 이벤트 페널티 3종이 이득 특성보다 확실히 탁하다

