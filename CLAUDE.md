# Project K 004 — Summoner's Keep — Claude Code 컨텍스트

> 이 파일은 Claude Code가 매 세션 자동으로 읽습니다.
> 프로젝트 파악 시간을 줄이기 위해 핵심 정보를 여기에 유지합니다.

---

## 이 프로젝트가 무엇인가 — 먼저 읽을 것

`Project_K_001`(원작)의 **진영 반전 파생작**이다. 코드베이스 전체를 원작에서 그대로
복사해 왔고(2026-08-26), 아직 **원작과 100% 동일하게 동작한다**. v2 기획의 신규
시스템은 아직 하나도 구현되지 않았다.

**정본 기획서: `Docs/GameDesign.md` (v2).** 원작 기획서는 대조용으로
`Docs/GameDesign_ProjectK001.md` 에 남겨 뒀다. 무엇을 만들지 판단이 필요할 때는
항상 v2 문서가 우선한다.

### 한 줄 요약

런 시작 시 1회 부여되고 **다시는 채워지지 않는 마나**로, 마왕성에 몰려오는 용사
웨이브를 막아내는 실시간 소환 디펜스 로그라이트.

### 진영이 통째로 뒤집혔다 — 계승 방향 (v2 3.0절)

| 원작 시스템 | 원작 진영 | 이 프로젝트 진영 |
|---|---|---|
| General (직업·등급·레벨·액티브33·패시브40·장비) | 아군 | **Hero (적)** |
| Soldier (장수 종속 병사) | 아군 | **HeroFollower (적)** |
| Enemy / Elite / Boss (무장비·무등급, 대량 스폰) | 적 | **MonsterSummon (아군, 플레이어의 소환수)** |

> ⚠ 이 표를 거꾸로 읽지 말 것. 플레이어의 소환수는 **단순 스탯 랭크** 체계이고,
> 화려한 스킬·장비·등급 apparatus는 **적(용사)** 쪽이 가져간다.

### 원작과 달라지는 핵심 (v3 확정, 2026-08-26)

**화면은 원작과 같은 가로 1920×1080이다.** (한때 세로로 잘못 적혀 있었다 — 가로가 맞다)

0. **진영 배정 — `Faction.cs` 가 정본** (확정 규칙)
   - **인간(용사) 진영 → `TeamType.Enemy`** · **몬스터 진영 → `TeamType.Ally`**. 원작과 정반대다.
   - 갈리는 기준은 **누가 부리는가**지 어떻게 생겼는가가 아니다. 좀비·고블린·스켈레톤은
     신체가 인간형이어도 플레이어의 소환수라 **아군**이다.
   - 프리팹 이름에 속지 말 것: `Enemy/Elite/Boss.prefab` 은 이제 **플레이어의 몬스터**,
     `General/Soldier.prefab` 은 **적(용사)** 이다. 원작 이름을 물려받았을 뿐이다.
   - 진영을 정하는 자리에서는 `TeamType.*` 을 직접 쓰지 말고 `Faction.Hero` / `Faction.Monster` 를 쓴다.

1. **소환사 캐릭터가 곧 마왕성이다** — 별도 코어 엔티티 없음. **캐릭터 HP = 마왕성 HP**,
   이게 0이면 런 종료. 캐릭터는 왼쪽 성벽 뒤에 고정, 기본 공격을 하고 어빌리티로 강화된다.
   프리팹으로 미리 빌드(의상·무기 착용 완성본), 런타임 외형 합성 안 씀.
2. **3대 전투 스탯** — 힘(→공격력) · 체력(→HP) · **지능(→소환력)**.
   ⚠ **소환력은 지휘력이 아니다.** 원작 지휘력은 병사가 장군 스탯에서 통째로 파생되는
   *종속* 구조였다. 소환력은 **가산**이다 — 몬스터는 종족별 기본 스탯을 이미 갖고 있고
   소환력은 그 위에 얹히는 추가 보너스다. `SoldierStatApplier` 공식을 베끼지 말 것.
   힘·체력도 몬스터에 조금 영향을 준다.
3. **마나 = 지능이 정한 그릇 + 스테이지 회복** (v4, 2026-08-28)
   `MaxMana = 40 + 지능×9`, 스테이지마다 `Max×25%` 회복. 회복은 Max 를 못 넘는다.
   소환사별 `StartMana` 는 없앴다 — 지능이 그릇을 정하는데 총량을 또 적으면
   지능을 올려도 마나가 안 느는 캐릭터가 생긴다.
   ⚠ **환수는 폐기됐다.** 살아남은 몬스터는 마나로 녹지 않고 **제 라인으로 돌아가
   다음 판에 다시 싸운다**(`MonsterLineReturner`, 체력 만회복). 옛 규칙은 마나 손실이
   곧 사망률이라 "더 많이 소환하기" 가 언제나 최적해였다.
   ⚠ **과부하(Overload)** — 한 스테이지에 같은 카드를 거듭 내면
   `원가 × (1 + 0.1×n)`. 안은 소수점, 표시·소모만 반올림. 곱셈 누적이 아니라
   선형 가산이고, '연속'이 아니라 스테이지 내 '누적'이다 (`SummonCostRule`).
   카드 마나 숫자: **붉은색=과부하 · 초록색=할인**. 값이 실제로 오르는 순간 카드가
   한 번 부풀며 번쩍인다.
   → 자세한 근거는 `Docs/GameDesign.md` 5.0절
4. **레벨업 없음** — 캐릭터도 몬스터도 인게임 레벨 요소가 없다.
   단 **적 용사만 Lv을 표시**해 점점 강해지는 느낌을 준다.
5. **몬스터는 품질(등급)과 고유 스킬·패시브를 갖는다** — 기존 `UnitGrade` 5단계 그대로,
   단 임의 배정 + "품질 개선"으로 향상. 스킬·패시브는 기존 시스템으로 발동.
   ⚠ v2의 "무등급·스킬 미탑재"는 폐기. **장비도 붙었다** (2026-09-06, 아래 '몬스터 장비' 항목).
6. **라인제 소환** — 도감에서 편성 → 인게임 하단 UI 카드 → 라인 선택 배치 →
   마나 소모 → **성벽 바로 옆**에 마법진과 함께 소환 → 0.1초 연출 후 오른쪽으로 돌진.
   스킬 소환(파이어볼 등)도 **같은 라인 칸**을 쓴다.
7. **엘리트 스테이지 = 보스 히어로** — 허들 스테이지 편성에 보스 용사가 1기 섞인다.
   `BossHeroSetup` 이 `BossComponent` 를 얹기만 하면 **원작 보스 장치가 통째로 따라온다**:
   근접 공격 잡은 `[WithNone(typeof(BossComponent))]` 로 자동 제외되고, `BossAttackSystem` 이
   AoE 평타·넉백·**평타 ×3(= 기본 공격의 300%)** 을 돌리며, `TopBarUI` 의 보스 HP 바가
   진영과 무관하게 그대로 잡는다. 공속 1/3 만 `BossHeroSetup` 이 직접 깎는다
   (용사는 직업 스탯 범위에서 굴러 나와 원작 `BossRange` 처럼 느리지 않기 때문).
8. **로비 없음** — `Lobby` 씬을 `RunSetup`으로 대체. 두 씬 상주 모델도 적용되지 않는다.
9. **몬스터 도감 = 소환 덱 편성 화면** — 별도 화면이 아니라 하나의 화면이다.

### 원작에서 그대로 쓸 수 있는 것 (조사 완료)

- `EnemyRace` enum — 종족 16종(Goblin/Skeleton/ZombieA/ZombieB/Werewolf/Furry/Teddy 등)과
  외형 롤러가 이미 있다. **슬라임만 신규**로 추가하면 된다.
- `UnitGrade` 5단계(Normal~Epic, 등급당 +10%) — 몬스터 품질로 그대로 전용
- `StatComponent`(Base/Final 레이어) · 데미지 공식 · 분리(Separation) 그리드
- `PoolController` + `AllySpawner`/`EnemySpawner` 스폰 파이프라인
- `ScreenClampJob` — 화면 경계 처리. **몬스터의 오른쪽 이탈만 예외 처리**하면 환수 훅이 된다

### 아직 구현되지 않은 신규 시스템 (스캐폴딩 폴더만 존재)

| 폴더 | 담을 것 |
|---|---|
| `InGame/Summon/` | 대부분 구현됨 — `SummonCostRule`(과부하) · `MonsterLineReturner`(라인 복귀) · `StageLoopDirector`(자동 시작) · `CardRewardPicker` · `CardEvolution` |
| `InGame/MonsterCodex/` | `MonsterUnlockCondition` — 조건부 결정적 해금(확률 뽑기 아님) |
| `RunSetup/` | `RunSetupDirector` — 소환사 선택 → 도감 편성 탭 |
| 코어 HP | `InGame/Battle/CoreHealthSystem.cs` (기존 Battle 폴더에 추가) |

### 미확정 사항 — 임의로 정하지 말 것

기획서 13장에 열린 질문 6개(소환 위치 자유도, Hero 레벨 축 처리, 덱 재편성 시점 등)가
있다. 해당 영역을 건드릴 때는 구현을 밀어붙이기 전에 사용자에게 확인한다.

---

## 프로젝트 개요

- **장르**: 실시간 소환 디펜스 + 오토배틀 하이브리드 + 로그라이트
- **엔진**: Unity 6 (URP 17.0.4) + **Unity ECS (Entities 1.4.5)** — 원작과 동일 버전
- **언어**: C# — 네임스페이스 `BattleGame.*`
- **작업 디렉터리**: `d:\project\Project_K_004`
- **원작 참조**: `d:\project\Project_K_001` (읽기 전용 참고. 이쪽 파일을 고치지 말 것)
- **스크립트 루트**: `Assets/_project/1.Script/`
- **번들 ID**: `com.Kim_Sky.Project_K_004` / productName `Summoners_Keep` (원작과 분리됨)
- **화면**: **가로 1920×1080** (원작과 동일)

---

## 아키텍처 — ECS + Managed Bridge 혼합

```
ECS (Jobs/Burst)                    Managed (MonoBehaviour)
────────────────                    ────────────────────────
UnitComponents.cs                   UnitRuntimeBridge.cs
UnitAttackSystem.cs                 GeneralRuntimeBridge.cs
UnitMovementSystem.cs               SoldierRuntimeBridge.cs
UnitTargetSearchSystem.cs           EnemyRuntimeBridge.cs
UnitHitSystem.cs                    UnitAppearanceBridge.cs
UnitStatusEffectSystem.cs
GeneralSkillSystem.cs
ActiveSkillAISystem.cs  →  ActiveSkillExecuteSystem.cs (managed)
ProjectileSystem.cs     →  ProjectileView.cs (managed)
```

**핵심 패턴**: ECS 시스템이 로직 처리 → Managed Bridge가 시각/애니메이션 동기화.  
스킬 실행은 `ActiveSkillExecuteSystem`에서 `ActiveSkillData.Execute(context)`를 호출 (managed).

---

## 유닛 계층 — 진영별로 쓰는 계층이 갈린다

**적 (용사 진영, `Faction.Hero`)**

| 타입 | ECS Component | Managed Bridge | 설명 |
|------|--------------|----------------|------|
| General (용사) | `GeneralComponent` | `GeneralRuntimeBridge` | 직업·등급·스킬·장비를 온전히 갖춘 적 주력 |
| Soldier (용사 병사) | `SoldierComponent` | `SoldierRuntimeBridge` | 용사 휘하 자동 전투원 |
| Elite (엘리트 용사) | `EliteComponent` | — | General 에 `HeroTierSetup.ApplyElite` 로 승격 |
| Boss (보스 용사) | `BossComponent` | — | General 에 `HeroTierSetup.ApplyBoss` 로 승격. **엘리트 스테이지 전용** |

**아군 (몬스터 진영, `Faction.Monster`)**

| 타입 | ECS Component | Managed Bridge | 설명 |
|------|--------------|----------------|------|
| Monster (소환 몬스터) | `MonsterComponent` | `MonsterRuntimeBridge` | 플레이어가 소환하는 유일한 유닛 타입 |

> ⚠ **몬스터에는 계층이 없다.** Elite/Boss 는 용사 전용이다.
> 몬스터의 강함은 **품질(`UnitGrade`, 도감 개체당 영구)** 과 **소환사의 소환력** 이 정하지
> "엘리트냐 보스냐" 가 정하지 않는다.
>
> ⚠ **원작에서 이름이 바뀌었다** — 옛 이름으로 검색하면 안 나온다.
>
> | 원작 | 현재 |
> |---|---|
> | `SpawnUnitType.Enemy` · `UnitType.Enemy` | `.Monster` |
> | `EnemyComponent` | `MonsterComponent` |
> | `EnemyRuntimeBridge` | `MonsterRuntimeBridge` |
> | `EnemyStatRoller` | `MonsterStatRoller` |
> | `EnemyAuthoring` | `MonsterAuthoring` |
> | `Enemy.prefab` | `Monster.prefab` (풀 키도 `"Monster"`) |
>
> 그대로 남은 이름: `EnemyRace`(외형 종족) · `EnemyAppearanceRoller` · `EnemySpawner` ·
> `EnemyGradeStatRange`. 이들은 유닛 계층이 아니라 다른 개념이다.
>
> ⚠ `Elite.prefab` · `Boss.prefab` 은 남아 있지만 **더 이상 쓰이지 않는다.**
> 용사 보스/엘리트는 `General.prefab` 에 컴포넌트를 얹어 만든다.

---

## 직업 시스템 (UnitJob)

```csharp
Knight       = 0  // 병사 특화 — 병사 수·지휘력 최고, 이동속도 최고
Archer       = 1  // 사거리 최고, 낮은 체력
Mage         = 2  // 공격력 최고, 낮은 체력·연사
ShieldBearer = 3  // 방어율·체력 최고
```

등급: `Normal(×1.0) → Uncommon(×1.1) → Rare(×1.2) → Unique(×1.3) → Epic(×1.4)`

---

## 스킬 시스템

### 액티브 스킬 (ActiveSkillId)
**총 33종** — 1~30 일반 · 31~33 보스/엘리트 전용.
`ActiveSkillData` SO + 각 `InGame/Skill/Actives/Active*.cs` 구현체.

> **⚠ enum 은 `InGame/Skill/ActiveSkillData.cs` 에 있다** (GameEnums.cs 아님).
> 번호·이름·직업 목록은 그 enum 의 줄 주석이 정본이다 — 여기에 표로 복사해 두지 말 것.
> (예전에 20종짜리 표가 남아 있어 실제와 13종 어긋났다)

### 패시브 스킬 (PassiveSkillType)
**40종** — `PassiveSkillData` SO + `PassiveSkillRuntimeSystem`.
목록은 `InGame/Skill/PassiveSkillType.cs` 가 정본.
등급별 슬롯: Normal/Uncommon=1, Rare/Unique=2, Epic=3

---

## 핵심 파일 위치

```
Assets/_project/
├── 1.Script/
│   ├── GameEnums.cs                         # GameState, PopupType, PoolType 등
│   ├── Data/
│   │   ├── Core/  UserDataManager, SaveCoordinator, ISaveSection
│   │   └── Sections/  UserData.cs, UnitData.cs
│   ├── InGame/
│       ├── Authoring/   *Authoring.cs + Baker (ECS 씬 설정)
│       ├── Battle/
│       │   ├── BattleManager.cs             # 전투 총괄
│       │   ├── BattleModeBase.cs · NormalMode.cs  # NormalMode(stageNumber) — RunBootstrap 이 만든다
│       │   ├── BattleEnums.cs               # BattleState, SpawnUnitType
│       │   ├── Spawner/  AllySpawner, EnemySpawner, HeroSpawner
│       │   └── Editor/  GameAssetCreator, ActiveSkillCreator, IconGenerator, CheatEditorWindow
│       ├── Skill/
│       │   ├── ActiveSkillData.cs           # SO 베이스, ActiveSkillId enum
│       │   ├── ActiveSkillExecuteSystem.cs  # Execute() 호출 (managed)
│       │   ├── ActiveSkillAISystem.cs       # 쿨다운·AI 판단 (ECS)
│       │   ├── ActiveSkillDatabase.cs       # SO 컬렉션
│       │   ├── Actives/  Active*.cs (스킬 구현체)
│       │   ├── PassiveSkillType.cs          # 패시브 enum
│       │   ├── PassiveSkillRuntimeSystem.cs # ECS 패시브 적용
│       │   └── Editor/  EffectTextureGenerator, EffectPrefabGenerator, EffectKeyLinker
│       ├── Unit/
│       │   ├── UnitComponents.cs            # ECS 컴포넌트 정의
│       │   ├── UnitJob.cs                   # UnitJob enum, UnitGrade enum
│       │   ├── UnitAttackSystem.cs
│       │   ├── UnitMovementSystem.cs
│       │   ├── UnitTargetSearchSystem.cs
│       │   ├── UnitHitSystem.cs
│       │   ├── UnitStatusEffectSystem.cs
│       │   └── *RuntimeBridge.cs (General/Soldier/Enemy)
│       ├── Projectile/  ProjectileSystem.cs, ProjectileView.cs
│       ├── Appearance/  UnitAppearanceBridge, UnitAnimationSync
│       ├── Stat/  StatType.cs(+StatRules), UnitStat.cs, HeroStatPipeline.cs
│       ├── Summon/  RunBootstrap · SummonController · 마나·특성·이벤트 규칙 (이 게임의 런)
│       ├── MonsterGear/ · MonsterCodex/  몬스터 장비 · 품질 개선
│       ├── Codex/  CodexCatalog.cs
│       ├── Difficulty/  DifficultyConfig.cs
│       ├── UI/  InGameHUD, TopBarUI, SummonDeckUI, ReincarnationPopup …
│       └── GameplayConfig.cs
│   ├── Tutorial/  TutorialManager, TutorialOverlay, Scenarios/ (도움말 셋 + 첫 유물)
│   ├── Relic/     RelicEnums + Tree/ (RelicTreeCatalog·Applier·Types·IconKey)
│   ├── Lobby/     LobbyManager, SceneDirector, LobbyDemoBattle
│   └── UI/
│       ├── Popup/   PopupBase·PopupManager + 팝업 구현 + Editor/ 각 Creator
│       ├── Lobby/   MainPanel(소환사 선택) + Editor/MainPanelCreator
│       ├── Common/  InfoTooltipUI, TooltipLayer, GeneralPortraitProvider
│       └── Juice/   UIJuice, UIJuiceLayer (성장 연출)
├── 2.Prefabs/
│   ├── Effect/  FX_*.prefab (이펙트 프리팹)
│   └── UI/      팝업·로비 프리팹 (Creator 산출물, 직접 편집 금지)
├── 3.Textures/
│   ├── FX/     이펙트 텍스처
│   └── Icons/  Classes·Skills·Items·RelicTree·RunPerks·Difficulty·StageNodes·LobbyBtns … (+ SpriteAtlas)
├── 4.Materials/
│   └── FX/     MAT_FX_*.mat (URP Add 머티리얼)
├── 5.Audio/    SFX·BGM (키 = 파일명)
└── Data/       몬스터 장비 · 소환사 · 도감 … SO 에셋

Assets/Resources/   *Database.asset · GameplayConfig · StageConfig · SpriteManager
Assets/PixelFantasy/  벤더 에셋 (외형 합성 — 우리 패치가 들어가 있다, 위 '공통' 항목 참고)
```

---

## 에디터 툴 — 루트가 둘이다 (확정 규칙)

> **메뉴 경로를 문자열로 직접 쓰지 말 것** — `Assets/_project/1.Script/Editor/ProjectKMenu.cs`
> 의 상수를 조합한다: `[MenuItem(ProjectKMenu.Popup + "Event", priority = ProjectKMenu.PrefabPrio + 42)]`

### ⚠ 루트 규칙 — 어디에 다는지가 곧 "믿어도 되는가" 의 표시다

| 루트 | 상수 | 무엇이 있나 |
|---|---|---|
| `Tools/Project K/` | `ProjectKMenu.Data` 등 | **이 프로젝트에 실제로 쓰는 도구만** |
| `Tools/올드Tools/` | `ProjectKMenu.LegacyData` 등 | 원작에서 그대로 넘어왔고 **아직 검증되지 않은 도구** |

**왜 나누나** — 이 프로젝트는 원작 코드베이스를 통째로 복사해 시작했다. 에디터 도구
수십 개가 처음부터 메뉴에 꽂혀 있는데 그중 상당수는 이 게임에 맞지 않는다(로비를 굽는
도구, 장수 배치 팝업을 만드는 도구처럼 진영이 뒤집히며 의미가 달라진 것들). 한 루트에
전부 두면 "이거 눌러도 되나?" 를 매번 코드를 열어 확인해야 한다. 분리해 두면 메뉴
위치만 보고 안다.

**규칙**

1. 원작에서 넘어온 도구는 전부 `Legacy*` 상수를 쓴다 (= 올드Tools 아래).
   **기본값이 이쪽이다. 판단이 서지 않으면 올드Tools 다.**
2. `Tools/Project K/` 로 올릴 수 있는 것은 둘뿐이다.
   - 이 프로젝트를 위해 **새로 만든** 도구
   - 원작 도구를 이 프로젝트에 맞게 **검증·수정해 이관한** 것
3. 이관은 "잘 도는 것 같다" 로 하지 않는다. **실제로 돌려서 산출물이 이 게임에 맞는지
   확인한 뒤** 옮긴다. 옮길 때 `Legacy*` → 일반 상수로 바꾸고 원작 위치에는 남기지 않는다
   (같은 도구가 두 루트에 동시에 보이면 분리한 의미가 없다).
4. 올드Tools 의 도구를 고칠 일이 생기면 **고치는 김에 검증해서 이관하는 쪽이 낫다.**
   올드Tools 는 "언젠가 정리할 것" 의 대기열이다.

### `Tools/Project K/` — 현재 내용 (2026-09-07)

```
Tools/Project K/
├─ 씬 이동/          Splash · Lobby · InGame 로드 (Ctrl+Shift+Alt+1/2/3)   (이관 09-07)
├─ 씬 셋업/          인게임 전장
├─ 데이터 생성/      몬스터 장비 · 액티브/패시브 스킬 · 난이도 · SpriteManager + 아틀라스
│                    비인간형 몬스터 라이브러리 · 몬스터 도감 · 소환사 · 스킬 카드 · 카드 목록
│                    유물 트리 점검 (표↔폴더 정합성)                    (신규 09-07)
├─ 아이콘·텍스처/    종족 · 시너지 · 종족 패시브 · 갈림길 그림 · 이벤트 그림 · 특성(런 특성)
│                    직업·스킬 · 시그니처 스킬 · 패시브                    (이관 09-07)
│                    데미지 숫자 폰트 · 스테이지 노드 · 아이템 · 난이도 · 이펙트 텍스처
│                    유물 트리 아이콘 (그림만)                          (이관 09-07)
│                    아이콘 임포트 설정 전체 재적용                        (이관 09-07)
├─ UI/               인게임 HUD (하단 카드 바 · 상단바 · 라인 대기열)
├─ 프리팹 생성/
│  ├─ 인게임/        인게임 유닛
│  ├─ 로비/          MainPanel
│  ├─ 이펙트/        Effect 프리팹 · 희귀·보스 스킬 이펙트                 (이관 09-07)
│  └─ 팝업/          ▶ 런 팝업 · 카드 선택 · 전투 통계 · 선택 목록 · 몬스터 상세
│                    강화소 · 제단 · 시설 · 상점 · 보상 상자 · 진화·융합 · 전황
│                    Pause · Loading · Codex · RelicTree · Reincarnation
│                    HeroDetail                                        (이관 09-07)
│                    장비 상세                                          (신규 09-10)
└─ 도구/             치트 (신규 09-11) · 스킬 SO 에 이펙트 키 연결 · 사운드 임포트 설정 (이관 09-07)
                     원작 잔재 정리 (씬·프리팹) (신규 09-11 — 아래 '원작 잔재 대청소')
```
(아이콘·텍스처 에 `로비 버튼 아이콘` — 유물·도감 두 장 — 도 09-11 에 이관했다)

**치트** (`InGame/Battle/Editor/CheatEditorWindow.cs`, 플레이 모드 전용) — 탭 여덟:
런(마나·마왕성·런 골드·시그니처·용사 전멸·스테이지 이동) · 카드 · 특성 · 재화(영구 골드·환생 포인트·
최고 도달·소환사 전부 해금) · 도감 · 유물 · 난이도 · 튜토리얼.
- ⚠ **게임이 쓰는 함수를 그대로 부른다** — 특성은 `RunPerkData.Add`, 카드는 `SummonDeckData.Acquire`(+도감 해금),
  용사 전멸은 `DeadTag` → 기존 사망 파이프라인. 값을 직접 쓰면 "치트로는 되는데 실제로는 안 되는" 상태를 시험한다
- 스테이지 이동은 `RunBootstrap.CheatJumpToStage`(에디터 전용) — 대기 중에만, 정상 `EnterStageReady` 경로를 탄다

```text
```

**▶ 전부 다시 굽기 (2026-09-07 신설)** — `Editor/ProjectKBuildAll.cs`

`Tools > Project K > ▶ 전부 다시 굽기` 하나로 끝난다. 그룹별 `▶ 전체 생성` 도 있다
(아이콘·텍스처 / 데이터 생성 / 프리팹 생성 > 팝업).

⚠ **순서가 이 도구의 전부다.** 손으로 누르면 이 순서를 외워야 한다.

| 차례 | 무엇 | 왜 이 자리인가 |
|---|---|---|
| ① | 아이콘·그림 | 아래 것들이 전부 이걸 참조한다 |
| ② | 데이터(SO) | 굽는 동안 ①의 PNG 를 찾아 꽂는다 (몬스터 장비가 그렇다) |
| ③ | 프리팹·HUD | 굽는 동안 ①②를 참조한다 (HUD 가 패시브 아이콘을 박는다) |
| ④ | SpriteManager + 아틀라스 | **맨 마지막.** 위에서 만든 그림을 쓸어 담는다 |

⚠ ④를 ①보다 먼저 돌리면 **에러 없이 그림만 전부 빈 칸**이 된다 —
이 프로젝트에서 가장 자주 밟은 함정이다.

⚠ 하나가 실패해도 멈추지 않는다. 실패를 모아 **끝에 한꺼번에** 에러로 알린다 —
멈추면 "다시 눌러야 하는 목록" 이 또 생겨서 도구를 만든 뜻이 사라진다.

⚠ **일부러 뺀 둘은 손으로 한다** — `씬 셋업 > 인게임 전장`(씬을 열고 저장해야 한다)과
씬의 `PopupManager > [Load Popup Prefabs]`(인스펙터 버튼이라 메뉴가 없다).

⚠ **도구를 새로 만들면 `ProjectKBuildAll` 표에도 넣을 것.** 안 넣으면
"전부 다시 굽기" 를 눌러도 그것만 옛날 것이 남는다.

**2026-09-07 이관 근거** — 판단 기준은 **이 게임 런타임이 산출물을 읽는가** 하나다.

| 옮긴 것 | 무엇이 읽나 |
|---|---|
| 유물 트리 | 이 게임의 시스템이다 — 애초에 올드에 있을 이유가 없었다. 메뉴 5개를 그림/표 2개로 갈랐다 |
| 직업·스킬 아이콘 | `skill_*` ← `ActiveSkillData.IconKey` (몬스터 고유 스킬 · 소환사 시그니처) |
| 패시브 아이콘 | `Icons/Passives/passive_*` ← **이미 이관된** `데이터 생성 > 패시브 스킬` 이 읽는다 |
| 스테이지 노드 아이콘 | `stage_*` ← `StageNodeUI` |
| 아이템 아이콘 | `item_*` ← `eItem.IconKey` (품질 개선 화면의 보유 골드) |
| 난이도 아이콘 | `difficulty_*`/`debuff_*` — 특성 아틀라스에 함께 얹혀 있다 |
| 이펙트 3종 + 키 연결 | 이 게임의 스킬이 전부 쓴다 (`SkillEffectHelper`) |
| 아이콘·사운드 임포트 | 파이프라인 공용 — `RelicTreeIconStubs` 가 `IconImportSetup` 을 직접 부른다 |
| 씬 이동 3 | 순수 네비게이션 |

⚠ **메뉴 우선순위는 두 루트 통틀어 충돌 0건이다.** 항목을 추가하면 같은 그룹 안에서
같은 `+N` 이 되지 않게 할 것 — 겹치면 유니티가 순서를 임의로 정한다.

**팝업 이관 근거 (2026-08-28)** — 실제로 열리는 것만 올렸다.

| 항목 | 여는 곳 |
|---|---|
| 카드 선택 | `RunBootstrap.HandleVictory` — 스테이지 클리어 3택 |
| 전투 통계 | `CardSelectPopup` 상단 버튼 |
| Reincarnation | `RunBootstrap.HandleDefeat` — **런 종료 화면이다**. 전용 패배 팝업은 만들지 않는다 |
| Pause | `TopBarUI`(인게임) |
| Loading | `SplashBootstrap` · `LobbyManager` 씬 전환 |
| Codex | `MainPanelUI` 도감 버튼 · 환생 직후 '이번 여정 수확' |
| RelicTree | `MainPanelUI` 유물 버튼 |

`GeneralStatRow.prefab` 은 Reincarnation 항목이 함께 굽는다 —
**전투 통계 팝업도 같은 행 프리팹을 쓴다.**

**데이터 도구 이관 근거 (2026-09-04)** — 이 게임의 런타임이 산출물을 읽고,
**진영 반전과 무관한** 것만 올렸다.

| 옮긴 것 | 왜 |
|---|---|
| 액티브 스킬 | `ActiveSkillDatabase` — 용사·몬스터·소환사가 **전부** 쓴다. 소환사 시그니처(34)도 여기서 굽는다 |
| 패시브 스킬 | `PassiveSkillDatabase` — 종족 패시브가 `AddPassiveSlot` 으로 쓴다 |
| 난이도 · SpriteManager | 진영과 무관한 설정값·그림 인덱스 |

원작의 어빌리티 · 특성(직업 시너지) · 용사 장비 · 이벤트 · StageConfig 도구는
**2026-09-11 에 코드째 지웠다** (아래 '원작 잔재 대청소'). 어빌리티에서 남은 규칙은
`StatRules.IsAbsoluteStat`(StatType.cs 끝) 하나다.
⚠ **몬스터 장비는 별개 물건이다** — `데이터 생성 > 몬스터 장비`(`MonsterGearCreator`)

⚠ **`액티브 스킬` 은 `db.Entries.Clear()` 로 시작한다** — `ActiveSkillCreator` 가 DB 의 정본이다.
거기 없는 스킬은 다시 구울 때마다 사라진다.

> Codex 는 **몬스터 · 특성** 두 탭이다 (2026-09-06). 장비·어빌리티·장수 탭은 제거했다.
> 자세한 것은 아래 '도감 (Codex)' 항목 참고.

### `Tools/올드Tools/` — 원작에서 넘어온 것 (미검증)

2026-09-11 대청소로 원작 화면(로비 패널·어빌리티·장비·용병·런 상점·이벤트·결과 팝업)의
Creator 가 **코드째 사라졌다.** 올드Tools 에 남은 것은 씬 셋업 몇 개뿐이다 —
메뉴를 열어 보고, 돌리기 전에 무엇을 만드는 도구인지 코드를 확인할 것.

**프리팹 정본 (중복 생성자 금지)** — 한 프리팹은 한 Creator 만 만든다.
Pause · Loading 은 `PopupPrefabCreator.cs`, 나머지 팝업은 각자 `*PopupCreator.cs` 다.

**공용 UI 빌더** — `Assets/_project/1.Script/Editor/EditorUIBuilder.cs`
Creator 들이 각자 복사해 쓰던 `Make*/Create*/Add*` 헬퍼의 본문은 전부 여기 있다.
각 Creator 는 기존 이름을 한 줄 포워더로만 유지한다. 새 헬퍼가 필요하면
로컬에 또 만들지 말고 여기에 추가할 것.

---

## UI 제작 규칙 (Creator 작성 시 필수)

> **⚠ 규칙 1 — 누를 수 있는 버튼에는 반드시 음각을 넣는다**
> 평평한 사각형은 버튼인지 라벨인지 구분이 안 된다.
> `EditorUIBuilder.RaisedBtn()` / `RaisedTextBtn()` / `RaisedBtnOn()` 으로만 만들 것.
> ```csharp
> var btn = EditorUIBuilder.RaisedBtn(parent, "BuyBtn", faceColor, out var body);
> // 라벨·아이콘은 반드시 body 아래에 넣는다 (루트에 넣으면 눌려도 안 내려간다)
> ```
> 구조: `Shadow`(아래 6px 노출 = 두께) → `Body` → `TopEdge`(밝은 2px) + `BottomEdge`(어두운 4px).
> 눌림 색은 `Button.colors` 가 targetGraphic 색에 **곱해지므로** `TintFor()` 로 역산한다.
> 템플릿처럼 루트가 이미 있으면 `RaisedBtnOn(root, ...)` — 자식 경로가 `Body/...` 로 유지된다.

> **⚠ 규칙 2 — 장식 기호에 폰트 글리프를 쓰지 않는다**
> 기본 폰트 `LiberationSans SDF` 는 **문자 250자(ASCII + Latin-1 일부)뿐**이고
> `m_AtlasPopulationMode: 0` = **Static** 이라 런타임에 글리프를 채울 수도 없다.
> 없는 글자는 □(두부)로 그려져 그대로 화면에 노출된다. (한글은 폴백 폰트가 처리)
>
> | 없음 (쓰지 말 것) | 있음 (써도 됨) |
> |---|---|
> | `★ ✔ ✕ ▶ ◀ ▲ ⚙ 🔒` | `› — × € ™ □` |
>
> `EditorUIBuilder.CheckMark / XMark / Chevron / Diamond / PadLock / Bar` 로 그릴 것.
> 새 기호가 필요하면 `Bar()`(회전 막대)를 조합해 헬퍼를 추가한다.

> **⚠ 규칙 3 — 반투명 테두리를 자식으로 두지 않는다**
> Unity UI 는 자기 Graphic 을 먼저 그리고 그 다음 자식을 그린다.
> `SetAsFirstSibling()` 을 해도 자식은 부모 Image 보다 뒤로 갈 수 없다.
> 테두리는 대상의 **앞 형제**로 만들어 뒤에 깔 것.

> **⚠ 규칙 4 — 폰트 크기는 `UIScale` 상수만 쓴다**
> `FontSm(34) / FontMd(42) / FontLg(56) / FontXl(76)`, 버튼 `BtnSm(100) / BtnMd(132) / BtnLg(164)`.
> 하드코딩 금지. 모바일 실기 기준으로 잡힌 값이며 `FontSm` 미만은 읽히지 않는다.

> **⚠ 규칙 5 — 칸 높이를 손으로 적지 않는다 (글자 잘림 방지)**
> TMP 한 줄은 폰트의 **약 1.25배**를 쓴다. 칸을 폰트보다 작게 잡으면 아래가 잘린다.
> ```csharp
> UIScale.RowSm / RowMd / RowLg     // 43 / 53 / 70 — 한 줄짜리 칸
> UIScale.Line(fontSize)            // 임의 폰트의 한 줄 높이
> UIScale.BtnFor(fontSize)          // 라벨이 안 눌리는 최소 버튼 높이 (×1.7)
> ```
> 폰트 상수를 올릴 때 이 값을 쓰는 칸은 자동으로 따라 커진다.
>
> **⚠ 한자·가나는 이 1.25배 가정을 넘는다** (사용자 지적, 2026-09-16)
> `Line()` 의 계수는 **라틴·한글 기준**이다. 한자·가나는 글자 상자를 꽉 채워 더 높아서,
> 같은 칸에서 라틴·한글만 멀쩡하고 CJK 만 잘린다. CJK 가 뜰 수 있는 칸은 **한 단계 위**를 준다
> (`FontSm` 이면 `RowSm`(43)이 아니라 `RowMd`(53)).
> - ⚠ **증상이 "잘림" 이 아니라 "빈칸" 이다** — 그래서 폰트에 글자가 없는 것으로 읽힌다.
>   실제로 일시정지 언어 드롭다운에서 이걸 폰트 문제로 오진해 폴백 폰트를 두 번 갈아엎었다.
>   같은 화면의 캡션 줄은 칸이 넉넉해 멀쩡히 나와서 더 헷갈렸다 — **한 화면에서 어떤 줄만
>   비어 있으면 폰트보다 칸 높이를 먼저 볼 것.**
> - ⚠ 라벨 RectTransform 만 위아래로 늘려 때우지 말 것 — 칸 밖으로 넘쳐 이웃 줄과 겹친다.
>   넓혀야 하는 것은 칸이다 (`EditorUIBuilder.LabeledDropdown` 의 `itemH` 참고).

> **⚠ 규칙 6 — 팝업 높이는 `UIScale.PopupMaxH`(1000) 를 넘기지 않는다**
> 로비 캔버스 세로가 1080 뿐이라 그 이상은 위아래가 잘린다.
> 고정 높이 대신 세로 스트레치 + 가변 영역으로 만드는 쪽이 더 안전하다
> (EventPopup 참고: 패널은 캔버스에 맞추고 `ChoiceRoot` 가 남는 높이를 흡수).

> **⚠ 규칙 8 — 글자 대비는 외곽선이 아니라 색으로 만든다** (2026-09-02)
> Creator 에서 `tmp.outlineWidth = 0.25f` 는 **아무 일도 하지 않는다.** TMP 가 그
> 자리에서 런타임 머티리얼 인스턴스를 만들어 값을 넣는데, 그 인스턴스는 에셋이
> 아니라 `SaveAsPrefabAsset` 이 끌고 가지 못한다 — 프리팹에는 남지 않는다.
> (`UISetupTool` 등 여러 Creator 가 아직 이 줄을 갖고 있지만 전부 무효다)
>
> 글자가 무엇 위에 놓이는지는 **만들 때 이미 정해져 있다.** 그 바탕의 밝기를 보고
> 글자색을 정하면 끝난다.
> ```
> 어두운 카드 면 위   →  밝은 글자
> 흰 말풍선 위        →  어두운 글자
> ```
> ⚠ 바탕을 **그림**으로 잡지 말 것. 한때 배지 숫자를 아이콘 그림 위에 앉히고
> 그 자리의 밝기로 글자색을 정했는데, 아이콘을 갈아 끼울 때마다 다시 재야 했다.
> 지금은 숫자를 아이콘 옆에 두어 바탕이 카드 면 하나로 고정돼 있다 (규칙 7).
> 아무 데나 통하게 만들겠다고 흰 글자에 검은 테두리를 두르면, 글자는 뭉개지고
> 머티리얼만 한 벌 더 는다. 정말로 바탕이 시시각각 변하는 자리(전장 위에 뜨는
> 피해 숫자)에서만 외곽선을 쓰고, 그때는 **머티리얼에 구워** 둔다
> (`DamageFontCreator` 참고).

> **⚠ 규칙 7 — 마나와 몬스터 마릿수는 언제나 아이콘이다** (2026-09-02 확정)
> `"마나 12"` · `"9마리"` 처럼 글자로 적지 말 것. 이 둘은 게임에서 가장 자주 읽는
> 두 숫자라, 화면마다 표기가 갈리면 같은 값을 매번 다시 읽어야 한다.
> ```csharp
> EditorUIBuilder.IconValueBadge(parent, "ManaCost", manaIcon, rightSide: false,
>                                iconSize, UIScale.FontSm, inset, numberColor, out var value);
> ```
> - 아이콘 PNG 는 **이미 있다** — `Editor/UIIconAssets.cs` 가 경로·임포트 설정의 정본이다.
>   생성하는 도구 같은 것은 없다. `Image.sprite` 에 그대로 꽂아 쓴다.
> - 배지 구조: **[아이콘][숫자] 를 가로로 나란히** (2026-09-03 확정 — 아래 근거).
>   좌우 한 쌍의 배지 **안쪽 순서는 양쪽 다 같다.** 거울상으로 뒤집으면 두 숫자가
>   카드 가운데에서 마주 봐 어느 아이콘의 값인지 헷갈린다.
> - ⚠ **숫자를 아이콘 그림 위에 얹지 말 것.** 2026-09-02 에 그렇게 만들었다가
>   되돌렸다. 그러면 맞춰야 할 값이 전부 **PNG 에 종속**된다 —
>   숫자의 세로 자리는 그림에서 비어 있는 줄을 알파로 재서 찾아야 하고,
>   글자색은 그 자리의 밝기를 봐야 하고, 폰트 크기는 그림의 불투명 폭에 묶여
>   `UIScale` 단계를 못 쓴다. 아이콘을 갈아 끼울 때마다 셋을 다시 재야 했다.
>   나란히 놓으면 숫자가 **카드 면**(늘 어두운 바탕) 위에 앉아 셋 다 사라진다.
> - 숫자 크기는 `UIScale` 단계를 그대로 쓴다 (규칙 4). 하단 카드 바 `FontSm`,
>   카드 3택 팝업 `FontLg`. 세 자리는 자동 축소가 받는다.
> - 글자색은 **바탕(카드 면)** 을 보고 정한다. 어두운 남색 면이라 둘 다 밝은 색이다
>   (마나=흰색 · 마릿수=크림색).
>   ⚠ 하단 카드 바의 **마나 색 정본은 Creator 가 아니라 `SummonCardUI`** 다 —
>   원가/과부하/할인에 따라 흰·붉은·초록으로 갈린다. Creator 값은 프리팹 기본값이니
>   런타임의 '원가' 색과 같게 유지할 것.
>   ⚠ 숫자 뒤에 면(칩·패널)을 깔지 말 것 — 아이콘이 가려져 무슨 배지인지 안 보인다.
>   ⚠ 외곽선도 두르지 않는다 (규칙 8).
> - ⚠ **좌우 한 쌍을 놓을 때는 폭을 반드시 확인할 것**
>   `EditorUIBuilder.BadgeWidthFor(아이콘, 폰트) × 2 + 여백 × 2 ≤ 부모 폭`.
>   ⚠ 아이콘 폭만으로 재지 말 것 — 나란히 놓으면 배지가 숫자 칸만큼 더 넓다.
>   이 계산을 빼먹어 150px 짜리 카드에 86px 짜리 배지를 둘 넣었다가 22px 겹쳤다.
> - 하단 카드 바는 이 식이 아이콘 크기를 정한다 — 카드가 150px 뿐이라
>   아이콘 36 · 폰트 `FontSm` 이 상한이고, 여백을 **음수(−6)** 로 두어 카드 사이
>   간격(14)의 절반씩을 빌려 쓴다. 카드 간격을 줄이면 여기가 먼저 깨진다.
> - ⚠ TMP 스프라이트 태그(`<sprite name=...>`)는 **쓰지 않는다.** TMP 는 일반
>   Sprite 를 글 안에서 못 읽어 별도 스프라이트 에셋을 구워 등록해야 하는데,
>   등록 전에는 스톡 이모지의 0번(**❓**)이 대신 그려진다.
>   PNG 가 이미 있는데 굽는 단계를 하나 더 두는 것은 값을 못 한다.

**캔버스 기준이 씬마다 다르다 (주의)**
현재 복사돼 온 상태: `Lobby.unity` = **1920×1080 가로**, `InGame.unity` = 1080×1920 세로.
팝업 대부분은 로비 위에 뜨므로 **세로 여유가 1080 뿐**이다.
팝업 높이를 고정하면 잘린다 — 세로 스트레치 + 가변 영역으로 만들 것.

> ⚠ **이 프로젝트 할 일**: 게임이 가로 기준이므로 `InGame.unity` 캔버스도
> **1920×1080 가로로 맞춰야 한다.** 인게임 코어 작업 때 함께 정리할 것.
> `UIScale.RefWidth/RefHeight` 상수도 1080×1920으로 박혀 있어 같이 봐야 한다.

---

## 오브젝트 풀 (PoolType)

```csharp
UI=0, Unit=1, Effect=2, Projectile=3
```
`PoolController` 싱글턴 → `ObjectPool<T>` 관리.

---

## 작업 유형별 관련 파일

> 세션 시작 시 아래 목록만 읽으면 탐색 없이 바로 작업 가능.
> **경로는 전부 `Assets/_project/1.Script/` 기준**이다. 여기 없는 영역을 만나면
> 작업이 끝난 뒤 이 표에 3~4줄 추가할 것 — 다음 세션의 탐색이 그만큼 줄어든다.

### 공통 — 고치기 전에 알아야 할 것

- **컴파일 검증**: 유니티를 켜지 않고 확인한다.
  `dotnet build Assembly-CSharp.csproj -v:q -nologo` (에디터 코드는 `Assembly-CSharp-Editor.csproj`).
  출력이 기니 `| grep -E " error CS|Build succeeded"` 로 자른다.
- **⚠ Creator 를 고쳤으면 프리팹은 따로 구워야 한다**
  `Tools > Project K > 프리팹 생성 > …` 실행 → 팝업이면 `PopupManager > Load Popup Prefabs` 까지.
  (이 게임에서 쓰지 않는 팝업은 `Tools > 올드Tools > …` 아래에 있다)
  런타임 스크립트만 고친 경우는 필요 없다 (동작은 바뀌고 겉모습만 옛날 것이 남는다).
- **벤더 에셋(PixelFantasy)을 업데이트한 직후**: 우리 패치 3개가 통째로 날아간다 —
  `CharacterBuilder.cs`(외형 공유 캐시) · `TextureHelper.cs`(머지 버퍼) · `Layer.cs`(mask 해시).
  `git diff -- Assets/PixelFantasy/**/*.cs` 로 사라진 부분을 확인해 새 코드 위에 다시 얹는다.

### 액티브 스킬 추가
1. `GameEnums.cs` — `ActiveSkillId` enum 값 추가
2. `InGame/Skill/Actives/Active{이름}.cs` — 신규 생성 (기존 파일 참고)
3. `InGame/Battle/Editor/ActiveSkillCreator.cs` — SO 자동 생성 에디터 툴
4. `Assets/Resources/ActiveSkillDatabase.asset` — SO 등록 (에디터)
5. `LocalizationManager.cs` — 한국어 이름 추가
6. (이펙트 필요 시) `InGame/Battle/Editor/GameAssetCreator.cs`

### 패시브 스킬 추가
1. `InGame/Skill/PassiveSkillType.cs` — enum 추가
2. `InGame/Skill/PassiveSkillRuntimeSystem.cs` — 효과 로직 추가
3. `Assets/Resources/PassiveSkillDatabase.asset` — 등록 (에디터)

### 로비 UI 수정
> **⚠ 로비 화면은 MainPanel(소환사 선택) 하나뿐이다** (2026-09-11) — 원작 탭(NavBar)·
> TopBar·HeroPanel·BattlePanel·ProfilePanel·ShopPanel 은 코드째 지웠다.
> `LobbyManager.SelectInitialPanel` 이 MainPanel 을 직접 켠다 (이어하기면 곧장 런으로).

- **소환사 선택 화면 (MainPanel)**: `UI/Lobby/MainPanelUI.cs` (+ `Editor/MainPanelCreator.cs` → `프리팹 생성 > 로비 > MainPanel`)
  - **2026-09-11 다시 짰다** (사용자 요청 — "너무 보기 어렵다") — [사이드] [소환사 격자 3×4] [오른쪽 칸]
    | 칸 | 무엇 | 컴포넌트 |
    |---|---|---|
    | 격자 | 전원을 카드로 (초상화·이름·잠금, 고른 카드만 금테) | `SummonerListCardUI` |
    | 오른쪽 ① | 소환사 정보 — 초상화 · 환산 칩(마왕성·[마나]·소환력·카드 칸) · 설명 · 눈금 3줄 · 개성 · 시그니처 스킬 · 친화/시작 카드 → [선택하기] | `SummonerCandidateCardUI` |
    | 오른쪽 ② | [선택하기] 뒤 **같은 자리**에 난이도 다섯 줄(보상 배율·해금 조건) → [뒤로][게임 시작] | `DifficultySelectorUI` |
  - ⚠ 잠긴 소환사도 **누를 수 있다** (정보·해금 조건을 보려면). 막는 것은 [선택하기] 와 `OnStartPressed` 의 재판정이다
  - ⚠ ② 에서 격자의 다른 카드를 누르면 ① 로 돌아간다 — 누구로 출정하는지가 흐려지지 않게
  - ⚠ 격자 칸은 12개(`ListCols`×`ListRows`)다. 소환사가 늘면 `MainPanelUI` 가 에러를 낸다 — 상수를 늘리고 `FrameH` 안에 드는지 볼 것
  - ⚠ 두 칸은 y 를 쌓아 짓고 `VerifyFits` 가 넘침을 잰다 (`FrameH` 952 · `PanelH` 836). 줄을 더하면 굽는 순간 에러로 알린다
  - ⚠ 눈금 3줄은 초상화 옆(머리)에 있다
- **시그니처 스킬은 이름·그림이 소환사마다 다르다** (사용자 지시, 2026-09-11) — 정본 `SignatureSkillDisplay` (SummonerSkillRule.cs 끝)
  - '권속 소환' 은 여덟 소환사가 나눠 쓴다 → 그림 = **부르는 종족의 초상화**, 이름 = `SummonerData.SignatureName`
    (로스터 `SkillName` — 슬라임 떼 · 치유의 친위대(힐 슬라임) · 고블린 약탈대 · 트롤 풀어놓기 · 오크 전사 소집 · 멧돼지 돌격 · 늑대 무리)
  - 나머지 스킬(비석·메테오·사형 선고…)은 SO 이름·그림 그대로
  - ⚠ **줄을 더 적어 설명하지 않는다** (사용자 지적 — "쓸모없는 UI 추가하지 마"). 한때 스킬 줄 아래
    "[초상] 슬라임 [마릿수] 3 · 탭한 자리에" + 설명을 붙였다가 걷어냈다 — 메테오처럼 소환이 아닌 스킬은 적을 것이 없다
  - 선택 화면(`SummonerCandidateCardUI`)과 전투 버튼(`SummonerSkillButtonUI`)이 같은 함수를 지난다
- **카드 칸은 소환사마다 4 · 5 · 6** (사용자 지시, 2026-09-11) — 정본 `SummonerCreator` 로스터(줄마다 이유 주석)
  | 칸 | 소환사 |
  |---|---|
  | 4 | 슬라임 킹 · 스컬 킹 · 역병 술사 · 트롤 조련사 · 오크 킹 (좁고 두껍게) |
  | 5 | 견습 · 흑마법사 · 리치 로드 · 도살자 (기준 `SummonerData.StandardDeckSlots`) |
  | 6 | 고블린 두목 · 드루이드 · 비스트마스터 (넓게) |
  - 시작 칸 = 소환사 + 유물 '전열 확장'(`SummonerData.StartDeckSlots`) · 특성까지는 `RunPerkRule.DeckSlotsFor`
  - ⚠ **상한 8** (`RunPerkRule.MaxDeckSlots`) — 강화소·제단 격자(`CardPickPopupBase.MaxCells` 가 이 값을 쓴다)·전황 아군 4×2 가 8칸까지만 담는다
  - ⚠ **이어하기는 칸을 늘리기만 한다** (`RunBootstrap.BuildStarterDeck`) — 런 도중의 칸 변화는 `RunPerkData.Add` 가
    그 자리에서 했고 덱 세이브에 남아 있다. 다시 줄이면 `ResizeTo` 가 뒤 칸의 **카드를 자른다**
  - 선택 화면의 숫자는 시작 칸(유물 포함), 색은 소환사 본래 칸이 기준보다 많으면 초록 · 적으면 주황
  - ⚠ 초상화는 격자·정보 칸이 **같은 함수**(`SummonerCandidateCardUI.RenderPortrait`)를 쓴다. 소환사가 바뀔 때만 다시 합성한다 (576×928 병합이 비싸다)
- **텍스트 상수**: `UIConstants.cs`, `LocalizationManager.cs`

### 팝업 추가/수정
- **베이스**: `UI/Popup/PopupBase.cs` — 상속 필수, `protected override void Awake()` + `base.Awake()` 호출
- **관리자**: `UI/Popup/PopupManager.cs` — `_prefabs` 배열에 신규 프리팹 등록 필요
- **열기**: `PopupManager.Instance.Open<T>(PopupType.X).Setup(...)`
- **enum**: `GameEnums.cs` — `PopupType` 에 값 추가 (지운 번호는 주석으로 예약돼 있다 — 재사용 금지)

### 전투 이벤트 버퍼
- **버퍼**: `InGame/Skill/PassiveSkillComponents.cs` (`EnemyKillEvent`·`SkillUseEvent`·`AttackHitEvent`·`SoldierDeathEvent`)
  — 읽는 쪽은 패시브·투지 시너지(`MonsterSynergyKillSystem`) 등
- **비우기**: `InGame/Skill/CombatTriggerSystem.cs` — 장비·어빌리티·특성 트리거가 사라져
  **이제 비우기만 한다.** ⚠ 지우지 말 것 — 버퍼를 비우는 곳이 여기뿐이라, 없으면 매 프레임 같은 사건이 쌓인다.
  먼저 도는 시스템이 지우면 읽는 쪽이 통째로 죽는다
- **이벤트 발행**: `InGame/Battle/UnitDeathDespawnSystem.cs` (처치 판정·사망 위치)
- **소환 효과**: `InGame/Skill/SkeletonSpawner.cs` — 스킬·비석·장비가 공유하는 단일 진입점
- **⚠ 소환수의 진영은 언제나 시전자에게서 온다** (`SkillCrowdControl.TeamOf`, 2026-09-06)
  - `SoldierRuntimeBridge._team` 기본값이 `Faction.Hero` 다. `SetTeam` 을 빠뜨리면 적으로 선다
  - 한때 스켈레톤 소환·돌격 병사·정예 소환이 `OnUnitSpawned(TeamType.Ally)` 를 **상수로**
    박아 둔 채 `SetTeam` 을 안 했다 → 스폰은 `AliveAllyCount`, 사망은 `AliveEnemyCount` 를
    깎아서 **소환수가 죽을 때마다 적 수가 줄었다.** 보스가 멀쩡한데 "적 전멸" 로 클리어됐다
  - 유닛을 스폰하는 코드는 `SetTeam(team)` → `OnUnitSpawned(team)` → `Initialize` 순서다
    (Initialize 안에서 Team 이 엔티티에 박힌다)

### 재화
- `eItem` 은 `Gold`(영구 골드) · `ReincarnationPoint` 둘뿐이다 (`Data/Item/eItem.cs`)
- **⚠ 환생 포인트는 `ItemData` 가 아니라 `Data/Sections/ReincarnationData.cs` 가 정본이다**
  (`ItemData` 가 그 항목만 위임한다). 갈림길 이벤트는 `InGame/Summon/RunEvent.cs` — 아래 항목

### 몬스터 도감 · 품질 개선
- **세이브**: `Data/Sections/MonsterCodexData.cs` — 해금(`Unlock`)과 종족별 **영구 품질**.
  환생해도 남는다. 해금은 `RunBootstrap`(시작 덱·카드 보상)이 언제나 `Normal` 로 연다
- **개선 규칙 정본**: `InGame/MonsterCodex/MonsterGradeUpgradeRule.cs`
  - **값은 한 런의 수입에 견준다** (사용자 지시, 2026-09-10) —
    `700 / 4,500 / 18,000 / 40,000` (한 종족 Epic 까지 **63,200**).
    영구 골드는 그 런에 **번 총액**이 그대로 들어온다 — 5스테이지에서 멈춘 런이
    약 **700**, 30스테이지를 다 돈 런이 약 **28,000** 이다.
    옛 표(300/900/2400/6000)는 한 종족 9,600이라 **한 런에 세 종족이 Epic** 이 됐다
    - ⚠ **첫 칸은 싸고 무게는 가운데 두 칸이 진다** — 초반은 부대가 아직 안 늘어
      수입이 적다. 첫 칸이 비싸면 도감을 **처음 만지기까지**가 너무 멀다.
      덜어 낸 몫은 뒤 칸에 얹었다
    - ⚠ **마지막 칸은 벽이 아니어야 한다** — 50,000 은 30스테이지 런 두 번이
      오롯이 한 칸에 들어가는 값이라 Epic 이 "안 갈 곳" 이 됐다. 40,000 = 런 1.5회
    - ⚠ 단계 배율은 6.4× → 4.0× → **2.2× 로 완만해진다.** 등급당 스탯은 +10% 로
      일정하므로, 완만해져야 마지막 칸이 가장 값어치 있는 칸이 된다
    - ⚠ 수입 손잡이(`RunGoldRule.GoldPerKill` · `HeroDeployment` 부대·병사 수)를
      고치면 이 표도 함께 볼 것
  — 비용(`CostFor`) · 판정(`Check`) · 차감(`TryUpgrade`)이 전부 여기 하나에 있다
  - **확정이다. 확률이 아니다** (사용자 확정, 2026-09-06). 무게는 비용이 만든다
  - ⚠ **영구 골드**(`ItemData` 의 `eItem.Gold`)로 산다. `RunGoldData` 로 사지 말 것 —
    런이 끝나면 사라지는 돈으로 영원한 것을 사면 "마지막 판에 몰아 지르기" 가 정답이 된다
  - ⚠ 비용 숫자를 화면에 적지 말 것 — 표시·판정·차감이 `CostFor` 하나를 본다
- ⚠ **런 중에는 도감을 못 고친다** (사용자 확정, 2026-09-10) — 정본 `InGame/MonsterCodex/CodexEditLock.cs`
  - 잠그는 것은 **품질 개선**과 **장비 장착·해제** 둘이다. 보는 것은 안 잠근다
    (전황에서도 같은 창이 열린다)
  - 이유 둘 — ① 스탯은 **소환하는 순간 굳는다**(`MonsterStatComposer`). 판 도중에
    올리면 라인에 이미 선 개체와 지금 나오는 개체가 다른 몬스터가 된다
    ② 품질 개선은 **영구 골드**를 쓴다 — 런 중에 쓸 수 있으면 "질 것 같으면 창을
    열어 등급을 올린다" 가 최적해가 되어 갈림길·상점이 값을 잃는다
  - ⚠ 판정은 한 곳이다. 품질은 `MonsterGradeUpgradeRule.Blocked.InRun`(Check 맨 앞),
    장비는 칸 버튼 + `HandleEquip`/`HandleUnequip` 이 같은 함수를 본다
  - ⚠ **런 중에는 품질 개선 버튼을 통째로 감춘다** (사용자 지시, 2026-09-12) —
    `MonsterDetailPopup.RefreshUpgrade` 가 `InRun` 이면 `_upgradeBtn` 과 `_costRoot` 를 끈다.
    "런 중 잠금" 같은 글을 라벨에 띄우지 말 것 — 못 누르는 버튼이 판 내내 남아
    창을 열 때마다 같은 말을 다시 읽게 된다. 런 중에 할 수 없는 일이면 그 자리는 비운다
    - ⚠ 단계 핍·`고급 › 희귀` 줄은 남긴다 — 그건 지금 품질을 **읽는** 줄이지 사는 버튼이 아니다
  - ⚠ `LobbyManager` 가 없으면 **안 잠근다** — 에디터에서 도감만 열어 보는 경우다

- **골드 두 갈래** (2026-09-06 확정)
  | | 지갑 | 수명 | 쓰는 곳 |
  |---|---|---|---|
  | 런 골드 | `RunGoldData` | 런 종료에 사라짐 | 갈림길·상점 |
  | 영구 골드 | `ItemData`/`eItem.Gold` | 영구 | 품질 개선 · 원작 용병·장비 |
  - 고블린 '약탈'(`SpeciesPassive.Loot`)은 **런 골드**를 준다 (`RunGoldRule.GrantLoot`)
  - 런이 끝나면 그 런에 **번 총액**(`Earned`, 쓴 것 포함)만큼 영구 골드가 따라온다
    (`RunGoldRule.SettleToPermanent`, `RunBootstrap.FinishRun` 에서 **한 번만**)
    - ⚠ '남은 잔액' 이 아니라 '번 총액' 이다 — 잔액을 주면 안 쓰고 모으기가 최적해가 된다
    - ⚠ **환생도 결산한다** (`UserDataManager.Reincarnate` 맨 앞, 2026-09-11) — 환생 버튼은
      `Reincarnate()` 를 `FinishRun` **보다 먼저** 부른다. 한때 그 안에서 `RunGoldData` 를 비워
      결산이 0 이 됐고, 게다가 `ItemData.SetDefaults()` 가 영구 골드를 500 으로 되돌렸다.
      지금은 `ItemData.ResetKeepingGold()` · 결산은 지갑을 비워 **두 번 불려도 한 번만** 준다
- **화면**: `UI/Popup/CodexPopup.cs` 의 `Monster` 탭(목록) → 칸을 누르면
  `UI/Popup/MonsterDetailPopup.cs`(세부 스탯 + 고유 스킬 + 품질 개선). 굽는 곳은 각각
  `Editor/CodexPopupCreator.cs` · `Editor/MonsterDetailPopupCreator.cs`
  - **품질 개선 패널의 구성이 규칙이다** (사용자 지적으로 다시 짬, 2026-09-06)
    | 어디에 | 무엇이 |
    |---|---|
    | 헤더 | **보유 골드** (아이콘 + 숫자) — 늘 같은 자리 |
    | 버튼 **안** | **낼 값** (아이콘 + 숫자) — 누를 것과 한 몸 |
    | 버튼 위 | 단계 핍 `● ● ○ ○ ○` + `고급 › 희귀` |
    | 스탯 행 | 체력·공격력만 `99 › 109` 로 다음 값을 함께 |
    ⚠ 지갑과 낼 값을 나란히 붙이지 말 것 — 같은 크기의 두 숫자가 붙으면 어느 쪽이
    낼 돈인지 매번 다시 읽어야 한다 (옛 `"900 (보유 12,400)"` 한 줄이 그랬다)
    ⚠ 누르면 `UIJuice.GradeUp` 이 터진다 — 자리는 **초상화 테두리**다
    (이 창에서 등급색이 실제로 바뀌는 물건이고 다시 만들어지지 않아 좌표를 믿을 수 있다)
  - **권속 소환은 패시브 목록의 한 줄이다** (사용자 지시, 2026-09-15) — `MonsterDetailPopup.AddBroodRow`.
    실제로는 스킬(`SummonBrood`)이지만 고유 스킬 칸이 희귀 스킬로 빠듯해 선천 패시브 바로 뒤에 선다.
    그림 = 권속 종족 초상화 · 마릿수는 줄 오른쪽 위 배지(UI 규칙 7) · 쿨다운은 `MonsterSpeciesData.BroodBaseCooldown`(전투와 같은 값)
  - **뿌리마다 1차가 둘 이상이다** (사용자 지시, 2026-09-15) — 하나뿐이던 다섯에 갈래를 더했다:
    구울(좀비·굶주림) · 핏빛 늑대(늑대·급소 찌르기) · 오크 도살자(오크·처형) · 철갑 멧돼지(멧돼지·성벽) · 늪 트롤(트롤·맹독 피부).
    각 2차의 `AlsoUpgradeOf` 에 붙였다. ⚠ 시너지 표식은 **비어 있다** — 시너지 종류를 늘리는 결정 뒤에 채운다.
    비인간형 셋의 시트는 `MonsterLibraryCreator` 의 `_Blood`·`_Iron`·`_Swamp` 변형이다
  - ⚠ **2차 업그레이드는 한 마리 고정이다** (사용자 지시, 2026-09-15) — `VerifyTierTwo` 가 `Count != 1` 을 에러로 잡는다.
    옛 두 마리 몫을 체력·공격력에 역할대로 나눠 담았다(벽은 체력, 딜러는 공격)
  - ⚠ **종족 소개는 글에 맞춰 칸이 늘고 아래를 민다** (사용자 지적, 2026-09-15) — 두 줄 고정이라 세 줄로 접힌 소개가
    첫 패시브 칸을 덮었다. `FitDescription` 이 네 줄까지 늘리고(넘으면 글 축소), 늘어난 만큼 줄 칸이 스킬 상자를
    최소 높이 밑으로 밀면 아이콘 모드로 넘긴다 (`RowsFit`)
  - ⚠ **패시브가 줄 칸(6)보다 많으면 아이콘 모드**다 (사용자 지시, 2026-09-10) —
    줄을 끄고 격자(`_passiveGrid`, 72px · 18칸)에 그림만, 설명은 올리거나 누르면 툴팁(`InfoIconUI`).
    사용자는 8 을 원했지만 8줄 = 752px 로 칸(727)을 넘어 **스킬 칸이 없어도** 깨진다 — 그래서 6.
    경계의 정본은 Creator 의 `PassiveSlots`, 런타임은 `_passiveRoots.Length` 로 읽는다
  - **패시브 칸은 축이 둘이다** (사용자 지적, 2026-09-09) — 종족 패시브(`SpeciesPassive`,
    계보 + 융합)와 **카드 레벨이 여는 패시브**(`PassiveSkillType`, 종족마다 하나)를
    한 목록에 이어 그린다. 슬라임 Lv4 의 `DefenseShield` 가 카드 3택 한 줄로 스쳐 갈 뿐
    어느 화면에도 안 남아 있었다. 아직 못 연 칸은 흐리게 + `Lv4` 표시(도감 모드는 전부 켜짐)
    ⚠ 칸이 6개다(계보 2 + 레벨 1 + 융합 3) — 고정 자리로는 세로가 88px 모자라
    **고유 스킬 칸을 런타임이 옮긴다**(`MonsterDetailPopup.LayoutSkillSection`).
    Creator 가 잡는 자리·높이가 그 계산의 기준값이다
  - **고유 액티브 스킬**을 적는다 — 이름·설명·쿨다운은 `ActiveSkillDatabase` 의 SO 가 정본,
    아이콘은 `ActiveSkillIdExtensions.IconKey` → `SpriteManager`.
    ⚠ 자리는 오른쪽 칸 **맨 아래**다 (사용자 지적, 2026-09-07) — **없는 종족이 더 많다.**
    위에 두면 대부분의 종족에서 251px 빈 자리가 칸 머리에 남고 종족 패시브가 밀린다.
    늘 있는 것이 위, 가끔 있는 것이 아래다
    ⚠ 쿨다운은 SO 원본이다 — 실제 값은 술법 시너지가 깎는다 (`MonsterRuntimeBridge.BuildSkillSlot`)
  - ⚠ **`HeroDetailPopup` 과 겸하지 않는다** (사용자 확정, 2026-09-06) —
    그쪽은 **인게임에서 적(용사) 정보**를 보는 화면으로 남는다.
    장비 3칸·용병 수·지휘력·레벨/EXP·해고가 몬스터에 없어 겸하면 빈 칸이 절반이다
  - ⚠ 스탯 행 순서의 정본은 `MonsterDetailPopup.StatRows` — Creator 가 이 배열로
    이름표를 굽고 런타임이 같은 순서로 값을 넣는다
  - ⚠ 표시값은 **종족 기본값 × 품질**이다. 소환력·카드 레벨·시너지는 안 곱한다 —
    소환사·덱마다 달라져 종족끼리 비교가 안 된다. 품질만 곱하는 이유는
    개선 버튼을 눌렀을 때 숫자가 움직여야 하기 때문
  - 등급이 오르면 `OnUpgraded` 콜백으로 도감이 격자를 다시 그린다
    (팝업이 도감을 직접 부르지 않는다 — 다른 화면에서도 열 수 있어야 한다)
  - 목록 조립은 `InGame/Codex/CodexCatalog.cs` 의 `CodexCategory.Monster` 분기
  - ⚠ **몬스터 칸 순서는 고정이다** (사용자 지시, 2026-09-15) — 기본 → 1차 → 2차(`TierOf`), 같은 단계는 `CardCatalog` 순.
    이름(번역마다 바뀐다)·품질(개선마다 자리가 옮겨진다)·해금으로 정렬하지 말 것
  - ⚠ `CodexCategory` 는 **뒤에만** 추가한다 — 탭 버튼 배열이 enum 순서를 그대로 쓴다
  - ⚠ 미해금 종족에 `GetGrade` 를 부르지 말 것 (예외가 난다). `IsUnlocked` 가 먼저다

### 몬스터 장비 (MonsterGear)
> ⚠ **용사 장비(`EquipmentData`)와 다른 물건이다.** 이름이 `Gear` 인 이유가 그것 —
> 검색이 갈린다. 강화석·분해·병사 트리거가 없고, 대신
> **몸 형태 제한**과 **외형 변경**이 있다. 레벨업은 **같은 장비 + 영구 골드**다
> (아래 '몬스터 장비 레벨업·강화').
- **정의**: `InGame/MonsterGear/MonsterGearData.cs` (SO) + `MonsterGearDatabase`(Resources)
  - `MonsterGearBody` = 인간형 / 비인간형 · `MonsterGearPart` = 갑옷·투구·방패·망토·등짐 / 가죽·덩치·장식
  - ⚠ **부위는 겹쳐 낄 수 있다** (사용자 확정, 2026-09-15) — 투구 둘도 함께 끼고 능력치는 전부 받는다.
    겉모습은 그 부위의 **앞 칸 장비**가 정한다 (`MonsterGearRule.BuildVisual` 의 `First`). 옛 "부위당 하나" 규칙은 폐기
- **규칙 정본**: `InGame/MonsterGear/MonsterGearRule.cs`
  | 무엇 | 값 |
  |---|---|
  | 칸 수 | **몬스터 품질**이 정한다 (사용자 확정) — 일반·희귀↓=1 · 희귀/유일=2 · 영웅=3. ⚠ 종족 패시브 슬롯과 **같은 표** |
  | 스탯 | **절대값** (사용자 확정). ⚠ `MonsterStatComposer` **⑦ 맨 마지막** — 앞에 두면 레벨·시너지가 장비 몫까지 곱한다 |
  | 불빛 | `AuraMinGrade`(희귀) 이상 장비 1개당 빛 하나 |
- **저장**: `Data/Sections/MonsterGearInventory.cs` (SaveKey 26) — **영구, 환생 무관**
  - Id → 보유 수 + 종족별 장착 목록. 개체 구분이 없다(강화가 없어 구분할 것이 없다)
  - ⚠ `UserDataManager.Reincarnate` 초기화 목록에 **넣지 말 것**
- **얻는 곳은 하나뿐**: 런 종료 보상 상자 (`MonsterGearRewardRule` → `RunBootstrap.GrantGearBox`)
  - **등급은 도달 스테이지가 확정**(5/10/15/20), **무엇이 나올지만 무작위**.
    ⚠ 확률로 한 단계 튀게 하지 말 것 — 품질 개선이 "확정이다"로 정해져 있다
  - 몸 형태는 **해금한 종족의 인간형/비인간형 비율**대로 굴린다 (쓸 수 없는 보상 방지)
  - ⚠ **뽑기·지급이 먼저, 연출이 나중.** `GearBoxPopup` 은 이미 받은 것을 흔들어 보여 줄 뿐이다 —
    '안 연 상자' 를 세이브에 두면 연출 중 앱이 꺼졌을 때 보상이 사라지는 길이 생긴다
  - ⚠ **후보가 하나뿐이면 그 칸의 보상은 무작위가 아니다** (사용자 지적, 2026-09-09)
    고급 비인간형이 `잿빛 가죽` 하나뿐이라 **세 번 연속 같은 것**이 나왔다. 규칙이 아니라
    **표에 물건이 없던 것**이 원인이다 — 비인간형 4종을 채워 등급마다 3후보로 맞췄고,
    `MonsterGearCreator.VerifyPoolSize` 가 굽는 순간 등급×몸형태별 후보 수를 세어 경고한다
  - 고르는 순서도 규칙이다 (`MonsterGearRewardRule.Pick`) —
    ① **아직 없는 것** 우선 → ② 전부 가졌으면 **직전에 준 것만 빼고** 무작위.
    ⚠ `_lastGiven` 은 세이브에 넣지 않는다 (앱을 껐다 켜면 잊어도 되는 값이다)
  - **연출 세기의 정본은 `GearBoxPopup.LookOf` 표 하나**다 (흔들림·빛살·불꽃·섬광 다섯 값).
    ⚠ 한 값이 아니라 **한 행**을 옮길 것 — 섬광만 올리면 번쩍이기만 하고 무게는 그대로다.
    일반 = 빛살 없음 · 영웅 = 불꽃 24 + 화면 섬광
    - 랜덤성은 **연출에만** 준다 (위상 · 길이 ±8% · 뚜껑 회전 방향 · 불꽃 각도/속도/크기).
      ⚠ 무엇이 나올지는 굴리지 않는다 — 연출이 결과를 정하는 것처럼 보이면 다시 열고 싶어진다
    - ⚠ 창 안의 자리는 `GearBoxPopupCreator` 의 **무대(Stage)** 가 정한다. 상자·장비 블록이
      각자 제 높이를 재서 남는 세로를 위아래로 나눈다 — 예전엔 둘 다 무대 맨 위에 붙어
      상자가 제목에 매달려 보였고 장비 설명은 확인 버튼과 **49px 겹쳐** 있었다.
      `Verify` 가 블록이 무대를 넘치면 굽는 순간 에러를 낸다
    - ⚠ 빛살 길이는 무대를 넘기지 않는다 — 자르는 마스크가 없어 제목 위를 가로지른다
- ⚠ **초상화도 장비를 입는다** (사용자 지적, 2026-09-11) — `MonsterPortraitProvider.Get` 이
  `BuildVisual` 을 불러 인간형은 `UnitAppearanceBridge.WithGear`, 비인간형은 가죽 색조를 얹고
  **캐시 열쇠 = 종족 ID + 장비 Key** 다. 한때 종족 ID 하나로 캐시해 도감·상세·카드가 늘 맨몸이었다
- **외형**: `MonsterGearVisual`(값 구조체) → `MonsterAppearanceBridge.Apply(species, name, gear)`
  - 인간형 → `UnitAppearanceBridge.ApplyEnemy(race, name, gear)` 가 롤 결과 **사본**에 덮어쓴다.
    ⚠ 롤 결과를 직접 고치지 말 것 — `EnemyAppearanceRoller` 가 캐시해 돌려주므로
    같은 이름의 맨몸 개체까지 갑옷을 입는다
  - ⚠ **`MonsterGearVisual.Key` 가 외형 캐시 키에 들어간다.** 없으면 종족·이름이 같아
    "장비를 바꿔도 겉모습이 그대로" 가 된다
  - 비인간형 → **도는 점 하나뿐** (사용자 지시, 2026-09-12) — 장비 하나당 점 하나(등급색), 등급 무관.
    예전의 가죽 색조 · 덩치 크기는 걷었다 (`MonsterGearRule.BuildVisual`) — 색조는 시트 색에 곱해져
    그림만 탁해졌고, 셋을 겹쳐 끼면 뒤섞여 이상해 보였다. `Tint`·`ScaleBonus` 필드는 흰색·0 으로 남는다
  - ⚠ 풀 재사용 — 색조는 **반드시 흰색으로 되돌리는 경로**가 있어야 한다
- **불빛**: `InGame/Appearance/MonsterGearAuraView.cs` — 몸 주위를 **도는** 빛
  - 버프 빛기둥(`UnitBuffAuraView`)은 발밑에서 **솟는다** — 움직임이 달라야 둘이 갈린다
  - 공유 스프라이트 + 정점 색만. `sortingOrder` 108 (무기 105 위, 버프 110 아래)
  - `OnEnable` 에서 반드시 끈다 (풀 재사용)
- **UI**: `MonsterDetailPopup` 가운데 칸 아래 3칸 + 칸을 누르면 겹쳐 뜨는 목록
  - ⚠ 전용 팝업을 만들지 않았다 — `PopupType` 이 늘고 팝업 위에 팝업이 겹친다
  - 목록 칸은 **런타임에 템플릿을 복제**한다 (보유 수가 계속 는다)
  - 상세의 스탯 행이 장비 몫을 함께 보여 준다 — **배율 밖 가산**, 전투와 같은 순서
- **데이터 생성**: `InGame/MonsterGear/Editor/MonsterGearCreator.cs` → `데이터 생성 > 몬스터 장비`
  - ⚠ `db.Entries.Clear()` 로 시작한다 — 이 파일이 DB 의 정본이다
  - ⚠ 개별 장비에 숫자를 적지 않는다. 등급별 예산(`BudgetOf`) × 성향(`Focus`) 이 정한다.
    절대값이라 눈대중으로 적으면 약한 종족 전용 장비가 된다
  - `Verify()` 가 벤더 에셋에 없는 외형 이름·부위/몸 형태 불일치를 잡는다

### 몬스터 장비 레벨업·강화 (2026-09-10, 사용자 확정)
> 중복 장비가 쌓이기만 하던 문제의 답이다 — **여분 사본이 곧 레벨업 재료다.**

- **규칙 정본**: `InGame/MonsterGear/MonsterGearLevelRule.cs` — 재료 수·골드·판정·차감이 전부 여기
  | 단계 | 무엇이 드나 | 무엇이 오르나 |
  |---|---|---|
  | Lv1→2→3→4→5 | 같은 장비 **1·2·2·3개** + 영구 골드 (등급 단위 × 1·2·3·5) | 레벨마다 `MonsterGearData.Levels` 한 칸 |
  | 강화 +1, +2, … (Lv5 뒤) | 같은 장비 **1개** + 영구 골드 (한 번마다 +25%) | 몬스터 공격력·체력 **+1%** 씩 — ⚠ **상한 없음** (사용자 확정) |
  - 골드 단위: 일반 150 · 고급 400 · 희귀 900 · 유일 1,800 · 영웅 3,500 (Lv5 까지 × 11)
  - ⚠ **확정이다. 확률이 아니다** · ⚠ **런 중에는 잠긴다** (`CodexEditLock` — 영구 골드를 쓴다)
  - Lv4·Lv5 는 **패시브가 열리는 자리**다 — `MonsterGearData.Lv4Passive/Lv5Passive`
    (배정 정본 `MonsterGearCreator.PassivesOf`). 아래 '패시브 각성' 참고
  - ⚠ **패시브는 Lv4·Lv5 중 한 칸뿐** · **카드 비용 −1 장비(술법·왕권)는 0칸** (사용자 확정, 2026-09-11)
    — 두 칸이던 때 심연의 껍질(생명흡수 + 역병 폭발 + 비용 −1)이 다른 영웅 장비를 전부 앞질렀다.
    일반·고급은 Lv4, 희귀부터 Lv5. `Verify` 가 칸 수를 센다(`GivesManaCut`)
  - 그 재배치로 **어느 장비에도 없게 된 패시브 22종**은 2차 장비 22개가 하나씩 이어받았다
    (2026-09-11, 총 52종). 망토·등짐 부위가 여기서 처음 쓰인다.
    ⚠ 새 장비를 넣을 때 술법·왕권 컨셉을 쓰면 패시브를 주지 말 것 (Verify 가 막는다)
  - 장비 상세 레벨 줄의 패시브는 **[그림][이름] 칩**이다 — 올리거나 누르면 설명
    (`SpeciesPassiveChipUI`). 그림 배열은 Creator 가 `SpeciesPassiveRule.All` 순서로 박는다
- ⚠ **장비 하나는 한 마리만 낀다** (사용자 확정, 2026-09-10) — 같은 장비를 또 얻으면
  **개수만 오르고 늘어난 수는 전부 재료다** (`SpareCount` = 보유 − 1, 본체는 안 먹는다).
  다른 종족이 낀 장비를 고르면 **벗겨 옮긴다** (버튼 글자가 "옮겨 장착" 으로 바뀐다).
  몬스터 상세 목록에서는 `EquipAt(species, gear, slot)` — **칸에 무엇이 있든 벗기고 끼운다** (사용자 지시, 2026-09-12)
  옛 세이브에서 여러 종족이 같은 장비를 끼고 있었으면 먼저 읽힌 쪽만 남긴다 (`Deserialize`)
- **능력치 어휘**: `InGame/MonsterGear/MonsterGearOption.cs` — `GearStat` 14종 (`StatType` 이 아니다)
  - 카드 비용(`ManaCost`)·넉백(`Knockback`)은 스탯이 아니라 규칙이 읽는다 —
    `MonsterGearRule.ManaCutFor` → `SummonerPerkRuntime.ManaCostFor` / `KnockbackBonusFor` → `MonsterSpawner.ApplyKnockbackPower`
  - ⚠ **비율이 먼저, 절대값이 나중** (`MonsterGearRule.ApplyStats`) — 비율은 레이어로 **더한다**(`Set(Get×배율)` 금지)
  - ⚠ 유물 '맞춤 제작'은 **절대값(체력·공격력·방어율)에만** 곱한다
  - ⚠ `GearOptionText.Describe` 는 **카드 비용을 빈 글자로** 돌려준다 — 마나는 아이콘이다 (UI 규칙 7).
    화면이 줄마다 [마나][-1] 배지를 따로 세운다
  - `Quirk` 플래그 = **특이 옵션** (분홍 글자). 혼자서는 쓸모가 적고 다른 옵션·종족과 겹칠 때 값을 한다
- **데이터**: `MonsterGearCreator` — Lv1 = 등급 예산 × `Focus`, Lv2~5 = **컨셉(`Theme`) 11종**의 모양 × 등급 값(`StepValue`)
  - 컨셉은 **두 축을 번갈아** 두껍게 한다 (급소: 치명타 → 공격력 → 치명타 → 치명타 피해+공격력).
    Lv5 는 재료가 셋이라 두 줄이다
  - ⚠ 카드 비용 −1 은 **술법 Lv5 · 왕권 Lv3** 둘뿐이다
  - ⚠ 옛 필드 `StatEntries` 는 **지웠다** — `데이터 생성 > 몬스터 장비` 를 다시 굽기 전에는 장비가 능력치를 안 준다
- **화면**: `UI/Popup/GearDetailPopup.cs` (+ `Editor/GearDetailPopupCreator.cs` → `프리팹 생성 > 팝업 > 장비 상세`, `PopupType.GearDetail`=30)
  - 여는 곳 둘: 도감 장비 칸 · 몬스터 상세 장비 목록 칸(→ **[장착]** 버튼이 함께)
  - ⚠ 목록 칸을 눌러도 **바로 끼우지 않는다** — 상세가 먼저 뜬다
  - 목록 (2026-09-12, 사용자 지시): 칸은 **그림 + 이름뿐** · 이 몬스터가 낀 것이 맨 앞 "장착 중" ·
    다른 몬스터가 낀 것은 오른쪽 위에 그 몬스터 초상화. [상세]·[벗기기] 버튼은 없앴다
  - 상세 머리에 **착용 몬스터 초상화** — 장착을 누르면 창을 닫지 않고 그 자리에서 바뀐다 (`RefreshWearer`)
  - ⚠ **창은 1800×1000 이다** (사용자 지적, 2026-09-11 "글이 너무 작다") — 1320 일 때 레벨 줄
    글 칸이 290px 뿐이라 자동 축소가 22px 까지 내려갔다. 지금은 레벨 줄 `FontMd`(바닥 `FontSm`),
    칸 높이 `RowLg`. `Verify` 가 레벨 줄 글 칸이 17자를 담는지 잰다.
    ⚠ 자동 축소의 바닥(`fontSizeMin`)을 `FontSm` 아래로 두지 말 것 — 넘치면 조용히 안 읽히게 된다
  - 재료는 버튼 안에 **[장비 그림][×N]** (모자라면 붉다, 2026-09-12). 안내 줄은 런 중 잠김만 말한다
  - 이 몬스터가 이미 낀 장비면 장착 버튼이 **[벗기기]** 가 된다
  - 몬스터 상세의 스탯 행은 `WithGear` 로 장비(레벨·비율 포함)를 얹는다. 마나 배지는 장비 할인을 빼고 **초록**으로
- ⚠ 굽는 순서: `데이터 생성 > 몬스터 장비` → `팝업 > 장비 상세` · `팝업 > 몬스터 상세` → `PopupManager [Load Popup Prefabs]`

### 패시브 각성 · 장비 패시브 (2026-09-10, 사용자 확정)
- **정본**: `InGame/Summon/PassiveAwakening.cs` — `PassiveResolver`(최종 목록) · `PassiveAwakening.Table`(19종) · `GearTierPassive`(단계 값)
- **출처 셋 → 한 목록**: 선천(계보) · 융합(카드) · 장비(Lv4·Lv5, 열린 칸만)
  - ⚠ **스폰(`MonsterSpawner`) · 사망(`MonsterDeathWatcher`) · 화면(`MonsterDetailPopup`) 셋이 전부 `PassiveResolver` 를 부른다.**
    패시브를 모으는 코드를 새로 짜지 말 것 — 한때 사망만 융합을 빠뜨려 사망 발동 패시브가 통째로 무효였다
  - 같은 패시브 둘 이상 → 각성판 하나 (원래 것은 목록에서 **빠진다**). 셋이어도 한 번
  - 각성표에 없는 것(특이 12종)은 겹치면 하나만. 단계 패시브는 **I 과 III 이 다른 패시브**(둘 다 적용), I + I 은 하나
  - ⚠ **결과를 저장하지 않는다** — 매번 출처에서 다시 계산한다. 그래서 장비를 벗기면 각성이 저절로 풀린다.
    필드의 몬스터는 스폰 때 받은 목록을 들고 있고, 런 중 장비 변경은 `CodexEditLock` 이 막는다
  - ⚠ 각성판은 **원래 것의 상위 호환**이어야 한다 — `SpeciesPassiveRule` 에 원래 값과 나란히 적어 두었다
- **enum**: `SpeciesPassive` 에 얹었다 — 특이 40~51 · 단계 60~83 · 각성 100~118. ⚠ 뒤에만 추가 (세이브·SO 가 정수)
  - `SpeciesPassiveRule.All` 뒤에 붙였다 → **`아이콘·텍스처 > 종족 패시브 아이콘` 을 다시 구워야** 새 아이콘이 생긴다
  - 단계 패시브는 스탯이라 `MonsterGearRule.ApplyStats` 가 적용한다(도감 숫자·쿨감이 함께 움직인다). `SpeciesPassiveRuntime` 은 무시한다
- **특이 패시브 훅** — 값은 스폰 때 `MonsterTag.cs` 의 컴포넌트로 굽는다 (Burst 잡이 규칙을 못 읽는다)
  | 패시브 | 어디서 도나 |
  |---|---|
  | 반동 · 성벽 | `UnitHitSystem` (`RecoilComponent` · `DamageTakenMultComponent`) |
  | 급소 찌르기 · 처형 | `UnitAttackSystem.RollDamage/IsExecute/PierceFor` — 근접·원거리 **같은 함수** |
  | 허세 | `GearPassiveConditionSystem` (상태효과 시스템 뒤 · 피격 시스템 앞) |
  | 광합성 | `ActiveSkillExecuteSystem` (스킬 실행 직후) |
  | 복수 | `SpeciesPassiveRuntime.NotifyAllyDied` ← `MonsterDeathWatcher` (모든 몬스터 사망) |
  | 외톨이 · 무게추 · 굶주림 | 스폰 때 상태효과·태그 |
  | 터지는 몸 · 잔불 | `SpeciesPassiveRuntime.OnDeath` |
  - ⚠ 새 컴포넌트를 만들면 `MonsterRuntimeBridge.ClearSpeciesPassiveResidue` 에 떼는 줄을 넣을 것 (풀 재사용)
- ⚠ **Multiply 상태효과의 Delta 는 배율이다 (1.25 = +25%)** — 2026-09-10 에 버그 네 개를 고쳤다:
  튼튼함(0.10 → 체력 ×0.1) · 신속(×0.25) · 최후의 함성(아군 공격력 ×0.25) · 냉기(−0.35 → 사실상 정지) ·
  시너지 중첩 돌격(이동 ×0.5). 비율을 넘길 때는 `SpeciesPassiveRuntime.AddMult` 를 쓸 것
- 피격 반응은 이제 **합친다** (`MergeRetaliate`) — 반사는 큰 쪽, 상태효과는 새것. 가시 + 냉기가 서로를 지우지 않는다
- 몬스터의 `KnockbackImmuneTag` 도 이제 풀 재사용 때 뗀다 (예전엔 야수 금으로 한 번 붙으면 영영 남았다)
- **화면**: 몬스터 상세 패시브 목록 — 각성판이 **맨 위**, 이름 옆에 `(분열 + 분열)`, 칸이 반짝인다(`PassiveShineUI`).
  융합은 `(융합)`, 장비는 `(장비)`. 장비 상세의 Lv4·Lv5 줄에 패시브 이름이 뜬다
- ⚠ 굽는 순서: `종족 패시브 아이콘` → `데이터 생성 > 몬스터 장비` → `팝업 > 몬스터 상세`·`장비 상세` → `[Load Popup Prefabs]`

### 도감 (Codex)
> ⚠ **수집 버프는 없다** (사용자 확정, 2026-09-06) — "몇 종 모았나" 로 공/체가 오르지 않는다.
> 그 버프가 걸리던 대상(`General`)은 이 게임에서 **적(용사)** 이라, 도감을 채울수록
> 적이 세지고 있었다. 함께 있던 장치도 전부 걷어냈다 —
> `CodexApplier`(파일 삭제) · `LockForRun`/`AppliedCount` · **"이번 여정 수확" 화면**
> (`CodexRunGains`·`MainPanelUI.ShowCodexGainsIfAny`). 도감의 값어치는 **품질 개선** 하나다.
- ⚠ **특성 탭은 런 특성(`RunPerk`) 30종이다** (사용자 지적, 2026-09-11) — 한때 원작 `TraitDatabase`
  (적 직업 시너지)를 읽어 영영 비어 있었다. 기록은 `CodexData.RecordPerk` ← `RunPerkData.Add`,
  지금 런에 쥔 특성도 본 것으로 친다. 이름·설명·아이콘은 `RunPerk` 가 정본
- **탭은 셋이다** — `CodexCategory` = `Monster`(0, 기본) · `Trait`(1) · `Gear`(2).
  어빌리티·장수 탭은 제거했다 (플레이어의 장수가 없고, 어빌리티 축은 폐기).
  `CodexData` 도 런 특성 기록 하나만 남겼다 (2026-09-11)
  - ⚠ **장비 탭은 몬스터 장비(`MonsterGearData`)다** — 원작 용사 장비가 아니다.
    보유 판정은 `MonsterGearInventory.OwnedCount`(장착 중인 것도 가진 것이다).
    여기서 끼우지 않는다 — 장착은 `MonsterDetailPopup` 의 칸이 맡는다.
    칸을 누르면 **장비 상세(레벨업)** 가 열린다 (2026-09-10 — 예전엔 툴팁이었다)
  - ⚠ `CodexCategory` 는 **뒤에만** 추가한다 (탭 버튼 배열이 enum 순서를 쓴다).
    분류를 늘렸으면 `CodexPopupCreator` 를 **다시 구워야** 탭이 생긴다
- **세이브**: `Data/Sections/CodexData.cs` — 순수한 기록이다
- **목록 조립**: `InGame/Codex/CodexCatalog.cs` (`Build(category)`)
- **UI**: `UI/Popup/CodexPopup.cs` (+ `Editor/CodexPopupCreator.cs`)
  - ⚠ **초상화는 정사각형 캔버스 + 발밑 바닥 정렬이다** (사용자 지적, 2026-09-15) — `MonsterPortraitProvider.CropAndTrim`.
    가운데 정렬이던 때는 납작한 종족(슬라임)이 칸 가운데에 떠 도감의 발밑 높이가 제각각이었다. 칸도 정사각형 + preserveAspect 로 둘 것
  - **칸은 초상화다** — `MonsterPortraitProvider.Get` 을 직접 부른다. 계보 아이콘
    (`LineageIcon`)을 쓰면 슬라임 계열 넷이 전부 같은 그림이 된다
  - 칸 하나가 [초상화 140] [이름] [품질] [마나·마릿수 배지] [시너지 표식 3] 을 함께 그린다.
    ⚠ 셀 자식 경로가 계약이다 (`CodexPopup` 의 `*Path` 상수) — Creator 와 반드시 일치
  - ⚠ 격자 폭은 7열이 딱 맞게 잡혀 있다 (`CellW` 248 · 간격 16). 셀 폭을 손대면 다시 계산할 것
  - 헤더 오른쪽은 **보유 영구 골드**다 (품질 개선이 여기서 값을 치른다)

### 유물 트리 (RelicTree) — 2026-09-07 전면 교체
> 원작 트리(69노드)는 **장수·병사 축**이라 이 게임에서 40노드가 "적을 강화하는 유물"이었다.
> 네 갈래를 이 게임의 자원 축으로 갈아 끼웠다 — **소환수 / 마왕성 / 마나 / 통솔**, 46노드.

- **정본**: `Relic/Tree/RelicTreeCatalog.cs` (노드 표) · `RelicTreeTypes.cs` (ID·계열) ·
  `RelicEnums.cs` (시스템 효과) · `RelicTreeApplier.cs` (적용)
- **몬스터 스탯 노드는 `RelicTarget.Unit_Monster` 다** (`RelicTreeTypes.cs` — 옛 `AbilityTarget` 에서 둘만 남겼다)
  - ⚠ `All` 로 두면 같은 노드가 장수 경로(`ApplyToGeneralStat`)에도 걸려 **적까지 강화된다**.
    `MatchesGeneralTarget` 의 `default: false` 가 걸러 주는 것이 이 값을 만든 이유다
  - 적용은 `MonsterStatComposer` **⑥-b** 한 곳뿐 (특성 뒤 · 장비 앞)
- ⚠ **시스템 효과를 만들면 읽는 쪽도 같이 만들 것** — 실패가 조용하다.
  실제로 `ApplyEnemyWeaken` 은 **부르는 곳이 없어 적 약화 6노드가 통째로 무효**였다
  (`GeneralRuntimeBridge.Initialize` 에 연결). 26종 전부 소비처가 있는지 확인 완료
- 훅 지점 (전부 그 규칙의 **단일 관문**에 붙였다):
  | 효과 | 훅 |
  |---|---|
  | 마나 그릇·비용·마릿수·배출 간격 | `RunPerkRule` (특성과 같은 자리) |
  | 마나 회복 | `ManaRegenRule.RawFor` |
  | 과부하 저항 | `SummonerVigorRule.OverloadStepFor` (패기와 같은 축) |
  | 평타 비율 | `SummonerStrikeRule.Build` — ⚠ **%p 가산**이다. 곱하면 잡병만 오른다 |
  | 시너지 문턱·중첩 | `MonsterSynergyRule.StepsOf` / `StackBonus` — ⚠ 문턱은 **동만** |
  | 마왕성 체력 | `SummonerData.MaxCoreHp` |
  | 야영지 회복 | `RunNodeFlow.CampHeal` (표시·실제가 같은 값을 본다) |
  | 카드·특성 선택지 | `CardRewardPicker.CurrentChoiceCount` / `RunBootstrap.OfferPerk` |
  | 새 카드 레벨 | `SummonDeckData.Acquire` — ⚠ 특성 '속성'과 **더해서** 한 번에 잡는다 |
  | 종족 패시브·분열체·사망 효과 | `SpeciesPassiveRuntime` 의 `PassivePower`/`DerivedScale`/`DeathPower` |
  | 장비 상자·스탯 | `RunBootstrap.GrantGearBox` / `MonsterGearRule` |
  | 런 골드·환생 포인트 | `RunGoldRule.Grant` / `ReincarnationData.PreviewPoints` |
  | 용사 약화 | `RelicTreeApplier.ApplyEnemyWeaken` ← `GeneralRuntimeBridge` |
- ⚠ **효과를 만들면 `RelicNodeDef.SystemLine` 에 설명 줄도 함께 만들 것**
  (사용자 지적, 2026-09-07) — 빠지면 팝업에서 **효과가 빈칸으로 뜬다.** 에러가 안 난다.
  트리를 갈아 끼웠을 때 실제로 **46개 중 29개가 빈칸**이었다. `GetDescription` 으로는
  못 잰다 — 빈 글자도 색 태그로 감싸 돌려주므로 언제나 "비어 있지 않다".
  판정은 `RelicNodeDef.HasEffectText` 다
- ⚠ **노드 이름은 화면과 진단이 갈린다** (2026-09-16)
  - `RelicNodeDef.DisplayName` = 표에서 **`Relic.<Id>` 기호 키**로 찾은 이름. **화면은 전부 이쪽이다** —
    `RelicTreePopup` 의 카드·툴팁 제목·선행 줄, `RelicTreeApplier.NextNodeNameFor`(잠긴 기능 안내)
  - `RelicNodeDef.Name` = **한국어 원문 그대로**. `Verify()` 의 예외와 `RelicTreeAudit` 의 목록이 쓴다 —
    거기까지 기호 키로 바꾸면 에러가 `'Relic.N_Claw' 의 Target 이…` 로 읽혀 진단이 쓸모없어진다
  - ⚠ **왜 기호 키인가** — 노드 이름이 `절약`·`인내`·`질주`·`안목` 처럼 **짧고 흔한 낱말**이다.
    한국어 키로 넣으면 `LocalizeText` 의 부분 치환이 **다른 문구 속 같은 글자까지 조용히 바꾼다**
  - ⚠ 노드를 추가하면 표에 `Relic.<Id>` 줄도 넣을 것 — 없으면 화면에 키가 그대로 뜨고 **에러는 안 난다**
- ⚠ **`RelicTreeCatalog.Ensure()` 끝의 `Verify()` 가 둘을 검사해 터뜨린다**
  ① 스탯 노드가 `Unit_Monster` 가 아니면(= 적까지 강화되면) ② 설명 줄이 없으면.
  조용한 실패를 시끄러운 실패로 바꾼 것이니 끄지 말 것
- **원작 잔재를 전부 걷어냈다 (2026-09-07)** — 지운 것:
  `ApplyToGeneralStat` · `ApplyGeneralOnly` · `ApplyStatNodes` · `CollectSoldier` ·
  `ApplyLoneWolf` · `SoldierCutTotal` · `GetRandomTraitCount` · `GeneralLayerKey`,
  그리고 호출처(`HeroStatPipeline` 2곳 · `SoldierStatApplier` 1곳).
  ⚠ **적(용사)·병사 스탯 경로는 이제 없다** — 유물이 그쪽에 걸리는 길 자체를 없앴다.
  스탯 노드가 전부 `Unit_Monster` 라 그 셋은 구조적으로 아무것도 못 찾았고,
  남겨 두면 "적을 강화하는 유물" 로 가는 문만 열어 둔 셈이었다
- `RelicSystemEffect` 는 **읽는 곳이 하나도 없던 5·6·11·12 를 지웠다.**
  남은 잔재 1·2·3(어빌리티 화면) · 9(장수 슬롯)는 원작 화면이 아직 읽는다.
  4·7·8·10 은 새 트리도 쓴다 (런 골드 · 용사 체력/공격력 · 배속)
- **구 유물 아이콘을 지웠다** — `Icons/Relics/` 29장 + `Atlas_Relics.spriteatlas` +
  `SpriteManager._relicAtlas`. 그걸 참조하던 `RelicData` SO 가 이미 없어서
  **아무도 안 찾는 그림**이 빌드에 실려 있었다. 트리 고아 PNG 60장도 함께 삭제
- 문서: `Docs/RelicTree.md`(노드 표·훅 표) · `Docs/RelicTree_Icon_Spec.md`(46종 아이콘 명세).
  ⚠ 둘 다 카탈로그에서 뽑은 것이다 — 카탈로그를 고치면 함께 고칠 것
- ⚠ 좌표는 같은 x 면 dy ≥ 2, 같은 y 면 dx ≥ 2 (칸이 겹친다). 46노드 검산 완료
- **도구는 둘이다 — 그림 쪽과 표 쪽** (2026-09-07, 다섯 개를 이렇게 갈랐다)
  | 도구 | 하는 일 | 안 하는 일 |
  |---|---|---|
  | `아이콘·텍스처 > 유물 트리 아이콘` | 없는 노드의 자리표시 PNG 를 굽고 임포트 설정을 건다 | 파일을 지우지 않는다 |
  | `데이터 생성 > 유물 트리 점검` | 아이콘 누락 · 고아 · 좌표 겹침 · 부모 끊김을 본다. 고아는 물어본 뒤 삭제 | 그림을 굽지 않는다 |
  ⚠ **서로 겹치지 않게 갈랐다** — 그림 굽는 도구가 파일을 지우기까지 하면
    "아이콘 만들려고 눌렀다가 뭔가 지워졌다" 가 된다. 순서도 안 외워도 된다
  ⚠ 있는 파일은 절대 덮어쓰지 않는다 (진짜 그림이 날아간다). 전부 다시 깔려면 폴더를 지울 것
  ⚠ 파일명은 **enum ID** 에서 나온다 (`RelicIconKey.Of`: `N_PlagueLore` → `node_plague_lore`)
    — 화면에 뜨는 표시 이름은 자유롭게 바꿔도 되고, **enum 이름**을 바꾸면 다시 구울 것
  ⚠ 그 뒤 `데이터 생성 > SpriteManager + 아틀라스` 까지 해야 화면에 뜬다
### 유물 노드 재정비 (사용자 지시, 2026-09-10)
> 48노드 · 전부 만렙 **2,679pt** (30스테이지 런 한 번 ≈ 289pt → 약 9.3런)

- **⚠ 기존 노드를 대체하지 않는다. 뒤에 붙인다** (사용자 지적)
  계열의 끝을 갈아 끼우면 원래 있던 효과가 사라진다. 새 효과는 **새 노드**로 낸다.
  | 새 노드 | 효과 | 부모 | pt |
  |---|---|---|---|
  | `N_EndlessSwarm` 끝없는 무리 | **카드당 마릿수 +1** | 마왕의 군세 | **200** |
  | `N_LastDrop` 마지막 한 방울 | **모든 종족 소환 비용 −1** | 무한의 그릇 | **200** |
  | `N_PlagueLore` 역병의 지혜 | 중독·화상·역병 피해 +15%/lv (Lv4) | 치유의 기억 | 50 |
- **값은 티어가 아니라 세기가 정한다** — `Sys(..., cost: N)` 로 티어 밖에서 매긴다
  | 노드 | 이전 → 지금 |
  |---|---|
  | 끝없는 무리 · 마지막 한 방울 | (신규) **각 200pt** |
  | 조기 성장 (새 카드 Lv2) | 8 → **60** |
  | 찰나의 지배 (배속 2단계) | 8 → **40** |
  | 대장간의 기억 (런 종료 상자 +1) | 5 → **35** |
  - ⚠ 티어는 "트리에서 얼마나 깊은가" 지 "얼마나 센가" 가 아니다. 판을 통째로
    바꾸는 한두 노드만 예외로 두고, **예외가 흔해지면 티어 표가 거짓말이 된다**
  - ⚠ `시간의 고삐`(배속 1.5×, 3pt)는 싼 채로 둔다 (사용자 확정) — 판을 빨리
    넘기는 편의다. 그 위 단계만 체감 난이도를 바꾸므로 값이 붙는다
- **⚠ 같은 스탯의 강화 버전이 위아래로 서는 것은 정상이다** (사용자 확정)
  발톱(공 +4%)→이빨(공 +5%), 껍질(체 +5%)→살점(체 +10%) 처럼 기본 스탯은
  계열을 따라 두꺼워지는 것이 이 트리의 기본 모양이다. **중복이라고 지우지 말 것.**
- **바꾼 것**
  | 노드 | 왜 |
  |---|---|
  | `N_ReinforceSeal` 증원의 인장 **삭제** | 마릿수 +1(가장 센 축)이 8pt 였다. 효과는 `N_EndlessSwarm` 이 200pt 로 받는다. ⚠ 번호 404 재사용 금지 (옛 세이브) |
  | 조율 `SynergyStepCut` → `SynergyStackBonus` +1%p/lv | Lv3 = 문턱 −3 이라 `Max(1,…)` 에 걸려 **1마리로 동이 켜졌다**. 지금은 자식(공명의 서 +2%p)의 약한 판이다 |
  | 재건 `CampHealBonus` → `CoreRegenPerStage` | 야영지 회복이 8→24 가 되며 +3 은 곁다리가 됐고, **갈림길에서 야영지를 골라야만** 값을 했다 |
  | 코어 HP 4노드 ×2 | 마왕성 체력 3배(54~78)에 맞춰 +21 → **+42** |
- **통솔 계열의 뿌리는 '약탈의 손'이다** (사용자 지시, 2026-09-12) — 약탈의 손(t1) → 시간의 고삐 → 찰나의 지배 ·
  영혼의 항아리 · 부름의 나팔(t2) → 성문 개방·안목. 골드와 배속을 먼저 사게 했다
- **트리 전체를 다시 배치했다** (2026-09-12, 사용자 지시 "난잡하다") — 짝수 격자 · 갈래마다 제 구역:
  소환수 = 위(x −4~4) · 마왕성 = 아래 · 마나 = 왼쪽 y=0 한 줄 · 통솔 = 오른쪽 y=0/2/4 세 줄. 부모는 안 바꿨다(해금 순서 그대로)
- **절약 = Lv1 · −1 · 100pt** (2026-09-12) — −0.5 는 정수 원가의 반올림에 먹혀 홀수 레벨이 무효였다. 마지막 한 방울(−1·200pt)과 합쳐 −2
- **무리의 부름**(`N_SwarmCall`=109, 100pt) — 마릿수 +1 의 싼 판. 끝없는 무리(200pt)와 합쳐 +2. ⚠ 아이콘이 없다 — 유물 트리 아이콘 → 아틀라스
- **덱 칸 유물 '전열 확장'(`N_WarCamp`, `DeckSlotBonus`=61)을 다시 넣었다** (사용자 지시, 2026-09-11)
  - 한때 "확장 편성의 존재 이유를 뺏는다" 로 되돌렸지만, 소환사 칸이 4~6 으로 갈리면서 좁은 소환사에게
    영구 성장의 길이 필요해졌다. **최대 2레벨** · Lv1 60pt · Lv2 120pt · 조기 성장(8,-2) 뒤 (10,-2)
  - 읽는 곳은 `SummonerData.StartDeckSlots` 하나 (상한 8). 확장 편성은 칸이 이미 8이면 후보에서 빠진다(`RunPerkData.CanOffer`)
  - ⚠ 아이콘이 없다 — `아이콘·텍스처 > 유물 트리 아이콘` → `SpriteManager + 아틀라스`
- **'오래된 계약'(`N_OldPact`=415, `StartingPerkCount`=62)** (사용자 지시, 2026-09-12) —
  런을 시작할 때 **무작위 특성 +1/lv** · **최대 2레벨** · 부모 '전승의 대가'(같은 특성 축) · 좌표 (4,4)
  - 값은 티어 밖이다 — **70/140, 합 210pt**. 특성 한 장은 **런마다** 공짜로 얹히는 축이라
    티어 값(12pt)으로는 안 찍을 이유가 없는 노드가 된다. 전열 확장(60/120) 바로 위
  - ⚠ 주는 곳은 `RunBootstrap.GrantStartingPerks` — **`SpawnSummoner()` 뒤**여야 한다.
    `RunPerkData.CollectMissing` 이 소환사 개성과 겹치는 특성을 거르고(`ShadowedByPerk`),
    '친화 확장' 은 소환사가 없으면 대상을 못 고른다. `BuildStarterDeck`·`GrantForRun` 보다도
    뒤라 '확장 편성'(칸 +2)·'유리 성채'(성 −40%)처럼 얻는 순간 값을 건드리는 특성도 제자리를 찾는다
  - ⚠ **새 런에서만** 준다 (`if (!resume)`) — 이어하기에서 또 주면 앱 재시작이 특성 획득 수단이 된다
  - ⚠ `RunPerkPicker.Pick(data, count)` 로 **한 번에** 뽑는다 — 한 장씩 뽑으면 같은 특성이 두 번 나온다
  - ⚠ 대가를 치르는 특성(유리 성채·봉인된 칸 등)도 그대로 뽑힌다 — 무작위라는 것이 이 노드의 값이자 위험이다
  - ⚠ 아이콘이 없다 — `아이콘·텍스처 > 유물 트리 아이콘` → `SpriteManager + 아틀라스`

### 대가를 치르는 특성 9종 (사용자 지시, 2026-09-11) — `RunPerk` 32~40
> 전부 **무언가를 내주고** 받는다. 수치 정본은 `RunPerkRule`, 설명은 그 상수에서 뽑는다.

| 특성 | 효과 | 훅 |
|---|---|---|
| 피의 계약 32 | 마나가 모자라면 모자란 만큼 마왕성 체력으로 (1 은 남긴다) | `RunPerkRule.TryPayWithBlood` ← `SummonController.UseMonsterCard` · 카드 바 `SummonDeckUI` 가 같은 `CanPayWithBlood` |
| 뒤집힌 과부하 33 | 이번 판 과부하 1단계당 **해당 몬스터 전부** 공/체 +12% — 대기열에서 나오는 순간의 단계로 굳는다. 낸 개체를 따로 기억하지 않는다 (사용자 확정, 2026-09-16). ⚠ 과부하는 카드 ID 로 센다 — 슬라임과 슬라임 킹은 따로 쌓인다. 권속·분열체·시그니처 소환은 이 배율을 안 받는다 | `RunPerkRule.DrainStatMult` |
| 봉인된 칸 34 | 빈 칸 하나 삭제, 전 몬스터 공/체 +20% | `RunPerkData.Add` → `SummonDeckData.RemoveEmptySlot` · `MonsterStatComposer` ⑥ |
| 기다림의 미학 35 | 판이 열린 뒤 1초당 공/체 +4% (최대 +40%) | `DrainStatMult` (판 시작 시각 `RunPerkRule.NoteStageStart`) |
| 유리 성채 36 | 얻는 순간 마왕성 최대 −40%, 소환력 ×1.5 | `RunCoreData.ScaleMax` · `RunPerkRule.SummonPowerMult` |
| 저주받은 금화 37 | 런 골드 **+100%** (2026-09-16 에 +60% 에서 올림 — 사용자 지시), 판을 넘길 때마다 마왕성 −3 | `RunGoldRule.Grant` · `RunBootstrap.AdvanceStage`(`Pay` — 1 아래로 안 간다) |
| 쌍둥이 라인 38 | 옆 라인에도 절반(내림)이 선다, 비용 +2 | `SummonController.UseMonsterCard` · `RunPerkRule.TwinLaneOf` · `ManaCostDelta` |
| 한 우물 39 | **필드·대기열에 한 가지 몬스터(몬스터 ID 하나)만 있으면 그 몬스터의 표식 전부** 카운트 +2 — ⚠ 설명에 '종족' 을 쓰지 말 것(계보 전체로 읽힌다. 슬라임과 힐 슬라임은 둘이다) (사용자 지시, 2026-09-16 — 옛 규칙 "덱 전부가 공유하는 표식 하나" 는 진화로 표식이 바뀌어 거의 안 켜졌다) | `MonsterSynergyRule.Recount` 가 `_singleSpeciesTags` 기록 → `SingleWellTags` → `CountOf`. ⚠ **스킬 소환(권속·시그니처·분열체)은 판정에서 뺀다** — 카드 몬스터만 따로 세는 `_aliveCard`(`MonsterLineReturner` 의 returnable) + 대기열로 판정. 시너지 카운트도 같은 집계를 쓴다. 하단 카드 시너지 표식에 **금빛 테두리** (`SummonCardUI.SetWellOutline`) |
| 매복 40 | 판 시작 **3초** 배출 정지 (2026-09-16 에 5초에서 줄임), 라인마다 처음 5마리 공/체 +60% | `SummonController.DrainLane`(`AmbushHoldRemaining`) · `DrainStatMult` |

- ⚠ **배출 순간에 굳는 배율은 `RunPerkRule.DrainStatMult` 한 곳**이다 — `SummonController.SpawnOne` 이 계산해
  `MonsterSpawner.SpawnFromCard(drainMult)` → `MonsterStatComposer.Compose(drainMult)` ⑥ 으로 넘긴다. 카드 몬스터만 받는다
- ⚠ '봉인된 칸' 은 빈 칸이 없으면 후보에서 빠진다(`CanOffer`). 상점 재고를 본 뒤 카드를 사서 칸이 차면 칸은 못 줄이고 스탯만 받는다 — **카드는 절대 버리지 않는다**
- ⚠ **HUD 특성 줄의 `VerifyPerkBar` 가 에러를 낸다** — 특성이 39종이 되어 이론상 최대(개성 1 + 특성 39 + 제단 8 = 48)가
  칸(40)을 넘는다. 실제 런에서 닿기 어려운 값이라 HUD 는 그대로 뒀다. 넓히려면 `PerkRowsH`(세 줄) → 아래 HUD 전체가 밀린다
- 아이콘: `아이콘·텍스처 > 특성 아이콘` → `SpriteManager + 아틀라스`
- **새 효과의 소비처**
  | 효과 | 어디 |
  |---|---|
  | `DotDamageBonus` | 단일 관문 `MonsterDotRule.Scale` — 도트를 만드는 **넷**이 전부 지난다 |
  | `CoreRegenPerStage` | `RunBootstrap.AdvanceStage` (마나 회복 옆) |
  - ⚠ 도트는 **초당 피해만** 올린다. 지속 시간을 늘리면 스택이 함께 늘어 세기가
    제곱으로 붙는다 (`UnitStatusEffectSystem` 이 버프마다 따로 틱한다)
  - ⚠ 새 노드 셋은 아이콘이 없다 — `아이콘·텍스처 > 유물 트리 아이콘` →
    `SpriteManager + 아틀라스`. 지운 노드의 PNG 는 `데이터 생성 > 유물 트리 점검` 이 잡는다
- **분열의 유산(`DerivedScaleBonus`)은 종족을 가리지 않는다** —
  분열·재조립을 **융합으로 배운** 카드에도 그대로 걸린다. 적용 지점이
  `SpeciesPassiveRuntime.DerivedScale` 하나이고, 그 함수는 누가 낳았는지를 안 본다
- ⚠ `Verify()` 가 이제 **좌표 겹침도** 본다 — 겹쳐도 에러가 안 나고 노드 하나가
  화면에서 통째로 사라질 뿐이었다

- ⚠ **유물 '안목' 은 효과가 바뀌었다** (사용자 확정, 2026-09-10)
  옛 효과 `CardChoiceCount`(카드 선택지 +1)는 특성 '감식안' 과 **같은 축**인데
  `CardRewardPicker.MaxChoiceCount`(4)가 특성 몫만 계산에 넣어, 둘을 함께 들면
  `Min(4, 3+1+1)` = 4 로 **유물이 통째로 무효**였다 — 에러도 로그도 없다.
  지금은 `ShopPriceCut`(런 골드 값 −6%/단계)이다
  - ⚠ 값은 `RunGoldRule.Price` 에만 걸린다. `PriceUnit` 에 걸면 이벤트의 골드
    **보상**까지 깎여 아무 일도 안 한 것이 된다 — 둘을 한 함수로 합치지 말 것
  - ⚠ 영구 골드(품질 개선)에는 안 걸린다. 하한 0.4 가 있어 공짜가 되지 않는다
  - ⚠ `CardChoiceCount` 는 enum 에 남겨 뒀다(읽는 쪽도 그대로) — 값이 0 이라
    아무 일도 하지 않는다. 다시 쓰려면 `MaxChoiceCount` 와 카드 칸 수부터 늘릴 것
  - ⚠ 반면 **특성 선택지 유물('전승의 대가')은 제대로 쌓인다** —
    `RunBootstrap.OfferPerk` 는 `ChoicePopup.MaxRows`(8)로 자른다
- ⚠ `RelicSystemEffect` 1~12 는 원작 잔재다. 원작 화면이 아직 참조하므로 남겨 뒀다 —
  **새 노드에 쓰지 말 것** (4·7·8·10 만 이 게임에서 살아 있다)

### 환생 / 유물
- **포인트·공식**: `Data/Sections/ReincarnationData.cs` (획득 곡선 · 강화 비용)
- **초기화 범위**: `Data/Core/UserDataManager.cs` 의 `Reincarnate()` — 여기 목록이 정본
- **유물**: 위 '유물 트리' 항목 참고. ⚠ 구 카드 그리드(`RelicData`·`RelicDatabase`·
  `RelicInventoryData`·`RelicPopup`·`Data/Relics/` SO)는 **2026-08-25 에 이미 없어졌다** —
  옛 이름으로 찾지 말 것. 지금 있는 것은 `Relic/RelicEnums.cs` + `Relic/Tree/` 뿐이다
- **UI**: `UI/Popup/RelicTreePopup.cs`, 패배 화면은 `InGame/UI/ReincarnationPopup.cs`

### 난이도 해금 = 20스테이지 돌파 (사용자 지시, 2026-09-11)
- 정본 `Data/Sections/DifficultyData.cs` — `UnlockStage`(20) · `TryBreakthrough` · `PendingUnlock`
- 그 난이도에서 20스테이지 이상을 **처음** 깨면 `RunBootstrap.AdvanceStage` 가 **기록만** 한다 (다음 등급 열림 + `PendingUnlock`)
- ⚠ **연출은 런 끝 보상 상자를 닫은 뒤다** (사용자 지시) — `GrantGearBox` 가 상자의 `SetOnClose` 로
  `DifficultyUnlockPopup.ShowPending()` 을 건다. 자물쇠가 흔들리다 깨지고 난이도 이름·설명·환생 배율이 뜬다
  - 굽기: `프리팹 생성 > 팝업 > 난이도 해금` (`DifficultyUnlockPopupCreator`, `PopupType.DifficultyUnlock`=31) → `[Load Popup Prefabs]`
  - ⚠ 기록은 창을 **연 뒤에** 지운다. 그 사이 앱이 꺼졌으면 다음 실행의 `LobbyManager.Start` 가 연다
    (런에서 돌아올 때는 부르지 않는다 — 상자가 아직 떠 있다)
- ⚠ 추가 상자를 주지 않는다 — 상자는 원래 런 끝 상자다

### 난이도 수치 (2026-09-11 상향, 사용자 지시) — 정본 `DifficultyConfigCreator`
| | 쉬움 | 보통 | 어려움 | 지옥 | 불지옥 |
|---|---|---|---|---|---|
| 환생 포인트 | **×0.8** | ×1.5 | ×2.3 | ×3.4 | ×5.0 |
| 적 공·체(광포) | — | +90% | +230% | +460% | +900% |
| 적 수(물량) | — | — | +25% | +55% | +80%(상한) |
| 우두머리 쿨감(각성) | — | — | — | −45% | −55%(상한) |
| 런 끝 장비 상자 | 1 | 2 | 3 | 4 | 5 |
- 쉬움을 깎은 이유 — 원작보다 스테이지가 훨씬 쉽게 깨진다
- **적 체력·공격력의 단일 관문은 `GeneralStatRoller.Roll`** (2026-09-11) — `GlobalScale`(0.8, 전체 20% 하향) × (1 + 광포)
  - 장수 스폰 · 병사만 오는 부대 · 전황 · 용사 상세가 전부 여기를 지나 **표시와 전투가 같다**
  - ⚠ **광포는 그전까지 아무 데도 안 걸려 있었다** — `MonsterStatRoller`(원작 적 롤러, 지금은 호출처 없음)에만 있었다.
    거기서 곱하면 아군 몬스터가 세지므로 걷어냈다. 새 적 배율은 반드시 `GeneralStatRoller` 에 넣을 것
- ⚠ `ReincarnationData.PreviewPoints` 에 배율 하한 `Max(1, …)` 를 두지 말 것 — 쉬움 ×0.8 이 조용히 무효가 된다
- 상자 수 = `TierEntry.GearBoxes` + 유물 '대장간의 기억'. `GearBoxPopup.SetupMany` 가 **한 창에서 차례로** 연다
  ([다음 상자] · 제목에 `(2/5)`). ⚠ 상자마다 팝업을 새로 열지 말 것 — 닫기 콜백에서 같은 타입을 열면 인스턴스가 하나 더 생긴다
- 에셋(`Resources/DifficultyConfig.asset`)도 같은 값으로 고쳐 두었다 — Creator 와 어긋나면 Creator 가 정본

### 인게임 배경 = 난이도 역순 (사용자 지시, 2026-09-11)
- 쉬움 BG5(마왕성) → 불지옥 BG1(숲). 원작은 숲에서 마왕성으로 쳐들어갔고, 이 게임은 마왕성이 출발점이다
- 순서는 `InGame.unity` 의 `BattleScrollManager._bgByTier` 배열이 정한다 (코드는 뒤집지 않는다)
- ⚠ 마지막 등급(불지옥)은 열 것이 없어 연출도 없다 · 치트의 `RecordClear` 는 기록만 올린다

### 로비 배경 데모 (사용자 지시, 2026-09-22) — `Lobby/LobbyDemoBattle.cs`
- MainPanel 뒤에서 **고른 소환사의 `StarterMonsters`** 가 `MonsterSpawner.SpawnFree`(generation 1)로 서서 오른쪽으로 걷고,
  오른쪽 끝에 닿으면 풀에 돌아간다. 소환사를 바꾸면 걷던 개체를 돌려보내고 새 덱으로 다시 흩어 놓는다
- 여닫기: `MainPanelUI.OnEnable/Select` → `Show(summoner)` · `OnDisable` → `Hide()`.
  판은 `BattleArena.Open(Demo)` + `PresentMode.ArenaBehindUI`, 닫기는 `BattleArena.Close()` 하나다 (Close 가 `Halt()` 를 부른다)
  - ⚠ **Returning 중에 켜진다** (런이 끝나면 로비 캔버스가 먼저 켜진다) — `Run` 이 `LobbyFlow.Idle` 까지 기다린다.
    그래서 Close 는 **데모 판을 닫을 때만** `LobbyDemoBattle.Halt` 를 부른다
  - ⚠ **용사는 세우지 않는다** — 용사가 죽으면 런 골드가 들어온다(`UnitDeathDespawnSystem`)
- **판을 닫으면 세우는 곳부터 멈춘다** (`BattleArena.Close` ⓪, 2026-09-22 버그) — `SummonController.Halt`(배출·풀 채우기) ·
  `HeroSpawner.Halt` · 데모. 전투 중 '즉시 환생' 하면 배출 코루틴이 남아, 판을 치운 **뒤에** 나온 몬스터가 로비 뒤에 남고 풀에 안 돌아갔다.
  함께 고친 것: `SummonController` 가 `OnDefeat` 에 배출을 멈춘다 · `Surrender` 가 `IsWaveRunning` 을 내린다 ·
  `SummonerRuntimeBridge.Current` 는 **풀에 돌아갈 때(OnDisable)** 비운다 (예전엔 파괴 때만 — 판을 닫아도 '서 있는 소환사' 가 남았다)
  - ⚠ 새로 스폰 코루틴을 만들면 `BattleArena.Close` ⓪ 에 멈추는 줄을 넣을 것

### 일시정지 창 (PausePopup)
- **'즉시 환생하기' 는 `LobbyManager.IsInRun` 이 정한다** (사용자 지적, 2026-09-10)
  - ⚠ `Flow == LobbyFlow.Battle` 로 판정하지 말 것 — 이 게임의 런은 **`SummonRun`** 으로
    돈다. `Battle` 은 원작 흐름의 값이라 한 번도 켜지지 않아서, **런 내내 그 버튼이
    통째로 감춰져 있었다.** 프리팹·Creator 는 멀쩡했다 — 조건만 옛 흐름을 보고 있었다
  - ⚠ 판정을 화면마다 적지 말 것. `LobbyFlow` 에 값을 늘리면 `IsInRun` 을 함께 볼 것
- 버튼이 감춰지면 패널 높이도 `_surrenderRowH` 만큼 줄어든다 (Creator 가 넘겨 준 값)

### 로컬라이징 (원작 패치 이식, 2026-09-16)
- **번역 정본**: `Assets/Resources/Localization/LocalizationTable.txt` — 탭 구분 11열(Key·한국어·영어 + 8개 언어).
  루트의 `validate_localization.py` 로 검증한다(열 수·중복 키·서식 자리·TMP 태그·줄바꿈).
- **읽는 곳**: `LocalizationManager` — `Get(키)` · `LocalizeText(문장)` · `Format` · `SetLanguageIndex`.
  언어는 `PlayerPrefs`(`ProjectK.Language`)에 남고, 첫 실행은 기기 언어를 따른다
- **자동 번역**: `Localization/LocalizedText.cs` — TMP 마다 붙어 언어 변경·런타임 대입을 번역한다.
  `EditorUIBuilder.TMP/Btn` 이 구울 때 붙이고, `PopupBase.Awake` 의 `EnsureIn` 과 씬 로드 훅이 옛 프리팹을 보완한다
- **언어 선택**: 일시정지 팝업의 '언 어' 드롭다운 (`EditorUIBuilder.LabeledDropdown`).
  - ⚠ **언어 이름은 원어 고정이다** (사용자 지적, 2026-09-16) — 드롭다운 안의 `LocalizedText` 를 전부 끈다.
    안 끄면 "日本語" 가 지금 언어로 되번역돼, 영어를 고른 순간 목록이 전부 영어가 된다. 제 나라 말을 찾을 수가 없다
  - ⚠ **드롭다운 표기는 번역 대상이 아니라 고정 상수다** (사용자 지시, 2026-09-16) —
    정본 `LocalizationManager.SupportedLanguageNames` (열 줄 하드코딩). 표(LocalizationTable)를 **안 지난다**
    - 그래서 `Instance`(표 로딩)도 `SetLanguage` 도 필요 없다. 지금 언어가 무엇이든 이 열 줄은 그대로다
    - ⚠ 예전엔 이 프로퍼티 안에서 `EnsureFor(일본어·중국어·프랑스어)` 를 불러 **기기 폰트를 런타임에 등록**했다.
      드롭다운이 유독 잘 깨진 원인이 그것이다 — 본문은 고른 언어 **하나**만 그리면 되지만
      드롭다운은 한국어 상태에서도 日本語·简体中文 을 **동시에** 그려야 해서, 기기 폰트 넷이 전부 맞아야 했다
    - ⚠ 순서는 `LanguageOrder` 와 한 묶음이다 (드롭다운 인덱스 = 그 배열의 자리). 길이가 어긋나면 생성자가 에러를 낸다
  - ⚠ **가나·한자는 미리 구운 에셋이 그린다** (사용자 지시, 2026-09-16) —
    `아이콘·텍스처 > 언어 선택 폰트` (`Localization/Editor/LanguagePickerFontCreator.cs`)
    → `Assets/_project/6.Fonts/LanguagePickerFont.asset`, 굽고 나서 **`LiberationSans SDF` 폴백표 맨 앞에 자동 등록**
    - 한글이 `TDS_RPG_2 SDF` 로 그려지는 것과 **같은 길**이다 — 런타임 코드가 아예 관여하지 않아
      런타임 폰트 생성·파괴가 만드는 `MissingReferenceException` 부류가 구조적으로 없다
    - ⚠ **구울 글자를 손으로 적지 않는다** — `SupportedLanguageNames` 에서 뽑아 기본 폰트가
      **실제로 못 그리는 것만** 남긴다(`HasCharacter(searchFallbacks: true, tryAddCharacter: false)`).
      라틴은 기본 폰트(Latin-1 안의 ñ·é·ê·ç 포함), 한글은 TDS_RPG_2 가 이미 갖고 있어 **실제로 굽는 것은 가나·한자 9자뿐**이다
    - ⚠ **언어를 추가하면 이 도구를 다시 돌릴 것.** 안 돌리면 그 줄만 □ 로 뜬다 (`Verify` 가 굽는 순간 글자를 대고 에러)
    - ⚠ 두 번째 실행부터는 제 에셋을 폴백표에서 **먼저 뗀 뒤** 판정한다 — 안 떼면 "이미 그릴 수 있다" 가 되어 한 글자도 안 굽는다
    - ⚠ Resources 밖에 둔다 — `LiberationSans SDF` 가 참조하므로 그 의존으로 빌드에 따라 들어간다
  ⚠ 드롭다운 루트에 `RaisedBtn` 을 쓰지 말 것 — Button 과 TMP_Dropdown 이 겹친다
- **폰트**: `Assets/_project/6.Fonts/Source/NotoSansCJKsc-Regular.otf` (16MB) — **굽는 재료일 뿐, 빌드에 안 실린다**
  - ⚠ **Resources 밖으로 옮겼다** (사용자 지시, 2026-09-16 — "안 쓰면 제거해").
    `Resources/` 안의 것은 **아무도 안 써도 통째로 빌드에 실린다** — 16MB 가 그렇게 실려 있었다.
    지우지는 않았다(git 에 없는 파일이라 지우면 되돌릴 수 없고, 언어를 추가할 때 다시 구울 원본이 필요하다)
  - ⚠ 함께 **코드째 사라진 것**: `LocalizationFontFallback.BundledFont` · `UsableBundledFont` · `_bundledFont` ·
    `PickerGlyphs`. 옛 이름으로 검색해 안 나오면 여기다
  - 지금 CJK 를 그리는 길은 둘뿐이다
    | 무엇 | 누가 그리나 |
    |---|---|
    | 언어 선택 드롭다운의 가나·한자 | 미리 구운 `LanguagePickerFont` (기본 폰트 폴백표) — 런타임 코드 없음 |
    | 본문 전체 (일본어·중국어를 골랐을 때) | **기기 폰트** (`EnsureFor` 가 찾아 폴백에 등록) |
  - ⚠ 기기에 그 폰트가 없으면 `EnsureFont` 가 **경고를 찍는다.** 예전엔 번들 폰트를 만들어
    `available = true` 로 덮었는데, 번들이 빠진 지금 그러면 "폰트가 있다" 는 거짓말만 남고 화면엔 □ 가 뜬다
    (Windows·macOS·iOS·Android 는 CJK 기본 탑재라 실제로 닿을 일은 드물다)
  - ⚠ **런타임 폰트는 파괴되면 폴백 목록에서 걷어내야 한다** (2026-09-16)
    `HideFlags.DontSave` 라 플레이 모드를 나갈 때 파괴되는데 `TMP_Settings.fallbackFontAssets` 와 정적 캐시는 남는다.
    그대로 두면 TMP 가 사라진 아틀라스를 읽어 `MissingReferenceException: m_AtlasTextures ...` 가 난다.
    정본 `LocalizationFontFallback.PruneDestroyed` — 목록을 청소하고 '확인함' 표시를 풀어 다시 등록하게 한다.
    ⚠ 폴백에 넣는 곳은 `RegisterFallback` 하나다. `fallbackFontAssets.Add` 를 직접 부르지 말 것
    ⚠ **청소는 씬이 열리기 전에** 한다 — `PruneBeforeSceneLoad`(`RuntimeInitializeOnLoadMethod`).
      매니저가 만들어질 때 청소해서는 늦다: 스플래시 첫 글자가 먼저 그려지며 터진다.
      디스크의 TMP Settings.asset 은 폴백이 비어 있어도, **로드된 에셋 인스턴스**에는 지난 플레이의 항목이 남는다
    ⚠ 에디터에서는 플레이를 나갈 때 우리가 넣은 항목을 되돌린다 (`OnPlayModeChanged`) — 파괴될 참조를 설정에 남기지 않는다
- ⚠ **표에 없는 한국어 문구는 번역되지 않고 그대로 나온다.** UI 에 뜨는 문구를 추가하면 표에도 한 줄 넣을 것
- ⚠ **수치가 든 설명에 보간 문자열(`$"…{값}…"`)을 쓰지 말 것** (2026-09-16, 실제로 당한 뒤 세운 규칙)
  보간은 **실행 시점에 이미 숫자로 바뀌어 있다.** 표의 키는 코드에 적힌 그대로의 문자열이므로
  `"공격력 +{Pct(Rule.X)}"` 같은 키는 런타임 문자열과 **영원히 일치하지 않는다** —
  표에는 줄이 멀쩡히 있는데 화면에는 한국어로 나온다. **에러도 경고도 안 난다.**
  - 정본은 `LocalizationManager.Format(키, 인자)` 다 — 키로 줄을 찾아 **번역문에** 인자를 끼운다.
    언어가 바뀌어도 숫자와 자리가 함께 따라간다. 네 파일이 `static string F(...)` 도우미로 부른다
    (`RunPerk` · `SpeciesPassive` · `RunEvent` · `MonsterSynergyRule`)
  - ⚠ **한 문장을 `+` 로 쪼개지 말 것** — 조각은 언어마다 어순이 달라 옮길 수가 없다. 줄바꿈은 인자 쪽에서 한다
  - ⚠ 다만 **조건부로 이어 붙는 조각**(`line += …`)은 합치지 않는다. 티어에 따라 붙었다 말았다 하므로
    한 문장으로 묶으면 없는 효과를 말하게 된다 — 조각마다 제 줄을 갖고, 앞의 `" · "` 까지가 키의 일부다
  - 판별은 기계적이다: 살아 있는 키는 `{` 뒤가 **숫자**(`{0}`·`{1:0.#}`), 죽은 키는 아니다
- **콘텐츠 문구는 2026-09-16 에 전부 넣었다** — 표 1,540 → **2,487줄**.
  끝난 파일: `MonsterCodexCreator` 234 · `MonsterGearCreator` 114 · `SpeciesPassive` 114 · `RunEvent` 110 ·
  `RunPerk` 81 · `MonsterSynergyRule` 64 · `SummonerCreator` 47 · `ActiveSkillCreator` 44 · `HeroNameRule` 55 ·
  `RelicTreeTypes` 35 · `SummonerPerk` 12 · `MonsterGearOption` 11 · `RelicTreeCatalog` 51(`Relic.*` 기호 키)
  - ⚠ 그중 **119줄이 죽은 보간식 키**였다 (위 규칙). 지우고 네 파일을 `Format()` 으로 고쳐
    **온전한 문장 104줄**로 다시 넣었다 — 조각이 합쳐지며 줄 수가 줄었다
  - **`Format()` 으로 고친 파일은 일곱이다** — 위 넷(`RunPerk`·`SpeciesPassive`·`RunEvent`·`MonsterSynergyRule`)과
    `RelicTreeTypes`·`SummonerPerk`·`MonsterGearOption`. 살아 있는 `{0}` 줄 70 → **221**
    - ⚠ `RelicTreeTypes` 의 10줄은 **원본 표에 이미 있던 것**이 그대로 살아났다 — 원본이 처음부터
      `{0}` 방식으로 적어 뒀는데 아무도 `Format()` 을 부르지 않아 잠들어 있었다. 이 변환이 맞다는 증거다
    - ⚠ 한글이 없는 줄(`{label} {sign}{v}%`)은 번역할 것이 없어 그대로 뒀다 — 라벨은 이미 `Get` 으로 들어온다
  - ⚠ **검증은 기억이 아니라 측정으로 한다** — 코드의 `F("…")` 키를 전부 뽑아 표에 있는지 대조한다.
    2026-09-16 기준 **166키 전부 찾아짐 · 죽은 줄 0**. 눈으로 훑다가 `GoldAbility` 넷과
    `RunEvent` 결과 문장 14개를 실제로 빠뜨렸다 — grep 이 아니었으면 그대로 남았다
  - **UI 화면 문구도 넣었다** (2026-09-17) — 표 **2,697줄**. 보간 문구 약 90곳을 `LocalizationManager.Instance.Format` 으로
    바꿨다(`TopBarUI`·`MonsterDetailPopup`·`ShopPopup`·`RunNodeFlow`·`ManaRegenRule` 등). 코드의 `Format` 키 **263개 전부 표에 있음**
  - ⚠ **아직 안 넣은 것** — ① **두 글자 이하 낱말**(`진화`·`융합`·`뒤로`·`제단`·`갑옷`·`증식`·`패기` 등) —
    부분 치환 오염 때문에 기호 키로 돌려야 한다(유물 노드 `Relic.*` 처럼) ② Creator 자리표시(`수집 84 / 120` 등 — 런타임에 덮인다)
    ③ 인스펙터 `[Header]`·툴팁 · 콘솔 로그(번역 대상 아님)
  - **튜토리얼 문구는 2026-09-18 에 넣었다** (표 2,695줄) — 한국어가 이 게임에 맞게 고쳐진 뒤에야 옮겼다.
    시나리오 5개 · 도움말 6개의 말풍선 전부 + `건너뛰기`·`화면을 누르면 계속`·`표시된 곳을 누르세요`
    - **원작 튜토리얼 96줄은 함께 지웠다** — 장수·어빌리티·용병·분해·웨이브를 설명하는 죽은 줄이었다.
      지우기 전에 `.cs`·`.prefab`·`.unity`·`.asset` 전체를 대조해 참조 0건을 확인했다
    - ⚠ **번역 훅을 따로 만들지 않았다** — TMP 에 자동으로 붙는 `LocalizedText` 가 말풍선 글을 옮긴다.
      다만 **칸은 `TutorialOverlay.LayoutBubble` 이 번역문으로 잰다**(`Localized`) — 한국어 길이로 재면
      1.5~2배로 늘어난 번역문이 넘쳐 자동 축소에 걸려 글자만 잘게 쪼그라든다
    - ⚠ 문구를 고치면 **표의 그 줄도 함께 고칠 것.** 키는 코드에 적힌 한국어 그대로이므로
      한 글자만 달라져도 조용히 한국어로 돌아간다 (에러가 안 난다)
  - ⚠ **시너지 이름 `숲`·`동`·`은`·`금` 은 일부러 뺐다** — 한 글자 키는 부분 치환으로 다른 문구를 오염시킨다
    (`금` → `황금`·`금화`, `동` → `이동`·`동족`). 넣으려면 기호 키로 돌릴 것
- ⚠ **짧고 흔한 조각은 한국어 키로 넣지 말 것 — 기호 키를 쓴다** (사용자 확정, 2026-09-16)
  `LocalizeText` 는 완전 일치가 없으면 표를 **긴 것부터 부분 치환**한다. 그래서 `강철`·`성벽`·`기사` 처럼
  게임 어휘와 겹치는 조각을 한국어 키로 넣으면 **다른 문구 속 같은 글자까지 조용히 바뀐다.**
  - 정본 사례 `HeroNameRule` — 수식어 16 · 이름 24 · 칭호 12 · 접두어 3 을 전부 `Hero.*` 기호 키로 바꿨다.
  - ⚠ **기호 키만으로는 치환을 못 피한다** (2026-09-17 정정) — 부분 치환 풀(`_replaceRows`)은 키가 아니라
    **한국어 칸 전부**로 만든다. 그래서 `LoadTable` 이 `Hero.*`·`Relic.*` 줄을 풀에서 **따로 뺀다.**
    고유명사 기호 키 계열을 새로 만들면 그 접두사도 거기에 넣을 것 (`MaxHp`·`Job_*` 같은 옛 기호 키는 아직 풀에 있다)
- ⚠ **`Format` 결과는 `LocalizedText` 가 다시 번역한다 — `_formatted` 가 없으면 부서진다** (2026-09-17 버그)
  TMP 마다 붙은 `LocalizedText` 는 번역문을 원문으로 되돌린 뒤(`SourceForRendered`) `LocalizeText` 로 다시 번역한다.
  숫자가 끼워진 원문은 표에 없어 부분 치환만 탔고, `Format` 의 온전한 번역을 `처치마다 Attack +12% (최대 5회)` 처럼
  **한글 섞인 문장으로 덮어썼다** — 시뮬레이션상 `Format` 키 258개 중 236개. 지금은 `Format` 이 원문 → (줄, 인자)를
  기억하고 `LocalizeText` 가 `TryReformat` 으로 **지금 언어로 다시 Format** 한다(언어 변경에도 맞다).
  ⚠ 이 경로를 우회하는 새 번역 함수를 만들지 말 것 — 반드시 `Format`/`Get` 을 지날 것
  ⚠ **번역된 조각을 `$"…"` 로 이어 붙이지 말 것** — 용사 이름이 그래서 한국어로 남았다(2026-09-17).
    `HeroNameRule` 은 `Format("Hero.Format.Full"/"Short"/"Prefixed", …)` 로 조립한다(띄어쓰기·어순은 표가 정한다)
- **번역문 넘침은 `LocalizedText.ApplyFit` 이 맞춘다** (사용자 지적, 2026-09-17) — 번역된 글이 들어간 동안만
  자동 축소(여러 줄 칸 0.6배·최소 18 / 한 줄 칸 0.35배·최소 12)를 켜고, 한 줄 높이 칸은 줄바꿈을 끈다. 한국어로 돌아오면 원래 설정 그대로다.
  ⚠ 한 줄 칸 바닥이 낮은 이유 — 18 이던 때 72px 배지의 한 단어가 "Afinid..." 로 잘렸다 (친화·전황 등급)
  ⚠ **툴팁 폭은 `InfoTooltipUI.ApplyLanguageWidth` 가 언어별로 넓힌다** — 구운 폭 × `LocalizationManager.TextWidthScale`
    (한·중·일 1 · 라틴 1.4, 상한 900). 모든 툴팁(`TooltipLayer` 포함)이 이 한 곳을 지난다. Creator 폭을 언어 때문에 늘리지 말 것
  ⚠ UI 규칙 4(FontSm 하한)는 한국어 기준이라 번역문은 그보다 작아질 수 있다 — 너무 작으면 칸을 넓힐 것.
  ⚠ `ContentSizeFitter` 가 붙은 글은 건드리지 않는다
- **MainPanel 사이드 '설정'** (사용자 지시, 2026-09-17) — 옛 '기타' 잠금 칸 자리. `PausePopup` 을 그대로 연다(소리·언어)
  - ⚠ **표에 없는 키는 `Get` 이 키 문자열을 그대로 돌려준다** — 화면에 `Hero.Epithet.01` 이 뜨고 에러는 안 난다.
    키를 바꾸면 표의 `Hero.*` 줄도 함께 고칠 것
  - 기존 기호 키 99개(`MaxHp`·`Job_Knight`·`Language.*`)와 같은 방식이다

### 튜토리얼
- **총괄·노출 시점**: `Tutorial/TutorialManager.cs` — 트리거는 전부 여기 (각 UI 에서 부르지 않는다)
- **시나리오**: `Tutorial/Scenarios/*.cs`, 스텝 정의는 `Tutorial/TutorialStep.cs`
- **화면**: `Tutorial/TutorialOverlay.cs` (sortingOrder 1000)
- **⚠ 강제 튜토리얼은 팝업 위에서 시작하지 않는다** — `TutorialScenario.StagePopup` 참고
- **기록**: `Data/Sections/TutorialData.cs` (환생으로 초기화하지 않는다)
- **이 게임용으로 다시 켰다** (사용자 지시, 2026-09-17) — `TutorialManager.Enabled = true`
  | 강제 | 언제 | 트리거 |
  |---|---|---|
  | `SummonBattle`(10) | 1스테이지 대기 — HUD·카드 고르기→라인 탭→[시작] → 전투 중 시그니처 스킬 직접 쓰기·전투 UI | `StageLoopDirector.OnStageReady(1)` |
  | `FirstReward`(11) | 1스테이지 클리어 — 카드 3택·갈림길 | `BattleManager.OnVictory` (스테이지 1) |
  | `FirstRelic`(6) → `FirstCodex`(12) | 첫 환생 뒤 메인 화면, 보상 상자 등 팝업이 다 닫힌 뒤 | `MainPanelUI.OnShown` (환생 1회 이상) |
  - **2스테이지부터는 강제 안내가 없다** (사용자 지시)
  - **첫 실행은 소환사 선택 없이 곧장 1스테이지다** — `LobbyManager.SelectInitialPanel` 의 `ConsumeFirstLaunch` 분기,
    소환사 = `FirstRunSummonerId`("novice", 견습). ⚠ `FirstCodex` 가 슬라임을 전제한다
  - ⚠ `FirstCodex` 는 첫 품질 개선(770)에 **모자란 영구 골드만 한 번 채운다** — 시작 골드 500 이라 첫 런을 일찍 끝내면 못 누른다
  - ⚠ 라인 탭은 월드 입력이다 — `LaneGuide/Band_3`(raycast 꺼짐)을 구멍으로 뚫어 탭이 전장에 닿게 한다
  - `TutorialScenario.Resumable` — 게임 상태를 따라가는 시나리오(인게임·보상)는 앱 재시작 시 처음부터다
- **도움말(i 버튼)**: 유물 · 도감 · 난이도 · **몬스터 상세 · 카드 3택 · 전황** (`HelpTutorials.cs`)
  - ⚠ i 버튼은 Creator 가 굽는다 — 시나리오만 등록하면 버튼이 없다. 굽기: `팝업 > RelicTree` · `몬스터 상세` · `▶ 런 팝업` · `전황` → `[Load Popup Prefabs]`
    - ⚠ **"i 버튼이 안 보인다" 는 대개 Creator 가 아니라 프리팹이 옛것이다** (사용자 지적, 2026-09-18)
      실제로 `Codex` 만 다시 구워져 있어 나머지 다섯(유물·몬스터 상세·카드 3택·전황·난이도)에 버튼이 통째로 없었다.
      **에러가 안 난다** — 코드에는 `InfoBtn` 한 줄이 멀쩡히 있다. 프리팹을 grep 해 `m_Name: InfoBtn` 이 있는지 볼 것
  - ⚠ **닫기와 i 는 같은 크기·같은 세로 자리여야 한다** (사용자 지적, 2026-09-18)
    `InfoBtn` 은 정사각형 하나(`size`)로 서는데 도감 닫기만 88×78 · y+4 라 머리 줄이 삐뚤어 보였다.
    나란히 서는 두 버튼은 **Creator 안에서 한 상수를 함께 쓴다** (`CodexPopupCreator.CloseSize`)
  - ⚠ 장비 상세는 머리(착용 초상화·지갑)에 i 버튼 자리가 없어 뺐다 (여유 16px)

### 성장 연출 (레벨업·강화 이펙트)
- `UI/Juice/UIJuice.cs` (프리셋) + `UI/Juice/UIJuiceLayer.cs` (실행)
- **⚠ 대상의 `localScale` 을 직접 만진다.** 버튼이 커져 보이면 레이아웃보다 여기를 먼저 본다

### 세이브 데이터 수정
- **매니저**: `Data/Core/UserDataManager.cs`
- **섹션 추가**: `Data/Core/ISaveSection.cs` 구현 → `UserDataManager` 등록
- **기존 섹션**: `Data/Sections/` 하위 파일들
- **저장 트리거**: `UserDataManager.Instance.RequestSave()`

### 밸런스 점검 (2026-09-16, 사용자 요청 — "업그레이드하면 너무 쉽다 · 지금도 쉽다")
- 적 전체 배율 `GeneralStatRoller.GlobalScale` — 1.0 으로 올렸다가 **0.88 로 되돌렸다** (사용자 지시, 같은 날 — 초반이 세졌다).
  ⚠ 스테이지 1 부터 곱해지는 값이다. 후반만 올리려면 가속을 본다
- 고정 성장 가속 `LevelGrowthAccel` 0.02 → 0.03 → **0.05** (에셋 + `UnitJobRoller` 폴백 — 초반 등급 상한을 넣은 대신 후반을 올렸다)
- **후반 복리** `GameplayConfig.LateGrowthFromLevel`(10) · `LateGrowthRate`(0.07) — 적 체력·공격력 × 1.07^(레벨−10) (사용자 지적 — "30스테이지가 10스테이지의 2배뿐")
  기사 체력 10→30스테이지 약 ×8, 공격 약 ×10. ⚠ 고정 성장까지 더한 뒤 곱한다(`UnitJobRoller.Roll`). 병사 수·방어율·속도엔 안 건다
- 품질 계수 `GradeMultPerTier` 는 에셋 값 **0.15 가 정본이다** (사용자 확정, 2026-09-16) — 문서·코드 폴백의 '+10%' 가 옛 값이다. 되돌리지 말 것
- 영구 성장(품질·유물 공 +62%/체 +92%·장비)에는 적 쪽 대응이 난이도 하나뿐이다. 더 벌어지면 유물 스탯 노드·2차 권속 쿨다운부터 볼 것

### 초반 용사 등급 상한 (사용자 지시, 2026-09-16)
- 레벨(= 스테이지) 1~9 용사의 태생 등급은 **고급(Uncommon)까지** — 정본 `UnitJobRoller.GetBirthGrade(이름, 레벨)`
- 이름 시드가 5스테이지 장수 둘(`Hero_S5_0/1`)을 영웅으로 굴려, 기사가 병사 14명을 스탯 99% 로 끌고 왔다 (견습 첫 런 벽)
- ⚠ 레벨을 아는 곳은 반드시 2인자판을 쓴다 — 전투(`HeroSpawner`·`GeneralRuntimeBridge`)와 화면(전황·`UnitEntry.Grade`)이 같아야 한다.
  1인자판은 레벨이 없는 곳(초상화 합성·희귀 스킬 로그)만 쓴다 — 초반 영웅 장수의 초상화 외형만 필드와 다를 수 있다

### 유물 '치유의 기억' — 재생·회복량 (사용자 지시, 2026-09-16, 옛 '야성의 기억')
- 정본 `SpeciesPassiveRuntime.PassivePower` (레벨당 +10%). 옛 설명은 "종족 패시브 효과" 인데 재생 둘에만 걸려 있었다
- 거는 곳: 재생·트롤의 피·치유의 잔재·생명의 씨앗·광합성(소환 때 비율에 굽는다)·힐 슬라임 치유 스킬(`ActiveSlimeMend`)·
  원작 회복 패시브 셋(흡혈 타격·긴급 회복·처치 회복 — `HealPowerFor`). 생명 흡수·흡혈귀는 그 슬롯을 붙여서 저절로 따라온다
- ⚠ 원작 회복 패시브는 **용사도 쓴다** — `HealPowerFor` 가 회복하는 쪽이 몬스터일 때만 배율을 준다
- ⚠ 시너지 회복(재생 시너지·숲 사망 회복)은 안 건다 — 패시브가 아니다. enum ID 는 `N_FeralMemory` 그대로(세이브·아이콘)

### ⚠ 성벽 앞에서 머리만 박는 용사 — 풀 재사용 때 `BreachedTag` 가 남았다 (2026-09-16 버그)
- 성벽을 통과한 용사 엔티티는 `BreachedTag`+`DeadTag` 로 풀에 돌아간다. 재사용 초기화(`UnitRuntimeBridge`)가 `DeadTag` 만 떼서,
  다음 판에 병사로 다시 선 엔티티가 처음부터 "이미 통과" 로 판정에서 빠졌다 — 성벽선(x −12.00 < 통과선 −11.85)에 서서 영영 안 거둬졌다
- 지금은 같은 자리에서 `BreachedTag` 도 뗀다. ⚠ 엔티티에 태그를 새로 붙이면 이 초기화에도 떼는 줄을 넣을 것

### 보스는 아군이 남아 있으면 성벽에 붙들린다 (사용자 지시, 2026-09-16)
- 정본 `CoreBreachSystem` — 필드 몬스터(`MonsterLineReturner.AliveCount`)나 **배출 중인** 대기열(`SummonController.HasPendingSpawns`)이 있으면 보스를 거두지 않는다.
  ⚠ 대기열 수로 세지 말 것 — 배출이 멈춘 라인의 몬스터까지 세어 필드가 빈 채 보스가 영원히 붙들렸다 (2026-09-16)
- ⚠ 전투 중 대기열 복귀(숲 금 · 특성 '귀환')는 `SummonController.ReturnToLine` 을 쓴다 — 넣기만 하면 배출이 끝난 라인에서 다음 판까지 갇힌다
  `ScreenClampJob` 이 성벽선에 물려 두어 그 자리에서 싸운다. 둘 다 비었을 때만 함락
- 특성 '매복'(3초 배출 정지) + 보스판에서 돌진한 보스가 빈 전장을 지나 즉시 패배시키던 문제다. 보통 용사는 그대로 −1
- **돌진은 성벽 1칸 앞에서 멈춘다** (사용자 지시, 2026-09-16) — `BossChargeRunner.WallStopMargin`. 착지점(관통 포함)이 성벽선을 넘으면 경로를 자른다.
  ⚠ 통과 판정선(`WallX + 0.15`)보다 바깥이어야 한다 — 안쪽이면 돌진 착지만으로 함락된다

### 패배 화면의 '이번 런' 칸 (사용자 요청, 2026-09-16)
- `ReincarnationPopup` 오른쪽 칸 — 보유 특성 격자 + 덱 8칸(누르면 몬스터 상세 런 모드). 창 1240 → 1840
- 특성 목록 정본은 `RunPerkBarUI.Collect` (HUD 줄과 공유 — 개성·특성·제단 표식)
- 굽기: `프리팹 생성 > 팝업 > Reincarnation` → `PopupManager [Load Popup Prefabs]` (시너지 아이콘이 먼저 있어야 한다)

### ⚠ 용사가 제 편을 죽이면 골드가 없다 (2026-09-15 버그)
- 용사 스킬 '병사 희생'(제 병사에게 999,999 피해) · '자폭 병사'(스스로 터짐)의 사망이 **처치로 세어져**
  골드와 동전 연출이 났다. 용사 스킬은 쿨다운 0 으로 서므로 스테이지가 열리자마자 화면 오른쪽에서 동전이 날아왔다
- 지금은 `UnitDeathDespawnSystem.KilledByOwnSide` — 마지막 일격(`DamageResultElement.IsKill`)의 공격자가 **같은 진영**이면
  골드·'강적의 정수'를 주지 않는다. 공격자가 이미 사라졌으면 확인할 수 없어 준다
- ⚠ 적 수 집계(`OnUnitDead`)·처치 이벤트는 그대로다 — 막은 것은 **보상**뿐이다

### 골드 값은 **수입을 따라간다** (사용자 지적, 2026-09-10)
> 정본은 `RunGoldRule.PriceUnit(stage)` / `Price(배수, stage)` 하나다.

- ⚠ **모든 런 골드 소비처는 배수로 적는다.** 숫자를 직접 적지 말 것
  | 소비처 | 배수 | 1스테이지 | 30스테이지 |
  |---|---|---|---|
  | 상점 카드 | ×2 | 80 | 660 |
  | 상점 특성 | ×2.5 | 100 | 825 |
  | **재고 교체(리롤)** | ×(0.75 + 0.75×굴린 횟수) | 30 | 250 → 495 → 745 |
  | 마력의 정수 | ×(2 + 0.75×산 횟수) — 2026-09-15 1.5 에서 반으로 | 80 | 660 → 907 → 1,155 |
  | 야영지 증축 | ×2.5 | 100 | 825 |
  | 강화소 각인·증식 | ×2 | 80 | 660 |
  | 이벤트 골드 갈래 | `GoldUnit` = 같은 단위 (×1~×4) | | |
  - ⚠ **카드와 특성은 가운데로 모았다** (사용자 지시, 2026-09-10) — ×1.5 와 ×3 은
    폭이 두 배라 특성이 "가끔 지르는 사치품", 카드가 "늘 사는 소모품" 으로 갈렸다.
    저울질하려면 값이 비슷해야 한다. 가운데(×2.25) 언저리에 놓고 **특성만 한 칸 위**다
- **왜** — 수입은 스테이지를 따라 커진다(적 총량 = 부대 5 × 병사 (스테이지+4)).
  1스테이지 40 골드 · 30스테이지 **1,700 골드**로 40배 차이인데 소비처는 전부
  고정가(40·70·90)였다. 중반부터 살 것을 다 사고도 돈이 남고, 유일한 반복 구매인
  **마력의 정수만 끝없이 사는** 상태가 됐다. 돈이 남은 것은 값이 싼 것이 아니라
  **값이 수입을 안 따라간 것**이다
- ⚠ 정수는 **스테이지와 산 횟수 둘 다**로 오른다 — 횟수만 보면 후반의 큰 수입 앞에서
  무의미하고, 스테이지만 보면 "한 상점에서 몰아 사기" 가 남는다
- ⚠ `RunEventRule.GoldUnit` 은 `RunGoldRule.PriceUnit` 에 **위임**한다. 주는 쪽과
  쓰는 쪽이 같은 자를 써야 "이 골드가 얼마짜리인가" 가 성립한다
- ⚠ 갈림길 카드 문구(`RunNodeKind.Describe`)와 시설 화면이 **스테이지를 받는다** —
  값이 변하므로 고정 숫자를 적으면 카드가 거짓말을 한다
- **재고 교체(리롤)가 끝없는 소비처다** (2026-09-10) — 정본 `RunShopRule.RerollPrice`
  - 상점 재고는 카드 4 + 특성 2 로 **유한하다**. 다 사면 남는 돈이 정수로만 흘러
    후반 상점이 "정수 자판기" 가 됐다. 리롤은 끝이 없어 언제나 갈 곳이 된다
  - ⚠ 굴린 횟수는 **이 방문 안에서만** 센다(`ShopPopup._rerolls`). 저장하지 않는다 —
    저장하면 "껐다 켜서 리롤 값 되돌리기" 가 생긴다
  - ⚠ **정수는 굴리지 않는다** — 재고가 아니라 상시 판매품이고 값이 '산 횟수' 로 오른다.
    리롤로 되돌릴 수 있으면 브레이크가 통째로 풀린다
  - ⚠ 여는 쪽과 리롤이 **같은 함수**(`ShopPopup.RollStock`)를 쓴다. 갈라지면
    "리롤한 재고만 규칙이 다른" 상태가 된다
  - 자리는 **카드 줄 오른쪽 옆, 카드 세로 가운데**다 (300×170 청록, 2026-09-15 — 라벨 줄 끝의 납작한 갈색 띠는 안 보였다).
    살 카드가 없으면 빈 카드 줄 한가운데로 옮긴다. 지갑(오른쪽 위) 옆에 두지 말 것.
    지갑은 읽는 것이고 이건 누르는 것이라, 값을 확인하려다 재고를 갈아 치우게 된다
  - ⚠ **Creator 를 고쳤으니 다시 구워야 뜬다** —
    `프리팹 생성 > 팝업 > 상점` → `PopupManager [Load Popup Prefabs]`
- ⚠ `RunNodeRule.ShopManaAmount`/`ShopManaCost` 는 **지웠다** — 아무도 안 읽는데
  `RunShopRule` 과 같은 것을 말하고 있었다

### 마왕성 체력은 **적의 마릿수**와 견준다 (사용자 지적, 2026-09-10)
- 성벽을 지난 용사 1기 = 체력 −1. 그런데 한 판의 적은 10스테이지 약 68기,
  30스테이지 약 170기다. 바닥 10 + 체력×2 = **18~26** 이던 시절에는 한 판을 놓치는
  것은 물론 **부대 하나만 새어 나가도**(30스테이지 33기) 그 자리에서 런이 끝났다
- 3배로 올렸다 — `SummonerData.BaseCoreHp` 10→**30** · `CoreHpPerVitality` 2→**6**
  (체력 4~8 → **54~78**). 소환사 SO 12개도 함께 고쳤다
- ⚠ **회복·대가도 같은 비율로 함께 옮긴다.** 하나만 3배면 "야영지가 조금밖에 안
  채워 주는" 상태로 되돌아간다
  | 무엇 | 이전 → 지금 |
  |---|---|
  | `RunNodeRule.CampHealAmount` | 8 → **24** (최대치의 약 1/3 유지) |
  | `RunNodeRule.CampMaxAmount` | 3 → **9** |
  | 이벤트 체력 대가 | −3/−4/−5 → **−9/−12/−15** |
  | 이벤트 `CoreMax` 보상 | +4/+7 → **+12/+21** |
  | 이벤트 회복 | `CampHealAmount × 0.35` 라 저절로 8 이 된다 |

### 마나는 **정수로만 움직인다** (사용자 지시, 2026-09-10)
- 정본은 **두 줄**이다 — 회복은 `ManaRegenRule.RawFor` 끝의 `Mathf.Floor`,
  **그릇은 `RunPerkRule.MaxManaFor` 끝의 `Mathf.Floor`** (2026-09-12 추가).
  비용은 원래 정수라, 이 둘이면 잔량도 그릇도 영영 정수로 남는다
  - ⚠ 그릇이 마지막 구멍이었다 — 깊은 그릇(×1.25)·유물 '넓은 그릇' 이 **배율**이라
    103.5 같은 값이 나왔고, 화면에 `84 / 103.5` 로 떴다
  - ⚠ 그릇도 **내림**이다. 올림으로 적으면 회복이 Max 에서 잘려
    (`RegenForStage`) "104 인데 아무리 채워도 103.5 에서 멈추는" 칸이 생긴다
- ⚠ **반올림이 아니라 내림이다** — 올림은 없는 마나를 만들어 준다. 어차피 소환
  비용이 전부 정수라 0.4 로는 아무것도 못 산다
- ⚠ 이걸로 **특성 '마력 폭주' 가 고쳐진다** — 그 특성은 잔량이 정확히 0 에 닿아야
  켜지는데(`SummonController.UseMonsterCard`), 0.5 같은 찌꺼기가 남으면 그걸로
  살 수 있는 카드가 없어 **영영 0 이 되지 않았다.** 가진 특성이 조건상 발동
  불가능한 것은 밸런스가 아니라 고장이다
- ⚠ 툴팁 내역(`Describe`)은 줄마다 **제 몫만** 적고, 맨 아래 '합계' 한 줄이 누계를 맡는다
  (사용자 지적, 2026-09-12) — 줄마다 누계를 적으니 `아껴 둔 마나 10% → 23` 이
  위의 지능 몫까지 더한 값이라 10%의 결과로 읽혔다. 한 줄이 두 가지를 말하면 둘 다 안 읽힌다
  - ⚠ 그래도 **적힌 것을 더하면 합계와 맞아야 한다** — 조각을 각자 내리면
    (0.6+0.6 → 0+0) 합계(1)와 어긋난다. `Step` 이 '내린 누계의 차' 를 돌려줘
    조각의 합이 언제나 마지막 누계의 내림과 같다
- ⚠ 옛 세이브는 소수점을 들고 이어진다 — `SummonManaData.Deserialize` 가 한 번 턴다

### 마나를 힘으로 (사용자 지시, 2026-09-12)
> 최대 마나가 넉넉해진 만큼 "쌓고 아낄 이유" 를 준다. 마나를 주는 길은 전부
> `RunPerkRule.RestoreMana`(잔량) · `GrowMaxMana`(그릇 + 채우기)를 지난다 — `SummonManaData.Restore` 는 정수·그릇 상한.

| 무엇 | 효과 | 훅 |
|---|---|---|
| 특성 넘치는 그릇(41) | 나오는 순간 보유 마나 10당 공/체 +5% | `RunPerkRule.DrainStatMult` |
| 특성 마력 결정화(42) | 판 경계 남은 마나 10% → 최대 마나 (상한 30) | `RunPerkData.SettleStageEnd` (`CrystalBonus`) |
| 특성 강적의 정수(43) | 엘리트·보스 처치마다 마나 +5 | `UnitDeathDespawnSystem` → `RunPerkRule.OnHeroKilled` |
| 야영지 수리 / 증축 | 마나 33% 회복 / 최대 마나 +6 | `RunNodeFlow.OpenCamp` |
| 이벤트 최대 체력 보상 | 최대 마나 +(체력의 2/3) | `RunEventRule.ManaForCoreMax` |
| 종족 패시브 마나 공명(130) | 최대 마나 1당 공·체 +0.2% — **비전 리치**(`arcane_lich`, 리치 진화) | `SpeciesPassiveRuntime.ApplyOne` |
| 종족 패시브 마나 방출(131) | 죽을 때 마나 +1, 한 판 최대 5 — **마력 해골**(`mana_skeleton`, 스켈레톤 진화) | `SpeciesPassiveRuntime.OnDeath` |
| 소환사 대마법사(`archmage`) | 최대 마나 10당 전군 공/체 +3% · 시그니처 **마나 폭발**(36, **화면 전체** · 대상 최대 체력 30% + 태운 마나(잔량 절반) 1당 1.5% · × 패기 · 보스 절반 · 방어율 무시 · 마나 0 이어도 기본 피해). **가장 늦게 연다** — 비전 리치 + 리치 킹 + 25 스테이지 | `MonsterStatComposer` ⑤-b · `ActiveManaBurst` |
| 소환사 결정술사(`crystalmancer`) | 판 경계 남은 마나 20% → 최대 마나 (상한 100) · 소환력 ×0.6 | `SummonerPerkRuntime.SettleCrystal` |
| 드루이드 개성 → 자연의 회복 | 판 경계 최대 마나의 10% 더 회복 (옛 '야성 질주' 교체) | `ManaRegenRule.RawFor`·`Describe` |
| 오크 킹 개성 → 총동원 | 친화 카드 마릿수 +1 (옛 '전쟁 함성' 교체) | `RunPerkRule.SummonCountFor` |

- ⚠ 최대 마나 저장고가 셋이다 — `RunBoonData.MaxManaBonus`(**상점 정수 산 횟수** — 값이 이 수로 오른다) ·
  `ExtraMaxMana`(야영지·이벤트) · `CrystalMaxMana`(결정술사). 섞으면 야영지를 들를수록 정수가 비싸진다
- ⚠ 새 종족의 시너지 표식은 `MonsterCodexCreator.Spec.Tags` 로 적는다 — 옛 종족 표식은 **에셋에만** 있다(비우면 안 덮는다)
- 굽는 순서: `몬스터 도감` → `액티브 스킬` → `소환사` → `카드 목록` · 아이콘(종족 패시브·특성·개성·유물 트리) → `SpriteManager + 아틀라스` ·
  `프리팹 생성 > 로비 > MainPanel`(격자 3×5 로 늘었다)

### 소환사 시작 시너지 · 마왕성 체력 (사용자 지시, 2026-09-12)
- **소환사마다 시작 시너지 컨셉이 하나다** — 시작 카드가 그 시너지의 동 문턱을 연다(소속 7종 시너지 3장 · 6종 시너지 2장).
  정본은 `SummonerCreator` 로스터의 `Starters` 줄 주석
- ⚠ **진화체(1차·2차)를 시작 카드로 주면 해금 조건이 그 종족의 도감 등록을 요구해야 한다** (사용자 확정, 2026-09-15)
  - 도감은 빈 채로 시작하고 기본 종족만 카드 보상으로 열린다. 조건 없이 진화체를 주면 런 시작(`RunBootstrap`)이
    도감에 공짜로 올려 진화의 문이 우회된다. `SummonerCreator.VerifyStarterUnlocks` 가 굽는 순간 에러로 잡는다
  - 2차로 시작해도 된다 (슬라임 킹 = 슬라임 · 힐 슬라임 · **슬라임 킹**)
  - 로스터(= 선택 화면) 순서는 **해금이 쉬운 순**이다 — 기본 종족만 → 진화체 아무거나 1종 → 1차 하나 → 1차 둘 → 2차
  - ⚠ **두 번째 소환사(군악대장)는 "진화체 아무거나 1종"(`SummonerUnlockKind.UpgradeCount`)으로 열린다** (사용자 지적, 2026-09-15)
    — 첫 런은 견습의 슬라임을 키우는데 진화가 무작위라, 특정 1차를 요구하면 첫 진화가 아무 소환사도 안 열었다.
    한두 런 안에 다음 소환사가 열려야 한다
- **마왕성 체력 = 12 + 체력 × 9** (옛 30 + 6) — 체력 6 에서 66 으로 같고 양끝이 벌어진다(3 → 39 · 10 → 102).
  ⚠ 값이 **소환사 SO 에 직렬화**돼 있다 — `SummonerData` 기본값만 고치면 에셋은 옛 값을 쓴다
- **군악대장(`bandmaster`)** — 15번째 소환사 (사용자 지시, 2026-09-12). 개성 **쌍둥이 소집**(`TwinCall`) =
  카드를 낼 때 **옆 라인에 1마리가 공짜로** 선다 (`SummonController.UseMonsterCard` — 특성 '쌍둥이 라인' 바로 옆).
  ⚠ 특성과 다른 축이다 — 특성은 절반을 세우고 비용 +2 를 받지만, 이쪽은 1마리 고정에 값이 없다. 둘은 겹쳐서 얹힌다.
  ⚠ 격자가 15칸이라 **정확히 찼다** — 소환사를 더 넣으려면 `MainPanelCreator.ListRows` 부터 늘릴 것

### 전투 통계 — 딜 칸은 셋이다 (사용자 지시, 2026-09-12)
- 세그먼트 바 = **평타(파랑) · 상태이상(초록) · 스킬(주황)**. 가운데는 원작의 '병사' 칸 자리다
  (이 게임엔 아군 병사가 없어 늘 0 이었다) — 프리팹은 3칸을 이미 갖고 있어 다시 굽지 않아도 된다
- 판정은 `HitType.Dot` 하나다. 집계는 `CardStatsTracker.RecordDamage(attacker, amount, **HitType**, isKill)` ·
  칸은 `CardStatEntry.DotDamage` → `GeneralStatEntry.DotDamageDealt`
- ⚠ **도트는 시전 창(BeginCast)을 보지 않는다** — 도트는 걸린 뒤 몇 초에 걸쳐 들어와서, 그 사이에 쓴 다른 스킬의
  창으로 빨려 들어가면 엉뚱한 카드의 딜이 된다. 도트의 주인은 **건 개체**이고 엔티티로 정확히 찾을 수 있다

### 소환 경제 (마나 · 과부하 · 라인 복귀)
- **그릇·회복**: `Data/Sections/SummonManaData.cs` (Max / RegenForStage / SetMax)
  — 그릇 크기의 정본은 `InGame/Summon/SummonerData.cs` 의 `MaxMana`(지능 기반)
- **과부하**: `InGame/Summon/SummonCostRule.cs` — 비용·색(`ManaCostState`)의 정본.
  ⚠ 비용을 다른 데서 다시 계산하지 말 것. 소환 경로와 카드 표시가 같은 함수를 쓴다
- **라인 복귀**: `InGame/Summon/MonsterLineReturner.cs` — 적 전멸 시 생존자를
  필드에서 내리고 **제 라인 대기열(`SummonReservation`)에 한 마리로 다시 넣는다.**
  다음 스테이지가 시작되면 배출 루프가 저절로 뱉는다.
  ⚠ 분열체·부활체는 대기열에 넣지 않는다 (`returnable = false`) — 공짜 물량이 불어난다
  ⚠ **중첩 3단계(시너지 7개)의 '한 마리 더' 는 라인·종족마다 한 번이다**
    (사용자 지적, 2026-09-09) 개체마다 주면 살아남은 수가 판마다 두 배가 된다 —
    27스테이지에서 한 라인 대기열이 **200마리**를 넘었다. 지수로 붙는 보너스는
    시간이 지나면 그냥 고장이다
  ⚠ **대기열에는 상한이 없다. 어디에도 다시 걸지 말 것** (사용자 지시, 2026-09-12)
    옛 `SummonReservation.MaxPerLane`(40)을 **코드째 지웠다** (`capped` 인자도 함께).
    - **이 게임의 정체성이 물량이다.** 라인에 쌓인 것은 전부 마나를 내고 샀거나
      한 판을 버텨 내고 살아 돌아온 것이다. 수치 하나로 조용히 지우면 안 된다
    - **상한이 막으려던 원인은 이미 따로 고쳐졌다** — 27스테이지 200마리 사건은
      중첩 보너스가 *개체마다* 붙던 지수 버그였고, `(라인, 종족)마다 한 번` 으로
      바뀌며 사라졌다. 남은 증식 경로(귀향·숲 금·중첩)는 전부 **선형**이다.
      근본 원인이 고쳐진 뒤 이 상한은 아무 버그도 안 막고 정상 물량만 잘랐다
    - **두 번 물어뜯었고 둘 다 "조용히 사라진다" 였다** — 2026-09-11 배출 도중 판이
      끝나면 생존자 증발 · 2026-09-12 앱 재시작에 40 초과분 증발(48·47·46 → 전부 40,
      40 이하 라인만 멀쩡해 원인이 안 보였다)
    - ⚠ 기본값이 "자른다" 인 인자를 두지 말 것 — 부르는 쪽이 잊을 때마다 데이터가 샌다.
      **잊을 수 있는 안전장치는 안전장치가 아니다**
    - 대신 `WarnPerLane`(300)을 넘으면 라인마다 **한 번 경고만** 찍는다 —
      아무것도 버리지 않는다. 불어나는 것이 문제면 **불어나게 만든 규칙**을 고칠 것
    - 2026-09-11 에도 같은 모습이었다 — 그때는 복귀 경로만 `capped: false` 로 풀었다.
      산 개체가 제자리로 가는 것이라 수가 안 느는데, 상한에 걸려 배출 도중(줄이 긴 채로)
      판이 끝나면 **생존자가 대기열에 안 돌아오고 사라졌다.** 한 경로씩 푸는 방식이
      다음 구멍(이어하기 복원)을 남겼고, 그래서 2026-09-12 에 상한 자체를 지웠다
    ⚠ **카드 소환(`Enqueue`)에는 걸지 않는다** (사용자 지적, 2026-09-09) —
    마나를 이미 낸 것이다. 여기에 걸었더니 라인이 찬 뒤로 **마나만 내고 한 마리도
    안 서고**, 하단 카드의 숫자와 실제로 쌓이는 수가 갈렸다
  ⚠ 거둘 때 `MonsterDeathWatcher.Disarm()` 을 먼저 부른다 — **거두는 것은 죽는 것이 아니다.**
    지금은 `EntityLink` 가 엔티티를 파괴하지 않고 `Disabled` 만 붙여 저절로 걸러지지만,
    그 사실 하나에 기대지 않는다
  ⚠ 판정은 개체가 아니라 `SummonController.CheckWaveCleared` 한 곳이 한다.
    거두기 전에 배출 코루틴을 먼저 멈춰야 넣자마자 다시 나오지 않는다
  ⚠ **거둔 뒤에는 사망 훅이 아무것도 낳지 않는다** (사용자 지적, 2026-09-09)
    거두기는 상태가 아직 `InWave` 일 때 돌고, 그 순간 쓰러지는 중이던 개체는
    일부러 건너뛴다(사망 파이프라인에 맡긴다). 그 개체가 조금 뒤 분열체를 낳으면
    **거두기가 끝난 필드에 남아 다음 스테이지까지 따라간다** — "가끔 분열 슬라임이
    남아 있다" 가 이것이었다. `MonsterDeathWatcher` 가 `SummonController.WaveSwept`
    를 보고 통째로 빠져나간다. 상태(`InWave`)만으로는 못 잡는다 — 상태가 내려오는
    것은 `BattleManager` 가 승리를 알아차린 **다음 프레임**이라 그 사이가 창이다
- **배출 속도**: `InGame/Summon/SpawnPaceRule.cs` — **등급 다섯** (사용자 확정, 2026-09-07)
  - 힘지수 = `MaxHp + Attack×6` → 경계 `140 / 190 / 250 / 330` 으로 0~4 등급
  - 간격 `0.25 / 0.50 / 0.75 / 1.00 / 1.50` · 공·체 보너스 `+0 / +20 / +40 / +60 / +80%`
  - ⚠ **폭이 커야 체감된다** (사용자 지적, 2026-09-07) — 0.25~0.65 였을 때는 화면에서
    둘 다 "우르르 나온다" 로 보였다. 지금은 6배 차이라 트롤이 성문에서 한 기씩 걸어 나온다
  - ⚠ **간격과 보너스는 한 묶음이다** — 마나당 값어치로 맞춘 값이다.
    보너스 없던 트롤은 마나 12에 체력 780(65/마나), 슬라임은 5에 640(128/마나)이라
    느린 데다 마나당 절반이었다. +80%면 117/마나로 나란해지고 공격력은 앞선다
  - ⚠ **느린 만큼 세다** — 보너스가 없으면 "빠른 놈만 쓰기" 가 언제나 정답이라 축이 죽는다.
    곱하는 곳은 `MonsterStatComposer` ② **한 곳뿐**이고, 도감은 `StatMultiplierFor` 를 그대로 쓴다
  - ⚠ `PowerOf` 는 **SO 원본값**만 본다 — 보너스가 지수로 되먹임되면 트롤이 무한히 느려진다
  - ⚠ 세 배열(`Thresholds`·`Intervals`·`StatBonuses`)은 한 묶음이다. 경계만 하나 짧다(등급 5 = 경계 4)
  - ⚠ 마릿수는 넣지 않는다 — 한 마리당 간격이라 저절로 곱해진다. 또 넣으면 물량 종족이 두 번 벌받는다.
    지금 값이면 한 무리를 다 세우는 데 1.35~2.20초 (숲의 트롤만 9마리라 3.15초)
  - **적는 곳은 몬스터 상세의 `소환 간격` 행 하나다.** 문구는 `SpawnPaceRule.DescribeFor`
    가 만든다 — 손으로 적지 말 것
    ⚠ **공·체 보너스는 안 적는다** (사용자 지시, 2026-09-10) — 그 창의 체력·공격력
    행이 **이미 곱해진 값**이라(`StatMultiplierFor`) 같은 것을 숫자로 한 번, 비율로
    한 번 두 번 말하게 된다. 이 행은 간격만 말한다
  - ⚠ **카드 3택에서는 뺐다** (사용자 지시, 2026-09-07). 한때 `DescribeSpecies` 가
    함께 적었는데, 3택 카드는 **고를지 말지**를 정하는 자리라 마나·마릿수·이름·
    공격·체력·개성 칩이 이미 들어차 있다. 거기에 규칙 설명까지 얹으면 무엇을 보고
    고르는지가 흐려진다
  - ⚠ 특성(과잉 소환·선발대)은 **곱하는 배율**이다 (`RunPerkRule.DrainMultiplierFor`).
    거기서 초를 돌려주면 특성을 켠 순간 모든 종족이 같은 속도가 된다
  - ⚠ 360° 표시의 분모는 `SummonController._laneInterval`(지금 기다리는 그 간격)이다.
    고정값으로 나누면 트롤 차례에 고리가 반만 돌고 슬라임 차례엔 되감긴다
- **빠른 종족은 체력 보너스를 조금 받는다** (사용자 지시, 2026-09-15) — 정본 `SpeedHpRule`(SpawnPaceRule.cs 끝)
  - 이동속도 2.5 초과분 1 당 체력 +12%, 상한 +25% (늑대 4.2 → +20%). **SO 원본 이속**으로 판정
  - 이유: 먼저 닿아 점사를 받고, 개체 DPS 가 높아 한 마리 손실이 크다 (슬라임은 반대로 피해가 흩어진다)
  - ⚠ 체력에만 준다 — 공격력을 올리면 한 마리가 죽을 때 잃는 딜이 더 커진다
  - 곱하는 곳 둘: `MonsterStatComposer` ②(전투) · `MonsterDetailPopup` 체력 행(도감)
- **줄 서는 순서 = 덱 순서** (사용자 확정, 2026-09-07) — 누른 순서가 아니다
  - 하단 카드 바를 **끌어 옮겨** 바꾼다 (`SummonCardUI` 드래그 → `SummonDeckUI` → `SummonDeckData.Move`)
  - ⚠ `SummonCardUI.ShowCard/ShowEmpty` 는 선택 표시를 끈다 — `SummonDeckUI.RefreshCards` 끝에서 `_selectedSlot` 으로 다시 켠다
    (2026-09-17 버그: 카드를 내면 덱 `Changed` 로 다시 그려져 선택이 풀린 것처럼 보였는데 실제 선택은 남아 있었다)
  - ⚠ 꺼낼 때 고르지 않고 **넣을 때 자리를 잡는다** (`SummonReservation.InsertIndex`) —
    꺼낼 때 고르면 화면의 대기열은 넣은 순서로 보여 보이는 줄과 나오는 줄이 갈린다
  - ⚠ 순서를 바꾸면 이미 선 줄도 다시 세운다 (`SummonReservation.Resort`)
  - ⚠ 덱에 없는 종족(진화로 사라진 카드의 예약)은 맨 뒤다
  - ⚠ 끌고 있는 동안 `Move` 를 부르지 말 것 — 카드 칸은 인덱스에 묶여 있어
    손가락에 붙은 그 칸이 다른 카드로 다시 그려진다. 손을 뗄 때 한 번만 옮긴다
  - ⚠ 드래그 뒤 클릭 한 번을 삼킨다 (`SummonCardUI._swallowClick`) — 안 그러면 옮길 때마다 카드가 선택된다
- **소모 경로**: `InGame/Summon/SummonController.cs` `UseMonsterCard`/`UseSkillCard`
  — 할인(개성) → 과부하 → `MarkUsed` 순서. ⚠ 마나를 실제로 낸 뒤에 센다
- **대기열 저장**: `Data/Sections/SummonQueueData.cs` (SaveKey 22) —
  예약은 **선불**이라 저장하지 않으면 앱을 껐다 켠 순간 마나만 사라진다.
  저장 지점은 **셋**이다 (`SummonController.PersistQueue`) —
  ① 스테이지 시작(배출 열기 전) · ② 적 전멸 직후(생존자를 거둔 뒤) ·
  ③ **대기 중 예약이 바뀔 때마다** (`HandleReservationChanged`, 2026-09-12 추가).
  이어하기면 `RunBootstrap.StartRun` 이 `RestoreQueue`, 새 런이면 `ClearQueue`
  - ⚠ ③ 은 **`IsStageReady` 일 때만** 받아 적는다 (사용자 지적 — "대기열 저장이 안 된다")
    경계 둘뿐이던 시절, 정작 플레이어가 줄을 세우는 **대기 구간**이 통째로
    안 저장됐다. 자동 시작이 꺼져 있어(`_readySeconds` 0) 그 구간은 [시작]을
    누를 때까지 무한정 이어지므로, 거기서 앱을 껐다 켜면 걸어 둔 예약이 사라졌다
  - ⚠ **전투 중에는 여전히 안 적는다** — 배출 루프가 라인당 0.15초마다
    `Reservation.Changed` 를 쏜다. 거기까지 받으면 한 판에 수백 번, 그것도
    `RequestSave` 는 전 섹션을 다시 쓴다. 대기 중에는 배출이 안 돌아
    이 사건이 **플레이어가 카드를 낼 때만** 온다
  - ⚠ 구독을 해지하지 않는다 — 예약 목록은 `SummonController` 가 소유하고 함께 죽는다
- **초기화 지점**: `RunBootstrap.StartRun`(런 시작) · `AdvanceStage`(스테이지 클리어)
- **카드 표시**: `InGame/UI/SummonCardUI.cs` — 색·번쩍임. 게이지는 `SummonDeckUI`

### 런 특성 (RunPerk) — 얻는 순간 효과가 서야 한다
- ⚠ '확장 편성' 은 `RunPerkData.Add` 가 **그 자리에서** 덱 칸을 늘린다 (2026-09-11) —
  칸을 늘리는 곳이 런 시작(`BuildStarterDeck`)뿐이라 런 도중에 얻으면 앱을 껐다 켜야 8칸이 됐다
- 설명(`RunPerk.Describe`)은 "언제 · 무엇이 · 얼마나" 순. 소환할 때 굳는 효과는 그렇다고 적는다

### 런 특성 (RunPerk) — 화면 표시
- **아이콘**: `InGame/Summon/Editor/RunPerkIconGenerator.cs` → `아이콘·텍스처 > 특성 아이콘`,
  경로 정본 `Editor/RunPerkIconAssets.cs` (`3.Textures/Icons/RunPerks/perk_<Enum>.png`)
  - ⚠ **다른 아이콘과 조회 방식이 다르다** — 종족 패시브·시너지는 Creator 가 `Sprite[]` 를
    프리팹에 박지만, 특성은 30종 중 무엇이 뜰지 런타임에 정해져 **아틀라스 키 조회**다
    (`RunPerkIconKey.Of` → `SpriteManager.Get`). 난이도 아이콘과 같은 방식
  - ⚠ 그래서 `SpriteManagerCreator` 의 **특성 아틀라스 폴더 목록에 `RunPerks` 가 있어야 한다.**
    빠지면 전부 빈 그림이 된다. 구운 뒤 `데이터 생성 > SpriteManager + 아틀라스` 를 반드시 실행
  - 특성을 추가하면 `RunPerkIconGenerator.Table` 에도 넣고 다시 구울 것 (`Verify` 가 잡아 준다)
- **보유 특성 줄**: `InGame/UI/RunPerkBarUI.cs` — 상단바 왼쪽.
  소환사 개성 1 → 주운 특성 → **제단이 얹은 표식**(`RunBoonData`) 순
  - ⚠ **소환사는 HUD 보다 늦게 선다** (사용자 지적, 2026-09-07) —
    `RunBootstrap` 이 BuildStarterDeck → SpawnSummoner 순이라 첫 그리기 때
    `SummonerRuntimeBridge.Current` 가 null 이다. 이벤트로만 갱신하면 **개성 칸이 영영 빈다.**
    `Update` 에서 소환사·개성·제단 표식 셋을 지켜본다 (`SummonerSkillButtonUI` 와 같은 이유)
  - ⚠ 매 프레임 `Refresh` 를 부르지 말 것 — `SetupCustom` 이 툴팁 리스너를 갈아 끼운다
  - ⚠ **원작 `TraitBarUI` 를 걷어낸 자리다** (사용자 지적, 2026-09-07) — 그쪽은
    `RunTraitData`·`JobSynergyEvaluator`(원작 축)를 읽어 **언제나 비어 있었다**.
    화면에는 "보유 특성을 볼 곳이 없다" 로 보였다
  - 칸은 `TraitIconUI.SetupCustom` 을 빌려 쓴다 (아이콘 + 눌러서 툴팁이 이미 되어 있다)
  - 칸 검산 — 넉넉할 때 `72 × 14열 = 1086`, 빽빽할 때 `48 × 20열 = 1074 ≤ 1086`,
    2줄 40칸 ≥ 39(개성 1 + 특성 30 + 제단 표식 8). 굽는 순간 `VerifyPerkBar` 가 본다
    (자세한 것은 위 'HUD 자리' 항목의 두 모습 표)
- **선택 화면**: `ChoicePopup` 줄에 **왼쪽 그림 칸**이 있다 (`Entry.Icon`)
  - 특성은 `SpriteManager`(perk_*), 강화소·제단의 카드 줄은 몬스터 초상화를 넘긴다
  - ⚠ 그림이 없는 줄에서도 글자 시작점은 그대로다 — 줄마다 달라지면 목록이 들쭉날쭉해 보인다
- **소환사 개성 설명 정본**: `SummonerPerkNames.Describe(perk, value)`
  — 한때 `SummonerCandidateCardUI` 의 private 함수였다. 인게임 줄이 같은 문장을 쓴다

### 카드 3택 후보 — 진화체는 도감 해금이 문이다
- **정본**: `CardRewardPicker.BuildPool`
- ⚠ **규칙이 바뀌었다** (사용자 확정, 2026-09-07)
  | | 2026-09-07 이전 | 지금 (2026-09-12) |
  |---|---|---|
  | 업그레이드 종족 | 후보에서 **통째로 제외** | 도감 해금 **+ 베이스를 손에 들었을 때**만, 그것도 **'진화' 선택지**로 |
  - 처음 얻는 길은 **여전히 진화 하나뿐**이다 — 그래야 만렙까지 키우는 과정이 값을 한다
  - 한 번 진화로 손에 넣어 본 종족은 이후 런의 보상에 섞인다.
    → **도감을 채우는 일이 다음 런의 선택지를 넓히는 일**이 된다
  - ⚠ 뿌리 종족은 도감과 무관하다. 해금으로 뿌리까지 막으면 첫 런에 카드가 셋뿐이다
- ⚠ **아무 일도 못 하는 카드는 후보에서 뺀다** — 만렙인데 진화도 융합도 막힌 카드는
  골라도 창이 안 뜨고 그냥 넘어간다(보상 한 번을 버린다). 진화체가 후보에 들어오면서
  이 상태가 흔해졌다 — **진화한 카드는 다시 진화하지 못한다**
### 진화체는 **베이스를 들고 있을 때 '진화' 선택지로** 나온다 (사용자 지시, 2026-09-12)
> 해금된 진화체가 그냥 새 카드로 뜨는 것이 불합리했다 — 힐 슬라임을 **줍는 것**과
> 슬라임을 힐 슬라임으로 **키우는 것**은 다른 일인데 화면에서는 둘 다 "새 카드" 였다.

- **문이 둘이고 순서가 있다** (사용자 확정)
  | 차례 | 무엇 | 조건 |
  |---|---|---|
  | ① 처음 얻기 | **만렙 랜덤 진화** (`CardEvolveUI`) — 이걸로 **도감이 열린다** | 베이스가 만렙 |
  | ② 그 뒤로 | 3택에 **"○○ 진화"** 카드가 섞인다 — 고르면 그 베이스 칸이 바뀐다 | 도감 해금 + 베이스 보유 (**레벨 무관**) |
- ⚠ **무작위 진화는 이미 도감에 오른 종족을 빼고 굴린다** (사용자 지시, 2026-09-12)
  `CardEvolution.CollectUpgrades` 가 `MonsterCodexData.IsUnlocked` 를 함께 본다.
  안 빼면 만렙까지 키워 얻은 **단 한 번의 무작위**가 이미 가진 종족으로 굴러 통째로 낭비된다.
  두 문이 서로 다른 것을 주게 갈라 둔 것이다 —
  **무작위는 못 본 것을 열고, 진화 카드는 아는 것을 고른다**
  - ⚠ 계보를 다 연 종족은 후보가 비어 **진화 갈래가 통째로 감춰진다**(`CanEvolve` false →
    `CardEvolveUI.StackBranches`). 그게 맞다 — 무작위가 더 줄 것이 없고 융합·강화가 남는다
  - ⚠ 도감은 **영구**라(환생 무관) 이 필터는 런이 거듭될수록 좁아진다 — 의도한 흐름이다
  - ⚠ `CollectUpgrades` 를 쓰는 셋(`CanEvolve` · `RollUpgrade` · 창의 "무작위 N종" 문구)이
    같은 함수를 지나 저절로 맞는다. 필터를 호출처에 흩지 말 것
- 판정 정본은 `CardEvolution.CanEvolveTo(deck, slotIndex, target)` 하나다 —
  후보를 고르는 `CardRewardPicker` 와 실제로 바꾸는 `SummonDeckData.EvolveTo` 가 함께 본다
  - ⚠ `CanEvolve`(만렙 문)와 **조건이 다르다. 합치지 말 것** — 둘은 서로 다른 문이다
  - 나머지 규칙은 그대로다 — `HasEvolved`·`HasFused` 면 막히고, 같은 종족이 두 칸이 되지 않는다
- 결과는 무작위 진화와 **똑같다** — Lv1 로 되돌아가고 부모 레벨 보너스의 절반이 `InheritBonus` 로 얹힌다
  - ⚠ 카드에 "Lv.1 부터 다시" 를 **반드시 적는다** (`DescribeGain`) — 키운 카드를
    진화시키는 것은 되돌릴 수 없는 선택이라 고르기 전에 알아야 한다
- ⚠ **이미 손에 든 진화체는 이 갈래를 안 탄다** — 보통 경로로 내려가 중복(레벨업) 후보가 된다.
  안 그러면 한 번 얻은 진화체를 영영 키울 수 없다
- ⚠ **칸(`hasRoom`)을 보지 않는다** — 새 칸을 먹지 않고 있는 칸을 바꾼다
- ⚠ `RunBootstrap.OnRewardPicked` 는 `IsEvolve` 를 **`Acquire` 보다 먼저** 가로챈다.
  Acquire 로 내려보내면 베이스를 남긴 채 진화체가 새 카드로 한 칸 더 들어온다 — 이 규칙의 정반대다
- ⚠ **상점에서는 뺀다** (`RunShopRule.RollCards`, 시너지 강화와 같은 자리) —
  상점은 "사서 칸에 넣는" 곳이고 진화는 "있는 칸을 바꾸는" 것이라 문법이 다르다.
  `ShopPopup.BuyCard` 가 `Acquire` 를 쓰므로 그냥 두면 위와 같은 사고가 난다
- 화면: `StateText` = "진화" · `DescText` 는 **비운다**(설명과 결론이 같은 사각형이다)

- ⚠ **카드 3택의 마나·마릿수·공격력·체력은 런 합성 값이다** (사용자 지시, 2026-09-15) — 몬스터 상세(런 모드)와
  같은 `MonsterStatComposer.Compose`(도감 품질 · 덱 칸의 계승분)를 지난다. 레벨이 오르는 카드는 `지금 › 고른 뒤`.
  예전엔 종족 원본 × 레벨 비율이라 시너지·특성·장비·레벨 표가 빠져 슬라임 킹이 체력 6000 으로 떴다.
  소환사가 없을 때(런 밖)만 옛 종족 값으로 떨어진다
- ⚠ **만렙 카드에는 초록 "+N" 을 적지 않는다** (사용자 지적, 2026-09-12)
  `CardSelectPopup.StatWithGain` 의 증가분은 레벨로 **이미 붙어 있는** 몫이지
  이번에 고르면 붙는 몫이 아니다. 만렙 카드를 고르면 레벨은 그대로고 진화·융합
  갈림길이 열릴 뿐인데, 그 줄이 `+38` 을 달고 있으면 "진화를 누르면 공·체가 오른다"
  로 읽힌다 — 실제로는 오르지 않는다. `IsMaxed` 면 레벨 몫까지 더한 **지금 값** 하나만 적는다

### 진화체는 반드시 부모보다 세야 한다
> ⚠ **개체 스탯만 보고 짜지 말 것 — 마릿수와 마나가 같이 정한다.**

- 판정식(정본): `MonsterCodexCreator.IndexOf` =
  `(마나당 총 체력) × (마나당 총 공격력)`, 소환력 8 기준.
  진화체는 부모의 **×1.15 이상**이어야 한다 (`MinUpgradeRatio`)
  - 곱으로 잡는 이유는 **종족의 성격을 지키기 위해서**다. 탱커는 체력으로,
    딜러는 공격력으로 채우면 된다 — 한쪽만 보고 억지로 맞추지 않아도 된다
- ⚠ **13종 중 12종이 부모보다 약했다** (사용자 지적, 2026-09-07)
  힐 슬라임은 슬라임의 **0.29배**였다. 개체는 더 셌지만(90 vs 80)
  마나가 5→7 로 오르고 마릿수가 8→6 으로 줄어 마나당 전투력이 3분의 1 토막 났다.
  화면에서는 "진화했더니 더 약해졌다" 로만 보인다
  - 마나·마릿수는 그대로 두고 **기본 스탯을 올려** 전부 ×1.20 으로 맞췄다.
    마나를 내리는 쪽은 "상위 카드가 더 싸다" 가 되어 버린다
  - 숲의 트롤(×2.94)만 원래 조건을 만족해 손대지 않았다
- `MonsterCodexCreator.VerifyEvolutionsAreUpgrades` 가 굽는 순간 검사해 **에러를 낸다.**
  ⚠ 마나·마릿수·스탯 셋은 한 묶음이다 — 하나를 바꾸면 나머지를 다시 잡을 것

### 진화 = **레벨 초기화 + 영구 계승** (사용자 확정, 2026-09-09)
- 진화하면 카드가 **Lv1 로 돌아간다** (`SummonDeckData.Evolve`).
  전에는 만렙을 그대로 물려받아, 진화한 그 판에 또 만렙이라 **곧바로 진화·융합 창이
  다시 떴다** — 키우는 과정이 통째로 건너뛰어졌다
- 대신 부모가 쌓아 둔 레벨 보너스의 **절반**이 영구히 얹힌다
  (`SummonDeckSlot.InheritBonus`, 비율은 `CardEvolution.EvolveInheritShare`)
  - Lv5(+48%)의 절반 = **+24%** 가 Lv1 부터. 다시 만렙까지 키우면 +72% —
    주워서 키운 카드(+48%)보다 확실히 세다. **진화체는 더 강해야 한다**가 규칙이다
  - ⚠ 종족 표(`LevelBonuses`)는 물려받지 않는다 — 그건 새 종족의 것이다
  - ⚠ 적용은 `MonsterStatComposer` ④ **한 곳뿐**이다(레벨 비율과 같은 자리·같은 순서).
    종족 표보다 앞이라야 표의 비율 항목이 그 위에서 곱해진다
  - ⚠ 세이브 필드가 늘었다 (`inherit`) — 옛 세이브는 0 이라 그대로 돈다

### 융합 = 패시브 + **재료 레벨의 덤** (사용자 확정, 2026-09-10)
- 재료가 쌓아 둔 레벨 보너스의 `CardEvolution.FuseInheritShare`(**0.10**)가 대상에게
  영구히 얹힌다. Lv5 재료(+48%) → **+5%**. Lv1 재료는 0 이다
- ⚠ **진화와 같은 칸에 쌓인다** (`SummonDeckSlot.InheritBonus`) — 적용도
  `MonsterStatComposer` ④ 한 곳뿐이다. 새 축을 만들지 말 것
- ⚠ 전에는 재료의 레벨이 통째로 사라져서 **재료로는 언제나 Lv1 잡카드를 쓰는 것이
  정답**이었다. 고를 수 있는데 고르면 손해인 선택지는 선택지가 아니다
- ⚠ **덤이지 본상이 아니다** (0.35 → 0.10, 사용자 지시) — 융합은 종족 패시브를 이미
  주고, 대상은 제 만렙까지 그대로 유지한다(진화는 Lv1 로 되돌아간다). 크게 주면
  "만렙끼리 먹이기" 가 다른 성장을 전부 덮는다. 3회를 다 채워도 +15% 아래다
- ⚠ **융합 창에 반드시 적는다** (`CardEvolveUI.BindMaterials`) — 안 적으면 화면에서는
  여전히 "Lv1 을 먹이는 게 이득" 으로만 보인다. 값은 `CardEvolution.FuseInheritFrom`
  하나가 만든다 (표시·적용이 같은 함수)

### 카드 3택 가중치 — 만렙·융합 카드는 드물게 (사용자 지적, 2026-09-09)
> 값을 전부 **4배**로 잡아 두었다. 풀이 "같은 항목을 N번 넣는" 방식이라 가중치가 곧
> 정수 개수다 — 기본이 2 면 그보다 드물게 만들 자리가 1 하나뿐이다.

| 카드 | 가중치 |
|---|---|
| 보통 (레벨이 오른다) | 8 (+친화 12) |
| 만렙 — 진화·융합만 남음 | 4 |
| **이미 융합한 만렙** | **1** |
- 융합을 한 번 한 카드가 계속 3택을 채웠다. 그 카드가 할 수 있는 일은 "재료를 하나 더
  먹는 것" 뿐이고 그건 이미 한 선택이다. 아예 빼지는 않는다 — 융합 슬롯이 셋이라
  끝까지 채우려는 사람의 길은 남긴다
- ⚠ **친화 가중치는 레벨이 오르는 카드에만** 얹는다. 만렙에도 얹으면 친화 종족이
  융합한 순간 1+12=13 이 되어 "아주 드물게" 가 통째로 무효가 된다

### 진화·융합 — 고를 수 있으면 반드시 되어야 한다 (2026-09-09)
- ⚠ **이미 덱에 있는 종족으로는 진화하지 않는다** (`CardEvolution.CollectUpgrades`)
  독 슬라임을 든 채로 슬라임을 진화시키면 또 독 슬라임이 나왔다. 같은 Id 가 두 칸이면
  `IndexOf` 가 앞 칸만 찾아 레벨·강화·시너지가 한쪽에만 걸린다.
  후보가 전부 덱에 있으면 목록이 비고 → `CanEvolve` 가 false → 진화 갈래가 흐려진다
- ⚠ **넘겨줄 개성이 없는 카드는 재료가 아니다** (`CardEvolution.CanBeMaterial`)
  `SummonDeckData.Fuse` 는 `TryLearn` 이 실패하면 **아무 일도 없이** false 를 돌려준다 —
  화면에서는 "골랐는데 융합이 안 된다" 로만 보였다(두 번째 융합부터 흔하다).
  판정을 재료 목록 쪽으로 끌어올렸다

### 강화소 — 카드당 각인·증식 **각각 한 번** (사용자 확정, 2026-09-09)
- 이미 새긴 쪽은 **누를 수 없게** 한다(목록에서 지우지 않는다 — 무엇을 했는지가 사라진다)
- 전에는 무제한이라 골드만 있으면 한 장을 −8 마나까지 깎을 수 있었다.
  그러면 강화소가 "어느 카드를 두껍게 할까" 가 아니라 "한 장에 몰아넣기" 가 된다

### ⚠ 소환사 개성과 같은 것을 주는 특성은 후보에서 뺀다 (2026-09-09)
- 정본은 `RunPerkData.ShadowedByPerk` — 견습(친화 할인)이 특성 '친화 할인' 을 또 받아
  할인이 두 번 겹쳤다. 같은 축을 두 번 쥐여 주는 것은 선택지가 아니다
- ⚠ 축이 같아도 **대상이 다르면** 겹쳐도 된다(개성=친화 종족, 특성=전 종족).
  막을 것은 같은 대상에 같은 값이 두 번 붙는 경우다
- **표에 든 짝은 둘이다** — 친화 할인(견습) · **심연 공명(리치, 2026-09-18 추가)**
  - 심연 공명은 이름·축·대상이 전부 같은데 **적용 지점이 둘로 갈려 있어** 안 보였다 —
    개성은 `MonsterStatComposer` ⓪ 에서 친화 배율에 `PerkValue`(1.6)를 한 번 더 곱하고,
    특성은 `RunPerkRule.AffinityMultFor` 가 배율 자체를 1.2 → 1.6 으로 갈아 끼운다.
    둘을 함께 쥐면 `1.6 × 1.6 = ×2.56` 으로 기본(×1.2)의 2.1배가 됐다
  - ⚠ **같은 효과가 두 enum 에 있으면 이 표를 먼저 볼 것** (`SummonerPerk` · `RunPerk`).
    이름이 같다고 자동으로 걸러지지 않는다 — 이 표에 적어야 걸러진다
- ⚠ 거르는 자리는 `CollectMissing` 하나다 — 3택·상점·이벤트·유물 '오래된 계약' 이
  전부 `RunPerkPicker.Pick` 을 지나므로 여기만 막으면 된다. 새 획득 경로를 만들면 그 길을 탈 것
- ⚠ **이미 둘 다 쥔 세이브는 그대로다** — 후보에서 빼는 것이지 가진 것을 뺏지 않는다

### ⚠ 카드의 마릿수는 실제 소환식과 같은 것을 쓴다 (2026-09-09)
- `SummonCardUI.SummonCountOf` = `RunPerkRule.SummonCountFor + slot.ExtraSummons` —
  `SummonController` 가 쓰는 식 그대로다. 종족 기본값만 적어 두어 **강화소에서 +1 을
  새겨도 카드 숫자가 그대로**였다
- ⚠ `RefreshCost` 에서도 다시 잰다 — 소환사는 카드보다 늦게 서고 특성은 판 도중에 붙는다

### ⚠ 도감 해금은 **부르는 쪽의 몫**이다
- `SummonDeckData.Evolve` / `Acquire` 는 칸만 바꾼다. `MonsterCodexData.Unlock` 은
  부르는 쪽이 따로 부른다 — `RunBootstrap`(시작 덱·카드 보상)과
  `CardEvolveUI.HandleEvolve`(진화) 셋이다
- ⚠ 진화 경로만 그 줄이 빠져 있었다 (사용자 지적, 2026-09-07) —
  손에 든 종족이 **도감에는 없는** 상태가 되고 품질 개선도 못 했다.
  새 해금 경로를 만들면 이 줄을 반드시 함께 넣을 것

### 시설 화면 — 네 화면이 **같은 전체화면 무대**를 쓴다
> ⚠ 무대 정본: `UI/Popup/Editor/FacilityStage.cs`

- 야영지·강화소·제단·**상점** 넷이 같은 무대를 쓴다 (사용자 요청, 2026-09-07)
  - 배경 그림이 **화면 전체**를 덮고, 위아래 **어둠 판** 위에만 글이 앉는다
  - 가운데는 그림이 그대로 보인다 — 그래야 "창" 이 아니라 "장소" 로 읽힌다
- ⚠ **예전엔 가운데 패널 + 머리에 그림 띠** 였다. 그러면 네 시설이 전부 같은
  회색 창으로 보이고, 어디에 들렀는지가 안 남는다
- ⚠ 그림 위에 글을 바로 얹지 않는다 (UI 규칙 8) — 그림이 시설마다 밝기가 달라
  글자색으로는 대비를 못 잡는다. **어둠 판을 깔고 그 위에만** 글을 놓는다
- ⚠ 무대를 고치면 **넷이 같이 바뀐다.** 한 화면만 다르게 하고 싶으면
  그 Creator 에서 `content` 안에 따로 짓는다 — `FacilityStage` 를 갈래로 나누지 말 것

### 시설 화면의 표기 규칙 — 아이콘이 정본이다 (사용자 지적, 2026-09-09)
> 강화소·제단·야영지·이벤트·상점이 **같은 규칙**을 쓴다. 한 화면만 다르면
> 같은 값을 화면마다 다시 읽어야 한다.

- **지갑은 오른쪽 위 금화 배지 하나** (`FacilityStage.PurseBadge`) — 제목 줄에
  골드를 적지 않는다. `"강화소 — 70 G (보유 6020 G)"` 는 한 줄에 숫자가 둘이라
  **둘 다 안 읽혔다**
- **낼 값은 버튼 안**에 [금화][70] 로 (몬스터 상세의 품질 개선과 같은 규칙).
  못 내면 숫자가 **붉게** — 버튼이 왜 잠겼는지가 그 자리에서 읽힌다
- **효과도 아이콘이다** — `"각인 −3 마나"` 가 아니라 [각인] [물방울]−3,
  `"+3 마리"` 가 아니라 [해골]+3 (UI 규칙 7)
- ⚠ **아이콘은 글자보다 크게** (사용자 지적, 2026-09-09) — 글자를 대신하는 그림이라
  작으면 무슨 그림인지 못 알아본다. 카드 칸 36 · 버튼 효과 56 · 버튼 값 46 · 지갑 52
- ⚠ **버튼 안에 설명 글을 넣지 않는다** — "소환 비용이 영구히 줄어든다" 는
  버튼을 넘겨 삐져나왔고 정작 읽어야 할 숫자를 밀어냈다
- 이야기 한 줄(`RunNodeFlavor`)은 **제목 아래 작게·흐리게 한 줄**이다.
  제목 크기로 두 줄을 차지하면 화면을 여는 순간 분위기 글이 먼저 읽힌다
- ⚠ 런타임 파일은 `FacilityStage`(에디터 전용)의 색을 못 쓴다 — 금색·부족색을
  각 팝업이 제 상수로 들고 있다. **바꿀 때 함께 바꿀 것**

### 고르는 화면은 **세로 카드**다 (사용자 지시, 2026-09-10)
> 로그라이트에서 하나를 골라 찍는 자리는 어디서나 나란히 선 카드다.
> 가로로 긴 줄 목록은 설정 메뉴·상점 재고의 모습이라 "고르는 자리" 로 안 읽힌다.

- **특성 선택 (`ChoicePopup`)** — 가로 줄 여덟 → **세로 카드**(284×640, 최대 5장)
  - 카드 안: [그림 156] [이름 FontMd] [구분선] [설명] — 전부 가운데 정렬
  - ⚠ 옛 주석의 "그림이 없고 문장 한 줄이라 가로가 낫다" 는 전제가 깨졌다 —
    2026-09-07 에 그림 칸이 생겼고 모든 줄에 아이콘이 있다
  - ⚠ **쓰는 곳은 특성 선택 하나뿐이다** — 갈림길은 `CrossroadUI`, 시설·이벤트는
    `FacilityPopup`, 강화소·제단은 `CardPickPopupBase` 로 각자 빠져나갔다.
    `PopupType.Choice` 를 여는 곳은 `RunBootstrap.OfferPerk` 뿐이라 곁가지가 없다
  - ⚠ Creator 는 8칸을 굽고 **런타임이 가운데로 다시 세운다**(`ChoicePopup.Recenter`).
    실제로 뜨는 것은 1(엘리트)·3(보스)·최대 5(유물 '전승의 대가' 2단계)다.
    `_cardStep` 의 정본은 Creator다
  - ⚠ `VerifyChoiceCards` 가 굽는 순간 넷을 본다 — 5장 가로 · 세로 · `PopupMaxH` ·
    **가장 긴 특성 설명이 카드에 들어가는가**(56자 예산 ≥ 40자)
- **유물 상세 (`RelicTreePopup`)** — 760×380 가로 상자 → **480×680 세로 카드**
  - **그림이 생겼다** — 트리 노드와 **같은 키**(`RelicIconKey`)로 찾아 크게 얹는다.
    지금까지 이 창에는 무엇을 찍는지 그림이 한 번도 안 나왔다
  - ⚠ 자리를 **오른쪽 아래**로 옮겼다 — 왼쪽 아래는 뿌리 노드가 서는 자리라
    상세를 열면 정작 방금 누른 노드가 가려졌다
  - ⚠ 효과 칸은 남는 세로를 전부 먹는다(고정 높이 금지). `VerifyTooltip` 이
    **세 줄이 들어가는지**를 잰다 — 여러 스탯 노드가 세 줄까지 나온다
  - ⚠ 카드를 더 좁히지 말 것 — 스탯 노드는 `[아군 몬스터]` 같은 대상 줄이 먼저
    오는데, 그게 두 줄로 접히면 카드가 갑자기 길어 보인다
- ⚠ **둘 다 프리팹을 다시 구워야 한다** —
  `프리팹 생성 > 팝업 > ▶ 런 팝업`(특성) · `RelicTree`(유물) →
  `PopupManager [Load Popup Prefabs]`

### 강화소·제단 (CardPickPopupBase)
- **덱 8칸이 한 줄**이다 (`CardPickPopupCreator`) — 4열이던 때는 다섯 번째부터
  아래로 접혔는데, 격자 높이(254)가 칸 높이(300)보다 낮아 **둘째 줄이 통째로
  잘려 보이지 않았다**
  - ⚠ 폭 검산: `8 × 208 + 7 × 14 = 1762 ≤ 1824`(1920 − 여백 48×2).
    넘치면 `GridLayoutGroup` 이 조용히 다음 줄로 접는다 — 에러가 안 난다
  - ⚠ 세로 검산: 행동 200 + 16 + 카드 286 = 502 ≤ `BottomVeilH`(560).
    어둠 판 높이는 네 시설이 함께 쓰므로 못 늘린다
- 칸 아래 한 줄의 입주자는 **셋이고 하나만 켜진다** (`CardCell`, 기본은 전부 꺼짐)
  | 화면 | 그 줄에 | 필드 |
  |---|---|---|
  | 강화소 | 마나·마릿수 배지 (무엇을 새겼나) | `ManaBadge`/`CountBadge` |
  | 제단 | **시너지 표식 아이콘** (2026-09-18) | `TagRow`/`TagRoots` |
  | 표식 없는 종족 | 글 "표식 없음" | `StateText` |
- **제단의 표식은 아이콘이다** (사용자 요청, 2026-09-18) — 옛 `DescribeCell` 은 "숲 · 야수" 로 적었다
  - 켜는 것은 `ShowsTagRow`(제단만 true) 하나, 그리는 것은 `CardPickPopupBase.FillTagRow` 하나다
  - 아이콘에 올리거나(PC) 누르면(모바일) 동·은·금 효과가 툴팁으로 뜬다 — `SynergyChipUI` 를 그대로 쓴다
  - 켜진 표식은 밝게·아직인 것은 흐리게 (`DimTag`). ⚠ 단계 색으로 물들이지 말 것 —
    아이콘이 저마다 제 색을 갖고 있어 회색으로 덮으면 여덟 개가 다 같아 보인다
  - ⚠ **칩을 직접 누르면 카드가 안 골라진다** — 칩이 클릭을 먹는다.
    카드 3택(`RunPopupCreator.BuildSynergyChip`)이 이미 같은 모양이라 맞춰 뒀다.
    PC 는 올리기만 해도 떠서 고르는 데 방해가 없다
  - ⚠ `Setup(tag)` 를 **매번** 부른다 — 칸이 재사용되므로 빠뜨리면 옛 카드의 설명이 뜬다
  - 폭 검산: `3 × 38 + 2 × 10 = 134 ≤ 188`. 아이콘 38 ≤ `RowSm`(43).
    `VerifyTagRow` 가 굽는 순간 둘 다 잰다 — `HorizontalLayoutGroup` 은 넘쳐도 에러를 안 낸다
  - ⚠ 아이콘 배열은 **강화소도 함께 받는다** (`_synergyIcons`) — 칸 굽기가 공유라
    한쪽만 비워 두면 "제단만 되는" 갈림이 생긴다
- **강화소는 다 새긴 카드를 목록에 올리지 않는다** (`RunNodeFlow.CollectForgeSlots`)
  - ⚠ 거르는 조건과 `ForgePopup` 이 버튼을 잠그는 조건이 **같아야 한다**.
    갈리면 "목록에는 있는데 아무것도 못 하는" 칸이 다시 생긴다
  - 한쪽만 새긴 카드는 남는다 — 그 쪽 버튼만 "완료" 로 잠근다

### 상점 (ShopPopup)
- **정본**: 재고·값 `InGame/Summon/RunShopRule.cs` · 화면 `UI/Popup/ShopPopup.cs`
  · 굽기 `UI/Popup/Editor/ShopPopupCreator.cs` → `프리팹 생성 > 팝업 > 상점`
- 파는 것 — **카드 4** · **특성 2** · 상시 판매 넷(**마력의 정수** 최대 마나 +1 · **전쟁 자금** 공/체 +2% · **소집의 북** 배출 간격 ×0.95 · **마나 회복 포션**)
  - 마나 회복 포션 (사용자 지시, 2026-09-16) — 지금 마나를 최대 마나의 30% 채운다(`RunShopRule.PotionAmount`, 채우는 곳은 `RunPerkRule.RestoreMana`).
    **방문당 한 병** — 사면 "품절" 덮개(사용자 지시). 값 ×1.5 고정. 방문 수는 저장하지 않는다(재고 교체와 같은 규칙). 마나가 가득이면 잠근다(`Bind(blocked)`).
    ⚠ 덮개 글자는 칸 공용("구입함")으로 구워져 있어 포션 칸만 런타임에 "품절" 로 갈아 끼운다(`ShopPopup.Refresh`)
    ⚠ 상시 판매가 네 줄이 되어 한 줄 76px 이다 — 더 늘리면 구매 버튼(58+8)이 안 들어간다. 아이콘 `item_mana_potion`(`ItemIconGenerator`)
  - 소집의 북 (사용자 지시, 2026-09-15) — 전쟁 자금과 같은 틀(값은 산 횟수만, 300 → 550 → …).
    스택 `RunBoonData.DrumStacks` · 곱하는 곳 `RunPerkRule.DrainMultiplierFor` 하나(`RunShopRule.DrumIntervalMult`).
    ⚠ 빼지 않고 곱한다 — 빼면 끝없이 샀을 때 간격이 0 이 된다. 아이콘 `item_war_drum`(`ItemIconGenerator`)
  - ⚠ 카드 후보는 **카드 3택과 같은 규칙**을 쓴다 (`CardRewardPicker.Pick(.., want)`).
    상점만 다른 규칙을 두면 "3택엔 안 나오는데 상점엔 나오는 카드" 가 생겨
    도감 해금이 무슨 뜻인지가 화면마다 갈린다
  - ⚠ 시너지 강화는 팔지 않는다 — 런에 한 장뿐이라 값을 매기면 무게가 어긋난다
  - ⚠ **특성 값도 산 횟수로 오른다** (사용자 지시, 2026-09-11) — `PerkPrice(stage, 산 수)` =
    ×(2.5 + 1.25×n). 수는 **런 단위**로 `RunPerkData.ShopPerkBuys` 에 저장한다
    (방문마다 되돌리면 상점마다 첫 값으로 두 개씩 사는 것이 남는다). 보상·이벤트로 얻은 것은 안 센다
  - ⚠ 정수 값은 **산 횟수**로 오른다(`MaxManaBonus` 가 곧 그 수). 스테이지로 올리면
    후반 상점이 "정수만 쓸어 담는 자리" 가 된다. 세이브 필드를 따로 두지 말 것
- ⚠ **런 골드**로 산다 (`RunGoldRule`). 영구 골드는 품질 개선의 것이다
- ⚠ 재고는 **화면을 여는 순간 한 번만** 굴리고 저장하지 않는다 —
  저장하면 "마음에 안 들면 껐다 켜서 다시 굴리기" 가 생긴다
- 산 칸은 지우지 않고 **덮어서 끈다** — 칸이 사라지면 줄이 밀려 무엇을 샀는지 지워진다
  - ⚠ 산 것은 **`_cardSold`/`_perkSold` 배열이 정본**이다 (2026-09-08).
    덮개만 켜면 바로 뒤의 `Refresh` 가 `Bind` 로 도로 끈다 — 같은 카드를 몇 번이고 샀다
- ⚠ **누르는 것은 칸이 아니라 칸 안의 구매 버튼**이다 (사용자 지적, 2026-09-08)
  - 칸 = 평평한 패널(읽는 것) · 아래 띠 = 입체 버튼(누르는 것). 값은 **버튼 안**에 적는다
    (몬스터 상세의 품질 개선 버튼과 같은 규칙 — 낼 값과 누를 것이 한 몸)
  - 세 상태를 전부 다르게 그린다: 금색 값 / 붉은 값 + "부족" / 칸 전체 덮개 + "구입함"
- ⚠ **클릭 콜백에 `for` 의 `i` 를 그대로 넣지 말 것** (2026-09-08 — 이걸로 구매가 통째로 죽었다)
  - C# 의 `for` 변수는 반복마다 새로 만들어지지 않는다. 람다 넷이 같은 변수를 붙잡아
    루프가 끝나면 `i` 가 칸 수(4)로 굳는다 → `BuyCard(4)` 가 되어
    `index >= _cards.Count` 에서 **조용히 return** 했다
  - 화면에서는 "몬스터·특성을 눌러도 아무 일이 없다" 로만 보였다 (정수는 메서드 그룹이라 멀쩡)
  - 반드시 루프 안에서 `int slot = i;` 로 복사한다. `foreach` 는 반복마다 새 변수라 안전하다
- ⚠ 자리는 **상수의 합**이다 (`CardH`/`StallH`) — 좌표를 손으로 적지 말 것.
  옛 칸은 카드의 설명과 값이 33px 겹쳤고 가로 칸은 설명이 칸을 21px 넘겨 잘려 있었다
- ⚠ 특성 설명은 **세 줄**이다. `VerifyDescriptionsFit` 이 굽는 순간 가장 긴 `RunPerk.Describe`
  를 재서 넘치면 에러를 낸다 — TMP 는 넘치는 글을 조용히 잘라 낸다
- ⚠ 공용 `FacilityStage.BottomVeilH`(560)를 키우지 않는다 — 네 시설이 같이 쓴다.
  상점만 물건이 두 줄이라 `ShopVeil` 을 **560 에서 위로 맞붙여** 이어 짓는다
  (겹쳐 깔면 그 구간만 짙어져 560 자리에 가로 줄이 생긴다)
- 아이콘: `item_mana_essence` — `ItemIconGenerator` 가 굽는다 (`Icons/Items/`).
  ⚠ 마나 물방울과 모양이 달라야 한다. 그쪽은 "지금 쓰는 마나", 이건 "그릇을 키우는 물건"

### ⚠ "골랐는데 그냥 넘어간다" — 판정이 두 곳으로 갈릴 때 생긴다
- **정본**: `CardEvolution.HasAnyChoice(deck, slot)` — 진화·융합 중 **실제로 할 수 있는
  것이 있는가**. 보상 후보를 거르는 쪽(`CardRewardPicker`)과 창을 여는 쪽
  (`CardEvolveUI.Setup`)이 **반드시 같은 판정**을 써야 한다
- ⚠ 실제로 갈려 있었다 (사용자 지적, 2026-09-07) — 후보 쪽은 `CanFuse` 만 보고
  **재료를 안 봤다**. `CanFuse` 는 "칸이 남았나" 만 본다.
  그래서 융합을 한 번 하고 재료가 떨어지면 그 카드가 보상에 또 뜨고,
  고르면 창이 안 뜬 채 다음 판으로 넘어갔다 — 보상 한 번이 통째로 사라진다
- `HasMaterial(deck, slot)` 이 재료 유무를 본다. `CanFuse` 와 **따로 쓰지 말 것**

### ⚠ 몬스터 스탯 합성 중에는 레이어(Add)를 쓰지 않는다 (2026-09-15 버그)
- `UnitStat.Set` 은 **기본 레이어만** 덮고 `Get` 은 **모든 레이어의 합**이다. `MonsterStatComposer` 는
  ④ 이후 시너지·강화 카드·중첩·특성을 `Set(Get × 배율)` 로 곱하므로, 그 앞에 `Add` 레이어가 있으면
  **곱할 때마다 그 레이어가 한 번씩 더 붙는다**
- 실제로 카드 레벨 표(`MonsterLevelBonus`)가 `"cardlevel"` 레이어였다 — Lv4 슬라임 킹이 31,000 이어야 할 체력이
  **58,000** 이었다(체력 % 칸이 셋인 종족일수록 심했다). 지금은 기본 레이어에 굽는다
- ⚠ 합성 도중에 새 보정을 넣으면 `Set(Get × 배율)` 로 굽는다. 레이어는 **맨 마지막(장비 ⑦)만** 쓴다

### 영웅 상세 (HeroDetailPopup)
- 머리의 '지휘력 +1 › 용병 스탯' 안내는 **감춘다** (사용자 지시, 2026-09-15) — 이름 줄에 겹쳐 그려졌다.
  런타임 `RefreshCommandHint` 가 끈다 (프리팹 재굽기 불필요)

### 전황 (BattleInfoPopup)
- **2026-09-15 다시 짰다** (사용자 지적 "허접하다 · 글자가 겹친다") — 머리 띠 [전황][스테이지 칩][X] ·
  좌우 패널(위 색 띠 파랑/빨강 · 머리에 라벨과 요약 "카드 N장" / "부대 N · 병사 N") · 가운데 VS 메달.
  제목과 스테이지를 **다른 칸**으로 갈랐다(`_stageText`) — 한 줄에 이어 붙였더니 진형 라벨과 겹쳐 그려졌다.
  자리는 전부 상수의 합이고 `Verify` 가 패널 넘침을 잰다
- **정본**: 화면 `UI/Popup/BattleInfoPopup.cs` · 굽기 `Editor/BattleInfoPopupCreator.cs`
  → `프리팹 생성 > 팝업 > 전황` · 버튼 `InGame/UI/BattleInfoButtonUI.cs` (상단바 오른쪽)
- 가운데를 기준으로 **왼쪽 아군(4열×2줄) · 오른쪽 적군(라인 5줄)**
  - ⚠ 적은 **나오는 자리 그대로** 세운다 — `HeroSpawner.LaneFor` 를 그대로 쓴다.
    그래서 그 함수는 `public` 이다. 목록으로 늘어놓으면 "가운데가 무겁다" 를 못 읽는다
  - ⚠ 아군은 라인이 없다(낼 때 고른다). 그래서 격자다 — 좌우가 다른 모양인 것이 맞다
  - ⚠ **2열×4줄은 화면 밖으로 넘친다** (960 > 920). 4열×2줄이어야 들어간다
- ⚠ **상세는 이미 있는 창을 연다. 여기서 다시 그리지 않는다** (사용자 지적, 2026-09-07)
  | 누르면 | 열리는 것 |
  |---|---|
  | 아군 | `MonsterDetailPopup.SetupRun(species, slot)` — **런 값 모드** |
  | 적군 | `HeroDetailPopup.SetupHero(entry, 표시이름)` |
  한때 이 창 안에 자체 Detail 패널을 만들었다가 걷어냈다. 같은 것을 두 번 그리면
  스탯 줄 하나를 고칠 때마다 두 곳을 고쳐야 하고 반드시 한쪽만 고쳐진다
- ⚠ **용사 초상화는 `GetCached` 로 못 얻는다** — 이름 그대로 이미 합성된 것만
  돌려준다. 원작 방식대로 몇 프레임에 걸쳐 합성되므로 처음 여는 창에서는 늘 null 이다.
  `GeneralPortraitProvider.Request(name, stillWanted, onReady)` 를 쓸 것
  (몬스터는 `MonsterPortraitProvider.Get` 이 그 자리에서 합성한다 — 종족이 열 종 남짓이라)
- ⚠ **용사 초상화는 등급을 함께 넘긴다** (2026-09-16) — `GeneralPortraitProvider.Request(이름, 등급, …)`, 캐시 열쇠 = 이름#등급.
  외형이 (이름·직업·등급)으로 굴러 나온다. 장수는 `GetBirthGrade(이름, 레벨)`(초반 상한), 병사만 오는 부대는 `SoldierRuntimeBridge.SoldierGradeOf`(장수 −1) — 이름만 넘기면 필드와 다른 옷을 입는다
- ⚠ 용사 표시 이름은 `HeroNameRule` 이 만든다. **시드 이름("Hero_S6_0")을 화면에 내지 말 것** —
  그건 직업·외형·패시브를 정하는 씨앗이라 바꾸면 그 판의 적이 통째로 달라진다

### 진화 / 융합 창 (CardEvolveUI)
- 만렙 카드를 고르면 `RunBootstrap.TryOpenEvolve` 가 연다 — **팝업이다** (`PopupType.CardEvolve`)
- ⚠ **HUD 자식이었다가 팝업으로 옮겼다** (사용자 지적, 2026-09-07)
  팝업 체계가 멀쩡히 있고 바로 앞 화면인 카드 3택도 팝업인데 이것만 HUD 안에 있었다.
  그 탓에 셋이 따라왔다 — ① 겹침 순서를 손으로 맞춰야 했고(`BringOverlaysToFront`),
  ② HUD 를 굽다 멈추면 창이 프리팹에서 통째로 사라졌으며, ③ 사라져도 런타임이
  조용히 넘어가 **"진화를 눌렀는데 아무 일도 안 난다"** 가 됐다 (두 번 겪었다).
  - 굽는 곳: `UI/Popup/Editor/CardEvolvePopupCreator.cs` → `프리팹 생성 > 팝업 > 진화·융합`
  - ⚠ 그림 조립 200줄은 **옮기지 않았다** — `InGameUIPrefabCreator.BuildCardEvolveRoot`
    에 그대로 있고 Creator 가 그 함수를 부른다. 옮기면 같은 코드가 두 벌이 될 위험이 있다
    (이 파일은 실제로 한 번 444줄이 중복된 적이 있다)
  - ⚠ 굽는 순서: `종족 패시브 아이콘` → `팝업 > 진화·융합` → `PopupManager [Load Popup Prefabs]`
- **갈래는 셋이다 — 진화 / 융합 / 강화** (사용자 지시, 2026-09-12)
  - '강화' 는 **언제나 켜져 있다** — 공·체 `CardEvolution.EmpowerBonus`(+10%)만 얹고
    종족도 레벨도 재료도 건드리지 않는다. 적용은 `SummonDeckData.Empower` →
    `SummonDeckSlot.InheritBonus` (진화·융합과 **같은 칸**, `MonsterStatComposer` ④)
  - ⚠ 왜 있나 — 진화가 막힌 카드는 남는 갈래가 융합뿐이라 **재료를 잃는 쪽이 강제**됐다.
    고를 것이 하나뿐인 창은 선택지가 아니라 확인 버튼이다
  - ⚠ 작아야 한다 — 언제나 고를 수 있는 안전한 갈래라, 크면 진화·융합을 볼 이유가 없어진다
- ⚠ **못 하는 갈래는 통째로 감춘다** (사용자 지시, 2026-09-12 — 옛 `DimBranch` 는 지웠다)
  흐리게 두면 "이미 진화했거나 상위 종족이 없다" 는 줄이 붙은 버튼이 화면에 남아
  **진화가 있을지도 모른다**는 착각을 준다. 이미 진화한 카드에게 진화는 *없는* 갈래지
  막힌 갈래가 아니다
  - ⚠ 감추면 자리가 빈다 — `CardEvolveUI.StackBranches` 가 켜진 갈래만 위에서부터
    다시 세운다. 자리의 정본은 Creator 의 `offsetY` 셋이고, 런타임은 프리팹에서
    한 번 읽어 둔다(`CaptureBranchLayout`) — 간격 상수를 런타임에 또 적지 말 것
  - ⚠ 창을 여는 판정은 그대로 `CardEvolution.HasAnyChoice`(진화 or 융합)다.
    '강화' 를 그 판정에 넣지 말 것 — 넣으면 아무것도 못 하는 카드까지 보상 후보가 된다
- ⚠ **`BuildCardEvolve` 는 아이콘 확인을 `DestroyChild` 보다 먼저 한다** —
  순서가 반대였을 때, 종족 패시브 아이콘을 안 구운 상태로 HUD 를 구우면
  옛 창을 지우고 새로 못 만든 채 빠져나가 **진화 창이 프리팹에서 통째로 사라졌다.**
  런타임에서는 `_evolveUI` 가 null 이라 만렙 카드를 골라도 아무 일이 없다

### 소환사 평타 — 대상 최대 체력 비례
- 정본 `InGame/Summon/SummonerStrikeRule.cs` — 잡병 **25%** · 엘리트 **15%** · 보스 **5%**
  (사용자 지시, 2026-09-16 — 옛 5% · 1% 의 ×3 · ×5)
- ⚠ **공격력 수치를 대신한다** (사용자 확정, 2026-09-07) — 더하지 않는다.
  패기×2 = 8~16 짜리 평타는 체력 수천짜리 후반 용사에게 없는 것과 같았다
- ⚠ 방어율을 지나지 않는다 (`UnitHitSystem` — 특성 '파쇄'와 같은 자리·같은 규칙)
- ⚠ **평타(`HitType.Normal`)에만** 걸린다 — 시그니처 스킬까지 비율이면 횟수 제한의 값이 폭발한다
- 격을 가르는 세 숫자가 "소환사가 다 한다" 를 막는다 — 공속 0.8 기준 잡병 5초·보스 25초
- 스폰 때 `SummonerStrikeComponent` 로 엔티티에 굽는다 (`SummonerRuntimeBridge`) —
  ⚠ Burst 잡에서 소환사를 조회할 수 없다 (`RendComponent` 와 같은 이유)

### 소환사 3대 스탯
> 지능이 "많이"(마나 그릇)와 "세게"(소환력) 를 둘 다 먹는다. 나머지 둘은
> **지능이 못 하는 것**을 맡아야 한다 — 안 그러면 소환력의 열화판이 된다.

| 스탯 | 하는 일 | 정본 |
|---|---|---|
| 지능 | 마나 그릇 `20 + Int×4` · 소환력(몬스터 공/체 가산) | `SummonerData` · `MonsterStatComposer` ⓪① |
| 체력 | 마왕성 HP `BaseCoreHp + Vit×2` | `SummonerData.MaxCoreHp` · `RunCoreData` |
| **패기** | ① 몬스터 평타 **넉백 배율** ② **과부하 저항** ③ **소환사 평타 비율 배율** | `InGame/Summon/SummonerVigorRule.cs` |

- ③ 은 2026-09-11 추가 (사용자 지시) — `StrikeMultFor` = 1 + (패기−5)×0.12 (하한 0.5).
  잡병 25%·엘리트 15%·보스 5% 에 **같이** 곱한다(격 사이 비는 유지). 곱하는 곳은 `SummonerStrikeRule.Build(summoner)` 하나
- **스탯 배분을 넓혔다** (2026-09-11) — 합계 19 는 그대로, 폭을 2~10 으로. 정본 `SummonerCreator` 로스터
- **개성 교체** (2026-09-11) — 슬라임 킹 `SlimeSpit`(13, 친화 N마리 중 1마리 원거리) ·
  스컬 킹 `BoneLegion`(14, 친화 카드 마릿수 +2 — `RunPerkRule.SummonCountFor` 에서 더한다) ·
  역병 술사 `PlagueRise` 는 **죽을 때 → 적을 쓰러뜨릴 때**(확률 = PerkValue).
  ⚠ 역병은 `PlagueRiserTag`(카드로 낸 친화 개체만) → `UnitDeathDespawnSystem` 이 마지막 일격을 보고 자리를 적고 →
  `SummonController.Update` 가 `FlushPlague` 로 세운다. 시스템 도중에 스폰하지 않는다
  ⚠ 옛 번호 2·4(`SwellOnRepeat`·`AffinityGrade`)는 남겨 뒀다 — 다시 굽기 전 SO 가 들고 있을 수 있다
- ⚠ **몬스터는 `EnemyKillEvent` 버퍼가 없다** — `ResolveKillCredit` 도 장군·병사만 본다.
  그래서 투지 시너지(처치 누적)·처치 발동 패시브가 몬스터에게는 **한 번도 안 돈다.** 버퍼를 붙이려면
  `CombatTriggerSystem` 이 몬스터 버퍼도 비우는지 먼저 볼 것 (안 비우면 매 프레임 같은 처치가 쌓인다)
- 하단 카드의 **[친화] 표식** — 왼쪽 아래(레벨 배지의 거울상), `SummonCardUI.RefreshAffinity` 가 `RunPerkRule.IsAffinity` 로 켠다
- 제단·강화소(`CardPickPopupBase`)·상점·카드 3택은 **`AffinityTagUI.Set(초상화 Image, species)`** (2026-09-11) —
  초상화 아래에 런타임으로 [친화] 판을 한 번 만들고 켜고 끈다(프리팹을 다시 굽지 않는다).
  ⚠ 칸이 재사용되므로 **매번** 부를 것 — 스킬·시너지 카드는 null 을 넘겨 끈다

- ⚠ **옛 이름은 '힘(Strength)' 이다** (2026-09-07 개명) — `SummonerData.Vigor` 에
  `[FormerlySerializedAs("Strength")]`, `AttackPerVigor` 에 `[FormerlySerializedAs("AttackPerStrength")]`
  가 달려 있다. **그 줄을 지우면 소환사 SO 12개의 값이 0 이 된다** (다시 굽기 전까지)
- **힘은 한때 죽은 스탯이었다** (사용자 지적, 2026-09-07) — 소환사 평타(DPS 6~13)와
  몬스터 공격력 `+힘×0.1`(4% 미만)뿐이라 둘 다 값이 없었다. 그 가산은 **걷어냈다**
- 패기 4~8, 기준 5 — 넉백 `×0.8 ~ ×1.6` · 과부하 계수 `0.11 ~ 0.07`(기본 0.10)
  - ⚠ 기준을 최솟값이 아니라 **가운데**에 둔다. 최솟값 기준이면 전원이 보너스만 받아 축이 안 갈린다
  - ⚠ 과부하 계수에 하한(0.04)이 있다 — 0 이 되면 도배를 막는 장치가 통째로 사라진다
  - ⚠ 넉백은 **평타에만** 걸린다. 스킬은 자기 `KnockbackMult` 로 밸런스가 잡혀 있다
  - ⚠ 배율은 스폰 때 `KnockbackPowerComponent` 로 **엔티티에 구워** 둔다 —
    피해 계산이 Burst 잡이라 그 안에서 소환사를 조회할 수 없다 (`RendComponent` 와 같은 이유).
    기준값(1)이어도 반드시 붙인다 — 풀 재사용이라 안 붙이면 지난 판 값이 남는다
  - ⚠ `UnitHitSystem` 에 `ComponentLookup` 을 추가하면 **필드 선언 · `Update(ref state)` ·
    잡 필드 대입 셋 다** 해야 한다. 하나만 빠지면 조용히 무효 lookup 이 된다
  - ⚠ 상한(`MaxSingleKnockback`)은 배율 **뒤에** 건다 — 앞에 걸면 힘이 상한에 먹힌다
- ⚠ **이름을 바꾸려면 세이브를 먼저 볼 것** — `SummonerData.Strength` 는 소환사 SO 12개에
  직렬화돼 있다. 그냥 바꾸면 값이 전부 0 이 된다. `[FormerlySerializedAs("Strength")]` 필수

### 원작 패시브를 몬스터가 들면 세기가 달라진다
> ⚠ **지속 버프는 쌓지 말고 시간만 되살릴 것** (사용자 지적, 2026-09-07)

- 종족 패시브 일부는 **원작 패시브에 그대로 얹혀 있다** (`SpeciesPassiveRuntime.ApplyOnSpawn`)
  — 방벽(Bulwark)→`DefenseShield` · 생명흡수(SoulDrain)→`VampiricStrike` ·
  피의 각성(Bloodlust)→`KillEmpower` · 무리 사냥(PackHunt)→`KillMomentum`
- ⚠ **그 수치는 원작 장수 기준이다.** 장수는 하나였고 상대도 소수였다.
  이 게임의 몬스터는 **물량이고 앞줄이 부대에 둘러싸인다** — 피격 횟수가 자릿수로 다르다
- 실제로 `DefenseShield` 가 피격마다 `+0.10` 을 **상한 없이** 쌓아
  슬라임 방어율이 **0.19 → 2.59** 가 됐다. 소프트캡(`DefenseMax` 0.9) 위로 한참 올라가
  사실상 불사다. `CounterStrike`(공격력 +20%)도 같은 구조였다
  - 둘 다 **갱신형**으로 고쳤다 — 같은 `SourceType`+`SourceId`+`Stat` 을 찾아
    `Remaining` 만 되살린다 (`AbilityGhostRally.AddOrMerge` 와 같은 조회 방식)
  - ⚠ 세기를 올리려면 **수치를 키운다. 중첩을 되살리지 말 것** — 중첩은
    "맞는 횟수" 가 세기를 정하게 만들어 둘러싸일수록 단단해지는 뒤집힌 규칙이 된다
- ⚠ 아직 **누적형으로 남은 것들**이 있다 (`SkillAdrenaline` · `SkillRally` ·
  `SoldierMorale` · `SoldierVigor` · 어빌리티 4종). 전부 스킬 사용·처치·병사 사망처럼
  **드문 사건**이 트리거라 지금은 드러나지 않는다 — 몬스터에 얹게 되면 먼저 확인할 것
- 몬스터가 원작 패시브를 얻는 길은 둘뿐이다 — **종족 정의**(`CollectSpeciesPassives`)와
  **융합으로 배운 것**(`SummonDeckSlot.CollectLearned`). 무작위 배정은 없다

### 종족 패시브 (SpeciesPassive)
- **정본**: `InGame/Summon/SpeciesPassive.cs` — enum 19종 · 수치(`SpeciesPassiveRule`) · 이름/설명
  - ⚠ 설명 문구에 숫자를 손으로 적지 말 것 — `Pct(SpeciesPassiveRule.*)` 로 뽑는다
  - ⚠ **`SpeciesPassiveRule.All` 이 순서 계약이다** — 아이콘 PNG · Creator 가 박는 `Sprite[]` ·
    런타임 `IndexOf` 셋이 이 배열 하나를 공유한다. 중간에 끼우지 말고 **뒤에만** 추가한다
- **⚠ 분열·부활은 원본을 그대로 물려받는다** (사용자 확정, 2026-09-07) —
  `MonsterOrigin`(카드 레벨 · 융합 개성 · **외형 시드**)이 정본. `MonsterSpawner.Arm` 이
  스폰 때 `MonsterDeathWatcher` 에 실어 두고, 죽을 때 `SpawnDerived` 로 그대로 넘어간다
  - 예전에는 파생체가 늘 `Lv1 · 융합 없음 · 이름 새로 굴림` 이었다 → Lv5 카드가 분열·부활하면
    레벨 보너스 +48%(`StatBonusPerLevel` 0.12×4)와 열린 고정 효과 4칸이 통째로 빠지고,
    인간형은 **얼굴·무기까지 다른 개체**로 일어났다
  - ⚠ 불어나는 것을 막는 것은 **세대**지 레벨을 깎는 게 아니다 (`MaxReproduceGeneration`)
  - ⚠ 라인(`lane`)은 물려주지 않는다 (사용자 확정) — 대기열로 안 돌아가는 개체라
    라인 특성까지 주면 공짜 물량이 이중으로 이득을 본다
  - ⚠ **부활은 작아지지 않는다** — 크기는 `scaleHpOnly` 가 가른다(`visualScale`).
    분열·재조립만 작아진다(몸이 쪼개진 그림). 부활은 같은 몸이 체력만 적게 서는 것이다
  - ⚠ 이름이 겹쳐도 안전하다 — `UnitPoolLinkComponent.PoolKey` 에 들어가지만
    실제 풀 반납은 `LinkedObject` 로 한다 (`UnitDeathDespawnSystem`)
  - ⚠ **스킬·개성이 새로 부른 몬스터는 `MonsterOrigin.None`** — 시그니처 소환·비석·
    강령술사의 스켈레톤은 되살아난 것이 아니라 새로 태어난 것이다
- ⚠ **몬스터 풀은 대기 시간에 미리 채운다** (`SummonController.PrewarmMonsters`, 2026-09-11)
  — 슬라임 67마리가 한꺼번에 죽자 분열체마다 `Instantiate` 가 돌아 313ms 가 걸렸다.
  죽는 개체는 `OnDisable` 시점에 아직 풀에 없어서 제 자리를 물려주지 못한다.
  StageReady 에 `대기열 수 + 64` 까지, 프레임당 4개씩 채운다. 풀 키는 전 종족 `"Monster"` 하나
- **런타임**: `InGame/Summon/SpeciesPassiveRuntime.cs` — `ApplyOnSpawn`(지속형) · `OnDeath`(단발형)
  - ⚠ **`OnDeath` 도 융합으로 배운 패시브를 함께 모은다** (2026-09-07 수정)
    `MonsterDeathWatcher` 가 `_species.CollectSpeciesPassives` **+** `_origin.Card.CollectLearned`
    를 부른다 — 스폰 경로(`MonsterSpawner`)와 **같은 순서**(종족이 먼저, 배운 것이 뒤)다.
    한때 사망 쪽만 종족 것을 모아서, 융합으로 배운 **사망 발동** 패시브(분열·역병·자폭·회복)가
    통째로 무효였다. 카드에는 아이콘이 멀쩡히 보여서 더 찾기 어려웠다
  - ⚠ 사망 효과의 스탯 기준은 **굴려 나온 값**이다 (`MonsterDeathWatcher` 가 `RolledStat` 을 넘긴다).
    `species.MaxHp`/`species.Attack` 은 SO 원본이라 카드 레벨·품질·소환력이 하나도 안 들어간다
    — 그걸 쓰면 만렙 Epic 이 Lv1 Normal 과 똑같은 값으로 터진다
  - ⚠ **사망 발동 회복은 카드 ID 로 통계에 단다** (`CardStatsTracker.RecordHealingForCard`, 2026-09-11)
    — 시전자가 이미 죽어 엔티티로 못 찾아 출처를 비웠고, 통계 '치유' 가 늘 0 이었다.
    `UnitHealSystem` 도 이제 카드 통계를 채운다(출처가 있는 회복, **실제로 찬 양**만).
    비율은 0.10 → **0.20** (각성 0.25 → 0.40) — 평타 한 대 값이라 회복으로 안 읽혔다
  - **힐 슬라임은 치유 스킬을 갖는다** — `ActiveSkillId.SlimeMend`(35, `ActiveSlimeMend`) (2026-09-11)
    반경 3.5 안 다친 몬스터 최대 6마리 · **받는 쪽** 최대 체력 8% · 쿨 6초(술법 쿨감을 받는다).
    ⚠ 원작 치유 오라·집중 치유는 `GeneralComponent` 만 찾아 **몬스터에게 쓰면 아무도 안 낫는다**
    ⚠ 몬스터 전용 스킬은 `IsMonsterOnly()` 에 넣을 것 — 안 넣으면 용사가 추첨해 플레이어를 치유한다
    힐 슬라임 스탯도 공격 13→8 · 체력 195→300 (진화 지수 ×1.22)
  - ⚠ 힐(`HealOnDeath`)은 **주는 쪽** 최대 체력 비례다. 숲 시너지(`HealForestAllies`)만
    받는 쪽 비례다 — 규칙이 다르니 합치지 말 것
- **아이콘**: `InGame/Summon/Editor/SpeciesPassiveIconGenerator.cs` → `아이콘·텍스처 > 종족 패시브 아이콘`,
  경로 정본은 `Editor/SpeciesPassiveIconAssets.cs` (`3.Textures/Icons/SpeciesPassives/passive_<Enum>.png`)
  - 패시브를 추가하면 `All` 과 `SpeciesPassiveIconGenerator.Table` **둘 다** 넣고 다시 굽는다
    (빠지면 `Generate` 의 `Verify` 가 잡아 준다)
- **화면 표시**: `InGame/UI/SpeciesPassiveChipUI.cs` (아이콘 + 눌러서 설명, `TooltipLayer` 공용)
  - 카드 3택 — `UI/Popup/CardSelectPopup.cs` `FillSpeciesPassives` + `Editor/RunPopupCreator.cs` 의 `PassiveRow`
    - ⚠ **시너지 줄과 붙여 놓지 말 것** — 그림 일곱 개가 한 덩어리로 보여 무엇이
      무엇인지 갈리지 않는다. 스탯 줄이 둘 사이를 가른다(패시브=이름 밑, 시너지=스탯 밑).
      둘 다 가운데 정렬이다. 그림도 갈린다 — 패시브는 **리벳 테두리** 한 종류다
      (`SpeciesPassiveIconGenerator.FamilyFrame`), 시너지 중 리벳은 '강철' 뿐이라
      방패 글리프만 피하면 테두리가 통째로 패시브 것이 된다
    - ⚠ 카드 자리는 런타임이 다시 잡는다 (`Recenter`) — Creator 는 4칸을 굽는데
      보통 3장만 떠서, 고정 자리를 그대로 쓰면 왼쪽으로 쏠린다
  - 융합 창 — `InGame/UI/CardEvolveUI.cs` `FillTargetPassives` + `Editor/InGameUIPrefabCreator.cs`
  - ⚠ **글로 적지 않는다** — 계보가 깊으면 넷까지 붙어 카드가 글로 덮인다.
    `DescribeSpecies` 는 이제 특징 분류 한 줄만 돌려준다

### 몬스터 시너지 (덱 조합)
- **정본**: `InGame/Summon/MonsterSynergyRule.cs` — 표식 13종·문턱·**모든 수치**가 여기 하나에 있다
  - **2026-09-15 추가 5종 + 재분배** (사용자 확정) — 사냥(치명타) · 사격(원거리) · 무리(뭉치기) · 선봉(첫 공격) · 왕권(2차 전용)
    | 시너지 | 동 | 은 | 금 | 금 능력 |
    |---|---|---|---|---|
    | 사냥 | 치확 +15%p · 치피 +20%p | +25%p · +40%p · 체력 50% 이하 적 치확 +15%p | +40%p · +70%p | 치명타가 방어율 무시 (`VitalStrikeTag`) |
    | 사격 | 원거리 공 +12% | +22% · 사거리 +15% | +30% · 사거리 +15% | 투사체 하나 더 (**같은 대상**, 피해 25%) |
    | 무리 | 같은 종족 곁(반경 3, 동료 3) 받는 피해 −10% | −18% · 공 +5% · 무리 사망마다 주변 공 +3%(최대 15%) | −20% · 공 +10% | 무리 카드 마릿수 +1 (`RunPerkRule.SummonCountFor`) |
    | 선봉 | 첫 공격 +50% · 이후 3초 받는 피해 −20% | +100% · 4초 −30% · 첫 공격 넉백 | +200% · 5초 −45% | 첫 공격이 주변 적 1초 기절 |
    | 왕권 | 권속 쿨 −20% | −35% · 권속 공·체 +30% | −50% · +60% | 권속 +1 |
    - ⚠ **표식 규칙**: 기본·1차 = 일반 2개 · 2차 = 일반 2개 + 왕권. 일반 시너지마다 6~7종 — `MonsterCodexCreator.VerifySynergyTags` 가 굽는 순간 본다.
      정본은 `MonsterCodexCreator` 로스터의 `Tags` 줄 (옛 에셋 값은 이제 전부 덮인다)
    - ⚠ **AllTags 는 뒤에만 붙인다** — `RunBoonData` 제단 몫이 그 자리(인덱스)로 저장된다
    - 선봉·무리는 공격 잡이 할 수 없는 일(상태효과·범위)이라 `MonsterSynergyRuntime.Tick` 이 한다 (`SummonController.Update` 가 부른다).
      선봉은 공격 잡이 `VanguardComponent.State = 1` 로 표시만 하고, 받는 피해 감소·무리 버프는 **방어율 가산**으로 건다
    - 새 컴포넌트 넷(`HuntCrit`·`ExtraProjectile`·`Vanguard`·`Swarm`)은 `ApplyOnSpawn` 이 **먼저 떼고** 붙인다 (풀 재사용)
    - HUD 시너지 칩 68 → 45px · 간격 3 · 글자 FontSm — 14칸(중첩 1 + 13)이 전장 안에 들어가야 한다
    - 굽는 순서: `아이콘·텍스처 > 시너지 아이콘` → `데이터 생성 > 몬스터 도감` → `카드 목록` → `UI > 인게임 HUD` ·
      `프리팹 생성 > 팝업 > ▶ 런 팝업`·`Codex`·`몬스터 상세` → `[Load Popup Prefabs]`
  - 문턱 규칙: **금 = 계열 완주**. 문턱은 **도감의 실제 소속 수**가 정한다(`MonsterSynergyRule.MemberCount`) —
    7종 이상 = 3/5/7 · 그 아래 = 2/4/6 · 왕권만 1/2/3. 예전엔 "언데드·숲 = 7종" 을 코드에 박아 두어 재분배하자 어긋났다
  - **금은 숫자가 아니라 규칙을 바꾼다** — 여덟 시너지 전부에 금 전용 능력이 하나씩 있다.
    목록의 정본은 `MonsterSynergyRule.GoldAbility` 다 (툴팁도 거기서 뽑아 **별도 줄**로 그린다).
    숲=죽어도 라인 복귀 · 언데드=부활체가 한 번 더 부활 · 야수=넉백 면역 ·
    재생=치명상 한 번 버티기 · 투지=누적 상한 20 + 스테이지 넘겨 유지 ·
    강철=한 방 피해 상한 25% · 역병=죽을 때 역병 전파 · 술법=스킬 준비된 채 소환.
    (중첩 7개는 별도로 "복귀 시 한 마리 더")
    ⚠ 금 리더를 "은의 두 배 숫자" 로만 두지 말 것 — 그러면 금을 향할 이유가 없다.
    새 시너지를 만들면 `GoldAbility` 도 반드시 채울 것
  - ⚠ **피해 계산(Burst 잡)에서는 `MonsterSynergyRule` 을 읽을 수 없다**
    강철(피해 상한)·재생(치명상 버티기)은 스폰 때 단계를 계산해
    `SynergyDamageCapComponent` / `SynergyLastStandComponent` 로 **엔티티에 구워** 둔다.
    ⚠ `UnitHitSystem` 에 `ComponentLookup` 을 추가하면 `Update(ref state)` 와
      잡 필드 대입을 **둘 다** 해야 한다 — 하나만 빠지면 조용히 무효 lookup 이 된다
  - ⚠ 수치는 **전투력지수**(EHP배율 × DPS배율)로 맞춰져 있다 — 동 ×1.12 · 은 ×1.30 · 금 ×1.60.
    한 줄만 고치면 그 시너지만 세진다. 반드시 같은 지수로 다시 맞출 것
  - 환산에 쓴 가정 셋(교전 8초 · 평균 4처치 · 스킬이 DPS의 40%)이 뿌리다. 실측과 다르면 여기만 고친다
- **표식**: `InGame/Summon/MonsterTag.cs` — ⚠ `MonsterTrait`(도감 표시용 분류)와 **다른 축**이다. 겸용 금지
  - ⚠ 계보로 상속되지 않는다. 진화가 표식을 **바꾸므로**(강철 슬라임은 숲을 잃는다) 종족마다 직접 적는다
- **적용**: `InGame/Summon/MonsterSynergyRuntime.cs` — `ApplyStats`(스탯) · `ApplyOnSpawn`(ECS 컴포넌트) · `OnDeath`(부활·범위 회복)
  - ⚠ 기존 컴포넌트를 덮어쓰지 않고 **합친다** — 종족 패시브가 같은 `RetaliateComponent`/`InflictOnHitComponent` 를 쓴다
- **투지 누적**: `InGame/Summon/MonsterSynergyKillSystem.cs` (+ `SynergyKillStackComponent`)
  - ⚠ `EnemyKillEvent` 버퍼를 **읽기만** 한다. 비우는 것은 `CombatTriggerSystem` 뿐이다
  - ⚠ **쿼리를 도는 도중에 `EntityManager` 를 부르지 말 것** (2026-09-03에 이걸로 크래시)
    `EntityManager.GetBuffer`/`SetComponentData` 는 동기화 지점이라 돌고 있는 잡을
    기다리며 쿼리의 청크 캐시를 무효화한다. 그 상태로 순회를 이어 가면 메모리가
    어긋나고, **엉뚱한 Burst 시스템에서 NullReferenceException 이 난다**
    (실제로 `ActiveSkillCooldownSystem` 안에서 터졌다).
    → `SystemAPI.HasBuffer/GetBuffer` 를 쓰고, 버퍼를 직접 읽기 전에 `CompleteDependency()`
- **집계 대상**: **지금 존재하는 종족**이다 — 필드에 살아 있거나 대기열에 남아 있거나
  (사용자 확정, 2026-09-09. 옛 규칙은 "한 번 내면 런 내내 유지" 였다)
  - 정본은 `MonsterSynergyRule.Recount` — `_alive`(필드) + 대기열을 합쳐 센다
  - 필드 집계는 `MonsterLineReturner.Setup`(+1) / `OnDisable`(−1) **한 쌍**이 소유한다.
    ⚠ **카드 몬스터(대기열로 돌아갈 자격이 있는 개체)만 센다** — 권속·시그니처·분열체 같은 스킬 소환은 시너지를 받기만 하고 카운트에 넣지 않는다 (사용자 지시, 2026-09-16. 한때 전부 세어 덱에 없는 몬스터를 스킬로 부르면 카운트가 올랐다)
  - ⚠ **전멸하면 카운트에서 빠진다** — 화면의 숫자가 전장과 무관해지는 것을 막는다
  - ⚠ 매 프레임 세지 않는다. **0↔1 을 넘는 순간에만** 다시 센다 (교전 중 깜빡임 방지)
  - ⚠ 이어하기가 저절로 맞는다 — 대기열은 저장되므로 같은 값이 나온다.
    한때 static `_summoned` 로만 세어 **껐다 켜면 카운트가 통째로 줄었다**
    (제단에 바친 카드의 몫이 사라진 것처럼 보였다)
  - `_summoned`(낸 적 있는 기록)는 **시너지 강화 카드 후보**를 고를 때만 쓴다
  - ⚠ `Bind()` 는 `RestoreQueue` **뒤에** 불러야 한다
- **카드 3택 표시**: `UI/Popup/CardSelectPopup.cs` `DescribeSynergy` + `Editor/RunPopupCreator.cs` 의 `Synergy` 줄
- **인게임 표시**: `InGame/UI/SynergyBarUI.cs` (상단바 바로 아래 칩 줄)
  - **맨 위는 중첩 칩**이다 (사용자 지시, 2026-09-12) — 켜진 시너지 수 `3/5/7`, 바탕은 중첩 단계를 동·은·금 색으로.
    한때 중첩 보너스는 전투에만 걸리고 화면 어디에도 없었다. 문턱 정본 `MonsterSynergyRule.StackThresholds`,
    글은 `StackStepsLabel`·`StackTitle`·`DescribeStack` (수치 함수에서 뽑는다). 시너지가 하나라도 보이면 3 미만이어도 흐리게 선다
  - 한 마리라도 낸 시너지는 **꺼져 있어도** 띄운다(0장만 감춘다). 금→은→동→꺼짐 순
  - 칩 글자는 `StepsLabelOf` 가 만드는 `2/4/6` — 도달한 문턱만 단계 색이다
- **하단 카드 표시**: `InGame/UI/SummonCardUI.cs` `RefreshSynergy` — 카드 **왼쪽 변**에 세로로 셋
  - ⚠ 가로로 눕히지 말 것 — 오른쪽 아래 레벨 배지(72×43)와 자리를 다툰다
  - 색이 곧 현재 단계다(꺼져 있으면 흐린 회색). 숫자는 넣지 않는다 — 상단 줄이 말한다
- **아이콘**: `InGame/Summon/Editor/SynergyIconGenerator.cs` → `아이콘·텍스처 > 시너지 아이콘`,
  경로 정본은 `Editor/SynergyIconAssets.cs` (`3.Textures/Icons/Synergies/synergy_<Tag>.png`)
  - ⚠ **시너지 이름은 글자가 아니라 그림이다** — 상단 줄·카드 3택 둘 다 [아이콘][숫자] 다
  - ⚠ 배열 순서는 언제나 `MonsterSynergyRule.AllTags` — 런타임이 `IndexOf` 로 그림을 찾는다.
    순서가 어긋나면 "숲인데 해골이 뜨는" 상태가 된다
  - ⚠ TMP 스프라이트 태그로 글 안에 박지 말 것 (UI 규칙 7) — 자리가 정해진 칸이라 진짜 Image 를 쓴다
  - 표식을 추가하면 `SynergyIconGenerator.Table` 에도 한 줄 넣고 다시 구울 것
- **효과 설명(툴팁)**: `InGame/UI/SynergyChipUI.cs` — 칩에 올리거나 누르면 동·은·금 3줄이 뜬다
  - ⚠ 툴팁 본체는 `UI/Common/TooltipLayer.cs` 가 **최상단 캔버스에 한 장만** 세운다
    (sortingOrder 32000 · DontDestroyOnLoad). 칩마다 달면 그 캔버스에 갇혀
    카드 면 밑으로 깔리고 둘이 동시에 뜬다. 새 툴팁이 필요하면 이 층을 쓸 것
  - 글은 `MonsterSynergyRule.DescribeAll` 이 **수치 함수에서 뽑아** 만든다. 손으로 적지 말 것 —
    한 번 적어 두면 밸런스를 고쳐도 툴팁만 옛 숫자를 말한다
  - 툴팁 본체는 공용 `InfoTooltipUI`(`InfoTooltipBuilder.Build`)다 — 특성 아이콘과 같은 것
  - ⚠ 칩의 바탕 Image 는 `raycastTarget = true` 여야 한다. 꺼지면 눌러도 안 뜬다
- ⚠ **집계 내부에서 `CountOf` 를 부르지 말 것** — 그건 강화 카드의 +1 을 얹어 돌려준다.
  `AddTags` 가 그걸 썼다가 종족을 셀 때마다 강화가 함께 곱해져, 슬라임 한 장 + 숲 강화가
  **3** 으로 세어졌다. 날것은 `RawCountOf` 다
- **표식 조회**: 필드에서 "이 개체가 무슨 표식인가" 는 `MonsterTagComponent` 가 정본이다.
  범위 효과(숲 사망 회복 등)가 이걸로 대상을 거른다 — 없으면 그냥 광역 힐이 된다
- ⚠ **술법 쿨감을 실제로 먹이는 곳은 `MonsterRuntimeBridge.BuildSkillSlot`** 이다.
  한때 그쪽이 SO 쿨다운을 그대로 박아 술법 시너지가 통째로 무효였다.
  술법의 예산은 전부 쿨다운 하나다 — 스킬 피해 축은 만들지 않는다 (사용자 확정)

### 소환사 시그니처 스킬 (무료 · 횟수 제한)
- **정본**: `InGame/Summon/SummonerSkillRule.cs` — 남은 횟수·발동을 한 곳이 소유한다
- ⚠ **피해는 대상 최대 체력 비례 × 패기 배율이다** (사용자 지시, 2026-09-15) — 정본 `SignatureDamageRule` (SummonerSkillRule.cs 끝)
  - 예전엔 소환사 공격력(패기 × 2 = 4~20) × 배율이라 후반에 메테오가 20 을 때렸다
  - 비율(패기 곱하기 전): 메테오 30% · 비석 5%/개 · 사형 선고 20%(35% 이하 처형은 그대로) · 독성 지대 1.5%/0.5초 ·
    피의 대가 태운 체력 1당 1% · 마나 폭발은 위 표. 패기 배율 = `SummonerVigorRule.StrikeMultFor`(평타와 같은 값)
  - 방어율 무시 · 보스 절반. 러너들은 `SignatureDamageRule.Hit` 하나를 지난다
  - ⚠ **시전자가 소환사일 때만** (`SummonerStrikeComponent`) — 같은 스킬을 쓰는 용사 보스·2차 몬스터는 옛 공식 그대로다
  - ⚠ 스킬 설명(`ActiveSkillCreator`)은 용사·몬스터와 공유라 옛 공식 문구가 남아 있다 (마나 폭발만 소환사 전용이라 고쳤다)
- 고블린 두목 개성 '약탈'(`Plunder`) = **런 골드 × PerkValue(1.5)** (`RunGoldRule.Grant`, 2026-09-15) — 옛 효과(스테이지 마나 회복 ×)는 걷었다
  - ⚠ **마나를 내지 않는다.** 세기는 오직 **횟수**로 조절한다 —
    마나를 매기면 "스킬을 아끼려 소환을 줄이는" 상태가 생겨 두 자원이 서로를 잡아먹는다
  - 스테이지당(`SkillUsesPerStage`)은 **저장하지 않는다**(앱 재시작이 리필이 된다).
    런 중(`SkillUsesPerRun`)은 `SummonRunData` 에 저장한다(저장 안 하면 재시작이 리필)
  - 발동은 소환사 엔티티에 `UseActiveSkillTag` 를 붙여 기존 파이프라인에 넘긴다.
    ⚠ 쿨다운을 0 으로 함께 내린다 — 이 스킬의 제한은 쿨다운이 아니라 횟수다
- **데이터**: `SummonerData.SignatureSkill / SkillUsesPerStage / SkillUsesPerRun /
  SignatureSummonSpecies / SignatureSummonCount`, 로스터 정본은 `Editor/SummonerCreator.cs`
- **소환형**: `InGame/Skill/Actives/ActiveSummonSignature.cs` (`ActiveSkillId.SummonSignature = 34`)
  - 스킬 하나가 여섯 소환사를 덮는다 — **무엇을 부르는지는 소환사가 들고 있다**
  - ⚠ **`generation: 0` 으로 낸다** (사용자 지적, 2026-09-07) — `SpawnFree` 의 기본값은
    1세대(= 죽음에서 되살아난 개체)다. 그대로 두면 불러낸 슬라임이 죽어도 **분열이 안 터진다**
    (`SpeciesPassiveRule.MaxReproduceGeneration = 0`). 사망 발동 패시브가 통째로 죽어
    "같은 몬스터인데 스킬로 부르면 다른 것" 이 된다. 증식은 안 샌다 — 그 개체가 낳는 것이 1세대다
  - ⚠ `MonsterSpawner.SpawnFree` 를 지나야 한다. 그 경로가 시너지·강화·품질을 얹어 준다.
    직접 스폰하면 "시너지를 안 받는 몬스터" 가 생긴다
  - ⚠ **덱에 그 종족이 있으면 그 카드로 낸다** (`MonsterSpawner.DeckCardFor`, 2026-09-09)
    한때 `MonsterOrigin.None` 이라 **언제나 Lv1** 이었다 — 견습(슬라임 3기)으로 부른
    슬라임이 적진 한복판에서 한 방에 죽었다. Lv4 카드와 견주면 공/체 +36% ·
    레벨 표의 체력·방어율 · **Lv4 에 열리는 패시브**가 통째로 빠진 개체였다.
    ⚠ 물량은 그대로다 — 세지 않고(`MarkSummoned` 없음) 대기열로도 안 돌아간다.
    바뀌는 것은 **세기**뿐이다 (키운 카드가 스킬에도 반영된다)
  - ⚠ `MarkSummoned` 는 부르지 않는다 — **받기만 하고 주지는 않는다**.
    스킬로 부른 개체가 시너지 카운트를 올리면 마나 없이 시너지를 켜는 길이 생긴다
- **⚠ 조작은 카드와 같은 두 단계다** (2026-09-06 확정) — 버튼을 누르면 **겨냥**이 켜지고
  (`SummonerSkillRule.IsArmed`), **다음 탭이 위치를 정한다**
  - ⚠ **쓰고 나서도 겨냥은 켜져 있다** (사용자 확정, 2026-09-07) — 카드가 한 번 고르면
    유지되는 것과 같다. 푸는 자리는 셋뿐 — 버튼 재클릭 · 카드 선택 · 횟수 소진(`TryUseAt`)
  - ⚠ **겨냥을 켜면 고른 카드가 풀린다** (`SummonController.HandleArmedChanged`) —
    카드를 고르면 겨냥이 풀리는 것의 반대편이다. 한쪽만 있으면 둘 다 켜진 채로 남아
    다음 탭이 어디로 갈지 화면만 보고는 알 수 없다
  - ⚠ 그래서 `HandleCardSelected` 는 **해제(slot < 0)일 때 `Cancel()` 을 부르지 않는다** —
    부르면 방금 켠 겨냥이 자기가 일으킨 해제 통지에 도로 꺼진다
  - ⚠ **탭한 자리에 그대로 나온다** — 성벽 옆이 아니다. 거리를 건너뛰는 것이 이 스킬의
    값어치고, 횟수 제한(스테이지당 1~2회)이 그 값이다. **카드 소환은 여전히 성벽 옆에서만**
    나온다 — 마나로 사는 카드에 같은 자유를 주면 라인 개념이 사라진다.
    성벽보다 왼쪽만 막는다(벽 뒤에 갇힌다)
  - 탭을 받는 곳은 `SummonController.ReadTap` 하나다 — 겨냥이 카드보다 먼저다.
    카드를 고르면 겨냥이 풀린다(`HandleCardSelected`) — 둘 다 "다음 탭" 을 기다려 겹칠 수 없다
- **아이콘 굽기**: `Tools > Project K > 아이콘·텍스처 > 시그니처 스킬 아이콘`
  (`IconGenerator.GenerateSignatureIcons`)
  - ⚠ `skill_summon_signature.png` 가 **한 번도 안 구워져 있었다** (사용자 지적, 2026-09-07)
    그림은 `DrawSummonSignature` 에 있었는데 굽는 메뉴가 올드Tools 안뿐이라 아무도 안 눌렀다.
    소환사 12명 중 8명이 쓰는 그림이다
  - 이 메뉴는 굽고 나서 **소환사 전원의 시그니처 아이콘이 실제로 있는지 검사**한다.
    없으면 어느 소환사·어느 스킬인지 이름을 대고 에러를 낸다
  - ⚠ 구운 뒤 `데이터 생성 > SpriteManager + 아틀라스` 를 실행해야 화면에 뜬다
- **아이콘**: 전용 세트를 만들지 않는다 — **액티브 스킬 아이콘을 그대로 쓴다**
  (`ActiveSkillIdExtensions.IconKey` → `SpriteManager.Get`). 시그니처는 여섯 종류뿐이고
  여러 소환사가 나눠 쓴다(`SummonSignature` 하나를 여덟이 함께 쓴다) — 소환사마다
  그림을 두면 같은 스킬이 여덟 얼굴을 갖는다
  - 새로 그린 것은 `skill_summon_signature` 한 장뿐이다. 나머지 다섯(비석·사형선고·
    독성 지대·메테오·피의 대가)은 원작 스킬이라 이미 있었다
  - 굽는 곳은 `IconGenerator`(= `skill_*.png` 의 정본) → **`Tools > 올드Tools >
    아이콘·텍스처 > 직업·스킬 아이콘`**, 그다음 `데이터 생성 > SpriteManager`(아틀라스 갱신)
- **버튼**: `InGame/UI/SummonerSkillButtonUI.cs` — 하단 카드 바 **오른쪽 끝 칸**
  - ⚠ **쓸 수 있는지를 이벤트로만 갱신하지 말 것** — `StartStage` 는 `IsStageReady` 를 끈
    직후 `OnStageStart` 를 쏘는데 그때 BattleManager 는 아직 `InWave` 가 아니다.
    그 순간의 값이 굳어 **판 내내 버튼이 잠겨 있었다**. 판 상태는 이벤트 없이도 바뀐다 —
    `Update` 에서 `CanUse` 를 물어본다
  - 카드와 같은 크기(150×150)·같은 세로 중심. 중심이 말풍선 버튼과 같은 세로선(오른쪽 끝 162)
  - ⚠ 카드 줄(`CardRow`)의 오른쪽 끝을 이 칸만큼 줄여야 한다. 안 줄이면 마지막 카드가 올라탄다
  - **칸에는 그림과 남은 횟수뿐이다** (사용자 확정, 2026-09-07) — 스킬 이름·"판당" 은 뺐다.
    150px 칸에 글자가 둘이면 그림이 76px 로 쪼그라들어 무엇을 누르는지가 제일 안 보인다.
    시그니처는 소환사당 하나뿐이라 이름을 읽고 고를 일이 없다.
    세로 예산은 `InGameUIPrefabCreator` 의 `SkillIcon*`/`SkillCount*` 상수가 정본
  - ⚠ **그림은 아직 안 구워져 있으면 빈 칸으로 뜬다** — `Refresh` 가 `art == null` 이면
    `_icon.enabled = false` 로 끈다. `skill_summon_signature.png` 를 구웠는지 먼저 볼 것
- **⚠ 소환사가 쓸 수 없는 스킬이 있다** — 원작 스킬은 대부분 **용사(General)** 를 전제한다
  - `GeneralRuntimeBridge` 를 요구하면 소환사에게는 그 컴포넌트가 없어 **조용히 return** 한다
    (`Gravestone` 이 그랬다 → 베이스의 `UnitRuntimeBridge.RolledStat` 로 고쳤다)
  - 병사·장군을 쿼리하는 스킬은 대상이 0 이라 무효거나 반쪽만 된다
    (치유 오라·집중 치유·일제 사격·광전사·신속 연격·돌격 병사·병사 희생·자폭 병사·정예 소환)
  - **아군 버프 계열은 통째로 맞지 않는다** — 시전자를 따라다니며 반경을 그리는데
    소환사는 성벽 뒤 고정이라 반경 안에 아군이 없다 (`WarBanner`·`BattleCry`)
  - 근접 이동형(강타·도약 강타·충격파·일도양단·관통 돌진)도 고정 시전자에겐 무의미하다
  - ⚠ **새 스킬을 소환사에게 붙이기 전에 위 셋을 먼저 확인할 것**
- **⚠ 겨냥할 곳은 `SummonerSkillRule.AimPoint` 가 정한다** — 성벽에 가장 가까운 적,
  없으면 성벽 앞 5. 소환사는 대부분 공격 타겟이 없어서 기본 경로에 맡기면 발밑에 떨어진다
- **⚠ 스킬 카드는 3택에서 제거됐다** (`CardRewardPicker`). `SkillCardData`·`SkillCardCaster` 는
  남겨 둔다 — 시그니처 스킬 **강화 카드** 한 장이 쓸 자리다

### 시너지 강화 카드
- **정본**: `MonsterSynergyRule.BoostedTag` / `ApplyBoost` — 런에 **한 장뿐**
- 효과: 그 시너지 **카운트 +1** + 소속 몬스터 **공/체 +15%**(`BoostStatBonus`)
  - ⚠ 카운트는 `CountOf` 안에서 더한다. 다른 곳에서 또 더하지 말 것 — 두 번 오른다
  - ⚠ **덱 칸을 먹지 않는다.** 8칸이 빠듯해 칸을 먹으면 제로섬이 된다
  - 후보는 **이미 한 장이라도 낸 시너지**만 — 0장짜리는 고르면 손해인 선택지다
- 저장: `SummonRunData.boostTag` (이어하기). ⚠ `RestoreBoost` 는 `Bind` **앞**에서 부른다
- 카드 3택 표시: `CardSelectPopup.BindSynergyBoost` (`CardRewardOption.IsSynergyBoost` 로 갈린다)
  - 결론 줄은 **"언데드 카운트 +1" 한 줄뿐이다** (사용자 확정, 2026-09-07)
    ⚠ 여기에 덧붙이지 말 것 — "(2/3)" 도 "은 단계 개방" 도 안 된다.
    둘 다 화면이 이미 말하는 것을 대신 계산해 준 것이다(개수와 문턱은 상단 시너지 줄).
    **판단은 플레이어가 한다**

### 몬스터 외형 (무기·신체)
- **무기 풀**: `InGame/Appearance/EnemyAppearanceRoller.cs` — **종족(EnemyRace)마다 다르다**
  - ⚠ 풀끼리 **겹치지 않는다.** 한 자루라도 겹치면 그 자루를 든 순간 두 종족이 같아 보인다
    (한때 20자루 공통 풀 하나라 고블린과 좀비가 같은 칼을 들었다)
  - ⚠ 이름은 벤더 에셋(PixelFantasy)의 슬롯 이름이다. 없는 이름을 적으면 무기가 안 그려진다
  - **무기 풀은 공격 형태로도 갈린다** (2026-09-12 구현) — `RangedWeaponsFor` 가
    원거리 종족에게 활·지팡이를 준다 (고블린 = ShortBow·Bow · 해골 술사 =
    NecromancerStaff·SkullWand · 리치 = ElderStaff·StormStaff)
    - ⚠ **근접 풀과도 서로끼리도 겹치지 않는다** — 근접 풀과 같은 규칙이다
    - ⚠ **공격 형태가 캐시 열쇠에 들어간다** (`_cache` 튜플 · `UnitAppearanceBridge._lastAttackKind`) —
      빼면 같은 이름의 근접 개체가 먼저 합성됐을 때 궁수가 그 무기를 물려받는다
    - ⚠ **초상화도 같은 것을 넘긴다** (`MonsterPortraitProvider.BuildHumanoid`) —
      안 넘기면 필드에서는 활, 카드·도감에서는 낫이 된다 (장비와 같은 이유)
    - 멜리가 기본값이라 용사·스켈레톤 소환 등 옛 호출부는 그대로 근접을 든다
- 신체·머리·눈·귀는 전부 `EnemyRace` 이름 그대로다. 비인간형(슬라임·늑대·멧돼지·트롤)은
  `NonHumanoidLibrary` 통짜라 이 경로를 타지 않는다

### 색조·덩치 — 종족을 눈으로 가르는 두 축 (2026-09-09, 사용자 요청)
> 값의 정본은 `MonsterCodexCreator` 로스터의 `Tint`/`Size` 다 (필드는
> `MonsterSpeciesData.BodyTint`/`BodyScale`). **바꾸면 `데이터 생성 > 몬스터 도감` 을 다시 굽는다.**

- **색조 = 계보 안에서 가른다** — 진화체는 뿌리와 **같은 그림**이다
  (인간형은 같은 `EnemyRace`, 비인간형은 같은 라이브러리). 힐·독·강철 슬라임이
  라인에 서면 한 마리로 보였다. 뿌리는 흰색, 진화체 13종에 옅은 색을 준다
  - 인간형도 물들인다 — `ApplyHumanoid` 이 `ApplyEnemy` **뒤에** `Tint` 를 건다
  - ⚠ 힐 슬라임의 몸 비율(1.20, 0.84)은 **걷어냈다** (사용자 지적, 2026-09-16 — 인게임에서 납작) — 분홍 시트가 이미 색으로 가른다
    (Rebuild 는 같은 `SpriteRenderer` 에 라이브러리만 갈아 끼우므로 정점 색이 살아남는다)
  - 비인간형은 **장비 색조와 곱한다** (`gear.Tint * species.BodyTint`) — 종족 색은
    "무엇인가", 장비 색은 "무엇을 입었나" 다. 덮어쓰면 한 축이 죽는다.
    ⚠ 그래서 양쪽 값 모두 옅어야 한다 (0.6 아래로 내려가면 실루엣이 된다)
  - **초상화에도 같은 값을 곱한다** (`MonsterPortraitProvider.CropAndTrim`) —
    전장만 물들이면 카드·도감·전황에서 다시 같은 그림이 된다
- **덩치 = 종족끼리 가른다** — 고블린·오크·좀비는 셋 다 초록 계열 인간형이라
  색으로는 안 갈린다. 고블린 0.85 · 좀비 1 · 오크 1.2 · **트롤 1.45**(가장 큼)
  - 곱하는 곳은 `MonsterSpawner` 의 `visualScale` **한 곳뿐**이다
  - ⚠ **히트박스·분리 반경이 함께 커진다** (`UnitSizeComponent.Radius` 가 `localScale`
    에서 나온다). 덩치가 곧 자리를 먹는다 — 값을 키우면 라인이 얼마나 비는지 함께 볼 것
  - ⚠ 분열체에도 곱해진다(같은 종족이니 맞다). 반쪽(0.5)이 종족 덩치 위에서 반쪽이 된다
  - ⚠ 초상화에는 안 쓴다 — 여백을 걷어내 칸에 꽉 채우므로 크기를 곱해도 결과가 같다
- **실루엣(`BodyStretch`) = 색조가 못 하는 일** (사용자 지적, 2026-09-09)
  - 색조는 원본 그림에 **곱해지는** 값이라 바탕이 짙은 종족일수록 차이가 묻힌다.
    실제로 독 슬라임과 일반 슬라임이 "거의 같아 보인다" 였다 (넷 다 `SlugLibrary`)
  - 그래서 계보를 **몸 비율**로 가른다 — 힐(1.20, 0.84) 납작 · 독(0.84, 1.24) 길쭉 ·
    강철(1.12, 1.02)+덩치 1.12 · 서리 늑대(1.08, 0.94) · 화염 멧돼지(1.10, 1.06)+덩치
  - ⚠ 히트박스·분리 반경은 `max(x, y)` 에서 나온다 — 크게 벌리면 그쪽도 커진다
  - ⚠ 초상화(카드·도감)에는 아직 안 들어간다. 더 갈라야 하면 다음 수단은
    **종족 표식**(`MonsterGearAuraView` 식의 작은 정점색 스프라이트)이다 —
    벤더 에셋에 슬라임 변종 그림이 없어(Hog/Slug/Troll/Wolf 넷뿐) 새 그림을 굽지
    않는 한 색·모양·표식 셋이 가진 수단의 전부다
  - ⚠ **사거리와 한 묶음이다** (사용자 지적, 2026-09-09) — 공격 판정은 **중심 사이
    거리**를 본다(`UnitAttackSystem`). 덩치를 키우면 분리(Separation)가 중심을 더
    멀리 떼어 놓아, 그림에서는 몸이 거의 닿았는데 사거리 밖이라 가만히 서 있는다.
    트롤 1.6 → **2.1** · 숲의 트롤 1.2 → **1.7** 로 함께 올렸다.
    ⚠ 덩치를 올리는 종족은 사거리도 같이 볼 것 — 늘어난 반경(`0.75 × (Size−1) / 2`)이
    최소 기준이다
- ⚠ **Animator 가 색을 매 프레임 흰색으로 되쓴다** (사용자 지적, 2026-09-11)
  벤더 `Hit.anim`·`Heal.anim` 이 `m_Color` 를 움직여서, 컨트롤러 전체가 Write Defaults 로
  **모든 상태에서** 바인드 시점의 색(흰색)을 되써 넣는다. 종족 색조·중독 초록·피격 번쩍임이
  필드에서 전부 지워졌다(애니메이터 없는 초상화에만 보였다).
  → `UnitAnimationSync.LateUpdate`(Animator 뒤)가 물든 개체만 `_currentTint` 를 다시 입힌다
  (`KeepTint` 는 `MonsterAppearanceBridge.Tint` 가 켠다). ⚠ 색을 입히는 새 코드도 `SetTint` 를 지날 것
- **머리 위 표식** `MonsterMarkView`(`MonsterSpeciesData.Mark`) — 힐 슬라임 십자 · 독 슬라임 물방울 ·
  슬라임 킹 왕관. 도트는 코드가 찍는다(에셋 없음)
- **슬라임 계열은 색을 시트로 굽는다** (사용자 지시, 2026-09-11) — 색조(곱하기)를 쓰지 않는다
  | 종족 | 라이브러리 | 색 |
  |---|---|---|
  | 슬라임 · 슬라임 킹 | `SlugLibrary_Yellow` | 노랑 |
  | 힐 슬라임 | `SlugLibrary_Pink` | 분홍 |
  | 독 슬라임 | `SlugLibrary` (원본) | 원본 초록 그대로 — 변형을 굽지 않는다 (사용자 지시) |
  | 강철 슬라임 | `SlugLibrary_Steel` | 회청 |
  | 서리 늑대 | `WolfLibrary_Frost` | 얼음 청색 (늑대는 원본 회색) |
  | 화염 멧돼지 | `HogLibrary_Flame` | 불꽃 주황 (멧돼지는 원본 갈색) |
  | 숲의 트롤 | `TrollLibrary_Forest` | 초록 피부 (트롤은 원본 갈색) |
  - 늑대·멧돼지·트롤은 10~17색이라 **범위 색상 이동**(`HueShift`)을 쓴다 — 몸 색이 모인
    색상·채도 범위만 색상을 옮기고 밝기는 유지. 눈·이빨·옷은 범위 밖이라 남는다
  - 굽는 곳: `MonsterLibraryCreator.Variants` (`데이터 생성 > 비인간형 몬스터 라이브러리`).
    슬라임 시트는 **다섯 색뿐**(검은 외곽선 + 초록 넷)이라 색상 회전이 아니라 **네 색을 정확히 맞바꾼다**
  - 시트는 `AssetDatabase.CopyAsset` 으로 복사해 .meta 의 자르기 정보를 그대로 가져간다(스프라이트 이름 유지).
    ⚠ 이미 있으면 다시 복사하지 않는다 — GUID 가 바뀌면 라이브러리 참조가 끊긴다
  - ⚠ 벤더가 시트 색을 바꾸면 바뀐 픽셀이 0 이 되어 **에러로 알린다** — `From` 을 다시 잴 것
  - ⚠ 이 종족들의 `BodyTint` 는 흰색이다 — 곱하면 시트 색이 탁해진다
  - 슬라임 킹은 `SummonerData.AppearanceScale`(1.6) — 원래 크기를 한 번만 기억하고 거기서 곱한다(씬이 런 사이에 상주한다)
- **몬스터 모습의 소환사** — `SummonerData.AppearanceSpecies`(+`AppearanceMark`)가 있으면
  `SummonerRuntimeBridge` 가 `MonsterAppearanceBridge` 로 통짜 라이브러리를 꽂는다(슬라임 킹 = 슬라임 + 왕관).
  선택 카드·통계 초상화도 그 종족 초상화를 쓴다
- ⚠ **색조는 '기준색' 으로 못 박아야 한다** (사용자 지적, 2026-09-09)
  피격 플래시(`UnitAnimationSync`)는 `기준색 × 배수` 로 걸고 끝나면 배수를 흰색으로
  되돌린다. 기준색을 Awake 에서 한 번만 잡으면 그것은 **프리팹의 흰색**이라,
  ① 스폰 직후 `ClearTint`(`UnitRuntimeBridge.SpawnEntity`)와 ② 첫 피격에서
  종족 색조가 통째로 지워졌다 — **도감·카드에는 색이 보이는데 필드에서만 원래 색**이
  이 증상이다. `MonsterAppearanceBridge.Tint` 가 색을 입힌 뒤
  `UnitAnimationSync.CaptureBaseColors()` 로 기준색을 다시 잡는다
  - ⚠ 플래시 중에 부르면 빨간색이 기준으로 굳는다 — `CaptureBaseColors` 가 먼저 끊는다
  - ⚠ 렌더러 목록도 다시 잡는다 — 인간형은 합성 뒤에 무기 렌더러가 늘어난다
- ⚠ **둘 다 계보로 상속되지 않는다** (스탯과 다르다) — 뿌리와 갈려 보이라고 두는 값이라
  물려받으면 목적이 사라진다. 뿌리와 같은 덩치를 쓰려면 그 종족에 다시 적는다

### ⚠ 도트 틱은 `HitType.Dot` 이다 — 아무 반응도 일으키지 않는다 (2026-09-11)
- 한때 `UnitStatusEffectSystem` 이 도트를 **스택마다 · 매 프레임** `HitType.Normal` 로 넣었다.
  그러면 틱 하나하나가 ① 방어율 하한 **1**(`DamageMath.AfterDefense`)을 받아 초당 60 이상으로 부풀고
  ② 파쇄·소환사 평타(최대 체력 비례)가 붙고 ③ 역병 중독(`InflictOnHit`)이 **틱마다 새 도트를 걸어**
  스택이 지수로 불었다. 증상: 독 슬라임을 때린 용사 즉사 · 무한 보스 즉사 ·
  `BattleStatCollectorSystem` 100ms + ECB 재생 20ms
- 지금: 도트는 프레임마다 **한 건으로 합쳐** `HitType.Dot` 으로 넣고(전과 = 가장 센 도트의 시전자),
  `UnitHitSystem` 은 `reacts`(Normal·Skill 만) 로 파쇄·거울·가시·중독 전이를 거른다.
  도트는 **방어율을 지나지 않고** 넉백·경직도 없다
- ⚠ 새 반응을 `UnitHitSystem` 에 넣으면 `reacts` 를 조건에 쓸 것 — `!= Reflected` 로 쓰면 도트가 샌다

### 지속 피해(도트) 중이면 몸이 초록빛이다 (2026-09-10, 사용자 요청)
- 정본은 `HitReactionComponent.IsPoisoned` / `.DotLook` — `UnitStatusEffectSystem` 이
  매 프레임 "Dot 버프가 있나 · 무슨 종류인가" 를 다시 쓰고, `UnitAnimationSync` 가
  그걸 보고 물들인다 (`_poisonTintColor` / `_burnTintColor`)
- **색은 종류가 정한다** (`DotKind`, 사용자 확정 2026-09-10) — 독·역병=초록 · 화상=붉은빛
  - ⚠ **겹치면 독이 이긴다.** `DotKind` 는 **값이 곧 우선순위**다(작은 쪽이 이긴다).
    우선순위가 없으면 나중에 붙은 쪽이 이겨 같은 조합인데 판마다 색이 달라진다
  - ⚠ 기본값(0)이 독이다 — `Dot` 을 안 적은 효과는 전부 초록이다(속박·역병 폭발 등)
  - 종류는 `StatusEffectBufferElement.Dot` 을 타고 흐른다. `RetaliateComponent.EffectDot` ·
    `InflictOnHitComponent.EffectDot`/`ExtraDot` 이 `UnitHitSystem` 을 거쳐 실어 보낸다 —
    ⚠ 새 Dot 을 만들면 **그 셋 중 어디를 지나는지** 보고 종류를 함께 실을 것.
    안 실으면 에러 없이 초록으로만 뜬다
  - ⚠ 피해 계산과 무관하다. **색만** 정한다 — 속성 상성을 여기에 얹지 말 것
- ⚠ **버퍼를 또 열지 않는다** — `UnitAnimationSync` 는 `HitReactionComponent` 를 이미
  매 프레임 읽는다(`NeedsFlash`). 도트 여부를 알려고 `StatusEffectBuffer` 를 한 번 더
  여는 것보다 지나가는 값에 얹는 쪽이 싸다
- ⚠ `NeedsFlash` 와 달리 **읽은 쪽이 지우지 않는다** — 사건이 아니라 상태다.
  도트가 끝나면 시스템이 저절로 false 를 쓴다
- ⚠ 피격 플래시가 끝나면 흰색이 아니라 `RestTint` 로 돌아간다 — 흰색으로 되돌리면
  도트가 남았는데도 초록이 벗겨져 "맞을 때마다 중독이 풀린" 것처럼 보인다
- ⚠ `ClearTint()` 가 `_isPoisoned` 도 내린다 (풀 재사용) — 안 내리면 초록이 굳은 채
  풀에 들어가 멀쩡한 다음 유닛이 물든 채로 선다
- ⚠ 종족 색조 위에 **곱해진다** — 원래 색이 짙은 종족은 초록이 어둡게 앉는다.
  그게 맞다 (색조는 "무엇인가", 도트는 "지금 어떤 상태인가")
- ⚠ 진영을 가리지 않는다 — 아군 몬스터가 용사의 독성 지대를 밟아도 물든다

### 용사 계층 몸집 (사용자 지시, 2026-09-11)
- 일반 1 · 엘리트 **1.2** · 보스 **1.4** — 정본 `HeroTierSetup.EliteScale/BossScale/ScaleFor`
- 곱하는 곳은 `HeroSpawner.SpawnOne` → `GeneralRuntimeBridge.Initialize(scaleMult)` 하나다
  - ⚠ 엔티티가 만들어지기 **전**이어야 분리 반경이 따라온다 — `ApplyBoss/ApplyElite` 안에서 `localScale` 을 만지지 말 것
  - ⚠ 무한 보스(×2)는 큰 쪽을 쓴다 (`Max`) — 곱하면 ×2.8 이 된다
  - 풀 재사용은 안전하다 — `Initialize` 가 매번 `localScale` 을 1 로 되돌린다

### ⚠ 풀에서 재사용되는 장수는 **승격을 되돌려야 한다**
- `HeroTierSetup.ApplyBoss/ApplyElite` 가 붙이는 `BossComponent`·`EliteComponent` 를
  **떼는 코드가 코드베이스에 하나도 없었다** (사용자 지적, 2026-09-07)
  - 장수 오브젝트는 풀에서 재사용된다. 5스테이지 보스가 쓰던 엔티티를 6스테이지의
    평범한 용사가 물려받으면 **그 용사가 그대로 보스다** — AoE 평타, 평타 ×3,
    보스 HP 바, 광폭화까지. 허들을 지날 때마다 풀에 보스가 하나씩 늘어난다
  - 증상이 "6스테이지에 또 보스가 나온다" 였다. 스테이지 종류(`RunStageKindRule.Of`)는
    멀쩡했다 — 문제는 **엔티티 상태가 판을 넘어 살아남은 것**이다
- 지금은 `GeneralRuntimeBridge.Initialize` 가 `RangedTag`·`RetreatFireTag`·`TauntTag` 를
  정리하는 그 자리에서 **승격 컴포넌트와 보스 패턴 슬롯(`ActiveSkillSlot`)도 비운다**
  - ⚠ 여기서 지워도 안전하다 — `HeroSpawner.SpawnOne` 이
    `Initialize` → `ApplyBoss/ApplyElite` 순서로 부른다. **이 순서를 바꾸지 말 것**
  - ⚠ 컴포넌트만 떼고 슬롯을 남기면 일반 용사가 보스 기술을 쓴다. 둘은 한 묶음이다
- ⚠ **풀 재사용 유닛에 붙이는 것은 전부 떼는 자리가 있어야 한다.** 붙이는 코드를
  쓸 때 떼는 코드를 같이 쓰지 않으면, 증상이 "가끔 이상한 개체가 나온다" 로만 보인다

### 소환사 자리 — 왼쪽 UI 줄기가 덮지 않는 선
- 정본은 `InGameSceneSetup.CourtyardBias` — 안뜰 안에서 소환사가 서는 비율
  (0 = 안뜰 왼쪽 끝, 1 = 성벽 몸통과 닿는 선)
- ⚠ **0.5 → 0.78** (사용자 지적, 2026-09-08) — 안뜰 한가운데는 화면 x≈93px 이라
  왼쪽 가장자리 UI 줄기(특성·체력·시너지, x 16~192) 밑에 통째로 묻혔다. 0.78 이면 x≈145px
- ⚠ 1 로 밀지 말 것 — 성벽 몸통에 붙으면 다시 "성벽 위에 올라선" 그림이 된다
- ⚠ 바꾸면 **씬을 다시 구워야 한다** (`씬 셋업 > 인게임 전장`) — 소환사 자리는 씬에 직렬화된다

### 갈림길 '이벤트' (RunEvent) — 2026-09-08 신규
> **정본은 `InGame/Summon/RunEvent.cs` 하나다** — 표 · 값 · 판정 · 적용이 전부 거기 있다.

- **이벤트만의 축은 지불 수단이다** — 시설 넷은 **골드**로 사고, 이벤트는 **골드로 사지 않는다.**
  지갑이 비어도 고를 수 있는 칸이고, 대신 내는 것이 목숨이거나 손에 든 카드다
  - ⚠ 이벤트가 골드로 사기 시작하면 시설과 같은 물건이 된다 — 갈림길에 넣을 이유가 사라진다
  - **대가는 셋이다** (`RunEventCost`) — `Core`(체력 N) · `HalfCore`(지금 체력의 절반) ·
    `LowestCard`(덱에서 가장 약한 카드 한 장)
    - ⚠ 상태가 정하는 대가는 화면에 **지금 얼마인지**를 적는다 (`DescribeCost`) —
      "절반" · "가장 약한 카드" 로만 적으면 고르기 전에 계산과 확인을 시킨다.
      실제 숫자와 카드 이름을 뽑아 주는 것이 그 갈래의 재미다
    - ⚠ 새 대가를 만들면 `DescribeCost`·`CanPay`·`Pay` **셋 다** 채울 것 —
      하나만 빠지면 대가가 안 적히거나, 못 낼 것을 고르게 되거나, 공짜가 된다
    - ⚠ `LowestCard` 는 **마지막 한 장을 못 바친다** (덱이 비면 아무것도 못 소환한다).
      제물 고르기는 `LowestLevelSlotForSacrifice` 로 `LowestLevelSlot`(레벨 올릴 카드)과
      **다른 함수**다 — 제물은 만렙이어도 바칠 수 있어야 갈래가 안 죽는다
    - ⚠ `Pay` 는 카드를 지우기 **전에** 이름을 읽는다 — 지운 뒤엔 무엇을 바쳤는지 말할 수 없다
- **무작위는 '어느 이벤트를 만나는가' 뿐이다. 고른 뒤의 결과는 확정이다**
  (품질 개선의 "확정이다. 확률이 아니다" 와 같은 규칙)
  - ⚠ 원작 `EventChoice.SuccessRate` 를 쓰지 않는 이유다 — 결과까지 확률이면 "골랐는데 꽝" 이 된다
- **갈래는 언제나 셋이다 — 보상 · 골드 · 회복** (사용자 확정, 2026-09-08)
  | 갈래 | 값 | 받는 것 |
  |---|---|---|
  | 보상 | 마왕성 체력 −3/−4/−5 | 구조적인 것 (덱·시너지·성벽) — **이벤트마다 다른 유일한 칸** |
  | 골드 | 공짜 | `GoldUnit(stage) × 배수` |
  | 회복 | 공짜 | 마왕성 체력 `HealAmount` |
  - 순서의 정본은 `RunEventDef.Choices` 다. ⚠ 뒤집지 말 것 — 여덟이 같은 모양이라
    두 번째부터는 읽지 않고도 안다
  - 셋이 서로 다른 것을 묻는다: 보상은 "지금 체력을 걸어 나중을 살 것인가",
    골드·회복은 "돈이 급한가 목숨이 급한가". **공짜 갈래가 둘이라 체력이 바닥나도
    이벤트 칸이 손해가 아니다**
  - ⚠ **야영지는 많이, 이벤트는 조금** (사용자 확정, 2026-09-08) — **야영지 8 · 이벤트 3**
    - `RunNodeRule.CampHealAmount` 를 5 → **8** 로 올렸다. 마왕성 체력은 18~26 뿐이라
      (`BaseCoreHp` 10 + 체력×2) 5 는 최대치의 5분의 1 남짓이고, 이벤트 회복(3)과
      두 칸 차이라 **둘 다 "조금 회복" 으로 읽혔다.** 그러면 야영지를 고를 이유가
      "증축이 있으니까" 뿐이다
    - 이벤트 쪽은 숫자를 직접 적지 않고 야영지에서 **비율로** 만든다
      (`HealAmount` = `CampHealAmount × 0.35`, 하한 1 · 상한 `CampHealAmount − 1`)
      - ⚠ **뺄셈이 아니라 비율이다** — 전에는 `−2` 였는데 야영지를 5→8 로 올리자
        이벤트가 6 이 되어 폭이 그대로였다. 비율이면 야영지를 얼마로 올리든 3분의 1쯤에 남는다
      - ⚠ 마지막 `Min` 이 안전장치다 — 비율을 잘못 적어도 야영지와 같아지지 않는다
    - ⚠ 유물 '재건'(`CampHealBonus`)은 안 얹는다 — 그건 야영지의 보너스다.
      얹으면 유물 하나가 두 자리를 동시에 키운다
    - ⚠ 가득 찼으면 못 고른다 (야영지 '수리' 와 같은 규칙)
    - ⚠ 결과 문장은 **실제로 오른 만큼** 적는다 — 상한에 걸리면 `HealAmount` 보다 적다
  - ⚠ 광산만 보상 갈래가 골드다(×4) — 돈이 나오는 곳이라 그게 맞다. 대신 골드 갈래를
    가장 작게(×1) 두어 [큰돈 / 푼돈 / 회복] 이 서로 다른 크기로 읽히게 했다.
    나머지는 전부 구조적인 것을 준다
- **이벤트는 12종이다** (가벼운 판 8 + 무거운 판 4, 2026-09-08)
  | | 보상 갈래 | 값 |
  |---|---|---|
  | 광부의 갱도 | 골드 ×4 | −5 |
  | 잊힌 서고 | 특성 1 | −3 |
  | 떠도는 혼 | 가장 낮은 카드 Lv +1 | −4 |
  | 길 잃은 몬스터 | 새 카드 1장 | −3 |
  | 제물의 흔적 | 덱에 가장 많은 표식 +1 | −5 |
  | 깨진 소환진 | 가장 비싼 카드 마릿수 +1 | −4 |
  | 버려진 보급 수레 | 마왕성 최대 체력 +4 | −3 |
  | 떠돌이 대장장이 | 가장 비싼 카드 비용 −1 | −4 |
  | **봉인된 지팡이** | 시그니처 스테이지당 +1 | −5 |
  | **용사들의 무덤** | 마왕성 최대 체력 +7 | −5 |
  | **핏빛 달** | 가장 낮은 카드 Lv +2 | −5 |
  | **버려진 사육장** | 가장 비싼 카드 마릿수 +2 | −5 |
  - 무거운 판 넷은 **같은 물건을 더 크게 주되 더 비싸다**(전부 −5). 값이 하나뿐이면
    체력이 넉넉한 판과 빠듯한 판이 같은 무게로 읽힌다
  - ⚠ `RunEventGain.SignatureUse` 는 `RunBoonData.SignatureBonus` 에 쌓이고
    `SummonerSkillRule.Remaining` 이 읽는다 (특성 '집중' 과 **같은 자리**).
    시그니처가 없는 소환사에게는 갈래가 흐려진다
  - ⚠ `RunEventId` 번호 = 표의 자리다. `RunEventRule.Verify` 가 처음 쓰일 때 검사해
    어긋나면 에러를 낸다 — 중간에 끼우면 제목과 선택지가 어긋나고 '만난 기록' 도 밀린다
  - ⚠ 카드 레벨 +2 는 **같은 카드에 거듭** 얹는다 — 매번 가장 낮은 것을 다시 찾으면
    두 카드에 흩어져 "한 장이 크게 자란다" 가 아니게 된다
- **대가가 체력이 아닌 판 셋** (2026-09-08, 사용자 요청 "재미있는 선택지")
  | 이벤트 | 보상 갈래 | 대가 | 조건 |
  |---|---|---|---|
  | **버려진 둥지** | 몬스터 카드 **3장** | 체력 −5 | 빈 칸 3 |
  | **굶주린 우상** | 가장 비싼 카드 Lv +2 | **가장 약한 카드 한 장** | 몬스터 카드 2장 이상 |
  | **뒤집힌 모래시계** | 특성 **2개** | **지금 체력의 절반** | 절반 내고 1 이상 남을 것 |
  - 앞의 열둘은 전부 "체력 얼마" 라는 같은 질문이었다 — 값만 다르고 묻는 것이 같으면
    열두 판이 한 판처럼 읽힌다. 이 셋은 **무엇을 내는가**가 달라서 덱을 보고 고르게 된다
  - ⚠ 셋 다 못 고르는 판이 흔하다(빈 칸이 없다 · 카드가 한 장뿐 · 체력이 빠듯). 그래도
    골드·회복 갈래가 늘 살아 있어 칸이 죽지 않는다
  - ⚠ **여러 장을 줄 때는 빈 칸 수를 먼저 본다** (`SummonDeckData.FreeSlotCount`) —
    모자란 채로 주면 `Acquire` 가 0 을 돌려주고 그 장이 조용히 사라지는데 대가는 이미 냈다
  - ⚠ 여러 장은 **한 장씩 다시 뽑는다** — `CardRewardPicker` 가 덱을 보고 후보를 만들므로,
    한 번에 셋을 받아 두면 두 번째 장이 방금 넣은 것과 겹칠 수 있다
  - ⚠ 특성 2개는 **한 번에** 뽑는다 (`RunPerkPicker.Pick(data, 2)`) — 한 장씩 뽑으면
    같은 특성이 두 번 나온다. `Pick` 은 한 호출 안에서만 중복을 막는다
  - ⚠ `TopCardLevel`(가장 비싼)은 `CardLevel`(가장 낮은)과 **반대 대상**이다. 합치지 말 것 —
    "약한 것을 먹여 강한 것을 키운다" 는 이야기가 성립하려면 대상이 반대여야 한다
  - ⚠ `Apply` 는 **대가를 먼저** 치른다 — 우상은 제물을 지운 뒤에 대상을 다시 고르므로
    바친 카드가 곧 대상이 되는 일이 없다
- ⚠ **마나는 건드리지 않는다** (`Docs/GameDesign.md` 5.0절 — "이벤트 보상에 마나 회복 옵션을
  넣지 않는다"). 최대 마나는 상점의 '마력의 정수' 하나로 남긴다 — 상점만의 물건이 있어야
  갈림길에서 상점을 고를 이유가 된다
- **보상 어휘는 `RunEventGain` 8종뿐이다** — 골드 · 특성 · 카드 레벨 · 새 카드 ·
  시너지 카운트 · 마릿수 · 소환 비용 · 마왕성 최대 체력.
  ⚠ 새 종류를 만들면 `Describe`·`Available`·`Apply` **셋 다** 채울 것 — 하나만 빠지면
  설명이 빈칸이 되거나 못 하는 갈래가 눌린다 (에러는 안 난다)
- ⚠ **못 하는 갈래는 흐려진다** (`Available`) — 체력이 모자라거나(내고 1 미만이 되거나)
  받을 자리가 없으면 못 고른다. **"골랐더니 런이 끝났다" 를 어떤 이유로도 만들지 않는다.**
  안전 갈래는 늘 골드라 이벤트 칸이 막다른 길이 되지 않는다
- ⚠ **흐리게 그릴 때와 실제로 줄 때가 같은 함수를 써야 한다** (`RollCards`) —
  갈라지면 "고를 수 있었는데 눌러 보니 빈손" 이 된다. 체력은 이미 냈다
- **덱에서 대상을 고르는 규칙은 전부 결정적이다** — 레벨 갈래는 **가장 낮은 카드**,
  마릿수·비용 갈래는 **가장 비싼 카드**, 시너지는 **덱에 가장 많은 표식**. 동점이면 앞 칸.
  ⚠ 무작위로 고르면 결과 화면을 읽고도 왜 그 카드인지 설명할 수 없다
- **타이틀 그림은 이벤트마다 하나씩이다** (2026-09-08)
  - 굽기 `InGame/Summon/Editor/RunEventArtGenerator.cs` → `아이콘·텍스처 > 이벤트 그림`
    · 경로 정본 `Editor/RunEventArtAssets.cs` (`3.Textures/Icons/RunEvents/event_<Id>.png`)
  - ⚠ **갈림길 그림(`node_Event.png`)과 다른 물건이다** — 그쪽은 "이벤트라는 칸" 한 장
    (갈림길 카드에 뜬다), 이건 "어느 이벤트인가" 열다섯 장이다. 폴더도 따로다.
    한 장만 쓰면 열다섯 이벤트가 전부 같은 배경이라 **어디에 들렀는지가 화면에 안 남는다**
  - ⚠ 순서가 계약이다 — `RunEventId` 번호 = 배열 자리. 런타임은
    `FacilityPopup._eventArt[(int)id]` 로 찾는다. 중간에 끼우면 "잊힌 서고인데
    갱도 그림" 이 된다
  - ⚠ **SpriteManager/아틀라스를 안 쓴다** — 열다섯 장이 전부 굽는 시점에 정해져 있어
    Creator 가 `Sprite[]` 를 프리팹에 박는다 (시너지·종족 패시브와 같은 방식).
    특성 아이콘처럼 아틀라스 키 조회를 할 이유가 없다
  - ⚠ **굽는 순서** — `아이콘·텍스처 > 이벤트 그림` → `프리팹 생성 > 팝업 > 시설`
    → `PopupManager [Load Popup Prefabs]`. 그림이 없으면 `FacilityPopupCreator` 가
    **굽기를 멈춘다** (빈 배열을 구워 두면 프리팹만 보고 무엇이 빠졌는지 알 수 없다)
  - ⚠ 이벤트를 추가하면 `RunEventArtGenerator.Generate` 에도 한 줄 넣을 것 —
    끝의 `Verify` 가 빠진 것을 잡아 준다
  - 색으로 무게를 말한다 — 가벼운 판(−3/−4) 푸른 밤 · 무거운 판(−5) 붉은 기 ·
    대가가 체력이 아닌 셋 보랏빛
  - 지금 것은 **더미(도형)** 다. 손그림이 오면 같은 경로·같은 파일명으로 덮으면 코드는 그대로 돈다
- **화면은 새로 만들지 않았다** — `FacilityPopup` 을 그대로 쓴다 (`PopupType` 안 늘림).
  선택 화면 → 결과 화면 두 단계다
  - ⚠ 결과 화면도 **같은 그림**을 유지한다 — 배경이 바뀌면 다른 곳으로 넘어온 것처럼 보인다
  - ⚠ **닫고 다시 열지 않는다. 같은 창을 그 자리에서 갈아 끼운다**
    (`FacilityPopup._keepOpenOnPick`). `Choose` 는 `Close()` 를 부르고 **곧바로** 콜백을
    부르는데, 그 콜백이 같은 `PopupType` 을 다시 열면 닫기 코루틴이 아직 `IsOpen` 을
    내리기 전이라 PopupManager 가 **인스턴스를 한 벌 더 만든다**
  - ⚠ 결과 화면에서 `SetOnClose` 를 **다시 걸어야 한다** — `Choose` 가 고른 순간
    `_onClose` 를 비운다. 안 걸면 결과를 ✕ 로 닫았을 때 **런이 그 자리에서 멈춘다**
- **만난 이벤트는 그 런에 다시 안 나온다** — `SummonRunData.seenEvents` (런 스코프·이어하기 유지).
  ⚠ 여덟을 다 만나면 **기록을 비우고 다시 돈다** — 후보가 없다고 건너뛰면 그 런의
  이벤트 칸이 조용히 사라진다
- ⚠ `RunEventId` 는 **뒤에만** 추가한다 — 만난 기록을 번호로 저장한다
- ⚠ 값을 낸 뒤 물건을 준다. 체력은 `RunCoreData.Pay` 로 낸다 —
  **`Breach` 와 다른 물건이다** (그쪽은 통과당한 마릿수를 함께 세는 통계라 섞으면 거짓말이 된다)
- ⚠ 골드는 `RunGoldRule.Grant` 를 지난다 — 유물 '약탈의 손' 이 거기서 얹힌다
- 원작 이벤트(`EventData`·`EventRewardHandler`·`EventPopup`)는 2026-09-11 에 지웠다 —
  보상 13종이 전부 원작 축(병사 수·어빌리티·용병·장수 HP%)이었다
- ⚠ `RunNodeRule.Implemented` 는 이제 언제나 `true` 다. 표에만 적고 화면을 안 만든
  종류가 생기면 **거기에 한 줄**을 두는 것이 규칙이다 (표에서 지우면 가중치를 다시 잡아야 한다)

### 화면 밖 사망 판정 — 용사 스폰과 한 묶음이다
> ⚠ **셋이 서로를 묶는다. 하나를 바꾸면 나머지를 다시 계산할 것.**
> | 값 | 정본 |
> |---|---|
> | 카메라 `orthographicSize` | `Assets/Scenes/InGame.unity` (12) |
> | 용사 스폰 x | `InGameSceneSetup.HeroX` (24) |
> | 살상 반경 | `UnitMovementSystem.ScreenClampJob.OutOfBoundsKillDist` (6) |
>
> 화면 반폭 = `orthographicSize × 16/9` = **21.33**. 허용 = 21.33 + 반경.
> 용사는 일부러 화면 밖에서 걸어 들어오므로 **스폰 x 가 허용 안에 있어야 한다.**

- ⚠ **밖에 있으면 웨이브 시작 순간 부대가 통째로 즉사한다** (2026-09-07)
  실제로 `HeroX 26` · 반경 4 → 허용 25.33 이라 **장수 3기와 병사 31명이 전멸**했다.
  증상이 지독하다 — 스폰 로그는 멀쩡히 찍히고(`활성=True`, 체력·이속 정상)
  액티브 스킬까지 발동하는데 **화면에는 아무도 안 나온다.**
  지금은 24 · 반경 6 → 허용 27.33 으로 3.3 여유

### 소환수는 낸 즉시 달려 나간다
- ⚠ **"적이 화면에 들어올 때까지 기다린다" 는 원작 규칙을 걷어냈다** (사용자 지적, 2026-09-07)
  `MoveToDestinationJob` 이 `AnyEnemyOnScreen` 을 보고 아군을 세워 뒀다.
  원작 아군은 진형을 짜고 기다리는 것이 맞았지만, 이 게임의 아군은
  **플레이어가 마나를 내고 부른 소환수**다. 성문 앞에 멈춰 서 있으면
  ① 소환한 순간과 움직이는 순간이 갈려 먹통으로 읽히고
  ② 성벽 앞에 쌓여 라인이 의미를 잃으며
  ③ 미리 소환해 두는 대기 시간이 통째로 죽은 시간이 된다
- 전멸 뒤 정지는 그대로다 (`EnemyDefeated`) — 승리 후 오른쪽으로 몰려 나가지 않는다

### 30스테이지 = 무한 보스 (2026-09-10, 원작 규칙 복원)
> **정본은 `InGame/Summon/EndlessBossRule.cs` 하나다.**

- `GameplayConfig.MaxStage`(= `StageConfig.NormalStageCount`, 기본 30)부터
  **깰 수 없는 판**이다. 보스를 잡으면 스텟 `×10`(누적)짜리 다음 보스가 온다.
  크기는 언제나 프리팹의 ×2, 넉백 면역. 런은 **마왕성이 뚫려야** 끝난다
- ⚠ **원작 코드는 있었는데 부르는 곳이 없었다** — `NormalMode.IsEndless` ·
  `GetEndlessBossEntries` 는 남아 있었지만 웨이브를 걷어내며 `BattleContext.EndlessBossIndex`
  가 함께 사라져 죽은 코드였다. 그래서 30을 넘어도 평범한 판이 계속 이어졌다
- ⚠ **세 곳이 이 규칙을 함께 본다 — 하나만 고치면 어긋난다**
  | 어디 | 무엇 |
  |---|---|
  | `HeroDeployment.Build` | 맨 앞에서 가로챈다 — 0번 보스 하나로 갈음 |
  | `RunBootstrap.Update` | 전멸하면 `DeclareVictory` 대신 다음 보스를 부른다 |
  | `SummonController.CheckWaveCleared` | **거두지 않는다** |
- ⚠ 거두기를 막지 않으면 **필드가 텅 빈 채로 멈춘다** — 보스가 죽는 순간 생존자가
  전부 대기열로 들어가는데, 그 대기열을 열어 줄 '다음 스테이지' 가 영영 오지 않는다
- ⚠ `SpawnNextEndlessBoss` 는 **세는 것(`OnUnitSpawned`)이 먼저**다. 실제 등장은
  `RespawnDelay`(2초) 뒤지만 그 사이에도 `AliveEnemyCount` 가 1 이어야 한다
- ⚠ **보스는 혼자 온다** (`SpawnEntry.LoneHero`) — 이 게임의 보스는 용사(General)라
  그냥 두면 30스테이지 기준 병사 33명을 데려온다. 그러면 "무한 보스" 가 아니라
  "보스가 낀 평범한 판" 으로 읽힌다
- 배율·몸집은 `GeneralRuntimeBridge.Initialize(statMult, scaleMult, loneHero)` 가 처리한다
  - ⚠ 자리는 **유물 약화 뒤 · SpawnEntity 앞**이다. 약화는 비율이라 앞에 두면 배율까지
    깎이고, 몸집은 `SpawnEntity` 가 `localScale` 로 분리 반경을 잡으므로 그 앞이라야 한다
  - ⚠ 몸집을 바꾸면 `_spawnScale` 도 함께 옮긴다 — 안 옮기면 **첫 광폭화에서 원래
    크기로 쪼그라든다**(`GrowEnrage` 가 그 값을 기준으로 삼는다)
  - ⚠ **체력·공격력만** 곱한다. 사거리·공속·이속까지 곱하면 ×10 한 번에 화면을
    가로지르며 연타한다
- ⚠ 넉백 면역 태그는 `HeroSpawner` 가 붙이고 **`GeneralRuntimeBridge.Initialize` 가 뗀다**
  (승격 컴포넌트와 같은 자리·같은 이유 — 풀 재사용)
- ⚠ 무한 보스 번호는 저장하지 않는다 — 이어하기로 돌아오면 0번부터다

### 스테이지 진행 (자동 시작 · 이어하기)
- **자동 시작**: `InGame/Summon/StageLoopDirector.cs` `_readySeconds`
  — ⚠ 스테이지당 용사 **부대 수**는 건드리지 말 것 (`HeroDeployment.GetSquadCount`)
  — 남은 시간은 `EnemyInfoButtonUI` 가 화면 오른쪽 끝 한가운데에서 360° 고리로 그린다
- **초반 스테이지 구성**: `HeroDeployment.GeneralFromStage`(5) — 그 전까지는
  `SpawnEntry.SoldiersOnly` 가 켜져 장수 없이 병사만 나온다
  (`HeroSpawner.SpawnSoldierSquad`). 첫 보스와 같은 번호를 유지할 것
- **남은 시간 표시**: `InGame/UI/EnemyInfoButtonUI.cs` (말풍선 버튼 안)
- **용사 자리 순서**: `InGame/Battle/Spawner/HeroSpawner.cs` `LaneOrder` = 3·2·4·1·5
- **이어하기**: `Data/Sections/SummonRunData.cs` (SaveKey 21) +
  `LobbyManager.SelectInitialPanel` 맨 앞 분기 + `RunLaunch.RequestResume`
  — ⚠ 이어하기에서 `GrantForRun` 을 부르면 앱 재시작이 마나 회복 수단이 된다

### 궁수 퇴각 사격 (무빙샷)
- 정본은 `InGame/Unit/UnitMovementSystem.cs` `MoveToDestinationJob` 의
  `RetreatSpeedMult`(0.5) · `ShotRecoveryRatio`(0.35)
- ⚠ **공짜 무빙샷이 아니다** (사용자 지적, 2026-09-07) — 한때 제 이속 그대로 물러나며 쏴서
  근접이 영영 못 따라잡았다. 지금은 ① 쏜 직후 공격 간격의 35% 동안 발이 묶이고
  ② 그 뒤에도 이속 절반으로만 물러난다
- ⚠ 두 값은 한 묶음이다 — 둘 다 후하게 주면 옛날로 돌아간다
- 대상은 `RetreatFireTag`(궁수 기본 행동, `GeneralRuntimeBridge` 가 붙인다)

### 전투 밸런스 / 스탯
> ⚠ **세 손잡이는 한 묶음이다** (2026-09-04 확정) — 하나만 돌리면 반드시 어긋난다.
> | 손잡이 | 정본 |
> |---|---|
> | 적이 **몇** 나오는가 | `HeroDeployment` MinSquads/MaxSquads/SquadRampEndStage + 에셋 `SoldierCount` + `LevelFlatSoldierCountPerLevel` |
> | 적 개체가 **얼마나 센가** | `GameplayConfig.asset` 의 `*Range` Hp/Attack + `LevelFlat*PerLevel` |
> | 아군이 **얼마나 낼 수 있는가** | `SummonerData.BaseMana` / `ManaPerIntelligence` (+ **12개 SO 에셋에 구워져 있다**) |
>
> 이 게임의 물량은 **아군 것이다**(라인 복귀로 저절로 불어난다). 용사는 소수정예 —
> 수를 늘리는 대신 개체를 세운다. 적이 세다고 느껴지면 스탯보다 **수**를 먼저 본다.
> ⚠ 후반 경사는 `LevelFlat*PerLevel` 로 만든다. `*Range` 를 키우면 초반이 같이 무너진다.
> ⚠ **고정 성장은 선형이 아니다** (사용자 확정, 2026-09-10) —
> `GameplayConfig.LevelGrowthAccel`(**0.05**, 2026-09-16 에 0.02 → 0.03 → 0.05 — 초반 등급 상한의 대가로 후반을 올림)이 `체력·공격력` 가산을
> `n × (1 + 0.05n)` 으로 가속시킨다(`UnitJobRoller`). 적의 **수**는
> `MaxSquads`(5) = 라인 수라 10스테이지에서 상한에 닿고, 그 뒤 적이 세지는
> 길이 이것뿐인데 선형이면 곱으로 크는 아군(라인 복귀 물량 × 시너지 × 패시브)과
> 반드시 갈린다 — 실제로 40스테이지가 넘도록 런이 안 끝났다.
> ⚠ **병사 수에는 안 건다** — 수는 부대 수와 곱해지는 데다 화면에 실제로 서는
> 오브젝트라, 가속하면 밸런스보다 프레임이 먼저 죽는다. 가팔라지는 것은 세기뿐이다.
> ⚠ 초반이 어려우면 이 값이 아니라 `*Range` 나 부대 수 표를 본다 —
> `n` 이 작을 때는 거의 1 이다 (10스테이지 +18% · 30스테이지 +58%).
- **2026-09-06 재조정 (초반↑ 후반↓)** — 초반은 **수**로, 후반 완화는 **개체 성장**으로
  | 손잡이 | 이전 → 이후 |
  |---|---|
  | `HeroDeployment` Min/Max/RampEnd | 1 / 4 / 20 → **1 / 5 / 10** |
  - 부대 수 표(사용자 확정): **1~2→1 · 3~4→2 · 5→3 · 7~8→4 · 9~10→5**.
    셋(Min·Max·RampEnd)의 선형 보간이 이 표와 정확히 맞는다 — 하나만 바꾸면 어긋난다
  - ⚠ `MaxSquads` 가 **라인 수와 같아졌다**(4→5). 예전의 "한 자리는 늘 비워 둔다" 규칙은
    폐기됐다 — 10스테이지부터 다섯 라인이 전부 찬다
  | `LevelFlatHpPerLevel` / `AttackPerLevel` | 45 / 4 → **20 / 1.8** |
  | 병사 수 `SoldierCount` (기사 외 / 기사) | 1~2 / 2~4 → **2~4 / 4~7** |
  | `LevelFlatSoldierCountPerLevel` | (없음) → **1** (스테이지마다 부대당 병사 +1) |
  | `DefenseGrowthMax` | (없음) → **0.6** |
  - ⚠ **`DefenseGrowthMax` 는 전투의 소프트캡과 다른 물건이다** — 그쪽(`DefenseMax` 0.9 +
    `DefenseOverflowRate`)은 '맞을 때' 의 환산이고, 이건 **스탯이 굴러 나올 때**의 천장이다.
    없으면 방패병 방어율이 1.7 을 넘어 소프트캡을 타고도 98% 감소가 된다(사실상 불사)
  - ⚠ 병사 수 성장은 **부대 수와 곱해진다** — 이 게임에서 가장 값이 센 손잡이다.
    30스테이지 기준 한 판 적이 약 136기가 된다. 후반이 버겁다면 스탯보다 여기를 먼저 본다
- **기본 스탯**: `InGame/GameplayConfig.cs` (SO) — `Assets/Resources/GameplayConfig.asset`
  ⚠ `UnitJobRoller.FallbackJobRange` 에 같은 값이 또 있다 — 함께 고칠 것 (한때 2배 어긋나 있었다)
- ⚠ **직업 스탯 범위(`*Range`)는 적(용사)의 값이다.** 원작에서 "아군 장군" 이던 수치라
  진영이 뒤집힌 뒤 스케일이 통째로 어긋나 있었다 (2026-09-04 재조정: 체력 ÷3.2 · 공격력 ÷2.0)
  - 아군 몬스터는 종족 기본 스탯(HP 55~260)뿐인데 적은 HP 840~4000 이었다 — 첫 판부터 졌다
  - **후반 경사는 `LevelFlat*PerLevel` 로 조절한다** (10→30 / 1→3).
    고정 성장은 스테이지 1 에서 0 이라 초반을 안 건드리고 후반만 올린다.
    범위를 다시 키우면 초반이 같이 무너진다
  - ⚠ 같은 값이 `UnitJobRoller.FallbackJobRange` 에 또 있다 — 함께 고칠 것
    (한때 둘이 어긋나 설정을 못 읽으면 적이 두 배로 세졌다)
  - ⚠ 스테이지 5 에서 적 EHP 가 3배로 뛴다 — `HeroDeployment.GeneralFromStage`(장군 등장)와
    `GetSquadCount`(부대 2→3)가 같은 번호를 쓴다
- **스탯 타입**: `InGame/Stat/StatType.cs`
- **유닛 스탯 계산**: `InGame/Stat/UnitStat.cs`
- **직업·등급 배율**: `InGame/Unit/UnitJob.cs`

### 피해 숫자 (데미지 폰트)
- **표시**: `InGame/UI/DamageNumberLayer.cs` — 월드 공간 TMP 풀. 캔버스를 쓰지 않는다
  (숫자가 대량이라 캔버스 리빌드를 피한다). 합산·상한·풀 세 장치로 개수를 억제
- **발행**: `InGame/Battle/BattleStatCollectorSystem.cs` `ShowDamageNumber`
  — `DamageResultElement` 를 비우는 유일한 시스템이라 여기서만 읽을 수 있다
- **폰트**: `InGame/UI/DamageFontNames.cs`(이름·문자 집합 정본) +
  `InGame/UI/Editor/DamageFontCreator.cs` → `아이콘·텍스처 > 데미지 숫자 폰트`
  — ⚠ **정적 아틀라스**다. 표기에 새 글자를 쓰게 되면 `Charset` 에 먼저 넣고 다시 구울 것
- **마나·마릿수 아이콘**: `Editor/UIIconAssets.cs`(경로·임포트) +
  `Editor/EditorUIBuilder.cs` `IconValueBadge`(배지 빌더). 생성 도구는 없다 — PNG 를 그대로 쓴다

### 인게임 UI 수정
- **HUD**: `InGame/UI/InGameHUD.cs` — 상단바 참조와 클릭음 바인딩뿐이다. 굽는 곳은 `InGameUIPrefabCreator`
- **상단바(배속·오토·일시정지)**: `InGame/UI/TopBarUI.cs`
- **전투 흐름**: `InGame/Battle/BattleManager.cs` — 준비·웨이브·승패 이벤트
- **런 흐름 / 승패 처리**: `InGame/Summon/RunBootstrap.cs` (`HandleVictory`·`HandleDefeat`)
- **전장 정리**: `InGame/Battle/BattleArena.cs` — 판을 닫을 때 유닛·이펙트·발사체 회수
- **마왕성 체력 표시**: `InGame/UI/CoreHpBarUI.cs` (+ `Editor/InGameUIPrefabCreator.BuildCoreHpBar`)
  - ⚠ 자리는 **화면 왼쪽 가장자리 고정**이다 (사용자 지적, 2026-09-07 재조정)
    처음에는 성벽 바깥면(x≈285)을 따라다니게 했는데 **안 보였다** — 거기는 라인 대기열·
    소환 지점·라인 띠가 모이는 자리라 얇은 기둥이 묻힌다. 가장자리는 성벽 그림뿐이라 비어 있다
  - 기둥 폭 72 · 왼쪽에서 14. 대기열 왼쪽 끝(114)까지 28px 여유 — 넓히려면 다시 잴 것
  - 수치는 기둥 **한가운데**에 흰색 고정. 게이지 색을 따라가면 채워진 구간에서 글자가 사라진다
  - ⚠ 색은 **보라 → 주황 → 붉은**이다. 보스 HP 바가 붉은색이라 가득 찬 상태를
    붉게 두면 화면에 붉은 막대가 둘이 되어 어느 쪽이 내 것인지 안 갈린다
  - ⚠ **줄어들 때만** 번쩍인다 — 갈림길 야영지가 파는 회복(`RunCoreData.Heal`)까지
    같은 연출로 알리면 경고가 경고로 안 읽힌다
  - 뚫림 반응: 성 쪽 화면 가장자리 붉은 띠 + 기둥 덧빛 + `CameraShaker.Impulse`.
    **소리는 아직 없다** — `SfxKey` 에 맞는 키가 없다(추가하려면 wav 부터)
- ⚠ **스프라이트 없는 `Image.Type.Filled` 는 줄지 않는다** (사용자 지적, 2026-09-11)
  유니티 Image 는 sprite 가 null 이면 Filled 를 무시하고 사각형 전체를 그린다 — `fillAmount` 가 먹지 않고 에러도 없다.
  `EditorUIBuilder.Img` 는 스프라이트를 안 넣으므로 마왕성·보스 HP·진행 막대·보스 스킬 쿨다운 고리가 전부 가득 찬 채였다.
  → 런타임에 `FillSprite.Ensure(image)` (`UI/Common/FillSprite.cs`, 공유 흰 스프라이트). 새 Filled 막대를 만들면 그 컴포넌트에서 부를 것
- ⚠ **화면을 새 Creator 로 옮길 때는 컴포넌트의 `SerializeField` 를 한 줄씩 훑을 것**
  (2026-09-07) — HUD 를 `UISetupTool`에서 `InGameUIPrefabCreator` 로 옮기며 보스
  **광폭화 표시와 스킬 4칸을 통째로 빠뜨렸다.** `TopBarUI` 로직은 멀쩡했지만 참조가
  비어 `RefreshBossEnrage`·`RefreshBossSkills` 가 첫 줄에서 return 하고 있었다.
  **빠져도 예외가 나지 않는다** — 화면에서 조용히 사라질 뿐이다
- ⚠ **위쪽 HUD 자리는 한 묶음이다** (사용자 지적, 2026-09-07) —
  `InGameUIPrefabCreator` 의 `LeftStackX` · `PerkRowTop` · `PerkIconSize` ·
  `SynergyBarTop` · `TopBarHeight` 가 서로를 참조한다. 하나만 손대면 겹친다.
  ```
  격자: 왼쪽 여백 HudMargin(16) · 줄 사이 Gutter(10) — 뒤 줄은 앞 줄 끝에서 잰다
  y   0..112  오른쪽 버튼 띠 (스테이지·배속·일시정지)   ⚠ UIScale.BtnSm(100) 보다 커야 함
  y  10..112  보유 특성 자리   x 16..1102   (언제나 두 줄만큼 비운다 = 102)
              ├ 14칸 이하 → y 10..82  아이콘 72, 한 줄
              └ 15칸 이상 → y 10..112 아이콘 48, 두 줄 20열
  y 122..168  마왕성 체력 막대 x 16..376    (가로 360×46 — 짧게)
  y 178..231  보스 HP (중앙)                (가로 760 — 길게)
  y 178..854  시너지 세로 줄   x 16..192    (칸 176×68, 맨 위 중첩 칩 1 + 시너지 최대 8칸)
  전장 y 112..908 · 카드 줄 시작 x 318
  오른쪽 버튼 띠 (오른쪽 끝에서): 일시정지 44 · 배속 154 · 전황 276 · 스테이지 390
  ```
  - ⚠ **셋이 같은 왼쪽 선(16)에서 시작한다** (사용자 지적, 2026-09-07) —
    줄마다 x·y 를 손으로 적어 2~6px 씩 어긋나 있었다. `HudMargin`·`Gutter` 둘로 묶었다
  - ⚠ 전에는 특성 줄(상단바 안, y15~133)과 시너지 줄(y104~172)이 **겹쳐 있었다.**
    특성 줄이 비어 있던 동안(원작 `TraitBarUI`) 드러나지 않았을 뿐이다
  - ⚠ 특성 아이콘 56 → 72 (사용자 지적) — 56 은 전장 위에서 안 읽혔다.
    시너지(48)보다 커야 한다. 시너지는 옆 숫자로 짚이지만 특성은 그림 하나가 전부다
  - ⚠ **특성 줄은 칸 수에 따라 두 모습을 갖는다** (사용자 확정, 2026-09-10)
    | 칸 수 | 아이콘 | 배치 |
    |---|---|---|
    | 14 이하 | 72 | 한 줄 14열 — 지금까지와 픽셀 단위로 같다 |
    | 15 이상 | 48 | **두 줄 20열 = 40칸** |
    - ⚠ 그전에는 14칸이 전부라 **15번째부터 조용히 잘렸다.** `RunPerkBarUI` 는
      칸이 모자라면 `break` 한다 — 에러도 경고도 없다. 담아야 하는 최대는
      개성 1 + 특성 30 + 제단 표식 8 = **39**
    - ⚠ **자리(세로)는 언제나 두 줄만큼 비워 둔다** (`PerkRowsH` 102) —
      프리팹은 미리 굽는데 칸 수는 런타임에 정해진다. 한 줄 높이로 구우면
      두 줄이 되는 순간 마왕성 체력 막대를 덮는다. 그래서 `CoreBarTop` 이
      92 → **122** 로 내려갔고 아래 줄(보스 HP·시너지)이 함께 따라간다
    - ⚠ 격자 정렬은 **UpperLeft** 다. MiddleLeft 면 한 줄일 때 두 줄 자리의
      가운데로 내려가 15px 어긋난다
    - ⚠ 수치의 정본은 **Creator** 다 (`PerkIconSize`/`PerkColumns`/`PerkDenseIcon`/
      `PerkDenseColumns`). 런타임(`RunPerkBarUI.ApplyLayout`)은 고르기만 한다 —
      줄 폭·칸 수 검산이 Creator 에서만 가능하기 때문이다
    - ⚠ `VerifyPerkBar` 가 굽는 순간 넷을 본다 — 칸 부족 · 두 줄 초과 ·
      빽빽한 줄 폭 초과 · 두 모습이 뒤집힘. 조용한 실패를 시끄럽게 바꾼 것이다
    - ⚠ 48 보다 더 줄이지 말 것 — 시너지 칩과 같은 값이고 그게 읽히는 하한이다.
      열을 더 늘리려면 줄 폭을 넓혀야 하는데 오른쪽 버튼 띠가 그 자리를 쓴다
  - ⚠ **시너지 줄은 위에서부터 쌓는다** (사용자 지적, 2026-09-08)
    세로 중심이 전장 한가운데였을 때, 칩이 둘뿐이면 줄이 화면 정중앙에 떠서
    **성 안뜰의 소환사를 정면으로 덮었다.** 칩 수는 판마다 달라지므로 가운데
    정렬은 무엇을 가릴지 미리 알 수 없다. 정본은 `SynergyBarTop`(체력 막대 끝)
    + `VerticalLayoutGroup.childAlignment = UpperLeft` 다
  - ⚠ **전황 버튼은 배속 옆이다** (사용자 지적, 2026-09-08) — 스테이지 표시와
    자리를 맞바꿨다. 누르는 것 셋(전황·배속·일시정지)을 붙이고 읽기만 하는
    스테이지 표시를 바깥으로 뺀다. 정본은 `InfoRightInset`/`StageRightInset`
  - ⚠ **마왕성 체력과 시너지는 자리를 맞바꿨다** (사용자 확정, 2026-09-07)
    체력은 **짧은 가로 막대**여야 체력으로 읽힌다 — 세로 기둥은 두 번 시도했고
    둘 다 "체력같지 않다" 였다. 보스 바(가운데 760)와 자리·길이·색이 다 달라야 갈린다.
    시너지는 개수가 들쭉날쭉해 세로로 쌓는 쪽이 자리를 덜 먹는다
  - ⚠ **자리가 없으면 성벽을 민다** (사용자 지적, 2026-09-07) — UI 를 줄이지 말 것.
    시너지 세로 줄이 라인 대기열과 부딪히자 `InGameSceneSetup.WallX` 를 −15 → **−13**
    으로 옮겼다. 성벽 화면 x 285 → 375, 대기열 198~369 가 되어 30px 여유가 생긴다.
    ⚠ **바꾸면 씬을 다시 구워야 한다** (`씬 셋업 > 인게임 전장`) — `WallBoundaryX` 가 그때 박힌다
  - ⚠ 시너지 칩에서 **보유 숫자를 뺐다** (사용자 지적) — 문턱 줄(2/4/6)이 이미 그 말을 한다
    (도달한 칸이 굵고 단계 색). 정확한 수는 툴팁 제목 "숲 3/5" 가 말한다. 칩 192 → 152
  - ⚠ **은(Silver) 색을 하늘빛으로 바꿨다** — 회색이라 '꺼짐'(푸른 회색)과 색조가 같아,
    4종을 모아 은에 닿아도 "색이 안 바뀐다" 로 보였다. 네 색은 한 묶음이다
  - ⚠ **툴팁에 고정(pin)은 없다** (사용자 지적) — 올리면 뜨고 벗어나면 닫힌다.
    마나 칸(`ManaRegenHoverUI`)도 같은 규칙이다
  - ⚠ **PC 는 올려서만, 모바일은 눌러서만 연다** (사용자 지시, 2026-09-17) — 정본 `UI/Common/TooltipInput.HoverMode`
    (`!Application.isMobilePlatform`). 버튼 클릭으로만 열던 칸(`TraitIconUI`·`SkillIconUI`·보스 스킬·용사 등급·도감 특성)은
    `TooltipInput.HookHover` 로 올림을 붙이고 클릭은 `!HoverMode` 로 막는다. 새 툴팁도 이 둘을 지날 것
    (상단 특성 줄이 클릭으로만 열려 올려도 안 떴다). PC 에서는 올린 칸을 눌러도 닫히지 않는다(`InfoTooltipUI.PointerOverOwner`)
  - **다음 판 회복 예고**: 마나 칸 아래 `다음 판 +18`, 올리면 내역이 툴팁으로 펴진다
    - 정본은 `InGame/Summon/ManaRegenRule.cs` — **실제 지급(`AdvanceStage`)과 같은 함수**다.
      ⚠ 화면이 따로 계산하면 반드시 갈린다. 새 보정은 여기에만 넣을 것
      ⚠ **`RawFor` 와 `Describe` 는 한 묶음이다** (2026-09-07) — 한때 유물 마나 회복
        보너스가 `RawFor` 에만 있어서, 툴팁 내역의 합계가 실제 회복량보다 작았다.
        보정을 넣으면 **두 함수 모두** 같은 자리·같은 순서로 넣을 것
    - ⚠ 잔량의 10%가 회복량에 들어가므로 소환할 때마다 이 숫자가 줄어든다 — 의도된 것이다
    - ⚠ **회복량 전체를 적는다 — 그릇(Max)에서 자르지 않는다** (사용자 지시, 2026-09-17) — 35/40 에서 +5 로만 떠
      회복량을 읽을 수 없었다. 자르는 것은 지급(`SummonManaData.RegenForStage`)뿐이다. 0 이어도 보인다
  - ⚠ **마나 칸에는 숫자뿐이다** (사용자 지적) — 라벨("소환 마나")과 진행 막대를 걷어냈다.
    셋이 같은 값을 세 번 말했다. `SummonDeckUI._manaFill` 필드도 **지웠다** —
    ⚠ 막대를 다시 넣지 말 것: 마나는 실시간이 아니라 스테이지 경계에서만 움직인다
  - ⚠ **카드 줄 시작점은 마나 칸에서 계산한다** (`ManaPanelLeft + ManaPanelW + ManaGap`).
    286 을 박아 뒀다가 마나 패널과 20px 겹쳤다
- ⚠ **HUD 루트에 무언가를 달면 만드는 함수가 `DestroyChild` 도 해야 한다**
  (사용자 지적, 2026-09-07) — `PerkBar` 를 `TopBar` 밖으로 빼면서 지우는 줄을
  빠뜨렸다. `TopBar` 만 지우고 새로 만드니 **구울 때마다 `PerkBar` 가 하나씩 쌓여
  넷이 됐다.** 화면에서는 "겹쳐 보인다" 로만 나타나 원인을 짚기 어렵다
  - `DestroyChild` 는 이제 같은 이름을 **전부** 지운다 (하나만 지우면 쌓인 것이 안 풀린다)
  - `PatchHud` 끝의 `VerifyNoDuplicateChildren` 이 중복을 이름으로 잡아 준다
- ⚠ **HUD 안의 겹침 순서는 계층 순서다** (사용자 지적, 2026-09-07) — 갈림길(`CrossroadUI`)·
  융합(`CardEvolveUI`)은 팝업이 아니라 **HUD 자식**이라 `sortingOrder` 가 없다.
  각자 만들어질 때 `SetAsLastSibling` 을 부르지만 카드 바(`SummonDeckBar`)가 그 뒤에
  만들어져 **덮개 위로 그려졌다** (갈림길 위에 시그니처 스킬 칸이 얹혀 있었다).
  `InGameUIPrefabCreator.PatchHud` 끝의 `BringOverlaysToFront` 가 정본 —
  전장을 덮는 판을 새로 만들면 그 목록에 넣을 것

### 이펙트 추가
1. `아이콘·텍스처 > 이펙트 텍스처·머티리얼`
2. `프리팹 생성 > 이펙트 > Effect 프리팹` (희귀·보스는 `희귀·보스 스킬 이펙트`)
3. `도구 > 스킬 SO 에 이펙트 키 연결`
   (구현: `InGame/Skill/Editor/EffectTextureGenerator.cs` · `EffectPrefabGenerator.cs` · `EffectKeyLinker.cs`)
- 키 형식: `"FX_스킬이름"` (예: `FX_Meteor_Explosion`)
- 프리팹 위치: `Assets/_project/2.Prefabs/Effect/`
- 스폰: `SkillEffectHelper.Spawn(key, pos, despawnDelay)` — 반납은 `EffectAutoReturn` 타이머
  (⚠ `Time.timeScale` 을 탄다. 멈춘 사이 판이 끝나면 다음 판까지 남는다)

### 싱글톤 생성
- **MonoBehaviour 싱글톤** (씬에 배치 필요): `Singleton<T>` 상속
- **순수 C# 싱글톤** (씬 독립, 자동 생성): `SingletonPure<T>` 상속
- 위치: `Assets/_project/1.Script/Singleton.cs`

---

## 코딩 규칙

> **⚠ 절대 규칙 — 방어적 null 체크 금지**
> `?.`, `if (x == null) return`, `?? default` 로 로직을 조용히 스킵하지 말 것.
> null이면 예외가 터져야 버그를 즉시 발견할 수 있다.
> 예외: Inspector 에서 선택적으로 연결하는 UI 컴포넌트 (`_button?.onClick` 등).

- **ECS Component 구조체**: `I ComponentData` 또는 `IBufferElementData`
- **Baker 클래스**: Authoring 파일 하단에 인라인으로 작성
- **스킬 추가 순서**: ① `ActiveSkillId` enum 추가 → ② `Active*.cs` 생성 → ③ SO 생성 → ④ DB 등록
- **이펙트 키**: `"FX_스킬이름"` 형식 (예: `FX_Meteor_Explosion`)
- **네임스페이스**: 유닛 관련은 `BattleGame.Units`, 나머지는 전역 또는 미사용
- **프리팹 풀 반납**: `EffectDespawnDelay` 초 후 자동 반납 (`SkillEffectHelper`)

---

## 원작 잔재 대청소 (2026-09-11)

> 게임이 1차 완성된 뒤 **원작에만 있고 이 게임이 안 쓰는 코드를 통째로 지웠다.**
> 옛 이름으로 검색해 안 나오면 여기부터 볼 것.

| 지운 축 | 대표 파일 |
|---|---|
| 원작 로비 | `HeroPanelUI`·`BattlePanel`·`StageSelectUI`·`TopBar*`·`LobbyNavUI`·`LobbyMenuButton`·`RunStarter`·`GameSession` |
| 원작 인게임 | `InGameManager`·`WaveSetupData`·`StageGenerator`·`GeneralPanelUI`·`RewardCardUI`·`BattleResultPopup` |
| 어빌리티 | `InGame/Ability/` 전체 · `RunAbilityData` |
| 특성(직업 시너지) | `InGame/Trait/` · `TraitTriggerHandlers` · `RunTraitData` · `TraitDatabase` |
| 용사 장비 | `InGame/Equipment/` · `EquipInventoryData` · 분해·비교 팝업 |
| 원작 이벤트·런 상점·용병 | `InGame/Event/` · `RunShop*` · `Merc*` |
| 원작 유닛 효과 | 기사 돌진 · 궁수 다중 사격 · 마법사 스킬 발동 · 피해 오라 · 거울 갑옷 · 불비 |

- 남은 규칙은 옮겨 뒀다 — `StatRules.IsAbsoluteStat`(StatType.cs) · `RelicTarget`(RelicTreeTypes.cs) ·
  `HeroNamePool`(UnitData.cs, 용사 이름표) · `NormalMode(int stageNumber)`
- 세이브 키·`PopupType` 번호는 **예약으로 남겼다** (`ISaveSection.cs`·`GameEnums.cs` 주석) — 재사용 금지
- **씬·프리팹은 유니티 안에서 한 번 정리해야 한다** — `Tools > Project K > 도구 > 원작 잔재 정리 (씬·프리팹)`
  (`Editor/LegacyCleanupTool.cs`). 로비 원작 패널 삭제 · 원작 프리팹/이벤트 데이터/옛 아이콘 삭제 ·
  Missing Script 제거 · HeroDetail·Reincarnation 재굽기 · PopupManager 목록 · SpriteManager 재굽기까지 한 번에 한다
- `HeroDetailPopup` 은 **적(용사) 정보 전용**이다 (`SetupHero` 하나). 장비·등급·성장 줄은 없다

## 구현된 시스템

- **전투**: ECS 공격·이동·타겟팅·피격 / 발사체 포물선 + 넉백 / 보스·엘리트 용사 / 무한 보스
- **스킬**: 액티브 + 패시브 + 종족 패시브 · 각성 + 이펙트 파이프라인
- **런**: 소환 덱 · 마나 · 과부하 · 라인 복귀 · 갈림길(야영지·강화소·제단·상점·이벤트) · 런 특성 · 시너지
- **메타**: 환생(포인트→유물 트리) · 몬스터 도감·품질 개선 · 몬스터 장비 · 난이도
- **UI**: 로비(MainPanel) · 팝업(전부 Creator 생성) · 인게임 HUD · 성장 연출
- **기반**: 세이브 섹션 · 오브젝트 풀 · 사운드 · 튜토리얼 · 전투 통계 · 씬 상주 모델 · 치트

---

## 기억해야 할 사항

- Unity 6 + ECS 1.4.5 — DOTS API 최신 버전 사용 (`SystemAPI`, `IAspect` 등)
- `com.unity.vectorgraphics` 패키지 없음 — SVG 직접 임포트 불가, PNG 필요
- 오브젝트 풀은 항상 사용 — `new` 대신 `PoolController.Get()`
- 스킬 Execute()는 메인 스레드에서 실행됨 (Burst 불가)
- ⚠ **원작 규칙**: 로비와 인게임 씬 동시 상주 (`SceneDirector`/`BattleArena` 경유).
  **이 프로젝트에서는 폐기** — 로비가 없고 `RunSetup`은 런당 1회 쓰고 언로드해도 된다 (v2 1.2절).
  코드는 아직 원작 상태이므로 손대기 전에 이 차이를 확인할 것.
- 캔버스 기준: **가로 1920×1080** (원작과 동일). `UIScale.PopupMaxH = 1000` 그대로 유효.
- 프리팹은 전부 Creator 산출물이다 — **프리팹을 손으로 고치지 말고 Creator 를 고친 뒤 다시 굽는다**
