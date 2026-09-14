# 아이템·재화 아이콘 생성 명세 (13종)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/Items/` — 13개 PNG (현재 192px, **256px 로 교체**)  
짝 문서: `Docs/Ability_Icon_Spec.md`(어빌리티) · `Docs/RelicTree_Icon_Spec.md`(유물)

파일명은 `eItem.IconKey()` 가 만드는 이름이다 — **바꾸면 안 된다.**
`SpriteManager` 가 이 이름으로 아틀라스에서 찾고, `HeroDetailPopupCreator` 는 경로로 직접 로드한다.

---

## 1. 어디에 어떻게 보이는가 — 이게 모든 결정의 기준이다

아이템 아이콘은 스킬 아이콘과 **성격이 다르다.** 스킬은 "행위"지만 아이템은 **소품(prop)** 이다.
그리고 재화 위젯에서 64px, 비용 라벨에서 30px 로 줄어든다 — 세공을 넣을수록 손해다.

| 화면 | 칸 크기 | 캔버스 | 상황 |
|---|---:|---|---|
| `RewardCard` (보상 카드) | **128** | InGame 1080×1920 | 가장 크다. 카드 전면을 채운다. 보상·이벤트·상점 공통 |
| `TopBar` 재화 위젯 | 64 | Lobby 1920×1080 | 아이콘 + 수량 텍스트 한 줄 |
| `HeroDetailPopup` 비용 아이콘 | **30** | Lobby | 등급업·강화 버튼 안의 인라인 아이콘. 최소 크기 |
| `RunShopPopup` · `MercenaryShopPopup` 가격 | 30~64 | Lobby | 위와 같은 인라인 |

**결론 두 개 — 프롬프트가 이 두 줄을 만족시켜야 한다.**

1. **최대 노출 128 → 파일은 256×256.** (노출 × 2)
   현재 192px 는 어중간하다. 1024 원본은 필요 없다 — 생성은 512로 충분하다.
2. **최소 노출 30 → 30px 에서 "무엇인지"가 아니라 최소한 "어느 재화인지"는 구분돼야 한다.**
   30px 에서 구분을 만드는 것은 디테일이 아니라 **실루엣 + 단색 대비** 두 가지뿐이다.
   그래서 아이템은 **색으로 먼저 구분**한다 — 아래 색표에서 겹치는 색을 쓰지 말 것.

### 겹치는 UI (반드시 피할 것)

- **보상 카드 우하단에 수량 배지(`×99`)가 깔린다** — 카드 폭의 62% × 높이 40.
  아이콘의 **우하단 1/4 에 판독에 필요한 형태를 두지 말 것.**
  금화 더미처럼 아래로 퍼지는 소재는 **왼쪽으로 흐르게** 쌓는다.
- 재화 위젯은 아이콘 **오른쪽에 수량 텍스트**가 붙는다. 오른쪽으로 길게 뻗는 형태(뻗은 창·기울인 검)는
  텍스트와 시각적으로 붙어 보인다 — 세로로 모인 실루엣이 안전하다.

---

## 2. 공통 시각 언어

| 요소 | 방향 | 의도 |
|---|---|---|
| 배경 | 완전 투명. 배경판·후광 원 금지 | 보상 카드가 이미 어두운 안쪽 판과 등급 테두리를 그린다 |
| 주제 | 소품 **하나**. 개수를 표현할 때만 같은 물건 2~3개까지 | 30px 에서 종류가 섞이면 얼룩이 된다 |
| 여백 | 중앙 주제 176~208px, 외곽 여백 24~40px (256 기준) | 카드가 8px 안쪽 여백을 더 먹는다 |
| 외곽선 | 어두운 4~6px 실루엣 외곽선 | 어두운 카드에서도 물체 경계가 산다 |
| 광원 | 좌상단 단일 광원 + 재질 하이라이트 1점 | 금속·유리·양피지를 광택으로 구분한다 |
| 재질 | 스킬 아이콘보다 **한 단계 사실적**으로. 다만 사진은 아니다 | 소품이라 손에 잡히는 물성이 있어야 한다 |
| 금지 | 문자·숫자·워터마크·프레임·수량 표시·드롭섀도 | 수량은 UI 배지가 그린다 |

### 색 = 1차 구분 키

30px 에서는 색이 형태보다 먼저 읽힌다. **13종이 서로 다른 색 자리를 차지해야 한다.**

| 아이템 | 주조색 (프롬프트에 넣을 영문) |
|---|---|
| 골드 | `warm gold` |
| 젬 | `violet amethyst` |
| 에너지 | `electric cyan` |
| 스태미나 | `vivid green` |
| 명예 | `gold with crimson ribbon` |
| 환생 포인트 | `pale soul white-violet` |
| 경험서 | `sky blue` |
| 스킬 서적 | `parchment tan with arcane violet glow` |
| 장군 강화석 | `royal gold-amber` |
| 장비 강화석 | `steel blue` |
| 장비 상자 | `bronze and dark wood` |
| 용병조각 | `weathered bronze` |
| 전투석 (폐지) | `dull crimson` |

---

## 3. 공통 프롬프트

각 항목 프롬프트 앞에 그대로 붙인다. `{ACCENT}` 에 위 표의 색을 넣는다.

```
256x256 game item icon, dark fantasy, a single game prop centered in frame,
painted illustration with clear material rendering, one light source from the upper left,
crisp dark outline that still reads at 30 pixels,
{ACCENT} as the dominant color, silhouette readable at a glance,
compact vertical silhouette, bottom-right quadrant kept simple,
fully transparent background, no background plate, no glow disc, no frame,
no text, no numbers, no watermark, no signature,
centered composition with even margin on all four sides
```

### 네거티브 프롬프트

```
text, letters, numbers, quantity label, watermark, signature, logo,
multiple different objects, cluttered pile, busy background, tiny engraved detail,
background plate, glow disc, circular badge, frame, border, UI chrome,
photorealistic photo, 3D render, depth of field, drop shadow on background,
cropped subject, subject touching the edge
```

### 생성 크기

**512×512 로 생성 → 256×256 축소.** 축소 필터는 Lanczos/Area,
반투명으로 번진 외곽 픽셀은 알파 임계값 0.35 로 자른다.

---

## 4. 항목별 프롬프트

| 파일명 | 아이템 | 쓰임 | 색 | 주제 | Prompt |
|---|---|---|---|---|---|
| `item_gold.png` | **골드** | 기본 재화. 상점·용병·강화 비용 | warm gold | 앞면이 보이게 쌓인 금화 세 닢 | a stack of three gold coins leaning to the left, the front coin facing the viewer with a worn embossed rim |
| `item_gem.png` | **젬** | 유료 재화 | violet amethyst | 각진 컷의 보석 한 알 | a single faceted amethyst gemstone with sharp cut planes and one bright specular highlight |
| `item_energy.png` | **에너지** | 전투 입장 재화 | electric cyan | 번개가 갇힌 결정 조각 | a crystal shard with a lightning bolt sealed inside it, faint sparks escaping the tip |
| `item_stamina.png` | **스태미나** | 행동력 | vivid green | 코르크로 봉한 둥근 물약병 | a round glass flask of glowing green draught, sealed with a cork stopper |
| `item_honor.png` | **명예** | PvP 재화 | gold with crimson ribbon | 접힌 리본이 달린 훈장 | a military medal disc hanging from a folded crimson ribbon, laurel embossed on its face |
| `item_reincarnation_point.png` | **환생 포인트** | 유물 트리 강화에 소비 | pale soul white-violet | 물방울 핵으로 모이는 혼의 소용돌이 | a spiral of pale soul light winding inward into a single teardrop-shaped core |
| `item_expbook.png` | **경험서** | 유닛 경험치 | sky blue | 책등에서 빛이 오르는 펼친 책 | an open book with softly glowing pages, a thin ribbon of light rising from the spine |
| `item_skillscroll.png` | **스킬 서적** | 스킬 잠금 해제 | parchment tan with arcane violet glow | 봉랍으로 묶인 두루마리 | a rolled parchment scroll bound with a cord and a wax seal, faint violet light leaking from the open end |
| `item_general_upgrade_stone.png` | **장군 강화석** | 장수 등급업 (`HeroDetailPopup`) | royal gold-amber | 문양이 새겨진 각진 금빛 원석 | a faceted amber-gold ore stone etched with a commander sigil, the etching glowing from within |
| `item_equip_upgrade_stone.png` | **장비 강화석** | 장비 강화 | steel blue | 불꽃이 튀는 푸른 강철 결정 | a faceted blue-steel crystal with sparks flicking off one of its facets |
| `item_equipbox.png` | **장비 상자** | 개봉 시 장비 획득 | bronze and dark wood | 빛이 새어 나오는 반쯤 열린 궤 | an iron-banded wooden chest cracked open, warm light spilling out through the gap |
| `item_soldier_shard.png` | **용병조각** | 영웅 용병 수 증가 | weathered bronze | 깨진 병사 문장 조각 | a broken shard of a soldier's shield emblem, jagged fracture edge catching the light |
| `item_battlestone.png` | **전투석** ⚠폐지 | 지급처 없음 — 옛 세이브 표시용 | dull crimson | 붉은 균열이 난 무딘 돌 | a dull grey stone split by a glowing red crack running through its middle |

> ⚠ `item_battlestone` 은 **폐지된 재화**다 (`eItem.cs` 주석 참고 — 주는 곳도 쓰는 곳도 없다).
> 옛 세이브에 수량이 남아 있어 표시만 될 수 있으므로 파일은 유지한다.
> **새로 만들 우선순위는 가장 낮다** — 12종만 교체해도 된다.

---

## 5. 저장·임포트 절차

1. 512×512 로 생성 → **256×256** 축소 → 알파 임계값 0.35 정리.
2. `Assets/_project/3.Textures/Icons/Items/` 에 **기존 파일명 그대로** 덮어쓴다.
3. Unity 임포트 설정: `Texture Type = Sprite (2D and UI)`, `Alpha Is Transparency = ON`,
   `Filter Mode = Bilinear`, `Compression = None`, **`Max Size = 256`**, `Mesh Type = Full Rect`,
   `Sprite Mode = Single` (`SpriteManager.Get()` 이 Single 이 아니면 못 찾는다).
4. `Tools > Project K > 데이터 생성 > SpriteManager + SpriteAtlas` 로 `Atlas_Items` 재패킹.
5. `HeroDetailPopup` 은 프리팹에 스프라이트를 **구워 넣는다** — 파일을 바꾼 뒤
   `Tools > Project K > 프리팹 생성 > 팝업 > HeroDetail` 를 다시 굽는다.
   (덮어쓰기라 GUID 가 유지되므로 대개 그대로 반영되지만, 크기가 바뀌었으니 확인할 것)

---

## 6. 검수 체크리스트

- [ ] 13장(또는 전투석 제외 12장) 전부 있고 파일명이 `eItem.IconKey()` 와 일치한다
- [ ] 배경이 완전 투명하다 (후광 원·배경판이 없다)
- [ ] **30px 로 줄였을 때 13종이 서로 구분된다** — 나란히 놓고 확인한다. 색이 겹치면 다시 만든다
- [ ] 우하단 1/4 이 비교적 비어 있다 (보상 카드 수량 배지와 겹치지 않는다)
- [ ] 실루엣이 세로로 모여 있다 (재화 위젯에서 오른쪽 수량 텍스트와 붙어 보이지 않는다)
- [ ] 금속·유리·양피지 재질이 구분된다 (강화석 2종이 같은 돌로 보이지 않는다)
- [ ] 문자·숫자·수량 표시가 없다

## 참조

- 아이템 정본: `Assets/_project/1.Script/Data/Item/eItem.cs` · `Data/Item/ItemMeta.cs`
- 옛 생성기(도안 참고용): `InGame/Battle/Editor/ItemIconGenerator.cs`
- 표시 칸 크기: `InGame/Battle/Editor/UISetupTool.cs`(보상 카드 128) ·
  `UI/Lobby/Editor/TopBarCreator.cs`(재화 위젯 64) ·
  `UI/Popup/Editor/HeroDetailPopupCreator.cs`(비용 아이콘 30)
