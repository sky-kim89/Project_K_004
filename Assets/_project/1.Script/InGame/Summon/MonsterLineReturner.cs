using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterLineReturner.cs
//  살아남은 몬스터를 **제 라인 대기열로 돌려보내는** 컴포넌트.
//
//  ■ 무엇이 바뀌었나 (2026-08-28)
//    ① 옛 이름은 MonsterRefundCarrier 였고, 적을 다 잡으면 살아남은 개체가
//       제 소환 비용을 마나로 돌려주고 사라졌다. 그 규칙에서는 마나 손실이
//       곧 **사망률**이라 "더 많이 소환하기" 가 언제나 최적해로 굳었다.
//    ② 그 다음 규칙은 살아남은 개체를 **필드 위 라인 시작점으로 옮겨 세워
//       두는** 것이었다. 이것도 접었다 — 아래 참고.
//
//    지금은 **대기열(SummonReservation)로 돌아간다.** 개체는 필드에서 사라지고,
//    다음 스테이지가 시작되면 대기열에서 한 마리씩 저절로 나온다.
//
//  ■ 왜 필드에 세워 두지 않는가
//    · 세워 두면 판이 넘어가도 필드에 그대로 남아, 다음 스테이지의 생존
//      카운트(AliveAllyCount)가 실제보다 적게 잡힌다. 그 어긋남 때문에
//      "몬스터가 다 죽으면 용사가 성벽으로 돌진하지 않는" 버그가 났다.
//    · 필드에 남은 개체는 스테이지 대기 중에도 이동·타겟 로직을 타서,
//      제자리에 서 있으라는 보장이 없었다(간헐적으로 안 돌아가 보이던 원인).
//    · 대기열로 돌리면 "이번 판에 무엇이 나가는가" 가 카드·대기열 UI 한곳에
//      모인다. 살아 돌아온 전력이 눈에 보이는 목록으로 남는다.
//
//  ■ 카드로 소환한 개체만 돌아간다
//    슬라임 분열체·강령술사가 부활시킨 개체는 **대기열에 들어가지 않는다.**
//    공짜로 나온 것이 줄에 쌓이면 판을 거듭할수록 마나를 내지 않은 물량이
//    불어나, 소환 경제가 통째로 무너진다. 그것들은 판이 끝나면 사라진다.
//
//  ■ 체력은 신경 쓰지 않아도 된다
//    다시 나올 때 MonsterSpawner 가 처음부터 새로 만든다 — 언제나 만피다.
//    카드 레벨도 그때 다시 읽으므로, 보상으로 카드를 키웠으면 강해져 나온다.
//
//  ■ 판단은 여기서 하지 않는다
//    "적을 모두 잡았는가" 는 SummonController 가 한 번만 판정하고
//    ReturnAllToQueue() 를 부른다. 개체마다 Update 로 각자 판정하면
//    배출 코루틴과 타이밍이 엇갈려 돌아가자마자 다시 나오는 일이 생긴다.
// ============================================================

[RequireComponent(typeof(MonsterRuntimeBridge))]
public class MonsterLineReturner : MonoBehaviour
{
    /// <summary>지금 필드에 서 있는 몬스터들. 판이 끝날 때 한 번에 훑는다.</summary>
    static readonly List<MonsterLineReturner> _live = new(64);

    /// <summary>이번 거두기에서 중첩 보너스를 이미 준 (라인, 종족) 짝.</summary>
    static readonly HashSet<(int, MonsterSpeciesData)> _doubled = new();

    /// <summary>이 개체가 돌아갈 라인. 소환할 때 한 번 심는다.</summary>
    int _lane;

    /// <summary>이 개체가 속한 라인. 사망 시 숲 금 복귀가 읽는다.</summary>
    public int Lane => _lane;

    /// <summary>다시 나올 때 무엇으로 나올 것인가.</summary>
    MonsterSpeciesData _species;

    /// <summary>대기열로 돌아갈 자격 — 카드로 소환된 개체만 true.</summary>
    bool _returnable;

    /// <summary>
    /// 소환 직후 한 번. 파생 개체(분열·부활)는 returnable 을 false 로 넘긴다.
    /// </summary>
    public void Setup(int lane, MonsterSpeciesData species, bool returnable)
    {
        _lane       = lane;
        _species    = species;
        _returnable = returnable;

        // ⚠ 시너지의 '지금 존재하는 종족' 은 여기서 센다 (2026-09-09)
        //   모든 몬스터가 이 컴포넌트를 달고 나오므로 — 카드 소환도, 분열체도,
        //   스킬 소환도 — 여기 한 곳이면 빠지는 경로가 없다.
        //   ⚠ 반드시 짝이 맞아야 한다: 여기서 +1, OnDisable 에서 −1.
        //     한쪽만 돌면 전멸해도 시너지가 안 꺼지거나, 살아 있는데 꺼진다.
        //   ⚠ 한 번만 센다 — Setup 은 개체 하나에 두 번 불릴 수 있다
        //     (MonsterSpawner 가 스폰 때 한 번, 카드 소환이 자격을 올리며 한 번).
        if (!_counted)
        {
            _counted = true;
            MonsterSynergyRule.NoteAlive(species, +1);
        }
    }

    /// <summary>시너지 집계에 이 개체를 더해 두었는가 (짝을 맞추기 위한 표식).</summary>
    bool _counted;

    void OnEnable()
    {
        // 풀에서 물려받은 개체가 지난 판의 라인·자격을 들고 있지 않게 한다.
        // ⚠ 기본값은 '돌아가지 않는다' 다 — Setup 을 빠뜨린 경로가 공짜로
        //   대기열을 불리는 것보다, 조용히 사라지는 편이 안전하다.
        _lane       = 0;
        _species    = null;
        _returnable = false;
        _counted    = false;

        _live.Add(this);
    }

    void OnDisable()
    {
        _live.Remove(this);

        if (!_counted) return;

        _counted = false;
        MonsterSynergyRule.NoteAlive(_species, -1);
    }

    // ── 라인별 머릿수 — 특성 '소수정예'·'군세' 가 읽는다 ──────

    /// <summary>지금 그 라인에 서 있는 몬스터 수.</summary>
    public static int AliveInLane(int lane)
    {
        int n = 0;
        for (int i = 0; i < _live.Count; i++)
            if (_live[i] != null && _live[i]._lane == lane) n++;

        return n;
    }

    /// <summary>
    /// <b>한 마리 이상 서 있는</b> 라인 중 가장 적은 라인의 머릿수. 아무도 없으면 0.
    ///
    /// ⚠ 빈 라인을 세지 않는다
    ///   0 을 후보에 넣으면 아무도 안 넣은 라인이 언제나 최소가 되어,
    ///   '소수정예' 의 보너스가 아무에게도 가지 않는다.
    /// </summary>
    public static int MinOccupiedLaneCount(int laneCount)
    {
        int min = int.MaxValue;

        for (int lane = 0; lane < laneCount; lane++)
        {
            int n = AliveInLane(lane);
            if (n > 0 && n < min) min = n;
        }

        return min == int.MaxValue ? 0 : min;
    }

    // ── 판 종료 처리 ─────────────────────────────────────────

    /// <summary>
    /// 필드에 남은 몬스터를 전부 거둔다.
    /// 카드로 소환된 개체는 제 라인 대기열 뒤에 한 마리로 다시 붙고,
    /// 파생 개체는 그냥 사라진다. 어느 쪽이든 필드에서는 내린다.
    ///
    /// 부르는 곳은 SummonController 하나다 (적 전멸을 판정한 그 프레임).
    /// </summary>
    public static void ReturnAllToQueue(SummonReservation reservation)
    {
        // ⚠ 사본을 훑는다 — Despawn 이 OnDisable 을 태워 _live 를 줄인다
        var snapshot = _live.ToArray();

        _doubled.Clear();

        foreach (MonsterLineReturner returner in snapshot)
        {
            if (returner == null) continue;

            // ⚠ 죽는 중인 개체는 건드리지 않는다
            //   사망 연출이 도는 동안에도 GameObject 는 켜져 있다. 여기서 거두면
            //   방금 쓰러진 몬스터가 대기열에 되살아나고, 분열·부활 훅(MonsterDeathWatcher)
            //   이 도는 타이밍까지 어긋난다. 사망 처리는 제 파이프라인에 맡긴다.
            if (!returner.GetComponent<MonsterRuntimeBridge>().IsAlive) continue;

            if (returner._returnable && returner._species != null)
            {
                // 산 개체가 제자리로 가는 것이라 수가 늘지 않는다.
                // ⚠ 대기열에는 상한이 없다 (SummonReservation 파일 머리 주석, 2026-09-12).
                reservation.EnqueueOne(returner._species, returner._lane);

                // ── 중첩 3단계(시너지 7개) — 한 마리 늘어 돌아온다 ──
                //   ⚠ 개체마다가 아니라 **라인·종족마다 한 번**이다
                //     (사용자 지적, 2026-09-09) 개체마다 주면 살아남은 수가
                //     매 판 두 배가 된다 — 27스테이지쯤에서 한 라인 대기열이
                //     200마리를 넘었다. 지수로 불어나는 보너스는 보너스가 아니라
                //     시간이 지나면 화면을 채우는 고장이다.
                if (MonsterSynergyRule.StackDoublesLineReturn(MonsterSynergyRule.ActiveCount)
                    && _doubled.Add((returner._lane, returner._species)))
                    reservation.EnqueueOne(returner._species, returner._lane);
            }

            // ⚠ 거두는 것은 죽는 것이 아니다 — 사망 훅을 명시적으로 내린다
            //   지금은 EntityLink 가 엔티티를 **파괴하지 않고 Disabled 만** 붙이는
            //   덕분에 MonsterDeathWatcher 의 IsAlive 검사가 true 로 남아 저절로
            //   빠져나간다. 즉 이 줄이 없어도 지금은 돈다 — 그 사실 하나에
            //   기대고 있을 뿐이다. EntityLink 가 언젠가 파괴로 바뀌면 거둔 개체가
            //   전부 '사망' 이 되어 숲 금이 대기열을 두 배로 불리고 분열까지 터진다.
            //   여기서 사라지는 것은 어떤 경우에도 죽음이 아니므로 그렇게 못 박는다.
            returner.GetComponent<MonsterDeathWatcher>().Disarm();

            PoolController.Instance.Despawn(returner.gameObject);
        }
    }
}
