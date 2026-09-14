using System.Collections.Generic;
using Random = UnityEngine.Random;

// ============================================================
//  RunPerkPicker.cs
//  허들 스테이지를 넘겼을 때 띄울 특성 후보를 뽑는다.
//
//  ■ 왜 허들에서만 주는가 (2026-09-04 확정)
//    카드 3택은 **매 스테이지** 뜬다. 특성까지 매 판 주면 판이 끝날 때마다
//    고를 것이 둘이라 어느 쪽도 무게가 없어진다. 특성은 수가 적고 하나하나가
//    강한 축이므로(RunPerk.cs 머리 주석), 리듬을 카드와 어긋나게 둔다.
//
//  ■ 가중치가 없다 — 못 가진 것 중에서 고르게 섞는다
//    카드 3택은 친화 가중치가 있지만(CardRewardPicker), 특성에는 "이 캐릭터에
//    어울리는 특성" 같은 축이 없다. 굳이 만들면 소환사마다 뜨는 특성이 갈려
//    "이 런에서 뭘 만날까" 가 캐릭터 선택에 흡수된다.
//
//  ⚠ 이미 가진 특성은 후보에 안 넣는다
//    RunPerkData.Add 가 중첩을 막으므로 넣어도 무해하지만, 화면에는
//    고를 수 없는 칸이 뜬다. 거르는 쪽은 여기다.
// ============================================================

public static class RunPerkPicker
{
    /// <summary>보스 판에서 보여 줄 후보 수.</summary>
    public const int ChoiceCount = 3;

    /// <summary>
    /// 엘리트 판에서 보여 줄 후보 수.
    ///
    /// ■ 보스와 같은 화면을 쓰되 **줄 수**로 무게를 가른다 (사용자 확정)
    ///   1 이면 "무작위 한 장을 받는다" 와 결과가 같지만, 화면이 같으므로
    ///   나중에 "엘리트 선택지 +1" 같은 업그레이드가 생기면 **이 숫자 하나만**
    ///   바꾸면 된다. 프리팹은 이미 8줄을 굽고 있다(ChoicePopup.MaxRows).
    /// </summary>
    public const int EliteChoiceCount = 1;

    /// <summary>
    /// 아직 갖지 않은 특성 중 <paramref name="count"/> 개를 뽑는다.
    /// 남은 게 모자라면 그만큼만 낸다 (빈 목록도 정상이다 — 전부 모은 런).
    /// </summary>
    /// <param name="count">
    /// 뽑을 개수. 기본은 <see cref="ChoiceCount"/>(보스 3택)이고,
    /// 엘리트는 1 을 넘겨 무작위 한 장만 받는다.
    /// </param>
    public static List<RunPerk> Pick(RunPerkData data, int count = ChoiceCount)
    {
        var result = new List<RunPerk>(count);
        if (data == null || count <= 0) return result;

        List<RunPerk> pool = data.CollectMissing();

        // ⚠ 풀에서 뽑아낸 것을 지우며 고른다
        //   같은 특성이 두 번 뜨면 한 칸이 통째로 낭비된다. 중복 검사보다
        //   꺼내면서 지우는 편이 짧고, 남은 수가 적을 때도 확실하다.
        int want = count < pool.Count ? count : pool.Count;

        for (int i = 0; i < want; i++)
        {
            int at = Random.Range(0, pool.Count);

            result.Add(pool[at]);
            pool.RemoveAt(at);
        }

        return result;
    }
}
