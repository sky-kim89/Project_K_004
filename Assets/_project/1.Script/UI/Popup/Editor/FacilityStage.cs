#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  FacilityStage.cs  [Editor Only]
//  시설 화면(야영지·강화소·제단·상점)의 **공통 무대**를 짓는다.
//
//  ■ 왜 공용으로 뺐나 (사용자 요청, 2026-09-07)
//    전에는 화면마다 "가운데 패널 980×940, 위쪽 380px 에 그림 띠" 였다.
//    그림이 머리에만 붙은 창은 **창이지 장소가 아니다** — 야영지에 들렀다는
//    느낌이 안 나고, 네 시설이 전부 같은 회색 패널로 보였다.
//    이제 그림이 화면 전체를 덮고 그 위에 UI 가 얹힌다.
//
//    한 곳에 모아 둔 이유는 **넷이 같아 보여야 하기 때문**이다. 화면마다
//    따로 짜면 여백·글자 크기·어둠 농도가 조금씩 갈리고, 그 차이가
//    "만들다 만 화면" 으로 읽힌다.
//
//  ■ 무대 구성 (뒤에서 앞으로)
//      Art        전체화면 그림. preserveAspect=false 로 꽉 채운다
//      TopVeil    위쪽 어둠 — 제목·보유 자원이 앉는 자리
//      BottomVeil 아래쪽 어둠 — 선택지·카드가 앉는 자리
//      Content    실제 UI 가 들어가는 빈 칸 (여백이 이미 잡혀 있다)
//
//  ⚠ 그림 위에 글을 바로 얹지 않는다 (UI 규칙 8)
//    그림은 시설마다 다르고 밝기도 제각각이라, 글자색으로는 대비를 잡을 수
//    없다. 그래서 **어둠 판**을 깔고 그 위에만 글을 놓는다. 판은 위아래
//    가장자리에만 있고 가운데는 그림이 그대로 보인다 — 장소가 남는다.
//
//  ⚠ 세로 여백은 UIScale.PopupMaxH 를 쓰지 않는다
//    이건 전체화면이라 캔버스(1080)를 그대로 쓴다. 팝업 높이 상한 규칙은
//    가운데 패널형 창의 것이다.
// ============================================================

public static class FacilityStage
{
    /// <summary>화면 가장자리 여백. 네 시설이 같은 값을 쓴다.</summary>
    public const float Margin = 48f;

    /// <summary>위쪽 어둠 높이 — 제목 + 이야기 한 줄 + 보유 자원이 들어간다.</summary>
    public const float TopVeilH = 236f;

    /// <summary>아래쪽 어둠 높이 — 선택지가 앉는 자리. 화면 절반을 넘기지 않는다.</summary>
    public const float BottomVeilH = 560f;

    public static readonly Color VeilTop    = new(0.03f, 0.035f, 0.07f, 0.92f);
    public static readonly Color VeilBottom = new(0.03f, 0.035f, 0.07f, 0.88f);
    public static readonly Color SubText    = new(0.74f, 0.79f, 0.92f, 1f);
    public static readonly Color GoldText   = new(1f,    0.86f, 0.42f, 1f);

    /// <summary>
    /// 전체화면 무대를 짓고 각 조각을 돌려준다.
    ///
    /// <param name="content">
    /// UI 를 넣을 빈 칸. 위아래 어둠 사이가 아니라 <b>화면 전체</b>다 —
    /// 카드를 그림 위로 살짝 걸치게 놓는 화면(상점)이 있기 때문이다.
    /// 여백은 부르는 쪽이 Margin 으로 잡는다.
    /// </param>
    /// </summary>
    public static void Build(GameObject root,
                             out Image           art,
                             out TextMeshProUGUI title,
                             out TextMeshProUGUI flavor,
                             out GameObject      content,
                             out Button          closeBtn)
    {
        EditorUIBuilder.Stretch(root);

        // ── 배경 그림 — 화면 전체 ──
        //   ⚠ preserveAspect = false. 잘려도 장면은 읽히고, 여백이 생기면
        //     "그림이 덜 그려진 창" 으로 보인다.
        art = EditorUIBuilder.Img(root, "Art", Color.white);
        art.preserveAspect = false;
        art.raycastTarget  = true;    // 뒤쪽 전장이 눌리지 않게 막는다
        EditorUIBuilder.Stretch(art.gameObject);

        // ── 위쪽 어둠 ──
        var topVeil = EditorUIBuilder.Img(root, "TopVeil", VeilTop);
        topVeil.raycastTarget = false;
        {
            var rt = topVeil.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -TopVeilH);
            rt.offsetMax = Vector2.zero;
        }

        // ── 아래쪽 어둠 ──
        var bottomVeil = EditorUIBuilder.Img(root, "BottomVeil", VeilBottom);
        bottomVeil.raycastTarget = false;
        {
            var rt = bottomVeil.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, BottomVeilH);
        }

        // ── 제목 ──
        title = EditorUIBuilder.TMP(root, "Title", "시설", UIScale.FontXl, FontStyles.Bold);
        {
            var rt = title.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Margin, -Margin);
            rt.sizeDelta        = new Vector2(-(Margin * 2f + 140f), UIScale.RowLg);
        }
        title.alignment        = TextAlignmentOptions.MidlineLeft;
        title.raycastTarget    = false;
        title.textWrappingMode = TextWrappingModes.NoWrap;

        // ── 이야기 한 줄 ──
        //   ⚠ 규칙 설명이 아니다 (RunNodeFlavor 참고). 장소를 말하는 줄이다.
        //   ⚠ **한 줄, 작게, 흐리게** (사용자 지적, 2026-09-09)
        //     제목과 같은 크기로 두 줄을 차지하면 화면을 여는 순간 가장 먼저
        //     읽히는 것이 분위기 글이 된다. 여기서 읽어야 하는 것은 "여기가
        //     어디고 내 지갑에 얼마가 있나" 다. 분위기는 그 다음이다.
        flavor = EditorUIBuilder.TMP(root, "Flavor", "", UIScale.FontSm, FontStyles.Italic);
        {
            var rt = flavor.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Margin, -(Margin + UIScale.RowLg + 6f));
            rt.sizeDelta        = new Vector2(-(Margin * 2f + 420f), UIScale.RowSm);
        }
        flavor.alignment        = TextAlignmentOptions.MidlineLeft;
        flavor.color            = new Color(SubText.r, SubText.g, SubText.b, 0.72f);
        flavor.raycastTarget    = false;
        flavor.textWrappingMode = TextWrappingModes.NoWrap;
        flavor.overflowMode     = TextOverflowModes.Ellipsis;

        // ── 내용 칸 ──
        content = EditorUIBuilder.Go("Content", root);
        EditorUIBuilder.Stretch(content);

        // ── 닫기 — 오른쪽 위 ──
        //   ⚠ ✕ 글리프는 폰트에 없다 (UI 규칙 2). XMark 로 그린다.
        closeBtn = EditorUIBuilder.RaisedBtn(root, "CloseBtn",
                                             new Color(0.20f, 0.16f, 0.24f), out var body);
        {
            var rt = closeBtn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Margin, -Margin);
            rt.sizeDelta        = new Vector2(84f, 84f);
        }
        EditorUIBuilder.XMark(body, "X", 36f, new Color(0.90f, 0.92f, 0.97f));
    }

    /// <summary>
    /// 오른쪽 위의 <b>보유 골드</b> — [금화][6,020].
    ///
    /// ■ ⚠ 글자 "G" 를 쓰지 않는다 (사용자 지적, 2026-09-09)
    ///   재화는 아이콘이 정본 표기다 (UI 규칙 7 — 마나·마릿수와 같은 이유).
    ///   "70 G (보유 6020 G)" 처럼 한 줄에 두 숫자를 적어 두면 어느 쪽이 낼
    ///   돈인지 매번 다시 읽어야 하고, 실제로는 **둘 다 안 읽힌다.**
    ///   지갑은 여기(늘 같은 자리), <b>낼 값은 버튼 안</b>이다
    ///   — 몬스터 상세의 품질 개선 패널과 같은 규칙이다.
    /// </summary>
    public static TextMeshProUGUI PurseBadge(GameObject root, Sprite goldIcon, string name)
    {
        GameObject badge = Badge(root, name, goldIcon, 52f, UIScale.FontLg, 150f,
                                 GoldText, out TextMeshProUGUI value);

        var rt = badge.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-(Margin + 84f + 24f), -Margin);

        return value;
    }

    /// <summary>
    /// [아이콘][숫자] 한 벌. 자리는 부르는 쪽이 잡는다 (여기서는 크기만 정한다).
    ///
    /// ⚠ EditorUIBuilder.IconValueBadge 와 무엇이 다른가
    ///   그쪽은 <b>부모의 모서리</b>에 붙는 카드용 배지고 숫자 칸이 두 자리다.
    ///   여기는 골드(네 자리)·비용처럼 자리와 폭을 직접 잡아야 하는 자리에 쓴다.
    /// </summary>
    public static GameObject Badge(GameObject parent, string name, Sprite icon,
                                   float iconSize, float fontSize, float numWidth,
                                   Color numberColor, out TextMeshProUGUI value)
    {
        float gap = Mathf.Round(iconSize * 0.18f);

        GameObject root = EditorUIBuilder.Go(name, parent);
        root.GetComponent<RectTransform>().sizeDelta =
            new Vector2(iconSize + gap + numWidth, Mathf.Max(iconSize, UIScale.Line(fontSize)));

        var img = EditorUIBuilder.Img(root, "Icon", Color.white);
        img.sprite         = icon;
        img.preserveAspect = true;
        img.raycastTarget  = false;
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(iconSize, iconSize);
        }

        value = EditorUIBuilder.TMP(root, "Value", "0", fontSize, FontStyles.Bold);
        {
            var rt = value.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(numWidth, UIScale.Line(fontSize));
        }
        value.alignment        = TextAlignmentOptions.MidlineLeft;
        value.color            = numberColor;
        value.raycastTarget    = false;
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.enableAutoSizing = true;
        value.fontSizeMax      = fontSize;
        value.fontSizeMin      = Mathf.Round(fontSize * 0.7f);

        return root;
    }

    /// <summary>줄 제목 — "몬스터" · "특성" 처럼 묶음을 가르는 작은 라벨.</summary>
    public static TextMeshProUGUI SectionLabel(GameObject parent, string name, string text,
                                               float x, float yFromBottom)
    {
        var tmp = EditorUIBuilder.TMP(parent, name, text, UIScale.FontMd, FontStyles.Bold);
        var rt  = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, yFromBottom);
        rt.sizeDelta        = new Vector2(400f, UIScale.RowMd);
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.color            = SubText;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
}
#endif
