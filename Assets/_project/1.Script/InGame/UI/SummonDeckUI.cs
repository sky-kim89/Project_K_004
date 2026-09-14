using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
//  SummonDeckUI.cs
//  인게임 하단 카드 바. 이번 런에 손에 든 카드를 그린다.
//
//  ■ 카드 목록의 출처는 CardCatalog 하나다
//    예전에는 인스펙터에 종족 배열(_speciesCatalog)을 직접 물려 뒀다.
//    런 중에 주운 카드가 그 배열에 없으면 카드 바에 빈 칸으로 떠서,
//    "보상으로 받았는데 쓸 수 없는" 상태가 됐다.
//    ID → SO 변환은 CardCatalog 한 곳만 한다.
//
//  ■ 카드가 늘거나 레벨이 오르면 즉시 다시 그린다
//    SummonDeckData.Changed 를 구독한다. 보상 화면에서 카드를 고른 순간
//    카드 바에 반영돼야 다음 스테이지 대기에서 바로 쓸 수 있다.
//
//  ■ 카드를 끌어 옮겨 순서를 바꾼다 (사용자 확정, 2026-09-07)
//    그 순서가 곧 **전장에 나가는 순서**다 — 대기열이 덱 칸 번호대로 줄을
//    선다(SummonReservation). 앞 칸에 방패를 두면 늘 방패가 먼저 걸어 나간다.
//    보기 좋으라고 정리하는 기능이 아니라 진형을 짜는 조작이다.
//
//    ⚠ 끌고 있는 동안 순서를 미리 바꾸지 않는다
//      카드 칸(_cards)은 인덱스에 묶여 있어서, 중간에 Move 를 부르면 지금
//      손가락에 붙어 있는 그 칸이 **다른 카드로 다시 그려진다.**
//      들어 올린 카드는 그대로 두고, 손을 뗄 때 한 번만 옮긴다.
// ============================================================
public class SummonDeckUI : MonoBehaviour, ISummonCardDrag
{
    [Header("마나")]
    // ⚠ 진행 막대는 없앴다 (사용자 지적, 2026-09-07)
    //   라벨("소환 마나") · 숫자 · 막대 셋이 같은 값을 세 번 말했다.
    //   라벨은 물방울 아이콘이, 막대는 숫자가 이미 말한다.
    //   ⚠ 다시 넣지 말 것 — 마나는 실시간으로 차오르는 자원이 아니라
    //     스테이지 경계에서만 움직인다. 게이지로 그리면 기다리면 채워지는 줄 안다.
    [SerializeField] TextMeshProUGUI _manaText;

    [Tooltip("다음 판에 돌아올 마나 — \"+18\". 올리면 내역이 툴팁으로 뜬다.")]
    [SerializeField] TextMeshProUGUI _manaRegenText;

    [Tooltip("마나 칸 전체를 덮는 레이캐스트 면. 여기에 올리면 툴팁이 뜬다.")]
    [SerializeField] ManaRegenHoverUI _manaHover;

    [Header("카드")]
    [SerializeField] SummonCardUI[] _cards;
    [SerializeField] Sprite         _fallbackMonsterIcon;
    [SerializeField, Range(1, 10)] int _emptyPreviewSlots = 6;

    SummonManaData _mana;
    SummonDeckData _deck;
    int _selectedSlot = -1;
    float _nextBindAttempt;
    bool _summonInputEnabled = true;

    /// <summary>
    /// UI에서 선택한 칸. 실제 소환 시스템(SummonController)이 구독한다.
    /// 선택이 풀리면 빈 칸(SummonDeckSlot.Empty)이 전달된다.
    /// </summary>
    public event Action<int, SummonDeckSlot> CardSelected;

    public int SelectedSlot => _selectedSlot;

    void Awake()
    {
        RequireWiring();

        for (int i = 0; i < _cards.Length; i++)
            _cards[i].Bind(i, SelectCard, this);
    }

    /// <summary>
    /// Creator 가 꽂아야 할 칸이 비어 있으면 <b>무엇을 눌러야 하는지</b> 말하며 터진다.
    ///
    /// ■ 왜 이 검사가 필요한가 (2026-09-07)
    ///   Creator 에 필드를 새로 추가하면 **프리팹을 다시 굽기 전까지 그 칸은 비어 있다.**
    ///   그 상태로 돌리면 `NullReferenceException` 이 RefreshRegen 한복판에서 나는데,
    ///   스택만 보고는 "프리팹을 안 구웠다" 를 짚을 수가 없다 —
    ///   실제로 `_manaRegenText` 를 추가한 날 런 시작이 통째로 죽었다.
    ///
    ///   ⚠ 방어적 null 체크가 아니다. 여전히 예외를 던진다 —
    ///     조용히 넘기면 "다음 판 +N" 이 영영 안 뜨는 채로 굴러간다.
    ///     바꾼 것은 **예외가 스스로 원인을 말하게** 한 것뿐이다.
    /// </summary>
    void RequireWiring()
    {
        string missing =
              _manaText      == null ? nameof(_manaText)
            : _manaRegenText == null ? nameof(_manaRegenText)
            : _manaHover     == null ? nameof(_manaHover)
            : _cards         == null ? nameof(_cards)
            : null;

        if (missing == null) return;

        throw new MissingReferenceException(
            $"[SummonDeckUI] '{missing}' 가 비어 있다 — HUD 프리팹이 지금 Creator 보다 오래됐다.\n" +
            "Tools > Project K > UI > 인게임 HUD 를 실행해 다시 구울 것.");
    }

    void OnEnable()
    {
        // 과부하는 세이브가 아니라 정적 규칙이라 데이터 바인딩과 수명이 다르다 —
        // 화면이 켜져 있는 동안만 듣는다.
        SummonCostRule.Changed += HandleCostChanged;

        // ⚠ 소환사가 선 뒤에 값을 한 번 다시 잰다
        //   카드는 소환사보다 먼저 놓인다(RunBootstrap: BuildStarterDeck → SpawnSummoner).
        //   그래서 처음 그릴 때는 개성 할인이 아직 붙지 않는다.
        //   스테이지 대기에 들어설 때쯤이면 소환사가 서 있으므로 그때 다시 잰다.
        StageLoopDirector.OnStageReady += HandleStageReady;

        TryBindData();
        Refresh();
    }

    void OnDisable()
    {
        SummonCostRule.Changed         -= HandleCostChanged;
        StageLoopDirector.OnStageReady -= HandleStageReady;

        if (_mana != null) _mana.OnManaChanged -= RefreshMana;
        if (_deck != null) _deck.Changed       -= Refresh;
        _mana = null;
        _deck = null;
    }

    void Update()
    {
        if (_mana == null || _deck == null)
        {
            if (Time.unscaledTime >= _nextBindAttempt)
            {
                _nextBindAttempt = Time.unscaledTime + 0.5f;
                TryBindData();
                Refresh();
            }
            return;
        }

        RefreshAvailability();
    }

    public void Refresh()
    {
        RefreshMana();
        RefreshCards();
        RefreshAvailability();
    }

    /// <summary>게임플레이 쪽에서 전체 소환 입력을 잠글 때 쓰는 UI 게이트.</summary>
    public void SetSummonInputEnabled(bool enabled)
    {
        _summonInputEnabled = enabled;
        RefreshAvailability();
    }

    void TryBindData()
    {
        var data = UserDataManager.Instance;
        if (data == null) return;

        var nextMana = data.Get<SummonManaData>();
        if (_mana != nextMana)
        {
            if (_mana != null) _mana.OnManaChanged -= RefreshMana;
            _mana = nextMana;
            if (_mana != null) _mana.OnManaChanged += RefreshMana;
        }

        var nextDeck = data.Get<SummonDeckData>();
        if (_deck != nextDeck)
        {
            if (_deck != null) _deck.Changed -= Refresh;
            _deck = nextDeck;
            if (_deck != null) _deck.Changed += Refresh;
        }
    }

    void RefreshMana()
    {
        float current = _mana?.Current ?? 0f;
        // ⚠ 분모는 Granted 가 아니라 Max 다 (2026-08-28)
        //   Granted 는 이제 "이번 런에 들어온 마나 누적" 이라 스테이지마다
        //   회복될 때 함께 늘어난다. 그걸 분모로 쓰면 게이지가 영원히
        //   반쯤 찬 상태로 보이고, 회복을 받을수록 오히려 비어 보인다.
        //   그릇의 크기는 Max 하나다.
        float granted = _mana?.Max ?? 0f;

        // ⚠ 언제나 정수다 (사용자 지적, 2026-09-12) — 마나는 정수로만 움직인다
        //   (ManaRegenRule 주석). 그릇도 RunPerkRule.MaxManaFor 가 내려서 준다.
        //   여기서 "0.#" 로 적으면 옛 세이브의 찌꺼기가 그대로 드러난다.
        _manaText.text = $"{Mathf.Floor(current):0} / {Mathf.Floor(granted):0}";

        // 바닥나면 글자로 알린다 — 막대가 없어졌으니 색이 그 몫을 한다.
        _manaText.color = current > 0f
            ? Color.white
            : new Color(0.98f, 0.42f, 0.40f);

        RefreshRegen();
    }

    /// <summary>
    /// 다음 판에 돌아올 양을 미리 띄운다 (사용자 요청, 2026-09-07).
    ///
    /// ■ 왜 필요한가
    ///   마나는 이 게임의 축인데 "다 쓰면 끝" 인지 "곧 돌아오는지" 를 알 수가
    ///   없었다. 얼마가 돌아오는지 보이면 이번 판에 얼마나 지를지가 선택이 된다.
    ///
    /// ⚠ 잔량을 보고 계산한다 — 그래서 숫자가 시시각각 움직인다
    ///   아껴 둔 마나의 10%가 회복량에 들어가므로(ManaRegenRule), 소환할 때마다
    ///   이 값도 줄어든다. 그게 "아끼면 이득" 을 눈으로 보여 준다.
    ///
    /// ⚠ 셈은 여기서 하지 않는다 — ManaRegenRule 이 정본이다.
    ///   실제 지급(RunBootstrap.AdvanceStage)과 같은 함수를 부른다.
    /// </summary>
    void RefreshRegen()
    {
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;

        bool show = summoner != null && _mana != null;

        if (_manaRegenText.gameObject.activeSelf != show)
            _manaRegenText.gameObject.SetActive(show);

        if (!show) return;

        _manaRegenText.text = $"다음 판 +{ManaRegenRule.PreviewFor(summoner, _mana):0}";
    }

    void RefreshCards()
    {
        int visibleSlots = _deck != null && _deck.SlotCount > 0
            ? Mathf.Min(_deck.SlotCount, _cards.Length)
            : Mathf.Min(_emptyPreviewSlots, _cards.Length);

        for (int i = 0; i < _cards.Length; i++)
        {
            var card = _cards[i];
            card.gameObject.SetActive(i < visibleSlots);
            if (i >= visibleSlots) continue;

            if (_deck == null || i >= _deck.SlotCount)
            {
                card.ShowEmpty(_fallbackMonsterIcon);
                continue;
            }

            SummonDeckSlot slot = _deck.GetSlot(i);
            if (slot.IsEmpty)
            {
                card.ShowEmpty(_fallbackMonsterIcon);
                continue;
            }

            card.ShowCard(slot, CardCatalog.Current, _fallbackMonsterIcon);
        }
    }

    void RefreshAvailability()
    {
        bool stateAllows = CanUseSummonUI();
        bool popupOpen   = PopupManager.Instance?.HasAnyOpen == true;

        for (int i = 0; i < _cards.Length; i++)
        {
            var card = _cards[i];
            if (!card.gameObject.activeSelf || !card.HasCard) continue;

            // 제약은 마나뿐이다 — 쿨다운은 없앴다.
            //
            // ⚠ 화면에 뜬 그 숫자로 판정한다
            //   카드의 ManaCost 는 개성 할인과 과부하가 모두 반영된 **실제 소모액**이라
            //   소환 경로(SummonController)와 같은 값이다. 예전에는 여기서 원가로
            //   보수적으로 판정했는데, 과부하가 생긴 뒤로는 그러면 반대가 된다 —
            //   "쓸 수 있다고 떴는데 실제로는 모자란" 카드가 나온다.
            //   ⚠ 특성 '피의 계약' 이면 모자라도 낼 수 있다 — 실제 소모와 같은 함수를 본다.
            bool enoughMana = _mana != null
                           && (_mana.CanSpend(card.ManaCost)
                               || RunPerkRule.CanPayWithBlood(_mana.Current, (int)card.ManaCost));
            bool available  = _summonInputEnabled && stateAllows && !popupOpen && enoughMana;

            card.SetAvailability(available);

            // ⚠ 선택은 유지한다
            //   마나가 잠깐 모자라도 고른 카드를 놓지 않는다. 마나가 차면
            //   다시 바로 걸 수 있어야 한다 — 매번 다시 고르게 하면 손이 바쁘다.
        }
    }

    void HandleStageReady(int stage) => HandleCostChanged();

    /// <summary>과부하가 바뀌었다 — 카드마다 숫자와 색을 다시 그린다.</summary>
    void HandleCostChanged()
    {
        foreach (var card in _cards)
            if (card != null && card.HasCard) card.RefreshCost();
    }

    bool CanUseSummonUI()
    {
        if (!_summonInputEnabled) return false;
        if (StageLoopDirector.Instance != null) return StageLoopDirector.Instance.CanSummon;

        var context = BattleManager.Instance?.Context;
        return context != null && context.State is BattleState.StageReady or BattleState.InWave;
    }

    // ── 끌어 옮기기 (ISummonCardDrag) ────────────────────────
    //
    //  ■ 들어 올렸다 놓는다
    //    ① 시작 — 레이아웃에서 빼고(ignoreLayout) 맨 앞으로 올린다.
    //       빼지 않으면 HorizontalLayoutGroup 이 매 프레임 제자리로 되돌린다.
    //    ② 이동 — 가로만 손가락을 따라간다. 세로까지 따라가면 카드가 전장 위로
    //       올라가 "여기 놓을 수 있나" 로 읽힌다. 이 조작은 줄 안에서의 순서다.
    //    ③ 놓기 — 가장 가까운 칸으로 옮기고 되돌린다.
    //
    //  ⚠ 칸 중심은 **시작할 때** 재 둔다
    //    끌고 있는 카드가 레이아웃에서 빠져 있어 나머지 칸이 그대로 서 있고,
    //    매 프레임 다시 재면 들어 올린 카드 자신의 좌표가 섞여 들어온다.

    int   _dragSlot = -1;
    float _dragCenterY;

    /// <summary>드래그 시작 시점의 칸별 화면 X 중심. 인덱스 = 칸 번호.</summary>
    readonly List<float> _dragCenters = new(10);

    LayoutElement _dragLayout;
    int           _dragSibling;

    void ISummonCardDrag.OnCardDragBegin(int slotIndex, PointerEventData e)
    {
        _dragSlot = slotIndex;

        _dragCenters.Clear();
        for (int i = 0; i < _cards.Length; i++)
        {
            if (!_cards[i].gameObject.activeSelf) break;
            _dragCenters.Add(_cards[i].transform.position.x);
        }

        var rt = (RectTransform)_cards[slotIndex].transform;
        _dragCenterY = rt.position.y;
        _dragSibling = rt.GetSiblingIndex();

        // ⚠ 레이아웃에서 빼지 않으면 매 프레임 제자리로 끌려간다
        _dragLayout = rt.GetComponent<LayoutElement>();
        if (_dragLayout != null) _dragLayout.ignoreLayout = true;

        // 다른 카드 위로 지나가야 하므로 맨 앞으로 올린다.
        rt.SetAsLastSibling();
    }

    void ISummonCardDrag.OnCardDrag(int slotIndex, PointerEventData e)
    {
        if (_dragSlot < 0) return;

        // 세로는 고정 — 줄 안에서의 순서를 바꾸는 조작이다.
        var rt = (RectTransform)_cards[_dragSlot].transform;
        rt.position = new Vector3(e.position.x, _dragCenterY, rt.position.z);
    }

    void ISummonCardDrag.OnCardDragEnd(int slotIndex, PointerEventData e)
    {
        if (_dragSlot < 0) return;

        int from = _dragSlot;
        int to   = NearestSlot(e.position.x);

        _dragSlot = -1;

        // 되돌린다 — 옮겼든 아니든 칸은 제자리로 돌아가야 한다.
        var rt = (RectTransform)_cards[from].transform;
        if (_dragLayout != null) _dragLayout.ignoreLayout = false;
        _dragLayout = null;
        rt.SetSiblingIndex(_dragSibling);

        if (_deck == null || !_deck.Move(from, to))
        {
            // 자리가 그대로여도 레이아웃은 다시 잡아 줘야 한다.
            RefreshCards();
            return;
        }

        // ⚠ 이미 서 있는 대기열도 새 순서로 다시 세운다
        //   판 중에 순서를 바꿨는데 걸어 둔 예약이 옛 순서로 나가면
        //   "바꿨는데 왜 그대로지" 가 된다. 마나는 그대로 두고 순서만 고친다.
        SummonController.Instance?.Reservation?.Resort();

        // 고른 카드가 따라 움직이게 한다 — 옮긴 뒤에도 같은 카드가 선택돼 있어야
        // 손이 하던 일을 이어 갈 수 있다.
        if (_selectedSlot == from)                            _selectedSlot = to;
        else if (_selectedSlot > from && _selectedSlot <= to)  _selectedSlot--;
        else if (_selectedSlot < from && _selectedSlot >= to)  _selectedSlot++;

        RefreshCards();
        for (int i = 0; i < _cards.Length; i++) _cards[i].SetSelected(i == _selectedSlot);

        // 덱 순서는 세이브에 남는다 — 다음 판에도 그대로 서야 한다.
        UserDataManager.Instance?.RequestSave();
    }

    /// <summary>화면 X 에 가장 가까운 칸 번호. 칸이 없으면 원래 자리.</summary>
    int NearestSlot(float screenX)
    {
        if (_dragCenters.Count == 0) return Mathf.Max(0, _dragSlot);

        int   best     = 0;
        float bestDist = Mathf.Abs(_dragCenters[0] - screenX);

        for (int i = 1; i < _dragCenters.Count; i++)
        {
            float d = Mathf.Abs(_dragCenters[i] - screenX);
            if (d >= bestDist) continue;

            best     = i;
            bestDist = d;
        }

        return best;
    }

    /// <summary>
    /// 카드 토글. 같은 카드를 다시 누르면 선택이 풀린다.
    ///
    /// 선택은 라인을 몇 번 탭하든 유지된다 — 연속 소환이 기본 조작이다.
    /// </summary>
    void SelectCard(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _cards.Length) return;

        var card = _cards[slotIndex];
        if (!card.HasCard) return;

        _selectedSlot = _selectedSlot == slotIndex ? -1 : slotIndex;

        for (int i = 0; i < _cards.Length; i++)
            _cards[i].SetSelected(i == _selectedSlot);

        // 해제도 알린다 — 컨트롤러가 선택을 비워야 라인 탭이 멎는다.
        CardSelected?.Invoke(_selectedSlot,
                             _selectedSlot >= 0 ? card.Card : SummonDeckSlot.Empty);
    }

    /// <summary>
    /// 고른 카드를 놓는다 — 시그니처 스킬 겨냥이 켜질 때 부른다.
    ///
    /// ⚠ 이미 아무것도 안 골랐으면 아무 일도 하지 않는다
    ///   그냥 불러도 CardSelected 가 튀어나가면, 그걸 듣는 쪽이 매번
    ///   "선택이 풀렸다" 로 반응해 겨냥까지 도로 꺼진다 (SummonController).
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedSlot < 0) return;

        _selectedSlot = -1;

        for (int i = 0; i < _cards.Length; i++) _cards[i].SetSelected(false);

        CardSelected?.Invoke(-1, SummonDeckSlot.Empty);
    }
}
