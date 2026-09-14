using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  RelicTreeData.cs
//  유물 테크트리 노드 레벨 세이브 섹션 (영구 — 환생으로 지워지지 않는다).
//
//  ■ 구조
//    Dictionary<RelicNodeId, int> — 노드 ID → 찍은 레벨.
//    딕셔너리에 없으면 0레벨. 0레벨 = 아직 안 찍음 = 효과 없음.
//
//  ■ 해금 규칙은 여기 없다
//    부모를 찍어야 자식이 열린다는 판정은 RelicTreeCatalog.IsUnlocked 하나뿐이다.
//    세이브가 규칙을 또 들고 있으면 표를 고칠 때마다 두 곳이 어긋난다.
//
//  ■ 구 RelicInventoryData(SaveKey 7)는 제거됐다
//    카드 그리드 시절 세이브다. 섹션 등록이 빠졌으므로 읽지도 쓰지도 않는다 —
//    옛 세이브의 "Save_7" 문자열은 그대로 남지만 아무도 보지 않는다.
// ============================================================

[Serializable]
class RelicTreeJson
{
    public List<int> ids    = new();
    public List<int> levels = new();
}

public class RelicTreeData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.RelicTree;

    readonly Dictionary<RelicNodeId, int> _levels = new();

    /// <summary>노드 → 레벨. RelicTreeCatalog 의 해금·시야 판정에 그대로 넘긴다.</summary>
    public IReadOnlyDictionary<RelicNodeId, int> Levels => _levels;

    // ── 조회 ──────────────────────────────────────────────────

    public int GetLevel(RelicNodeId id) => _levels.TryGetValue(id, out int lv) ? lv : 0;

    public bool IsUnlocked(RelicNodeId id)   => RelicTreeCatalog.IsUnlocked(id, _levels);
    public bool IsVisible(RelicNodeId id)    => RelicTreeCatalog.IsVisible(id, _levels);
    public bool IsSilhouette(RelicNodeId id) => RelicTreeCatalog.IsSilhouette(id, _levels);

    /// <summary>1레벨 이상 찍은 노드 수.</summary>
    public int TakenCount
    {
        get
        {
            int n = 0;
            foreach (var kvp in _levels) if (kvp.Value > 0) n++;
            return n;
        }
    }

    /// <summary>지금까지 트리에 부은 총 포인트 (초기화 환불액과 같다).</summary>
    public int InvestedPoints
    {
        get
        {
            int sum = 0;
            foreach (var kvp in _levels)
            {
                var def = RelicTreeCatalog.Get(kvp.Key);
                for (int lv = 0; lv < kvp.Value; lv++) sum += def.LevelUpCost(lv);
            }
            return sum;
        }
    }

    // ── 조작 ──────────────────────────────────────────────────

    /// <summary>
    /// 노드 레벨을 1 올린다. 해금 안 됐거나 만렙이면 false.
    /// ⚠ 포인트 차감은 하지 않는다 — 부르는 쪽이 TrySpendPoints 로 먼저 결제한다.
    /// </summary>
    public bool LevelUp(RelicNodeId id)
    {
        var def = RelicTreeCatalog.Get(id);
        int lv  = GetLevel(id);
        if (lv >= def.MaxLevel)   return false;
        if (!IsUnlocked(id))      return false;
        _levels[id] = lv + 1;
        return true;
    }

    /// <summary>테스트·에디터용 — 레벨을 직접 설정한다.</summary>
    public void SetLevel(RelicNodeId id, int level)
    {
        if (level <= 0) _levels.Remove(id);
        else            _levels[id] = level;
    }

    /// <summary>
    /// 트리를 전부 0레벨로 되돌리고 투자한 포인트 전액을 반환한다.
    /// 호출한 쪽이 ReincarnationData.EarnPoints() 로 적립해야 한다.
    ///
    /// 지불한 것이 레벨업 비용뿐이라 전액 환불이며 손해도 이득도 없다 —
    /// 트리는 한 번 잘못 타면 되돌릴 방법이 없으므로 재분배는 공짜여야 한다.
    /// </summary>
    public int ResetAll()
    {
        int refund = InvestedPoints;
        _levels.Clear();
        return refund;
    }

    // ── ISaveSection ─────────────────────────────────────────

    public string Serialize()
    {
        var dto = new RelicTreeJson();
        foreach (var kvp in _levels)
        {
            dto.ids.Add((int)kvp.Key);
            dto.levels.Add(kvp.Value);
        }
        return JsonUtility.ToJson(dto);
    }

    public void Deserialize(string json)
    {
        _levels.Clear();
        if (string.IsNullOrEmpty(json)) return;

        var dto = JsonUtility.FromJson<RelicTreeJson>(json);
        if (dto == null) return;

        int n = Mathf.Min(dto.ids.Count, dto.levels.Count);
        for (int i = 0; i < n; i++)
        {
            var id = (RelicNodeId)dto.ids[i];
            // ⚠ 표에서 사라진 노드는 버린다
            //   트리를 개편하면 옛 세이브에 없는 ID 가 남는다. 그대로 두면
            //   RelicTreeCatalog.Get 이 KeyNotFound 로 터진다.
            if (!System.Enum.IsDefined(typeof(RelicNodeId), id)) continue;
            if (dto.levels[i] > 0) _levels[id] = dto.levels[i];
        }
    }

    public void SetDefaults() => _levels.Clear();
}
