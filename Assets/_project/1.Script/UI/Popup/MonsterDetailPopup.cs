using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  MonsterDetailPopup.cs
//  몬스터 상세 — 도감에서 종족 하나를 펼쳐 보고 **품질을 개선**한다.
//
//  ■ HeroDetailPopup 을 본떴지만 겸하지 않는다 (사용자 확정, 2026-09-06)
//    3단 구성(초상화 / 스탯 / 능력)과 "행을 눌러 더 보기" 는 그대로 가져왔다.
//    하지만 장수 화면의 절반은 몬스터에 없는 것이다 —
//      장비 3칸 · 용병 수 · 지휘력 · 레벨·EXP · 해고
//    그대로 쓰면 빈 칸이 절반인 창이 된다. 그래서 별도 팝업이고,
//    HeroDetailPopup 은 **인게임에서 적(용사) 정보를 보는 화면**으로 남는다.
//
//  ■ 스탯은 '종족 기본값 × 품질 + 장비' 다 — 실제 소환값이 아니다
//    실제로 필드에 서는 몬스터는 여기에 소환력·카드 레벨·시너지·특성이
//    전부 곱해져 나온다(MonsterStatComposer). 그건 소환사와 덱에 따라
//    달라지므로 도감에서는 말할 수 없다.
//    도감이 답해야 하는 질문은 "이 종족이 다른 종족보다 나은가" 이고,
//    거기에 필요한 것은 **소환사와 무관한 기준값**이다.
//
//    ⚠ 배출 등급 보너스(SpawnPaceRule)도 함께 얹는다
//      종족마다 고정이라 소환사·덱과 무관하다. 빼 두면 느리게 나오는 종족의
//      도감 숫자가 실제 전투보다 낮게 적혀, 그 종족을 쓸 이유가 안 보인다.
//
//    ⚠ 품질과 장비만 얹는 이유는 같다 — <b>이 화면에서 바꾸는 값</b>이라서다
//      둘 다 종족에 붙은 영구 데이터라 소환사·덱과 무관하고, 버튼을 눌렀을 때
//      숫자가 움직이지 않으면 무엇을 샀는지 알 수 없다.
//    ⚠ 장비는 배율 **밖**에서 더한다 — 전투와 같은 순서다
//      (MonsterStatComposer ⑦). 여기서만 곱하면 두 값이 갈린다.
//
//  ■ 고유 액티브 스킬을 적는다 — 오른쪽 칸 **맨 아래**다
//    멧돼지·리치처럼 ActiveSkill 을 가진 종족이 여럿인데 화면에 한 글자도
//    없었다. 종족 패시브만 보고는 "얘가 뭘 하는 애인가" 의 절반을 못 본다.
//    ⚠ 자리가 맨 아래인 이유 — <b>없는 종족이 더 많다</b> (사용자 지적, 2026-09-07)
//      맨 위에 두면 대부분의 종족에서 빈 자리가 오른쪽 칸 머리에 남고
//      종족 패시브가 아래로 밀린다. 늘 있는 것이 위, 가끔 있는 것이 아래다.
//    ⚠ 이름·설명·쿨다운을 손으로 적지 않는다 — ActiveSkillDatabase 의 SO 가 정본이다.
//    ⚠ 쿨다운은 **SO 원본**이다. 실제 값은 술법 시너지가 깎는다
//      (MonsterRuntimeBridge.BuildSkillSlot). 스탯과 같은 이유로 기준값을 적는다.
//
//  ■ 품질 개선 — 이 화면의 유일한 '누를 것'
//    ⚠ 지갑과 값을 붙여 놓지 않는다 (사용자 지적, 2026-09-06)
//      한때 버튼 위에 "900 (보유 12,400)" 한 줄이었다. 같은 크기의 두 숫자가
//      붙어 있으면 어느 쪽이 낼 돈인지 매번 다시 읽어야 하고, 골드가 드는
//      일이라는 사실 자체가 안 보였다.
//      → 지갑은 **헤더**(늘 같은 자리), 값은 **버튼 안**(누를 것과 한 몸),
//        둘 다 골드 아이콘을 단다 (UI 규칙 7).
//    ⚠ 무엇이 바뀌는지 미리 보여 준다
//      단계 핍이 몇 칸 남았는지 말하고, 체력·공격력 행이 "99 › 109" 로
//      다음 값을 함께 그린다. 되돌릴 수 없는 지출이라 누르기 전에 보여야 한다.
//    ⚠ 누르면 티가 나야 한다
//      UIJuice.GradeUp — 이 게임에서 가장 무거운 성장 연출이다. 영구 데이터를
//      골드로 사는 자리라 등급업과 같은 무게가 맞다.
//
//    비용·판정은 전부 MonsterGradeUpgradeRule 이 소유한다. 여기서 숫자를
//    다시 적지 않는다 — 밸런스를 고친 날부터 표시만 옛말을 한다.
//
//  Inspector 연결은 MonsterDetailPopupCreator 가 전부 자동으로 한다.
// ============================================================

public class MonsterDetailPopup : PopupBase
{
    public override bool BlockBackgroundClose => true;

    [Header("헤더")]
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _lineageText;   // "슬라임 계열 · 물량 · 방패"
    [SerializeField] TextMeshProUGUI _goldText;      // 보유 영구 골드
    [SerializeField] Button          _closeBtn;

    [Header("초상화")]
    [SerializeField] Image _gradeBorder;
    [SerializeField] Image _portraitImage;

    [Header("품질")]
    [SerializeField] Image           _gradeBadge;
    [SerializeField] TextMeshProUGUI _gradeText;     // "고급"

    [Header("소환 비용")]
    [SerializeField] TextMeshProUGUI _manaText;
    [SerializeField] TextMeshProUGUI _countText;
    [SerializeField] TextMeshProUGUI _kindText;      // 근접 / 원거리

    [Header("시너지 표식")]
    [SerializeField] GameObject[]      _tagRoots;
    [SerializeField] Image[]           _tagIcons;
    [SerializeField] TextMeshProUGUI[] _tagNames;

    [Tooltip("칩에 올리거나 누르면 동·은·금 효과가 뜬다. 상단 시너지 줄과 같은 컴포넌트다.")]
    [SerializeField] SynergyChipUI[]   _tagHovers;

    [Tooltip("시너지 아이콘. ⚠ MonsterSynergyRule.AllTags 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _synergyIcons;

    [Header("스탯 (행 순서 = StatRows)")]
    [SerializeField] TextMeshProUGUI[] _statValues;

    [Header("고유 액티브 스킬")]
    [SerializeField] GameObject      _skillRoot;     // 스킬이 없는 종족이면 통째로 끈다
    [Tooltip("스킬 상자. 능력 칸이 많으면 남는 만큼만 편다 (LayoutSkillSection).")]
    [SerializeField] RectTransform   _skillBox;
    [SerializeField] Image           _skillIcon;
    [SerializeField] TextMeshProUGUI _skillName;
    [SerializeField] TextMeshProUGUI _skillCooldown;
    [SerializeField] TextMeshProUGUI _skillDesc;

    [Header("패시브 (종족 + 카드 레벨)")]
    [SerializeField] TextMeshProUGUI   _descText;
    [SerializeField] Image[]           _passiveIcons;
    [SerializeField] TextMeshProUGUI[] _passiveNames;
    [SerializeField] TextMeshProUGUI[] _passiveDescs;
    [SerializeField] GameObject[]      _passiveRoots;

    [Tooltip("각성 패시브 칸에만 켜는 반짝임 (PassiveShineUI). 줄마다 하나.")]
    [SerializeField] GameObject[]      _passiveShines;

    [Tooltip("줄 오른쪽 위 [마릿수][숫자] 배지 — 권속 소환 줄만 켠다. 줄마다 하나.")]
    [SerializeField] GameObject[]      _passiveCountRoots;
    [SerializeField] TextMeshProUGUI[] _passiveCountTexts;

    [Header("패시브 — 아이콘 모드 (칸 수보다 많을 때)")]
    [Tooltip("패시브가 줄 칸(_passiveRoots)보다 많으면 줄을 끄고 이 격자에 아이콘만 그린다.\n" +
             "설명은 아이콘에 올리거나 누르면 뜬다 (InfoIconUI).")]
    [SerializeField] GameObject        _passiveGridRoot;
    [SerializeField] GridLayoutGroup   _passiveGrid;
    [SerializeField] Image[]           _passiveGridIcons;
    [SerializeField] InfoIconUI[]      _passiveGridTips;
    [SerializeField] GameObject[]      _passiveGridShines;

    [Tooltip("종족 패시브 아이콘. ⚠ SpeciesPassiveRule.All 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _speciesIcons;

    [Header("장비 (칸 수 = 품질)")]
    [SerializeField] GameObject[]      _gearSlots;      // 3칸 — 잠긴 칸은 자물쇠
    [SerializeField] Button[]          _gearButtons;
    [SerializeField] Image[]           _gearFrames;     // 등급색 테두리
    [SerializeField] Image[]           _gearIcons;
    [SerializeField] TextMeshProUGUI[] _gearLabels;     // 이름 / "비었다" / "희귀부터"
    [SerializeField] GameObject[]      _gearLocks;      // 자물쇠 — 잠긴 칸에만
    [SerializeField] TextMeshProUGUI   _gearHint;       // 칸 아래 한 줄 안내

    [Header("장비 고르기 (겹쳐 뜨는 목록)")]
    [SerializeField] GameObject      _pickRoot;
    [SerializeField] TextMeshProUGUI _pickTitle;
    [SerializeField] TextMeshProUGUI _pickEmpty;      // 고를 것이 없을 때
    [SerializeField] RectTransform   _pickContent;
    [SerializeField] GameObject      _pickCellTemplate;
    [SerializeField] Button          _pickCloseBtn;
    // ⚠ [상세]·[벗기기] 버튼은 없앴다 (사용자 지시, 2026-09-12) — 낀 장비가 목록 맨 앞에
    //   "장착 중" 으로 서고, 누르면 같은 장비 상세가 열린다.

    [Header("품질 개선")]
    [SerializeField] Image[]         _gradePips;     // 5칸 — Normal~Epic
    [SerializeField] TextMeshProUGUI _stepText;      // "고급 › 희귀" / "최고 품질"
    [SerializeField] Button          _upgradeBtn;
    [SerializeField] TextMeshProUGUI _upgradeLabel;  // "품질 개선" / "골드 부족"
    [SerializeField] GameObject      _costRoot;      // [금][2,400] — 만렙이면 끈다
    [SerializeField] TextMeshProUGUI _costText;

    /// <summary>
    /// 스탯 행의 순서 정본.
    ///
    /// ⚠ Creator 가 이 배열로 행 이름표를 굽고, 런타임이 같은 순서로 값을 넣는다
    ///   순서가 어긋나면 "방어율 칸에 이동속도가 뜨는" 상태가 된다.
    /// </summary>
    public static readonly (string label, StatKind kind)[] StatRows =
    {
        ("체력",        StatKind.Hp),
        ("공격력",      StatKind.Attack),
        ("방어율",      StatKind.Defense),
        ("소환 간격",   StatKind.SpawnPace),
        ("공격 사거리", StatKind.Range),
        ("공격 속도",   StatKind.AttackSpeed),
        ("이동 속도",   StatKind.MoveSpeed),
        ("치명타 확률", StatKind.CritChance),
        ("치명타 피해", StatKind.CritDamage),
    };

    public enum StatKind
    {
        Hp, Attack, Defense, SpawnPace, Range, AttackSpeed, MoveSpeed, CritChance, CritDamage,
    }

    /// <summary>품질 단계 수 — Normal~Epic. Creator 가 굽는 핍 수와 같아야 한다.</summary>
    public const int GradeSteps = 5;

    /// <summary>다음 값 미리보기의 흐린 색. "99 › 109" 의 뒷부분.</summary>
    const string PreviewHex = "7C86A0";

    MonsterSpeciesData _species;

    readonly List<SpeciesPassive> _passiveBuffer = new(6);

    /// <summary>능력 칸 하나에 그릴 것. 축이 둘이라(아래 RefreshPassives) 값으로 한 번 모은다.</summary>
    readonly struct AbilityRow
    {
        public readonly Sprite Icon;
        public readonly string Name;
        public readonly string Desc;

        /// <summary>아직 열리지 않은 칸 — 런 모드에서 카드 레벨이 모자랄 때다.</summary>
        public readonly bool Locked;

        /// <summary>각성 패시브 — 반짝이고 맨 위에 선다.</summary>
        public readonly bool Awakened;

        /// <summary>마릿수 배지에 적을 수. 0 이면 배지를 끈다 (권속 소환 줄만 쓴다 — UI 규칙 7).</summary>
        public readonly int Count;

        public AbilityRow(Sprite icon, string name, string desc, bool locked = false, bool awakened = false,
                          int count = 0)
        {
            Icon = icon; Name = name; Desc = desc; Locked = locked; Awakened = awakened; Count = count;
        }
    }

    readonly List<AbilityRow>      _abilityRows = new(8);
    readonly List<ResolvedPassive> _resolved    = new(12);

    // ── 열기 ─────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _closeBtn?.onClick.AddListener(() => Close());
        _upgradeBtn?.onClick.AddListener(HandleUpgrade);

        for (int i = 0; i < _gearButtons.Length; i++)
        {
            int slot = i;   // ⚠ 클로저가 마지막 값을 물지 않게 복사
            _gearButtons[i].onClick.AddListener(() => OpenPicker(slot));
        }

        _pickCloseBtn?.onClick.AddListener(ClosePicker);

        CacheRightLayout();
    }

    // ── 오른쪽 칸의 자리 (능력 칸 · 고유 스킬) ────────────────
    //
    //  ⚠ 프리팹이 잡아 둔 자리를 한 번만 재어 둔다
    //    LayoutSkillSection 이 스킬 칸을 옮기므로, 두 번째로 열 때는 이미
    //    옮겨진 자리를 읽게 된다. 기준은 Awake 에서 딱 한 번 잰 값이어야 한다.

    float _rowTopY;     // 첫 능력 칸의 위 끝 (칸 위에서부터의 거리)
    float _rowStep;     // 칸 하나가 먹는 세로 (칸 높이 + 사이 여백)
    float _skillHeadH;  // 스킬 칸 안에서 상자가 시작하는 자리 (제목 높이)
    float _skillBoxH;   // 상자의 기본 높이 — 남는 자리가 넉넉하면 이 값을 쓴다

    float   _descBaseH;   // 종족 소개 칸의 기본 높이 (두 줄)
    float   _descFontH;   // 소개 글의 기본 크기 — 넘칠 때만 줄였다가 되돌린다
    float[] _rowBaseY;    // 능력 칸마다 프리팹이 잡은 자리
    float   _gridBaseY;   // 아이콘 격자 자리

    void CacheRightLayout()
    {
        var r0 = (RectTransform)_passiveRoots[0].transform;
        var r1 = (RectTransform)_passiveRoots[1].transform;

        _rowTopY = -r0.anchoredPosition.y;
        _rowStep = r0.anchoredPosition.y - r1.anchoredPosition.y;

        _skillHeadH = -_skillBox.anchoredPosition.y;
        _skillBoxH  = _skillBox.sizeDelta.y;

        _descBaseH = _descText.rectTransform.sizeDelta.y;
        _descFontH = _descText.fontSize;

        _rowBaseY = new float[_passiveRoots.Length];
        for (int i = 0; i < _passiveRoots.Length; i++)
            _rowBaseY[i] = ((RectTransform)_passiveRoots[i].transform).anchoredPosition.y;

        _gridBaseY = ((RectTransform)_passiveGridRoot.transform).anchoredPosition.y;
    }

    // ── 종족 소개가 두 줄을 넘으면 칸을 늘리고 아래를 민다 (사용자 지적, 2026-09-15) ──
    //
    //  소개 칸은 두 줄(RowSm×2)로 고정돼 있었다. 오른쪽 칸 폭(≈760px)에 FontSm 이면
    //  한 줄이 스무 자 남짓이라, 줄바꿈을 넣어 두 줄로 적은 소개도 거의 다 세 줄로 접혀
    //  첫 능력 칸 위로 흘렀다 (TMP 는 넘치는 글을 칸 밖에 그냥 그린다 — UI 규칙 5).
    //
    //  ⚠ 네 줄까지는 칸을 늘리고, 그보다 길면 글을 줄인다 — 소개가 능력 칸을 다 밀어내면
    //    정작 이 창에서 읽어야 할 것이 사라진다.

    const int DescMaxLines = 4;

    /// <summary>소개 칸 높이를 글에 맞추고, 기본 높이보다 늘어난 만큼을 돌려준다.</summary>
    float FitDescription()
    {
        TextMeshProUGUI t = _descText;
        t.enableAutoSizing = false;
        t.fontSize         = _descFontH;

        float width = t.rectTransform.rect.width;
        if (width <= 1f) return 0f;   // 캔버스가 아직 폭을 안 잡았다 — 프리팹 높이 그대로

        float cap  = _descBaseH / 2f * DescMaxLines;
        float want = Mathf.Ceil(t.GetPreferredValues(t.text, width, 0f).y);
        float h    = Mathf.Clamp(want, _descBaseH, cap);

        if (want > cap)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax      = _descFontH;
            t.fontSizeMin      = _descFontH * 0.8f;
        }

        t.rectTransform.sizeDelta = new Vector2(t.rectTransform.sizeDelta.x, h);
        return h - _descBaseH;
    }

    /// <summary>능력 칸·격자를 소개가 늘어난 만큼 아래로 민다.</summary>
    void ShiftAbilityArea(float extra)
    {
        for (int i = 0; i < _passiveRoots.Length; i++)
        {
            var rt = (RectTransform)_passiveRoots[i].transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _rowBaseY[i] - extra);
        }

        var grid = (RectTransform)_passiveGridRoot.transform;
        grid.anchoredPosition = new Vector2(grid.anchoredPosition.x, _gridBaseY - extra);
    }

    /// <summary>
    /// 줄 모드로 그려도 고유 스킬 상자가 최소 높이를 지키는가.
    /// ⚠ 소개가 길어진 만큼 줄 칸이 먹을 자리가 준다 — 모자라면 아이콘 모드로 넘긴다.
    /// </summary>
    bool RowsFit(int count, float extra)
    {
        float colH = ((RectTransform)_skillRoot.transform.parent).rect.height;
        if (colH <= 1f) return true;

        bool  hasSkill = _species.ActiveSkill != ActiveSkillId.None;
        float need     = _rowTopY + extra + count * _rowStep
                       + (hasSkill ? _skillHeadH + MinSkillBoxH : 0f);

        return need <= colH;
    }

    /// <summary>도감 칸에서 부른다. 해금되지 않은 종족은 넘기지 않는다.</summary>
    public MonsterDetailPopup Setup(MonsterSpeciesData species)
    {
        _species = species;
        _runSlot = SummonDeckSlot.Empty;
        _runMode = false;
        Refresh();
        return this;
    }

    /// <summary>
    /// 전황 화면에서 부른다 — <b>지금 이 런의 값</b>을 보여 준다.
    ///
    /// ■ ⚠ 도감 모드와 무엇이 다른가
    ///   도감은 <b>종족 기본값 × 품질</b>만 보여 준다. 소환사·덱이 달라도
    ///   종족끼리 비교가 되게 하려는 화면이라 그렇다.
    ///   여기는 반대로 "지금 내 손의 이 카드가 얼마나 센가" 를 봐야 하므로
    ///   전투와 같은 MonsterStatComposer 를 지난다 — 소환력·친화·카드 레벨·
    ///   품질·시너지·특성·유물·장비가 전부 들어 있다.
    ///
    /// ⚠ 품질 개선 칸은 끈다. 그건 도감(영구 골드)의 일이라
    ///   전투 중에 열면 "지금 여기서 올릴 수 있나" 로 읽혀 갈린다.
    /// </summary>
    public MonsterDetailPopup SetupRun(MonsterSpeciesData species, in SummonDeckSlot slot)
    {
        _species = species;
        _runSlot = slot;
        _runMode = true;
        Refresh();
        return this;
    }

    // ── 런 모드 ──────────────────────────────────────────────

    bool           _runMode;
    SummonDeckSlot _runSlot;

    /// <summary>런 모드에서 쓸 지금 값. 소환사가 없으면 null 이다.</summary>
    UnitStat ComposeNow()
    {
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        if (summoner == null) return null;

        return MonsterStatComposer.Compose(_species, summoner, Grade, _runSlot.Level,
                                           lane: -1, inherit: _runSlot.InheritBonus);
    }

    // ── 그리기 ───────────────────────────────────────────────

    void Refresh()
    {
        _nameText.text = _species.DisplayName;

        // 계보 · 특징 — 업그레이드 종족이면 뿌리를 밝힌다. 뿌리 자신이면 특징만.
        MonsterSpeciesData root = _species.RootSpecies;
        string lineage = root != null && root != _species ? LocalizationManager.Instance.Format("{0} 계열", root.DisplayName) : "";
        string traits  = MonsterTraitNames.Describe(_species.Traits);

        _lineageText.text = string.IsNullOrEmpty(lineage) ? traits
                          : string.IsNullOrEmpty(traits)  ? lineage
                          : $"{lineage} · {traits}";

        RefreshPortrait();

        RefreshGrade();
        RefreshSummonCost();
        RefreshTags();
        RefreshStats();
        // ⚠ 그리는 순서가 곧 화면의 위아래다 — 패시브가 위, 고유 스킬이 아래다
        RefreshPassives();
        RefreshSkill();
        RefreshGear();
        RefreshUpgrade();

        ClosePicker();
    }

    void RefreshGrade()
    {
        UnitGrade grade = Grade;
        Color     color = GradeStyle.GetColor(grade);

        _gradeBorder.color = color;
        _gradeBadge.color  = color;
        _gradeText.text    = GradeStyle.GetLabel(grade);
        _gradeText.color   = color;
    }

    /// <summary>
    /// ⚠ 장비가 깎는 카드 비용을 함께 뺀다 (MonsterGearRule.ManaCutFor)
    ///   장비 상세에서 "카드 비용 −1" 을 봤는데 여기 숫자가 그대로면 무엇을 샀는지
    ///   안 보인다. 깎였으면 초록 — 하단 카드 바의 '할인' 색과 같은 규칙이다.
    ///   하한 1 은 전투와 같다 (SummonerPerkRuntime.ManaCostFor).
    /// </summary>
    void RefreshSummonCost()
    {
        int cut = MonsterGearRule.ManaCutFor(_species.Id);

        _manaText.text  = Mathf.Max(1f, _species.ManaCost - cut).ToString("0.#");
        _manaText.color = cut > 0 ? ManaDiscountC : Color.white;
        _countText.text = _species.SummonCount.ToString();

        bool ranged = _species.AttackKind == MonsterAttackKind.Ranged;
        _kindText.text = ranged ? "원거리" : "근접";
    }

    /// <summary>
    /// 시너지 표식 — 이 종족이 어느 계열에 세는지.
    ///
    /// ⚠ 이름만 적지 않는다 — 아이콘이 정본 표기다
    ///   상단 시너지 줄·카드 3택이 전부 [아이콘][숫자] 라, 여기만 글자로 적으면
    ///   같은 시너지를 화면마다 다른 모양으로 읽어야 한다.
    ///
    /// ⚠ 이름과 그림만으로는 "동·은·금이 각각 무엇을 주는가" 를 말할 수 없다
    ///   그 설명은 칩에 올리면(터치는 누르면) 뜬다 — SynergyChipUI 가 맡는다.
    ///   상단 시너지 줄·카드 3택과 **같은 컴포넌트**라 글도 동작도 저절로 같다.
    ///   ⚠ Setup 을 빠뜨리면 칩은 그려지는데 올려도 아무것도 안 뜬다.
    ///     칸은 종족마다 다른 시너지를 맡으므로 매번 다시 알려 줘야 한다.
    /// </summary>
    void RefreshTags()
    {
        int slot = 0;

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            if (slot >= _tagRoots.Length) break;
            if ((_species.Tags & tag) == 0) continue;

            int    idx  = MonsterSynergyRule.IndexOf(tag);
            Sprite icon = (_synergyIcons != null && idx >= 0 && idx < _synergyIcons.Length)
                        ? _synergyIcons[idx] : null;

            _tagIcons[slot].sprite  = icon;
            _tagIcons[slot].enabled = icon != null;
            _tagNames[slot].text    = MonsterSynergyRule.NameOf(tag);
            _tagHovers[slot].Setup(tag);

            _tagRoots[slot].SetActive(true);
            slot++;
        }

        // 남는 칸은 끄고 표식도 지운다 — 꺼진 칸이 옛 시너지를 들고 있으면
        // 다른 종족을 열었을 때 그 칸이 다시 켜지며 남의 설명을 띄운다.
        for (int i = slot; i < _tagRoots.Length; i++)
        {
            _tagHovers[i].Setup(MonsterTag.None);
            _tagRoots[i].SetActive(false);
        }
    }

    /// <summary>
    /// 값은 <b>종족 기본값 × 품질 배율</b>이다 (파일 머리 주석 참고).
    ///
    /// ⚠ 품질이 곱해지는 것은 체력·공격력뿐이다
    ///   사거리·이동속도·연사는 종족의 정체성이라 품질이 건드리지 않는다
    ///   (MonsterStatComposer 와 같은 규칙). 여기서만 곱하면 도감 숫자와
    ///   실제 전투가 어긋난다.
    ///
    /// ⚠ 그 둘만 다음 값을 함께 그린다 ("99 › 109")
    ///   개선 버튼이 무엇을 사는 것인지 누르기 전에 보여야 한다. 안 바뀌는
    ///   행에까지 화살표를 그리면 전부 오르는 것처럼 읽힌다.
    /// </summary>
    void RefreshStats()
    {
        // ⚠ 배출 등급 보너스도 함께 곱한다 — 전투와 같은 자리다
        //   (MonsterStatComposer ②). 종족마다 고정이라 소환사·덱과 무관하고,
        //   빼 두면 느린 종족의 도감 숫자가 실제보다 낮게 적힌다.
        float pace = SpawnPaceRule.StatMultiplierFor(_species);

        float mult = GradeMultiplier          * pace;
        float next = MultiplierFor(Grade + 1) * pace;
        bool  max  = MonsterGradeUpgradeRule.IsMax(Grade);

        // ── 런 모드 — 전투와 같은 값으로 갈아 끼운다 ──
        //   ⚠ 품질 미리보기(Growing)는 쓰지 않는다. 지금 값 하나만 보여 준다 —
        //     "다음 등급이면 얼마" 는 도감에서 볼 이야기다.
        UnitStat now = _runMode ? ComposeNow() : null;

        if (now != null)
        {
            for (int i = 0; i < StatRows.Length && i < _statValues.Length; i++)
            {
                _statValues[i].text = StatRows[i].kind switch
                {
                    StatKind.Hp          => $"{now.Get(StatType.MaxHp):0}",
                    StatKind.Attack      => $"{now.Get(StatType.Attack):0}",   // 공·체는 반올림 정수 (사용자 지시, 2026-09-12)
                    StatKind.Defense     => $"{now.Get(StatType.Defense) * 100f:0.#}%",
                    StatKind.SpawnPace   => SpawnPaceRule.DescribeFor(_species),
                    StatKind.Range       => $"{now.Get(StatType.AttackRange):0.#}",
                    StatKind.AttackSpeed => LocalizationManager.Instance.Format("{0:0.##} 회/초", now.Get(StatType.AttackSpeed)),
                    StatKind.MoveSpeed   => $"{now.Get(StatType.MoveSpeed):0.##}",

                    // ⚠ 치명타 두 줄을 빠뜨리지 말 것 (2026-09-13)
                    //   `_ =>` 로 흘려보내면 프리팹에 구워진 기본 문구("0")가 그대로 남아
                    //   런 모드에서만 "치명타 확률 0 · 치명타 피해 0" 으로 보인다.
                    //   값은 도감 모드와 같은 자리에서 나온다 (MonsterStatComposer ③).
                    StatKind.CritChance  => $"{now.Get(StatType.CritChance) * 100f:0.#}%",
                    StatKind.CritDamage  => $"{now.Get(StatType.CritDamage) * 100f:0}%",

                    _                    => _statValues[i].text,
                };
            }
            return;
        }

        // ⚠ 장비 몫은 **품질 배율 밖**에서 얹는다 — 전투도 그렇다 (MonsterStatComposer ⑦)
        //   비율(체력 +5%)은 그 시점의 값에, 절대값은 그 뒤에. 순서까지 WithGear 가 전투와 같다.
        for (int i = 0; i < StatRows.Length && i < _statValues.Length; i++)
        {
            _statValues[i].text = StatRows[i].kind switch
            {
                // 체력만 빠른 종족 보너스를 받는다 — 전투와 같은 자리 (MonsterStatComposer ②, SpeedHpRule)
                StatKind.Hp          => Growing(WithGear(StatType.MaxHp,  _species.MaxHp  * mult * SpeedHpRule.HpMultiplierFor(_species)),
                                                WithGear(StatType.MaxHp,  _species.MaxHp  * next * SpeedHpRule.HpMultiplierFor(_species)), "0",   max),
                StatKind.Attack      => Growing(WithGear(StatType.Attack, _species.Attack * mult),
                                                WithGear(StatType.Attack, _species.Attack * next), "0",   max),
                StatKind.Defense     => $"{WithGear(StatType.Defense, _species.Defense) * 100f:0.#}%",

                // 늦게 나오는 대가로 붙은 공/체 보너스를 같은 줄에 적는다 —
                // 간격만 적으면 손해로만 읽힌다 (SpawnPaceRule).
                StatKind.SpawnPace   => SpawnPaceRule.DescribeFor(_species),
                StatKind.Range       => WithGear(StatType.AttackRange, _species.AttackRange).ToString("0.#"),
                StatKind.AttackSpeed => LocalizationManager.Instance.Format("{0:0.##} 회/초",
                                        WithGear(StatType.AttackSpeed, _species.AttackSpeed)),
                StatKind.MoveSpeed   => WithGear(StatType.MoveSpeed, _species.MoveSpeed).ToString("0.##"),
                StatKind.CritChance  => $"{WithGear(StatType.CritChance, _species.CritChance) * 100f:0.#}%",
                StatKind.CritDamage  => $"{WithGear(StatType.CritDamage, _species.CritDamage) * 100f:0}%",
                _                    => "",
            };
        }
    }

    /// <summary>
    /// 장비를 얹기 전 값 <paramref name="before"/> 에 지금 낀 장비를 얹은 값.
    ///
    /// ⚠ 여기서 규칙을 다시 짜지 않는다
    ///   열려 있는 칸만 세는 것도, 레벨·강화도, 비율이 먼저라는 것도 MonsterGearRule 이
    ///   정한다. UnitStat 을 한 벌 만들어 그쪽에 물어보면 두 곳이 갈릴 수가 없다.
    /// </summary>
    float WithGear(StatType type, float before)
    {
        var probe = new UnitStat();
        probe.Set(type, before);
        MonsterGearRule.ApplyStats(probe, _species.Id);
        return probe.Get(type);
    }

    /// <summary>"99" 또는 "99 › 109" — 만렙이면 화살표가 갈 곳이 없다.</summary>
    static string Growing(float now, float then, string fmt, bool max)
    {
        string a = now.ToString(fmt);
        if (max) return a;

        return $"{a} <size=80%><color=#{PreviewHex}>› {then.ToString(fmt)}</color></size>";
    }

    /// <summary>
    /// 고유 액티브 스킬. 없는 종족이 더 많으므로 통째로 껐다 켠다.
    ///
    /// ⚠ 아이콘은 전용 세트를 만들지 않는다 — 액티브 스킬 아이콘을 그대로 쓴다
    ///   (ActiveSkillIdExtensions.IconKey → SpriteManager). 같은 스킬을 용사도
    ///   소환사도 쓰므로 화면마다 그림이 갈리면 안 된다.
    /// </summary>
    void RefreshSkill()
    {
        ActiveSkillId id = _species.ActiveSkill;

        var db   = ActiveSkillDatabase.Current;
        var data = id == ActiveSkillId.None || db == null ? null : db.Get(id);

        _skillRoot.SetActive(data != null);
        if (data == null) return;

        Sprite icon = SpriteManager.Instance.Get(id.IconKey());

        _skillIcon.sprite  = icon;
        _skillIcon.enabled = icon != null;

        _skillName.text = string.IsNullOrEmpty(data.SkillName) ? id.ToString() : data.SkillName;
        _skillDesc.text = data.Description;

        // ⚠ SO 원본 쿨다운이다 — 술법 시너지가 실제로는 이걸 깎는다
        //   (MonsterRuntimeBridge.BuildSkillSlot). 스탯과 같은 이유로 기준값을 적는다.
        _skillCooldown.text = LocalizationManager.Instance.Format("{0:0.#}초", data.Cooldown);
    }

    /// <summary>
    /// 능력 칸 — <b>축이 둘이다.</b> 한 목록에 이어 그린다.
    ///
    ///   ① 종족 패시브(SpeciesPassive) — 계보로 물려받은 것 + 융합으로 배운 것
    ///   ② 카드 레벨이 여는 패시브(PassiveSkillType) — 원작 패시브 축
    ///
    /// ■ ⚠ 둘을 한 목록에 두는 이유 (사용자 지적, 2026-09-09)
    ///   슬라임은 Lv4 에 '피격 시 방어율 증가'(DefenseShield)를 얻는데, 그 사실이
    ///   카드 3택의 한 줄로 한 번 스쳐 갈 뿐 <b>어느 화면에도 남지 않았다.</b>
    ///   플레이어에게 둘은 그냥 "이 몬스터가 가진 패시브" 다 — 축이 갈리는 것은
    ///   우리 사정이지 화면의 사정이 아니다.
    ///
    /// ■ 순서 — 종족 → 레벨 개방 → 융합
    ///   위로 갈수록 "그 종족이면 언제나 있는 것" 이고 아래로 갈수록 이번 런에서
    ///   붙인 것이다. 융합 것이 맨 아래라야 재료로 쓴 카드가 무엇을 남겼는지 읽힌다.
    ///
    /// ⚠ 이름·설명을 손으로 적지 않는다
    ///   종족 쪽은 SpeciesPassive.ToKorean/Describe (수치는 SpeciesPassiveRule),
    ///   레벨 쪽은 PassiveSkillDatabase 의 SO 가 정본이다.
    /// </summary>
    void RefreshPassives()
    {
        _descText.text = _species.Description;

        float extra = FitDescription();
        ShiftAbilityArea(extra);

        _abilityRows.Clear();

        // ⚠ 전투와 **같은 함수**로 모은다 (PassiveResolver) — 선천 · 융합 · 장비 + 각성.
        //   한때 여기만 융합을 빠뜨려(2026-09-07) 배운 패시브가 화면에서 사라졌다.
        //   ⚠ 융합은 런 모드에서만이다 — 도감에서 열 때는 카드가 없다.
        //     장비는 **두 모드 다** 들어간다 — 도감에 끼워 두는 영구 데이터다.
        PassiveResolver.ResolveFor(_species, _runMode ? _runSlot : SummonDeckSlot.Empty, _resolved);

        // ① 각성 — 맨 위 (사용자 지시). 둘이 모여 생긴 것이라 가장 먼저 보여야 한다.
        foreach (ResolvedPassive r in _resolved)
            if (r.IsAwakened) AddSpeciesRow(r);

        // ② 선천 — "그 종족이면 언제나 있는 것"
        foreach (ResolvedPassive r in _resolved)
            if (!r.IsAwakened && (r.Origins & PassiveOrigin.Innate) != 0) AddSpeciesRow(r);

        // ②-b 권속 소환 — 2차 업그레이드가 늘 갖는 능력이라 선천 바로 뒤
        AddBroodRow();

        // ③ 카드 레벨이 여는 것
        AddLevelRows();

        // ④ 융합 · 장비 — 이번 런·도감에서 붙인 것
        foreach (ResolvedPassive r in _resolved)
            if (!r.IsAwakened && (r.Origins & PassiveOrigin.Innate) == 0) AddSpeciesRow(r);

        // ⚠ 줄 칸보다 많으면 **아이콘 모드**로 바꾼다 (사용자 지시, 2026-09-10)
        //   장비 패시브(칸 3 × Lv4·Lv5)가 붙으면 최대 12개가 된다. 줄(94px)로는
        //   7개부터 칸 밖으로 넘친다 — 오른쪽 칸 910 − 머리 183 = 727 이 전부다.
        //   경계는 줄 칸 수(_passiveRoots, Creator 의 PassiveSlots)가 정한다.
        bool iconMode = _abilityRows.Count > _passiveRoots.Length
                     || !RowsFit(_abilityRows.Count, extra);
        _passiveGridRoot.SetActive(iconMode);

        LayoutSkillSection(extra + (iconMode ? FillPassiveGrid() : FillPassiveRows()));
    }

    /// <summary>줄 모드 — 아이콘 · 이름 · 설명. 쓴 세로를 돌려준다.</summary>
    float FillPassiveRows()
    {
        int shown = Mathf.Min(_abilityRows.Count, _passiveRoots.Length);

        for (int i = 0; i < _passiveRoots.Length; i++)
        {
            bool show = i < shown;
            _passiveRoots[i].SetActive(show);
            if (!show) continue;

            AbilityRow row = _abilityRows[i];

            _passiveIcons[i].sprite  = row.Icon;
            _passiveIcons[i].enabled = row.Icon != null;
            _passiveIcons[i].color   = row.Locked ? LockedC : Color.white;

            _passiveNames[i].text  = row.Name;
            _passiveNames[i].color = row.Locked ? LockedC : Color.white;

            _passiveDescs[i].text  = row.Desc;
            _passiveDescs[i].color = row.Locked ? LockedC : DescC;

            // ⚠ 설명 칸은 한 줄이다 (88px 줄 여섯 칸) — 번역문이 길면 말줄임(…)으로 잘린다.
            //   그래서 줄에 올리거나 누르면 **전문이 툴팁으로** 뜬다 (아이콘 모드와 같은 부품).
            //   (사용자 지적, 2026-09-17 — 스페인어 패시브 설명이 칸 끝에서 잘렸다)
            if (!_passiveRoots[i].TryGetComponent(out InfoIconUI rowTip))
            {
                rowTip = _passiveRoots[i].AddComponent<InfoIconUI>();
                if (_passiveRoots[i].TryGetComponent(out Image rowBg)) rowBg.raycastTarget = true;
            }
            rowTip.Setup(row.Name, row.Desc);

            _passiveShines[i].SetActive(row.Awakened);

            _passiveCountRoots[i].SetActive(row.Count > 0);
            if (row.Count > 0) _passiveCountTexts[i].text = row.Count.ToString();
        }

        return shown * _rowStep;
    }

    /// <summary>
    /// 아이콘 모드 — 그림만 늘어놓고 설명은 툴팁으로. 쓴 세로를 돌려준다.
    /// ⚠ 순서는 줄 모드와 같다 (종족 → 레벨 → 융합) — 모드가 바뀌어도 자리가 안 뒤섞인다.
    /// </summary>
    float FillPassiveGrid()
    {
        foreach (var r in _passiveRoots) r.SetActive(false);

        int n = Mathf.Min(_abilityRows.Count, _passiveGridIcons.Length);

        for (int i = 0; i < _passiveGridIcons.Length; i++)
        {
            GameObject cell = _passiveGridTips[i].gameObject;
            bool show = i < n;
            cell.SetActive(show);
            if (!show) continue;

            AbilityRow row = _abilityRows[i];

            _passiveGridIcons[i].sprite  = row.Icon;
            _passiveGridIcons[i].enabled = row.Icon != null;
            _passiveGridIcons[i].color   = row.Locked ? LockedC : Color.white;

            _passiveGridTips[i].Setup(row.Name, row.Desc);
            _passiveGridShines[i].SetActive(row.Awakened);
        }

        int cols = Mathf.Max(1, _passiveGrid.constraintCount);
        int rows = (n + cols - 1) / cols;

        return rows * _passiveGrid.cellSize.y + Mathf.Max(0, rows - 1) * _passiveGrid.spacing.y + GridBottomGap;
    }

    /// <summary>아이콘 격자와 고유 스킬 칸 사이 여백.</summary>
    const float GridBottomGap = 14f;

    static readonly Color DescC   = new Color(0.62f, 0.67f, 0.82f, 1f);   // Creator 의 SubText
    static readonly Color LockedC = new Color(0.40f, 0.43f, 0.54f, 1f);

    const string LearnedHex = "8FD4FF";   // 융합으로 배운 것
    const string LevelHex   = "C8E08A";   // 카드 레벨이 연 것
    const string GearHex    = "F0B87A";   // 장비가 연 것
    const string AwakenHex  = "FFD34A";   // 각성 — 재료 표기

    /// <summary>
    /// 패시브 한 줄. 어디서 왔는지 표시를 달리한다.
    ///
    /// ⚠ 각성은 "대분열 (분열 + 분열)" 로 적는다 (사용자 지시, 2026-09-10)
    ///   무엇 둘이 모여 무엇이 됐는지가 이름 옆에 있어야 한다. 원래 패시브는
    ///   목록에서 사라지므로(PassiveResolver) 여기서 말하지 않으면 없어진 것으로 읽힌다.
    /// </summary>
    void AddSpeciesRow(in ResolvedPassive r)
    {
        SpeciesPassive p     = r.Passive;
        int            index = SpeciesPassiveRule.IndexOf(p);
        Sprite         icon  = (_speciesIcons != null && index >= 0 && index < _speciesIcons.Length)
                             ? _speciesIcons[index] : null;

        string name;
        if (r.IsAwakened)
        {
            string b = r.Base.ToKorean();
            name = $"{p.ToKorean()}  <color=#{AwakenHex}>({b} + {b})</color>";
        }
        else if ((r.Origins & PassiveOrigin.Learned) != 0 && (r.Origins & PassiveOrigin.Innate) == 0)
            name = LocalizationManager.Instance.Format("{0}  <color=#{1}>(융합)</color>", p.ToKorean(), LearnedHex);
        else if ((r.Origins & PassiveOrigin.Gear) != 0 && (r.Origins & PassiveOrigin.Innate) == 0)
            name = LocalizationManager.Instance.Format("{0}  <color=#{1}>(장비)</color>", p.ToKorean(), GearHex);
        else
            name = p.ToKorean();

        _abilityRows.Add(new AbilityRow(icon, name, p.Describe(), awakened: r.IsAwakened));
    }

    /// <summary>
    /// 권속 소환 — 2차 업그레이드가 주기적으로 제 하위 종족을 불러낸다 (사용자 지시, 2026-09-15).
    ///
    /// ■ 실제로는 스킬(ActiveSkillId.SummonBrood)인데 패시브 줄에 둔다
    ///   고유 스킬 칸은 희귀 스킬 설명만으로 빠듯하고, 권속은 누르지 않아도 늘 도는 능력이라
    ///   "이 몬스터가 가진 것" 목록에 서는 편이 읽힌다. 줄이 넘치면 아이콘 모드가 받는다.
    ///
    /// ⚠ 마릿수는 글로 적지 않는다 — 줄 오른쪽 위 배지다 (UI 규칙 7).
    ///   그림은 불러내는 종족의 초상화 — 무엇을 부르는지가 그림으로 읽힌다.
    /// </summary>
    void AddBroodRow()
    {
        MonsterSpeciesData brood = _species.BroodSpecies;
        if (brood == null || _species.BroodCount <= 0) return;

        _abilityRows.Add(new AbilityRow(MonsterPortraitProvider.Get(brood),
                                        "권속 소환",
                                        LocalizationManager.Instance.Format("{0:0.#}초마다 {1} 소환", _species.BroodBaseCooldown, brood.DisplayName),
                                        count: _species.BroodCount));
    }

    /// <summary>
    /// 카드 레벨이 여는 패시브 (+ 종족이 언제나 갖는 원작 패시브).
    ///
    /// ⚠ 아직 못 연 칸도 그린다 — 흐리게, "Lv4" 를 달아서
    ///   레벨 표가 <b>종족마다 미리 정해져 있는</b> 것이 이 축의 설계다
    ///   (MonsterLevelBonus 파일 머리). "이 카드를 더 키우면 무엇이 되는가" 를
    ///   보여 주지 않으면 그 설계가 화면에 하나도 안 남는다.
    ///   도감 모드에는 카드가 없으므로 전부 열린 것으로 그린다 — 그쪽은
    ///   "이 종족이 무엇을 갖는가" 를 묻는 화면이다.
    /// </summary>
    void AddLevelRows()
    {
        var db = PassiveSkillDatabase.Current;

        // 레벨과 무관하게 늘 붙는 것 (지금 데이터에는 없지만 축은 열려 있다)
        for (int i = 0; i < _species.Passives.Length; i++)
            AddSkillPassiveRow(db, _species.Passives[i], level: 0);

        // [0] = Lv2 · [1] = Lv3 … (MonsterSpeciesData.LevelBonuses)
        MonsterLevelBonus[] bonuses = _species.LevelBonuses;
        for (int i = 0; i < bonuses.Length; i++)
            AddSkillPassiveRow(db, bonuses[i].Passive, level: i + 2);
    }

    void AddSkillPassiveRow(PassiveSkillDatabase db, PassiveSkillType type, int level)
    {
        if (type == PassiveSkillType.None) return;

        PassiveSkillData data = db.Get(type);

        // DB 가 아직 안 구워졌으면 enum 이름으로 떨어진다 (MonsterLevelBonus 와 같은 규칙)
        string label = data != null && !string.IsNullOrEmpty(data.SkillName)
                     ? data.SkillName : type.ToString();

        bool   locked = _runMode && level > 0 && _runSlot.Level < level;
        string tag    = level > 0
                      ? $"  <color=#{(locked ? "6E7690" : LevelHex)}>Lv{level}</color>" : "";

        _abilityRows.Add(new AbilityRow(data != null ? data.Icon : null,
                                        label + tag,
                                        data != null ? data.Description : "",
                                        locked));
    }

    /// <summary>
    /// 고유 스킬 칸을 <b>마지막 능력 칸 바로 아래</b>로 옮긴다.
    ///
    /// ■ ⚠ 칸이 여섯이라 고정 자리로는 세로가 모자란다
    ///   여섯을 다 쓰는 것은 (계보 2 + 레벨 1 + 융합 3) 뿐이고 보통은 둘셋이다.
    ///   고정 자리로 두면 칸이 둘일 때 스킬 칸이 네 칸 아래에 홀로 떠 있고,
    ///   여섯일 때는 팝업 밖으로 밀려난다. 쓰는 칸 수에 맞춰 붙여 올린다.
    ///
    /// ⚠ 상자는 남는 만큼만 편다
    ///   여섯 칸 + 고유 스킬이 함께 오는 종족은 화염 멧돼지 하나뿐이고 그때만
    ///   자리가 모자란다. TMP 는 넘치는 글을 상자 밖으로 그냥 흘리므로(UI 규칙 5)
    ///   상자를 줄여 두는 편이 팝업 밖으로 새는 것보다 낫다.
    /// </summary>
    /// <param name="usedHeight">능력 칸(줄 또는 아이콘 격자)이 쓴 세로.</param>
    void LayoutSkillSection(float usedHeight)
    {
        var rt  = (RectTransform)_skillRoot.transform;
        float top = _rowTopY + usedHeight;

        rt.anchoredPosition = new Vector2(0f, -top);

        // ⚠ 칸 높이는 캔버스가 잡아 준 값이다 — 첫 프레임에 0 이면 기본 높이를 쓴다
        //   (0 을 그대로 빼면 상자가 최소 높이로 굳는다)
        float colH = ((RectTransform)rt.parent).rect.height;
        float room = colH > 1f ? colH - top - _skillHeadH : _skillBoxH;

        _skillBox.sizeDelta = new Vector2(_skillBox.sizeDelta.x,
                                          Mathf.Clamp(room, MinSkillBoxH, _skillBoxH));
    }

    /// <summary>스킬 상자가 줄어들 수 있는 한계. 아이콘·이름 줄은 지켜야 한다.</summary>
    const float MinSkillBoxH = 110f;

    // ── 장비 ─────────────────────────────────────────────────
    //
    //  ■ ⚠ 칸 수를 정하는 것은 <b>이 종족의 품질</b>이다 (사용자 확정, 2026-09-06)
    //    장비 자신의 등급이 아니다. 그래서 이 구역이 품질 개선 패널 바로 옆에
    //    있어야 말이 된다 — 품질을 올리면 여기 칸이 하나 열린다.
    //    ⚠ 숫자를 여기 적지 말 것 — MonsterGearRule.SlotsFor 가 정본이다.
    //
    //  ■ 잠긴 칸도 보여 준다
    //    감추면 "칸이 더 있다" 는 사실 자체를 모른다. 자물쇠와 함께
    //    "희귀부터" 라고 적어 두면 품질 개선이 무엇을 사는지가 화면에서 이어진다.
    //
    //  ■ 몸 형태가 맞는 장비만 낄 수 있다
    //    인간형은 레이어를 갈아 끼우고 비인간형은 색조·덩치로 받는다.
    //    한쪽 장비를 반대쪽에 끼우면 보여 줄 것이 없다 (MonsterGearData 주석).

    /// <summary>카드 비용이 깎였을 때의 초록. SummonCardUI 의 '할인' 색과 같게 유지할 것.</summary>
    static readonly Color ManaDiscountC = new Color(0.45f, 0.92f, 0.55f, 1f);

    static readonly Color GearEmptyC  = new Color(0.26f, 0.29f, 0.42f, 1f);
    static readonly Color GearLockedC = new Color(0.16f, 0.18f, 0.26f, 1f);
    static readonly Color GearDimText = new Color(0.44f, 0.48f, 0.60f, 1f);

    void RefreshGear()
    {
        var inv = UserDataManager.Instance.Get<MonsterGearInventory>();
        var db  = MonsterGearDatabase.Current;

        int open = MonsterGearRule.SlotsOf(_species.Id);
        IReadOnlyList<string> worn = inv.EquippedOn(_species.Id);

        // ⚠ 런 중에는 갈아 끼울 수 없다 (CodexEditLock)
        //   칸은 그대로 보여 준다 — 무엇을 끼고 있는지는 전황에서도 봐야 한다.
        //   막는 것은 **누르는 것**뿐이다.
        bool locked = CodexEditLock.Locked;

        for (int i = 0; i < _gearSlots.Length; i++)
        {
            bool unlocked = i < open;

            _gearLocks[i].SetActive(!unlocked);
            _gearButtons[i].interactable = unlocked && !locked;

            if (!unlocked)
            {
                // 잠긴 칸 — 무엇을 하면 열리는지 적는다.
                _gearFrames[i].color  = GearLockedC;
                _gearIcons[i].enabled = false;
                _gearLabels[i].text   = LocalizationManager.Instance.Format("{0}부터", GradeStyle.GetLabel(MonsterGearRule.GradeToOpen(i)));
                _gearLabels[i].color  = GearDimText;
                continue;
            }

            MonsterGearData gear = i < worn.Count && db != null ? db.Get(worn[i]) : null;

            if (gear == null)
            {
                _gearFrames[i].color  = GearEmptyC;
                _gearIcons[i].enabled = false;
                _gearLabels[i].text   = "비었다";
                _gearLabels[i].color  = GearDimText;
                continue;
            }

            Color grade = GradeStyle.GetColor(gear.Grade);

            _gearFrames[i].color  = grade;
            _gearIcons[i].sprite  = gear.Icon;
            _gearIcons[i].enabled = gear.Icon != null;
            _gearLabels[i].text   = $"{gear.DisplayName}\n<size=80%>{LevelTag(inv, gear)}</size>";
            _gearLabels[i].color  = grade;
        }

        // 아래 한 줄 — 런 중에 왜 눌리지 않는지만 적는다.
        //   ⚠ 칸 수·"품질을 올리면 늘어난다" 는 뺐다 (사용자 지시, 2026-09-12) —
        //     잠긴 칸의 자물쇠와 "희귀부터" 가 이미 그 말을 한다.
        _gearHint.text = locked ? CodexEditLock.Reason : "";
    }

    // ── 장비 고르기 ──────────────────────────────────────────
    //
    //  ■ 전용 팝업을 만들지 않았다
    //    PopupType 을 하나 더 만들면 PopupManager 프리팹 배열에 등록하는 단계가
    //    늘고, 팝업 위에 팝업이 겹친다. 이 창 안에 겹쳐 뜨는 판 하나면 된다 —
    //    고르는 동안에도 뒤에 그 몬스터의 스탯이 보이는 편이 낫다.
    //
    //  ⚠ 칸은 런타임에 만든다
    //    보유한 장비 수가 계속 늘어난다. Creator 가 N칸을 미리 구우면 그 수를
    //    넘는 순간 조용히 잘린다. 템플릿 하나를 복제해 쓰고 남으면 끈다.

    /// <summary>지금 고르는 중인 칸. −1 = 목록이 닫혀 있다.</summary>
    int _pickSlot = -1;

    readonly List<GameObject> _pickCells = new(16);

    void OpenPicker(int slot)
    {
        _pickSlot = slot;
        _pickRoot.SetActive(true);

        var inv = UserDataManager.Instance.Get<MonsterGearInventory>();
        var db  = MonsterGearDatabase.Current;

        IReadOnlyList<string> worn = inv.EquippedOn(_species.Id);
        MonsterGearData       cur  = slot < worn.Count && db != null ? db.Get(worn[slot]) : null;

        _pickTitle.text = cur != null
            ? LocalizationManager.Instance.Format("{0}번 칸 — {1}", slot + 1, cur.DisplayName)
            : LocalizationManager.Instance.Format("{0}번 칸", slot + 1);

        int shown = FillPickList(inv, db);

        _pickEmpty.gameObject.SetActive(shown == 0);
        _pickEmpty.text = inv.HasAny
            ? LocalizationManager.Instance.Format("이 몬스터에 맞는 장비가 없다 ({0} 전용)",
                  _species.BodyType == MonsterBodyType.Humanoid ? "인간형" : "비인간형")
            : "아직 장비가 없다 — 런을 끝내면 보상 상자가 하나 나온다";
    }

    /// <summary>목록을 채우고 그린 칸 수를 돌려준다.</summary>
    int FillPickList(MonsterGearInventory inv, MonsterGearDatabase db)
    {
        foreach (var cell in _pickCells) cell.SetActive(false);

        if (db == null) return 0;

        int used = 0;

        // ── ① 이 몬스터가 낀 것 — 맨 앞, "장착 중" (사용자 지시, 2026-09-12) ──
        //   지금 고르는 칸의 것이 첫째, 다른 칸의 것이 그 뒤. 누르면 상세(레벨업)가 열린다 —
        //   예전의 [상세] 버튼이 하던 일이다.
        IReadOnlyList<string> mine = inv.EquippedOn(_species.Id);
        if (_pickSlot < mine.Count) BindMine(mine[_pickSlot]);
        for (int i = 0; i < mine.Count; i++)
            if (i != _pickSlot) BindMine(mine[i]);

        void BindMine(string id)
        {
            MonsterGearData gear = db.Get(id);
            if (gear == null) return;
            BindPickCell(CellAt(used++), gear, _species.Id);
        }

        // ── ② 나머지 — 다른 몬스터가 낀 것은 그 몬스터의 초상화를 단다. 고르면 벗겨 옮긴다 ──
        foreach (string id in inv.OwnedIds)
        {
            MonsterGearData gear = db.Get(id);
            if (gear == null || !gear.Fits(_species)) continue;

            string wearer = inv.WearerOf(id);
            if (wearer == _species.Id) continue;   // ①에서 그렸다

            BindPickCell(CellAt(used++), gear, wearer);
        }

        return used;
    }

    GameObject CellAt(int index)
    {
        while (_pickCells.Count <= index)
        {
            var made = Instantiate(_pickCellTemplate, _pickContent);
            _pickCells.Add(made);
        }

        _pickCells[index].SetActive(true);
        return _pickCells[index];
    }

    /// <summary>
    /// 목록 칸 하나 — **그림과 이름뿐**이다 (사용자 지시, 2026-09-12 "설명이 너무 많아 보기 어렵다").
    /// 등급·레벨·능력치는 누르면 뜨는 장비 상세가 말한다.
    /// </summary>
    /// <param name="wearerId">지금 이 장비를 낀 종족. 없으면 null.</param>
    void BindPickCell(GameObject cell, MonsterGearData gear, string wearerId)
    {
        Color grade = GradeStyle.GetColor(gear.Grade);

        // ⚠ 자식 경로가 Creator 와 계약이다 (BuildPickCell 참고)
        var frame    = cell.transform.Find("Frame").GetComponent<Image>();
        var icon     = cell.transform.Find("Frame/Fill/Icon").GetComponent<Image>();
        var name     = cell.transform.Find("Frame/Fill/Name").GetComponent<TextMeshProUGUI>();
        var equipped = cell.transform.Find("Frame/Fill/Equipped").gameObject;
        var wearer   = cell.transform.Find("Frame/Fill/Wearer").gameObject;
        var portrait = cell.transform.Find("Frame/Fill/Wearer/Portrait").GetComponent<Image>();

        frame.color  = grade;
        icon.sprite  = gear.Icon;
        icon.enabled = gear.Icon != null;

        name.text  = gear.DisplayName;
        name.color = grade;

        bool mine   = wearerId == _species.Id;
        bool others = wearerId != null && !mine;

        equipped.SetActive(mine);

        // 다른 몬스터가 끼고 있으면 그 몬스터의 초상화 — 고르면 거기서 벗겨 온다.
        wearer.SetActive(others);
        if (others)
        {
            Sprite art = MonsterPortraitProvider.Get(CardCatalog.Current.GetMonster(wearerId));
            portrait.sprite  = art;
            portrait.enabled = art != null;
        }

        // ⚠ 누르면 바로 끼우지 않는다 — 상세를 먼저 연다 (2026-09-10)
        //   레벨 트랙·레벨업이 거기 있다. 끼우기는 상세의 [장착] 이 한다.
        int slot = _pickSlot;
        var btn  = cell.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OpenGearDetail(gear, slot));
    }

    /// <summary>"Lv3" · "Lv5 +4" — 칸·목록이 같은 표기를 쓴다.</summary>
    static string LevelTag(MonsterGearInventory inv, MonsterGearData gear)
    {
        int lv  = inv.LevelOf(gear.Id);
        int enh = inv.EnhanceOf(gear.Id);
        return enh > 0 ? $"Lv{lv} +{enh}" : $"Lv{lv}";
    }

    /// <summary>
    /// 장비 상세를 이 창 위에 연다.
    /// 레벨업하면 보유 수(재료로 먹힌다)·능력치가 바뀐다 — 칸·스탯·목록을 다시 그린다.
    /// </summary>
    void OpenGearDetail(MonsterGearData gear, int slot)
    {
        var popup = PopupManager.Instance.Open<GearDetailPopup>(PopupType.GearDetail);
        if (popup == null) return;

        popup.SetupForEquip(gear, _species, slot, () => HandleEquip(gear, slot), () => HandleUnequip(gear));
        popup.OnChanged = HandleGearChanged;
    }

    /// <summary>장비 상세의 [벗기기] — 이 몬스터가 낀 그 장비를 벗긴다. 목록은 다시 그린다.</summary>
    void HandleUnequip(MonsterGearData gear)
    {
        if (CodexEditLock.Locked) return;   // HandleEquip 과 같은 이유

        var inv = UserDataManager.Instance.Get<MonsterGearInventory>();
        if (!inv.Unequip(_species.Id, SlotOf(gear.Id))) return;

        RefreshPortrait();
        RefreshGear();
        RefreshStats();
        RefreshSummonCost();

        if (_pickSlot >= 0) OpenPicker(_pickSlot);

        OnUpgraded?.Invoke();
    }

    /// <summary>
    /// 초상화 — 낀 장비가 겉모습에 드러난다 (MonsterPortraitProvider 가 장비 Key 로 캐시한다).
    /// ⚠ 장비를 끼고 벗을 때마다 부른다 (2026-09-11) — 한때 창을 열 때만 그려서
    ///   끼워도 초상화가 그대로였다.
    /// </summary>
    void RefreshPortrait()
    {
        Sprite art = MonsterPortraitProvider.Get(_species);
        _portraitImage.sprite  = art;
        _portraitImage.enabled = art != null;
    }

    void HandleGearChanged()
    {
        RefreshPortrait();
        RefreshGear();
        RefreshStats();
        RefreshSummonCost();

        if (_pickSlot >= 0) OpenPicker(_pickSlot);   // 여분 수가 바뀌었다

        OnUpgraded?.Invoke();
    }

    /// <summary>
    /// 고른 칸에 끼운다 — 칸에 무엇이 있든 벗기고 끼운다 (MonsterGearInventory.EquipAt).
    /// ⚠ 목록은 닫지 않고 다시 그린다 — 방금 낀 장비가 맨 앞 "장착 중" 으로 올라온다.
    /// </summary>
    void HandleEquip(MonsterGearData gear, int slot)
    {
        // ⚠ 칸 버튼을 끄는 것으로 끝내지 않는다 — 실제로 바꾸는 자리에서 막는다.
        //   같은 함수(CodexEditLock.Locked)를 보므로 두 판정이 갈릴 일은 없다.
        if (CodexEditLock.Locked) return;

        var inv = UserDataManager.Instance.Get<MonsterGearInventory>();

        if (!inv.EquipAt(_species, gear, slot))
        {
            Debug.Log($"[MonsterDetailPopup] '{gear.DisplayName}' 을 끼우지 못했습니다: " +
                      inv.CheckAt(_species, gear, slot));
            return;
        }

        RefreshPortrait();
        RefreshGear();
        RefreshStats();
        RefreshSummonCost();

        // 빈 칸을 골랐으면 목록 끝에 붙는다 — 실제로 들어간 칸을 다시 찾는다.
        int placed = SlotOf(gear.Id);
        if (_pickSlot >= 0) OpenPicker(placed);

        UIJuice.GearEquip(_gearFrames[Mathf.Clamp(placed, 0, _gearFrames.Length - 1)].rectTransform,
                          gear.DisplayName);

        OnUpgraded?.Invoke();
    }

    /// <summary>그 장비가 들어간 칸. 못 찾으면 0.</summary>
    int SlotOf(string gearId)
    {
        IReadOnlyList<string> worn =
            UserDataManager.Instance.Get<MonsterGearInventory>().EquippedOn(_species.Id);

        for (int i = 0; i < worn.Count; i++)
            if (worn[i] == gearId) return i;

        return 0;
    }

    void ClosePicker()
    {
        _pickSlot = -1;
        _pickRoot.SetActive(false);
    }

    // ── 품질 개선 ────────────────────────────────────────────

    static readonly Color PipOff = new Color(0.20f, 0.23f, 0.33f, 1f);

    void RefreshUpgrade()
    {
        UnitGrade grade   = Grade;
        bool      max     = MonsterGradeUpgradeRule.IsMax(grade);
        var       blocked = MonsterGradeUpgradeRule.Check(_species.Id);

        // 단계 핍 — 채워진 칸이 현재 품질. 몇 칸 남았는지가 숫자 없이 읽힌다.
        Color on = GradeStyle.GetColor(grade);
        for (int i = 0; i < _gradePips.Length; i++)
            _gradePips[i].color = i <= (int)grade ? on : PipOff;

        _stepText.text = max
            ? "최고 품질"
            : $"{GradeStyle.GetLabel(grade)} <color=#{PreviewHex}>›</color> " +
              $"<color=#{ColorUtility.ToHtmlStringRGB(GradeStyle.GetColor(grade + 1))}>" +
              $"{GradeStyle.GetLabel(grade + 1)}</color>";

        // ── 런 중에는 **버튼을 통째로 감춘다** (사용자 지시, 2026-09-12) ──
        //
        //  ⚠ "런 중 잠금" 같은 글을 띄우지 않는다
        //    예전에는 버튼을 남겨 두고 라벨만 CodexEditLock.Reason 으로 바꿨다.
        //    그러면 못 누르는 버튼이 판 내내 화면에 남아, 이 창을 열 때마다
        //    "지금은 안 된다" 를 다시 읽어야 한다. 런 중에 할 수 없는 일이면
        //    그 자리는 비어 있는 편이 낫다 — 도감을 **보는** 것이 이 창의 목적이다.
        //
        //  ⚠ 단계 핍·"고급 › 희귀" 줄은 남긴다 — 그건 지금 품질을 읽는 줄이지
        //    사는 버튼이 아니다.
        bool inRun = blocked == MonsterGradeUpgradeRule.Blocked.InRun;

        _upgradeBtn.gameObject.SetActive(!inRun);
        _costRoot.SetActive(!max && !inRun);

        if (!max && !inRun)
        {
            int cost = MonsterGradeUpgradeRule.CostFor(grade);

            // 모자라면 값을 붉게 — 버튼이 안 눌리는 이유가 값에 붙어 있어야 한다.
            _costText.text  = cost.ToString("N0");
            _costText.color = blocked == MonsterGradeUpgradeRule.Blocked.NotEnoughGold
                            ? new Color(1f, 0.42f, 0.42f)
                            : new Color(1f, 0.83f, 0.30f);
        }

        // ⚠ InRun 갈래가 없다 — 위에서 버튼을 감췄으므로 적을 라벨이 없다.
        _upgradeLabel.text = blocked switch
        {
            MonsterGradeUpgradeRule.Blocked.MaxGrade      => "최고 품질",
            MonsterGradeUpgradeRule.Blocked.NotEnoughGold => "골드 부족",
            MonsterGradeUpgradeRule.Blocked.NotUnlocked   => "미해금",
            _                                             => "품질 개선",
        };

        _upgradeBtn.interactable = blocked == MonsterGradeUpgradeRule.Blocked.None;

        RefreshGold();
    }

    /// <summary>
    /// 보유 영구 골드 — 헤더에 상주한다.
    ///
    /// ⚠ 낼 값(버튼 안)과 떨어뜨려 둔다. 붙여 놓으면 어느 쪽이 지갑인지
    ///   매번 다시 읽어야 한다 (파일 머리 주석).
    /// </summary>
    void RefreshGold() => _goldText.text = MonsterGradeUpgradeRule.Wallet.ToString("N0");

    void HandleUpgrade()
    {
        if (!MonsterGradeUpgradeRule.TryUpgrade(_species.Id)) return;

        // 등급·스탯·핍·버튼·지갑이 한꺼번에 움직여야 무엇을 샀는지 읽힌다.
        RefreshGrade();
        RefreshStats();
        RefreshUpgrade();

        // ⚠ 장비 칸도 다시 그린다 — 품질이 오르면 칸이 하나 열린다
        //   (MonsterGearRule.SlotsFor). 안 그리면 "올렸는데 칸이 그대로" 로 보인다.
        RefreshGear();

        // ⚠ 갱신이 끝난 뒤에 터뜨린다 (UIJuice 파일 머리 주석)
        //   숫자가 새 값으로 바뀐 다음이라야 "오르면서 터진다" 로 읽힌다.
        //   등급업 프리셋을 쓴다 — 영구 데이터를 골드로 사는, 이 게임에서
        //   가장 무거운 성장이다.
        //   ⚠ 자리는 초상화 테두리다 — 이 창에서 등급색이 실제로 바뀌는 물건이고,
        //     유물 카드와 달리 다시 만들어지지 않아 좌표를 믿을 수 있다.
        UIJuice.GradeUp(_gradeBorder.rectTransform, GradeStyle.GetLabel(Grade));

        OnUpgraded?.Invoke();
    }

    /// <summary>
    /// 품질이 올랐다 — 뒤에 깔린 도감이 칸을 다시 그리도록 알린다.
    ///
    /// ⚠ 이 팝업이 도감을 직접 부르지 않는다
    ///   도감 말고 다른 화면에서도 열 수 있어야 한다(덱 편성 등).
    ///   부르는 쪽이 무엇을 새로 그릴지 정하는 편이 얽히지 않는다.
    /// </summary>
    public System.Action OnUpgraded;

    protected override void OnAfterClose() => OnUpgraded = null;

    // ── 조회 ─────────────────────────────────────────────────

    UnitGrade Grade
        => UserDataManager.Instance.Get<MonsterCodexData>().GetGrade(_species.Id);

    /// <summary>
    /// 품질 배율. MonsterStatComposer 의 ② 단계와 같은 식이어야 한다 —
    /// 두 곳이 갈리면 도감이 말한 값과 필드의 몬스터가 다르다.
    /// </summary>
    float GradeMultiplier => MultiplierFor(Grade);

    static float MultiplierFor(UnitGrade grade)
    {
        var   cfg  = GameplayConfig.Current;
        float coef = cfg != null ? cfg.GradeMultPerTier : 0.10f;

        return 1f + (int)grade * coef;
    }
}
