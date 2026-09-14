using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CardSelectPopup.cs
//  스테이지를 깬 뒤 뜨는 **카드 3택**.
//
//  ■ 어빌리티 선택 팝업의 자리를 이어받는다
//    원작은 여기서 어빌리티를 뽑아 부대를 강화했다. 이 게임은 그 축을 없애고
//    **몬스터·스킬 카드**를 고르게 한다 — 무엇을 키울지가 곧 빌드다.
//      새 카드 → 카드 바에 등록 + 도감 기록
//      중복    → 그 카드 레벨업
//      만렙    → 진화/융합 갈림길이 이어서 열린다
//
//  ■ ⚠ HUD 패널에서 팝업으로 옮겼다
//    예전에는 InGameHUD 안의 CardRewardUI 였다. 전투 통계를 그 위에 띄우려면
//    캔버스 두 장의 앞뒤를 손으로 관리해야 했는데, PopupManager 는 그 쌓임을
//    이미 소유하고 있다. 통계 버튼이 붙는 순간 팝업이 맞는 자리가 됐다.
//
//  ■ 닫기 버튼이 없다
//    선택이 곧 다음 스테이지 진입 신호다. 셋 중 하나는 반드시 고른다.
//    (BlockBackgroundClose = true — 바깥을 눌러도 닫히지 않는다)
//
//  ■ 상단 '전투 통계' 버튼
//    방금 판에서 어느 카드가 얼마나 때렸는지 보고 고르라는 뜻이다.
//    통계를 보고 다시 이 화면으로 돌아온다 — 선택은 그대로 남는다.
//
//  ■ 이미 가진 카드는 **얼마나 강해지는지**를 적는다 (2026-08-28)
//    예전에는 "Lv.3" 한 줄이 전부였다. 그 숫자만으로는 고를 수가 없다 —
//    레벨업이 무엇을 주는지는 종족마다 다르고(MonsterLevelBonus), 애초에
//    **이번 장으로 레벨이 오르는지조차** 알 수 없었다. 문턱이 1,2,4,7,11장이라
//    중복을 먹어도 그대로인 경우가 절반이 넘는데 화면은 늘 "Lv.3" 이라
//    올랐다고 읽혔다.
//
//    그래서 두 가지를 적는다.
//      · 레벨 변화   — "Lv.2 › Lv.3" 인지 "Lv.3 (2장 더)" 인지
//      · 열리는 효과 — 그 레벨 칸이 주는 스탯·패시브 한 줄
//    오르지 않는 경우에도 **다음 레벨에 무엇이 열리는지**를 함께 보여 준다.
//    "지금은 안 오르지만 두 장 모으면 이게 열린다" 가 곧 고를 이유이기 때문이다.
//
//  ■ ⚠ 이미 가진 카드에는 종족 패시브를 다시 적지 않는다 (2026-09-02)
//    "분열 — 죽으면 절반 크기로 둘로 나뉜다" 는 그 카드를 처음 얻을 때
//    한 번 알면 되는 내용이다. 중복 카드에 또 적으면 정작 고를 근거인
//    **이번에 무엇이 오르는가**가 그 아래로 밀려난다.
// ============================================================

public class CardSelectPopup : PopupBase
{
    [Header("헤더")]
    [Tooltip("시너지 아이콘. ⚠ MonsterSynergyRule.AllTags 순서로 넣는다 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _synergyIcons;

    [Tooltip("종족 패시브 아이콘. ⚠ SpeciesPassiveRule.All 순서로 넣는다 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _speciesIcons;

    [Tooltip("카드 사이의 가로 간격(카드 폭 + 여백). 띄운 장수에 맞춰 가운데로 다시 놓는 데 쓴다.")]
    [SerializeField] float _cardStep;

    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] Button          _statsButton;

    [Header("후보")]
    [SerializeField] OptionView[] _options;

    [Header("공통")]
    [SerializeField] Sprite _fallbackIcon;

    /// <summary>후보 한 장의 표시 묶음.</summary>
    [Serializable]
    public class OptionView
    {
        public GameObject      Root;
        public Button          Button;
        public Image           Icon;
        public TextMeshProUGUI NameText;

        [Tooltip("\"NEW\" · \"Lv.3\" · \"진화\" 가 들어가는 칸.")]
        public TextMeshProUGUI StateText;

        // ── 초상화 머리 위 배지 ──────────────────────────────
        //
        //  ⚠ 글이 아니라 Image 다 (2026-09-02)
        //    처음에는 설명 줄에 TMP 스프라이트 태그로 넣었는데, 스프라이트
        //    에셋이 등록되기 전에는 ❓ 로 그려졌다. 자리가 정해진 칸에서는
        //    진짜 Image 를 쓴다 — 하단 카드 바와 같은 방식이고, 아무 설정
        //    없이도 반드시 그려진다. (규칙 근거는 CLAUDE.md UI 규칙 7)

        [Tooltip("마나 배지의 숫자 칸. 아이콘은 프리팹이 들고 있다.")]
        public TextMeshProUGUI ManaText;

        [Tooltip("마나 배지 전체. 마나를 내지 않는 카드(시너지 강화)에서는 통째로 끈다.")]
        public GameObject ManaRoot;

        [Tooltip("마릿수 배지의 숫자 칸.")]
        public TextMeshProUGUI CountText;

        [Tooltip("마릿수 배지 전체. 한 마리짜리·스킬 카드에서는 통째로 끈다.")]
        public GameObject CountRoot;

        [Tooltip("근접 / 원거리 / 즉시 발동. 숫자와 색·크기를 달리해 섞이지 않게 한다.")]
        public TextMeshProUGUI KindText;

        [Tooltip("기본 스탯 왼쪽 칸 (공격력).")]
        public TextMeshProUGUI StatText;

        [Tooltip("기본 스탯 오른쪽 칸 (체력). 스킬 카드에서는 비운다.")]
        public TextMeshProUGUI StatText2;

        // ── 시너지 칩 ────────────────────────────────────────
        //
        //  ⚠ 글이 아니라 [아이콘][숫자] 다 (사용자 확정, 2026-09-03)
        //    시너지 이름은 그림이 대신한다. 그리고 TMP 스프라이트 태그는
        //    쓰지 않는다 — 스프라이트 에셋을 굽기 전에는 ❓ 가 그려진다
        //    (CLAUDE.md UI 규칙 7). 자리가 정해진 칸에서는 진짜 Image 를 쓴다.
        //
        //    한 종족이 갖는 표식은 최대 셋이라 칸도 셋이다.

        [Tooltip("시너지 칩 루트. 남는 칸은 끈다.")]
        public GameObject[] SynergyRoots;

        [Tooltip("시너지 칩의 그림. 런타임이 표식에 맞춰 갈아 끼운다.")]
        public Image[] SynergyIcons;

        // ⚠ 숫자 칸은 없앴다 (2026-09-03, 사용자 확정)
        //   카드에서 알고 싶은 것은 "이 몬스터가 어느 계열인가" 다. 진행도(2/4)는
        //   상단 시너지 줄이 훨씬 크게 말하고 있고, 카드에 또 적으면 세 칸이
        //   숫자로 차서 정작 그림이 안 읽힌다.

        [Tooltip("눌러서 효과를 보는 부분. 칩마다 하나씩.")]
        public SynergyChipUI[] SynergyHovers;

        // ── 종족 패시브 칩 ───────────────────────────────────
        //
        //  ⚠ 이게 곧 "이건 뭘 하는 놈인가" 다
        //    예전에는 아래 Desc 에 "분열 — 죽으면 절반 크기로 둘로 나뉜다"
        //    처럼 글로 적었다. 계보가 깊으면 네 줄이 되어 카드 세 장을 훑는
        //    동안 아무도 읽지 않았다. 그림이면 한눈에 갈리고, 설명은 칩에
        //    올리거나 눌러서 본다 (SpeciesPassiveChipUI).
        //
        //    칸은 넷 — 계보가 가장 깊은 종족이 갖는 패시브 수다.

        [Tooltip("종족 패시브 칩 루트. 남는 칸은 끈다.")]
        public GameObject[] PassiveRoots;

        [Tooltip("종족 패시브 칩의 그림. 런타임이 패시브에 맞춰 갈아 끼운다.")]
        public Image[] PassiveIcons;

        [Tooltip("눌러서 효과를 보는 부분. 칩마다 하나씩.")]
        public SpeciesPassiveChipUI[] PassiveHovers;

        [Tooltip("종족 패시브 설명. **새 카드에만** 채운다 — 이미 가진 카드는 아는 내용이다.")]
        public TextMeshProUGUI DescText;

        [Tooltip("레벨이 오르면 무엇이 열리는가. 한 줄에 하나씩. 카드 맨 아래에 놓인다.")]
        public TextMeshProUGUI GainText;
    }

    /// <summary>바깥을 눌러 넘기지 못한다 — 반드시 하나를 골라야 진행된다.</summary>
    public override bool BlockBackgroundClose => true;

    Action<CardRewardOption> _onPicked;
    readonly List<CardRewardOption> _shown = new(3);

    static readonly List<SpeciesPassive> _passiveBuffer = new(4);

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>후보를 띄운다. 하나를 고르면 onPicked 가 불리고 팝업이 닫힌다.</summary>
    public CardSelectPopup Setup(List<CardRewardOption> choices,
                                 Action<CardRewardOption> onPicked)
    {
        _onPicked = onPicked;

        _shown.Clear();
        _shown.AddRange(choices);

        if (_titleText != null) _titleText.text = "카드를 하나 고르세요";

        if (_statsButton != null)
        {
            _statsButton.onClick.RemoveAllListeners();

            // ⚠ 이 팝업을 닫지 않는다 — 통계를 그 위에 얹는다
            //   닫으면 돌아올 때 후보를 다시 뽑게 되고, 보고 온 것과 다른
            //   카드가 떠 버린다.
            _statsButton.onClick.AddListener(() =>
                PopupManager.Instance.Open<BattleStatsPopup>(PopupType.BattleStats)?.Setup());
        }

        CardCatalog catalog = CardCatalog.Current;

        for (int i = 0; i < _options.Length; i++)
        {
            OptionView view = _options[i];

            if (i >= _shown.Count)
            {
                view.Root.SetActive(false);
                continue;
            }

            view.Root.SetActive(true);
            Bind(view, _shown[i], catalog, i);
        }

        Recenter();

        return this;
    }

    /// <summary>
    /// 띄운 카드 수에 맞춰 가로 자리를 다시 잡는다.
    ///
    /// ⚠ Creator 는 <b>네 칸</b>을 굽는다 (특성 '감식안' 이 4택으로 만든다)
    ///   그런데 보통은 세 장이라, 구울 때의 고정 자리를 그대로 쓰면 네 번째
    ///   칸이 빈 채로 오른쪽에 남아 세 장이 통째로 왼쪽으로 쏠려 보인다.
    ///   자리는 "몇 장을 띄우는가" 가 정해야 한다.
    /// </summary>
    void Recenter()
    {
        float start = -(_shown.Count - 1) * 0.5f * _cardStep;

        for (int i = 0; i < _shown.Count && i < _options.Length; i++)
        {
            var rt = (RectTransform)_options[i].Root.transform;

            // 세로는 Creator 가 잡은 값을 그대로 둔다 — 헤더 높이가 걸려 있다.
            rt.anchoredPosition = new Vector2(start + i * _cardStep, rt.anchoredPosition.y);
        }
    }

    // ── 표시 ─────────────────────────────────────────────────

    // ── 카드 한 장 그리기 ────────────────────────────────────
    //
    //  ■ 무엇을 어디에 두는가 (2026-09-02 개편)
    //
    //      ┌──────────────────┐
    //      │       NEW        │  상태
    //      │ ◈8         ☠3    │  마나·마릿수 — 초상화 머리 위 배지
    //      │     (초상화)      │
    //      │      슬라임       │  이름
    //      │       근접        │  공격 형태 — 색·크기를 달리한다
    //      │ 공격력 24 | 체력 120 │  기본 스탯 — 반씩 나눈 한 줄
    //      ├──────────────────┤
    //      │   방어율 +6%p     │  얻는 것 — 한 줄에 하나, 크게
    //      │   체력 +10%       │  (새 카드에서는 종족 패시브 설명이 이 자리)
    //      └──────────────────┘
    //
    //  ■ ⚠ 마나·마릿수를 글로 적지 않는다 (확정 규칙 — CLAUDE.md UI 규칙 7)
    //    하단 카드 바가 아이콘인데 여기만 "마나 12" 라고 적으면 같은 값을
    //    화면마다 다시 읽어야 한다. 배지는 하단 카드와 **같은 빌더**로 굽는다
    //    (EditorUIBuilder.IconValueBadge).
    //
    //  ■ ⚠ 종족 패시브는 **새 카드에만** 적는다
    //    "분열 — 죽으면 절반 크기로 둘로 나뉜다" 는 그 카드를 처음 얻을 때
    //    한 번 알면 되는 내용이다. 이미 가진 카드에 또 적으면, 정작 고를
    //    근거인 **이번에 무엇이 오르는가**가 그 아래로 밀려난다.
    //    그래서 중복 카드에서는 이 칸을 비우고 레벨업 내역만 남긴다.
    //
    //  ■ ⚠ 기본 스탯은 한 줄(두 칸), 얻는 것은 한 줄에 하나 (2026-09-02)
    //    공격력·체력은 "이 카드가 원래 어떤 놈인가" 를 말하는 배경 정보다.
    //    세로로 쌓으면 아래의 **이번에 오르는 값**과 무게가 같아져 둘이
    //    구분되지 않는다. 세로로 쌓는 것은 얻는 쪽에만 쓴다 —
    //    그쪽이 카드를 고르는 실제 근거이기 때문이다.
    //    대신 한 줄 안을 **반씩 나눠** 두 값이 늘 같은 자리에 뜨게 한다
    //    (문자열로 이으면 자릿수에 따라 가운데점이 밀려 다닌다).
    //
    //  ■ ⚠ 기본 스탯은 종족 원본값이다
    //    소환력·카드 레벨 보정 전 숫자다. 세 후보를 **같은 기준**으로
    //    비교하려는 값이라, 소환사마다 달라지는 최종치를 쓰면 안 된다.

    /// <summary>공격 형태 — 숫자 줄과 섞이지 않게 색을 뗀다.</summary>
    static readonly Color MeleeColor  = new(1.00f, 0.72f, 0.45f);   // 근접 — 주황
    static readonly Color RangedColor = new(0.55f, 0.85f, 1.00f);   // 원거리 — 하늘
    static readonly Color SkillColor  = new(0.82f, 0.68f, 1.00f);   // 스킬 — 보라

    /// <summary>
    /// "공격력 24" 한 칸.
    ///
    /// ⚠ 이름표를 값보다 흐리고 작게 만든다
    ///   읽어야 하는 것은 숫자지 "공격력" 세 글자가 아니다. 같은 크기·같은
    ///   밝기로 두면 카드 세 장이 글자 덩어리로 보여 값이 눈에 안 들어온다.
    /// </summary>
    /// <summary>
    /// "공격력 8" — 이름은 흐리고 작게, 값은 밝고 크게.
    /// 값은 반올림한 정수다 (위 StatWithGain 주석 참고).
    /// </summary>
    static string Stat(string label, float value, string format = "{0:0}")
        => $"<size=80%><color=#7E88A8>{label}</color></size> "
         + string.Format(format, value);

    /// <summary>
    /// "공격력 8 +2" — 종족 기본값과 **레벨로 붙는 증가분**을 나눠 적는다.
    ///
    /// ⚠ 합친 수치 하나만 적지 않는다 (사용자 확정, 2026-09-04)
    ///   합쳐 놓으면 그 숫자가 종족이 센 것인지 내가 키운 것인지 알 수 없다.
    ///   나눠 적으면 "이 카드를 또 주우면 얼마가 더 붙나" 가 그대로 읽힌다.
    ///
    /// 증가분이 0(Lv1)이면 기본값만 적는다 — "+0" 은 정보가 아니라 잡음이다.
    /// </summary>
    static string StatWithGain(string label, float baseValue, int level)
    {
        float ratio = CardLevelRule.StatBonusRatio(level);
        if (ratio <= 0f) return Stat(label, baseValue);

        // ⚠ 반올림해서 정수로 띄운다 (사용자 확정, 2026-09-04)
        //   "+1.9" 는 읽는 데 한 박자가 더 걸리고, 카드 세 장을 나란히 비교할 때
        //   소수점 자리가 들쭉날쭉해 숫자가 눈에 안 들어온다.
        //   ⚠ 실제 스탯은 반올림하지 않는다 — 여기는 표시만이다.
        //     MonsterStatComposer 가 쓰는 값은 그대로 소수다.
        int gain = Mathf.RoundToInt(baseValue * ratio);

        return Stat(label, baseValue)
             + $" <color=#{GainHex}>+{gain}</color>";
    }

    /// <summary>증가분 색 — "얻는 것" 줄과 같은 금색이다.</summary>
    const string GainHex = "FFD34A";

    void Bind(OptionView view, in CardRewardOption option, CardCatalog catalog, int index)
    {
        Sprite icon = null;
        string name = option.Id;
        string desc = string.Empty;

        // ⚠ 먼저 지운다 — 칸은 카드마다 재사용된다
        //   몬스터 카드만 패시브를 채우므로, 끄지 않으면 스킬·시너지 강화
        //   카드에 직전 몬스터의 패시브가 남는다.
        HidePassivesFrom(view, 0);

        // ⚠ 시너지 강화는 종족 카드가 아니다 — 갈래를 먼저 나눈다
        //   아래 몬스터/스킬 경로는 CardCatalog 에서 ID 를 찾으므로, 여기서
        //   갈라내지 않으면 빈 ID 로 조회해 카드가 통째로 비어 뜬다.
        if (option.IsSynergyBoost)
        {
            BindSynergyBoost(view, option, ref icon, ref name, ref desc);
            Finish(view, option, catalog, icon, name, desc, index);
            return;
        }

        if (option.Kind == SummonKind.Monster)
            BindMonster(view, option, catalog, ref icon, ref name, ref desc);
        else
            BindSkill(view, option, catalog, ref icon, ref name, ref desc);

        Finish(view, option, catalog, icon, name, desc, index);
    }

    /// <summary>
    /// 세 갈래(몬스터·스킬·시너지 강화)가 공유하는 마무리.
    /// 그림·이름·설명을 꽂고 버튼을 새로 문다.
    /// </summary>
    void Finish(OptionView view, in CardRewardOption option, CardCatalog catalog,
                Sprite icon, string name, string desc, int index)
    {
        view.Icon.sprite  = icon != null ? icon : _fallbackIcon;
        view.Icon.enabled = view.Icon.sprite != null;

        // 친화 종족 표식 — 몬스터 카드만. 칸이 재사용되니 스킬·시너지 강화 카드에서는 끈다.
        AffinityTagUI.Set(view.Icon,
            !option.IsSynergyBoost && option.Kind == SummonKind.Monster
                ? catalog.GetMonster(option.Id)
                : null);

        view.NameText.text = name;

        // 종족 패시브는 새 카드에만 — 위 주석 참고.
        // ⚠ 진화 카드도 비운다 — 설명(DescText)과 결론(GainText)은 **같은 사각형**이다
        //   (BindSynergyBoost 주석). 진화는 아래 GainText 가 "슬라임 › 진화" 를 적으므로
        //   종족 설명까지 채우면 두 글이 겹쳐 찍힌다.
        view.DescText.text = option.IsDuplicate || option.IsEvolve ? string.Empty : desc;

        BindState(view, option);

        view.GainText.text  = DescribeGain(option, catalog);
        view.GainText.color = option.IsEvolve ? EvolveColor
                            : option.IsMaxed  ? EvolveColor
                            : option.LevelsUp ? LevelUpColor
                            :                   ProgressColor;

        // ⚠ 리스너를 매번 갈아 끼운다
        //   팝업은 풀에서 재사용된다. 지우지 않으면 지난 판의 선택지가 함께
        //   불려 카드를 여러 장 받는다.
        view.Button.onClick.RemoveAllListeners();
        view.Button.onClick.AddListener(() => Pick(index));
    }

    /// <summary>
    /// 시너지 강화 후보 한 장.
    ///
    ///   [숲 아이콘]
    ///   숲 강화        ← 이름
    ///   시너지         ← 형태 (근접/원거리 자리)
    ///   공격력 +15%  체력 +15%
    ///   시너지 카운트 +1
    ///
    /// ⚠ 이름을 "강화" 로만 적지 않는다
    ///   세 후보가 나란히 뜨는 화면이라 어느 시너지인지가 이름에 있어야 한다.
    /// </summary>
    void BindSynergyBoost(OptionView view, in CardRewardOption option,
                          ref Sprite icon, ref string name, ref string desc)
    {
        MonsterTag tag = option.BoostTag;

        int index = MonsterSynergyRule.IndexOf(tag);
        icon = (_synergyIcons != null && index >= 0 && index < _synergyIcons.Length)
             ? _synergyIcons[index] : null;

        name = $"{MonsterSynergyRule.NameOf(tag)} 강화";

        int    after = MonsterSynergyRule.CountOf(tag) + MonsterSynergyRule.BoostCount;
        int    step  = MonsterSynergyRule.NextStepAt(tag);
        string pct   = $"{MonsterSynergyRule.BoostStatBonus * 100f:0}";

        // ⚠ 설명(DescText)과 결론(GainText)은 **같은 사각형**을 쓴다
        //   둘 다 채우면 글자가 겹쳐 찍힌다 (원래 동시에 뜨지 않는 전제다 —
        //   CardSelectPopup 머리 주석 참고). 강화 카드는 결론만 쓴다.
        desc = string.Empty;

        _ = after; _ = step;   // 진행도는 아래 DescribeGain 이 적는다

        // 마나·마릿수 배지는 이 카드에 뜻이 없다 — 통째로 끈다.
        //   ⚠ "-" 를 적어 두면 "마나 0" 처럼 읽힌다. 배지 자체를 없앤다.
        view.ManaRoot .SetActive(false);
        view.CountRoot.SetActive(false);

        view.KindText.text  = "시너지";
        view.KindText.color = MonsterSynergyRule.ColorOf(
            MonsterSynergyRule.TierIfAdded(tag, alreadyCounted: false));

        view.StatText .text = $"<size=80%><color=#7E88A8>공격력</color></size> "
                            + $"<color=#{GainHex}>+{pct}%</color>";
        view.StatText2.text = $"<size=80%><color=#7E88A8>체력</color></size> "
                            + $"<color=#{GainHex}>+{pct}%</color>";

        // 강화 대상 하나만 칩으로 세운다 — 이 카드가 어느 줄에 얹히는지 그림으로 말한다.
        FillSynergyChips(view, tag);
    }

    void BindMonster(OptionView view, in CardRewardOption option, CardCatalog catalog,
                     ref Sprite icon, ref string name, ref string desc)
    {
        MonsterSpeciesData species = catalog.GetMonster(option.Id);
        if (species == null) return;

        // 초상화는 런타임 합성물이다 — 공급자가 만들어 캐시한다.
        icon = MonsterPortraitProvider.Get(species);
        name = species.DisplayName;
        desc = DescribeSpecies(species);

        view.ManaText.text  = species.ManaCost.ToString("0.#");
        view.CountText.text = species.SummonCount.ToString();

        // 한 마리짜리에 "1" 을 띄우면 정보가 아니라 잡음이다 (하단 카드와 같은 규칙).
        view.CountRoot.SetActive(species.SummonCount > 1);

        bool ranged = species.AttackKind == MonsterAttackKind.Ranged;
        view.KindText.text  = ranged ? "원거리" : "근접";
        view.KindText.color = ranged ? RangedColor : MeleeColor;

        // ⚠ 고른 **뒤**의 레벨로 그린다 (ResultLevel)
        //   지금 레벨로 그리면 "이걸 고르면 얼마가 되는가" 를 말하지 못한다.
        //
        // ⚠ 만렙 카드는 **증가분을 적지 않는다** (사용자 지적, 2026-09-12)
        //   StatWithGain 의 초록 "+N" 은 레벨로 **이미 붙어 있는** 몫이지 이번에
        //   고르면 붙는 몫이 아니다. 만렙 카드를 고르면 레벨은 그대로고 진화·융합
        //   갈림길이 열릴 뿐인데, 그 줄이 "+38" 을 달고 있으면 "진화를 누르면
        //   공·체가 오른다" 로 읽힌다 — 실제로는 오르지 않는다.
        //   그래서 레벨 몫까지 더한 **지금 값** 하나만 적는다.
        if (option.IsMaxed)
        {
            float ratio = 1f + CardLevelRule.StatBonusRatio(option.ResultLevel);

            view.StatText .text = Stat("공격력", species.Attack * ratio);
            view.StatText2.text = Stat("체력",   species.MaxHp  * ratio);
        }
        else
        {
            view.StatText .text = StatWithGain("공격력", species.Attack, option.ResultLevel);
            view.StatText2.text = StatWithGain("체력",   species.MaxHp,  option.ResultLevel);
        }

        FillSynergy(view, species, option);
        FillSpeciesPassives(view, species);
    }

    /// <summary>
    /// 이 몬스터의 종족 패시브를 그림으로 늘어놓는다. 계보를 타고 물려받은 것까지 전부.
    ///
    /// ⚠ 순서는 CollectSpeciesPassives 가 준 순서 그대로다 (뿌리 → 업그레이드)
    ///   All 순서로 다시 정렬하면 "슬라임이니까 분열이 먼저" 라는 계보가 사라진다.
    ///   All 은 <b>그림을 찾는 인덱스</b>일 뿐이지 표시 순서가 아니다.
    /// </summary>
    void FillSpeciesPassives(OptionView view, MonsterSpeciesData species)
    {
        _passiveBuffer.Clear();
        species.CollectSpeciesPassives(_passiveBuffer);

        int shown = 0;

        for (int i = 0; i < _passiveBuffer.Count; i++)
        {
            if (shown >= view.PassiveRoots.Length) break;

            SpeciesPassive passive = _passiveBuffer[i];

            int index = SpeciesPassiveRule.IndexOf(passive);
            Sprite icon = (_speciesIcons != null && index >= 0 && index < _speciesIcons.Length)
                        ? _speciesIcons[index] : null;

            view.PassiveRoots[shown].SetActive(true);
            view.PassiveIcons[shown].sprite  = icon;
            view.PassiveIcons[shown].enabled = icon != null;

            if (view.PassiveHovers[shown] != null) view.PassiveHovers[shown].Setup(passive);

            shown++;
        }

        HidePassivesFrom(view, shown);
    }

    static void HidePassivesFrom(OptionView view, int from)
    {
        if (view.PassiveRoots == null) return;

        for (int i = from; i < view.PassiveRoots.Length; i++)
            view.PassiveRoots[i].SetActive(false);
    }

    /// <summary>
    /// 이 몬스터가 속한 시너지를 그림으로 늘어놓는다.
    ///
    ///   [숲][야수][술법]   ← 멧돼지 (표식 셋)
    ///   [언데드][재생]      ← 스켈레톤 (표식 둘)
    ///
    /// ■ 가진 표식을 **전부** 띄운다
    ///   한때 "이 카드로 열리는 것만" 띄웠다. 그러면 아무것도 안 열리는 카드는
    ///   줄이 통째로 비어, 그 몬스터가 어느 계열인지조차 알 수 없었다.
    ///   카드에서 알고 싶은 것은 진행도가 아니라 **계열**이다.
    ///
    /// ■ 이미 열린 표식은 밝게, 아직인 것은 흐리게
    ///   ⚠ 단계 색으로 물들이지 않는다 — 아이콘이 저마다 제 색을 갖고 있어서
    ///     회색으로 물들이면 여덟 개가 다 같아 보인다. 밝기만 낮춘다.
    ///
    /// 자세한 효과는 칩에 올리거나 눌러서 본다 (SynergyChipUI).
    /// </summary>
    void FillSynergy(OptionView view, MonsterSpeciesData species, in CardRewardOption option)
    {
        int shown = 0;

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            if ((species.Tags & tag) == 0)         continue;
            if (shown >= view.SynergyRoots.Length) break;

            int index = MonsterSynergyRule.IndexOf(tag);
            Sprite icon = (_synergyIcons != null && index >= 0 && index < _synergyIcons.Length)
                        ? _synergyIcons[index] : null;

            bool lit = MonsterSynergyRule.TierOf(tag) != SynergyTier.None;

            view.SynergyRoots[shown].SetActive(true);

            view.SynergyIcons[shown].sprite  = icon;
            view.SynergyIcons[shown].enabled = icon != null;
            view.SynergyIcons[shown].color   = lit ? Color.white : DimIcon;

            // 칩마다 담는 시너지가 카드에 따라 달라진다 — 주인을 다시 알려 준다.
            if (view.SynergyHovers[shown] != null) view.SynergyHovers[shown].Setup(tag);

            shown++;
        }

        HideSynergyFrom(view, shown);
    }

    /// <summary>아직 안 열린 표식의 밝기. 색조는 건드리지 않는다.</summary>
    static readonly Color DimIcon = new(1f, 1f, 1f, 0.42f);

    /// <summary>표식 하나만 칩으로 세운다 (시너지 강화 카드가 쓴다).</summary>
    void FillSynergyChips(OptionView view, MonsterTag tag)
    {
        int index = MonsterSynergyRule.IndexOf(tag);
        Sprite icon = (_synergyIcons != null && index >= 0 && index < _synergyIcons.Length)
                    ? _synergyIcons[index] : null;

        view.SynergyRoots[0].SetActive(true);
        view.SynergyIcons[0].sprite  = icon;
        view.SynergyIcons[0].enabled = icon != null;
        view.SynergyIcons[0].color   = Color.white;

        if (view.SynergyHovers[0] != null) view.SynergyHovers[0].Setup(tag);

        HideSynergyFrom(view, 1);
    }

    static void HideSynergyFrom(OptionView view, int from)
    {
        for (int i = from; i < view.SynergyRoots.Length; i++)
            view.SynergyRoots[i].SetActive(false);
    }

    void BindSkill(OptionView view, in CardRewardOption option, CardCatalog catalog,
                   ref Sprite icon, ref string name, ref string desc)
    {
        SkillCardData card = catalog.GetSkill(option.Id);
        if (card == null) return;

        icon = card.Icon;
        name = card.DisplayName;
        desc = card.Description;

        view.ManaText.text = card.ManaCost.ToString("0.#");

        // 스킬은 유닛을 남기지 않는다 — 마릿수도 공격력·체력도 없다.
        view.CountRoot.SetActive(false);

        view.KindText.text  = "즉시 발동";
        view.KindText.color = SkillColor;

        view.StatText .text = Stat("위력", card.PowerMultiplier(option.ResultLevel), "×{0:0.##}");
        view.StatText2.text = string.Empty;

        // 스킬 카드는 시너지 표식을 갖지 않는다 — 덱에서 세는 것은 종족뿐이다.
        HideSynergyFrom(view, 0);
    }

    /// <summary>신규 · 레벨업 · 진화는 완전히 다른 선택이라 한눈에 갈려야 한다.</summary>
    static void BindState(OptionView view, in CardRewardOption option)
    {
        // 시너지 강화는 레벨 개념이 없다 — "NEW" 로 두면 새 카드처럼 읽힌다.
        if (option.IsSynergyBoost)
        {
            view.StateText.text  = "강화";
            view.StateText.color = EvolveColor;
            return;
        }

        // 진화 후보 — 새 카드도 레벨업도 아니다. 덱의 베이스 칸이 이것으로 바뀐다.
        // ⚠ 만렙 카드(아래)와 글자가 같아도 된다 — 둘 다 "진화" 라는 같은 일을 말한다.
        //   무엇이 무엇으로 바뀌는지는 DescribeGain 이 적는다.
        if (option.IsEvolve)
        {
            view.StateText.text  = "진화";
            view.StateText.color = EvolveColor;
            return;
        }

        if (option.IsMaxed)
        {
            view.StateText.text  = "진화";
            view.StateText.color = EvolveColor;
            return;
        }

        if (option.LevelsUp)
        {
            // ⚠ '→' 는 기본 폰트에 없어 □ 로 나온다 (UI 규칙 2번). '›' 를 쓴다.
            view.StateText.text  = $"Lv.{option.CurrentLevel} › Lv.{option.ResultLevel}";
            view.StateText.color = LevelUpColor;
            return;
        }

        if (option.IsDuplicate)
        {
            // ⚠ 오르지 않는다는 사실을 감추지 않는다
            //   "Lv.3" 만 띄우면 이번 장으로 오른 것처럼 읽힌다. 남은 장수를
            //   함께 적어야 "지금 고르면 무엇이 되는가" 가 정확해진다.
            int left = CardLevelRule.CopiesToNextLevel(option.CopiesAfter);

            view.StateText.text  = $"Lv.{option.CurrentLevel}  ({left}장 더)";
            view.StateText.color = ProgressColor;
            return;
        }

        view.StateText.text  = "NEW";
        view.StateText.color = NewColor;
    }

    // ── 상태 색 ──────────────────────────────────────────────
    //   세 상태가 완전히 다른 선택이므로 색으로 먼저 갈린다.

    static readonly Color NewColor      = new(0.55f, 0.95f, 0.60f);  // 초록 — 신규
    static readonly Color LevelUpColor  = new(1.00f, 0.84f, 0.35f);  // 금색 — 이번에 오른다
    static readonly Color ProgressColor = new(0.62f, 0.72f, 0.92f);  // 청회색 — 아직 안 오른다
    static readonly Color EvolveColor   = new(0.72f, 0.55f, 1.00f);  // 보라 — 갈림길

    /// <summary>
    /// 이미 가진 카드가 **얼마나 강해지는가** 한 줄.
    ///
    /// ■ 이번에 오르면 그 칸을, 안 오르면 다음 칸을 보여 준다
    ///   둘 다 플레이어가 알고 싶은 것은 같다 — "이 카드를 계속 밀면 뭐가 되나".
    ///   오르지 않는 경우에는 앞에 "다음:" 을 붙여 지금 열리는 것이 아님을 밝힌다.
    ///   (오른다는 사실 자체는 StateText 의 화살표와 색이 이미 말한다)
    ///
    /// ■ 새 카드·진화에는 적지 않는다
    ///   새 카드는 아직 레벨 축이 없고, 진화는 레벨업이 아니라 갈림길이다.
    ///   그 둘에 억지로 한 줄을 채우면 세 카드가 다 같아 보인다.
    ///   (새 카드의 그 자리는 종족 패시브 설명이 대신 쓴다 — 둘은 동시에
    ///    뜨지 않으므로 카드 아래쪽 같은 구역을 나눠 쓴다)
    ///
    /// ⚠ 종족 표(MonsterLevelBonus)가 정본이다 — 여기서 수치를 다시 만들지 말 것
    ///   레벨 보너스는 종족마다 다른 고정 효과다. Describe() 를 그대로 쓴다.
    /// </summary>
    static string DescribeGain(in CardRewardOption option, CardCatalog catalog)
    {
        // 시너지 강화 — 이 카드가 하는 일 **한 줄만** 적는다.
        //
        //  ⚠ 여기에 덧붙이지 말 것 (사용자 지시, 2026-09-07)
        //    한때 "(2/3)" 을, 그다음엔 "은 단계 개방" 을 함께 적었다.
        //    둘 다 화면이 이미 말하는 것을 대신 계산해 준 것이다 — 현재 개수와
        //    문턱은 상단 시너지 줄에 늘 떠 있다. **판단은 플레이어가 한다.**
        if (option.IsSynergyBoost)
            return $"{MonsterSynergyRule.NameOf(option.BoostTag)} 카운트 " +
                   $"+{MonsterSynergyRule.BoostCount}";

        // ⚠ 만렙 카드는 빈 칸으로 두지 않는다
        //   아래 구역에 바탕이 깔려 있어서(RunPopupCreator.BuildGainBackdrop),
        //   비워 두면 강조된 사각형만 덩그러니 남는다. 이 카드를 고르면
        //   실제로 무슨 일이 일어나는지가 곧 고를 이유이기도 하다.
        // 진화 후보 — **무엇이 사라지고 무엇이 오는가**가 이 카드의 전부다.
        //   ⚠ 레벨이 1 로 되돌아간다는 말을 빠뜨리지 말 것 (SummonDeckData.EvolveTo).
        //     키운 카드를 진화시키는 것은 되돌릴 수 없는 선택이라, 고르기 전에 알아야 한다.
        //   ⚠ '→' 는 기본 폰트에 없어 □ 로 나온다 (UI 규칙 2). '›' 를 쓴다.
        if (option.IsEvolve)
        {
            MonsterSpeciesData from = catalog.GetMonster(option.EvolveFromId);
            string fromName = from != null ? from.DisplayName : option.EvolveFromId;

            return $"{fromName} › 진화 (Lv.1 부터 다시)";
        }

        if (option.IsMaxed) return "고르면 진화·융합 갈림길이 열린다";

        if (!option.IsDuplicate) return string.Empty;

        // 오르면 그 레벨, 안 오르면 바로 다음 레벨을 미리 보여 준다.
        int shownLevel = option.LevelsUp ? option.ResultLevel : option.CurrentLevel + 1;
        if (shownLevel > CardLevelRule.MaxLevel) return string.Empty;

        string body = option.Kind == SummonKind.Monster
            ? MonsterGain(catalog.GetMonster(option.Id), shownLevel)
            : SkillGain(catalog.GetSkill(option.Id), shownLevel);

        if (string.IsNullOrEmpty(body)) return string.Empty;

        return option.LevelsUp ? body : $"다음: {body}";
    }

    /// <summary>그 레벨 칸이 여는 스탯·패시브. 표가 비어 있으면 빈 문자열.</summary>
    static string MonsterGain(MonsterSpeciesData species, int level)
    {
        if (species == null) return string.Empty;

        // [0] = Lv2 · [1] = Lv3 … (MonsterSpeciesData.LevelBonuses 참고)
        int index = level - 2;
        if (index < 0 || index >= species.LevelBonuses.Length) return string.Empty;

        MonsterLevelBonus bonus = species.LevelBonuses[index];

        // ⚠ 한 줄에 하나씩 (DescribeLines). Describe() 는 " · " 로 이어 붙인
        //   한 줄이라 카드 폭 380 에서 자동 축소에 걸려 글자가 쪼그라들었다.
        return bonus.IsEmpty ? string.Empty : bonus.DescribeLines();
    }

    /// <summary>
    /// 스킬 카드는 레벨 표가 없다 — 위력 배율 하나가 전부다.
    /// 그래서 "지금 몇 배에서 몇 배가 되는가" 를 그대로 적는다.
    /// </summary>
    static string SkillGain(SkillCardData card, int level)
    {
        if (card == null) return string.Empty;

        float before = card.PowerMultiplier(level - 1);
        float after  = card.PowerMultiplier(level);

        if (Mathf.Approximately(before, after)) return string.Empty;

        return $"위력 ×{before:0.##} → ×{after:0.##}";
    }

    /// <summary>
    /// 카드 설명 — 특징 분류 한 줄.
    ///
    /// ⚠ 종족 패시브는 여기 적지 않는다 — 칩이 그린다 (FillSpeciesPassives)
    ///   예전엔 "분열 — 죽으면 절반 크기로 둘로 나뉜다" 를 줄마다 쌓았다.
    ///   계보가 깊으면 네 줄이 되어 카드 세 장을 훑는 동안 읽히지 않았고,
    ///   자동 축소까지 걸려 카드마다 글자 크기가 달라졌다.
    ///   지금은 그림이 이름을 대신하고, 설명은 칩을 눌러서 본다.
    /// </summary>
    /// <summary>
    /// 특징 분류 한 줄.
    ///
    /// ⚠ 소환 간격·공체 보너스 줄은 뺐다 (사용자 지시, 2026-09-07)
    ///   3택 카드는 <b>고를지 말지</b>를 정하는 자리다. 이미 마나·마릿수·이름·
    ///   공격·체력·개성 칩이 들어차 있는데 규칙 설명까지 얹으면 무엇을 보고
    ///   고르는지가 흐려진다.
    ///
    ///   ⚠ SpawnPaceRule.DescribeFor 자체는 지우지 말 것 —
    ///     몬스터 상세의 '소환 간격' 행이 그대로 쓴다.
    /// </summary>
    static string DescribeSpecies(MonsterSpeciesData species)
        => MonsterTraitNames.Describe(species.Traits);

    // ── 선택 ─────────────────────────────────────────────────

    void Pick(int index)
    {
        if (index >= _shown.Count) return;

        CardRewardOption option = _shown[index];

        Action<CardRewardOption> callback = _onPicked;
        _onPicked = null;

        // 통계 팝업이 위에 떠 있을 수 있다 — 함께 정리한다.
        PopupManager.Instance.Close(PopupType.BattleStats);

        // ⚠ 콜백보다 먼저 닫는다 — 콜백이 진화 창을 여는 경우가 있다.
        Close();

        callback?.Invoke(option);
    }
}
