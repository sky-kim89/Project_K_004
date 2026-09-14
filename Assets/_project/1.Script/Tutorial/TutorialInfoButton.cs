using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  TutorialInfoButton.cs
//  팝업 헤더의 'i' 버튼 — 그 팝업의 도움말 튜토리얼을 다시 재생한다.
//
//  ■ 붙이는 법
//    Creator 에서 EditorUIBuilder.InfoBtn(header, TutorialId.HelpXxx) 한 줄.
//    닫기 버튼 왼쪽에 자동으로 놓인다.
//
//  ■ Replay 를 쓴다 — TryPlay 가 아니다
//    도움말은 "이미 봤는지" 와 무관하게 눌렀을 때 나와야 한다.
//    Replay 는 완료 기록을 보지도, 남기지도 않는다 (100번대 = IsForced false).
//
//  ⚠ 시나리오가 없으면 버튼을 숨긴다
//    아직 안 만든 팝업에 눌러도 아무 일 없는 버튼이 남아 있으면
//    "고장난 버튼" 으로 읽힌다. 등록 여부를 보고 스스로 사라진다.
//
//  ⚠ 튜토리얼 중이라고 버튼을 잠그지 않는다
//    잠글 이유가 없다. 튜토리얼이 도는 동안은 TutorialBlocker 가 화면 전체를
//    먹으므로 이 버튼까지 손이 닿지 않고, 닿는 유일한 틈(Unblock 대기 구간)에서
//    눌러도 Replay 가 IsPlaying 을 보고 스스로 물러난다.
//    중복 재생 방어는 매니저 몫이다 — 여기서 또 막으면, 잠긴 상태로 굳어
//    "영영 안 눌리는 i 버튼" 이 되는 쪽이 훨씬 흔한 사고다.
// ============================================================

[RequireComponent(typeof(Button))]
public class TutorialInfoButton : MonoBehaviour
{
    [SerializeField] TutorialId _tutorialId = TutorialId.None;

    /// <summary>Creator 가 프리팹을 만들 때 꽂는다.</summary>
    public void SetTutorial(TutorialId id) => _tutorialId = id;

    void Awake() => GetComponent<Button>().onClick.AddListener(OnClick);

    void OnEnable()
    {
        // 매니저가 아직 없을 수도 있다 (씬 로드 순서) — 그때는 일단 보여 준다.
        var mgr = TutorialManager.Instance;
        gameObject.SetActive(_tutorialId != TutorialId.None
                             && (mgr == null || mgr.Has(_tutorialId)));
    }

    void OnClick()
    {
        if (_tutorialId == TutorialId.None) return;
        TutorialManager.Ensure().Replay(_tutorialId);
    }
}
