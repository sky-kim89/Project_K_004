using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  MonsterDetailPopupCreator.cs
//  Tools > Project K > 프리팹 생성 > 팝업 > 몬스터 상세
//
//  ┌──────────────────────────────────────────────────────────────┐  ← 전체 화면
//  │ ◆ 힐 슬라임   슬라임 계열 · 물량        [금] 12,400     [ × ] │
//  ├───────────────┬────────────────────┬─────────────────────────┤
//  │   (초상화)     │  스 탯              │  종 족 패 시 브          │
//  │   ◈ 고급      │  체력    99 › 109   │  [▨] 분열  …설명…       │
//  │   [◈8] [☠4]  │  공격력  5.5 › 6.1  │  [▨] 치유의 잔재 …      │
//  │   숲  재생     │  방어율  8%         │                         │
//  │               │  …                  │                         │
//  │ ┌───────────┐ │  장 비              │  고 유 스 킬            │
//  │ │● ● ○ ○ ○  │ │ [▨ ][비었][🔒희귀] │  [▨] 강타      15초     │
//  │ │ 고급 › 희귀│ │  칸 2개 — 품질을…   │      …설명…             │
//  │ │[ 품질 개선 │ │                     │                         │
//  │ │   [금]900 ]│ │                     │                         │
//  │ └───────────┘ │                     │                         │
//  └───────────────┴────────────────────┴─────────────────────────┘
//         칸을 누르면 ↑ 가운데·오른쪽을 덮는 목록이 떠서 장비를 고른다
//
//  ■ 장비 칸은 가운데 아래 — 품질 개선 패널 바로 옆이다
//    ⚠ 칸 수를 정하는 것이 **몬스터의 품질**이다 (사용자 확정, 2026-09-06)
//      장비 등급이 아니다. 품질을 올린 손이 바로 옆에서 칸이 열리는 것을
//      봐야 두 구역이 하나의 이야기로 읽힌다.
//    ⚠ 잠긴 칸도 그린다 — 감추면 칸이 더 있다는 사실 자체를 모른다
//
//  ■ 왼쪽 아래 = 품질 개선 패널 (사용자 지적으로 다시 짰다, 2026-09-06)
//    옛 모습은 버튼 하나 위에 "900 (보유 12,400)" 한 줄이 전부였다. 골드가
//    드는 일이라는 사실도, 몇 칸 남았는지도, 무엇이 오르는지도 안 보였다.
//    지금은 네 층이다 —
//      ① 단계 핍 ● ● ○ ○ ○   (몇 칸 남았나)
//      ② 고급 › 희귀           (다음이 무엇인가)
//      ③ 버튼 [품질 개선][금 900]  (값은 누를 것과 한 몸이다)
//      ④ 지갑은 헤더           (붙여 두면 어느 쪽이 낼 돈인지 헷갈린다)
//    스탯 행의 "99 › 109" 가 ①~③ 이 사는 값을 숫자로 확인해 준다.
//
//  ■ 고유 액티브 스킬은 오른쪽 칸 **맨 아래**다 (사용자 지적, 2026-09-07)
//    멧돼지·리치처럼 ActiveSkill 을 가진 종족이 여럿이라 칸을 만들었지만,
//    **없는 종족이 더 많다.** 맨 위에 두면 대부분의 종족에서 251px 짜리
//    빈 자리가 오른쪽 칸 머리에 남고, 종족 패시브가 그 아래로 밀려
//    "이 몬스터가 뭘 하는 애인가" 를 스크롤 없이 읽던 것을 못 읽게 된다.
//    자리를 맨 아래로 내리면 없는 종족에서는 칸이 꺼져도 위쪽 짜임새가
//    그대로다 — 있는 종족만 아래에 한 덩어리가 더 붙는다.
//    ⚠ 그래서 자리를 고정으로 잡는다 — 위 블록이 y 를 밀지 않는다
//
//  ⚠ 스탯 행 이름표는 MonsterDetailPopup.StatRows 가 정본이다
//    여기서 이름을 손으로 적으면 런타임이 넣는 값과 순서가 어긋나
//    "방어율 칸에 이동속도가 뜨는" 상태가 된다.
//
//  ⚠ 마나·마릿수·골드는 글자가 아니라 아이콘이다 (UI 규칙 7)
//    "마나 7" · "골드 900" 이라고 적지 않는다. EditorUIBuilder 로 배지를 만든다.
// ============================================================

public static class MonsterDetailPopupCreator
{
    const string SavePath = "Assets/_project/2.Prefabs/UI/MonsterDetailPopup.prefab";
    const string Tag      = "MonsterDetailPopupCreator";

    const float HeaderH = 118f;
    const float Pad     = 26f;

    // 3단 — 왼쪽(초상화·표식·품질 개선) / 가운데(스탯) / 오른쪽(스킬·패시브)
    const float LeftW  = 470f;
    const float MidW   = 560f;
    const float ColGap = 20f;

    const float PortraitSize = 300f;
    const float RowGap       = 6f;

    // 품질 개선 패널 (왼쪽 칸 맨 아래)
    const float PanelH   = 268f;
    const float PipSize  = 26f;
    const float PipGap   = 10f;
    const float CostIcon = 40f;
    /// <summary>품질 개선 버튼에서 글과 값을 가르는 자리 (0~1). 값 칸이 "40,000" 을 담아야 한다.</summary>
    const float CostSplit = 0.5f;

    /// <summary>
    /// 능력 칸 수 — 계보가 가장 깊은 종족이 갖는 패시브 수보다 넉넉히.
    /// 지금 데이터의 계보는 (뿌리 → 진화) 두 단이라 실제로 붙는 것은 둘까지다.
    ///
    /// ⚠ 칸을 늘리기 전에 오른쪽 칸의 세로 예산을 다시 계산할 것
    ///   여백 26 + 제목 61 + 소개 96 + 칸×(PassiveH+6) + 고유 스킬 251 ≤ 910.
    ///   쓸 수 있는 것은 476 이다.
    ///
    /// ⚠ 3 → 5 (사용자 지적, 2026-09-07)
    ///   융합이 **최대 3개**를 더 얹는다(CardEvolution.MaxLearned). 계보가 두 단인
    ///   종족이면 2 + 3 = 5 라 3칸으로는 배운 것이 화면에서 잘려 나갔다.
    ///   5 × (88 + 6) = 470 ≤ 476 ✔ — 그래서 PassiveH 도 140 → 88 로 줄였다.
    ///
    /// ⚠ 5 → 6 (사용자 지적, 2026-09-09)
    ///   카드 레벨이 여는 패시브(PassiveSkillType)도 여기에 함께 그린다 —
    ///   종족마다 하나씩 있다(슬라임 Lv4 = 피격 시 방어율 증가). 계보 2 + 레벨 1
    ///   + 융합 3 = 6 이다.
    ///   ⚠ 고정 자리로는 세로가 88px 모자란다 (6 × 94 = 564 > 476).
    ///     그래서 **고유 스킬 칸을 런타임이 옮긴다** —
    ///     MonsterDetailPopup.LayoutSkillSection 이 쓰는 칸 수에 맞춰 붙여 올리고,
    ///     여섯 칸 + 스킬이 함께 오는 종족(화염 멧돼지)에서는 상자를 줄인다.
    ///     여기서 잡는 자리는 그 계산의 **기준값**이다 — 칸 높이·간격을 바꾸면
    ///     런타임이 프리팹에서 다시 재므로 따로 고칠 것은 없다.
    /// </summary>
    ///
    /// ⚠ 이 수가 곧 **줄 모드의 상한**이다 (사용자 지시, 2026-09-10)
    ///   이보다 많으면 런타임이 줄을 끄고 아이콘 격자로 바꾼다 (MonsterDetailPopup.FillPassiveGrid).
    ///   사용자는 8 을 원했지만 8줄 = 752px 로 칸(727)을 넘어 **스킬 칸이 없어도** 깨진다 —
    ///   그래서 지금 칸이 버티는 6 에서 바꾼다.
    const int PassiveSlots = 6;

    // ── 아이콘 모드 격자 ──
    //  ⚠ 칸 수는 가질 수 있는 패시브의 최대치 이상이어야 한다 — 모자라면 조용히 잘린다.
    //    계보 2 + 카드 레벨 1 + 융합 3 + 장비 (칸 3 × Lv4·Lv5) 6 = 12.
    const float PassiveIconSize = 72f;
    const float PassiveIconGap  = 12f;
    const int   PassiveIconMax  = 18;
    const int   PassiveMaxOwned = 12;

    /// <summary>오른쪽 칸의 폭 — Column 이 오른쪽 끝까지 늘리므로 기준 캔버스 폭에서 뺀다.</summary>
    static float RightColW => 1920f - Pad * 2f - (LeftW + ColGap + MidW + ColGap);

    /// <summary>한 줄에 들어가는 아이콘 수 (안쪽 여백 18 × 2 를 뺀 폭 기준).</summary>
    static int PassiveIconCols
        => Mathf.FloorToInt((RightColW - 36f + PassiveIconGap) / (PassiveIconSize + PassiveIconGap));

    /// <summary>시너지 표식 칸 수 — 종족 하나가 두세 개를 갖는다 (MonsterTag).</summary>
    const int TagSlots = 3;

    // 고유 스킬 칸 — 제목(RowMd) + 여백 8 + 설명 상자. 셋을 따로 적으면 어긋난다.
    static readonly float SkillBoxH     = 190f;
    static readonly float SkillSectionH = UIScale.RowMd + 8f + 190f;

    // 장비 칸 — 3칸이 가운데 칸(560 − 안쪽 18 = 542)에 들어가야 한다.
    // 3×160 + 2×20 = 520 ≤ 542 ✔  ⚠ 폭을 손대면 이 검산을 다시 할 것.
    const float GearSlotW = 160f;
    const float GearSlotH = 152f;

    // 장비 고르기 목록의 칸. 4열 × (가운데+오른쪽 폭 − 여백) 에 맞춰 잡았다.
    const float PickCellW = 210f;
    const float PickCellH = 210f;
    const int   PickColumns = 5;

    // 스킬 상자 안의 세로선 — 아이콘(14 + 76)의 오른쪽. 이름과 설명이 여기서 시작한다.
    // ⚠ 설명을 상자 왼쪽 끝에서 시작하면 첫 줄이 아이콘과 겹친다.
    const float SkillIconSize = 76f;
    const float SkillTextX    = 14f + SkillIconSize + 14f;
    const float SkillCoolW    = 140f;

    static readonly Color MonsterC = new Color(0.68f, 0.90f, 0.42f, 1f);
    static readonly Color GoldC    = new Color(1.00f, 0.83f, 0.30f, 1f);
    static readonly Color ScrimBg  = new Color(0.035f, 0.040f, 0.070f, 0.97f);
    static readonly Color ColBg    = new Color(0.055f, 0.062f, 0.105f, 1f);
    static readonly Color RowBg    = new Color(0.105f, 0.115f, 0.180f, 1f);
    static readonly Color PanelBg  = new Color(0.085f, 0.075f, 0.135f, 1f);
    static readonly Color SubText  = new Color(0.62f, 0.67f, 0.82f, 1f);
    static readonly Color CountC   = new Color(1.00f, 0.96f, 0.85f, 1f);

    [MenuItem(ProjectKMenu.Popup + "몬스터 상세", priority = ProjectKMenu.PrefabPrio + 44)]
    public static void Run()
    {
        // ⚠ 아이콘이 없으면 굽지 않는다
        //   빈 배지·빈 칩을 구워 두면 프리팹만 보고는 무엇이 빠졌는지 알 수 없다
        //   (UIIconAssets · SpeciesPassiveIconAssets 의 계약과 같다).
        if (!UIIconAssets.TryLoad(Tag, out Sprite manaIcon, out Sprite countIcon)) return;
        if (!UIIconAssets.TryLoadGold(Tag, out Sprite goldIcon))                   return;
        if (!SpeciesPassiveIconAssets.TryLoad(Tag, out Sprite[] passiveIcons))     return;
        if (!SynergyIconAssets.TryLoad(Tag, out Sprite[] synergyIcons))            return;

        var root = new GameObject("MonsterDetailPopup", typeof(RectTransform));
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<MonsterDetailPopup>();

        EditorUIBuilder.Stretch(root);

        var scrim = EditorUIBuilder.Img(root, "Scrim", ScrimBg);
        EditorUIBuilder.Stretch(scrim.gameObject);

        BuildHeader(root, goldIcon, out var nameText, out var lineageText,
                    out var goldText, out var closeBtn);

        BuildLeft(root, manaIcon, countIcon, goldIcon, out var left);
        BuildMid(root, out var statValues, out var midGear);
        BuildRight(root, countIcon, out var right);

        // ⚠ 마지막에 만든다 — 가운데·오른쪽 칸을 덮어야 한다
        //   Unity UI 는 형제 순서대로 그린다. 먼저 만들면 칸들 밑에 깔린다.
        BuildPicker(root, out var pick);

        var so = new SerializedObject(popup);
        EditorUIBuilder.SetEnum(so, "_popupType", (int)PopupType.MonsterDetail, Tag);

        EditorUIBuilder.SetObj(so, "_nameText",    nameText,    Tag);
        EditorUIBuilder.SetObj(so, "_lineageText", lineageText, Tag);
        EditorUIBuilder.SetObj(so, "_goldText",    goldText,    Tag);
        EditorUIBuilder.SetObj(so, "_closeBtn",    closeBtn,    Tag);

        EditorUIBuilder.SetObj(so, "_gradeBorder",   left.GradeBorder, Tag);
        EditorUIBuilder.SetObj(so, "_portraitImage", left.Portrait,    Tag);
        EditorUIBuilder.SetObj(so, "_gradeBadge",    left.GradeBadge,  Tag);
        EditorUIBuilder.SetObj(so, "_gradeText",     left.GradeText,   Tag);
        EditorUIBuilder.SetObj(so, "_manaText",      left.Mana,        Tag);
        EditorUIBuilder.SetObj(so, "_countText",     left.Count,       Tag);
        EditorUIBuilder.SetObj(so, "_kindText",      left.Kind,        Tag);

        EditorUIBuilder.SetObjArray(so, "_tagRoots", left.TagRoots, Tag);
        EditorUIBuilder.SetObjArray(so, "_tagIcons", left.TagIcons, Tag);
        EditorUIBuilder.SetObjArray(so, "_tagNames", left.TagNames, Tag);
        EditorUIBuilder.SetObjArray(so, "_tagHovers", left.TagHovers, Tag);

        EditorUIBuilder.SetObjArray(so, "_gradePips", left.Pips, Tag);
        EditorUIBuilder.SetObj(so, "_stepText",     left.StepText,     Tag);
        EditorUIBuilder.SetObj(so, "_upgradeBtn",   left.UpgradeBtn,   Tag);
        EditorUIBuilder.SetObj(so, "_upgradeLabel", left.UpgradeLabel, Tag);
        EditorUIBuilder.SetObj(so, "_costRoot",     left.CostRoot,     Tag);
        EditorUIBuilder.SetObj(so, "_costText",     left.CostText,     Tag);

        EditorUIBuilder.SetObjArray(so, "_statValues", statValues, Tag);

        EditorUIBuilder.SetObjArray(so, "_gearSlots",   midGear.Slots,   Tag);
        EditorUIBuilder.SetObjArray(so, "_gearButtons", midGear.Buttons, Tag);
        EditorUIBuilder.SetObjArray(so, "_gearFrames",  midGear.Frames,  Tag);
        EditorUIBuilder.SetObjArray(so, "_gearIcons",   midGear.Icons,   Tag);
        EditorUIBuilder.SetObjArray(so, "_gearLabels",  midGear.Labels,  Tag);
        EditorUIBuilder.SetObjArray(so, "_gearLocks",   midGear.Locks,   Tag);
        EditorUIBuilder.SetObj(so, "_gearHint", midGear.Hint, Tag);

        EditorUIBuilder.SetObj(so, "_pickRoot",         pick.Root,         Tag);
        EditorUIBuilder.SetObj(so, "_pickTitle",        pick.Title,        Tag);
        EditorUIBuilder.SetObj(so, "_pickEmpty",        pick.Empty,        Tag);
        EditorUIBuilder.SetObj(so, "_pickContent",      pick.Content,      Tag);
        EditorUIBuilder.SetObj(so, "_pickCellTemplate", pick.CellTemplate, Tag);
        EditorUIBuilder.SetObj(so, "_pickCloseBtn",     pick.CloseBtn,     Tag);

        EditorUIBuilder.SetObj(so, "_skillRoot",     right.SkillRoot, Tag);
        EditorUIBuilder.SetObj(so, "_skillBox",      right.SkillBox,  Tag);
        EditorUIBuilder.SetObj(so, "_skillIcon",     right.SkillIcon, Tag);
        EditorUIBuilder.SetObj(so, "_skillName",     right.SkillName, Tag);
        EditorUIBuilder.SetObj(so, "_skillCooldown", right.SkillCool, Tag);
        EditorUIBuilder.SetObj(so, "_skillDesc",     right.SkillDesc, Tag);

        EditorUIBuilder.SetObj(so, "_descText", right.Desc, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveRoots", right.Roots, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveIcons", right.Icons, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveNames", right.Names, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveDescs", right.Descs, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveCountRoots", right.CountRoots, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveCountTexts", right.CountTexts, Tag);

        EditorUIBuilder.SetObj(so, "_passiveGridRoot", right.GridRoot, Tag);
        EditorUIBuilder.SetObj(so, "_passiveGrid",     right.Grid,     Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveGridIcons", right.GridIcons, Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveGridTips",  right.GridTips,  Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveShines",     right.Shines,     Tag);
        EditorUIBuilder.SetObjArray(so, "_passiveGridShines", right.GridShines, Tag);

        // ⚠ 아래 두 배열은 **순서가 계약이다** — 런타임이 IndexOf 로 그림을 찾는다
        EditorUIBuilder.SetObjArray(so, "_speciesIcons", passiveIcons, Tag);
        EditorUIBuilder.SetObjArray(so, "_synergyIcons", synergyIcons, Tag);

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MonsterDetailPopupCreator] 생성 완료 → " + SavePath +
                  "\nPopupManager 의 _prefabs 배열에 등록해야 열린다.");
    }

    // ── 헤더 ─────────────────────────────────────────────────

    static void BuildHeader(GameObject root, Sprite goldIcon,
                            out TextMeshProUGUI name, out TextMeshProUGUI lineage,
                            out TextMeshProUGUI gold, out Button close)
    {
        var header = EditorUIBuilder.Img(root, "Header", EditorUIBuilder.Pop.HeaderBg).gameObject;
        {
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -HeaderH);
            rt.offsetMax = Vector2.zero;
        }

        var edge = EditorUIBuilder.Img(header, "BottomEdge", EditorUIBuilder.Pop.Divider);
        {
            var rt = edge.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, 3f);
        }
        edge.raycastTarget = false;

        // ◆ 는 도형으로 그린다 (UI 규칙 2 — 장식 기호에 글리프를 쓰지 않는다)
        var dia = EditorUIBuilder.Diamond(header, "Diamond", 26f, MonsterC);
        {
            var rt = dia.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Pad + 6f, 0f);
        }

        name = EditorUIBuilder.TMP(header, "Name", "몬스터 이름", UIScale.FontLg, FontStyles.Bold);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        {
            var rt = name.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Pad + 44f, 0f);
            rt.sizeDelta        = new Vector2(480f, UIScale.RowLg);
        }

        // 계보 · 특징 — 이름보다 흐리고 작다.
        lineage = EditorUIBuilder.TMP(header, "Lineage", "", UIScale.FontSm, FontStyles.Normal);
        lineage.color         = SubText;
        lineage.alignment     = TextAlignmentOptions.MidlineLeft;
        lineage.raycastTarget = false;
        {
            var rt = lineage.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(Pad + 540f, 0f);
            rt.sizeDelta        = new Vector2(620f, UIScale.RowMd);
        }

        // ── 보유 골드 ──
        //  ⚠ 낼 값(버튼 안)과 떨어뜨려 둔다 — 붙여 놓으면 어느 쪽이 지갑인지
        //    매번 다시 읽어야 한다. 지갑은 늘 이 자리다.
        var wallet = EditorUIBuilder.Go("Wallet", header);
        {
            var rt = wallet.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-(Pad + 84f + 12f + 84f + 24f), 0f);
            rt.sizeDelta        = new Vector2(280f, 56f);
        }

        var walletIcon = EditorUIBuilder.Img(wallet, "GoldIcon", Color.white);
        walletIcon.sprite         = goldIcon;
        walletIcon.preserveAspect = true;
        walletIcon.raycastTarget  = false;
        {
            var rt = walletIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(48f, 48f);
        }

        gold = EditorUIBuilder.TMP(wallet, "GoldValue", "0", UIScale.FontMd, FontStyles.Bold);
        gold.color         = GoldC;
        gold.alignment     = TextAlignmentOptions.MidlineLeft;
        gold.raycastTarget = false;
        {
            var rt = gold.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(58f, 0f); rt.offsetMax = Vector2.zero;
        }

        close = EditorUIBuilder.RaisedBtn(header, "CloseBtn",
                                          new Color(0.62f, 0.20f, 0.24f), out var body);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-Pad, 0f);
            rt.sizeDelta        = new Vector2(84f, 84f);
        }
        EditorUIBuilder.XMark(body, "X", 34f, new Color(0.92f, 0.94f, 0.98f));

        // 도움말 — 닫기(84) 왼쪽. 지갑은 그만큼 더 왼쪽에 선다 (위 Wallet 자리)
        EditorUIBuilder.InfoBtn(header, TutorialId.HelpMonsterDetail, 84f, -Pad);
    }

    // ── 왼쪽 — 초상화 · 소환 비용 · 표식 · 품질 개선 ─────────

    struct LeftRefs
    {
        public Image           GradeBorder, Portrait, GradeBadge;
        public TextMeshProUGUI GradeText, Mana, Count, Kind;

        public Object[] TagRoots, TagIcons, TagNames, TagHovers;

        public Object[]        Pips;
        public TextMeshProUGUI StepText;
        public Button          UpgradeBtn;
        public TextMeshProUGUI UpgradeLabel, CostText;
        public GameObject      CostRoot;
    }

    static void BuildLeft(GameObject root, Sprite manaIcon, Sprite countIcon,
                          Sprite goldIcon, out LeftRefs refs)
    {
        GameObject col = Column(root, "Left", 0f, LeftW);

        float y = Pad;

        // 초상화 — 등급 테두리가 밖으로 조금 드러난다 (도감 칸과 같은 수법).
        // ⚠ 품질 개선 연출(UIJuice.GradeUp)의 자리이기도 하다 — 이 창에서
        //   등급색이 실제로 바뀌는 물건이고, 다시 만들어지지 않아 좌표를 믿을 수 있다.
        var border = EditorUIBuilder.Img(col, "GradeBorder", Color.white);
        border.raycastTarget = false;
        {
            var rt = border.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta        = new Vector2(PortraitSize + 8f, PortraitSize + 8f);
        }

        var inner = EditorUIBuilder.Img(border.gameObject, "Fill", new Color(0.09f, 0.10f, 0.16f));
        EditorUIBuilder.Stretch(inner.gameObject);
        inner.rectTransform.offsetMin = new Vector2(4f, 4f);
        inner.rectTransform.offsetMax = new Vector2(-4f, -4f);
        inner.raycastTarget = false;

        var portrait = EditorUIBuilder.Img(inner.gameObject, "Portrait", Color.white);
        portrait.preserveAspect = true;
        portrait.raycastTarget  = false;
        EditorUIBuilder.Stretch(portrait.gameObject);

        y += PortraitSize + 8f + 12f;

        // 등급 줄 — [배지] 고급
        var gradeRow = EditorUIBuilder.Go("GradeRow", col);
        EditorUIBuilder.AnchorTop(gradeRow.GetComponent<RectTransform>(), y, UIScale.RowMd, 20f);

        var badge = EditorUIBuilder.Img(gradeRow, "Badge", Color.white);
        badge.raycastTarget = false;
        {
            var rt = badge.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(20f, 20f);
        }

        var gradeText = EditorUIBuilder.TMP(gradeRow, "Grade", "일반", UIScale.FontMd, FontStyles.Bold);
        gradeText.alignment     = TextAlignmentOptions.MidlineLeft;
        gradeText.raycastTarget = false;
        {
            var rt = gradeText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(30f, 0f);
            rt.sizeDelta        = new Vector2(200f, UIScale.RowMd);
        }

        y += UIScale.RowMd + 8f;

        // 소환 비용 줄 — [◈ 7]  [☠ 6]  근접
        //   ⚠ 글자로 "마나 7" 이라고 쓰지 않는다 (UI 규칙 7)
        var costRow = EditorUIBuilder.Go("CostRow", col);
        EditorUIBuilder.AnchorTop(costRow.GetComponent<RectTransform>(), y, 56f, 20f);

        EditorUIBuilder.IconValueBadge(costRow, "ManaCost", manaIcon, rightSide: false,
                                       44f, UIScale.FontMd, 0f, Color.white,
                                       out TextMeshProUGUI manaText);

        var countRoot = EditorUIBuilder.Go("CountHolder", costRow);
        {
            var rt = countRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(150f, 0f); rt.offsetMax = Vector2.zero;
        }
        EditorUIBuilder.IconValueBadge(countRoot, "SummonCount", countIcon, rightSide: false,
                                       44f, UIScale.FontMd, 0f, CountC,
                                       out TextMeshProUGUI countText);

        var kind = EditorUIBuilder.TMP(costRow, "Kind", "근접", UIScale.FontSm, FontStyles.Bold);
        kind.color         = SubText;
        kind.alignment     = TextAlignmentOptions.MidlineRight;
        kind.raycastTarget = false;
        {
            var rt = kind.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(140f, 0f);
        }

        y += 56f + 10f;

        BuildTagRow(col, y, out var tagRoots, out var tagIcons, out var tagNames, out var tagHovers);

        BuildUpgradePanel(col, goldIcon, out var pips, out var step,
                          out var btn, out var label, out var costRoot2, out var costText);

        refs = new LeftRefs
        {
            GradeBorder  = border,
            Portrait     = portrait,
            GradeBadge   = badge,
            GradeText    = gradeText,
            Mana         = manaText,
            Count        = countText,
            Kind         = kind,
            TagRoots     = tagRoots,
            TagIcons     = tagIcons,
            TagNames     = tagNames,
            TagHovers    = tagHovers,
            Pips         = pips,
            StepText     = step,
            UpgradeBtn   = btn,
            UpgradeLabel = label,
            CostRoot     = costRoot2,
            CostText     = costText,
        };
    }

    /// <summary>
    /// 시너지 표식 칩 — [아이콘][이름] 셋까지.
    ///
    /// ⚠ 이름만 적지 않는다. 상단 시너지 줄·카드 3택이 전부 그림이라
    ///   여기만 글자면 같은 시너지를 화면마다 다른 모양으로 읽어야 한다.
    /// </summary>
    static void BuildTagRow(GameObject col, float y, out Object[] roots, out Object[] icons,
                            out Object[] names, out Object[] hovers)
    {
        // ⚠ 크게 (사용자 지적, 2026-09-12 "시너지 아이콘이 너무 작다") — 칩 48 · 그림 34 → 칩 68 · 그림 56
        //   폭 검산: 칸 430(470 − 20×2) ≥ 3×138 + 2×8 = 430 ✔
        var row = EditorUIBuilder.Go("TagRow", col);
        EditorUIBuilder.AnchorTop(row.GetComponent<RectTransform>(), y, 68f, 20f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment        = TextAnchor.MiddleLeft;
        hlg.childControlWidth     = true;  hlg.childControlHeight     = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

        roots  = new Object[TagSlots];
        icons  = new Object[TagSlots];
        names  = new Object[TagSlots];
        hovers = new Object[TagSlots];

        for (int i = 0; i < TagSlots; i++)
        {
            var chipImg = EditorUIBuilder.Img(row, $"Tag{i}", RowBg);
            var chip    = chipImg.gameObject;
            EditorUIBuilder.LE(chip, 138f, 0f);

            // ⚠ 칩의 바탕 Image 가 레이캐스트를 받아야 한다
            //   자식(아이콘·이름)은 전부 꺼 두므로 이 면이 유일한 입력 창구다.
            //   꺼지면 올려도 눌러도 툴팁이 안 뜬다.
            chipImg.raycastTarget = true;

            var icon = EditorUIBuilder.Img(chip, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
            {
                var rt = icon.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(6f, 0f);
                rt.sizeDelta        = new Vector2(56f, 56f);
            }

            // 이름 칸 138 − 68 − 4 = 66 — "언데드" 는 자동 축소가 받는다
            var nm = EditorUIBuilder.TMP(chip, "Name", "", UIScale.FontSm * 0.86f, FontStyles.Bold);
            nm.color            = SubText;
            nm.alignment        = TextAlignmentOptions.MidlineLeft;
            nm.raycastTarget    = false;
            nm.textWrappingMode = TextWrappingModes.NoWrap;
            nm.enableAutoSizing = true;
            nm.fontSizeMin      = UIScale.FontSm * 0.66f;
            nm.fontSizeMax      = UIScale.FontSm * 0.86f;
            {
                var rt = nm.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(68f, 0f); rt.offsetMax = new Vector2(-4f, 0f);
            }

            // ── 올리면(누르면) 동·은·금 효과가 뜬다 ──
            //   ⚠ 툴팁을 여기서 만들지 않는다 — TooltipLayer 의 한 장을 함께 쓴다
            //     (칩 자식으로 두면 팝업 면 밑으로 깔리고 둘이 동시에 뜬다).
            //   상단 시너지 줄·카드 3택과 **같은 컴포넌트**라 동작도 글도 같다.
            var hover = chip.AddComponent<SynergyChipUI>();

            chip.SetActive(false);

            roots[i]  = chip;
            icons[i]  = icon;
            names[i]  = nm;
            hovers[i] = hover;
        }
    }

    /// <summary>
    /// 품질 개선 패널 — 칸 맨 아래에 붙인다.
    ///
    /// ⚠ 위에서 쌓아 온 커서를 쓰지 않는다
    ///   이것은 결론이라 종족이 바뀌어도 늘 같은 자리에 있어야 한다.
    ///
    /// ⚠ 값은 버튼 **안**에 있다
    ///   "무엇을 누르면 얼마가 나가는가" 가 한 덩어리여야 한다. 버튼 밖에
    ///   따로 적고 그 옆에 지갑까지 붙여 두면 셋 다 그냥 숫자로 보인다.
    /// </summary>
    static void BuildUpgradePanel(GameObject col, Sprite goldIcon,
                                  out Object[] pips, out TextMeshProUGUI step,
                                  out Button btn, out TextMeshProUGUI label,
                                  out GameObject costRoot, out TextMeshProUGUI costText)
    {
        var panel = EditorUIBuilder.Img(col, "UpgradePanel", PanelBg).gameObject;
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, Pad);
            rt.sizeDelta        = new Vector2(-40f, PanelH);
        }

        var title = EditorUIBuilder.TMP(panel, "Title", "품 질 개 선", UIScale.FontSm, FontStyles.Bold);
        title.color         = MonsterC;
        title.alignment     = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        EditorUIBuilder.AnchorTop(title.rectTransform, 14f, UIScale.RowSm, 20f);

        // ── 단계 핍 ──
        //  ⚠ 숫자를 적지 않는다 — "3/5" 보다 ● ● ● ○ ○ 가 남은 칸을 바로 말해 준다.
        var pipRow = EditorUIBuilder.Go("Pips", panel);
        EditorUIBuilder.AnchorTop(pipRow.GetComponent<RectTransform>(),
                                  14f + UIScale.RowSm + 8f, PipSize, 20f);

        var hlg = pipRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = PipGap;
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.childControlWidth     = true;  hlg.childControlHeight     = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        pips = new Object[MonsterDetailPopup.GradeSteps];
        for (int i = 0; i < pips.Length; i++)
        {
            var pip = EditorUIBuilder.Img(pipRow, $"Pip{i}", Color.white);
            pip.sprite        = EditorUIBuilder.Circle();
            pip.raycastTarget = false;
            EditorUIBuilder.LE(pip.gameObject, PipSize, PipSize);
            pips[i] = pip;
        }

        // ── 다음 단계 ──
        step = EditorUIBuilder.TMP(panel, "Step", "일반 › 고급", UIScale.FontMd, FontStyles.Bold);
        step.alignment     = TextAlignmentOptions.Center;
        step.raycastTarget = false;
        EditorUIBuilder.AnchorTop(step.rectTransform,
                                  14f + UIScale.RowSm + 8f + PipSize + 10f, UIScale.RowMd, 20f);

        // ── 버튼 ──
        //  ⚠ 누를 수 있는 것이므로 음각이다 (UI 규칙 1). 라벨·값은 body 아래에 넣는다.
        float btnH = UIScale.BtnFor(UIScale.FontMd) + 14f;

        btn = EditorUIBuilder.RaisedBtn(col, "UpgradeBtn", new Color(0.34f, 0.26f, 0.52f),
                                        out var body);
        {
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, Pad + 16f);
            rt.sizeDelta        = new Vector2(-72f, btnH);
        }

        // ⚠ 폭 검산 (사용자 지적, 2026-09-12 — "4,500" 이 두 줄로 접혔다)
        //   버튼 398 을 반씩: 글 177 · 값 177 − 아이콘 48 = 129. 가장 긴 값은 "40,000"(6자).
        //   한때 0.58 : 0.42 라 값 칸이 97 뿐이었다. 둘 다 줄바꿈을 끄고 자동 축소(바닥 FontSm)에 맡긴다.
        label = EditorUIBuilder.TMP(body, "Label", "품질 개선", UIScale.FontMd, FontStyles.Bold);
        label.alignment        = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget    = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin      = UIScale.FontSm;
        label.fontSizeMax      = UIScale.FontMd;
        {
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(CostSplit, 1f);
            rt.offsetMin = new Vector2(22f, 0f); rt.offsetMax = Vector2.zero;
        }

        // [금][900] — 만렙이면 런타임이 통째로 끈다
        costRoot = EditorUIBuilder.Go("CostRoot", body);
        {
            var rt = costRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(CostSplit, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(-22f, 0f);
        }

        var costIcon = EditorUIBuilder.Img(costRoot, "GoldIcon", Color.white);
        costIcon.sprite         = goldIcon;
        costIcon.preserveAspect = true;
        costIcon.raycastTarget  = false;
        {
            var rt = costIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(CostIcon, CostIcon);
        }

        costText = EditorUIBuilder.TMP(costRoot, "CostValue", "0", UIScale.FontMd, FontStyles.Bold);
        costText.color            = GoldC;
        costText.alignment        = TextAlignmentOptions.MidlineRight;
        costText.raycastTarget    = false;
        costText.textWrappingMode = TextWrappingModes.NoWrap;
        costText.enableAutoSizing = true;
        costText.fontSizeMin      = UIScale.FontSm;
        costText.fontSizeMax      = UIScale.FontMd;
        {
            var rt = costText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(CostIcon + 8f, 0f); rt.offsetMax = Vector2.zero;
        }
    }

    // ── 가운데 — 스탯 ────────────────────────────────────────
    //
    //  ⚠ 세로 예산 (칸 높이 910 = 1080 − 헤더 118 − 여백 26×2)
    //    여백 26 + 제목 61 + 행 N×(53+6) + 12 + 제목 61 + 장비 152 + 8 + 안내 43
    //    9행이면 894 ≤ 910 ✔ — 행을 하나 더 늘리면 넘친다.
    //    (행 목록의 정본은 MonsterDetailPopup.StatRows 다)

    static void BuildMid(GameObject root, out Object[] values, out MidGearRefs gear)
    {
        GameObject col = Column(root, "Mid", LeftW + ColGap, MidW);

        SectionTitle(col, "스 탯", Pad);

        var list = new List<Object>();
        float y  = Pad + UIScale.RowMd + 8f;

        // ⚠ 이름표는 MonsterDetailPopup.StatRows 가 정본이다
        //   여기서 손으로 적으면 런타임이 넣는 값과 순서가 어긋난다.
        foreach (var (label, _) in MonsterDetailPopup.StatRows)
        {
            var row = EditorUIBuilder.Img(col, $"Row_{label}", RowBg).gameObject;
            EditorUIBuilder.AnchorTop(row.GetComponent<RectTransform>(), y, UIScale.RowMd, 18f);

            var lbl = EditorUIBuilder.TMP(row, "Label", label, UIScale.FontSm, FontStyles.Normal);
            lbl.color         = SubText;
            lbl.alignment     = TextAlignmentOptions.MidlineLeft;
            lbl.raycastTarget = false;
            {
                var rt = lbl.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0.42f, 1f);
                rt.offsetMin = new Vector2(16f, 0f); rt.offsetMax = Vector2.zero;
            }

            // ⚠ 값 칸이 넓다 — 체력·공격력은 "99 › 109" 로 다음 값까지 그린다
            var val = EditorUIBuilder.TMP(row, "Value", "0", UIScale.FontMd, FontStyles.Bold);
            val.alignment        = TextAlignmentOptions.MidlineRight;
            val.raycastTarget    = false;
            val.textWrappingMode = TextWrappingModes.NoWrap;
            {
                var rt = val.rectTransform;
                rt.anchorMin = new Vector2(0.42f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(-16f, 0f);
            }

            list.Add(val);
            y += UIScale.RowMd + RowGap;
        }

        values = list.ToArray();

        // ── 장비 ──
        //  ⚠ 품질 개선 패널 바로 옆(왼쪽 칸)과 짝이 되는 자리다
        //    칸 수를 정하는 것이 **몬스터의 품질**이라, 품질을 올린 손이
        //    바로 옆에서 칸이 열리는 것을 봐야 두 화면이 하나로 읽힌다.
        y += 12f;
        SectionTitle(col, "장 비", y);
        y += UIScale.RowMd + 8f;

        BuildGearSlots(col, y, out gear);
    }

    // ── 장비 칸 ──────────────────────────────────────────────

    struct MidGearRefs
    {
        public Object[]        Slots, Buttons, Frames, Icons, Labels, Locks;
        public TextMeshProUGUI Hint;
    }

    /// <summary>
    /// 장비 칸 셋을 가로로 늘어놓는다.
    ///
    /// ⚠ 잠긴 칸도 만든다 — 감추지 않는다
    ///   감추면 "칸이 더 있다" 는 사실 자체를 모른다. 자물쇠와 "희귀부터" 를
    ///   함께 두면 품질 개선이 무엇을 사는지가 화면에서 이어진다.
    ///
    /// ⚠ 폭 검산 — 3칸 + 간격 둘이 칸 안에 들어가야 한다
    ///   가운데 칸 560 − 안쪽 여백 18 = 542. 3×160 + 2×20 = 520 ≤ 542 ✔
    /// </summary>
    static void BuildGearSlots(GameObject col, float y, out MidGearRefs refs)
    {
        var row = EditorUIBuilder.Go("GearRow", col);
        EditorUIBuilder.AnchorTop(row.GetComponent<RectTransform>(), y, GearSlotH, 18f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.childControlWidth     = true;  hlg.childControlHeight     = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        int n = MonsterGearRule.MaxSlots;

        var slots = new Object[n];
        var btns  = new Object[n];
        var frames = new Object[n];
        var icons = new Object[n];
        var labels = new Object[n];
        var locks = new Object[n];

        for (int i = 0; i < n; i++)
        {
            var slot = EditorUIBuilder.Go($"GearSlot{i}", row);
            EditorUIBuilder.LE(slot, GearSlotW, GearSlotH);

            var btn = slot.AddComponent<Button>();

            var frame = EditorUIBuilder.Img(slot, "Frame", new Color(0.26f, 0.29f, 0.42f));
            EditorUIBuilder.Stretch(frame.gameObject);
            btn.targetGraphic = frame;
            // 누름 표시는 테두리 밝기로 준다 (targetGraphic 색에 곱해진다 — UI 규칙 1)
            EditorUIBuilder.TintTransition(slot, Color.white);

            var fill = EditorUIBuilder.Img(frame.gameObject, "Fill", RowBg);
            EditorUIBuilder.Stretch(fill.gameObject);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            fill.raycastTarget = false;

            var icon = EditorUIBuilder.Img(fill.gameObject, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
            {
                var rt = icon.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                // ⚠ 칸 146 = 아이콘(8~70) + 글 두 줄(아래 8~82). 아이콘을 키우면 이름과 겹친다 (2026-09-12)
                rt.anchoredPosition = new Vector2(0f, -8f);
                rt.sizeDelta        = new Vector2(62f, 62f);
            }

            // 자물쇠 — 잠긴 칸에만. ⚠ 글리프(🔒)를 쓰지 않는다 (UI 규칙 2)
            //   ⚠ 잠긴 칸의 글("희귀부터")은 한 줄이라 아래에 붙는다 — 자물쇠는 위로 올려 둘을 가른다
            var padlock = EditorUIBuilder.PadLock(fill.gameObject, "Lock", 48f, SubText);
            {
                var rt = padlock.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -16f);
            }

            var label = EditorUIBuilder.TMP(fill.gameObject, "Label", "비었다",
                                            UIScale.FontSm * 0.86f, FontStyles.Bold);
            // ⚠ 아래 정렬 — 위 정렬이면 한 줄짜리("희귀부터")가 자물쇠 밑동에 올라탄다 (사용자 지적, 2026-09-12)
            label.alignment        = TextAlignmentOptions.Bottom;
            label.raycastTarget    = false;
            label.enableAutoSizing = true;
            label.fontSizeMin      = UIScale.FontSm * 0.60f;
            label.fontSizeMax      = UIScale.FontSm * 0.86f;
            {
                var rt = label.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot     = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(6f, 8f);
                rt.offsetMax = new Vector2(-6f, 8f + UIScale.Line(UIScale.FontSm * 0.86f) * 2f);
            }

            slots[i]  = slot;
            btns[i]   = btn;
            frames[i] = frame;
            icons[i]  = icon;
            labels[i] = label;
            locks[i]  = padlock;
        }

        var hint = EditorUIBuilder.TMP(col, "GearHint", "", UIScale.FontSm * 0.86f, FontStyles.Normal);
        hint.color         = SubText;
        hint.alignment     = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
        EditorUIBuilder.AnchorTop(hint.rectTransform, y + GearSlotH + 8f, UIScale.RowSm, 18f);

        refs = new MidGearRefs
        {
            Slots = slots, Buttons = btns, Frames = frames,
            Icons = icons, Labels = labels, Locks = locks, Hint = hint,
        };
    }

    // ── 오른쪽 — 종족 패시브 · 고유 스킬 ─────────────────────

    struct RightRefs
    {
        public GameObject      SkillRoot;
        public RectTransform   SkillBox;
        public Image           SkillIcon;
        public TextMeshProUGUI SkillName, SkillCool, SkillDesc;

        public TextMeshProUGUI Desc;
        public Object[]        Roots, Icons, Names, Descs;
        public Object[]        CountRoots, CountTexts;

        public GameObject      GridRoot;
        public GridLayoutGroup Grid;
        public Object[]        GridIcons, GridTips;

        public Object[]        Shines, GridShines;
    }

    static void BuildRight(GameObject root, Sprite countIcon, out RightRefs refs)
    {
        GameObject col = Column(root, "Right", LeftW + ColGap + MidW + ColGap, 0f);

        float y = Pad;

        // ── 패시브 (종족 + 카드 레벨이 여는 것) ──
        //  ⚠ 제목이 "종족 패시브" 가 아닌 이유 — 두 축을 한 목록에 그린다
        //    (MonsterDetailPopup.RefreshPassives). 종족 것만이라고 적어 두면
        //    Lv4 에 열린 칸이 남의 것처럼 읽힌다.
        SectionTitle(col, "패 시 브", y);
        y += UIScale.RowMd + 8f;

        // 종족 소개 — 두 줄.
        var desc = EditorUIBuilder.TMP(col, "Desc", "", UIScale.FontSm, FontStyles.Normal);
        EditorUIBuilder.AnchorTop(desc.rectTransform, y, UIScale.RowSm * 2f, 18f);
        desc.color         = SubText;
        desc.alignment     = TextAlignmentOptions.TopLeft;
        desc.raycastTarget = false;

        y += UIScale.RowSm * 2f + 10f;

        // ── 아이콘 모드 격자 — 줄 칸과 **같은 자리**에 겹쳐 두고 런타임이 하나만 켠다 ──
        BuildPassiveGrid(col, y, out var gridRoot, out var grid, out var gridIcons, out var gridTips,
                         out var gridShines);

        var shines = new Object[PassiveSlots];
        var roots = new Object[PassiveSlots];
        var icons = new Object[PassiveSlots];
        var names = new Object[PassiveSlots];
        var descs = new Object[PassiveSlots];
        var countRoots = new Object[PassiveSlots];
        var countTexts = new Object[PassiveSlots];

        // ⚠ 여러 칸이 들어가야 해서 140 → 88 로 줄였다 (PassiveSlots 주석 참고)
        //   설명은 이제 한 줄이다. 자동 축소(아래 fontSizeMin)가 긴 문장을 받는다 —
        //   TMP 는 rect 를 넘겨도 그냥 그리므로 칸 밖으로 흐르면 아래 행과 겹친다.
        //   ⚠ 더 줄이지 말 것. 여기서 더 낮추면 설명이 읽히지 않는다 —
        //     그럴 바에는 설명을 빼고 이름만 두는 편이 낫다.
        const float PassiveH     = 88f;
        float       PassiveDescH = PassiveH - 10f - UIScale.RowSm - 6f;

        for (int i = 0; i < PassiveSlots; i++)
        {
            var row = EditorUIBuilder.Img(col, $"Passive_{i + 1}", RowBg).gameObject;
            EditorUIBuilder.AnchorTop(row.GetComponent<RectTransform>(), y, PassiveH, 18f);

            var icon = EditorUIBuilder.Img(row, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
            {
                var rt = icon.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(14f, 0f);
                rt.sizeDelta        = new Vector2(56f, 56f);
            }

            var nm = EditorUIBuilder.TMP(row, "Name", "이름", UIScale.FontSm, FontStyles.Bold);
            nm.alignment     = TextAlignmentOptions.TopLeft;
            nm.raycastTarget = false;
            {
                var rt = nm.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(92f, -10f);
                rt.sizeDelta        = new Vector2(-108f, UIScale.RowSm);
            }

            var dc = EditorUIBuilder.TMP(row, "Desc", "설명", UIScale.FontSm * 0.88f, FontStyles.Normal);
            dc.color         = SubText;
            dc.alignment     = TextAlignmentOptions.TopLeft;
            dc.raycastTarget = false;
            // 두 줄까지 담고, 그보다 길면 줄여서 담는다 (UI 규칙 5)
            dc.enableAutoSizing = true;
            dc.fontSizeMax      = UIScale.FontSm * 0.88f;
            dc.fontSizeMin      = UIScale.FontSm * 0.70f;
            {
                var rt = dc.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(92f, -12f - UIScale.RowSm);
                rt.sizeDelta        = new Vector2(-108f, PassiveDescH);
            }

            // 마릿수 배지 — 이름 줄 오른쪽 끝. 권속 소환 줄만 런타임이 켠다 (UI 규칙 7)
            var badge = EditorUIBuilder.IconValueBadge(row, "Count", countIcon, rightSide: true,
                                                       40f, UIScale.FontSm, 10f, CountC,
                                                       out TextMeshProUGUI countText);
            badge.SetActive(false);
            countRoots[i] = badge;
            countTexts[i] = countText;

            // 각성 반짝임 — ⚠ 마지막 자식이다 (위에 그려져야 빛이 글 위를 지나간다)
            shines[i] = BuildShine(row);

            row.SetActive(false);

            roots[i] = row;
            icons[i] = icon;
            names[i] = nm;
            descs[i] = dc;

            y += PassiveH + RowGap;
        }

        // ── 고유 액티브 스킬 (맨 아래) ──
        //  ⚠ 없는 종족이 더 많다 — 그래서 맨 아래다 (파일 머리 주석)
        //    제목까지 한 덩어리로 껐다 켜므로, 꺼져도 위쪽 짜임새는 그대로다.
        var skillRoot = EditorUIBuilder.Go("SkillSection", col);
        EditorUIBuilder.AnchorTop(skillRoot.GetComponent<RectTransform>(), y, SkillSectionH, 0f);

        SectionTitle(skillRoot, "고 유 스 킬", 0f);

        var skillBox = EditorUIBuilder.Img(skillRoot, "SkillBox", RowBg).gameObject;
        EditorUIBuilder.AnchorTop(skillBox.GetComponent<RectTransform>(),
                                  UIScale.RowMd + 8f, SkillBoxH, 18f);

        var skillIcon = EditorUIBuilder.Img(skillBox, "Icon", Color.white);
        skillIcon.preserveAspect = true;
        skillIcon.raycastTarget  = false;
        {
            var rt = skillIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(14f, -14f);
            rt.sizeDelta        = new Vector2(SkillIconSize, SkillIconSize);
        }

        // ⚠ 이름 칸의 오른쪽 끝이 쿨다운 칸을 넘지 않아야 한다
        //   쿨다운이 오른쪽 140px 을 차지하므로, 폭을 (텍스트 왼쪽 104 + 쿨다운 140
        //   + 사이 여백 16) 만큼 줄인다. 한때 -220 이라 둘이 24px 겹쳤다.
        var skillName = EditorUIBuilder.TMP(skillBox, "Name", "스킬", UIScale.FontMd, FontStyles.Bold);
        skillName.alignment        = TextAlignmentOptions.MidlineLeft;
        skillName.raycastTarget    = false;
        skillName.textWrappingMode = TextWrappingModes.NoWrap;
        {
            var rt = skillName.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(SkillTextX, -16f);
            rt.sizeDelta        = new Vector2(-(SkillTextX + SkillCoolW + 16f), UIScale.RowMd);
        }

        // 쿨다운 — 오른쪽 끝. ⚠ SO 원본 값이다 (술법 시너지가 실제로는 깎는다)
        var skillCool = EditorUIBuilder.TMP(skillBox, "Cooldown", "0초",
                                            UIScale.FontSm, FontStyles.Bold);
        skillCool.color         = SubText;
        skillCool.alignment     = TextAlignmentOptions.MidlineRight;
        skillCool.raycastTarget = false;
        {
            var rt = skillCool.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-14f, -16f);
            rt.sizeDelta        = new Vector2(SkillCoolW, UIScale.RowMd);
        }

        // ⚠ 설명은 아이콘 **오른쪽**에서 시작한다 (SkillTextX)
        //   전에는 상자 왼쪽 끝(14)에서 시작해 첫 줄이 76px 아이콘 아래쪽과
        //   겹쳤다 — 아이콘은 -14~-90, 설명은 -75 부터라 15px 이 포개졌다.
        //   이름과 같은 세로선에 맞추면 겹칠 자리가 아예 없어지고,
        //   아래 종족 패시브 행(아이콘 왼쪽 · 글 오른쪽)과 짜임새도 같아진다.
        var skillDesc = EditorUIBuilder.TMP(skillBox, "Desc", "", UIScale.FontSm, FontStyles.Normal);
        skillDesc.color         = SubText;
        skillDesc.alignment     = TextAlignmentOptions.TopLeft;
        skillDesc.raycastTarget = false;
        // 긴 설명은 줄여서 담는다 — 넘치면 상자 밖으로 흘러 아래 제목을 덮는다 (UI 규칙 5)
        skillDesc.enableAutoSizing = true;
        skillDesc.fontSizeMax      = UIScale.FontSm;
        skillDesc.fontSizeMin      = UIScale.FontSm * 0.74f;
        {
            var rt = skillDesc.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(SkillTextX, 12f);
            rt.offsetMax = new Vector2(-14f, -(16f + UIScale.RowMd + 4f));
        }

        refs = new RightRefs
        {
            SkillRoot = skillRoot,
            SkillBox  = skillBox.GetComponent<RectTransform>(),
            SkillIcon = skillIcon,
            SkillName = skillName,
            SkillCool = skillCool,
            SkillDesc = skillDesc,
            Desc      = desc,
            Roots     = roots,
            Icons     = icons,
            Names     = names,
            Descs     = descs,
            CountRoots = countRoots,
            CountTexts = countTexts,
            GridRoot  = gridRoot,
            Grid      = grid,
            GridIcons = gridIcons,
            GridTips  = gridTips,
            Shines     = shines,
            GridShines = gridShines,
        };
    }

    static readonly Color ShineGold = new Color(1.00f, 0.83f, 0.30f, 0.9f);

    /// <summary>
    /// 각성 패시브 칸의 반짝임 — 금테 네 변 + 비스듬한 빛줄기 (PassiveShineUI 가 움직인다).
    /// 칸을 가득 덮고, 빛줄기는 RectMask2D 가 칸 안으로 잘라 준다. 기본은 꺼 둔다.
    ///
    /// ⚠ 레이캐스트를 받지 않는다 — 아이콘 모드에서는 칸 바탕이 툴팁 입력 창구다.
    /// </summary>
    static GameObject BuildShine(GameObject parent)
    {
        var root = EditorUIBuilder.Go("Shine", parent);
        EditorUIBuilder.Stretch(root);
        root.AddComponent<RectMask2D>();
        var shine = root.AddComponent<PassiveShineUI>();

        var sweep = EditorUIBuilder.Img(root, "Sweep", new Color(1f, 0.96f, 0.80f, 0.22f));
        sweep.raycastTarget = false;
        {
            var rt = sweep.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(44f, 400f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -20f);
        }

        const float T = 3f;
        var edges = new Object[4];
        for (int i = 0; i < 4; i++)
        {
            var bar = EditorUIBuilder.Img(root, $"Edge{i}", ShineGold);
            bar.raycastTarget = false;

            var rt = bar.rectTransform;
            switch (i)
            {
                case 0:  rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
                         rt.offsetMin = new Vector2(0f, -T);  rt.offsetMax = Vector2.zero; break;   // 위
                case 1:  rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 0f);
                         rt.offsetMin = Vector2.zero;        rt.offsetMax = new Vector2(0f, T); break;   // 아래
                case 2:  rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f);
                         rt.offsetMin = Vector2.zero;        rt.offsetMax = new Vector2(T, 0f); break;   // 왼쪽
                default: rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = Vector2.one;
                         rt.offsetMin = new Vector2(-T, 0f);  rt.offsetMax = Vector2.zero; break;   // 오른쪽
            }
            edges[i] = bar;
        }

        var so = new SerializedObject(shine);
        EditorUIBuilder.SetObj(so, "_sweep", sweep.rectTransform, Tag);
        EditorUIBuilder.SetObjArray(so, "_edges", edges, Tag);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// 패시브 아이콘 격자 — 패시브가 줄 칸(PassiveSlots)보다 많을 때만 켜진다.
    /// 칸 = 바탕(레이캐스트) + 아이콘. 설명은 올리거나 누르면 TooltipLayer 한 장에 뜬다.
    /// </summary>
    static void BuildPassiveGrid(GameObject col, float y, out GameObject root, out GridLayoutGroup grid,
                                 out Object[] icons, out Object[] tips, out Object[] shines)
    {
        shines = new Object[PassiveIconMax];
        int   cols = PassiveIconCols;
        int   rows = (PassiveIconMax + cols - 1) / cols;
        float h    = rows * PassiveIconSize + (rows - 1) * PassiveIconGap;

        if (PassiveIconMax < PassiveMaxOwned)
            Debug.LogError($"[{Tag}] 아이콘 칸 {PassiveIconMax} 개가 가질 수 있는 패시브 {PassiveMaxOwned} 개보다 적습니다.");

        root = EditorUIBuilder.Go("PassiveGrid", col);
        EditorUIBuilder.AnchorTop(root.GetComponent<RectTransform>(), y, h, 18f);

        grid = root.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(PassiveIconSize, PassiveIconSize);
        grid.spacing         = new Vector2(PassiveIconGap, PassiveIconGap);
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        icons = new Object[PassiveIconMax];
        tips  = new Object[PassiveIconMax];

        for (int i = 0; i < PassiveIconMax; i++)
        {
            // ⚠ 바탕이 레이캐스트를 받아야 한다 — 아이콘은 꺼 두므로 이 면이 유일한 입력 창구다
            var cell = EditorUIBuilder.Img(root, $"PassiveIcon_{i + 1}", RowBg);
            cell.raycastTarget = true;

            var icon = EditorUIBuilder.Img(cell.gameObject, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
            EditorUIBuilder.Stretch(icon.gameObject);
            icon.rectTransform.offsetMin = new Vector2(8f, 8f);
            icon.rectTransform.offsetMax = new Vector2(-8f, -8f);

            tips[i]   = cell.gameObject.AddComponent<InfoIconUI>();
            icons[i]  = icon;
            shines[i] = BuildShine(cell.gameObject);

            cell.gameObject.SetActive(false);
        }

        root.SetActive(false);
    }

    // ── 장비 고르기 (겹쳐 뜨는 목록) ─────────────────────────

    struct PickRefs
    {
        public GameObject      Root, CellTemplate;
        public TextMeshProUGUI Title, Empty;
        public RectTransform   Content;
        public Button          CloseBtn;
    }

    /// <summary>
    /// 장비 목록 — 가운데·오른쪽 칸을 덮는 판.
    ///
    /// ■ 전용 팝업이 아니다
    ///   PopupType 을 하나 더 만들면 PopupManager 프리팹 배열 등록이 늘고
    ///   팝업 위에 팝업이 겹친다. 고르는 동안 뒤에 그 몬스터의 스탯이
    ///   보이는 편이 낫기도 하다 — 무엇이 오를지 대조하며 고른다.
    ///
    /// ⚠ 칸은 여기서 굽지 않는다 — 템플릿 하나만 만든다
    ///   보유 장비 수가 계속 늘어난다. N칸을 미리 구우면 그 수를 넘는 순간
    ///   조용히 잘린다. 런타임이 템플릿을 복제해 쓰고 남으면 끈다.
    /// </summary>
    static void BuildPicker(GameObject root, out PickRefs refs)
    {
        // 가운데 칸 왼쪽 끝부터 화면 오른쪽 끝까지 덮는다.
        var panel = EditorUIBuilder.Img(root, "GearPicker", ColBg).gameObject;
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(Pad + LeftW + ColGap, Pad);
            rt.offsetMax = new Vector2(-Pad, -(HeaderH + Pad));
        }

        var title = EditorUIBuilder.TMP(panel, "Title", "1번 칸", UIScale.FontMd, FontStyles.Bold);
        title.color         = MonsterC;
        title.alignment     = TextAlignmentOptions.MidlineLeft;
        title.raycastTarget = false;
        EditorUIBuilder.AnchorTop(title.rectTransform, Pad, UIScale.RowMd, 24f);

        // 닫기 — 오른쪽 위
        var close = EditorUIBuilder.RaisedBtn(panel, "PickCloseBtn",
                                              new Color(0.30f, 0.33f, 0.46f), out var closeBody);
        {
            var rt = close.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Pad, -Pad + 6f);
            rt.sizeDelta        = new Vector2(72f, 72f);
        }
        EditorUIBuilder.XMark(closeBody, "X", 30f, new Color(0.92f, 0.94f, 0.98f));

        // ⚠ [벗기기]·[상세] 는 없다 (사용자 지시, 2026-09-12) — 낀 장비가 목록 맨 앞에
        //   "장착 중" 으로 서고, 누르면 장비 상세(레벨업)가 열린다.

        // 고를 것이 없을 때의 안내 — 목록 자리 한가운데.
        var empty = EditorUIBuilder.TMP(panel, "Empty", "", UIScale.FontMd, FontStyles.Normal);
        empty.color         = SubText;
        empty.alignment     = TextAlignmentOptions.Center;
        empty.raycastTarget = false;
        EditorUIBuilder.Stretch(empty.gameObject);

        // ── 목록 ──
        float listTop = Pad + UIScale.RowMd + 16f;

        var box = EditorUIBuilder.Go("ListBox", panel);
        {
            var rt = box.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(Pad, Pad);
            rt.offsetMax = new Vector2(-Pad, -listTop);
        }
        box.AddComponent<RectMask2D>();

        var scroll = box.AddComponent<ScrollRect>();
        scroll.horizontal        = false;
        scroll.movementType      = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 46f;

        var content = EditorUIBuilder.Go("Content", box);
        {
            var rt = content.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        // ⚠ 여기는 GridLayoutGroup 을 써도 된다 (도감 격자와 다르다)
        //   도감은 RecycleGridScroll 이 자리를 직접 계산해서 레이아웃 그룹과
        //   싸운다. 이쪽은 칸 수가 적어 재활용 없이 전부 만든다 —
        //   그러면 자리 계산을 레이아웃 그룹에 맡기는 편이 짧다.
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize    = new Vector2(PickCellW, PickCellH);
        grid.spacing     = new Vector2(14f, 14f);
        grid.padding     = new RectOffset(6, 6, 6, 6);
        grid.constraint  = GridLayoutGroup.Constraint.FixedColumnCount;
        // ⚠ 폭 검산 — 목록 칸 1378 − 여백 26×2 = 1326. 5×210 + 4×14 + 12 = 1118 ✔ (6열이면 1342 로 넘친다)
        grid.constraintCount = PickColumns;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = box.GetComponent<RectTransform>();
        scroll.content  = content.GetComponent<RectTransform>();

        GameObject template = BuildPickCell(content);

        panel.SetActive(false);

        refs = new PickRefs
        {
            Root         = panel,
            CellTemplate = template,
            Title        = title,
            Empty        = empty,
            Content      = content.GetComponent<RectTransform>(),
            CloseBtn     = close,
        };
    }

    /// <summary>
    /// 목록 칸 하나 — 도감 셀과 같은 짜임(Frame &gt; Fill &gt; …)이다.
    ///
    /// ⚠ 자식 이름이 런타임과의 계약이다
    ///   MonsterDetailPopup.BindPickCell 이 이 경로로 Find 한다.
    /// </summary>
    static GameObject BuildPickCell(GameObject parent)
    {
        var cell = EditorUIBuilder.Go("PickCellTemplate", parent);
        var btn  = cell.AddComponent<Button>();

        var frame = EditorUIBuilder.Img(cell, "Frame", new Color(0.32f, 0.36f, 0.52f));
        EditorUIBuilder.Stretch(frame.gameObject);
        btn.targetGraphic = frame;
        EditorUIBuilder.TintTransition(cell, Color.white);

        var fill = EditorUIBuilder.Img(frame.gameObject, "Fill", RowBg);
        EditorUIBuilder.Stretch(fill.gameObject);
        fill.rectTransform.offsetMin = new Vector2(3f, 3f);
        fill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        fill.raycastTarget = false;

        // ⚠ 칸에는 **그림과 이름뿐**이다 (사용자 지시, 2026-09-12 "설명이 너무 많아 보기 어렵다")
        //   등급·레벨·능력치는 누르면 뜨는 장비 상세가 말한다.
        //   세로 예산 (칸 안 204): 여백 10 + 그림 136 + 6 + 이름 한 줄(≈38) = 190 ✔
        var icon = EditorUIBuilder.Img(fill.gameObject, "Icon", Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        {
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.sizeDelta        = new Vector2(PickIcon, PickIcon);
        }

        float f = UIScale.FontSm * 0.9f;

        var name = EditorUIBuilder.TMP(fill.gameObject, "Name", "이름", f, FontStyles.Bold);
        name.alignment        = TextAlignmentOptions.Top;
        name.raycastTarget    = false;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.enableAutoSizing = true;
        name.fontSizeMin      = f * 0.7f;
        name.fontSizeMax      = f;
        EditorUIBuilder.AnchorTop(name.rectTransform, 10f + PickIcon + 6f, UIScale.Line(f), 8f);

        // ── "장착 중" — 이 몬스터가 낀 장비 (왼쪽 위) ──
        var equipped = EditorUIBuilder.Img(fill.gameObject, "Equipped", new Color(0.22f, 0.44f, 0.36f));
        equipped.raycastTarget = false;
        {
            var rt = equipped.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(6f, -6f);
            rt.sizeDelta        = new Vector2(112f, 40f);
        }
        var eqText = EditorUIBuilder.TMP(equipped.gameObject, "Text", "장착 중", UIScale.FontSm * 0.8f, FontStyles.Bold);
        eqText.color         = Color.white;   // 짙은 초록 면 위 — 밝은 글자 (UI 규칙 8)
        eqText.alignment     = TextAlignmentOptions.Center;
        eqText.raycastTarget = false;
        EditorUIBuilder.Stretch(eqText.gameObject);
        equipped.gameObject.SetActive(false);

        // ── 다른 몬스터가 낀 장비 — 그 몬스터의 초상화 (오른쪽 위) ──
        //   ⚠ 테두리는 앞 형제 대신 부모 면으로 깐다 — 초상화가 자식이라 위에 그려진다 (UI 규칙 3)
        var wearer = EditorUIBuilder.Img(fill.gameObject, "Wearer", new Color(0.72f, 0.76f, 0.90f));
        wearer.raycastTarget = false;
        {
            var rt = wearer.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta        = new Vector2(WearerSize, WearerSize);
        }
        var wBg = EditorUIBuilder.Img(wearer.gameObject, "Bg", new Color(0.07f, 0.08f, 0.13f));
        wBg.raycastTarget = false;
        EditorUIBuilder.Stretch(wBg.gameObject);
        wBg.rectTransform.offsetMin = new Vector2(3f, 3f);
        wBg.rectTransform.offsetMax = new Vector2(-3f, -3f);

        var wPortrait = EditorUIBuilder.Img(wearer.gameObject, "Portrait", Color.white);
        wPortrait.preserveAspect = true;
        wPortrait.raycastTarget  = false;
        EditorUIBuilder.Stretch(wPortrait.gameObject);
        wPortrait.rectTransform.offsetMin = new Vector2(5f, 5f);
        wPortrait.rectTransform.offsetMax = new Vector2(-5f, -5f);
        wearer.gameObject.SetActive(false);

        cell.SetActive(false);
        return cell;
    }

    /// <summary>목록 칸의 장비 그림 — 칸 폭 210 의 2/3 쯤. 이름 한 줄이 아래에 들어가야 한다.</summary>
    const float PickIcon   = 136f;
    /// <summary>목록 칸 오른쪽 위의 착용 몬스터 초상화.</summary>
    const float WearerSize = 72f;

    // ── 공통 ─────────────────────────────────────────────────

    /// <summary>세로 칸 하나. width 가 0 이면 오른쪽 끝까지 늘린다.</summary>
    static GameObject Column(GameObject root, string name, float x, float width)
    {
        var col = EditorUIBuilder.Img(root, name, ColBg).gameObject;

        var rt = col.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(width > 0f ? 0f : 1f, 1f);
        rt.pivot     = new Vector2(0f, 0.5f);

        if (width > 0f)
        {
            rt.offsetMin = new Vector2(Pad + x, Pad);
            rt.offsetMax = new Vector2(Pad + x + width, -(HeaderH + Pad));
        }
        else
        {
            rt.offsetMin = new Vector2(Pad + x, Pad);
            rt.offsetMax = new Vector2(-Pad, -(HeaderH + Pad));
        }

        return col;
    }

    static void SectionTitle(GameObject col, string text, float yFromTop)
    {
        var tmp = EditorUIBuilder.TMP(col, "SectionTitle", text, UIScale.FontMd, FontStyles.Bold);
        EditorUIBuilder.AnchorTop(tmp.rectTransform, yFromTop, UIScale.RowMd, 18f);
        tmp.color         = MonsterC;
        tmp.alignment     = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
    }
}
