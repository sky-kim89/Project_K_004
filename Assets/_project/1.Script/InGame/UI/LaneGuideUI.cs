using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  LaneGuideUI.cs
//  전장 위에 라인 다섯 칸의 경계를 그린다.
//
//  ■ 왜 필요한가
//    카드를 고른 뒤 전장을 탭하면 SummonFieldLayout.GetNearestLane 이
//    "가장 가까운 라인" 을 골라 준다. 그런데 화면에는 그 경계가 어디에도
//    그려져 있지 않았다. 벌판이 통짜 흙바닥이라 두 라인의 경계가 눈에
//    보이지 않고, 플레이어는 자기가 3번을 눌렀는지 4번을 눌렀는지
//    **몬스터가 나온 뒤에야** 알 수 있었다.
//
//  ■ 띠 사이를 띄운다 (사용자 요청, 2026-09-03)
//    선 하나로 나누면 흙 무늬에 묻힌다. 라인마다 띠를 깔고 띠와 띠 사이를
//    비워 두면, 비워진 틈이 곧 경계다 — 밝기가 아니라 **간격**으로 나뉘므로
//    배경이 무엇이든 읽힌다.
//
//  ■ 밝기를 번갈아 준다
//    이웃한 두 띠가 같은 색이면 틈이 좁아 보이는 자리에서 하나로 붙어
//    보인다. 한 칸씩 걸러 조금 진하게 두면 다섯 칸이 각각 세어진다.
//
//  ■ 좌표는 매 프레임 다시 잰다
//    카메라가 스크롤하고 라인 간격은 화면 비율을 탄다. SummonQueueUI 와
//    같은 방식이다 — 월드 좌표를 스크린 좌표로 바꿔 얹는다.
//
//  ■ 성벽 왼쪽은 그리지 않는다
//    성 안은 소환 대기열이 쓰는 자리다. 띠가 거기까지 뻗으면 대기열 뒤에
//    깔려 성벽이 어디인지가 흐려진다.
// ============================================================

public class LaneGuideUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("라인 좌표를 읽을 전장 레이아웃. 비우면 씬에서 찾는다.")]
    [SerializeField] SummonFieldLayout _field;

    [Tooltip("월드 → 스크린 변환에 쓸 카메라. 비우면 Camera.main 을 쓴다.")]
    [SerializeField] Camera _cam;

    [Tooltip("라인 수(5)만큼. 위에서 아래 순서다.")]
    [SerializeField] RectTransform[] _bands;

    [Tooltip("띠 색을 갈아 끼울 이미지. _bands 와 같은 순서·같은 개수다.")]
    [SerializeField] Image[] _bandImages;

    [Header("배치")]
    [Tooltip("띠와 띠 사이에 비워 둘 세로 간격(px). 이 틈이 곧 라인 경계다.")]
    [SerializeField] float _gap = 14f;

    [Tooltip("성벽 바깥면에서 오른쪽으로 띄울 거리(px). 띠는 여기서 시작한다.")]
    [SerializeField] float _wallInsetX = 4f;

    /// <summary>짝수 칸 색. 흙바닥 위라 어둡게 깐다.</summary>
    static readonly Color BandEven = new(0f, 0f, 0f, 0.17f);

    /// <summary>홀수 칸 색. 이웃과 붙어 보이지 않을 만큼만 다르다.</summary>
    static readonly Color BandOdd = new(0f, 0f, 0f, 0.07f);

    Canvas _canvas;

    /// <summary>라인별 화면 Y. 매 프레임 새로 재고, 이웃을 찾을 때 다시 읽는다.</summary>
    readonly float[] _laneY = new float[SummonFieldLayout.LaneCount];

    Camera Cam
    {
        get
        {
            // 씬 전환에서 앞 씬 카메라가 파괴되면 == null 이 true 가 된다.
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }
    }

    void Awake()
    {
        if (_field == null) _field = FindAnyObjectByType<SummonFieldLayout>();

        _canvas = GetComponentInParent<Canvas>().rootCanvas;

        for (int i = 0; i < _bandImages.Length; i++)
            _bandImages[i].color = (i % 2 == 0) ? BandEven : BandOdd;
    }

    void LateUpdate()
    {
        if (_field == null) return;

        Camera cam = Cam;
        if (cam == null) return;

        // 캔버스 단위 ≠ 화면 픽셀이다. 크기는 나눠서 넣고 위치는 픽셀 그대로 넣는다
        // (스크린 스페이스 캔버스에서 RectTransform.position 은 화면 픽셀이다).
        float scale = _canvas.scaleFactor;

        float leftPx  = cam.WorldToScreenPoint(new Vector3(_field.WallBoundaryX, 0f, 0f)).x
                      + _wallInsetX;
        float rightPx = Screen.width;
        float widthPx = rightPx - leftPx;
        float centerX = leftPx + widthPx * 0.5f;

        int count = Mathf.Min(_bands.Length, _field.LanePoints.Count);

        for (int i = 0; i < count; i++)
            _laneY[i] = cam.WorldToScreenPoint(_field.GetLanePosition(i)).y;

        for (int i = 0; i < count; i++)
        {
            float y = _laneY[i];

            // ⚠ LanePoints 의 **순서를 믿지 않는다**
            //   "위에서 아래로 5개" 는 인스펙터 규약일 뿐 강제되지 않는다.
            //   i−1 / i+1 을 이웃으로 잡았다가 한 칸이라도 뒤바뀌면 띠 높이가
            //   음수가 되어 라인 하나가 통째로 사라진다. 화면 Y 로 직접 찾는다.
            float above = float.MaxValue;   // 바로 위 라인 (Y 가 더 큰 것 중 가장 가까운)
            float below = float.MinValue;   // 바로 아래 라인

            for (int j = 0; j < count; j++)
            {
                if (j == i) continue;

                float o = _laneY[j];
                if (o > y && o < above) above = o;
                if (o < y && o > below) below = o;
            }

            // 끝 라인은 이웃이 한쪽뿐이다 — 반대쪽 간격을 그대로 되접어 쓴다.
            // 안 그러면 맨 위·맨 아래 칸만 반쪽으로 보여 라인 수가 잘못 세어진다.
            if (above > float.MaxValue * 0.5f) above = y + (y - below);
            if (below < float.MinValue * 0.5f) below = y - (above - y);

            float top    = (y + above) * 0.5f;
            float bottom = (y + below) * 0.5f;

            RectTransform band = _bands[i];
            band.position  = new Vector3(centerX, (top + bottom) * 0.5f, 0f);
            band.sizeDelta = new Vector2(widthPx / scale,
                                         Mathf.Max(0f, (top - bottom) - _gap) / scale);
        }
    }
}
