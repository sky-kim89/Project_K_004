using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SummonQueueUI.cs
//  각 라인 위에 "그 라인에서 출전 대기 중인 유닛" 을 초상화 × 개수로 띄운다.
//
//  ■ 라인마다 따로 뜬다
//    어느 라인에 무엇이 대기 중인지가 판을 짜는 정보다.
//    한 목록으로 합치면 "슬라임 6" 만 보이고 그게 어느 라인인지 알 수 없다.
//
//      라인1  [고블린×2]
//      라인2
//      라인3  [슬라임×6] [트롤×1]
//
//  ■ 성벽 안에 그린다 (라인의 Y 만 따라간다)
//    예전에는 소환 지점(성문 앞)에 붙여 오른쪽 벌판으로 뻗었다. 대기열이
//    전장 위에 겹쳐 싸움을 가렸고, 특히 물량 종족을 걸면 화면 절반이 초상화로
//    덮였다. 대기열은 "성에서 나갈 순서" 지 전장에서 벌어지는 일이 아니다.
//
//    그래서 가로 위치는 성벽에 고정하고(오른쪽 끝 = 성벽 바깥면, 왼쪽으로 자란다)
//    세로 위치만 라인을 따라간다. 성벽 위에 줄이 늘어선 그림이라
//    "이 줄이 여기서 나간다" 가 그대로 읽힌다.
//
//  ■ 스크린 캔버스에 그린다
//    월드 캔버스를 쓰면 스케일·정렬을 따로 관리해야 하고 글자가 흐려진다.
//    월드 좌표를 매 프레임 스크린 좌표로 바꿔 얹는 편이 단순하다.
//    (카메라가 스크롤하므로 한 번 잡아 두면 어긋난다)
//
//  ■ 슬롯은 미리 만들어 두고 켜고 끈다
//    예약할 때마다 Instantiate 하면 연속 소환에서 GC 가 튄다.
// ============================================================

public class SummonQueueUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("라인 좌표를 읽을 전장 레이아웃. 비우면 씬에서 찾는다.")]
    [SerializeField] SummonFieldLayout _field;

    [Tooltip("월드 → 스크린 변환에 쓸 카메라. 비우면 Camera.main 을 쓴다.")]
    [SerializeField] Camera _cam;

    [Tooltip("라인별 표시 줄. 라인 수(5)만큼 넣는다.")]
    [SerializeField] LaneColumn[] _lanes;

    [Tooltip("초상화가 없는 종족에 쓸 대체 그림.")]
    [SerializeField] Sprite _fallbackIcon;

    [Header("배치")]
    [Tooltip("성벽 바깥면에서 성 안쪽(왼쪽)으로 들여놓을 거리(px).\n" +
             "0 이면 줄의 오른쪽 끝이 성벽 바깥면에 딱 닿는다.")]
    [SerializeField] float _wallInsetX = 6f;

    [Tooltip("라인 Y 에서 화면 위쪽으로 띄울 거리(px). 보통 0 이다.")]
    [SerializeField] float _screenOffsetY;

    SummonReservation _reservation;

    /// <summary>라인 한 줄 — 그 라인의 대기 목록이 가로로 늘어선다.</summary>
    [System.Serializable]
    public class LaneColumn
    {
        [Tooltip("이 라인 표시 전체의 루트. 대기가 없으면 통째로 끈다.")]
        public RectTransform Root;

        [Tooltip("한 종족을 그리는 칸. SummonReservation.DisplayLimit(6) 개.")]
        public QueueSlot[] Slots;
    }

    /// <summary>
    /// 대기열 한 칸 — 초상화 두 장을 겹쳐 쓴다.
    ///
    ///   Portrait  : 아래. 몬스터 스냅샷.
    ///   Progress  : 위. 같은 그림에 검은 반투명을 입히고 360° 로 채운다.
    ///
    /// 위쪽이 줄어들며 아래 초상화가 드러나는 식이라, 남은 소환 시간이
    /// 그 몬스터의 그림 위에서 바로 읽힌다.
    /// </summary>
    [System.Serializable]
    public class QueueSlot
    {
        public GameObject      Root;
        public Image           Portrait;
        public Image           Progress;
        public TextMeshProUGUI CountText;
    }

    Camera Cam
    {
        get
        {
            // 씬 전환에서 앞 씬 카메라가 파괴되면 == null 이 true 가 된다.
            if (_cam == null) _cam = Camera.main;
            return _cam;
        }
    }

    // ── 생명주기 ─────────────────────────────────────────────

    void Awake()
    {
        if (_field == null) _field = FindAnyObjectByType<SummonFieldLayout>();

        AnchorLanesToWall();
    }

    /// <summary>
    /// 대기열이 성벽에서 **왼쪽(성 안)으로** 자라게 한다.
    ///
    /// ⚠ 피벗이 오른쪽이어야 성 밖으로 넘치지 않는다
    ///   기준점은 성벽 바깥면 하나뿐이고, 줄 길이는 대기 종류 수에 따라 변한다.
    ///   피벗이 왼쪽이면 줄이 길어질수록 벌판 쪽으로 밀려 나가 결국 전장을 덮는다.
    ///   오른쪽으로 두면 길어지는 방향이 성 안쪽이라 늘어나도 전장을 가리지 않는다.
    /// </summary>
    void AnchorLanesToWall()
    {
        for (int i = 0; i < _lanes.Length; i++)
            _lanes[i].Root.pivot = new Vector2(1f, 0.5f);
    }

    void OnEnable()
    {
        TryBind();
        Refresh();
    }

    void OnDisable() => Unbind();

    void LateUpdate()
    {
        // 컨트롤러가 늦게 서면 OnEnable 에서 못 잡는다 — 잡힐 때까지 본다.
        if (_reservation == null) TryBind();

        FollowLanes();
        TickProgress();
    }

    /// <summary>
    /// 각 라인의 맨 앞 칸에 남은 소환 시간을 360° 로 그린다.
    ///
    /// 맨 앞 칸만 채운다 — 지금 뽑고 있는 건 그 한 종족이다.
    /// 뒤 칸까지 돌리면 전부 동시에 나오는 것처럼 보인다.
    /// </summary>
    void TickProgress()
    {
        var controller = SummonController.Instance;
        if (controller == null) return;

        for (int lane = 0; lane < _lanes.Length; lane++)
        {
            LaneColumn column = _lanes[lane];
            if (!column.Root.gameObject.activeSelf) continue;

            float ratio = controller.GetLaneCooldownRatio(lane);

            for (int i = 0; i < column.Slots.Length; i++)
            {
                QueueSlot slot = column.Slots[i];
                if (!slot.Root.activeSelf) continue;

                slot.Progress.fillAmount = i == 0 ? ratio : 0f;
            }
        }
    }

    void TryBind()
    {
        var controller = SummonController.Instance;
        if (controller == null) return;
        if (_reservation == controller.Reservation) return;

        Unbind();
        _reservation = controller.Reservation;
        _reservation.Changed += Refresh;
        Refresh();
    }

    void Unbind()
    {
        if (_reservation == null) return;

        _reservation.Changed -= Refresh;
        _reservation = null;
    }

    // ── 위치 추적 ────────────────────────────────────────────

    /// <summary>
    /// 각 라인 표시를 성벽 안, 그 라인의 높이에 얹는다.
    ///
    /// X 는 라인마다 같다 — 성벽 바깥면 한 곳이다. 라인이 정하는 것은 Y 뿐이다.
    ///
    /// ⚠ 세로로 띄우지 않는다
    ///   라인 간격이 약 135px 인데 예전처럼 70px 을 띄우면 표시가 이웃 라인
    ///   가까이로 올라가 어느 줄의 대기열인지 헷갈린다. 성벽 위에서는 줄이
    ///   라인 높이에 정확히 걸려 있어야 옆으로 눈을 옮겨 짝을 지을 수 있다.
    /// </summary>
    void FollowLanes()
    {
        if (_field == null) return;

        Camera cam = Cam;
        if (cam == null) return;

        int count = Mathf.Min(_lanes.Length, _field.LanePoints.Count);

        for (int i = 0; i < count; i++)
        {
            RectTransform root = _lanes[i].Root;
            if (!root.gameObject.activeSelf) continue;

            // 가로는 성벽 바깥면, 세로는 그 라인 — 둘을 한 점으로 합친다.
            Vector3 anchor = _field.GetLanePosition(i);
            anchor.x = _field.WallBoundaryX;

            Vector3 screen = cam.WorldToScreenPoint(anchor);
            screen.x -= _wallInsetX;
            screen.y += _screenOffsetY;
            root.position = screen;
        }
    }

    // ── 표시 ─────────────────────────────────────────────────

    public void Refresh()
    {
        if (_reservation == null)
        {
            HideAll();
            return;
        }

        for (int lane = 0; lane < _lanes.Length; lane++)
        {
            LaneColumn column = _lanes[lane];
            List<SummonQueueGroup> groups = _reservation.BuildGroups(lane);

            // 대기가 없으면 라인 표시를 통째로 끈다 — 빈 칸이 줄줄이 남으면
            // 어디에 무엇이 걸렸는지가 오히려 안 읽힌다.
            column.Root.gameObject.SetActive(groups.Count > 0);
            if (groups.Count == 0) continue;

            for (int i = 0; i < column.Slots.Length; i++)
            {
                QueueSlot slot = column.Slots[i];

                if (i >= groups.Count)
                {
                    slot.Root.SetActive(false);
                    continue;
                }

                SummonQueueGroup g = groups[i];

                slot.Root.SetActive(true);

                Sprite made = MonsterPortraitProvider.Get(g.Species);
                Sprite icon = made != null ? made : _fallbackIcon;
                slot.Portrait.sprite  = icon;
                slot.Portrait.enabled = icon != null;

                // 진행 표시도 같은 그림을 쓴다 — 색만 검은 반투명이다.
                slot.Progress.sprite  = icon;
                slot.Progress.enabled = icon != null;

                // "×" 는 붙이지 않는다 — 초상화가 이미 "무엇" 을 말하고 있다.
                slot.CountText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                slot.CountText.text = g.Count.ToString();
            }
        }
    }

    void HideAll()
    {
        foreach (LaneColumn column in _lanes)
            column.Root.gameObject.SetActive(false);
    }
}
