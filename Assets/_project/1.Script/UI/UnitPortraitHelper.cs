using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using Assets.PixelFantasy.Common.Scripts.Utils;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  UnitPortraitHelper.cs
//  장군 초상화 렌더링 공통 유틸.
//
//  HeroPanelUI / HeroCardUI / ExpRowUI 등에서 공유.
// ============================================================

public static class UnitPortraitHelper
{
    public static readonly Color[] JobBgColors =
    {
        new Color(0.50f, 0.14f, 0.14f),  // Knight
        new Color(0.14f, 0.45f, 0.18f),  // Archer
        new Color(0.16f, 0.27f, 0.56f),  // Mage
        new Color(0.18f, 0.38f, 0.48f),  // ShieldBearer
    };

    /// <summary>
    /// 장군 초상화를 portraitImage 에 렌더링.
    /// portraitTexture 는 호출자가 보관하며 재사용된다 (ref 로 전달).
    /// </summary>
    public static void Render(string unitName, UnitJob job, UnitGrade grade,
        UnitAppearanceBridge bridge, Image portraitBg, Image portraitImage,
        ref Texture2D portraitTexture)
    {
        if (portraitBg != null)
            portraitBg.color = JobBgColors[Mathf.Clamp((int)job, 0, JobBgColors.Length - 1)];

        if (bridge == null || portraitImage == null) return;

        var builder = bridge.GetComponent<CharacterBuilder>();
        if (builder == null) return;

        if (builder.SpriteCollection == null)
        {
            builder.SpriteCollection = Resources.Load<SpriteCollection>("SpriteCollection");
            if (builder.SpriteCollection == null) return;
        }

        var data = AllyAppearanceRoller.Roll(unitName, job, grade);
        builder.Body    = data.Body;
        builder.Head    = data.Head;
        builder.Ears    = data.Ears;
        builder.Eyes    = data.Eyes;
        builder.Hair    = data.Hair;
        builder.Armor   = data.Armor;
        builder.Helmet  = data.Helmet;
        builder.Mask    = data.Mask;
        builder.Horns   = data.Horns;
        builder.Cape    = data.Cape;
        builder.Weapon  = data.Weapon;
        builder.Shield  = data.Shield;
        builder.Back    = data.Back;
        builder.Firearm = "";

        var layers = builder.BuildLayers();
        if (layers.Count == 0) return;

        if (portraitTexture == null)
            portraitTexture = new Texture2D(576, 928) { filterMode = FilterMode.Point };

        TextureHelper.MergeLayers(portraitTexture, layers.Values.ToArray());
        portraitImage.sprite = ExtractPortraitSprite(portraitTexture);
        portraitImage.color  = Color.white;
    }

    public static Sprite ExtractPortraitSprite(Texture2D texture)
    {
        var l  = CharacterBuilder.Layout["Idle_0"];
        int fx = l[0], fy = l[1], fw = l[2], fh = l[3];

        var pixels = texture.GetPixels(fx, fy, fw, fh);
        int minX = fw, maxX = 0, minY = fh, maxY = 0;

        for (int py = 0; py < fh; py++)
            for (int px = 0; px < fw; px++)
                if (pixels[py * fw + px].a > 0.01f)
                {
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;
                }

        if (minX > maxX || minY > maxY)
            return Sprite.Create(texture, new Rect(fx, fy, fw, fh),
                new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);

        const int pad = 2;
        minX = Mathf.Max(0,      minX - pad);
        minY = Mathf.Max(0,      minY - pad);
        maxX = Mathf.Min(fw - 1, maxX + pad);
        maxY = Mathf.Min(fh - 1, maxY + pad);

        return Sprite.Create(
            texture,
            new Rect(fx + minX, fy + minY, maxX - minX + 1, maxY - minY + 1),
            new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
    }
}
