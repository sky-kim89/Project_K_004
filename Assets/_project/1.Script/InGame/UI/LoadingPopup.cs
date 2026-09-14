using System.Collections;
using UnityEngine;
using TMPro;

// ============================================================
//  LoadingPopup.cs
//  게임 시작 로딩 팝업 — 장군이 모두 스폰되면 자동으로 닫힌다.
//
//  InGameManager 가 BattleManager.OnAlliesReady 이벤트를 받아
//  Close() 를 호출한다.
//
//  Hierarchy 예시:
//    LoadingPopup (PopupBase, CanvasGroup)
//      ├ TitleText  (TMP — _titleText)
//      └ StatusText (TMP — _statusText, 점 애니메이션)
// ============================================================

public class LoadingPopup : PopupBase
{
    public override bool BlockBackgroundClose => true;

    // 로딩은 플레이어가 연 팝업이 아니다 — 여닫는 소리가 나면 조작음으로 오해된다.
    protected override bool PlayOpenCloseSfx => false;

    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _statusText;

    Coroutine _dotRoutine;

    protected override void OnAfterOpen()
    {
        if (_titleText != null)
            _titleText.text = "배틀 준비 중";

        _dotRoutine = StartCoroutine(DotAnimation());
    }

    protected override void OnAfterClose()
    {
        if (_dotRoutine != null)
        {
            StopCoroutine(_dotRoutine);
            _dotRoutine = null;
        }
    }

    // ── 점 애니메이션 ─────────────────────────────────────────

    IEnumerator DotAnimation()
    {
        int i = 0;
        while (true)
        {
            if (_statusText != null)
                _statusText.text = "장군 소환 중" + new string('.', (i % 3) + 1);
            i++;
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
}
