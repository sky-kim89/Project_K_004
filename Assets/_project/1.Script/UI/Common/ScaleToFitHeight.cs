using UnityEngine;

// ============================================================
//  ScaleToFitHeight.cs
//  고정 크기 콘텐츠를 부모 칸 높이에 맞게 통째로 축소한다.
//
//  ■ 왜 필요한가 — 화면이 넓어지면 캔버스 '세로' 가 줄어든다
//    팝업 캔버스는 1920×1080 기준 + matchWidthOrHeight = 0.5 다.
//    이 설정에서 스케일은 가로·세로 배율의 기하평균이라, 16:9 보다 넓은 화면은
//    스케일이 1 을 넘고 그만큼 **캔버스 세로 단위가 1080 아래로 내려간다.**
//
//      1920×1080 → 캔버스 1920×1080  (body 866)
//      2160×1080 → 캔버스 2036×1018  (body 804)   ← 862 짜리 카드가 58 넘침
//      2560×1080 → 캔버스 2217× 935  (body 721)   ← 141 넘침
//
//    가로가 넓어졌는데 세로가 모자라는 게 직관에 어긋나서 놓치기 쉽다.
//    폭만 앵커로 잡아 두면 "가로는 대응되는데 세로만 잘리는" 증상이 된다.
//
//  ■ 왜 레이아웃을 다시 짜지 않고 스케일로 해결하나
//    카드(MainPanelCreator.BuildHeroCard)는 초상화·스탯·스킬 칸 높이가
//    서로 물려 있는 정본이다. 화면비마다 높이를 다시 배분하면 그 규칙이
//    화면마다 갈라진다. 통째로 줄이면 비율이 그대로 유지된다.
//
//  ■ 키우지는 않는다 (상한 1)
//    862 는 디자인 크기다. 세로가 남는 화면(4:3 등)에서 늘리면 글자만 커지고
//    카드가 칸을 벗어난다.
// ============================================================

[ExecuteAlways]
[DisallowMultipleComponent]
public class ScaleToFitHeight : MonoBehaviour
{
    [Tooltip("줄일 대상 — 고정 크기 콘텐츠. 비워 두면 첫 번째 자식을 쓴다.")]
    [SerializeField] RectTransform _content;

    [Tooltip("콘텐츠의 설계 높이. 0 이면 런타임에 _content 의 높이를 읽는다.\n" +
             "⚠ 가급적 값을 넣을 것 — 스케일이 걸린 뒤의 rect 를 다시 읽으면 계속 줄어든다.")]
    [SerializeField] float _designHeight;

    RectTransform _self;

    void OnEnable()
    {
        _self = (RectTransform)transform;
        Apply();
    }

    // 부모 칸 크기가 바뀔 때(해상도 변경·캔버스 스케일 갱신) 유니티가 불러 준다.
    void OnRectTransformDimensionsChange() => Apply();

    void Apply()
    {
        if (_self == null) _self = transform as RectTransform;
        if (_self == null) return;

        var content = _content != null
            ? _content
            : (transform.childCount > 0 ? transform.GetChild(0) as RectTransform : null);
        if (content == null) return;

        // ⚠ 설계 높이를 rect 에서 읽으면 안 된다 (값이 비어 있을 때만 폴백)
        //   이미 스케일이 걸린 상태에서 rect.height 를 다시 읽고 또 나누면
        //   프레임마다 조금씩 더 줄어든다. sizeDelta 는 스케일과 무관하다.
        float design = _designHeight > 0f ? _designHeight : content.rect.height;
        if (design <= 0f) return;

        float avail = _self.rect.height;
        if (avail <= 0f) return;

        float s = Mathf.Min(1f, avail / design);
        var target = new Vector3(s, s, 1f);

        // 매 프레임 대입하면 더티 플래그가 계속 서서 레이아웃이 다시 돈다
        if ((content.localScale - target).sqrMagnitude > 0.0000001f)
            content.localScale = target;
    }
}
