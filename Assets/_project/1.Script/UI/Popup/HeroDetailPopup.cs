using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  HeroDetailPopup.cs
//  적(용사) 상세 — 전황(BattleInfoPopup)에서 적을 누르면 열린다.
//
//  ■ 레이아웃 (HeroDetailPopupCreator)
//    Header   ◆ 장 수 | 이름 · 지휘력 안내                          [X]
//    ├ Left   초상화 / 직업 · 등급
//    ├ Mid    스 탯   — 11행. 행을 누르면 출처별 분해. [장 수 | 용 병] 토글
//    └ Right  스 킬   — 액티브 1 + 패시브 최대 3
//
//  ■ 용사는 세이브 기록이 없다
//    그 판에 굴러 나온 적이다. 직업·등급·패시브는 전부 이름 시드에서
//    결정적으로 나오므로(UnitJobRoller), 이름만 담은 UnitEntry 면
//    실제로 선 그 용사와 같은 값이 된다.
//
//  Inspector 연결은 전부 HeroDetailPopupCreator 가 자동으로 한다.
// ============================================================

public class HeroDetailPopup : PopupBase
{
    public override bool BlockBackgroundClose => true;

    [Header("헤더")]
    [SerializeField] TextMeshProUGUI _nameText;
    // 이름 뒤에 깔리는 그림자 사본. 같이 갱신하지 않으면 프리팹의 플레이스홀더
    // ("영웅 이름") 가 실제 이름 옆에 검은 글씨로 그대로 남는다.
    [SerializeField] TextMeshProUGUI _nameShadowText;
    [SerializeField] Button          _closeBtn;

    [Header("초상화")]
    [SerializeField] Image                _gradeBorder;
    [SerializeField] Image                _portraitBg;
    [SerializeField] Image                _portraitImage;
    [SerializeField] UnitAppearanceBridge _portraitBridge;

    [Header("기본 정보")]
    [SerializeField] TextMeshProUGUI _jobText;
    [SerializeField] Image           _gradeBadge;
    [SerializeField] TextMeshProUGUI _gradeText;

    [Tooltip("등급 배지를 눌렀을 때 뜨는 등급·품질 설명. 배지 아래로 펼쳐진다.")]
    [SerializeField] Button          _gradeInfoBtn;
    [SerializeField] InfoTooltipUI   _gradeTooltip;

    [Tooltip("헤더 이름 옆 지휘력 안내. 수치는 GameplayConfig 에서 읽어 채운다.")]
    [SerializeField] TextMeshProUGUI _commandHintText;

    [Header("스탯 — 장수 / 용병 토글")]
    [SerializeField] Button       _generalTabBtn;
    [SerializeField] Button       _soldierTabBtn;
    [SerializeField] GameObject[] _generalOnlyRows;   // 용병 수·지휘력·스킬 쿨타임

    [Header("스탯")]
    [SerializeField] TextMeshProUGUI _hpText;
    [SerializeField] TextMeshProUGUI _atkText;
    [SerializeField] TextMeshProUGUI _defText;
    [SerializeField] TextMeshProUGUI _spdText;
    [SerializeField] TextMeshProUGUI _atkSpdText;
    [SerializeField] TextMeshProUGUI _rangeText;
    [SerializeField] TextMeshProUGUI _critChanceText;
    [SerializeField] TextMeshProUGUI _critDmgText;
    [SerializeField] TextMeshProUGUI _soldierCountText;
    [SerializeField] TextMeshProUGUI _cmdPwrText;
    [SerializeField] TextMeshProUGUI _cooldownText;

    [Header("스킬")]
    [SerializeField] Image             _activeSkillIcon;
    [SerializeField] TextMeshProUGUI   _activeSkillText;
    [SerializeField] TextMeshProUGUI   _activeSkillDescText;
    [SerializeField] GameObject[]      _passiveBoxes;
    [SerializeField] Image[]           _passiveIcons;
    [SerializeField] TextMeshProUGUI[] _passiveNameTexts;
    [SerializeField] TextMeshProUGUI[] _passiveDescTexts;

    UnitEntry _entry;
    Texture2D _portraitTexture;

    HeroStatResult _statResult;
    int            _expandedStatIndex = -1;
    bool           _showSoldier;          // false = 장수(기본), true = 용병

    struct StatRowEntry
    {
        public TextMeshProUGUI ValueTmp;
        public StatType        Type;
    }
    StatRowEntry[] _statRowEntries;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 적 용사를 본다.
    /// ⚠ 시드 이름("Hero_S6_0")을 그대로 두지 않는다 — 그건 씨앗이지 이름이 아니다.
    /// </summary>
    public void SetupHero(UnitEntry entry, string displayName)
    {
        _entry             = entry;
        _expandedStatIndex = -1;

        SetStatTarget(soldier: false);   // 열 때는 항상 장수부터
        RefreshUI();

        _nameText.text       = displayName;
        _nameShadowText.text = displayName;
    }

    // ── 생명주기 ──────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _closeBtn.onClick.AddListener(() => Close());
        _gradeInfoBtn.onClick.AddListener(ShowGradeTooltip);

        _generalTabBtn.onClick.AddListener(() => SetStatTarget(soldier: false));
        _soldierTabBtn.onClick.AddListener(() => SetStatTarget(soldier: true));

        SetupStatClickHandlers();
    }

    // ── UI 갱신 ───────────────────────────────────────────────

    void RefreshUI()
    {
        UnitJob job = UnitJobRoller.GetJob(_entry.UnitName);
        _statResult = HeroStatResolver.Resolve(_entry);
        Color gc    = GradeStyle.GetColor(_entry.Grade);

        _gradeBorder.color = gc;
        _gradeBadge.color  = gc;
        // ⚠ '?' 같은 표시를 라벨에 이어 붙이지 않는다 — 배지가 좁아 두 줄로 접힌다.
        //   "누를 수 있다" 는 신호는 배지 모서리의 ⓘ 가 맡는다 (Creator 참고).
        _gradeText.text  = GradeStyle.GetLabelWithQuality(_entry.Grade, _entry.UnitName);
        _gradeText.color = Color.white;
        RefreshCommandHint();
        _jobText.text = JobStyle.GetLabel(job);

        RefreshAllStatTexts();
        FillSkills(job, _entry);

        UnitPortraitHelper.Render(_entry.UnitName, job, _entry.Grade,
            _portraitBridge, _portraitBg, _portraitImage, ref _portraitTexture);
    }

    void FillSkills(UnitJob job, UnitEntry entry)
    {
        var activeDb  = ActiveSkillDatabase.Current;
        var passiveDb = PassiveSkillDatabase.Current;

        var activeId   = RareSkillArbiter.Resolve(entry.UnitName, job, activeDb, entry.Grade);
        var activeData = activeDb.Get(activeId);
        _activeSkillText.text     = activeData?.SkillName   ?? "-";
        _activeSkillDescText.text = activeData?.Description ?? "";

        // ⚠ 그림이 없을 때 어두운 색을 칠하지 않는다 — 빈 칸은 홈만 남아야 빈 칸으로 읽힌다.
        var sp = SpriteManager.Instance?.Get(activeId.IconKey());
        _activeSkillIcon.sprite  = sp;
        _activeSkillIcon.color   = Color.white;
        _activeSkillIcon.enabled = sp != null;

        var (s0, s1, s2)            = PassiveSkillRoller.Roll(entry.UnitName);
        int slotCount               = PassiveSkillRoller.GetActiveSlotCount(entry.Grade);
        PassiveSkillType[] passives = { s0, s1, s2 };

        for (int i = 0; i < _passiveBoxes.Length; i++)
        {
            bool show = i < slotCount;
            _passiveBoxes[i].SetActive(show);
            if (!show) continue;

            var pd = passiveDb.Get(passives[i]);
            _passiveNameTexts[i].text = pd?.SkillName   ?? "-";
            _passiveDescTexts[i].text = pd?.Description ?? "";

            // 이름을 '패시브' 색(초록)으로 — 스탯 창에서 초록으로 뜬 수치가
            // 어느 패시브에서 왔는지 색으로 이어진다.
            _passiveNameTexts[i].color = StatBonusColors.PassiveColor;

            var pic = pd?.Icon;
            _passiveIcons[i].sprite  = pic;
            _passiveIcons[i].color   = Color.white;
            _passiveIcons[i].enabled = pic != null;
        }
    }

    // ── 등급·품질 설명 ───────────────────────────────────────
    //
    //  배지에 뜨는 "영웅 5" 는 서로 다른 두 값이 붙어 있는 것이다.
    //    영웅 : 등급 (UnitGrade)  ·  5 : 품질 (이름 시드가 정한 굴림)

    void ShowGradeTooltip()
    {
        // ⚠ 등급 이름·색을 여기 문자열로 적지 않는다 — GradeStyle 이 정본이다.
        var sb    = new System.Text.StringBuilder();
        var order = (UnitGrade[])System.Enum.GetValues(typeof(UnitGrade));
        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0) sb.Append("<color=#808080> > </color>");

            string hex = ColorUtility.ToHtmlStringRGB(GradeStyle.GetColor(order[i]));
            sb.Append($"<color=#{hex}>{GradeStyle.GetLabel(order[i])}</color>");
        }

        _gradeTooltip.ShowAnchored(
            _gradeInfoBtn.transform as RectTransform,
            "등급과 품질",
            $"{sb}\n오른쪽으로 갈수록 기본 스탯이 높다.",
            "옆의 숫자는 품질(1~9)이다.\n같은 등급이라도 숫자가 클수록 스탯이 높다.");
    }

    /// <summary>
    /// "지휘력 +1 › 용병 스탯 +N%" — 수치는 GameplayConfig 에서 읽는다.
    /// ⚠ 화살표(→)는 폰트에 없어서 □ 로 뜬다 (UI 규칙 2) — › 를 쓴다.
    /// </summary>
    void RefreshCommandHint()
    {
        float perCmd = GameplayConfig.Current.SoldierRatioPerCommandPower;
        _commandHintText.text = $"지휘력 +1 › 용병 스탯 +{perCmd * 100f:0.#}%";
    }

    // ── 스탯 행 (클릭 → 출처별 분해) ──────────────────────────

    void SetupStatClickHandlers()
    {
        // 순서는 화면에 놓인 행 순서와 맞춘다 (HeroDetailPopupCreator.BuildStatColumn).
        var defs = new (TextMeshProUGUI tmp, StatType type)[]
        {
            (_hpText,           StatType.MaxHp),
            (_atkText,          StatType.Attack),
            (_defText,          StatType.Defense),
            (_soldierCountText, StatType.SoldierCount),
            (_spdText,          StatType.MoveSpeed),
            (_atkSpdText,       StatType.AttackSpeed),
            (_rangeText,        StatType.AttackRange),
            (_cmdPwrText,       StatType.CommandPower),
            (_cooldownText,     StatType.SkillCooldownReduce),
            (_critChanceText,   StatType.CritChance),
            (_critDmgText,      StatType.CritDamage),
        };

        _statRowEntries = new StatRowEntry[defs.Length];

        for (int i = 0; i < defs.Length; i++)
        {
            var (tmp, type) = defs[i];
            var rowGo = tmp.transform.parent.gameObject;

            int idx = i;
            rowGo.GetComponent<Button>().onClick.AddListener(() => ToggleStatRow(idx));

            _statRowEntries[i] = new StatRowEntry { ValueTmp = tmp, Type = type };
        }
    }

    void ToggleStatRow(int index)
    {
        _expandedStatIndex = (_expandedStatIndex == index) ? -1 : index;
        RefreshAllStatTexts();
    }

    // ── 장수 / 용병 전환 ──────────────────────────────────────
    //  용병은 장수 스탯을 그대로 물려받아 배율만 곱한 값이라 같은 행을 다시 쓴다.
    //  용병에게 의미가 없는 세 줄(용병 수·지휘력·스킬 쿨타임)만 감춘다.

    void SetStatTarget(bool soldier)
    {
        _showSoldier       = soldier;
        _expandedStatIndex = -1;

        foreach (var row in _generalOnlyRows)
            row.SetActive(!soldier);

        StyleStatTab(_generalTabBtn, !soldier);
        StyleStatTab(_soldierTabBtn,  soldier);

        if (_statResult != null) RefreshAllStatTexts();
    }

    // 탭 바탕(Body Image) · 라벨 · 밑줄을 한꺼번에 바꾼다.
    static readonly Color TabFaceOn  = new(0.20f, 0.38f, 0.62f);
    static readonly Color TabFaceOff = new(0.15f, 0.16f, 0.25f);
    static readonly Color TabTextOn  = new(0.90f, 0.96f, 1.00f);
    static readonly Color TabTextOff = new(0.58f, 0.60f, 0.72f);

    static void StyleStatTab(Button btn, bool active)
    {
        btn.targetGraphic.color = active ? TabFaceOn : TabFaceOff;

        var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        lbl.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        lbl.color     = active ? TabTextOn : TabTextOff;

        btn.transform.Find("Body/ActiveBar").gameObject.SetActive(active);
    }

    /// <summary>
    /// 용병 탭일 때 이 스탯에 곱할 배율.
    /// 공식은 SoldierRuntimeBridge 가 소유한다 — 실제 전투 병사와 같은 값이다.
    /// </summary>
    float SoldierScale(StatType type)
    {
        if (!_showSoldier || SoldierRuntimeBridge.IsUnscaled(type)) return 1f;
        return SoldierRuntimeBridge.StatRatio(_statResult.Total(StatType.CommandPower));
    }

    void RefreshAllStatTexts()
    {
        for (int i = 0; i < _statRowEntries.Length; i++)
            RefreshStatRow(i);
    }

    // ⚠ overflowMode 를 Ellipsis 로 두면 안 된다 — 분해 문자열이 칸보다 길면 통째로
    //   "..." 이 된다. AutoSize 로 줄여서 담는다 (칸 높이는 그대로).
    void RefreshStatRow(int index)
    {
        var row = _statRowEntries[index];

        // 용병 배율은 출처별 값에 그대로 곱해도 된다 — 선형이라 분해가 그대로 성립한다.
        float k          = SoldierScale(row.Type);
        float baseVal    = _statResult.Base.Get(row.Type)   * k;

        // ── 패시브 칸은 탭마다 출처가 다르다 ──────────────────
        //  장수 : Target.General 몫 · 용병 : Target.Soldier 몫
        //  ⚠ 비율은 '환산된 병사 스탯' 에 곱하고, 절대값은 환산 없이 더한다 (전투와 같다).
        float passiveVal;
        if (_showSoldier)
        {
            float inherited = baseVal;
            passiveVal = inherited * _statResult.GetSoldierPassiveRatio(row.Type)
                       + _statResult.GetSoldierPassiveFlat(row.Type);
        }
        else
        {
            passiveVal = _statResult.GetPassive(row.Type);
        }

        float total = baseVal + passiveVal;

        bool hasBonus = passiveVal != 0f;

        row.ValueTmp.text = (index == _expandedStatIndex && hasBonus)
            ? StatDisplayHelper.BuildBreakdown(row.Type, baseVal, passiveVal)
            : StatDisplayHelper.FormatStat(row.Type, total, isFinal: true);
    }
}
