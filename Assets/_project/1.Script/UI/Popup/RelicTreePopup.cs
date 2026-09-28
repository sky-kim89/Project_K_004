using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
//  RelicTreePopup.cs
//  유물 테크트리 화면 — 구 RelicPopup(카드 그리드)을 대체한다.
//
//  ■ 조작
//    · 배경 드래그로 이동, 휠·두 손가락으로 확대/축소 (ZoomMin~ZoomMax 로 제한)
//    · 노드를 누르면 툴팁에 효과가 뜬다
//    · 노드 아이콘 아래 강화 버튼(▲ + 비용)으로 레벨을 올린다
//
//  ■ 안개
//    부모를 1레벨 이상 찍어야 자식이 열린다(RelicTreeCatalog.IsUnlocked).
//    열리기 전에는 이름도 효과도 보이지 않고, 보이는 노드의 자식만
//    "?" 실루엣으로 그려진다 — 트리가 어디로 이어지는지는 알아야
//    지금 찍는 노드가 막다른 길인지 판단이 된다.
//
//  ⚠ 노드는 한 번만 만든다
//    수십 개를 Refresh 마다 Destroy/Instantiate 하면 강화 한 번에 프레임이 튄다.
//    EnsureBuilt() 가 최초 1회 만들고, 이후에는 상태만 갈아 끼운다.
//    (구 RelicPopup 은 매번 카드를 다시 만들어 연출 대상까지 놓쳤다)
//
//  Inspector 연결 — 이름을 바꾸면 RelicTreePopupCreator 도 같이 고칠 것:
//    _pointText  : 보유 환생 포인트 TMP
//    _viewport   : 마스크가 걸린 트리 영역 (드래그·줌 판정 기준)
//    _content    : 실제로 움직이고 확대되는 RectTransform
//    _nodeTemplate / _edgeTemplate / _ghostTemplate : 비활성 원본
//    _tooltip*   : 노드 클릭 시 뜨는 설명 패널
// ============================================================

public class RelicTreePopup : PopupBase,
    IBeginDragHandler, IDragHandler, IScrollHandler, IPointerClickHandler
{
    [Header("헤더")]
    [SerializeField] TextMeshProUGUI _pointText;
    [SerializeField] TextMeshProUGUI _summaryText;

    [Header("트리 캔버스")]
    [SerializeField] RectTransform _viewport;
    [SerializeField] RectTransform _content;
    [SerializeField] GameObject    _nodeTemplate;
    [SerializeField] GameObject    _edgeTemplate;
    [SerializeField] GameObject    _ghostTemplate;

    [Header("상세 카드")]
    [SerializeField] GameObject      _tooltipRoot;

    [Tooltip("트리 노드와 **같은 그림**. 없으면 칸만 비고 글 자리는 그대로다.")]
    [SerializeField] Image           _tipIcon;

    [SerializeField] TextMeshProUGUI _tipName;
    [SerializeField] TextMeshProUGUI _tipSub;
    [SerializeField] TextMeshProUGUI _tipEffect;
    [SerializeField] TextMeshProUGUI _tipCost;

    [Header("하단")]
    [SerializeField] Button          _reincarnateBtn;
    [SerializeField] TextMeshProUGUI _reincLabel;
    [SerializeField] Button          _resetBtn;
    [SerializeField] Button          _closeBtn;

    // ⚠ 최소 배율은 "트리 전체가 한 화면에 들어가는" 값이어야 한다
    //   트리는 6440×5320 캔버스 단위인데 뷰포트는 1920×844 뿐이다.
    //   0.16 에서 대략 전체가 들어온다.
    //
    // ⚠ _zoomInit 이 아이콘 체감 크기를 결정한다 (2026-08-25 상향)
    //   0.50 에서는 140 아이콘이 실효 70px, 강화 버튼이 98×52 로 그려져
    //   "무슨 그림인지 안 보이고 누르기도 어려운" 상태였다. 0.65 로 연다.
    //
    //   0.65 를 넘기지 말 것 — 뿌리에서 두 칸 위(N_Blade)까지가 뷰포트 세로
    //   절반(422)에 들어와야 한다. 2 × Spacing × 0.65 = 364 로 아슬하게 맞고,
    //   더 키우면 첫 환생 튜토리얼이 가리키는 실루엣이 화면 밖으로 나간다.
    [Header("확대 한계")]
    [SerializeField] float _zoomMin  = 0.16f;
    [SerializeField] float _zoomMax  = 1.60f;
    [SerializeField] float _zoomInit = 0.65f;

    /// <summary>
    /// 그리드 한 칸의 캔버스 픽셀. 노드 카드가 264px 이라 그보다 넉넉히 잡는다.
    /// ⚠ RelicTreePopupCreator.NodeW 보다 커야 가로 이웃끼리 겹치지 않는다.
    /// </summary>
    public const float Spacing = 280f;

    /// <summary>
    /// 노드 카드 안에서 아이콘 판(Face)이 얹힌 높이.
    ///
    /// ⚠ 간선·실루엣은 카드 원점이 아니라 이 높이에 맞춘다
    ///   카드의 anchoredPosition 은 이름·레벨칩·강화버튼까지 포함한 자리라서
    ///   아이콘은 그보다 124 위에 있다. 간선을 카드 원점끼리 이으면 선이
    ///   아이콘이 아니라 이름표 언저리에 붙어, 무엇과 무엇이 이어졌는지가 안 보인다.
    ///
    ///   ⚠ RelicTreePopupCreator 의 Face anchoredPosition.y 와 같은 값이어야 한다 —
    ///     Creator 가 이 상수를 직접 쓴다.
    /// </summary>
    public const float FaceOffsetY = 124f;

    // ── 계열 색 ───────────────────────────────────────────────
    public static readonly Color RootColor    = new Color(0.85f, 0.70f, 0.22f, 1f);
    public static readonly Color AttackColor  = new Color(0.88f, 0.41f, 0.24f, 1f);
    public static readonly Color DefenseColor = new Color(0.31f, 0.60f, 0.77f, 1f);
    public static readonly Color SoldierColor = new Color(0.78f, 0.58f, 0.22f, 1f);
    public static readonly Color UtilityColor = new Color(0.60f, 0.47f, 0.82f, 1f);

    // ⚠ 계열이 바뀌었다 (2026-09-07) — 색 이름은 그대로 두고 짝만 옮겼다
    //   AttackColor(붉은) → 소환수 · DefenseColor(푸른) → 마왕성
    //   SoldierColor(금) → 마나  · UtilityColor(보라) → 통솔
    //   ⚠ 색 상수 이름까지 바꾸면 Creator·문서를 함께 고쳐야 해서 그대로 뒀다.
    public static Color ColorOf(RelicBranch b) => b switch
    {
        RelicBranch.Brood    => AttackColor,
        RelicBranch.Keep     => DefenseColor,
        RelicBranch.Font     => SoldierColor,
        RelicBranch.Dominion => UtilityColor,
        _                    => RootColor,
    };

    static readonly Color FogColor  = new Color(0.30f, 0.34f, 0.42f, 1f);
    static readonly Color EdgeDim   = new Color(0.22f, 0.24f, 0.34f, 1f);
    static readonly Color CostOk    = new Color(1f, 0.86f, 0.40f, 1f);
    static readonly Color CostShort = new Color(1f, 0.48f, 0.45f, 1f);

    // ── 노드 뷰 ───────────────────────────────────────────────
    class NodeView
    {
        public RelicNodeDef   Def;
        public RectTransform  Root;
        public GameObject     Ghost;
        public GameObject     Edge;
        public Image          Face;
        public Image          Icon;

        /// <summary>이 노드에 실제 그림이 붙었는가 (아직 안 만든 노드는 계열 색 타일로 남는다).</summary>
        public bool           HasIcon;
        public TextMeshProUGUI Name;
        public Image[]        Pips;
        public Button         Buy;
        public GameObject     BuyRoot;
        public TextMeshProUGUI Cost;
    }

    readonly Dictionary<RelicNodeId, NodeView> _views = new();

    // ── 캐시 ──────────────────────────────────────────────────
    RelicTreeData     _tree;
    ReincarnationData _reinc;
    StageProgressData _stage;
    Canvas            _canvas;
    float             _zoom = 1f;
    RelicNodeId       _selected = RelicNodeId.None;
    float             _pinchPrev;

    // ── 생명주기 ──────────────────────────────────────────────

    // ⚠ 여는 시점마다 다시 묶는다 (Awake 가 아니라)
    //   PopupManager 는 닫힌 팝업을 풀에 넣어 재사용한다 — Awake 는 한 번뿐이다.
    protected override void OnBeforeOpen()
    {
        _tree   = UserDataManager.Instance.Get<RelicTreeData>();
        _reinc  = UserDataManager.Instance.Get<ReincarnationData>();
        _stage  = UserDataManager.Instance.Get<StageProgressData>();
        _canvas = GetComponentInParent<Canvas>();

        _reincarnateBtn.onClick.RemoveAllListeners();
        _reincarnateBtn.onClick.AddListener(OnReincarnate);
        _resetBtn.onClick.RemoveAllListeners();
        _resetBtn.onClick.AddListener(OnReset);
        _closeBtn.onClick.RemoveAllListeners();
        _closeBtn.onClick.AddListener(() => Close());

        EnsureBuilt();
        HideTooltip();
        CenterOnRoot();
        Refresh();
    }

    // ══════════════════════════════════════════════════════════
    //  트리 생성 (최초 1회)
    // ══════════════════════════════════════════════════════════

    void EnsureBuilt()
    {
        if (_views.Count > 0) return;

        // 간선을 먼저 전부 깔고 노드를 얹는다 — 같은 부모 아래에서는 나중 형제가 위로
        // 그려지므로, 순서를 섞으면 선이 아이콘을 가로지른다.
        foreach (var def in RelicTreeCatalog.All)
        {
            if (def.Parent == RelicNodeId.None) continue;
            var edge = Instantiate(_edgeTemplate, _content);
            edge.name = $"Edge_{def.Id}";
            edge.SetActive(true);
            PlaceEdge(edge.GetComponent<RectTransform>(), RelicTreeCatalog.Get(def.Parent), def);
            _edgeByNode[def.Id] = edge;
        }

        foreach (var def in RelicTreeCatalog.All)
        {
            var view = BuildNode(def);
            _views[def.Id] = view;
        }

        // 콘텐츠 크기 — 팬 제한(ClampPan)이 이 값을 본다
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var d in RelicTreeCatalog.All)
        {
            if (d.X < minX) minX = d.X;
            if (d.X > maxX) maxX = d.X;
            if (d.Y < minY) minY = d.Y;
            if (d.Y > maxY) maxY = d.Y;
        }
        _content.sizeDelta = new Vector2((maxX - minX + 4) * Spacing, (maxY - minY + 4) * Spacing);
    }

    readonly Dictionary<RelicNodeId, GameObject> _edgeByNode = new();

    NodeView BuildNode(RelicNodeDef def)
    {
        var go = Instantiate(_nodeTemplate, _content);
        go.name = $"Node_{def.Id}";
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = PosOf(def);

        var v = new NodeView
        {
            Def     = def,
            Root    = rt,
            Face    = go.transform.Find("Face").GetComponent<Image>(),
            Icon    = go.transform.Find("Face/Icon").GetComponent<Image>(),
            Name    = go.transform.Find("Name").GetComponent<TextMeshProUGUI>(),
            Buy     = go.transform.Find("BuyBtn").GetComponent<Button>(),
            BuyRoot = go.transform.Find("BuyBtn").gameObject,
            Cost    = go.transform.Find("BuyBtn/Body/Cost").GetComponent<TextMeshProUGUI>(),
            Edge    = _edgeByNode.TryGetValue(def.Id, out var e) ? e : null,
        };

        var pipRoot = go.transform.Find("Pips");
        v.Pips = pipRoot.GetComponentsInChildren<Image>(true);

        // 마름모(특수 노드) — 레벨이 없는 한 방 노드라 형태로 구분한다.
        // ⚠ 아이콘은 반대로 되돌린다 — localRotation 은 부모 회전에 얹힌다
        //   identity 로 두면 부모를 따라 45° 기운 그림이 된다.
        if (def.Special)
        {
            v.Face.rectTransform.localRotation = Quaternion.Euler(0f, 0f,  45f);
            v.Icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }

        var c = ColorOf(def.Branch);
        v.Face.color = c;
        v.Name.text  = def.DisplayName;

        // ── 노드 그림 ────────────────────────────────────────
        //  파일명 규칙은 RelicIconKey 하나가 갖는다 (N_Blade → "node_blade").
        //  아틀라스에 그 이름이 없으면 그림 없이 계열 색 타일로 남는다 —
        //  전부를 한꺼번에 그리지 않아도 화면이 깨지지 않아야 한다.
        var sprite = SpriteManager.Instance?.Get(RelicIconKey.Of(def.Id));
        v.HasIcon    = sprite != null;
        v.Icon.sprite = sprite;

        // 그림이 없으면 계열 색으로 칠한 빈 사각형 = 예전과 같은 모습.
        // 그림이 있으면 원색 그대로 보여야 하므로 색을 빼앗지 않는다 (Refresh 가 밝기만 만진다).
        v.Icon.color = v.HasIcon ? Color.white : c;

        var id = def.Id;
        v.Face.GetComponent<Button>().onClick.AddListener(() => Select(id));
        v.Buy.onClick.AddListener(() => TryLevelUp(id));

        // 안개 실루엣 — 노드와 같은 자리에 겹쳐 두고 둘 중 하나만 켠다
        var ghost = Instantiate(_ghostTemplate, _content);
        ghost.name = $"Ghost_{def.Id}";
        ghost.GetComponent<RectTransform>().anchoredPosition = IconPosOf(def);
        ghost.SetActive(false);
        v.Ghost = ghost;

        return v;
    }

    /// <summary>카드(노드 루트)가 놓이는 자리.</summary>
    static Vector2 PosOf(RelicNodeDef d) => new Vector2(d.X * Spacing, d.Y * Spacing);

    /// <summary>아이콘 판의 자리 — 간선과 실루엣은 이쪽에 맞춘다.</summary>
    static Vector2 IconPosOf(RelicNodeDef d) => PosOf(d) + new Vector2(0f, FaceOffsetY);

    static void PlaceEdge(RectTransform rt, RelicNodeDef from, RelicNodeDef to)
    {
        Vector2 a = IconPosOf(from), b = IconPosOf(to);
        Vector2 mid = (a + b) * 0.5f;
        Vector2 dir = b - a;

        rt.anchoredPosition = mid;
        rt.sizeDelta        = new Vector2(dir.magnitude, rt.sizeDelta.y);
        rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // ══════════════════════════════════════════════════════════
    //  갱신
    // ══════════════════════════════════════════════════════════

    public void Refresh()
    {
        var levels = _tree.Levels;
        int points = _reinc.ReincarnationPoints;

        _pointText.text = points.ToString();
        _summaryText.text =
            LocalizationManager.Instance.Format("찍은 노드 {0} / {1}   ·   투자 {2}pt",
                _tree.TakenCount, RelicTreeCatalog.All.Length, _tree.InvestedPoints);

        foreach (var v in _views.Values)
        {
            var def   = v.Def;
            int lv    = _tree.GetLevel(def.Id);
            bool vis  = RelicTreeCatalog.IsVisible(def.Id, levels);
            bool gh   = RelicTreeCatalog.IsSilhouette(def.Id, levels);

            v.Root.gameObject.SetActive(vis);
            v.Ghost.SetActive(gh);

            if (v.Edge != null)
            {
                bool parentVis = RelicTreeCatalog.IsVisible(def.Parent, levels);
                v.Edge.SetActive((vis || gh) && parentVis);
                v.Edge.GetComponent<Image>().color = lv > 0 ? ColorOf(def.Branch) : EdgeDim;
            }

            if (!vis) continue;

            // 찍은 노드는 면을 채우고, 아직 안 찍은 노드는 테두리만 남긴다
            var c = ColorOf(def.Branch);
            v.Face.color = lv > 0 ? c : new Color(c.r, c.g, c.b, 0.35f);
            v.Name.color = lv > 0 ? Color.white : new Color(0.78f, 0.80f, 0.86f, 1f);

            // 아직 안 찍은 노드는 그림도 같이 죽인다 — 판만 흐려지면 그림이 떠 보인다.
            // 그림이 없는 노드는 판과 같은 색이라 여기서 따로 만지지 않는다.
            if (v.HasIcon)
                v.Icon.color = lv > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);

            for (int i = 0; i < v.Pips.Length; i++)
            {
                bool used = i < def.MaxLevel && def.MaxLevel > 1;
                v.Pips[i].gameObject.SetActive(used);
                if (used) v.Pips[i].color = i < lv ? c : new Color(1f, 1f, 1f, 0.18f);
            }

            bool maxed = lv >= def.MaxLevel;
            v.BuyRoot.SetActive(!maxed);
            if (!maxed)
            {
                int cost = def.LevelUpCost(lv);
                v.Cost.text  = cost.ToString();
                v.Cost.color = points >= cost ? CostOk : CostShort;
                v.Buy.interactable = points >= cost;
            }
        }

        UpdateReincarnateBtn();
        if (_selected != RelicNodeId.None) ShowTooltip(_selected);

        // 노드 버튼은 EnsureBuilt 에서 한 번 만들어지므로 여기서 다시 걸어도 중복은 걸러진다
        UIClickSfx.Bind(gameObject);
    }

    /// <summary>
    /// 환생 버튼은 조건이 될 때만 존재한다.
    ///
    /// ⚠ "N스테이지부터 환생 가능" 을 띄우지 않는다
    ///   대부분의 시간 동안 <b>누를 수 없는 버튼</b>이 자리만 차지하고,
    ///   조건을 읽어도 지금 할 수 있는 일이 없다. 조건이 되면 버튼이 나타나는 편이
    ///   "지금 누를 수 있다" 를 훨씬 분명하게 말한다. (구 RelicPopup 도 같은 이유로
    ///   하단 200px 을 먹던 비활성 버튼을 걷어냈다)
    /// </summary>
    void UpdateReincarnateBtn()
    {
        int  cleared  = _stage.ClearedNormalStages;
        bool canReinc = ReincarnationData.CanReincarnate(cleared);

        _reincarnateBtn.gameObject.SetActive(canReinc);
        if (!canReinc) return;

        int pts = ReincarnationData.PreviewPoints(cleared);   // 난이도 배율 포함
        _reincLabel.text = LocalizationManager.Instance.Format("환생 — {0}pt 획득", pts);
    }

    // ══════════════════════════════════════════════════════════
    //  툴팁 · 강화
    // ══════════════════════════════════════════════════════════

    void Select(RelicNodeId id)
    {
        _selected = id;
        ShowTooltip(id);
    }

    void ShowTooltip(RelicNodeId id)
    {
        var def = RelicTreeCatalog.Get(id);
        int lv  = _tree.GetLevel(id);

        _tooltipRoot.SetActive(true);

        // ⚠ 트리 노드와 **같은 키**로 찾는다 (RelicIconKey) — 두 곳이 다른 그림을
        //   쓰면 "누른 것과 카드에 뜬 것이 다르다" 가 된다.
        //   ⚠ 그림이 없으면 칸만 끈다. 자리는 프리팹이 비워 두고 있어 글은 안 움직인다.
        if (_tipIcon != null)
        {
            _tipIcon.sprite  = SpriteManager.Instance?.Get(RelicIconKey.Of(def.Id));
            _tipIcon.enabled = _tipIcon.sprite != null;
        }

        _tipName.text  = def.DisplayName;
        _tipName.color = ColorOf(def.Branch);

        string lvText = def.Special ? "단일 습득" : LocalizationManager.Instance.Format("{0} / {1} 레벨", lv, def.MaxLevel);
        string parent = def.Parent == RelicNodeId.None
            ? "시작 노드"
            : LocalizationManager.Instance.Format("선행 {0}",
                                                  RelicTreeCatalog.Get(def.Parent).DisplayName);
        // ⚠ 두 줄로 — 한 줄이면 선행 이름이 길 때 카드 폭을 넘는다 (RelicTreePopupCreator 의 TipSub 주석)
        _tipSub.text = LocalizationManager.Instance.Format("티어 {0}  ·  {1}\n{2}", def.Tier, lvText, parent);

        // 0레벨이면 1레벨 미리보기가 뜬다 — 뭐가 붙는지 봐야 살지 말지 정한다
        _tipEffect.text = def.GetDescription(lv);

        _tipCost.text = lv >= def.MaxLevel
            ? "최대 레벨"
            : LocalizationManager.Instance.Format("다음 레벨 {0}pt   (만렙까지 {1}pt)", def.LevelUpCost(lv), def.TotalCost);
    }

    void HideTooltip()
    {
        _selected = RelicNodeId.None;
        _tooltipRoot.SetActive(false);
    }

    void TryLevelUp(RelicNodeId id)
    {
        var def  = RelicTreeCatalog.Get(id);
        int lv   = _tree.GetLevel(id);
        int cost = def.LevelUpCost(lv);

        if (lv >= def.MaxLevel) return;
        if (!_tree.IsUnlocked(id)) return;
        if (!_reinc.TrySpendPoints(cost)) return;

        // ⚠ 클릭 지점을 Refresh 전에 잡아 둔다 — 연출이 손가락 자리에서 터져야 한다
        Vector2 click = UIJuice.CapturePointer();

        _tree.LevelUp(id);
        UserDataManager.Instance.RequestSave();
        Refresh();

        // 노드는 파괴되지 않으므로 참조를 그대로 쓴다 (구 RelicPopup 과 다른 점)
        UIJuice.RelicUp(_views[id].Root, _tree.GetLevel(id), click);
    }

    // ══════════════════════════════════════════════════════════
    //  이동 · 확대
    // ══════════════════════════════════════════════════════════

    // ── 툴팁은 다른 조작이 들어오면 즉시 닫는다 ────────────────
    //
    //  ⚠ 이 핸들러들은 노드를 눌렀을 때는 돌지 않는다
    //    노드 판(Face)·강화 버튼이 Button 이라 클릭을 거기서 소비한다.
    //    즉 OnPointerClick 은 "빈 배경을 눌렀다" 는 뜻이고, 드래그·휠·핀치는
    //    트리를 움직이는 조작이다 — 어느 쪽이든 설명을 띄워 둘 이유가 없다.
    //
    //  ⚠ OnDrag 가 아니라 OnBeginDrag 에서 닫는다
    //    OnDrag 는 프레임마다 돈다. 이미 닫힌 툴팁을 계속 끄면 Refresh 가
    //    선택을 되살리는 자리(_selected)를 매 프레임 건드리게 된다.

    public void OnBeginDrag(PointerEventData e) => HideTooltip();

    public void OnPointerClick(PointerEventData e) => HideTooltip();

    public void OnDrag(PointerEventData e)
    {
        // ⚠ delta 는 스크린 픽셀이고 content 는 캔버스 단위다
        //   scaleFactor 로 나누지 않으면 해상도가 다른 기기에서 손가락보다
        //   빠르거나 느리게 끌린다.
        _content.anchoredPosition += e.delta / _canvas.scaleFactor;
        ClampPan();
    }

    public void OnScroll(PointerEventData e)
    {
        HideTooltip();
        ZoomAt(e.position, _zoom * (e.scrollDelta.y > 0f ? 1.12f : 1f / 1.12f));
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Input.touchCount != 2) { _pinchPrev = 0f; return; }

        Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
        float dist = Vector2.Distance(a.position, b.position);
        if (_pinchPrev > 0f && dist > 0f)
        {
            // 두 손가락이 닿은 순간(_pinchPrev == 0)에는 아직 안 닫는다 —
            // 확대가 실제로 일어나는 프레임부터 닫아야 살짝 스친 손가락에 안 꺼진다.
            HideTooltip();
            ZoomAt((a.position + b.position) * 0.5f, _zoom * (dist / _pinchPrev));
        }
        _pinchPrev = dist;
    }

    /// <summary>
    /// 스크린 좌표를 UI 로컬 좌표로 바꿀 때 쓸 카메라.
    ///
    /// ⚠ Overlay 캔버스에서는 반드시 null 이어야 한다
    ///   worldCamera 를 그대로 넘기면 Overlay 에서 좌표가 어긋나 확대 중심이 튄다.
    ///   반대로 ScreenSpaceCamera 에서 null 을 넘겨도 같은 증상이 난다.
    /// </summary>
    Camera EventCam => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    void ZoomAt(Vector2 screenPos, float want)
    {
        float next = Mathf.Clamp(want, _zoomMin, _zoomMax);
        if (Mathf.Approximately(next, _zoom)) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPos, EventCam, out var local);

        // 손가락(커서) 아래에 있던 트리 좌표가 그대로 있게 팬을 되민다
        Vector2 anchor = (local - _content.anchoredPosition) / _zoom;
        _zoom = next;
        _content.localScale       = Vector3.one * _zoom;
        _content.anchoredPosition = local - anchor * _zoom;
        ClampPan();
    }

    void CenterOnRoot()
    {
        _zoom = Mathf.Clamp(_zoomInit, _zoomMin, _zoomMax);
        _content.localScale       = Vector3.one * _zoom;
        _content.anchoredPosition = Vector2.zero;   // 뿌리가 (0,0) 이라 그대로 중앙이다
    }

    /// <summary>트리가 화면 밖으로 완전히 빠져나가지 않게 팬을 가둔다.</summary>
    void ClampPan()
    {
        Vector2 half = _content.sizeDelta * 0.5f * _zoom;
        Vector2 view = _viewport.rect.size * 0.5f;
        Vector2 lim  = new Vector2(Mathf.Max(0f, half.x - view.x * 0.35f),
                                   Mathf.Max(0f, half.y - view.y * 0.35f));
        var p = _content.anchoredPosition;
        _content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -lim.x, lim.x),
                                                Mathf.Clamp(p.y, -lim.y, lim.y));
    }

    // ══════════════════════════════════════════════════════════
    //  환생 · 초기화
    // ══════════════════════════════════════════════════════════

    void OnReincarnate()
    {
        int cleared = _stage.ClearedNormalStages;
        if (!ReincarnationData.CanReincarnate(cleared)) return;

        UserDataManager.Instance.Reincarnate();
        Refresh();
    }

    // 전액 환불이라 손해도 이득도 없다 — 트리는 한 번 잘못 타면 되돌릴 방법이
    // 없으므로 재분배가 공짜여야 한다.
    void OnReset()
    {
        int refund = _tree.ResetAll();
        _reinc.EarnPoints(refund);
        UserDataManager.Instance.RequestSave();
        HideTooltip();
        Refresh();
    }
}
