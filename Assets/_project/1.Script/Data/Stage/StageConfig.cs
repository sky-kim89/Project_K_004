using UnityEngine;

// ============================================================
//  StageConfig.cs
//  런 길이·환생 문턱 — Resources/StageConfig.asset (LobbyManager 가 Current 에 꽂는다).
//
//  ⚠ 원작의 웨이브·적 수·보상 곡선 필드는 걷어냈다 — 적 편성은 HeroDeployment,
//    보상은 RunGoldRule·카드 3택이 정한다.
// ============================================================

[CreateAssetMenu(fileName = "StageConfig", menuName = "Project K/Stage Config")]
public class StageConfig : ScriptableObject
{
    static StageConfig _current;
    public static StageConfig Current
    {
        get => _current != null ? _current : (_current = Resources.Load<StageConfig>("StageConfig"));
        internal set => _current = value;
    }

    [Tooltip("일반 스테이지 수 — 이 번호부터 무한 보스다 (GameplayConfig.MaxStage · EndlessBossRule).")]
    public int NormalStageCount = 30;

    [Tooltip("환생 포인트가 쌓이기 시작하는 스테이지 (ReincarnationData).")]
    public int ReincarnateMinStage = 2;
}
