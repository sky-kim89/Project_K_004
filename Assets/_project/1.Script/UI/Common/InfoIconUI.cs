using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
//  InfoIconUI.cs
//  아이콘 한 칸의 "올리거나 누르면 설명" — 무엇을 가리키든 글만 넘기면 된다.
//
//  ■ SpeciesPassiveChipUI 와 무엇이 다른가
//    그쪽은 종족 패시브 전용이라 enum 을 받는다. 몬스터 상세의 아이콘 모드는
//    종족 패시브 · 레벨 패시브 · (장비 패시브) 가 한 줄에 섞이므로 글을 직접 받는다.
//
//  ■ 고정(pin)은 없다 — 올리면 뜨고 벗어나면 닫힌다 (CLAUDE.md 툴팁 규칙)
//    누르는 것은 터치용 문일 뿐 결과가 같다.
//
//  ⚠ 툴팁 판때기는 TooltipLayer 한 장이다 — 칸마다 두지 않는다
//    칸 자식으로 두면 팝업 면 밑에 깔리고 둘이 동시에 뜬다.
// ============================================================

public class InfoIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    /// <summary>지금 판때기를 쥔 칸. 남의 칸이 연 툴팁을 닫지 않으려고 둔다.</summary>
    static InfoIconUI _shown;

    string _title, _desc, _stat;

    public void Setup(string title, string desc, string stat = "")
    {
        _title = title;
        _desc  = desc;
        _stat  = stat ?? "";

        // 칸의 주인이 바뀌었는데 옛 설명이 떠 있으면 엉뚱한 글을 읽게 된다.
        if (_shown == this) Hide();
    }

    // PC 는 올려서, 모바일은 눌러서 연다 (TooltipInput)
    public void OnPointerEnter(PointerEventData _) { if (TooltipInput.HoverMode) Show(); }
    public void OnPointerExit(PointerEventData _)  { if (TooltipInput.HoverMode && _shown == this) Hide(); }
    public void OnPointerClick(PointerEventData _) { if (!TooltipInput.HoverMode) Show(); }

    void OnDisable() { if (_shown == this) Hide(); }

    void Show()
    {
        if (string.IsNullOrEmpty(_title)) return;

        _shown = this;
        TooltipLayer.Open((RectTransform)transform, _title, _desc, _stat);
    }

    static void Hide()
    {
        _shown = null;
        TooltipLayer.Close();
    }
}
