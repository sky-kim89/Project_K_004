using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  EnemyInfoButtonUI.cs
//  상단 오른쪽 끝의 말풍선 버튼.
//
//  ■ 최종 역할은 "적 정보 팝업" 이다
//    이번 스테이지에 어떤 용사가 오는지 미리 보여 주는 창을 띄울 자리다.
//    그 팝업이 아직 없어서, 지금은 **스테이지 시작** 버튼으로 쓴다.
//
//    ⚠ 임시 용도다 — 팝업이 생기면 시작은 팝업 안의 "출전" 으로 옮기고
//      이 버튼은 본래 역할(정보 열기)만 남긴다.
//
//  ■ 대기 중에만 보인다
//    시작은 스테이지당 한 번뿐이다. 전에는 흐려지기만 해서, 판이 도는
//    내내 전장 오른쪽 한복판에 눌리지도 않는 버튼이 그대로 떠 있었다
//    ("게임 시작하고도 안 사라져"). 지금은 통째로 사라지고, 다음 스테이지
//    대기에 들어설 때 다시 나타난다.
//
//    ⚠ 이 컴포넌트는 버튼이 아니라 **StageStartRoot(부모)** 에 붙는다
//      자기가 붙은 오브젝트를 끄면 OnDisable 에서 구독을 놓아 버려
//      다시 켜 줄 사람이 없어진다. 루트는 늘 켜져 있다.
//
//  ■ 준비 시간이 다 되면 저절로 시작된다 (2026-08-28)
//    이 버튼은 그 시간을 **건너뛰는** 것이지 유일한 시작 수단이 아니다.
//    그래서 남은 시간을 여기에 띄운다 — 언제 적이 들어오는지 모르면
//    사전 소환을 얼마나 할지 판단할 수 없다.
//    (자동 시작 자체는 StageLoopDirector 가 돌린다)
//
//  ■ 남은 시간은 360° 고리로 읽힌다 (사용자 확정, 2026-08-28)
//    숫자는 읽어야 알지만 고리는 힐끗 봐도 안다 — 손이 카드 바에 가 있는
//    동안 곁눈으로 확인할 수 있어야 하는 정보다. 고리가 한 바퀴 닳아
//    사라지면 용사가 들어온다. 숫자는 그 안에 함께 두어 정확한 초를 받친다.
//
//  ■ 자리는 화면 오른쪽 끝 한가운데다
//    상단바에 두었을 때는 배속·일시정지와 한 줄에 섞여 "누르면 판이 시작되는"
//    버튼으로 보이지 않았다. 용사가 걸어 들어오는 쪽(오른쪽) 한복판에 두면
//    무엇을 여는 버튼인지가 위치로 읽힌다.
// ============================================================

public class EnemyInfoButtonUI : MonoBehaviour
{
    [Tooltip("스테이지 대기일 때만 켜지는 버튼. 이 컴포넌트가 붙은 오브젝트가 아니어야 한다.")]
    [SerializeField] Button     _button;

    [Tooltip("대기 중이 아닐 때 흐리게 덮는 오브젝트. 없으면 비워 둬도 된다.")]
    [SerializeField] GameObject _disabledOverlay;

    [Tooltip("남은 준비 시간(초)을 띄우는 텍스트. 없으면 비워 둬도 된다.")]
    [SerializeField] TextMeshProUGUI _countdownText;

    [Tooltip("남은 준비 시간을 360° 로 그리는 고리. Filled/Radial360 이어야 한다. 없으면 비워 둬도 된다.")]
    [SerializeField] Image _readyFill;

    [Tooltip("고리와 함께 켜고 끄는 배경 트랙. 없으면 비워 둬도 된다.")]
    [SerializeField] GameObject _readyTrack;

    /// <summary>남은 시간이 이 아래로 떨어지면 붉게 — 곧 들어온다는 뜻이다.</summary>
    const float UrgentSeconds = 5f;

    // ⚠ 숫자는 **흰 말풍선 안**에 앉는다 — 어두워야 읽힌다 (2026-09-03)
    //   여기 두 색이 원래 밝았다(0.86,0.92,1.00). Creator 는 프리팹에 어두운 색을
    //   넣어 뒀지만 Update 가 매 프레임 이 값으로 덮어써서, 흰 바탕에 흰 글자가
    //   되어 카운트다운이 통째로 안 보였다.
    //   ⚠ 색의 정본은 Creator 가 아니라 여기다. 말풍선 색을 바꾸면 이 둘을 볼 것.
    static readonly Color CountdownColor = new(0.05f, 0.05f, 0.09f);

    /// <summary>임박 — 흰 바탕에서도 붉게 읽히도록 밝기를 낮춘 빨강이다.</summary>
    static readonly Color UrgentColor    = new(0.78f, 0.09f, 0.09f);

    /// <summary>고리 색. 숫자보다 진하다 — 배경(전장) 위에 바로 얹히기 때문이다.</summary>
    static readonly Color RingColor       = new(0.46f, 0.70f, 1.00f, 0.95f);
    static readonly Color UrgentRingColor = new(1.00f, 0.36f, 0.32f, 0.98f);

    void Awake()
    {
        _button.onClick.RemoveListener(HandleClick);
        _button.onClick.AddListener(HandleClick);
    }

    void OnEnable()
    {
        StageLoopDirector.OnStageReady += HandleStageReady;
        StageLoopDirector.OnStageStart += HandleStageStart;

        Refresh();
    }

    void OnDisable()
    {
        StageLoopDirector.OnStageReady -= HandleStageReady;
        StageLoopDirector.OnStageStart -= HandleStageStart;
    }

    void HandleStageReady(int stage) => Refresh();
    void HandleStageStart(int stage) => Refresh();

    void Update()
    {
        var director = StageLoopDirector.Instance;
        bool ticking = director != null && director.HasReadyTimer;

        SetTimerVisible(ticking);
        if (!ticking) return;

        float left = director.ReadyRemaining;
        Color tint = left <= UrgentSeconds ? UrgentColor : CountdownColor;

        if (_readyFill != null)
        {
            // 한 바퀴에서 시작해 0 으로 닳는다. ReadyFill 이 남은 비율의 정본이다.
            _readyFill.fillAmount = director.ReadyFill;
            _readyFill.color      = left <= UrgentSeconds ? UrgentRingColor : RingColor;
        }

        if (_countdownText == null) return;

        // 올림이다 — 1.2 초 남았는데 "1" 로 뜨면 0 을 보기도 전에 시작된다.
        _countdownText.text  = Mathf.CeilToInt(left).ToString();
        _countdownText.color = tint;
    }

    /// <summary>준비 타이머 표시(숫자·고리·트랙)를 한꺼번에 켜고 끈다.</summary>
    void SetTimerVisible(bool on)
    {
        if (_countdownText != null && _countdownText.gameObject.activeSelf != on)
            _countdownText.gameObject.SetActive(on);

        if (_readyFill != null && _readyFill.gameObject.activeSelf != on)
            _readyFill.gameObject.SetActive(on);

        if (_readyTrack != null && _readyTrack.activeSelf != on)
            _readyTrack.SetActive(on);
    }

    void Refresh()
    {
        bool ready = StageLoopDirector.Instance != null
                  && StageLoopDirector.Instance.IsStageReady;

        _button.interactable = ready;

        // 대기가 아니면 버튼 자체를 내린다 — 흐리게만 두면 판이 도는 내내
        // 전장 한복판을 가린다. 다음 대기에서 Refresh 가 다시 켠다.
        if (_button.gameObject.activeSelf != ready)
            _button.gameObject.SetActive(ready);

        if (_disabledOverlay != null) _disabledOverlay.SetActive(!ready);

        SetTimerVisible(ready);
    }

    /// <summary>
    /// 지금은 스테이지를 시작한다.
    /// 적 정보 팝업이 생기면 여기서 그 팝업을 열고, 시작은 팝업 쪽으로 옮긴다.
    /// </summary>
    void HandleClick()
    {
        var director = StageLoopDirector.Instance;
        if (director == null)
        {
            Debug.LogError("[EnemyInfoButtonUI] StageLoopDirector 가 씬에 없습니다.");
            return;
        }

        director.StartStage();
        Refresh();
    }
}
