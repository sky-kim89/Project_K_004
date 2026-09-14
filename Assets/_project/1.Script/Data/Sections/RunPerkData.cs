using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RunPerkData.cs
//  이번 런에 얻은 특성(RunPerk) 목록 — 런 스코프 세이브 섹션.
//
//  ■ 중첩되지 않는다
//    같은 특성은 한 번만 들어간다. 중첩을 허용하면 "과잉 소환 3중첩" 이
//    라인 배출 간격을 0에 수렴시키는 식으로 규칙이 무너진다.
//    특성은 수가 적고 하나하나가 강해야 하는 축이다 (RunPerk 파일 머리 참고).
//
//  ■ "스테이지마다 1회" 는 여기서 센다
//    첫 소환 무료 같은 특성은 **이번 스테이지에 이미 썼는가**를 기억해야 한다.
//    그 상태를 SummonController 에 두면 특성이 없을 때도 계속 들고 있게 되고,
//    스테이지 경계에서 누가 지워야 하는지가 흐려진다. 특성의 상태는 특성이 갖는다.
//
//  ■ 환생·패배로 사라진다
//    영구 강화는 유물(RelicTree)이 담당한다.
// ============================================================

[Serializable]
class RunPerkJson
{
    public List<int> perks = new();

    /// <summary>비축(Hoard)으로 쌓인 최대 마나. 런 내내 누적되므로 저장한다.</summary>
    public float hoardBonus;

    /// <summary>친화 확장(AffinityExpand)이 고른 종족 ID.</summary>
    public string expandedAffinityId = "";

    /// <summary>이번 런에 상점에서 산 특성 수. 상점 특성 값이 이 수로 오른다.</summary>
    public int shopPerkBuys;

    /// <summary>마력 결정화(Crystallize)로 쌓인 최대 마나. 런 내내 누적되므로 저장한다.</summary>
    public float crystalBonus;
}

public class RunPerkData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.RunPerk;

    readonly List<RunPerk> _perks = new();

    /// <summary>이번 스테이지에 '첫 소환 무료' 를 이미 썼는가.</summary>
    bool _freeSummonUsed;

    /// <summary>이번 스테이지에 마나 잔량이 0 에 닿았는가 (마력 폭주).</summary>
    public bool ManaEmptiedThisStage { get; private set; }

    /// <summary>
    /// 이번 스테이지가 열릴 때의 누적 소모량. 비축이 "한 방울도 안 썼는가" 를 이걸로 본다.
    ///
    /// ⚠ 잔량이 아니라 **누적 소모량**을 본다
    ///   잔량으로 재면 회복이 섞여 들어와 "썼는데 안 쓴 것" 이 된다.
    ///   SummonManaData.Spent 는 소환에서만 오르므로 이쪽이 정확하다.
    /// </summary>
    float _spentAtStageBegin;

    /// <summary>비축으로 쌓인 최대 마나. RunPerkRule.MaxManaFor 가 더한다.</summary>
    public float HoardBonus { get; private set; }

    /// <summary>마력 결정화로 쌓인 최대 마나. RunPerkRule.MaxManaFor 가 더한다.</summary>
    public float CrystalBonus { get; private set; }

    /// <summary>친화 확장이 고른 종족 ID. 비어 있으면 아직 안 골랐다.</summary>
    public string ExpandedAffinityId { get; private set; } = "";

    /// <summary>
    /// 이번 런에 상점에서 산 특성 수 (RunShopRule.PerkPrice 가 읽는다).
    ///
    /// ⚠ 저장한다 — 안 하면 껐다 켜는 것이 특성 값을 되돌리는 길이 된다.
    /// ⚠ 보상(엘리트·보스)·이벤트로 얻은 것은 세지 않는다 — 값이 오르는 것은 '사는 것' 이다.
    /// </summary>
    public int ShopPerkBuys { get; private set; }

    public void NoteShopPerkBought() => ShopPerkBuys++;

    /// <summary>특성이 늘었다. UI 가 구독한다.</summary>
    public event Action Changed;

    public IReadOnlyList<RunPerk> Perks => _perks;

    public bool Has(RunPerk perk) => _perks.Contains(perk);

    // ── 획득 ─────────────────────────────────────────────────

    /// <summary>특성을 얻는다. 이미 갖고 있으면 false (중첩 없음).</summary>
    public bool Add(RunPerk perk)
    {
        if (perk == RunPerk.None) return false;
        if (_perks.Contains(perk)) return false;

        _perks.Add(perk);

        // 도감 '특성' 탭 — 한 번이라도 얻은 특성은 영구히 기록된다 (CodexData.RecordPerk)
        CodexData.RecordPerk(perk);

        // 친화 확장은 **얻는 순간** 대상을 정한다. 나중에 정하면 덱이 바뀔 때마다
        // 대상이 흔들려 "무엇이 친화인지" 를 화면에서 설명할 수 없다.
        if (perk == RunPerk.AffinityExpand) ChooseExpandedAffinity();

        // '확장 편성' 은 **얻는 순간** 칸을 늘린다 (사용자 지적, 2026-09-11)
        //   칸을 늘리는 곳이 런 시작(RunBootstrap.BuildStarterDeck) 하나뿐이라,
        //   런 도중 보상·상점·이벤트로 얻으면 카드 바가 6칸 그대로였다 —
        //   앱을 껐다 켜야 8칸이 됐다. 늘어난 칸은 Changed 로 카드 바에 곧바로 뜬다.
        if (perk == RunPerk.ExtraSlots)
        {
            var deck = UserDataManager.Instance.Get<SummonDeckData>();
            // ⚠ 상한에서 자른다 — 7칸 소환사는 +1 만 받는다 (화면이 8칸까지만 담는다)
            deck.ResizeTo(System.Math.Min(deck.SlotCount + RunPerkRule.ExtraSlotCount,
                                          RunPerkRule.MaxDeckSlots));
        }

        // '봉인된 칸' — 빈 칸 하나를 그 자리에서 없앤다. ⚠ 카드는 절대 버리지 않는다.
        //   후보에서 빈 칸이 없으면 이미 뺐다(CanOffer). 상점에서 재고를 본 뒤 카드를 사서
        //   칸이 찬 경우만 여기까지 온다 — 그때는 칸을 못 줄이고 스탯만 받는다.
        if (perk == RunPerk.SealedSlot)
        {
            var deck = UserDataManager.Instance.Get<SummonDeckData>();
            if (!deck.RemoveEmptySlot())
                Debug.LogWarning("[RunPerkData] 봉인된 칸 — 빈 칸이 없어 칸을 줄이지 못했습니다 (카드는 버리지 않는다).");
        }

        // '유리 성채' — 얻는 순간 성이 얇아진다. 소환력은 RunPerkRule.SummonPowerMult 가 준다.
        if (perk == RunPerk.GlassKeep)
            UserDataManager.Instance.Get<RunCoreData>().ScaleMax(1f - RunPerkRule.GlassKeepCoreCut);

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// 친화 확장의 대상을 고른다 — 덱에서 <b>아직 친화가 아닌</b> 몬스터 중 앞칸.
    ///
    /// ⚠ 지금은 자동 선택이다
    ///   고르는 화면이 생기면 여기 대신 <see cref="SetExpandedAffinity"/> 를 부르면 된다.
    ///   자동이라도 결정적이어야 하므로 덱 순서를 기준으로 잡는다 — 무작위로 고르면
    ///   같은 상황에서 런마다 다른 결과가 나와 설명할 수 없다.
    /// </summary>
    void ChooseExpandedAffinity()
    {
        if (!string.IsNullOrEmpty(ExpandedAffinityId)) return;

        var deck     = UserDataManager.Instance?.Get<SummonDeckData>();
        var summoner = SummonerRuntimeBridge.Current?.Data;
        var catalog  = CardCatalog.Current;

        if (deck == null || summoner == null || catalog == null) return;

        for (int i = 0; i < deck.SlotCount; i++)
        {
            SummonDeckSlot slot = deck.GetSlot(i);
            if (slot.IsEmpty || slot.Kind != SummonKind.Monster) continue;

            MonsterSpeciesData species = catalog.GetMonster(slot.Id);
            if (species == null || summoner.IsAffinity(species)) continue;

            ExpandedAffinityId = species.RootSpecies.Id;
            return;
        }
    }

    /// <summary>친화 확장의 대상을 손으로 정한다 (고르는 화면이 생기면 이쪽을 쓴다).</summary>
    public void SetExpandedAffinity(string speciesId)
    {
        ExpandedAffinityId = speciesId ?? "";
        Changed?.Invoke();
    }

    /// <summary>아직 갖지 않은 특성 목록. 보상 후보를 고를 때 쓴다.</summary>
    public List<RunPerk> CollectMissing()
    {
        var result = new List<RunPerk>();

        // ⚠ 소환사 개성과 **같은 것을 주는 특성**은 후보에서 뺀다
        //   (사용자 지적, 2026-09-09) 견습(친화 할인)이 특성 '친화 할인' 을
        //   또 받으면 할인이 두 번 겹쳐 친화 종족이 거의 공짜가 됐다.
        //   같은 축을 두 번 쥐여 주는 것은 선택지가 아니라 사고다 —
        //   막는 자리는 "고를 수 있게 만드는 곳" 인 여기다.
        RunPerk shadowed = ShadowedByPerk(SummonerRuntimeBridge.Current?.Data?.Perk
                                          ?? SummonerPerk.None);

        foreach (RunPerk perk in Enum.GetValues(typeof(RunPerk)))
        {
            if (perk == RunPerk.None) continue;
            if (perk == shadowed)     continue;
            if (_perks.Contains(perk)) continue;
            if (!CanOffer(perk))       continue;

            result.Add(perk);
        }

        return result;
    }

    /// <summary>
    /// 그 개성이 이미 주고 있는 특성. 없으면 <see cref="RunPerk.None"/>.
    ///
    /// ⚠ 표를 늘릴 때는 "겹치면 못 쓸 만큼 강해지는가" 를 볼 것
    ///   축이 같아도 서로 다른 대상에 걸리면(개성=친화 종족, 특성=전 종족)
    ///   겹쳐도 된다. 문제는 <b>같은 대상에 같은 값</b>이 두 번 붙는 경우다.
    /// </summary>
    static RunPerk ShadowedByPerk(SummonerPerk perk) => perk switch
    {
        SummonerPerk.CheapAffinity => RunPerk.CheapAffinity,
        _                          => RunPerk.None,
    };

    /// <summary>
    /// 지금 얻어도 뜻이 있는 특성인가. 아니면 후보에서 뺀다 — 골라도 아무 일이 없는 선택지는 선택지가 아니다.
    /// </summary>
    static bool CanOffer(RunPerk perk)
    {
        var deck = UserDataManager.Instance?.Get<SummonDeckData>();

        return perk switch
        {
            // 칸이 이미 상한이면 확장 편성은 아무 일도 안 한다
            RunPerk.ExtraSlots => deck == null || deck.SlotCount < RunPerkRule.MaxDeckSlots,

            // 빈 칸이 있어야 없앤다 — 카드를 버리게 하지 않는다. 칸이 셋 이하면 내지 않는다
            RunPerk.SealedSlot => deck != null && deck.FreeSlotCount > 0 && deck.SlotCount > 3,

            _ => true,
        };
    }

    // ── 스테이지 경계 ────────────────────────────────────────

    /// <summary>
    /// 스테이지가 새로 시작됐다 — 스테이지 단위 상태를 되돌린다.
    ///
    /// ⚠ 이걸 빠뜨리면 '첫 소환 무료' 가 런 전체에서 한 번만 발동한다
    ///   증상이 조용하다 — 첫 스테이지에서는 멀쩡히 동작하기 때문이다.
    /// </summary>
    public void OnStageBegin()
    {
        _freeSummonUsed      = false;
        ManaEmptiedThisStage = false;

        var mana = UserDataManager.Instance?.Get<SummonManaData>();
        _spentAtStageBegin = mana != null ? mana.Spent : 0f;
    }

    /// <summary>
    /// 마나 잔량이 0 에 닿았다 — 마력 폭주가 이번 판 내내 켜진다.
    ///
    /// ⚠ 한 번 켜지면 판이 끝날 때까지 안 꺼진다
    ///   매 프레임 잔량을 보고 켰다 껐다 하면 회복 한 방울에 소환력이 출렁여
    ///   같은 카드가 낸 몬스터끼리 세기가 달라진다.
    /// </summary>
    public void NoticeManaEmpty()
    {
        if (ManaEmptiedThisStage) return;
        if (!Has(RunPerk.ManaSurge)) return;

        ManaEmptiedThisStage = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// 스테이지를 넘겼다 — 비축이 여기서 정산된다.
    ///
    /// ⚠ 마나 회복보다 **먼저** 불러야 한다
    ///   회복이 그릇을 기준으로 잘리므로, 그릇이 커지기 전에 회복하면
    ///   비축으로 늘어난 칸이 그 판에는 비어 있게 된다.
    /// </summary>
    public void SettleStageEnd()
    {
        var mana = UserDataManager.Instance?.Get<SummonManaData>();
        if (mana == null) return;

        // 마력 결정화 — 회복 **전** 잔량을 본다 (아낀 만큼이 굳는다).
        //   ⚠ 내림이다 — 마나는 정수로만 움직인다 (ManaRegenRule.RawFor 주석).
        if (Has(RunPerk.Crystallize) && CrystalBonus < RunPerkRule.CrystallizeCeiling)
        {
            float gain = Mathf.Floor(mana.Current * RunPerkRule.CrystallizeRatio);
            if (gain > 0f)
            {
                CrystalBonus = Mathf.Min(CrystalBonus + gain, RunPerkRule.CrystallizeCeiling);
                Changed?.Invoke();
            }
        }

        if (!Has(RunPerk.Hoard)) return;
        if (HoardBonus >= RunPerkRule.HoardCeiling) return;

        // 한 방울이라도 썼으면 이번 판은 없던 일이다.
        if (mana.Spent > _spentAtStageBegin + 0.001f) return;

        HoardBonus = Mathf.Min(HoardBonus + RunPerkRule.HoardGain, RunPerkRule.HoardCeiling);
        Changed?.Invoke();
    }

    /// <summary>
    /// 이번 소환이 공짜인가. <b>맞으면 그 자리에서 소모한다.</b>
    ///
    /// ⚠ 조회와 소모를 나누지 않는다
    ///   "쓸 수 있나?" 와 "썼다" 를 따로 두면 부르는 쪽이 한쪽만 부르는 실수를
    ///   하게 되고, 그 결과는 무한 무료 소환이다. 한 번에 끝낸다.
    /// </summary>
    public bool ConsumeFreeSummon()
    {
        if (!Has(RunPerk.FreeFirstSummon)) return false;
        if (_freeSummonUsed)               return false;

        _freeSummonUsed = true;
        return true;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
    {
        var json = new RunPerkJson
        {
            hoardBonus         = HoardBonus,
            expandedAffinityId = ExpandedAffinityId,
            shopPerkBuys       = ShopPerkBuys,
            crystalBonus       = CrystalBonus,
        };
        foreach (RunPerk perk in _perks) json.perks.Add((int)perk);

        return JsonUtility.ToJson(json);
    }

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<RunPerkJson>(json);

        _perks.Clear();
        if (data?.perks != null)
            foreach (int value in data.perks) _perks.Add((RunPerk)value);

        HoardBonus         = data?.hoardBonus ?? 0f;
        ExpandedAffinityId = data?.expandedAffinityId ?? "";
        ShopPerkBuys       = data?.shopPerkBuys ?? 0;
        CrystalBonus       = data?.crystalBonus ?? 0f;

        // ⚠ 저장하지 않는다 — 스테이지 단위 상태다
        //   세이브를 불러온 시점이 스테이지 도중일 수 있지만, 무료 소환 한 번을
        //   더 주는 쪽이 못 쓰게 막는 쪽보다 낫다.
        _freeSummonUsed      = false;
        ManaEmptiedThisStage = false;
        _spentAtStageBegin   = UserDataManager.Instance?.Get<SummonManaData>()?.Spent ?? 0f;

        Changed?.Invoke();
    }

    public void SetDefaults()
    {
        _perks.Clear();
        _freeSummonUsed      = false;
        ManaEmptiedThisStage = false;
        _spentAtStageBegin   = 0f;
        HoardBonus           = 0f;
        ExpandedAffinityId   = "";
        ShopPerkBuys         = 0;
        CrystalBonus         = 0f;
        Changed?.Invoke();
    }

    /// <summary>런이 끝났다 — 특성을 전부 버린다. 카드와 같은 수명이다.</summary>
    public void ResetForNewRun() => SetDefaults();
}
