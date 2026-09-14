using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  BattleStatsPopup.cs
//  전투 통계 — 카드 3택 위에서 "방금 판이 어땠나" 를 보는 화면.
//
//  ■ 화면은 새로 만들지 않았다 (2026-08-28)
//    원작의 전투 기록 화면을 그대로 쓴다 —
//      CombatStatTab      딜 / 탱 / 힐 탭
//      GeneralStatRowUI   한 줄 (초상화 · 이름 · 세그먼트 바 · 총량 · DPS · 범례)
//      StatBarUI          세그먼트 프로그레스 바
//    이 파일이 하는 일은 **어떤 목록을 넘길지 고르는 것뿐**이다.
//    ReincarnationPopup 의 전투 기록 절과 같은 코드·같은 모양이라,
//    한쪽을 고치면 다른 쪽도 같이 좋아진다.
//
//  ■ 통계의 단위는 카드다
//    플레이어가 고르고 키우는 단위가 카드다. "슬라임을 5레벨까지 올린 게
//    값어치가 있었나" 를 판단할 수 있어야 다음 3택이 선택이 된다.
//    개체 단위로 세면 물량 종족이 무조건 1등이라 아무것도 못 읽는다 —
//    그래서 **카드에 합산**한다 (분열체·부활체도 원본 카드에 달린다).
//
//    카드 집계(CardStatsTracker)를 원작 화면이 읽는 판(GeneralStatEntry)으로
//    옮기는 것은 CardStatsTracker.BuildStatEntries 가 한다.
//
//  ■ 이번 **스테이지**의 통계다 (런 누적이 아니다)
//    (CardStatsTracker 는 스테이지 준비마다 비워진다)
//
//  ■ ⚠ 스킬 카드 수치에는 오차가 있을 수 있다
//    스킬 피해는 전부 소환사 이름으로 들어오므로 시전 창으로 카드를 되짚는다.
//    장판 둘이 겹치면 나중에 깐 쪽으로 몰릴 수 있다 —
//    자세한 사정은 CardStatsTracker 파일 머리 참고.
// ============================================================

public class BattleStatsPopup : PopupBase
{
    [Header("헤더")]
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _totalText;
    [SerializeField] Button          _closeButton;

    [Header("딜 · 탱 · 힐 탭")]
    [SerializeField] Button[] _tabButtons;     // 0=딜, 1=탱, 2=힐
    [SerializeField] Image[]  _tabButtonBgs;   // 탭 하단 강조바 (활성만 색을 켠다)

    [Header("목록")]
    [SerializeField] Transform        _rowArea;       // ScrollRect content (VLG)
    [SerializeField] GeneralStatRowUI _rowTemplate;   // 비활성 프리팹
    [SerializeField] TextMeshProUGUI  _emptyText;

    readonly List<GeneralStatRowUI> _rows = new();
    List<GeneralStatEntry>          _entries = new();

    CombatStatTab _currentTab = CombatStatTab.Damage;
    float         _elapsedSec;

    /// <summary>활성 탭 강조 색 — ReincarnationPopup·BattleResultPopup 과 같은 값.</summary>
    static readonly Color TabActiveColor = new(0.42f, 0.62f, 1.00f);

    // ── 수명 ─────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        if (_tabButtons == null) return;

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            if (_tabButtons[i] == null) continue;
            int captured = i;
            _tabButtons[i].onClick.AddListener(() => OnTabClicked((CombatStatTab)captured));
        }
    }

    // ── 공개 API ─────────────────────────────────────────────

    public BattleStatsPopup Setup()
    {
        _currentTab = CombatStatTab.Damage;
        _entries    = CardStatsTracker.Instance.BuildStatEntries(CardCatalog.Current);
        _elapsedSec = BattleManager.Instance?.Context?.BattleElapsedSeconds ?? 0f;

        float total = 0f;
        foreach (GeneralStatEntry e in _entries) total += e.TotalDamageDealt;

        float dps = _elapsedSec > 0f ? total / _elapsedSec : 0f;

        if (_titleText != null) _titleText.text = "전투 통계";
        if (_totalText != null) _totalText.text = $"총 피해  {Format(total)}  |  DPS  {Format(dps)}";
        if (_emptyText != null) _emptyText.gameObject.SetActive(_entries.Count == 0);

        BuildRows();
        RefreshTabHighlight();

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => Close());
        }

        return this;
    }

    // ── 목록 ─────────────────────────────────────────────────

    void BuildRows()
    {
        foreach (GeneralStatRowUI row in _rows)
            if (row != null) Destroy(row.gameObject);
        _rows.Clear();

        if (_rowArea == null || _rowTemplate == null) return;

        float maxValue = CalcMaxValue(_currentTab);

        foreach (GeneralStatEntry e in _entries)
        {
            GeneralStatRowUI row = Instantiate(_rowTemplate, _rowArea);
            row.gameObject.SetActive(true);
            row.Setup(e);            // 항목이 제 그림을 들고 온다
            row.SetStats(e);
            row.SetDPS(_elapsedSec);
            row.RefreshTab(_currentTab, maxValue);
            _rows.Add(row);
        }
    }

    // ── 탭 ───────────────────────────────────────────────────

    void OnTabClicked(CombatStatTab tab)
    {
        if (_currentTab == tab) return;

        _currentTab = tab;
        RefreshTabHighlight();

        float maxValue = CalcMaxValue(_currentTab);
        foreach (GeneralStatRowUI row in _rows)
            if (row != null) row.RefreshTab(_currentTab, maxValue);
    }

    // 활성 표시는 버튼 면 색이 아니라 하단 강조바로 한다 —
    // 음각 버튼의 모서리 색이 면 색에서 구워져 있어 런타임에 따라오지 않는다.
    void RefreshTabHighlight()
    {
        if (_tabButtonBgs == null) return;

        for (int i = 0; i < _tabButtonBgs.Length; i++)
        {
            if (_tabButtonBgs[i] == null) continue;
            _tabButtonBgs[i].color = (int)_currentTab == i ? TabActiveColor : Color.clear;
        }
    }

    /// <summary>
    /// 이 탭에서 바를 가득 채우는 값 — 1등의 값이다.
    ///
    /// ⚠ 합계가 아니라 최댓값을 분모로 쓴다
    ///   합계로 나누면 카드가 여덟 장일 때 1등조차 바가 1/4 밖에 안 차서
    ///   누가 캐리했는지가 안 읽힌다. 원작 화면과 같은 규칙이다.
    /// </summary>
    float CalcMaxValue(CombatStatTab tab)
    {
        float max = 0f;

        foreach (GeneralStatEntry s in _entries)
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

    static string Format(float v)
    {
        if (v >= 1_000_000f) return $"{v / 1_000_000f:0.0}M";
        if (v >= 1_000f)     return $"{v / 1_000f:0.0}K";
        return $"{(int)v}";
    }
}
