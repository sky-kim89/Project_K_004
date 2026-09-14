using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  BattleInfoButtonUI.cs
//  전황 정보창을 여는 버튼. 상단바 오른쪽 띠에 선다.
//
//  ■ ⚠ 언제든 눌린다 — 대기 중에도, 전투 중에도
//    "시작" 버튼(EnemyInfoButtonUI)과 다른 물건이다. 그쪽은 대기 중에만
//    눌리는 진행 버튼이고, 이건 **읽는 창**이라 막을 이유가 없다.
//    전투 중에 열면 지금 덱이 얼마나 센지를 그대로 보여 준다.
//
//  ■ ⚠ 스테이지 번호·성격은 StageLoopDirector 에서 받는다
//    RunBootstrap 의 _stageKind 는 private 이고, 화면이 그 값을 다시 계산하면
//    갈림길에서 고른 엘리트가 화면에만 일반으로 뜬다.
// ============================================================

public class BattleInfoButtonUI : MonoBehaviour
{
    [Tooltip("누르면 전황 창이 열린다. 이 컴포넌트가 붙은 오브젝트가 아니어도 된다.")]
    [SerializeField] Button _button;

    void Awake()
    {
        _button.onClick.RemoveListener(HandleClick);
        _button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        var director = StageLoopDirector.Instance;

        int          stage = director != null ? director.StageNumber : 1;
        RunStageKind kind  = director != null ? director.Kind : RunStageKind.Normal;

        var popup = PopupManager.Instance?.Open<BattleInfoPopup>(PopupType.BattleInfo);

        if (popup == null)
        {
            // ⚠ 조용히 넘어가지 않는다 — 눌렀는데 아무 일도 안 나는 것처럼 보인다
            Debug.LogError("[BattleInfoButtonUI] 전황 창을 열지 못했습니다.\n" +
                           "Tools > Project K > 프리팹 생성 > 팝업 > 전황 을 실행하고 " +
                           "PopupManager > Load Popup Prefabs 를 누르세요.");
            return;
        }

        popup.Setup(stage, kind);
    }
}
