using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  FacilityPopup.cs
//  시설 화면 — 야영지 · 강화소 · 제단 · 상점이 한 프리팹을 나눠 쓴다.
//
//  ■ ChoicePopup 과 무엇이 다른가 (사용자 확정, 2026-09-06)
//    ChoicePopup 은 "글자 목록" 이다. 시설은 전투가 멈추는 유일한 자리라
//    그 화면이 장부처럼 보이면 갈림길을 고른 보람이 사라진다.
//    여기는 **배경 그림 + 한 토막의 이야기 + 선택지** 로 세운다.
//
//    ⚠ 시설마다 프리팹을 따로 굽지 않는다
//      다른 것은 배경 그림과 글 두 줄뿐이고 구조는 같다. 넷으로 나누면
//      한 줄을 고칠 때마다 네 번 구워야 한다. 배경은 RunNodeKind 로 갈아
//      끼우고(_nodeArt), 글은 RunNodeFlavor 가 준다.
//
//  ■ 선택지 수는 가변이다
//    상점은 하나, 야영지는 둘, 강화소·제단은 덱 칸 수만큼 온다.
//    남는 줄은 끈다 — ChoicePopup 과 같은 방식이다.
//
//  ■ ⚠ 흐름을 두 번 이어 가지 않는다
//    "골랐다" 와 "그냥 닫았다" 가 둘 다 일어날 수 있다. 막지 않으면
//    스테이지가 두 칸 넘어간다 — 그 방어는 부르는 쪽(RunNodeFlow)이 한다.
// ============================================================

public class FacilityPopup : PopupBase
{
    [Header("배경")]
    [SerializeField] Image _art;

    [Tooltip("갈림길·시설 그림. ⚠ RunNodeRule.AllKinds 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _nodeArt;

    [Tooltip("이벤트 타이틀 그림. ⚠ RunEventId 번호 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _eventArt;

    [Header("글")]
    [SerializeField] TextMeshProUGUI _titleText;

    [Tooltip("오른쪽 위 보유 골드 숫자. 아이콘은 프리팹이 이미 달고 있다.\n" +
             "⚠ 제목 줄에 골드를 적지 않는다 — 지갑은 늘 같은 자리다 (UI 규칙 7).")]
    [SerializeField] TextMeshProUGUI _purseValue;

    [Tooltip("한 토막의 이야기. 규칙 설명이 아니다 — RunNodeFlavor 가 준다.")]
    [SerializeField] TextMeshProUGUI _flavorText;

    [Header("선택지")]
    [SerializeField] RowView[] _rows;

    [Header("닫기")]
    [SerializeField] Button _closeBtn;

    [Serializable]
    public class RowView
    {
        public GameObject      Root;
        public Button          Button;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI DescText;
    }

    /// <summary>화면이 담을 수 있는 최대 줄 수. 넘치면 스크롤 대신 잘린다.</summary>
    public const int MaxRows = 8;

    Action<int> _onPicked;
    Action      _onClose;
    bool        _blockClose;

    /// <summary>
    /// 고른 뒤에도 창을 닫지 않는다 — <b>같은 창을 다음 화면으로 바꿔 쓰는</b> 쪽이 켠다.
    ///
    /// ⚠ 왜 필요한가 — 닫고 다시 여는 길이 막혀 있다 (2026-09-08)
    ///   Choose 는 Close() 를 부르고 **곧바로** 콜백을 부른다. 그 콜백이 같은
    ///   PopupType 을 다시 열면, 닫기 코루틴이 아직 IsOpen 을 내리기 전이라
    ///   PopupManager 가 "이미 열려 있음" 으로 보고 한 번 더 닫은 뒤 **새 인스턴스를
    ///   하나 더 만든다.** 경고가 찍히고 같은 프리팹이 두 벌 뜬다.
    ///   창을 그대로 두고 Setup 을 다시 부르면 그 전부가 사라진다.
    /// </summary>
    bool _keepOpenOnPick;

    public override bool BlockBackgroundClose => _blockClose;

    protected override void Awake()
    {
        base.Awake();

        for (int i = 0; i < _rows.Length; i++)
        {
            int index = i;   // ⚠ 캡처 — 루프 변수를 넘기면 전부 마지막 값이 된다
            _rows[i].Button.onClick.AddListener(() => Choose(index));
        }

        _closeBtn?.onClick.AddListener(() => Close());
    }

    /// <summary>
    /// 시설 화면을 세운다.
    /// </summary>
    /// <param name="kind">배경 그림과 이야기를 고르는 기준.</param>
    /// <param name="stageNumber">이야기 줄을 고르는 시드. 같은 판이면 같은 글이다.</param>
    /// <param name="eventId">
    /// 이벤트면 그 번호. 주면 <c>_eventArt</c> 에서 그림을 고른다.
    ///
    /// ⚠ 이벤트는 열다섯이 각자 그림을 갖는다 — <c>_nodeArt</c> 로는 못 고른다
    ///   그쪽은 갈림길 <b>칸</b>의 그림이라 이벤트 몫이 한 장뿐이다. 그대로 두면
    ///   열다섯 이벤트가 전부 같은 배경이라 "어디에 들렀는지" 가 화면에 안 남는다.
    /// </param>
    /// <param name="flavor">
    /// 이야기 줄을 직접 준다. 비우면 <see cref="RunNodeFlavor"/> 가 고른다.
    ///
    /// ⚠ 이벤트가 이 자리를 쓴다 — 시설은 "그 자리가 어떤 곳인가" 라 종류마다
    ///   고정된 글이면 되지만, 이벤트는 **무슨 일이 일어났는가** 라서 매번 다르다.
    ///   RunNodeFlavor 에 Event 줄을 넣어 두면 여덟 이벤트가 같은 글을 쓴다.
    /// </param>
    /// <param name="keepOpenOnPick">
    /// 고른 뒤에도 창을 닫지 않는다. 이벤트가 쓴다 — 선택 화면을 그 자리에서
    /// 결과 화면으로 바꾼다 (<see cref="_keepOpenOnPick"/> 주석 참고).
    /// </param>
    public FacilityPopup Setup(RunNodeKind kind, int stageNumber, string title,
                               IReadOnlyList<ChoicePopup.Entry> entries,
                               Action<int> onPicked, bool blockClose,
                               string flavor = null, bool keepOpenOnPick = false,
                               RunEventId? eventId = null)
    {
        _onPicked       = onPicked;
        _blockClose     = blockClose;
        _keepOpenOnPick = keepOpenOnPick;

        _titleText.text  = title;

        // 지갑은 늘 같은 자리에서 같은 모양으로 — 시설 넷이 같은 규칙을 쓴다.
        if (_purseValue != null) _purseValue.text = $"{RunGoldRule.Current:N0}";
        _flavorText.text = string.IsNullOrEmpty(flavor)
                         ? RunNodeFlavor.Of(kind, stageNumber)
                         : flavor;

        Sprite art = eventId.HasValue ? EventArt(eventId.Value) : NodeArt(kind);

        _art.sprite  = art;
        _art.enabled = art != null;

        for (int i = 0; i < _rows.Length; i++)
        {
            bool has = i < entries.Count;
            _rows[i].Root.SetActive(has);
            if (!has) continue;

            ChoicePopup.Entry e = entries[i];

            _rows[i].NameText.text    = e.Name;
            _rows[i].DescText.text    = e.Desc;
            _rows[i].Button.interactable = e.Enabled;

            // 고를 수 없는 줄은 글까지 흐리게 — 버튼만 죽이면 왜 못 고르는지
            // 알 수 없다 (ChoicePopup 과 같은 규칙).
            Color c = e.Enabled ? ActiveText : DimText;
            _rows[i].NameText.color = c;
            _rows[i].DescText.color = e.Enabled ? SubText : DimText;
        }

        return this;
    }

    Sprite NodeArt(RunNodeKind kind)
    {
        int at = RunNodeRule.IndexOf(kind);
        return _nodeArt != null && at >= 0 && at < _nodeArt.Length ? _nodeArt[at] : null;
    }

    Sprite EventArt(RunEventId id)
    {
        int at = (int)id;
        return _eventArt != null && at >= 0 && at < _eventArt.Length ? _eventArt[at] : null;
    }

    static readonly Color ActiveText = new Color(0.94f, 0.96f, 1.00f);
    static readonly Color SubText    = new Color(0.70f, 0.75f, 0.88f);
    static readonly Color DimText    = new Color(0.38f, 0.41f, 0.52f);

    /// <summary>그냥 닫았을 때 부를 것. 닫을 수 있는 화면에서만 쓴다.</summary>
    public void SetOnClose(Action onClose) => _onClose = onClose;

    void Choose(int index)
    {
        Action<int> callback = _onPicked;

        // ⚠ 콜백보다 먼저 비운다 — 콜백이 다음 시설 화면을 여는 경우가 있다
        //   (강화소: 카드 고르기 → 무엇을 새길까). 같은 팝업이 재사용되므로
        //   비우지 않으면 다음 화면의 선택이 이전 콜백으로 샌다.
        _onPicked = null;
        _onClose  = null;

        // ⚠ 닫지 않는 모드에서는 콜백이 Setup 을 다시 불러 창을 갈아 끼운다.
        //   여기서 닫아 버리면 그 Setup 이 닫히는 창을 꾸미게 된다.
        if (!_keepOpenOnPick) Close();

        callback?.Invoke(index);
    }

    protected override void OnAfterClose()
    {
        Action onClose = _onClose;
        _onClose  = null;
        _onPicked = null;

        onClose?.Invoke();
    }
}
