using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SummonerListCardUI.cs
//  MainPanel 왼쪽 격자의 소환사 카드 한 장 — 초상화 · 이름 · 잠금.
//
//  ■ 고르는 카드는 가볍게 둔다 (2026-09-11, 사용자 요청으로 화면을 다시 짬)
//    예전엔 소환사 한 명을 큰 카드로 띄우고 ◀ ▶ 로 넘겼다. 열두 명을 보려면
//    열두 번 넘겨야 했고, 누가 있는지·누가 잠겼는지를 한눈에 볼 수 없었다.
//    지금은 전원을 격자로 늘어놓고, 자세한 것은 오른쪽 정보 칸이 말한다.
//    이 카드는 "누구인가" 만 — 얼굴과 이름 — 말한다.
//
//  ■ 잠긴 소환사도 누를 수 있다
//    눌러야 오른쪽에 해금 조건이 뜬다. 막는 것은 **선택 버튼**이다.
// ============================================================

public class SummonerListCardUI : MonoBehaviour
{
    [SerializeField] Button               _button;
    [Tooltip("고른 카드만 밝아지는 테두리. ⚠ 버튼의 앞 형제다 (UI 규칙 3).")]
    [SerializeField] Image                _frame;
    [SerializeField] Image                _portraitBg;
    [SerializeField] Image                _portraitImage;
    [SerializeField] UnitAppearanceBridge _portraitBridge;
    [SerializeField] TextMeshProUGUI      _nameText;
    [SerializeField] GameObject           _lockRoot;

    static readonly Color FrameOn  = new(1.00f, 0.84f, 0.36f);
    static readonly Color FrameOff = new(0.16f, 0.18f, 0.27f);

    SummonerData _data;
    Texture2D    _portraitTexture;

    public void Setup(SummonerData data, Action<SummonerData> onClick)
    {
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onClick(data));

        _nameText.text = data.DisplayName;
        _lockRoot.SetActive(!SummonerUnlockRule.IsUnlocked(data));

        // 초상화 합성은 비싸다 (576×928 병합) — 같은 소환사면 다시 그리지 않는다.
        if (_data == data) return;
        _data = data;

        SummonerCandidateCardUI.RenderPortrait(data, _portraitBridge, _portraitBg,
                                               _portraitImage, ref _portraitTexture);
    }

    public void SetSelected(bool on) => _frame.color = on ? FrameOn : FrameOff;
}
