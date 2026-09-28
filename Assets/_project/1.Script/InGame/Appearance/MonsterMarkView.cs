using UnityEngine;

// ============================================================
//  MonsterMarkView.cs
//  머리 위에 뜨는 작은 표식 — 같은 그림을 쓰는 종족을 한눈에 가른다.
//
//  ■ 왜 필요한가 (사용자 지적, 2026-09-11 "힐 슬라임과 독 슬라임 구분이 어렵다")
//    슬라임 계보 넷은 벤더 그림이 하나(SlugLibrary)뿐이다. 색조·몸 비율로 갈랐지만
//    물량이 뒤엉킨 라인에서는 둘 다 묻힌다. 기호는 크기·색과 무관하게 읽힌다.
//      십자 = 치유 · 물방울 = 독 · 왕관 = 슬라임 킹(소환사)
//
//  ■ 그림은 코드가 찍는다 — 에셋이 없다
//    몇 픽셀짜리 도트라 PNG·임포트 설정을 따로 두는 것보다 여기서 한 번 찍는 편이
//    싸다. 종류마다 스프라이트 하나를 모두가 공유한다 (MonsterGearAuraView 와 같은 규율).
//
//  ■ 몸 비율에 휘지 않는다
//    부모(루트)가 종족 몸 비율(BodyStretch)로 늘어나 있어 그대로 두면 표식이 찌그러진다.
//    매 프레임 부모 배율을 되받아 월드 크기를 고정한다 — 좌우 뒤집힘도 함께 상쇄된다.
//
//  ⚠ 풀에서 재사용된다 — Setup 을 스폰마다 부른다 (MonsterRuntimeBridge.InitializeWithStat).
//    None 이면 끈다. 안 끄면 지난 개체의 표식을 달고 나온다.
// ============================================================

public enum MonsterMark
{
    None  = 0,
    Crown = 1,   // 왕관 — 슬라임 킹
    Cross = 2,   // 십자 — 치유 (힐 슬라임)
    Drop  = 3,   // 물방울 — 독 (독 슬라임)
}

public class MonsterMarkView : MonoBehaviour
{
    /// <summary>루트(발밑) 기준 표식의 높이. 슬라임 머리 위.</summary>
    /// ⚠ 초상화(MonsterPortraitProvider.ComposeMark)가 이 값으로 같은 자리를 잡는다.
    public const float MarkHeight = 0.62f;

    /// <summary>표식의 월드 폭. ⚠ 초상화도 이 값으로 몸 대비 크기를 맞춘다.</summary>
    public const float MarkSize = 0.26f;

    /// <summary>위아래로 살짝 떠 흔들리는 폭과 주기 — 붙박이 스티커처럼 보이지 않게.</summary>
    const float BobAmp   = 0.025f;
    const float BobCycle = 1.6f;

    /// <summary>SortingGroup 안의 자리 — 장비 빛(108)보다 앞, 버프 기둥(110)보다 뒤.</summary>
    const int MarkOrder = 109;

    SpriteRenderer _sr;
    MonsterMark    _mark;
    float          _phase;

    void Awake() => _phase = Random.value;

    /// <summary>표식을 정한다. None 이면 끈다. 스폰할 때마다 부른다.</summary>
    public void Setup(MonsterMark mark)
    {
        _mark = mark;

        if (mark == MonsterMark.None)
        {
            if (_sr != null) _sr.gameObject.SetActive(false);
            return;
        }

        if (_sr == null)
        {
            var go = new GameObject("MonsterMark");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = MarkOrder;
        }

        _sr.sprite = SpriteOf(mark);
        _sr.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        if (_mark == MonsterMark.None || _sr == null) return;

        Transform t = _sr.transform;

        float bob = Mathf.Sin((Time.unscaledTime / BobCycle + _phase) * Mathf.PI * 2f) * BobAmp;

        // 부모 배율을 되받아 월드 크기·방향을 고정한다 (파일 머리 주석)
        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Abs(lossy.x) > 0.0001f ? lossy.x : 1f;
        float sy = Mathf.Abs(lossy.y) > 0.0001f ? lossy.y : 1f;

        // 높이는 부모 공간에서 잡는다 — 몸이 길쭉하면(독 슬라임) 표식도 그만큼 위에 앉는다.
        t.localPosition = new Vector3(0f, MarkHeight + bob / sy, 0f);

        // ── 몸 비율은 상쇄하고 **덩치는 따라간다** (2026-09-15) ──
        //
        //  나누기만 하면(MarkSize / sx) 표식의 월드 크기가 언제나 같다. 그건 Shape
        //  (힐 슬라임 1.20×0.84 처럼 납작·길쭉)로 표식이 찌그러지는 것을 막으려던 것이다.
        //  그런데 슬라임 킹(Size 2.0)처럼 **덩치가 다른** 종족이 생기면서, 몸이 두 배인데
        //  왕관만 그대로여서 머리 위에 점처럼 얹혔다.
        //
        //  기하평균으로 나누면 둘이 갈린다 — Shape 는 곱이 대략 1 이라 상쇄되고
        //  (1.20 × 0.84 = 1.008), Size 는 양쪽에 똑같이 곱해져 그대로 남는다.
        float mean = Mathf.Sqrt(Mathf.Max(0.0001f, sx * sy));

        t.localScale = new Vector3(MarkSize * mean / sx, MarkSize * mean / sy, 1f);
    }

    // ── 도트 그림 ────────────────────────────────────────────
    //   위에서 아래로 한 줄씩. 문자 → 색: . 투명 · 그 외는 Palette.

    static readonly string[] CrownArt =
    {
        "k....k....k",
        "yk..kyk..ky",
        "yyk.yyy.kyy",
        "yyyyyyyyyyy",
        "yrydyyydyby",
        "yyyyyyyyyyy",
        "ddddddddddd",
    };

    static readonly string[] CrossArt =
    {
        "..ppp..",
        "..pwp..",
        "pppwppp",
        "pwwwwwp",
        "pppwppp",
        "..pwp..",
        "..ppp..",
    };

    static readonly string[] DropArt =
    {
        "...o...",
        "..ogo..",
        "..ogo..",
        ".oggGo.",
        "oggggGo",
        "ogwgggo",
        "ogwgggo",
        ".oggggo",
        "..ooo..",
    };

    static Color32 Palette(char c) => c switch
    {
        'y' => new Color32(255, 212,  64, 255),   // 금
        'd' => new Color32(196, 134,  24, 255),   // 짙은 금
        'k' => new Color32(255, 240, 150, 255),   // 금 꼭지(밝게)
        'r' => new Color32(230,  50,  70, 255),   // 루비
        'b' => new Color32( 70, 150, 255, 255),   // 사파이어
        'p' => new Color32(220,  60, 100, 255),   // 십자 테두리
        'w' => new Color32(255, 255, 255, 255),   // 흰 속
        'o' => new Color32( 34,  78,  24, 255),   // 물방울 테두리
        'g' => new Color32(120, 230,  70, 255),   // 독 초록
        'G' => new Color32( 70, 170,  40, 255),   // 독 그늘
        _   => new Color32(  0,   0,   0,   0),
    };

    static readonly Sprite[] _cache = new Sprite[4];

    /// <summary>
    /// 표식의 도트 픽셀 — <b>아래가 0행</b>인 텍스처 순서로 돌려준다.
    ///
    /// ■ 왜 공개하나 — 초상화도 같은 표식을 그려야 한다 (사용자 지적, 2026-09-15)
    ///   표식은 전장에만 있었다. 카드·도감·전황 초상화에는 왕관도 십자도 안 나와서,
    ///   덱을 짤 때 보는 그림과 전장에서 보는 그림이 서로 다른 말을 했다.
    ///   MonsterPortraitProvider 가 이 픽셀을 초상화 위에 합성한다.
    ///
    /// ⚠ 그림의 정본은 여기 하나다 — 초상화 쪽에 도트를 다시 적지 말 것.
    ///   두 벌이 되면 왕관을 고칠 때 한쪽만 고쳐진다.
    /// </summary>
    public static bool TryGetPixels(MonsterMark mark, out Color32[] px, out int w, out int h)
    {
        px = null; w = 0; h = 0;
        if (mark == MonsterMark.None) return false;

        string[] art = ArtOf(mark);

        h = art.Length;
        w = art[0].Length;

        px = new Color32[w * h];
        for (int row = 0; row < h; row++)
        for (int x = 0; x < w; x++)
            px[(h - 1 - row) * w + x] = Palette(art[row][x]);   // 위에서 아래로 적었으니 뒤집는다

        return true;
    }

    static string[] ArtOf(MonsterMark mark) => mark switch
    {
        MonsterMark.Crown => CrownArt,
        MonsterMark.Cross => CrossArt,
        _                 => DropArt,
    };

    static Sprite SpriteOf(MonsterMark mark)
    {
        int i = (int)mark;
        if (_cache[i] != null) return _cache[i];

        if (!TryGetPixels(mark, out Color32[] px, out int w, out int h)) return null;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // 도트가 번지지 않게
            wrapMode   = TextureWrapMode.Clamp,
            name       = $"MonsterMark_{mark}",
        };

        tex.SetPixels32(px);
        tex.Apply(false, true);

        // 폭이 1 유닛이 되게 — 크기는 LateUpdate 의 localScale 이 정한다. 피벗은 아래 가운데.
        _cache[i] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
        _cache[i].name = tex.name;
        return _cache[i];
    }
}
