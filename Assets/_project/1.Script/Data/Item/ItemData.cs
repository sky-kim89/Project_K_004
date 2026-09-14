using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  ItemData.cs
//  재화·아이템 보유 수량 저장 섹션 (ISaveSection).
//
//  보유 데이터: eItem 별 보유 수량
//
//  사용법:
//    var items = UserDataManager.Instance.Get<ItemData>();
//    items.Add(eItem.Gold, 500);
//    if (items.Spend(eItem.Gold, 100)) ...
//    UserDataManager.Instance.RequestSave();
// ============================================================

public class ItemData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.ItemData;

    // ── 재화 변경 알림 이벤트 — 수량이 바뀐 아이템과 변경 후 수량 ──
    public static event Action<eItem, int>      OnItemChanged;

    // ── 내부 직렬화 데이터 ───────────────────────────────────

    ItemRawData _raw = new();

    // ── 환생 포인트 위임 ─────────────────────────────────────
    //  ⚠ 환생 포인트는 이 섹션에 저장하지 않는다
    //    잔액의 정본은 ReincarnationData 하나뿐이다 (유물 강화가 거기서 차감한다).
    //    게다가 이 섹션은 환생할 때 SetDefaults 로 통째로 비워진다 —
    //    여기에 쌓으면 "환생 포인트 +5" 이벤트 보상이 어디에도 보이지 않고
    //    환생과 함께 사라진다. 실제로 그랬다.
    //    지급·소비·조회가 어느 경로로 들어오든 전부 그쪽으로 넘긴다.

    static bool IsReincPoint(eItem item) => item == eItem.ReincarnationPoint;

    static ReincarnationData Reinc => UserDataManager.Instance.Get<ReincarnationData>();

    // ── 조회 ─────────────────────────────────────────────────

    public int Get(eItem item) => IsReincPoint(item) ? Reinc.ReincarnationPoints : _raw.Get(item);

    public bool CanSpend(eItem item, int amount) => Get(item) >= amount;

    // ── 단일 획득 ────────────────────────────────────────────

    public void Add(eItem item, int amount)
    {
        if (amount <= 0) return;

        if (IsReincPoint(item))
        {
            Reinc.EarnPoints(amount);
            OnItemChanged?.Invoke(item, Reinc.ReincarnationPoints);
            return;
        }

        _raw.Add(item, amount);
        OnItemChanged?.Invoke(item, _raw.Get(item));
    }

    // ── 소비 ─────────────────────────────────────────────────

    /// <returns>소비 성공 여부. 잔액 부족이면 false 반환하고 수량 변경 없음.</returns>
    public bool Spend(eItem item, int amount)
    {
        if (IsReincPoint(item))
        {
            if (!Reinc.TrySpendPoints(amount)) return false;
            OnItemChanged?.Invoke(item, Reinc.ReincarnationPoints);
            return true;
        }

        if (!CanSpend(item, amount)) return false;
        _raw.Add(item, -amount);
        OnItemChanged?.Invoke(item, _raw.Get(item));
        return true;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize() => JsonUtility.ToJson(_raw);

    public void SetDefaults()
    {
        _raw = new ItemRawData();
        _raw.Set(eItem.Gold, 500);
        NotifyAll();
    }

    /// <summary>
    /// 환생 초기화 — <b>영구 골드만 남기고</b> 나머지를 기본값으로 되돌린다.
    ///
    /// ⚠ 이 게임에서 eItem.Gold 는 영구 재화다 (몬스터 품질·장비 레벨업이 쓴다)
    ///   환생이 SetDefaults 를 불러 매번 500 으로 되돌려서, 런에서 번 골드가
    ///   결산되자마자 사라졌다 (사용자 지적, 2026-09-11).
    /// </summary>
    public void ResetKeepingGold()
    {
        int gold = _raw.Get(eItem.Gold);
        SetDefaults();
        _raw.Set(eItem.Gold, gold);
        OnItemChanged?.Invoke(eItem.Gold, gold);

        // ⚠ 진단 로그 (사용자 지적, 2026-09-11) — 원인을 잡으면 지울 것
        Debug.Log($"[ItemData] 환생 초기화 — 영구 골드 {gold} 유지");
    }

    public void Deserialize(string json)
    {
        _raw = JsonUtility.FromJson<ItemRawData>(json) ?? new ItemRawData();
        NotifyAll();
    }

    void NotifyAll()
    {
        foreach (eItem item in System.Enum.GetValues(typeof(eItem)))
        {
            // 환생 포인트는 여기서 빼둔다 — 로드 도중이라 ReincarnationData 가
            // 아직 자기 세이브를 읽기 전일 수 있다. 0 을 방송하면 거짓말이 된다.
            if (item == eItem.None || IsReincPoint(item)) continue;
            OnItemChanged?.Invoke(item, _raw.Get(item));
        }
    }

    // ── 직렬화 전용 내부 클래스 ──────────────────────────────
    // JsonUtility 는 Dictionary 직렬화를 지원하지 않으므로 병렬 List 사용.

    [Serializable]
    class ItemRawData
    {
        public List<int> Keys   = new();
        public List<int> Values = new();

        public int Get(eItem item)
        {
            int idx = Keys.IndexOf((int)item);
            return idx < 0 ? 0 : Values[idx];
        }

        public void Set(eItem item, int value)
        {
            int key = (int)item;
            int idx = Keys.IndexOf(key);
            if (idx < 0)
            {
                Keys.Add(key);
                Values.Add(Mathf.Max(0, value));
            }
            else
            {
                Values[idx] = Mathf.Max(0, value);
            }
        }

        public void Add(eItem item, int delta)
        {
            Set(item, Get(item) + delta);
        }
    }
}
