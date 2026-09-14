using UnityEngine;

// ============================================================
//  ActiveSummonSignature.cs  [SummonSignature]
//  소환사의 **대표 종족**을 마나 없이 불러낸다.
//
//  ■ 스킬 하나로 여섯 소환사를 덮는다
//    무엇을 부르는지는 스킬이 아니라 **소환사**가 들고 있다
//    (SummonerData.SignatureSummonSpecies · SignatureSummonCount).
//    종족마다 스킬을 따로 만들면 같은 코드가 여섯 벌 생기고, 소환사를
//    추가할 때마다 enum·SO·DB 를 또 늘려야 한다.
//
//  ■ 소환된 유닛은 **보통 몬스터와 똑같다** (사용자 확정, 2026-09-04)
//    MonsterSpawner.SpawnFree 를 지나므로 시너지·강화·품질·소환력이 전부
//    그대로 얹힌다. 그 경로를 우회해 직접 스폰하지 말 것 —
//    그러면 "시너지를 안 받는 몬스터" 가 생긴다.
//
//  ■ ⚠ 시너지 카운트는 주지 않는다
//    MarkSummoned 는 카드로 냈을 때만 부른다(SummonController). 스킬로 부른
//    개체가 카운트를 올리면 마나를 내지 않고 시너지를 켜는 길이 생긴다.
//    **받기만 하고 주지는 않는다** 가 규칙이다.
//
//  ■ ⚠ 라인 대기열로 돌아가지 않는다
//    SpawnFree 는 카드 정보 없이 내므로 returnable = false 다. 판이 끝나면
//    사라진다 — 돌아가면 무료 스킬이 매 판 물량을 불려 소환 경제가 무너진다.
//
//  ■ ⚠ 세대는 0 이다 — 카드 소환과 같다 (사용자 지적, 2026-09-07)
//    SpawnFree 의 기본값(1세대)은 '죽음에서 되살아난 개체' 를 위한 값이다.
//    그대로 쓰면 불러낸 슬라임이 죽어도 분열하지 않는다. 위의 "보통 몬스터와
//    똑같다" 는 약속이 사망 발동 패시브에서만 깨져 있었다.
//
//  ■ 어디에 세우나 — **플레이어가 탭한 바로 그 자리**다 (사용자 확정, 2026-09-06)
//    라인만 고르고 성벽 옆에서 내보내던 때가 있었다. 그러면 "지금 뚫리는
//    곳" 에 즉시 보낼 수가 없어, 걸어가는 동안 이미 성벽이 맞고 있었다.
//    이 스킬의 값어치는 **거리를 건너뛰는 것**이다.
//
//    ⚠ 이건 의도된 예외다 — 카드 소환은 여전히 성벽 옆에서만 나온다
//      횟수 제한(스테이지당 1~2회)이 이 특권의 값이다. 마나로 사는 카드에
//      같은 자유를 주면 라인 개념이 통째로 사라진다.
//
//    ⚠ 성벽보다 왼쪽에는 세우지 않는다
//      성 안(성벽 뒤)을 탭하면 몬스터가 벽 뒤에 갇혀 영영 싸우지 못한다.
//      취향이 아니라 그냥 못 쓰는 자리라서 막는다.
// ============================================================

[CreateAssetMenu(fileName = "Active_SummonSignature",
                 menuName = "BattleGame/Actives/SummonSignature")]
public class ActiveSummonSignature : ActiveSkillData
{
    public override void Execute(ActiveSkillContext ctx)
    {
        SummonerRuntimeBridge bridge = SummonerRuntimeBridge.Current;
        if (bridge == null) return;

        SummonerData summoner = bridge.Data;

        MonsterSpeciesData species = summoner.SignatureSummonSpecies;
        if (species == null)
        {
            Debug.LogError($"[ActiveSummonSignature] '{summoner.Id}' 에 " +
                           "SignatureSummonSpecies 가 비어 있습니다. " +
                           "Tools > Project K > 데이터 생성 > 소환사 를 다시 실행하세요.");
            return;
        }

        var field = SummonFieldLayout.Instance;
        if (field == null)
        {
            Debug.LogError("[ActiveSummonSignature] SummonFieldLayout 이 씬에 없습니다 — " +
                           "소환 위치를 알 수 없어 아무것도 나오지 않습니다.");
            return;
        }

        // 탭한 자리 그대로다. 성벽 뒤만 막는다 — 거기 세우면 벽에 갇힌다.
        Vector3 spawn = ctx.TargetPosition;
        spawn.x = Mathf.Max(spawn.x, field.WallBoundaryX);
        spawn.z = 0f;

        int count = Mathf.Max(1, summoner.SignatureSummonCount);

        // ⚠ 0세대다 — 카드로 낸 것과 똑같이 취급한다 (사용자 지적, 2026-09-07)
        //   SpawnFree 의 기본값은 1세대(= 죽음에서 되살아난 개체)라, 그대로 두면
        //   불러낸 슬라임이 죽어도 분열하지 않는다. 종족 패시브의 절반은 사망
        //   발동인데 그게 통째로 죽으면 "같은 몬스터인데 스킬로 부르면 다른 것"
        //   이 된다 — 이 스킬의 약속(위 '보통 몬스터와 똑같다')과 정면으로 어긋난다.
        //   무한 증식은 열리지 않는다: 여기서 나온 것이 낳는 것은 1세대다.
        for (int i = 0; i < count; i++)
            MonsterSpawner.SpawnFree(species, summoner, Scatter(spawn, i), generation: 0);

        if (!string.IsNullOrEmpty(species.SummonCircleEffectKey))
            SkillEffectHelper.Spawn(species.SummonCircleEffectKey, spawn, 1.2f);
    }

    /// <summary>
    /// 같은 라인에 여러 마리를 세울 때 살짝 흩뜨린다.
    ///
    /// ⚠ 같은 자리에 두 마리를 세우면 분리(Separation)가 폭발한다
    ///   완전히 겹친 두 유닛은 밀어낼 방향이 정해지지 않아 한 프레임에
    ///   튕겨 나간다 (SpeciesPassiveRuntime.ScatterAround 와 같은 이유).
    /// </summary>
    static Vector3 Scatter(Vector3 at, int index)
    {
        if (index == 0) return at;

        float angle = (index * 137.5f) * Mathf.Deg2Rad;   // 황금각 — 몇 마리든 고르게 퍼진다
        return at + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.6f;
    }
}
