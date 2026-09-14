using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PassiveSkillDatabase.cs
//  모든 패시브 스킬 데이터를 보관하는 ScriptableObject.
//
//  ■ 사용법
//    - Assets → Create → BattleGame → PassiveSkillDatabase 로 생성
//    - InGameManager 에서 PassiveSkillDatabase.Current 에 주입
//    - PassiveSkillApplier 등에서 PassiveSkillDatabase.Current.Get(type) 로 조회
//
//  ■ Entries 등록
//    각 PassiveSkillData SO 를 Entries 리스트에 순서 무관하게 추가.
//    Get() 이 내부 캐시를 통해 O(1) 조회를 지원한다.
// ============================================================

[CreateAssetMenu(fileName = "PassiveSkillDatabase", menuName = "BattleGame/PassiveSkillDatabase")]
public class PassiveSkillDatabase : ScriptableObject
{
    // ── 전역 참조 ────────────────────────────────────────────
    static PassiveSkillDatabase _current;
    public static PassiveSkillDatabase Current
        => _current != null ? _current : (_current = Resources.Load<PassiveSkillDatabase>("PassiveSkillDatabase"));

    [Header("패시브 스킬 목록")]
    [Tooltip("모든 PassiveSkillData SO 를 여기에 등록. 순서 무관.")]
    public List<PassiveSkillData> Entries = new();

    // ── 내부 캐시 ────────────────────────────────────────────
    Dictionary<PassiveSkillType, PassiveSkillData> _cache;

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>
    /// PassiveSkillType 으로 데이터를 조회한다.
    /// 등록되지 않은 타입이면 null 반환.
    /// </summary>
    public PassiveSkillData Get(PassiveSkillType type)
    {
        BuildCacheIfNeeded();
        _cache.TryGetValue(type, out var data);
        return data;
    }

    // ── Unity 생명주기 ────────────────────────────────────────

    void OnEnable()
    {
        _cache = null;  // SO 리로드 시 캐시 무효화
    }

    // ── 내부 ─────────────────────────────────────────────────

    void BuildCacheIfNeeded()
    {
        if (_cache != null) return;

        _cache = new Dictionary<PassiveSkillType, PassiveSkillData>(Entries.Count);
        foreach (var entry in Entries)
        {
            if (entry == null) continue;
            if (_cache.ContainsKey(entry.Type))
            {
                Debug.LogWarning($"[PassiveSkillDatabase] 중복 등록: {entry.Type} — 첫 번째 항목을 유지합니다.");
                continue;
            }
            _cache[entry.Type] = entry;
        }
    }
}
