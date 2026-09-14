using UnityEditor;
using UnityEngine;

// ============================================================
//  DifficultyConfigCreator.cs  [Editor Only]
//  Tools > Project K > 데이터 생성 > 난이도
//
//  난이도 5단계 수치 테이블을 만든다. Assets/Resources/DifficultyConfig.asset
//
//  ■ 2026-09-11 상향 (사용자 지시) — 스탯·보상 둘 다 더 가파르게
//    ① 쉬움의 환생 포인트를 ×0.8 로 깎았다 — 원작보다 스테이지가 훨씬 쉽게
//       깨져서, 가장 낮은 난이도만 돌아도 포인트가 넉넉히 쌓였다
//    ② 보상 배율    1.0/1.3/1.7/2.25/3.0 → 0.8/1.5/2.3/3.4/5.0
//    ③ 광포        +0/70/180/360/700%  → +0/90/230/460/900%  (약 +25~30%)
//       물량은 어려움·지옥만 올렸다(0.25/0.55) — 불지옥은 이미 상한(0.8)이다
//       각성은 지옥만 0.45 로 — 불지옥은 상한(0.55)이다
//    ④ 장비 상자    런 끝에 1/2/3/4/5개 — 한 등급 오를 때마다 하나씩
//    ⚠ 쉬움 배율이 1 미만이 되면서 ReincarnationData.PreviewPoints 의 Max(1, 배율)
//      하한을 걷어냈다 — 남아 있으면 ①이 조용히 무효가 된다
//
//  ■ 수치 근거 — 보통부터 계단이 점점 커진다
//    광포(적 공·체)는 보통 이후 한 단계마다 대략 두 배씩 뛴다.
//      보통 +90% → 어려움 +230% → 지옥 +460% → 불지옥 +900%
//    스테이지 진행 배율(StageConfig)을 완만하게 낮춘 대신, 난이도 등급의
//    체감은 여기서 벌어야 한다 — 상향 폭이 스테이지가 아니라 선택에서 나온다.
//    앞 단계는 완만해서 난이도를 올려볼 마음이 들고, 뒷 단계는
//    한 칸 올릴 때마다 확실히 다른 게임이 된다.
//
//    상한이 있는 값은 이 곡선을 따르지 않는다 —
//    물량은 +80% 가 상한이다. 후반 웨이브가 이미 1,000마리라
//    그 이상은 프레임이 먼저 무너진다.
//    각성(우두머리 쿨감)은 -55% 가 한계선이다. 더 줄이면 보스 연출이
//    끝나기도 전에 다음 스킬이 나가 겹친다.
//
//  ■ 환생 포인트 배율이 유일한 보상이다
//    난이도를 올릴 이유가 없으면 아무도 안 올린다.
//    광포가 900% 까지 가는 만큼 보상도 ×5 까지 올렸다.
//    다만 보상은 점진적이다 — 증가폭이 +0.7 → +0.8 → +1.1 → +1.6.
//    쉬움을 깎은 만큼 첫 칸부터 올라갈 이유가 보인다.
//      쉬움 ×0.8 → 보통 ×1.5 → 어려움 ×2.3 → 지옥 ×3.4 → 불지옥 ×5.0
// ============================================================

public static class DifficultyConfigCreator
{
    const string Path = "Assets/Resources/DifficultyConfig.asset";

    [MenuItem(ProjectKMenu.Data + "난이도", priority = ProjectKMenu.DataPrio + 19)]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var cfg = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(Path);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<DifficultyConfig>();
            AssetDatabase.CreateAsset(cfg, Path);
        }

        cfg.Tiers = new[]
        {
            // 출정 — 기준선. 디버프 없음.
            new DifficultyConfig.TierEntry
            {
                Tier                    = DifficultyTier.Easy,
                ReincarnationMultiplier = 0.8f,   // 깎는다 — 원작보다 훨씬 쉽게 깨진다 (0.6 → 0.8, 사용자 지시)
                GearBoxes               = 1,
            },
            // 혈전 — 광포 하나만. 플레이어가 난이도 체감을 보정하는 기준점이라
            //        가장 단순한 디버프여야 한다. 계단의 출발점이므로 여기는 그대로 둔다.
            new DifficultyConfig.TierEntry
            {
                Tier                    = DifficultyTier.Normal,
                EnemyStatBonus          = 0.9f,   // +90%
                ReincarnationMultiplier = 1.5f,
                GearBoxes               = 2,
            },
            // 사지 — 물량 추가. 여기서부터 광역 스킬의 가치가 뛴다.
            new DifficultyConfig.TierEntry
            {
                Tier                    = DifficultyTier.Hard,
                EnemyStatBonus          = 2.3f,   // +230% (직전 대비 +140%p)
                EnemyCountBonus         = 0.25f,
                ReincarnationMultiplier = 2.3f,
                GearBoxes               = 3,
            },
            // 초열 — 각성 추가. 우두머리가 스킬을 두 배 가까이 쏟는다.
            new DifficultyConfig.TierEntry
            {
                Tier                    = DifficultyTier.Hell,
                EnemyStatBonus          = 4.6f,   // +460% (직전 대비 +230%p)
                EnemyCountBonus         = 0.55f,
                BossCooldownCut         = 0.45f,
                ReincarnationMultiplier = 3.4f,
                GearBoxes               = 4,
            },
            // 무간 — 폭주 추가. 엘리트가 돌진을 배우고 보스가 분쇄 강타를 쓴다.
            //        마지막 단계는 '더 큰 숫자' 가 아니라 '다른 게임' 이어야 한다.
            new DifficultyConfig.TierEntry
            {
                Tier                    = DifficultyTier.Inferno,
                EnemyStatBonus          = 9.0f,   // +900% (직전 대비 +440%p)
                EnemyCountBonus         = 0.8f,   // 상한 (1,000마리 프레임 한계)
                BossCooldownCut         = 0.55f,  // 상한 (보스 연출 길이 한계)
                FrenzyPatterns          = true,
                ReincarnationMultiplier = 5.0f,
                GearBoxes               = 5,
            },
        };

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 플레이 중에 다시 만들 수 있다 — 캐시를 버려야 새 수치가 먹는다.
        DifficultyConfig.Invalidate();

        Debug.Log($"[DifficultyConfigCreator] 난이도 {cfg.Tiers.Length}단계 생성 → {Path}");
        EditorGUIUtility.PingObject(cfg);
    }
}
