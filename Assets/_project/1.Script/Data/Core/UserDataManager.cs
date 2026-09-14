using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  UserDataManager.cs
//  데이터 저장 시스템의 중심 관리자. PureSingleton 기반.
//
//  주요 역할:
//  - ISaveSection 구현체를 등록·보관
//  - 게임 시작 시 전체 섹션 로드 (LoadAll)
//  - 변경 발생 시 1프레임 지연 일괄 저장 (RequestSave → SaveCoordinator)
//
//  사용법:
//    // 섹션 접근
//    UserData user = UserDataManager.Instance.Get<UserData>();
//
//    // 데이터 변경 후 저장 예약
//    user.AddGold(100);
//    UserDataManager.Instance.RequestSave();
//
//  새 섹션 추가 방법:
//  1. ISaveSection 구현 클래스 작성 (SaveKey 추가 포함)
//  2. OnInitialize() 안에서 RegisterSection(new YourSection()) 호출
// ============================================================

public class UserDataManager : PureSingleton<UserDataManager>
{
    // ── 섹션 저장소 ──────────────────────────────────────────

    readonly Dictionary<SaveKey, ISaveSection> _sections = new();

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 타입으로 섹션을 가져온다.
    /// 등록되지 않은 타입이면 null 을 반환한다.
    /// </summary>
    public T Get<T>() where T : class, ISaveSection
    {
        foreach (ISaveSection section in _sections.Values)
        {
            if (section is T typed)
                return typed;
        }
        return null;
    }

    /// <summary>
    /// 저장 키로 섹션을 가져온다.
    /// </summary>
    public ISaveSection Get(SaveKey key)
    {
        _sections.TryGetValue(key, out ISaveSection section);
        return section;
    }

    /// <summary>
    /// 다음 프레임에 전체 섹션을 일괄 저장하도록 예약한다.
    /// 같은 프레임에 여러 번 호출해도 저장은 1회만 실행된다.
    /// </summary>
    public void RequestSave()
    {
        SaveCoordinator.Request(SaveAll);
    }

    /// <summary>전체 섹션을 즉시 저장한다. 일반적으로 RequestSave 를 사용할 것.</summary>
    public void SaveAll()
    {
        foreach (ISaveSection section in _sections.Values)
        {
            string json = section.Serialize();
            PlayerPrefs.SetString(GetPrefKey(section.SaveKey), json);
        }
        PlayerPrefs.Save();
        Debug.Log("[UserDataManager] 저장 완료");
    }

    // ── 최초 실행 판별 ───────────────────────────────────────

    // ⚠ "세이브가 하나도 없었다" 가 유일하게 믿을 수 있는 신호다
    //   장수 수·클리어 수로 판단하면 환생 직후와 구분이 안 된다 —
    //   환생하면 장수도 스테이지도 0 으로 돌아가지만 그때는 장수를 골라야 한다.
    //   UserData 는 환생해도 남으므로 이 키의 유무가 설치 직후인지를 가른다.
    bool _firstLaunch;

    /// <summary>
    /// 설치 후 첫 실행이면 true 를 한 번만 돌려주고 플래그를 내린다.
    /// ⚠ 반드시 소비형이어야 한다 — 전투를 마치고 로비 씬을 다시 로드해도
    ///   매니저는 살아 있어서, 플래그가 남아 있으면 자동 진입이 무한히 반복된다.
    /// </summary>
    public bool ConsumeFirstLaunch()
    {
        bool was = _firstLaunch;
        _firstLaunch = false;
        return was;
    }

    /// <summary>전체 섹션의 데이터를 디스크에서 로드한다.</summary>
    public void LoadAll()
    {
        _firstLaunch = !PlayerPrefs.HasKey(GetPrefKey(SaveKey.UserData));

        foreach (ISaveSection section in _sections.Values)
        {
            string prefKey = GetPrefKey(section.SaveKey);
            if (PlayerPrefs.HasKey(prefKey))
            {
                section.Deserialize(PlayerPrefs.GetString(prefKey));
            }
            else
            {
                section.SetDefaults();
            }
        }
        Debug.Log("[UserDataManager] 로드 완료");
    }

    /// <summary>특정 섹션만 저장한다.</summary>
    public void SaveSection(SaveKey key)
    {
        if (!_sections.TryGetValue(key, out ISaveSection section)) return;

        string json = section.Serialize();
        PlayerPrefs.SetString(GetPrefKey(key), json);
        PlayerPrefs.Save();
    }

    // ── 환생 ─────────────────────────────────────────────────

    /// <summary>
    /// 환생: 환생 포인트 적립 후 유물·환생 데이터·유저 정보를 제외한
    /// 런 데이터(재화·스테이지·카드·마나·특성)를 초기화.
    /// </summary>
    public void Reincarnate()
    {
        var stageData = Get<StageProgressData>();
        var reincData = Get<ReincarnationData>();

        int cleared = stageData?.ClearedNormalStages ?? 0;

        // ⚠ 런 골드 결산이 **초기화보다 먼저**다 (사용자 지적, 2026-09-11)
        //   환생 버튼(ReincarnationPopup)은 이 함수를 먼저 부르고 RunBootstrap.FinishRun 을
        //   나중에 부른다. 아래에서 RunGoldData 를 비우면 FinishRun 의 결산은 0 을 준다 —
        //   번 골드가 영구 골드로 한 푼도 안 들어왔다. 결산은 두 번 불려도 한 번만 준다.
        RunGoldRule.SettleToPermanent();

        // 포인트 적립 (초기화 전에 먼저 계산)
        reincData?.EarnPointsByStage(cleared);
        reincData?.ResetOnReincarnation();

        // 런 데이터 전체 초기화 (UserData·RelicInventory·ReincarnationData·Codex 제외)
        // ⚠ CodexData 를 여기에 넣지 말 것 — 도감은 "지금까지 만나 본 것" 의 영구 기록이다.
        //   회귀로 지워지면 도감 버프가 매 런 0 에서 다시 시작해 존재 의미가 사라진다.
        // ⚠ 영구 골드는 남긴다 — 품질 개선·장비 레벨업의 재화다 (ItemData.ResetKeepingGold)
        Get<ItemData>()?.ResetKeepingGold();
        Get<StageProgressData>()?.SetDefaults();

        // 소환 반전판(v3) 런 스코프 섹션
        // ⚠ MonsterCodexData 는 여기에 넣지 말 것 — 몬스터 도감은 해금 상태와
        //   종족별 영구 품질을 담는 영구 기록이다. 지워지면 품질 개선이 매 런 리셋된다.
        Get<SummonManaData>()?.SetDefaults();
        Get<SummonDeckData>()?.SetDefaults();
        Get<RunPerkData>()?.SetDefaults();

        // ⚠ 이어하기 자리표도 함께 지운다
        //   카드·마나는 위에서 비웠는데 이것만 남으면, 다음 실행에서
        //   빈손으로 지난 스테이지에 떨어진다.
        Get<SummonRunData>()?.SetDefaults();

        // ⚠ 라인 대기열도 함께 지운다
        //   다음 런이 지난 런의 예약분을 공짜로 물려받으면 안 된다.
        Get<SummonQueueData>()?.SetDefaults();

        // ⚠ 런 골드도 함께 지운다 — 런이 끝나면 사라지는 재화다
        //   남겨 두면 지난 런에서 모은 돈으로 다음 런의 갈림길을 사 버린다.
        //   영구 재화(ItemData.eItem.Gold)는 여기서 건드리지 않는다 — 다른 축이다.
        Get<RunGoldData>()?.SetDefaults();
        Get<RunCoreData>()?.SetDefaults();
        Get<RunBoonData>()?.SetDefaults();

        // ⚠ 도감은 손대지 않는다 — 순수한 기록이다 (CodexData 파일 머리 참고)
        //   한때 '이번 여정 고정치' 잠금과 수확 화면이 여기서 돌았는데,
        //   그 둘은 수집 버프를 설명하려고 있던 장치라 버프와 함께 폐기됐다.

        // 이번이 몇 번째 환생인지 — 첫 환생 직후에만 뜨는 안내가 이 값을 본다.
        reincData?.CountReincarnation();

        SaveAll();

        Debug.Log($"[UserDataManager] 환생 완료 — +{ReincarnationData.CalculateReincarnationPoints(cleared)}pt, 누적 {reincData?.ReincarnationPoints}pt");
    }

    // ── 초기화 ───────────────────────────────────────────────

    protected override void OnInitialize()
    {
        RegisterSection(new UserData());
        RegisterSection(new ItemData());
        RegisterSection(new StageProgressData());
        RegisterSection(new RelicTreeData());
        RegisterSection(new ReincarnationData());
        RegisterSection(new CodexData());
        RegisterSection(new BattleSettingsData());
        RegisterSection(new DifficultyData());
        // ⚠ Reincarnate() 의 초기화 목록에 넣지 말 것 — 환생마다 튜토리얼이 다시 뜬다.
        RegisterSection(new TutorialData());

        // ── 소환 반전판(v3) 신규 섹션 ─────────────────────────
        RegisterSection(new SummonManaData());
        RegisterSection(new SummonDeckData());
        RegisterSection(new RunPerkData());
        RegisterSection(new SummonRunData());
        RegisterSection(new SummonQueueData());
        RegisterSection(new RunGoldData());
        RegisterSection(new RunCoreData());
        RegisterSection(new RunBoonData());
        // ⚠ 아래 둘은 영구 데이터다 — Reincarnate() 초기화 목록에 넣지 말 것.
        //   도감(해금·품질)과 장비(보유·장착)는 런을 반복할 이유 그 자체다.
        RegisterSection(new MonsterCodexData());
        RegisterSection(new MonsterGearInventory());

        LoadAll();
    }

    // ── 내부 ─────────────────────────────────────────────────

    void RegisterSection(ISaveSection section)
    {
        if (_sections.ContainsKey(section.SaveKey))
        {
            Debug.LogWarning($"[UserDataManager] 중복 등록 시도: {section.SaveKey}");
            return;
        }
        _sections[section.SaveKey] = section;
    }

    static string GetPrefKey(SaveKey key) => $"Save_{(int)key}";
}
