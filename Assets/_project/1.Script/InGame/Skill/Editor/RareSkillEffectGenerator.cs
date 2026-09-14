using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  RareSkillEffectGenerator.cs  [Editor Only]
//  희귀 액티브 스킬 4종의 이펙트 프리팹 9개를 생성한다.
//
//  Tools > Project K > 프리팹 생성 > 이펙트 > 희귀 스킬 이펙트 (8종)
//
//  ■ 왜 EffectPrefabGenerator 에 넣지 않았나
//    그 파일은 이미 1000줄이 넘는 22종 정본이다. 희귀 스킬은 연출 규모가
//    달라(다층 · 장시간 · 스케일 연동) 파라미터 기준이 섞이면 둘 다 읽기 어려워진다.
//    같은 규칙(재질·정렬 순서·명명)만 지키고 파일을 나눈다.
//
//  ■ 스케일 연동 규칙
//    Runner 가 반경/길이에 맞춰 localScale 을 곱한다.
//    · 원형 이펙트 : 프리팹 기준 반경 3   → scale = radius / 3
//    · 참격선(Beam): 프리팹 기준 길이 6   → scale = length / 6
//    이 기준을 바꾸면 Runner 의 나눗셈도 같이 고쳐야 한다.
//
//  ■ 목록
//    FX_Bisect_Charge    — 검광 응축 (시전자, 0.4초)
//    FX_Bisect_Slash     — 참격선 작렬 (길이 연동)
//    FX_Bisect_Cut       — 피격 대상 절단 섬광
//    FX_ArrowStorm_Volley— 전방 산탄 발사 (사거리 연동, 1발)
//    FX_ArrowStorm_Burst — 산탄 명중
//    FX_Gravity_Vortex   — 흡입 소용돌이 (반경 연동, 루프)
//    FX_Gravity_Collapse — 붕괴 폭발
//    FX_Bulwark_Dome     — 육각 방벽 돔 (반경 연동, 루프)
//    FX_Bulwark_Burst    — 방벽 붕괴 + 치유광
//    FX_Chain_Cast       — 시전자 방전
//    FX_Chain_Bolt       — 번개 줄기 (LineRenderer, from→to)
//    FX_Chain_Hit        — 감전 착탄
//    FX_Sentence_Mark    — 사형 낙인 (루프)
//    FX_Sentence_Execute — 처형 순간
//    FX_Death_Skull      — 처형된 자리에서 떠오르는 해골
//
//  ■ 보스 패턴 (핏빛 고정 — 아군 스킬의 직업색과 섞이면 안 된다)
//    FX_BossCharge_Windup — 돌진 웅크림 (발밑 링이 조여든다, 바닥)
//    FX_BossCharge_Trail  — 돌진 잔상 (0.1초마다 뿌려지므로 가볍게)
//    FX_BossCharge_Impact — 관통 착지
//    FX_BossSlam_Warning  — 강타 예고 (루프, 반경 연동, 바닥)
//    FX_BossSlam_Impact   — 강타 착탄 (반경 연동)
//    FX_BossSlam_Hit      — 강타 피격 (대상마다 1개)
// ============================================================

public static class RareSkillEffectGenerator
{
    const string kSavePath = "Assets/_project/2.Prefabs/Effect";
    const string kMatPath  = "Assets/_project/4.Materials/FX";
    const int    kSortOrder = 200;   // EffectPrefabGenerator.EffectSortingOrder 와 같은 값 — 유닛 그룹(100)보다 앞

    // 바닥에 깔리는 범위 표시용. 유닛(100/105)보다 **뒤** 라야 캐릭터를 덮지 않는다.
    // 범위 표시는 "어디까지 걸리는가" 를 읽는 정보라 캐릭터를 가리면 안 된다.
    // 반대로 타격·폭발은 캐릭터 앞(200)에서 터져야 맞는다 — 전부 내리지 말 것.
    const int kGroundSortOrder = 90;

    // 레이어 순서 = 자식 생성 순서. 첫 항목이 루트 파티클이다.
    static readonly Dictionary<string, string[]> kMaterials = new()
    {
        { "FX_Bisect_Charge",    new[] { "MAT_FX_Spark_Add",     "MAT_FX_Line_Add",    "MAT_FX_Soft_Add"                          } },
        { "FX_Bisect_Slash",     new[] { "MAT_FX_Beam_Add",      "MAT_FX_Blade_Add",   "MAT_FX_Spark_Add",  "MAT_FX_Shard_Add"    } },
        { "FX_Bisect_Cut",       new[] { "MAT_FX_Blade_Add",     "MAT_FX_Spark_Add",   "MAT_FX_Soft_Add"                          } },
        // 화살은 Arrow 텍스처 + Stretch 렌더라야 "떨어지는 화살" 로 읽힌다 (StretchArrow 참고)
        { "FX_ArrowStorm_Volley",new[] { "MAT_FX_ArrowH_Add",    "MAT_FX_Star_Add",    "MAT_FX_Spark_Add",  "MAT_FX_Smoke_Alpha"  } },
        { "FX_ArrowStorm_Burst", new[] { "MAT_FX_ArrowH_Add",    "MAT_FX_Halo_Add",    "MAT_FX_Spark_Add",  "MAT_FX_Smoke_Alpha"  } },
        { "FX_Gravity_Vortex",   new[] { "MAT_FX_Vortex_Add",    "MAT_FX_Wisp_Add",    "MAT_FX_Shard_Add",  "MAT_FX_Soft_Add"     } },
        { "FX_Gravity_Collapse", new[] { "MAT_FX_Halo_Add",      "MAT_FX_Shard_Add",   "MAT_FX_Soft_Add",   "MAT_FX_Smoke_Alpha"  } },
        { "FX_Bulwark_Dome",     new[] { "MAT_FX_HexShield_Add", "MAT_FX_Soft_Add",    "MAT_FX_Crystal_Add"                       } },
        { "FX_Bulwark_Burst",    new[] { "MAT_FX_Halo_Add",      "MAT_FX_Crystal_Add", "MAT_FX_Petal_Add",  "MAT_FX_Spark_Add"    } },
        // 공통 희귀 — 연쇄 번개 / 사형 선고
        { "FX_Chain_Cast",       new[] { "MAT_FX_Spark_Add",     "MAT_FX_Soft_Add"                                              } },
        // FX_Chain_Bolt 는 LineRenderer 라 파티클 머티리얼이 자식(ImpactSparks)부터 붙는다
        { "FX_Chain_Bolt",       new[] { "MAT_FX_Spark_Add"                                                                     } },
        { "FX_Chain_Hit",        new[] { "MAT_FX_Halo_Add",      "MAT_FX_Lightning_Add", "MAT_FX_Soft_Add"                      } },
        { "FX_Sentence_Mark",    new[] { "MAT_FX_Brand_Add",     "MAT_FX_Spark_Add"                                             } },
        { "FX_Sentence_Execute", new[] { "MAT_FX_Blade_Add",     "MAT_FX_Halo_Add",    "MAT_FX_Shard_Add"                       } },
        // 비석 강림 / 군기 강림 / 피의 대가 / 관통 돌진
        { "FX_Grave_Warning",    new[] { "MAT_FX_Ring_Add",      "MAT_FX_Wisp_Add"                                              } },
        // 비석 본체는 Alpha — Additive 로 두면 돌덩이가 유리처럼 비쳐 보인다
        // 자식 순서대로 매칭된다: Root(비석) · Ring · Dirt · GroundDust · Flash
        { "FX_Grave_Impact",     new[] { "MAT_FX_Tomb_Alpha",    "MAT_FX_Halo_Add",    "MAT_FX_Smoke_Alpha",
                                         "MAT_FX_Smoke_Alpha",  "MAT_FX_Halo_Add"                                              } },
        { "FX_Grave_Rise",       new[] { "MAT_FX_Wisp_Add",      "MAT_FX_Rune_Add"                                              } },
        { "FX_Banner_Aura",      new[] { "MAT_FX_Banner_Add",    "MAT_FX_Ring_Add",    "MAT_FX_Star_Add"                        } },
        { "FX_Blood_Burst",      new[] { "MAT_FX_Blade_Add",     "MAT_FX_Spark_Add",   "MAT_FX_Soft_Add"                        } },
        { "FX_Dash_Slash",       new[] { "MAT_FX_Beam_Add",      "MAT_FX_Spark_Add"                                             } },
        { "FX_Death_Skull",      new[] { "MAT_FX_Skull_Add",     "MAT_FX_Halo_Add",    "MAT_FX_Wisp_Add"                        } },
        // 보스 패턴 — 돌진 / 분쇄 강타 (자식 생성 순서대로 매칭)
        { "FX_BossCharge_Windup",new[] { "MAT_FX_Ring_Add",      "MAT_FX_Spark_Add",   "MAT_FX_Smoke_Alpha"                     } },
        { "FX_BossCharge_Trail", new[] { "MAT_FX_Streak_Add",    "MAT_FX_Smoke_Alpha"                                           } },
        { "FX_BossCharge_Impact",new[] { "MAT_FX_Halo_Add",      "MAT_FX_Shard_Add",   "MAT_FX_Smoke_Alpha", "MAT_FX_Soft_Add"  } },
        { "FX_BossSlam_Warning", new[] { "MAT_FX_Ring_Add",      "MAT_FX_Spark_Add"                                             } },
        { "FX_BossSlam_Impact",  new[] { "MAT_FX_Halo_Add",      "MAT_FX_Shard_Add",   "MAT_FX_Smoke_Alpha", "MAT_FX_Ring_Add"  } },
        { "FX_BossSlam_Hit",     new[] { "MAT_FX_Spark_Add",     "MAT_FX_Soft_Add"                                              } },
    };

    // ⚠ 방향이 곧 연출인 이펙트는 여기에 등록한다
    //   Billboard 파티클은 이미터의 Z 회전을 무시하고 항상 카메라를 본다.
    //   참격선·돌진선·부채꼴처럼 "어느 쪽으로 나가는가" 가 그림인 이펙트는
    //   Local 정렬로 바꿔야 Runner 가 넘긴 회전값대로 눕는다.
    //   안 그러면 위로 쏘든 아래로 쏘든 늘 수평으로만 그려진다.
    static readonly HashSet<string> kDirectional = new()
    {
        "FX_Dash_Slash",    // 관통 돌진 — 돌진 방향으로 눕는 참격선
        "FX_Blood_Burst",   // 피의 대가 — 전방 부채꼴 파도
    };

    // ⚠ 캐릭터 뒤(바닥)에 깔려야 하는 레이어만 여기에 등록한다
    //   값은 자식 GameObject 이름, "" 는 루트 파티클을 뜻한다.
    //   등록된 레이어만 kGroundSortOrder 로 내려가고 나머지는 앞(200)에 남는다 —
    //   범위 표시는 바닥, 타격·불꽃은 캐릭터 앞이라는 구분을 지키기 위한 표다.
    static readonly Dictionary<string, string[]> kGroundLayers = new()
    {
        // 군기 강림 — 버프 반경을 그리는 링만 바닥으로
        { "FX_Banner_Aura",  new[] { "Ring" } },
        // 불멸의 방벽 — 방벽 본체와 내부 광채가 곧 범위 표시다.
        // 표면 결정(Facets)은 그 위를 도는 장식이라 앞에 남긴다.
        { "FX_Bulwark_Dome", new[] { "", "InnerGlow" } },
        // 분쇄 강타 예고 — 통째로 바닥이다. "어디까지 맞는가" 를 읽는 그림이라
        // 보스 몸이 이걸 덮으면 피할 자리를 못 본다.
        { "FX_BossSlam_Warning", new[] { "", "Converge" } },
        // 돌진 웅크림 — 발밑 링만 바닥으로, 불티·먼지는 앞에 남긴다
        { "FX_BossCharge_Windup", new[] { "" } },
    };

    [MenuItem(ProjectKMenu.Fx + "희귀·보스 스킬 이펙트 (27종)", priority = ProjectKMenu.PrefabPrio + 52)]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "_project/2.Prefabs/Effect"));
        AssetDatabase.Refresh();

        int n = 0;
        n += Save("FX_Bisect_Charge",    BuildBisectCharge());
        n += Save("FX_Bisect_Slash",     BuildBisectSlash());
        n += Save("FX_Bisect_Cut",       BuildBisectCut());
        n += Save("FX_ArrowStorm_Volley", BuildArrowStormVolley());
        n += Save("FX_ArrowStorm_Burst", BuildArrowStormBurst());
        n += Save("FX_Gravity_Vortex",   BuildGravityVortex());
        n += Save("FX_Gravity_Collapse", BuildGravityCollapse());
        n += Save("FX_Bulwark_Dome",     BuildBulwarkDome());
        n += Save("FX_Bulwark_Burst",    BuildBulwarkBurst());
        n += Save("FX_Chain_Cast",       BuildChainCast());
        n += Save("FX_Chain_Bolt",       BuildChainBolt());
        n += Save("FX_Chain_Hit",        BuildChainHit());
        n += Save("FX_Sentence_Mark",    BuildSentenceMark());
        n += Save("FX_Sentence_Execute", BuildSentenceExecute());
        n += Save("FX_Grave_Warning",    BuildGraveWarning());
        n += Save("FX_Grave_Impact",     BuildGraveImpact());
        n += Save("FX_Grave_Rise",       BuildGraveRise());
        n += Save("FX_Banner_Aura",      BuildBannerAura());
        n += Save("FX_Blood_Burst",      BuildBloodBurst());
        n += Save("FX_Dash_Slash",       BuildDashSlash());
        n += Save("FX_Death_Skull",      BuildDeathSkull());

        // 보스 패턴
        n += Save("FX_BossCharge_Windup", BuildBossChargeWindup());
        n += Save("FX_BossCharge_Trail",  BuildBossChargeTrail());
        n += Save("FX_BossCharge_Impact", BuildBossChargeImpact());
        n += Save("FX_BossSlam_Warning",  BuildBossSlamWarning());
        n += Save("FX_BossSlam_Impact",   BuildBossSlamImpact());
        n += Save("FX_BossSlam_Hit",      BuildBossSlamHit());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RareSkillEffectGenerator] ✓ 희귀·보스 스킬 이펙트 {n}종 생성 → {kSavePath}\n" +
                  "PoolController 의 Effect 풀 프리팹 목록을 다시 불러와야 스폰된다.");
    }

    // ══════════════════════════════════════════════════════════
    //  기사 — 일도양단
    // ══════════════════════════════════════════════════════════

    // 검광 응축 — 사방의 빛이 시전자에게 빨려 들어가 한 점에 모인다
    static GameObject BuildBisectCharge()
    {
        var go = NewGO();

        // Root — 바깥에서 안으로 수렴하는 빛 조각
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(-7f, -4.5f);   // 음수 = 중심으로 수렴
            m.startSize     = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(210, 240, 255), C(120, 190, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.6f; sh.radiusThickness = 0f;   // 테두리에서만 생성

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 255, 255)), (1f, (Color)C(120, 200, 255)) },
                new[] { (0f, 0f), (0.35f, 1f), (1f, 0.9f) }));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1f, 0.7f, 0.55f, 0.1f));

            var tr = ps.trails; tr.enabled = true; tr.ratio = 0.6f;
            tr.lifetime = new ParticleSystem.MinMaxCurve(0.18f);
            tr.dieWithParticles = true;
        }

        // 칼날에 맺히는 세로 섬광
        {
            var c = Child(go, "BladeGlint");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.18f, 3) });
            var shOff = ps.shape; shOff.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(150, 210, 255)) },
                new[] { (0f, 0f), (0.25f, 1f), (1f, 0f) }));
        }

        // 발밑 응축광
        {
            var c = Child(go, "GroundGlow");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(140, 200, 255, 190));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.5f, 1.2f, 1.5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(180, 220, 255)), (1f, (Color)C(80, 150, 255)) },
                new[] { (0f, 0.9f), (1f, 0f) }));
        }

        return go;
    }

    // 참격선 — 프리팹 기준 길이 6. Runner 가 length/6 로 늘린다.
    static GameObject BuildBisectSlash()
    {
        var go = NewGO();

        // Root — 가로로 길게 뻗는 백색 검선
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.30f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(6f);   // ← 기준 길이 6
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            // 얇게 → 확 부풀었다가 사라진다 (베인 직후의 잔광)
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.separateAxes = true;
            sz.x = new ParticleSystem.MinMaxCurve(1f, AC3(0.25f, 0.12f, 1.1f, 1.15f));
            sz.y = new ParticleSystem.MinMaxCurve(1f, AC3(1.4f, 0.2f, 1f, 0.15f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, (Color)C(190, 230, 255)), (1f, (Color)C(90, 160, 255)) },
                new[] { (0f, 1f), (0.55f, 1f), (1f, 0f) }));
        }

        // 검기 초승달 — 참격선 위에 겹쳐 두께를 만든다
        {
            var c = Child(go, "Crescent");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.26f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(3.4f, 4.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(230, 245, 255));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.7f, 0.25f, 1.15f, 1.35f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(120, 190, 255)) },
                new[] { (0f, 0.95f), (0.6f, 0.7f), (1f, 0f) }));
        }

        // 참격선을 따라 튀는 불티
        {
            var c = Child(go, "Sparks");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 13f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.18f, 0.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 235), C(150, 210, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.35f;
            m.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 44) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(5.5f, 0.25f, 0f);   // 참격선을 따라 길게 분출

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(90, 160, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 갈라진 지면 파편
        {
            var c = Child(go, "Debris");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(2.5f, 7f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(190, 205, 225), C(110, 130, 165));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 1.1f;
            m.maxParticles = 30;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(5f, 0.2f, 0f);

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(120, 140, 170)) },
                new[] { (0f, 1f), (0.7f, 0.8f), (1f, 0f) }));
        }

        return go;
    }

    // 절단 섬광 — 베인 대상 위에서 십자로 터진다
    static GameObject BuildBisectCut()
    {
        var go = NewGO();

        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255));
            m.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.6f, 0.2f, 1.2f, 1.4f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(140, 200, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        {
            var c = Child(go, "Burst");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(5f, 12f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 250, 220), C(160, 210, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 30;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.15f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(100, 170, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255, 210));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.2f, 1.3f, 1.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(120, 180, 255)) },
                new[] { (0f, 0.85f), (1f, 0f) }));
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  궁수 — 화살 폭풍
    // ══════════════════════════════════════════════════════════

    // 전방 산탄 발사 (1발). 프리팹 기준 사거리 6 — Runner 가 range/6 로 늘린다.
    //
    //  ⚠ 화살은 Stretch 렌더 + 가로 화살 텍스처(TX_FX_ArrowH) 조합이어야 한다
    //    Billboard 로 두면 방향을 잃고 빛 알갱이로 보인다.
    //  ⚠ 방향은 이미터의 Z 회전이 정한다
    //    Cone 은 로컬 +Z 로 뿜으므로 shape.rotation 을 (0,90,0) 으로 눕혀 +X 를 향하게 한다.
    static GameObject BuildArrowStormVolley()
    {
        var go = NewGO();

        // Root — 부채꼴로 퍼지는 화살 산탄
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(22f, 30f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 245, 215), C(255, 190, 95));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.25f;      // 살짝 처지며 날아간다
            m.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 26f;                       // 산탄이 퍼지는 각
            sh.radius    = 0.35f;
            sh.rotation  = new Vector3(0f, 90f, 0f);  // 콘 축을 +X 로 (2D 정면)

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 245, 215)), (0.6f, (Color)C(255, 185, 90)), (1f, (Color)C(225, 120, 40)) },
                new[] { (0f, 1f), (0.75f, 1f), (1f, 0f) }));

            StretchArrow(ps, lengthScale: 2.6f, velocityScale: 0.012f);
        }

        // 총구 섬광 — "지금 쐈다" 를 알리는 한 방
        {
            var c = Child(go, "Muzzle");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 250, 225));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.2f, 1.25f, 1.5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(255, 170, 60)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 발사 불티 — 앞으로 튀는 잔불
        {
            var c = Child(go, "Sparks");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(8f, 18f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 250, 220), C(255, 150, 50));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 32f;
            sh.radius    = 0.2f;
            sh.rotation  = new Vector3(0f, 90f, 0f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(220, 100, 30)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 발사 연기 — 총구 앞에 옅게 깔린다
        {
            var c = Child(go, "Smoke");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(210, 195, 170, 130));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 20;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 24f;
            sh.radius    = 0.25f;
            sh.rotation  = new Vector3(0f, 90f, 0f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(235, 225, 205)), (1f, (Color)C(140, 125, 105)) },
                new[] { (0f, 0f), (0.2f, 0.5f), (1f, 0f) }));
        }

        return go;
    }

    // 산탄 명중 — 화살이 꽂히고 뒤로 밀려나는 순간
    static GameObject BuildArrowStormBurst()
    {
        var go = NewGO();

        // Root — 꽂힌 화살이 튕겨 나가는 파편
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(6f, 14f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 240, 200), C(255, 165, 60));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.8f;
            m.maxParticles = 24;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 55f;
            sh.radius    = 0.15f;
            sh.rotation  = new Vector3(0f, 90f, 0f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(230, 120, 40)) },
                new[] { (0f, 1f), (1f, 0f) }));

            StretchArrow(ps, lengthScale: 2.2f, velocityScale: 0.02f);
        }

        // 착탄 링 — 밀려나는 충격
        {
            var c = Child(go, "ImpactRing");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.9f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 235, 190));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.35f, 2.2f, 2.9f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(255, 140, 40)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 불티
        {
            var c = Child(go, "Sparks");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(5f, 12f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 245, 200), C(255, 120, 30));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.7f;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.25f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(200, 60, 10)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 먼지
        {
            var c = Child(go, "Dust");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.35f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(185, 165, 135, 140));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 16;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 9) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(205, 190, 165)), (1f, (Color)C(120, 105, 85)) },
                new[] { (0f, 0f), (0.2f, 0.55f), (1f, 0f) }));
        }

        return go;
    }


    // ══════════════════════════════════════════════════════════
    //  법사 — 중력 붕괴
    // ══════════════════════════════════════════════════════════

    // 흡입 소용돌이 (루프). 프리팹 기준 반경 3.
    static GameObject BuildGravityVortex()
    {
        var go = NewGO();

        // Root — 회전하는 나선판
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(6f);   // ← 기준 지름 6
            m.startColor    = new ParticleSystem.MinMaxGradient(C(190, 130, 255, 220));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 8;

            var em = ps.emission; em.rateOverTime = 3f;
            var shOff = ps.shape; shOff.enabled = false;

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-7f);   // 빨려드는 방향으로 회전

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(215, 160, 255)), (0.6f, (Color)C(140, 70, 235)), (1f, (Color)C(60, 15, 130)) },
                new[] { (0f, 0f), (0.25f, 0.95f), (0.8f, 0.8f), (1f, 0f) }));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1.15f, 0.5f, 1f, 0.8f));
        }

        // 빨려 들어가는 영혼 조각
        {
            var c = Child(go, "Wisps");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(-6.5f, -4f);   // 음수 = 중심으로
            m.startSize     = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(230, 190, 255), C(150, 90, 240));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 160;

            var em = ps.emission; em.rateOverTime = 90f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 3.2f; sh.radiusThickness = 0.25f;

            // 직선으로 빨려들지 않고 휘감기며 들어간다
            SetVelocity(ps, ParticleSystemSimulationSpace.Local, orbitalZ: new Vector2(3.5f, 6.5f));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1f, 0.7f, 0.6f, 0.05f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(240, 215, 255)), (1f, (Color)C(120, 45, 210)) },
                new[] { (0f, 0f), (0.2f, 1f), (1f, 0.4f) }));

            var tr = ps.trails; tr.enabled = true; tr.ratio = 0.45f;
            tr.lifetime = new ParticleSystem.MinMaxCurve(0.22f);
            tr.dieWithParticles = true;
        }

        // 끌려오는 파편
        {
            var c = Child(go, "Shards");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(-5.5f, -3f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.3f, 0.75f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(200, 165, 255), C(110, 60, 190));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 26f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 3.4f; sh.radiusThickness = 0.15f;

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-9f, 9f);

            SetVelocity(ps, ParticleSystemSimulationSpace.Local, orbitalZ: new Vector2(2.5f, 5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(220, 195, 255)), (1f, (Color)C(90, 40, 160)) },
                new[] { (0f, 0f), (0.25f, 1f), (1f, 0.2f) }));
        }

        // 중심 특이점
        {
            var c = Child(go, "Singularity");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(35, 5, 70, 235));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 4f;
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.85f, 0.5f, 1.1f, 0.9f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(120, 60, 210)), (0.5f, (Color)C(45, 10, 90)), (1f, (Color)C(20, 0, 45)) },
                new[] { (0f, 0f), (0.3f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // 붕괴 폭발. 프리팹 기준 반경 3.
    static GameObject BuildGravityCollapse()
    {
        var go = NewGO();

        // Root — 충격파 이중 링
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(235, 205, 255));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.12f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            // 1.5 → 6 (기준 지름 6 = 반경 3)
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.25f, 0.3f, 3f, 4f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, (Color)C(200, 140, 255)), (1f, (Color)C(90, 30, 170)) },
                new[] { (0f, 1f), (0.6f, 0.8f), (1f, 0f) }));
        }

        // 사방으로 터지는 파편
        {
            var c = Child(go, "Shards");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(9f, 20f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.3f, 0.85f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(240, 220, 255), C(130, 70, 220));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 90;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.3f;

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-10f, 10f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(80, 25, 150)) },
                new[] { (0f, 1f), (0.75f, 0.9f), (1f, 0f) }));
        }

        // 중심 섬광
        {
            var c = Child(go, "CoreFlash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(4.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255, 230));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.15f, 0.15f, 1.3f, 1.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.5f, (Color)C(210, 160, 255)), (1f, (Color)C(70, 20, 140)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 잔연
        {
            var c = Child(go, "Smoke");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(90, 60, 140, 170));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 26;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 16) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.2f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(140, 100, 200)), (1f, (Color)C(40, 20, 70)) },
                new[] { (0f, 0f), (0.2f, 0.7f), (1f, 0f) }));
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  방패병 — 불멸의 방벽
    // ══════════════════════════════════════════════════════════

    // 육각 방벽 돔 (루프). 프리팹 기준 반경 3.
    static GameObject BuildBulwarkDome()
    {
        var go = NewGO();

        // Root — 육각 방벽
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(6f);   // ← 기준 지름 6
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 225, 140, 210));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 1.6f;
            var shOff = ps.shape; shOff.enabled = false;

            // 숨쉬듯 미세하게 커졌다 작아진다 — 살아 있는 방벽
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.94f, 0.5f, 1.03f, 0.97f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 240, 190)), (0.5f, (Color)C(255, 205, 90)), (1f, (Color)C(230, 160, 40)) },
                new[] { (0f, 0f), (0.2f, 0.95f), (0.8f, 0.85f), (1f, 0f) }));
        }

        // 돔 내부 광채
        {
            var c = Child(go, "InnerGlow");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(4.6f, 5.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 210, 120, 70));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 2f;
            var shOff = ps.shape; shOff.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 235, 170)), (1f, (Color)C(240, 170, 50)) },
                new[] { (0f, 0f), (0.35f, 0.35f), (1f, 0f) }));
        }

        // 방벽 표면을 타고 도는 결정 파편
        {
            var c = Child(go, "Facets");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 245, 200), C(255, 190, 70));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 45;

            var em = ps.emission; em.rateOverTime = 22f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.9f; sh.radiusThickness = 0.12f;

            SetVelocity(ps, ParticleSystemSimulationSpace.Local, orbitalZ: new Vector2(1.2f, 2.4f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(255, 175, 55)) },
                new[] { (0f, 0f), (0.25f, 1f), (0.75f, 0.9f), (1f, 0f) }));
        }

        return go;
    }

    // 방벽 붕괴 + 치유광. 프리팹 기준 반경 3.
    static GameObject BuildBulwarkBurst()
    {
        var go = NewGO();

        // Root — 터져 나가는 충격 링
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 240, 190));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.1f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.3f, 2.4f, 3.1f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.45f, (Color)C(255, 215, 110)), (1f, (Color)C(220, 140, 30)) },
                new[] { (0f, 1f), (0.6f, 0.75f), (1f, 0f) }));
        }

        // 부서진 방벽 조각
        {
            var c = Child(go, "Fragments");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(7f, 16f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 240, 200), C(255, 180, 60));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.5f;
            m.maxParticles = 70;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 44) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.6f; sh.radiusThickness = 0.2f;

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(230, 150, 40)) },
                new[] { (0f, 1f), (0.7f, 0.85f), (1f, 0f) }));
        }

        // 치유광 — 위로 피어오르는 꽃잎
        {
            var c = Child(go, "HealMotes");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.9f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(190, 255, 205), C(110, 235, 160));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.08f, 34) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.8f; sh.radiusThickness = 0.6f;

            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(1.2f, 2.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(235, 255, 240)), (1f, (Color)C(80, 220, 140)) },
                new[] { (0f, 0f), (0.25f, 1f), (1f, 0f) }));
        }

        // 중심 섬광
        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.28f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255, 225));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.2f, 0.15f, 1.2f, 1.5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(255, 200, 90)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  공통 희귀 — 연쇄 번개
    // ══════════════════════════════════════════════════════════

    // 번개 줄기 — LineRenderer 로 from→to 를 잇는다.
    //
    //  ⚠ 파티클이 아니라 LineRenderer 다
    //    SkillEffectHelper.Spawn(key, from, to, delay) 오버로드가
    //    LineRenderer 의 0/1 번 점을 잡아 주고, "ImpactSparks" 이름의 자식을
    //    to 위치로 옮겨 준다. 이름을 바꾸면 착탄 불꽃이 시전자 쪽에 남는다.
    static GameObject BuildChainBolt()
    {
        var go = NewGO();

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount  = 2;
        lr.useWorldSpace  = true;
        lr.numCapVertices = 2;
        lr.alignment      = LineAlignment.View;
        lr.textureMode    = LineTextureMode.Stretch;
        lr.sortingOrder   = kSortOrder;

        // 가운데가 굵고 양 끝이 가늘다 — 튀어 나간 줄기처럼 보인다
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.28f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0.28f));

        lr.colorGradient = MakeGrad(
            new[] { (0f, (Color)C(215, 240, 255)), (0.5f, (Color)C(140, 200, 255)), (1f, (Color)C(90, 150, 255)) },
            new[] { (0f, 1f), (1f, 1f) });

        var boltMat = AssetDatabase.LoadAssetAtPath<Material>($"{kMatPath}/MAT_FX_Bolt_Add.mat");
        if (boltMat != null) lr.material = boltMat;
        else Debug.LogWarning("[RareSkillEffectGenerator] MAT_FX_Bolt_Add 없음 — 이펙트 텍스처를 먼저 생성하세요.");

        // 착탄 불꽃 — Spawn(from,to) 가 이 이름을 찾아 to 로 옮긴다
        {
            var c = Child(go, "ImpactSparks");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(5f, 13f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(235, 248, 255), C(110, 180, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.2f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(70, 130, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // 감전 착탄 — 맞은 적 위에서 터지는 방전
    static GameObject BuildChainHit()
    {
        var go = NewGO();

        // Root — 방전 링
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.28f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.8f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(200, 235, 255));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.3f, 2.2f, 2.8f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(80, 140, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 잔류 방전 — 감전 지속을 알리는 짧은 스파크
        {
            var c = Child(go, "Arcs");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(180, 225, 255));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 30;

            var em = ps.emission; em.rateOverTime = 22f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.45f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(60, 120, 240)) },
                new[] { (0f, 0f), (0.3f, 1f), (1f, 0f) }));
        }

        // 중심 섬광
        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.14f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255, 215));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.2f, 1.3f, 1.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(120, 180, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // 시전자 방전 — 번개가 손끝에 모이는 순간
    static GameObject BuildChainCast()
    {
        var go = NewGO();

        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(-6f, -3f);   // 중심으로 수렴
            m.startSize     = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(210, 240, 255), C(110, 180, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.2f; sh.radiusThickness = 0f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(90, 150, 255)) },
                new[] { (0f, 0f), (0.35f, 1f), (1f, 0.8f) }));

            var tr = ps.trails; tr.enabled = true; tr.ratio = 0.5f;
            tr.lifetime = new ParticleSystem.MinMaxCurve(0.15f);
            tr.dieWithParticles = true;
        }

        {
            var c = Child(go, "Core");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 255, 200));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.1f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.3f, 1.2f, 1.4f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(100, 160, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  공통 희귀 — 사형 선고
    // ══════════════════════════════════════════════════════════

    // 낙인 — 선고받은 적의 몸통에서 붉게 맥동한다 (EffectDuration 동안 유지)
    static GameObject BuildSentenceMark()
    {
        var go = NewGO();

        // Root — 회전하는 낙인 문양
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 1f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 80, 80, 220));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 2f;
            var shOff = ps.shape; shOff.enabled = false;

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(1.6f);

            // 맥동 — 커졌다 작아지며 "곧 온다" 를 알린다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.75f, 0.5f, 1.15f, 0.85f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 160, 140)), (0.5f, (Color)C(255, 60, 60)), (1f, (Color)C(180, 20, 30)) },
                new[] { (0f, 0f), (0.2f, 1f), (0.8f, 0.9f), (1f, 0f) }));
        }

        // 아래로 떨어지는 붉은 재
        {
            var c = Child(go, "Embers");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 1f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 120, 90), C(200, 30, 30));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = -0.15f;   // 살짝 떠오른다
            m.maxParticles = 30;

            var em = ps.emission; em.rateOverTime = 14f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.6f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 180, 140)), (1f, (Color)C(160, 20, 20)) },
                new[] { (0f, 0f), (0.3f, 0.9f), (1f, 0f) }));
        }

        return go;
    }

    // 처형 — 낙인이 내리꽂히며 터진다
    static GameObject BuildSentenceExecute()
    {
        var go = NewGO();

        // Root — 위에서 내리꽂히는 처형의 칼날
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2.4f, 3.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 90, 80));
            m.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.separateAxes = true;
            sz.x = new ParticleSystem.MinMaxCurve(1f, AC3(0.35f, 0.2f, 1.1f, 1.2f));
            sz.y = new ParticleSystem.MinMaxCurve(1f, AC3(1.5f, 0.25f, 1f, 0.2f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.4f, (Color)C(255, 70, 60)), (1f, (Color)C(150, 10, 20)) },
                new[] { (0f, 1f), (0.7f, 0.9f), (1f, 0f) }));
        }

        // 처형 충격 링
        {
            var c = Child(go, "Ring");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 120, 100));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.35f, 2.6f, 3.4f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(180, 20, 30)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 피처럼 흩어지는 파편
        {
            var c = Child(go, "Shards");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(6f, 15f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 140, 120), C(170, 15, 25));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0.9f;
            m.maxParticles = 50;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.25f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(140, 10, 20)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }


    // ══════════════════════════════════════════════════════════
    //  공통 희귀 — 비석 강림 / 군기 강림 / 피의 대가 / 관통 돌진
    // ══════════════════════════════════════════════════════════

    // 낙하 예고 — 땅에 파인 무덤 자국과 새어 나오는 망령의 빛
    static GameObject BuildGraveWarning()
    {
        var go = NewGO();

        // Root — 지면에 번지는 보라 원
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 1f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(150, 100, 220, 190));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 2f;
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1.25f, 0.6f, 1f, 0.9f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(200, 170, 255)), (1f, (Color)C(90, 40, 160)) },
                new[] { (0f, 0f), (0.3f, 0.95f), (1f, 0f) }));
        }

        // 무덤 자국에서 피어오르는 망령 빛
        {
            var c = Child(go, "Wisps");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 1f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(210, 180, 255), C(120, 70, 210));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 30;

            var em = ps.emission; em.rateOverTime = 16f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.8f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(230, 210, 255)), (1f, (Color)C(90, 40, 160)) },
                new[] { (0f, 0f), (0.3f, 0.85f), (1f, 0f) }));
        }

        return go;
    }

    // 착탄 — 비석이 내리꽂히고 흙이 솟구친다
    static GameObject BuildGraveImpact()
    {
        var go = NewGO();

        // 비석이 바닥에 닿는 순간(초). 링·흙·먼지·섬광이 전부 이 시각에 맞춰 터진다.
        //   낙하거리 5유닛 ÷ (startSpeed 48) = 0.104초.
        //   ⚠ 버스트는 "초", speedModifier 커브는 "수명 대비 비율" 이라 단위가 다르다.
        //     수명 0.9초이므로 커브 쪽은 0.104 / 0.9 = 0.1157 을 쓴다.
        const float Landing = 0.104f;

        // Root — 위에서 떨어져 꽂히는 비석 본체
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f);
            // ⚠ "딱" 은 낙하 속도에서 나온다
            //   22 로는 5유닛을 0.23초에 걸쳐 내려와 눈이 따라가 버린다.
            //   48 로 올려 0.1초에 꽂히게 하면 "떨어졌다" 가 아니라 "박혔다" 가 된다.
            m.startSpeed    = new ParticleSystem.MinMaxCurve(48f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2.6f, 3.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(215, 210, 225));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            // 바로 위에서 아래로 떨어진다
            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 0f; sh.radius = 0.01f;
            sh.position  = new Vector3(0f, 5f, 0f);
            sh.rotation  = new Vector3(90f, 0f, 0f);   // 콘 축을 아래로

            // 꽂히고 나면 그 자리에 잠깐 서 있다가 사라진다
            //  ⚠ 예전엔 비석이 멈추질 않았다
            //    speedModifier 가 계속 1 이라 수명 0.9초 동안 등속으로 내려가
            //    착탄 지점을 **뚫고 화면 아래로 지나가** 버렸다. 링·흙만 터지고
            //    정작 비석은 그 자리에 없으니 "꽂혔다" 는 느낌이 날 수가 없었다.
            //    t=0.11(≈0.1초, 낙하거리 5유닛)에서 속도를 즉시 0 으로 끊는다.
            //    감속 커브로 서서히 멈추면 사뿐히 내려앉는 그림이 되므로 계단으로 끊을 것.
            SetSpeedCurve(ps, ParticleSystemSimulationSpace.World, HardStop(0.1157f));

            // 착탄 순간 세로로 눌렸다가 튀어 오르는 압축 — 정지의 "딱"
            var sz = ps.sizeOverLifetime;
            sz.enabled  = true;
            sz.separateAxes = true;
            sz.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.11f, 1f),
                new Keyframe(0.16f, 1.18f), new Keyframe(0.28f, 1f), new Keyframe(1f, 1f)));
            sz.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.11f, 1f),
                new Keyframe(0.16f, 0.82f), new Keyframe(0.28f, 1f), new Keyframe(1f, 1f)));
            sz.z = new ParticleSystem.MinMaxCurve(1f, 1f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(235, 232, 245)), (0.7f, (Color)C(190, 185, 205)), (1f, (Color)C(120, 110, 140)) },
                new[] { (0f, 1f), (0.75f, 1f), (1f, 0f) }));
        }

        // 착탄 충격 링
        {
            var c = Child(go, "Ring");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(200, 180, 255));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            // 착탄 시각(0.11)에 정확히 맞춘다 — 예전 0.22 는 비석이 지나간 뒤였다
            em.SetBursts(new[] { new ParticleSystem.Burst(Landing, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            // 링은 순식간에 퍼지고 바로 사라진다.
            //  ⚠ 천천히 커지면 "연기" 로 보인다. 충격파는 빠를수록 단단해 보인다.
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.25f), new Keyframe(0.35f, 3.2f), new Keyframe(1f, 4.0f)));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.3f, (Color)C(215, 195, 255)), (1f, (Color)C(110, 60, 190)) },
                new[] { (0f, 1f), (0.35f, 0.85f), (1f, 0f) }));
        }

        // 솟구치는 흙덩이
        {
            var c = Child(go, "Dirt");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 11f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(150, 120, 90), C(90, 70, 55));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 1.6f;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(Landing, 24) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.4f; sh.arc = 180f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(160, 130, 100)), (1f, (Color)C(70, 55, 45)) },
                new[] { (0f, 1f), (0.8f, 0.9f), (1f, 0f) }));
        }

        // ── 바닥을 기는 먼지 ─────────────────────────────────
        //  위로 솟는 흙(Dirt)만으로는 "터졌다" 로 보인다.
        //  바닥에 낮게 깔려 옆으로 밀려나는 먼지가 있어야 "찍어눌렀다" 가 된다.
        {
            var c = Child(go, "GroundDust");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(7f, 13f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(180, 168, 190), C(120, 112, 130));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 30;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(Landing, 14) });

            // 완전히 수평(arc 360, 반경 0)으로 밀어낸다
            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.05f; sh.arc = 360f;

            // 밀려나면서 급격히 느려진다 — 충격이 퍼지다 멎는 그림
            SetSpeedCurve(ps, ParticleSystemSimulationSpace.World, AC3(1f, 0.3f, 0.25f, 0f));

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.5f, 1.3f, 1.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(190, 180, 200)), (1f, (Color)C(90, 84, 100)) },
                new[] { (0f, 0.75f), (0.5f, 0.4f), (1f, 0f) }));
        }

        // ── 착탄 섬광 ────────────────────────────────────────
        //  아주 짧게(0.1초) 번쩍이는 흰 빛. 눈이 "언제" 부딪혔는지 집는 신호다.
        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.6f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.1f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(3.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(Color.white);
            m.maxParticles = 2;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(Landing, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            // 커졌다 꺼지는 게 아니라 **처음이 가장 크고** 즉시 사그라든다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1f, 0.35f, 0.45f, 0f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(200, 170, 255)) },
                new[] { (0f, 1f), (0.25f, 0.7f), (1f, 0f) }));
        }

        return go;
    }

    // 기상 — 무덤에서 망자가 일어난다
    static GameObject BuildGraveRise()
    {
        var go = NewGO();

        // Root — 땅에서 솟는 보라 기둥
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.8f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(3f, 6f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(190, 150, 255), C(110, 60, 200));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 45;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.45f; sh.arc = 360f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(235, 215, 255)), (1f, (Color)C(80, 30, 160)) },
                new[] { (0f, 0f), (0.2f, 1f), (1f, 0f) }));

            var tr = ps.trails; tr.enabled = true; tr.ratio = 0.4f;
            tr.lifetime = new ParticleSystem.MinMaxCurve(0.2f);
            tr.dieWithParticles = true;
        }

        // 발밑 소환진
        {
            var c = Child(go, "Circle");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.8f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(170, 120, 255, 210));
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(2.5f);

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.35f, 1.15f, 1f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(220, 190, 255)), (1f, (Color)C(90, 40, 170)) },
                new[] { (0f, 0f), (0.25f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // 군기 오라 — 깃발이 꽂히고 금빛 결의가 퍼진다 (지속 유지)
    static GameObject BuildBannerAura()
    {
        var go = NewGO();

        // Root — 펄럭이는 군기
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(2.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 220, 130, 235));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0.8f;
            var shOff = ps.shape; shOff.enabled = false;

            // 살짝 흔들린다 — 정지 이미지로 두면 깃발이 죽어 보인다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.separateAxes = true;
            sz.x = new ParticleSystem.MinMaxCurve(1f, AC3(0.96f, 0.5f, 1.04f, 0.98f));
            sz.y = new ParticleSystem.MinMaxCurve(1f, AC3(1f, 0.5f, 0.97f, 1f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 240, 190)), (1f, (Color)C(255, 190, 70)) },
                new[] { (0f, 0f), (0.15f, 1f), (0.85f, 1f), (1f, 0f) }));
        }

        // 바닥 오라 링
        {
            var c = Child(go, "Ring");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(6f);   // 기준 지름 6 (반경 3)
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 205, 110, 170));
            m.maxParticles = 6;

            var em = ps.emission; em.rateOverTime = 1.4f;
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.9f, 0.5f, 1.02f, 1.06f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 235, 175)), (1f, (Color)C(230, 150, 40)) },
                new[] { (0f, 0f), (0.3f, 0.8f), (0.8f, 0.6f), (1f, 0f) }));
        }

        // 피어오르는 금빛 티끌
        {
            var c = Child(go, "Motes");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 2f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 240, 190), C(255, 180, 60));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 22f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.9f; sh.radiusThickness = 1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 250, 220)), (1f, (Color)C(240, 160, 45)) },
                new[] { (0f, 0f), (0.25f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // 피의 대가 — 자기 피를 뿌려 전방을 쓸어버린다
    static GameObject BuildBloodBurst()
    {
        var go = NewGO();

        // Root — 전방으로 퍼지는 핏빛 참격
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 60, 60));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.25f, 0.25f, 1.15f, 1.35f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 190, 180)), (0.4f, (Color)C(230, 30, 40)), (1f, (Color)C(120, 0, 10)) },
                new[] { (0f, 1f), (0.7f, 0.9f), (1f, 0f) }));
        }

        // 흩뿌리는 핏방울
        {
            var c = Child(go, "Blood");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(7f, 18f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(220, 40, 50), C(130, 5, 15));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 1.1f;
            m.maxParticles = 70;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 44) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 55f; sh.radius = 0.2f;
            sh.rotation  = new Vector3(0f, 90f, 0f);

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 90, 90)), (1f, (Color)C(90, 0, 10)) },
                new[] { (0f, 1f), (0.8f, 0.9f), (1f, 0f) }));
        }

        // 시전자 발밑 핏자국
        {
            var c = Child(go, "Pool");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.8f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(150, 10, 20, 190));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.3f, 1.2f, 1.3f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(200, 30, 35)), (1f, (Color)C(70, 0, 5)) },
                new[] { (0f, 0.9f), (1f, 0f) }));
        }

        return go;
    }

    // 관통 돌진 — 1초마다 도는 기술이라 짧고 가볍게
    static GameObject BuildDashSlash()
    {
        var go = NewGO();

        // Root — 지나간 자리에 남는 참격선
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(3.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(230, 245, 255));
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.separateAxes = true;
            sz.x = new ParticleSystem.MinMaxCurve(1f, AC3(0.5f, 0.15f, 1.1f, 1.2f));
            sz.y = new ParticleSystem.MinMaxCurve(1f, AC3(1.2f, 0.2f, 0.9f, 0.2f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(140, 200, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 잔불 몇 점
        {
            var c = Child(go, "Sparks");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.25f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 10f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 255, 240), C(150, 210, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 20;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.3f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(90, 160, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }


    // 처형 해골 — 사형 선고로 즉사한 적 자리에서 혼이 떠오른다.
    //
    //  ⚠ 이건 "몇 명이 죽었나" 를 세어 보이는 장치다
    //    피해 이펙트가 아니라 결과 표시다. 그래서 크고 느리고 오래 간다 —
    //    다른 이펙트가 다 꺼진 뒤에도 잠깐 남아 있어야 눈에 들어온다.
    static GameObject BuildDeathSkull()
    {
        var go = NewGO();

        // Root — 떠오르는 해골
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 1.1f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.05f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(235, 245, 255));
            m.simulationSpace = ParticleSystemSimulationSpace.World;   // 적이 사라져도 그 자리에 남는다
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            // 천천히 떠오른다 — 위로 1.3 정도
            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(1.2f, 1.4f));

            // 튀어나오듯 커졌다가 마지막에 살짝 줄며 사라진다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.35f, 0.18f, 1.12f, 0.9f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (0.35f, (Color)C(220, 240, 255)), (1f, (Color)C(120, 150, 200)) },
                new[] { (0f, 0f), (0.12f, 1f), (0.65f, 0.95f), (1f, 0f) }));
        }

        // 발밑 섬광 — 처형된 위치를 못 놓치게 한 번 때린다
        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.28f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 235, 245));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 3;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.25f, 1.6f, 2f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(180, 120, 255)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 흩어지는 혼불
        {
            var c = Child(go, "Souls");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 1f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(230, 240, 255), C(150, 120, 230));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 24;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.45f;

            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(0.7f, 1.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(130, 100, 220)) },
                new[] { (0f, 0f), (0.2f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  보스 패턴 — 돌진 / 분쇄 강타
    // ══════════════════════════════════════════════════════════
    //
    //  ■ 색은 핏빛으로 고정한다
    //    아군 스킬은 직업색(청/녹/금)을 쓴다. 보스 패턴이 같은 색을 쓰면
    //    난전에서 "지금 나한테 오는 것" 과 "내가 쓴 것" 이 구분되지 않는다.
    //
    //  ■ 예고(Warning)는 반드시 바닥에 깐다
    //    "어디까지 맞는가" 를 읽는 정보라 캐릭터를 덮으면 안 된다 (kGroundLayers).
    //
    //  ■ 반경 연동
    //    분쇄 강타는 프리팹 기준 반경 3 으로 그리고 Runner 가 SlamRadius/3 를 곱한다.
    //    기본 반경이 7 이라 그대로 두면 표시가 실제 범위의 절반도 안 된다.

    // 돌진 ① 웅크림 — 발밑으로 힘이 모이고 흙이 빨려 들어온다
    static GameObject BuildBossChargeWindup()
    {
        var go = NewGO();

        // Root — 발밑 링이 조여든다 (곧 튀어나간다는 신호)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(3.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 120, 70, 210));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var shOff = ps.shape; shOff.enabled = false;

            // 커지는 게 아니라 **조여든다** — 모으는 동작이라 안쪽으로 수축해야 읽힌다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1.3f, 0.7f, 0.75f, 0.5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 190, 120)), (1f, (Color)C(190, 40, 30)) },
                new[] { (0f, 0f), (0.25f, 0.95f), (1f, 0f) }));
        }

        // 사방에서 빨려 들어오는 불티 (startSpeed 음수 = 중심으로 수렴)
        {
            var c = Child(go, "Gather");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(-6f, -3.5f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 210, 150), C(220, 70, 40));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.8f; sh.radiusThickness = 0f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(200, 50, 30)) },
                new[] { (0f, 0f), (0.3f, 1f), (1f, 0.85f) }));
        }

        // 뒷발에 밟혀 튀는 흙먼지
        {
            var c = Child(go, "Dust");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(1f, 2.4f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(150, 125, 105, 170));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 24;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1f;

            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(0.3f, 1.1f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(170, 145, 120)), (1f, (Color)C(90, 75, 60)) },
                new[] { (0f, 0f), (0.2f, 0.6f), (1f, 0f) }));
        }

        return go;
    }

    // 돌진 ② 잔상 — 0.1초마다 경로에 뿌려진다
    //  ⚠ 가볍게 만들어야 한다
    //    긴 돌진이면 한 번에 20개 넘게 깔린다. 층을 늘리면 그 수만큼 곱해진다.
    static GameObject BuildBossChargeTrail()
    {
        var go = NewGO();

        // Root — 지나간 자리에 남는 붉은 잔상
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.9f, 2.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 110, 60, 160));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(1f, 0.5f, 0.8f, 0.45f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 180, 120)), (1f, (Color)C(180, 30, 25)) },
                new[] { (0f, 0.8f), (1f, 0f) }));
        }

        // 발밑에서 뒤로 튀는 흙
        {
            var c = Child(go, "Kick");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(150, 128, 108, 150));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 10;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.6f;

            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(0.4f, 1.3f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(165, 140, 115)), (1f, (Color)C(85, 70, 58)) },
                new[] { (0f, 0.55f), (1f, 0f) }));
        }

        return go;
    }

    // 돌진 ③ 관통 착지 — 몸통으로 들이받은 자리
    static GameObject BuildBossChargeImpact()
    {
        var go = NewGO();

        // Root — 충격파 고리
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 210, 170));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.4f, 0.45f, 3.4f, 4.6f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(210, 60, 40)) },
                new[] { (0f, 1f), (0.5f, 0.6f), (1f, 0f) }));
        }

        // 사방으로 튀는 파편
        {
            var c = Child(go, "Shards");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(6f, 13f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 200, 150), C(200, 60, 40));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.3f; sh.radiusThickness = 1f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(170, 40, 30)) },
                new[] { (0f, 1f), (0.6f, 0.8f), (1f, 0f) }));
        }

        // 뭉게 먼지
        {
            var c = Child(go, "Dust");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.5f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(2f, 5f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(150, 128, 110, 180));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 24;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.9f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(175, 150, 125)), (1f, (Color)C(80, 68, 58)) },
                new[] { (0f, 0f), (0.15f, 0.7f), (1f, 0f) }));
        }

        // 순간 섬광
        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(3.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 235, 200));
            m.maxParticles = 2;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(255, 150, 90)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // 분쇄 강타 ① 예고 — 발밑에 그려지는 위험 범위 (바닥 정렬)
    //  기준 반경 3 — Runner 가 SlamRadius/3 를 곱한다.
    static GameObject BuildBossSlamWarning()
    {
        var go = NewGO();

        // Root — 붉은 위험 원. 도약하는 내내 떠 있어야 하므로 loop.
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.55f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(6f);   // 지름 = 반경 3 × 2
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 70, 55, 200));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 2f;
            var shOff = ps.shape; shOff.enabled = false;

            // 맥박 — 같은 크기로 가만히 있으면 "예고" 가 아니라 장식으로 보인다
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.92f, 0.5f, 1f, 0.92f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 140, 110)), (1f, (Color)C(200, 30, 25)) },
                new[] { (0f, 0f), (0.3f, 0.9f), (0.7f, 0.9f), (1f, 0f) }));
        }

        // 안쪽으로 떨어져 모이는 불티 — "여기로 내려온다" 를 방향으로 말해 준다
        {
            var c = Child(go, "Converge");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.55f; m.loop = true;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(-5f, -3f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 190, 140), C(225, 60, 45));
            m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.maxParticles = 60;

            var em = ps.emission; em.rateOverTime = 34f;

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 3f; sh.radiusThickness = 0f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(200, 45, 35)) },
                new[] { (0f, 0f), (0.25f, 0.9f), (1f, 0.7f) }));
        }

        return go;
    }

    // 분쇄 강타 ② 착탄 — 땅이 꺼진다
    //  기준 반경 3 — Runner 가 SlamRadius/3 를 곱한다.
    static GameObject BuildBossSlamImpact()
    {
        var go = NewGO();

        // Root — 바깥으로 터져 나가는 충격파 고리
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.7f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.5f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 225, 190));
            m.maxParticles = 4;

            var em = ps.emission; em.rateOverTime = 0f;
            // 두 번 터뜨려 파문이 겹치게 한다 — 한 겹이면 반경 7 이 얇아 보인다
            em.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f,    1),
                new ParticleSystem.Burst(0.09f, 1),
            });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.3f, 0.4f, 3.2f, 4.2f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(205, 55, 40)) },
                new[] { (0f, 1f), (0.5f, 0.55f), (1f, 0f) }));
        }

        // 위로 솟구치는 돌덩이 — 넉백이 '띄우는' 스킬이라 파편도 위로 간다
        {
            var c = Child(go, "Debris");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.7f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(5f, 11f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(190, 165, 140), C(120, 100, 85));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 1.6f;   // 솟았다가 도로 떨어져야 무게가 실린다
            m.maxParticles = 50;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 2.2f; sh.radiusThickness = 1f;

            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(3f, 7f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(200, 175, 150)), (1f, (Color)C(95, 80, 68)) },
                new[] { (0f, 1f), (0.7f, 0.9f), (1f, 0f) }));
        }

        // 바닥을 기어 퍼지는 흙먼지
        {
            var c = Child(go, "Dust");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.7f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(4f, 9f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(155, 133, 112, 190));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 40;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 1.2f; sh.radiusThickness = 1f;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(180, 155, 130)), (1f, (Color)C(78, 66, 56)) },
                new[] { (0f, 0f), (0.12f, 0.75f), (1f, 0f) }));
        }

        // 갈라진 땅에서 새어 나오는 붉은 빛
        {
            var c = Child(go, "Crack");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.7f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(5.2f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 90, 60, 220));
            m.maxParticles = 2;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, (Color)C(255, 170, 120)), (1f, (Color)C(150, 20, 15)) },
                new[] { (0f, 0.95f), (0.4f, 0.7f), (1f, 0f) }));
        }

        return go;
    }

    // 분쇄 강타 ③ 피격 — 맞은 유닛마다 하나씩. 가볍게.
    static GameObject BuildBossSlamHit()
    {
        var go = NewGO();

        // Root — 위로 튀어 오르는 타격 불티 (넉백 방향과 같이 위)
        {
            var ps = AddPS(go);
            var m = ps.main;
            m.duration = 0.4f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(3f, 7f);
            m.startSize     = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 205, 160), C(215, 60, 45));
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = 16;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.25f; sh.radiusThickness = 1f;

            SetVelocity(ps, ParticleSystemSimulationSpace.World, y: new Vector2(2f, 4.5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(180, 40, 30)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        // 짧은 타격 섬광
        {
            var c = Child(go, "Flash");
            var ps = AddPS(c);
            var m = ps.main;
            m.duration = 0.3f; m.loop = false;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.14f);
            m.startSpeed    = new ParticleSystem.MinMaxCurve(0f);
            m.startSize     = new ParticleSystem.MinMaxCurve(1.4f);
            m.startColor    = new ParticleSystem.MinMaxGradient(C(255, 225, 190));
            m.maxParticles = 2;

            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shOff = ps.shape; shOff.enabled = false;

            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AC3(0.6f, 0.4f, 1.3f, 1.5f));

            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(MakeGrad(
                new[] { (0f, Color.white), (1f, (Color)C(255, 120, 80)) },
                new[] { (0f, 1f), (1f, 0f) }));
        }

        return go;
    }

    // ══════════════════════════════════════════════════════════
    //  헬퍼 — EffectPrefabGenerator 와 같은 규칙
    // ══════════════════════════════════════════════════════════

    static int Save(string key, GameObject go)
    {
        go.name = key;
        if (kMaterials.TryGetValue(key, out var mats)) ApplyMaterials(go, mats);

        if (kDirectional.Contains(key) || key.StartsWith("FX_Bisect_")) AlignToTransform(go);
        if (kGroundLayers.TryGetValue(key, out var ground)) SortToGround(go, ground);
        PrefabUtility.SaveAsPrefabAsset(go, $"{kSavePath}/{key}.prefab");
        Object.DestroyImmediate(go);
        return 1;
    }

    // ══════════════════════════════════════════════════════════
    //  속도 모듈 — 반드시 이 헬퍼로만 설정한다
    // ══════════════════════════════════════════════════════════
    //
    //  ⚠ "Particle Velocity curves must all be in the same mode"
    //    VelocityOverLifetime 의 곡선(x/y/z · orbital · radial · speedModifier)은
    //    전부 같은 모드여야 한다. y 만 TwoConstants 로 주고 x/z 를 기본값(Constant)
    //    으로 두면 Unity 가 이 에러를 뱉는다.
    //    → 건드리지 않는 축까지 전부 TwoConstants 로 채워 모드를 통일한다.

    static void SetVelocity(ParticleSystem ps, ParticleSystemSimulationSpace space,
                            Vector2 x = default, Vector2 y = default, Vector2 orbitalZ = default)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = space;

        vel.x = TwoC(x);
        vel.y = TwoC(y);
        vel.z = TwoC(default);

        vel.orbitalX = TwoC(default);
        vel.orbitalY = TwoC(default);
        vel.orbitalZ = TwoC(orbitalZ);

        vel.radial        = TwoC(default);
        vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);
    }

    static ParticleSystem.MinMaxCurve TwoC(Vector2 v)
        => new ParticleSystem.MinMaxCurve(v.x, v.y);

    /// <summary>
    /// 속도를 "수명에 따라 변하는 배율"로 제어한다 (낙하 → 급정지 같은 연출).
    ///
    /// ⚠ SetVelocity 를 대신 쓰면 안 된다
    ///   그쪽은 speedModifier 를 TwoConstants(1,1) 로 고정해 곡선을 넣을 수 없다.
    ///   그렇다고 speedModifier 만 곡선으로 바꾸면 x/y/z 와 모드가 달라져
    ///   "Particle Velocity curves must all be in the same mode" 가 터진다.
    ///   → 여기서는 모든 축을 **0 짜리 곡선**으로 채워 전부 Curve 모드로 맞춘다.
    /// </summary>
    static void SetSpeedCurve(ParticleSystem ps, ParticleSystemSimulationSpace space,
                              AnimationCurve speedModifier)
    {
        var zero = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 0f));

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = space;

        vel.x = zero; vel.y = zero; vel.z = zero;
        vel.orbitalX = zero; vel.orbitalY = zero; vel.orbitalZ = zero;
        vel.radial   = zero;

        vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, speedModifier);
    }

    /// <summary>
    /// 파티클을 속도 방향으로 늘여 화살처럼 보이게 한다 (Render Mode = Stretched Billboard).
    ///
    /// ⚠ 낙하물은 Billboard 로 두면 안 된다
    ///   빌보드는 항상 카메라 정면을 보므로 아무리 빨리 떨어져도 동그란 빛으로만 보인다.
    ///   화살·유성처럼 "방향이 곧 정체" 인 파티클은 반드시 Stretch 여야 한다.
    /// </summary>
    static void StretchArrow(ParticleSystem ps, float lengthScale, float velocityScale)
    {
        var rd = ps.GetComponent<ParticleSystemRenderer>();
        rd.renderMode    = ParticleSystemRenderMode.Stretch;
        rd.lengthScale   = lengthScale;
        rd.velocityScale = velocityScale;
    }

    // 파티클을 이미터 회전에 맞춰 눕힌다 (Render Alignment = Local).
    static void AlignToTransform(GameObject root)
    {
        foreach (var rd in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            rd.alignment = ParticleSystemRenderSpace.Local;
    }

    // 지정한 레이어만 유닛 뒤(바닥)로 내린다. names 의 "" 는 루트를 뜻한다.
    static void SortToGround(GameObject root, string[] names)
    {
        foreach (var name in names)
        {
            var t = string.IsNullOrEmpty(name) ? root.transform : root.transform.Find(name);
            if (t == null)
            {
                Debug.LogWarning($"[RareSkillEffectGenerator] 바닥 정렬 대상 없음: {root.name}/{name}");
                continue;
            }

            t.GetComponent<ParticleSystemRenderer>().sortingOrder = kGroundSortOrder;
        }
    }

    static void ApplyMaterials(GameObject root, string[] mats)
    {
        var rs = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < Mathf.Min(rs.Length, mats.Length); i++)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{kMatPath}/{mats[i]}.mat");
            if (mat != null) rs[i].material = mat;
            else Debug.LogWarning($"[RareSkillEffectGenerator] 머티리얼 없음: {mats[i]} " +
                                  "— 아이콘·텍스처 > 이펙트 텍스처·머티리얼 을 먼저 실행하세요.");
        }
    }

    static GameObject NewGO() => new GameObject("FX");

    static GameObject Child(GameObject parent, string name)
    {
        var c = new GameObject(name);
        c.transform.SetParent(parent.transform, false);
        return c;
    }

    // ⚠ 파티클 모듈(main/shape/emission…)은 struct 를 값으로 돌려준다.
    //   ps.shape.enabled = false 처럼 직접 대입하면 CS1612 로 컴파일이 깨진다.
    //   반드시 지역 변수로 받아서(var sh = ps.shape) 고칠 것.
    static ParticleSystem AddPS(GameObject go)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var rd = go.GetComponent<ParticleSystemRenderer>();
        rd.renderMode   = ParticleSystemRenderMode.Billboard;
        rd.sortingOrder = kSortOrder;
        var def = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (def != null) rd.material = def;
        return ps;
    }

    static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);

    /// <summary>
    /// t 까지 1 로 달리다가 그 자리에서 0 으로 끊기는 계단 커브.
    /// 낙하물이 "감속해서 멈추는" 게 아니라 "박혀서 서는" 느낌을 만든다.
    /// 두 키를 아주 가깝게(0.001) 두고 접선을 0 으로 눕혀야 계단이 된다 —
    /// 그냥 두면 Unity 가 부드럽게 이어 붙여 감속 곡선이 되어버린다.
    /// </summary>
    static AnimationCurve HardStop(float t)
    {
        var curve = new AnimationCurve(
            new Keyframe(0f,           1f, 0f, 0f),
            new Keyframe(t,            1f, 0f, 0f),
            new Keyframe(t + 0.001f,   0f, 0f, 0f),
            new Keyframe(1f,           0f, 0f, 0f));

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve,  i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    static AnimationCurve AC3(float v0, float t1, float v1, float v2) =>
        new AnimationCurve(new Keyframe(0f, v0), new Keyframe(t1, v1), new Keyframe(1f, v2));

    static Gradient MakeGrad((float t, Color c)[] cols, (float t, float a)[] alps)
    {
        var g  = new Gradient();
        var ck = new GradientColorKey[cols.Length];
        var ak = new GradientAlphaKey[alps.Length];
        for (int i = 0; i < cols.Length; i++) ck[i] = new GradientColorKey(cols[i].c, cols[i].t);
        for (int i = 0; i < alps.Length; i++) ak[i] = new GradientAlphaKey(alps[i].a, alps[i].t);
        g.SetKeys(ck, ak);
        return g;
    }
}
