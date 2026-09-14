using UnityEngine;

// ============================================================
//  SkillCardData.cs
//  스킬 카드 한 장의 정의. 몬스터 카드와 **같은 칸**을 쓴다 (SummonKind.Skill).
//
//  ■ 스킬은 유닛을 남기지 않는다
//    몬스터 카드는 라인에 유닛을 세우고, 그 유닛이 살아남으면 마나를 환수한다.
//    스킬 카드는 라인에 효과만 터뜨린다 — 남는 게 없으니 환수도 없다.
//    "쓴 마나가 그대로 사라지는 대신 즉발" 이 스킬 카드의 값이다.
//
//  ■ 실행은 기존 액티브 스킬 시스템을 그대로 탄다
//    ActiveSkillId 를 들고 있다가, 시전 시점에 ActiveSkillDatabase 에서
//    SO 를 찾아 Execute(context) 를 부른다. 메테오·블리자드·독성 지대처럼
//    이미 있는 33종을 카드로 재활용할 수 있다는 뜻이다 —
//    스킬 카드를 늘리는 데 새 실행 코드가 필요하지 않다.
//
//    ⚠ 시전자는 소환사다
//      context.CasterEntity 에 소환사 엔티티가 들어간다. 그래서 스킬 위력이
//      소환사 공격력을 타는 스킬(EffectValue 가 배율인 것들)은 패기 스탯을 따라간다.
//      TargetPosition 은 플레이어가 탭한 라인 좌표다 — 조준은 손이 한다.
//
//  ■ 쿨다운이 없다
//    카드의 제약은 마나뿐이라는 규칙을 스킬도 그대로 따른다
//    (SummonCardUI 주석 참고). 대신 비용을 몬스터보다 무겁게 잡는다.
//
//  ■ 카드 레벨은 몬스터와 같은 규칙이다
//    중복 획득 → CardLevelRule 로 레벨이 오르고, 레벨당 위력이 오른다.
//    몬스터처럼 패시브를 개방하지는 않는다 — 스킬은 한 방이 전부라
//    숫자가 커지는 편이 읽기 쉽다.
// ============================================================

[CreateAssetMenu(fileName = "SkillCard_", menuName = "ProjectK/SkillCardData")]
public class SkillCardData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("내부 식별자. 카드 보유 상태가 이 값으로 저장되므로 바꾸지 않는다.")]
    public string Id;

    [Tooltip("표시 이름 (카드 · 보상 화면).")]
    public string DisplayName;

    [TextArea(2, 3)]
    [Tooltip("카드 설명 한두 줄. 무엇이 어디에 터지는지 적는다.")]
    public string Description;

    [Tooltip("카드 아이콘.")]
    public Sprite Icon;

    [Header("소환")]
    [Tooltip("한 번 쓰는 데 드는 마나. 환수되지 않으므로 몬스터보다 무겁게 잡는다.")]
    [Min(1f)]
    public float ManaCost = 12f;

    [Header("효과")]
    [Tooltip("발동할 액티브 스킬. ActiveSkillDatabase 에 등록돼 있어야 한다.")]
    public ActiveSkillId Skill = ActiveSkillId.None;

    [Tooltip("카드 레벨 1당 위력 배율 증가 (0.2 = 레벨당 +20%).\n" +
             "실제 적용은 SkillCardCaster 가 ActiveSkillContext 로 넘긴다.")]
    [Min(0f)]
    public float PowerPerLevel = 0.2f;

    /// <summary>카드 레벨에 따른 위력 배율. Lv1 은 1.0 이다.</summary>
    public float PowerMultiplier(int level)
        => 1f + Mathf.Max(0, level - 1) * PowerPerLevel;
}
