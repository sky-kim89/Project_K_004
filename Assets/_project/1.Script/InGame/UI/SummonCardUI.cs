using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 카드를 끌어 옮기는 손짓을 받는 쪽 — 하단 카드 바(SummonDeckUI)가 구현한다.
///
/// ⚠ 카드는 자리를 직접 바꾸지 않는다
///   카드 칸은 인덱스에 묶여 있고(SummonDeckUI._cards) 순서의 정본은 세이브
///   (SummonDeckData)다. 칸이 스스로 옮기면 화면과 데이터가 갈린다.
///   여기서는 "지금 이 칸을 이만큼 끌었다" 만 알린다.
/// </summary>
public interface ISummonCardDrag
{
    void OnCardDragBegin(int slotIndex, PointerEventData e);
    void OnCardDrag(int slotIndex, PointerEventData e);
    void OnCardDragEnd(int slotIndex, PointerEventData e);
}

// ============================================================
//  SummonCardUI.cs
//  소환 덱 한 칸의 표시 전용 UI.
//  실제 소환·마나 차감은 하지 않고, 현재 상태를 보여 주고 선택만 알린다.
//
//  ■ 구성 — 초상화가 카드의 전부다
//    이름 텍스트가 없다. 카드가 작아서 이름을 넣으면 초상화가 뭉개지고,
//    어차피 몬스터는 **초상화로 구분**한다.
//
//      ┌────────────────┐
//      │ ◈8         ☠3  │   마나 = 왼쪽 위 · 마릿수 = 오른쪽 위 (좌우 대칭)
//      │     초상화      │   숫자는 아이콘 **옆에** 나란히 놓는다
//      │           Lv.3 │   레벨 = 오른쪽 아래
//      └────────────────┘
//
//    ⚠ 두 배지는 같은 치수의 거울상이다 (InGameUIPrefabCreator.CreateBadge)
//      한쪽만 크면 카드 윗변이 기울어 보인다. 치수를 고칠 일이 생기면
//      그 함수 하나만 고친다 — 반대쪽이 저절로 따라온다.
//      (배지 **안쪽** 순서는 양쪽 다 [아이콘][숫자] 로 같다 — 거울상으로
//       뒤집으면 두 숫자가 카드 가운데에서 마주 봐 짝짓기가 어려워진다)
//
//    ⚠ 아이콘 위에 숫자를 얹지 않는다 (2026-09-03 되돌림)
//      그러면 숫자의 세로 자리·글자색·폰트 상한이 전부 아이콘 PNG 에
//      종속돼, 그림을 갈아 끼울 때마다 셋을 다시 재야 한다.
//      나란히 놓으면 숫자가 카드 면 위에 앉아 그 셋이 사라진다.
//      대신 배지가 넓어지므로 카드 폭 검산이 필수다
//      (EditorUIBuilder.BadgeWidthFor — 카드 사이 간격을 절반씩 빌려 쓴다).
//
//    ⚠ 숫자 뒤에 면을 깔지 않는다 — 아이콘이 가려진다
//      어두운 칩을 깔았더니 무엇을 뜻하는 배지인지 안 보였다.
//      대비는 글자색 하나로 만든다 — 바탕이 늘 어두운 카드 면이다.
//
//  ■ 몬스터 카드와 스킬 카드가 같은 칸을 쓴다
//    둘 다 마나를 내고 라인을 탭해 쓴다. 다른 점은 스킬이 유닛을 남기지
//    않는다는 것뿐이라, 칸을 나눌 이유가 없다. 마릿수 배지는 몬스터에만 뜬다.
//
//  ■ 레벨은 중복 획득으로 오른다 (CardLevelRule)
//    Lv1 일 때는 표기를 숨긴다 — 모든 카드에 "Lv.1" 이 붙어 있으면
//    정보가 아니라 잡음이다. 오른 카드만 눈에 띄어야 한다.
//
//  ■ 쿨다운이 없다
//    카드를 다시 쓰는 데 제약은 마나뿐이다. 그래서 쿨다운 표시도 없앴다.
//
//  ■ 선택은 다시 누를 때까지 유지된다
//    한 번 고르면 라인을 계속 탭해 연속으로 걸 수 있다.
//    그래서 **지금 무엇을 들고 있는지가 계속 보여야 한다** — 강조는
//    테두리·물들임·삼각 표식 세 겹이다 (CreateSelectedAccent 참고).
//    예전의 밑변 7px 짜리 선 하나는 옆 카드 그림자에 묻혀 안 보였다.
// ============================================================

public class SummonCardUI : MonoBehaviour,
                            IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Button          _button;

    [Tooltip("카드 그림. 이 그림이 카드의 유일한 식별자다.")]
    [SerializeField] Image           _monsterIcon;

    [Header("소모 마나 — 카드 왼쪽 위 (마릿수와 좌우 대칭)")]
    [SerializeField] GameObject      _costRoot;
    [SerializeField] TextMeshProUGUI _manaCostText;

    [Header("소환 마릿수 — 카드 오른쪽 위 (마나와 좌우 대칭)")]
    [SerializeField] GameObject      _countRoot;
    [SerializeField] TextMeshProUGUI _summonCountText;

    // ── 시너지 표식 ──────────────────────────────────────────
    //
    //  ⚠ 카드 왼쪽 모서리에 세로로 세운다
    //    가로로 눕히면 아래쪽 레벨 배지(72px)와 자리를 다툰다. 왼쪽 변은
    //    마나 배지 아래부터 레벨 배지 위까지 비어 있어 셋이 그대로 들어간다.
    //
    //  ⚠ 그림만 있고 숫자는 없다
    //    "이 카드가 어느 시너지에 얹히나" 만 말하면 된다. 몇 장인지는
    //    상단 줄이 훨씬 크게 말하고 있고, 150px 카드에 숫자까지 넣으면
    //    초상화가 밀린다.

    [Tooltip("이 종족의 시너지 표식 칸. 한 종족이 갖는 최대 수(3)만큼.")]
    [SerializeField] Image[] _synergyIcons;

    [Tooltip("시너지 그림. ⚠ MonsterSynergyRule.AllTags 순서로 넣는다 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _synergyIconSet;

    [Header("카드 레벨 — 오른쪽 아래")]
    [SerializeField] GameObject      _levelRoot;
    [SerializeField] TextMeshProUGUI _levelText;

    [Header("친화 — 왼쪽 아래 (레벨 배지의 거울상)")]
    [Tooltip("이 소환사의 친화 종족이면 켠다 — 할인·소환력 ×1.2·개성이 걸리는 카드다.")]
    [SerializeField] GameObject      _affinityRoot;

    [Header("상태")]
    [SerializeField] GameObject      _disabledOverlay;
    [SerializeField] GameObject      _selectedAccent;

    Action<int> _onClicked;
    int   _slotIndex;

    // ── 끌어 옮기기 ──────────────────────────────────────────
    ISummonCardDrag _drag;
    bool            _dragging;

    /// <summary>
    /// 방금 끝난 손짓이 드래그였다 — 이어서 오는 클릭 한 번을 삼킨다.
    ///
    /// ⚠ 이게 없으면 순서를 바꿀 때마다 카드가 선택된다
    ///   EventSystem 은 누른 오브젝트와 뗀 오브젝트가 같으면 드래그였어도
    ///   클릭을 마저 보낸다. 카드는 손가락을 따라다니므로 뗄 때 그 자리에
    ///   있어서 언제나 조건이 맞는다.
    /// </summary>
    bool _swallowClick;

    /// <summary>개성 할인까지 반영한 원가. 과부하 계산의 밑값이다.</summary>
    float _adjustedCost;

    // ── 과부하 번쩍임 ────────────────────────────────────────
    //
    //  ■ 왜 필요한가
    //    과부하는 **표시가 바뀌지 않는 구간**이 있다. 원가 2 짜리는
    //    2 → 2 → 2 → 3 이라 네 번째에야 숫자가 오른다. 그 순간을 놓치면
    //    "아까랑 같은 카드를 냈는데 마나가 더 빠졌다" 로만 남는다.
    //    실제로 더 내게 되는 그 한 번을 몸으로 알려 줘야 규칙이 학습된다.
    //
    //  ■ 왜 UIJuice 가 아닌가
    //    그쪽은 성장 연출이다 — 파티클·링·떠오르는 라벨이 한 벌로 붙고,
    //    대상을 물들이지는 못한다. 카드 여덟 장이 늘어선 좁은 바에서
    //    파티클이 터지면 무엇이 왜 터졌는지 오히려 안 읽힌다.
    //    여기서 필요한 것은 "이 카드가" "비싸졌다" 두 가지뿐이다.

    /// <summary>같은 카드가 직전에 보여 준 소모액. -1 이면 아직 잰 적 없다.</summary>
    int _lastShownCost = -1;

    /// <summary>_lastShownCost 가 어느 카드의 값인가 — 칸이 다른 카드로 바뀌면 무효다.</summary>
    string _lastCostCardId;

    Coroutine _overloadPulse;

    const float PulseScale = 1.18f;   // 최대 배율
    const float PulseTime  = 0.26f;   // 커졌다 돌아오기까지

    /// <summary>번쩍임의 정점 색 — 정착 색(OverloadColor)보다 밝다.</summary>
    static readonly Color OverloadFlash = new(1.00f, 0.72f, 0.68f);

    /// <summary>이 칸이 들고 있는 카드. 비어 있으면 IsEmpty 가 true.</summary>
    public SummonDeckSlot Card { get; private set; } = SummonDeckSlot.Empty;

    /// <summary>카드 데이터에 적힌 원가. 색을 정할 때의 기준선이다.</summary>
    public float BaseManaCost { get; private set; }

    /// <summary>
    /// 지금 이 카드를 내면 실제로 빠져나갈 마나 — 개성 할인과 과부하가 모두 반영된 값.
    /// 화면에 뜨는 숫자이자 사용 가능 판정의 기준이다.
    /// </summary>
    public int ManaCost { get; private set; }

    // ── 마나 숫자 색 ─────────────────────────────────────────
    //
    //  ⚠ 색이 곧 설명이다
    //    과부하는 "왜 갑자기 비싸졌지" 가 바로 읽혀야 한다. 툴팁을 열어야
    //    아는 규칙은 실시간 게임에서 없는 규칙이나 마찬가지다.

    /// <summary>과부하 — 거듭 내서 값이 올랐다.</summary>
    static readonly Color OverloadColor = new(1.00f, 0.42f, 0.38f);

    /// <summary>할인 — 개성 등으로 값이 내렸다.</summary>
    static readonly Color DiscountColor = new(0.45f, 0.95f, 0.55f);

    /// <summary>원가 그대로.</summary>
    static readonly Color NormalCostColor = Color.white;

    public bool HasCard => !Card.IsEmpty;

    public void Bind(int slotIndex, Action<int> onClicked, ISummonCardDrag drag = null)
    {
        _slotIndex = slotIndex;
        _onClicked = onClicked;
        _drag      = drag;

        _button.onClick.RemoveListener(HandleClick);
        _button.onClick.AddListener(HandleClick);
    }

    // ── 끌어 옮기기 ──────────────────────────────────────────
    //
    //  ■ 순서가 곧 전장에 나가는 순서다 (사용자 확정, 2026-09-07)
    //    보기 좋으라고 정리하는 것이 아니라 진형을 짜는 조작이다
    //    (SummonDeckData.Move · SummonReservation 머리 주석).
    //
    //  ⚠ 빈 칸은 끌지 않는다 — 옮길 것이 없다.
    //  ⚠ 살 수 없는 카드도 끌 수 있다
    //    마나가 모자라면 Button.interactable 이 꺼지지만 드래그는 Button 이
    //    아니라 이 컴포넌트가 받는다. 지금 못 내는 카드일수록 자리를 미리
    //    잡아 두고 싶은 법이다.

    public void OnBeginDrag(PointerEventData e)
    {
        if (_drag == null || !HasCard) return;

        _dragging = true;
        _drag.OnCardDragBegin(_slotIndex, e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_dragging) _drag.OnCardDrag(_slotIndex, e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;

        _dragging     = false;
        _swallowClick = true;
        _drag.OnCardDragEnd(_slotIndex, e);
    }

    /// <summary>카드 한 장을 그린다. 몬스터·스킬 어느 쪽이든 여기로 들어온다.</summary>
    public void ShowCard(in SummonDeckSlot slot, CardCatalog catalog, Sprite fallbackIcon)
    {
        // 다른 카드로 갈렸다면 이전 카드의 번쩍임을 끌고 가지 않는다.
        if (slot.Id != Card.Id) StopPulse();

        Card = slot;

        Sprite icon  = null;
        int    count = 1;

        if (slot.Kind == SummonKind.Skill)
        {
            SkillCardData card = catalog.GetSkill(slot.Id);

            icon         = card != null ? card.Icon : null;
            BaseManaCost = card != null ? card.ManaCost : 0f;
        }
        else
        {
            MonsterSpeciesData species = catalog.GetMonster(slot.Id);

            // 초상화는 런타임 합성물이다 — 공급자가 만들어 캐시한다.
            icon         = species != null ? MonsterPortraitProvider.Get(species) : null;
            BaseManaCost = species != null ? species.ManaCost : 0f;
            count        = SummonCountOf(species, slot);
        }


        _monsterIcon.sprite  = icon != null ? icon : fallbackIcon;
        _monsterIcon.enabled = _monsterIcon.sprite != null;

        RefreshCost();

        // "×" 없이 숫자만 — 옆의 해골 아이콘이 "마리" 를 뜻한다.
        _summonCountText.text = count.ToString();

        int level = slot.Level;
        _levelText.text = $"Lv.{level}";

        _costRoot .SetActive(true);
        _countRoot.SetActive(slot.Kind == SummonKind.Monster && count > 1);

        // Lv1 은 숨긴다 — 모든 카드에 붙어 있으면 정보가 아니라 잡음이다.
        _levelRoot.SetActive(level > 1);

        RefreshSynergy(slot.Kind == SummonKind.Monster ? catalog.GetMonster(slot.Id) : null);

        SetSelected(false);
    }

    /// <summary>
    /// 이 카드가 한 번에 세우는 마릿수.
    ///
    /// ⚠ 종족 기본값(SummonCount)만 적지 말 것 (사용자 지적, 2026-09-09)
    ///   강화소 '증식'(+1)·특성 증원·유물 증원의 인장이 전부 빠져, 강화소에서
    ///   돈을 내고 +1 을 새겨도 카드 숫자가 그대로였다.
    ///   ⚠ 실제 소환은 SummonController 가 RunPerkRule.SummonCountFor +
    ///     card.ExtraSummons 로 낸다 — <b>같은 식</b>을 써야 화면과 전장이 맞는다.
    ///   ⚠ 소환사는 카드보다 늦게 선다(RefreshCost 주석과 같은 이유). 없으면
    ///     친화 증원만 빠진 값이 나오고, 소환사가 서면 다시 그려진다.
    /// </summary>
    static int SummonCountOf(MonsterSpeciesData species, in SummonDeckSlot slot)
    {
        if (species == null) return 1;

        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;

        return RunPerkRule.SummonCountFor(summoner, species) + slot.ExtraSummons;
    }

    /// <summary>
    /// 마나 숫자와 색을 다시 그린다.
    ///
    /// 과부하는 카드를 낼 때마다 오르므로, 카드 내용이 그대로여도 이 값은
    /// 바뀐다. SummonDeckUI 가 SummonCostRule.Changed 를 받아 불러 준다.
    /// </summary>
    public void RefreshCost()
    {
        if (Card.IsEmpty) return;

        // ⚠ 할인을 여기서 다시 잰다 — 카드가 먼저 놓이고 소환사가 나중에 선다
        //   RunBootstrap 은 BuildStarterDeck → SpawnSummoner 순서라, ShowCard 시점에는
        //   SummonerRuntimeBridge.Current 가 아직 없다. 그때 굳혀 두면 개성 할인이
        //   런 내내 반영되지 않는다.
        _adjustedCost = ResolveAdjustedCost();

        // ⚠ 같은 카드일 때만 비교한다
        //   칸이 다른 카드로 갈린 것을 "값이 올랐다" 로 읽으면, 덱이 갱신될
        //   때마다 엉뚱한 카드가 번쩍인다.
        int previous = Card.Id == _lastCostCardId ? _lastShownCost : -1;

        ManaCost = SummonCostRule.CostFor(Card.Id, _adjustedCost);

        _lastCostCardId = Card.Id;
        _lastShownCost  = ManaCost;

        // 마나는 두 자리까지만 들어간다 — 배지 숫자 칸이 그만큼뿐이다.
        _manaCostText.text  = FormatCost(ManaCost);
        _manaCostText.color = SummonCostRule.StateOf(Card.Id, BaseManaCost, _adjustedCost) switch
        {
            ManaCostState.Overloaded => OverloadColor,
            ManaCostState.Discounted => DiscountColor,
            _                        => NormalCostColor,
        };

        // 실제로 더 내게 된 그 순간에만 터뜨린다 (첫 표시는 previous < 0 이라 제외).
        if (previous >= 0 && ManaCost > previous) PlayOverloadPulse();

        // ⚠ 마릿수도 여기서 다시 잰다 — 마나와 같은 이유다
        //   소환사는 카드보다 늦게 서고(위 주석), 특성 '증원' 은 판 도중에 붙는다.
        //   ShowCard 때 굳혀 두면 그 뒤로 영영 옛 숫자가 남는다.
        if (Card.Kind == SummonKind.Monster)
        {
            int count = SummonCountOf(CardCatalog.Current?.GetMonster(Card.Id), Card);

            _summonCountText.text = count.ToString();
            _countRoot.SetActive(count > 1);
        }

        RefreshAffinity();
    }

    /// <summary>
    /// 친화 종족 표식 (사용자 요청, 2026-09-11).
    ///
    /// ⚠ RefreshCost 에서 부른다 — 소환사가 카드보다 늦게 서므로(위 주석) ShowCard 시점에는
    ///   아직 판정할 수 없다. 특성 '친화 확장' 도 판 도중에 붙는다.
    /// ⚠ 판정은 RunPerkRule.IsAffinity 다 — 계보(진화체)와 친화 확장까지 한 곳이 본다.
    /// </summary>
    void RefreshAffinity()
    {
        if (_affinityRoot == null) return;   // Inspector 연결 — 프리팹을 다시 구우면 채워진다

        SummonerData       summoner = SummonerRuntimeBridge.Current?.Data;
        MonsterSpeciesData species  = Card.Kind == SummonKind.Monster
                                    ? CardCatalog.Current?.GetMonster(Card.Id)
                                    : null;

        _affinityRoot.SetActive(species != null && RunPerkRule.IsAffinity(summoner, species));
    }

    /// <summary>
    /// 카드를 한 번 부풀렸다 되돌리며 마나 숫자를 붉게 번쩍인다.
    ///
    /// ⚠ localScale 을 만진다
    ///   카드 바는 HorizontalLayoutGroup 이지만 레이아웃은 scale 을 보지 않으므로
    ///   옆 카드를 밀지 않는다. 다만 이 값이 1 이 아닌 채로 풀에 돌아가면
    ///   다음에 커진 카드가 나오므로, 카드가 갈릴 때 반드시 되돌린다(StopPulse).
    ///
    /// ⚠ unscaledDeltaTime 이다
    ///   배속·일시정지를 타면 2배속에서 눈에 안 들어오고, 팝업이 열려
    ///   멈춘 사이에 눌린 카드는 아예 번쩍이지 않는다. 이건 전투가 아니라
    ///   **입력에 대한 응답**이라 실제 시간으로 도는 편이 맞다.
    /// </summary>
    void PlayOverloadPulse()
    {
        StopPulse();
        if (!isActiveAndEnabled) return;

        _overloadPulse = StartCoroutine(OverloadPulseRoutine());
    }

    IEnumerator OverloadPulseRoutine()
    {
        var rt = (RectTransform)transform;

        float t = 0f;

        while (t < PulseTime)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / PulseTime);

            // 0 → 1 → 0. 앞을 빠르게, 뒤를 느리게 빼야 '툭 튀고 가라앉는' 느낌이 난다.
            float pulse = Mathf.Sin(k * Mathf.PI);
            pulse *= pulse < 0.5f ? 1f : Mathf.SmoothStep(1f, 0.85f, k);

            rt.localScale       = Vector3.one * Mathf.LerpUnclamped(1f, PulseScale, pulse);
            _manaCostText.color = Color.Lerp(OverloadColor, OverloadFlash, pulse);

            yield return null;
        }

        rt.localScale       = Vector3.one;
        _manaCostText.color = OverloadColor;
        _overloadPulse      = null;
    }

    /// <summary>연출을 끊고 크기·색을 제자리로. 카드가 갈리거나 꺼질 때 부른다.</summary>
    void StopPulse()
    {
        if (_overloadPulse != null)
        {
            StopCoroutine(_overloadPulse);
            _overloadPulse = null;
        }

        transform.localScale = Vector3.one;
    }

    void OnDisable() => StopPulse();

    /// <summary>
    /// 이 종족이 얹히는 시너지를 왼쪽 변에 세운다.
    ///
    /// 이미 열린 표식은 밝게, 아직인 것은 흐리게 — 카드만 보고도
    /// "이 카드가 켜 놓은 줄" 과 "아직인 줄" 이 갈린다.
    ///
    /// ⚠ 스킬 카드는 표식이 없다 — 시너지가 세는 것은 종족뿐이다.
    /// </summary>
    void RefreshSynergy(MonsterSpeciesData species)
    {
        if (_synergyIcons == null) return;

        int shown = 0;

        // 특성 '한 우물' 이 카운트 +2 를 얹은 표식들 — 없으면 None
        MonsterTag wellTags = MonsterSynergyRule.SingleWellTags;

        if (species != null && _synergyIconSet != null)
        {
            foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            {
                if ((species.Tags & tag) == 0)  continue;
                if (shown >= _synergyIcons.Length) break;

                int index = MonsterSynergyRule.IndexOf(tag);
                if (index < 0 || index >= _synergyIconSet.Length) continue;

                Image slot = _synergyIcons[shown++];
                slot.gameObject.SetActive(true);
                slot.sprite = _synergyIconSet[index];
                // ⚠ 단계 색으로 물들이지 않는다 — 아이콘이 제 색을 갖고 있어서
                //   회색으로 물들이면 여덟 개가 다 같아 보인다. 밝기만 낮춘다.
                slot.color  = MonsterSynergyRule.TierOf(tag) != SynergyTier.None
                            ? Color.white
                            : new Color(1f, 1f, 1f, 0.42f);

                SetWellOutline(slot, (wellTags & tag) != 0);
            }
        }

        for (int i = shown; i < _synergyIcons.Length; i++)
        {
            SetWellOutline(_synergyIcons[i], false);
            _synergyIcons[i].gameObject.SetActive(false);
        }
    }

    static readonly Color WellOutlineColor = new(1f, 0.82f, 0.25f, 1f);

    /// <summary>
    /// 한 우물 표식 — 아이콘 둘레에 금빛 테두리 (사용자 지시, 2026-09-16 — "무엇에 +2 가 붙었는지 모른다").
    ///
    /// ⚠ 형제 오브젝트로 테두리를 두지 않는다 — 표식 줄이 레이아웃 그룹이라 형제를 끼우면 칸이 밀린다.
    ///   대신 아이콘 자체에 Outline 효과를 켜고 끈다 (레이아웃 무관 · 프리팹 재굽기 불필요).
    /// </summary>
    static void SetWellOutline(Image icon, bool on)
    {
        var outline = icon.GetComponent<Outline>();
        if (!on)
        {
            if (outline != null) outline.enabled = false;
            return;
        }

        if (outline == null) outline = icon.gameObject.AddComponent<Outline>();
        outline.effectColor     = WellOutlineColor;
        outline.effectDistance  = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        outline.enabled         = true;
    }

    public void ShowEmpty(Sprite fallbackIcon)
    {
        StopPulse();

        Card            = SummonDeckSlot.Empty;
        ManaCost        = 0;
        BaseManaCost    = 0f;
        _adjustedCost   = 0f;
        _lastShownCost  = -1;
        _lastCostCardId = null;

        _monsterIcon.sprite  = fallbackIcon;
        _monsterIcon.enabled = fallbackIcon != null;

        RefreshSynergy(null);
        _costRoot .SetActive(false);
        _countRoot.SetActive(false);
        _levelRoot.SetActive(false);
        if (_affinityRoot != null) _affinityRoot.SetActive(false);

        SetSelected(false);
        SetAvailability(false);
    }

    /// <summary>
    /// 지금 쓸 수 있는가. 못 쓰는 이유는 표시하지 않는다 —
    /// 카드가 작아 문구를 넣으면 초상화를 덮는다. 흐려지는 것으로 충분하다.
    /// </summary>
    public void SetAvailability(bool available)
    {
        _button.interactable = available;
        _disabledOverlay.SetActive(!available);
    }

    public void SetSelected(bool selected)
        => _selectedAccent.SetActive(selected);

    void HandleClick()
    {
        // 끌어 옮긴 손짓이 소환 선택으로 새지 않게 한 번 삼킨다.
        if (_swallowClick)
        {
            _swallowClick = false;
            return;
        }

        _onClicked?.Invoke(_slotIndex);
    }

    /// <summary>
    /// 개성·특성 할인까지 반영한 원가. 과부하는 이 값 위에 얹힌다.
    /// 소환사가 아직 없으면 할인 없이 원가 그대로다.
    /// </summary>
    float ResolveAdjustedCost()
    {
        if (Card.Kind == SummonKind.Skill) return BaseManaCost;   // 스킬 할인 개성은 아직 없다

        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        if (summoner == null) return BaseManaCost;

        MonsterSpeciesData species = CardCatalog.Current?.GetMonster(Card.Id);
        if (species == null) return BaseManaCost;

        return SummonerPerkRuntime.ManaCostFor(summoner, species, Card);
    }

    /// <summary>마나 표기. 두 자리를 넘으면 99 로 붙인다 (배지 숫자 칸이 그만큼뿐이다).</summary>
    static string FormatCost(int cost) => cost > 99 ? "99" : cost.ToString();
}
