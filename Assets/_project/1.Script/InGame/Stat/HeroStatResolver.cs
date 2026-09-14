using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  HeroStatResolver.cs
//  화면이 읽는 창구. 계산은 하지 않는다 — HeroStatPipeline 이 만든 결과를
//  UI 가 쓰기 좋은 모양으로 감싸 줄 뿐이다.
//
//  ⚠ 여기에 계산을 다시 적지 말 것
//    예전엔 이 파일이 전투(GeneralRuntimeBridge)와 같은 순서를 손으로 한 번 더
//    적고 있었다. 자료구조까지 달라서(딕셔너리 8통 vs UnitStat 레이어) 한쪽에
//    항목을 추가하면 다른 쪽이 조용히 빠졌고, 같은 버그가 반복해서 났다.
//    스탯을 조립하는 코드는 HeroStatPipeline 하나뿐이어야 한다.
//
//  사용:
//    HeroStatResult r = HeroStatResolver.Resolve(entry);
//    float hp = r.Total(StatType.MaxHp);
//    // 스탯 클릭 분해: r.Base / r.GetPassive()
// ============================================================

/// <summary>
/// 파이프라인 결과를 출처별로 꺼내 보는 얇은 창구.
///
/// 값을 따로 들고 있지 않는다 — 전부 UnitStat 레이어에서 즉석으로 읽는다.
/// 그래서 "표시에는 있는데 전투에는 없는 보너스" 가 원천적으로 불가능하다.
/// </summary>
public class HeroStatResult
{
    /// <summary>전투에 그대로 들어가는 장수 최종 스탯.</summary>
    public UnitStat Stat { get; }

    /// <summary>성장만의 값 (등급·레벨 롤). 분해 표시의 '기본' 칸.</summary>
    public UnitStat Base { get; }

    /// <summary>장수 전용을 걷어낸 사본 — 용병 탭이 이걸 환산해 보여 준다.</summary>
    public UnitStat SoldierSource { get; }

    readonly HeroStatBuild _build;

    public HeroStatResult(HeroStatBuild build)
    {
        _build        = build;
        Stat          = build.Stat;
        Base          = build.BaseRoll;
        SoldierSource = build.SoldierSource;
    }

    // ── 총합 ─────────────────────────────────────────────────
    //  쿨감의 곱연산 결합까지 UnitStat 이 처리한다 (CombineMode.MultiplyResidual).

    public float Total(StatType stat)          => Stat.Get(stat);

    /// <summary>장수 전용을 뺀 값 — 병사가 물려받는 몫.</summary>
    public float TotalForSoldier(StatType stat) => SoldierSource.Get(stat);

    // ── 출처별 분해 ──────────────────────────────────────────
    public float GetPassive(StatType stat) => Stat.GetLayer(PassiveSkillApplier.GeneralLayerKey, stat);

    // ── 병사 전용 패시브 ─────────────────────────────────────
    //  환산된 병사 스탯에 Ratios 를 곱하고 Flats 를 더한다 (SoldierStatApplier 와 같은 규칙).

    public float GetSoldierPassiveRatio(StatType s)
        => _build.SoldierPassiveRatios.TryGetValue(s, out var v) ? v : 0f;

    public float GetSoldierPassiveFlat(StatType s)
        => _build.SoldierPassiveFlats.TryGetValue(s, out var v) ? v : 0f;

    /// <summary>1 - Π(1 - v). 출처가 하나면 그 값 그대로.</summary>
    public static float CombineResidual(params float[] values)
    {
        float remain = 1f;
        foreach (float v in values) remain *= (1f - v);
        return 1f - remain;
    }
}

public static class HeroStatResolver
{
    /// <summary>
    /// 기본 스탯 = 등급·레벨 롤.
    ///
    /// ⚠ 전투(GeneralRuntimeBridge.Initialize)도 반드시 이 함수를 쓴다.
    ///   한쪽에만 항목을 더하면 로비 표시와 실제 소환 수가 어긋난다.
    /// </summary>
    public static UnitStat RollBase(UnitEntry entry)
    {
        return GeneralStatRoller.Roll(entry.UnitName, entry.Level, entry.Grade);
    }

    /// <summary>
    /// 화면에 보여 줄 스탯 — 전투 시작 패시브 예상치까지 얹은 값이다.
    /// 계산은 HeroStatPipeline 이 한다.
    /// </summary>
    public static HeroStatResult Resolve(UnitEntry entry)
        => new HeroStatResult(HeroStatPipeline.Build(entry, previewBattleStart: true));
}
