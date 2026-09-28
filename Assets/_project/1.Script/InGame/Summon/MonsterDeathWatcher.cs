using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterDeathWatcher.cs
//  몬스터가 죽는 순간을 잡아 **종족 패시브**와 **소환사 개성**에 알린다.
//
//  ■ 왜 ECS 시스템이 아니라 MonoBehaviour 인가
//    분열·부활은 결국 PoolController 로 새 GameObject 를 꺼내야 한다 —
//    managed 작업이다. UnitDeathDespawnSystem 에서 하려면 거기에 managed 분기를
//    또 만들어야 하는데, 그 시스템은 사망 처리라는 한 가지 일에만 집중해야 한다.
//
//    대신 그 시스템이 이미 지나는 길목을 쓴다:
//      DeadTag 감지 → UnitAnimationSync.TriggerDeath() → 연출 → Despawn(비활성)
//    풀 반납이 OnDisable 로 들어오므로 여기서는 '죽어서 꺼졌는가' 만 보면 된다.
//
//  ■ 살아서 물러난 것과 죽은 것을 구분한다
//    MonsterLineReturner 는 적을 다 잡으면 생존자를 **죽이지 않고** 반납한다
//    (대기열로 돌려보내는 길이다). 그것까지 사망으로 세면 스테이지가 끝날
//    때마다 슬라임이 우수수 분열한다.
//    그래서 HP 를 직접 보고 판단한다 (UnitRuntimeBridge.IsAlive).
//
//  ■ 죽은 개체가 물려주는 것을 들고 있다 (MonsterOrigin, 2026-09-07)
//    분열체·부활체는 <b>그 몬스터가 다시 서는 것</b>이라, 카드 레벨·융합 개성·
//    외형 시드를 그대로 이어받아야 한다. 그 짐을 스폰 때 여기 실어 두고
//    죽는 순간 그대로 넘긴다 — 사망 시점에는 카드가 어디 있는지 알 수 없다
//    (덱은 그 사이에 바뀔 수 있고, 필드의 개체는 카드를 참조하지 않는다).
//
//  ■ 두 축이 각각 다른 조건으로 걸린다
//    종족 패시브 — **세대**로 제한한다. 분열체(1세대)는 더 분열하지 않지만
//                  회복·중독 같은 비증식 효과는 그대로 터진다.
//    소환사 개성 — **카드 소환 여부**로 제한한다. 부활한 개체가 또 부활을
//                  부르면 전투가 끝나지 않는다.
//
//    ⚠ 둘을 하나의 플래그로 합치지 말 것
//      "분열체도 힐 슬라임이면 죽을 때 회복해야 한다" 와
//      "부활체가 또 부활하면 안 된다" 는 다른 규칙이다.
// ============================================================

[RequireComponent(typeof(MonsterRuntimeBridge))]
public class MonsterDeathWatcher : MonoBehaviour
{
    /// <summary>종족 패시브 목록 버퍼. 개체마다 새로 만들지 않는다 (메인 스레드 전용).</summary>
    static readonly List<SpeciesPassive> _buffer = new(8);

    SummonerData       _summoner;
    MonsterSpeciesData _species;
    bool               _isCardSummoned;
    int                _generation;
    MonsterOrigin      _origin;
    bool               _armed;

    /// <summary>
    /// 스폰 직후 한 번. 이 개체가 무엇이고 어떤 훅을 태울지를 정한다.
    /// </summary>
    /// <param name="generation">0 = 카드로 직접 소환. 분열·부활마다 1씩 는다.</param>
    /// <param name="origin">
    /// 이 개체가 다음 개체에게 물려줄 것 — 카드 레벨·융합 개성·외형 시드.
    /// ⚠ 스폰 때 받아 둔다. 죽는 순간에는 이 개체가 어느 카드에서 나왔는지
    ///   알아낼 방법이 없다 (필드의 개체는 덱을 참조하지 않는다).
    /// </param>
    public void Arm(SummonerData summoner, MonsterSpeciesData species,
                    bool isCardSummoned, int generation, in MonsterOrigin origin)
    {
        _summoner       = summoner;
        _species        = species;
        _isCardSummoned = isCardSummoned;
        _generation     = generation;
        _origin         = origin;
        _armed          = true;
    }

    /// <summary>
    /// 사망 훅을 내린다 — <b>이 개체가 사라지는 것은 죽음이 아니다.</b>
    ///
    /// ⚠ 판이 끝나 거둘 때 부른다 (MonsterLineReturner.ReturnAllToQueue)
    ///   Despawn 이 엔티티를 지우면 아래 OnDisable 의 IsAlive 가 false 가 되어
    ///   **멀쩡히 살아 돌아간 개체가 죽은 것으로 처리**된다. 숲 금이 걸려 있으면
    ///   대기열에 한 번 더 들어가고(같은 개체가 둘로 늘어난다), 분열까지 터진다.
    /// </summary>
    public void Disarm() => _armed = false;

    void OnDisable()
    {
        if (!_armed) return;
        _armed = false;   // 풀에 들어갔다 — 다음 Arm 까지는 아무것도 하지 않는다

        if (_summoner == null || _species == null) return;

        // 살아서 물러난 개체(환수)는 사망이 아니다 — 파일 머리 주석 참고.
        var bridge = GetComponent<MonsterRuntimeBridge>();
        if (bridge.IsAlive) return;

        // ⚠ 씬이 내려가는 중에는 아무것도 하지 않는다
        //   전투 종료·씬 전환에서 전 유닛이 한꺼번에 꺼지는데, 그때 분열을 돌리면
        //   이미 정리된 풀에 스폰을 요청하게 된다.
        if (BattleManager.Instance == null)                             return;
        if (BattleManager.Instance.Context == null)                     return;
        if (BattleManager.Instance.Context.State != BattleState.InWave) return;

        // ⚠ 이미 판을 거둔 뒤면 아무것도 하지 않는다 (사용자 지적, 2026-09-09)
        //   "적 전멸" 판정(SummonController.CheckWaveCleared)은 **상태가 아직
        //   InWave 일 때** 돌아 생존자를 전부 거둔다. 그런데 그 순간 쓰러지는
        //   중이던 개체는 거두기에서 일부러 건너뛰므로(사망 파이프라인에 맡긴다),
        //   조금 뒤 여기까지 와서 **거두기가 끝난 필드에 분열체를 낳는다.**
        //   그 개체는 다시 거둬지지 않아 다음 스테이지까지 남는다 —
        //   "가끔 슬라임 분열체가 남아 있다" 가 이것이다.
        //   ⚠ 상태만 보고는 못 잡는다. 상태가 InWave 에서 내려오는 것은
        //     BattleManager 가 승리를 알아차린 다음 프레임이라, 그 사이가 창이다.
        if (SummonController.Instance != null && SummonController.Instance.WaveSwept) return;

        Vector3 at = transform.position;

        // ── 종족 패시브 (분열·재조립·역병·자폭·회복 …) ──
        //   ⚠ 최대 체력은 굴려 나온 값을 넘긴다 (species.MaxHp 가 아니다)
        //     SO 원본값에는 카드 레벨·품질·소환력·시너지·분열 배율이 하나도
        //     안 들어가 있어서, 그걸 쓰면 만렙 힐 슬라임이 Lv1 과 똑같은 양을
        //     회복시킨다. 성장이 회복량을 따라가려면 반드시 RolledStat 이다.
        float maxHp  = bridge.RolledStat.Get(StatType.MaxHp);
        float attack = bridge.RolledStat.Get(StatType.Attack);

        // ⚠ 스폰과 **같은 함수**로 모은다 (PassiveResolver) — 선천 · 융합 · 장비 + 각성.
        //   한때 여기만 융합을 빠뜨려(2026-09-07) 융합으로 배운 사망 발동 패시브가
        //   통째로 무효였다. 목록을 두 곳에서 따로 짜면 반드시 다시 갈린다.
        // ⚠ 빈 카드(MonsterOrigin.None)여도 안전하다 — 융합이 없는 것과 같다.
        PassiveResolver.CollectFor(_species, _origin.Card, _buffer);

        SpeciesPassiveRuntime.OnDeath(_species, _summoner, at, maxHp, attack, _generation,
                                      _origin, _buffer);

        // '복수' — 같은 종족 아군에게 알린다. 죽은 개체가 무엇을 가졌든 부른다.
        SpeciesPassiveRuntime.NotifyAllyDied(_species);
        MonsterSynergyRuntime.OnDeath(_species, _summoner, at, _generation, _origin);

        // ── 숲 금 — 죽어도 제 라인으로 돌아간다 ──
        //   ⚠ 카드로 낸 개체만이다
        //     분열체·부활체까지 돌려보내면 한 판마다 공짜 물량이 불어난다
        //     (MonsterLineReturner 의 returnable 규칙과 같은 이유).
        //   라인 번호는 복귀 담당이 들고 있다 — 여기서 다시 세지 않는다.
        if (_isCardSummoned && TryGetComponent<MonsterLineReturner>(out var returner))
        {
            MonsterSynergyRuntime.OnDeathReturnToLine(_species, returner.Lane);

            // ── 특성 '귀환' — 죽어도 확률로 제 라인 대기열에 돌아간다 ──
            //   ⚠ 숲 금과 겹치면 둘 다 들어간다(두 마리로 돌아온다).
            //     겹치기 어려운 조합이라 그대로 둔다 — 막으면 "금까지 갔는데
            //     특성이 무효" 가 되어 더 나쁘다.
            //   ⚠ 카드로 낸 개체만이다. 분열체·부활체까지 돌려보내면
            //     마나를 내지 않은 물량이 판을 거듭할수록 불어난다.
            if (RunPerkRule.Has(RunPerk.Homecoming) &&
                Random.value < RunPerkRule.HomecomingChance)
                SummonController.Instance.ReturnToLine(_species, returner.Lane);   // 배출까지 켠다
        }

        // ── 소환사 개성 (강령술사의 부활 등) ──
        if (_isCardSummoned)
            SummonerPerkRuntime.OnMonsterDied(_summoner, _species, at);
    }
}
