using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
//  ManaRegenHoverUI.cs
//  마나 칸에 올리면(터치는 누르면) **다음 판 회복 내역**을 툴팁으로 편다.
//
//  ■ 왜 툴팁인가
//    카드 바에 적을 수 있는 것은 결과 한 줄("다음 판 +18")뿐이다.
//    그 18 이 어디서 왔는지 — 지능 몫, 아껴 둔 몫, 개성·특성 배율 — 는
//    네 줄이라 상시 표시할 자리가 없다. 궁금할 때만 펴는 것이 맞다.
//
//  ■ 시너지 칩과 **같은 규칙**이다 (SynergyChipUI)
//    올리면 뜨고, 벗어나면 닫히고, 고정하지 않는다.
//    누르는 것은 터치를 위한 문일 뿐 결과가 같다.
//    ⚠ 화면 안에서 툴팁 동작이 두 가지면 사용자는 매번 시험해 봐야 한다.
//
//  ■ 툴팁 판은 화면 전체가 한 장을 돌려 쓴다 (TooltipLayer)
//    시너지 칩과 같은 장을 쓰므로 둘이 동시에 뜰 수 없다.
//
//  ⚠ 글은 ManaRegenRule.Describe 가 만든다 — 여기서 문장을 조립하지 말 것.
//    실제 지급과 같은 함수에서 뽑아야 설명과 결과가 갈리지 않는다.
// ============================================================

public class ManaRegenHoverUI : MonoBehaviour,
                                IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // PC 는 올려서, 모바일은 눌러서 연다 (TooltipInput)
    public void OnPointerEnter(PointerEventData _) { if (TooltipInput.HoverMode) Open(); }
    public void OnPointerExit(PointerEventData _)  { if (TooltipInput.HoverMode) TooltipLayer.Close(); }
    public void OnPointerClick(PointerEventData _) { if (!TooltipInput.HoverMode) Open(); }

    /// <summary>칸이 꺼지면 설명도 닫는다 — 가리키던 것이 사라졌는데 글만 남으면 안 된다.</summary>
    void OnDisable() => TooltipLayer.Close();

    void Open()
    {
        SummonerData summoner = SummonerRuntimeBridge.Current?.Data;
        var          mana     = UserDataManager.Instance?.Get<SummonManaData>();

        if (summoner == null || mana == null) return;

        TooltipLayer.Open((RectTransform)transform,
                          LocalizationManager.Instance.Format("다음 판 회복  +{0:0.#}", ManaRegenRule.PreviewFor(summoner, mana)),
                          ManaRegenRule.Describe(summoner, mana),
                          string.Empty);
    }
}
