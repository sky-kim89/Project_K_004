using System;
using UnityEngine;

// ============================================================
//  BattleSettingsData.cs
//  조작·사운드 설정 저장 섹션.
//
//  AutoSkill  : 액티브 스킬 자동 사용 여부 (상단바 AUTO 토글).
//               꺼져 있으면 장수 카드를 눌러야만 스킬이 나간다.
//  SpeedIndex : 배속 토글 단계 인덱스 (TopBarUI.SpeedSteps 의 인덱스).
//  SfxOn      : 효과음 재생 여부 (PausePopup 토글).
//  BgmOn      : 배경음 재생 여부 (PausePopup 토글).
//
//  ■ 사운드도 여기 둔다
//    저장 섹션을 하나 더 만들 만큼의 내용이 아니고, "환생해도 남는 취향" 이라는
//    성격이 배속·자동 스킬과 똑같다. 섹션 키(SaveKey.BattleSettings)는 그대로다.
//
//  ⚠ 환생으로 초기화하지 않는다
//    런 데이터가 아니라 조작 취향이다. UserDataManager.Reincarnate() 목록에
//    넣으면 환생할 때마다 배속과 자동 설정이 풀린다.
// ============================================================

public class BattleSettingsData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.BattleSettings;

    RawData _raw = new();

    /// <summary>
    /// 자동 스킬 사용 여부의 정적 사본.
    ///
    /// ActiveSkillAISystem(ECS) 이 매 프레임 읽는 값이다.
    /// 그때마다 UserDataManager 의 섹션 딕셔너리를 훑지 않도록 여기에 복사해 둔다.
    /// 쓰기는 이 클래스 안에서만 한다 — 저장값과 어긋나지 않게 하려는 것이다.
    /// </summary>
    public static bool AutoSkillEnabled { get; private set; }

    /// <summary>
    /// 사운드 on/off 의 정적 사본.
    ///
    /// AudioManager 가 효과음 하나하나마다(그리고 배경음은 매 프레임) 읽는 값이다.
    /// AutoSkillEnabled 와 같은 이유로 여기 복사해 둔다 —
    /// 덕분에 어디서 토글하든 AudioManager 에 따로 알려 줄 필요가 없다.
    ///
    /// ⚠ 기본값은 켜짐이다
    ///   저장 파일이 없거나(첫 실행) 이 필드가 없던 옛 저장을 읽어도
    ///   소리가 꺼진 채로 시작하면 안 된다.
    /// </summary>
    public static bool SfxEnabled { get; private set; } = true;
    public static bool BgmEnabled { get; private set; } = true;

    // ── 읽기 ────────────────────────────────────────────────────

    public bool AutoSkill  => _raw.AutoSkill;
    public int  SpeedIndex => _raw.SpeedIndex;
    public bool SfxOn      => _raw.SfxOn;
    public bool BgmOn      => _raw.BgmOn;

    // ── 쓰기 ────────────────────────────────────────────────────

    public void SetAutoSkill(bool on)
    {
        _raw.AutoSkill   = on;
        AutoSkillEnabled = on;
    }

    public void SetSpeedIndex(int index) => _raw.SpeedIndex = index;

    public void SetSfxOn(bool on) { _raw.SfxOn = on; SfxEnabled = on; }
    public void SetBgmOn(bool on) { _raw.BgmOn = on; BgmEnabled = on; }

    // ── ISaveSection ────────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(_raw);

    public void Deserialize(string json)
    {
        _raw = JsonUtility.FromJson<RawData>(json) ?? new RawData();
        Mirror();
    }

    public void SetDefaults()
    {
        _raw = new RawData();
        Mirror();
    }

    /// <summary>정적 사본을 저장값에 맞춘다. 읽는 경로가 둘(Deserialize·SetDefaults)이라 묶어 둔다.</summary>
    void Mirror()
    {
        AutoSkillEnabled = _raw.AutoSkill;
        SfxEnabled       = _raw.SfxOn;
        BgmEnabled       = _raw.BgmOn;
    }

    [Serializable]
    class RawData
    {
        public bool AutoSkill  = false;   // 기본은 수동 — 장수 카드를 눌러 쓴다
        public int  SpeedIndex = 0;       // 1×

        // ⚠ 필드 초기값이 곧 '옛 저장 파일의 기본값' 이다
        //   JsonUtility 는 JSON 에 없는 필드를 건드리지 않으므로, 이 두 줄이
        //   사운드 설정이 없던 시절의 저장을 읽었을 때의 값이 된다.
        public bool SfxOn = true;
        public bool BgmOn = true;
    }
}
