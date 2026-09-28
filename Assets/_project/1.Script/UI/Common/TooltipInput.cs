using System;
using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
//  TooltipInput.cs
//  툴팁을 "올려서" 여는가 "눌러서" 여는가 — 한 곳이 정한다.
//
//  ■ 규칙 (사용자 지시, 2026-09-17)
//    PC     → 마우스를 올리면 뜨고 벗어나면 닫힌다. 눌러도 열리지 않는다.
//    모바일 → 누르면 뜬다. 다른 곳을 누르면 닫힌다 (InfoTooltipUI.Update).
//    예전엔 둘 다 받아서, PC 에서 아이콘을 누르면 올려서 뜬 툴팁이
//    클릭으로 닫혔다가 다시 열리는 등 어느 쪽인지 헷갈렸다.
//
//  ⚠ 판정을 화면마다 적지 말 것 — HoverMode 하나를 본다.
//    Device Simulator 는 Application.isMobilePlatform 을 흉내 내므로 에디터에서도 둘 다 시험할 수 있다.
// ============================================================

public static class TooltipInput
{
    public static bool HoverMode => !Application.isMobilePlatform;

    /// <summary>
    /// 버튼처럼 클릭 리스너로만 툴팁을 열던 칸에 "올리면 열기" 를 붙인다.
    /// 클릭 쪽은 부르는 쪽이 <c>if (!TooltipInput.HoverMode)</c> 로 막는다.
    /// </summary>
    public static void HookHover(GameObject go, Action open, Action close)
    {
        if (!go.TryGetComponent(out TooltipHoverRelay relay))
            relay = go.AddComponent<TooltipHoverRelay>();

        relay.Enter = open;
        relay.Exit  = close;
    }
}

/// <summary>올림·벗어남을 콜백으로 넘긴다. PC(HoverMode)에서만 반응한다.</summary>
public class TooltipHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action Enter;
    public Action Exit;

    public void OnPointerEnter(PointerEventData _)
    {
        if (TooltipInput.HoverMode) Enter?.Invoke();
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (TooltipInput.HoverMode) Exit?.Invoke();
    }
}
