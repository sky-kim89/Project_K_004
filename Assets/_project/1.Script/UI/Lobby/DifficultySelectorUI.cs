using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  DifficultySelectorUI.cs
//  MainPanel 의 난이도 선택 칸 — 소환사를 고른 **다음** 단계에 뜬다.
//
//    난이도                              (?)
//    견습 소환사  출정 준비
//    ┌ [아이콘] 쉬움        환생 포인트 ×1.0 ┐
//    ┃ [아이콘] 보통        환생 포인트 ×1.2 ┃  ← 고른 줄만 등급 색 테두리
//    │ [자물쇠] 어려움  보통 20스테이지 돌파 │  ← 잠긴 줄은 흐리게 · 누를 수 없다//    …
//    적이 더 강해지고 수도 늘어난다.
//    적용 중인 제약  [광포][물량]           ← 눌러서 수치 툴팁
//
//  ■ ‹ › 로 넘기던 것을 다섯 줄로 펼쳤다 (2026-09-11, 사용자 요청)
//    한 번에 하나만 보이면 "위에 무엇이 더 있는지 · 얼마나 더 주는지" 를
//    넘겨 보기 전에는 모른다. 다섯 줄이 나란히 서면 보상 배율과 해금 조건을
//    한 번에 견줄 수 있다.
//
//  ■ 디버프는 아이콘으로 보여주고 눌러서 자세히 본다 (TraitIconUI 재사용)
//
//  ■ 런 도중에는 잠긴다
//    30스테이지를 쉬운 등급으로 깔고 마지막만 올리는 악용을 막는다.
// ============================================================

public class DifficultySelectorUI : MonoBehaviour
{
    [Serializable]
    public class TierRow
    {
        public Button          Button;
        [Tooltip("고른 줄만 등급 색으로 켜진다. ⚠ 버튼의 앞 형제다 (UI 규칙 3).")]
        public Image           Frame;
        public Image           Icon;
        public TextMeshProUGUI Label;
        [Tooltip("열린 줄은 보상 배율, 잠긴 줄은 해금 조건.")]
        public TextMeshProUGUI Note;
        public GameObject      Lock;
    }

    [Tooltip("DifficultyTier 순서 그대로 (0 = 쉬움).")]
    [SerializeField] TierRow[]       _rows;
    [SerializeField] TextMeshProUGUI _summaryLabel;
    [SerializeField] TextMeshProUGUI _lockLabel;       // 런 중 안내
    [SerializeField] TextMeshProUGUI _noDebuffLabel;   // 제약 없을 때 "없음"
    [SerializeField] TraitIconUI[]   _debuffIcons;

    public static readonly Color[] TierColors =
    {
        new Color(0.62f, 0.66f, 0.74f),   // 쉬움   — 강철
        new Color(0.25f, 0.76f, 0.66f),   // 보통   — 청록
        new Color(1.00f, 0.63f, 0.20f),   // 어려움 — 주황
        new Color(1.00f, 0.27f, 0.13f),   // 지옥   — 적색
        new Color(0.71f, 0.34f, 1.00f),   // 불지옥 — 보라
    };

    static readonly Color FrameOff  = new(0f, 0f, 0f, 0f);
    static readonly Color DimText   = new(0.36f, 0.38f, 0.48f);
    static readonly Color RewardOn  = new(0.45f, 0.86f, 0.62f);
    static readonly Color RewardOff = new(0.55f, 0.57f, 0.68f);
    static readonly Color RewardCut = new(1.00f, 0.62f, 0.35f);

    void Awake()
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            var tier = (DifficultyTier)i;   // ⚠ 캡처 — 루프 변수를 넘기면 전부 마지막 값이 된다
            _rows[i].Button.onClick.AddListener(() => Pick(tier));
        }
    }

    void OnEnable()
    {
        DifficultyData.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable() => DifficultyData.OnChanged -= Refresh;

    // ── 조작 ──────────────────────────────────────────────────

    void Pick(DifficultyTier tier)
    {
        UserDataManager.Instance.Get<DifficultyData>().Select(tier);   // 잠긴 등급은 Select 가 거른다
        UserDataManager.Instance.RequestSave();
    }

    // ── 표시 ──────────────────────────────────────────────────

    public void Refresh()
    {
        var  data     = UserDataManager.Instance.Get<DifficultyData>();
        var  config   = DifficultyConfig.Current;
        DifficultyTier selected = data.SelectedTier;

        for (int i = 0; i < _rows.Length; i++)
        {
            var  row      = _rows[i];
            var  tier     = (DifficultyTier)i;
            bool open     = data.IsUnlocked(tier);
            bool isChosen = tier == selected;
            Color col     = TierColors[i];

            row.Frame.color = isChosen ? col : FrameOff;

            row.Label.text  = tier.Label();
            row.Label.color = open ? col : DimText;

            row.Icon.sprite = SpriteManager.Instance.Get(tier.IconKey());
            row.Icon.color  = open ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            row.Lock.SetActive(!open);

            if (open)
            {
                float mul = config.Get(tier).ReincarnationMultiplier;
                row.Note.text  = $"환생 포인트 ×{mul:0.0#}";
                // 1 초과 = 보상 · 1 = 기준 · 1 미만 = 깎인다(쉬움)
                row.Note.color = mul > 1.001f ? RewardOn : mul < 0.999f ? RewardCut : RewardOff;
            }
            else
            {
                // 한 칸 아래 등급에서 UnlockStage 를 깨야 열린다 (DifficultyData.TryBreakthrough)
                // ⚠ 짧게 — 이 칸은 약 320px 뿐이고 줄바꿈·자동 축소가 없다.
                //   "어려움 20스테이지 돌파" 는 넘친다.
                row.Note.text  = $"{((DifficultyTier)(i - 1)).Label()} {DifficultyData.UnlockStage}스테이지";
                row.Note.color = DimText;
            }

            row.Button.interactable = open;
        }

        _summaryLabel.text = selected.Summary();

        RefreshDebuffIcons(config.Get(selected));

        _lockLabel.gameObject.SetActive(false);   // 로비에서만 열리는 칸이라 잠길 일이 없다
    }

    void RefreshDebuffIcons(DifficultyConfig.TierEntry entry)
    {
        int idx = 0;
        foreach (var d in entry.ActiveDebuffs())
        {
            if (idx >= _debuffIcons.Length) break;

            _debuffIcons[idx].SetupCustom(SpriteManager.Instance.Get(d.IconKey()),
                                          d.Label(),
                                          DifficultyConfig.Flavor(d),
                                          entry.DescribeDebuff(d));
            _debuffIcons[idx].gameObject.SetActive(true);
            idx++;
        }

        for (int i = idx; i < _debuffIcons.Length; i++)
            _debuffIcons[i].gameObject.SetActive(false);

        // 아이콘이 하나도 없으면 그 줄이 통째로 비어 보인다
        _noDebuffLabel.gameObject.SetActive(idx == 0);
    }
}
