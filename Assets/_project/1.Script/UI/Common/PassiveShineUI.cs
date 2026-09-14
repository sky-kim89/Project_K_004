using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  PassiveShineUI.cs
//  **각성 패시브** 칸의 반짝임 — 금테가 숨 쉬고, 빛줄기가 칸을 훑고 지나간다.
//
//  ■ 왜 필요한가 (사용자 지시, 2026-09-10)
//    각성은 "패시브 둘이 겹쳐 더 센 하나가 됐다" 는 사건이다. 같은 모양의 줄에
//    이름만 바뀌어 있으면 무엇이 일어났는지 아무도 모른다. 칸이 스스로 빛나야
//    눈이 먼저 간다 — 그래서 목록에서도 **맨 위**다 (MonsterDetailPopup).
//
//  ■ 가볍게 — Update 에서 위치·알파만 만진다. 머티리얼·셰이더 없음
//    ⚠ unscaledTime — 몬스터 상세는 일시정지 중에도 열린다(전황).
//  ⚠ 빛줄기는 부모의 RectMask2D 가 잘라 준다 (Creator 가 붙인다)
// ============================================================

public class PassiveShineUI : MonoBehaviour
{
    [SerializeField] RectTransform _sweep;    // 비스듬한 빛줄기
    [SerializeField] Image[]       _edges;    // 금테 네 변

    /// <summary>한 번 훑고 다음 번까지의 주기(초)와, 그중 실제로 지나가는 시간.</summary>
    const float Period    = 2.4f;
    const float SweepTime = 0.75f;

    RectTransform _rt;

    void Awake() => _rt = (RectTransform)transform;

    void Update()
    {
        float now = Time.unscaledTime;
        float w   = _rt.rect.width;

        // 빛줄기 — 왼쪽 밖에서 오른쪽 밖으로. 쉬는 동안은 칸 밖에 둔다.
        float t = now % Period;
        float x = t < SweepTime
                ? Mathf.Lerp(-w * 0.65f, w * 0.65f, t / SweepTime)
                : w * 2f;
        _sweep.anchoredPosition = new Vector2(x, 0f);

        // 금테 — 숨 쉬듯. 0.45 ~ 0.95
        float a = 0.70f + 0.25f * Mathf.Sin(now * 3.2f);
        for (int i = 0; i < _edges.Length; i++)
        {
            Color c = _edges[i].color;
            _edges[i].color = new Color(c.r, c.g, c.b, a);
        }
    }
}
