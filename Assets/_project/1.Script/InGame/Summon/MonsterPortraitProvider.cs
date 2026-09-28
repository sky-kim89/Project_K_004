using System;
using System.Collections.Generic;
using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using Assets.PixelFantasy.Common.Scripts.Utils;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;
using UnityEngine.U2D.Animation;

// ============================================================
//  MonsterPortraitProvider.cs
//  몬스터 초상화를 런타임에 만들어 돌려주는 공급자.
//
//  ■ 원작 GeneralPortraitProvider 와 같은 방식이다
//    초상화는 PNG 에셋이 아니라 **런타임 합성물**이다. 원작이 장수 초상화를
//    그렇게 만들고 있었고, 그 방식이 이 게임에도 그대로 맞는다:
//
//      · 장비·무기가 붙으면 초상화에 저절로 반영된다.
//        (PNG 로 구워 두면 장비가 바뀔 때마다 다시 구워야 한다)
//      · 관리할 이미지 에셋이 없다.
//      · 종족이 늘어도 굽는 절차가 없다.
//
//    ⚠ 에디터에서 PNG 로 굽는 방식은 폐기했다
//      프리뷰 씬 + 카메라 렌더링은 Animator·렌더 파이프라인이 런타임과 달라
//      빈 그림이 나왔다(실제로 전 종족 실패했다).
//
//  ■ Idle 첫 프레임을 잘라 쓴다
//    합성 시트는 576×928(2MB)이다. 그대로 캐시하면 종족 수만큼 2MB 씩 든다.
//    Idle_0 만 잘라 독립 텍스처로 굽고(≈64×64) 시트는 계속 덮어쓴다.
//
//    ⚠ 시트로 Sprite.Create 를 하면 안 된다
//      다음 합성 때 이미 뿌려 둔 초상화가 통째로 바뀐다.
//
//  ■ 픽셀을 어디서 얻는지만 신체 형태에 따라 갈린다
//    인간형   : CharacterBuilder 가 레이어를 병합한 시트에서
//    비인간형 : 완성 라이브러리의 Idle 스프라이트에서
//    그 뒤 자르기·여백 트림·캐시는 완전히 같은 경로다.
//    (이 갈림은 MonsterAppearanceBridge 가 이미 갖고 있는 것과 같은 갈림이다)
// ============================================================

public class MonsterPortraitProvider : MonoBehaviour
{
    static MonsterPortraitProvider _inst;

    static MonsterPortraitProvider Inst
    {
        get
        {
            if (_inst != null) return _inst;

            var go = new GameObject("[MonsterPortraitProvider]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<MonsterPortraitProvider>();
            return _inst;
        }
    }

    readonly Dictionary<string, Sprite> _cache = new();

    CharacterBuilder _builder;
    Texture2D        _sheet;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// 그 종족의 초상화를 돌려준다. 처음 요청이면 그 자리에서 합성한다.
    ///
    /// 종족 수가 열 종 남짓이라 비동기로 나눌 이유가 없다 —
    /// 원작 장수 초상화는 400명을 격자로 뿌려서 프레임 예산이 필요했지만,
    /// 여기는 카드 6장과 라인 대기열이 전부다.
    /// </summary>
    public static Sprite Get(MonsterSpeciesData species)
        => Get(species, species != null ? species.Mark : MonsterMark.None);

    /// <summary>
    /// 표식을 <b>따로</b> 지정해 초상화를 만든다 — 소환사가 쓴다.
    ///
    /// ⚠ 소환사는 종족과 표식이 갈려 있다 (SummonerData.AppearanceSpecies / AppearanceMark)
    ///   '슬라임 킹' 은 <b>평범한 슬라임</b> 그림에 왕관만 얹은 것이다. 종족의 Mark
    ///   (슬라임 = None)를 쓰면 왕관이 영영 안 나온다 — 전장에서는 나오는데
    ///   선택 화면·전투 통계에서만 맨 슬라임이었다.
    /// </summary>
    public static Sprite Get(MonsterSpeciesData species, MonsterMark mark)
    {
        if (species == null) return null;

        var inst = Inst;

        // ⚠ 장비를 반영하고, **캐시 열쇠에 장비 Key 를 넣는다** (사용자 지적, 2026-09-11)
        //   한때 종족 ID 하나로 캐시하고 장비를 아예 안 봤다 — 전장은 갑옷을 입는데
        //   도감·상세·카드·대기열 초상화는 전부 맨몸이라 "껴도 외형이 안 바뀐다" 로 보였다.
        //   전장(MonsterRuntimeBridge)과 같은 BuildVisual 을 불러 같은 겉모습을 만든다.
        MonsterGearVisual gear = MonsterGearRule.BuildVisual(species.Id);

        // ⚠ 표식도 열쇠다 — 같은 종족이 표식만 다를 수 있다
        //   소환사 '슬라임 킹'(슬라임 + 왕관)과 카드 '슬라임'(맨몸)이 바로 그 경우다.
        //   빼면 먼저 만들어진 쪽이 캐시에 남아 둘 중 하나가 남의 그림을 쓴다.
        string key = species.Id + "#" + (gear.Key ?? "") + "#" + (int)mark;

        if (inst._cache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Sprite made = inst.Build(species, gear, mark);
        if (made != null) inst._cache[key] = made;

        return made;
    }

    // ── 합성 ─────────────────────────────────────────────────

    Sprite Build(MonsterSpeciesData species, in MonsterGearVisual gear, MonsterMark mark)
    {
        // ⚠ "손으로 넣어 둔 그림이 우선" 이라는 예외는 없앴다 (2026-08-28)
        //   그 한 줄 때문에 종족 아이콘을 데이터에 채운 순간 게임 전체의
        //   초상화가 아이콘으로 바뀌었다. 초상화는 예외 없이 합성한다 —
        //   특정 종족만 갈아 끼우고 싶다면 그건 외형 시드(EnemyRace·Id)로
        //   조절할 일이지, 완성 그림을 끼워 넣을 일이 아니다.

        return species.BodyType == MonsterBodyType.Humanoid
            ? BuildHumanoid(species, gear, mark)
            : BuildNonHumanoid(species, gear, mark);
    }

    /// <summary>
    /// 인간형 — 원작 장수 초상화와 완전히 같은 절차다.
    /// 장비 칸은 전장과 같은 덮어쓰기(UnitAppearanceBridge.WithGear)로 얹는다.
    /// </summary>
    Sprite BuildHumanoid(MonsterSpeciesData species, in MonsterGearVisual gear, MonsterMark mark)
    {
        if (!EnsureBuilder()) return null;

        // ⚠ 공격 형태를 함께 넘긴다 — 전장과 **같은 무기**가 나와야 한다 (2026-09-12)
        //   안 넘기면 근접 풀에서 뽑혀, 고블린 궁수가 필드에서는 활을 들고
        //   카드·도감·전황 초상화에서는 낫을 든다. 장비를 얹는 것과 같은 이유로
        //   (아래 WithGear) 두 경로는 같은 입력을 지나야 한다.
        UnitAppearanceData rolled =
            EnemyAppearanceRoller.Roll(species.Race, species.Id, species.AttackKind);
        UnitAppearanceData data   = gear.IsEmpty ? rolled : UnitAppearanceBridge.WithGear(rolled, gear);

        _builder.Body    = data.Body;
        _builder.Head    = data.Head;
        _builder.Ears    = data.Ears;
        _builder.Eyes    = data.Eyes;
        _builder.Hair    = data.Hair;
        _builder.Armor   = data.Armor;
        _builder.Helmet  = data.Helmet;
        _builder.Mask    = data.Mask;
        _builder.Horns   = data.Horns;
        _builder.Cape    = data.Cape;
        _builder.Weapon  = data.Weapon;
        _builder.Shield  = data.Shield;
        _builder.Back    = data.Back;
        _builder.Firearm = "";

        var layers = _builder.BuildLayers();
        if (layers.Count == 0) return null;

        if (_sheet == null)
            _sheet = new Texture2D(576, 928, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

        TextureHelper.MergeLayers(_sheet, layers.Values.ToArray());

        int[] l = CharacterBuilder.Layout["Idle_0"];

        Color[] px = ReadRegion(_sheet, l[0], l[1], l[2], l[3]);
        if (px == null) return null;

        // PixelFantasy 합성 시트는 16 PPU 다
        return CropAndTrim(px, l[2], l[3], species.BodyTint, species.BodyScale, mark, 16f);
    }

    /// <summary>
    /// 비인간형 — 합성할 레이어가 없다. 완성 라이브러리의 Idle 프레임을 쓴다.
    ///
    /// 자르기·트림·캐시는 인간형과 같은 경로를 탄다.
    /// </summary>
    Sprite BuildNonHumanoid(MonsterSpeciesData species, in MonsterGearVisual gear, MonsterMark mark)
    {
        if (species.NonHumanoidLibrary == null)
        {
            Debug.LogWarning($"[MonsterPortraitProvider] '{species.Id}' 의 라이브러리가 비어 있습니다. " +
                             "데이터 생성 > 비인간형 몬스터 라이브러리 를 먼저 실행하세요.");
            return null;
        }

        Sprite src = species.NonHumanoidLibrary.GetSprite("Idle", "0");
        if (src == null)
        {
            Debug.LogWarning($"[MonsterPortraitProvider] '{species.Id}' 라이브러리에 Idle_0 이 없습니다.");
            return null;
        }

        Rect r = src.rect;

        // ⚠ GetPixels 를 직접 부르지 않는다
        //   임포트된 PNG 는 Read/Write 가 꺼져 있는 것이 기본이라 CPU 읽기가 막힌다.
        //   ReadRegion 이 그 경우 GPU 를 거쳐 읽는다.
        Color[] px = ReadRegion(src.texture,
                                (int)r.x, (int)r.y, (int)r.width, (int)r.height);
        if (px == null) return null;

        // 비인간형 장비(가죽)는 색조로 입는다 — 전장(MonsterAppearanceBridge)과 같은 곱이다.
        return CropAndTrim(px, (int)r.width, (int)r.height,
                           gear.Tint * species.BodyTint, species.BodyScale, mark, src.pixelsPerUnit);
    }

    bool EnsureBuilder()
    {
        if (_builder == null)
        {
            var go = new GameObject("Builder");
            go.transform.SetParent(transform, false);
            go.SetActive(false);              // 화면에 그릴 일이 없다 — 합성기만 쓴다
            _builder = go.AddComponent<CharacterBuilder>();
        }

        if (_builder.SpriteCollection == null)
            _builder.SpriteCollection = Resources.Load<SpriteCollection>("SpriteCollection");

        return _builder.SpriteCollection != null;
    }

    // ── 픽셀 읽기 ────────────────────────────────────────────

    /// <summary>
    /// 텍스처의 한 영역을 픽셀 배열로 읽는다.
    ///
    /// ■ 왜 그냥 GetPixels 를 쓰지 않나
    ///   임포트된 PNG 는 Read/Write Enabled 가 꺼져 있는 것이 기본이라
    ///   GetPixels 가 예외를 던진다
    ///   ("texture data is either not readable, corrupted or does not exist").
    ///
    ///   비인간형 몬스터의 스프라이트가 정확히 그 경우다 —
    ///   Bonus/Monsters 의 SpriteSheet.png 는 그냥 임포트된 이미지다.
    ///
    /// ■ 왜 임포트 설정을 켜지 않나
    ///   Read/Write 를 켜면 Unity 가 CPU 사본을 따로 들고 있어 그 텍스처의
    ///   메모리가 두 배가 된다. 초상화 한 번 만들자고 시트를 상시 두 벌 들 이유가 없다.
    ///
    /// ■ 어떻게 우회하나
    ///   GPU 로 복사한 뒤(Blit) RenderTexture 에서 읽는다. 이 경로는 CPU 읽기
    ///   권한과 무관하다. 읽기 가능한 텍스처(런타임 합성 시트)는 그냥 직접 읽는다.
    /// </summary>
    static Color[] ReadRegion(Texture2D src, int x, int y, int w, int h)
    {
        if (src == null || w <= 0 || h <= 0) return null;

        // 빠른 길 — 런타임에 만든 텍스처는 읽을 수 있다.
        if (src.isReadable) return src.GetPixels(x, y, w, h);

        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;   // 픽셀아트라 뭉개지면 안 된다

        RenderTexture prev = RenderTexture.active;

        // 원본에서 잘라 낼 영역을 비율로 환산해 통째로 옮긴다.
        var scale  = new Vector2((float)w / src.width, (float)h / src.height);
        var offset = new Vector2((float)x / src.width, (float)y / src.height);

        Graphics.Blit(src, rt, scale, offset);

        RenderTexture.active = rt;

        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tmp.Apply(false, false);

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color[] px = tmp.GetPixels();
        Destroy(tmp);

        return px;
    }

    // ── 자르기 ───────────────────────────────────────────────

    /// <summary>
    /// 투명 여백을 걷어내고 독립 텍스처로 굽는다.
    /// 칸 안에서 몬스터가 최대한 크게 보이도록 하는 것이 목적이다.
    ///
    /// ■ <paramref name="tint"/> — 종족 색조 (MonsterSpeciesData.BodyTint)
    ///   전장에서 물들여 놓고 초상화만 원래 색이면 카드·도감·전황에서
    ///   힐 슬라임과 강철 슬라임이 다시 같은 그림이 된다. 같은 값을 곱해 둔다.
    ///   ⚠ 알파는 건드리지 않는다 — 여백 판정(a > 0.01)과 잘라 낸 모양이 달라진다.
    ///   ⚠ 덩치(BodyScale)는 반영하지 않는다 — 여기서 여백을 걷어내 칸에 꽉 채우므로
    ///     크기를 곱해도 결과가 같다. 덩치는 전장에서만 읽히는 축이다.
    /// </summary>
    // ── 초상화에도 덩치가 보여야 한다 (사용자 지시, 2026-09-15) ────
    //
    //  ■ 예전에는 여백을 바짝 걷어내 칸을 꽉 채웠다
    //    그래서 **전 종족이 같은 크기로 보였다.** 고블린(0.85)과 슬라임 킹(2.0)이
    //    도감에서 나란히 같은 덩치라, 전장에서 두 배 차이인 것이 카드에서는
    //    아무 데도 안 남았다. 덩치는 이 게임이 종족을 가르는 두 축 중 하나인데
    //    (다른 하나가 색조) 한 축이 화면의 절반에서 통째로 사라져 있었다.
    //
    //  ■ 잘라낸 그림을 **투명 여백으로 다시 감싼다** — 그림을 키우지 않는다
    //    픽셀 아트라 확대는 뭉개지고 축소는 픽셀이 깨진다. 대신 작은 종족일수록
    //    여백을 넓게 둘러 같은 칸 안에서 작게 보이게 한다. 리샘플링이 없다.
    //
    //  ⚠ 선형이 아니라 눌러서 매핑한다 (PortraitScaleCurve)
    //    그대로 비율을 쓰면 고블린이 칸의 42% 로 쪼그라들어 무엇인지 안 읽힌다.
    //    0.65 제곱이면 57% 로 올라오면서 순서는 그대로 유지된다 —
    //    "작다" 를 전하되 "안 보인다" 로 가지 않는 선이다.
    //
    //  ⚠ 기준값을 넘는 종족이 생기면 그 종족만 칸을 넘어 보인다
    //    MonsterCodexCreator 의 검산이 굽는 순간 잡는다.

    /// <summary>초상화 크기의 기준이 되는 가장 큰 덩치. 이 종족이 칸을 꽉 채운다.</summary>
    public const float PortraitMaxBodyScale = 2.0f;

    /// <summary>덩치 → 칸 점유율의 눌림 정도. 1 이면 그대로, 작을수록 작은 종족이 커진다.</summary>
    public const float PortraitScaleCurve = 0.65f;

    /// <summary>그 덩치가 초상화 칸에서 차지하는 비율 (0~1).</summary>
    public static float PortraitFillFor(float bodyScale)
    {
        float ratio = Mathf.Clamp01(bodyScale / PortraitMaxBodyScale);
        if (ratio <= 0f) return 1f;

        return Mathf.Pow(ratio, PortraitScaleCurve);
    }

    // ── 머리 위 표식 — 전장과 같은 그림을 초상화에도 얹는다 (2026-09-15) ──
    //
    //  ■ 표식이 전장에만 있었다
    //    힐 슬라임의 십자도, 독 슬라임의 물방울도, 왕관도 카드·도감·전황에는
    //    안 나왔다. 덱을 짤 때 보는 그림과 전장에서 보는 그림이 서로 다른 말을 했다.
    //
    //  ⚠ 도트는 MonsterMarkView 가 정본이다 — 여기서 다시 그리지 않는다
    //    두 벌이 되면 왕관을 고칠 때 한쪽만 고쳐진다.
    //
    //  ⚠ 몸 여백(덩치 반영)보다 **먼저** 합성한다
    //    표식도 몸과 함께 커지고 작아져야 한다. 뒤에 얹으면 덩치가 두 배인 왕의
    //    왕관만 그대로여서 머리 위에 점처럼 앉는다 (전장에서 이미 한 번 겪은 함정).

    //  ■ 크기·높이는 전장 값(MonsterMarkView.MarkSize/MarkHeight)을 원본 픽셀로 옮긴다 (사용자 지적, 2026-09-15)
    //    한때 몸통 폭 대비 비율(0.55)에 머리 위 간격(6%)을 따로 두고, 왕관 배율을 정수 1 이상으로만 잡았다.
    //    슬라임 몸통은 17px 뿐이라 11px 왕관이 1배로도 몸의 65% 가 됐다(전장은 28%).
    //    간격까지 떠서 초상화에서만 "크고 공중에 뜬" 왕관이었다.
    //  ⚠ 왕관을 줄일 수는 없다(1배 아래는 도트가 깨진다) — 대신 **몸통을 정수배로 키워** 비율을 맞춘다.

    /// <summary>몸통을 키울 수 있는 최대 정수배. 크게 둘수록 비율이 정확하고 텍스처가 커진다.</summary>
    const int MaxMarkBodyZoom = 8;

    /// <summary>
    /// 잘라낸 몸통 위에 표식을 얹은 새 버퍼를 돌려준다. 표식이 없으면 원본 그대로.
    /// <paramref name="ppu"/> 원본 스프라이트의 유닛당 픽셀 · <paramref name="frameMinY"/> 버퍼 0행이 원본 프레임의 몇 행인가(발밑 = 0).
    /// </summary>
    static Color[] ComposeMark(Color[] body, ref int w, ref int h, MonsterMark mark,
                               float ppu, int frameMinY)
    {
        if (!MonsterMarkView.TryGetPixels(mark, out Color32[] art, out int aw, out int ah))
            return body;

        // 전장의 표식 폭을 원본 픽셀로 — 몸통 k배 · 표식 z배 조합 중 가장 가까운 것
        float targetW = MonsterMarkView.MarkSize * ppu;
        int   k = 1, zoom = 1;
        float bestErr = float.MaxValue;
        for (int bk = 1; bk <= MaxMarkBodyZoom; bk++)
        {
            int   bz  = Mathf.Max(1, Mathf.RoundToInt(targetW * bk / aw));
            float err = Mathf.Abs((float)aw * bz / bk - targetW) / targetW;
            if (err < bestErr) { bestErr = err; k = bk; zoom = bz; }
            if (err <= 0.05f) break;
        }

        int bw = w * k, bh = h * k;
        int mw = aw * zoom;
        int mh = ah * zoom;

        // 전장과 같은 높이 — 발밑(프레임 0행)에서 MarkHeight. 머리에 살짝 걸쳐 앉는다.
        int markY = Mathf.Max(0, Mathf.RoundToInt((MonsterMarkView.MarkHeight * ppu - frameMinY) * k));

        int cw = Mathf.Max(bw, mw);
        int ch = Mathf.Max(bh, markY + mh);

        var canvas = new Color[cw * ch];   // 기본값이 투명이다

        int bodyX = (cw - bw) / 2;
        for (int y = 0; y < bh; y++)
        for (int x = 0; x < bw; x++)
            canvas[y * cw + bodyX + x] = body[(y / k) * w + x / k];   // 몸통은 아래에 (최근접 확대)

        int markX = (cw - mw) / 2;

        for (int y = 0; y < mh; y++)
        for (int x = 0; x < mw; x++)
        {
            Color32 c = art[(y / zoom) * aw + (x / zoom)];
            if (c.a == 0) continue;

            canvas[(markY + y) * cw + markX + x] = c;
        }

        w = cw; h = ch;
        return canvas;
    }

    static Sprite CropAndTrim(Color[] px, int fw, int fh, Color tint, float bodyScale,
                              MonsterMark mark, float ppu)
    {
        int minX = fw, maxX = -1, minY = fh, maxY = -1;

        for (int y = 0; y < fh; y++)
        for (int x = 0; x < fw; x++)
            if (px[y * fw + x].a > 0.01f)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        if (maxX < minX || maxY < minY) return null;   // 전부 투명 = 합성 실패

        const int Pad = 1;
        minX = Mathf.Max(0,      minX - Pad);
        minY = Mathf.Max(0,      minY - Pad);
        maxX = Mathf.Min(fw - 1, maxX + Pad);
        maxY = Mathf.Min(fh - 1, maxY + Pad);

        int w = maxX - minX + 1, h = maxY - minY + 1;

        var sub = new Color[w * h];
        for (int y = 0; y < h; y++)
            Array.Copy(px, (minY + y) * fw + minX, sub, y * w, w);

        if (tint != Color.white)
            for (int i = 0; i < sub.Length; i++)
            {
                Color c = sub[i];
                sub[i]  = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            }

        // ⚠ 표식은 **색조 뒤**에 얹는다 — 왕관까지 종족 색으로 물들면 금색이 죽는다
        sub = ComposeMark(sub, ref w, ref h, mark, ppu, minY);

        // ── 덩치만큼 투명 여백을 두른다 ──
        //   ⚠ 가로·세로에 **같은 배율**을 건다. 따로 잡으면 비율이 틀어져
        //     preserveAspect 가 켜진 칸에서 종족이 늘어나 보인다.
        //
        //   ⚠ 캔버스는 **정사각형**이고 몸은 **바닥에 붙인다** (사용자 지적, 2026-09-15)
        //     예전엔 가로·세로를 따로 늘리고 가운데 정렬했다. 초상화 칸은 정사각형 +
        //     preserveAspect 라 납작한 그림(슬라임)은 칸 가운데에, 길쭉한 그림은 칸을 꽉 채워
        //     도감에서 종족마다 발밑 높이가 제각각이었다. 정사각형이면 칸과 비율이 같아
        //     그림이 칸을 정확히 채우고, oy = 0 이라 전 종족의 발이 칸 바닥에 선다.
        //     크기는 그대로다 — preserveAspect 는 원래도 긴 변으로 칸에 맞췄다.
        float fill = PortraitFillFor(bodyScale);
        int   side = Mathf.Max(Mathf.Max(w, h), Mathf.RoundToInt(Mathf.Max(w, h) / fill));

        int canvasW = side;
        int canvasH = side;

        var canvas = new Color[canvasW * canvasH];   // 기본값이 투명(0,0,0,0)이다

        int ox = (canvasW - w) / 2;   // 가로는 가운데
        const int oy = 0;             // 세로는 바닥 기준

        for (int y = 0; y < h; y++)
            Array.Copy(sub, y * w, canvas, (oy + y) * canvasW + ox, w);

        var tex = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };
        tex.SetPixels(canvas);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, canvasW, canvasH),
                             new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
    }
}
