using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MonsterGearRewardRule.cs
//  런 종료 보상 상자 — 무엇이 나오는지의 정본.
//
//  ■ 등급은 **도달 스테이지**가 정한다 (확정) · 무엇이 나올지는 무작위다
//    "랜덤한 보상 상자" 와 "스테이지에 따라 등급이 달라진다" 를 둘 다 만족시키는
//    유일한 갈래다. 등급까지 무작위로 두면 깊이 들어간 런이 첫 판보다 못한
//    보상을 주는 일이 생겨, 더 가야 할 이유가 사라진다.
//
//    ⚠ 확률로 한 단계 튀게 하지 말 것
//      도감의 품질 개선이 "확정이다, 확률이 아니다" 로 정해져 있다
//      (MonsterGradeUpgradeRule). 같은 화면에서 얻은 장비만 도박이면 결이 어긋난다.
//
//  ■ 몸 형태는 **내가 가진 종족 구성**을 따른다
//    50:50 으로 뽑으면, 인간형만 해금한 사람이 비인간형 장비만 계속 받는 일이
//    생긴다. 도감에서 해금한 종족의 인간형/비인간형 비율을 그대로 쓰면
//    "쓸 수 없는 보상" 이 구조적으로 줄어든다.
//    ⚠ 해금이 하나도 없으면(첫 실행) 인간형으로 둔다 — 시작 덱이 인간형이다.
//
//  ■ 상자는 '이미 받은 것을 여는 연출' 이다 (GearBoxPopup)
//    무엇이 나올지는 이 함수가 정해서 **즉시 인벤토리에 넣는다.** 팝업은 그것을
//    흔들어 보여 줄 뿐이다.
//    ⚠ '안 연 상자' 를 세이브에 두지 않는다
//      연출 도중에 앱이 꺼지면 보상이 사라지는 길이 생기고, 그걸 막으려면
//      상자 상태 기계를 하나 더 들여야 한다. 얻는 순간 확정하면 둘 다 없어진다.
// ============================================================

public static class MonsterGearRewardRule
{
    /// <summary>
    /// 이 스테이지까지 갔으면 이 등급.
    ///
    /// ⚠ 숫자를 화면에 적지 말 것 — 표시·판정이 이 함수 하나를 본다.
    /// </summary>
    public static UnitGrade GradeForStage(int stage)
    {
        if (stage >= 20) return UnitGrade.Epic;
        if (stage >= 15) return UnitGrade.Unique;
        if (stage >= 10) return UnitGrade.Rare;
        if (stage >=  5) return UnitGrade.Uncommon;
        return UnitGrade.Normal;
    }

    static readonly List<MonsterGearData> Buffer = new(16);

    /// <summary>아직 갖지 않은 후보만 담는 버퍼. Buffer 와 같은 이유로 재사용한다.</summary>
    static readonly List<MonsterGearData> Fresh = new(16);

    /// <summary>
    /// 이번 런의 보상을 뽑아 <b>인벤토리에 넣고</b> 무엇이었는지 돌려준다.
    /// 뽑을 것이 없으면 null (그 등급·몸 형태의 장비가 DB 에 없다).
    /// </summary>
    public static MonsterGearData GrantForStage(int stage)
    {
        var db  = MonsterGearDatabase.Current;
        var inv = UserDataManager.Instance?.Get<MonsterGearInventory>();

        if (db == null || inv == null)
        {
            Debug.LogWarning("[MonsterGearRewardRule] 장비 DB 또는 세이브가 없어 보상을 건너뜁니다. " +
                             "Tools > Project K > 데이터 생성 > 몬스터 장비 를 먼저 실행하세요.");
            return null;
        }

        UnitGrade       grade = GradeForStage(stage);
        MonsterGearBody body  = RollBody();

        MonsterGearData picked = Pick(db, inv, grade, body) ?? Pick(db, inv, grade, Other(body));

        if (picked == null)
        {
            Debug.LogWarning($"[MonsterGearRewardRule] {GradeStyle.GetLabel(grade)} 등급 장비가 " +
                             "DB 에 하나도 없습니다 — 보상을 건너뜁니다.");
            return null;
        }

        inv.Add(picked.Id);
        _lastGiven = picked.Id;
        return picked;
    }

    /// <summary>
    /// 직전에 준 것. <b>같은 것을 연달아 주지 않기 위한 기억</b>이다.
    ///
    /// ⚠ 세이브에 넣지 않는다
    ///   앱을 껐다 켜면 잊는다 — 그래도 상관없다. 이 값이 막는 것은 "방금 받은
    ///   것을 또 받는" 한 번뿐이고, 그건 한 자리에 앉아 연달아 열 때 생긴다.
    ///   세이브 필드를 하나 늘려 지킬 만큼의 값은 아니다.
    /// </summary>
    static string _lastGiven;

    static MonsterGearBody Other(MonsterGearBody body)
        => body == MonsterGearBody.Humanoid ? MonsterGearBody.NonHumanoid : MonsterGearBody.Humanoid;

    /// <summary>
    /// 그 등급·몸 형태에서 하나 고른다. 없으면 null.
    ///
    /// ■ ⚠ 고르는 순서가 규칙이다 (사용자 지적, 2026-09-09)
    ///     ① <b>아직 없는 것</b>이 있으면 그중에서 — 도감이 채워지는 것이 보인다
    ///     ② 전부 갖고 있으면 아무거나, 단 <b>직전에 준 것은 뺀다</b>
    ///   한때 그냥 균등 무작위였다. 그래서 "잿빛 가죽" 이 세 번 연속 나왔다 —
    ///   후보가 하나뿐인 칸이 있었던 것이 근본 원인이지만(MonsterGearCreator
    ///   VerifyPoolSize 가 이제 센다), 후보가 셋이어도 균등이면 연속은 흔하다.
    ///
    ///   ⚠ 등급은 여전히 <b>스테이지가 확정</b>한다 — 여기서 굴리는 것은
    ///     "그 등급 안에서 무엇" 뿐이다 (파일 머리 주석).
    ///   ⚠ 후보가 하나뿐이면 그대로 그것을 준다. 안 주는 것보다 낫다.
    /// </summary>
    static MonsterGearData Pick(MonsterGearDatabase db, MonsterGearInventory inv,
                                UnitGrade grade, MonsterGearBody body)
    {
        Buffer.Clear();
        db.Collect(grade, body, Buffer);

        if (Buffer.Count == 0) return null;

        // ① 아직 없는 것 우선
        Fresh.Clear();
        for (int i = 0; i < Buffer.Count; i++)
            if (inv.OwnedCount(Buffer[i].Id) == 0) Fresh.Add(Buffer[i]);

        if (Fresh.Count > 0) return Fresh[Random.Range(0, Fresh.Count)];

        // ② 전부 갖고 있다 — 직전 것만 뺀다
        if (Buffer.Count > 1 && !string.IsNullOrEmpty(_lastGiven))
            Buffer.RemoveAll(g => g.Id == _lastGiven);

        return Buffer[Random.Range(0, Buffer.Count)];
    }

    /// <summary>
    /// 해금한 종족의 인간형/비인간형 비율대로 굴린다.
    /// 해금이 없으면 인간형 — 시작 덱이 인간형이다.
    /// </summary>
    static MonsterGearBody RollBody()
    {
        var cards = CardCatalog.Current;
        var codex = UserDataManager.Instance?.Get<MonsterCodexData>();

        if (cards == null || codex == null) return MonsterGearBody.Humanoid;

        int humanoid = 0, total = 0;

        foreach (var s in cards.Monsters)
        {
            if (s == null || !codex.IsUnlocked(s.Id)) continue;

            total++;
            if (s.BodyType == MonsterBodyType.Humanoid) humanoid++;
        }

        if (total == 0) return MonsterGearBody.Humanoid;

        return Random.Range(0, total) < humanoid
             ? MonsterGearBody.Humanoid
             : MonsterGearBody.NonHumanoid;
    }
}
