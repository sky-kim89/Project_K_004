using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CrossroadUI.cs
//  갈림길 — 다음 판이 무엇이 될지 고르는 **인게임 화면**.
//
//  ■ 팝업이 아니다 (사용자 확정, 2026-09-06)
//    한때 ChoicePopup(가운데 목록 창)이었다. 그러면 이 선택이 "창을 하나 더
//    닫는 일" 로 읽힌다. 갈림길은 전장 위에서 **길이 갈리는 그림**이어야
//    한다 — 그래서 전장을 가리지 않고 오른쪽에 두 갈래를 띄운다.
//      위쪽 칸 / 아래쪽 칸 — 화면의 오른쪽 위·아래에 하나씩.
//    용사가 걸어 들어오는 쪽이 오른쪽이므로, 그쪽에 길이 갈려 보이는 것이
//    "다음에 무엇이 오는가" 와 방향이 맞는다.
//
//  ■ ⚠ 시계가 없다
//    스테이지 자동 시작 카운트다운도 함께 걷어냈다
//    (StageLoopDirector._readySeconds = 0). 갈림길은 이 런의 방향을 정하는
//    자리라 충분히 들여다볼 수 있어야 한다.
//
//  ■ ⚠ 닫을 수 없다
//    닫으면 다음 판이 정해지지 않은 상태가 된다. 배경을 눌러도 아무 일이
//    없고, 둘 중 하나를 골라야만 사라진다.
//
//  ■ 시설을 고르면 그다음은 전용 화면이 받는다
//    여기서는 "어디로 갈까" 만 정한다. 야영지·제단에서 무엇을 할지는
//    RunNodeFlow 가 시설 화면으로 넘긴다.
// ============================================================

public class CrossroadUI : MonoBehaviour
{
    public static CrossroadUI Instance { get; private set; }

    [Tooltip("갈림길이 떠 있는 동안만 켜지는 자식. ⚠ 이 컴포넌트가 붙은 뿌리가 아니어야 한다.")]
    [SerializeField] GameObject _content;

    [Tooltip("전장 위에 얇게 까는 면. 눌러도 아무 일이 없다 — 고르기 전에는 못 닫는다.")]
    [SerializeField] GameObject _blocker;

    [Tooltip("두 갈래. 0 = 위쪽, 1 = 아래쪽.")]
    [SerializeField] NodeView[] _nodes;

    [Serializable]
    public class NodeView
    {
        public GameObject      Root;
        public Button          Button;
        public Image           Art;        // 시설·전투를 알아보는 그림
        public Image           Frame;      // 종류별 색 — 전투는 붉고 시설은 푸르다
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI DescText;
    }

    [Tooltip("갈림길 그림. ⚠ RunNodeRule.AllKinds 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _nodeArt;

    Action<RunNodeKind> _onPicked;

    readonly List<RunNodeKind> _shown = new(2);

    void Awake()
    {
        Instance = this;

        for (int i = 0; i < _nodes.Length; i++)
        {
            int index = i;   // ⚠ 캡처 — 루프 변수를 그대로 넘기면 전부 마지막 값이 된다
            _nodes[i].Button.onClick.AddListener(() => Pick(index));
        }

        _content.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── 열기 ─────────────────────────────────────────────────

    /// <summary>두 갈래를 띄운다. 하나를 고르면 onPicked 가 불리고 화면이 닫힌다.</summary>
    public void Show(List<RunNodeKind> nodes, Action<RunNodeKind> onPicked)
    {
        _onPicked = onPicked;

        _shown.Clear();
        _shown.AddRange(nodes);

        for (int i = 0; i < _nodes.Length; i++)
        {
            bool has = i < _shown.Count;
            _nodes[i].Root.SetActive(has);
            if (!has) continue;

            Bind(_nodes[i], _shown[i]);
        }

        _blocker.SetActive(true);
        _content.SetActive(true);
    }

    void Bind(NodeView view, RunNodeKind kind)
    {
        view.NameText.text = kind.ToKorean();
        // ⚠ 값이 스테이지를 따라 오른다 (RunGoldRule.Price) — 스테이지를 넘겨야
        //   카드에 적히는 골드가 실제로 낼 값과 같다.
        view.DescText.text = kind.Describe(
            StageLoopDirector.Instance != null ? StageLoopDirector.Instance.StageNumber : 1);

        int index = RunNodeRule.IndexOf(kind);
        Sprite art = (_nodeArt != null && index >= 0 && index < _nodeArt.Length)
                   ? _nodeArt[index] : null;

        view.Art.sprite  = art;
        view.Art.enabled = art != null;

        // 전투와 시설을 색으로 가른다 — 그림을 다 읽기 전에 성격이 먼저 보인다.
        view.Frame.color = kind.IsBattle() ? BattleColor : FacilityColor;
    }

    static readonly Color BattleColor   = new Color(0.86f, 0.32f, 0.30f);
    static readonly Color FacilityColor = new Color(0.34f, 0.68f, 0.92f);

    // ── 고르기 ───────────────────────────────────────────────

    void Pick(int index)
    {
        if (index >= _shown.Count) return;

        RunNodeKind picked = _shown[index];

        // ⚠ 콜백보다 먼저 닫는다 — 콜백이 시설 화면을 여는 경우가 있다
        Close();

        Action<RunNodeKind> callback = _onPicked;
        _onPicked = null;
        callback?.Invoke(picked);
    }

    void Close()
    {
        _content.SetActive(false);
        _blocker.SetActive(false);
    }
}
