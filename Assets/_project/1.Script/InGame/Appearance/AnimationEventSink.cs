using UnityEngine;

// ============================================================
//  AnimationEventSink.cs
//  PixelHeroes 애니메이션 클립이 부르는 이벤트를 받아 주는 자리.
//
//  ■ 왜 필요한가
//    에셋의 클립 세 개가 AnimationEvent 를 들고 있다.
//        SitDown  → SetBool("Crouched")
//        StandUp  → UnsetBool("Crouched")
//        Land     → SetState(1)
//    원래 수신자는 에셋의 예제 스크립트(CharacterAnimation.cs)인데
//    이 프로젝트는 그것을 쓰지 않고 UnitAnimationSync 로 직접 몬다.
//    그래서 해당 클립이 재생되는 순간
//        "AnimationEvent 'SetBool' on animation 'SitDown' has no receiver!"
//    가 유닛 수만큼 쏟아진다.
//
//  ⚠ UnitAnimationSync 에 메서드를 추가하는 것으로는 해결되지 않는다
//    Unity 는 애니메이션 이벤트를 **Animator 가 붙은 GameObject** 의
//    컴포넌트에서만 찾는다. Animator 는 자식 "Character" 에 있고
//    UnitAnimationSync 는 루트에 있어서 서로 다른 오브젝트다.
//    그래서 이 스크립트는 반드시 Animator 와 같은 오브젝트에 붙어야 한다
//    (UnitAnimationSync.Awake 가 자동으로 붙인다).
//
//  ⚠ 클립에서 이벤트를 지우는 쪽은 택하지 않았다
//    컨트롤러에 Crouched 파라미터가 실제로 있고, 앉은 자세를 유지하는 래치로
//    쓰인다. 이벤트를 지우면 그 래치가 영영 꺼진 채로 남는다.
//    또 에셋을 업데이트하면 원상복구되어 같은 에러가 조용히 돌아온다.
// ============================================================

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class AnimationEventSink : MonoBehaviour
{
    Animator _animator;

    void Awake() => _animator = GetComponent<Animator>();

    /// <summary>SitDown 클립이 부른다 — 앉은 상태 래치를 켠다.</summary>
    public void SetBool(string paramName) => _animator.SetBool(paramName, true);

    /// <summary>StandUp 클립이 부른다 — 앉은 상태 래치를 끈다.</summary>
    public void UnsetBool(string paramName) => _animator.SetBool(paramName, false);

    /// <summary>
    /// Land 클립이 부른다.
    ///
    /// 에셋 예제는 여기서 자기 상태 기계(CharacterState)를 갈아 끼웠다.
    /// 이 프로젝트의 상태 정본은 ECS(UnitStateComponent)이고 UnitAnimationSync 가
    /// 그것만 반영하므로, 클립이 상태를 되돌리면 오히려 어긋난다.
    /// 받아만 두고 아무것도 하지 않는 것이 맞다 — 에러만 사라진다.
    /// </summary>
    public void SetState(int state) { }
}
