using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  MainPanelUI.cs
//  런 시작 전 **소환사 선택** 화면.
//
//  ┌ 사이드 ┐┌── 소환사 격자 ──┐┌──── 오른쪽 칸 ────┐
//  │ 유물   ││ [카드][카드][카드] ││ ① 소환사 정보      │
//  │ 도감   ││ [카드][카드][카드] ││    [ 선택하기 ]    │
//  │        ││ …  (3열 × 4줄)    ││ ② 난이도 선택      │
//  └────────┘└──────────────────┘│ [뒤로][ 게임 시작 ]│
//                                 └───────────────────┘
//
//  ■ 두 단계다 (2026-09-11, 사용자 요청으로 화면을 다시 짬)
//    ① 격자에서 카드를 누르면 오른쪽에 그 소환사의 정보가 뜬다
//    ② [선택하기] 를 누르면 정보 칸이 **난이도 선택 칸으로 바뀐다**
//    ③ [게임 시작] → RunLaunch.Request(id) → 인게임
//    ⚠ 난이도 단계에서 격자의 다른 카드를 누르면 ① 로 돌아간다 —
//      고른 소환사가 바뀌었는데 난이도 칸이 그대로 떠 있으면 누구로 출정하는지가 흐려진다.
//
//  ■ 후보는 굴리지 않는다 — CardCatalog 의 소환사 전원이 격자에 선다
//    3대 스탯 합계가 전원 같도록 잡혀 있어(19 고정) 무엇이 나오느냐가 아니라
//    **무엇을 고르느냐**가 선택이다.
//
//  ■ 시작이 두 단계인 이유
//    Splash 가 Lobby 와 InGame 을 둘 다 올려 두므로 인게임은 이미 살아 있고
//    RunBootstrap 이 **요청을 기다리는 중**이다. 여기서 하는 일은
//    "이 소환사로 시작해" 를 알리고 화면을 인게임으로 돌리는 것뿐이다.
//
//  Inspector 연결은 MainPanelCreator 가 한다.
// ============================================================

public class MainPanelUI : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] Image _backgroundImage;

    [Header("소환사 격자")]
    [SerializeField] SummonerListCardUI[] _listCards;

    [Header("① 정보 단계")]
    [SerializeField] GameObject              _infoPhase;
    [SerializeField] SummonerCandidateCardUI _info;
    [SerializeField] Button                  _selectBtn;
    [SerializeField] TextMeshProUGUI         _selectLabel;

    [Header("② 난이도 단계")]
    [SerializeField] GameObject      _difficultyPhase;
    [SerializeField] TextMeshProUGUI _chosenText;
    [SerializeField] Button          _backBtn;
    [SerializeField] Button          _startBtn;

    [Header("사이드")]
    [SerializeField] Button _relicBtn;
    [SerializeField] Button _codexBtn;

    int _selected;
    readonly List<SummonerData> _candidates = new(12);

    SummonerData Chosen => _candidates[_selected];

    static readonly Color LabelOn  = Color.white;
    static readonly Color LabelOff = new(0.55f, 0.57f, 0.68f);

    /// <summary>
    /// 이 화면이 떴다 — 튜토리얼이 이 신호로 안내를 건다.
    /// ⚠ 여기서 TryPlay 를 부르지 않는다 — "언제 띄울지" 는 TutorialManager 가 소유한다.
    /// </summary>
    public static event System.Action OnShown;

    /// <summary>이 화면을 떠났다 — 아직 시작 못 한 튜토리얼 예약을 취소한다.</summary>
    public static event System.Action OnHidden;

    // ── 생명주기 ──────────────────────────────────────────────

    void Awake()
    {
        _selectBtn.onClick.AddListener(ShowDifficulty);
        _backBtn.onClick.AddListener(ShowInfo);
        _startBtn.onClick.AddListener(OnStartPressed);

        // ⚠ 탭 전환(Switch)으로 열지 않는다
        //   탭은 이 패널을 끈다 → OnEnable 이 다시 돌아 고르던 소환사가 초기화된다.
        //   팝업으로 덮어 이 화면을 그대로 살려 둔다.
        //   닫히면 숫자를 다시 읽는다 — 유물이 마왕성 체력을 올린다.
        _relicBtn.onClick.AddListener(() =>
            PopupManager.Instance.Open<RelicTreePopup>(PopupType.Relic).SetOnClose(RefreshAll));

        _codexBtn.onClick.AddListener(() =>
            PopupManager.Instance.Open<CodexPopup>(PopupType.Codex));
    }

    void OnEnable()
    {
        // 배경으로 전장을 비춘다 — 정적인 화면을 피한다.
        SceneDirector.Ensure().RequestArenaBackdrop(true);

        LoadCandidates();
        _selected = 0;   // 0번 = 견습 소환사 (SummonerData.ListOrder)

        if (_candidates.Count > 0)
        {
            RefreshList();
            ShowInfo();
        }

        // 화면을 다 세운 뒤에 알린다 — 튜토리얼이 곧바로 버튼을 누르게 한다.
        OnShown?.Invoke();
    }

    void OnDisable()
    {
        SceneDirector.Instance?.RequestArenaBackdrop(false);

        OnHidden?.Invoke();
    }

    // ── 후보 목록 ─────────────────────────────────────────────

    /// <summary>
    /// 고를 수 있는 소환사 전원 (카탈로그 순서 = ListOrder).
    /// ⚠ 시작 카드가 없는 소환사는 뺀다 — 골라도 소환을 아예 못 한다.
    /// </summary>
    void LoadCandidates()
    {
        _candidates.Clear();

        foreach (SummonerData summoner in CardCatalog.Current.Summoners)
        {
            if (summoner.StarterMonsters.Length == 0)
            {
                Debug.LogWarning($"[MainPanelUI] '{summoner.Id}' 는 시작 카드가 없어 목록에서 뺍니다.");
                continue;
            }

            _candidates.Add(summoner);
        }

        if (_candidates.Count == 0)
            Debug.LogError("[MainPanelUI] 고를 수 있는 소환사가 없습니다. " +
                           "데이터 생성 > 몬스터 도감 → 소환사 → 카드 목록 순서로 실행하세요.");

        // ⚠ 격자 칸은 Creator 가 굽는다 — 소환사가 늘면 칸도 늘려야 한다.
        //   조용히 잘리면 "그 캐릭터가 사라졌다" 를 추적하게 된다.
        if (_candidates.Count > _listCards.Length)
            Debug.LogError($"[MainPanelUI] 소환사 {_candidates.Count}명인데 격자 칸은 {_listCards.Length}개입니다. " +
                           "MainPanelCreator 의 ListCols/ListRows 를 늘리고 다시 구우세요.");
    }

    // ── 격자 ──────────────────────────────────────────────────

    void RefreshList()
    {
        for (int i = 0; i < _listCards.Length; i++)
        {
            bool has = i < _candidates.Count;
            _listCards[i].gameObject.SetActive(has);
            if (!has) continue;

            _listCards[i].Setup(_candidates[i], Select);
            _listCards[i].SetSelected(i == _selected);
        }
    }

    void Select(SummonerData summoner)
    {
        _selected = _candidates.IndexOf(summoner);

        for (int i = 0; i < _candidates.Count && i < _listCards.Length; i++)
            _listCards[i].SetSelected(i == _selected);

        ShowInfo();
    }

    void RefreshAll()
    {
        RefreshList();
        if (_infoPhase.activeSelf) ShowInfo();
    }

    // ── 단계 전환 ─────────────────────────────────────────────

    /// <summary>
    /// ① 정보 단계. 잠긴 소환사는 선택 버튼이 꺼진다.
    ///
    /// ⚠ 잠긴 소환사를 격자에서 빼지 않는다
    ///   숨기면 존재를 모르니 해금 조건이 목표가 되지 못한다.
    /// ⚠ 판정은 정보 칸에게 묻는다 — 같은 규칙을 두 곳에서 부르면 언젠가 갈린다.
    /// </summary>
    void ShowInfo()
    {
        _infoPhase.SetActive(true);
        _difficultyPhase.SetActive(false);

        _info.Setup(Chosen);

        bool open = _info.IsUnlocked;
        _selectBtn.interactable = open;
        _selectLabel.text  = open ? "선택하기" : "잠겨 있음";
        _selectLabel.color = open ? LabelOn : LabelOff;
    }

    /// <summary>② 난이도 단계 — 정보 칸 자리에 난이도 칸이 선다.</summary>
    void ShowDifficulty()
    {
        if (!_info.IsUnlocked) return;

        _infoPhase.SetActive(false);
        _difficultyPhase.SetActive(true);

        _chosenText.text = $"{Chosen.DisplayName}  —  출정 준비";
    }

    // ── 게임 시작 ─────────────────────────────────────────────

    /// <summary>
    /// 고른 소환사로 런을 시작한다.
    ///
    /// ⚠ 여기서 소환사를 세우지 않는다 — 스폰·마나·카드 배치는 전부 RunBootstrap 이 소유한다.
    /// </summary>
    void OnStartPressed()
    {
        SummonerData chosen = Chosen;

        // ⚠ 버튼이 꺼져 있어도 한 번 더 막는다
        //   다른 경로(단축키·자동 시작·튜토리얼)가 이 함수를 직접 부를 수 있다.
        //   잠금은 규칙이지 버튼 상태가 아니다.
        if (!SummonerUnlockRule.IsUnlocked(chosen))
        {
            Debug.Log($"[MainPanelUI] '{chosen.DisplayName}' 는 아직 잠겨 있습니다 — " +
                      SummonerUnlockRule.Describe(chosen).Replace('\n', ' '));
            return;
        }

        RunLaunch.Request(chosen.Id);

        PopupManager.Instance?.CloseAll();

        // ⚠ SceneDirector.Present 를 직접 부르지 않는다
        //   화면·전장·상태 셋이 함께 움직여야 한다 (LobbyManager.OnEnterFlow 주석).
        LobbyManager.Instance.EnterSummonRun();
    }
}
