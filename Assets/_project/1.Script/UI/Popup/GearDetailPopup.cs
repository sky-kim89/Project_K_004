using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  GearDetailPopup.cs
//  몬스터 장비 상세 — **레벨 트랙을 보고 레벨업·강화한다.**
//
//  ■ 여는 곳이 둘이다
//    · 도감 장비 탭  — Setup(gear)             (보기 + 레벨업)
//    · 몬스터 상세   — SetupForEquip(...)       (보기 + 레벨업 + 장착)
//    두 곳이 같은 창을 쓴다. 한쪽에만 레벨업이 있으면 "어디서 올리지?" 가 된다.
//
//  ■ 레벨 트랙이 이 창의 몸통이다
//    Lv1~Lv5 다섯 줄을 **전부** 보여 준다 — 이미 연 줄은 밝게, 다음 줄은 강조,
//    그 뒤는 흐리게. "이 장비를 키우면 무엇이 되는가" 가 누르기 전에 보여야
//    재료(같은 장비)를 모을 이유가 생긴다. 컨셉(급소·방벽…)이 그 줄들을 한 단어로 묶는다.
//
//  ■ 낼 것은 버튼 **안**에 있다 (몬스터 상세의 품질 개선과 같은 규칙)
//    [레벨 업] [장비 그림 ×필요 수] [금화 값] — 재료는 **작은 장비 그림**으로 센다
//    (사용자 지시). 가진 만큼 밝고 모자란 만큼 흐리다 — 숫자를 읽지 않아도
//    몇 개가 모자라는지 보인다.
//
//  ⚠ 숫자를 여기서 다시 계산하지 않는다
//    재료 수·값·판정은 MonsterGearLevelRule, 능력치 글은 GearOptionText 가 정본이다.
//  ⚠ 마나는 글자가 아니라 아이콘이다 (UI 규칙 7) — 카드 비용 옵션은 줄마다 배지로 따로 그린다.
//
//  Inspector 연결은 GearDetailPopupCreator 가 전부 자동으로 한다.
// ============================================================

public class GearDetailPopup : PopupBase
{
    [Header("헤더")]
    [SerializeField] Image           _iconFrame;     // 등급색 테두리 — 레벨업 연출 자리이기도 하다
    [SerializeField] Image           _icon;
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _subText;       // "유일 · 갑옷 · 인간형 · 급소"
    [SerializeField] TextMeshProUGUI _goldText;      // 보유 영구 골드
    [SerializeField] Button          _closeBtn;

    // ⚠ 누가 끼고 있는지는 **초상화**로 보여 준다 (사용자 지시, 2026-09-12)
    //   장착을 누르면 이 창을 닫지 않고 그 자리에서 초상화가 바뀐다.
    [SerializeField] Image           _wearerFrame;
    [SerializeField] Image           _wearerPortrait;
    [SerializeField] TextMeshProUGUI _wearerName;

    // ⚠ [이름 ········ 값] 줄이다 — 몬스터 상세의 스탯 줄과 같은 짜임 (사용자 지적, 2026-09-12 "보기 어렵다")
    //   한 칸에 글을 쏟아 넣었을 때 줄이 늘면 자동 축소로 작아졌고, 이름과 값이 붙어 안 읽혔다.
    [Header("현재 능력치 — [이름 ···· 값] 줄")]
    [SerializeField] GameObject[]      _statRows;
    [SerializeField] TextMeshProUGUI[] _statLabels;
    [SerializeField] TextMeshProUGUI[] _statValues;

    /// <summary>능력치 줄 수 — Creator 가 이만큼 굽는다. 넘치면 에러를 낸다 (SetStatRow).</summary>
    public const int StatRowCount = 8;

    [SerializeField] GameObject      _statManaRoot;  // [마나][-1] — 카드 비용을 깎을 때만
    [SerializeField] TextMeshProUGUI _statManaText;
    [SerializeField] TextMeshProUGUI _descText;

    [Header("레벨 트랙 (Lv1~Lv5)")]
    [SerializeField] Image[]           _rowBgs;
    [SerializeField] Image[]           _rowAccents;   // 왼쪽 띠 — 연 줄 등급색 · 다음 줄 금색 · 아직 없음
    [SerializeField] Image[]           _rowChips;
    [SerializeField] TextMeshProUGUI[] _rowNums;
    [SerializeField] TextMeshProUGUI[] _rowTexts;
    [SerializeField] GameObject[]      _rowManaRoots;
    [SerializeField] TextMeshProUGUI[] _rowManaTexts;
    [SerializeField] TextMeshProUGUI[] _rowPassives;  // Lv4·Lv5 에만 켠다 — 패시브 이름

    // ⚠ 패시브는 **그림 + 이름**, 올리거나 누르면 설명이 뜬다 (사용자 지시, 2026-09-11)
    //   이름만으로는 "역병 폭발이 무엇을 하는가" 를 알 수 없었다. 몬스터 상세·융합 창과
    //   같은 칩(SpeciesPassiveChipUI → TooltipLayer)을 쓴다. 칩이 켜고 끄는 단위다.
    [SerializeField] SpeciesPassiveChipUI[] _rowPassiveChips;
    [SerializeField] Image[]                _rowPassiveIcons;

    [Tooltip("종족 패시브 아이콘. ⚠ SpeciesPassiveRule.All 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _speciesIcons;
    [SerializeField] Image             _enhanceBg;
    [SerializeField] TextMeshProUGUI   _enhanceText;

    [Header("하단")]
    [SerializeField] TextMeshProUGUI _hintText;
    [SerializeField] Button          _advanceBtn;
    [SerializeField] TextMeshProUGUI _advanceLabel;  // "레벨 업" · "강화" · "최대 강화"
    [SerializeField] GameObject      _matRoot;
    [SerializeField] Image[]         _matIcons;      // 재료 그림 — 한 칸만 쓴다 [그림][×N]
    [SerializeField] TextMeshProUGUI _matCount;      // "×2" — 모자라면 붉다
    [SerializeField] GameObject      _costRoot;
    [SerializeField] TextMeshProUGUI _costText;
    [SerializeField] Button          _equipBtn;
    [SerializeField] TextMeshProUGUI _equipLabel;    // "장착" · "옮겨 장착" · "장착 중"

    // ── 색 ── (에디터 Pop 팔레트는 런타임에서 못 읽는다 — 같은 값을 유지할 것)
    static readonly Color GoldC     = new Color(1.00f, 0.83f, 0.30f, 1f);
    static readonly Color ShortC    = new Color(1.00f, 0.42f, 0.42f, 1f);
    static readonly Color DimText   = new Color(0.44f, 0.48f, 0.60f, 1f);
    static readonly Color SubText   = new Color(0.72f, 0.76f, 0.90f, 1f);
    // ⚠ 세 줄 상태의 바탕은 대비가 커야 한다 — 한때 0.085 / 0.105 / 0.16 이라 연 줄과 아직인 줄이 한 색으로 보였다
    static readonly Color RowOff    = new Color(0.068f, 0.072f, 0.118f, 1f);
    static readonly Color RowOn     = new Color(0.125f, 0.135f, 0.215f, 1f);
    static readonly Color RowNext   = new Color(0.20f, 0.22f, 0.37f, 1f);
    static readonly Color NextNum   = new Color(0.14f, 0.11f, 0.04f, 1f);   // 금색 동그라미 위 — 어두운 숫자 (UI 규칙 8)
    static readonly Color QuirkC    = new Color(0.878f, 0.549f, 1f, 1f);   // GearOptionText.QuirkHex
    static readonly Color ChipOff   = new Color(0.20f, 0.23f, 0.33f, 1f);
    static readonly Color MatMiss   = new Color(1f, 1f, 1f, 0.22f);

    MonsterGearData    _gear;
    MonsterSpeciesData _species;       // 장착 모드일 때만
    Action             _onEquip;

    /// <summary>레벨업·장착으로 무언가 바뀌었다 — 뒤에 깔린 창이 다시 그리도록 알린다.</summary>
    public Action OnChanged;

    readonly List<GearOption> _buffer = new(12);

    // ── 열기 ─────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        RequireWiring();

        _closeBtn?.onClick.AddListener(() => Close());
        _advanceBtn.onClick.AddListener(HandleAdvance);
        _equipBtn.onClick.AddListener(HandleEquip);
    }

    /// <summary>
    /// 프리팹이 Creator 보다 오래됐으면 <b>무엇을 눌러야 하는지</b> 말하며 터진다.
    ///
    /// ⚠ 방어적 체크가 아니다 — 여전히 예외다 (SummonDeckUI.RequireWiring 과 같은 이유)
    ///   2026-09-11 에 패시브 칩 배열을 늘린 뒤 다시 굽지 않은 프리팹으로 열자
    ///   RefreshTrack 한복판에서 IndexOutOfRange 가 났다. 스택만 보고는
    ///   "프리팹을 안 구웠다" 를 짚을 수 없어서, 예외가 스스로 원인을 말하게 한다.
    /// </summary>
    void RequireWiring()
    {
        int rows = _rowTexts.Length;

        string missing =
              _rowPassiveChips == null || _rowPassiveChips.Length < rows ? nameof(_rowPassiveChips)
            : _rowPassiveIcons == null || _rowPassiveIcons.Length < rows ? nameof(_rowPassiveIcons)
            : _speciesIcons    == null || _speciesIcons.Length    == 0   ? nameof(_speciesIcons)
            : _wearerPortrait  == null                                   ? nameof(_wearerPortrait)
            : _matCount        == null                                   ? nameof(_matCount)
            : _statRows        == null || _statRows.Length < StatRowCount   ? nameof(_statRows)
            : _rowAccents      == null || _rowAccents.Length < rows         ? nameof(_rowAccents)
            : null;

        if (missing == null) return;

        throw new MissingReferenceException(
            $"[GearDetailPopup] '{missing}' 가 비어 있다 — 장비 상세 프리팹이 지금 Creator 보다 오래됐다.\n" +
            "Tools > Project K > 프리팹 생성 > 팝업 > 장비 상세 → PopupManager [Load Popup Prefabs] 를 실행할 것.");
    }

    /// <summary>도감에서 연다 — 보고 레벨업한다.</summary>
    public GearDetailPopup Setup(MonsterGearData gear)
    {
        _gear    = gear;
        _species = null;
        _onEquip = null;
        Refresh();
        return this;
    }

    /// <summary>
    /// 몬스터 상세의 장비 목록에서 연다 — 장착 버튼이 함께 뜬다.
    /// ⚠ 장착 자체는 부르는 쪽이 한다 (onEquip). <paramref name="slot"/> 은 판정(CheckAt)에만 쓴다.
    /// </summary>
    public GearDetailPopup SetupForEquip(MonsterGearData gear, MonsterSpeciesData species, int slot,
                                         Action onEquip, Action onUnequip)
    {
        _gear      = gear;
        _species   = species;
        _slot      = slot;
        _onEquip   = onEquip;
        _onUnequip = onUnequip;
        Refresh();
        return this;
    }

    int    _slot;
    Action _onUnequip;

    protected override void OnAfterClose()
    {
        OnChanged  = null;
        _onEquip   = null;
        _onUnequip = null;
    }

    // ── 그리기 ───────────────────────────────────────────────

    static MonsterGearInventory Inv => UserDataManager.Instance.Get<MonsterGearInventory>();

    void Refresh()
    {
        var inv   = Inv;
        int level = inv.LevelOf(_gear.Id);
        int enh   = inv.EnhanceOf(_gear.Id);
        Color grade = GradeStyle.GetColor(_gear.Grade);

        RefreshHeader(level, enh, grade);
        RefreshStats(inv, level, enh);
        RefreshTrack(level, enh, grade);
        RefreshFooter(inv, level);
    }

    void RefreshHeader(int level, int enh, Color grade)
    {
        _iconFrame.color = grade;
        _icon.sprite     = _gear.Icon;
        _icon.enabled    = _gear.Icon != null;

        // 레벨은 이름 바로 뒤에 붙인다 — 따로 두면 이름이 짧을 때 사이가 휑하게 뜬다
        string enhTag = enh > 0 ? $" <color=#{GoldHex}>+{enh}</color>" : "";
        _nameText.text  = $"{_gear.DisplayName}  <size=62%><color=#FFFFFF>Lv{level}</color>{enhTag}</size>";
        _nameText.color = grade;

        // 등급 이름은 등급색으로 — 테두리 색과 이어져 "무슨 등급인가" 가 글자로도 읽힌다
        string body = _gear.Body == MonsterGearBody.Humanoid ? "인간형" : "비인간형";
        string sub  = $"<color=#{ColorUtility.ToHtmlStringRGB(grade)}>{GradeStyle.GetLabel(_gear.Grade)}</color>" +
                      $" · {MonsterGearData.NameOf(_gear.Part)} · {body}";
        if (!string.IsNullOrEmpty(_gear.Concept)) sub += $" · {_gear.Concept}";
        _subText.text = sub;

        _goldText.text = MonsterGradeUpgradeRule.Wallet.ToString("N0");

        RefreshWearer();
    }

    static readonly Color WearerOn  = new Color(0.68f, 0.90f, 0.42f, 1f);   // Creator 의 TitleC
    static readonly Color WearerOff = new Color(0.20f, 0.23f, 0.33f, 1f);

    /// <summary>이 장비를 낀 몬스터의 초상화 — 초상화는 낀 장비까지 입는다 (MonsterPortraitProvider).</summary>
    void RefreshWearer()
    {
        string id = Inv.WearerOf(_gear.Id);

        if (id == null)
        {
            _wearerFrame.color      = WearerOff;
            _wearerPortrait.enabled = false;
            _wearerName.text        = "없음";
            _wearerName.color       = DimText;
            return;
        }

        MonsterSpeciesData wearer = CardCatalog.Current.GetMonster(id);
        Sprite art = MonsterPortraitProvider.Get(wearer);

        _wearerFrame.color      = WearerOn;
        _wearerPortrait.sprite  = art;
        _wearerPortrait.enabled = art != null;
        _wearerName.text        = wearer.DisplayName;
        _wearerName.color       = Color.white;
    }

    static readonly string GoldHex = ColorUtility.ToHtmlStringRGB(GoldC);

    /// <summary>패시브 글자색 — 장비 목록의 (장비) 표기와 같은 계열.</summary>
    static readonly Color PassiveC = new Color(0.94f, 0.72f, 0.48f, 1f);

    readonly List<GearOption> _merged = new(12);

    /// <summary>
    /// 지금 레벨까지 붙은 것의 <b>합계</b> — [이름 ···· 값] 한 줄에 하나.
    ///
    /// ■ 같은 능력치는 합친다 — "체력 +66 · 체력 +33" 이 아니라 "체력 +99"
    ///   ⚠ 특이 옵션은 따로 둔다 (색이 달라 "일부러 붙은 것" 으로 읽혀야 한다).
    /// ■ 강화 몫은 공격력%·체력% 줄에 더한다 — 적용 자리가 같다 (MonsterGearRule.ApplyStats ①)
    /// ■ 패시브는 이름만 — 설명은 레벨 트랙의 패시브 칩에 올리면 뜬다
    /// </summary>
    void RefreshStats(MonsterGearInventory inv, int level, int enh)
    {
        _gear.CollectUpTo(level, _buffer);

        _merged.Clear();
        foreach (var o in _buffer) Merge(o);
        if (enh > 0)
        {
            float pct = MonsterGearLevelRule.EnhancePct(enh);
            Merge(new GearOption(GearStat.AttackPct, pct));
            Merge(new GearOption(GearStat.HpPct,     pct));
        }

        int row = 0;
        foreach (var o in _merged)
        {
            string line = GearOptionText.Describe(o);
            if (string.IsNullOrEmpty(line)) continue;   // 카드 비용은 배지로 (아래)

            // "치명타 확률 +5%" → "치명타 확률" / "+5%"
            int sp = line.LastIndexOf(' ');
            SetStatRow(row++, line.Substring(0, sp), line.Substring(sp + 1),
                       o.Quirk ? QuirkC : SubText, o.Quirk ? QuirkC : Color.white);
        }

        for (int lv = 4; lv <= level; lv++)
        {
            SpeciesPassive p = _gear.PassiveAt(lv);
            if (p == SpeciesPassive.None) continue;
            SetStatRow(row++, "패시브", p.ToKorean(), PassiveC, PassiveC);
        }

        for (; row < _statRows.Length; row++) _statRows[row].SetActive(false);

        int cut = GearOptionText.ManaCutOf(_buffer);
        _statManaRoot.SetActive(cut > 0);
        if (cut > 0) _statManaText.text = $"-{cut}";

        _descText.text = _gear.Description;
    }

    void Merge(GearOption o)
    {
        for (int i = 0; i < _merged.Count; i++)
        {
            if (_merged[i].Stat != o.Stat || _merged[i].Quirk != o.Quirk) continue;

            GearOption m = _merged[i];   // 구조체 사본 — SO 의 값은 건드리지 않는다
            m.Value += o.Value;
            _merged[i] = m;
            return;
        }
        _merged.Add(o);
    }

    void SetStatRow(int index, string label, string value, Color labelC, Color valueC)
    {
        // ⚠ 조용히 자르지 않는다 — 줄이 모자라면 무엇을 늘려야 하는지 말한다
        if (index >= _statRows.Length)
        {
            Debug.LogError($"[GearDetailPopup] '{_gear.DisplayName}' 의 능력치가 {index + 1}줄이다 — " +
                           $"StatRowCount({StatRowCount})를 늘리고 장비 상세를 다시 구울 것.");
            return;
        }

        _statRows[index].SetActive(true);
        _statLabels[index].text  = label;
        _statLabels[index].color = labelC;
        _statValues[index].text  = value;
        _statValues[index].color = valueC;
    }

    /// <summary>
    /// Lv1~Lv5 다섯 줄 + 강화 줄.
    ///
    /// ⚠ 흐린 줄에도 내용을 적는다 — 무엇이 열릴지 알아야 모은다 (파일 머리 주석)
    /// </summary>
    void RefreshTrack(int level, int enh, Color grade)
    {
        int max = MonsterGearLevelRule.MaxLevel;

        for (int i = 0; i < _rowTexts.Length; i++)
        {
            int  lv      = i + 1;
            bool reached = lv <= level;
            bool next    = lv == level + 1;

            IReadOnlyList<GearOption> step = _gear.StepOf(lv);

            // ⚠ 세 상태가 한눈에 갈려야 한다 (사용자 지적, 2026-09-12 "보기 어렵다")
            //   연 줄 = 등급색 띠 · 다음 줄 = 금색 띠 + 금색 동그라미 · 아직 = 띠 없이 흐리게
            _rowBgs[i].color     = next ? RowNext : reached ? RowOn : RowOff;
            _rowAccents[i].color = next ? GoldC   : reached ? grade : Color.clear;
            _rowChips[i].color   = next ? GoldC   : reached ? grade : ChipOff;
            _rowNums[i].color    = next ? NextNum : reached ? Color.white : DimText;

            string text = GearOptionText.Join(step);
            _rowTexts[i].text  = string.IsNullOrEmpty(text) && GearOptionText.ManaCutOf(step) == 0 ? "—" : text;
            _rowTexts[i].color = reached || next ? Color.white : DimText;

            int cut = GearOptionText.ManaCutOf(step);
            _rowManaRoots[i].SetActive(cut > 0);
            if (cut > 0)
            {
                _rowManaTexts[i].text  = $"-{cut}";
                _rowManaTexts[i].color = reached || next ? Color.white : DimText;
            }

            // Lv4·Lv5 — 그 레벨이 여는 패시브 [그림][이름] · 올리면 설명
            SpeciesPassive passive = _gear.PassiveAt(lv);
            bool hasPassive = passive != SpeciesPassive.None;
            _rowPassiveChips[i].gameObject.SetActive(hasPassive);
            if (hasPassive)
            {
                _rowPassiveChips[i].Setup(passive);

                _rowPassives[i].text  = passive.ToKorean();
                _rowPassives[i].color = reached || next ? PassiveC : DimText;

                Sprite icon = IconOf(passive);
                _rowPassiveIcons[i].sprite  = icon;
                _rowPassiveIcons[i].enabled = icon != null;
                _rowPassiveIcons[i].color   = reached || next ? Color.white : MatMiss;
            }
        }

        // ── 강화 줄 ──
        bool open = level >= max;
        _enhanceBg.color = open ? RowOn : RowOff;

        // ⚠ 상한이 없다 — "+4 / 20" 처럼 분모를 적지 않는다
        float per = MonsterGearLevelRule.EnhanceStatPct * 100f;
        _enhanceText.text = open
            ? $"강화 +{enh}  ·  공격력·체력 +{MonsterGearLevelRule.EnhancePct(enh) * 100f:0}%"
            : $"강화 — Lv{max} 부터  ·  한 번에 공격력·체력 +{per:0}%";
        _enhanceText.color = open ? GoldC : DimText;
    }

    /// <summary>패시브의 그림. 순서 계약의 정본은 SpeciesPassiveRule.All 이다 (CardEvolveUI.IconOf 와 같다).</summary>
    Sprite IconOf(SpeciesPassive passive)
    {
        int index = SpeciesPassiveRule.IndexOf(passive);
        return index >= 0 && index < _speciesIcons.Length ? _speciesIcons[index] : null;
    }

    void RefreshFooter(MonsterGearInventory inv, int level)
    {
        var  blocked = MonsterGearLevelRule.Check(_gear);
        bool levelUp = !MonsterGearLevelRule.IsMaxLevel(level);

        // ── 재료 — [장비 그림][×N] (사용자 지시, 2026-09-12) ──
        //   "같은 장비가 1개 더 필요하다" 안내 줄을 없애고 버튼 안에서 센다. 모자라면 숫자가 붉다.
        int need  = MonsterGearLevelRule.MaterialsFor(level);
        int spare = inv.SpareCount(_gear.Id);

        _matRoot.SetActive(need > 0);
        _matIcons[0].sprite = _gear.Icon;
        _matCount.text      = $"×{need}";
        _matCount.color     = spare >= need ? Color.white : ShortC;

        // ── 값 ──
        int cost = MonsterGearLevelRule.CostFor(_gear);
        _costText.text  = cost.ToString("N0");
        _costText.color = blocked == MonsterGearLevelRule.Blocked.NotEnoughGold ? ShortC : GoldC;

        _advanceLabel.text = levelUp ? "레벨 업" : "강화";
        _advanceBtn.interactable = blocked == MonsterGearLevelRule.Blocked.None;

        // ⚠ 안내 줄은 **런 중 잠김**만 말한다 (사용자 지시, 2026-09-12 "필요 없는 텍스트 제거")
        //   재료·골드 부족은 버튼 안의 붉은 숫자가, 다음 레벨은 강조된 트랙 줄이 이미 말한다.
        _hintText.text = blocked == MonsterGearLevelRule.Blocked.InRun ? CodexEditLock.Reason : "";

        // ── 장착 (몬스터 상세에서 열었을 때만) ──
        //   ⚠ 다른 종족이 끼고 있으면 벗겨 옮긴다 (MonsterGearInventory.TryEquip) —
        //     그 사실을 버튼 글자가 말한다. 모르고 누르면 다른 몬스터의 장비가 사라진 것처럼 보인다.
        bool equipMode = _species != null;
        _equipBtn.gameObject.SetActive(equipMode);
        if (!equipMode) return;

        // ⚠ 이 몬스터가 이미 끼고 있으면 같은 버튼이 [벗기기] 가 된다 (사용자 지시, 2026-09-12)
        string wearer = inv.WearerOf(_gear.Id);
        bool   worn   = wearer == _species.Id;

        _equipLabel.text = worn           ? "벗기기"
                         : wearer == null ? "장착"
                         :                  "옮겨 장착";

        // ⚠ 칸에 무엇이 있든 누를 수 있다 — 누르면 그것을 벗기고 끼운다 (CheckAt)
        _equipBtn.interactable = !CodexEditLock.Locked
                               && (worn || inv.CheckAt(_species, _gear, _slot) == MonsterGearInventory.Blocked.None);
    }

    // ── 누르기 ───────────────────────────────────────────────

    void HandleAdvance()
    {
        int  before  = Inv.LevelOf(_gear.Id);
        bool levelUp = !MonsterGearLevelRule.IsMaxLevel(before);

        if (!MonsterGearLevelRule.TryAdvance(_gear)) return;

        Refresh();

        // ⚠ 갱신이 끝난 뒤에 터뜨린다 (UIJuice 파일 머리 주석)
        //   레벨업은 유닛 레벨업과 같은 무게, 강화는 가장 가벼운 강화 연출이다 —
        //   +1% 짜리에 레벨업 폭죽을 쓰면 무게가 거꾸로 읽힌다.
        if (levelUp) UIJuice.LevelUp(_iconFrame.rectTransform, Inv.LevelOf(_gear.Id));
        else         UIJuice.EquipEnhance(_iconFrame.rectTransform, Inv.EnhanceOf(_gear.Id));

        OnChanged?.Invoke();
    }

    /// <summary>
    /// ⚠ 창을 닫지 않는다 (사용자 지시, 2026-09-12) — 끼운 뒤 그 자리에서 착용 초상화가
    ///   이 몬스터로 바뀌고 버튼이 "장착 중" 으로 잠긴다. 무엇이 바뀌었는지가 눈앞에서 읽힌다.
    /// </summary>
    void HandleEquip()
    {
        bool worn = _species != null && Inv.WearerOf(_gear.Id) == _species.Id;

        if (worn) _onUnequip?.Invoke();
        else      _onEquip?.Invoke();

        Refresh();
    }
}
