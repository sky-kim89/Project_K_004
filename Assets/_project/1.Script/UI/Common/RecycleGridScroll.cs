using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  RecycleGridScroll.cs
//  보이는 만큼만 칸을 만들고 재사용하는 격자 스크롤 (무한 스크롤).
//
//  ■ 왜 필요한가
//    도감 장수 탭은 400칸이다. 전부 Instantiate 하면 GameObject 400개 ×
//    (Image + Image + Image + TMP) = 1600개가 한 번에 생긴다.
//    탭을 누를 때마다 그걸 다 만들고 다 파괴하니 프레임이 끊긴다.
//    화면에 들어오는 건 어차피 20~30칸뿐이라 그만큼만 만들어 돌려 쓴다.
//
//  ■ 쓰는 법
//      _grid.Bind(entries.Count, (index, cell) => Paint(cell, entries[index]));
//    Bind 는 목록이 바뀔 때만 부르면 된다. 스크롤 중 재배치는 알아서 한다.
//
//  ⚠ 셀은 재사용된다 — 바인더는 모든 상태를 매번 덮어써야 한다
//    "보유일 때만 색을 칠하는" 식으로 짜면, 그 칸이 미보유 항목으로
//    재사용될 때 이전 색이 그대로 남는다. 버튼 리스너도 매번 지우고 새로 단다.
//
//  ⚠ GridLayoutGroup / ContentSizeFitter 를 같이 쓰지 말 것
//    위치와 Content 높이를 이 스크립트가 직접 계산한다. 레이아웃 그룹이 붙어
//    있으면 매 프레임 서로 값을 덮어써 칸이 떨리거나 겹친다.
// ============================================================

public class RecycleGridScroll : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] ScrollRect    _scroll;
    [SerializeField] RectTransform _viewport;
    [SerializeField] RectTransform _content;
    [SerializeField] GameObject    _cellTemplate;   // 비활성 템플릿

    [Header("격자")]
    [SerializeField] Vector2 _cellSize = new(176f, 186f);
    [SerializeField] Vector2 _spacing  = new(16f, 16f);
    [SerializeField] float   _padLeft   = 18f;
    [SerializeField] float   _padRight  = 18f;
    [SerializeField] float   _padTop    = 18f;
    [SerializeField] float   _padBottom = 18f;

    Action<int, GameObject> _bind;
    int _count;
    int _columns  = 1;
    int _rowCount;
    int _firstRow = -1;          // 마지막으로 배치한 첫 줄 (-1 = 아직 없음)
    float _viewportH;

    readonly List<GameObject> _cells = new();

    float RowStride => _cellSize.y + _spacing.y;
    float ColStride => _cellSize.x + _spacing.x;

    void Awake()
    {
        if (_cellTemplate != null) _cellTemplate.SetActive(false);
    }

    void OnEnable()
    {
        if (_scroll != null) _scroll.onValueChanged.AddListener(OnScrolled);
    }

    void OnDisable()
    {
        if (_scroll != null) _scroll.onValueChanged.RemoveListener(OnScrolled);
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>목록을 갈아 끼운다. binder 는 (항목 인덱스, 재사용된 셀) 을 받는다.</summary>
    public void Bind(int count, Action<int, GameObject> binder)
    {
        _bind  = binder;
        _count = Mathf.Max(0, count);

        Rebuild();
    }

    /// <summary>맨 위로 되돌린다. 탭을 바꿀 때 부른다.</summary>
    public void ScrollToTop()
    {
        if (_content == null) return;

        _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, 0f);
        _firstRow = -1;      // 강제로 다시 배치시킨다
        Reposition();
    }

    // ── 내부 ─────────────────────────────────────────────────

    void Rebuild()
    {
        if (_content == null || _viewport == null || _cellTemplate == null) return;

        // 한 줄에 몇 칸이 들어가나 — 뷰포트 폭에서 여백을 뺀 값으로 계산한다.
        // (창 크기가 바뀌어도 Bind 를 다시 부르면 열 수가 따라간다)
        float usable = _viewport.rect.width - _padLeft - _padRight;
        _columns  = Mathf.Max(1, Mathf.FloorToInt((usable + _spacing.x) / ColStride));
        _rowCount = Mathf.CeilToInt(_count / (float)_columns);

        // Content 높이 = 전체 줄 높이. 스크롤 막대의 길이가 여기서 나온다.
        float height = _padTop + _padBottom
                     + _rowCount * _cellSize.y
                     + Mathf.Max(0, _rowCount - 1) * _spacing.y;

        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot     = new Vector2(0.5f, 1f);
        _content.sizeDelta = new Vector2(0f, height);

        // 필요한 셀 수 = 화면에 걸치는 줄 + 위아래 여유 1줄씩
        _viewportH = _viewport.rect.height;
        int rowsOnScreen = Mathf.CeilToInt(_viewportH / RowStride) + 2;
        int need         = Mathf.Min(_count, rowsOnScreen * _columns);

        EnsureCells(need);

        _firstRow = -1;
        Reposition();
    }

    void EnsureCells(int need)
    {
        // 모자라면 만든다. 남으면 끄기만 하고 들고 있는다 —
        // 탭을 오갈 때마다 파괴·생성하면 재사용의 의미가 없다.
        while (_cells.Count < need)
        {
            var go = Instantiate(_cellTemplate, _content);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = _cellSize;
            _cells.Add(go);
        }

        for (int i = need; i < _cells.Count; i++)
            if (_cells[i] != null) _cells[i].SetActive(false);
    }

    void OnScrolled(Vector2 _) => Reposition();

    void Reposition()
    {
        if (_bind == null || _cells.Count == 0) return;

        // Content 는 위로 밀려 올라가므로 anchoredPosition.y 가 곧 스크롤 양이다
        float scrolled = Mathf.Max(0f, _content.anchoredPosition.y);
        int   firstRow = Mathf.FloorToInt((scrolled - _padTop) / RowStride);

        int maxFirstRow = Mathf.Max(0, _rowCount - Mathf.CeilToInt(_viewportH / RowStride) - 1);
        firstRow = Mathf.Clamp(firstRow, 0, maxFirstRow);

        // 줄이 안 바뀌었으면 다시 그릴 이유가 없다 (스크롤 중 매 프레임 호출된다)
        if (firstRow == _firstRow) return;
        _firstRow = firstRow;

        int start = firstRow * _columns;

        for (int k = 0; k < _cells.Count; k++)
        {
            var go    = _cells[k];
            int index = start + k;

            if (index >= _count)
            {
                if (go.activeSelf) go.SetActive(false);
                continue;
            }

            int row = index / _columns;
            int col = index % _columns;

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(
                 _padLeft + col * ColStride,
                -(_padTop + row * RowStride));

            if (!go.activeSelf) go.SetActive(true);
            _bind(index, go);
        }
    }
}
