using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  ChoicePopup.cs
//  "목록에서 하나 고르기" — 런 흐름의 갈림길·시설이 **전부 이 하나**를 쓴다.
//
//    갈림길 2택 · 특성 3택(보스) · 특성 1택(엘리트)
//    야영지(회복/최대치) · 강화소(카드 → 강화 종류) · 제단(제물 고르기)
//
//  ■ 왜 한 팝업으로 모으나
//    전부 "줄 몇 개 중에 하나를 누른다" 다. 화면마다 팝업을 따로 만들면
//    프리팹·Creator·등록이 여섯 벌로 늘고, 줄 간격 하나를 고치려면
//    여섯 곳을 고쳐야 한다. 카드 3택(CardSelectPopup)만 예외다 —
//    그쪽은 그림이 주인공이라 생김새가 근본적으로 다르다.
//
//  ■ 줄 수는 런타임이 정한다
//    프리팹은 언제나 MaxRows 칸을 굽고, 남는 칸은 숨긴다.
//    ⚠ 그래서 "엘리트는 1택, 나중에 업그레이드로 2택" 같은 변경이
//      프리팹을 다시 굽지 않고 숫자 하나로 끝난다.
//
//  ■ 닫을 수 있는가는 부르는 쪽이 정한다
//    보상(특성)은 못 닫는다 — 고르지 않고 닫으면 보상이 조용히 사라진다.
//    시설(강화소·제단)은 닫을 수 있다 — 살 것이 없으면 물러날 수 있어야 한다.
// ============================================================

public class ChoicePopup : PopupBase
{
    /// <summary>프리팹이 갖고 있는 줄 수. 덱 최대 칸(8)을 담을 수 있어야 한다.</summary>
    public const int MaxRows = 8;

    [Serializable]
    public class RowView
    {
        public GameObject Root;
        public Button     Button;
        public Image      Icon;
        public TMP_Text   NameText;
        public TMP_Text   DescText;
    }

    /// <summary>줄 하나에 담기는 것. 부르는 쪽이 만들어 넘긴다.</summary>
    public readonly struct Entry
    {
        public readonly string Name;
        public readonly string Desc;

        /// <summary>false 면 눌리지 않는다 (골드 부족 등). 흐리게 그린다.</summary>
        public readonly bool Enabled;

        /// <summary>
        /// 줄 왼쪽 그림. 없으면 칸이 통째로 꺼지고 글자가 왼쪽으로 당겨진다.
        ///
        /// ■ 왜 넣었나 (사용자 지적, 2026-09-07)
        ///   특성 3택이 글자 목록이라 셋 중 하나를 고르려면 세 줄을 다 읽어야 했다.
        ///   그림이 있으면 아는 특성은 읽지 않고 지나갈 수 있다.
        ///
        /// ⚠ 부르는 쪽이 준다 — 이 팝업은 무엇을 고르는 자리인지 모른다
        ///   특성은 SpriteManager(perk_*), 카드 줄은 몬스터 초상화를 넘긴다.
        /// </summary>
        public readonly Sprite Icon;

        public Entry(string name, string desc, bool enabled = true, Sprite icon = null)
        {
            Name    = name;
            Desc    = desc;
            Enabled = enabled;
            Icon    = icon;
        }
    }

    [Header("연결")]
    [SerializeField] TMP_Text  _titleText;
    [SerializeField] RowView[] _rows;

    [Tooltip("카드 한 장 + 간격. ⚠ 값의 정본은 Creator(RunPopupCreator)다.")]
    [SerializeField] float _cardStep = 292f;

    /// <summary>못 누르는 줄의 글자색 — 눌리는 줄과 확실히 갈려야 한다.</summary>
    static readonly Color Dimmed = new(0.45f, 0.45f, 0.52f, 1f);

    Action<int> _onPicked;
    bool        _blockClose;
    Color       _nameColor;
    Color       _descColor;
    bool        _colorsCaptured;

    public override bool BlockBackgroundClose => _blockClose;

    /// <param name="title">머리글. 무엇을 고르는 자리인지 한 줄로.</param>
    /// <param name="entries">줄 목록. <see cref="MaxRows"/> 를 넘으면 앞에서 잘린다.</param>
    /// <param name="onPicked">고른 줄의 <b>인덱스</b>를 넘긴다. 취소되면 안 불린다.</param>
    /// <param name="blockClose">
    /// true 면 고르기 전에는 못 닫는다. 보상 화면은 true, 시설은 false 다.
    /// </param>
    public ChoicePopup Setup(string title, IReadOnlyList<ChoicePopup.Entry> entries,
                             Action<int> onPicked, bool blockClose)
    {
        CaptureColors();

        _onPicked   = onPicked;
        _blockClose = blockClose;

        if (_titleText != null) _titleText.text = title;

        int shown = entries != null ? Mathf.Min(entries.Count, _rows.Length) : 0;

        for (int i = 0; i < _rows.Length; i++)
        {
            RowView row = _rows[i];

            if (i >= shown)
            {
                row.Root.SetActive(false);
                continue;
            }

            row.Root.SetActive(true);
            Bind(row, entries[i], i);
        }

        Recenter(shown);

        return this;
    }

    /// <summary>
    /// 띄운 카드 수에 맞춰 가로 자리를 다시 잡는다.
    ///
    /// ⚠ Creator 는 <b>여덟 칸</b>을 굽는데 실제로 뜨는 것은 1·3·최대 5 장이다.
    ///   구울 때의 고정 자리를 그대로 쓰면 남는 칸이 오른쪽에 빈 채로 남아
    ///   실제 카드가 통째로 왼쪽으로 쏠려 보인다.
    ///   자리는 "몇 장을 띄우는가" 가 정해야 한다 (CardSelectPopup.Recenter 와 같은 규칙).
    ///
    /// ⚠ 세로는 건드리지 않는다 — 헤더 높이가 걸려 있어 Creator 가 잡은 값이 맞다.
    /// </summary>
    void Recenter(int shown)
    {
        float start = -(shown - 1) * 0.5f * _cardStep;

        for (int i = 0; i < shown && i < _rows.Length; i++)
        {
            var rt = (RectTransform)_rows[i].Root.transform;
            rt.anchoredPosition = new Vector2(start + i * _cardStep, rt.anchoredPosition.y);
        }
    }

    /// <summary>
    /// 프리팹이 들고 있던 원래 색을 한 번만 기억해 둔다.
    ///
    /// ⚠ 이게 없으면 흐리게 그린 줄이 다음 번에도 흐린 채로 나온다
    ///   팝업은 풀에서 재사용되므로 지난번에 덮어쓴 색이 그대로 남는다.
    /// </summary>
    void CaptureColors()
    {
        if (_colorsCaptured || _rows == null || _rows.Length == 0) return;

        RowView first = _rows[0];
        if (first?.NameText == null) return;

        _nameColor      = first.NameText.color;
        _descColor      = first.DescText != null ? first.DescText.color : Color.white;
        _colorsCaptured = true;
    }

    void Bind(RowView row, in Entry entry, int index)
    {
        // ⚠ 그림이 없으면 칸을 끈다 — 켜 둔 채로 두면 흰 사각형이 남는다.
        //   글자 자리는 프리팹이 이미 그림 폭만큼 비워 두고 있어 그대로 둔다
        //   (줄마다 글자 시작점이 달라지면 목록이 들쭉날쭉해 보인다).
        if (row.Icon != null)
        {
            row.Icon.sprite  = entry.Icon;
            row.Icon.enabled = entry.Icon != null;

            // 못 누르는 줄은 그림도 함께 죽인다 — 글자만 흐리면 어긋나 보인다.
            row.Icon.color = entry.Enabled ? Color.white : Dimmed;
        }

        if (row.NameText != null)
        {
            row.NameText.text  = entry.Name;
            row.NameText.color = entry.Enabled ? _nameColor : Dimmed;
        }

        if (row.DescText != null)
        {
            row.DescText.text  = entry.Desc;
            row.DescText.color = entry.Enabled ? _descColor : Dimmed;
        }

        if (row.Button == null) return;

        row.Button.onClick.RemoveAllListeners();
        row.Button.interactable = entry.Enabled;

        if (entry.Enabled) row.Button.onClick.AddListener(() => Choose(index));
    }

    void Choose(int index)
    {
        // ⚠ 콜백을 비운 뒤에 부른다
        //   닫히는 애니메이션 동안 한 번 더 눌릴 수 있다. 두 번 불리면
        //   골드가 두 번 빠지거나 특성이 두 장 들어온다.
        Action<int> cb = _onPicked;
        _onPicked = null;
        if (cb == null) return;

        Close();
        cb(index);
    }
}
