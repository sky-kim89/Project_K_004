using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  TutorialBlocker.cs
//  화면 전체를 덮되 구멍 안쪽만 입력을 통과시키는 투명 판.
//  TutorialOverlay 가 런타임에 붙인다.
//
//  ⚠ Image 를 네 장 둘러 구멍을 만들지 않는다
//    사각형 네 장으로 감싸면 모서리에서 1픽셀씩 겹치거나 비어
//    "분명 눌렀는데 안 눌리는" 자리가 생긴다.
//    판정은 한 장이 통째로 맡고 구멍만 예외 처리하는 편이 정확하다.
//
//  ⚠ 투명해도 raycastTarget 은 켜 둔다
//    alpha 0 이어도 Graphic 이 있으면 레이캐스트를 받는다. 이 판이
//    입력 차단의 전부이므로 색을 이유로 끄면 튜토리얼 중에 아무 버튼이나 눌린다.
// ============================================================

public class TutorialBlocker : Image
{
    Rect _hole;
    bool _hasHole;

    /// <summary>입력을 통과시킬 구멍 (오버레이 로컬 좌표). null 이면 전부 막는다.</summary>
    public void SetHole(Rect? hole)
    {
        _hasHole = hole.HasValue;
        if (_hasHole) _hole = hole.Value;
    }

    public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!_hasHole) return true;   // 구멍이 없으면 전부 내가 먹는다

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPoint, eventCamera, out var local);

        // 구멍 안이면 "내 것이 아니다" → 그 아래 버튼이 그대로 받는다
        return !_hole.Contains(local);
    }
}
