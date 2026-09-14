using System;
using UnityEngine;

// ============================================================
//  UserData.cs
//  유저 기본 정보 저장 섹션.
//
//  보유 데이터:
//  - 닉네임, 레벨, 경험치
//  - 최초 접속일, 마지막 접속일
//
//  골드 등 재화는 ItemData(eItem.Gold)로 관리.
//  새 유저 정보 추가 시: UserRawData 내부에 필드만 추가하면 됨.
// ============================================================

public class UserData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.UserData;

    // ── 런타임 접근용 프로퍼티 ───────────────────────────────

    public string Nickname      => _raw.Nickname;
    public int    Level         => _raw.Level;
    public int    Exp           => _raw.Exp;
    public string FirstLoginAt  => _raw.FirstLoginAt;
    public string LastLoginAt   => _raw.LastLoginAt;

    // ── 내부 직렬화 데이터 ───────────────────────────────────

    UserRawData _raw = new();

    // ── 데이터 갱신 메서드 ───────────────────────────────────

    public void SetNickname(string nickname)   => _raw.Nickname = nickname;
    public void SetLevel(int level)            => _raw.Level    = level;
    public void AddExp(int amount)             => _raw.Exp     += amount;
    public void UpdateLastLogin()              => _raw.LastLoginAt = DateTime.UtcNow.ToString("O");

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()              => JsonUtility.ToJson(_raw);
    public void   Deserialize(string json) => _raw = JsonUtility.FromJson<UserRawData>(json) ?? new UserRawData();

    public void SetDefaults()
    {
        _raw = new UserRawData
        {
            Nickname     = "Player",
            Level        = 1,
            Exp          = 0,
            FirstLoginAt = DateTime.UtcNow.ToString("O"),
            LastLoginAt  = DateTime.UtcNow.ToString("O"),
        };
    }

    // ── 직렬화 전용 내부 클래스 ──────────────────────────────

    [Serializable]
    class UserRawData
    {
        public string Nickname     = "Player";
        public int    Level        = 1;
        public int    Exp          = 0;
        public string FirstLoginAt = "";
        public string LastLoginAt  = "";
    }
}
