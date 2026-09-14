using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SummonFieldLayout.cs
//  전장의 소환 관련 좌표를 한곳에 모은 씬 컴포넌트.
//  InGame 씬의 전장 루트에 붙이고 Inspector 에서 지점을 잡는다.
//
//  ■ 라인(칸) = 5개 — "소환 배치 지점"일 뿐이다
//    라인은 소환되는 위치(Y좌표)만 정한다.
//    소환된 뒤의 행동은 기존 시스템과 완전히 동일하다 —
//    라인을 따라 직진하는 레인 디펜스가 아니다.
//    몬스터는 소환 직후 돌진한 뒤 원작의 타겟 탐색·이동·분리·전투 AI 그대로
//    필드를 자유롭게 돌아다닌다.
//    → 라인에 갇힌다고 가정하고 이동 로직을 짜지 말 것.
//
//  ■ 생존자 회수에는 좌표가 필요 없다
//    예전에는 오른쪽에 "환수 경계" 를 두고 거기까지 걸어 나간 몬스터만
//    마나를 돌려줬다. 지금은 **적을 모두 잡은 그 순간** 살아 있는 몬스터가
//    제자리에서 거둬져 제 라인 대기열로 돌아간다 (MonsterLineReturner).
//    그래서 이 파일에는 회수 관련 좌표가 없다 — 찾다가 없다고 새로 만들지 말 것.
// ============================================================

public class SummonFieldLayout : MonoBehaviour
{
    /// <summary>라인(칸) 개수. 기획 확정값.</summary>
    public const int LaneCount = 5;

    public static SummonFieldLayout Instance { get; private set; }

    [Header("소환 라인 — 위에서 아래 순서로 5개")]
    [Tooltip("성벽 바로 옆의 소환 지점. 라인은 이 지점의 Y좌표만 결정한다.\n" +
             "소환된 몬스터는 이후 라인과 무관하게 자유롭게 움직인다.")]
    public List<Transform> LanePoints = new();

    [Header("라인 두께")]
    [Tooltip("라인 중심에서 위아래로 흩뜨릴 범위(월드 단위).\n" +
             "0 이면 전부 같은 Y 에 나와 일렬로 늘어선다.")]
    [Min(0f)]
    public float LaneHalfHeight = 1.1f;

    [Header("소환사 위치")]
    [Tooltip("소환사(= 마왕성) 가 서 있는 지점. 이동하지 않는다.")]
    public Transform SummonerPoint;

    [Header("성벽 경계 (월드 X)")]
    [Tooltip("유닛이 이 X 보다 왼쪽으로 갈 수 없다. 여기부터가 성이다.\n" +
             "⚠ 소환사보다 너무 오른쪽에 두면 용사가 소환사를 때리지 못해\n" +
             "  패배가 성립하지 않는다. 근접 사거리 안에 들어오게 잡을 것.")]
    // ⚠ 정본은 InGameSceneSetup.WallX 다 — 이건 씬을 굽기 전의 기본값일 뿐이다.
    //   둘이 갈리면 "씬을 안 구운 상태" 가 눈에 안 띄므로 같은 숫자로 맞춰 둔다.
    public float WallBoundaryX = -12f;

    /// <summary>
    /// 유닛이 넘어갈 수 없는 왼쪽 한계.
    ///
    /// ⚠ 소환사 본인은 이 경계 뒤(왼쪽)에 선다
    ///   성 안에 있는 것이 정상이므로 클램프 대상이 아니다.
    /// </summary>
    public static float WallX
        => Instance != null ? Instance.WallBoundaryX : float.NegativeInfinity;

    /// <summary>
    /// 성벽과 소환사 사이의 거리 = 성벽의 두께.
    ///
    /// 용사는 성벽에서 멈추고 소환사는 그 뒤에 서므로, 둘 사이는 이 거리보다
    /// 가까워질 수 없다. 근접 용사의 사거리(0.7~1.2)가 이보다 짧으면 소환사를
    /// 영영 때리지 못하므로, 이 값이 그대로 공격자의 사거리 보정이 된다.
    /// (WallCoverComponent 참고)
    /// </summary>
    public float WallGap => WallBoundaryX - SummonerPoint.position.x;

    void Awake()
    {
        Instance = this;

        if (LanePoints.Count != LaneCount)
            Debug.LogError($"[SummonFieldLayout] 라인은 {LaneCount}개여야 합니다. 현재 {LanePoints.Count}개.");

        // ⚠ 소환사가 성벽 오른쪽에 있으면 성 밖에 서 있는 것이다
        //   그림상으로도 틀리고, 용사가 성벽에 막히기 전에 소환사와 먼저 부딪힌다.
        if (WallGap <= 0f)
            Debug.LogError($"[SummonFieldLayout] 소환사가 성벽 왼쪽에 있어야 합니다. " +
                           $"성벽 {WallBoundaryX} / 소환사 {SummonerPoint.position.x}");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>laneIndex 번 라인의 기준 지점. (0 = 맨 위)</summary>
    public Vector3 GetLanePosition(int laneIndex)
    {
        return LanePoints[laneIndex].position;
    }

    /// <summary>
    /// 실제로 몬스터를 세울 위치. 라인 안에서 Y 를 흩뜨린다.
    ///
    /// ⚠ 기준점 그대로 쓰면 한 줄로 늘어선다
    ///   같은 Y 에 계속 뱉으면 몬스터가 일렬로 서서 앞의 한 마리만 싸운다.
    ///   흩어 놓아야 여럿이 동시에 붙고 분리(Separation)도 자연스럽게 풀린다.
    /// </summary>
    public Vector3 GetSpawnPosition(int laneIndex)
    {
        Vector3 at = LanePoints[laneIndex].position;
        at.y += Random.Range(-LaneHalfHeight, LaneHalfHeight);
        return at;
    }

    /// <summary>
    /// 월드 좌표에서 가장 가까운 라인 인덱스를 찾는다.
    /// 플레이어가 맵을 탭했을 때 어느 칸에 놓으려 했는지 판정하는 데 쓴다.
    /// </summary>
    public int GetNearestLane(Vector3 worldPos)
    {
        int   best     = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < LanePoints.Count; i++)
        {
            float d = Mathf.Abs(LanePoints[i].position.y - worldPos.y);
            if (d < bestDist)
            {
                bestDist = d;
                best     = i;
            }
        }
        return best;
    }

    // ── 에디터 기즈모 (배치 확인용) ──────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        for (int i = 0; i < LanePoints.Count; i++)
        {
            if (LanePoints[i] == null) continue;

            Vector3 p = LanePoints[i].position;
            Gizmos.DrawWireSphere(p, 0.35f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + Vector3.up * 0.5f, $"라인 {i + 1}");
#endif
        }

        // 성벽 경계 — 유닛은 이 왼쪽으로 못 간다
        Gizmos.color = new Color(0.95f, 0.35f, 0.35f, 0.9f);
        Gizmos.DrawLine(new Vector3(WallBoundaryX, -20f, 0f),
                        new Vector3(WallBoundaryX,  20f, 0f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(new Vector3(WallBoundaryX, 10f, 0f), "성벽 (진입 불가)");
#endif

        if (SummonerPoint != null)
        {
            Gizmos.color = new Color(0.9f, 0.3f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(SummonerPoint.position, Vector3.one * 0.8f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(SummonerPoint.position + Vector3.up * 0.7f, "소환사 (= 마왕성)");
#endif
        }
    }
}
