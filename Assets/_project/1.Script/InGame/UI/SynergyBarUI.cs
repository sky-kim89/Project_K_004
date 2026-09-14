using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SynergyBarUI.cs
//  지금 켜져 있는 몬스터 시너지를 상단에 늘어놓는다.
//
//  ■ 왜 필요한가
//    시너지는 덱 구성이 정하는데, 전투 화면에는 덱이 카드 그림으로만
//    보인다. 무엇이 켜졌는지 확인할 길이 없으면 "이 조합이 지금 도는가" 를
//    카드 3택 화면에서만 알 수 있고, 정작 그 결과를 보는 전투 중에는 모른다.
//
//  ■ 한 마리라도 낸 시너지는 **꺼져 있어도** 띄운다 (2026-09-03, 사용자 확정)
//    문턱을 못 넘긴 시너지가 안 보이면 "지금 하나 더 내면 열리는가" 를 알 수 없다.
//    아직 아무것도 안 낸(0장) 시너지만 감춘다 — 그건 이번 판과 상관없는 줄이다.
//
//  ■ 금 → 은 → 동 → 꺼짐 순으로 앞에 온다
//    가장 센 것이 왼쪽 끝에 있어야 곁눈으로 읽힌다. 아직 안 열린 것은
//    바탕을 어둡게 깔아 뒤로 물러나게 한다.
//
//  ■ 칸은 미리 만들어 두고 켜고 끈다
//    시너지가 바뀌는 것은 스테이지 경계뿐이라 잦지 않지만, Instantiate 를
//    쓰면 그때마다 GC 가 튄다. 여덟 칸이면 다 만들어 두는 편이 싸다.
// ============================================================

public class SynergyBarUI : MonoBehaviour
{
    /// <summary>칸 하나 — 바탕 + [아이콘][문턱].</summary>
    [System.Serializable]
    public class Chip
    {
        public GameObject      Root;
        public Image           Face;
        public Image           Icon;

        [Tooltip("문턱 줄 — \"2/4/6\". 도달한 것만 단계 색이다.")]
        public TextMeshProUGUI Label;

        [Tooltip("눌러서 효과를 보는 부분. 칸의 주인이 바뀔 때마다 Setup 을 다시 받는다.")]
        public SynergyChipUI   Hover;
    }

    [Tooltip("시너지 표식 수(8)만큼. 켜진 것부터 앞에서 채운다.")]
    [SerializeField] Chip[] _chips;

    // ⚠ 중첩 칩 — 줄 맨 위 (사용자 지시, 2026-09-12)
    //   중첩 보너스(켜진 시너지 3/5/7개)는 전투에만 걸리고 화면 어디에도 없었다.
    //   시너지가 하나라도 보이면 늘 선다 — 3개 미만이어도 흐리게 띄워야 "노릴 것" 이 보인다.
    [Tooltip("중첩 칩 — 켜진 시너지 수 3/5/7. 아이콘 칸은 Creator 가 겹친 마름모로 그린다.")]
    [SerializeField] Chip _stackChip;

    [Tooltip("아무 시너지도 안 켜졌을 때 통째로 끌 루트. 비워도 된다.")]
    [SerializeField] GameObject _root;

    [Tooltip("시너지 아이콘. ⚠ MonsterSynergyRule.AllTags 순서로 넣는다 (Creator 가 채운다).")]
    [SerializeField] Sprite[] _icons;

    void OnEnable()
    {
        // ⚠ 방어가 아니다 — 프리팹이 오래됐으면 무엇을 눌러야 하는지 말하며 터진다
        if (_stackChip == null || _stackChip.Root == null)
            throw new MissingReferenceException(
                "[SynergyBarUI] '_stackChip' 이 비어 있다 — HUD 프리팹이 Creator 보다 오래됐다.\n" +
                "Tools > Project K > UI > 인게임 HUD 를 다시 구울 것.");

        MonsterSynergyRule.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => MonsterSynergyRule.Changed -= Refresh;

    void Refresh()
    {
        int shown = 0;

        // 금 → 은 → 동 → 꺼짐. 같은 단계 안에서는 MonsterSynergyRule.AllTags 순서다.
        for (var tier = SynergyTier.Gold; tier >= SynergyTier.None; tier--)
        {
            foreach (MonsterTag tag in MonsterSynergyRule.AllTags)
            {
                if (MonsterSynergyRule.TierOf(tag) != tier) continue;

                // 한 마리도 안 낸 줄은 이번 판과 상관이 없다.
                if (MonsterSynergyRule.CountOf(tag) <= 0) continue;

                if (shown >= _chips.Length) break;

                Fill(_chips[shown++], tag, tier);
            }
        }

        for (int i = shown; i < _chips.Length; i++)
            _chips[i].Root.SetActive(false);

        _stackChip.Root.SetActive(shown > 0);
        if (shown > 0) FillStack(_stackChip);

        if (_root != null) _root.SetActive(shown > 0);
    }

    /// <summary>중첩 칩 — 바탕은 중첩 단계(1~3)를 동·은·금 색으로. 일반 칩과 같은 명암 규칙이다.</summary>
    void FillStack(Chip chip)
    {
        var tier = (SynergyTier)MonsterSynergyRule.StackStep(MonsterSynergyRule.ActiveCount);

        Color tint = MonsterSynergyRule.ColorOf(tier);
        float lit  = tier == SynergyTier.None ? 0.16f : 0.30f;
        chip.Face.color = new Color(tint.r * lit, tint.g * lit, tint.b * lit, 0.92f);

        chip.Label.text  = MonsterSynergyRule.StackStepsLabel();
        chip.Label.color = Color.white;   // 색은 글 안에 있다

        chip.Hover.SetupStack();
    }

    void Fill(Chip chip, MonsterTag tag, SynergyTier tier)
    {
        chip.Root.SetActive(true);

        Color tint = MonsterSynergyRule.ColorOf(tier);

        // 바탕은 단계 색을 어둡게 깐다. 글자는 그 위에 밝게 얹힌다 —
        // 색으로만 대비를 만든다 (UI 규칙 8).
        //
        // ⚠ 아직 안 열린 줄은 한 단계 더 어둡다
        //   같은 밝기면 켜진 것과 섞여, 여덟 개가 다 떠 있을 때 무엇이
        //   실제로 도는지 구분되지 않는다.
        float lit = tier == SynergyTier.None ? 0.16f : 0.30f;
        chip.Face.color = new Color(tint.r * lit, tint.g * lit, tint.b * lit, 0.92f);

        // 그림도 함께 죽인다 — 바탕만 어둡히면 아이콘이 떠 보인다.
        chip.Icon.color = tier == SynergyTier.None
            ? new Color(1f, 1f, 1f, 0.45f)
            : Color.white;

        // ⚠ 이름을 글자로 적지 않는다 — 그림이 이름을 대신한다 (사용자 확정)
        //   여덟 시너지가 상단에 늘어서면 한글 이름이 줄 폭을 다 먹는다.
        int index = MonsterSynergyRule.IndexOf(tag);
        chip.Icon.sprite  = (_icons != null && index >= 0 && index < _icons.Length)
                          ? _icons[index] : null;
        chip.Icon.enabled = chip.Icon.sprite != null;

        // ── 둘로 읽는다: [아이콘] [문턱] ──
        //
        //   ⚠ 보유 카운터를 따로 두지 않는다 (사용자 지적, 2026-09-07)
        //     한때 [아이콘][보유][문턱] 셋이었는데, 앞의 숫자 하나가 하는 말을
        //     문턱 줄이 이미 하고 있었다(도달한 것이 굵고 단계 색이다).
        //     정확한 수는 올리면 뜨는 툴팁 제목이 "숲 3/5" 로 말해 준다.
        chip.Label.text  = MonsterSynergyRule.StepsLabelOf(tag);
        chip.Label.color = Color.white;   // 색은 글 안에 있다 — 여기서 덮으면 안 된다

        // 칸마다 어느 시너지가 올지는 매 판 다르다 — 주인을 다시 알려 준다.
        if (chip.Hover != null) chip.Hover.Setup(tag);
    }
}
