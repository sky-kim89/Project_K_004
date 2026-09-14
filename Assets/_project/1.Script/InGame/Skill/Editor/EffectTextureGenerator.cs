using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  EffectTextureGenerator.cs
//  BattleGame > Generate Effect Textures & Materials
//
//  ■ 텍스처 목록 (128×128, 흰색 RGB + 알파 마스크)
//  [기존 14종]
//    Soft, Slash, Spark, Smoke, Ring, Flame, Snowflake,
//    Cross, Star, Diamond, Rune, Poison, Arrow, Line
//  [신규 6종]
//    Lightning    — 지그재그 번개 볼트 + 가지
//    Crystal      — 6각 크리스탈 + 내부 패싯
//    Spiral       — 2아암 아르키메데스 나선
//    Wisp         — 영혼 눈물방울 + 꼬리
//    Petal        — 자연 꽃잎 (기울임)
//    Shard        — 각진 파편 (회전 마름모)
//  [추가 1종]
//    ElectricBeam — LineRenderer 전용 전기 빔 (지그재그 코어 + 글로우 + 가지)
//  [희귀 스킬용 6종]
//    Blade        — 끝이 뾰족한 초승달 검기 (일도양단)
//    Beam         — 얇은 코어 + 글로우의 가로 섬광 (참격선)
//    Halo         — 굵은 이중 동심 링 (붕괴·폭발 충격파)
//    Vortex       — 4아암 로그 나선 (중력 붕괴 흡입)
//    HexShield    — 육각 방벽 3중 링 (불멸의 방벽 돔)
//    Streak       — 머리가 밝고 꼬리가 사라지는 낙하 궤적 (화살 폭풍)
// ============================================================

public static class EffectTextureGenerator
{
    const string kTexPath = "Assets/_project/3.Textures/FX";
    const string kMatPath = "Assets/_project/4.Materials/FX";
    const int    kSz      = 128;

    [MenuItem(ProjectKMenu.Icon + "이펙트 텍스처·머티리얼", priority = ProjectKMenu.IconPrio + 15)]
    public static void GenerateAll()
    {
        EnsureDir(kTexPath);
        EnsureDir(kMatPath);

        // ── 기존 14종 ──────────────────────────────────────────
        SavePng("TX_FX_Soft",      GenSoft());
        SavePng("TX_FX_Slash",     GenSlash());
        SavePng("TX_FX_Spark",     GenSpark());
        SavePng("TX_FX_Smoke",     GenSmoke());
        SavePng("TX_FX_Ring",      GenRing());
        SavePng("TX_FX_Flame",     GenFlame());
        SavePng("TX_FX_Snowflake", GenSnowflake());
        SavePng("TX_FX_Cross",     GenCross());
        SavePng("TX_FX_Star",      GenStar());
        SavePng("TX_FX_Diamond",   GenDiamond());
        SavePng("TX_FX_Rune",      GenRune());
        SavePng("TX_FX_Poison",    GenPoison());
        SavePng("TX_FX_Arrow",     GenArrow());
        SavePng("TX_FX_Line",      GenLine());

        // ── 신규 6종 ───────────────────────────────────────────
        SavePng("TX_FX_Lightning",    GenLightning());
        SavePng("TX_FX_Crystal",      GenCrystal());
        SavePng("TX_FX_Spiral",       GenSpiral());
        SavePng("TX_FX_Wisp",         GenWisp());
        SavePng("TX_FX_Petal",        GenPetal());
        SavePng("TX_FX_Shard",        GenShard());

        // ── 추가 1종 ───────────────────────────────────────────
        SavePng("TX_FX_ElectricBeam", GenElectricBeam());

        // ── 희귀 스킬용 6종 ────────────────────────────────────
        SavePng("TX_FX_Blade",     GenBlade());
        SavePng("TX_FX_Beam",      GenBeam());
        SavePng("TX_FX_Halo",      GenHalo());
        SavePng("TX_FX_Vortex",    GenVortex());
        SavePng("TX_FX_HexShield", GenHexShield());
        SavePng("TX_FX_Streak",    GenStreak());
        SavePng("TX_FX_ArrowH",    GenArrowH());
        SavePng("TX_FX_Bolt",      GenBolt());
        SavePng("TX_FX_Brand",     GenBrand());
        SavePng("TX_FX_Tomb",      GenTomb());
        SavePng("TX_FX_Banner",    GenBanner());
        SavePng("TX_FX_Skull",     GenSkull());

        AssetDatabase.Refresh();
        ConfigureTextureImports();

        // ── 기존 15종 머티리얼 ─────────────────────────────────
        MakeMat("MAT_FX_Soft_Add",      "TX_FX_Soft",      additive: true);
        MakeMat("MAT_FX_Soft_Alpha",    "TX_FX_Soft",      additive: false);
        MakeMat("MAT_FX_Slash_Add",     "TX_FX_Slash",     additive: true);
        MakeMat("MAT_FX_Spark_Add",     "TX_FX_Spark",     additive: true);
        MakeMat("MAT_FX_Smoke_Alpha",   "TX_FX_Smoke",     additive: false);
        MakeMat("MAT_FX_Ring_Add",      "TX_FX_Ring",      additive: true);
        MakeMat("MAT_FX_Flame_Add",     "TX_FX_Flame",     additive: true);
        MakeMat("MAT_FX_Snowflake_Add", "TX_FX_Snowflake", additive: true);
        MakeMat("MAT_FX_Cross_Add",     "TX_FX_Cross",     additive: true);
        MakeMat("MAT_FX_Star_Add",      "TX_FX_Star",      additive: true);
        MakeMat("MAT_FX_Diamond_Add",   "TX_FX_Diamond",   additive: true);
        MakeMat("MAT_FX_Rune_Add",      "TX_FX_Rune",      additive: true);
        MakeMat("MAT_FX_Poison_Alpha",  "TX_FX_Poison",    additive: false);
        MakeMat("MAT_FX_Arrow_Add",     "TX_FX_Arrow",     additive: true);
        MakeMat("MAT_FX_Line_Add",      "TX_FX_Line",      additive: true);

        // ── 신규 6종 머티리얼 ──────────────────────────────────
        MakeMat("MAT_FX_Lightning_Add",    "TX_FX_Lightning",    additive: true);
        MakeMat("MAT_FX_Crystal_Add",      "TX_FX_Crystal",      additive: true);
        MakeMat("MAT_FX_Spiral_Add",       "TX_FX_Spiral",       additive: true);
        MakeMat("MAT_FX_Wisp_Add",         "TX_FX_Wisp",         additive: true);
        MakeMat("MAT_FX_Petal_Add",        "TX_FX_Petal",        additive: true);
        MakeMat("MAT_FX_Shard_Add",        "TX_FX_Shard",        additive: true);

        // ── 추가 1종 머티리얼 ──────────────────────────────────
        MakeMat("MAT_FX_ElectricBeam_Add", "TX_FX_ElectricBeam", additive: true);

        // ── 희귀 스킬용 6종 머티리얼 ───────────────────────────
        MakeMat("MAT_FX_Blade_Add",     "TX_FX_Blade",     additive: true);
        MakeMat("MAT_FX_Beam_Add",      "TX_FX_Beam",      additive: true);
        MakeMat("MAT_FX_Halo_Add",      "TX_FX_Halo",      additive: true);
        MakeMat("MAT_FX_Vortex_Add",    "TX_FX_Vortex",    additive: true);
        MakeMat("MAT_FX_HexShield_Add", "TX_FX_HexShield", additive: true);
        MakeMat("MAT_FX_Streak_Add",    "TX_FX_Streak",    additive: true);
        MakeMat("MAT_FX_ArrowH_Add",    "TX_FX_ArrowH",    additive: true);
        MakeMat("MAT_FX_Bolt_Add",      "TX_FX_Bolt",      additive: true);
        MakeMat("MAT_FX_Brand_Add",     "TX_FX_Brand",     additive: true);
        MakeMat("MAT_FX_Tomb_Alpha",    "TX_FX_Tomb",      additive: false);
        MakeMat("MAT_FX_Tomb_Add",      "TX_FX_Tomb",      additive: true);
        MakeMat("MAT_FX_Banner_Add",    "TX_FX_Banner",    additive: true);
        MakeMat("MAT_FX_Skull_Add",     "TX_FX_Skull",     additive: true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EffectTextureGenerator] ✓ 33 textures + 35 materials generated.");
    }

    // ── 공통 헬퍼 ───────────────────────────────────────────────────

    delegate float PixFn(float cx, float cy);

    static Texture2D BuildTex(PixFn fn)
    {
        var tex = new Texture2D(kSz, kSz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        for (int py = 0; py < kSz; py++)
        for (int px = 0; px < kSz; px++)
        {
            float cx = (px + 0.5f) / kSz - 0.5f;
            float cy = (py + 0.5f) / kSz - 0.5f;
            float a  = Mathf.Clamp01(fn(cx, cy));
            tex.SetPixel(px, py, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    static void SavePng(string name, Texture2D tex)
    {
        byte[] png  = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        string full = Path.Combine(Application.dataPath, "_project/3.Textures/FX", name + ".png");
        File.WriteAllBytes(full, png);
    }

    static float Gauss(float x, float mu, float sigma)
    {
        float d = (x - mu) / sigma;
        return Mathf.Exp(-0.5f * d * d);
    }

    // 점 P에서 선분 AB까지의 최단 거리
    static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        float len2 = dx * dx + dy * dy;
        if (len2 < 1e-10f)
            return Mathf.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        float t  = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / len2);
        float nx = ax + t * dx, ny = ay + t * dy;
        return Mathf.Sqrt((px - nx) * (px - nx) + (py - ny) * (py - ny));
    }

    // ── 기존 14종 텍스처 ─────────────────────────────────────────────

    static Texture2D GenSoft() => BuildTex((cx, cy) =>
    {
        float d = Mathf.Sqrt(cx * cx + cy * cy) / 0.5f;
        return Mathf.Pow(Mathf.Max(0f, 1f - d * d), 1.5f);
    });

    static Texture2D GenSlash() => BuildTex((cx, cy) =>
    {
        float rx =  cx * 0.7071f + cy * 0.7071f;
        float ry = -cx * 0.7071f + cy * 0.7071f;
        float a  = Mathf.Max(0f, 1f - (rx / 0.45f) * (rx / 0.45f) - (ry / 0.07f) * (ry / 0.07f));
        float a2 = Mathf.Max(0f, 1f - (rx / 0.45f) * (rx / 0.45f) - (ry / 0.02f) * (ry / 0.02f));
        return Mathf.Clamp01(a + a2 * 0.6f);
    });

    static Texture2D GenSpark() => BuildTex((cx, cy) =>
    {
        float tip = Mathf.Max(0f, cy);
        float ex  = cx / 0.10f;
        float ey  = cy / 0.38f;
        float d   = Mathf.Sqrt(ex * ex + ey * ey);
        return Mathf.Max(0f, 1f - d * (1f + tip * 2.5f));
    });

    static Texture2D GenSmoke() => BuildTex((cx, cy) =>
    {
        float a = 0f;
        a += Gauss(Mathf.Sqrt(cx * cx + cy * cy), 0f, 0.21f);
        a += Gauss(Mathf.Sqrt((cx + 0.10f) * (cx + 0.10f) + (cy - 0.07f) * (cy - 0.07f)), 0f, 0.15f) * 0.75f;
        a += Gauss(Mathf.Sqrt((cx - 0.11f) * (cx - 0.11f) + (cy + 0.05f) * (cy + 0.05f)), 0f, 0.13f) * 0.65f;
        a += Gauss(Mathf.Sqrt((cx + 0.04f) * (cx + 0.04f) + (cy + 0.09f) * (cy + 0.09f)), 0f, 0.17f) * 0.55f;
        return Mathf.Clamp01(a);
    });

    static Texture2D GenRing() => BuildTex((cx, cy) =>
    {
        float d = Mathf.Sqrt(cx * cx + cy * cy) / 0.5f;
        return Gauss(d, 0.72f, 0.11f);
    });

    static Texture2D GenFlame() => BuildTex((cx, cy) =>
    {
        float scaleX = 0.32f * (1f - Mathf.Max(0f, cy) * 0.9f);
        float scaleY = 0.38f;
        float ox     = cx / Mathf.Max(0.01f, scaleX);
        float oy     = (cy + 0.05f) / scaleY;
        float d      = Mathf.Sqrt(ox * ox + oy * oy);
        return Mathf.Pow(Mathf.Max(0f, 1f - d), 0.65f);
    });

    static Texture2D GenSnowflake() => BuildTex((cx, cy) =>
    {
        float d     = Mathf.Sqrt(cx * cx + cy * cy);
        float angle = Mathf.Atan2(cy, cx);
        float result = Gauss(d, 0f, 0.055f);
        for (int i = 0; i < 6; i++)
        {
            float a  = angle - i * Mathf.PI / 3f;
            float ax = d * Mathf.Cos(a), ay = d * Mathf.Sin(a);
            float arm = Mathf.Max(0f, 1f - (ax / 0.42f) * (ax / 0.42f) - (ay / 0.025f) * (ay / 0.025f));
            arm *= Mathf.Max(0f, 1f - d / 0.44f);
            float[] bds = { 0.13f, 0.25f };
            foreach (float bd in bds)
            {
                float bx   = ax - bd;
                float bArm = Mathf.Max(0f, 1f - (bx / 0.015f) * (bx / 0.015f) - (ay / 0.065f) * (ay / 0.065f));
                arm = Mathf.Max(arm, bArm * 0.65f);
            }
            result = Mathf.Max(result, arm);
        }
        return Mathf.Clamp01(result);
    });

    static Texture2D GenCross() => BuildTex((cx, cy) =>
    {
        float ax = Mathf.Abs(cx), ay = Mathf.Abs(cy);
        float hw = 0.09f, hl = 0.43f;
        float h  = (ax <= hl && ay <= hw) ? Mathf.Pow(1f - ay / hw, 1.5f) : 0f;
        float v  = (ay <= hl && ax <= hw) ? Mathf.Pow(1f - ax / hw, 1.5f) : 0f;
        return Mathf.Clamp01(Mathf.Max(h, v));
    });

    static Texture2D GenStar() => BuildTex((cx, cy) =>
    {
        float d     = Mathf.Sqrt(cx * cx + cy * cy);
        float angle = Mathf.Atan2(cy, cx);
        float result = Gauss(d, 0f, 0.075f);
        for (int i = 0; i < 4; i++)
        {
            float a  = angle - i * Mathf.PI / 2f;
            float sx = d * Mathf.Cos(a), sy = d * Mathf.Sin(a);
            float sp = Mathf.Max(0f, 1f - (sx / 0.44f) * (sx / 0.44f) - (sy / 0.035f) * (sy / 0.035f));
            sp *= Mathf.Max(0f, 1f - d / 0.46f);
            result = Mathf.Max(result, sp);
        }
        for (int i = 0; i < 4; i++)
        {
            float a  = angle - (i * Mathf.PI / 2f + Mathf.PI / 4f);
            float sx = d * Mathf.Cos(a), sy = d * Mathf.Sin(a);
            float sp = Mathf.Max(0f, 1f - (sx / 0.26f) * (sx / 0.26f) - (sy / 0.028f) * (sy / 0.028f));
            sp *= Mathf.Max(0f, 1f - d / 0.28f);
            result = Mathf.Max(result, sp);
        }
        return Mathf.Clamp01(result);
    });

    static Texture2D GenDiamond() => BuildTex((cx, cy) =>
    {
        float d = (Mathf.Abs(cx) + Mathf.Abs(cy)) / 0.44f;
        return Mathf.Pow(Mathf.Max(0f, 1f - d), 0.75f);
    });

    static Texture2D GenRune() => BuildTex((cx, cy) =>
    {
        float d     = Mathf.Sqrt(cx * cx + cy * cy) / 0.5f;
        float angle = Mathf.Atan2(cy, cx);
        float outer  = Gauss(d, 0.86f, 0.055f);
        float inner  = Gauss(d, 0.56f, 0.04f) * 0.65f;
        float lines  = 0f;
        for (int i = 0; i < 6; i++)
        {
            float a     = angle - i * Mathf.PI / 3f;
            float perp  = d * 0.5f * Mathf.Sin(a);
            bool  inZ   = d >= 0.52f && d <= 0.90f;
            float lineA = Mathf.Max(0f, 1f - (perp / 0.025f) * (perp / 0.025f)) * (inZ ? 1f : 0f);
            lines = Mathf.Max(lines, lineA * 0.45f);
        }
        float center = Gauss(d * 0.5f, 0f, 0.055f) * 0.55f;
        return Mathf.Clamp01(outer + inner + lines + center);
    });

    static Texture2D GenPoison() => BuildTex((cx, cy) =>
    {
        float circR  = Mathf.Sqrt(cx * cx + (cy - 0.10f) * (cy - 0.10f)) / 0.33f;
        float circle = Mathf.Max(0f, 1f - circR * circR);
        float ex2    = cx / 0.085f;
        float ey2    = (cy + 0.27f) / 0.185f;
        float drip   = Mathf.Max(0f, 1f - ex2 * ex2 - ey2 * ey2);
        return Mathf.Clamp01(circle + drip);
    });

    static Texture2D GenArrow() => BuildTex((cx, cy) =>
    {
        float tip = Mathf.Max(0f, cy + 0.1f);
        float ex  = cx / 0.035f;
        float ey  = cy / 0.44f;
        float d   = Mathf.Sqrt(ex * ex + ey * ey);
        return Mathf.Max(0f, 1f - d * (1f + tip * 2.5f));
    });

    static Texture2D GenLine() => BuildTex((cx, cy) =>
    {
        float a  = Mathf.Max(0f, 1f - (cx / 0.45f) * (cx / 0.45f) - (cy / 0.025f) * (cy / 0.025f));
        float a2 = Mathf.Max(0f, 1f - (cx / 0.45f) * (cx / 0.45f) - (cy / 0.008f) * (cy / 0.008f));
        return Mathf.Clamp01(a + a2 * 0.7f);
    });

    // ── 신규 6종 텍스처 ──────────────────────────────────────────────────

    // 지그재그 번개 볼트 (두 갈래 가지 포함)
    static Texture2D GenLightning() => BuildTex((cx, cy) =>
    {
        float[][] pts = {
            new[] { 0.04f,  0.45f },
            new[] {-0.13f,  0.20f },
            new[] { 0.11f,  0.00f },
            new[] {-0.09f, -0.22f },
            new[] { 0.04f, -0.45f },
        };
        float minD = float.MaxValue;
        for (int i = 0; i < pts.Length - 1; i++)
            minD = Mathf.Min(minD, SegDist(cx, cy, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1]));

        minD = Mathf.Min(minD, SegDist(cx, cy, -0.02f,  0.12f,  0.18f,  0.00f));
        minD = Mathf.Min(minD, SegDist(cx, cy, -0.02f, -0.10f,  0.14f, -0.30f));

        float core  = Mathf.Max(0f, 1f - minD / 0.012f);
        float glow  = Mathf.Exp(-minD * 18f) * 0.55f;
        float outer = Mathf.Exp(-minD *  7f) * 0.22f;
        return Mathf.Clamp01(core + glow + outer);
    });

    // 6각 크리스탈 (내부 패싯 선 포함)
    static Texture2D GenCrystal() => BuildTex((cx, cy) =>
    {
        float ax  = Mathf.Abs(cx), ay = Mathf.Abs(cy);
        float hex  = Mathf.Max(ay, ax * 0.577f + ay * 0.5f) / 0.40f;
        float body = Mathf.Pow(Mathf.Max(0f, 1f - hex * 1.05f), 0.6f);
        float d     = Mathf.Sqrt(cx * cx + cy * cy);
        float angle = Mathf.Atan2(cy, cx);
        float facet = 0f;
        for (int f = 0; f < 3; f++)
        {
            float a    = angle - f * Mathf.PI / 3f;
            float perp = Mathf.Sin(a) * d;
            float para = Mathf.Cos(a) * d;
            float fa   = Gauss(perp, 0f, 0.013f) * Mathf.Max(0f, 1f - Mathf.Abs(para) / 0.34f);
            facet = Mathf.Max(facet, fa * 0.55f);
        }
        float center = Gauss(d, 0f, 0.09f) * 0.65f;
        return Mathf.Clamp01(body + facet * body + center);
    });

    // 2아암 아르키메데스 나선
    static Texture2D GenSpiral() => BuildTex((cx, cy) =>
    {
        float d = Mathf.Sqrt(cx * cx + cy * cy);
        if (d < 0.001f) return 1.0f;
        float angle = Mathf.Atan2(cy, cx);
        if (angle < 0f) angle += 2f * Mathf.PI;
        float a_coeff = 0.42f / (2f * Mathf.PI);
        float result  = 0f;
        for (int arm = 0; arm < 2; arm++)
        {
            float baseAngle = angle - arm * Mathf.PI;
            if (baseAngle < 0f) baseAngle += 2f * Mathf.PI;
            float spiralAngle = (d / a_coeff) % (2f * Mathf.PI);
            float dTheta = baseAngle - spiralAngle;
            while (dTheta >  Mathf.PI) dTheta -= 2f * Mathf.PI;
            while (dTheta < -Mathf.PI) dTheta += 2f * Mathf.PI;
            float arcDist = Mathf.Abs(dTheta) * d;
            float width   = Mathf.Lerp(0.055f, 0.018f, d / 0.42f);
            float alpha   = Mathf.Max(0f, 1f - arcDist / width);
            alpha *= Mathf.Max(0f, 1f - d / 0.46f);
            result = Mathf.Max(result, alpha);
        }
        return Mathf.Clamp01(Mathf.Max(result, Mathf.Max(0f, 1f - d / 0.07f)));
    });

    // 영혼 눈물방울 (본체 + 꼬리 + 측면 소용돌이)
    static Texture2D GenWisp() => BuildTex((cx, cy) =>
    {
        float bx = cx / 0.13f, by = (cy - 0.07f) / 0.28f;
        float body = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Sqrt(bx * bx + by * by)), 1.2f);
        float tx = cx / 0.038f, ty = (cy + 0.22f) / 0.17f;
        float tail  = Mathf.Max(0f, 1f - tx * tx - ty * ty) * 0.65f;
        float w2x = (cx - 0.06f) / 0.03f, w2y = (cy + 0.11f) / 0.09f;
        float w2   = Mathf.Max(0f, 1f - w2x * w2x - w2y * w2y) * 0.45f;
        float w3x = (cx + 0.07f) / 0.028f, w3y = (cy + 0.09f) / 0.08f;
        float w3   = Mathf.Max(0f, 1f - w3x * w3x - w3y * w3y) * 0.45f;
        return Mathf.Clamp01(body + tail + w2 + w3);
    });

    // 15° 기울임 꽃잎 (아래가 넓고 위가 뾰족)
    static Texture2D GenPetal() => BuildTex((cx, cy) =>
    {
        float cos = 0.966f, sin = 0.259f;
        float rx = cx * cos + cy * sin, ry = -cx * sin + cy * cos;
        float scaleX = 0.14f + Mathf.Max(0f, -ry) * 0.13f;
        float ex = rx / scaleX, ey = ry / 0.36f;
        float d   = Mathf.Sqrt(ex * ex + ey * ey);
        float tip = Mathf.Max(0f, ry) * 2.2f;
        return Mathf.Pow(Mathf.Max(0f, 1f - d * (1f + tip)), 0.7f);
    });

    // 22° 회전 각진 파편
    static Texture2D GenShard() => BuildTex((cx, cy) =>
    {
        float ang = 22f * Mathf.Deg2Rad;
        float rx  = cx * Mathf.Cos(ang) + cy * Mathf.Sin(ang);
        float ry  = -cx * Mathf.Sin(ang) + cy * Mathf.Cos(ang);
        float d   = Mathf.Abs(rx) / 0.09f + Mathf.Abs(ry) / 0.42f;
        float body = Mathf.Max(0f, 1f - d);
        return body > 0.12f ? Mathf.Pow(body, 0.65f) : body;
    });

    // LineRenderer 전용 전기 빔 (지그재그 코어 + 글로우 + 가지)
    // cx = 선의 길이 방향(-0.5~0.5), cy = 선의 폭 방향 (-0.5~0.5, 중앙=0 이 코어)
    static Texture2D GenElectricBeam() => BuildTex((cx, cy) =>
    {
        // 주 선: sin 두 개를 합성해 자연스러운 지그재그 중심선
        float jitter = Mathf.Sin(cx * 52f) * 0.028f + Mathf.Sin(cx * 21f + 1.3f) * 0.013f;
        float d = Mathf.Abs(cy - jitter);

        float core  = Mathf.Max(0f, 1f - d / 0.016f);   // 날카로운 백색 코어
        float inner = Mathf.Exp(-d * 32f) * 0.78f;       // 내부 글로우
        float outer = Mathf.Exp(-d * 11f) * 0.38f;       // 외부 확산 글로우

        // 가지: 메인 선에서 갈라지는 작은 전기 가지
        float branchCenter = jitter * 2.2f + Mathf.Sin(cx * 78f + 1.9f) * 0.016f;
        float branchD      = Mathf.Abs(cy - branchCenter);
        float branchMask   = Mathf.Max(0f, Mathf.Sin(cx * 14f + 0.7f));
        float branch       = Mathf.Exp(-branchD * 55f) * 0.28f * branchMask;

        return Mathf.Clamp01(core + inner + outer + branch);
    });

    // ══════════════════════════════════════════════════════════════════
    //  희귀 스킬용 6종
    //  전부 "코어 + 글로우" 2단 구조다. 코어만 있으면 도형처럼 납작해 보이고,
    //  글로우만 있으면 뿌옇게 뭉갠다. Additive 로 겹칠 때 이 대비가 세기를 만든다.
    // ══════════════════════════════════════════════════════════════════

    // 초승달 검기 — 호를 따라가되 양 끝으로 갈수록 얇아져 칼끝처럼 뾰족해진다
    static Texture2D GenBlade() => BuildTex((cx, cy) =>
    {
        float r = Mathf.Sqrt(cx * cx + cy * cy);
        float a = Mathf.Atan2(cy, cx);

        float t = Mathf.Abs(a) / (Mathf.PI * 0.62f);   // 0 = 호 중앙, 1 = 끝
        if (t > 1f) return 0f;

        float thick = Mathf.Lerp(0.070f, 0.003f, Mathf.Pow(t, 0.65f));
        float d     = Mathf.Abs(r - 0.40f);

        float core = Mathf.Pow(Mathf.Clamp01(1f - d / thick), 0.55f);
        float glow = Mathf.Exp(-d * 26f) * 0.35f;
        float taper = Mathf.Clamp01(1f - Mathf.Pow(t, 2.6f));

        return Mathf.Clamp01((core + glow) * taper);
    });

    // 가로 섬광 — 참격선. 코어는 머리카락처럼 얇고 위아래로만 번진다
    static Texture2D GenBeam() => BuildTex((cx, cy) =>
    {
        float ends = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(cx) / 0.5f), 0.45f);

        float core = Mathf.Exp(-(cy * cy) / (2f * 0.014f * 0.014f));
        float glow = Mathf.Exp(-(cy * cy) / (2f * 0.070f * 0.070f)) * 0.42f;
        // 중앙이 가장 굵게 부풀어 오른 형태 (양끝은 실선)
        float belly = Mathf.Exp(-(cx * cx) / (2f * 0.22f * 0.22f)) * 0.35f *
                      Mathf.Exp(-(cy * cy) / (2f * 0.13f * 0.13f));

        return Mathf.Clamp01((core + glow + belly) * ends);
    });

    // 이중 링 — 폭발 충격파. 바깥은 굵고 안쪽은 가늘어 퍼져 나가는 인상을 준다
    static Texture2D GenHalo() => BuildTex((cx, cy) =>
    {
        float r = Mathf.Sqrt(cx * cx + cy * cy);

        float outer = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(r - 0.440f) / 0.048f), 0.7f);
        float inner = Mathf.Clamp01(1f - Mathf.Abs(r - 0.300f) / 0.016f) * 0.65f;
        float haze  = Mathf.Exp(-Mathf.Abs(r - 0.440f) * 14f) * 0.25f;

        return Mathf.Clamp01(outer + inner + haze);
    });

    // 4아암 로그 나선 — 중심으로 빨려 들어가는 흐름
    static Texture2D GenVortex() => BuildTex((cx, cy) =>
    {
        float r = Mathf.Sqrt(cx * cx + cy * cy);
        if (r > 0.5f || r < 0.02f) return 0f;

        float a      = Mathf.Atan2(cy, cx);
        float spiral = Mathf.Sin(4f * a - Mathf.Log(r) * 3.4f);
        float arm    = Mathf.Pow(Mathf.Clamp01(spiral), 2.4f);

        // 바깥으로 갈수록 옅어지고, 중심 바로 앞이 가장 진하다 (빨려드는 목)
        float radial = Mathf.Clamp01(1f - r / 0.5f) * Mathf.Clamp01(r / 0.07f);
        float core   = Mathf.Exp(-(r * r) / (2f * 0.055f * 0.055f)) * 0.9f;

        return Mathf.Clamp01(arm * radial * 1.25f + core);
    });

    // 육각 방벽 — 정육각형 3중 링. 돔이 겹겹이 쌓인 느낌
    static Texture2D GenHexShield() => BuildTex((cx, cy) =>
    {
        float x = Mathf.Abs(cx), y = Mathf.Abs(cy);
        float hex = Mathf.Max(x * 1.1547f, x * 0.5774f + y);   // 정육각형 거리

        float r1 = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(hex - 0.460f) / 0.030f), 0.8f);
        float r2 = Mathf.Clamp01(1f - Mathf.Abs(hex - 0.320f) / 0.018f) * 0.60f;
        float r3 = Mathf.Clamp01(1f - Mathf.Abs(hex - 0.185f) / 0.013f) * 0.35f;
        float fill = Mathf.Clamp01(1f - hex / 0.46f) * 0.10f;   // 옅은 내부 채움

        return Mathf.Clamp01(r1 + r2 + r3 + fill);
    });

    // 묘비 — 둥근 머리 + 기둥 + 받침, 가운데 십자 각인.
    //  ⚠ 실루엣이 읽혀야 "비석" 이다
    //    파티클로 흩뿌리는 조각이 아니라 통째로 하나 떨어지는 물체이므로
    //    윤곽이 또렷하고 내부는 살짝 채운다 (Alpha 머티리얼로도 쓴다).
    static Texture2D GenTomb() => BuildTex((cx, cy) =>
    {
        float ax = Mathf.Abs(cx);

        // 기둥 — 아래로 갈수록 아주 살짝 넓어진다
        float bodyW = Mathf.Lerp(0.155f, 0.185f, Mathf.InverseLerp(0.30f, -0.34f, cy));
        bool  inBody = cy < 0.30f && cy > -0.34f && ax < bodyW;

        // 머리 — 반원
        float hx = cx / 0.165f, hy = (cy - 0.30f) / 0.165f;
        bool  inHead = cy >= 0.30f && (hx * hx + hy * hy) < 1f;

        // 받침 — 아래 두 단
        bool inBase1 = cy <= -0.34f && cy > -0.40f && ax < 0.235f;
        bool inBase2 = cy <= -0.40f && cy > -0.46f && ax < 0.285f;

        bool solid = inBody || inHead || inBase1 || inBase2;
        if (!solid) return 0f;

        // 십자 각인 — 비어 보이게 파낸다
        bool crossV = ax < 0.030f && cy < 0.34f && cy > 0.02f;
        bool crossH = Mathf.Abs(cy - 0.22f) < 0.030f && ax < 0.095f;
        if (crossV || crossH) return 0.35f;

        // 가장자리를 밝게 (윤곽선)
        float edge = Mathf.Min(bodyW - ax, 0.12f) / 0.12f;
        return Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(edge));
    });

    // 군기 — 깃대 + 펄럭이는 삼각 깃발.
    static Texture2D GenBanner() => BuildTex((cx, cy) =>
    {
        float a = 0f;

        // 깃대
        if (Mathf.Abs(cx + 0.28f) < 0.022f && cy > -0.46f && cy < 0.44f)
            a = 1f;

        // 깃발 — 오른쪽으로 뻗고 끝이 갈라진다. 사인으로 펄럭임을 준다
        if (cx > -0.26f && cx < 0.44f && cy < 0.40f && cy > -0.02f)
        {
            float t    = Mathf.InverseLerp(-0.26f, 0.44f, cx);
            float wave = Mathf.Sin(t * 5.5f) * 0.035f;
            float top  = 0.38f + wave;
            float bot  = Mathf.Lerp(0.02f, 0.20f, t) + wave;   // 끝으로 갈수록 좁아진다
            // 끝단 제비꼬리
            if (t > 0.82f) bot = Mathf.Lerp(bot, top - 0.02f, (t - 0.82f) / 0.18f);
            if (cy < top && cy > bot) a = Mathf.Max(a, 1f);
        }

        // 깃대 끝 장식
        float dx = (cx + 0.28f) / 0.055f, dy = (cy - 0.46f) / 0.055f;
        if (dx * dx + dy * dy < 1f) a = Mathf.Max(a, 1f);

        return a;
    });

    // 해골 — 사형 선고로 처형된 적 자리에 떠오르는 표식.
    //
    //  ⚠ 실루엣과 눈구멍이 전부다
    //    작게 그려지므로 디테일은 어차피 안 보인다. 두개골+턱의 윤곽과
    //    시커먼 눈구멍 두 개만 확실하면 그 거리에서도 해골로 읽힌다.
    //    구멍은 알파를 **깎아서** 만든다 — 덧그리면 흰 덩어리가 된다.
    static Texture2D GenSkull() => BuildTex((cx, cy) =>
    {
        // 두개골(위 타원) + 턱(아래 타원) 을 합쳐 윤곽을 만든다
        float cranium = Ellip(cx, cy, 0f,  0.09f, 0.30f, 0.29f, 0.10f);
        float jaw     = Ellip(cx, cy, 0f, -0.21f, 0.18f, 0.15f, 0.14f);
        float a       = Mathf.Max(cranium, jaw);

        // 파낼 구멍들 — 눈 두 개 · 코 · 이빨 틈
        float eyeL = Ellip(cx, cy, -0.125f, 0.10f, 0.098f, 0.108f, 0.22f);
        float eyeR = Ellip(cx, cy,  0.125f, 0.10f, 0.098f, 0.108f, 0.22f);
        float nose = Ellip(cx, cy,  0f,    -0.04f, 0.042f, 0.070f, 0.30f);

        float teeth = 0f;
        if (cy < -0.13f && cy > -0.33f)
            teeth = Gauss(Mathf.Repeat(cx + 0.5f, 0.072f) - 0.036f, 0f, 0.011f) * 0.95f;

        float hole = Mathf.Max(Mathf.Max(eyeL, eyeR), Mathf.Max(nose, teeth));
        return Mathf.Clamp01(a * (1f - Mathf.Clamp01(hole)));
    });

    /// <summary>타원 마스크. edge 는 가장자리가 흐려지는 폭 (0 에 가까울수록 또렷).</summary>
    static float Ellip(float cx, float cy, float ox, float oy, float rx, float ry, float edge)
    {
        float dx = (cx - ox) / rx, dy = (cy - oy) / ry;
        float d  = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01((1f - d) / Mathf.Max(0.001f, edge));
    }

    // 번개 줄기 — LineRenderer 전용. 가로로 길게 늘여도 형태가 살아 있어야 한다.
    //
    //  ⚠ 기존 TX_FX_ElectricBeam 과 따로 만든다
    //    그건 붉은 사슬 연출 전용으로 굵기·가지 밀도가 맞춰져 있다.
    //    연쇄 번개는 더 날카롭고 밝은 코어에 잔가지가 많아야 "튄다" 로 읽힌다.
    static Texture2D GenBolt() => BuildTex((cx, cy) =>
    {
        // 주 줄기 — 주파수가 다른 사인 3개를 합쳐 규칙성이 안 보이게 한다
        float jitter = Mathf.Sin(cx * 61f) * 0.052f
                     + Mathf.Sin(cx * 27f + 1.7f) * 0.030f
                     + Mathf.Sin(cx * 13f + 0.4f) * 0.014f;
        float d = Mathf.Abs(cy - jitter);

        float core  = Mathf.Max(0f, 1f - d / 0.013f);     // 흰 코어
        float inner = Mathf.Exp(-d * 30f) * 0.72f;         // 안쪽 발광
        float outer = Mathf.Exp(-d * 9f)  * 0.30f;         // 바깥 확산

        // 잔가지 — 주 줄기에서 갈라져 나가는 짧은 방전
        float branchY = jitter + Mathf.Sin(cx * 95f + 2.3f) * 0.085f;
        float branchD = Mathf.Abs(cy - branchY);
        float branchOn = Mathf.Max(0f, Mathf.Sin(cx * 23f + 1.1f) - 0.45f) * 1.8f;
        float branch  = Mathf.Exp(-branchD * 48f) * 0.42f * branchOn;

        // 양 끝은 살짝 여며 준다 (LineRenderer 이음매가 뚝 끊겨 보이지 않게)
        float ends = Mathf.Clamp01((0.5f - Mathf.Abs(cx)) / 0.06f);

        return Mathf.Clamp01((core + inner + outer + branch) * ends);
    });

    // 사형 낙인 — 이중 원 + 안쪽 십자 눈금. 머리 위에 찍히는 표식.
    static Texture2D GenBrand() => BuildTex((cx, cy) =>
    {
        float r  = Mathf.Sqrt(cx * cx + cy * cy);
        float ax = Mathf.Abs(cx), ay = Mathf.Abs(cy);

        float ring1 = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(r - 0.44f) / 0.030f), 0.8f);
        float ring2 = Mathf.Clamp01(1f - Mathf.Abs(r - 0.30f) / 0.016f) * 0.7f;

        // 십자 눈금 — 바깥 링에 걸치는 짧은 4개 (조준선 느낌)
        float tickV = (ay > 0.30f && ay < 0.50f) ? Mathf.Clamp01(1f - ax / 0.020f) : 0f;
        float tickH = (ax > 0.30f && ax < 0.50f) ? Mathf.Clamp01(1f - ay / 0.020f) : 0f;

        // 중심 점
        float dot = Mathf.Exp(-(r * r) / (2f * 0.045f * 0.045f)) * 0.85f;

        return Mathf.Clamp01(ring1 + ring2 + tickV + tickH + dot);
    });

    // 가로 화살 — 촉·샤프트·깃이 전부 있는 진짜 화살 실루엣
    //
    //  ⚠ 기존 TX_FX_Arrow 는 세로(+Y) 방향이라 Stretch 렌더에 못 쓴다
    //    Stretch 는 파티클의 +X 를 진행 방향에 맞춘다. 세로 화살을 넣으면
    //    90° 누운 채로 늘어나 화살이 아니라 빛 덩어리가 된다.
    //    → 낙하물용은 반드시 이 가로 버전을 쓴다.
    static Texture2D GenArrowH() => BuildTex((cx, cy) =>
    {
        float a  = 0f;
        float ay = Mathf.Abs(cy);

        // 샤프트 — 꼬리에서 촉 쪽으로 아주 살짝 굵어진다
        if (cx > -0.44f && cx < 0.26f)
        {
            float w = Mathf.Lerp(0.013f, 0.024f, Mathf.InverseLerp(-0.44f, 0.26f, cx));
            a = Mathf.Max(a, Mathf.Clamp01(1f - ay / w));
        }

        // 촉 — 앞이 뾰족한 삼각형
        if (cx >= 0.18f && cx <= 0.48f)
        {
            float t     = Mathf.InverseLerp(0.48f, 0.18f, cx);   // 0 = 끝, 1 = 밑변
            float halfH = Mathf.Max(0.004f, 0.095f * t);
            a = Mathf.Max(a, Mathf.Clamp01(1f - ay / halfH));
        }

        // 깃 — 꼬리 쪽 두 갈래
        if (cx >= -0.48f && cx <= -0.26f)
        {
            float t     = Mathf.InverseLerp(-0.48f, -0.26f, cx);
            float spread = Mathf.Lerp(0.085f, 0.012f, t);
            a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(ay - spread) / 0.028f) * 0.9f);
        }

        // 진행 방향으로 눕는 은은한 글로우 (촉 쪽이 밝다)
        float glow = Mathf.Exp(-(cy * cy) / (2f * 0.045f * 0.045f)) *
                     Mathf.Clamp01(1f - Mathf.Abs(cx) / 0.5f) *
                     Mathf.Lerp(0.12f, 0.35f, Mathf.InverseLerp(-0.5f, 0.5f, cx));

        return Mathf.Clamp01(a + glow);
    });

    // 낙하 궤적 — 머리가 밝고 꼬리로 갈수록 가늘어지며 사라진다
    static Texture2D GenStreak() => BuildTex((cx, cy) =>
    {
        float t = Mathf.InverseLerp(0.5f, -0.5f, cy);          // 0 = 머리(위), 1 = 꼬리
        float w = Mathf.Lerp(0.042f, 0.005f, t);

        float body = Mathf.Clamp01(1f - Mathf.Abs(cx) / w) * Mathf.Pow(1f - t, 1.5f);
        float dy   = cy - 0.36f;
        float head = Mathf.Exp(-((cx * cx) / (2f * 0.045f * 0.045f) +
                                 (dy * dy) / (2f * 0.055f * 0.055f)));

        return Mathf.Clamp01(body + head);
    });

    // ── 텍스처 임포트 설정 ──────────────────────────────────────────────

    static readonly string[] kTexNames =
    {
        "TX_FX_Soft", "TX_FX_Slash", "TX_FX_Spark", "TX_FX_Smoke",
        "TX_FX_Ring", "TX_FX_Flame", "TX_FX_Snowflake", "TX_FX_Cross",
        "TX_FX_Star", "TX_FX_Diamond", "TX_FX_Rune", "TX_FX_Poison",
        "TX_FX_Arrow", "TX_FX_Line",
        "TX_FX_Lightning", "TX_FX_Crystal", "TX_FX_Spiral",
        "TX_FX_Wisp", "TX_FX_Petal", "TX_FX_Shard",
        "TX_FX_ElectricBeam",
        "TX_FX_Blade", "TX_FX_Beam", "TX_FX_Halo",
        "TX_FX_Vortex", "TX_FX_HexShield", "TX_FX_Streak", "TX_FX_ArrowH",
        "TX_FX_Bolt", "TX_FX_Brand", "TX_FX_Tomb", "TX_FX_Banner",
        "TX_FX_Skull",
    };

    static void ConfigureTextureImports()
    {
        foreach (var name in kTexNames)
        {
            string assetPath = $"{kTexPath}/{name}.png";
            var    importer  = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;
            importer.textureType         = TextureImporterType.Default;
            importer.alphaSource         = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture         = false;
            importer.mipmapEnabled       = false;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.filterMode          = FilterMode.Bilinear;
            importer.maxTextureSize      = 128;
            importer.SaveAndReimport();
        }
    }

    // ── 머티리얼 생성 ────────────────────────────────────────────────────

    static void MakeMat(string matName, string texName, bool additive)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{kTexPath}/{texName}.png");
        if (tex == null)
        {
            Debug.LogWarning($"[EffectTextureGenerator] 텍스처 없음: {texName}.png");
            return;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? (additive
                         ? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive")
                         : Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Alpha Blended"));
        Material mat;
        if (shader != null)
        {
            mat = new Material(shader) { mainTexture = tex };
            ApplyBlending(mat, additive);
        }
        else
        {
            var baseMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            mat = new Material(baseMat) { mainTexture = tex };
            if (additive)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite",   0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
        string path     = $"{kMatPath}/{matName}.mat";
        var    existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader      = mat.shader;
            existing.mainTexture = tex;
            ApplyBlending(existing, additive);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, path);
        }
    }

    static void ApplyBlending(Material mat, bool additive)
    {
        bool isUrp = mat.shader.name.Contains("Universal Render Pipeline");
        if (isUrp)
        {
            mat.SetFloat("_Surface",        1f);
            mat.SetFloat("_Blend",          additive ? 2f : 0f);
            mat.SetFloat("_ZWrite",         0f);
            mat.SetFloat("_ZWriteControl",  0f);
            mat.SetFloat("_AlphaClip",      0f);
            mat.SetInt("_SrcBlend",      (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",      additive
                ? (int)UnityEngine.Rendering.BlendMode.One
                : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlendAlpha", additive
                ? (int)UnityEngine.Rendering.BlendMode.One
                : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) mat.EnableKeyword("_BLENDMODE_ADD");
            else          mat.DisableKeyword("_BLENDMODE_ADD");
        }
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath);
        Directory.CreateDirectory(full);
    }
}
