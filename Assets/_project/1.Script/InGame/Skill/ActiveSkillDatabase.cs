using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  ActiveSkillDatabase.cs
//  모든 액티브 스킬 데이터를 보관하는 ScriptableObject.
//
//  ■ 사용법
//    - Assets → Create → BattleGame → ActiveSkillDatabase 로 생성
//    - InGameManager.Awake() 에서 ActiveSkillDatabase.Current 에 주입
//    - ActiveSkillExecuteSystem 이 Get(skillId) 로 스킬 데이터 조회
// ============================================================

[CreateAssetMenu(fileName = "ActiveSkillDatabase", menuName = "BattleGame/ActiveSkillDatabase")]
public class ActiveSkillDatabase : ScriptableObject
{
    // ── 전역 참조 ────────────────────────────────────────────
    static ActiveSkillDatabase _current;
    public static ActiveSkillDatabase Current
        => _current != null ? _current : (_current = Resources.Load<ActiveSkillDatabase>("ActiveSkillDatabase"));

    [Header("액티브 스킬 목록")]
    [Tooltip("모든 ActiveSkillData SO 를 여기에 등록. 순서 무관.")]
    public List<ActiveSkillData> Entries = new();

    // ── 내부 캐시 ────────────────────────────────────────────
    Dictionary<ActiveSkillId, ActiveSkillData> _cache;

    // ── 조회 ─────────────────────────────────────────────────

    /// <summary>
    /// ActiveSkillId 로 데이터를 조회한다.
    /// 등록되지 않은 ID 이면 null 반환.
    /// </summary>
    public ActiveSkillData Get(ActiveSkillId id)
    {
        BuildCacheIfNeeded();
        _cache.TryGetValue(id, out var data);
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

        _cache = new Dictionary<ActiveSkillId, ActiveSkillData>(Entries.Count);
        foreach (var entry in Entries)
        {
            if (entry == null) continue;
            if (_cache.ContainsKey(entry.SkillId))
            {
                Debug.LogWarning($"[ActiveSkillDatabase] 중복 등록: {entry.SkillId} — 첫 번째 항목을 유지합니다.");
                continue;
            }
            _cache[entry.SkillId] = entry;
        }
    }
}
