using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  RunPerkBarUI.cs
//  상단바 왼쪽 — 이번 런에 **지금 갖고 있는 것**을 아이콘 줄로 세운다.
//    ① 소환사 개성 (런 시작에 이미 갖고 있는 것)
//    ② 주운 특성 (RunPerkData)
//
//  ■ 왜 만들었나 (사용자 지적, 2026-09-07)
//    보유 특성을 볼 곳이 아예 없었다. 이 자리에는 원작의 TraitBarUI 가
//    그대로 남아 있었는데, 그건 **원작의 특성 체계**(RunTraitData ·
//    JobSynergyEvaluator)를 읽는다. 이 게임은 그 축을 쓰지 않으므로
//    줄이 언제나 비어 있었다 — 화면에는 "특성 칸이 없다" 로 보인다.
//
//  ■ ⚠ 소환사는 HUD 보다 **늦게** 선다 (사용자 지적, 2026-09-07)
//    RunBootstrap 순서가 BuildStarterDeck → SpawnSummoner 라, 이 줄이 처음
//    그려질 때 SummonerRuntimeBridge.Current 는 아직 null 이다. 이벤트로만
//    다시 그리면 **개성 칸이 영영 비어 있다** — 견습 소환사의 '친화 할인'
//    같은 것이 화면에 한 번도 안 뜬다.
//    그래서 소환사가 잡힐 때까지 Update 에서 지켜본다
//    (SummonerSkillButtonUI 가 같은 이유로 같은 구조를 쓴다).
//
//  ■ 제단이 얹은 시너지도 여기 선다 (사용자 지적, 2026-09-07)
//    카드를 바쳐 얻은 표식 +1(RunBoonData)은 **카드가 사라진 대가**라
//    특성과 성격이 같다. 저장은 되고 시너지 계산에도 들어가는데
//    (MonsterSynergyRule.CountOf) 화면에 이름이 없어서, 무엇을 얻었는지
//    확인할 방법이 없었다.
//
//  ■ 소환사 개성을 맨 앞에 둔다
//    "기본적으로 갖고 있는 것" 도 특성이다. 주운 것과 같은 줄에 세워야
//    "지금 내 빌드가 무엇인가" 가 한 줄로 읽힌다. 다만 **맨 앞 고정**이라
//    특성을 주울 때마다 자리가 밀리지 않는다.
//
//  ■ 슬롯은 TraitIconUI 를 그대로 빌려 쓴다
//    아이콘 + 눌러서 툴팁이 이미 되어 있고, 난이도 디버프도 같은 방식으로
//    이 칸을 빌려 쓴다(SetupCustom). 새 컴포넌트를 만들면 툴팁 동작이
//    두 벌이 되어 미묘하게 달라진다.
//
//  ■ 칸이 많아지면 **작아지면서 두 줄**이 된다 (사용자 확정, 2026-09-10)
//      14개 이하 → 아이콘 72, 한 줄       ← 흔한 판. 지금까지와 같다
//      15개 이상 → 아이콘 48, 두 줄 20열
//    ⚠ 한때 14칸이 전부라 15번째부터 **조용히 잘렸다.** 아래 Refresh 의
//      `slot >= _slots.Length` 는 에러도 경고도 내지 않는다 — 화면에서는
//      "특성을 주웠는데 줄에 안 뜬다" 로만 보인다.
//    ⚠ 수치의 정본은 Creator(InGameUIPrefabCreator)다. 여기서는 **고르기만** 한다 —
//      자리 검산(줄 폭·칸 수)이 거기서만 가능하기 때문이다.
//
//  ⚠ 아이콘은 아틀라스에서 키로 찾는다 (RunPerkIconKey)
//    무엇을 주울지 런타임에 정해져서 Creator 가 미리 박아 둘 수 없다.
//    ⚠ 그림이 없으면 칸은 뜨되 회색이다 — 툴팁은 그대로 열린다.
//      "아이콘을 아직 안 구웠다" 가 특성을 통째로 숨겨서는 안 된다.
// ============================================================

public class RunPerkBarUI : MonoBehaviour
{
    [Tooltip("아이콘 칸. 소환사 개성 1 + 특성 30 + 제단 표식을 담을 만큼 있어야 한다.")]
    [SerializeField] TraitIconUI[] _slots;

    [Tooltip("시너지 아이콘. ⚠ MonsterSynergyRule.AllTags 순서 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _synergyIcons;

    // ── 두 모습 (Creator 가 채운다) ──────────────────────────

    [Header("칸 배치 — 값의 정본은 InGameUIPrefabCreator 다")]
    [SerializeField] GridLayoutGroup _grid;

    [Tooltip("넉넉할 때(칸 수 ≤ 열 수)의 아이콘 크기.")]
    [SerializeField] float _iconSize = 72f;

    [Tooltip("넉넉할 때의 열 수. 이 수를 넘으면 빽빽한 모습으로 바뀐다.")]
    [SerializeField] int _columns = 14;

    [Tooltip("빽빽할 때의 아이콘 크기. ⚠ 시너지 칩(48)이 읽히는 하한이다.")]
    [SerializeField] float _denseIconSize = 48f;

    [Tooltip("빽빽할 때의 열 수. 두 줄이므로 이 값 ×2 가 실제 수용량이다.")]
    [SerializeField] int _denseColumns = 20;

    RunPerkData _data;

    // 마지막으로 그린 상태. 매 프레임 다시 그리지 않기 위한 값이다.
    SummonerPerk _shownPerk = SummonerPerk.None;
    bool         _shownSummoner;
    int          _shownBoons = -1;

    void OnEnable()
    {
        Bind();
        Refresh();
    }

    void OnDisable()
    {
        if (_data != null) _data.Changed -= Refresh;
        _data = null;
    }

    /// <summary>
    /// 세이브 섹션을 잡고 구독한다.
    ///
    /// ⚠ Awake 에서 잡지 않는다 — HUD 는 씬에 미리 놓여 있고 런 데이터는
    ///   RunBootstrap 이 채운다. 잡힐 때까지 Update 에서 다시 시도한다.
    /// </summary>
    void Bind()
    {
        if (_data != null) return;

        var user = UserDataManager.Instance;
        if (user == null) return;

        _data = user.Get<RunPerkData>();
        if (_data == null) return;

        _data.Changed += Refresh;
    }

    /// <summary>
    /// 화면 밖에서 바뀌는 것 셋을 지켜본다.
    ///
    ///   ① 세이브 섹션이 아직 없다 (런 시작 전)
    ///   ② 소환사가 아직 안 섰다 — 개성 칸이 비어 있는 이유
    ///   ③ 제단이 표식을 얹었다 — RunBoonData 에는 Changed 이벤트가 없다
    ///
    /// ⚠ 매 프레임 Refresh 를 부르지 않는다
    ///   칸마다 SetupCustom 이 툴팁 리스너를 갈아 끼운다. 매 프레임 돌리면
    ///   툴팁이 열린 채로 지워져 눌러도 안 뜨는 상태가 된다.
    /// </summary>
    void Update()
    {
        if (_data == null)
        {
            Bind();
            if (_data != null) Refresh();
            return;
        }

        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        bool hasSummoner = summoner != null;

        if (hasSummoner != _shownSummoner
            || (hasSummoner && summoner.Perk != _shownPerk)
            || BoonTotal() != _shownBoons)
            Refresh();
    }

    /// <summary>제단이 얹어 둔 표식 수의 합. 바뀌었는지만 보면 되므로 합계로 충분하다.</summary>
    int BoonTotal()
    {
        int total = 0;

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            total += MonsterSynergyRule.BoonCountOf(tag);

        return total;
    }

    // ── 그리기 ───────────────────────────────────────────────

    /// <summary>
    /// 지금 세워야 하는 칸 수. <b>그리기 전에</b> 센다 — 칸 크기와 열 수를
    /// 먼저 정해야 격자가 한 번에 제 모습으로 그려진다.
    ///
    /// ⚠ 제단 표식은 개수가 아니라 <b>표식 종류</b>마다 한 칸이다
    ///   (아래 ③ 과 같은 규칙). 합계로 세면 언데드 +3 이 세 칸으로 잡힌다.
    /// </summary>
    int CountNeeded()
    {
        int n = 0;

        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        if (summoner != null && summoner.Perk != SummonerPerk.None) n++;

        if (_data != null)
            foreach (RunPerk perk in _data.Perks)
                if (perk != RunPerk.None) n++;

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            if (MonsterSynergyRule.BoonCountOf(tag) > 0) n++;

        return n;
    }

    /// <summary>
    /// 칸 수에 맞는 모습으로 격자를 맞춘다.
    ///
    /// ⚠ 값이 그대로면 아무것도 하지 않는다 — GridLayoutGroup 에 대입하면
    ///   그때마다 레이아웃 재계산이 예약된다. Refresh 는 특성을 주울 때마다
    ///   불리므로 굳이 매번 흔들 이유가 없다.
    ///
    /// ⚠ 세로는 건드리지 않는다 — 자리는 Creator 가 <b>언제나 두 줄만큼</b>
    ///   잡아 뒀다(PerkRowsH). 여기서 늘렸다 줄였다 하면 아래 마왕성 체력
    ///   막대와의 간격이 판마다 달라진다.
    /// </summary>
    void ApplyLayout(int needed)
    {
        if (_grid == null) return;   // Inspector 선택 연결 (코딩 규칙 예외)

        bool  dense = needed > _columns;
        float cell  = dense ? _denseIconSize : _iconSize;
        int   cols  = dense ? _denseColumns  : _columns;

        if (_grid.constraintCount == cols && Mathf.Approximately(_grid.cellSize.x, cell))
            return;

        _grid.cellSize        = new Vector2(cell, cell);
        _grid.constraintCount = cols;
    }

    void Refresh()
    {
        // ⚠ 칸을 채우기 **전에** 모습을 정한다 (ApplyLayout 주석 참고).
        ApplyLayout(CountNeeded());

        int slot = 0;

        // ① 소환사 개성 — 맨 앞 고정.
        //
        //    ⚠ 아이콘을 넘긴다 (사용자 지적, 2026-09-07 — 두 번 지적받았다)
        //      한때 SetupCustom(null, ...) 로 **그림 없이** 넘겼다. 주석에는
        //      "개성은 하나뿐이라 이름과 설명이면 된다" 고 적혀 있었지만,
        //      화면에서는 그냥 **빈 칸**이다 — 줄에 선 나머지 칸이 전부
        //      그림이라 더 그렇다. 개성은 그 런의 성격을 정하는 단 하나의
        //      상시 효과라 오히려 제일 잘 보여야 하는 칸이다.
        //      그림은 SummonerPerkIconGenerator 가 굽는다 (sperk_*).
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;

        _shownSummoner = summoner != null;
        _shownPerk     = summoner != null ? summoner.Perk : SummonerPerk.None;

        if (summoner != null && summoner.Perk != SummonerPerk.None && slot < _slots.Length)
        {
            _slots[slot].gameObject.SetActive(true);
            _slots[slot].SetupCustom(SpriteManager.Instance?.Get(summoner.Perk.IconKey()),
                                     $"{summoner.Perk.ToKorean()}  (소환사)",
                                     summoner.Perk.Describe(summoner.PerkValue));
            slot++;
        }

        // ② 주운 특성.
        if (_data != null)
        {
            foreach (RunPerk perk in _data.Perks)
            {
                if (slot >= _slots.Length) break;
                if (perk == RunPerk.None)  continue;

                _slots[slot].gameObject.SetActive(true);
                _slots[slot].SetupCustom(SpriteManager.Instance?.Get(perk.IconKey()),
                                         perk.ToKorean(),
                                         perk.Describe());
                slot++;
            }
        }

        // ③ 제단이 얹은 표식 — 카드를 바쳐 산 것이라 특성과 같은 줄에 선다.
        //    ⚠ 아이콘은 시너지 것을 그대로 쓴다. 전용 그림을 또 만들면
        //      상단 시너지 줄과 여기가 같은 것을 다르게 그리게 된다.
        _shownBoons = 0;

        foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
        {
            int n = MonsterSynergyRule.BoonCountOf(tag);
            if (n <= 0) continue;

            _shownBoons += n;
            if (slot >= _slots.Length) continue;

            int    idx  = MonsterSynergyRule.IndexOf(tag);
            Sprite icon = _synergyIcons != null && idx >= 0 && idx < _synergyIcons.Length
                        ? _synergyIcons[idx] : null;

            _slots[slot].gameObject.SetActive(true);
            _slots[slot].SetupCustom(icon,
                                     $"{MonsterSynergyRule.NameOf(tag)} +{n}  (제단)",
                                     "제물로 바친 카드가 남긴 표식이다. 카드가 없어도 카운트에 든다.");
            slot++;
        }

        // 남는 칸은 끈다 — 켜 둔 채로 두면 지난 런의 특성이 그대로 남는다.
        for (int i = slot; i < _slots.Length; i++)
            _slots[i].gameObject.SetActive(false);
    }
}
