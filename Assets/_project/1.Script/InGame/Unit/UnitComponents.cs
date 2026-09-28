using Unity.Entities;
using Unity.Mathematics;

// ============================================================
//  UnitComponents.cs
//  유닛의 모든 데이터(Component)를 정의하는 파일
//  ECS 원칙: 데이터와 로직을 완전히 분리
//  - Component = 순수 데이터만 보유, 로직 없음
//
//  ■ 스텟 구조 (버프/성장 대응)
//    StatComponent.Base  — 기본 스텟 (성장·장비 확정값, 버프 절대 미적용)
//    StatComponent.Final — 최종 스텟 (버프·디버프 적용 후 캐시)
//    → 접근법:  stat.Final[StatType.Attack]
//    → UnitStatusEffectSystem 이 매 프레임 Final 재계산
//    → 전투 시스템은 모두 StatFinal 에서만 읽는다
//
//  ■ 통합된 컴포넌트
//    MovementComponent  ← VelocityComponent 흡수 (Velocity 필드)
//    HealthComponent    ← CurrentHp 런타임 상태만 (MaxHp → StatFinal)
//    AttackComponent    ← 쿨다운·타겟만 (공격력 등 → StatFinal)
//
//  ■ 제거
//    VelocityComponent  — MovementComponent.Velocity 로 대체
//    StatusEffectType   — StatType + EffectMode 로 대체
// ============================================================

namespace BattleGame.Units
{
    // ──────────────────────────────────────────
    // 유닛 기본 정보
    // ──────────────────────────────────────────

    /// <summary>유닛 고유 식별 및 소속 정보</summary>
    public struct UnitIdentityComponent : IComponentData
    {
        public int      UnitId;
        public TeamType Team;    // 아군 / 적군
        public UnitType Type;    // 병사 / 장군 / 일반적 / 엘리트 / 보스
    }

    /// <summary>
    /// 유닛 종류.
    ///
    /// ⚠ 진영별로 쓰는 값이 갈린다 (Faction.cs 참고)
    ///   적(용사)  : General · Soldier · Elite · Boss
    ///   아군(몬스터): Monster 하나뿐
    ///
    ///   Elite / Boss 는 **용사 전용**이다. 소환 몬스터에는 등급 계층이 없다 —
    ///   몬스터의 강함은 품질(UnitGrade)과 소환력이 정하지 계층이 정하지 않는다.
    /// </summary>
    public enum UnitType : byte
    {
        Soldier  = 0,   // 용사 휘하 병사
        General  = 1,   // 용사 — 병사를 지휘
        Monster  = 2,   // 소환 몬스터 — 플레이어의 아군 유닛 (구 Enemy)
        Elite    = 3,   // 엘리트 용사 — 강화 계층
        Boss     = 4,   // 보스 용사 — 특수 계층 (엘리트 스테이지)
        Summoner = 5,   // 소환사 — 플레이어 본체. 이 유닛의 HP 가 곧 마왕성 HP 다
    }

    // ──────────────────────────────────────────
    // 장군 전용 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// 장군 유닛에게만 붙는 컴포넌트.
    /// 스킬은 GeneralPassiveSkillComponent / GeneralActiveSkillComponent 로 분리.
    /// </summary>
    public struct GeneralComponent : IComponentData
    {
        public float CommandRadius; // 지휘 반경 — 이 범위 내 소속 병사에게 패시브 버프 적용
    }

    /// <summary>
    /// 장군 생성 시 Baker 가 붙이는 병사 스폰 요청.
    /// SoldierSpawnSystem 이 처리 후 이 컴포넌트를 제거한다.
    /// </summary>
    public struct SpawnSoldiersRequest : IComponentData
    {
        public Entity SoldierPrefab;
        public int    Count;
        public float  StatScaleRatio;  // 병사 스텟 = 장군 스텟 × 이 값
    }

    // ──────────────────────────────────────────
    // 병사 전용 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// 병사 유닛에게만 붙는 컴포넌트.
    /// GeneralEntity 는 SoldierSpawnSystem 이 스폰 시 주입한다.
    /// </summary>
    public struct SoldierComponent : IComponentData
    {
        public Entity GeneralEntity;   // 소속 장군 (스폰 시 채워짐)
        public float  StatScaleRatio;  // 장군 스텟 대비 병사 스텟 비율
        public bool   IsInitialized;
    }

    // ──────────────────────────────────────────
    // 엘리트 전용 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>엘리트 유닛 마커. 독립 전투 유닛으로 장군-병사 계층 없음.</summary>
    public struct EliteComponent : IComponentData
    {
        public bool  HasSkill;           // GeneralActiveSkillComponent 공유 여부
        /// <summary>넉백 내성. 0 = 없음, 1 = 완전 면역. 넉백 벡터를 (1-값)배로 감소.</summary>
        public float KnockbackResistance;
    }

    // ──────────────────────────────────────────
    // 보스 전용 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>보스 유닛 마커 + 페이즈 데이터 + AoE 공격 + 돌진 패턴.</summary>
    public struct BossComponent : IComponentData
    {
        // ── 페이즈 ────────────────────────────────────────────
        public int   PhaseCount;
        public int   CurrentPhase;    // 현재 페이즈 (1부터 시작)
        public float Phase2HpRatio;   // 2페이즈 전환 체력 비율 (예: 0.5 = 50%)
        public float Phase3HpRatio;   // 3페이즈 전환 체력 비율 (PhaseCount < 3 이면 무시)

        /// <summary>행동불능(스턴) 내성. 0 = 없음, 1 = 완전 면역.</summary>
        public float CCResistance;
        /// <summary>넉백 내성. 0 = 없음, 1 = 완전 면역.</summary>
        public float KnockbackResistance;

        // ── AoE 기본 공격 ─────────────────────────────────────
        /// <summary>AoE 공격 반경. 타겟 주변 이 범위 내 아군도 피해를 받는다.</summary>
        public float AoeRadius;
        /// <summary>AoE 범위 피해 비율. 1.0 = 100%, 0.6 = 60%.</summary>
        public float AoeSplashRatio;
        /// <summary>공격 시 적용되는 넉백 강도 (이동 속도 단위).</summary>
        public float AttackKnockbackForce;
        /// <summary>넉백 지속 시간 (초).</summary>
        public float AttackKnockbackDuration;

        // ⚠ 돌진 필드는 전부 없앴다
        //   돌진이 ActiveSkillId.BossCharge 스킬로 옮겨가면서
        //   쿨다운·타겟·이동을 ActiveSkillSlot + BossChargeRunner 가 갖는다.
        //   패턴을 추가할 때마다 이 컴포넌트에 필드를 늘리던 구조를 끝냈다.
        //   광폭화도 마찬가지다 — ActiveSkillId.BossEnrage 슬롯이 쿨다운을 갖는다.
    }

    // ──────────────────────────────────────────
    // 소환사 전용 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// 성벽 너머에 서 있는 유닛 — 공격자가 성벽 두께만큼 더 멀리서도 때릴 수 있다.
    ///
    /// ■ 왜 필요한가 — 이게 없으면 패배가 영영 성립하지 않는다
    ///   소환사는 성벽(WallBoundaryX) 왼쪽에 서고, 용사는 성벽에서 멈춘다.
    ///   즉 둘 사이는 **성벽 두께만큼 절대 좁혀지지 않는다**.
    ///   그런데 사거리 판정은 중심-중심 거리라 반경을 더해 주지 않는다
    ///   (UnitAttackSystem 의 distSq > attackRange²).
    ///
    ///   근접 용사의 사거리는 굴림값이다 — 방패병 0.7~1.0 · 기사 0.8~1.2
    ///   (GameplayConfig.Reset). 배율도 안 붙는다(UnitJobRoller: "배율 미적용").
    ///   성벽 두께가 1.0 이면 **근접 용사는 소환사를 영원히 때리지 못한다.**
    ///   성벽에 붙어 선 채로 서 있기만 하고, 마왕성 HP 가 줄지 않아 런이 끝나지 않는다.
    ///
    /// ■ 왜 사거리 하한을 올리지 않고 이 방식인가
    ///   용사 사거리를 건드리면 전투 밸런스 전체가 따라 움직인다. 이 보정은
    ///   **타겟이 이 컴포넌트를 가졌을 때만** 걸리므로 소환사를 때릴 때 외에는
    ///   아무것도 바뀌지 않는다.
    ///
    /// ■ 값의 출처
    ///   SummonerRuntimeBridge 가 스폰 시 SummonFieldLayout.WallGap 을 그대로 넣는다.
    ///   성벽이나 소환사 자리를 옮기면 보정도 저절로 따라온다 — 숫자를 박지 말 것.
    /// </summary>
    /// <summary>
    /// 이 유닛은 **절대 자리를 뜨지 않는다** — 마왕(소환사) 전용.
    ///
    /// ■ 왜 태그인가
    ///   마왕은 성벽 뒤 한 점에 못 박힌 채, 적이 사거리에 들어오면 쏘기만 한다.
    ///   이걸 "이동속도 0" 으로 표현했더니 위치를 건드리는 잡마다 따로 막아야
    ///   했고, 하나라도 빠뜨리면 **아주 느리게 앞으로 걸어가는** 증상이 났다.
    ///   실제로 분리(Separation)가 빠져 있어서 겹친 용사에게 조금씩 떠밀렸다.
    ///
    ///   태그 하나를 달고 위치를 쓰는 잡 전부에 [WithNone] 을 걸면, 그 잡들의
    ///   **쿼리에서 아예 빠진다.** 막는 게 아니라 대상이 아니게 되는 것이라
    ///   나중에 이동 잡이 하나 늘어도 이 유닛이 휩쓸릴 여지가 없다.
    ///
    /// ■ 무엇을 막지 않는가
    ///   공격·타겟팅·피격·상태효과는 그대로다. 서 있되 싸운다.
    ///   넉백은 이 태그가 아니라 KnockbackImmuneTag 가 맡는다 (기존 장치).
    /// </summary>
    public struct StationaryTag : IComponentData { }

    public struct WallCoverComponent : IComponentData
    {
        /// <summary>공격자에게 더해 줄 사거리(월드 단위) = 성벽과 소환사 사이 거리.</summary>
        public float ExtraReach;
    }

    // ──────────────────────────────────────────
    // 스텟 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// 유닛 스텟 컴포넌트.
    ///
    /// Base  — 성장·장비로 확정된 기본값. 인게임 버프는 절대 쓰지 않는다.
    /// Final — Base + 활성 버프/디버프. 전투 시스템은 여기서만 읽는다.
    ///
    /// 읽기:  float atk = stat.Final[StatType.Attack];
    /// 쓰기:  stat.Base[StatType.MaxHp] = 500f;
    /// 복사:  stat.ResetFinalToBase();
    ///
    /// StatType 추가 시 이 파일은 건드리지 않아도 된다 (StatBlock 이 자동 확장).
    /// </summary>
    public struct StatComponent : IComponentData
    {
        public StatBlock Base;   // 기본 스텟 (성장·장비)
        public StatBlock Final;  // 최종 스텟 (버프 적용 후, 매 프레임 재계산)

        /// <summary>Final 을 Base 값으로 초기화. 버프 재계산 직전에 호출.</summary>
        public void ResetFinalToBase() => Final = Base;  // StatBlock 은 값 타입이므로 struct copy
    }

    /// <summary>
    /// 성장만으로 얻은 스탯 — 등급·레벨 롤(GeneralStatRoller.Roll)까지다.
    /// 장비·패시브·어빌리티·유물·특성·도감이 붙기 **전** 값.
    ///
    /// ■ 왜 따로 들고 있나 — "외부로 오른 증가분" 은 StatComponent 만으로 못 센다
    ///   StatComponent.Base 는 이미 모든 출처가 합쳐진 값이고, Final - Base 는
    ///   전투 중 버프뿐이다. 그래서 '속전속결' 처럼 성장분과 외부분을 갈라야 하는
    ///   패시브가 전투 시작 시점에 기준을 잡을 방법이 없었다
    ///   (Final - Base 로 읽어 늘 0 이 나왔다).
    ///
    ///   증가분 = StatComponent.Base[s] - Roll[s]
    ///
    /// ⚠ 장군에게만 붙인다
    ///   병사는 장수 스탯을 환산해 받으므로 자기 롤이라는 것이 없다.
    /// </summary>
    public struct BaseRollStatComponent : IComponentData
    {
        public StatBlock Roll;
    }

    // ──────────────────────────────────────────
    // 이동 관련 컴포넌트 (VelocityComponent 흡수)
    // ──────────────────────────────────────────

    /// <summary>
    /// 이동 상태 컴포넌트. VelocityComponent 를 흡수해 Velocity 필드를 가진다.
    /// MoveSpeed 는 StatComponent.Final[StatType.MoveSpeed] 에서 읽는다.
    /// </summary>
    public struct MovementComponent : IComponentData
    {
        public float3 Velocity;          // 현재 프레임 이동 벡터 (VelocityComponent 대체)
        public float3 Destination;       // 목적지 (전선 위치 등)
        public float  StoppingDistance;  // 이 거리 안으로 들어오면 이동 중지
        public float  MoveDelay;         // 스폰 후 이동 시작까지 대기 시간 (초). 0이면 즉시 이동.
        public bool   IsMoving;
    }

    /// <summary>포지션 레이어 (전열/중열/후열)</summary>
    public struct FormationSlotComponent : IComponentData
    {
        public int    Row;          // 0 = 전열, 1 = 중열, 2 = 후열
        public int    Column;       // 같은 열 안에서의 가로 인덱스
        public float3 SlotPosition; // 배정된 진형 슬롯 월드 좌표
    }

    // ──────────────────────────────────────────
    // 전투 런타임 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// HP 런타임 상태만 보유.
    /// MaxHp / Defense → StatComponent.Final
    /// 사망 여부 → DeadTag
    /// </summary>
    public struct HealthComponent : IComponentData
    {
        public float CurrentHp;

        /// <summary>
        /// 비행 중인 발사체가 이미 확정지은 피해량 (방어율 적용 후).
        /// ProjectileIncomingDamageSystem 이 매 프레임 살아있는 발사체로부터 다시 계산한다
        /// — 누적 필드가 아니므로 환불·정합성 관리가 필요 없다.
        /// </summary>
        public float IncomingDamage;

        /// <summary>날아오는 발사체까지 감안한 실효 체력. 0 이하 = 이미 죽은 목숨.</summary>
        public float EffectiveHp => CurrentHp - IncomingDamage;

        /// <summary>
        /// 아직 살아 있지만(이동·피격 판정 유지) 도착 예정 피해로 이미 죽은 것이 확정된 상태.
        /// 새 공격의 타겟에서 제외해 오버킬 낭비를 막는다.
        /// </summary>
        public bool IsDoomed => CurrentHp > 0f && CurrentHp - IncomingDamage <= 0f;
    }

    /// <summary>
    /// 공격 런타임 상태만 보유.
    /// AttackDamage / AttackRange / AttackSpeed → StatComponent.Final
    /// </summary>
    public struct AttackComponent : IComponentData
    {
        public float  AttackCooldown;      // 다음 공격까지 남은 시간
        public Entity TargetEntity;
        public float3 TargetPosition;      // 타겟 마지막 위치 캐시 (Chasing 이동용, 3프레임마다 갱신)
        public bool   HasTarget;
        public uint   RandomSeed;          // 크리티컬 판정용 per-entity 랜덤 시드
        public bool   AttackedThisFrame;   // 이번 프레임에 공격 발생 — CooldownTickJob이 매 프레임 초기화
        public float  LastDamageDealt;     // 마지막 공격으로 가한 피해 — OnAttack 트리거용
    }

    /// <summary>
    /// 피격 이벤트 타입.
    /// Normal    — 일반 공격 (근거리·원거리 기본 공격)
    /// Skill     — 스킬 직접 타격 (HitDirection 을 넉백으로 그대로 사용)
    /// Reflected — 거울 방어 반사 피해 (다시 반사 불가)
    /// Dot       — 지속 피해 틱 (UnitStatusEffectSystem 이 프레임마다 합쳐 한 건으로 넣는다)
    /// </summary>
    public enum HitType : byte
    {
        Normal    = 0,
        Skill     = 1,
        Reflected = 2,

        /// <summary>
        /// ⚠ 도트 틱은 **아무 반응도 일으키지 않는다** (2026-09-11)
        ///   한때 도트 틱이 Normal 로 들어갔다. 그러면 매 프레임 틱마다
        ///   ① 방어율 하한(1)이 붙어 초당 피해가 60 이상으로 부풀고
        ///   ② 파쇄·소환사 평타(최대 체력 비례)가 틱마다 붙고
        ///   ③ 역병의 중독(InflictOnHit)이 틱마다 **새 도트를 또 걸어** 스택이 지수로 불었다.
        ///   증상: 독 슬라임을 때린 용사가 즉사 · 무한 보스 즉사 · 프레임 100ms+.
        ///   ⚠ 방어율도 지나지 않는다 — 도트 값은 "초당 피해" 그대로다 (MonsterDotRule).
        /// </summary>
        Dot       = 3,
    }

    /// <summary>
    /// 피격 이벤트 버퍼 (한 프레임에 여러 번 맞을 수 있음)
    /// DynamicBuffer 로 선언해 GC 없이 가변 크기 처리
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HitEventBufferElement : IBufferElementData
    {
        public float   Damage;
        public float3  HitDirection;   // Normal: 방향벡터, Skill: 방향×힘(직접 사용)
        public Entity  AttackerEntity;
        public HitType Type;

        /// <summary>
        /// 이 타격이 무시하는 방어율 (0~1) — 공격자의 StatType.DefensePenetration.
        ///
        /// ⚠ 공격자 엔티티를 되짚어 읽지 않고 값으로 실어 보낸다
        ///   보스 AoE 는 AttackerEntity 를 비워 보내고, 발사체는 시전자가 먼저
        ///   죽어 풀에 반납될 수 있다. 그때마다 관통이 조용히 0 이 되면
        ///   "가끔 피해가 다르게 들어가는" 재현 안 되는 버그가 된다.
        /// </summary>
        public float   DefensePierce;
    }

    // ──────────────────────────────────────────
    // 버프 / 디버프 버퍼
    // ──────────────────────────────────────────

    /// <summary>
    /// 버프·디버프 적용 방식.
    /// Add      — StatFinal += Delta  (절대값 증감)
    /// Multiply — StatFinal *= Delta  (배율, 1.3 = 30% 증가)
    /// Dot      — 초당 Delta 만큼 CurrentHp 직접 감소 (도트 데미지, Stat 필드 무시)
    /// </summary>
    public enum EffectMode : byte
    {
        Add      = 0,
        Multiply = 1,
        Dot      = 2,
    }

    /// <summary>버프/디버프 출처 종류. GeneralPanelUI 아이콘 표시에 사용.</summary>
    public enum BuffSourceType : byte
    {
        None        = 0,
        ActiveSkill = 1,  // ActiveSkillId 값
        Passive     = 2,  // PassiveSkillType 값
        Ability     = 3,  // AbilityId 값
        Equipment   = 4,  // 장비 슬롯 인덱스 (0 또는 1)
    }

    /// <summary>
    /// 활성 버프/디버프 하나.
    /// StatType 기반이므로 새 스텟 추가 시 이 구조체는 수정하지 않아도 된다.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct StatusEffectBufferElement : IBufferElementData
    {
        public StatType        Stat;        // 영향받는 스텟 종류 (Dot 이면 무시)
        public float           Delta;       // 효과 수치 (양수 = 강화, 음수 = 약화)
        public EffectMode      Mode;        // Add / Multiply / Dot
        public float           Duration;    // 전체 지속 시간 (-1 이면 영구)
        public float           Remaining;   // 남은 시간
        public BuffSourceType  SourceType;  // 출처 종류
        public int             SourceId;    // 출처 내 ID (종류별 의미는 BuffSourceType 주석 참고)

        /// <summary>
        /// 이 효과를 건 유닛. Dot 모드에서 처치 귀속에 쓴다 (그 외에는 비어 있어도 된다).
        ///
        /// ⚠ 비워 두면 도트로 죽인 적이 아무의 전과도 아니게 된다
        ///   UnitDeathDespawnSystem 은 마지막 일격의 AttackerEntity 로 부대를 찾는다.
        ///   도트 피해가 출처 없이 들어가면 그 적을 잡아도 "처치 시" 장비·패시브가
        ///   아무에게도 발동하지 않는다.
        /// </summary>
        public Entity          SourceEntity;

        /// <summary>
        /// 이 도트가 무엇으로 보여야 하는가 — 몸에 입히는 색만 정한다.
        /// Dot 이 아닌 효과에서는 의미가 없다.
        ///
        /// ⚠ 기본값(0)이 독이다 — 대부분의 도트가 독·역병이고, 겹쳤을 때
        ///   이기는 쪽도 독이다(DotKind 주석). 안 적어도 맞는 값이어야 한다.
        /// </summary>
        public DotKind         Dot;
    }

    /// <summary>
    /// 도트의 겉모습.
    ///
    /// ■ ⚠ 겹치면 <b>독이 이긴다</b> (사용자 확정, 2026-09-10)
    ///   화상과 독을 동시에 맞으면 초록이다. 우선순위를 두지 않으면 나중에
    ///   붙은 쪽이 이겨, 같은 조합인데 화면 색이 판마다 달라진다.
    ///   ⚠ 값이 곧 우선순위다 — <b>작은 쪽이 이긴다.</b> 새 종류는 뒤에 붙인다.
    ///
    /// ⚠ 피해 계산에는 아무 영향이 없다. 색만 정한다 —
    ///   속성 상성 같은 것을 여기에 얹지 말 것.
    /// </summary>
    public enum DotKind : byte
    {
        /// <summary>독·역병 — 초록.</summary>
        Poison = 0,

        /// <summary>화상 — 붉은빛. 불이다.</summary>
        Burn   = 1,
    }

    // ──────────────────────────────────────────
    // 상태 머신 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>유닛 현재 행동 상태</summary>
    public struct UnitStateComponent : IComponentData
    {
        public UnitState Current;
        public UnitState Previous;
        public float     StateTimer; // 현재 상태 진입 후 경과 시간
    }

    public enum UnitState : byte
    {
        Idle      = 0,  // 대기
        Moving    = 1,  // 이동 중
        Chasing   = 2,  // 적 추격
        Attacking = 3,  // 공격 중
        Hit       = 4,  // 피격 경직
        Dead      = 5,
        // 6 = Charging (원작 기사 달인 돌진) — 폐기
    }

    // ──────────────────────────────────────────
    // 피격 반응 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>피격 시 넉백, 경직 처리용</summary>
    public struct HitReactionComponent : IComponentData
    {
        public float3 KnockbackVelocity; // 넉백 이동 벡터
        public float  StunDuration;      // 경직 지속 시간 (초)
        public float  StunTimer;         // 경직 잔여 시간
        public bool   IsStunned;
        /// <summary>
        /// 데미지를 받은 프레임에 true 로 설정된다.
        /// 스턴 여부와 무관하게 피격 플래시를 발동시키기 위해 사용.
        /// UnitAnimationSync 가 읽은 뒤 false 로 리셋한다.
        /// </summary>
        public bool   NeedsFlash;

        /// <summary>
        /// 지금 지속 피해(Dot)를 받고 있는가 — 켜져 있으면 몸이 초록빛으로 물든다.
        ///
        /// ■ 왜 여기 있나 (2026-09-10)
        ///   UnitAnimationSync 는 이 컴포넌트를 <b>이미</b> 매 프레임 읽는다
        ///   (NeedsFlash). 도트 여부를 알려고 StatusEffectBuffer 를 한 번 더
        ///   여는 것보다, 이미 지나가는 값에 얹는 쪽이 싸다.
        ///
        /// ⚠ NeedsFlash 와 달리 읽은 쪽이 지우지 않는다
        ///   이건 사건이 아니라 <b>상태</b>다. UnitStatusEffectSystem 이 매
        ///   프레임 참·거짓을 다시 쓴다 — 도트가 끝나면 저절로 꺼진다.
        /// </summary>
        public bool   IsPoisoned;

        /// <summary>
        /// 물들 색을 정하는 도트 종류. <see cref="IsPoisoned"/> 가 false 면 의미가 없다.
        ///
        /// ⚠ 여럿 걸려 있으면 <b>가장 작은 값</b>이 이긴다 (DotKind 주석).
        /// </summary>
        public DotKind DotLook;
    }

    // ──────────────────────────────────────────
    // 공간 분할용 Grid 컴포넌트
    // ──────────────────────────────────────────

    // ──────────────────────────────────────────
    // 화면 경계 상태
    // ──────────────────────────────────────────

    /// <summary>
    /// 유닛이 한 번이라도 화면 안에 들어왔으면 HasEnteredScreen = true.
    /// ScreenClampSystem 이 이 값을 보고 화면 밖으로 나가지 않게 위치를 클램프한다.
    /// </summary>
    public struct ScreenStateComponent : IComponentData
    {
        public bool HasEnteredScreen;

        /// <summary>
        /// 아직 화면에 못 들어온 채로 허용 범위 밖에 머문 시간(초).
        /// ScreenClampSystem 이 쌓고, 범위 안으로 돌아오면 0 으로 되돌린다.
        ///
        /// ⚠ '누적' 이 아니라 '연속' 이다 — 한 번이라도 범위 안에 들어오면 처음부터 다시 잰다.
        ///   누적으로 세면 스폰 → 진입을 반복하는 개체가 언젠가 반드시 죽는다.
        ///
        /// ⚠ 0 이 올바른 초기값이라 풀 재사용이 저절로 맞는다
        ///   UnitRuntimeBridge 가 이 구조체를 통째로 새로 써서 스폰할 때마다 0 이 된다.
        ///   남은 시간을 세는 방식(= 스폰 때 유예값을 넣어야 함)이었다면 그 줄을
        ///   빠뜨린 경로에서 유예가 조용히 사라졌을 것이다.
        /// </summary>
        public float OutOfBoundsSeconds;
    }

    /// <summary>
    /// 유닛이 현재 속한 Grid 셀 좌표.
    /// SpatialGridSystem 이 매 프레임 갱신.
    /// </summary>
    public struct GridCellComponent : IComponentData
    {
        public int2 Cell;      // 현재 셀
        public int2 PrevCell;  // 직전 프레임 셀 (변경 감지용)
    }

    /// <summary>
    /// 유닛의 물리적 크기 반경.
    /// GameObject.transform.localScale 에서 계산 (Max(x,y) * 0.5f).
    /// SeparationJob 에서 두 유닛의 반경 합을 밀어낼 거리로 사용한다.
    /// </summary>
    public struct UnitSizeComponent : IComponentData
    {
        /// <summary>
        /// 반경 — localScale 에서 나온다.
        /// ⚠ 분리(Separation)에서는 **가로** 반지름으로만 쓰인다 —
        ///   세로는 SeparationJob.FootSquash 로 눌러 발밑 타원을 만든다.
        /// </summary>
        public float Radius;
        /// <summary>
        /// 분리 질량. 클수록 다른 유닛에게 밀리지 않고 더 강하게 밀어낸다.
        /// General = 5, Soldier/Enemy = 1
        /// </summary>
        public float Mass;
    }

    // ──────────────────────────────────────────
    // 태그
    // ──────────────────────────────────────────

    /// <summary>Hybrid Renderer 와 연결용 태그. 이 컴포넌트가 붙은 Entity 만 스프라이트 업데이트 수행.</summary>
    public struct NeedsRenderSyncTag : IComponentData { }

    /// <summary>죽은 유닛에 붙이는 태그 — 각 시스템에서 이 태그로 필터링해 연산 제외.</summary>
    public struct DeadTag : IComponentData { }

    /// <summary>
    /// 무적 유닛 — 피격 연출(플래시·넉백·경직)은 그대로 받되 체력이 깎이지 않는다.
    ///
    /// ■ 왜 태그인가
    ///   로비 데모는 예전에 0.2초마다 아군 체력을 가득 채우는 방식으로 버텼다.
    ///   장군은 체력 통이 커서 버텼지만 **병사는 그 0.2초 안에 한 방에 죽었다**.
    ///   죽음은 UnitHitSystem 이 피해를 넣는 그 자리에서 확정되므로,
    ///   밖에서 아무리 자주 채워도 타이밍 싸움을 이길 수 없다.
    ///
    /// ⚠ 예약 피해(IncomingDamage)도 함께 막아야 한다
    ///   실효 체력이 0 이하면 IsDoomed 가 서고, UnitAttackSystem 은 그 유닛을
    ///   '사망 확정' 으로 보고 공격을 멈춘다 — 안 죽는데 싸우지도 않게 된다.
    ///   ProjectileIncomingDamageSystem 이 이 태그를 건너뛰는 이유다.
    /// </summary>
    public struct InvulnerableTag : IComponentData { }

    /// <summary>
    /// 도발 태그 — 이 태그를 가진 유닛은 적의 우선 타겟이 된다.
    /// 철벽 방어 스킬 시전 시 EffectDuration 동안 부여된다.
    /// </summary>
    public struct TauntTag : IComponentData
    {
        public float Remaining;  // 남은 지속시간 (초)
    }

    // ──────────────────────────────────────────
    // 직업 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// 유닛 직업 컴포넌트.
    /// GeneralRuntimeBridge.AddComponents() 에서 설정.
    /// 적은 직업 시스템 도입 시 MonsterRuntimeBridge.AddComponents() 에서 설정 예정.
    /// </summary>
    public struct UnitJobComponent : IComponentData
    {
        public UnitJob Job;
    }

    // ──────────────────────────────────────────
    // 원거리 공격 태그
    // ──────────────────────────────────────────

    /// <summary>
    /// 원거리 공격(발사체 사용) 유닛 마커.
    /// UnitJob.Archer / UnitJob.Mage 인 경우에만 추가.
    /// RangedAttackJob 필터링 및 ProjectileLaunchRequest 버퍼 추가 여부에 사용.
    /// </summary>
    public struct RangedTag : IComponentData { }

    /// <summary>
    /// 발사체 속도를 직업 기본값 대신 이 값으로 쓴다. 붙어 있을 때만 적용된다.
    ///
    /// ⚠ 사거리가 길어지면 속도도 같이 올려야 한다
    ///   RangedAttackJob 의 기본 속도(화살 15 · 마법구 10)는 사거리 5~10 짜리
    ///   용사 기준이다. 소환사는 성벽 뒤에서 20 이상을 쏘는데 같은 속도를 쓰면
    ///   한 발이 날아가는 데 2초가 넘어 "쏘긴 쏘는데 아무 일도 안 일어나는" 그림이 된다.
    /// </summary>
    public struct ProjectileSpeedOverride : IComponentData
    {
        public float Speed;
    }

    // ──────────────────────────────────────────
    // 발사체 발사 요청 버퍼
    // ──────────────────────────────────────────

    /// <summary>
    /// 원거리 유닛(RangedTag 보유) entity 에만 추가되는 버퍼.
    /// RangedAttackJob 이 공격마다 추가 → ProjectileSpawnSystem 이 같은 프레임에 처리 후 Clear.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct ProjectileLaunchRequest : IBufferElementData
    {
        public Entity   TargetEntity;
        public Entity   AttackerEntity;  // 통계 귀속용
        public float3   AttackerPos;
        public float3   TargetPos;
        public float    Damage;
        public float    Speed;
        public TeamType Team;
        /// <summary>공격자의 방어율 관통 — ProjectileComponent 로 그대로 넘어간다.</summary>
        public float    DefensePierce;
    }

    // ──────────────────────────────────────────
    // 전투 통계 버퍼 / 태그
    // ──────────────────────────────────────────

    /// <summary>
    /// 피격 처리 결과 버퍼. ProcessHitEventsJob 이 TARGET 엔티티에 기록.
    /// BattleStatCollectorSystem 이 매 프레임 읽고 BattleStatsTracker 에 귀속 후 Clear.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct DamageResultElement : IBufferElementData
    {
        public Entity  AttackerEntity;   // 공격자 (딜 귀속용)
        public float   ActualDamage;    // 방어 적용 후 실제 피해
        public float   AbsorbedDamage;  // 방어로 감소된 피해
        public bool    IsKill;          // 이 히트로 대상이 사망했는가
        public HitType Type;            // 피격 종류 (통계 분류·반사 여부 판단용)
    }

    /// <summary>
    /// 힐 이벤트 버퍼. 모든 힐 소스가 TARGET 엔티티에 Append.
    /// UnitHealSystem 이 매 프레임 읽어 HP 회복 + BattleStatsTracker 기록 후 Clear.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HealEventBufferElement : IBufferElementData
    {
        public float  Amount;        // 회복량 (최대 체력 클램프 전)
        public Entity SourceEntity;  // 회복 주체 (미래 확장용)
    }

    /// <summary>
    /// 소환 스킬로 생성된 유닛 마커 (SummonSkeleton / SummonElite 등).
    /// 딜 귀속 시 SoldierDmg 가 아닌 SkillDmg 로 분류하기 위해 사용.
    /// </summary>
    public struct SummonedTag : IComponentData { }

    // ──────────────────────────────────────────
    // 종족 패시브용 컴포넌트
    // ──────────────────────────────────────────

    /// <summary>
    /// 자기 최대 체력의 일정 비율을 초당 회복한다. (트롤 '재생' 등 종족 패시브)
    ///
    /// ⚠ 비율이지 절대값이 아니다
    ///   몬스터 HP 는 소환력·품질·카드 레벨로 크게 흔들린다. 절대값으로 두면
    ///   초반엔 불사신이고 후반엔 있으나 마나가 된다.
    /// </summary>
    public struct RegenComponent : IComponentData
    {
        /// <summary>초당 회복량 (최대 체력 대비 비율. 0.03 = 3%/초)</summary>
        public float RatioPerSecond;

        /// <summary>다음 회복 틱까지 남은 시간. 매 프레임 회복하지 않는다.</summary>
        public float Timer;

        /// <summary>
        /// 체력이 <see cref="LowHpThreshold"/> 아래일 때 회복량에 곱하는 값.
        /// 0 이나 1 이면 아무 일도 하지 않는다 (재생 시너지 은·금이 채운다).
        ///
        /// ⚠ 회복 총량이 아니라 **틱당 회복량**에 곱한다
        ///   총량에 곱하면 죽기 직전에만 몰아서 차올라 회복이 아니라 부활이 된다.
        /// </summary>
        public float LowHpMult;

        /// <summary>배율이 걸리는 체력 비율. 0.30 이면 30% 아래에서 걸린다.</summary>
        public float LowHpThreshold;
    }

    /// <summary>
    /// 피격 시 <b>때린 상대</b>에게 되돌려주는 반응. (독 슬라임·서리 늑대·강철 슬라임)
    ///
    /// ■ 왜 하나의 컴포넌트로 묶었나
    ///   중독·둔화·가시는 전부 "맞는 순간 공격자에게 무언가를 한다" 이다.
    ///   각각 컴포넌트를 만들면 UnitHitSystem 의 피격 루프에 조회가 셋 붙는다.
    ///   하나로 묶으면 조회 한 번으로 끝난다.
    ///
    /// ■ 처리 위치는 UnitHitSystem 의 피격 루프다
    ///   이미 "누가 나를 때렸나" 를
    ///   들고 있는 유일한 지점이라, 여기 말고는 공격자를 알 방법이 없다.
    ///
    /// ⚠ 반사 피해(Type=Reflected)에는 반응하지 않는다
    ///   가시 vs 가시가 만나면 무한 반사가 된다.
    /// </summary>
    public struct RetaliateComponent : IComponentData
    {
        /// <summary>공격자에게 걸 상태효과의 대상 스탯. Dot 이면 무시된다.</summary>
        public StatType EffectStat;

        /// <summary>상태효과 수치. 음수면 약화다.</summary>
        public float EffectDelta;

        public EffectMode EffectMode;

        /// <summary>상태효과 지속 시간. 0 이면 상태효과를 걸지 않는다.</summary>
        public float EffectDuration;

        /// <summary>받은 피해의 몇 배를 그대로 되돌려주는가. 0 이면 반사하지 않는다.</summary>
        public float ThornRatio;

        /// <summary>Dot 일 때 몸에 입힐 색. 기본(0)은 독이다 (DotKind 주석).</summary>
        public DotKind EffectDot;
    }

    /// <summary>
    /// <b>때린 상대</b>에게 거는 상태효과. (화염 멧돼지의 화상 등)
    ///
    /// ■ RetaliateComponent 와 방향이 반대다
    ///   Retaliate = "내가 맞으면 때린 놈에게"
    ///   Inflict   = "내가 때리면 맞은 놈에게"
    ///
    /// ■ 처리 위치도 UnitHitSystem 의 피격 루프다
    ///   그 루프는 **맞는 쪽**을 돌지만 hit.AttackerEntity 를 들고 있다.
    ///   공격자를 조회해 그 컴포넌트를 읽으면 "때린 놈이 나에게 무엇을 걸었나" 가
    ///   된다 — 공격 시스템에 훅을 새로 뚫는 것보다 훨씬 싸다.
    ///
    /// ⚠ 반사 피해(Type=Reflected)로는 걸리지 않는다
    ///   가시에 찔린 것까지 "때렸다" 로 세면 앞뒤가 안 맞는다.
    /// </summary>
    public struct InflictOnHitComponent : IComponentData
    {
        public StatType   EffectStat;
        public float      EffectDelta;
        public EffectMode EffectMode;

        /// <summary>지속 시간. 0 이면 아무것도 걸지 않는다.</summary>
        public float EffectDuration;

        /// <summary>Dot 일 때 몸에 입힐 색. 기본(0)은 독이다 (DotKind 주석).</summary>
        public DotKind EffectDot;

        // ── 두 번째 효과 ─────────────────────────────────────
        //
        //  ⚠ 왜 두 칸인가 — 역병 시너지가 **한 번에 둘**을 건다
        //    중독(Dot)과 함께 "중독된 적은 피해를 더 받는다" 를 걸어야 하는데,
        //    칸이 하나뿐이라 둘 중 하나를 버려야 했다.
        //    컴포넌트를 목록으로 바꾸는 것보다 칸 하나를 더 두는 편이 싸다 —
        //    지금 셋 이상을 거는 효과는 없다.

        public StatType   ExtraStat;
        public float      ExtraDelta;
        public EffectMode ExtraMode;

        /// <summary>두 번째 효과의 지속 시간. 0 이면 두 번째는 없다.</summary>
        public float ExtraDuration;

        /// <summary>두 번째 효과가 Dot 일 때의 색. 기본(0)은 독이다.</summary>
        public DotKind ExtraDot;
    }

    /// <summary>
    /// 연타 — MeleeAttackJob / RangedAttackJob 이 1회 공격마다 HitEvent 를 2번 넣는다 (종족 패시브).
    /// </summary>
    public struct DoubleStrikeTag : IComponentData { }

    /// <summary>
    /// 넉백 완전 무시 — 무한 보스 · 야수 시너지 금이 붙인다.
    /// ProcessHitEventsJob 이 이 태그를 확인해 KnockbackVelocity / StunDuration 적용을 건너뛴다.
    /// </summary>
    public struct KnockbackImmuneTag : IComponentData { }

    // ──────────────────────────────────────────
    // 원거리 행동 · 착탄 이벤트
    // ──────────────────────────────────────────

    /// <summary>
    /// 퇴각 사격 — 적이 공격 사거리 절반 이내로 붙으면 뒤로 물러나며 사격을 유지한다.
    /// MoveToDestinationJob 이 이 태그를 감지해 후퇴 이동을 적용한다.
    ///
    /// ⚠ 특성이 아니라 **궁수의 기본 행동**이다 (GeneralRuntimeBridge 가 붙인다)
    ///   예전엔 TraitType.ArcherRetreatFire 특성이었다. 궁수의 정체성 자체가
    ///   "거리를 유지하며 평타로 딜한다" 인데, 그 정체성을 고를 수 있는 옵션으로
    ///   두니 안 고르면 궁수가 근접 유닛처럼 굴었다. 특성 슬롯 하나를 쓸 만한
    ///   선택지도 아니어서 기본 행동으로 내렸다.
    ///
    ///   ⚠ 궁수 전용이 아니다 — 법사도 붙는다
    ///     법사는 사거리 4~7 에 공격속도가 궁수의 1/3 이다. 제자리에 서 있으면
    ///     쿨다운 도는 3초 동안 근접이 그대로 붙어 사거리가 무의미해졌다.
    ///     "거리를 벌며 큰 걸 꽂는다" 는 원거리 공통 규칙으로 넓혔다.
    /// </summary>
    public struct RetreatFireTag : IComponentData { }

    /// <summary>
    /// 장군의 기본 공격이 실제로 타겟에 닿았을 때 기록되는 이벤트.
    /// 근거리: MeleeAttackJob(ECB) → 다음 프레임 CombatTriggerSystem 에서 처리.
    /// 원거리: ProjectileHitJob(ECB) → 다음 프레임 CombatTriggerSystem 에서 처리.
    /// OnAttackLanded 트리거 핸들러가 이 버퍼를 읽어 동작한다.
    /// </summary>
    public struct AttackHitEvent : IBufferElementData
    {
        public Entity TargetEntity; // 피격된 주 타겟
        public float3 TargetPos;    // 착탄 위치 (이펙트 출발점 등에 사용)
        public float  Damage;       // 가해진 피해량
    }
}
