using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  ShopPopupCreator.cs  [Editor Only]
//  Tools > Project K > 프리팹 생성 > 팝업 > 상점
//
//  ■ 전체화면 시설 무대를 쓴다 (FacilityStage)
//    배경 그림이 화면을 덮고, 위아래 어둠 위에만 글이 앉는다.
//    네 시설이 같은 무대를 써야 "같은 세계의 장소" 로 읽힌다.
//
//  ■ ⚠ 칸은 버튼이 아니다 — 칸 **안에** 구매 버튼이 따로 있다
//    (사용자 지적, 2026-09-08) 전에는 칸 전체가 RaisedBtn 이었다. 그러면
//    "어디를 눌러야 사는가" 가 화면에 없고, 못 누르는 상태(골드 부족)와
//    그냥 어두운 카드 면이 구분되지 않는다. 지금은
//      칸 = 평평한 패널(읽는 것)  ·  아래 띠 = 입체 버튼(누르는 것)
//    로 갈라 두었다. 값은 그 버튼 안에 적는다 — 낼 값과 누를 것이 한 몸이다
//    (몬스터 상세의 품질 개선 버튼과 같은 규칙).
//
//  ■ ⚠ 세로 예산을 상수로 쌓는다 — 좌표를 손으로 적지 않는다
//    예전 칸은 글자 자리를 눈대중으로 박아 두어 **실제로 겹쳐 있었다**:
//    카드는 설명(위에서 268)과 값(아래에서 65 = 위에서 235)이 33px 겹쳤고,
//    가로 칸은 설명이 칸 높이(132)를 21px 넘겨 잘렸다. 지금은 아래 CardH /
//    StallH 가 각 조각의 합이라, 조각을 키우면 칸이 저절로 따라 커진다.
//
//  ■ 자리 배치 (아래에서 위로) — 숫자는 상수에서 계산돼 나온 값이다
//      y  44 ~ 366   몬스터 카드 4칸 (250×322)   x 430 ~ 1490
//                    재고 교체 버튼 (300×170)    x 1530 ~ 1830 — 카드 줄 오른쪽, 세로 가운데
//      y 374 ~ 427   "몬스터" 라벨
//      y 447 ~ 729   특성 2 + 마력의 정수 1 (580×282)   x 70 ~ 1850
//      y 737 ~ 790   "특성 · 정수" 라벨
//      y   0 ~ 810   ShopVeil — 공용 어둠(560)이 모자라 위로 이어 붙인 판
//                    ⚠ 위쪽 어둠이 844 에서 시작한다. 34px 남았다
//
//  ⚠ 카드 칸은 세로형, 특성·정수 칸은 가로형이다
//    카드는 그림이 주인공이라 초상화를 크게 세운다. 특성은 **설명을 읽어야**
//    고르는 물건이라 글이 들어갈 가로 폭이 필요하다 (가장 긴 설명이 37자다).
//    같은 모양으로 맞추면 한쪽이 반드시 답답해진다.
// ============================================================

public static class ShopPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/ShopPopup.prefab";
    const string Tag      = "ShopPopupCreator";

    // ── 칸 공통 ──────────────────────────────────────────────

    /// <summary>칸 안쪽 여백. 네 변이 같은 값을 쓴다.</summary>
    const float Pad = 16f;

    /// <summary>구매 버튼 높이. ⚠ 라벨이 눌리지 않는 최소값 (UI 규칙 5).</summary>
    static readonly float BuyH = UIScale.BtnFor(UIScale.FontSm);

    /// <summary>칸 내용과 구매 버튼 사이. 버튼이 내용에 붙으면 한 덩어리로 읽힌다.</summary>
    const float BuyGap = 10f;

    // ── 카드 칸 (세로형) ─────────────────────────────────────
    //
    //   높이 = 여백 + 초상화 + 이름 + 설명 + 틈 + 버튼 + 여백
    //   ⚠ 숫자를 따로 적지 말 것 — 조각을 키우면 칸이 따라 커져야 한다.

    // ⚠ 초상화·아래 여백을 조금 줄였다 (2026-09-10)
    //   특성 설명 줄이 3 → 4 로 늘면서 가로 칸이 42px 높아졌다. 그대로 두면
    //   물건 줄이 위쪽 어둠(844)을 넘어 제목과 부딪힌다 — 아래 Verify 가 잡는다.
    //   초상화 10px, 바닥 여백 6px 를 내주면 16px 가 돌아온다.
    const float CardIcon = 110f;
    const float CardW    = 250f;
    const float CardGap  = 20f;
    const float CardY    = 38f;

    static readonly float CardH = Pad + CardIcon + 6f + UIScale.RowMd + UIScale.RowSm
                                + BuyGap + BuyH + Pad;

    // ── 특성·정수 칸 (가로형) ────────────────────────────────
    //
    //   ⚠ 설명 줄 수는 **가장 긴 특성 설명**이 정한다 (아래 Verify 가 검산한다)
    //     지금 가장 긴 것이 40자다 —
    //     "마나를 한 번도 쓰지 않고 판을 넘기면 최대 마나 +10 (최대 +50)"
    //     ⚠ 두 줄로 줄이지 말 것 — 26자에서 끊긴다. 잘려도 에러가 나지 않는다.
    //     ⚠ 글자를 FontSm 밑으로 줄여 맞추지도 말 것 (UI 규칙 4) —
    //       그게 읽히는 하한이다. 모자라면 줄을 늘리거나 칸을 넓힌다.
    //
    //   ■ 2026-09-10 — 40자가 39자 예산을 넘겨 굽기가 멈췄다
    //     세 손잡이를 함께 돌려 예산을 55자로 벌렸다.
    //       칸 폭  580 → 594   (가로 줄 1822 ≤ 1824 — 남는 2px 가 전부다)
    //       그림    84 →  72   (글 폭이 그만큼 는다. 이 칸의 주인공은 글이다)
    //       줄 수    3 →   4
    //     ⚠ 칸 폭은 더 못 넓힌다 — 셋이 한 줄에 서므로 1824 가 천장이다.
    //       다음에 또 넘치면 **줄 수**를 늘리고, 그만큼 카드 칸에서 세로를 꾼다.

    const float StallIcon = 72f;
    const float StallW    = 594f;
    const float StallGap  = 20f;
    const float StallDescLines = 4f;

    static readonly float StallH = Pad + UIScale.RowMd
                                 + UIScale.Line(UIScale.FontSm) * StallDescLines
                                 + BuyGap + BuyH + Pad;

    /// <summary>가로 칸의 구매 버튼 폭. 카드 것(칸 폭 −여백)보다 좁게 잡는다.</summary>
    const float StallBuyW = 300f;

    // ── 상시 판매 칸 (2026-09-13) ────────────────────────────
    //
    //  정수 하나가 쓰던 594 짜리 자리에 **셋을 세로로 쌓는다.**
    //  ⚠ 가로로 늘릴 수 없다 — 특성 2 + 이 자리 = 1822 로 화면(1824)이 이미 꽉 찼다.
    //    그래서 넓히는 대신 한 줄을 납작하게 만들어 셋을 넣는다.
    //  ⚠ 줄 높이는 **가로 칸 높이에서 나눈다** — 특성 칸과 아랫변·윗변이 맞아야
    //    한 줄로 읽힌다. 특성 설명이 길어져 StallH 가 커지면 여기도 따라 커진다.

    const float BoonGap  = 6f;
    const float BoonIcon = 56f;
    const float BoonBuyW = 190f;

    static readonly float BoonRowH = Mathf.Floor((StallH - BoonGap * (ShopPopup.BoonStalls - 1))
                                                 / ShopPopup.BoonStalls);

    // ── 재고 교체 버튼 ───────────────────────────────────────
    //
    //  ⚠ 자리는 **카드 줄 오른쪽 옆, 카드와 같은 세로 가운데**다 (사용자 지적, 2026-09-15)
    //    한때 "몬스터" 라벨 줄 오른쪽 끝에 구매 버튼과 같은 갈색·같은 높이로 서 있었다.
    //    라벨 줄 높이에 눌린 납작한 띠라 **안 보였다.** 지금은 카드 옆 빈 자리에
    //    크게 세우고, 면 색도 구매 버튼(갈색)과 다른 청록이다 — 사는 것이 아니라
    //    진열을 바꾸는 버튼이라는 것이 색으로 갈린다.
    //  ⚠ 카드 줄이 감춰지면(살 카드가 없음) 비어 버린 카드 줄 **한가운데**로 옮긴다.
    //  ⚠ 지갑(오른쪽 위) 옆에 두지 말 것 — 지갑은 읽는 것이고 이건 누르는 것이다.
    //    같은 자리에 두면 값을 확인하려다 재고를 갈아 치우게 된다.

    const float RerollW   = 300f;
    const float RerollH   = 170f;
    const float RerollGap = 40f;

    static readonly Color RerollFace = new(0.10f, 0.34f, 0.40f);

    // ── 줄 자리 ──────────────────────────────────────────────

    const float LabelGap = 8f;
    const float RowGap   = 20f;

    static readonly float CardLabelY  = CardY + CardH + LabelGap;
    static readonly float StallY      = CardLabelY + UIScale.RowMd + RowGap;
    static readonly float StallLabelY = StallY + StallH + LabelGap;

    /// <summary>
    /// 상점만 쓰는 어둠 판 — 공용 아래 어둠(560)이 끝나는 곳에서 위로 이어 붙인다.
    ///
    /// ⚠ 공용 <see cref="FacilityStage.BottomVeilH"/> 를 키우지 않는다
    ///   그 값은 네 시설이 같이 쓴다. 상점만 물건이 두 줄이라 더 필요한 것이므로
    ///   여기서 content 안에 따로 짓는다 (CLAUDE.md 시설 화면 항목의 규칙).
    ///
    /// ⚠ 겹치지 않고 **맞붙인다** — 같은 색을 겹쳐 깔면 그 구간만 짙어져
    ///   560 자리에 가로 줄이 생긴다. 560 에서 시작해 위로만 뻗는다.
    /// </summary>
    static readonly float ShopVeilTop = StallLabelY + UIScale.RowMd + RowGap;

    static readonly Color CardFace    = new(0.115f, 0.125f, 0.215f, 0.97f);
    static readonly Color PerkFace    = new(0.135f, 0.115f, 0.205f, 0.97f);
    static readonly Color EssenceFace = new(0.105f, 0.145f, 0.215f, 0.97f);
    static readonly Color SoldVeil    = new(0.02f,  0.02f,  0.04f,  0.82f);

    /// <summary>구매 버튼 면 — 셋 다 같은 색이다. 누를 것은 하나로 보여야 한다.</summary>
    static readonly Color BuyFace = new(0.24f, 0.19f, 0.10f);

    /// <summary>칸 테두리 — 어두운 배경 위에서 칸의 경계를 짚어 준다.</summary>
    static readonly Color CardEdge = new(1f, 0.93f, 0.72f, 0.16f);

    /// <summary>
    /// 상시 판매 줄의 면 색 — 마나(푸름) · 공격(붉음).
    /// 순서는 ShopPopup.BoonEssence / BoonWarFund 다.
    ///
    /// ⚠ 칸 수(ShopPopup.BoonStalls)와 길이가 같아야 한다 — 짧으면 굽다가 터진다.
    /// ⚠ <b>반드시 EssenceFace 선언 뒤에 둔다</b> — 정적 필드 초기화는 적힌 순서로 돈다.
    ///   위쪽 상수 블록에 두면 EssenceFace 가 아직 기본값(투명)이라, 에러 없이
    ///   첫 줄만 배경이 비쳐 보인다.
    /// </summary>
    static readonly Color[] BoonFaces =
    {
        EssenceFace,
        new(0.185f, 0.115f, 0.105f, 0.97f),
        new(0.095f, 0.175f, 0.120f, 0.97f),
        new(0.150f, 0.105f, 0.215f, 0.97f),   // 마나 회복 포션 — 보랏빛 (ShopPopup.BoonPotion)
    };

    [MenuItem(ProjectKMenu.Popup + "상점", priority = ProjectKMenu.PrefabPrio + 53)]
    public static void Run()
    {
        // ⚠ 그림이 없으면 굽지 않는다 — 빈 배경을 구워 두면 프리팹만 보고는
        //   무엇이 빠졌는지 알 수 없다 (FacilityPopupCreator 와 같은 계약).
        if (!RunNodeArtAssets.TryLoad(Tag, out Sprite[] nodeArt)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon)) return;

        var root  = new GameObject("ShopPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<ShopPopup>();

        FacilityStage.Build(root, out Image art, out TextMeshProUGUI title,
                            out TextMeshProUGUI flavor, out GameObject content,
                            out Button closeBtn);

        // ⚠ 지갑은 아이콘 배지다 — "6,020 G" 처럼 글자를 붙이지 않는다 (UI 규칙 7)
        TextMeshProUGUI purse = FacilityStage.PurseBadge(root, goldIcon, "Purse");

        // ── 어둠 이어 붙이기 — 물건보다 먼저 만들어 뒤에 깔린다 ──
        BuildShopVeil(content);

        // ── 몬스터 카드 줄 ──
        float totalCardW = ShopPopup.MaxCardStalls * CardW
                         + (ShopPopup.MaxCardStalls - 1) * CardGap;
        float cardX = (1920f - totalCardW) * 0.5f;

        TextMeshProUGUI cardLabel =
            FacilityStage.SectionLabel(content, "CardLabel", "몬스터", cardX, CardLabelY);

        // ⚠ 카드 줄 오른쪽 옆 · 카드와 같은 세로 가운데 — 줄 폭이 바뀌면 함께 따라간다.
        float rerollY = CardY + (CardH - RerollH) * 0.5f;
        var rerollWithCards = new Vector2(cardX + totalCardW + RerollGap, rerollY);

        Button rerollBtn = BuildRerollButton(
            content, rerollWithCards.x, rerollWithCards.y,
            goldIcon, out TextMeshProUGUI rerollPrice);

        var cardStalls = new List<ShopPopup.StallView>(ShopPopup.MaxCardStalls);
        for (int i = 0; i < ShopPopup.MaxCardStalls; i++)
            cardStalls.Add(BuildCardStall(content, i,
                                          cardX + i * (CardW + CardGap), CardY, goldIcon));

        // ── 특성 + 정수 줄 ──
        //   ⚠ 정수를 특성 옆에 둔다 — 셋 다 "값을 치르고 얻는 것" 이라
        //     한 줄에 있어야 무엇과 무엇을 저울질하는지가 보인다.
        float totalStallW = (ShopPopup.MaxPerkStalls + 1) * StallW
                          + ShopPopup.MaxPerkStalls * StallGap;
        float stallX = (1920f - totalStallW) * 0.5f;

        FacilityStage.SectionLabel(content, "PerkLabel", "특성 · 보급품", stallX, StallLabelY);

        var perkStalls = new List<ShopPopup.StallView>(ShopPopup.MaxPerkStalls);
        for (int i = 0; i < ShopPopup.MaxPerkStalls; i++)
            perkStalls.Add(BuildWideStall(content, $"PerkStall{i}", PerkFace,
                                          stallX + i * (StallW + StallGap), StallY, goldIcon));

        // ── 상시 판매 셋 — 정수가 쓰던 자리에 세로로 쌓는다 ──
        //   ⚠ 배열 순서가 곧 화면 순서다 (ShopPopup.BoonEssence/WarFund/Rampart).
        float boonX = stallX + ShopPopup.MaxPerkStalls * (StallW + StallGap);

        var boonStalls = new List<ShopPopup.StallView>(ShopPopup.BoonStalls);
        for (int i = 0; i < ShopPopup.BoonStalls; i++)
        {
            float rowY = StallY + StallH - BoonRowH - i * (BoonRowH + BoonGap);
            boonStalls.Add(BuildBoonStall(content, $"BoonStall{i}", BoonFaces[i],
                                          boonX, rowY, goldIcon));
        }

        // ── 배선 ──
        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.Shop, Tag);
        EditorUIBuilder.SetObj(so, "_art",        art,      Tag);
        EditorUIBuilder.SetObj(so, "_titleText",  title,    Tag);
        EditorUIBuilder.SetObj(so, "_flavorText", flavor,   Tag);
        EditorUIBuilder.SetObj(so, "_purseText",  purse,    Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",   closeBtn, Tag);
        EditorUIBuilder.SetObj(so, "_rerollBtn",   rerollBtn,   Tag);
        EditorUIBuilder.SetObj(so, "_rerollPrice", rerollPrice, Tag);
        EditorUIBuilder.SetObj(so, "_cardLabel",   cardLabel,   Tag);

        // ⚠ 카드 줄이 감춰졌을 때 재고 교체 버튼이 갈 자리 — **여기가 정본이다**
        //   비어 버린 카드 줄의 한가운데. 런타임(ShopPopup.Refresh)은 둘 중 하나를
        //   고르기만 한다 — 좌표를 저쪽에 적으면 칸 크기를 바꾼 날 한쪽만 옛 자리에 남는다.
        var rerollNoCards = new Vector2((1920f - RerollW) * 0.5f, rerollY);

        so.FindProperty("_rerollPosWithCards").vector2Value = rerollWithCards;
        so.FindProperty("_rerollPosNoCards")  .vector2Value = rerollNoCards;
        EditorUIBuilder.SetObjArray(so, "_nodeArt", nodeArt, Tag);

        WriteStalls(so, "_cardStalls", cardStalls);
        WriteStalls(so, "_perkStalls", perkStalls);
        WriteStalls(so, "_boonStalls", boonStalls);

        so.ApplyModifiedPropertiesWithoutUndo();

        Verify(cardStalls, perkStalls, boonStalls);

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);

        Debug.Log($"[{Tag}] 저장: {SavePath}\n" +
                  $"카드 칸 {CardW}×{CardH} · 가로 칸 {StallW}×{StallH} · " +
                  $"어둠 {ShopVeilTop:0}\n" +
                  "⚠ PopupManager 의 [Load Popup Prefabs] 를 눌러야 열립니다.");
    }

    // ── 어둠 이어 붙이기 ─────────────────────────────────────

    /// <summary>
    /// 공용 아래 어둠이 끝나는 곳(560)부터 <see cref="ShopVeilTop"/> 까지를 덮는다.
    /// 물건이 두 줄이라 공용 판만으로는 위쪽 줄이 배경 그림 위에 뜬다.
    /// </summary>
    static void BuildShopVeil(GameObject content)
    {
        // 이미 충분하면 판을 만들지 않는다 — 공용 값이 커지면 저절로 사라진다.
        if (ShopVeilTop <= FacilityStage.BottomVeilH) return;

        var veil = EditorUIBuilder.Img(content, "ShopVeil", FacilityStage.VeilBottom);
        veil.raycastTarget = false;

        var rt = veil.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, FacilityStage.BottomVeilH);
        rt.offsetMax = new Vector2(0f, ShopVeilTop);
    }

    // ── 카드 칸 (세로형) ─────────────────────────────────────

    static ShopPopup.StallView BuildCardStall(GameObject parent, int index, float x, float y,
                                              Sprite goldIcon)
    {
        GameObject stall = Panel(parent, $"CardStall{index}", CardFace, x, y, CardW, CardH);

        // 초상화 — 카드는 그림이 주인공이다.
        var icon = EditorUIBuilder.Img(stall, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        {
            var r = icon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -Pad);
            r.sizeDelta        = new Vector2(CardIcon, CardIcon);
        }

        float nameTop = Pad + CardIcon + 6f;

        TextMeshProUGUI name = CenterLabel(stall, "NameText", UIScale.FontMd, FontStyles.Bold,
                                           Color.white, nameTop, UIScale.RowMd);

        TextMeshProUGUI desc = CenterLabel(stall, "DescText", UIScale.FontSm, FontStyles.Normal,
                                           FacilityColors.Sub,
                                           nameTop + UIScale.RowMd, UIScale.RowSm);

        // 구매 버튼 — 칸 아래 띠 전체. 여기가 "누르는 곳" 이다.
        Button buy = BuyButton(stall, CardW - Pad * 2f, goldIcon, out TextMeshProUGUI price);

        return Finish(stall, buy, icon, name, desc, price);
    }

    // ── 특성·정수 칸 (가로형) ────────────────────────────────

    static ShopPopup.StallView BuildWideStall(GameObject parent, string name, Color face,
                                              float x, float y, Sprite goldIcon)
    {
        GameObject stall = Panel(parent, name, face, x, y, StallW, StallH);

        var icon = EditorUIBuilder.Img(stall, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        {
            var r = icon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(Pad, -Pad);
            r.sizeDelta        = new Vector2(StallIcon, StallIcon);
        }

        float textLeft = Pad + StallIcon + Pad;

        var nameTmp = EditorUIBuilder.TMP(stall, "NameText", "", UIScale.FontMd, FontStyles.Bold);
        {
            var r = nameTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.offsetMin = new Vector2(textLeft, -(Pad + UIScale.RowMd));
            r.offsetMax = new Vector2(-Pad, -Pad);
        }
        nameTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ 설명은 두 줄까지 접힌다 — 가장 긴 특성 설명이 37자다.
        //   글 폭은 칸 폭에서 그림·여백만 뺀 값이다. 구매 버튼이 옆이 아니라
        //   아래에 있어 여기를 좁힐 이유가 없다.
        var descTmp = EditorUIBuilder.TMP(stall, "DescText", "", UIScale.FontSm, FontStyles.Normal);
        {
            float descTop = Pad + UIScale.RowMd;
            var r = descTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0f, 1f);
            r.offsetMin = new Vector2(textLeft,
                                      -(descTop + UIScale.Line(UIScale.FontSm) * StallDescLines));
            r.offsetMax = new Vector2(-Pad, -descTop);
        }
        descTmp.alignment     = TextAlignmentOptions.TopLeft;
        descTmp.color         = FacilityColors.Sub;
        descTmp.raycastTarget = false;

        Button buy = BuyButton(stall, StallBuyW, goldIcon, out TextMeshProUGUI price);

        return Finish(stall, buy, icon, nameTmp, descTmp, price);
    }

    // ── 상시 판매 칸 (납작한 가로 줄) ────────────────────────

    /// <summary>
    /// 상시 판매 한 줄 — [그림] [이름 / 효과] ······ [구매].
    ///
    /// ⚠ 설명은 <b>한 줄</b>이다. 칸이 납작해 두 줄이 되면 아래가 잘린다 —
    ///   산 횟수는 설명이 아니라 <b>이름 옆</b>에 붙인다 (ShopPopup.Stacked).
    /// ⚠ 값은 버튼 안이다 (다른 칸과 같은 규칙 — 낼 값과 누를 것이 한 몸).
    /// </summary>
    static ShopPopup.StallView BuildBoonStall(GameObject parent, string name, Color face,
                                              float x, float y, Sprite goldIcon)
    {
        GameObject stall = Panel(parent, name, face, x, y, StallW, BoonRowH);

        var icon = EditorUIBuilder.Img(stall, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        {
            var r = icon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(Pad, 0f);
            r.sizeDelta        = new Vector2(BoonIcon, BoonIcon);
        }

        float textLeft  = Pad + BoonIcon + 12f;
        float textRight = Pad + BoonBuyW + 12f;

        // 이름 — 위 절반
        var nameTmp = EditorUIBuilder.TMP(stall, "NameText", "", UIScale.FontSm, FontStyles.Bold);
        {
            var r = nameTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(textLeft, 0f);
            r.offsetMax = new Vector2(-textRight, -6f);
        }
        nameTmp.alignment        = TextAlignmentOptions.BottomLeft;
        nameTmp.color            = Color.white;
        nameTmp.raycastTarget    = false;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // 효과 — 아래 절반
        var descTmp = EditorUIBuilder.TMP(stall, "DescText", "", UIScale.FontSm, FontStyles.Normal);
        {
            var r = descTmp.rectTransform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0.5f);
            r.offsetMin = new Vector2(textLeft, 6f);
            r.offsetMax = new Vector2(-textRight, 0f);
        }
        descTmp.alignment        = TextAlignmentOptions.TopLeft;
        descTmp.color            = FacilityColors.Sub;
        descTmp.raycastTarget    = false;
        descTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // 구매 버튼 — 오른쪽 끝
        Button buy = EditorUIBuilder.RaisedBtn(stall, "BuyBtn", BuyFace, out GameObject body);
        {
            var r = buy.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot     = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(-Pad, 0f);
            r.sizeDelta        = new Vector2(BoonBuyW, BuyH);
        }

        GameObject badge = FacilityStage.Badge(body, "PriceBadge", goldIcon, 36f,
                                               UIScale.FontSm, 88f, FacilityColors.Gold,
                                               out TextMeshProUGUI price);
        {
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot     = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
        }

        return Finish(stall, buy, icon, nameTmp, descTmp, price);
    }

    // ── 공용 조각 ────────────────────────────────────────────

    /// <summary>
    /// 칸의 몸통 — <b>평평한 패널이다</b>. 누르는 것은 안의 구매 버튼뿐이다.
    ///
    /// ⚠ 테두리는 자식이 아니라 **앞 형제**로 깔지 않아도 된다
    ///   여기서는 반투명 테두리를 네 변의 얇은 막대로 그린다 (UI 규칙 3 은
    ///   테두리를 한 장의 큰 반투명 판으로 깔 때의 이야기다).
    /// </summary>
    static GameObject Panel(GameObject parent, string name, Color face,
                            float x, float y, float w, float h)
    {
        Image img = EditorUIBuilder.Img(parent, name, face);

        // 뒤쪽 전장·배경 그림이 눌리지 않게 막는다. 칸 자체는 아무 일도 하지 않는다.
        img.raycastTarget = true;

        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);

        Edge(img.gameObject, "EdgeTop",    0f, 1f, 1f, 1f,  0f, -1f, 0f, 2f);
        Edge(img.gameObject, "EdgeBottom", 0f, 0f, 1f, 0f,  0f,  1f, 0f, 2f);
        Edge(img.gameObject, "EdgeLeft",   0f, 0f, 0f, 1f,  1f,  0f, 2f, 0f);
        Edge(img.gameObject, "EdgeRight",  1f, 0f, 1f, 1f, -1f,  0f, 2f, 0f);

        return img.gameObject;
    }

    static void Edge(GameObject parent, string name,
                     float aMinX, float aMinY, float aMaxX, float aMaxY,
                     float px, float py, float w, float h)
    {
        var img = EditorUIBuilder.Img(parent, name, CardEdge);
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(aMinX, aMinY);
        rt.anchorMax = new Vector2(aMaxX, aMaxY);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(px, py);
        rt.sizeDelta        = new Vector2(w, h);
    }

    /// <summary>
    /// 칸 아래에 붙는 구매 버튼 — <b>값이 이 안에 있다</b>.
    ///
    /// ⚠ 라벨은 반드시 body 아래에 넣는다 (UI 규칙 1) — 루트에 넣으면
    ///   눌러도 라벨만 제자리에 남아 버튼이 안 눌린 것처럼 보인다.
    /// </summary>
    static Button BuyButton(GameObject stall, float width, Sprite goldIcon,
                            out TextMeshProUGUI price)
    {
        Button btn = EditorUIBuilder.RaisedBtn(stall, "BuyBtn", BuyFace, out GameObject body);

        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, Pad);
        rt.sizeDelta        = new Vector2(width, BuyH);

        // ⚠ 값은 [금화][숫자] 다 — 글자 "G" 를 쓰지 않는다 (UI 규칙 7)
        //   배지 전체를 버튼 가운데에 놓는다. 아이콘 폭까지 세어야 가운데가 맞는다.
        // ⚠ 아이콘은 글자보다 크다 — 작으면 무슨 그림인지 못 알아본다 (2026-09-09)
        GameObject badge = FacilityStage.Badge(body, "PriceBadge", goldIcon, 40f,
                                               UIScale.FontSm, 92f, FacilityColors.Gold,
                                               out price);
        {
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot     = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
        }

        return btn;
    }

    /// <summary>
    /// 재고 교체 버튼 — 위 [재고 교체] / 아래 [금화][값] 두 줄.
    ///
    /// ⚠ 라벨을 반드시 적는다. 구매 버튼처럼 값만 두면
    ///   무엇을 사는 버튼인지 알 수 없다 — 이 버튼은 물건 칸 밖에 홀로 선다.
    /// ⚠ 라벨·배지는 반드시 body 아래에 넣는다 (UI 규칙 1) — 루트에 넣으면
    ///   눌러도 같이 안 내려간다.
    /// </summary>
    static Button BuildRerollButton(GameObject parent, float x, float y, Sprite goldIcon,
                                    out TextMeshProUGUI price)
    {
        Button btn = EditorUIBuilder.RaisedBtn(parent, "RerollBtn", RerollFace,
                                               out GameObject body);

        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(RerollW, RerollH);

        var label = EditorUIBuilder.TMP(body, "Label", "재고 교체",
                                        UIScale.FontMd, FontStyles.Bold);
        {
            var r = label.rectTransform;
            r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(Pad, 0f);
            r.offsetMax = new Vector2(-Pad, -Pad);
        }
        label.alignment        = TextAlignmentOptions.Midline;
        label.color            = Color.white;
        label.raycastTarget    = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject badge = FacilityStage.Badge(body, "PriceBadge", goldIcon, 48f,
                                               UIScale.FontMd, 120f, FacilityColors.Gold,
                                               out price);
        {
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.25f);
            brt.pivot     = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, 4f);
        }

        return btn;
    }

    /// <summary>칸 안 가운데 정렬 한 줄. 위에서부터의 거리로 자리를 잡는다.</summary>
    static TextMeshProUGUI CenterLabel(GameObject stall, string name, float font,
                                       FontStyles style, Color color, float top, float h)
    {
        var tmp = EditorUIBuilder.TMP(stall, name, "", font, style);
        var r   = tmp.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot     = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(10f, -(top + h));
        r.offsetMax = new Vector2(-10f, -top);
        tmp.alignment        = TextAlignmentOptions.Midline;
        tmp.color            = color;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static ShopPopup.StallView Finish(GameObject stall, Button buy, Image icon,
                                      TextMeshProUGUI name, TextMeshProUGUI desc,
                                      TextMeshProUGUI price)
    {
        // 산 뒤에 덮는 판 — 자리는 남기고 흐리게 만든다.
        //   ⚠ 마지막에 만든다 — Unity UI 는 계층 순서대로 그리므로 구매 버튼보다
        //     뒤에 있어야 버튼 위를 덮는다. 레이캐스트는 켜 둔다: 덮개 밑의
        //     버튼이 눌리면 "구입함" 인데 또 사진다.
        var sold = EditorUIBuilder.Img(stall, "SoldOverlay", SoldVeil);
        EditorUIBuilder.Stretch(sold.gameObject);
        sold.raycastTarget = true;

        var soldTmp = EditorUIBuilder.TMP(sold.gameObject, "SoldText", "구입함",
                                          UIScale.FontLg, FontStyles.Bold);
        EditorUIBuilder.Stretch(soldTmp.gameObject);
        soldTmp.alignment     = TextAlignmentOptions.Midline;
        soldTmp.color         = FacilityColors.Sub;
        soldTmp.raycastTarget = false;

        sold.gameObject.SetActive(false);

        return new ShopPopup.StallView
        {
            Root        = stall,
            Button      = buy,
            Icon        = icon,
            NameText    = name,
            DescText    = desc,
            PriceText   = price,
            SoldOverlay = sold.gameObject,
        };
    }

    // ── 검산 ─────────────────────────────────────────────────

    /// <summary>
    /// 구운 자리가 화면과 서로를 벗어나지 않았는지 본다.
    ///
    /// ⚠ 조용한 실패를 시끄러운 실패로 바꾸는 자리다 — 칸이 겹치거나 어둠 밖으로
    ///   나가도 예외는 나지 않는다. 화면에서 "글이 잘려 보인다" 로만 나타난다.
    /// </summary>
    static void Verify(List<ShopPopup.StallView> cards,
                       List<ShopPopup.StallView> perks,
                       List<ShopPopup.StallView> boons)
    {
        // 상시 판매 줄에 구매 버튼이 들어가는가 — 넘치면 버튼이 칸 밖으로 삐져나온다.
        if (BoonRowH < BuyH + 8f)
            Debug.LogError($"[{Tag}] 상시 판매 줄이 구매 버튼보다 낮습니다 " +
                           $"({BoonRowH:0} < {BuyH + 8f:0}) — 칸을 줄이거나 줄 수를 줄이세요.");

        // 위 줄이 상단 어둠(위에서 236)까지 올라가면 제목과 부딪힌다.
        const float TopVeilBottom = 1080f - FacilityStage.TopVeilH;

        if (ShopVeilTop > TopVeilBottom)
            Debug.LogError($"[{Tag}] 물건 줄이 위쪽 어둠까지 올라갑니다 " +
                           $"({ShopVeilTop:0} > {TopVeilBottom:0}) — 칸 높이를 줄이세요.");

        // 가로 줄이 화면 여백 안에 들어가는가.
        float totalStallW = (ShopPopup.MaxPerkStalls + 1) * StallW
                          + ShopPopup.MaxPerkStalls * StallGap;

        if (totalStallW > 1920f - FacilityStage.Margin * 2f)
            Debug.LogError($"[{Tag}] 특성·정수 줄이 화면을 넘칩니다 ({totalStallW:0}) — " +
                           "StallW 를 줄이거나 칸 수를 줄이세요.");

        VerifyDescriptionsFit();

        // 재고 교체 버튼이 화면 여백 안에 드는가 — 카드 줄 오른쪽 옆에 선다.
        float totalCardW = ShopPopup.MaxCardStalls * CardW
                         + (ShopPopup.MaxCardStalls - 1) * CardGap;
        float rerollRight = (1920f + totalCardW) * 0.5f + RerollGap + RerollW;

        if (rerollRight > 1920f - FacilityStage.Margin)
            Debug.LogError($"[{Tag}] 재고 교체 버튼이 화면 여백을 넘습니다 " +
                           $"({rerollRight:0} > {1920f - FacilityStage.Margin:0}) — RerollW 를 줄이세요.");

        if (RerollH > CardH)
            Debug.LogError($"[{Tag}] 재고 교체 버튼이 카드보다 높습니다 ({RerollH} > {CardH:0}).");

        int wired = cards.Count + perks.Count + boons.Count;
        foreach (var v in cards) CheckStall(v);
        foreach (var v in perks) CheckStall(v);
        foreach (var v in boons) CheckStall(v);

        Debug.Log($"[{Tag}] 칸 {wired}개 검산 완료.");
    }

    /// <summary>
    /// 가장 긴 특성 설명이 칸 안에 들어가는가.
    ///
    /// ⚠ 이 검산이 없으면 실패가 조용하다 — TMP 는 넘치는 글을 그냥 잘라 낸다.
    ///   특성 설명은 RunPerk.Describe 가 만드므로, 밸런스를 고치다 한 줄만
    ///   길어져도 상점에서만 뒷말이 사라진다. 다른 화면은 멀쩡해서 더 찾기 어렵다.
    ///
    /// ⚠ 한글 한 자를 폰트 크기 그대로(34px) 잡는다. 숫자·공백은 그보다 좁으니
    ///   보수적인 어림이다 — 넉넉히 잡아야 경고가 늦지 않는다.
    /// </summary>
    static void VerifyDescriptionsFit()
    {
        float textW  = StallW - (Pad + StallIcon + Pad) - Pad;
        int   budget = Mathf.FloorToInt(textW * StallDescLines / UIScale.FontSm);

        var longest = "";
        foreach (RunPerk perk in System.Enum.GetValues(typeof(RunPerk)))
        {
            if (perk == RunPerk.None) continue;

            string d = perk.Describe();
            if (d.Length > longest.Length) longest = d;
        }

        if (longest.Length > budget)
            Debug.LogError($"[{Tag}] 특성 설명이 칸을 넘칩니다 — {longest.Length}자 > {budget}자\n" +
                           $"  {longest}\n" +
                           "  StallDescLines 를 늘리거나 StallW 를 넓히세요. " +
                           "글자를 FontSm 밑으로 줄이지는 말 것 — UI 규칙 4.");
    }

    static void CheckStall(ShopPopup.StallView v)
    {
        if (v.Root == null || v.Button == null || v.Icon == null ||
            v.NameText == null || v.DescText == null || v.PriceText == null ||
            v.SoldOverlay == null)
        {
            Debug.LogError($"[{Tag}] 칸의 참조가 비었습니다 — 런타임이 조용히 NullReference 로 " +
                           "죽습니다.");
        }
    }

    // ── 배열 배선 ────────────────────────────────────────────

    static void WriteStalls(SerializedObject so, string field, List<ShopPopup.StallView> list)
    {
        SerializedProperty arr = so.FindProperty(field);
        arr.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
            WriteStall(arr.GetArrayElementAtIndex(i), list[i]);
    }

    static void WriteStall(SerializedProperty e, ShopPopup.StallView v)
    {
        e.FindPropertyRelative("Root")       .objectReferenceValue = v.Root;
        e.FindPropertyRelative("Button")     .objectReferenceValue = v.Button;
        e.FindPropertyRelative("Icon")       .objectReferenceValue = v.Icon;
        e.FindPropertyRelative("NameText")   .objectReferenceValue = v.NameText;
        e.FindPropertyRelative("DescText")   .objectReferenceValue = v.DescText;
        e.FindPropertyRelative("PriceText")  .objectReferenceValue = v.PriceText;
        e.FindPropertyRelative("SoldOverlay").objectReferenceValue = v.SoldOverlay;
    }
}
