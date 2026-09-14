# InGame/Summon — 소환 시스템

기획서: `Docs/GameDesign.md` 4장 · 5장
**이 문서가 폴더의 현재 상태를 말한다.** 코드를 열기 전에 여기부터 볼 것.

---

## 흐름 한눈에

```
[로비] MainPanel ─ 소환사 선택 → RunLaunch.Request(id) → LobbyManager.EnterSummonRun()
                                          │
                                          ▼
RunBootstrap ─ 소환사 확정 → 시작 카드 배치 → 마나 지급 → 소환사 스폰 → 스테이지 대기
     │
     ├ StageLoopDirector ─ "지금 소환할 수 있는가" 판단 (CanSummon)
     │
     ├ SummonDeckUI (카드 탭) ─→ SummonController (라인 탭)
     │        │                        ├ 몬스터 카드 → SummonReservation 큐 → MonsterSpawner
     │        │                        └ 스킬 카드   → SkillCardCaster (즉발)
     │        │
     │        └ MonsterSpawner ─ 스폰의 단일 지점. 품질·패시브·환수 몫을 여기서 심는다
     │
     ├ 승리 → CardRewardPicker (3택) → CardSelectPopup → SummonDeckData.Acquire
     │                                        └ [전투 통계] → BattleStatsPopup
     │        만렙 카드를 골랐으면 → CardEvolveUI (진화/융합) → 다음 스테이지
     │
     └ 패배 → RunDefeatPopup (결산) → 카드·특성 버림 → 소환사 선택으로
```

## 결과 화면 (2026-08-27)

| 시점 | 화면 |
|---|---|
| 스테이지 클리어 | `CardSelectPopup` — 카드 3택. 상단에 **전투 통계** 버튼 |
| 통계 보기 | `BattleStatsPopup` — **카드별 딜 기여** (피해량 내림차순) |
| 런 종료(패배) | `RunDefeatPopup` — 도달 스테이지 · 들고 있던 카드 · 통계 버튼 |

- ⚠ **승리 팝업은 없앴다.** 스테이지를 깨면 곧장 카드 3택으로 간다 —
  "확인" 한 번을 더 받는 화면이라 클릭만 늘었다. 원작 `BattleResultPopup` 은
  이 게임 흐름에서 열리지 않는다
- ⚠ 결산은 **카드를 버리기 전에** 띄운다 (`RunBootstrap.HandleDefeat` → `FinishRun`).
  순서를 뒤집으면 "무엇으로 갔는가" 가 빈 칸으로만 뜬다
- 카드별 딜은 `CardStatsTracker` 가 센다. **스테이지 단위**로 비워진다

---

## ⚠ 최근 설계 변경 (2026-08-27) — 옛 코드/주석과 어긋날 수 있다

---

## 진입 — 소환사 선택 (2026-08-27)

- **MainPanel 이 소환사 선택 화면이다.** 원작의 장수 선택을 갈아 끼웠다.
  후보를 굴리지 않는다 — `CardCatalog.Summoners` 전원이 후보다
  (합계가 전원 같도록 잡혀 있어 뽑기로 만들면 설계가 죽는다)
- 카드에는 **개성 · 친화 종족 아이콘 · 시작 카드 · 3대 스탯 10칸 눈금**이 뜬다
  (`SummonerCandidateCardUI` · `StatPipsUI`)
- ⚠ **Splash 가 Lobby 와 InGame 을 둘 다 올린다.** 그래서 `RunBootstrap.Start` 는
  플레이어가 고르기도 전에 돈다 — `RunLaunch` 요청을 기다리게 해서 순서를 잡았다.
  인게임 씬만 열어 확인할 때는 로비가 없으므로 기다리지 않고 바로 시작한다
- ⚠ **런은 반복된다.** `RunBootstrap.Start` 가 `while(true)` 로 돌며
  "선택 대기 → 런 → 패배 → 선택 대기" 를 잇는다. 씬을 다시 로드하지 않는
  구조라 여기서 돌지 않으면 두 번째 런이 영영 시작되지 않는다
- ⚠ `SceneDirector.Present` 를 직접 부르지 말 것 — `LobbyManager.EnterSummonRun()` /
  `ReturnFromSummonRun()` 을 쓴다. 화면·전장·상태 셋이 함께 움직여야 한다.
  `LobbyFlow.SummonRun` 은 `Battle` 과 달리 **BeginWaves 를 부르지 않는다**
  (이 게임은 스테이지 대기에서 플레이어가 시작을 누른다)

### ⚠ InGame 씬은 "올라와 있는 것처럼 보이지만" 아직 없다 (2026-08-28)

스플래시는 InGame 씬을 **`allowSceneActivation = false`** 로 받아만 둔다
(`ScenePreloader`). 씬 목록에 없고 그 안의 `RunBootstrap` 도 존재하지 않는다.

```
Splash ─ Lobby(활성) + InGame(대기, 비활성) 을 additive 로 받음
              │
   [게임 시작] ▼
LobbyManager.EnterSummonRun()
   └ SceneDirector.EnsureInGameResident()   ← ★ 여기서 비로소 씬이 활성화된다
   └ BattleArena.Open(Real)
   └ SetFlow(SummonRun) → Present(Battle)
```

**화면만 인게임으로 돌리면 아무 일도 일어나지 않는다** — 켤 카메라도 HUD 도 없고
RunBootstrap 도 없어 검은 화면에서 멈춘다. 실제로 "게임 시작을 눌러도 시작이 안 되는"
버그가 이것이었다 (`Present` 를 직접 불러 상주 단계를 건너뛰었다).

씬을 올리는 곳은 `SceneDirector.EnsureInGameResident` 하나뿐이다.
**인게임으로 넘기는 새 경로를 만들 때 이 호출을 빠뜨리지 말 것.**

---

### 1. 런 전 덱 편성이 없어졌다
- 시작 카드는 **소환사에 붙박이** (`SummonerData.StarterMonsters` / `StarterSkills`)
- 나머지는 **런 중 보상**으로 줍는다 (`CardRewardPicker`)
- `MonsterCodexData`(도감)는 편성 풀이 아니라 **순수한 기록 + 품질 저장소**다.
  보상 후보를 도감으로 거르지 말 것 — 첫 런에서 카드가 세 장밖에 안 나온다.

### 2. 카드 레벨 (중복 획득) — **어빌리티를 대체하는 축**
- 규칙 정본: `CardLevelRule` — 1·2·4·7·11장 → Lv1~5
- 레벨이 주는 것: `MonsterSpeciesData.LevelBonuses` 의 Lv2/3/4/5 칸이 하나씩 열린다.
  한 칸 = 스탯 1~2개 + 선택적 패시브 1개 (`MonsterLevelBonus`)
- ⚠ **어빌리티 시스템은 만들지 않는다** (2026-08-27 확정)
  스테이지마다 어빌리티를 뽑아 부대를 강화하는 축을 없애고, 그 역할을
  카드 레벨업으로 옮겼다. 무작위로 뽑는 것이 아니라 종족마다 **미리 정해진**
  순서로 열리므로, "이 종족을 키우면 무엇이 되는가" 를 알고 고를 수 있다.
  카드 3택으로 무엇을 키울지 고르는 것 자체가 어빌리티 선택을 대신한다.
- ⚠ 예전의 "레벨당 일괄 +18%" 는 폐기. 전부 똑같이 커지면 선택이 사라진다
- ⚠ **유닛이 성장하는 게 아니다.** 손에 든 카드가 좋아지는 것이라
  "인게임 레벨업 없음" 규칙과 어긋나지 않는다

### 2-1. 런 특성 (`RunPerk` · `RunPerkData`)
런을 도는 동안 줍는, **규칙 자체를 바꾸는** 것들. 4종뿐이고 중첩되지 않는다.
| 특성 | 효과 | 적용 지점 |
|---|---|---|
| 첫 소환 무료 | 스테이지마다 첫 카드 1회 무료 | `SummonController.UseMonsterCard` |
| 잔존 정산 | 환수량 +50% | `SummonerPerkRuntime.RefundFor` |
| 과잉 소환 | 라인 배출 간격 −40% | `SummonController.CurrentDrainInterval` |
| 확장 편성 | 카드 칸 +2 | `RunBootstrap.BuildStarterDeck` |
- ⚠ 스탯 %를 여기 넣지 말 것 — 레벨업과 역할이 겹쳐 둘 다 밋밋해진다
- ⚠ 스테이지 단위 상태(`OnStageBegin`)는 **대기에 들어설 때** 되돌린다.
  '시작' 시점에 되돌리면 대기·전투에서 무료 소환이 두 번 나간다
- ❗ **획득 지점이 아직 없다.** `RunPerkData.Add` 는 있지만 주는 화면이 없다

### 3. 소환 시간(SummonTime) 개념은 폐기됐다
무게는 마나 하나로만 표현한다. 라인 배출 간격은
`SummonController.DrainInterval` 고정값이고 **연출용**이지 밸런스 축이 아니다.

### 4. 마나 환수는 카드로 소환한 개체만 한다
부활·특성·스킬로 공짜로 나온 개체는 환수하지 않는다
(`MonsterSpawner.SpawnFree` 가 `Carry(0)`). 공짜 개체가 환수하면
"죽을수록 마나가 는다" 가 되어 런당 1회 지급이라는 축이 무너진다.

### 5. 친화도 (A안)
- 친화 종족 : 소환력 ×1.75 · 그 외 : ×0.55 (`SummonerAffinityRule`)
- 편성 제한이 아니라 배율이다 — 주운 카드는 무엇이든 쓸 수 있다
- 캐릭터 차이는 "무엇이 친화인가" + 개성이 만든다. **배율은 전 캐릭터 공통이다.**

### 6. 종족 고유 패시브 + 계보 (`SpeciesPassive`)
- 종족마다 **하나씩**, 그 종족을 그 종족답게 만드는 규칙 (슬라임 = 죽으면 분열)
- 업그레이드 종족은 `MonsterSpeciesData.UpgradeOf` 로 뿌리를 가리키고
  **뿌리의 패시브를 자동으로 물려받는다.** 업그레이드에 다시 적지 말 것
```
슬라임(분열) ─┬─ 힐 슬라임  (분열 + 죽을 때 아군 회복)
              ├─ 독 슬라임  (분열 + 피격 시 적 중독)
              └─ 강철 슬라임(분열 + 피격 시 반사)
```
- ⚠ **무한 증식은 '세대'로 막는다.** 카드로 소환된 개체가 0세대,
  분열·부활로 나온 것이 1세대. 유닛을 만드는 패시브는 0세대만 발동한다
  (`SpeciesPassiveRule.MaxReproduceGeneration`). 회복·중독처럼 개체를
  늘리지 않는 패시브는 세대와 무관하게 계속 작동한다
- 발동 지점은 둘뿐 — `ApplyOnSpawn`(지속형) · `OnDeath`(단발형)
- ⚠ **새 소환 경로를 만들 때 세대를 빠뜨리면 그 경로로 무한 증식이 뚫린다**
- ⚠ 풀 재사용 시 잔재를 지운다 (`MonsterRuntimeBridge.ClearSpeciesPassiveResidue`) —
  안 지우면 "슬라임인데 독을 뿜는" 유닛이 조용히 생긴다

### 7. 만렙 카드의 두 갈래 — 진화 / 융합 (`CardEvolution`)
```
슬라임 만렙 ─┬─ 진화 → 힐/독/강철 슬라임 중 **무작위**. 장수(레벨)는 그대로 넘어감
             └─ 융합 → 다른 카드를 먹고 그 종족 패시브를 배움. 재료는 사라짐
```
- ⚠ **한쪽을 고르면 다른 쪽이 닫힌다** — 진화한 카드는 융합만, 융합한 카드는 진화 불가
  (`HasEvolved` / `HasFused`). 진화는 카드당 런에 1회이고 융합보다 먼저 해야 한다
- 진화가 무작위인 이유: 고르게 하면 가장 센 업그레이드 하나로 수렴한다.
  대신 업그레이드끼리는 **강약이 아니라 성격**으로 갈려 있어야 한다
- 융합이 넘겨주는 것은 재료의 **고유 종족 패시브 하나**뿐이다.
  상속받은 것·융합으로 배운 것은 넘어가지 않는다 (패시브가 무한히 옮겨 다니게 된다)
- 배울 수 있는 패시브는 최대 3개 (`CardEvolution.MaxLearned`)
- ⚠ **분열체는 융합 패시브를 물려받지 않는다** — 물려받으면 융합 한 번에 판 전체가 강화된다
- ⚠ **런이 끝나면 전부 사라진다** (`SummonDeckData.ResetForNewRun`, `RunBootstrap.HandleDefeat`).
  다음 런은 시작 카드의 기본 패시브만 갖고 시작한다
- **언제 열리나** — 보상 3택에서 **이미 만렙인 카드**를 고르면 `CardEvolveUI` 가 뜬다.
  중복이 더 이상 레벨을 못 올리는 대신 갈림길이 열린다 (`RunBootstrap.TryOpenEvolve`)
- 둘 다 불가능한 카드면 창을 띄우지 않고 장수만 올린다 —
  고를 게 없는 창은 진행만 끊는다

### 8. 다수 생산 체제 (밸런싱 기준 변경)
가장 비싼 트롤도 3마리씩 나온다. 종족 패시브(분열·자폭·최후의 함성)가 전부
개체 수에 비례해 값어치가 커지고, 라인 5개에 배분하려면 마릿수가 라인 수보다
많아야 하기 때문이다. **"정예" 는 마릿수가 아니라 개체 두께로만 표현한다.**

### 9. 몬스터 공격 형태 (`MonsterAttackKind`)
- `Melee` / `Ranged`. 원거리는 RangedTag + UnitJobComponent + 발사 버퍼 **세 개**가
  전부 있어야 한 발이라도 나간다 (`MonsterRuntimeBridge.AddComponents`)

### 10. 몬스터도 액티브 스킬을 쓴다
- `MonsterSpeciesData.ActiveSkill` (멧돼지 = 강타). 슬롯은 종족과 무관하게 **항상**
  붙이고, 스킬이 없으면 `SkillId = None` 으로 둔다 (풀 재사용 시 구조 변경을 피하려고)
- ⚠ `ActiveSkillAISystem` 에서 `SkillId == None` 을 걸러야 한다 — 안 그러면
  스킬 없는 몬스터가 매 프레임 발동 요청을 넣는다
- ⚠ 몬스터는 `AutoSkillEnabled` 설정에서 제외된다 — 수동으로 쓸 카드가 없어서
  함께 막으면 몬스터 스킬이 영영 안 나간다

---

## 개성(Perk) — 왜 TraitData 가 아닌가

`TraitData` 는 **숫자를 얼마 올릴 것인가**만 표현한다(스탯 가산·환산·스택).
"아군이 죽은 자리에 스켈레톤을 공짜로 세운다" 같은 **파이프라인 개입**은
표현할 수 없다. 그래서 두 층으로 나눴다.

| 개성의 성격 | 어디에 있나 |
|---|---|
| 숫자로 끝나는 것 | `MonsterSpeciesData.LevelPassives` / `TraitData` (기존 시스템, 새 코드 없음) |
| 파이프라인에 끼어드는 것 | `SummonerPerk` + `SummonerPerkRuntime` (훅 4개) |

훅은 넷뿐이다 — `ManaCostFor` · `RefundFor` · `GradeFor` · `OnMonsterSpawned` / `OnMonsterDied`.
늘리기 전에 정말 필요한지 볼 것.

---

## 파일 지도

| 파일 | 하는 일 |
|---|---|
| `RunBootstrap` | 런 진입 + 스테이지 루프(승리 감시 → 보상 → 다음 판) |
| `StageLoopDirector` | "지금 소환 가능한가" 판단만 |
| `SummonController` | 카드 선택 → 라인 탭 → 예약/즉발 |
| `SummonReservation` | 라인별 대기 큐 |
| `MonsterSpawner` | **스폰의 단일 지점** — 여기 말고 다른 데서 몬스터를 세우지 말 것 |
| `MonsterStatComposer` | **스탯 합성의 단일 지점** — 개성이 스탯을 바꾸면 여기서 곱한다 |
| `MonsterDeathWatcher` | 사망 → 종족 패시브(세대 제한) + 소환사 개성(카드 소환분만) |
| `SpeciesPassive` / `SpeciesPassiveRuntime` | 종족 고유 패시브 · 수치 정본 |
| `MonsterRefundCarrier` | 생존 개체의 마나 환수 |
| `CardCatalog` | ID → SO 변환의 단일 지점. Resources 에 한 장 |
| `CardLevelRule` | 카드 레벨 규칙 |
| `CardEvolution` | 진화·융합 가능 판정 · 진화 후보 · 재료가 넘기는 패시브 |
| `CardRewardPicker` | 클리어 보상 3택 후보 |
| `SkillCardData` / `SkillCardCaster` | 스킬 카드 (기존 액티브 33종 재활용) |
| `SummonerData` / `SummonerPerk*` | 캐릭터 정의 · 개성 |
| `MonsterLevelBonus` | 카드 레벨이 주는 고정 효과 한 칸 (어빌리티 대체) |
| `RunPerk` / `RunPerkData` | 런 중 줍는 특성 — 규칙을 바꾸는 것들 |
| `CardEvolveUI` | 만렙 카드의 진화/융합 창 (HUD 안에 있다 — 카드 바 옆에서 고른다) |
| `CardStatsTracker` | 카드별 딜·처치·받은 피해. 스테이지 단위 |
| `CardSelectPopup` / `BattleStatsPopup` / `RunDefeatPopup` | 런 진행 팝업 3종 |
| `MonsterIconGenerator` | 종족 아이콘 9 + 카테고리 3 (에디터) |

---

## 에디터 굽기 순서 (⚠ 순서가 있다)

```
Tools > Project K > 데이터 생성 > 몬스터 도감    ← 종족 SO
Tools > Project K > 데이터 생성 > 스킬 카드      ← 스킬 카드 SO
Tools > Project K > 데이터 생성 > 소환사        ← 위 둘을 참조한다
Tools > Project K > 데이터 생성 > 카드 목록      ← 위 전부를 긁어 Resources 에 담는다
Tools > Project K > 씬 셋업 > 인게임 전장
Tools > Project K > UI > 인게임 HUD
Tools > Project K > 아이콘·텍스처 > 종족 아이콘   ← 종족 SO 에 아이콘을 물린다
Tools > Project K > 프리팹 생성 > 팝업 > ▶ 런 팝업
PopupManager 인스펙터 > Load Popup Prefabs        ← 새 팝업 3종 등록
PoolController 인스펙터 > Load Prefabs From Folder
```

⚠ **카드를 새로 만들면 '카드 목록' 을 반드시 다시 굽는다.**
런타임은 `Resources/CardCatalog` 만 본다 — 여기 없는 카드는 보상에도 안 나오고
카드 바에서 빈 칸으로 뜬다.

---

## 미확정 (임의로 정하지 말 것)

- **진화·융합 UI** — 규칙(`CardEvolution` · `SummonDeckData.Evolve/Fuse`)은 다 있지만
  고르는 화면이 없다. 만렙 카드를 눌렀을 때 뜨는 창이 필요하다
- 캐릭터 선택 화면(`RunSetup`) — 지금은 `RunBootstrap` 이 자동으로 하나 집는다
- 카드 칸이 다 찼을 때의 처리 — 지금은 신규 카드를 후보에서 뺀다 (교체 UI 없음)
- **런 특성 획득 지점** — `RunPerkData.Add` 는 있지만 주는 화면·시점이 없다
- **종족 패시브 강화 이벤트** — "이벤트에서 몬스터를 골라 그 종족 패시브를 강화"
  가 확정 사양이지만, 새 런 루프에 이벤트 단계 자체가 아직 없다
- **스킬 카드 레벨업** — 지금은 위력 배율만 오른다. 몬스터처럼 고정 효과 표를
  주는 것이 추후 계획
- ⚠ **원작 어빌리티 코드(`InGame/Ability/`)는 남아 있다.** 이 게임에서는 죽은 값이지만
  51개 파일이 참조하고 그중 용사(적) 스탯 파이프라인은 살아 있다 —
  지우려면 그쪽을 함께 손봐야 하므로 별도 작업으로 남긴다
