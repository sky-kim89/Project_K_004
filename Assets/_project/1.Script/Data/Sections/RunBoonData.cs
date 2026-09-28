using System;
using UnityEngine;

// ============================================================
//  RunBoonData.cs
//  런 도중에 **영구히 얹히는 보너스**를 한 곳에 모은 스탯 블록.
//
//  ■ 왜 한 섹션에 모으나
//    제단이 남긴 시너지 카운트, 상점에서 산 최대 마나 … 전부 성격이 같다:
//      · 런 스코프다 (환생·패배로 사라진다)
//      · 되돌릴 수 없다
//      · 여러 시설·이벤트가 같은 값을 건드린다
//    시설마다 저장 자리를 따로 만들면 이어하기 복원 순서가 시설 수만큼 늘고,
//    "지금 내가 무엇을 얹었나" 를 한눈에 못 본다.
//
//  ■ ⚠ 값을 읽는 곳은 각자의 정본이다
//    시너지 카운트 → MonsterSynergyRule.CountOf
//    최대 마나     → RunPerkRule.MaxManaFor
//    여기는 **저장고**이지 규칙이 아니다. 여기서 문턱을 판정하거나
//    그릇을 계산하지 말 것.
//
//  ■ 시너지는 표식 순서로 담는다
//    ⚠ 배열 순서의 정본은 MonsterSynergyRule.AllTags 다. 그 순서가 바뀌면
//      옛 세이브의 값이 다른 시너지로 옮겨 간다 — 아이콘 배열과 같은 주의다.
// ============================================================

[Serializable]
class RunBoonJson
{
    /// <summary>표식별 카운트 보너스. 인덱스는 MonsterSynergyRule.AllTags 순서.</summary>
    public int[] synergy = Array.Empty<int>();

    /// <summary>상점·이벤트로 늘린 최대 마나.</summary>
    public int maxMana;

    /// <summary>이벤트로 늘린 시그니처 스킬의 스테이지당 사용 횟수.</summary>
    public int signature;

    /// <summary>야영지 증축·이벤트가 늘린 최대 마나 (2026-09-12).</summary>
    public int extraMana;

    /// <summary>결정술사 개성 '결정화' 가 쌓은 최대 마나 (2026-09-12).</summary>
    public int crystalMana;

    /// <summary>상점 '전쟁 자금' 을 산 횟수 (2026-09-13). 값이 이 수로 오른다.</summary>
    public int warFund;

    /// <summary>상점 '소집의 북' 을 산 횟수 (2026-09-15). 배출 간격 스택이자 값의 계단.</summary>
    public int drum;
}

public class RunBoonData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.RunBoon;

    int[] _synergy = new int[MonsterSynergyRule.AllTags.Length];

    /// <summary>상점·이벤트로 늘린 최대 마나. RunPerkRule.MaxManaFor 가 더한다.</summary>
    public int MaxManaBonus { get; private set; }

    /// <summary>
    /// 이벤트로 늘린 시그니처 스킬의 <b>스테이지당</b> 사용 횟수.
    /// 읽는 곳은 SummonerSkillRule.Remaining 하나다 (특성 '집중' 과 같은 자리).
    /// </summary>
    public int SignatureBonus { get; private set; }

    /// <summary>
    /// 야영지 증축·이벤트가 늘린 최대 마나. RunPerkRule.MaxManaFor 가 더한다.
    /// ⚠ MaxManaBonus 와 따로 둔다 — 그쪽은 **상점 정수를 산 횟수**이기도 해서
    ///   (정수 값이 그 수로 오른다) 여기에 섞으면 야영지를 들를수록 정수가 비싸진다.
    /// </summary>
    public int ExtraMaxMana { get; private set; }

    /// <summary>결정술사 개성 '결정화' 가 쌓은 최대 마나 (SummonerPerkRuntime.SettleCrystal — 상한이 따로 있다).</summary>
    public int CrystalMaxMana { get; private set; }

    /// <summary>
    /// 상점 '전쟁 자금' 을 산 횟수. <b>두 가지를 동시에 뜻한다</b> —
    /// 값이 오르는 계단(RunShopRule.WarFundPrice)이자 전 몬스터에 얹히는
    /// 공/체 스택 수(RunShopRule.WarFundStatMult)다.
    ///
    /// ⚠ 정수(MaxManaBonus)와 같은 규칙이다 — 세이브에 수를 하나 더 두면 둘이 갈린다.
    /// </summary>
    public int WarFundStacks { get; private set; }

    /// <summary>상점 '소집의 북' 을 산 횟수 — 값의 계단(RunShopRule.DrumPrice)이자 간격 스택(DrumIntervalMult).</summary>
    public int DrumStacks { get; private set; }

    /// <summary>값이 바뀌었다 — 시너지 줄·마나 게이지가 다시 그려져야 한다.</summary>
    public event Action Changed;

    // ── 시너지 ───────────────────────────────────────────────

    /// <summary>그 표식에 얹힌 카운트. 없으면 0.</summary>
    public int SynergyOf(MonsterTag tag)
    {
        int at = MonsterSynergyRule.IndexOf(tag);
        return at >= 0 && at < _synergy.Length ? _synergy[at] : 0;
    }

    /// <summary>표식 하나에 카운트를 얹는다 (제단 제물·이벤트).</summary>
    public void AddSynergy(MonsterTag tag, int amount = 1)
    {
        int at = MonsterSynergyRule.IndexOf(tag);
        if (at < 0 || at >= _synergy.Length || amount == 0) return;

        _synergy[at] = Mathf.Max(0, _synergy[at] + amount);

        // ⚠ 문턱을 다시 세야 한다 — 카운트가 바뀌면 단계가 바뀔 수 있다.
        MonsterSynergyRule.Recount();
        Changed?.Invoke();
    }

    /// <summary>얹힌 시너지가 하나라도 있는가. 화면이 "제단 보너스" 줄을 띄울지 정한다.</summary>
    public bool HasAnySynergy
    {
        get
        {
            for (int i = 0; i < _synergy.Length; i++)
                if (_synergy[i] > 0) return true;

            return false;
        }
    }

    // ── 마나 ─────────────────────────────────────────────────

    public void AddMaxMana(int amount)
    {
        if (amount == 0) return;

        MaxManaBonus = Mathf.Max(0, MaxManaBonus + amount);
        Changed?.Invoke();
    }

    /// <summary>야영지 증축·이벤트 몫. ⚠ 직접 부르지 말고 RunPerkRule.GrowMaxMana 를 지날 것 — 그릇·잔량을 함께 맞춘다.</summary>
    public void AddExtraMaxMana(int amount)
    {
        if (amount == 0) return;

        ExtraMaxMana = Mathf.Max(0, ExtraMaxMana + amount);
        Changed?.Invoke();
    }

    public void AddCrystalMaxMana(int amount)
    {
        if (amount == 0) return;

        CrystalMaxMana = Mathf.Max(0, CrystalMaxMana + amount);
        Changed?.Invoke();
    }

    // ── 상시 판매 (상점) ─────────────────────────────────────

    /// <summary>전쟁 자금을 한 번 샀다. ⚠ 값의 계단이자 스택 수다 (위 주석).</summary>
    public void AddWarFund(int count = 1)
    {
        if (count == 0) return;

        WarFundStacks = Mathf.Max(0, WarFundStacks + count);
        Changed?.Invoke();
    }

    /// <summary>소집의 북을 한 번 샀다.</summary>
    public void AddDrum(int count = 1)
    {
        if (count == 0) return;

        DrumStacks = Mathf.Max(0, DrumStacks + count);
        Changed?.Invoke();
    }

    // ── 시그니처 스킬 ────────────────────────────────────────

    /// <summary>스테이지당 사용 횟수를 늘린다 (이벤트 '봉인된 지팡이').</summary>
    public void AddSignatureUse(int amount)
    {
        if (amount == 0) return;

        SignatureBonus = Mathf.Max(0, SignatureBonus + amount);
        Changed?.Invoke();
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(new RunBoonJson
    {
        synergy   = (int[])_synergy.Clone(),
        maxMana     = MaxManaBonus,
        signature   = SignatureBonus,
        extraMana   = ExtraMaxMana,
        crystalMana = CrystalMaxMana,
        warFund     = WarFundStacks,
        drum        = DrumStacks,
    });

    public void Deserialize(string json)
    {
        var data = JsonUtility.FromJson<RunBoonJson>(json);

        _synergy = new int[MonsterSynergyRule.AllTags.Length];

        // ⚠ 길이를 믿지 않는다 — 표식이 늘어난 뒤의 옛 세이브는 배열이 짧다.
        if (data?.synergy != null)
        {
            int n = Mathf.Min(data.synergy.Length, _synergy.Length);
            for (int i = 0; i < n; i++) _synergy[i] = data.synergy[i];
        }

        MaxManaBonus   = data?.maxMana     ?? 0;
        SignatureBonus = data?.signature   ?? 0;
        ExtraMaxMana   = data?.extraMana   ?? 0;
        CrystalMaxMana = data?.crystalMana ?? 0;
        WarFundStacks  = data?.warFund     ?? 0;
        DrumStacks     = data?.drum        ?? 0;

        Changed?.Invoke();
    }

    public void SetDefaults()
    {
        _synergy       = new int[MonsterSynergyRule.AllTags.Length];
        MaxManaBonus   = 0;
        SignatureBonus = 0;
        ExtraMaxMana   = 0;
        CrystalMaxMana = 0;
        WarFundStacks  = 0;
        DrumStacks     = 0;
        Changed?.Invoke();
    }
}
