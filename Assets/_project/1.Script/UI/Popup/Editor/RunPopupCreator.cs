#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  RunPopupCreator.cs  [Editor Only]
//  이 게임의 런 진행 팝업 2종을 굽는다.
//
//    CardSelectPopup  — 스테이지 클리어 카드 3택 (어빌리티 선택을 대체)
//    BattleStatsPopup — 전투 통계 (카드별 딜 기여, 딜/탱/힐 탭)
//
//  ■ 둘을 한 파일에 둔 이유
//    같은 런 흐름 안에서 서로를 연다 (카드 3택 → 통계).
//    치수·색을 공유해야 한 벌로 보인다.
//
//  ■ ⚠ 패배 결산은 여기 없다 — 환생 팝업(원작)이 그 자리다.
//
//  ■ ⚠ 승리 팝업은 만들지 않는다
//    스테이지를 깨면 곧장 카드 3택으로 간다. 원작 BattleResultPopup 은
//    이 게임의 흐름에서 열리지 않는다 (RunBootstrap.HandleVictory 참고).
//
//  사용: Tools > Project K > 프리팹 생성 > 팝업 > ▶ 런 팝업
//  ⚠ 구운 뒤 PopupManager 인스펙터에서 'Load Popup Prefabs' 를 눌러야 등록된다.
// ============================================================

public static class RunPopupCreator
{
    const string SaveRoot = "Assets/_project/2.Prefabs/UI";
    const string Tag      = "RunPopupCreator";

    // ── 공통 치수 ────────────────────────────────────────────
    const float HeaderH = 136f;
    const float Outset  =   6f;   // 테두리가 패널 밖으로 드러나는 두께

    /// <summary>통계 줄 배경에 깔리는 비중 막대 — 글자가 읽힐 만큼만 옅게.</summary>
    static readonly Color BarFill = new(0.29f, 0.52f, 0.95f, 0.24f);

    // ── 공통 색 ──────────────────────────────────────────────
    static readonly Color Overlay   = new(0f,     0f,     0f,     0.86f);
    static readonly Color PanelBg   = new(0.07f,  0.075f, 0.13f,  1f);
    static readonly Color HeaderBg  = new(0.08f,  0.10f,  0.18f,  1f);
    static readonly Color CardFace  = new(0.115f, 0.125f, 0.21f,  1f);
    static readonly Color RowBg     = new(0.10f,  0.11f,  0.19f,  1f);
    static readonly Color Muted     = new(0.72f,  0.76f,  0.90f,  1f);
    static readonly Color Accent    = new(0.62f,  0.82f,  1.00f,  1f);
    static readonly Color StatsFace = new(0.16f,  0.30f,  0.42f,  1f);
    static readonly Color ConfirmC  = new(0.11f,  0.72f,  0.58f,  1f);
    static readonly Color BorderC   = new(0.26f,  0.44f,  0.72f,  1f);

    [MenuItem(ProjectKMenu.Popup + "▶ 런 팝업", priority = ProjectKMenu.PrefabPrio + 40)]
    public static void CreateAll()
    {
        CreateCardSelect();
        CreateBattleStats();
        CreateChoice();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[{Tag}] 완료 — 3종. " +
                  "⚠ PopupManager 인스펙터에서 'Load Popup Prefabs' 를 눌러야 등록됩니다.");
    }

    // ══════════════════════════════════════════════════════════
    //  ① 카드 3택
    // ══════════════════════════════════════════════════════════

    // ⚠ 치수를 키울 때 검산할 것 (2026-09-03: 1500×860 → 1700×980)
    //
    //   ■ 2026-09-04 — **4택**이 가능해져 가로를 다시 잡았다
    //     특성 '감식안' 이 3택을 4택으로 만든다. 프리팹은 언제나 4칸을 굽고
    //     남는 칸은 런타임이 숨긴다(CardSelectPopup.Setup).
    //
    //     4장 : SelCW × 4 + SelGap × 3 = 412×4 + 40×3 = 1768  ≤ SelW(1820)
    //     3장 : 412×3 + 40×2 = 1316 — 가운데 정렬이라 남는 폭은 여백이 된다
    //     캔버스 : SelW + Outset×2 = 1832 ≤ 1920  → 좌우 44px 남는다
    //
    //     ⚠ 카드를 480 → 412 로 줄였다. 안폭이 464 → 396 이 되므로 아래 둘을 검산했다.
    //       배지 한 쌍 : BadgeWidthFor(56, FontMd 42) × 2 + inset 6 × 2
    //                    = (56 + 7 + 50) × 2 + 12 = 238  ≤ 396  ✓
    //       시너지 칩  : (72 + 12) × 3 − 12 = 240              ≤ 396  ✓
    //     더 줄이면 240 이 먼저 걸린다 — SelCW 하한은 256 이다.
    //
    //     가로 : SelCW × 4 + SelGap × 3 = 1768  ≤ SelW(1820)
    //     세로 : SelH 는 UIScale.PopupMaxH(1000) 를 넘으면 위아래가 잘린다
    //            카드는 헤더 아래에 중앙 정렬된다 — SelCH 는 SelH − HeaderH − 여백
    //   ⚠ 700 → 780 (2026-09-03) — 초상화·시너지 아이콘을 키우려고 늘렸다
    //     세로 검산: 카드 중심은 헤더 아래로 밀려 있다(-(HeaderH/2)+14 = -54).
    //       카드 위 = -54 + 390 = 336  ≤ 헤더 아래(490-136 = 354)   → 18px 남는다
    //       카드 아래 = -54 - 390 = -444  ≥ 패널 아래(-490)          → 46px 남는다
    //     더 늘리려면 SelH 를 함께 올려야 하는데 UIScale.PopupMaxH(1000)가 상한이다.
    const float SelW   = 1820f;
    const float SelH   =  980f;
    const float SelCW  =  412f;   // 카드 한 장 (4장 = 1768 ≤ 1820)
    const float SelCH  =  780f;

    // ⚠ 카드 안은 **위에서 아래로** 한 줄씩 쌓는다 (2026-08-28)
    //   예전에는 상태·아이콘은 위 기준, 이름·설명은 아래 기준으로 잡았다.
    //   양쪽에서 자라다 보니 가운데서 만나 **이름과 설명이 같은 자리에
    //   겹쳐 찍혔다** (아래에서 재면 이름 92~145 · 설명 16~145).
    //   기준을 하나로 모으면 다음 줄이 앞줄 끝에서 시작하므로, 줄을
    //   늘리거나 폰트를 키워도 겹칠 수가 없다.
    const float SelPadY  =  14f;   // 카드 안쪽 위 여백
    const float SelIconH = 248f;
    const float SelGap   =  40f;

    // ── 카드 아래쪽 공용 구역 ────────────────────────────────
    //
    //   ⚠ 설명(새 카드)과 레벨업 내역(중복 카드)이 **같은 자리**를 쓴다
    //     둘은 동시에 뜨지 않는다 — 새 카드는 종족 패시브를, 중복 카드는
    //     이번에 열리는 것을 적는다(CardSelectPopup 참고). 자리를 따로
    //     잡으면 어느 쪽이든 카드 절반이 늘 비어 보인다.
    //     아래에서 재므로 위쪽 줄이 몇 줄이든 이 구역은 흔들리지 않는다.
    const float SelBottomPad = 12f;
    const float SelBottomL   =  4f;   // 이 구역이 담는 줄 수

    // ⚠ 스탯 줄과 이 구역 사이에 60px 쯤이 빈다 — 일부러 남긴 자리다 (2026-09-03)
    //   카드를 700 으로 키우면서 그만큼을 위 칸들에 다 나눠 주지 않았다.
    //   시너지 표시가 들어올 자리다. 지금 다른 칸을 늘려 메우면 그때 전부 다시
    //   밀어야 한다.

    /// <summary>얻는 것 구역의 바탕. 카드 면보다 밝아 "여기부터 다른 이야기" 가 읽힌다.</summary>
    static readonly Color GainBg = new(0.20f, 0.24f, 0.40f, 0.45f);

    // ── 초상화 머리 위 배지 ──────────────────────────────────
    //
    //   하단 카드 바와 **같은 빌더**(EditorUIBuilder.IconValueBadge)를 쓴다 —
    //   [아이콘][숫자] 를 가로로 나란히 놓는 형태다 (2026-09-03, 사용자 확정).
    //
    //   ⚠ 두 개가 카드 폭에 들어가는지 반드시 확인할 것
    //     검산은 반드시 BadgeWidthFor 로 한다 (아이콘 폭만으로는 못 잰다).
    //         (56 + 7 + 50) × 2 + 6 × 2 = 238  ≤ 424(카드 안폭)  → 가운데가 186px 빈다
    //     (하단 카드 바에서 이 계산을 빼먹어 배지가 서로 겹쳤다)
    //
    //   ⚠ 84/FontLg → 56/FontMd (2026-09-03) — 초상화를 덮었다
    //     가로 폭만 재고 넣었더니 배지 한 벌이 161px 이라 초상화(208 짜리, 가운데
    //     정렬) 위로 60px 씩 올라탔다. 배지는 **초상화와 같은 칸**을 쓰므로
    //     가로 여유가 남는다고 키우면 그대로 그림 위에 겹친다.
    //     지금은 한 벌이 113px 이라 초상화와 11px 만 스친다 — 그 자리는 몬스터
    //     그림의 투명한 위 모서리다.
    //
    //   ■ 그래도 하단 카드 바(36/FontSm)보다는 크다
    //     여기는 판이 멈춘 채 읽고 고르는 화면이고 카드가 440 이라 그만큼 쓴다.
    const float SelBadgeIconSize = 56f;
    const float SelBadgeInset    =  6f;

    // 숫자가 카드 면 위에 앉으므로 UIScale 단계를 그대로 쓴다 (규칙 4).
    const float SelBadgeFont = UIScale.FontMd;

    // ⚠ 글자색은 바탕(카드 면)을 보고 정한다 — 하단 카드 바와 같은 두 색이다.
    static readonly Color SelManaNumberColor  = Color.white;
    static readonly Color SelCountNumberColor = new(0.96f, 0.94f, 0.87f);

    [MenuItem(ProjectKMenu.Popup + "카드 선택", priority = ProjectKMenu.PrefabPrio + 41)]
    public static void CreateCardSelect()
    {
        // ⚠ 마나·마릿수는 글이 아니라 아이콘이다 (확정 규칙 — CLAUDE.md UI 규칙 7)
        //   경로·임포트 설정의 정본은 UIIconAssets 다. 하단 카드 바와 같은 그림을 쓴다.
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;

        // 시너지 아이콘 여덟 장 — 어느 칩에 어느 그림이 갈지는 런타임이 정한다.
        // 카드마다 표식이 다르므로 Creator 는 여덟 장 전부를 팝업에 쥐여 준다.
        if (!SynergyIconAssets.TryLoad(Tag, out Sprite[] synergyIcons)) return;

        // 종족 패시브 아이콘 19장 — 같은 이유로 전부 쥐여 준다.
        // 카드마다 붙는 패시브가 다르고, 계보를 타고 최대 넷까지 온다.
        if (!SpeciesPassiveIconAssets.TryLoad(Tag, out Sprite[] passiveIcons)) return;

        GameObject root = MakeRoot("CardSelectPopup", PopupType.CardSelect,
                                   out GameObject panel, SelW, SelH);

        var ui = root.AddComponent<CardSelectPopup>();

        TextMeshProUGUI title = MakeHeader(panel, "카드 선택", "카드를 하나 고르세요");

        // 전투 통계 버튼 — 헤더 오른쪽.
        // ⚠ 여기 두는 이유: "방금 판이 어땠는지 보고 고르라" 는 뜻이다.
        //   카드 아래에 두면 이미 고른 뒤에나 눈에 들어온다.
        Button statsBtn = MakeStatsButton(panel);

        // 카드 3장 — 가운데 정렬
        // ⚠ 3 이 아니라 MaxChoiceCount(4) 로 굽는다
        //   특성 '감식안' 이 4택으로 만든다. 3칸만 구우면 네 번째 카드가
        //   뽑히기만 하고 화면에 안 뜬다. 남는 칸은 런타임이 숨긴다.
        var options = new CardSelectPopup.OptionView[CardRewardPicker.MaxChoiceCount];
        float step  = SelCW + SelGap;
        float start = -(CardRewardPicker.MaxChoiceCount - 1) * 0.5f * step;

        for (int i = 0; i < options.Length; i++)
            options[i] = BuildSelectOption(panel, i, start + i * step, manaIcon, countIcon);

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_titleText",   title,    Tag);
        EditorUIBuilder.SetObj(so, "_statsButton", statsBtn, Tag);
        EditorUIBuilder.SetObj(so, "_fallbackIcon", null,    Tag);

        // ⚠ AllTags 순서 그대로 — 런타임이 그 인덱스로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_synergyIcons", synergyIcons, Tag);

        // ⚠ SpeciesPassiveRule.All 순서 그대로 — 같은 계약이다
        EditorUIBuilder.SetObjArray(so, "_speciesIcons", passiveIcons, Tag);

        // 가로 간격 — 런타임이 "몇 장을 띄우는가" 로 자리를 다시 잡는다
        // (CardSelectPopup.Recenter). 네 칸을 굽고 세 장만 띄우면 쏠린다.
        so.FindProperty("_cardStep").floatValue = step;

        SerializedProperty arr = so.FindProperty("_options");
        arr.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")     .objectReferenceValue = options[i].Root;
            e.FindPropertyRelative("Button")   .objectReferenceValue = options[i].Button;
            e.FindPropertyRelative("Icon")     .objectReferenceValue = options[i].Icon;
            e.FindPropertyRelative("NameText") .objectReferenceValue = options[i].NameText;
            e.FindPropertyRelative("StateText").objectReferenceValue = options[i].StateText;
            e.FindPropertyRelative("DescText") .objectReferenceValue = options[i].DescText;
            e.FindPropertyRelative("StatText") .objectReferenceValue = options[i].StatText;
            e.FindPropertyRelative("GainText") .objectReferenceValue = options[i].GainText;
            e.FindPropertyRelative("ManaText") .objectReferenceValue = options[i].ManaText;
            e.FindPropertyRelative("ManaRoot") .objectReferenceValue = options[i].ManaRoot;
            e.FindPropertyRelative("CountText").objectReferenceValue = options[i].CountText;
            e.FindPropertyRelative("CountRoot").objectReferenceValue = options[i].CountRoot;
            e.FindPropertyRelative("KindText") .objectReferenceValue = options[i].KindText;
            e.FindPropertyRelative("StatText2").objectReferenceValue = options[i].StatText2;
            SetRefArray(e, "SynergyRoots",  options[i].SynergyRoots);
            SetRefArray(e, "SynergyIcons",  options[i].SynergyIcons);
            SetRefArray(e, "SynergyHovers", options[i].SynergyHovers);
            SetRefArray(e, "PassiveRoots",  options[i].PassiveRoots);
            SetRefArray(e, "PassiveIcons",  options[i].PassiveIcons);
            SetRefArray(e, "PassiveHovers", options[i].PassiveHovers);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "CardSelectPopup");
    }

    // ── 시너지 칩 ────────────────────────────────────────────
    //   [아이콘 44][숫자]. 셋을 나란히 놓아도 (110+10)×3 = 340 ≤ 424(카드 안폭).
    //
    //   ■ 숫자 없이 **그림만** 늘어놓는다 (2026-09-03, 사용자 확정)
    //     카드에서 알고 싶은 것은 "이 몬스터가 어느 계열인가" 다.
    //     진행도(2/4)는 상단 시너지 줄이 훨씬 크게 말하고 있고, 카드에 또 적으면
    //     세 칸이 숫자로 차서 정작 그림이 안 읽힌다.
    //     숫자를 뺀 만큼 아이콘을 56 → 72 로 키웠다.
    //
    //   ⚠ 세로를 반드시 검산할 것 — 아래 "얻는 것" 구역과 붙는다
    //     14 + 61(상태) + 258(초상화) + 57(이름) + 47(형태) + 57(스탯) = 494
    //     칩 줄 494~566,  얻는 것 구역은 780 − 189 = 591 에서 시작한다 → 25px 남는다
    //     칩을 26px 이상 키우려면 SelCH 를 함께 올릴 것.
    //
    //   가로: (72 + 12) × 3 − 12 = 240  ≤ 396(카드 안폭, SelCW 412 − 좌우 8)
    const int   SelSynergySlots = 3;     // 한 종족이 갖는 표식의 최대 수
    const float SelSynergyH     = 72f;
    const float SelSynergyW     = 72f;   // 칸 = 아이콘 (숫자 칸이 없다)
    const float SelSynergyGap   = 12f;
    const float SelSynergyIcon  = 72f;

    // ── 종족 패시브 칩 ────────────────────────────────────────
    //   ⚠ 시너지 칩보다 한 단계 작다
    //     시너지는 덱 전체의 결론이고 패시브는 이 한 마리의 성질이다.
    //     같은 크기로 두면 두 줄이 한 덩어리로 보여 무엇이 무엇인지 갈리지 않는다.
    const int   SelPassiveSlots = 4;     // 계보가 가장 깊은 종족이 갖는 패시브 수
    const float SelPassiveH     = 56f;
    const float SelPassiveW     = 56f;
    const float SelPassiveGap   = 10f;

    /// <summary>이름과 공격 형태("근접"·"원거리"·"즉시 발동") 사이 간격.</summary>
    const float SelKindGap = 10f;

    /// <summary>카드 안의 시너지 칩 하나 — [아이콘][숫자].</summary>
    static GameObject BuildSynergyChip(GameObject parent, int index,
                                       out Image icon, out SynergyChipUI hover)
    {
        var chip = EditorUIBuilder.Go($"Synergy_{index + 1}", parent);
        EditorUIBuilder.LE(chip, SelSynergyW, SelSynergyH);

        // ⚠ 클릭을 받을 면이 필요하다 — 빈 오브젝트는 레이캐스트를 못 받는다
        //   카드 면이 그대로 비쳐야 하므로 거의 투명한 색만 깐다
        //   (특성 아이콘 슬롯이 쓰는 것과 같은 수법).
        var hit = EditorUIBuilder.Img(chip, "Hit", new Color(0f, 0f, 0f, 0.001f));
        EditorUIBuilder.Stretch(hit.gameObject);
        hit.raycastTarget = true;

        // 그림이 칸을 그대로 채운다 — 숫자 칸이 없으므로 나눌 것이 없다.
        icon = EditorUIBuilder.Img(chip, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        EditorUIBuilder.Stretch(icon.gameObject);

        // ── 눌러서 효과 보기 ──
        //   ⚠ 툴팁은 여기서 만들지 않는다 — TooltipLayer 의 한 장을 함께 쓴다
        //     칩 자식으로 두면 카드 면·"얻는 것" 구역 밑으로 깔린다.
        hover = chip.AddComponent<SynergyChipUI>();

        chip.SetActive(false);
        return chip;
    }

    /// <summary>
    /// 카드 안의 종족 패시브 칩 하나 — 그림 한 장이 전부다.
    /// 시너지 칩과 구조가 같다 (레이캐스트 면 + 그림 + 툴팁 훅).
    /// </summary>
    static GameObject BuildPassiveChip(GameObject parent, int index,
                                       out Image icon, out SpeciesPassiveChipUI hover)
    {
        var chip = EditorUIBuilder.Go($"Passive_{index + 1}", parent);
        EditorUIBuilder.LE(chip, SelPassiveW, SelPassiveH);

        // ⚠ 클릭을 받을 면이 필요하다 — 빈 오브젝트는 레이캐스트를 못 받는다
        var hit = EditorUIBuilder.Img(chip, "Hit", new Color(0f, 0f, 0f, 0.001f));
        EditorUIBuilder.Stretch(hit.gameObject);
        hit.raycastTarget = true;

        icon = EditorUIBuilder.Img(chip, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        EditorUIBuilder.Stretch(icon.gameObject);

        // ⚠ 툴팁은 여기서 만들지 않는다 — TooltipLayer 의 한 장을 함께 쓴다
        hover = chip.AddComponent<SpeciesPassiveChipUI>();

        chip.SetActive(false);
        return chip;
    }

    /// <summary>OptionView 의 배열 필드를 직렬화 배열에 그대로 옮긴다.</summary>
    static void SetRefArray(SerializedProperty element, string field, System.Array values)
    {
        SerializedProperty arr = element.FindPropertyRelative(field);
        arr.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = (UnityEngine.Object)values.GetValue(i);
    }

    /// <summary>
    /// 후보 카드 한 장.
    ///
    ///   ┌──────────────────┐
    ///   │       NEW        │  ← 상태
    ///   │ ◈8         ☠3    │  ← 마나·마릿수 배지 (초상화 머리 위)
    ///   │     (초상화)      │
    ///   │      슬라임       │  ← 이름
    ///   │       근접        │  ← 공격 형태 (색이 다르다)
    ///   │ 공격력 24 | 체력 120 │  ← 기본 스탯, 반씩 나눈 한 줄
    ///   ├──────────────────┤
    ///   │   방어율 +6%p     │  ← 얻는 것 — 한 줄에 하나, 크게
    ///   │   체력 +10%       │
    ///   └──────────────────┘
    /// </summary>
    static CardSelectPopup.OptionView BuildSelectOption(GameObject panel, int index, float offsetX,
                                                        Sprite manaIcon, Sprite countIcon)
    {
        var slot = EditorUIBuilder.Go($"Option_{index + 1}", panel);
        var rt   = slot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(offsetX, -(HeaderH * 0.5f) + 14f);
        rt.sizeDelta        = new Vector2(SelCW, SelCH);

        // ⚠ 누를 수 있는 것이므로 음각 버튼이어야 한다 (UI 규칙 1)
        Button button = EditorUIBuilder.RaisedBtnOn(slot, CardFace, out GameObject body);

        // 위에서부터 한 줄씩 쌓는다. y 는 "여기까지 찼다" 는 커서다.
        float y = SelPadY;

        var state = EditorUIBuilder.TMP(body, "State", "NEW", UIScale.FontMd, FontStyles.Bold);
        EditorUIBuilder.AnchorTop(state.rectTransform, y, UIScale.RowMd, 14f);
        state.alignment        = TextAlignmentOptions.Center;
        state.raycastTarget    = false;
        state.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ "NEW" 만 들어가던 칸이 아니다
        //   지금은 "Lv.2 › Lv.3" · "Lv.3  (2장 더)" 까지 온다. 폭이 352 뿐이라
        //   고정 크기로 두면 긴 쪽이 카드 밖으로 나간다 — 줄이지 말고 줄여 쓴다.
        state.enableAutoSizing = true;
        state.fontSizeMax      = UIScale.FontMd;
        state.fontSizeMin      = UIScale.FontSm;

        y += UIScale.RowMd + 8f;

        // ── 초상화 + 머리 위 배지 ────────────────────────────
        //
        //   ⚠ 배지를 초상화와 같은 칸(PortraitRow)에 담는다
        //     초상화 이미지에 직접 붙이면 preserveAspect 때문에 실제 그림이
        //     칸보다 작아졌을 때 배지가 그림에서 떨어져 허공에 뜬다.
        //     칸을 하나 두고 그 두 모서리에 앉히면 종족이 바뀌어도 자리가 같다.
        var portraitRow = EditorUIBuilder.Go("PortraitRow", body);
        EditorUIBuilder.AnchorTop(portraitRow.GetComponent<RectTransform>(), y, SelIconH, 8f);

        var icon = EditorUIBuilder.Img(portraitRow, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot     = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta        = new Vector2(SelIconH, SelIconH);

        // 하단 카드 바와 같은 빌더 — 아이콘이 안쪽, 숫자가 바깥쪽인 거울상이다.
        // 마나 배지는 늘 보이므로 루트를 들고 있을 필요가 없다 (마릿수만 켜고 끈다).
        GameObject manaRoot = EditorUIBuilder.IconValueBadge(
            portraitRow, "ManaCost", manaIcon, rightSide: false,
            SelBadgeIconSize, SelBadgeFont, SelBadgeInset, SelManaNumberColor,
            out TextMeshProUGUI manaText);

        GameObject countRoot = EditorUIBuilder.IconValueBadge(
            portraitRow, "SummonCount", countIcon, rightSide: true,
            SelBadgeIconSize, SelBadgeFont, SelBadgeInset, SelCountNumberColor,
            out TextMeshProUGUI countText);

        y += SelIconH + 10f;

        // ── 이름 + 공격 형태 — 한 줄 ────────────────────────
        //
        //   ⚠ 두 줄이었던 것을 합쳤다
        //     "근접" 두 글자가 제 줄을 통째로 차지하고 있었다. 카드 세로가
        //     빠듯한데 한 줄값(RowSm)을 쓸 만한 정보가 아니다.
        //     ⚠ 양 끝으로 밀지 않는다 — 둘은 **가운데 나란히** 붙는다
        //       한쪽은 왼쪽 끝, 한쪽은 오른쪽 끝에 두면 이름이 짧을 때
        //       카드 폭만큼 벌어져 두 토막이 남남으로 보인다.
        //       가로 레이아웃이 각자 글자 폭만큼만 자리를 주고 그 묶음을
        //       가운데 놓으므로, 이름이 길든 짧든 분류가 늘 이름 바로 옆이다.
        var nameRow = EditorUIBuilder.Go("NameRow", body);
        EditorUIBuilder.AnchorTop(nameRow.GetComponent<RectTransform>(), y, UIScale.RowMd, 12f);

        var nameLayout = nameRow.AddComponent<HorizontalLayoutGroup>();
        nameLayout.spacing                = SelKindGap;
        nameLayout.childAlignment         = TextAnchor.MiddleCenter;
        nameLayout.childControlWidth      = true;    // 각자 글자 폭만큼만
        nameLayout.childControlHeight     = true;
        nameLayout.childForceExpandWidth  = false;   // ⚠ true 면 둘이 다시 양 끝으로 벌어진다
        nameLayout.childForceExpandHeight = false;

        var name = EditorUIBuilder.TMP(nameRow, "Name", "이름", UIScale.FontMd, FontStyles.Bold);
        name.alignment        = TextAlignmentOptions.Midline;
        name.raycastTarget    = false;
        name.textWrappingMode = TextWrappingModes.NoWrap;

        // ⚠ 이름은 줄바꿈이 아니라 폰트를 줄여서 맞춘다
        //   "좀비" 같은 두 글자와 "숲의 트롤 파수꾼" 같은 긴 이름이 같은 칸에
        //   온다. NoWrap 만 걸면 긴 이름이 카드 밖으로 삐져나가고, 줄바꿈을
        //   허용하면 아래 설명을 밀어 버린다. 자동 축소가 답이다.
        name.enableAutoSizing = true;
        name.fontSizeMax      = UIScale.FontMd;
        name.fontSizeMin      = UIScale.FontSm;

        // ── 공격 형태 — 근접 / 원거리 / 즉시 발동 ────────────
        //
        //   ⚠ 숫자 줄과 **다르게** 보여야 한다 (2026-09-02)
        //     예전에는 "마나 5 · 8마리 · 근접" 이 한 줄에 같은 색·같은 크기로
        //     붙어 있어서, 숫자를 읽다가 마지막 토막에서 눈이 걸렸다.
        //     이건 값이 아니라 **분류**라 색을 뗀다 (색은 런타임이 넣는다 —
        //     근접·원거리·스킬이 서로 다르다). 크기도 한 단계 내린다.
        //   ⚠ 폭을 고정하지 않는다 — 글자 폭만큼만 차지해야 이름에 붙는다
        //     "근접" 과 "즉시 발동" 은 폭이 두 배 넘게 차이 난다. 넉넉한 고정
        //     폭을 주면 짧은 쪽이 빈칸을 달고 다녀 이름에서 떨어져 보인다.
        var kind = EditorUIBuilder.TMP(nameRow, "Kind", "근접", UIScale.FontSm, FontStyles.Bold);
        kind.alignment        = TextAlignmentOptions.Midline;
        kind.color            = Muted;
        kind.raycastTarget    = false;
        kind.textWrappingMode = TextWrappingModes.NoWrap;

        y += UIScale.RowMd + 4f;

        // ── 종족 패시브 칩 줄 — "이 한 마리가 무엇을 하는가" ──
        //
        //   ⚠ 시너지 줄과 **떨어뜨려** 둔다 (2026-09-06, 사용자 지적)
        //     한때 시너지 줄 바로 아래에 붙여 놨더니 그림 일곱 개가 한 덩어리로
        //     보여 무엇이 시너지고 무엇이 패시브인지 갈리지 않았다.
        //     지금은 **스탯 줄이 둘 사이를 가른다** — 위(이름 밑)가 개체의 성질,
        //     아래(스탯 밑)가 덱에서의 쓸모다.
        //     ⚠ 두 줄을 다시 붙이지 말 것. 가르는 것은 **자리와 그림**이다 —
        //       둘 다 가운데 정렬이므로(사용자 확정) 정렬로는 갈리지 않는다.
        //       그림 쪽은 패시브가 리벳 테두리 한 종류라 통째로 구분된다
        //       (SpeciesPassiveIconGenerator.FamilyFrame 참고).
        var passiveRow = EditorUIBuilder.Go("PassiveRow", body);
        EditorUIBuilder.AnchorTop(passiveRow.GetComponent<RectTransform>(),
                                  y, SelPassiveH, 12f);

        var passiveLayout = passiveRow.AddComponent<HorizontalLayoutGroup>();
        passiveLayout.spacing                = SelPassiveGap;
        passiveLayout.childAlignment         = TextAnchor.MiddleCenter;
        passiveLayout.childControlWidth      = true;
        passiveLayout.childControlHeight     = true;
        passiveLayout.childForceExpandWidth  = false;
        passiveLayout.childForceExpandHeight = false;

        var passiveRoots  = new GameObject[SelPassiveSlots];
        var passiveIconsL = new Image[SelPassiveSlots];
        var passiveHovers = new SpeciesPassiveChipUI[SelPassiveSlots];

        for (int i = 0; i < SelPassiveSlots; i++)
        {
            GameObject pChip = BuildPassiveChip(passiveRow, i,
                                                out Image pChipIcon,
                                                out SpeciesPassiveChipUI pChipHover);
            passiveRoots[i]  = pChip;
            passiveIconsL[i] = pChipIcon;
            passiveHovers[i] = pChipHover;
        }

        y += SelPassiveH + 6f;

        // ── 기본 스탯 — 두 칸으로 나눈 한 줄 ─────────────────
        //
        //   ⚠ 한 줄로 몰아넣되 **글자를 이어 붙이지 않는다** (2026-09-02)
        //     "공격력 24 · 체력 120" 처럼 한 문자열로 쓰면 값이 커질수록
        //     가운데점이 밀려 다니고, 자동 축소까지 걸리면 카드마다 글자
        //     크기가 달라져 세 장을 나란히 비교할 수 없다.
        //     칸을 반씩 나눠 각자 가운데 정렬하면 값이 몇 자리든 **같은
        //     자리**에 뜬다 — 카드끼리 위아래로 곧장 비교된다.
        //
        //     이름표는 값보다 흐리고 작다(런타임이 리치 텍스트로 넣는다).
        //     읽어야 하는 것은 숫자지 "공격력" 세 글자가 아니다.
        var statRow = EditorUIBuilder.Go("StatRow", body);
        EditorUIBuilder.AnchorTop(statRow.GetComponent<RectTransform>(), y, UIScale.RowMd, 12f);

        var stat      = HalfStat(statRow, "Attack", leftHalf: true);
        var statRight = HalfStat(statRow, "Hp",     leftHalf: false);

        y += UIScale.RowMd + 4f;

        // ── 시너지 칩 줄 — [숲] 2/3   [재생] 1/2 ────────────
        //
        //   ⚠ 카드를 700 으로 키우며 비워 둔 자리가 여기다
        //     이 줄이 곧 "왜 이 몬스터인가" 다. 스탯 아래·결론 위에 두어
        //     숫자를 읽고 나서 조합을 판단하는 순서가 되게 한다.
        //
        //   ⚠ 이름을 글자로 적지 않는다 — 그림이 대신한다
        //     그리고 TMP 스프라이트 태그로 글 안에 박지 않는다 (UI 규칙 7).
        //     자리가 정해진 칸이므로 진짜 Image 를 쓴다.
        //
        //   칸은 셋 — 한 종족이 갖는 표식의 최대 수다.
        //   색은 런타임이 단계에 맞춰 넣는다. 두 곳에서 정하면 갈린다.
        var synergyRow = EditorUIBuilder.Go("SynergyRow", body);
        EditorUIBuilder.AnchorTop(synergyRow.GetComponent<RectTransform>(),
                                  y, SelSynergyH, 12f);

        var synergyLayout = synergyRow.AddComponent<HorizontalLayoutGroup>();
        synergyLayout.spacing                = SelSynergyGap;
        synergyLayout.childAlignment         = TextAnchor.MiddleCenter;
        synergyLayout.childControlWidth      = true;
        synergyLayout.childControlHeight     = true;
        synergyLayout.childForceExpandWidth  = false;
        synergyLayout.childForceExpandHeight = false;

        var synergyRoots  = new GameObject[SelSynergySlots];
        var synergyIconsL = new Image[SelSynergySlots];
        var synergyHovers = new SynergyChipUI[SelSynergySlots];

        for (int i = 0; i < SelSynergySlots; i++)
        {
            GameObject chip = BuildSynergyChip(synergyRow, i,
                                               out Image chipIcon,
                                               out SynergyChipUI chipHover);
            synergyRoots[i]  = chip;
            synergyIconsL[i] = chipIcon;
            synergyHovers[i] = chipHover;
        }


        // ── 아래 붙박이 구역 — 설명(새 카드) 또는 레벨업 내역(중복) ──
        //
        //  ⚠ 위에서 쌓아 온 커서(y)를 쓰지 않고 **아래에 붙인다**
        //    이 줄은 "결론" 이라 항상 같은 자리에 있어야 눈이 먼저 간다.
        //    두 텍스트가 같은 사각형을 쓰지만 동시에 채워지는 일이 없다
        //    (CardSelectPopup.Bind 참고).
        // ⚠ 바탕과 구분선을 먼저 만든다 = 글씨보다 뒤에 그려진다
        //   "얻는 것" 이 카드에서 가장 먼저 눈에 들어와야 한다. 글씨만 키우면
        //   위쪽 설명 줄과 뒤섞여 어디부터가 결론인지 읽히지 않는다.
        BuildGainBackdrop(body);

        var desc = EditorUIBuilder.TMP(body, "Desc", "", UIScale.FontSm, FontStyles.Normal);
        AnchorBottomBlock(desc.rectTransform);
        desc.alignment     = TextAlignmentOptions.Bottom;
        desc.color         = Muted;
        desc.raycastTarget = false;

        // 계보가 깊은 종족은 패시브가 넷까지 붙는다 — 세 줄 칸을 넘치면 줄여 쓴다.
        desc.enableAutoSizing = true;
        desc.fontSizeMax      = UIScale.FontSm;
        desc.fontSizeMin      = UIScale.FontSm * 0.72f;

        // ⚠ 설명보다 한 단계 크다 (2026-09-02)
        //   이 줄이 곧 "이 카드를 고를 이유" 다. 위쪽 정보와 같은 크기면
        //   카드 세 장을 훑을 때 눈이 어디에 멈춰야 할지 알 수 없다.
        var gain = EditorUIBuilder.TMP(body, "Gain", "", UIScale.FontMd, FontStyles.Bold);
        AnchorBottomBlock(gain.rectTransform);
        gain.alignment     = TextAlignmentOptions.Bottom;
        gain.color         = Accent;
        gain.raycastTarget = false;

        // 한 줄에 하나씩 쌓는다 — 스탯 둘 + 패시브 하나가 최대라 세 줄이면 찬다.
        // 네 줄짜리 칸이라 보통은 그대로 들어가고, 넘치면 줄이지 말고 줄여 쓴다.
        gain.enableAutoSizing = true;
        gain.fontSizeMax      = UIScale.FontMd;
        gain.fontSizeMin      = UIScale.FontSm * 0.85f;

        return new CardSelectPopup.OptionView
        {
            Root      = slot,
            Button    = button,
            Icon      = icon,
            NameText  = name,
            StateText = state,
            DescText  = desc,
            StatText  = stat,
            GainText  = gain,
            ManaText  = manaText,
            ManaRoot  = manaRoot,
            CountText = countText,
            CountRoot = countRoot,
            KindText  = kind,
            StatText2 = statRight,
            SynergyRoots  = synergyRoots,
            SynergyIcons  = synergyIconsL,
            SynergyHovers = synergyHovers,
            PassiveRoots  = passiveRoots,
            PassiveIcons  = passiveIconsL,
            PassiveHovers = passiveHovers,
        };
    }

    /// <summary>
    /// 기본 스탯 한 칸. 줄의 왼쪽·오른쪽 절반을 각각 차지한다.
    ///
    /// 두 값이 늘 같은 자리에 뜨게 하려고 칸을 나눈다 — 한 문자열로 이으면
    /// 값 자릿수에 따라 가운데점이 밀려 다닌다.
    /// </summary>
    static TextMeshProUGUI HalfStat(GameObject row, string name, bool leftHalf)
    {
        var tmp = EditorUIBuilder.TMP(row, name, "", UIScale.FontSm, FontStyles.Bold);

        var rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(leftHalf ? 0f   : 0.5f, 0f);
        rt.anchorMax = new Vector2(leftHalf ? 0.5f : 1f,   1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(4f, 0f);
        rt.offsetMax = new Vector2(-4f, 0f);

        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Accent;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        // 다섯 자리(체력 12000)까지는 그대로 들어간다. 그 위는 줄여 쓴다.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax      = UIScale.FontSm;
        tmp.fontSizeMin      = UIScale.FontSm * 0.75f;

        return tmp;
    }

    /// <summary>
    /// 얻는 것 구역의 바탕과 구분선.
    ///
    /// 글씨 크기만으로는 "여기부터 결론" 이 안 읽힌다 — 카드 세 장이 나란히
    /// 있으면 크기 차이는 상대적으로만 보이기 때문이다. 면을 깔아 두면
    /// 훑는 눈이 그 사각형에 먼저 걸린다.
    /// </summary>
    static void BuildGainBackdrop(GameObject body)
    {
        float height = UIScale.RowSm * SelBottomL + 10f;

        var panel = EditorUIBuilder.Img(body, "GainBg", GainBg);
        panel.raycastTarget = false;

        var pRt = panel.rectTransform;
        pRt.anchorMin = new Vector2(0f, 0f);
        pRt.anchorMax = new Vector2(1f, 0f);
        pRt.pivot     = new Vector2(0.5f, 0f);
        pRt.anchoredPosition = new Vector2(0f, SelBottomPad - 5f);
        pRt.sizeDelta        = new Vector2(-12f, height);

        // ⚠ 구분선은 바탕의 **형제**로 둔다 (UI 규칙 3)
        //   자식으로 넣으면 부모 Image 보다 앞으로만 갈 수 있어, 바탕 위에
        //   떠 보이는 것은 같지만 정렬을 바탕 크기에 묶이게 된다.
        var line = EditorUIBuilder.Img(body, "GainDivider", BorderC);
        line.raycastTarget = false;

        var lRt = line.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 0f);
        lRt.pivot     = new Vector2(0.5f, 0f);
        lRt.anchoredPosition = new Vector2(0f, SelBottomPad - 5f + height);
        lRt.sizeDelta        = new Vector2(-12f, 2f);
    }

    /// <summary>카드 아래쪽 공용 구역에 붙인다. 설명과 레벨업 내역이 같은 자리를 쓴다.</summary>
    static void AnchorBottomBlock(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, SelBottomPad);
        rt.sizeDelta        = new Vector2(-20f, UIScale.RowSm * SelBottomL);
    }

    // ══════════════════════════════════════════════════════════
    //  ② 전투 통계
    //
    //  ⚠ 화면을 새로 짜지 않는다 — 원작 '전투 기록' 절을 그대로 세운다
    //    딜/탱/힐 탭 + 세그먼트 프로그레스 바 + 세로 스크롤 목록.
    //    행은 GeneralStatRow.prefab 을 그대로 쓴다
    //    (ReincarnationPopupCreator 가 굽는 것과 **같은 프리팹**이다).
    //
    //    한때 이 자리에 전용 줄 레이아웃을 손으로 짰다가 프로그레스 바도
    //    탭도 없는 화면이 됐다. 필요한 조각이 전부 있는데 새로 만들 이유가 없다.
    // ══════════════════════════════════════════════════════════

    const float StatW = 1180f;
    const float StatH = UIScale.PopupMaxH;   // 1000 — 캔버스 세로 한계

    const float StatTabY  = HeaderH + 74f;   // 탭 띠 상단
    const float StatTabH  =  84f;
    const float StatListY = StatTabY + StatTabH + 14f;

    /// <summary>닫기 버튼(아래 24 + 높이 BtnMd) 위로 목록이 멈추는 높이.</summary>
    const float StatBottom = 24f + UIScale.BtnMd + 18f;

    [MenuItem(ProjectKMenu.Popup + "전투 통계", priority = ProjectKMenu.PrefabPrio + 42)]
    public static void CreateBattleStats()
    {
        // 행 프리팹은 환생 팝업과 공유한다 — 없으면 그쪽 생성기가 굽는다.
        var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SaveRoot}/GeneralStatRow.prefab");
        if (rowPrefab == null)
        {
            ReincarnationPopupCreator.CreateGeneralStatRowPrefab();
            rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SaveRoot}/GeneralStatRow.prefab");
        }

        GameObject root = MakeRoot("BattleStatsPopup", PopupType.BattleStats,
                                   out GameObject panel, StatW, StatH);

        var ui = root.AddComponent<BattleStatsPopup>();

        TextMeshProUGUI title = MakeHeader(panel, "결 산", "전투 통계");

        // 총 피해 · DPS — 헤더 바로 아래 한 줄.
        var total = EditorUIBuilder.TMP(panel, "TotalText", "총 피해 0  |  DPS 0",
                                        UIScale.FontMd, FontStyles.Bold);
        EditorUIBuilder.AnchorTop(total.rectTransform, HeaderH + 12f, UIScale.RowMd, 32f);
        total.alignment        = TextAlignmentOptions.Center;
        total.color            = Accent;
        total.raycastTarget    = false;
        total.textWrappingMode = TextWrappingModes.NoWrap;

        // ── 딜 · 탱 · 힐 탭 ──────────────────────────────────
        var (tabButtons, tabBars) = BuildTabBar(panel);

        // ── 목록 (세로 스크롤) ───────────────────────────────
        //   카드 칸이 여덟이라 행이 여덟까지 온다. 고정 칸으로 잡으면
        //   반드시 넘치므로 원작 EXP 목록과 같은 ScrollRect 로 감싼다.
        var box = EditorUIBuilder.Go("ListBox", panel);
        {
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(28f, StatBottom);
            rt.offsetMax = new Vector2(-28f, -StatListY);
        }
        var scroll = box.AddComponent<ScrollRect>();

        var viewport = EditorUIBuilder.Go("Viewport", box);
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);  // Mask 는 Graphic 필요
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        EditorUIBuilder.Stretch(viewport.GetComponent<RectTransform>());

        var content = EditorUIBuilder.Go("RowArea", viewport);
        {
            var rt = content.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
        }
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 6f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperCenter;

        scroll.horizontal   = false;
        scroll.vertical     = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.viewport     = viewport.GetComponent<RectTransform>();
        scroll.content      = content.GetComponent<RectTransform>();

        var empty = EditorUIBuilder.TMP(box, "EmptyText",
                                        "아직 아무도 때리지 않았습니다.",
                                        UIScale.FontMd, FontStyles.Normal);
        EditorUIBuilder.Stretch(empty.gameObject);
        empty.alignment     = TextAlignmentOptions.Center;
        empty.color         = Muted;
        empty.raycastTarget = false;

        Button close = EditorUIBuilder.RaisedTextBtn(panel, "CloseBtn", "닫 기",
                                                     UIScale.FontLg, StatsFace);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta        = new Vector2(400f, UIScale.BtnMd);
        }

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_titleText",   title, Tag);
        EditorUIBuilder.SetObj(so, "_totalText",   total, Tag);
        EditorUIBuilder.SetObj(so, "_closeButton", close, Tag);
        EditorUIBuilder.SetObj(so, "_rowArea",     content.transform, Tag);
        EditorUIBuilder.SetObj(so, "_emptyText",   empty, Tag);

        if (rowPrefab != null)
            EditorUIBuilder.SetObj(so, "_rowTemplate",
                                   rowPrefab.GetComponent<GeneralStatRowUI>(), Tag);
        else
            Debug.LogWarning($"[{Tag}] GeneralStatRow.prefab 이 없어 행 템플릿을 비워 둡니다.");

        FillArray(so, "_tabButtons",   tabButtons);
        FillArray(so, "_tabButtonBgs", tabBars);

        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "BattleStatsPopup");
    }

    /// <summary>
    /// 딜 · 탱 · 힐 탭 띠. 원작 BattleResultPopup·ReincarnationPopup 과 같은 구조다.
    ///
    /// ⚠ 활성 표시는 버튼 면 색이 아니라 하단 강조바로 한다
    ///   RaisedBtnOn 은 TopEdge/BottomEdge 색을 면 색에서 **구워 넣는다.**
    ///   런타임에 Body 색만 바꾸면 모서리가 따라오지 않아 어긋난다.
    /// </summary>
    static (Button[], Image[]) BuildTabBar(GameObject panel)
    {
        var bar = EditorUIBuilder.Go("TabBar", panel);
        EditorUIBuilder.AnchorTop(bar.GetComponent<RectTransform>(),
                                  StatTabY, StatTabH, (StatW - 900f) * 0.5f);

        var hlg = bar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 10f;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        string[] labels = { "딜", "탱", "힐" };
        var buttons = new Button[3];
        var bars    = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            var slot = EditorUIBuilder.Go($"TabBtn{i}", bar);
            Button btn = EditorUIBuilder.RaisedBtnOn(slot, RowBg, out GameObject body);

            var label = EditorUIBuilder.TMP(body, "Label", labels[i],
                                            UIScale.FontMd, FontStyles.Bold);
            EditorUIBuilder.Stretch(label.gameObject);
            label.alignment     = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var active = EditorUIBuilder.Img(body, "ActiveBar",
                                             i == 0 ? Accent : Color.clear);
            active.raycastTarget = false;
            {
                var rt = active.rectTransform;
                rt.anchorMin        = new Vector2(0.08f, 0f);
                rt.anchorMax        = new Vector2(0.92f, 0f);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 5f);   // BottomEdge(4px) 바로 위
                rt.sizeDelta        = new Vector2(0f, 5f);
            }

            buttons[i] = btn;
            bars[i]    = active;
        }

        return (buttons, bars);
    }

    static void FillArray(SerializedObject so, string field, Object[] values)
    {
        SerializedProperty arr = so.FindProperty(field);
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }


    // ══════════════════════════════════════════════════════════
    //  ⚠ 런 종료 결산 화면은 여기서 만들지 않는다 (2026-08-28)
    //
    //    런이 끝났을 때 플레이어가 받아야 하는 것은 "다시 도전" 이 아니라
    //    **환생 포인트**다. 그 화면은 원작에 이미 있다 —
    //    ReincarnationPopup(+ ReincarnationPopupCreator): 딜/탱/힐 탭,
    //    세그먼트 바, 획득 포인트 미리보기, 환생 버튼까지 갖췄고
    //    초기화 목록도 UserDataManager.Reincarnate() 하나가 소유한다.
    //
    //    한때 이 자리에 RunDefeatPopup 을 따로 만들었다가 그 전부를 다시
    //    짜고 있었다. 지금은 RunBootstrap.HandleDefeat 가 환생 팝업을 연다.
    // ══════════════════════════════════════════════════════════


    // ══════════════════════════════════════════════════════════
    //  공통 조각
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 전체화면 오버레이 + 가운데 패널. 세 팝업이 같은 틀을 쓴다.
    ///
    /// ⚠ 테두리는 패널의 **앞 형제**로 만든다 (UI 규칙 3)
    ///   자식으로 두면 패널 그래픽보다 뒤로 갈 수 없어 팝업 전체를 덮는다.
    /// </summary>
    static GameObject MakeRoot(string name, PopupType type,
                               out GameObject panel, float w, float h)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.GetComponent<Image>().color = Overlay;
        EditorUIBuilder.Stretch(root.GetComponent<RectTransform>());

        var border = EditorUIBuilder.Panel(root, "Border", BorderC);
        SetRect(border.GetComponent<RectTransform>(), new Vector2(w + Outset, h + Outset));

        panel = EditorUIBuilder.Panel(root, "Panel", PanelBg);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(w, h));

        // PopupBase._popupType 은 [SerializeField] 라 컴포넌트를 붙인 뒤에 채운다.
        // 붙이는 것은 각 Create* 가 하고, 여기서는 값만 예약해 둔다.
        _pendingType = type;

        return root;
    }

    static PopupType _pendingType;

    /// <summary>제목 줄 — 왼쪽 태그(◆ 라벨) + 가운데 제목.</summary>
    static TextMeshProUGUI MakeHeader(GameObject panel, string tag, string title)
    {
        var header = EditorUIBuilder.Panel(panel, "Header", HeaderBg);
        EditorUIBuilder.AnchorTop(header.GetComponent<RectTransform>(), 0f, HeaderH);

        // ★ 는 폰트에 없다 — 마름모 도형으로 그린다 (UI 규칙 2)
        var mark = EditorUIBuilder.Diamond(header, "Mark", 16f, Accent);
        {
            var r = mark.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(34f, 0f);
        }

        var tagTmp = EditorUIBuilder.TMP(header, "Tag", tag, UIScale.FontSm, FontStyles.Bold);
        {
            var r = tagTmp.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot     = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(62f, 0f);
            r.sizeDelta        = new Vector2(240f, UIScale.RowSm);
        }
        tagTmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tagTmp.color            = Accent;
        tagTmp.raycastTarget    = false;
        tagTmp.textWrappingMode = TextWrappingModes.NoWrap;

        var titleTmp = EditorUIBuilder.TMP(header, "TitleText", title,
                                           UIScale.FontLg, FontStyles.Bold);
        EditorUIBuilder.Stretch(titleTmp.gameObject);
        titleTmp.alignment        = TextAlignmentOptions.Center;
        titleTmp.color            = new Color(1f, 0.94f, 0.78f);
        titleTmp.raycastTarget    = false;
        titleTmp.textWrappingMode = TextWrappingModes.NoWrap;

        return titleTmp;
    }

    /// <summary>헤더 오른쪽의 '전투 통계' 버튼.</summary>
    static Button MakeStatsButton(GameObject panel)
    {
        Button btn = EditorUIBuilder.RaisedTextBtn(panel, "StatsBtn", "전투 통계",
                                                   UIScale.FontSm, StatsFace);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -(HeaderH - UIScale.BtnSm) * 0.5f);
        rt.sizeDelta        = new Vector2(280f, UIScale.BtnSm);

        return btn;
    }

    static void PinBottom(RectTransform rt, float y, float h, float sidePad)
    {
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.offsetMin = new Vector2( sidePad, y);
        rt.offsetMax = new Vector2(-sidePad, y + h);
    }

    static void SetRect(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;
    }

    /// <summary>
    /// 프리팹으로 저장한다.
    ///
    /// ⚠ PopupBase._popupType 을 여기서 채운다
    ///   [SerializeField] 라 코드로 대입할 수 없다. 컴포넌트가 다 붙은 뒤인
    ///   이 자리에서 SerializedObject 로 써야 한다 — 비워 두면 PopupManager 가
    ///   None 으로 등록해 Open(PopupType.X) 이 영영 못 찾는다.
    /// </summary>
    static void Save(GameObject root, string fileName)
    {
        var popup = root.GetComponent<PopupBase>();
        if (popup != null)
        {
            var so = new SerializedObject(popup);
            // ⚠ enumValueIndex 가 아니라 intValue 다
            //   enumValueIndex 는 enum 값 목록에서의 **자리 번호**라, 값에 구멍이
            //   있으면(PopupType 은 20 이 비어 있다) 엉뚱한 팝업을 가리킨다.
            //   0~19 는 자리와 값이 같아 지금껏 드러나지 않았을 뿐이다.
            so.FindProperty("_popupType").intValue = (int)_pendingType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        string path = $"{SaveRoot}/{fileName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log($"[{Tag}] 저장: {path}");
    }

    // ══════════════════════════════════════════════════════════
    //  ③ 선택 목록 — 갈림길·특성·시설이 전부 이 하나를 쓴다
    // ══════════════════════════════════════════════════════════
    //
    //  ■ ⚠ 가로 줄 목록에서 **세로 카드**로 갈아엎었다 (사용자 지시, 2026-09-10)
    //    옛 모습은 [아이콘][이름][설명]이 가로로 긴 줄 여덟이었다. 로그라이트의
    //    선택 화면은 어디서나 **나란히 선 카드**다 — 가로 줄 목록은 설정 메뉴나
    //    상점 재고의 모습이라, 같은 화면이 "고르는 자리" 로 안 읽힌다.
    //    카드 3택(CardSelectPopup)과 같은 결로 맞춰 "고르는 화면은 카드" 를
    //    이 게임의 규칙으로 만든다.
    //
    //    ⚠ 옛 주석은 "그림이 없고 문장 한 줄이 전부라 가로가 낫다" 였다.
    //      그 전제가 깨졌다 — 2026-09-07 에 그림 칸이 생겼고, 지금 이 창을
    //      쓰는 곳은 **특성 선택 하나뿐**이라 모든 줄에 아이콘이 있다.
    //
    //  ■ ⚠ 쓰는 곳은 특성 선택 하나다 (2026-09-10 확인)
    //    갈림길은 CrossroadUI, 시설·이벤트는 FacilityPopup, 강화소·제단은
    //    CardPickPopupBase 로 각자 빠져나갔다. PopupType.Choice 를 여는 곳은
    //    RunBootstrap.OfferPerk 뿐이다 — 그래서 카드로 바꿔도 곁가지가 없다.
    //
    //  ■ 칸 수는 MaxRows(8) 를 굽고 런타임이 가운데로 다시 세운다
    //    실제로 뜨는 수는 1(엘리트) · 3(보스) · 최대 5(유물 '전승의 대가' 2단계).
    //    ⚠ 여덟 장은 가로에 안 들어간다 — 아래 Verify 가 **다섯 장까지**를 검산한다.
    //      그 위는 남는 칸이라 화면에 서지 않는다(Setup 이 숨긴다).
    //
    //  ■ 가로 검산 (5장 기준)
    //    카드 284 × 5 + 간격 28 × 4 = 1532 ≤ 카드 영역(PerkW − 여백 44×2 = 1532) ✔
    //    창 1620 ≤ 캔버스 1920 ✔
    //
    //  ■ 세로 검산
    //    헤더 136 + 위 여백 24 + 카드 640 + 아래 여백 40 = 840 ≤ PopupMaxH(1000) ✔
    //    카드 안쪽: 여백 22 + 그림 156 + 12 + 이름 53 + 10 + 선 2 + 14 + 설명 349 + 22 = 640
    //    설명 예산 7자 × 8줄 = 56자 ≥ 가장 긴 특성 설명 40자 ✔ (아래 Verify 가 잰다)

    const float PerkW    = 1620f;
    const float PerkH    =  840f;

    /// <summary>카드 한 장. ⚠ 다섯 장이 가로에 들어가는 상한이다 (Verify).</summary>
    const float PerkCardW = 284f;
    const float PerkCardH = 640f;
    const float PerkGap   =  28f;

    /// <summary>창 좌우 여백. 카드 영역은 PerkW 에서 이만큼 양쪽을 뺀 값이다.</summary>
    const float PerkSideMargin = 44f;

    /// <summary>카드 줄의 위 — 헤더 아래 한 눈금.</summary>
    const float PerkTop = HeaderH + 24f;

    /// <summary>카드 안쪽 여백. 네 변이 같은 값을 쓴다.</summary>
    const float PerkPad = 22f;

    /// <summary>
    /// 카드 그림. <b>카드의 주인공이다</b> — 아는 특성은 글을 안 읽고 지나가게 한다.
    /// ⚠ 줄 목록 시절의 68 에서 크게 키웠다. 세로 카드에서 68 은 장식으로 보인다.
    /// </summary>
    const float PerkIconSz = 156f;

    /// <summary>특성 카드의 면 — 카드 3택과 같은 결의 어두운 남색이다.</summary>
    static readonly Color PerkFace = new(0.150f, 0.165f, 0.275f, 1f);

    /// <summary>이름과 설명을 가르는 선. 카드 3택의 구분선과 같은 뜻이다.</summary>
    static readonly Color PerkDivider = new(1f, 0.93f, 0.72f, 0.20f);

    [MenuItem(ProjectKMenu.Popup + "선택 목록", priority = ProjectKMenu.PrefabPrio + 43)]
    public static void CreateChoice()
    {
        GameObject root = MakeRoot("ChoicePopup", PopupType.Choice,
                                   out GameObject panel, PerkW, PerkH);

        var ui = root.AddComponent<ChoicePopup>();

        TextMeshProUGUI title = MakeHeader(panel, "선택", "하나를 고르세요");

        // 카드는 가운데 정렬 — 실제로 뜨는 수는 런타임이 다시 잡는다(ChoicePopup.Recenter).
        var options = new ChoicePopup.RowView[ChoicePopup.MaxRows];
        float step  = PerkCardW + PerkGap;
        float start = -(ChoicePopup.MaxRows - 1) * 0.5f * step;

        for (int i = 0; i < options.Length; i++)
            options[i] = BuildPerkOption(panel, i, start + i * step);

        VerifyChoiceCards();

        var so = new SerializedObject(ui);
        EditorUIBuilder.SetObj(so, "_titleText", title, Tag);
        so.FindProperty("_cardStep").floatValue = step;

        SerializedProperty arr = so.FindProperty("_rows");
        arr.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("Root")    .objectReferenceValue = options[i].Root;
            e.FindPropertyRelative("Button")  .objectReferenceValue = options[i].Button;
            e.FindPropertyRelative("Icon")    .objectReferenceValue = options[i].Icon;
            e.FindPropertyRelative("NameText").objectReferenceValue = options[i].NameText;
            e.FindPropertyRelative("DescText").objectReferenceValue = options[i].DescText;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "ChoicePopup");
    }

    /// <summary>
    /// 특성 카드 한 장 — 위에서부터 [그림] [이름] [구분선] [설명].
    ///
    ///     ┌──────────────┐
    ///     │   ▓▓▓▓▓▓▓▓   │  그림이 주인공 (168)
    ///     │              │
    ///     │   과잉 소환   │  이름 — 강조색, 굵게
    ///     │ ──────────── │  구분선
    ///     │  배출 간격이  │  설명 — 가운데 정렬, 여러 줄
    ///     │  20% 짧아진다 │
    ///     └──────────────┘
    ///
    /// ⚠ 라벨은 반드시 <b>body</b> 아래에 넣는다 (UI 규칙 1)
    ///   루트에 넣으면 버튼이 눌려도 글자가 안 내려가 눌린 느낌이 사라진다.
    /// ⚠ 가운데 정렬이다 — 줄 목록 시절의 왼쪽 정렬을 그대로 가져오면
    ///   좁은 카드 안에서 글이 한쪽으로 쏠려 빈 공간이 크게 남는다.
    /// </summary>
    static ChoicePopup.RowView BuildPerkOption(GameObject panel, int index, float x)
    {
        var slot = EditorUIBuilder.Go($"Perk{index}", panel);
        {
            var r = slot.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(x, -PerkTop);
            r.sizeDelta        = new Vector2(PerkCardW, PerkCardH);
        }

        Button btn = EditorUIBuilder.RaisedBtnOn(slot, PerkFace, out GameObject body);

        // ── 그림 — 카드 위쪽 가운데 ──
        //   ⚠ 여기서 그림을 넣지 않는다. 무엇이 뜰지 런타임에 정해진다
        //     (특성 30종 중 셋).
        //   ⚠ 꺼진 채로 굽는다 — 켜 두면 그림 없는 카드에 흰 사각형이 남는다.
        //   ⚠ 그림이 없어도 **자리는 비워 둔다** — 카드마다 이름 높이가
        //     달라지면 나란히 선 셋이 들쭉날쭉해 보인다.
        var icon = EditorUIBuilder.Img(body, "Icon", Color.white);
        icon.sprite         = null;
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        icon.enabled        = false;
        {
            var r = icon.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -PerkPad);
            r.sizeDelta        = new Vector2(PerkIconSz, PerkIconSz);
        }

        float nameTop = PerkPad + PerkIconSz + 12f;

        // 이름 — 그림 아래. FontMd 로 한 단계 키운다(카드는 이름이 크게 읽혀야 한다).
        var name = EditorUIBuilder.TMP(body, "NameText", "특성 이름",
                                       UIScale.FontMd, FontStyles.Bold);
        {
            var r = name.rectTransform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(10f, -(nameTop + UIScale.RowMd));
            r.offsetMax = new Vector2(-10f, -nameTop);
        }
        name.alignment        = TextAlignmentOptions.Midline;
        name.color            = Accent;
        name.raycastTarget    = false;
        name.textWrappingMode = TextWrappingModes.NoWrap;

        // 구분선 — 이름과 설명을 가른다.
        //   ⚠ 반투명 테두리를 자식으로 두는 것과 다르다 (UI 규칙 3) —
        //     이건 형제가 아니라 body 안의 얇은 면이라 겹침 문제가 없다.
        float lineTop = nameTop + UIScale.RowMd + 10f;
        {
            var line = EditorUIBuilder.Img(body, "Divider", PerkDivider);
            line.raycastTarget = false;

            var r = line.rectTransform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(PerkPad, -(lineTop + 2f));
            r.offsetMax = new Vector2(-PerkPad, -lineTop);
        }

        // 설명 — 구분선 아래를 전부 쓴다. 어두운 면 위라 밝은 글자다 (UI 규칙 8).
        var desc = EditorUIBuilder.TMP(body, "DescText", "무엇을 하는 특성인가",
                                       UIScale.FontSm, FontStyles.Normal);
        {
            var r = desc.rectTransform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot     = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(PerkPad, PerkPad);
            r.offsetMax = new Vector2(-PerkPad, -(lineTop + 2f + 14f));
        }
        desc.alignment     = TextAlignmentOptions.Top;
        desc.color         = Muted;
        desc.raycastTarget = false;

        return new ChoicePopup.RowView
        {
            Root     = slot,
            Button   = btn,
            Icon     = icon,
            NameText = name,
            DescText = desc,
        };
    }

    /// <summary>
    /// 특성 카드 검산 — <b>조용한 실패</b>를 시끄럽게 바꾼다.
    ///
    /// ⚠ 넘치면 에러가 안 난다. 카드가 창 밖으로 나가 잘리거나 겹칠 뿐이다.
    /// </summary>
    static void VerifyChoiceCards()
    {
        // 실제로 화면에 설 수 있는 최대 — 보스 3택 + 유물 '전승의 대가' 2단계.
        const int MaxShown = 5;

        float need  = PerkCardW * MaxShown + PerkGap * (MaxShown - 1);
        float space = PerkW - PerkSideMargin * 2f;

        if (need > space)
            Debug.LogError($"[{Tag}] 특성 카드 {MaxShown}장이 창을 넘칩니다 " +
                           $"({need:0} > {space:0}) — PerkCardW 를 줄이거나 PerkW 를 넓히세요.");

        float tall = PerkTop + PerkCardH + 40f;
        if (tall > PerkH)
            Debug.LogError($"[{Tag}] 특성 카드가 창 아래로 넘칩니다 ({tall:0} > {PerkH:0}).");

        if (PerkH > UIScale.PopupMaxH)
            Debug.LogError($"[{Tag}] 창이 캔버스 세로 한계를 넘습니다 " +
                           $"({PerkH:0} > {UIScale.PopupMaxH}) — 위아래가 잘립니다 (UI 규칙 6).");

        // 설명이 카드 안에 들어가는가 — 가장 긴 특성 설명으로 잰다.
        float descH = PerkCardH - (PerkPad + PerkIconSz + 12f + UIScale.RowMd + 10f + 2f + 14f)
                    - PerkPad;
        float textW = PerkCardW - PerkPad * 2f;

        int budget = Mathf.FloorToInt(textW / UIScale.FontSm)
                   * Mathf.FloorToInt(descH / UIScale.Line(UIScale.FontSm));

        var longest = "";
        foreach (RunPerk perk in System.Enum.GetValues(typeof(RunPerk)))
        {
            if (perk == RunPerk.None) continue;

            string d = perk.Describe();
            if (d != null && d.Length > longest.Length) longest = d;
        }

        if (longest.Length > budget)
            Debug.LogError($"[{Tag}] 특성 설명이 카드를 넘칩니다 — {longest.Length}자 > {budget}자\n" +
                           $"  {longest}\n" +
                           "  PerkCardH 를 늘리거나 PerkCardW 를 넓히세요. " +
                           "글자를 FontSm 밑으로 줄이지는 말 것 — UI 규칙 4.");
    }
}
#endif
