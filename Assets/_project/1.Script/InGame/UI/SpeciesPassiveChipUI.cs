using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
//  SpeciesPassiveChipUI.cs
//  종족 패시브 칩 하나의 "눌러서 설명 보기".
//  카드 3택과 융합 창이 같이 쓴다. SynergyChipUI 와 같은 물건이다.
//
//  ■ 그림만으로는 효과를 알 수 없다
//    패시브 이름을 아이콘으로 바꾸면서 화면에서 글자가 사라졌다. 그림이
//    한눈에 갈리게는 해도 "자폭이 얼마나 아픈가" 까지는 못 한다.
//    그 설명이 닿을 곳이 필요하다.
//
//  ■ 눌러도 뜨고 올려도 뜬다
//    마우스는 올리면 바로 보이는 편이 빠르고, 터치는 올릴 수가 없어 눌러야
//    한다. 둘 다 받는다. 고정을 두는 이유는 툴팁을 읽는 동안 마우스가 칩을
//    벗어나기 때문이다 — 고정이 없으면 읽으려고 다가가는 순간 사라진다.
//
//  ■ ⚠ 고정은 SynergyChipUI 와 따로 논다
//    툴팁 판때기(TooltipLayer)는 한 장이라 화면에는 하나만 뜬다. 하지만
//    "무엇이 고정됐나" 는 각자 들고 있어서, 시너지 칩을 고정한 채 패시브
//    칩에 올렸다 나가면 시너지 쪽으로 되돌아가지 않고 그냥 닫힌다.
//    두 줄이 세로로 떨어져 있어 오가는 일이 드물기에 그대로 둔다 —
//    합치려면 고정 주인을 TooltipLayer 로 올려야 하고, 그건 이 한 가지
//    증상을 위해 층을 하나 더 만드는 일이다.
//
//  ■ 글은 SpeciesPassiveNames 가 만든다
//    수치와 설명이 갈리지 않게 한 곳에서만 뽑는다 (Describe 참고).
// ============================================================

public class SpeciesPassiveChipUI : MonoBehaviour,
                                    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    /// <summary>고정된 칩. 툴팁이 한 장뿐이므로 고정도 하나뿐이다.</summary>
    static SpeciesPassiveChipUI _pinned;

    SpeciesPassive _passive;

    /// <summary>이 칸이 어느 패시브를 가리키는지 정한다. 칸은 카드마다 다른 패시브가 온다.</summary>
    public void Setup(SpeciesPassive passive)
    {
        _passive = passive;

        // 칸의 주인이 바뀌었는데 이전 설명이 떠 있으면 엉뚱한 글을 읽게 된다.
        if (_pinned == this) Unpin();
    }

    void OnDisable()
    {
        if (_pinned == this) Unpin();
    }

    // ⚠ 올리면 **언제나** 그 칩의 내용으로 덮는다
    //   "고정된 칩이 있으면 아무것도 안 한다" 로 두면, 고정한 뒤 다른 곳을
    //   눌러 InfoTooltipUI 가 스스로 닫혔을 때 _pinned 만 남아 어느 칩에
    //   올려도 반응이 없는 막다른 상태가 된다 (SynergyChipUI 와 같은 이유).
    public void OnPointerEnter(PointerEventData _) => Open();

    public void OnPointerExit(PointerEventData _)
    {
        if (_pinned == this) return;              // 고정된 칩에서 나가도 남는다

        if (_pinned != null) _pinned.Open();
        else                 TooltipLayer.Close();
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (_pinned == this) { Unpin(); return; }

        _pinned = this;
        Open();
    }

    void Unpin()
    {
        _pinned = null;
        TooltipLayer.Close();
    }

    void Open()
    {
        if (_passive == SpeciesPassive.None) return;

        TooltipLayer.Open((RectTransform)transform,
                          _passive.ToKorean(),
                          _passive.Describe(),
                          string.Empty);
    }
}
