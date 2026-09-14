using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  AffinityTagUI.cs
//  초상화 왼쪽 아래의 [친화] 표식 — 카드를 늘어놓는 화면들이 함께 쓴다.
//
//  ■ 왜 필요한가 (사용자 요청, 2026-09-11)
//    친화 종족은 할인 · 소환력 ×1.2 · 개성이 걸리는 카드다. 하단 카드 바에는 표식이
//    붙었는데(SummonCardUI.RefreshAffinity) 제단·강화소·상점·카드 3택에는 없어서,
//    "어느 카드를 키울까·바칠까" 를 고르는 바로 그 자리에서 친화가 안 보였다.
//
//  ■ 런타임에 붙인다 — 프리팹을 다시 굽지 않는다
//    화면이 넷이고 초상화 칸 구조가 전부 다르다. Creator 넷을 고치는 대신 초상화 Image
//    아래에 처음 필요해진 순간 한 번 만들고 켜고 끈다 (MonsterMarkView 와 같은 방식).
//    모양은 하단 카드의 [친화] 배지와 같다 — 보라 판 위 흰 글자.
//
//  ■ 판정은 RunPerkRule.IsAffinity 하나다 — 계보(진화체)·특성 '친화 확장' 까지 본다.
//    소환사가 없으면(런 밖) 끈다.
// ============================================================

public static class AffinityTagUI
{
    const string ChildName = "AffinityTag";

    static readonly Color PlateColor = new(0.42f, 0.22f, 0.66f, 0.92f);   // 하단 카드 배지와 같은 색

    /// <summary>
    /// 이 초상화에 [친화] 표식을 켜거나 끈다. species 가 null(스킬·시너지 카드)이면 끈다.
    /// ⚠ 칸이 재사용되므로 **매번** 부른다 — 켜기만 하면 다른 카드로 바뀐 칸에 표식이 남는다.
    /// </summary>
    public static void Set(Image portrait, MonsterSpeciesData species)
    {
        if (portrait == null) return;   // Inspector 연결 칸 — 없는 화면이면 할 일이 없다

        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        bool on = species != null && summoner != null && RunPerkRule.IsAffinity(summoner, species);

        Transform existing = portrait.transform.Find(ChildName);

        if (!on)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }

        GameObject tag = existing != null ? existing.gameObject : Build(portrait.rectTransform);
        tag.SetActive(true);
    }

    /// <summary>초상화 크기에 비례해 왼쪽 아래에 앉힌다 — 칸 크기가 화면마다 달라서다.</summary>
    static GameObject Build(RectTransform portrait)
    {
        var go = new GameObject(ChildName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(portrait, false);

        // 초상화의 왼쪽 아래 — 폭 48% · 높이 24%. 부모 크기를 따라 늘고 준다.
        rt.anchorMin = new Vector2(0f,    0f);
        rt.anchorMax = new Vector2(0.48f, 0.24f);
        rt.offsetMin = new Vector2(4f, 4f);
        rt.offsetMax = Vector2.zero;

        var plate = go.AddComponent<Image>();
        plate.color         = PlateColor;
        plate.raycastTarget = false;   // 칸 버튼의 클릭을 가로채지 않는다

        var textGo = new GameObject("Text", typeof(RectTransform));
        var trt    = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text             = "친화";
        text.color            = Color.white;
        text.fontStyle        = FontStyles.Bold;
        text.alignment        = TextAlignmentOptions.Center;
        text.raycastTarget    = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        // 칸 크기가 화면마다 달라 자동 맞춤이다 — 상한은 UI 규칙 4 의 FontSm
        text.enableAutoSizing = true;
        text.fontSizeMax      = UIScale.FontSm;
        text.fontSizeMin      = 14f;

        return go;
    }
}
