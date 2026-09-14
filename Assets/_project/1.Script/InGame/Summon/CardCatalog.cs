using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  CardCatalog.cs
//  이 게임에 존재하는 모든 카드(몬스터·스킬)의 목록. Resources 에 한 장 둔다.
//
//  ■ 왜 필요한가 — 세이브에는 ID 만 있다
//    SummonDeckData 가 저장하는 건 문자열 ID 뿐이다(SO 참조를 저장할 수 없으니까).
//    그 ID 를 다시 SO 로 되돌릴 곳이 필요한데, 지금은 그 역할이 곳곳에 흩어져
//    있었다 — SummonDeckUI 는 인스펙터 배열을, RunBootstrap 은 Resources.LoadAll 을,
//    에디터 툴은 AssetDatabase.FindAssets 를 썼다. 셋이 다른 목록을 보면
//    "카드 바에는 뜨는데 소환하면 없는 종족" 같은 버그가 난다.
//
//  ■ 보상 후보의 출처이기도 하다
//    런 중에 무엇이 나올 수 있는가 = 이 목록이다. CardRewardPicker 가 여기서 뽑는다.
//
//  ■ 목록은 에디터가 굽는다
//    Tools > Project K > 데이터 생성 > 카드 목록 (CardCatalogCreator)
//    프로젝트의 모든 MonsterSpeciesData / SkillCardData 를 긁어 담는다.
//    ⚠ 카드를 새로 만들었으면 다시 구울 것 — 안 그러면 보상에 안 나온다.
// ============================================================

[CreateAssetMenu(fileName = "CardCatalog", menuName = "ProjectK/CardCatalog")]
public class CardCatalog : ScriptableObject
{
    const string ResourcePath = "CardCatalog";

    static CardCatalog _current;

    public static CardCatalog Current
        => _current != null ? _current : (_current = Resources.Load<CardCatalog>(ResourcePath));

    [Header("몬스터 카드")]
    public List<MonsterSpeciesData> Monsters = new();

    [Header("스킬 카드")]
    public List<SkillCardData> Skills = new();

    // ⚠ 소환사도 여기 담는다 — 목록이 두 벌이 되면 안 된다
    //   소환사 선택 화면(MainPanel)이 "고를 수 있는 캐릭터" 를 알아야 하는데,
    //   SummonerData 는 Resources 밖에 있어 런타임에 훑을 방법이 없었다.
    //   카드가 아니라서 이름이 어색하지만, 목록을 또 만드는 것보다 낫다.
    [Header("소환사")]
    public List<SummonerData> Summoners = new();

    Dictionary<string, MonsterSpeciesData> _monsterById;
    Dictionary<string, SkillCardData>      _skillById;

    void OnEnable() { _monsterById = null; _skillById = null; }

    /// <summary>ID 로 소환사를 찾는다. 없으면 null.</summary>
    public SummonerData GetSummoner(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var s in Summoners)
            if (s != null && s.Id == id) return s;

        return null;
    }

    // ── 조회 ─────────────────────────────────────────────────

    public MonsterSpeciesData GetMonster(string id)
    {
        BuildIfNeeded();
        _monsterById.TryGetValue(id ?? string.Empty, out var species);
        return species;
    }

    public SkillCardData GetSkill(string id)
    {
        BuildIfNeeded();
        _skillById.TryGetValue(id ?? string.Empty, out var card);
        return card;
    }

    /// <summary>카드 칸이 가리키는 표시 이름. 없는 ID 면 ID 를 그대로 돌려준다.</summary>
    public string DisplayNameOf(in SummonDeckSlot slot)
    {
        if (slot.IsEmpty) return string.Empty;

        if (slot.Kind == SummonKind.Skill)
        {
            SkillCardData card = GetSkill(slot.Id);
            return card != null ? card.DisplayName : slot.Id;
        }

        MonsterSpeciesData species = GetMonster(slot.Id);
        return species != null ? species.DisplayName : slot.Id;
    }

    /// <summary>
    /// 그 칸의 마나 비용. 카드 종류에 따라 출처가 다르므로 여기서 합쳐 준다.
    /// ⚠ 친화 할인은 포함하지 않는다 — 그건 소환사가 정하는 값이라
    ///   SummonerPerkRuntime.ManaCostFor 가 따로 얹는다.
    /// </summary>
    public float BaseManaCostOf(in SummonDeckSlot slot)
    {
        if (slot.IsEmpty) return 0f;

        if (slot.Kind == SummonKind.Skill)
        {
            SkillCardData card = GetSkill(slot.Id);
            return card != null ? card.ManaCost : 0f;
        }

        MonsterSpeciesData species = GetMonster(slot.Id);
        return species != null ? species.ManaCost : 0f;
    }

    void BuildIfNeeded()
    {
        if (_monsterById != null) return;

        _monsterById = new Dictionary<string, MonsterSpeciesData>(Monsters.Count);
        foreach (var m in Monsters)
        {
            if (m == null || string.IsNullOrEmpty(m.Id)) continue;
            _monsterById[m.Id] = m;
        }

        _skillById = new Dictionary<string, SkillCardData>(Skills.Count);
        foreach (var s in Skills)
        {
            if (s == null || string.IsNullOrEmpty(s.Id)) continue;
            _skillById[s.Id] = s;
        }
    }
}
