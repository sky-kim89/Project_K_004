#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  ActiveSkillDebugOverlay.cs
//  액티브 스킬 발동 위치 · 범위를 에디터 기즈모로 시각화.
//
//  사용법:
//    씬의 아무 GameObject 에 AddComponent 하거나,
//    도구 메뉴 [BattleGame > Add Skill Debug Overlay] 로 자동 추가.
//
//  표시 내용:
//    ● 노란 구   — 스킬 발동 예정 위치 (1초간 유지)
//    ○ 초록 원   — 존 스킬 범위 (SkillZoneRunner 가 그림)
//    ○ 파란 원   — 직접 AoE 스킬 범위 (별도 등록 시)
// ============================================================

public class ActiveSkillDebugOverlay : MonoBehaviour
{
    struct Entry
    {
        public Vector3 Center;
        public float   Radius;
        public Color   Color;
        public float   ExpireTime;
        public string  Label;
    }

    static ActiveSkillDebugOverlay _instance;

    public static ActiveSkillDebugOverlay Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[SkillDebugOverlay]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ActiveSkillDebugOverlay>();
            }
            return _instance;
        }
    }

    readonly List<Entry> _entries = new();

    /// <summary>존 스킬 발동 위치 + 범위를 등록 (초록).</summary>
    public static void RegisterZone(Vector3 center, float radius, string label = "")
    {
        Instance._entries.Add(new Entry
        {
            Center     = center,
            Radius     = radius,
            Color      = new Color(0.2f, 1f, 0.2f, 0.85f),
            ExpireTime = Time.time + 1.5f,
            Label      = string.IsNullOrEmpty(label) ? "Zone" : label,
        });
    }

    /// <summary>AoE 스킬 발동 위치 + 범위를 등록 (파란).</summary>
    public static void RegisterAoe(Vector3 center, float radius, string label = "")
    {
        Instance._entries.Add(new Entry
        {
            Center     = center,
            Radius     = radius,
            Color      = new Color(0.2f, 0.6f, 1f, 0.85f),
            ExpireTime = Time.time + 1.5f,
            Label      = string.IsNullOrEmpty(label) ? "AoE" : label,
        });
    }

    /// <summary>단일 대상 스킬 발동 위치를 등록 (빨간).</summary>
    public static void RegisterSingle(Vector3 center, string label = "")
    {
        Instance._entries.Add(new Entry
        {
            Center     = center,
            Radius     = 0.3f,
            Color      = new Color(1f, 0.3f, 0.3f, 0.85f),
            ExpireTime = Time.time + 1.5f,
            Label      = string.IsNullOrEmpty(label) ? "Hit" : label,
        });
    }

    void OnDrawGizmos()
    {
        float now = Time.time;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].ExpireTime < now) { _entries.RemoveAt(i); continue; }

            var e = _entries[i];
            float t = 1f - Mathf.Clamp01((e.ExpireTime - now) / 1.5f);  // 0→1 fade
            Color c = e.Color;
            c.a *= (1f - t * 0.7f);

            // 원형 범위
            UnityEditor.Handles.color = new Color(c.r, c.g, c.b, c.a * 0.2f);
            UnityEditor.Handles.DrawSolidDisc(e.Center, Vector3.forward, e.Radius);
            UnityEditor.Handles.color = c;
            UnityEditor.Handles.DrawWireDisc(e.Center, Vector3.forward, e.Radius);

            // 중심 마커
            Gizmos.color = c;
            Gizmos.DrawSphere(e.Center, 0.1f);

            // 라벨
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(e.Center + new Vector3(0f, e.Radius + 0.15f, 0f), e.Label);
        }
    }
}
#endif
