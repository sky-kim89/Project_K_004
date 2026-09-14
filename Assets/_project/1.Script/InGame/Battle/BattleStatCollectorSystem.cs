using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using BattleGame.Units;

// ============================================================
//  BattleStatCollectorSystem.cs
//  DamageResultBuffer 를 매 프레임 읽어 BattleStatsTracker 에 귀속.
//
//  실행 순서: UnitHitSystem 이후 → 같은 프레임 내 결과를 즉시 집계
//
//  딜 분류:
//    HitType.Skill                → DamageCategory.Skill
//    SummonedTag 있는 공격자      → DamageCategory.Skill
//    SoldierComponent 있는 공격자 → DamageCategory.Soldier
//    GeneralComponent 있는 공격자 → DamageCategory.General
//
//  피해 수신:
//    피격자가 아군 장군   → DamageTaken
//    피격자가 아군 병사   → SoldierDamageTaken (소속 장군 기준)
// ============================================================

namespace BattleGame.Units
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitHitSystem))]
    public partial class BattleStatCollectorSystem : SystemBase
    {
        ComponentLookup<GeneralComponent>       _generalLookup;
        ComponentLookup<SoldierComponent>       _soldierLookup;
        ComponentLookup<SummonedTag>            _summonedLookup;
        // 성벽 뒤에 선 마왕 — 이 유닛이 맞으면 성벽이 반응한다
        ComponentLookup<BattleGame.Units.WallCoverComponent> _wallCoverLookup;

        protected override void OnCreate()
        {
            _generalLookup  = GetComponentLookup<GeneralComponent>(isReadOnly: true);
            _soldierLookup  = GetComponentLookup<SoldierComponent>(isReadOnly: true);
            _summonedLookup = GetComponentLookup<SummonedTag>(isReadOnly: true);
            _wallCoverLookup = GetComponentLookup<BattleGame.Units.WallCoverComponent>(isReadOnly: true);
        }

        protected override void OnUpdate()
        {
            var tracker = BattleStatsTracker.Instance;
            if (tracker == null) return;

            // ⚠ 두 통계가 같은 버퍼를 읽는다
            //   BattleStatsTracker 는 **장군 기준**(원작), CardStatsTracker 는
            //   **카드 기준**(이 게임)이다. 버퍼를 비우는 것은 이 시스템뿐이라
            //   여기서 둘 다 채워야 한다 — 시스템을 하나 더 만들면 먼저 도는 쪽이
            //   버퍼를 지워 나머지 하나가 영영 0 이 된다.
            var cards = CardStatsTracker.Instance;

            _generalLookup.Update(this);
            _soldierLookup.Update(this);
            _summonedLookup.Update(this);
            _wallCoverLookup.Update(this);

            foreach (var (results, identityRO, entity) in
                     SystemAPI.Query<DynamicBuffer<DamageResultElement>,
                                    RefRO<UnitIdentityComponent>>()
                              .WithEntityAccess())
            {
                if (results.Length == 0) continue;

                // ⚠ 마왕이 맞으면 성벽이 반응한다
                //   피해는 소환사 엔티티로 들어오지만(캐릭터 HP = 마왕성 HP),
                //   화면에서 번쩍이고 흔들리는 것은 성벽이어야 한다.
                //   UnitHitSystem 이 본체 플래시를 껐으므로 여기서 성벽에 넘긴다.
                //   ⚠ 이 자리인 이유: DamageResultElement 를 비우는 것이 이
                //     시스템뿐이라, 다른 데서 읽으면 이미 비어 있다.
                if (_wallCoverLookup.HasComponent(entity))
                    CastleWallView.Instance?.Hit();

                // ── 피격자의 소속 장군 결정 ──────────────────
                Entity victimGeneralEntity = Entity.Null;
                bool   victimIsGeneral     = false;
                var    identity            = identityRO.ValueRO;

                if (identity.Team == TeamType.Ally)
                {
                    if (identity.Type == UnitType.General)
                    {
                        victimGeneralEntity = entity;
                        victimIsGeneral     = true;
                    }
                    else if (identity.Type == UnitType.Soldier &&
                             _soldierLookup.HasComponent(entity))
                    {
                        victimGeneralEntity = _soldierLookup[entity].GeneralEntity;
                    }
                }

                // ── 피해 숫자 표시용 합산 ─────────────────
                //
                //   ⚠ 개체 하나에 **한 프레임에 하나만** 띄운다
                //     한 프레임에 대여섯 대를 맞는 일이 흔한데(광역·다발 공격),
                //     타격마다 숫자를 띄우면 같은 자리에 겹쳐 뜨기만 하고
                //     읽히지 않는다. 여기서 합쳐 보내면 DamageNumberLayer 의
                //     시간 합산(MergeWindow)과 두 겹으로 개수를 줄인다.
                float frameDamage = 0f;
                bool  frameKill   = false;

                for (int i = 0; i < results.Length; i++)
                {
                    DamageResultElement r = results[i];

                    frameDamage += r.ActualDamage;
                    frameKill   |= r.IsKill;

                    // ── 카드 기준 집계 (이 게임의 정본) ─────────
                    //   장군·병사 계층을 거치지 않고 공격자 엔티티에서 곧장 카드를 찾는다.
                    cards?.RecordDamageTaken(entity, r.ActualDamage);

                    if (r.AttackerEntity != Entity.Null)
                        cards?.RecordDamage(r.AttackerEntity, r.ActualDamage, r.Type, r.IsKill);

                    // ── 피해 수신 기록 (아군 피격 시만) ─────────
                    if (victimGeneralEntity != Entity.Null &&
                        tracker.GetEntry(victimGeneralEntity) != null)
                    {
                        tracker.RecordDamageTaken(victimGeneralEntity, r.ActualDamage, !victimIsGeneral);
                        if (r.AbsorbedDamage > 0f)
                            tracker.RecordAbsorbed(victimGeneralEntity, r.AbsorbedDamage);
                    }

                    // ── 피해 귀속 (공격자 → 소속 장군) ──────────
                    if (r.AttackerEntity == Entity.Null) continue;

                    Entity attackerGeneral = ResolveGeneral(r.AttackerEntity, _generalLookup, _soldierLookup);
                    if (attackerGeneral == Entity.Null) continue;
                    if (tracker.GetEntry(attackerGeneral) == null) continue;

                    DamageCategory cat = ClassifyDamage(
                        r.AttackerEntity, r.Type,
                        _generalLookup, _soldierLookup, _summonedLookup);

                    tracker.RecordDamage(attackerGeneral, r.ActualDamage, cat);

                    if (r.IsKill)
                        tracker.RecordKill(attackerGeneral);
                }

                ShowDamageNumber(entity, identity.Team, frameDamage, frameKill);

                results.Clear();
            }
        }

        // ── 피해 숫자 ────────────────────────────────────────────

        /// <summary>
        /// 이번 프레임에 이 유닛이 받은 피해를 화면에 띄운다.
        ///
        /// ⚠ 이 자리인 이유 — DamageResultElement 를 비우는 것이 이 시스템뿐이다
        ///   다른 데서 읽으면 이미 비어 있다. 성벽 반응(CastleWallView)이
        ///   여기 붙어 있는 것과 같은 이유다.
        ///
        /// 위치는 유닛의 발밑이다. 숫자가 뜨는 높이는 DamageNumberLayer 가 정한다 —
        /// 유닛마다 키가 달라도 숫자 높이는 일정해야 눈이 따라갈 수 있다.
        /// </summary>
        void ShowDamageNumber(Entity victim, TeamType team, float damage, bool killed)
        {
            if (damage <= 0f) return;
            if (!SystemAPI.HasComponent<LocalTransform>(victim)) return;

            float3 p = SystemAPI.GetComponent<LocalTransform>(victim).Position;

            DamageNumberLayer.Instance.Show(
                victim,
                new Vector3(p.x, p.y, p.z),
                damage,
                toHero: team == Faction.Hero,
                isKill: killed);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────

        static Entity ResolveGeneral(Entity attacker,
            ComponentLookup<GeneralComponent> generalLookup,
            ComponentLookup<SoldierComponent> soldierLookup)
        {
            if (generalLookup.HasComponent(attacker)) return attacker;
            if (soldierLookup.HasComponent(attacker)) return soldierLookup[attacker].GeneralEntity;
            return Entity.Null;
        }

        static DamageCategory ClassifyDamage(Entity attacker, BattleGame.Units.HitType hitType,
            ComponentLookup<GeneralComponent> generalLookup,
            ComponentLookup<SoldierComponent> soldierLookup,
            ComponentLookup<SummonedTag>      summonedLookup)
        {
            if (hitType == BattleGame.Units.HitType.Skill)  return DamageCategory.Skill;
            if (summonedLookup.HasComponent(attacker))      return DamageCategory.Skill;
            if (soldierLookup.HasComponent(attacker))       return DamageCategory.Soldier;
            if (generalLookup.HasComponent(attacker))       return DamageCategory.General;
            return DamageCategory.General;
        }
    }
}
