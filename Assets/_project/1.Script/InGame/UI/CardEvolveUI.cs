using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CardEvolveUI.cs
//  만렙 카드를 또 뽑았을 때 뜨는 갈림길 — **진화 / 융합**.
//
//  ■ 언제 뜨나
//    보상 3택에서 **이미 만렙인 카드**를 고르면 뜬다. 중복이 더 이상
//    레벨을 올리지 못하는 대신 이 선택이 열린다 (RunBootstrap.OnRewardPicked).
//    둘 다 불가능한 카드면 창을 띄우지 않고 장수만 올린다 — 아무것도 고를 게
//    없는 창을 띄우면 진행만 끊긴다.
//
//  ■ 두 단계다
//    1단계 : [진화] / [융합] 선택
//    2단계 : 융합을 골랐으면 재료 카드 선택 (덱의 다른 몬스터 카드)
//    진화는 무작위라 고를 것이 없어 1단계에서 끝난다.
//
//  ■ 닫기 버튼이 없다
//    선택이 곧 다음 스테이지 진입 신호다 (CardRewardUI 와 같은 이유).
//    ⚠ 단, 융합 2단계에서는 **1단계로 돌아가는 길**이 있어야 한다.
//      재료를 보고 나서 "역시 진화가 낫겠다" 가 정상적인 판단이다.
//
//  ■ 규칙은 여기 없다
//    가능 여부·결과는 전부 CardEvolution / SummonDeckData 가 정한다.
//    UI 가 조건을 다시 쓰면 두 곳이 조용히 어긋난다.
// ============================================================

public class CardEvolveUI : PopupBase
{
    [Header("연결")]
    // ⚠ _root 는 없앴다 (2026-09-07) — 이제 팝업이라 여닫는 것은 PopupBase 다.

    [Header("대상 카드")]
    [SerializeField] Image           _targetIcon;
    [SerializeField] TextMeshProUGUI _targetName;
    [SerializeField] TextMeshProUGUI _targetDesc;

    [Header("1단계 — 갈림길")]
    [SerializeField] GameObject      _choiceStep;
    [SerializeField] Button          _evolveButton;
    [SerializeField] TextMeshProUGUI _evolveDesc;
    [SerializeField] Button          _fuseButton;
    [SerializeField] TextMeshProUGUI _fuseDesc;

    [Tooltip("제3의 갈래 '강화' — 재료를 잃지 않는 대신 공·체만 조금 얹는다. 언제나 고를 수 있다.")]
    [SerializeField] Button          _powerButton;
    [SerializeField] TextMeshProUGUI _powerDesc;

    [Header("2단계 — 재료 선택")]
    [SerializeField] GameObject   _materialStep;
    [SerializeField] Button       _backButton;
    [SerializeField] MaterialView[] _materials;

    [Header("공통")]
    [SerializeField] Sprite _fallbackIcon;

    [Tooltip("종족 패시브 아이콘. ⚠ SpeciesPassiveRule.All 순서로 넣는다 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _speciesIcons;

    // ── 대상 카드의 패시브 칩 ────────────────────────────────
    //   계보(최대 4) + 융합으로 배운 것까지 오므로 칸은 넉넉히 여섯이다.

    [SerializeField] GameObject[]            _targetPassiveRoots;
    [SerializeField] Image[]                 _targetPassiveIcons;
    [SerializeField] SpeciesPassiveChipUI[]  _targetPassiveHovers;

    /// <summary>재료 후보 한 칸.</summary>
    [Serializable]
    public class MaterialView
    {
        public GameObject      Root;
        public Button          Button;
        public Image           Icon;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI PassiveText;

        [Tooltip("넘어갈 패시브의 그림. 이름 왼쪽에 놓인다.")]
        public Image PassiveIcon;

        [Tooltip("눌러서 효과를 보는 부분.")]
        public SpeciesPassiveChipUI PassiveHover;
    }

    int         _targetSlot = -1;
    Action      _onDone;
    readonly List<int> _materialSlots = new(8);
    static readonly List<SpeciesPassive> _buffer = new(8);

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 그 칸의 갈림길을 띄운다. 고르고 나면 onDone 이 불린다.
    ///
    /// 돌려주는 값이 false 면 **띄우지 않았다** — 진화도 융합도 불가능한
    /// 카드다. 부르는 쪽이 그때는 그냥 진행해야 한다.
    /// </summary>
    public bool Setup(int targetSlot, Action onDone)
    {
        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        SummonDeckSlot target = deck.GetSlot(targetSlot);

        bool canEvolve = CardEvolution.CanEvolve(target);
        bool canFuse   = CardEvolution.CanFuse(target) && CollectMaterials(deck, target) > 0;

        // ⚠ 보상 후보를 거르는 쪽과 **같은 판정**이어야 한다 (CardEvolution.HasAnyChoice)
        //   갈리면 후보에는 떴는데 창은 안 뜨는 카드가 생겨 보상 한 번이 사라진다.
        if (!canEvolve && !canFuse) return false;

        _targetSlot = targetSlot;
        _onDone     = onDone;

        CaptureBranchLayout();
        BindTarget(target);

        // ── 못 하는 갈래는 **통째로 감춘다** (사용자 지시, 2026-09-12) ──
        //
        //  ⚠ 흐리게 두지 않는다
        //    예전에는 버튼을 남기고 라벨까지 흐리게(DimBranch) 칠했다. 그래도
        //    "이미 진화했거나 상위 종족이 없다" 는 줄이 붙은 버튼이 화면에 남아,
        //    **진화가 있을지도 모른다**는 착각을 준다. 이미 진화한 카드에게
        //    진화는 없는 갈래지 막힌 갈래가 아니다 — 없는 것은 안 보여야 한다.
        //
        //  ⚠ '강화' 는 언제나 켠다 — 이 창이 선택지가 되게 하는 갈래다
        //    진화가 없는 카드에서 융합만 남으면 재료를 잃는 쪽이 강제된다.
        _evolveButton.gameObject.SetActive(canEvolve);
        _fuseButton  .gameObject.SetActive(canFuse);
        _powerButton .gameObject.SetActive(true);

        if (canEvolve)
            _evolveDesc.text =
                LocalizationManager.Instance.Format("같은 계보의 상위 종족으로 변한다 (무작위 {0}종)",
                                                CardEvolution.CollectUpgrades(target.Id).Count);

        if (canFuse)
            _fuseDesc.text = "다른 카드를 먹고 그 종족 패시브를 배운다 (재료 소멸)";

        // ⚠ 값은 CardEvolution 이 정본이다 — 숫자를 손으로 적지 말 것.
        _powerDesc.text = LocalizationManager.Instance.Format(
            "이 카드의 공격력·체력이 영구히 +{0:0}% (잃는 것 없음)", CardEvolution.EmpowerBonus * 100f);

        StackBranches();

        ShowStep(material: false);
        return true;
    }


    /// <summary>
    /// 갈래 슬롯이 <b>구워져 나온</b> 세로 자리 셋. 감춘 갈래의 빈칸을 메울 때 쓴다.
    ///
    /// ⚠ 간격을 상수로 적지 않는다
    ///   자리를 정하는 것은 Creator(BuildBranchButton 의 offsetY)다. 여기에
    ///   같은 숫자를 또 적으면 Creator 를 고친 날 둘이 갈린다 — 프리팹에서
    ///   한 번 읽어 두고 그 값만 돌려쓴다.
    /// </summary>
    float[] _branchY;

    void CaptureBranchLayout()
    {
        if (_branchY != null) return;

        _branchY = new[]
        {
            ((RectTransform)_evolveButton.transform).anchoredPosition.y,
            ((RectTransform)_fuseButton  .transform).anchoredPosition.y,
            ((RectTransform)_powerButton .transform).anchoredPosition.y,
        };
    }

    /// <summary>
    /// 켜진 갈래만 위에서부터 차곡차곡 다시 세운다.
    ///
    /// ⚠ 감추기만 하면 그 자리가 빈칸으로 남는다 — 진화를 감춘 카드에서
    ///   융합 버튼이 창 한복판에 떠 있고 위가 텅 빈다.
    /// </summary>
    void StackBranches()
    {
        int index = 0;

        foreach (Button branch in new[] { _evolveButton, _fuseButton, _powerButton })
        {
            if (!branch.gameObject.activeSelf) continue;

            var rt = (RectTransform)branch.transform;

            Vector2 pos = rt.anchoredPosition;
            pos.y = _branchY[index++];
            rt.anchoredPosition = pos;
        }
    }

    // ── 1단계 ────────────────────────────────────────────────

    void OnEnable()
    {
        _evolveButton.onClick.AddListener(HandleEvolve);
        _fuseButton  .onClick.AddListener(() => ShowStep(material: true));
        _powerButton .onClick.AddListener(HandleEmpower);
        _backButton  .onClick.AddListener(() => ShowStep(material: false));
    }

    void OnDisable()
    {
        _evolveButton.onClick.RemoveAllListeners();
        _fuseButton  .onClick.RemoveAllListeners();
        _powerButton .onClick.RemoveAllListeners();
        _backButton  .onClick.RemoveAllListeners();
    }

    void HandleEvolve()
    {
        var deck = UserDataManager.Instance.Get<SummonDeckData>();

        MonsterSpeciesData next = deck.Evolve(_targetSlot);

        // ⚠ 도감도 함께 연다 (사용자 지적, 2026-09-07)
        //   Evolve 는 칸의 Id 만 바꾼다. 도감 해금은 이 프로젝트에서 늘
        //   **부르는 쪽의 몫**인데(RunBootstrap 의 시작 덱·카드 보상이 그렇다)
        //   진화 경로만 그 줄이 빠져 있었다. 손에 든 종족이 도감에는 없는
        //   상태가 되고, 품질 개선도 못 한다.
        //   ⚠ 품질은 Normal 로 연다 — 다른 해금 경로와 같은 규칙이다.
        if (next != null)
            UserDataManager.Instance.Get<MonsterCodexData>().Unlock(next.Id, UnitGrade.Normal);

        Debug.Log(next != null
            ? $"[CardEvolveUI] 진화 — {next.DisplayName} (도감 해금)"
            : "[CardEvolveUI] 진화 실패 (조건 불충족)");

        Finish();
    }

    /// <summary>
    /// 제3의 갈래 — 종족도 재료도 건드리지 않고 공·체만 얹는다.
    /// 진화가 없는 카드에서 융합이 강제되지 않게 하는 것이 이 갈래의 전부다.
    /// </summary>
    void HandleEmpower()
    {
        var deck = UserDataManager.Instance.Get<SummonDeckData>();

        bool ok = deck.Empower(_targetSlot, CardEvolution.EmpowerBonus);

        Debug.Log(ok
            ? $"[CardEvolveUI] 강화 — 공·체 +{CardEvolution.EmpowerBonus * 100f:0}%"
            : "[CardEvolveUI] 강화 실패 (조건 불충족)");

        Finish();
    }

    // ── 2단계 ────────────────────────────────────────────────

    void ShowStep(bool material)
    {
        _choiceStep  .SetActive(!material);
        _materialStep.SetActive(material);

        if (material) BindMaterials();
    }

    /// <summary>재료가 될 수 있는 칸 번호를 모은다. 돌려주는 값은 개수.</summary>
    int CollectMaterials(SummonDeckData deck, in SummonDeckSlot target)
    {
        _materialSlots.Clear();

        for (int i = 0; i < deck.SlotCount; i++)
        {
            if (i == _targetSlot) continue;
            if (!CardEvolution.CanBeMaterial(target, deck.GetSlot(i))) continue;

            _materialSlots.Add(i);
        }

        return _materialSlots.Count;
    }

    void BindMaterials()
    {
        var deck = UserDataManager.Instance.Get<SummonDeckData>();
        CollectMaterials(deck, deck.GetSlot(_targetSlot));

        CardCatalog catalog = CardCatalog.Current;

        for (int i = 0; i < _materials.Length; i++)
        {
            MaterialView view = _materials[i];

            if (i >= _materialSlots.Count)
            {
                view.Root.SetActive(false);
                continue;
            }

            int slotIndex = _materialSlots[i];
            SummonDeckSlot slot = deck.GetSlot(slotIndex);
            MonsterSpeciesData species = catalog.GetMonster(slot.Id);

            view.Root.SetActive(true);

            Sprite icon = species != null ? MonsterPortraitProvider.Get(species) : null;
            view.Icon.sprite  = icon != null ? icon : _fallbackIcon;
            view.Icon.enabled = view.Icon.sprite != null;

            view.NameText.text = species != null ? species.DisplayName : slot.Id;

            // 넘어갈 패시브를 미리 보여 준다 — 재료를 고르는 유일한 기준이다.
            //   ⚠ 그림과 이름을 함께 둔다 (카드 3택과 다르다)
            //     여기는 칸이 하나뿐이라 이름이 줄을 잡아먹지 않는다. 그리고
            //     "무엇이 넘어오는가" 가 이 화면의 전부라, 그림만 두고 눌러
            //     보게 하면 한 번 더 손이 간다.
            SpeciesPassive gift = CardEvolution.PassiveFrom(species);
            bool hasGift = gift != SpeciesPassive.None;

            // ⚠ 키운 재료는 공·체도 함께 넘긴다 (2026-09-10) — 반드시 적는다
            //   안 적으면 화면에서는 "Lv1 잡카드를 먹이는 게 이득" 으로만 보인다.
            //   값은 CardEvolution.FuseInheritFrom 이 만든다 — 손으로 적지 말 것.
            float carry = CardEvolution.FuseInheritFrom(slot);

            // ⚠ 이름과 공·체는 **줄을 나눈다** (사용자 지적, 2026-09-13)
            //   한 줄로 이으면 "→ 해골 방패병 · 공·체 +5%" 가 190px 칸의 두 배가 되어
            //   NoWrap 인 채로 옆 칸까지 흘러넘치고 패시브 아이콘까지 덮었다.
            //   칸을 넓힐 수 없으니(4열 격자) 줄을 나눈다 — Creator 가 두 줄을 잡아 둔다.
            string gain = carry > 0f ? LocalizationManager.Instance.Format("공·체 +{0:0}%", carry * 100f) : "";

            string head = hasGift ? $"→ {gift.ToKorean()}" : "→ 넘길 패시브 없음";

            view.PassiveText.text = gain.Length > 0 ? $"{head}\n{gain}" : head;

            view.PassiveIcon.sprite  = hasGift ? IconOf(gift) : null;
            view.PassiveIcon.enabled = view.PassiveIcon.sprite != null;

            if (view.PassiveHover != null) view.PassiveHover.Setup(gift);

            // ⚠ 리스너를 매번 갈아 끼운다 — 창이 스테이지마다 재사용된다.
            view.Button.onClick.RemoveAllListeners();
            view.Button.onClick.AddListener(() => HandleFuse(slotIndex));
        }
    }

    void HandleFuse(int materialSlot)
    {
        var deck = UserDataManager.Instance.Get<SummonDeckData>();

        bool ok = deck.Fuse(_targetSlot, materialSlot);
        Debug.Log(ok ? "[CardEvolveUI] 융합 완료" : "[CardEvolveUI] 융합 실패 (조건 불충족)");

        Finish();
    }

    // ── 마무리 ───────────────────────────────────────────────

    void Finish()
    {
        // ⚠ 콜백보다 먼저 닫는다 — 콜백이 다음 화면을 열 수 있다.
        //   PopupBase.Close 가 닫기 연출과 스택 정리를 함께 한다.
        Close();

        Action callback = _onDone;
        _onDone     = null;
        _targetSlot = -1;
        callback?.Invoke();

        UserDataManager.Instance.RequestSave();
    }

    // ── 표시 ─────────────────────────────────────────────────

    void BindTarget(in SummonDeckSlot slot)
    {
        CardCatalog catalog = CardCatalog.Current;
        MonsterSpeciesData species = catalog.GetMonster(slot.Id);

        Sprite icon = species != null ? MonsterPortraitProvider.Get(species) : null;
        _targetIcon.sprite  = icon != null ? icon : _fallbackIcon;
        _targetIcon.enabled = _targetIcon.sprite != null;

        _targetName.text = species != null ? species.DisplayName : slot.Id;

        // 지금 갖고 있는 종족 패시브 — 융합으로 무엇을 더할지 판단하는 기준이다.
        //
        // ⚠ 글이 아니라 그림이다
        //   " · " 로 이름을 이어 붙이던 줄이었다. 넷까지 붙는 데다 융합으로
        //   배워 온 것까지 섞여, 정작 "무엇을 더 넣을까" 를 판단할 자리에서
        //   가장 안 읽히는 줄이 됐다. 설명은 칩을 눌러서 본다.
        _buffer.Clear();
        if (species != null) species.CollectSpeciesPassives(_buffer);
        slot.CollectLearned(_buffer);

        FillTargetPassives();
    }

    /// <summary>_buffer 에 모인 패시브를 칩 줄에 늘어놓는다.</summary>
    void FillTargetPassives()
    {
        int shown = 0;

        for (int i = 0; i < _buffer.Count && shown < _targetPassiveRoots.Length; i++)
        {
            SpeciesPassive passive = _buffer[i];

            _targetPassiveRoots[shown].SetActive(true);
            _targetPassiveIcons[shown].sprite  = IconOf(passive);
            _targetPassiveIcons[shown].enabled = _targetPassiveIcons[shown].sprite != null;

            if (_targetPassiveHovers[shown] != null) _targetPassiveHovers[shown].Setup(passive);

            shown++;
        }

        for (int i = shown; i < _targetPassiveRoots.Length; i++)
            _targetPassiveRoots[i].SetActive(false);

        // 칸이 전부 비면 왜 비었는지 말해 준다 — 빈 줄은 버그처럼 보인다.
        _targetDesc.text = shown > 0 ? string.Empty : "패시브 없음";
    }

    /// <summary>
    /// 패시브의 그림. 순서 계약의 정본은 SpeciesPassiveRule.All 이다
    /// (Creator 가 그 순서로 _speciesIcons 를 채운다).
    /// </summary>
    Sprite IconOf(SpeciesPassive passive)
    {
        int index = SpeciesPassiveRule.IndexOf(passive);

        return (_speciesIcons != null && index >= 0 && index < _speciesIcons.Length)
             ? _speciesIcons[index] : null;
    }
}
