using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  BattleInfoPopup.cs
//  전황 — 가운데를 기준으로 **왼쪽 아군 · 오른쪽 적군**을 나란히 보여 준다.
//
//  ■ 무엇을 위한 화면인가 (사용자 요청, 2026-09-07)
//    이 게임은 오토배틀이라 판이 시작되면 손댈 것이 없다. 그래서 **시작 전에**
//    "무엇이 오는가" 를 읽고 덱을 짜는 것이 유일한 판단이다. 그 판단에 필요한
//    것이 화면 어디에도 없었다 — 적은 걸어 들어오기 전까지 보이지 않는다.
//
//  ■ ⚠ 적은 **나오는 자리 그대로** 세운다
//    HeroSpawner.LaneOrder 가 정하는 줄 번호에 맞춰 위에서 아래로 놓는다.
//    목록으로 늘어놓으면 "가운데 라인이 위험하다" 같은 판단을 할 수가 없다.
//
//  ■ ⚠ 상세는 **이미 있는 창을 연다. 여기서 다시 그리지 않는다**
//    (사용자 지적, 2026-09-07 — 한때 이 창 안에 자체 Detail 패널을 만들었다)
//      아군 → MonsterDetailPopup.SetupRun  (몬스터 상세)
//      적군 → HeroDetailPopup.SetupHero    (영웅 상세)
//    같은 것을 두 번 그리면 스탯 줄 하나를 고칠 때마다 두 곳을 고쳐야 하고,
//    반드시 한쪽만 고쳐져 값이 갈린다.
//
//  ■ ⚠ 아군 상세는 **런 값 모드**로 연다 (SetupRun)
//    도감에서 열면 종족 기본값 × 품질이지만, 여기서 열면 전투와 같은
//    MonsterStatComposer 값이다 — 소환력·시너지·특성·유물·장비가 다 들어 있다.
//
//  ⚠ 적 스탯은 이름 시드에서 결정적으로 나온다 (UnitJobRoller · GeneralStatRoller).
//    실제로 세워 보지 않아도 같은 값이 나온다 — 그래서 대기 중에도 보여 줄 수 있다.
// ============================================================

public class BattleInfoPopup : PopupBase
{
    // ── 머리 ─────────────────────────────────────────────────

    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] Button          _closeBtn;

    // ── 초상화 칸 ────────────────────────────────────────────

    [Serializable]
    public class SlotView
    {
        public GameObject      Root;
        public Button          Button;
        public Image           Portrait;
        public TextMeshProUGUI NameText;

        /// <summary>적: "Lv 6" · 아군: 카드 레벨. 초상화 아래 왼쪽.</summary>
        public TextMeshProUGUI LeftBadge;

        /// <summary>적: 병사 수 · 아군: 품질. 초상화 아래 오른쪽.</summary>
        public TextMeshProUGUI RightBadge;

        /// <summary>아군 전용 — 마나 배지의 숫자. 적 칸은 비어 있다 (UI 규칙 7).</summary>
        public TextMeshProUGUI ManaText;

        /// <summary>아군 전용 — 마릿수 배지의 숫자.</summary>
        public TextMeshProUGUI CountText;
    }

    [Tooltip("아군 — 덱에 든 카드만. 덱 칸 수만큼 굽는다.")]
    [SerializeField] SlotView[] _allySlots;

    [Tooltip("적군 — 라인 수만큼. 위에서 아래가 곧 전장의 줄 번호다.")]
    [SerializeField] SlotView[] _enemySlots;

    [SerializeField] Sprite _fallbackPortrait;

    /// <summary>Creator 가 굽는 칸 수.</summary>
    public const int MaxAllySlots  = 8;
    public const int MaxEnemySlots = 5;

    // ── 상태 ─────────────────────────────────────────────────

    readonly List<SpawnEntry>     _enemies = new(MaxEnemySlots);
    readonly List<SummonDeckSlot> _allies  = new(MaxAllySlots);

    int _stageNumber;

    // ── 열기 ─────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _closeBtn.onClick.AddListener(() => Close());
    }

    public BattleInfoPopup Setup(int stageNumber, RunStageKind kind)
    {
        _stageNumber = stageNumber;

        _titleText.text = $"전황   —   스테이지 {stageNumber}";

        BuildEnemies(stageNumber, kind);
        BuildAllies();

        return this;
    }

    // ── 적군 ─────────────────────────────────────────────────

    void BuildEnemies(int stageNumber, RunStageKind kind)
    {
        _enemies.Clear();

        // ⚠ 실제 편성을 그대로 다시 만든다 — 화면용으로 흉내 내지 않는다
        //   같은 인자면 같은 결과가 나오므로(HeroDeployment.Build) 여기서 만든
        //   목록이 곧 이 판에 설 적이다. 흉내 내면 규칙을 고칠 때마다 갈린다.
        _enemies.AddRange(HeroDeployment.Build(stageNumber, kind, stageBias: 0f));

        for (int i = 0; i < _enemySlots.Length; i++)
        {
            // ⚠ 편성 순서가 아니라 **줄 번호**로 놓는다 (HeroSpawner.LaneFor)
            //   0번 편성이 가운데 줄에 서므로, 목록 순서대로 놓으면 화면과
            //   전장이 어긋난다.
            int at = IndexAtLane(i);

            bool has = at >= 0;
            _enemySlots[i].Root.SetActive(has);
            if (!has) continue;

            SpawnEntry e = _enemies[at];
            UnitStat   s = GeneralStatRoller.Roll(e.Name, e.Level,
                                                  UnitJobRoller.GetBirthGrade(e.Name));

            // ⚠ GetCached 는 **이미 합성된 것만** 돌려준다 (사용자 지적, 2026-09-07)
            //   용사 초상화는 원작 방식대로 몇 프레임에 걸쳐 합성되므로,
            //   처음 여는 창에서는 캐시가 늘 비어 있다 — 그래서 아무것도 안 떴다.
            //   Request 는 캐시에 있으면 즉시, 없으면 합성 뒤에 돌려준다.
            //   ⚠ stillWanted 로 "아직 그 칸이 그 용사인가" 를 물어본다.
            //     창을 닫았다 다시 열면 칸이 다른 용사를 그리고 있을 수 있다.
            Image      portrait = _enemySlots[i].Portrait;
            GameObject slotRoot = _enemySlots[i].Root;
            string     seed     = e.Name;

            portrait.sprite  = _fallbackPortrait;
            portrait.enabled = _fallbackPortrait != null;

            GeneralPortraitProvider.Request(
                seed,
                () => slotRoot != null && slotRoot.activeInHierarchy,
                sp =>
                {
                    if (portrait == null) return;
                    portrait.sprite  = sp != null ? sp : _fallbackPortrait;
                    portrait.enabled = portrait.sprite != null;
                });

            _enemySlots[i].NameText.text = HeroNameRule.ShortOf(e.Name);

            // ⚠ 여기에는 **Lv 과 병사 수만** 둔다 (사용자 확정)
            //   초상화 밑에 스탯을 늘어놓으면 다섯 줄이 글자로 덮여
            //   "어느 줄이 무거운가" 가 오히려 안 읽힌다. 나머지는 눌러서 본다.
            _enemySlots[i].LeftBadge .text = $"Lv {e.Level}";
            _enemySlots[i].RightBadge.text = e.SoldiersOnly
                ? $"병사 {Mathf.RoundToInt(s.Get(StatType.SoldierCount))}"
                : $"병사 {Mathf.RoundToInt(s.Get(StatType.SoldierCount))}";

            int captured = at;
            _enemySlots[i].Button.onClick.RemoveAllListeners();
            _enemySlots[i].Button.onClick.AddListener(() => ShowEnemyDetail(captured));
        }
    }

    /// <summary>이 줄에 서는 편성 번호. 없으면 -1.</summary>
    int IndexAtLane(int lane)
    {
        for (int i = 0; i < _enemies.Count; i++)
            if (HeroSpawner.LaneFor(i) == lane) return i;
        return -1;
    }

    /// <summary>
    /// 적 상세 — <b>영웅 상세 창(HeroDetailPopup)을 그대로 연다.</b>
    ///
    /// ⚠ UnitEntry 를 이름 시드로 만들어 넘긴다
    ///   용사는 플레이어가 키운 장수가 아니라 세이브 기록이 없다. 직업·등급은
    ///   전부 이름에서 결정적으로 나오므로(UnitJobRoller) 이름과 장수만 채우면
    ///   실제로 설 그 용사와 같은 값이 나온다 — HeroSpawner 가 쓰는 방식 그대로다.
    /// </summary>
    void ShowEnemyDetail(int index)
    {
        SpawnEntry e = _enemies[index];

        var popup = PopupManager.Instance?.Open<HeroDetailPopup>(PopupType.HeroDetail);

        if (popup == null)
        {
            Debug.LogError("[BattleInfoPopup] 영웅 상세 창을 열지 못했습니다.\n" +
                           "Tools > Project K > 프리팹 생성 > 팝업 > HeroDetail 을 실행하고 " +
                           "PopupManager > Load Popup Prefabs 를 누르세요.");
            return;
        }

        popup.SetupHero(new UnitEntry { UnitName = e.Name, Level = e.Level },
                        HeroNameRule.Of(e.Name, e.IsBossHero, e.IsEliteHero));
    }

    // ── 아군 ─────────────────────────────────────────────────

    void BuildAllies()
    {
        _allies.Clear();

        var deck = UserDataManager.Instance.Get<SummonDeckData>();

        // ⚠ 덱에 **있는 것만**이다 (사용자 확정). 도감 전체가 아니다 —
        //   이 화면은 "이번 판에 낼 수 있는 것" 을 보는 자리다.
        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;
            _allies.Add(slot);
        }

        var    codex    = UserDataManager.Instance.Get<MonsterCodexData>();
        var    catalog  = CardCatalog.Current;
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;

        for (int i = 0; i < _allySlots.Length; i++)
        {
            bool has = i < _allies.Count;
            _allySlots[i].Root.SetActive(has);
            if (!has) continue;

            SummonDeckSlot     slot = _allies[i];
            MonsterSpeciesData sp   = catalog?.GetMonster(slot.Id);

            if (sp == null)
            {
                // ⚠ 조용히 끄지 않는다 — 덱에는 있는데 화면에만 없는 카드가 된다.
                //   카드 목록(CardCatalog)을 다시 굽지 않았을 때 이렇게 된다.
                Debug.LogError($"[BattleInfoPopup] 카드 목록에 '{slot.Id}' 가 없습니다 — " +
                               "데이터 생성 > 카드 목록 을 실행하세요.");
                _allySlots[i].Root.SetActive(false);
                continue;
            }

            _allySlots[i].Portrait.sprite  = MonsterPortraitProvider.Get(sp) ?? _fallbackPortrait;
            _allySlots[i].Portrait.enabled = _allySlots[i].Portrait.sprite != null;

            _allySlots[i].NameText.text = sp.DisplayName;

            // ⚠ 마나·마릿수는 배지의 숫자만 채운다 — 그림은 Creator 가 박았다.
            //   글자로 "마나 5" 라고 적지 않는다 (UI 규칙 7).
            //   ⚠ 비용은 SummonCostRule 하나가 정한다 — 여기서 다시 계산하지 말 것.
            //     개성 할인과 과부하가 그 안에 들어 있다 (하단 카드 바와 같은 함수).
            if (_allySlots[i].ManaText != null)
                _allySlots[i].ManaText.text = $"{SummonCostRule.CostFor(sp.Id, SummonerPerkRuntime.ManaCostFor(summoner, sp, slot))}";

            if (_allySlots[i].CountText != null)
                _allySlots[i].CountText.text = $"{RunPerkRule.SummonCountFor(summoner, sp)}";

            UnitGrade g = codex.IsUnlocked(sp.Id) ? codex.GetGrade(sp.Id) : UnitGrade.Normal;

            _allySlots[i].LeftBadge .text = $"Lv {slot.Level}";
            _allySlots[i].RightBadge.text = LocalizationManager.Instance.Get(g.ToString());

            int captured = i;
            _allySlots[i].Button.onClick.RemoveAllListeners();
            _allySlots[i].Button.onClick.AddListener(() => ShowAllyDetail(captured));
        }
    }

    /// <summary>
    /// 아군 상세 — <b>지금 이 런의 값</b>이다.
    ///
    /// ⚠ 종족 기본값이 아니다 (파일 머리 주석 참고)
    ///   전투가 쓰는 것과 같은 MonsterStatComposer 를 지나므로
    ///   소환력·친화·카드 레벨·품질·시너지·특성·유물·장비가 전부 들어 있다.
    /// </summary>
    void ShowAllyDetail(int index)
    {
        SummonDeckSlot     slot = _allies[index];
        MonsterSpeciesData sp   = CardCatalog.Current.GetMonster(slot.Id);

        var popup = PopupManager.Instance?.Open<MonsterDetailPopup>(PopupType.MonsterDetail);

        if (popup == null)
        {
            Debug.LogError("[BattleInfoPopup] 몬스터 상세 창을 열지 못했습니다.\n" +
                           "Tools > Project K > 프리팹 생성 > 팝업 > 몬스터 상세 를 실행하고 " +
                           "PopupManager > Load Popup Prefabs 를 누르세요.");
            return;
        }

        // ⚠ 런 값 모드다 — 도감에서 열 때와 다른 숫자를 보여 준다 (머리 주석 참고).
        popup.SetupRun(sp, slot);
    }

}
