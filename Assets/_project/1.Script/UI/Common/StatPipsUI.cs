using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  StatPipsUI.cs
//  "힘 ■■■■■■■■□□ 8" — 10칸 눈금으로 보여 주는 스탯 한 줄.
//
//  ■ 왜 숫자가 아니라 눈금인가
//    소환사의 3대 스탯은 **상한이 10 으로 정해져 있다**(SummonerData.MaxCoreStat).
//    상한이 있는 값은 "8" 보다 "10칸 중 8칸" 이 훨씬 빨리 읽힌다 —
//    캐릭터끼리 비교할 때 눈으로 길이만 견주면 되기 때문이다.
//    합계가 전부 같도록 배분해 둔 설계(19 고정)도 이렇게 해야 드러난다.
//
//  ■ 숫자를 지우지는 않는다
//    눈금은 비교용, 숫자는 확인용이다. 8과 9를 눈금만으로 구별하기는 어렵다.
//
//  ■ 상한을 넘는 값도 표현한다
//    유물·특성으로 10을 넘길 수 있다(SummonerData 주석). 넘친 만큼은
//    마지막 칸을 다른 색으로 칠해 "상한을 넘었다" 를 알린다 —
//    칸을 더 그리면 다른 줄과 길이가 달라져 비교가 깨진다.
// ============================================================

public class StatPipsUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("스탯 이름. \"힘\" · \"체력\" · \"지능\".")]
    [SerializeField] TextMeshProUGUI _label;

    [Tooltip("현재 수치. 눈금만으로는 8과 9가 구별되지 않는다.")]
    [SerializeField] TextMeshProUGUI _valueText;

    [Tooltip("눈금 칸. MaxPips(10) 개를 왼쪽부터 넣는다.")]
    [SerializeField] Image[] _pips;

    [Header("색")]
    [Tooltip("채워진 칸.")]
    [SerializeField] Color _filled = new(0.95f, 0.78f, 0.32f);

    [Tooltip("빈 칸.")]
    [SerializeField] Color _empty = new(0.16f, 0.17f, 0.26f);

    [Tooltip("상한(10)을 넘겼을 때 마지막 칸에 쓰는 색.")]
    [SerializeField] Color _overflow = new(1f, 0.42f, 0.36f);

    /// <summary>눈금 칸 수. SummonerData.MaxCoreStat 과 같아야 한다.</summary>
    public const int MaxPips = 10;

    /// <summary>
    /// 한 줄을 그린다.
    ///
    /// ⚠ 반올림해서 채운다
    ///   8.4 를 8칸으로 그리고 숫자는 "8.4" 로 띄우면 어긋나 보인다.
    ///   숫자도 같은 반올림 규칙을 쓴다 — 소수점이 필요한 값이 아니다.
    /// </summary>
    public void Set(string label, float value, Color? filledOverride = null)
    {
        if (_label != null) _label.text = label;

        int rounded = Mathf.RoundToInt(value);
        if (_valueText != null) _valueText.text = rounded.ToString();

        Color on = filledOverride ?? _filled;

        for (int i = 0; i < _pips.Length; i++)
        {
            if (_pips[i] == null) continue;

            bool lit = i < Mathf.Min(rounded, MaxPips);

            // 마지막 칸은 상한 초과를 알리는 자리로 쓴다.
            bool over = rounded > MaxPips && i == MaxPips - 1;

            _pips[i].color = over ? _overflow
                           : lit  ? on
                                  : _empty;
        }
    }
}
