using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  FillSprite.cs
//  Image.Type.Filled 가 실제로 줄어들게 하는 흰 스프라이트 하나.
//
//  ■ ⚠ 유니티 Image 는 스프라이트가 없으면 Filled 를 **무시한다**
//    Image.OnPopulateMesh 가 sprite == null 이면 그냥 사각형 전체를 그린다.
//    fillAmount 를 아무리 바꿔도 막대가 가득 찬 채로 남는다 — 에러도 경고도 없다.
//    (2026-09-11 사용자 지적: 마왕성·보스 체력 막대가 줄지 않았다)
//
//    Creator 들은 EditorUIBuilder.Img 로 막대를 만드는데 그건 스프라이트를
//    넣지 않는다. 그래서 채움 막대는 런타임에 이 스프라이트를 꽂는다.
//    ⚠ 새 Filled 막대를 만들면 그 컴포넌트의 Awake 에서 Ensure 를 부를 것.
//
//  ■ Texture2D.whiteTexture 를 한 번만 감싼다 — 모든 막대가 같은 스프라이트를
//    공유하므로 배칭이 깨지지 않는다. 색은 Image.color 가 그대로 입힌다.
// ============================================================

public static class FillSprite
{
    static Sprite _white;

    public static Sprite White
    {
        get
        {
            if (_white != null) return _white;

            Texture2D tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), 100f);
            _white.name = "FillSprite.White";
            return _white;
        }
    }

    /// <summary>스프라이트가 비어 있으면 흰 스프라이트를 꽂는다. 이미 있으면 건드리지 않는다.</summary>
    public static void Ensure(Image img)
    {
        if (img.sprite == null) img.sprite = White;
    }
}
