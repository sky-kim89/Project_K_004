using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
//  SynergyChipUI.cs
//  시너지 칩 하나의 "눌러서 설명 보기". 상단 줄과 카드 3택이 같이 쓴다.
//
//  ■ 그림만으로는 효과를 알 수 없다
//    시너지 이름을 아이콘으로 바꾸면서 화면에서 글자가 사라졌다. 그림이
//    이름을 대신할 수는 있어도 "동/은/금이 각각 무엇을 주는가" 까지는 못 한다.
//    그 설명이 닿을 곳이 필요하다.
//
//  ■ 눌러도 뜨고 올려도 뜬다 — 둘 다 **같은 동작**이다
//      올림 → 뜬다 · 누름 → 뜬다 · 내리면 닫힌다.
//
//    ⚠ **고정(pin)은 없앴다** (사용자 지적, 2026-09-07)
//      한때 누르면 고정돼 마우스를 치워도 남았다. 그러면 다른 칩에 올렸다가
//      내렸을 때 고정된 쪽으로 되돌아와, 화면에 툴팁이 남아 있는 상태가
//      생긴다. 어느 칩도 안 가리키는데 설명만 떠 있으면 무엇의 설명인지
//      알 수 없다.
//      지금 규칙은 하나다 — **가리키는 동안만 뜬다.**
//      누르는 것은 터치를 위한 문일 뿐, 올리는 것과 결과가 같다.
//
//  ■ 글은 MonsterSynergyRule 이 만든다
//    수치와 설명이 갈리지 않게 한 곳에서만 뽑는다 (DescribeAll 참고).
//
//  ■ 툴팁은 화면 전체가 한 장을 돌려 쓴다 (TooltipLayer)
//    칩마다 하나씩 달았더니 카드 면·"얻는 것" 구역 밑으로 깔렸고, 둘이
//    동시에 뜨기도 했다. 최상단 캔버스의 한 장을 쓰면 둘 다 사라진다.
// ============================================================

public class SynergyChipUI : MonoBehaviour,
                             IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    MonsterTag _tag;
    bool       _stack;   // 중첩 칩 — 시너지 하나가 아니라 "켜진 개수" 를 가리킨다

    /// <summary>이 칸이 어느 시너지를 가리키는지 정한다. 칸은 매 판 다른 시너지가 온다.</summary>
    public void Setup(MonsterTag tag) { _tag = tag; _stack = false; }

    /// <summary>중첩 칩으로 쓴다 — 툴팁이 중첩 세 단계를 띄운다 (SynergyBarUI 맨 위 칸).</summary>
    public void SetupStack() { _tag = MonsterTag.None; _stack = true; }

    /// <summary>
    /// 칸이 꺼지면 설명도 닫는다.
    ///
    /// ⚠ 이게 없으면 시너지 줄을 다시 그릴 때 툴팁만 남는다 — 가리키던 칸이
    ///   사라졌는데 설명은 그대로다.
    /// </summary>
    void OnDisable() => TooltipLayer.Close();

    public void OnPointerEnter(PointerEventData _) => Open();

    /// <summary>
    /// 벗어나면 닫는다. **되돌아갈 곳은 없다** — 고정을 없앴다.
    ///
    /// ⚠ 다른 칩으로 옮겨 갈 때도 이 함수가 먼저 돈다
    ///   그쪽 OnPointerEnter 가 곧바로 다시 열므로 깜빡이지 않는다.
    ///   순서가 뒤집혀 있어도(EventSystem 은 Exit 를 먼저 보낸다) 결과는 같다.
    /// </summary>
    public void OnPointerExit(PointerEventData _) => TooltipLayer.Close();

    /// <summary>터치를 위한 문. 올리는 것과 결과가 같다 — 고정하지 않는다.</summary>
    public void OnPointerClick(PointerEventData _) => Open();

    void Open()
    {
        if (_stack)
        {
            TooltipLayer.Open((RectTransform)transform,
                              MonsterSynergyRule.StackTitle(),
                              MonsterSynergyRule.DescribeStack(),
                              string.Empty);
            return;
        }

        if (_tag == MonsterTag.None) return;

        // ⚠ 마지막 칸(stat)은 비운다 — "현재: 은" 표시는 없앴다 (사용자 확정)
        //   단계 색과 아이콘 옆 숫자가 이미 그 말을 하고 있어서 한 줄이 남았다.
        TooltipLayer.Open((RectTransform)transform,
                          MonsterSynergyRule.TitleOf(_tag),
                          MonsterSynergyRule.DescribeAll(_tag),
                          string.Empty);
    }
}
