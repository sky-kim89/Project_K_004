using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  StatBarUI.cs
//  전투 통계 팝업용 세그먼트 프로그레스 바.
//
//  구조 (Inspector):
//    StatBar (StatBarUI)
//      └── BarBg   (Image — 회색 배경, full-stretch)
//            ├── Seg0 (Image — 세그먼트 0)
//            ├── Seg1 (Image — 세그먼트 1)
//            └── Seg2 (Image — 세그먼트 2)
//
//  동작 원리:
//    각 Seg는 BarBg 내부에서 anchor를 사용해 비율대로 배치된다.
//    fillRatio: 이 유닛의 합산값 / 팀 최대값 → 실제 바 길이 비율.
//    세그먼트는 그 채워진 영역 안에서 비율대로 나뉜다.
// ============================================================

public class StatBarUI : MonoBehaviour
{
    [SerializeField] RectTransform _barBg;
    [SerializeField] Image[]       _segments;   // 최대 3개 (Seg0, Seg1, Seg2)

    // ── 공개 API ──────────────────────────────────────────────

    /// <summary>
    /// 세그먼트 비율과 채움 정도를 설정한다.
    /// </summary>
    /// <param name="values">각 세그먼트 절대값 (예: {장군딜, 병사딜, 스킬딜})</param>
    /// <param name="colors">세그먼트 색상</param>
    /// <param name="fillRatio">전체 바에서 채울 비율 (0~1). 이 유닛 합산/팀 최고값.</param>
    public void Setup(float[] values, Color[] colors, float fillRatio)
    {
        if (_segments == null || _barBg == null) return;

        float total = 0f;
        for (int i = 0; i < values.Length; i++) total += values[i];

        fillRatio = Mathf.Clamp01(fillRatio);

        float cursor = 0f;
        for (int i = 0; i < _segments.Length; i++)
        {
            if (_segments[i] == null) continue;

            bool hasValue = i < values.Length && values[i] > 0f;
            _segments[i].gameObject.SetActive(hasValue);
            if (!hasValue) continue;

            float segRatio = total > 0f ? (values[i] / total) * fillRatio : 0f;
            var rt = _segments[i].rectTransform;
            rt.anchorMin = new Vector2(cursor, 0f);
            rt.anchorMax = new Vector2(cursor + segRatio, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _segments[i].color = i < colors.Length ? colors[i] : Color.white;
            cursor += segRatio;
        }
    }

    public void Clear()
    {
        if (_segments == null) return;
        foreach (var seg in _segments)
            if (seg != null) seg.gameObject.SetActive(false);
    }

    // ── 색상 상수 ─────────────────────────────────────────────

    public static readonly Color[] DamageColors =
    {
        new Color(0.30f, 0.55f, 0.95f),  // 평타     — 파랑
        new Color(0.35f, 0.80f, 0.45f),  // 상태이상 — 초록 (2026-09-12, 옛 '병사' 칸 자리)
        new Color(0.95f, 0.55f, 0.20f),  // 스킬     — 주황
    };

    public static readonly Color[] TankColors =
    {
        new Color(0.90f, 0.25f, 0.25f),  // 받은 피해 — 빨강
        new Color(0.30f, 0.55f, 0.95f),  // 감소 피해 — 파랑
    };

    public static readonly Color[] HealColors =
    {
        new Color(0.35f, 0.85f, 0.55f),  // 가한 힐 — 초록
    };
}
