using UnityEngine;

// ============================================================
//  UIJuiceTex.cs
//  연출용 스프라이트 두 장을 코드로 그린다.
//
//  ■ 왜 PNG 를 안 쓰나
//    점 하나와 링 하나뿐이라 파일로 두면 관리 비용이 그림값보다 크다.
//    무엇보다 에셋 참조는 끊긴다 — 참조가 빠지면 연출만 조용히 사라지고
//    "왜 이펙트가 안 나오지" 를 프리팹부터 뒤지게 된다.
//    코드로 만들면 스크립트가 있는 한 반드시 나온다.
//
//  ■ 부드러운 가장자리가 전부다
//    사각형 Image 를 그대로 쓰면 픽셀 덩어리가 튄다.
//    가장자리를 알파로 흐리면 같은 도형이 빛으로 보인다.
//
//  ⚠ 한 번만 만든다
//    파티클 하나당 텍스처를 만들면 강화 한 번에 20장이 생긴다.
//    정적 캐시로 앱 수명 동안 두 장만 존재한다.
// ============================================================

public static class UIJuiceTex
{
    static Sprite _dot;
    static Sprite _ring;

    /// <summary>가운데가 밝고 가장자리로 사라지는 원. 스파크·글로우에 쓴다.</summary>
    public static Sprite Dot => _dot ??= BuildDot(64);

    /// <summary>가운데가 비고 테두리만 빛나는 원. 충격파에 쓴다.</summary>
    public static Sprite Ring => _ring ??= BuildRing(128);

    // ── 생성 ─────────────────────────────────────────────────

    static Sprite BuildDot(int size)
    {
        var tex = NewTex(size);
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;

            // 중심부는 꽉 찬 흰색, 바깥으로 갈수록 제곱으로 사라진다.
            // 선형으로 빼면 테두리가 또렷해서 '원판' 으로 보인다.
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (0.35f + 0.65f * a);

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        return Finish(tex);
    }

    static Sprite BuildRing(int size)
    {
        var tex = NewTex(size);
        float r = size * 0.5f;

        const float Mid   = 0.78f;   // 링이 가장 밝은 반지름 (0~1)
        const float Width = 0.20f;   // 밝은 띠의 두께

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;

            float a = Mathf.Clamp01(1f - Mathf.Abs(d - Mid) / Width);
            a = a * a;                        // 띠 안쪽도 부드럽게
            if (d > 1f) a = 0f;               // 텍스처 밖으로 새는 것 방지

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        return Finish(tex);
    }

    // ── 공통 ─────────────────────────────────────────────────

    static Texture2D NewTex(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            // 파티클은 크게 늘어난다 — Point 로 두면 계단이 그대로 보인다
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave,   // 씬에 딸려 저장되지 않게
        };
    }

    static Sprite Finish(Texture2D tex)
    {
        tex.Apply(false, true);   // makeNoLongerReadable — CPU 사본을 놓아 준다
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
