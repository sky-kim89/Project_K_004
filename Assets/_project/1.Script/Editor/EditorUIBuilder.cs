// ============================================================
//  EditorUIBuilder.cs  [Editor Only]
//  프리팹 Creator 들이 공유하는 UI 생성 헬퍼.
//
//  통합 배경
//    Creator 11개가 각자 Make*/Create*/Add* 이름으로 똑같은 헬퍼를
//    복사해 쓰고 있었다. 이름만 다르고 본문은 동일했으며, 일부는
//    alignment 지정이 빠지는 등 미묘하게 어긋나 있었다.
//    → 본문을 이 파일로 모으고, 각 Creator 는 기존 이름을
//      한 줄 포워더로 남긴다 (호출부는 그대로 유지).
//
//  주의
//    Panel() 의 parent null 허용은 "루트 오브젝트 생성" 용도다.
//    그 외 인자는 null 을 방어하지 않는다 — 잘못 넘기면 즉시 터져야 한다.
// ============================================================
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EditorUIBuilder
{
    // ══════════════════════════════════════════════════════════
    //  계층 생성
    // ══════════════════════════════════════════════════════════

    /// <summary>RectTransform + 지정 컴포넌트를 붙인 빈 GameObject.</summary>
    public static GameObject Go(string name, GameObject parent, params Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (components != null)
            foreach (var c in components) go.AddComponent(c);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// <summary>RectTransform + Image 패널. parent 가 null 이면 루트로 생성된다.</summary>
    public static GameObject Panel(GameObject parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        if (parent != null) go.transform.SetParent(parent.transform, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    /// <summary>Panel() 과 동일하되 Image 를 반환.</summary>
    public static Image Img(GameObject parent, string name, Color color)
        => Panel(parent, name, color).GetComponent<Image>();

    /// <summary>
    /// TextMeshProUGUI 생성. center=false 면 alignment 를 건드리지 않는다
    /// (TMP 기본값 TopLeft 유지 — DisassemblePopup 등이 이 동작에 의존).
    /// </summary>
    public static TextMeshProUGUI TMP(GameObject parent, string name, string text,
                                      float size, FontStyles style, bool center = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = Color.white;
        if (center) tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    /// <summary>Image + Button + 풀스트레치 Label(TMP) 구조의 버튼.</summary>
    public static GameObject Btn(GameObject parent, string name, string label,
                                 Color bgColor, float fontSize, bool boldLabel = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent.transform, false);
        go.GetComponent<Image>().color = bgColor;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        Stretch(labelGo);

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = boldLabel ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        return go;
    }

    /// <summary>버튼에 밝기 기반 하이라이트/프레스 전이를 적용.</summary>
    public static void TintTransition(GameObject btnGo, Color baseColor, float amount = 0.20f)
    {
        var btn = btnGo.GetComponent<Button>();
        var cb  = btn.colors;
        cb.highlightedColor = Color.Lerp(baseColor, Color.white, amount);
        cb.pressedColor     = Color.Lerp(baseColor, Color.black, amount);
        btn.colors = cb;
    }

    // ══════════════════════════════════════════════════════════
    //  입체 버튼 — 누를 수 있는 요소의 프로젝트 표준
    // ══════════════════════════════════════════════════════════
    //  ★ UI 규칙: 누를 수 있는 버튼은 반드시 이 헬퍼로 만든다.
    //    평평한 사각형은 버튼인지 라벨인지 구분이 안 된다.
    //
    //  구조
    //    Root (Button)
    //    ├─ Shadow   전체를 채움 → 아래 lift 만큼이 Body 밖으로 노출 (두께)
    //    └─ Body     상단 정렬, 높이 = H - lift   ← Button.targetGraphic
    //        ├─ TopEdge     상단 2px 밝은 선   (빛 받는 면)
    //        └─ BottomEdge  하단 4px 어두운 선 (음각)
    //
    //  내용(라벨·아이콘)은 반드시 out 으로 받은 body 아래에 넣는다.
    //  Root 에 직접 넣으면 눌렸을 때 같이 내려가지 않아 어색해진다.

    public const float BtnLift = 6f;   // 그림자 노출 폭 = 떠 있는 정도

    /// <summary>입체 버튼 루트를 만들고 내용 컨테이너(body)를 돌려준다.</summary>
    /// <summary>
    /// 팝업 헤더의 'i' 도움말 버튼. 닫기 버튼 **왼쪽**에 놓는다.
    ///
    ///   EditorUIBuilder.InfoBtn(header, TutorialId.HelpRelic);
    ///
    /// ⚠ 닫기 버튼과 같은 크기·같은 세로 위치로 맞춘다
    ///   나란히 놓이는 두 버튼의 크기가 다르면 헤더가 삐뚤어 보인다.
    ///   closeSize·closeRight 는 그 팝업의 닫기 버튼에 준 값을 그대로 넘긴다.
    ///
    /// ⚠ 'i' 는 폰트에 있는 글자다 (UI 규칙 2)
    ///   ⓘ·ℹ 같은 기호는 LiberationSans SDF 에 없어 두부(□)로 나온다.
    ///   그냥 소문자 i 를 굵게 쓴다.
    /// </summary>
    /// <param name="closeSize">닫기 버튼의 폭. 그만큼 왼쪽으로 비켜 놓는다.</param>
    /// <param name="size">i 버튼 자체의 크기. 0 이면 closeSize 를 따라간다.</param>
    /// <param name="anchorY">세로 기준. 0.5 = 부모 중앙(헤더 기본), 1 = 부모 위쪽.</param>
    /// <param name="y">anchorY 기준에서의 세로 오프셋.</param>
    public static Button InfoBtn(GameObject header, TutorialId tutorialId,
                                 float closeSize = 76f, float closeRight = -24f,
                                 float gap = 12f, float size = 0f,
                                 float anchorY = 0.5f, float y = 0f)
    {
        // ⚠ 크기와 '비켜 놓을 거리' 는 다른 값이다
        //   닫기 버튼이 없는 팝업(EventPopup)은 closeSize 를 0 으로 넘긴다.
        //   그때 크기까지 0 이 되면 눌리지 않는 점 하나가 남는다.
        if (size <= 0f) size = closeSize > 0f ? closeSize : 76f;

        var face = new Color(0.20f, 0.34f, 0.54f, 1f);   // 닫기(붉은)와 구분되는 파랑
        var btn  = RaisedBtn(header, "InfoBtn", face, out var body);

        // 닫기 버튼 왼쪽으로 (닫기 폭 + 간격)만큼 더 밀어 놓는다
        AnchorRightIn(btn.gameObject, closeRight - closeSize - gap, size, size, anchorY, y);

        var label = TMP(body, "Mark", "i", UIScale.FontMd, FontStyles.Bold);
        label.alignment     = TextAlignmentOptions.Center;
        label.color         = Color.white;
        label.raycastTarget = false;
        Stretch(label.gameObject);

        var info = btn.gameObject.AddComponent<TutorialInfoButton>();
        info.SetTutorial(tutorialId);

        return btn;
    }

    /// <summary>
    /// 동그라미 안에 i — "눌러 보면 설명이 있다" 는 표시.
    ///
    /// ■ 왜 헬퍼로 두나
    ///   로비 장수 카드(HeroPanelCreator)가 같은 그림을 직접 만들어 쓰고 있었다.
    ///   같은 뜻의 기호는 한 곳에서 그려야 화면마다 크기·색이 갈리지 않는다.
    ///
    /// ■ 동그라미는 유니티 내장 Knob 스프라이트다
    ///   ○ · ⓘ 같은 글자는 폰트에 없어서 □ 로 뜬다 (UI 규칙 2).
    ///   원형 스프라이트를 깔고 그 위에 ASCII 'i' 를 얹는 것이 가장 싸다.
    ///
    /// ⚠ 이 배지 자체는 버튼이 아니다 (raycastTarget = false)
    ///   눌리는 것은 이걸 얹은 대상이어야 한다 — 28px 짜리 점을 정확히 노리게
    ///   만들면 모바일에서 안 눌린다. 배지는 "여기 누를 게 있다" 는 신호만 한다.
    /// </summary>
    public static GameObject InfoDot(GameObject parent, string name, float size, Color face)
    {
        var badge = Img(parent, name, face);
        badge.raycastTarget = false;

        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (knob != null) badge.sprite = knob;

        var rt = badge.rectTransform;
        rt.sizeDelta = new Vector2(size, size);

        var label = TMP(badge.gameObject, "Mark", "i", size * 0.72f, FontStyles.Bold);
        label.alignment     = TextAlignmentOptions.Center;
        label.color         = Color.white;
        label.raycastTarget = false;
        var lRt = label.rectTransform;
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        // 'i' 는 위쪽이 비어 보인다 — 1px 내려 시각적 중심을 맞춘다
        lRt.offsetMin = new Vector2(0f, 1f);
        lRt.offsetMax = Vector2.zero;

        return badge.gameObject;
    }

    /// <summary>
    /// 헤더 오른쪽 끝에서 [도움말][닫기] 묶음이 차지하는 폭.
    ///
    /// ⚠ 헤더에 다른 위젯을 놓을 때는 반드시 이만큼 비켜선다
    ///   재화 표시·개수 배지·새로고침 버튼은 "닫기 버튼 왼쪽" 을 자기 자리로 잡고
    ///   있었다. i 버튼이 뒤늦게 같은 자리에 들어가면서 일곱 군데가 겹쳤다.
    ///   눈으로는 버튼이 글자 위에 얹힌 것으로 보이고, 그 위젯은 눌리지도 않는다.
    ///
    /// ⚠ 인자 규칙은 InfoBtn 과 같아야 한다
    ///   양쪽이 각자 계산하면 한쪽만 고쳤을 때 조용히 다시 겹친다.
    ///   closeSize 가 0(닫기 없는 팝업)이면 InfoBtn 과 똑같이 76 으로 본다.
    /// </summary>
    /// <param name="closePad">닫기 버튼이 오른쪽 끝에서 띄운 거리 (양수).</param>
    public static float HeaderRightBlock(float closeSize = 76f, float closePad = 24f,
                                         float gap = 12f, float infoSize = 0f)
    {
        if (infoSize <= 0f) infoSize = closeSize > 0f ? closeSize : 76f;
        return closePad + closeSize + gap + infoSize;
    }

    /// <summary>오른쪽 정렬 배치 — InfoBtn 이 쓰는 최소 앵커 헬퍼.</summary>
    static void AnchorRightIn(GameObject go, float right, float width, float height,
                              float anchorY = 0.5f, float y = 0f)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, anchorY);
        rt.pivot     = new Vector2(1f, anchorY);
        rt.anchoredPosition = new Vector2(right, y);
        rt.sizeDelta        = new Vector2(width, height);
    }

    public static Button RaisedBtn(GameObject parent, string name, Color face,
                                   out GameObject body, float lift = BtnLift)
        => RaisedBtnOn(Go(name, parent), face, out body, lift);

    /// <summary>
    /// 이미 있는 루트를 입체 버튼으로 만든다.
    /// 템플릿 프리팹처럼 루트가 먼저 정해진 경우에 쓴다
    /// (자식 경로가 "Body/..." 로 유지된다).
    /// </summary>
    public static Button RaisedBtnOn(GameObject root, Color face,
                                     out GameObject body, float lift = BtnLift)
    {
        var shadow = Go("Shadow", root);
        var shImg = shadow.AddComponent<Image>();
        shImg.color         = Color.Lerp(face, Color.black, 0.82f);
        shImg.raycastTarget = false;
        Stretch(shadow);

        body = Go("Body", root);
        body.AddComponent<Image>().color = face;
        var bRt = body.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0f);   bRt.anchorMax = new Vector2(1f, 1f);
        bRt.offsetMin = new Vector2(0f, lift); bRt.offsetMax = Vector2.zero;

        BtnEdge(body, "TopEdge",    Color.Lerp(face, Color.white, 0.30f), true,  2f);
        BtnEdge(body, "BottomEdge", Color.Lerp(face, Color.black, 0.45f), false, 4f);

        var btn = root.AddComponent<Button>();
        btn.targetGraphic = body.GetComponent<Image>();
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = TintFor(Color.Lerp(face, Color.white, 0.14f), face);
        cb.pressedColor     = TintFor(Color.Lerp(face, Color.black, 0.28f), face);
        cb.selectedColor    = Color.white;
        cb.disabledColor    = TintFor(Color.Lerp(face, Color.black, 0.45f), face);
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;
        return btn;
    }

    /// <summary>가운데 라벨이 있는 입체 버튼.</summary>
    public static Button RaisedTextBtn(GameObject parent, string name, string label,
                                       float fontSize, Color face, float lift = BtnLift)
    {
        var btn = RaisedBtn(parent, name, face, out var body, lift);
        var tmp = TMP(body, "Label", label, fontSize, FontStyles.Bold);
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        Stretch(tmp.gameObject);
        return btn;
    }

    /// <summary>
    /// 둥근 입체 버튼. 사각 <see cref="RaisedBtn"/> 의 원형판이다.
    ///
    /// ■ 왜 따로 있나
    ///   RaisedBtn 은 위·아래 모서리에 밝고 어두운 **막대**를 깔아 두께를
    ///   만든다. 원형 스프라이트에 그 막대를 얹으면 원 밖으로 삐져나온
    ///   직선이 그대로 보인다. 원은 원반 두 장을 어긋나게 겹쳐 두께를 낸다.
    ///
    /// ■ 몸통·그림자를 정사각으로 잡는다 (찌그러짐 방지)
    ///   루트를 그대로 늘리면 몸통이 lift 만큼 납작해져 타원이 된다.
    ///   한 변 = 루트 높이 − lift 인 정사각을 위/아래로 나눠 붙이면
    ///   원은 정원 그대로고 그림자만 아래로 lift 만큼 내민다.
    ///
    /// ⚠ 라벨·아이콘은 반드시 body 아래에 넣는다 (루트에 넣으면 눌려도 안 내려간다).
    /// </summary>
    public static Button RoundBtn(GameObject parent, string name, Color face,
                                  out GameObject body, float lift = BtnLift)
    {
        var root = Go(name, parent);
        Sprite disc = Circle();

        var shadow = Go("Shadow", root);
        var shImg = shadow.AddComponent<Image>();
        shImg.sprite         = disc;
        shImg.color          = Color.Lerp(face, Color.black, 0.82f);
        shImg.raycastTarget  = false;
        shImg.preserveAspect = true;
        SquareIn(shadow, bottom: true, lift: lift);

        body = Go("Body", root);
        var bodyImg = body.AddComponent<Image>();
        bodyImg.sprite         = disc;
        bodyImg.color          = face;
        bodyImg.preserveAspect = true;
        SquareIn(body, bottom: false, lift: lift);

        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bodyImg;
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = TintFor(Color.Lerp(face, Color.white, 0.14f), face);
        cb.pressedColor     = TintFor(Color.Lerp(face, Color.black, 0.28f), face);
        cb.selectedColor    = Color.white;
        cb.disabledColor    = TintFor(Color.Lerp(face, Color.black, 0.45f), face);
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;

        return btn;
    }

    /// <summary>루트 안에서 위(또는 아래)에 붙는 정사각. 한 변 = 루트 높이 − lift.</summary>
    static void SquareIn(GameObject go, bool bottom, float lift)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, bottom ? 0f : 1f);
        rt.pivot     = new Vector2(0.5f, bottom ? 0f : 1f);
        rt.anchoredPosition = Vector2.zero;   // 앵커가 한 점이라 sizeDelta 가 곧 크기다
        rt.sizeDelta        = Vector2.zero;   // 실제 값은 SetRoundSize 가 넣는다
    }

    /// <summary>
    /// <see cref="RoundBtn"/> 의 크기를 정한다.
    /// 루트 높이 = 지름 + lift 이므로 지름만 주면 나머지는 여기서 맞춘다.
    /// </summary>
    public static void SetRoundSize(Button btn, float diameter, float lift = BtnLift)
    {
        var rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(diameter, diameter + lift);

        foreach (string child in new[] { "Shadow", "Body" })
            ((RectTransform)rt.Find(child)).sizeDelta = new Vector2(diameter, diameter);
    }

    /// <summary>유니티 내장 원형 스프라이트. 원을 그릴 때 폰트 글리프를 쓰지 않기 위한 것이다 (UI 규칙 2).</summary>
    public static Sprite Circle()
        => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    static void BtnEdge(GameObject parent, string name, Color color, bool top, float thickness)
    {
        var go = Go(name, parent);
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot     = new Vector2(0.5f, top ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, thickness);
    }

    /// <summary>
    /// Button.colors 는 targetGraphic 색에 곱해진다.
    /// 원하는 최종 색을 얻으려면 기준색으로 나눈 값을 넣어야 한다.
    /// </summary>
    public static Color TintFor(Color want, Color baseColor) => new Color(
        baseColor.r <= 0.001f ? 1f : Mathf.Clamp01(want.r / baseColor.r),
        baseColor.g <= 0.001f ? 1f : Mathf.Clamp01(want.g / baseColor.g),
        baseColor.b <= 0.001f ? 1f : Mathf.Clamp01(want.b / baseColor.b),
        1f);

    /// <summary>부모를 가득 채우는 배경 Image 를 첫 자식으로 삽입.</summary>
    public static GameObject BgPanel(GameObject parent, Color color)
    {
        var go = Panel(parent, "BgPanel", color);
        go.transform.SetAsFirstSibling();
        Stretch(go);
        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  팝업 공용 시각 언어
    // ══════════════════════════════════════════════════════════
    //  "어두운 남색 패널 + 헤더 밴드 + 강조선 + 섹션 라벨" 계열.
    //  EventPopup 에서 시작해 BattleResult / Reincarnation 이 공유한다.
    //  승패색·확인버튼색처럼 팝업 하나에만 쓰는 강조색은 여기 두지 않는다.

    public static class Pop
    {
        public static readonly Color PanelBg     = new Color(0.070f, 0.075f, 0.130f, 1f);
        public static readonly Color PanelBorder = new Color(0.24f,  0.30f,  0.52f,  1f);
        public static readonly Color HeaderBg    = new Color(0.095f, 0.110f, 0.200f, 1f);
        public static readonly Color TitleShadow = new Color(0.02f,  0.02f,  0.06f,  0.85f);
        public static readonly Color SectionLbl  = new Color(0.66f,  0.70f,  0.86f,  1f);
        public static readonly Color Divider     = new Color(0.26f,  0.29f,  0.44f,  0.85f);
        public static readonly Color SlotBg      = new Color(0.105f, 0.115f, 0.190f, 1f);
        public static readonly Color SubText     = new Color(0.72f,  0.76f,  0.90f,  1f);
        public static readonly Color TabActive   = new Color(0.24f,  0.40f,  0.74f,  1f);
        public static readonly Color TabInactive = new Color(0.155f, 0.175f, 0.275f, 1f);
    }

    /// <summary>
    /// 가운데 글자 + 좌우 라인 형태의 섹션 구분 라벨 (상단 밴드 배치).
    /// contentW 는 라인 길이 계산용 — 라벨 줄의 실제 폭이다.
    /// </summary>
    public static GameObject SectionLabel(GameObject parent, string text, float yFromTop,
                                          float contentW, float sidePad)
    {
        const float LabelW = 200f;
        const float Gap    = 16f;
        float h = UIScale.RowSm;   // FontSm 한 줄이 잘리지 않는 최소 높이 (UI 규칙 5)

        var row = Go($"Section_{text}", parent);
        AnchorTop(row.GetComponent<RectTransform>(), yFromTop, h, sidePad * 2f);

        var label = TMP(row, "Label", text, UIScale.FontSm, FontStyles.Bold);
        label.color         = Pop.SectionLbl;
        label.raycastTarget = false;
        Center(label.rectTransform, Vector2.zero, new Vector2(LabelW, h));

        for (int side = 0; side < 2; side++)
        {
            var line = Go(side == 0 ? "LineL" : "LineR", row);
            var img  = line.AddComponent<Image>();
            img.color         = Pop.Divider;
            img.raycastTarget = false;
            var rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(side == 0 ? 0f : 1f, 0.5f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot     = new Vector2(side == 0 ? 0f : 1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2((contentW - LabelW) * 0.5f - Gap, 2f);
        }
        return row;
    }

    // ══════════════════════════════════════════════════════════
    //  기호 도형 — 폰트 글리프 대신 Image 로 그린다
    // ══════════════════════════════════════════════════════════
    //  ⚠ 기본 폰트(LiberationSans SDF)에 없는 글자: ★ ✔ ✕ ▶ ◀ ▲ ⚙ 🔒
    //    (› — × 는 있다. 한글은 폴백 폰트가 처리한다)
    //    아틀라스가 Static 이라 런타임에 글리프를 채울 수도 없어,
    //    없는 글자는 □(두부)로 그려져 UI 에 그대로 노출된다.
    //    장식 기호는 아래 헬퍼로 만들 것 — 폰트에 의존하지 않는다.

    // ── 아이콘 + 숫자 배지 ───────────────────────────────────
    //
    //  ⚠ 마나와 몬스터 마릿수는 언제나 아이콘이다 (확정 규칙 — CLAUDE.md UI 규칙 7)
    //    "마나 12" 처럼 글자로 적지 않는다. 게임에서 가장 자주 읽는 두 숫자라
    //    화면마다 표기가 갈리면 같은 값을 매번 다시 읽어야 한다.
    //
    //  ■ 구조 — 아이콘과 숫자를 **가로로 나란히** 놓는다 (2026-09-03, 사용자 확정)
    //
    //      ┌──────────┐        ┌──────────┐
    //      │  ◈   8   │        │  ☠   3   │
    //      └──────────┘        └──────────┘
    //        아이콘 숫자
    //
    //    ■ 왜 바꿨나 — 아이콘 위에 얹는 방식은 맞추는 값이 너무 많다
    //      예전에는 숫자를 아이콘 그림 위에 앉혔다. 그러려면 아이콘마다
    //      "비어 있는 줄"(numberY)을 알파를 재서 찾아야 하고, 글자색도 그
    //      자리의 밝기를 보고 따로 정해야 하고, 폰트 크기는 그림의 불투명
    //      폭에 묶여 UIScale 단계를 못 썼다. 값 세 개가 전부 **그림 파일에
    //      종속**이라 아이콘을 갈아 끼울 때마다 다시 재야 했고, 실제로 크기가
    //      맞지 않는다는 지적이 여러 번 나왔다.
    //
    //      나란히 놓으면 그 셋이 전부 사라진다 — 숫자는 카드 면(늘 어두운
    //      바탕) 위에 앉으므로 세로 자리도, 글자색도, 폰트 상한도 그림과
    //      무관하다. 폰트는 다시 UIScale 단계(규칙 4)를 그대로 쓴다.
    //
    //    ⚠ 대신 배지가 넓어진다 — 좌우 한 쌍이 들어가는지 반드시 확인할 것
    //        BadgeWidthFor(아이콘, 폰트) × 2 + inset × 2 ≤ 부모 폭
    //      하단 카드 바는 폭이 150 뿐이라 이 식이 아이콘 크기를 정한다.
    //      (예전에 이 계산을 빼먹어 좌우 배지가 22px 겹친 적이 있다)
    //
    //    ⚠ 숫자 뒤에 면(칩·패널)을 깔지 않는다
    //      한때 어두운 칩을 깔았더니 아이콘이 그 뒤로 가려져 무엇을 뜻하는
    //      배지인지 안 보였다. 카드 면이 이미 어두우니 깔 것이 없다.
    //
    //    ⚠ 외곽선도 두르지 않는다 (UI 규칙 8)
    //      숫자가 앉는 바탕은 카드 면 하나로 정해져 있다 — 밝은 글자 하나로 끝난다.
    //
    //  ■ 좌우 한 쌍으로 쓰는 물건이다
    //    rightSide 만 뒤집으면 두 모서리에 대칭으로 앉는다. 배지 **안쪽**
    //    순서는 양쪽 다 [아이콘][숫자] 로 같다 — 거울상으로 뒤집으면 두 숫자가
    //    카드 가운데에서 마주 보게 되어 어느 아이콘의 값인지 헷갈린다.
    //    두 배지를 만드는 곳은 전부 이 함수 하나를 부른다
    //    (하단 카드 바 · 카드 3택 팝업).

    /// <summary>아이콘과 숫자 사이 간격.</summary>
    public static float BadgeGapFor(float iconSize) => Mathf.Round(iconSize * 0.12f);

    /// <summary>
    /// 숫자 칸의 폭. <b>두 자리</b>가 들어가는 크기다.
    /// (LiberationSans 숫자 폭 0.556em → 두 자리 ≈ 폰트 × 1.11, 여유를 조금 더 준다)
    /// 세 자리(과부하가 크게 붙은 마나)는 자동 축소가 받는다.
    /// </summary>
    public static float BadgeNumWidthFor(float fontSize) => Mathf.Ceil(fontSize * 1.18f);

    /// <summary>
    /// 배지 하나의 전체 폭. <b>좌우 한 쌍을 놓기 전에 반드시 이 값으로 검산할 것</b> —
    /// <c>BadgeWidthFor × 2 + inset × 2 ≤ 부모 폭</c>.
    /// </summary>
    public static float BadgeWidthFor(float iconSize, float fontSize)
        => iconSize + BadgeGapFor(iconSize) + BadgeNumWidthFor(fontSize);

    /// <summary>
    /// [아이콘][숫자] 한 벌. 부모의 <b>위쪽 모서리</b>에 붙는다.
    /// </summary>
    /// <param name="rightSide">true 면 오른쪽 위, false 면 왼쪽 위.</param>
    /// <param name="iconSize">아이콘 한 변. 배지 전체 폭은 <see cref="BadgeWidthFor"/> 로 구한다.</param>
    /// <param name="fontSize">숫자 크기. 카드 면 위에 앉으므로 UIScale 단계를 그대로 쓴다 (규칙 4).</param>
    /// <param name="numberColor">숫자 색. 바탕은 늘 어두운 카드 면이므로 밝은 색이다.</param>
    /// <param name="inset">모서리에서 띄우는 여백. 음수면 카드 밖으로 내민다.</param>
    public static GameObject IconValueBadge(GameObject parent, string name, Sprite icon,
                                            bool rightSide, float iconSize, float fontSize,
                                            float inset, Color numberColor,
                                            out TextMeshProUGUI valueText)
    {
        float gap   = BadgeGapFor(iconSize);
        float numW  = BadgeNumWidthFor(fontSize);
        float width = iconSize + gap + numW;

        // 글자 한 줄이 아이콘보다 높을 수 있다 — 낮은 쪽에 맞추면 아래가 잘린다.
        float height = Mathf.Max(iconSize, UIScale.Line(fontSize));

        var root = Go(name, parent);
        var rt   = root.GetComponent<RectTransform>();

        float outer = rightSide ? 1f : 0f;   // 부모의 바깥쪽 변

        rt.anchorMin = rt.anchorMax = new Vector2(outer, 1f);
        rt.pivot     = new Vector2(outer, 1f);
        rt.anchoredPosition = new Vector2(rightSide ? -inset : inset, -inset);
        rt.sizeDelta        = new Vector2(width, height);

        var img = Img(root, "Icon", Color.white);
        img.sprite         = icon;
        img.preserveAspect = true;
        img.raycastTarget  = false;
        var iRt = img.rectTransform;
        iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
        iRt.pivot     = new Vector2(0f, 0.5f);
        iRt.anchoredPosition = Vector2.zero;
        iRt.sizeDelta        = new Vector2(iconSize, iconSize);

        valueText = TMP(root, "Value", "9", fontSize, FontStyles.Bold);

        var vRt = valueText.rectTransform;
        vRt.anchorMin = vRt.anchorMax = new Vector2(1f, 0.5f);
        vRt.pivot     = new Vector2(1f, 0.5f);
        vRt.anchoredPosition = Vector2.zero;
        vRt.sizeDelta        = new Vector2(numW, UIScale.Line(fontSize));

        valueText.alignment        = TextAlignmentOptions.Center;
        valueText.raycastTarget    = false;
        valueText.textWrappingMode = TextWrappingModes.NoWrap;
        valueText.color            = numberColor;

        // 세 자리("120" — 과부하가 크게 붙은 마나)는 줄여서 칸에 맞춘다.
        // 넘치게 두면 옆 배지의 아이콘 위로 올라탄다.
        valueText.enableAutoSizing = true;
        valueText.fontSizeMax      = fontSize;
        valueText.fontSizeMin      = Mathf.Round(fontSize * 0.72f);

        return root;
    }


    /// <summary>회전한 막대 하나. 기호를 조립하는 기본 단위.</summary>
    public static GameObject Bar(GameObject parent, string name, float length, float thickness,
                                 float angleDeg, Vector2 offset, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);

        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta        = new Vector2(length, thickness);
        rt.localEulerAngles = new Vector3(0f, 0f, angleDeg);
        return go;
    }

    /// <summary>체크 표시 (✔ 대체). size = 외접 정사각형 한 변.</summary>
    public static GameObject CheckMark(GameObject parent, string name, float size, Color color)
    {
        var root = Go(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        float t = Mathf.Max(3f, size * 0.14f);
        Bar(root, "Short", size * 0.42f, t,  45f, new Vector2(-size * 0.20f, -size * 0.12f), color);
        Bar(root, "Long",  size * 0.80f, t, -45f, new Vector2( size * 0.10f,  size * 0.06f), color);
        return root;
    }

    /// <summary>
    /// 위로 향한 삼각형 (▲ 대체) — "업(상승)" 표시용.
    /// 가로 막대를 계단식으로 쌓아 그린다. 도트 느낌이라 이 게임 아트와도 맞는다.
    /// </summary>
    public static GameObject TriangleUp(GameObject parent, string name, float size, Color color)
    {
        var root = Go(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        const int Steps = 5;
        float rowH = size / Steps;
        for (int i = 0; i < Steps; i++)
        {
            // 위에서 i번째 줄 — 아래로 갈수록 넓어진다.
            // 높이에 +1 을 더해 줄 사이에 실오라기 같은 틈이 생기지 않게 한다.
            float w = size * (i + 1) / Steps;
            float y = size * 0.5f - rowH * (i + 0.5f);
            Bar(root, $"Row{i}", w, rowH + 1f, 0f, new Vector2(0f, y), color);
        }
        return root;
    }

    /// <summary>X 표시 (✕ 대체) — 닫기 버튼용.</summary>
    public static GameObject XMark(GameObject parent, string name, float size, Color color)
    {
        var root = Go(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        float t = Mathf.Max(3f, size * 0.13f);
        Bar(root, "A", size * 0.92f, t,  45f, Vector2.zero, color);
        Bar(root, "B", size * 0.92f, t, -45f, Vector2.zero, color);
        return root;
    }

    /// <summary>
    /// 당구장 표시 (※ 대체). size = 외접 정사각형 한 변.
    ///
    /// ⚠ ※ (U+203B) 는 이 프로젝트 폰트에 <b>없다</b> — 그려야 한다
    ///   LiberationSans SDF 는 글리프 250자(ASCII + Latin-1 일부)뿐이고
    ///   한글 폴백(TDS_RPG_2 SDF)은 한글 음절 11,172자 <b>만</b> 갖고 있다.
    ///   둘 다 AtlasPopulationMode = Static 이라 런타임에 채울 수도 없다.
    ///   그냥 쓰면 화면에 □ 가 뜬다 (UI 규칙 2).
    ///
    /// 획 구성: 가로 막대 2개 + 대각 막대 4개 — 원본의 '쌀 米' 꼴을 작은 크기에서
    /// 읽히는 선까지만 줄인 근사다. 본문 글자 크기(FontSm)에서 주석 기호로 읽힌다.
    /// </summary>
    public static GameObject ReferenceMark(GameObject parent, string name, float size, Color color)
    {
        var root = Go(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        float t    = Mathf.Max(2f, size * 0.11f);
        float arm  = size * 0.44f;
        float span = size * 0.30f;

        // 위·아래 가로 획
        Bar(root, "H1", size * 0.86f, t, 0f, new Vector2(0f,  span), color);
        Bar(root, "H2", size * 0.86f, t, 0f, new Vector2(0f, -span), color);

        // 네 귀퉁이 대각 획 — 중심에서 바깥으로 뻗는다
        Bar(root, "D1", arm, t,  45f, new Vector2(-span * 0.62f,  span * 0.62f), color);
        Bar(root, "D2", arm, t, -45f, new Vector2( span * 0.62f,  span * 0.62f), color);
        Bar(root, "D3", arm, t, -45f, new Vector2(-span * 0.62f, -span * 0.62f), color);
        Bar(root, "D4", arm, t,  45f, new Vector2( span * 0.62f, -span * 0.62f), color);
        return root;
    }

    /// <summary>꺾쇠 (› ‹ ▶ ◀ 대체). dirDeg: 0=우, 180=좌, 90=위, -90=아래.</summary>
    public static GameObject Chevron(GameObject parent, string name, float size,
                                     float dirDeg, Color color)
    {
        var root = Go(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0f, 0f, dirDeg);

        float t = Mathf.Max(3f, size * 0.16f);
        float h = size * 0.52f;
        Bar(root, "Up",   h, t, -45f, new Vector2(-size * 0.10f,  size * 0.19f), color);
        Bar(root, "Down", h, t,  45f, new Vector2(-size * 0.10f, -size * 0.19f), color);
        return root;
    }

    /// <summary>자물쇠 (🔒 대체) — 잠금 상태 표시.</summary>
    public static GameObject PadLock(GameObject parent, string name, float size, Color color)
    {
        var root = Go(name, parent);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        float t  = Mathf.Max(3f, size * 0.11f);
        float bw = size * 0.62f;          // 몸통 폭
        float bh = size * 0.46f;          // 몸통 높이
        float sw = size * 0.36f;          // 고리 폭

        // 몸통
        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Image));
        bodyGo.transform.SetParent(root.transform, false);
        var bImg = bodyGo.GetComponent<Image>();
        bImg.color = color; bImg.raycastTarget = false;
        var bRt = bodyGo.GetComponent<RectTransform>();
        bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 0.5f);
        bRt.pivot     = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = new Vector2(0f, -size * 0.16f);
        bRt.sizeDelta        = new Vector2(bw, bh);

        // 고리 (ㄷ 자 — 좌·우 세로 + 상단 가로)
        float shTop = size * 0.36f;
        Bar(root, "ShackleL", size * 0.30f, t, 90f, new Vector2(-sw * 0.5f, shTop * 0.62f), color);
        Bar(root, "ShackleR", size * 0.30f, t, 90f, new Vector2( sw * 0.5f, shTop * 0.62f), color);
        Bar(root, "ShackleT", sw + t,       t,  0f, new Vector2(0f,         shTop),         color);
        return root;
    }

    /// <summary>마름모 (★ 대체) — 태그·불릿용 소형 장식.</summary>
    public static GameObject Diamond(GameObject parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);

        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(size, size);
        rt.localEulerAngles = new Vector3(0f, 0f, 45f);
        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  RectTransform
    // ══════════════════════════════════════════════════════════

    /// <summary>부모를 가득 채우도록 앵커·오프셋 설정.</summary>
    /// <summary>
    /// 이름으로 자손을 찾는다 (직계 우선 → 깊이 우선).
    /// Creator 가 다른 Creator 의 프리팹(HeroCard 등)에서 필드를 뽑아 쓸 때 사용한다.
    /// 그쪽 계층이 한 단계 깊어져도 연결이 끊기지 않는다.
    /// </summary>
    public static T FindDeep<T>(Transform root, string name) where T : Component
    {
        var direct = root.Find(name);
        if (direct != null) return direct.GetComponent<T>();

        foreach (Transform child in root)
        {
            var found = FindDeep<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static void Stretch(GameObject go) => Stretch(go.GetComponent<RectTransform>());

    /// <summary>부모 중앙 기준 고정 크기 배치.</summary>
    public static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    /// <summary>상단 스트레치 배치 — 위에서 yFromTop 만큼 내려온 높이 height 영역.</summary>
    public static void AnchorTop(RectTransform rt, float yFromTop, float height, float padH = 0f)
    {
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -yFromTop);
        rt.sizeDelta        = new Vector2(-padH, height);
    }

    // ══════════════════════════════════════════════════════════
    //  레이아웃
    // ══════════════════════════════════════════════════════════

    /// <summary>LayoutElement 를 붙이고 preferred 크기 지정 (0 이하는 건너뜀).</summary>
    public static LayoutElement LE(GameObject go, float width, float height)
    {
        if (!go.TryGetComponent<LayoutElement>(out var le))
            le = go.AddComponent<LayoutElement>();
        if (width  > 0f) le.preferredWidth  = width;
        if (height > 0f) le.preferredHeight = height;
        return le;
    }

    /// <summary>재화 위젯용 가로 레이아웃 (아이콘 + 수량 텍스트).</summary>
    public static void CostHlg(GameObject go)
    {
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing                = 3f;
    }

    /// <summary>레이아웃 안에서 아이콘 크기를 고정.</summary>
    public static void IconLE(Image img, float size)
    {
        img.rectTransform.sizeDelta = new Vector2(size, size);
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth  = size;
        le.preferredHeight = size;
        le.minWidth        = size;
        le.minHeight       = size;
    }

    // ══════════════════════════════════════════════════════════
    //  SerializedObject 필드 연결
    // ══════════════════════════════════════════════════════════
    //  필드를 못 찾으면 LogError — 프리팹은 만들어지지만 연결이 빠진
    //  상태로 조용히 넘어가면 안 된다. (필드명 변경·삭제를 즉시 노출)

    public static void SetObj(SerializedObject so, string field, UnityEngine.Object obj, string tag)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[{tag}] 직렬화 필드 없음: {field}"); return; }
        prop.objectReferenceValue = obj;
    }

    public static void SetObjArray(SerializedObject so, string field,
                                   UnityEngine.Object[] objs, string tag)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[{tag}] 직렬화 배열 필드 없음: {field}"); return; }
        prop.arraySize = objs.Length;
        for (int i = 0; i < objs.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = objs[i];
    }

    public static void SetEnum(SerializedObject so, string field, int value, string tag)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[{tag}] 직렬화 필드 없음: {field}"); return; }
        prop.intValue = value;
    }
}
