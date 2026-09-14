using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  TooltipLayer.cs
//  화면 어디서 열든 **가장 위에** 뜨는 공용 툴팁 한 장.
//
//  ■ 왜 자기 캔버스를 갖는가
//    툴팁을 여는 쪽(시너지 칩·특성 아이콘)이 어느 캔버스에 있든, 그 캔버스
//    안에서 아무리 맨 앞으로 보내도 **다른 캔버스보다 위로는 못 간다.**
//    실제로 카드 3택의 시너지 툴팁이 카드 면과 "얻는 것" 구역 밑으로 깔렸다.
//    sortingOrder 를 크게 준 제 캔버스에 두면 그 순서 싸움에서 벗어난다.
//
//  ■ 한 장뿐이다 — 그래서 동시에 둘이 뜰 수 없다
//    예전에는 칩마다 툴팁을 하나씩 달았다. 칩이 여덟 개면 툴팁도 여덟 개고,
//    각자 열고 닫으니 "겹쳐서 두 개가 떠 있는" 상태가 생겼다.
//    한 장을 돌려 쓰면 새로 여는 순간 이전 내용이 덮인다 — 규칙이 아니라
//    구조가 하나만 뜨게 만든다.
//
//  ■ 씬을 넘겨도 산다
//    로비·인게임·팝업이 전부 쓴다. 씬마다 하나씩 만들면 그 순간 다시
//    "여러 장" 문제로 돌아간다.
//
//  ■ 코드로 만든다 — 프리팹이 아니다
//    이 층은 화면 하나에 속하지 않아서 어느 Creator 의 산출물도 아니다.
//    구조가 세 줄짜리 패널이라 코드로 세우는 편이 짧다.
// ============================================================

public class TooltipLayer : MonoBehaviour
{
    /// <summary>다른 UI 보다 확실히 위. 팝업(보통 100~1000)보다 훨씬 크게 잡는다.</summary>
    const int SortingOrder = 32000;

    const float PanelWidth = 620f;

    static readonly Color PanelBg  = new(0.05f, 0.06f, 0.12f, 0.97f);
    static readonly Color DescGray = new(0.62f, 0.65f, 0.80f);
    static readonly Color StatMint = new(0.55f, 0.90f, 0.65f);

    static TooltipLayer _current;

    InfoTooltipUI _tooltip;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>owner 아래에 툴팁을 띄운다. 이미 떠 있으면 내용이 덮인다.</summary>
    public static void Open(RectTransform owner, string title, string desc, string stat)
    {
        Ensure();
        _current._tooltip.ShowAnchored(owner, title, desc, stat);
    }

    public static void Close()
    {
        if (_current != null) _current._tooltip.Close();
    }

    /// <summary>지금 떠 있나. 다른 데를 눌러 저절로 닫혔는지 확인하는 데 쓴다.</summary>
    public static bool IsOpen => _current != null && _current._tooltip.IsOpen;

    // ── 생성 ─────────────────────────────────────────────────

    static void Ensure()
    {
        if (_current != null) return;

        // ⚠ RectTransform 을 **명시해서** 만든다
        //   InfoTooltipUI.ShowAnchored 는 첫 줄에서 부모를 RectTransform 으로
        //   캐스팅하고, 실패하면 **아무 말 없이 그냥 돌아간다**(툴팁이 안 뜬다).
        //   new GameObject(name) 는 평범한 Transform 을 단다 — Canvas 를
        //   나중에 붙일 때 바뀌기를 기대하지 말고 처음부터 박아 둔다.
        var go = new GameObject("TooltipLayer",
                                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);

        _current = go.AddComponent<TooltipLayer>();
        _current.Build();
    }

    void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        // ⚠ 여는 쪽 캔버스와 같은 기준으로 스케일해야 자리가 맞는다
        //   ShowAnchored 가 대상의 월드 좌표를 이 캔버스 좌표로 옮기는데,
        //   배율이 다르면 툴팁이 엉뚱한 곳에 뜬다.
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UIScale.RefWidth, UIScale.RefHeight);
        scaler.matchWidthOrHeight  = UIScale.Match;

        // ⚠ 레이캐스터를 달지 않는다
        //   툴팁은 읽는 것이지 누르는 것이 아니다. 최상단 캔버스가 클릭을
        //   가로채면 그 아래의 버튼이 통째로 안 눌린다.

        _tooltip = BuildPanel(gameObject);
    }

    static InfoTooltipUI BuildPanel(GameObject parent)
    {
        var go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        go.GetComponent<Image>().color = PanelBg;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = Vector2.zero;   // 부모 좌하단
        rt.pivot            = new Vector2(0f, 1f);           // 아래로 펼침
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta        = new Vector2(PanelWidth, 0f);   // 높이는 CSF 가 정한다

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(14, 14, 12, 12);
        vlg.spacing                = 6f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI nameTmp = Line(go, "TooltipName", FontStyles.Bold,   Color.white);
        TextMeshProUGUI descTmp = Line(go, "TooltipDesc", FontStyles.Normal, DescGray);
        TextMeshProUGUI statTmp = Line(go, "TooltipStat", FontStyles.Normal, StatMint);
        statTmp.gameObject.SetActive(false);

        var ui = go.AddComponent<InfoTooltipUI>();
        ui.Bind(nameTmp, descTmp, statTmp);

        go.SetActive(false);
        return ui;
    }

    static TextMeshProUGUI Line(GameObject parent, string name, FontStyles style, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text             = "";
        tmp.fontSize         = UIScale.FontSm;
        tmp.fontStyle        = style;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }
}
