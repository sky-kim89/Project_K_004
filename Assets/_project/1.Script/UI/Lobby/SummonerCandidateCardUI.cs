using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SummonerCandidateCardUI.cs
//  MainPanel 오른쪽 **소환사 정보 칸** — 격자에서 고른 한 명을 자세히 말한다.
//
//  ┌────────────────────────────────────┐
//  │ [초상화]  이름                       │
//  │           패기 ■■■■■□□□□□ 5         │  ← 배분 (10칸 눈금)
//  │           체력 ■■■■■■■□□□ 7         │
//  │           지능 ■■■■■■■□□□ 7         │
//  │ [마왕성 60]        [◆ 52]           │  ← 배분이 무엇이 되는가
//  │ [소환력 7 ]        [카드 칸 5]        │
//  │ 설명                                 │
//  │ [개성] 이름 / 설명                    │
//  │ [스킬] 이름             스테이지당 N회 │
//  │ 친화 ●●●  │  시작 ●●                  │
//  └────────────────────────────────────┘
//
//  ■ 왜 눈금과 숫자를 둘 다 두나
//    눈금은 **배분**(합계 19 고정)을, 숫자 칩은 그 배분이 **무엇이 되는지**를 말한다.
//
//  ■ 마나는 아이콘이다 (UI 규칙 7) — 칩의 이름표 자리에 물방울 그림이 앉는다.
//
//  ■ 카드 칸은 소환사마다 4~6 이다 (2026-09-11) — 기준(5)보다 많으면 초록 · 적으면 주황.
//    숫자는 유물 '전열 확장' 까지 얹은 시작 칸 수다 (마왕성 체력이 유물을 얹는 것과 같다).
//
//  ■ 시그니처 스킬은 **이름과 그림이 소환사마다 다르다** (사용자 지시, 2026-09-11)
//    '권속 소환' 은 여덟 소환사가 나눠 쓴다 — 줄을 더 적는 대신 이름·그림 자체가
//    무엇을 부르는지 말한다. 정본은 SignatureSkillDisplay 다.
//
//  ■ 잠겨도 내용은 그대로 보인다 — 덮개는 조건과 "고를 수 없다" 만 얹는다.
// ============================================================

public class SummonerCandidateCardUI : MonoBehaviour
{
    [Header("초상화 · 이름 · 설명")]
    [SerializeField] Image                _portraitBg;
    [SerializeField] Image                _portraitImage;
    [SerializeField] UnitAppearanceBridge _portraitBridge;
    [SerializeField] TextMeshProUGUI      _nameText;
    [SerializeField] TextMeshProUGUI      _descText;

    [Header("환산 수치 칩")]
    [SerializeField] TextMeshProUGUI _coreHpText;
    [SerializeField] TextMeshProUGUI _manaText;
    [SerializeField] TextMeshProUGUI _powerText;
    [SerializeField] TextMeshProUGUI _slotText;

    [Header("3대 스탯 — 10칸 눈금")]
    [SerializeField] StatPipsUI _strengthPips;
    [SerializeField] StatPipsUI _vitalityPips;
    [SerializeField] StatPipsUI _intelligencePips;

    [Header("개성")]
    [SerializeField] Image           _perkIcon;
    [SerializeField] TextMeshProUGUI _perkName;
    [SerializeField] TextMeshProUGUI _perkDesc;

    [Header("시그니처 스킬")]
    [SerializeField] Image           _skillIcon;
    [SerializeField] TextMeshProUGUI _skillName;
    [SerializeField] TextMeshProUGUI _skillUses;

    [Header("친화 종족 · 시작 카드")]
    [SerializeField] Image[] _affinityIcons;
    [SerializeField] Image[] _starterIcons;

    [Header("잠금")]
    [SerializeField] GameObject      _lockRoot;
    [SerializeField] TextMeshProUGUI _lockText;

    [Header("공통")]
    [Tooltip("개성 그림이 아직 없을 때 쓰는 대체 아이콘.")]
    [SerializeField] Sprite _fallbackIcon;

    /// <summary>지금 올라온 소환사를 고를 수 있는가. 선택 버튼이 이 값을 본다.</summary>
    public bool IsUnlocked { get; private set; } = true;

    public SummonerData Data => _data;

    /// <summary>3대 스탯의 색 — 눈금 줄끼리 구별되게 각자 다른 색을 쓴다.</summary>
    static readonly Color StrColor = new(0.98f, 0.52f, 0.42f);   // 패기 — 붉은
    static readonly Color VitColor = new(0.45f, 0.88f, 0.55f);   // 체력 — 초록
    static readonly Color IntColor = new(0.62f, 0.66f, 1.00f);   // 지능 — 푸른

    /// <summary>카드 칸 — 기준보다 많음 / 적음.</summary>
    static readonly Color SlotMore = new(0.45f, 0.90f, 0.55f);
    static readonly Color SlotLess = new(1.00f, 0.66f, 0.32f);

    SummonerData _data;
    Texture2D    _portraitTexture;

    // ── 공개 API ─────────────────────────────────────────────

    public void Setup(SummonerData data)
    {
        // 초상화 합성은 비싸다 — 같은 소환사면 다시 그리지 않는다.
        // (유물 창을 닫고 돌아오면 숫자만 다시 읽는다 — 유물이 마왕성 체력·칸을 올린다)
        if (_data != data)
            RenderPortrait(data, _portraitBridge, _portraitBg, _portraitImage, ref _portraitTexture);
        _data = data;

        _nameText.text = data.DisplayName;
        _descText.text = data.Description;

        _coreHpText.text = data.MaxCoreHp.ToString();
        _manaText.text   = Mathf.RoundToInt(data.MaxMana).ToString();
        _powerText.text  = data.SummonPower.ToString("0.#");
        BindSlots(data);

        _strengthPips    .Set("패기", data.Vigor,        StrColor);
        _vitalityPips    .Set("체력", data.Vitality,     VitColor);
        _intelligencePips.Set("지능", data.Intelligence, IntColor);

        BindPerk(data);
        BindSkill(data);
        BindAffinities(data);
        BindStarters(data);
        BindLock(data);
    }

    /// <summary>
    /// 소환사 초상화 — 격자 카드와 정보 칸이 같은 함수를 쓴다.
    ///
    /// 소환사 외형은 결정적이다 — 전투에서 쓰는 것과 **같은 입력**을 넘겨야
    /// 화면과 실물이 어긋나지 않는다 (SummonerRuntimeBridge.ApplyAppearance 와 같은 세 값).
    /// ⚠ 몬스터 모습의 소환사(슬라임 킹)는 그 종족의 초상화를 쓴다 — 전투의 모습과 같아야 한다.
    /// </summary>
    public static void RenderPortrait(SummonerData data, UnitAppearanceBridge bridge,
                                      Image bg, Image image, ref Texture2D texture)
    {
        if (data.AppearanceSpecies != null)
        {
            // ⚠ 표식도 함께 — 소환사는 종족과 표식이 갈려 있다 (슬라임 + 왕관)
            image.sprite         = MonsterPortraitProvider.Get(data.AppearanceSpecies,
                                                               data.AppearanceMark);
            image.preserveAspect = true;
            image.enabled        = image.sprite != null;
            return;
        }

        UnitPortraitHelper.Render(data.ResolvedAppearanceSeed,
                                  data.AppearanceJob, data.AppearanceGrade,
                                  bridge, bg, image, ref texture);
    }

    // ── 내부 ─────────────────────────────────────────────────

    /// <summary>
    /// 칸 수는 유물까지 얹은 **시작 칸**, 색은 **소환사 본래 칸**(기준 대비)이다 —
    /// 색은 캐릭터의 성격을 말하고, 숫자는 지금 들고 들어갈 칸을 말한다.
    /// </summary>
    void BindSlots(SummonerData data)
    {
        _slotText.text  = data.StartDeckSlots.ToString();
        _slotText.color = data.DeckSlots > SummonerData.StandardDeckSlots ? SlotMore
                        : data.DeckSlots < SummonerData.StandardDeckSlots ? SlotLess
                        :                                                   Color.white;
    }

    void BindLock(SummonerData data)
    {
        IsUnlocked = SummonerUnlockRule.IsUnlocked(data);

        _lockRoot.SetActive(!IsUnlocked);
        if (!IsUnlocked) _lockText.text = SummonerUnlockRule.Describe(data);
    }

    /// <summary>⚠ 설명 문장의 정본은 SummonerPerkNames.Describe 다 — 여기서 만들지 않는다.</summary>
    void BindPerk(SummonerData data)
    {
        bool has = data.Perk != SummonerPerk.None;

        _perkName.text = has ? data.Perk.ToKorean()               : "개성 없음";
        _perkDesc.text = has ? data.Perk.Describe(data.PerkValue) : "";

        Sprite art = has ? SpriteManager.Instance.Get(data.Perk.IconKey()) : null;
        _perkIcon.sprite  = art != null ? art : _fallbackIcon;
        _perkIcon.enabled = _perkIcon.sprite != null;
        _perkIcon.color   = has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    /// <summary>
    /// 시그니처 스킬 — ⚠ 이름·그림의 정본은 SignatureSkillDisplay 다.
    /// 소환형은 부르는 종족의 초상화와 소환사마다의 이름을 쓴다 — 전투 버튼과 같은 그림이다.
    /// 제한은 쿨다운이 아니라 **스테이지당 횟수**라 그 수를 함께 적는다.
    /// </summary>
    void BindSkill(SummonerData data)
    {
        if (!data.HasSignatureSkill)
        {
            _skillName.text    = "시그니처 스킬 없음";
            _skillUses.text    = "";
            _skillIcon.enabled = false;
            return;
        }

        _skillName.text    = SignatureSkillDisplay.NameOf(data);
        _skillUses.text    = LocalizationManager.Instance.Format("스테이지당 {0}회", data.SkillUsesPerStage);
        _skillIcon.sprite  = SignatureSkillDisplay.IconOf(data);
        _skillIcon.enabled = _skillIcon.sprite != null;
    }

    /// <summary>
    /// 친화 종족 — 계보 뿌리 기준이라 업그레이드까지 함께 뜻한다.
    /// 특징(MonsterTrait)으로 잡은 친화는 여러 칸이 뜬다 (드루이드 = 야수 전부).
    /// </summary>
    void BindAffinities(SummonerData data)
    {
        List<MonsterSpeciesData> roots = CollectAffinityRoots(data);

        for (int i = 0; i < _affinityIcons.Length; i++)
        {
            Image icon = _affinityIcons[i];
            bool  has  = i < roots.Count;
            icon.gameObject.SetActive(has);
            if (!has) continue;

            // ⚠ Icon 이 아니라 LineageIcon 이다 — 작은 칸에 쓰는 식별 그림은 이쪽이다.
            icon.sprite  = roots[i].LineageIcon != null ? roots[i].LineageIcon : _fallbackIcon;
            icon.enabled = icon.sprite != null;
        }

        if (roots.Count > _affinityIcons.Length)
            Debug.LogError($"[SummonerCandidateCardUI] '{data.Id}' 친화 종족 {roots.Count}종인데 칸은 " +
                           $"{_affinityIcons.Length}개입니다. MainPanelCreator.SAffMax 를 늘리세요.");
    }

    static readonly List<MonsterSpeciesData> _rootBuffer = new(8);

    static List<MonsterSpeciesData> CollectAffinityRoots(SummonerData data)
    {
        _rootBuffer.Clear();

        foreach (MonsterSpeciesData species in CardCatalog.Current.Monsters)
        {
            if (species == null) continue;

            // 뿌리만 센다 — 업그레이드까지 세면 슬라임 계열이 네 칸을 먹는다.
            if (species.UpgradeOf != null) continue;
            if (!data.IsAffinity(species)) continue;

            _rootBuffer.Add(species);
        }

        return _rootBuffer;
    }

    /// <summary>
    /// 시작 카드 — "무엇을 데리고 들어가는가".
    /// ⚠ 계보 아이콘이 아니라 초상화다 — 계보 아이콘은 슬라임 계열 넷이 같은 그림이다.
    /// </summary>
    void BindStarters(SummonerData data)
    {
        for (int i = 0; i < _starterIcons.Length; i++)
        {
            Image icon = _starterIcons[i];
            MonsterSpeciesData species = i < data.StarterMonsters.Length ? data.StarterMonsters[i] : null;

            icon.gameObject.SetActive(species != null);
            if (species == null) continue;

            icon.sprite  = MonsterPortraitProvider.Get(species);
            icon.enabled = icon.sprite != null;
        }
    }
}
