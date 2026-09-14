using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  EffectPrefabGenerator.cs  —  1부: FX #01~#11 구현 / #12~#22 스텁
// ============================================================

public static class EffectPrefabGenerator
{
    const string kSavePath = "Assets/_project/2.Prefabs/Effect";
    const string kMatPath  = "Assets/_project/4.Materials/FX";

    static readonly Dictionary<string, string[]> kEffectMaterials = new()
    {
        { "FX_Slash_Impact",     new[] { "MAT_FX_Slash_Add",    "MAT_FX_Spark_Add",     "MAT_FX_Soft_Add"      } },
        { "FX_Leap_Land",        new[] { "MAT_FX_Ring_Add",     "MAT_FX_Smoke_Alpha",   "MAT_FX_Soft_Add"      } },
        { "FX_Dust_Dash",        new[] { "MAT_FX_Smoke_Alpha"                                                   } },
        { "FX_Shockwave",        new[] { "MAT_FX_Spark_Add",    "MAT_FX_Lightning_Add", "MAT_FX_Ring_Add"      } },
        { "FX_Meteor_Warning",   new[] { "MAT_FX_Flame_Add",    "MAT_FX_Ring_Add"                              } },
        { "FX_Meteor_Explosion", new[] { "MAT_FX_Flame_Add",    "MAT_FX_Smoke_Alpha",   "MAT_FX_Ring_Add",    "MAT_FX_Shard_Add"   } },
        { "FX_Arrow_Volley",     new[] { "MAT_FX_Arrow_Add",    "MAT_FX_Spark_Add"                             } },
        { "FX_Arrow_Rain_Zone",  new[] { "MAT_FX_Arrow_Add",    "MAT_FX_Star_Add",      "MAT_FX_Soft_Add"      } },
        { "FX_Charge_Impact",    new[] { "MAT_FX_Star_Add",     "MAT_FX_Ring_Add",      "MAT_FX_Spark_Add"     } },
        { "FX_Explosion",        new[] { "MAT_FX_Flame_Add",    "MAT_FX_Shard_Add",     "MAT_FX_Smoke_Alpha",  "MAT_FX_Ring_Add"   } },
        { "FX_Summon_Circle",    new[] { "MAT_FX_Rune_Add",     "MAT_FX_Wisp_Add",      "MAT_FX_Soft_Add"      } },
        { "FX_Sacrifice",        new[] { "MAT_FX_Wisp_Add",     "MAT_FX_Soft_Add",      "MAT_FX_Smoke_Alpha"   } },
        { "FX_Absorb",           new[] { "MAT_FX_Soft_Add",     "MAT_FX_Ring_Add",      "MAT_FX_Spark_Add"     } },
        { "FX_Battle_Cry",       new[] { "MAT_FX_Star_Add",     "MAT_FX_Ring_Add",      "MAT_FX_Spark_Add"     } },
        { "FX_Berserk",          new[] { "MAT_FX_Flame_Add",    "MAT_FX_Line_Add",      "MAT_FX_Lightning_Add" } },
        { "FX_Shield_Up",        new[] { "MAT_FX_Diamond_Add",  "MAT_FX_Crystal_Add",   "MAT_FX_Soft_Add"      } },
        { "FX_Speed_Up",         new[] { "MAT_FX_Spark_Add",    "MAT_FX_Wisp_Add",      "MAT_FX_Ring_Add"      } },
        { "FX_Heal_Aura",        new[] { "MAT_FX_Petal_Add",    "MAT_FX_Soft_Add",      "MAT_FX_Ring_Add"      } },
        { "FX_Heal_Target",      new[] { "MAT_FX_Cross_Add",    "MAT_FX_Wisp_Add",      "MAT_FX_Soft_Add"      } },
        { "FX_Bind",             new[] { "MAT_FX_Spiral_Add",   "MAT_FX_Ring_Add",      "MAT_FX_Smoke_Alpha"   } },
        { "FX_Poison_Zone",      new[] { "MAT_FX_Smoke_Alpha",  "MAT_FX_Poison_Alpha",  "MAT_FX_Ring_Add"      } },
        { "FX_Blizzard",             new[] { "MAT_FX_Snowflake_Add","MAT_FX_Streak_Add",    "MAT_FX_Vortex_Add",  "MAT_FX_Smoke_Alpha" } },
        // 메테오 본체 — 폭발(FX_Meteor_Explosion)과는 다른 물건이다. 이건 떨어지는 돌덩이.
        { "FX_Meteor_Rock",          new[] { "MAT_FX_Shard_Add",   "MAT_FX_Flame_Add",     "MAT_FX_Smoke_Alpha", "MAT_FX_Spark_Add"   } },
        // 버프 범위 — 흰색으로 만들고 색은 EffectTint 가 런타임에 입힌다
        { "FX_Buff_Range",           new[] { "MAT_FX_Ring_Add",    "MAT_FX_Wisp_Add",      "MAT_FX_Soft_Add"      } },
        // 파티클 없이 LineRenderer 하나뿐 — 머티리얼은 BuildRedLightningChain 이 직접 넣는다
        // 순교 — 병사가 쓰러진 자리의 폭발
        { "FX_Martyr_Explosion",     new[] { "MAT_FX_Flame_Add",   "MAT_FX_Shard_Add",     "MAT_FX_Ring_Add",    "MAT_FX_Wisp_Add"    } },
    };

    [MenuItem(ProjectKMenu.Fx + "Effect 프리팹 (26종)", priority = ProjectKMenu.PrefabPrio + 51)]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "_project/2.Prefabs/Effect"));
        AssetDatabase.Refresh();

        int n = 0;
        n += Save("FX_Slash_Impact",    BuildSlashImpact());
        n += Save("FX_Leap_Land",       BuildLeapLand());
        n += Save("FX_Dust_Dash",       BuildDustDash());
        n += Save("FX_Shockwave",       BuildShockwave());
        n += Save("FX_Meteor_Warning",  BuildMeteorWarning());
        n += Save("FX_Meteor_Explosion",BuildMeteorExplosion());
        n += Save("FX_Arrow_Volley",    BuildArrowVolley());
        n += Save("FX_Arrow_Rain_Zone", BuildArrowRainZone());
        n += Save("FX_Charge_Impact",   BuildChargeImpact());
        n += Save("FX_Explosion",       BuildExplosion());
        n += Save("FX_Summon_Circle",   BuildSummonCircle());
        n += Save("FX_Sacrifice",       BuildSacrifice());
        n += Save("FX_Absorb",          BuildAbsorb());
        n += Save("FX_Battle_Cry",      BuildBattleCry());
        n += Save("FX_Berserk",         BuildBerserk());
        n += Save("FX_Shield_Up",       BuildShieldUp());
        n += Save("FX_Speed_Up",        BuildSpeedUp());
        n += Save("FX_Heal_Aura",       BuildHealAura());
        n += Save("FX_Heal_Target",     BuildHealTarget());
        n += Save("FX_Bind",            BuildBind());
        n += Save("FX_Poison_Zone",     BuildPoisonZone());
        n += Save("FX_Blizzard",           BuildBlizzard());
        n += Save("FX_RedLightning_Chain", BuildRedLightningChain());
        n += Save("FX_Martyr_Explosion",   BuildMartyrExplosion());
        n += Save("FX_Meteor_Rock",        BuildMeteorRock());
        n += Save("FX_Buff_Range",         BuildBuffRange());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EffectPrefabGenerator] ✓ {n} effect prefabs generated → {kSavePath}");
    }

    // ── 헬퍼 ────────────────────────────────────────────────

    static int Save(string key, GameObject go)
    {
        go.name = key;
        if (kEffectMaterials.TryGetValue(key, out var mats)) ApplyMaterials(go, mats);
        PrefabUtility.SaveAsPrefabAsset(go, $"{kSavePath}/{key}.prefab");
        Object.DestroyImmediate(go);
        return 1;
    }

    static void ApplyMaterials(GameObject root, string[] mats)
    {
        var rs = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < Mathf.Min(rs.Length, mats.Length); i++)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{kMatPath}/{mats[i]}.mat");
            if (mat != null) rs[i].material = mat;
            else Debug.LogWarning($"[EffectPrefabGenerator] 머티리얼 없음: {mats[i]}");
        }
    }

    static GameObject NewGO() => new GameObject("FX");

    // ── 정렬 순서 ────────────────────────────────────────────
    //  ⚠ 이 프로젝트의 Sorting Layer 는 "Default" 하나뿐이다.
    //    예전 코드가 넣던 sortingLayerName = "Effect" 는 존재하지 않는 레이어라
    //    Unity 가 조용히 무시했고, 결국 모든 이펙트가 Default 레이어의 order 5 로 남았다.
    //    유닛 스프라이트는 order 100 / 105 를 쓰므로 이펙트가 전부 캐릭터 뒤에 깔렸다.
    //    → 같은 레이어 안에서 order 로만 앞뒤가 갈리므로 유닛보다 확실히 큰 값을 준다.
    //
    //  유닛은 SortingGroup 으로 묶여 order 100 하나만 쓴다(UnitSortingSetup).
    //  200 이면 항상 그 위에 뜬다.
    //
    //  ⚠ 이 값을 바꾸면 RareSkillEffectGenerator.kSortOrder 와
    //    LightningChainRenderer.SortingOrder 도 같이 바꿔야 한다 (같은 대역이다).
    const int EffectSortingOrder = 200;

    static ParticleSystem AddPS(GameObject go)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var rd = go.GetComponent<ParticleSystemRenderer>();
        rd.renderMode = ParticleSystemRenderMode.Billboard;
        rd.sortingOrder = EffectSortingOrder;
        var def = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (def != null) rd.material = def;
        return ps;
    }

    static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);

    static AnimationCurve AC3(float v0, float t1, float v1, float v2) =>
        new AnimationCurve(new Keyframe(0f, v0), new Keyframe(t1, v1), new Keyframe(1f, v2));

    static Gradient MakeGrad(
        (float t, Color c)[] cols, (float t, float a)[] alps)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[cols.Length];
        var ak = new GradientAlphaKey[alps.Length];
        for (int i = 0; i < cols.Length; i++) ck[i] = new GradientColorKey(cols[i].c, cols[i].t);
        for (int i = 0; i < alps.Length; i++) ak[i] = new GradientAlphaKey(alps[i].a, alps[i].t);
        g.SetKeys(ck, ak);
        return g;
    }

    // ── #01  FX_Slash_Impact ─────────────────────────────────
    // Root:Slash_Add  c1:Spark_Add  c2:Soft_Add
    static GameObject BuildSlashImpact()
    {
        var go = NewGO();

        // Root — 칼날 파편 (주황→적)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(6f, 18f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,210), C(255,100,15));
            m.startRotation  = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 28;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 38f; sh.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.35f, new Color(1f,0.55f,0f)), (1f, new Color(1f,0.08f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.85f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.2f, 1.4f, 0f));
        }

        // c1 — 금속 스파크 (흰→금)
        var c1 = new GameObject("Sparks"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(8f, 24f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255), C(255,220,70));
            m.gravityModifier = 0.5f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 42;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 30) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55f; sh.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, new Color(1f,0.8f,0.1f)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // c2 — 백색 코어 플래시 (Soft_Add)
        var c2 = new GameObject("Flash"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.08f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255), C(255,240,170));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 3;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, Color.white) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ── #02  FX_Leap_Land ────────────────────────────────────
    // Root:Ring_Add  c1:Smoke_Alpha  c2:Soft_Add
    static GameObject BuildLeapLand()
    {
        var go = NewGO();

        // Root — 방사 링 (청백)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(7f, 20f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.28f, 0.75f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(220,240,255), C(55,155,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 50;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 38) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, new Color(0.3f,0.7f,1f)), (1f, new Color(0.1f,0.35f,1f)) },
                new[] { (0f, 1f), (0.5f, 0.7f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.25f, 1.35f, 0f));
        }

        // c1 — 지면 먼지 (Smoke_Alpha)
        var c1 = new GameObject("Dust"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.3f, 0.85f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,175,130), C(245,220,165));
            m.gravityModifier = 0.2f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 22;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.8f,0.7f,0.5f)), (1f, new Color(0.5f,0.44f,0.32f)) },
                new[] { (0f, 0.7f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0f, 0.3f, 1.5f, 0.3f));
        }

        // c2 — 착지 섬광 (Soft_Add)
        var c2 = new GameObject("Flash"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.1f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 1f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255), C(200,230,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 3;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, Color.white) },
                new[] { (0f, 0.9f), (1f, 0f) }));
        }

        return go;
    }

    // ── #03  FX_Dust_Dash ────────────────────────────────────
    // Root:Smoke_Alpha
    static GameObject BuildDustDash()
    {
        var go = NewGO();
        var ps = AddPS(go);
        var m = ps.main;
        m.duration = 0.4f; m.loop = false;
        m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 7f);
        m.startSize      = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
        m.startColor     = new ParticleSystem.MinMaxGradient(C(215,195,148), C(255,235,182));
        m.gravityModifier = 0.25f;
        m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 65f; sh.radius = 0.25f;
        var col = ps.colorOverLifetime; col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
            new[] { (0f, new Color(0.9f,0.85f,0.68f)), (1f, new Color(0.62f,0.58f,0.42f)) },
            new[] { (0f, 0.75f), (1f, 0f) }));
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.3f, 1.2f, 0.2f));
        return go;
    }

    // ── #04  FX_Shockwave ────────────────────────────────────
    // Root:Spark_Add  c1:Lightning_Add  c2:Ring_Add
    // 모든 emitter 가 Circle arc(부채꼴) 형태 → ActiveShockwave 가 GO rotation 으로 방향 제어.
    // 프리팹 기준 arc = 120°, 반경 = 3 (SkillEffectHelper scale 로 조정).
    static GameObject BuildShockwave()
    {
        var go = NewGO();

        // Root — 전방 부채꼴 에너지 입자 (Spark_Add) — Circle arc 120°
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.7f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.25f, 0.60f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 16f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.2f, 0.65f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(220,250,255), C(28,175,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 80;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            // Circle arc: XY 평면에서 전방 120° 부채꼴로 방출
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.2f; sh.radiusThickness = 1f;
            sh.arc = 120f; sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.2f,0.7f,1f)), (1f, new Color(0f,0.28f,1f)) },
                new[] { (0f, 1f), (0.4f, 0.7f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.5f, 1.15f, 0.05f));
        }

        // c1 — 번개 파편 (Lightning_Add) — 좁은 90° 집중
        var c1 = new GameObject("Lightning"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(10f, 28f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255), C(140,225,255));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.04f, 22) });
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.3f; sh.radiusThickness = 1f;
            sh.arc = 90f; sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, new Color(0.4f,0.8f,1f)), (1f, new Color(0.1f,0.3f,1f)) },
                new[] { (0f, 1f), (0.4f, 0.6f), (1f, 0f) }));
        }

        // c2 — 퍼져나가는 충격파 (Ring_Add) — 넓은 140° 부채꼴 파형
        var c2 = new GameObject("Ring"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.65f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(7f, 18f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,240,255), C(75,195,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.03f, 48) });
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.35f; sh.radiusThickness = 1f;
            sh.arc = 140f; sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, new Color(0.3f,0.7f,1f)), (1f, new Color(0f,0.2f,0.8f)) },
                new[] { (0f, 0.9f), (0.5f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.5f, 1.4f, 0.1f));
        }

        return go;
    }

    // ── #05  FX_Meteor_Warning ───────────────────────────────
    // Root:Flame_Add(loop)  c1:Ring_Add(loop)
    static GameObject BuildMeteorWarning()
    {
        var go = NewGO();

        // Root — 불꽃 원형 경고 (loop)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.2f, 1.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,200,28), C(255,45,8));
            m.gravityModifier = -0.1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 80;
            var em = ps.emission; em.rateOverTime = 35f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.4f; sh.radiusThickness = 0.06f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.85f,0.18f)), (0.5f, new Color(1f,0.22f,0f)), (1f, new Color(0.4f,0f,0f)) },
                new[] { (0f, 1f), (0.5f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0f, 0.4f, 1.3f, 0.2f));
        }

        // c1 — 맥박 링 (Ring_Add, loop)
        var c1 = new GameObject("PulseRing"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 1f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(3f, 9f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,170,0), C(255,70,0));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40), new ParticleSystem.Burst(0.5f, 40) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.4f,0f)), (1f, new Color(1f,0f,0f)) },
                new[] { (0f, 0.8f), (0.5f, 0.4f), (1f, 0f) }));
        }

        return go;
    }

    // ── #06  FX_Meteor_Explosion ─────────────────────────────
    // Root:Flame_Add  c1:Smoke_Alpha  c2:Ring_Add  c3:Shard_Add
    static GameObject BuildMeteorExplosion()
    {
        var go = NewGO();

        // Root — 거대 화염 (Flame_Add)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(6f, 22f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,240,95), C(255,65,8));
            m.gravityModifier = -0.3f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 70;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 55) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.3f, new Color(1f,0.5f,0f)), (1f, new Color(0.22f,0.04f,0f)) },
                new[] { (0f, 1f), (0.35f, 0.85f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.2f, 1.55f, 0.1f));
        }

        // c1 — 검은 연기 (Smoke_Alpha)
        var c1 = new GameObject("Smoke"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 7f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1f, 2.8f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(55,38,28), C(115,85,55));
            m.gravityModifier = -0.15f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 25;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 18) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.28f,0.19f,0.13f)), (1f, new Color(0.13f,0.09f,0.06f)) },
                new[] { (0f, 0.8f), (0.4f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.4f, 1.5f, 0.4f));
        }

        // c2 — 바닥 충격 링 (Ring_Add)
        var c2 = new GameObject("ShockRing"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(12f, 32f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.28f, 0.8f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,220,95), C(255,90,0));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 52) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.2f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.5f,0f)), (1f, new Color(0.5f,0.08f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.6f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.3f, 1.45f, 0f));
        }

        // c3 — 불꽃 파편 (Shard_Add)
        var c3 = new GameObject("Debris"); c3.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c3);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(6f, 20f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.42f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,175,55), C(175,55,8));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.gravityModifier = 1.1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 35;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.68f,0.2f)), (1f, new Color(0.45f,0.08f,0f)) },
                new[] { (0f, 1f), (0.5f, 0.7f), (1f, 0f) }));
        }

        return go;
    }

    // ── #07  FX_Arrow_Volley ─────────────────────────────────
    // Root:Arrow_Add  c1:Spark_Add
    static GameObject BuildArrowVolley()
    {
        var go = NewGO();

        // Root — 화살 궤적 (황금)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(10f, 26f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,220,75), C(195,135,18));
            m.startRotation  = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.95f,0.58f)), (0.5f, new Color(1f,0.68f,0.1f)), (1f, new Color(0.68f,0.28f,0f)) },
                new[] { (0f, 0.9f), (0.5f, 0.7f), (1f, 0f) }));
        }

        // c1 — 충격 스파크 (Spark_Add)
        var c1 = new GameObject("ImpactSparks"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.07f, 0.2f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 16f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,200), C(255,195,45));
            m.gravityModifier = 0.5f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 30) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.9f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, new Color(1f,0.75f,0.1f)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ── #08  FX_Arrow_Rain_Zone ──────────────────────────────
    // Root:Arrow_Add(loop)  c1:Star_Add(loop)  c2:Soft_Add(loop)
    static GameObject BuildArrowRainZone()
    {
        var go = NewGO();

        // Root — 낙하 화살 (loop)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(8f, 20f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,215,75), C(200,128,18));
            m.gravityModifier = 1.5f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 25f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Rectangle; sh.scale = new Vector3(3f, 3f, 0f);
            sh.position  = new Vector3(0f, 4f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.9f,0.5f)), (1f, new Color(0.8f,0.5f,0.1f)) },
                new[] { (0f, 0.9f), (0.7f, 0.6f), (1f, 0f) }));
        }

        // c1 — 지면 충격 별 (Star_Add, loop)
        var c1 = new GameObject("GroundStars"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 6f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.14f, 0.42f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,240,148), C(255,175,28));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 50;
            var em = ps.emission; em.rateOverTime = 20f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Rectangle; sh.scale = new Vector3(3f, 3f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, new Color(1f,0.75f,0.1f)), (1f, new Color(1f,0.38f,0f)) },
                new[] { (0f, 1f), (0.5f, 0.5f), (1f, 0f) }));
        }

        // c2 — 황금 글로우 (Soft_Add, loop)
        var c2 = new GameObject("GoldGlow"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,220,95,175), C(255,145,28,115));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 18;
            var em = ps.emission; em.rateOverTime = 8f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Rectangle; sh.scale = new Vector3(3f, 3f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.85f,0.28f)), (1f, new Color(1f,0.58f,0.1f)) },
                new[] { (0f, 0.5f), (1f, 0f) }));
        }

        return go;
    }

    // ── #09  FX_Charge_Impact ────────────────────────────────
    // Root:Star_Add  c1:Ring_Add  c2:Spark_Add
    static GameObject BuildChargeImpact()
    {
        var go = NewGO();

        // Root — 별 폭발 (황금백)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 16f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,215), C(255,195,55));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 20;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.85f,0.2f)), (1f, new Color(0.6f,0.28f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.15f, 1.4f, 0f));
        }

        // c1 — 충격 링 (Ring_Add)
        var c1 = new GameObject("Ring"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(9f, 22f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.18f, 0.52f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,240,175), C(200,148,45));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 52;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 44) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.75f,0.15f)), (1f, new Color(0.6f,0.24f,0f)) },
                new[] { (0f, 0.9f), (0.5f, 0.5f), (1f, 0f) }));
        }

        // c2 — 산란 스파크 (Spark_Add)
        var c2 = new GameObject("Sparks"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(6f, 20f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,195), C(255,188,38));
            m.gravityModifier = 0.6f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 45;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 34) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, new Color(1f,0.7f,0.1f)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ── #10  FX_Explosion ────────────────────────────────────
    // Root:Flame_Add  c1:Shard_Add  c2:Smoke_Alpha  c3:Ring_Add
    static GameObject BuildExplosion()
    {
        var go = NewGO();

        // Root — 화염 폭발
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 18f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,228,88), C(255,55,8));
            m.gravityModifier = -0.2f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 55;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 42) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.3f, new Color(1f,0.44f,0f)), (1f, new Color(0.2f,0.04f,0f)) },
                new[] { (0f, 1f), (0.35f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.2f, 1.45f, 0.1f));
        }

        // c1 — 파편 (Shard_Add)
        var c1 = new GameObject("Debris"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.28f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 18f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.42f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,158,38), C(158,48,8));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.gravityModifier = 1.2f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.25f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.58f,0.14f)), (1f, new Color(0.42f,0.09f,0f)) },
                new[] { (0f, 1f), (0.5f, 0.7f), (1f, 0f) }));
        }

        // c2 — 검은 연기 (Smoke_Alpha)
        var c2 = new GameObject("Smoke"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.7f, 2.1f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(48,33,22), C(98,68,42));
            m.gravityModifier = -0.1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 20;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 14) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.24f,0.17f,0.11f)), (1f, new Color(0.11f,0.07f,0.04f)) },
                new[] { (0f, 0.75f), (0.4f, 0.42f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.4f, 1.5f, 0.4f));
        }

        // c3 — 폭발 링 (Ring_Add)
        var c3 = new GameObject("ExpRing"); c3.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c3);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(10f, 25f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.18f, 0.6f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,198,55), C(255,75,8));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 55;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.15f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.44f,0f)), (1f, new Color(0.4f,0.04f,0f)) },
                new[] { (0f, 1f), (0.45f, 0.55f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.3f, 1.42f, 0f));
        }

        return go;
    }

    // ── #11  FX_Summon_Circle ────────────────────────────────
    // Root:Rune_Add  c1:Wisp_Add  c2:Soft_Add  (단발 0.5초)
    static GameObject BuildSummonCircle()
    {
        var go = NewGO();

        // Root — 룬 원진 폭발 (자주색, 단발)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 6f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.3f, 0.85f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,98,255), C(115,38,255));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.2f; sh.radiusThickness = 0.08f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.9f,0.58f,1f)), (0.5f, new Color(0.58f,0.2f,1f)), (1f, new Color(0.28f,0f,0.58f)) },
                new[] { (0f, 1f), (0.5f, 0.7f), (1f, 0f) }));
        }

        // c1 — 위습 분출 (Wisp_Add, 단발)
        var c1 = new GameObject("Wisps"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(3f, 7f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.14f, 0.45f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(218,155,255), C(175,75,255));
            m.gravityModifier = -0.3f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.0f; sh.radiusThickness = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.78f,1f)), (0.5f, new Color(0.7f,0.28f,1f)), (1f, new Color(0.3f,0f,0.5f)) },
                new[] { (0f, 0.9f), (0.5f, 0.6f), (1f, 0f) }));
        }

        // c2 — 보라빛 플래시 (Soft_Add, 단발)
        var c2 = new GameObject("PurpleGlow"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(175,75,255,200), C(95,18,195,140));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 4;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.78f,0.38f,1f)), (1f, new Color(0.38f,0.08f,0.78f)) },
                new[] { (0f, 0.8f), (1f, 0f) }));
        }

        return go;
    }

    // ── #12  FX_Sacrifice ────────────────────────────────────
    // Root:Wisp_Add  c1:Soft_Add  c2:Smoke_Alpha
    static GameObject BuildSacrifice()
    {
        var go = NewGO();

        // Root — 영혼 위습 상승 (흰→금, 위로)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.8f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,220), C(255,215,80));
            m.gravityModifier = -0.5f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 35;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.9f,0.5f)), (1f, new Color(1f,0.7f,0.1f)) },
                new[] { (0f, 1f), (0.5f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.4f, 1.2f, 0.1f));
        }

        // c1 — 황금 글로우 (Soft_Add)
        var c1 = new GameObject("GoldGlow"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 0.8f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,240,140,200), C(255,185,40,140));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 8;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.95f,0.55f)), (1f, new Color(1f,0.65f,0.1f)) },
                new[] { (0f, 0.7f), (1f, 0f) }));
        }

        // c2 — 소멸 연기 (Smoke_Alpha)
        var c2 = new GameObject("Dissolve"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(180,155,100), C(130,108,65));
            m.gravityModifier = -0.08f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 15;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 10) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.72f,0.62f,0.4f)), (1f, new Color(0.38f,0.32f,0.2f)) },
                new[] { (0f, 0.55f), (1f, 0f) }));
        }

        return go;
    }

    // ── #13  FX_Absorb ───────────────────────────────────────
    // Root:Soft_Add  c1:Ring_Add  c2:Spark_Add
    static GameObject BuildAbsorb()
    {
        var go = NewGO();

        // Root — 흡수 글로우 (청록, 내향)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 2f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(80,255,210,200), C(30,195,255,150));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 15;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.8f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.3f,1f,0.82f)), (0.5f, new Color(0.12f,0.76f,1f)), (1f, new Color(0.05f,0.4f,0.8f)) },
                new[] { (0f, 0.8f), (0.5f, 0.5f), (1f, 0f) }));
        }

        // c1 — 수축 링 (Ring_Add)
        var c1 = new GameObject("ContrRing"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 14f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(100,255,220), C(40,200,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 50;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.2f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.2f,0.9f,1f)), (1f, new Color(0f,0.4f,0.9f)) },
                new[] { (0f, 0.9f), (0.5f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0f, 0.5f, 0.8f, 0f));
        }

        // c2 — 에너지 스파크 (Spark_Add)
        var c2 = new GameObject("EnergySparks"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(4f, 12f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(180,255,235), C(55,215,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 35;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 26) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.9f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, new Color(0.2f,0.85f,1f)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ── #14  FX_Battle_Cry ───────────────────────────────────
    // Root:Star_Add  c1:Ring_Add  c2:Spark_Add
    static GameObject BuildBattleCry()
    {
        var go = NewGO();

        // Root — 황금 별 폭발
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(4f, 14f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,245,140), C(255,175,18));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.gravityModifier = -0.1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.35f, new Color(1f,0.88f,0.2f)), (1f, new Color(1f,0.5f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.85f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.25f, 1.3f, 0f));
        }

        // c1 — 확장 링 (Ring_Add)
        var c1 = new GameObject("Ring"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(8f, 22f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,235,100), C(255,155,20));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 55;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 46) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.8f,0.15f)), (1f, new Color(1f,0.42f,0f)) },
                new[] { (0f, 0.9f), (0.5f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.35f, 1.4f, 0f));
        }

        // c2 — 금빛 스파크 (Spark_Add)
        var c2 = new GameObject("GoldSparks"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 16f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,180), C(255,195,35));
            m.gravityModifier = 0.4f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 45;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.03f, 35) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, new Color(1f,0.72f,0.08f)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ── #15  FX_Berserk ──────────────────────────────────────
    // Root:Flame_Add  c1:Line_Add  c2:Lightning_Add
    static GameObject BuildBerserk()
    {
        var go = NewGO();

        // Root — 진홍 화염 (상승)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.8f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 7f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,60,20), C(200,10,10));
            m.gravityModifier = -0.4f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 45;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 22f; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.9f,0.3f)), (0.3f, new Color(1f,0.2f,0.02f)), (1f, new Color(0.35f,0f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.35f, 1.3f, 0.05f));
        }

        // c1 — 에너지 슬래시 선 (Line_Add)
        var c1 = new GameObject("EnergyLines"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(8f, 20f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,120,50), C(255,30,10));
            m.startRotation  = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 20;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 45f; sh.radius = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.3f, new Color(1f,0.38f,0.05f)), (1f, new Color(0.6f,0f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.7f), (1f, 0f) }));
        }

        // c2 — 붉은 번개 (Lightning_Add)
        var c2 = new GameObject("RedLightning"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(10f, 28f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.48f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,180,140), C(255,30,20));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 22;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 15) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.8f,0.7f)), (0.5f, new Color(1f,0.15f,0.05f)), (1f, new Color(0.4f,0f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.6f), (1f, 0f) }));
        }

        return go;
    }

    // ── #16  FX_Shield_Up ────────────────────────────────────
    // Root:Diamond_Add  c1:Crystal_Add  c2:Soft_Add
    static GameObject BuildShieldUp()
    {
        var go = NewGO();

        // Root — 다이아몬드 방패 파편 (시안/빙설)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(3f, 10f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.2f, 0.65f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(210,245,255), C(80,200,255));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 28;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.5f,0.88f,1f)), (1f, new Color(0.15f,0.55f,1f)) },
                new[] { (0f, 1f), (0.5f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.3f, 1.3f, 0f));
        }

        // c1 — 얼음 결정 파편 (Crystal_Add)
        var c1 = new GameObject("IceCrystals"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 8f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.38f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(220,248,255), C(100,210,255));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.gravityModifier = 0.15f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 22) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.6f,0.92f,1f)), (1f, new Color(0.2f,0.6f,1f)) },
                new[] { (0f, 1f), (0.5f, 0.7f), (1f, 0f) }));
        }

        // c2 — 빙청 글로우 (Soft_Add)
        var c2 = new GameObject("IceGlow"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 1f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1f, 2.8f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,240,255,210), C(80,195,255,155));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 6;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.78f,0.94f,1f)), (1f, new Color(0.3f,0.68f,1f)) },
                new[] { (0f, 0.8f), (1f, 0f) }));
        }

        return go;
    }

    // ── #17  FX_Speed_Up ─────────────────────────────────────
    // Root:Spark_Add  c1:Wisp_Add  c2:Ring_Add
    static GameObject BuildSpeedUp()
    {
        var go = NewGO();

        // Root — 라임 스파크 스트림
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(6f, 18f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(220,255,100), C(100,255,50));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 50;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 38) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55f; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.3f, new Color(0.82f,1f,0.28f)), (1f, new Color(0.22f,0.8f,0.05f)) },
                new[] { (0f, 1f), (0.4f, 0.7f), (1f, 0f) }));
        }

        // c1 — 속도 위습 트레일 (Wisp_Add)
        var c1 = new GameObject("SpeedWisps"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(3f, 10f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(180,255,120), C(80,220,40));
            m.gravityModifier = -0.1f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 22;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.03f, 16) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.85f,1f,0.55f)), (0.5f, new Color(0.4f,1f,0.18f)), (1f, new Color(0.1f,0.55f,0f)) },
                new[] { (0f, 0.9f), (0.5f, 0.6f), (1f, 0f) }));
        }

        // c2 — 링 펄스 (Ring_Add)
        var c2 = new GameObject("RingPulse"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(8f, 20f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,255,120), C(85,220,30));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 48;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.04f, 40) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.65f,1f,0.18f)), (1f, new Color(0.18f,0.65f,0f)) },
                new[] { (0f, 0.9f), (0.5f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.35f, 1.4f, 0f));
        }

        return go;
    }

    // ── #18  FX_Heal_Aura ────────────────────────────────────
    // Root:Petal_Add  c1:Soft_Add  c2:Ring_Add
    static GameObject BuildHealAura()
    {
        var go = NewGO();

        // Root — 상승 꽃잎 (밝은 녹색/청록)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.8f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1f, 4f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.48f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(140,255,180), C(40,220,130));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.gravityModifier = -0.35f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 35;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.8f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.35f, new Color(0.48f,1f,0.62f)), (1f, new Color(0.1f,0.75f,0.4f)) },
                new[] { (0f, 1f), (0.5f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.4f, 1.2f, 0.1f));
        }

        // c1 — 치유 글로우 (Soft_Add)
        var c1 = new GameObject("HealGlow"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 1f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(155,255,195,200), C(55,210,130,145));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 8;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.6f,1f,0.72f)), (1f, new Color(0.18f,0.8f,0.45f)) },
                new[] { (0f, 0.75f), (1f, 0f) }));
        }

        // c2 — 치유 링 (Ring_Add)
        var c2 = new GameObject("HealRing"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(6f, 16f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(175,255,205), C(55,215,138));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 50;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.03f, 42) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.42f,1f,0.6f)), (1f, new Color(0.1f,0.7f,0.38f)) },
                new[] { (0f, 0.9f), (0.5f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.35f, 1.38f, 0f));
        }

        return go;
    }

    // ── #19  FX_Heal_Target ──────────────────────────────────
    // Root:Cross_Add  c1:Wisp_Add  c2:Soft_Add
    static GameObject BuildHealTarget()
    {
        var go = NewGO();

        // Root — 치유 십자 (Cross_Add)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 8f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(175,255,205), C(55,220,140));
            m.startRotation  = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            m.gravityModifier = -0.2f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 18;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.35f, new Color(0.5f,1f,0.65f)), (1f, new Color(0.12f,0.78f,0.45f)) },
                new[] { (0f, 1f), (0.4f, 0.85f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.25f, 1.25f, 0f));
        }

        // c1 — 치유 위습 (Wisp_Add)
        var c1 = new GameObject("HealWisps"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(190,255,215), C(65,225,148));
            m.gravityModifier = -0.25f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 20;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 15) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.75f,1f,0.84f)), (0.5f, new Color(0.3f,1f,0.58f)), (1f, new Color(0.08f,0.62f,0.3f)) },
                new[] { (0f, 0.9f), (0.5f, 0.6f), (1f, 0f) }));
        }

        // c2 — 치유 섬광 (Soft_Add)
        var c2 = new GameObject("HealFlash"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.12f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,255,220,230), C(100,240,175,180));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 3;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.78f,1f,0.86f)), (1f, new Color(0.38f,1f,0.6f)) },
                new[] { (0f, 0.9f), (1f, 0f) }));
        }

        return go;
    }

    // ── #20  FX_Bind ─────────────────────────────────────────
    // 속박 지속 이펙트 (3초 loop) — EffectDespawnDelay=4로 자동 소멸
    // Root:Spiral_Add(loop)  c1:Ring_Add(loop)  c2:Smoke_Alpha(loop)
    static GameObject BuildBind()
    {
        var go = NewGO();

        // Root — 나선 속박 파티클 (보라, loop)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(180,80,255), C(100,20,200));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 18f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.6f; sh.radiusThickness = 0f;  // 가장자리에서만 발생 → 링처럼 보임
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.85f,0.55f,1f)), (0.5f, new Color(0.58f,0.12f,1f)), (1f, new Color(0.25f,0f,0.55f)) },
                new[] { (0f, 1f), (0.6f, 0.65f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.5f, 1.0f, 0.0f));
        }

        // c1 — 맥동하는 링 (Ring_Add, loop)
        var c1 = new GameObject("BindRings"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(210,140,255), C(130,30,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 10f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.75f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(0.72f,0.28f,1f)), (1f, new Color(0.3f,0f,0.62f)) },
                new[] { (0f, 0.85f), (0.5f, 0.45f), (1f, 0f) }));
        }

        // c2 — 어둠 안개 (Smoke_Alpha, loop)
        var c2 = new GameObject("DarkMist"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(55,20,80), C(30,8,50));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 15;
            var em = ps.emission; em.rateOverTime = 4f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.22f,0.08f,0.32f)), (1f, new Color(0.1f,0.03f,0.18f)) },
                new[] { (0f, 0.55f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.6f, 1.2f, 0.1f));
        }

        return go;
    }

    // ── #21  FX_Poison_Zone ──────────────────────────────────
    // Root:Smoke_Alpha(loop)  c1:Poison_Alpha(loop)  c2:Ring_Add
    static GameObject BuildPoisonZone()
    {
        var go = NewGO();

        // Root — 짙은 독 연기 (Smoke_Alpha, loop)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(28,55,18), C(50,80,25));
            m.gravityModifier = -0.05f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 14f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.2f; sh.radiusThickness = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.16f,0.28f,0.08f)), (1f, new Color(0.08f,0.14f,0.04f)) },
                new[] { (0f, 0.7f), (0.5f, 0.45f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.4f, 1.4f, 0.4f));
        }

        // c1 — 독성 구름 (Poison_Alpha, loop)
        var c1 = new GameObject("ToxicCloud"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(100,200,30,200), C(160,240,40,170));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 10f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.0f; sh.radiusThickness = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.42f,0.88f,0.1f)), (0.5f, new Color(0.58f,0.95f,0.12f)), (1f, new Color(0.22f,0.5f,0.05f)) },
                new[] { (0f, 0.65f), (0.5f, 0.45f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.4f, 1.3f, 0.35f));
        }

        // c2 — 독 존 링 마커 (Ring_Add)
        var c2 = new GameObject("PoisonRing"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 1f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(2f, 6f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(120,220,35), C(80,180,20));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 55;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 45), new ParticleSystem.Burst(0.5f, 45) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.58f,1f,0.18f)), (0.5f, new Color(0.38f,0.8f,0.1f)), (1f, new Color(0.18f,0.45f,0.05f)) },
                new[] { (0f, 0.8f), (0.5f, 0.4f), (1f, 0f) }));
        }

        return go;
    }

    // ── #22  FX_Blizzard ─────────────────────────────────────
    // Root:Snowflake_Add  c1:Crystal_Add  c2:Smoke_Alpha  (지속 장판)
    // ── FX_Blizzard ──────────────────────────────────────────────────
    //  눈보라 — 바람에 실려 비스듬히 몰아치는 눈.
    //
    //  ⚠ 예전 것은 '눈보라' 가 아니라 '내리는 눈' 이었다
    //    입자가 중력만 받아 수직으로 떨어졌다. 조용한 강설이라 광역 빙결
    //    마법으로 읽히지 않았다. 눈보라를 만드는 것은 눈송이가 아니라 **바람**이다.
    //    → 강한 가로 속도(velocityOverLifetime.x)를 주고, 늘어난 줄기(Streak)를
    //      섞어 방향을 만든다. 바닥에는 휘도는 소용돌이를 깔아 회전을 더한다.
    //
    //  ⚠ 방향은 한쪽으로 고정한다
    //    입자마다 방향이 다르면 폭발처럼 보인다. 바람은 한 방향이라야 바람이다.
    static GameObject BuildBlizzard()
    {
        var go = NewGO();

        // 바람이 부는 방향 — 자식들이 전부 이 값을 쓴다. 어긋나면 바람이 아니라 난기류가 된다.
        const float WindX = -7.5f;
        const float WindY = -3.2f;

        // Root — 몰아치는 눈송이 (Snowflake_Add, loop)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(240,250,255), C(160,220,255));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 220;
            var em = ps.emission; em.rateOverTime = 110f;

            // 위쪽·바람 상류에서 뿌린다 — 원 안에서 생기면 '솟아나는 눈' 이 된다
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale     = new Vector3(6.5f, 4.5f, 0.1f);
            sh.position  = new Vector3(2.2f, 1.6f, 0f);

            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // ⚠ x·y·z 를 전부 같은 모드로 채운다
            //   MinMaxCurve 는 축마다 모드(상수/두 상수/커브)를 따로 갖는데,
            //   Unity 는 세 축이 같은 모드일 때만 받는다. z 를 비워 두면
            //   "Particle Velocity curves must all be in the same mode" 로 거부당한다.
            vel.x = new ParticleSystem.MinMaxCurve(WindX * 0.75f, WindX * 1.25f);
            vel.y = new ParticleSystem.MinMaxCurve(WindY * 0.7f,  WindY * 1.3f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // 흔들림 — 곧게만 날면 눈이 아니라 빗줄기다
            var noise = ps.noise; noise.enabled = true;
            noise.strength = 0.9f; noise.frequency = 1.4f; noise.scrollSpeed = 1.2f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, new Color(0.75f,0.92f,1f)), (1f, new Color(0.4f,0.7f,1f)) },
                new[] { (0f, 0f), (0.15f, 1f), (0.75f, 0.85f), (1f, 0f) }));
        }

        // c1 — 늘어난 눈줄기 (Streak_Add, loop)
        //   빠른 입자를 진행 방향으로 늘여 '몰아친다' 는 인상을 만든다.
        var c1 = new GameObject("SnowStreaks"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var rd = c1.GetComponent<ParticleSystemRenderer>();
            rd.renderMode         = ParticleSystemRenderMode.Stretch;
            rd.velocityScale      = 0.14f;   // 빠를수록 길어진다
            rd.lengthScale        = 2.2f;

            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.26f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255), C(185,230,255));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 140;
            var em = ps.emission; em.rateOverTime = 70f;

            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale     = new Vector3(6.5f, 5f, 0.1f);
            sh.position  = new Vector3(2.6f, 1.8f, 0f);

            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(WindX * 1.3f, WindX * 1.9f);
            vel.y = new ParticleSystem.MinMaxCurve(WindY * 1.1f, WindY * 1.6f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // 세 축 모드 일치 (위 주석 참고)

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, new Color(0.6f,0.85f,1f)) },
                new[] { (0f, 0f), (0.2f, 0.9f), (0.8f, 0.6f), (1f, 0f) }));
        }

        // c2 — 바닥 소용돌이 (Vortex_Add, loop)
        //   장판의 경계를 알려 주는 역할도 겸한다 — 어디까지가 얼어붙는 곳인지 보인다.
        var c2 = new GameObject("GroundSwirl"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            m.startSpeed     = 0f;
            m.startSize      = new ParticleSystem.MinMaxCurve(3.4f, 4.6f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(200,236,255,120), C(130,200,255,80));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 8;
            var em = ps.emission; em.rateOverTime = 3.5f;
            var sh = ps.shape; sh.enabled = false;

            // 회전 — 소용돌이는 돌아야 소용돌이다
            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(120f * Mathf.Deg2Rad, 210f * Mathf.Deg2Rad);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.85f,0.95f,1f)), (1f, new Color(0.45f,0.75f,1f)) },
                new[] { (0f, 0f), (0.3f, 0.5f), (0.7f, 0.35f), (1f, 0f) }));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.7f, 0.5f, 1.05f, 1.2f));
        }

        // c3 — 빙설 안개 (Smoke_Alpha, loop) — 바람을 타고 흘러간다
        var c3 = new GameObject("IcyMist"); c3.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c3);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            m.startSize      = new ParticleSystem.MinMaxCurve(1.0f, 2.4f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(195,230,255,120), C(130,195,255,80));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 9f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 2.9f; sh.radiusThickness = 1f;

            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(WindX * 0.28f, WindX * 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(WindY * 0.2f,  WindY * 0.35f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // 세 축 모드 일치 (위 주석 참고)

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.8f,0.92f,1f)), (1f, new Color(0.5f,0.76f,1f)) },
                new[] { (0f, 0f), (0.25f, 0.45f), (0.6f, 0.3f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.5f, 1.3f, 0.5f));
        }

        return go;
    }

    // ── FX_Meteor_Rock ───────────────────────────────────────────────
    //  하늘에서 떨어지는 운석 본체.
    //
    //  ⚠ FX_Meteor_Explosion 은 착탄 폭발이지 운석이 아니다
    //    낙하 연출에 폭발 프리팹을 쓰면 하늘에서부터 계속 터지면서 내려온다.
    //    떨어지는 것은 '덩어리' 여야 하고, 폭발은 땅에 닿는 순간 한 번이어야 한다.
    //
    //  ⚠ 본체는 Local, 꼬리는 World
    //    본체가 World 면 트랜스폼(EffectFallMotion)이 움직여도 입자가 제자리에 남는다.
    //    반대로 꼬리가 Local 이면 운석에 딱 붙어 따라와 꼬리로 보이지 않는다.
    static GameObject BuildMeteorRock()
    {
        var go = NewGO();

        // Root — 운석 덩어리 (Shard_Add). 트랜스폼을 따라 움직여야 하므로 Local.
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = 0.25f;
            m.startSpeed     = 0f;
            m.startSize      = new ParticleSystem.MinMaxCurve(1.5f, 1.9f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,190,90), C(255,110,40));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 12;
            var em = ps.emission; em.rateOverTime = 40f;   // 촘촘히 겹쳐 하나의 덩어리로 보이게
            var sh = ps.shape; sh.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.95f,0.7f)), (0.5f, new Color(1f,0.55f,0.15f)), (1f, new Color(0.6f,0.18f,0.05f)) },
                new[] { (0f, 1f), (0.8f, 1f), (1f, 0.6f) }));
        }

        // c1 — 불꽃 꼬리 (Flame_Add, World) — 지나온 길에 남는다
        var c1 = new GameObject("FireTrail"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,215,120), C(255,95,25));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 90;
            var em = ps.emission; em.rateOverTime = 65f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.35f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.92f,0.6f)), (0.45f, new Color(1f,0.45f,0.1f)), (1f, new Color(0.45f,0.1f,0.02f)) },
                new[] { (0f, 0.95f), (0.5f, 0.6f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1f, 0.5f, 0.6f, 0.15f));
        }

        // c2 — 검은 연기 (Smoke_Alpha, World) — 불꽃보다 오래 남아 궤적을 그린다
        var c2 = new GameObject("SmokeTrail"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(90,70,60,170), C(40,32,30,120));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 30f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(0.35f,0.28f,0.25f)), (1f, new Color(0.12f,0.1f,0.1f)) },
                new[] { (0f, 0.55f), (0.5f, 0.35f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.6f, 1.3f, 1.7f));
        }

        // c3 — 흩날리는 불티 (Spark_Add, World)
        var c3 = new GameObject("Embers"); c3.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c3);
            var m = ps.main;
            m.duration = 3f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.32f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,235,150), C(255,140,45));
            m.gravityModifier = -0.25f;   // 뒤로 흩날려 올라간다
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 70;
            var em = ps.emission; em.rateOverTime = 40f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.3f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.95f,0.7f)), (1f, new Color(1f,0.35f,0.05f)) },
                new[] { (0f, 1f), (0.6f, 0.7f), (1f, 0f) }));
        }

        return go;
    }

    // ── FX_Buff_Range ────────────────────────────────────────────────
    //  버프·오라의 적용 범위를 바닥에 그리는 전용 원.
    //
    //  ⚠ 소환진(FX_Summon_Circle)을 빌려 쓰면 안 된다
    //    소환진은 "여기서 스켈레톤이 나온다" 는 뜻을 이미 갖고 있다.
    //    전투 함성 범위에 같은 원이 뜨면 플레이어는 소환을 기다린다.
    //
    //  ⚠ 흰색으로 만든다 — 색은 EffectTint 가 런타임에 입힌다
    //    공격력 버프는 붉게, 방어 버프는 파랗게 같은 프리팹을 물들여 쓴다.
    //    색깔 수만큼 프리팹을 만들면 풀도 머티리얼도 그만큼 갈린다.
    //
    //  ⚠ scalingMode 는 반드시 Hierarchy
    //    SpawnRange 는 루트 스케일로 반경을 맞춘다. 기본값 Local 이면 자식이
    //    부모 스케일을 무시해서, 반경을 아무리 바꿔도 원이 그대로다.
    //    (WarBannerRunner 가 예전에 같은 함정을 밟았다)
    //
    //  기준 반경 = 1 : SpawnRange(scale = radius) 가 그대로 먹도록 맞춰 둔다.
    static GameObject BuildBuffRange()
    {
        var go = NewGO();

        // 링 텍스처(TX_FX_Ring)는 쿼드 반폭의 72% 지점에 원이 그려진다.
        // 보이는 원을 반경 1 에 맞추려면 그만큼 되키워야 한다. (WarBannerRunner 와 같은 값)
        const float RingTexRatio = 0.72f;
        const float RingSize     = 2f / RingTexRatio;

        // Root — 경계선 (Ring_Add). 한 장이 계속 떠 있고 숨쉬듯 밝기만 오간다.
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 1.2f; m.loop = true;
            m.startLifetime  = 1.2f;
            m.startSpeed     = 0f;
            m.startSize      = RingSize;
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255,225));
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            m.maxParticles    = 4;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) { cycleCount = 0, repeatInterval = 1.2f } });
            var sh = ps.shape; sh.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, Color.white) },
                new[] { (0f, 0f), (0.18f, 1f), (0.65f, 0.75f), (1f, 0f) }));

            // 살짝 벌어졌다 오므라든다 — 완전히 고정된 원은 UI 처럼 보인다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.94f, 0.5f, 1.03f, 0.97f));
        }

        // c1 — 경계에서 피어오르는 티끌 (Wisp_Add) — "약간의 임펙트"
        var c1 = new GameObject("EdgeMotes"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 1.2f; m.loop = true;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed     = 0f;
            m.startSize      = new ParticleSystem.MinMaxCurve(0.1f, 0.26f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255,215));
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            m.maxParticles    = 40;
            var em = ps.emission; em.rateOverTime = 26f;

            // 테두리에서만 — radiusThickness 0 이 곧 '선 위에서만 생성'
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 1f; sh.radiusThickness = 0f;

            // 위로 떠오른다 — 바닥에 붙어 있으면 원의 일부로 보여 눈에 안 띈다
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            // ⚠ x·y·z 를 전부 같은 모드로 채운다
            //   MinMaxCurve 는 축마다 모드(상수/두 상수/커브)를 따로 갖는데,
            //   Unity 는 세 축이 같은 모드일 때만 받는다. z 를 비워 두면
            //   "Particle Velocity curves must all be in the same mode" 로 거부당한다.
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, Color.white) },
                new[] { (0f, 0f), (0.25f, 0.95f), (1f, 0f) }));
        }

        // c2 — 안쪽을 아주 옅게 채우는 빛 (Soft_Add) — 범위가 '면' 임을 알려 준다
        var c2 = new GameObject("Fill"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 1.6f; m.loop = true;
            m.startLifetime  = 1.6f;
            m.startSpeed     = 0f;
            m.startSize      = 2f;
            // ⚠ 알파를 낮게 — 진하면 그 안의 유닛이 안 보인다. 범위 표시가 전투를 가리면 안 된다.
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,255,255,60));
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            m.maxParticles    = 4;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) { cycleCount = 0, repeatInterval = 1.6f } });
            var sh = ps.shape; sh.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, Color.white) },
                new[] { (0f, 0f), (0.3f, 0.28f), (0.7f, 0.22f), (1f, 0f) }));
        }

        return go;
    }

    // ── FX_RedLightning_Chain ────────────────────────────────────────
    // 폭우 사격 특성 — 주 타겟 A → 스플래시 타겟 B 붉은 전기 체인
    // Root : LineRenderer + MAT_FX_ElectricBeam_Add (붉은 전기 선)
    // c1   : ImpactSparks — B 위치 붉은 스파크 버스트   (MAT_FX_Spark_Add)
    // c2   : RedGlow      — B 위치 붉은 플래시 글로우   (MAT_FX_Soft_Add)
    // SpawnLine() 이 런타임에 LineRenderer 양 끝점과 c1/c2 월드 위치를 설정.
    static GameObject BuildRedLightningChain()
    {
        var go = NewGO();

        // Root — LineRenderer 하나가 전부다. 렌더러 1개 = 인스턴스당 드로우콜 1.
        //
        // ⚠ 예전에는 여기에 ImpactSparks / RedGlow 파티클을 더 달아 뒀다.
        //   보기엔 좋았지만 머티리얼이 서로 달라 배칭이 안 되고, 인스턴스 하나가
        //   드로우콜 3개를 먹었다. 병사까지 폭우 사격을 쓰게 되면서 동시에
        //   수십 발이 뜨는 상황이 되어 그대로 두면 수백 콜이 된다.
        //   → 파티클을 걷어내고, 사라진 타격감은 선 자체의 굵기·색으로 살린다.
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount       = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.right * 2f);  // SkillEffectHelper 가 런타임에 덮어씀
        lr.numCapVertices      = 0;             // 캡 버텍스 = 추가 삼각형. 짧은 선엔 불필요
        lr.numCornerVertices   = 0;
        lr.alignment           = LineAlignment.View;
        lr.useWorldSpace       = true;
        lr.textureMode         = LineTextureMode.Stretch;
        lr.sortingOrder        = EffectSortingOrder;
        lr.generateLightingData = false;
        lr.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows      = false;
        lr.lightProbeUsage     = UnityEngine.Rendering.LightProbeUsage.Off;

        // 착탄측(A)을 굵고 희게, 튄 쪽(B)을 가늘고 붉게 — 파티클 없이도 방향이 읽힌다
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.25f, 0.75f), new Keyframe(1f, 0.28f));
        lr.widthMultiplier = 0.30f;

        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0f),   // 착탄 지점 백열
                new GradientColorKey(new Color(1f, 0.25f, 0.15f), 0.3f),
                new GradientColorKey(new Color(0.65f, 0f, 0f),    1f),
            },
            new[]
            {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.9f, 0.5f),
                new GradientAlphaKey(0f,   1f),
            });
        lr.colorGradient = grad;

        var lrMat = AssetDatabase.LoadAssetAtPath<Material>($"{kMatPath}/MAT_FX_ElectricBeam_Add.mat");
        if (lrMat != null) lr.material = lrMat;
        else Debug.LogWarning("[EffectPrefabGenerator] 머티리얼 없음: MAT_FX_ElectricBeam_Add");

        return go;
    }

    // ── #24  FX_Martyr_Explosion ─────────────────────────────
    //  특성 "순교" — 병사가 쓰러진 자리에서 터지는 소형 폭발.
    //  Root:Flame_Add  c1:Shard_Add  c2:Ring_Add  c3:Wisp_Add
    //
    //  일반 폭발(FX_Explosion)보다 작고 짧게 잡았다 — 병사가 여럿 죽으면
    //  같은 프레임에 여러 발이 겹치므로, 크게 만들면 화면이 통째로 가려진다.
    //  마지막 위습은 "혼이 빠져나간다" 는 순교 컨셉을 담당한다.
    static GameObject BuildMartyrExplosion()
    {
        var go = NewGO();

        // Root — 붉은 화염구 (짧고 강하게)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(3f, 9f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,220,120), C(220,40,10));
            m.gravityModifier = -0.15f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 32;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.3f, new Color(1f,0.38f,0.05f)), (1f, new Color(0.25f,0.03f,0f)) },
                new[] { (0f, 1f), (0.4f, 0.8f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.15f, 0.2f, 1.4f, 0.1f));
        }

        // c1 — 갑주 파편 (Shard_Add)
        var c1 = new GameObject("Debris"); c1.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c1);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(4f, 13f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,170,60), C(150,45,10));
            m.startRotation  = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.gravityModifier = 1.4f;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 22;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.62f,0.18f)), (1f, new Color(0.4f,0.08f,0f)) },
                new[] { (0f, 1f), (0.55f, 0.65f), (1f, 0f) }));
        }

        // c2 — 지면 충격 링 (Ring_Add) — 피해 반경 2 를 눈으로 알려 준다
        var c2 = new GameObject("ShockRing"); c2.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c2);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(7f, 9f);   // ≈ 반경 2 까지 퍼진다
            m.startSize      = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,200,90), C(255,70,15));
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 44;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.1f; sh.radiusThickness = 0f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, new Color(1f,0.45f,0.05f)), (1f, new Color(0.35f,0.03f,0f)) },
                new[] { (0f, 1f), (0.5f, 0.5f), (1f, 0f) }));
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.1f, 0.3f, 1.35f, 0f));
        }

        // c3 — 떠오르는 혼백 (Wisp_Add) — 순교 컨셉
        var c3 = new GameObject("Soul"); c3.transform.SetParent(go.transform, false);
        {
            var ps = AddPS(c3);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            m.startSize      = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            m.startColor     = new ParticleSystem.MinMaxGradient(C(255,240,200), C(255,180,90));
            m.gravityModifier = -0.5f;   // 위로 떠오른다
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 10;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.08f, 6) });
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.18f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, new Color(1f,0.95f,0.8f)), (1f, new Color(1f,0.6f,0.2f)) },
                new[] { (0f, 0f), (0.25f, 0.85f), (1f, 0f) }));
        }

        return go;
    }
}
