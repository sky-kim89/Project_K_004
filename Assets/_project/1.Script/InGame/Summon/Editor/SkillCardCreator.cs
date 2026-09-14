using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
//  SkillCardCreator.cs  [Editor Only]
//  스킬 카드(SkillCardData) 를 한 번에 굽는다.
//
//  ■ 새 실행 코드가 없다
//    스킬 카드는 기존 액티브 스킬 33종을 **소환사가 시전자**가 되어
//    그대로 부르는 껍데기다 (SkillCardCaster). 그래서 카드를 늘리는 데
//    필요한 건 이 표에 한 줄 추가하는 것뿐이다.
//
//  ■ 어떤 스킬이 카드가 되는가 — "좌표에 터지는 것"
//    카드는 플레이어가 라인을 탭해 쓴다. 즉 조준이 **위치**다.
//    메테오·독성 지대처럼 위치에 터지는 스킬이 가장 잘 맞는다.
//    ⚠ 병사를 전제로 하는 스킬은 넣지 말 것
//      SummonSkeleton·SacrificeSoldier·SuicideSoldier 는 "죽은 병사" 나
//      "휘하 병사" 를 재료로 쓴다. 소환사에게는 병사가 없어 아무 일도 안 일어난다.
//    ⚠ 보스 패턴(31~33)도 넣지 말 것 — 적 전용이다.
//
//  ■ 비용은 몬스터보다 무겁다
//    몬스터는 살아남으면 마나를 환수하지만 스킬은 쓴 만큼 그대로 사라진다.
//    같은 값이면 몬스터가 언제나 이득이라 카드로서 성립하지 않는다.
//
//  사용: Tools > Project K > 데이터 생성 > 스킬 카드
// ============================================================

public static class SkillCardCreator
{
    const string OutputRoot = "Assets/_project/Data/SkillCards";

    struct Spec
    {
        public string        Id;
        public string        Name;
        public ActiveSkillId Skill;
        public float         Cost;
        public float         PowerPerLevel;
        public string        Desc;
    }

    // ──────────────────────────────────────────────────────────
    // ■ 카드 목록 — 여기가 정본이다
    // ──────────────────────────────────────────────────────────
    static readonly Spec[] Roster =
    {
        new Spec
        {
            Id = "card_meteor", Name = "메테오", Skill = ActiveSkillId.Meteor,
            Cost = 22f, PowerPerLevel = 0.25f,
            Desc = "탭한 자리에 운석이 떨어진다. 넓게 터지고 밀어낸다.",
        },
        new Spec
        {
            Id = "card_poison_zone", Name = "독성 지대", Skill = ActiveSkillId.PoisonZone,
            Cost = 14f, PowerPerLevel = 0.2f,
            Desc = "그 자리에 독 장판을 깐다. 지나가는 용사가 느려지고 계속 깎인다.",
        },
        new Spec
        {
            Id = "card_blizzard", Name = "블리자드", Skill = ActiveSkillId.Blizzard,
            Cost = 18f, PowerPerLevel = 0.2f,
            Desc = "얼어붙는 폭풍. 공격속도와 이동속도를 함께 떨어뜨린다.",
        },
        new Spec
        {
            Id = "card_arrow_rain", Name = "화살 비", Skill = ActiveSkillId.ArrowRain,
            Cost = 13f, PowerPerLevel = 0.22f,
            Desc = "넓은 범위에 화살이 쏟아진다. 뭉쳐 오는 용사에게 강하다.",
        },
        new Spec
        {
            Id = "card_arrow_storm", Name = "화살 폭풍", Skill = ActiveSkillId.ArrowStorm,
            Cost = 24f, PowerPerLevel = 0.25f,
            Desc = "3연타 광역 낙하. 한 번에 판을 정리할 때 쓴다.",
        },
        new Spec
        {
            Id = "card_chain_lightning", Name = "연쇄 번개", Skill = ActiveSkillId.ChainLightning,
            Cost = 16f, PowerPerLevel = 0.3f,
            Desc = "적 사이를 튀며 피해가 누적된다. 여럿이 붙어 있을수록 세다.",
        },
        new Spec
        {
            Id = "card_gravity", Name = "중력 붕괴", Skill = ActiveSkillId.GravityCollapse,
            Cost = 26f, PowerPerLevel = 0.25f,
            Desc = "빨아들여 묶어 두었다가 터뜨린다. 돌파를 끊는 카드.",
        },
        new Spec
        {
            Id = "card_bind", Name = "속박", Skill = ActiveSkillId.Bind,
            Cost = 10f, PowerPerLevel = 0.15f,
            Desc = "하나를 완전히 묶는다. 보스 용사의 한 방을 미룰 때.",
        },
        new Spec
        {
            Id = "card_shockwave", Name = "충격파", Skill = ActiveSkillId.Shockwave,
            Cost = 12f, PowerPerLevel = 0.2f,
            Desc = "전방을 부채꼴로 밀어낸다. 성벽에 붙은 용사를 떼어 낸다.",
        },
        new Spec
        {
            Id = "card_gravestone", Name = "비석 강림", Skill = ActiveSkillId.Gravestone,
            Cost = 20f, PowerPerLevel = 0.2f,
            Desc = "비석이 떨어져 피해를 주고 그 자리에서 스켈레톤이 일어난다.",
        },
        new Spec
        {
            Id = "card_war_banner", Name = "군기 강림", Skill = ActiveSkillId.WarBanner,
            Cost = 15f, PowerPerLevel = 0.2f,
            Desc = "깃발을 꽂아 주변 몬스터의 공격력·공속·이속을 올린다.",
        },
        new Spec
        {
            Id = "card_battle_cry", Name = "전투 함성", Skill = ActiveSkillId.BattleCry,
            Cost = 11f, PowerPerLevel = 0.2f,
            Desc = "주변 몬스터의 공격력을 한동안 끌어올린다. 물량과 잘 맞는다.",
        },
    };

    [MenuItem(ProjectKMenu.Data + "스킬 카드", priority = ProjectKMenu.DataPrio + 24)]
    public static void CreateAll()
    {
        Directory.CreateDirectory(OutputRoot);

        var made = new List<SkillCardData>(Roster.Length);
        foreach (Spec spec in Roster)
            made.Add(Create(spec));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SkillCardCreator] 완료 — {made.Count}장. 경로: {OutputRoot}\n" +
                  "⚠ 이어서 '데이터 생성 > 카드 목록' 을 실행해야 보상에 나옵니다.");
    }

    static SkillCardData Create(Spec spec)
    {
        string path = $"{OutputRoot}/SkillCard_{spec.Id}.asset";

        // 이미 있으면 내용만 갈아끼운다 — 지웠다 만들면 GUID 가 바뀌어
        // 소환사의 시작 카드 참조가 끊어진다.
        var so = AssetDatabase.LoadAssetAtPath<SkillCardData>(path);
        bool isNew = so == null;
        if (isNew) so = ScriptableObject.CreateInstance<SkillCardData>();

        so.Id            = spec.Id;
        so.DisplayName   = spec.Name;
        so.Description   = spec.Desc;
        so.Skill         = spec.Skill;
        so.ManaCost      = spec.Cost;
        so.PowerPerLevel = spec.PowerPerLevel;

        // 아이콘은 기존 스킬 아이콘을 그대로 빌린다 — 카드용 그림을 따로 만들지 않는다.
        so.Icon = LoadSkillIcon(spec.Skill);

        if (isNew) AssetDatabase.CreateAsset(so, path);
        else       EditorUtility.SetDirty(so);

        return so;
    }

    /// <summary>
    /// 스킬 아이콘을 찾는다. ActiveSkillId.IconKey() 가 파일명을 정한다.
    /// 없으면 null 로 두고 카드 UI 의 대체 그림이 대신 나온다.
    /// </summary>
    static Sprite LoadSkillIcon(ActiveSkillId id)
    {
        string key = id.IconKey();
        if (string.IsNullOrEmpty(key)) return null;

        string[] guids = AssetDatabase.FindAssets($"{key} t:Sprite");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }

        return null;
    }
}
