using System;
using Unity.Entities;

// ============================================================
//  MonsterTag.cs
//  몬스터 시너지 표식. 종족 하나가 두세 개를 갖는다.
//
//  ■ MonsterTrait 와 다른 축이다 — 겸용하지 말 것
//    MonsterTrait  : "이 몬스터를 어떻게 쓰나" (물량·방패·원거리…) — 도감 표시용
//    MonsterTag    : "덱에 몇 장 모았나" 로 단계가 열리는 **시너지** 축
//    한 enum 으로 합치면 표시용 분류를 손볼 때마다 밸런스가 흔들린다.
//
//  ■ 소속 수가 곧 문턱이다 (MonsterSynergyRule)
//      금 = 그 시너지에 속한 종족을 **전부** 모았다
//    그래서 소속 7종이면 3/5/7, 6종이면 2/4/6 이 된다.
//    ⚠ 종족을 추가·삭제하면 문턱이 따라 움직인다.
//      MonsterSynergyRule.Validate() 가 에디터에서 그 어긋남을 잡아 준다.
//
//  ■ 진화는 표식을 **바꾼다**
//    강철 슬라임은 숲을 잃고 강철을 얻는다. 강해지는 선택이 곧 시너지를
//    깨는 선택이 되어야 3택에 계산이 생긴다.
//    ⚠ UpgradeOf 로 상속되지 않는다 — SpeciesPassive 와 다른 점이다.
//      종족마다 Tags 를 직접 적는다.
// ============================================================

[Flags]
public enum MonsterTag
{
    None = 0,

    /// <summary>언데드 — 죽어도 다시 일어난다. (소속 7종 · 3/5/7)</summary>
    Undead = 1 << 0,

    /// <summary>숲 — 최대 체력. (소속 7종 · 3/5/7)</summary>
    Forest = 1 << 1,

    /// <summary>야수 — 공격속도·이동속도. (소속 6종 · 2/4/6)</summary>
    Beast = 1 << 2,

    /// <summary>재생 — 초당 회복. 판이 길수록 값이 커진다. (소속 6종 · 2/4/6)</summary>
    Regrowth = 1 << 3,

    /// <summary>투지 — 처치할수록 강해진다. 후반 가중. (소속 6종 · 2/4/6)</summary>
    Ferocity = 1 << 4,

    /// <summary>강철 — 방어율·반사. (소속 6종 · 2/4/6)</summary>
    Steel = 1 << 5,

    /// <summary>역병 — 중독 지속 피해. (소속 6종 · 2/4/6)</summary>
    Plague = 1 << 6,

    /// <summary>술법 — 액티브 스킬을 가진 종족. (소속 6종 · 2/4/6)</summary>
    Sorcery = 1 << 7,
}

/// <summary>시너지 단계. 동 → 은 → 금.</summary>
public enum SynergyTier
{
    None   = 0,
    Bronze = 1,
    Silver = 2,
    Gold   = 3,
}

namespace BattleGame.Units
{
    /// <summary>
    /// 이 몬스터가 가진 시너지 표식. 스폰 때 붙는다.
    ///
    /// ■ 왜 필요한가
    ///   "숲 몬스터가 죽으면 **숲 아군**을 회복" 같은 효과는 필드에서 상대가
    ///   무슨 표식인지 알아야 한다. 종족 SO 는 GameObject 쪽에만 있고
    ///   ECS 질의는 엔티티만 본다 — 그래서 표식을 엔티티에도 심는다.
    ///
    ///   ⚠ 이게 없을 때는 범위 회복이 **모든 몬스터**를 회복했다.
    ///     숲 시너지가 아니라 그냥 광역 힐이 되어 다른 시너지의 값을 먹었다.
    /// </summary>
    public struct MonsterTagComponent : IComponentData
    {
        public MonsterTag Tags;
    }

    /// <summary>
    /// 강철 금 — 한 번에 받을 수 있는 피해의 상한 (최대 체력 대비 비율).
    ///
    /// ⚠ 시너지 단계를 **엔티티에 구워 둔다**
    ///   피해 계산은 Burst 잡(UnitHitSystem) 안에서 돈다. 거기서는 관리형
    ///   static(MonsterSynergyRule)을 읽을 수 없다. 스폰할 때 값을 컴포넌트로
    ///   박아 두면 잡은 컴포넌트만 보면 된다.
    /// </summary>
    public struct SynergyDamageCapComponent : IComponentData
    {
        /// <summary>0.25 면 한 방에 최대 체력의 25% 를 넘게 잃지 않는다.</summary>
        public float Fraction;
    }

    /// <summary>
    /// 특성 '파쇄' — 이 유닛의 착탄마다 대상 최대 체력에 비례한 추가 피해.
    ///
    /// ⚠ 상한을 **스폰 때 구워 둔다**
    ///   원래 규칙은 "공격력의 3배" 인데, 피해 계산은 Burst 잡 안에서 돌고
    ///   거기서 공격자의 StatComponent 를 또 조회하면 잡이 무거워진다.
    ///   공격력은 스폰 시점에 이미 정해져 있으므로 그때 곱해 넣는다.
    ///   (강철 금이 시너지 단계를 구워 두는 것과 같은 이유)
    ///
    /// ⚠ 방어율을 지나지 않는다
    ///   최대 체력 비례 피해는 "방패병 용사의 해답" 이 존재 이유다.
    ///   방어율을 태우면 그 목적이 사라진다.
    /// </summary>
    public struct RendComponent : IComponentData
    {
        /// <summary>착탄마다 더할 대상 최대 체력의 비율 (0.02 = 2%).</summary>
        public float MaxHpRatio;

        /// <summary>한 방에 더할 수 있는 추가 피해의 상한 (= 공격력 × 3).</summary>
        public float Cap;
    }

    /// <summary>
    /// 소환사 <b>패기</b> — 이 유닛의 평타 넉백 배율 (SummonerVigorRule).
    ///
    /// ⚠ 스폰 때 구워 둔다
    ///   피해 계산은 Burst 잡이라 그 안에서 소환사를 조회할 수 없다.
    ///   패기는 런 내내 바뀌지 않으므로 스폰 시점에 정해도 어긋날 일이 없다.
    ///   (RendComponent 가 상한을 구워 두는 것과 같은 이유)
    ///
    /// ⚠ 평타에만 걸린다 — 스킬 넉백은 자기 값으로 밸런스가 잡혀 있다.
    /// </summary>
    public struct KnockbackPowerComponent : IComponentData
    {
        /// <summary>평타 넉백에 곱할 배율. 1 이면 힘이 기준값인 것과 같다.</summary>
        public float Mult;
    }

    /// <summary>
    /// 소환사 평타 — <b>대상 최대 체력 비례</b> 피해 (사용자 확정, 2026-09-07).
    ///
    /// ■ 왜 비율인가
    ///   소환사의 공격력은 패기 × 2 라 8~16 뿐이다. 20스테이지 용사가 체력
    ///   수천이 되면 그 평타는 아무 일도 하지 않는다 — 성벽 뒤에서 깨작대는
    ///   그림만 남는다. 비율이면 스테이지가 아무리 올라가도 같은 무게로 문다.
    ///
    /// ■ 격이 높을수록 덜 먹힌다
    ///   잡병 25% · 엘리트 5% · 보스 1%. 잡병 넷은 소환사가 혼자 정리하지만
    ///   보스는 100대를 때려야 한다 — "소환사가 다 한다" 가 되지 않는다.
    ///
    /// ⚠ 방어율을 지나지 않는다 (파쇄와 같은 규칙)
    ///   방어율을 태우면 방패 용사 앞에서 다시 0 이 되어 만든 이유가 사라진다.
    /// ⚠ 원래 공격력을 **대신한다** — 더하지 않는다
    ///   더하면 패기를 올릴수록 비율 피해 위에 얹혀 두 축이 섞인다.
    /// </summary>
    public struct SummonerStrikeComponent : IComponentData
    {
        /// <summary>잡병에게 먹이는 최대 체력 비율.</summary>
        public float NormalRatio;

        /// <summary>엘리트에게 먹이는 비율.</summary>
        public float EliteRatio;

        /// <summary>보스에게 먹이는 비율.</summary>
        public float BossRatio;
    }

    /// <summary>
    /// 재생 금 — 치명상을 입어도 한 번은 체력 1 로 버틴다.
    ///
    /// ⚠ 개체당 한 번뿐이다 (판당이 아니라 개체당)
    ///   라인 복귀로 다시 나오면 새 개체이므로 다시 한 번 쓴다. 죽지 않는
    ///   유닛이 되지는 않는다 — 버틴 직후 또 맞으면 그대로 죽는다.
    /// </summary>
    public struct SynergyLastStandComponent : IComponentData
    {
        /// <summary>0 = 아직 안 씀, 1 = 이미 버텼다.</summary>
        public byte Used;
    }

    // ══════════════════════════════════════════════════════════
    //  장비 특이 패시브 (2026-09-10) — SpeciesPassiveRuntime 이 스폰 때 붙인다
    //
    //  ⚠ 전부 **스폰 때 값을 구워 둔다** — 피해 계산이 Burst 잡이라 그 안에서
    //    규칙(SpeciesPassiveRule)을 읽을 수 없다 (강철 금·파쇄와 같은 이유)
    //  ⚠ 풀 재사용 — MonsterRuntimeBridge.ClearSpeciesPassiveResidue 가 전부 뗀다.
    //    여기 새 컴포넌트를 만들면 거기에도 한 줄 넣을 것.
    // ══════════════════════════════════════════════════════════

    /// <summary>허세 — 체력이 문턱 이상이면 공격력을 곱한다 (GearPassiveConditionSystem).</summary>
    public struct BravadoComponent : IComponentData
    {
        public float Threshold;
        public float AttackMult;
    }

    /// <summary>반동 — 평타로 적을 실제로 밀쳐 낼 때마다 이만큼 회복 (UnitHitSystem).</summary>
    public struct RecoilComponent : IComponentData
    {
        public float HealAmount;
    }

    /// <summary>급소 찌르기 — 치명타는 방어율을 무시한다 (UnitAttackSystem).</summary>
    public struct VitalStrikeTag : IComponentData { }

    /// <summary>처형 — 대상 체력이 문턱 이하면 치명타 확정 (UnitAttackSystem).</summary>
    public struct ExecuteComponent : IComponentData
    {
        public float Threshold;
    }

    /// <summary>성벽 — 받는 피해 배율 (UnitHitSystem, 체력에 들어가기 직전).</summary>
    public struct DamageTakenMultComponent : IComponentData
    {
        public float Mult;
    }

    /// <summary>광합성 — 스킬을 쓸 때마다 최대 체력의 이 비율을 회복 (ActiveSkillExecuteSystem).</summary>
    public struct PhotosynthesisComponent : IComponentData
    {
        public float Ratio;
    }

    /// <summary>복수 — 같은 종족(SpeciesKey) 아군이 죽을 때마다 쌓인다 (SpeciesPassiveRuntime.NotifyAllyDied).</summary>
    public struct VengeanceComponent : IComponentData
    {
        public int SpeciesKey;
        public int Stacks;
    }
}
