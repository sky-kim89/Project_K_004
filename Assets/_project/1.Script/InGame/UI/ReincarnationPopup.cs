using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  ReincarnationPopup.cs
//  패배 시 환생 전용 팝업.
//  초상화·스탯바를 포함한 런 요약 화면 (헤더 밴드 · 섹션 라벨 · 음각 탭).
//
//  레이아웃 (위 → 아래) — 실제 좌표는 ReincarnationPopupCreator 참조:
//    헤더 밴드: "패  배" + 웨이브·처치(좌) / 총피해·DPS(우)
//    "전투 기록" 섹션 → 딜·탱·힐 탭 → 장수별 StatBar 목록(VScroll)
//    포인트 패널 (보유 › 획득 › 환생 후) / 환생 버튼
//    오른쪽 칸 '이번 런' — 보유 특성 아이콘 격자 · 덱 카드 8칸 (2026-09-16)
// ============================================================

public class ReincarnationPopup : PopupBase
{
    public override bool BlockBackgroundClose => true;

    [SerializeField] TextMeshProUGUI _subText;
    [SerializeField] TextMeshProUGUI _statsText;

    [Header("통계 탭")]
    [SerializeField] Button[] _tabButtons;            // 0=딜, 1=탱, 2=힐
    [SerializeField] Image[]  _tabButtonBgs;          // 탭 하단 강조바 (활성만 색을 켠다)

    [Header("장수 목록")]
    [SerializeField] Transform        _generalArea;          // ScrollRect content (VLG)
    [SerializeField] GeneralStatRowUI _generalRowTemplate;   // 비활성 프리팹

    [Header("환생 포인트")]
    [SerializeField] TextMeshProUGUI _currentPtsText;
    [SerializeField] TextMeshProUGUI _earnPtsText;
    [SerializeField] TextMeshProUGUI _totalPtsText;
    [SerializeField] Button          _reincarnateBtn;

    // ── 이번 런 — 특성 · 덱 (사용자 요청, 2026-09-16) ─────────
    //
    //  ■ "무엇으로 여기까지 왔나" 를 결산에서 본다
    //    카드와 특성은 FinishRun 에서 지워진다. 그 전에 볼 곳이 이 창뿐인데
    //    전투 기록(딜·탱·힐)만 있어, 어떤 빌드였는지 되짚을 수가 없었다.
    //  ⚠ 특성 목록은 RunPerkBarUI.Collect 가 정본이다 — HUD 줄과 같은 것을 같은 순서로 세운다.
    //  ⚠ 덱 칸을 누르면 몬스터 상세(런 값 모드)가 열린다 — 전황 창과 같은 규칙. 여기서 다시 그리지 않는다.

    [Serializable]
    public class DeckCell
    {
        public GameObject      Root;
        public Button          Button;
        public Image           Portrait;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI GradeText;
    }

    [Header("이번 런 — 특성")]
    [SerializeField] Transform   _perkArea;       // 격자 content
    [SerializeField] TraitIconUI _perkTemplate;   // 비활성 — 런타임에 복제한다 (칸 수가 런마다 다르다)
    [SerializeField] GameObject  _perkEmpty;      // "주운 특성이 없다"
    [Tooltip("제단 표식 그림. ⚠ MonsterSynergyRule.AllTags 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[]    _synergyIcons;

    [Header("이번 런 — 덱")]
    [SerializeField] DeckCell[]  _deckCells;

    int           _earnPoints;
    CombatStatTab _currentTab = CombatStatTab.Damage;
    Action        _onReincarnated;
    BattleContext _context;
    readonly List<GeneralStatRowUI>     _generalRows = new();
    readonly List<TraitIconUI>          _perkIcons   = new();
    readonly List<RunPerkBarUI.Entry>   _perkEntries = new();
    readonly List<SummonDeckSlot>       _deck        = new();

    // 색은 ReincarnationPopupCreator 의 팔레트와 맞춰 둔다.
    static readonly Color TabActiveColor = new Color(0.42f, 0.62f, 1.00f);

    // ── PopupBase 훅 ─────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _reincarnateBtn.onClick.AddListener(OnReincarnate);

        if (_tabButtons == null) return;
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            if (_tabButtons[i] == null) continue;
            int captured = i;
            _tabButtons[i].onClick.AddListener(() => OnTabClicked((CombatStatTab)captured));
        }
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 런이 끝났다 — 결산과 환생 포인트를 보여 준다.
    /// </summary>
    /// <param name="onReincarnated">
    /// 환생 버튼을 눌렀을 때 갈 곳 — RunBootstrap 이 제 대기 루프를 깨우는 마무리를 넘긴다.
    /// </param>
    public void Setup(BattleContext context, int killCount, Action onReincarnated)
    {
        _context        = context;
        _currentTab     = CombatStatTab.Damage;
        _onReincarnated = onReincarnated;

        int   currentWave = context?.StageNumber ?? 0;   // 웨이브 없음 — 스테이지로 표기
        int   totalWaves  = context?.StageNumber ?? 0;
        float elapsedSec  = context?.BattleElapsedSeconds ?? 0f;

        // ⚠ 도달 스테이지가 이 화면의 첫 줄이다
        //   패배 화면에서 플레이어가 가장 먼저 확인하는 것은 "어디까지 갔나" 인데
        //   웨이브·처치만 있고 스테이지가 어디에도 없었다. 환생 포인트도 여기서
        //   갈리므로(스테이지 기반) 같은 줄에 있어야 납득이 된다.
        int stage = context?.StageNumber ?? 0;
        _subText.text = stage > 0
            ? LocalizationManager.Instance.Format("도달 스테이지 {0}  ·  웨이브 {1} / {2}  ·  처치 {3}명",
                                                   stage, currentWave, totalWaves, killCount)
            : LocalizationManager.Instance.Format("웨이브 {0} / {1}  ·  처치 {2}명",
                                                   currentWave, totalWaves, killCount);

        float totalDmg = 0f;
        if (context?.CombatStats != null && context.CombatStats.Count > 0)
            foreach (var e in context.CombatStats) totalDmg += e.TotalDamageDealt;
        else if (BattleStatsTracker.Instance != null)
            foreach (var e in BattleStatsTracker.Instance.GetAllEntries()) totalDmg += e.TotalDamageDealt;

        float dps = elapsedSec > 0f ? totalDmg / elapsedSec : 0f;
        _statsText.text = LocalizationManager.Instance.Format("총 피해  {0}  |  DPS  {1}",
                                                              FormatNum(totalDmg), FormatNum(dps));

        BuildGeneralRows(context);
        RefreshTabHighlight();

        BuildPerks();
        BuildDeck();

        var reincarData = UserDataManager.Instance?.Get<ReincarnationData>();
        var progress    = UserDataManager.Instance?.Get<StageProgressData>();
        int cleared     = progress?.ClearedNormalStages ?? 0;
        // 난이도 배율까지 들어간 실제 지급값 — 지급하는 쪽과 같은 함수여야 한다
        _earnPoints     = ReincarnationData.PreviewPoints(cleared);
        int current     = reincarData?.ReincarnationPoints ?? 0;

        _currentPtsText.text = LocalizationManager.Instance.Format("보유  {0} pt", current);
        _earnPtsText.text    = $"+{_earnPoints} pt";
        _totalPtsText.text   = LocalizationManager.Instance.Format("환생 후  {0} pt", current + _earnPoints);
    }

    // ── 장수 행 ───────────────────────────────────────────────

    void BuildGeneralRows(BattleContext context)
    {
        foreach (var row in _generalRows)
            if (row != null) Destroy(row.gameObject);
        _generalRows.Clear();

        if (_generalArea == null || _generalRowTemplate == null) return;

        IEnumerable<GeneralStatEntry> entries =
            (context?.CombatStats != null && context.CombatStats.Count > 0)
                ? (IEnumerable<GeneralStatEntry>)context.CombatStats
                : BattleStatsTracker.Instance?.GetAllEntries();

        if (entries == null) return;

        float elapsedSec = context?.BattleElapsedSeconds ?? 0f;
        float maxValue   = CalcMaxValue(_currentTab);
        foreach (var e in entries)
        {
            var row = Instantiate(_generalRowTemplate, _generalArea);
            row.gameObject.SetActive(true);

            // ⚠ 이름이 아니라 항목을 통째로 넘긴다
            //   이 게임의 통계 단위는 카드라 UnitData 에 이름이 없다.
            //   항목이 제 그림(합성 초상화·스킬 아이콘·소환사 외형)을 들고 온다.
            row.Setup(e);
            row.SetStats(e);
            row.SetDPS(elapsedSec);
            row.RefreshTab(_currentTab, maxValue);
            _generalRows.Add(row);
        }
    }

    // ── 이번 런 — 특성 ────────────────────────────────────────

    void BuildPerks()
    {
        foreach (var icon in _perkIcons) Destroy(icon.gameObject);
        _perkIcons.Clear();

        _perkEntries.Clear();
        RunPerkBarUI.Collect(_perkEntries, UserDataManager.Instance.Get<RunPerkData>(), _synergyIcons);

        foreach (RunPerkBarUI.Entry e in _perkEntries)
        {
            TraitIconUI icon = Instantiate(_perkTemplate, _perkArea);
            icon.gameObject.SetActive(true);
            icon.SetupCustom(e.Icon, e.Title, e.Desc);
            _perkIcons.Add(icon);
        }

        _perkEmpty.SetActive(_perkEntries.Count == 0);
    }

    // ── 이번 런 — 덱 ──────────────────────────────────────────

    void BuildDeck()
    {
        _deck.Clear();

        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;
            _deck.Add(slot);
        }

        var codex   = UserDataManager.Instance.Get<MonsterCodexData>();
        var catalog = CardCatalog.Current;

        for (int i = 0; i < _deckCells.Length; i++)
        {
            DeckCell cell = _deckCells[i];
            bool     has  = i < _deck.Count;
            cell.Root.SetActive(has);
            if (!has) continue;

            SummonDeckSlot     slot = _deck[i];
            MonsterSpeciesData sp   = catalog.GetMonster(slot.Id);

            cell.Portrait.sprite  = MonsterPortraitProvider.Get(sp);
            cell.Portrait.enabled = cell.Portrait.sprite != null;
            cell.NameText.text    = sp.DisplayName;

            UnitGrade g = codex.IsUnlocked(sp.Id) ? codex.GetGrade(sp.Id) : UnitGrade.Normal;
            cell.LevelText.text  = $"Lv {slot.Level}";
            cell.GradeText.text  = LocalizationManager.Instance.Get(g.ToString());
            cell.GradeText.color = GradeStyle.GetColor(g);   // 도감·전황과 같은 등급색

            int captured = i;
            cell.Button.onClick.RemoveAllListeners();
            cell.Button.onClick.AddListener(() => ShowDeckDetail(captured));
        }
    }

    /// <summary>덱 칸 → 몬스터 상세(런 값 모드). 전황 창(BattleInfoPopup)과 같은 창을 연다.</summary>
    void ShowDeckDetail(int index)
    {
        SummonDeckSlot     slot = _deck[index];
        MonsterSpeciesData sp   = CardCatalog.Current.GetMonster(slot.Id);

        PopupManager.Instance.Open<MonsterDetailPopup>(PopupType.MonsterDetail).SetupRun(sp, slot);
    }

    // ── 탭 전환 ──────────────────────────────────────────────

    void OnTabClicked(CombatStatTab tab)
    {
        if (_currentTab == tab) return;
        _currentTab = tab;
        RefreshTabHighlight();
        RefreshAllRowBars();
    }

    // _tabButtonBgs 는 각 탭 하단의 강조바다 — 활성 탭만 색을 켠다.
    // (버튼 면 색을 바꾸면 입체 버튼의 모서리 색이 따라오지 않아 어긋난다)
    void RefreshTabHighlight()
    {
        if (_tabButtonBgs == null) return;
        for (int i = 0; i < _tabButtonBgs.Length; i++)
        {
            if (_tabButtonBgs[i] == null) continue;
            _tabButtonBgs[i].color = (int)_currentTab == i ? TabActiveColor : Color.clear;
        }
    }

    void RefreshAllRowBars()
    {
        float maxValue = CalcMaxValue(_currentTab);
        foreach (var row in _generalRows)
            if (row != null) row.RefreshTab(_currentTab, maxValue);
    }

    float CalcMaxValue(CombatStatTab tab)
    {
        IEnumerable<GeneralStatEntry> entries =
            (_context?.CombatStats != null && _context.CombatStats.Count > 0)
                ? (IEnumerable<GeneralStatEntry>)_context.CombatStats
                : BattleStatsTracker.Instance?.GetAllEntries();

        if (entries == null) return 1f;

        float max = 0f;
        foreach (var s in entries)
        {
            float v = tab switch
            {
                CombatStatTab.Damage => s.TotalDamageDealt,
                CombatStatTab.Tank   => s.DamageTaken + s.SoldierDamageTaken + s.DamageAbsorbed,
                CombatStatTab.Heal   => s.HealingDone,
                _                    => 0f,
            };
            if (v > max) max = v;
        }
        return max > 0f ? max : 1f;
    }

    // ── 환생 버튼 ─────────────────────────────────────────────

    /// <summary>
    /// 환생 실행 — 초기화 내용은 **UserDataManager.Reincarnate() 하나가 소유한다.**
    ///
    /// ⚠ 여기서 직접 섹션을 지우지 말 것
    ///   예전엔 이 팝업이 초기화 목록을 따로 들고 있었다. 유물 화면의 '즉시 환생'
    ///   (UserDataManager.Reincarnate)과 목록이 갈라져 실제로 이런 차이가 났다:
    ///     · ItemData 를 안 지워 **장비 강화석·용병조각이 환생해도 계속 쌓였다**
    ///       (골드만 0 으로 만들고 있었다)
    ///     · RunShopData · RunEventBonusData 가 남아 상점 재고와 이벤트 보너스가 이어졌다
    ///     · AutoDeployFirstHeroIfNeeded 가 없어 첫 장수가 배치되지 않았다
    ///   환생 경로가 둘이면 반드시 또 갈라진다 — 목록은 한 곳에만 둔다.
    /// </summary>
    void OnReincarnate()
    {
        var udm = UserDataManager.Instance;

        udm?.Reincarnate();

        // SaveAll: 씬 전환 전 즉시 저장 (RequestSave는 다음 프레임 실행 → 씬 전환 시 누락 가능)
        udm?.SaveAll();

        Close(_onReincarnated);
    }

    static string FormatNum(float v)
    {
        if (v >= 1_000_000f) return $"{v / 1_000_000f:0.0}M";
        if (v >= 1_000f)     return $"{v / 1_000f:0.0}K";
        return $"{(int)v}";
    }
}
