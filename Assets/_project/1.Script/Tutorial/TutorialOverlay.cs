using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  TutorialOverlay.cs
//  튜토리얼 전용 UI 팔레트. TutorialManager 가 소유하며 화면 최상단에 뜬다.
//
//  ■ 구성 (전부 런타임 생성 — 프리팹 의존 없음)
//    TutorialCanvas (sortingOrder 1000)
//      ├ Dim_T / Dim_B / Dim_L / Dim_R  — 하이라이트 구멍 바깥만 덮는 네 장
//      ├ Blocker                        — 화면 전체 입력 차단 (구멍만 통과)
//      ├ Frame                          — 구멍 테두리 네 줄
//      ├ Bubble                         — 말풍선 (배경 + 본문 + 안내)
//      └ SkipBtn                        — 건너뛰기 (항상 오른쪽 위)
//
//  ⚠ 건너뛰기 버튼은 Blocker 보다 뒤 형제여야 한다
//    Blocker 는 화면 전체를 먹는 판이다. 앞 형제로 두면 레이캐스트가 Blocker 에
//    먼저 걸려 눌리지 않는다 — "빠져나갈 수 없는 튜토리얼" 이 된다.
//    그래서 Build() 의 맨 마지막에 만든다.
//
//  ■ 왜 딤을 네 장으로 나누나
//    한 장으로 덮으면 하이라이트 대상까지 같이 어두워져 "가리키는 중" 이
//    안 읽힌다. 기본 UI 셰이더로 구멍을 뚫으려면 스텐실이나 전용 셰이더가
//    필요한데, 사각형 네 장이면 셰이더 없이 같은 그림이 나온다.
//
//  ■ 입력 차단은 딤이 아니라 Blocker 가 한다
//    딤은 보여주는 역할만 맡는다. 차단을 딤에 맡기면 구멍의 네 모서리
//    바깥쪽에 판정이 겹치거나 비는 자리가 생긴다.
//    Blocker 는 화면 전체를 덮되 ICanvasRaycastFilter 로 구멍 안만
//    "내 것이 아니다" 라고 답해 그 아래 버튼이 눌리게 둔다.
//
//  ⚠ 폰트에 없는 글리프를 쓰지 않는다 (UI 규칙 2)
//    안내 문구에 ▶ ✔ 같은 기호를 쓰면 두부(□)로 나온다.
// ============================================================

public class TutorialOverlay : MonoBehaviour
{
    // ── 치수 ─────────────────────────────────────────────────
    const int   SortingOrder = 1000;   // 로비 0 · 인게임 10 · 팝업 200 보다 위
    const float BubbleMaxW   = 900f;

    // ⚠ 자리가 좁으면 말풍선을 이만큼까지 줄인다
    //   최대 폭을 고집하면 "양옆 어디에도 900 이 안 들어감 → 화면 중앙" 으로 떨어져
    //   가리키던 대상을 그대로 덮는다 (LayoutBubble 주석 참고).
    //   FontMd(42) 기준 본문 폭 456 ≈ 한 줄 10자 — 이보다 좁으면 읽기가 더 나빠지므로
    //   그때는 차라리 중앙에 두고 겹치는 쪽을 택한다.
    const float BubbleMinW   = 520f;
    const float BubblePad    = 32f;
    const float BubbleGap    = 28f;    // 타겟과 말풍선 사이 간격
    const float FrameThick   = 4f;
    const float ScreenMargin = 24f;
    const float SkipW        = 236f;
    const float SkipDepth    = 6f;     // 그림자로 드러나는 두께 (UI 규칙 1)

    // ── 색상 ─────────────────────────────────────────────────
    static readonly Color DimColor    = new(0f,     0f,     0f,     0.72f);
    static readonly Color FrameColor  = new(1f,     0.86f,  0.38f,  1f);
    static readonly Color BubbleBg    = new(0.07f,  0.085f, 0.15f,  0.98f);
    static readonly Color BubbleEdge  = new(0.40f,  0.72f,  1.00f,  1f);
    static readonly Color BodyColor   = new(0.94f,  0.96f,  1.00f,  1f);
    static readonly Color HintColor   = new(0.62f,  0.70f,  0.86f,  1f);
    static readonly Color SkipFace    = new(0.24f,  0.26f,  0.34f,  1f);
    static readonly Color SkipShadow  = new(0.06f,  0.07f,  0.10f,  1f);
    static readonly Color SkipTop     = new(1f,     1f,     1f,     0.16f);
    static readonly Color SkipBottom  = new(0f,     0f,     0f,     0.28f);
    static readonly Color SkipLabel   = new(0.78f,  0.82f,  0.90f,  1f);

    // ── 구성 요소 ────────────────────────────────────────────
    Canvas          _canvas;
    RectTransform   _root;
    TutorialBlocker _blocker;
    RectTransform[] _dims  = new RectTransform[4];
    RectTransform[] _frame = new RectTransform[4];
    RectTransform   _bubble;
    RectTransform   _bubbleEdge;
    TextMeshProUGUI _body;
    TextMeshProUGUI _hint;

    RectTransform _skipRoot;

    Action _onAnyClick;
    Action _onSkip;

    // ── 생성 ─────────────────────────────────────────────────

    /// <summary>
    /// 팔레트를 만든다. onSkip 은 건너뛰기 버튼이 부를 콜백
    /// (TutorialManager.Skip — 완료로 기록해 다시 뜨지 않게 한다).
    /// </summary>
    public static TutorialOverlay Create(Transform parent, Action onSkip)
    {
        var go = new GameObject("TutorialOverlay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var overlay = go.AddComponent<TutorialOverlay>();
        overlay._onSkip = onSkip;
        overlay.Build();
        return overlay;
    }

    void Build()
    {
        // ── 전용 캔버스 ──────────────────────────────────────
        //  팝업 캔버스를 빌려 쓰지 않는다 — 팝업이 닫히면 같이 사라지고,
        //  팝업 위를 덮어야 하는데 같은 캔버스 안에서는 순서 싸움이 된다.
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        _root = GetComponent<RectTransform>();

        // ── 딤 네 장 (구멍 바깥) ─────────────────────────────
        for (int i = 0; i < 4; i++) _dims[i] = MakeQuad($"Dim_{i}", DimColor, raycast: false);

        // ── 입력 차단 ────────────────────────────────────────
        var blockerGo = new GameObject("Blocker", typeof(RectTransform));
        blockerGo.transform.SetParent(_root, false);
        _blocker = blockerGo.AddComponent<TutorialBlocker>();
        _blocker.color         = new Color(0f, 0f, 0f, 0f);   // 보이지 않지만 판정은 있다
        _blocker.raycastTarget = true;
        Stretch(blockerGo.GetComponent<RectTransform>());

        var btn = blockerGo.AddComponent<Button>();
        btn.transition    = Selectable.Transition.None;
        btn.targetGraphic = _blocker;
        btn.onClick.AddListener(() => _onAnyClick?.Invoke());

        // ── 하이라이트 테두리 ────────────────────────────────
        for (int i = 0; i < 4; i++) _frame[i] = MakeQuad($"Frame_{i}", FrameColor, raycast: false);

        // ── 말풍선 ───────────────────────────────────────────
        BuildBubble();

        // ── 건너뛰기 ─────────────────────────────────────────
        //  반드시 마지막이다 — Blocker 뒤 형제라야 클릭이 여기로 온다.
        BuildSkipButton();

        SetActive(false);
    }

    void BuildBubble()
    {
        var go = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_root, false);
        _bubble = go.GetComponent<RectTransform>();
        _bubble.pivot = new Vector2(0.5f, 0.5f);

        var bg = go.GetComponent<Image>();
        bg.color         = BubbleBg;
        bg.raycastTarget = false;

        // ⚠ 테두리는 앞 형제로 깐다 (UI 규칙 3)
        //   자식으로 두면 부모 Image 보다 뒤로 못 가서 본문을 덮는다.
        var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        edge.transform.SetParent(_root, false);
        edge.transform.SetSiblingIndex(_bubble.GetSiblingIndex());
        var edgeImg = edge.GetComponent<Image>();
        edgeImg.color         = BubbleEdge;
        edgeImg.raycastTarget = false;
        _bubbleEdge = edge.GetComponent<RectTransform>();

        _body = MakeText(go, "Body", UIScale.FontMd, BodyColor, FontStyles.Normal);
        _body.alignment = TextAlignmentOptions.TopLeft;
        var brt = _body.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(BubblePad, BubblePad + UIScale.RowSm);
        brt.offsetMax = new Vector2(-BubblePad, -BubblePad);

        _hint = MakeText(go, "Hint", UIScale.FontSm, HintColor, FontStyles.Normal);
        _hint.alignment = TextAlignmentOptions.MidlineRight;
        var hrt = _hint.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f);
        hrt.offsetMin = new Vector2(BubblePad, BubblePad * 0.5f);
        hrt.offsetMax = new Vector2(-BubblePad, BubblePad * 0.5f + UIScale.RowSm);
    }

    /// <summary>
    /// 오른쪽 위 건너뛰기 버튼.
    ///
    /// ⚠ EditorUIBuilder.RaisedBtn 을 못 쓴다 — 그쪽은 에디터 전용 스크립트다
    ///   이 팔레트는 런타임에 통째로 만들어지므로 음각 구조(그림자·본체·모서리)를
    ///   여기서 직접 쌓는다. 모양 규칙은 UI 규칙 1 과 같다.
    ///
    /// ⚠ 라벨은 Body 아래에 넣는다
    ///   루트에 넣으면 눌려도 같이 안 내려가서 눌린 느낌이 사라진다.
    ///
    /// ⚠ 눌림 색은 targetGraphic 색에 곱해진다
    ///   normalColor 를 흰색으로 두면 Body 의 face 색이 그대로 나오고,
    ///   pressedColor 만 어둡게 잡으면 누를 때만 눌린 색이 된다.
    /// </summary>
    void BuildSkipButton()
    {
        float h = UIScale.BtnFor(UIScale.FontSm);   // 라벨이 안 눌리는 최소 높이 (UI 규칙 5)

        var rootGo = new GameObject("SkipBtn", typeof(RectTransform));
        rootGo.transform.SetParent(_root, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        _skipRoot = rootRt;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot     = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);
        rootRt.sizeDelta        = new Vector2(SkipW, h + SkipDepth);

        // 그림자 — 아래로 내려가 두께로 보인다
        var shadow = MakeChildQuad(rootGo, "Shadow", SkipShadow);
        Stretch(shadow);
        shadow.offsetMin = new Vector2(0f, 0f);
        shadow.offsetMax = new Vector2(0f, -SkipDepth);

        // 본체
        var bodyGo  = new GameObject("Body", typeof(RectTransform), typeof(Image));
        bodyGo.transform.SetParent(rootGo.transform, false);
        var bodyImg = bodyGo.GetComponent<Image>();
        bodyImg.color = SkipFace;
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        Stretch(bodyRt);
        bodyRt.offsetMin = new Vector2(0f, SkipDepth);
        bodyRt.offsetMax = Vector2.zero;

        // 모서리 — 위는 밝게 2px, 아래는 어둡게 4px
        var top = MakeChildQuad(bodyGo, "TopEdge", SkipTop);
        top.anchorMin = new Vector2(0f, 1f); top.anchorMax = Vector2.one;
        top.pivot     = new Vector2(0.5f, 1f);
        top.offsetMin = Vector2.zero; top.offsetMax = Vector2.zero;
        top.sizeDelta = new Vector2(0f, 2f);

        var bottom = MakeChildQuad(bodyGo, "BottomEdge", SkipBottom);
        bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1f, 0f);
        bottom.pivot     = new Vector2(0.5f, 0f);
        bottom.offsetMin = Vector2.zero; bottom.offsetMax = Vector2.zero;
        bottom.sizeDelta = new Vector2(0f, 4f);

        var label = MakeText(bodyGo, "Label", UIScale.FontSm, SkipLabel, FontStyles.Bold);
        label.text      = "건너뛰기";
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform);

        var skipBtn = rootGo.AddComponent<Button>();
        skipBtn.targetGraphic = bodyImg;
        var colors = skipBtn.colors;
        colors.normalColor      = Color.white;          // face 색을 그대로 통과시킨다
        colors.highlightedColor = Color.white;
        colors.pressedColor     = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor    = Color.white;
        skipBtn.colors          = colors;
        skipBtn.onClick.AddListener(() => _onSkip?.Invoke());
    }

    // ── 표시 ─────────────────────────────────────────────────

    public void SetActive(bool on) => gameObject.SetActive(on);

    /// <summary>
    /// 건너뛰기 버튼 노출. 강제 진행 튜토리얼에서는 끈다.
    ///
    /// ⚠ 버튼 자리가 배속·오토 버튼 자리와 겹친다
    ///   화면 오른쪽 위에 상주하는데, 인게임 시나리오는 바로 그 두 버튼을
    ///   가리키며 설명한다. 켜 둔 채로는 설명 대상이 버튼에 가린다.
    /// </summary>
    public void SetSkipVisible(bool on)
    {
        if (_skipRoot != null) _skipRoot.gameObject.SetActive(on);
    }

    /// <summary>가장 위로 끌어올린다 — 뒤늦게 만들어진 캔버스에 밀리지 않게.</summary>
    public void BringToFront() => _canvas.sortingOrder = SortingOrder;

    /// <summary>
    /// 스텝 하나를 화면에 올린다.
    /// onAnyClick 은 화면(구멍 바깥) 클릭 콜백 — 넘어갈 필요가 없으면 null.
    /// </summary>
    public void Show(TutorialStep step, RectTransform target, string hint, Action onAnyClick)
    {
        _onAnyClick = onAnyClick;
        SetActive(true);

        Rect hole = target != null ? WorldRectOf(target, step.Padding) : Rect.zero;
        bool hasHole = target != null && hole.width > 1f && hole.height > 1f;

        // 타겟을 직접 눌러야 하는 스텝만 구멍으로 입력을 흘린다.
        // 설명 스텝에서 구멍을 열어 두면 아무 버튼이나 눌러 시나리오가 어긋난다.
        bool passThrough = hasHole && step.Advance == TutorialAdvance.ClickTarget;
        _blocker.SetHole(passThrough ? hole : (Rect?)null);

        LayoutDim(hasHole ? hole : (Rect?)null);
        LayoutFrame(hasHole ? hole : (Rect?)null);
        LayoutBubble(step, hasHole ? hole : (Rect?)null);

        _body.text = step.Message ?? "";
        _hint.text = hint ?? "";
        bool showBubble = !string.IsNullOrEmpty(step.Message) || !string.IsNullOrEmpty(hint);
        _bubble.gameObject.SetActive(showBubble);
        _bubbleEdge.gameObject.SetActive(showBubble);
    }

    /// <summary>
    /// 스텝 사이 — 아무것도 안 보여주고 입력만 막는다.
    ///
    /// dim=false 면 어둡게 하지 않는다.
    /// ⚠ 기다리는 구간을 어둡게 덮으면 안 된다
    ///   쿨타임처럼 몇 초씩 기다리는 스텝에서 화면을 깔아 두면, 정작 봐야 할
    ///   전투가 안 보이는 채로 "설명도 없이 어두운 화면" 만 남는다.
    ///   막는 것과 가리는 것은 다른 일이다.
    /// </summary>
    public void ShowBlockOnly(bool dim = true)
    {
        _onAnyClick = null;
        SetActive(true);
        _blocker.SetHole(null);
        LayoutDim(dim ? (Rect?)null : FullScreenHole);
        LayoutFrame(null);
        _bubble.gameObject.SetActive(false);
        _bubbleEdge.gameObject.SetActive(false);
    }

    /// <summary>화면 전체가 구멍 = 딤이 한 장도 안 그려진다 (테두리는 따로 끈다).</summary>
    Rect FullScreenHole => _root.rect;

    public void Hide()
    {
        _onAnyClick = null;
        _blocker.SetHole(null);
        SetActive(false);
    }

    // ── 레이아웃 ─────────────────────────────────────────────

    void LayoutDim(Rect? hole)
    {
        Rect full = _root.rect;
        if (hole == null)
        {
            SetRect(_dims[0], full);
            for (int i = 1; i < 4; i++) SetRect(_dims[i], Rect.zero);
            return;
        }

        Rect h = hole.Value;
        float xMin = full.xMin, xMax = full.xMax, yMin = full.yMin, yMax = full.yMax;

        SetRect(_dims[0], Rect.MinMaxRect(xMin, h.yMax, xMax, yMax));   // 위
        SetRect(_dims[1], Rect.MinMaxRect(xMin, yMin, xMax, h.yMin));   // 아래
        SetRect(_dims[2], Rect.MinMaxRect(xMin, h.yMin, h.xMin, h.yMax)); // 좌
        SetRect(_dims[3], Rect.MinMaxRect(h.xMax, h.yMin, xMax, h.yMax)); // 우
    }

    void LayoutFrame(Rect? hole)
    {
        if (hole == null)
        {
            for (int i = 0; i < 4; i++) SetRect(_frame[i], Rect.zero);
            return;
        }

        Rect h = hole.Value;
        float t = FrameThick;
        SetRect(_frame[0], Rect.MinMaxRect(h.xMin - t, h.yMax,     h.xMax + t, h.yMax + t));
        SetRect(_frame[1], Rect.MinMaxRect(h.xMin - t, h.yMin - t, h.xMax + t, h.yMin));
        SetRect(_frame[2], Rect.MinMaxRect(h.xMin - t, h.yMin,     h.xMin,     h.yMax));
        SetRect(_frame[3], Rect.MinMaxRect(h.xMax,     h.yMin,     h.xMax + t, h.yMax));
    }

    /// <summary>
    /// 말풍선 자리를 잡는다.
    ///
    /// ⚠ 폭을 고정해 두면 가운데 열을 가리킬 때 대상을 덮는다
    ///   장수 상세의 스탯 열처럼 **세로로 길고 화면 가운데 있는** 타겟은
    ///   위·아래에 자리가 없고, 좌우에 남는 폭도 BubbleMaxW(900)보다 좁다.
    ///   그러면 TryPlaceBeside 가 전부 실패해 화면 중앙으로 떨어지고,
    ///   가리키던 스탯 목록을 설명 글이 그대로 가렸다. (2026-08-26)
    ///
    ///   실제 수치 — StatColumn 좌우 여유 646 / 786, 필요 폭 952.
    ///   오른쪽에 786 이 남아 있는데 900 을 고집하다 못 쓴 것이다.
    ///
    /// 그래서 최대 폭으로 실패하면 **남는 쪽 폭에 맞춰 줄여** 다시 시도한다.
    /// BubbleMinW 보다도 좁으면 그때는 중앙에 둔다 — 읽을 수 없는 폭보다는 겹치는 편이 낫다.
    /// </summary>
    void LayoutBubble(TutorialStep step, Rect? hole)
    {
        _body.text = step.Message ?? "";

        Rect  full = _root.rect;
        float maxW = Mathf.Min(BubbleMaxW, full.width - ScreenMargin * 2f);

        var anchor = step.Anchor;
        if (hole == null) anchor = TutorialAnchor.Center;

        float   w = maxW;
        float   h = MeasureHeight(step.Message, w);
        Vector2 center;

        if (anchor == TutorialAnchor.Center)
        {
            center = full.center;
        }
        else if (!TryPlaceBeside(full, hole.Value, w, h, anchor, out center))
        {
            // 좌우 중 넓은 쪽에 맞춰 줄여 본다.
            // (세로 여유는 폭을 줄이면 오히려 나빠진다 — 줄바꿈이 늘어 h 가 커진다)
            float side = Mathf.Max(hole.Value.xMin - full.xMin,
                                   full.xMax - hole.Value.xMax) - BubbleGap - ScreenMargin;

            if (side >= BubbleMinW)
            {
                // ⚠ 1px 뺀다 — 딱 맞추면 부동소수점에 걸려 다시 실패한다
                //   needH = w + Gap + Margin 이므로 w = side 로 두면
                //   needH 가 남는 폭과 '정확히' 같아진다. + 와 - 를 거친 값이라
                //   마지막 자리가 어긋나면 조건이 거짓이 되고 그대로 중앙으로 떨어진다.
                w = Mathf.Min(w, side) - 1f;
                h = MeasureHeight(step.Message, w);
                TryPlaceBeside(full, hole.Value, w, h, anchor, out center);
            }
            else
            {
                center = full.center;
            }
        }

        var rect = new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
        SetRect(_bubble, rect);
        SetRect(_bubbleEdge, new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f));
    }

    /// <summary>본문 높이를 먼저 재고 그만큼만 칸을 잡는다 — 고정 높이는 글자를 자른다.</summary>
    float MeasureHeight(string message, float w)
    {
        float textH = string.IsNullOrEmpty(message)
            ? 0f
            : _body.GetPreferredValues(message, w - BubblePad * 2f, 0f).y;
        return textH + BubblePad * 2f + UIScale.RowSm;
    }


    /// <summary>
    /// 말풍선을 타겟 <b>옆</b>에 놓는다. 화면 안에 들어가면서 타겟을 가리지 않는 자리를 고른다.
    ///
    /// ⚠ 화면 안으로 당기는 것만으로는 부족하다
    ///   예전엔 위/아래만 계산하고 화면 밖으로 나가면 안쪽으로 clamp 만 했다.
    ///   출전 화면의 배치 칸(DeployArea)처럼 **세로로 화면을 꽉 채운 타겟**은
    ///   위에도 아래에도 자리가 없어서, 당겨진 말풍선이 그대로 그 칸 위에 얹혔다
    ///   — 눌러 보라고 가리킨 병사 슬롯을 설명 글이 덮어 버렸다.
    ///
    /// ■ 고르는 순서
    ///   ① 요청받은 방향이 들어가면 그대로
    ///   ② 위·아래 중 자리가 있는 쪽 (넓은 쪽 우선)
    ///   ③ 좌·우 중 자리가 있는 쪽 — 세로로 긴 타겟은 여기서 풀린다
    ///
    /// ⚠ 못 찾으면 <b>중앙으로 떠넘기지 않고 false 를 돌려준다</b>
    ///   예전엔 여기서 바로 화면 중앙을 반환했다. 그러면 부르는 쪽은 "자리를 못 찾았다" 를
    ///   알 수 없어 말풍선 폭을 줄여 다시 시도할 기회가 없었다 (LayoutBubble 주석 참고).
    ///   겹치는 자리를 고르는 판단은 부르는 쪽이 마지막에 한다.
    /// </summary>
    bool TryPlaceBeside(Rect full, Rect hole, float w, float h,
                        TutorialAnchor requested, out Vector2 result)
    {
        float needV = h + BubbleGap + ScreenMargin;
        float needH = w + BubbleGap + ScreenMargin;

        float above = full.yMax - hole.yMax;
        float below = hole.yMin - full.yMin;
        float left  = hole.xMin - full.xMin;
        float right = full.xMax - hole.xMax;

        // 타겟 중심을 따라가되 화면 밖으로 나가지 않게 묶는다
        float cxAligned = Mathf.Clamp(hole.center.x, full.xMin + w * 0.5f + ScreenMargin,
                                                     full.xMax - w * 0.5f - ScreenMargin);
        float cyAligned = Mathf.Clamp(hole.center.y, full.yMin + h * 0.5f + ScreenMargin,
                                                     full.yMax - h * 0.5f - ScreenMargin);

        // ⚠ 화면 가장자리가 아니라 **타겟 바로 옆**에 붙인다
        //   예전엔 Above 가 full.yMax(화면 맨 위)로 보냈다. 그래서 화면 아래쪽
        //   스킬 슬롯을 가리키면 말풍선만 화면 꼭대기로 날아가, 무엇을 가리키는지
        //   눈으로 이을 수 없었다 ("지금 눌러 보세요" 가 아이콘에서 멀던 이유).
        //   타겟에서 BubbleGap 만큼만 띄우고, 화면을 벗어날 때만 안으로 당긴다.
        Vector2 Above() => new(cxAligned,
            Mathf.Min(hole.yMax + BubbleGap + h * 0.5f,
                      full.yMax - ScreenMargin - h * 0.5f));

        Vector2 Below() => new(cxAligned,
            Mathf.Max(hole.yMin - BubbleGap - h * 0.5f,
                      full.yMin + ScreenMargin + h * 0.5f));

        Vector2 Left()  => new(
            Mathf.Max(hole.xMin - BubbleGap - w * 0.5f,
                      full.xMin + ScreenMargin + w * 0.5f), cyAligned);

        Vector2 Right() => new(
            Mathf.Min(hole.xMax + BubbleGap + w * 0.5f,
                      full.xMax - ScreenMargin - w * 0.5f), cyAligned);

        // ① 요청받은 방향
        if (requested == TutorialAnchor.Above && above >= needV) { result = Above(); return true; }
        if (requested == TutorialAnchor.Below && below >= needV) { result = Below(); return true; }

        // ② 위·아래
        if (above >= needV || below >= needV)
        {
            result = above >= below ? Above() : Below();
            return true;
        }

        // ③ 좌·우 — 세로로 긴 타겟(배치 칸 같은 열)이 여기서 풀린다
        if (left >= needH || right >= needH)
        {
            result = right >= left ? Right() : Left();
            return true;
        }

        result = full.center;
        return false;
    }

    // ── 유틸 ─────────────────────────────────────────────────

    /// <summary>타겟의 화면 사각형을 오버레이 로컬 좌표로 옮긴다.</summary>
    Rect WorldRectOf(RectTransform target, float pad)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, cam, out var local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
    }

    /// <summary>임의의 부모 밑에 붙이는 사각형 (건너뛰기 버튼의 그림자·모서리용).</summary>
    static RectTransform MakeChildQuad(GameObject parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    RectTransform MakeQuad(string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_root, false);
        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = raycast;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return rt;
    }

    static TextMeshProUGUI MakeText(GameObject parent, string name, float size,
                                    Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize      = size;
        tmp.color         = color;
        tmp.fontStyle     = style;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    // 중심 기준 앵커라 위치·크기를 한 번에 넣는다
    void SetRect(RectTransform rt, Rect r)
    {
        if (r.width <= 0f || r.height <= 0f)
        {
            rt.sizeDelta = Vector2.zero;
            return;
        }
        rt.anchoredPosition = r.center;
        rt.sizeDelta        = r.size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
