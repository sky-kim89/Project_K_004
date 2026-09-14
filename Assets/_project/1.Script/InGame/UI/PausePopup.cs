using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  PausePopup.cs
//  일시 정지 / 메뉴 팝업.
//
//  열릴 때: Time.timeScale = 0 (게임 정지)
//  닫힐 때: Time.timeScale 원복 (PopupBase 는 unscaledDeltaTime 사용)
//
//  ■ 여는 곳이 둘이다
//    인게임  TopBarUI 의 일시 정지 버튼
//
//  ■ 항목
//    계속하기        → Close() (배속 복원)
//    효과음 / 배경음 → BattleSettingsData 토글. AudioManager 가 그 값을 읽는다.
//    즉시 환생하기   → BattleManager.Surrender() → 아군 전멸과 같은 경로로 패배 처리
//                     → InGameManager.HandleDefeat() 가 ReincarnationPopup 을 연다
//
//    ⚠ 예전엔 "다시 시작"·"종료" 가 있었지만 둘 다 Debug.Log 만 찍는 빈 껍데기였다.
//      런 도중 나갈 길은 환생 하나뿐이라 선택지를 그대로 둘 이유가 없다.
//
//  ⚠ "즉시 환생하기" 는 전투 중에만 보인다
//    로비에서는 포기할 전투가 없다 (Surrender 는 그냥 무시된다). 안 눌리는 버튼을
//    띄워 두는 대신 행을 접고 패널을 그만큼 줄인다 — 빈칸이 남으면 더 어색하다.
//    행 높이는 Creator 가 _surrenderRowH 로 넘겨 준다.
//
//  ⚠ Surrender() 전에 Time.timeScale 을 되돌려야 한다
//    timeScale=0 인 채로 패배 팝업이 뜨면 그 팝업의 애니메이션·버튼이 멈춘 것처럼 보인다.
//    (PopupBase 는 unscaledDeltaTime 을 쓰지만 그 뒤의 전투 정리 코루틴은 아니다)
//
//  Hierarchy — PopupPrefabCreator.CreatePausePopup() 이 만든다.
// ============================================================

public class PausePopup : PopupBase
{
    [Header("선택지")]
    [SerializeField] Button _resumeButton;
    [SerializeField] Button _reincarnateButton;

    [Header("사운드 토글")]
    [SerializeField] Button          _sfxButton;
    [SerializeField] Image           _sfxPill;
    [SerializeField] TextMeshProUGUI _sfxState;
    [SerializeField] Button          _bgmButton;
    [SerializeField] Image           _bgmPill;
    [SerializeField] TextMeshProUGUI _bgmState;

    [Header("전투 전용 행 접기 (Creator 가 채운다)")]
    [SerializeField] RectTransform _panelRect;
    [SerializeField] RectTransform _borderRect;
    [SerializeField] float         _panelFullH;
    [SerializeField] float         _surrenderRowH;

    /// <summary>테두리가 패널 밖으로 드러나는 두께 — Creator 의 값과 같아야 한다.</summary>
    const float BorderOutset = 6f;

    static readonly Color PillOn  = new Color(0.16f, 0.50f, 0.34f, 1f);
    static readonly Color PillOff = new Color(0.28f, 0.28f, 0.34f, 1f);

    float _prevTimeScale = 1f;
    bool  _surrendering;

    protected override void OnBeforeOpen()
    {
        // 현재 배속 저장 후 정지
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        _surrendering  = false;

        // 팝업은 풀에서 재사용되므로 매번 정리하고 다시 붙인다
        _resumeButton?.onClick.RemoveAllListeners();
        _resumeButton?.onClick.AddListener(() => Close());

        _reincarnateButton?.onClick.RemoveAllListeners();
        _reincarnateButton?.onClick.AddListener(OnReincarnateClicked);

        _sfxButton?.onClick.RemoveAllListeners();
        _sfxButton?.onClick.AddListener(ToggleSfx);

        _bgmButton?.onClick.RemoveAllListeners();
        _bgmButton?.onClick.AddListener(ToggleBgm);

        ApplyContext();
        RefreshSound();
    }

    protected override void OnAfterClose()
    {
        // 배속 복원 — 포기했으면 1× 로 되돌린다 (환생 팝업·로비를 배속으로 볼 이유가 없다)
        Time.timeScale = _surrendering ? 1f : _prevTimeScale;

        if (_surrendering)
            BattleManager.Instance?.Surrender();
    }

    // ── 문맥 (전투 중인가) ───────────────────────────────────

    /// <summary>
    /// 전투 중이 아니면 "즉시 환생하기" 행을 접고 패널을 그만큼 줄인다.
    ///
    /// 버튼들은 패널 위쪽에 고정돼 있으므로(pivot 상단) 높이만 줄이면
    /// 남은 항목의 자리는 그대로다.
    /// </summary>
    void ApplyContext()
    {
        // LobbyManager 가 없다 = 인게임 씬만 떠 있는 상태 → 전투로 본다
        //
        // ⚠ Flow == Battle 로 판정하지 말 것 (사용자 지적, 2026-09-10)
        //   이 게임의 런은 LobbyFlow.SummonRun 으로 돈다. Battle 은 원작 흐름의
        //   값이라 한 번도 켜지지 않아서, **런 내내 이 버튼이 감춰져 있었다.**
        //   판정의 정본은 LobbyManager.IsInRun 하나다 — 여기서 다시 적지 말 것.
        bool inBattle = LobbyManager.Instance == null || LobbyManager.Instance.IsInRun;

        _reincarnateButton?.gameObject.SetActive(inBattle);

        if (_panelRect == null || _panelFullH <= 0f) return;

        float h = inBattle ? _panelFullH : _panelFullH - _surrenderRowH;
        _panelRect.sizeDelta = new Vector2(_panelRect.sizeDelta.x, h);
        if (_borderRect != null)
            _borderRect.sizeDelta = new Vector2(_borderRect.sizeDelta.x, h + BorderOutset);
    }

    // ── 사운드 토글 ──────────────────────────────────────────

    void ToggleSfx()
    {
        var s = UserDataManager.Instance.Get<BattleSettingsData>();
        s.SetSfxOn(!s.SfxOn);
        UserDataManager.Instance.RequestSave();
        RefreshSound();
    }

    void ToggleBgm()
    {
        var s = UserDataManager.Instance.Get<BattleSettingsData>();
        s.SetBgmOn(!s.BgmOn);
        UserDataManager.Instance.RequestSave();
        RefreshSound();
    }

    void RefreshSound()
    {
        var s = UserDataManager.Instance.Get<BattleSettingsData>();
        SetPill(_sfxPill, _sfxState, s.SfxOn);
        SetPill(_bgmPill, _bgmState, s.BgmOn);
    }

    /// <summary>
    /// 상태 알약을 갱신한다.
    ///
    /// ⚠ 버튼 본체(Body) 색은 건드리지 않는다
    ///   Body 는 Button.targetGraphic 이라 눌림 색이 그 색에 곱해진다 —
    ///   여기서 바꾸면 EditorUIBuilder.TintFor 로 역산해 둔 눌림 색이 어긋난다.
    ///   그래서 상태는 버튼 위에 얹은 별도 이미지로 보여 준다 (TopBarUI 의 배속 띠와 같은 방식).
    /// </summary>
    static void SetPill(Image pill, TextMeshProUGUI label, bool on)
    {
        if (pill  != null) pill.color = on ? PillOn : PillOff;
        if (label != null) label.text = on ? "켜짐" : "꺼짐";
    }

    // ── 버튼 핸들러 ──────────────────────────────────────────

    /// <summary>
    /// 즉시 환생 — 이 팝업이 완전히 닫힌 **뒤에** 패배를 처리한다.
    /// 여기서 바로 Surrender() 를 부르면 환생 팝업이 열리는 도중에
    /// 이 팝업의 닫기 애니메이션이 겹쳐 두 팝업이 동시에 보인다.
    /// </summary>
    void OnReincarnateClicked()
    {
        _surrendering = true;
        Close();
    }
}
