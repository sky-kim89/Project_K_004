using UnityEngine;

// ============================================================
//  ActiveSummonBrood.cs  [SummonBrood]
//  2차 업그레이드 몬스터가 **자기 권속**을 주기적으로 불러낸다.
//
//  ■ 무엇을 몇 마리 부르는지는 스킬이 아니라 **종족**이 들고 있다
//    MonsterSpeciesData.BroodSpecies / BroodCount. 시그니처 소환
//    (ActiveSummonSignature)이 소환사에게 묻는 것과 같은 구조다 —
//    종족마다 스킬을 따로 만들면 2차가 늘 때마다 enum·SO·DB 가 함께 는다.
//
//  ■ ⚠ 시전자의 **두 번째 슬롯**이다
//    2차는 희귀 스킬(GeneralActiveSkillComponent)과 이 소환을 함께 갖는다.
//    두 번째는 ActiveSkillSlot 버퍼에 꽂는다 — 보스 행동 패턴이 쓰는 그 버퍼고,
//    ActiveSkillAISystem 이 진영·계층과 무관하게 돌려 준다
//    (MonsterRuntimeBridge.BuildBroodSlot).
//
//  ■ ⚠ 소환된 권속은 보통 몬스터와 똑같다
//    MonsterSpawner.SpawnFree 를 지나므로 시너지·품질·소환력·덱 카드 레벨이
//    그대로 얹힌다. 우회해 직접 스폰하면 "시너지를 안 받는 몬스터" 가 생긴다.
//
//  ■ ⚠ 1세대로 낸다 — 시그니처 소환(0세대)과 **다르다**
//    이건 쿨다운마다 무한히 도는 스킬이다. 0세대로 내면 나온 권속이 죽을 때
//    또 분열하고(SpeciesPassiveRule.MaxReproduceGeneration = 0), 그 왕이 살아
//    있는 한 슬라임이 판을 덮는다. 시그니처는 스테이지당 1~2회라 그 위험이 없다.
//    ⚠ 이 한 줄이 권속 소환의 유일한 브레이크다. 0 으로 바꾸지 말 것.
//
//  ■ ⚠ 시너지 카운트는 주지 않는다 · 라인 대기열로 돌아가지 않는다
//    SpawnFree 가 둘 다 이미 지킨다 (MarkSummoned 없음 · returnable false).
//    마나를 내지 않고 나온 것이 줄에 쌓이면 소환 경제가 통째로 무너진다.
//
//  ■ 어디에 세우나 — **왕의 발밑**이다
//    시그니처 소환은 플레이어가 탭한 자리에 낸다(거리를 건너뛰는 것이 그 값이다).
//    이건 AI 가 알아서 도는 스킬이라 고를 사람이 없다 — 부른 자 옆에 선다.
// ============================================================

[CreateAssetMenu(fileName = "Active_SummonBrood",
                 menuName = "BattleGame/Actives/SummonBrood")]
public class ActiveSummonBrood : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        // 시전자는 언제나 몬스터다 — 종족이 무엇을 부를지 들고 있다.
        if (!ctx.CasterObject.TryGetComponent<MonsterRuntimeBridge>(out var bridge)) return;

        MonsterSpeciesData self = bridge.Species;
        if (self == null || self.BroodSpecies == null) return;

        // 시너지 왕권 — 권속 공·체(은부터) · 한 마리 더(금) (2026-09-15)
        SynergyTier royal = (self.Tags & MonsterTag.Royal) != 0
                          ? MonsterSynergyRule.TierOf(MonsterTag.Royal)
                          : SynergyTier.None;

        int   count    = Mathf.Max(1, self.BroodCount) + MonsterSynergyRule.RoyalExtraBrood(royal);
        float statMult = 1f + MonsterSynergyRule.RoyalBroodStatBonus(royal);

        // 소환사는 스탯 합성(MonsterStatComposer)이 쓴다 — 없으면 낼 수 없다.
        SummonerRuntimeBridge summonerBridge = SummonerRuntimeBridge.Current;
        if (summonerBridge == null) return;

        Vector3 at = ctx.CasterTransform.position;

        var field = SummonFieldLayout.Instance;
        if (field != null) at.x = Mathf.Max(at.x, field.WallBoundaryX);
        at.z = 0f;

        for (int i = 0; i < count; i++)
            MonsterSpawner.SpawnFree(self.BroodSpecies, summonerBridge.Data,
                                     Scatter(at, i), generation: 1, statMult: statMult);

        if (!string.IsNullOrEmpty(self.BroodSpecies.SummonCircleEffectKey))
            SkillEffectHelper.Spawn(self.BroodSpecies.SummonCircleEffectKey, at, 1.2f);
    }

    /// <summary>
    /// 여러 마리를 세울 때 살짝 흩뜨린다.
    ///
    /// ⚠ 같은 자리에 두 마리를 세우면 분리(Separation)가 폭발한다 —
    ///   완전히 겹친 둘은 밀어낼 방향이 없어 한 프레임에 튕겨 나간다.
    /// </summary>
    static Vector3 Scatter(Vector3 at, int index)
    {
        if (index == 0) return at;

        float angle = (index * 137.5f) * Mathf.Deg2Rad;   // 황금각 — 몇 마리든 고르게 퍼진다
        return at + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.6f;
    }
}
