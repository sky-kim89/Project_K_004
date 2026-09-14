using System;
using UnityEngine;

// ============================================================
//  StageProgressData.cs
//  이번 여정에서 깬 가장 깊은 스테이지 — 환생 포인트의 근거다.
//
//  기록은 RunBootstrap.AdvanceStage 한 곳이 한다.
//  ⚠ 환생 때 지워진다 (UserDataManager.Reincarnate). 환생을 넘어 남아야 하는
//    최고 기록은 ReincarnationData 가 따로 든다.
// ============================================================

public class StageProgressData : ISaveSection
{
    public SaveKey SaveKey => SaveKey.StageProgress;

    public int ClearedNormalStages => _raw.ClearedNormal;

    public void RecordClear(int stageNumber)
        => _raw.ClearedNormal = Mathf.Max(_raw.ClearedNormal, stageNumber);

    // ── ISaveSection ─────────────────────────────────────────

    RawData _raw = new();

    public string Serialize()              => JsonUtility.ToJson(_raw);
    public void   Deserialize(string json) => _raw = JsonUtility.FromJson<RawData>(json) ?? new RawData();
    public void   SetDefaults()            => _raw = new RawData();

    [Serializable]
    class RawData
    {
        public int ClearedNormal = 0;
    }
}
