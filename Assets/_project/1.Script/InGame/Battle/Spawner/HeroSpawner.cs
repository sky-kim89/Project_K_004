using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

// ============================================================
//  HeroSpawner.cs
//  용사(Hero) 편성을 오른쪽 진영에 세운다.
//
//  ■ 원작 AllySpawner 를 좌우 반전한 것이다
//    원작은 왼쪽 SpawnPoints 에 장수를 슬롯 순서대로 배치했다.
//    여기서는 같은 일을 오른쪽에서 한다. 슬롯 인덱스 = 부대 번호다.
//
//  ■ EnemySpawner 와 무엇이 다른가 — 이게 이 클래스가 따로 있는 이유다
//    EnemySpawner 는 MonsterRuntimeBridge 로 초기화한다. 그건 원작의
//    "일반 적/엘리트/보스" 용이라 직업·등급·스킬·장비·휘하 병사가 없는
//    단순 스탯 유닛이다.
//
//    용사는 원작 장수 체계를 통째로 물려받는다 — 직업 4종, 등급 5단계,
//    액티브 33 / 패시브 40, 장비, 휘하 병사. 그래서 GeneralRuntimeBridge 로
//    초기화해야 한다. 그 경로가 EnemySpawner 에는 없다.
//
//  ■ 진영은 Initialize 보다 먼저 박는다
//    Initialize 안에서 엔티티가 만들어지며 Team 이 확정된다.
//    SetTeam 을 나중에 부르면 이미 늦다.
//    (GeneralRuntimeBridge 의 기본값도 Faction.Hero 라 빠뜨려도 적으로 서지만,
//     진영을 정하는 자리에서는 항상 명시한다 — Faction.cs 규칙)
//
//  ■ 1스테이지 = 1편성
//    1웨이브에만 세우고 이후 웨이브에서는 그대로 둔다 —
//    원작 아군이 그랬던 것과 같다.
//
//  ■ 첫 보스 전 스테이지는 장수 없이 병사만 세운다
//    편성 항목의 SoldiersOnly 가 켜져 있으면 SpawnSoldierSquad 로 빠진다.
//    스테이지 경계는 HeroDeployment.GeneralFromStage 가 쥐고 있다.
//
//  ■ 엘리트 스테이지에는 보스 히어로가 섞인다
//    편성 목록의 IsBossHero 항목을 HeroTierSetup 이 승격시킨다.
//    보스 UI·공격 패턴은 원작 것을 그대로 쓴다 (HeroTierSetup 참고).
// ============================================================

public class HeroSpawner : MonoBehaviour
{
    [Header("용사 부대 배치 위치 (오른쪽 진형)")]
    [Tooltip("위에서 아래로 1~5번 자리. 채우는 순서는 LaneOrder 가 정한다.\n" +
             "휘하 병사는 GeneralRuntimeBridge 가 장수 주변에 알아서 붙인다.")]
    public List<Transform> SquadPoints = new();

    /// <summary>
    /// 부대를 채우는 자리 순서 — **3 · 2 · 4 · 1 · 5** (위에서 센 번호).
    ///
    /// ■ 왜 순서대로 채우지 않는가
    ///   예전에는 목록 순서가 그대로 자리 번호였다. 그래서 2부대짜리 초반
    ///   스테이지에서는 용사가 늘 **맨 위 두 줄**에만 섰다. 아래 세 줄은
    ///   비어 있으니 플레이어도 위쪽에만 소환하면 됐고, 다섯 라인이 있다는
    ///   사실 자체가 한동안 게임에 등장하지 않았다.
    ///
    ///   가운데부터 바깥으로 채우면 부대가 둘이어도 전장 한복판에 서고,
    ///   늘어날 때마다 위아래로 균형 있게 벌어진다. 화면이 항상 가운데를
    ///   중심으로 읽히고, 라인 선택이 첫 스테이지부터 의미를 갖는다.
    ///
    /// ⚠ 목록 0번이 가운데다
    ///   허들 스테이지의 보스 히어로가 편성 0번이므로 자동으로 한복판에 선다
    ///   (HeroDeployment.Build 참고).
    /// </summary>
    static readonly int[] LaneOrder = { 2, 1, 3, 0, 4 };

    /// <summary>
    /// n번째로 세우는 부대가 들어갈 자리. 자리보다 부대가 많으면 순서대로 흘린다.
    ///
    /// ⚠ 전황 화면(BattleInfoPopup)도 이 함수를 쓴다 — 화면의 줄 순서와
    ///   전장의 줄 순서가 갈리면 "가운데가 위험하다" 같은 판단이 거짓이 된다.
    /// </summary>
    public static int LaneFor(int order)
        => order < LaneOrder.Length ? LaneOrder[order] : order;

    /// <summary>외부(BattleManager)에서 스폰 중 여부 확인용.</summary>
    public bool IsSpawning { get; private set; }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>편성을 슬롯 순서대로 세운다.</summary>
    public void Spawn(List<SpawnEntry> entries)
    {
        if (IsSpawning)
        {
            Debug.LogWarning("[HeroSpawner] 이미 스폰 중입니다.");
            return;
        }
        StartCoroutine(SpawnRoutine(entries));
    }

    /// <summary>
    /// 코루틴 없이 같은 프레임에 끝내는 즉시 스폰.
    /// 웨이브 시작과 동시에 편성이 서 있어야 할 때 쓴다.
    /// </summary>
    /// <summary>대기 없이 즉시 세운다. 자리 순서는 Spawn 과 같다.</summary>
    public void SpawnImmediate(List<SpawnEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            SpawnOne(entries[i], LaneFor(i));
    }

    // ── 내부 ─────────────────────────────────────────────────

    IEnumerator SpawnRoutine(List<SpawnEntry> entries)
    {
        IsSpawning = true;

        // ⚠ 편성이 몇으로 나왔는지 남긴다 (사용자 지적, 2026-09-07)
        //   "왜 이 판에 적이 이것밖에 안 나오나" 를 화면만 보고는 알 수 없다.
        //   부대 수(HeroDeployment.GetSquadCount)가 적은 것인지, 부대는 섰는데
        //   병사가 안 딸려 나온 것인지, 슬롯이 모자란 것인지가 갈리지 않는다.
        //   셋은 고치는 자리가 전부 다르므로 여기서 한 줄로 구분해 준다.
        LogDeployment(entries);

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].DelayBefore > 0f)
                yield return new WaitForSeconds(entries[i].DelayBefore);

            SpawnOne(entries[i], LaneFor(i));

            // 부대마다 한 프레임씩 나눠 스터터를 피한다.
            // 장수 1기가 휘하 병사까지 끌고 나오므로 한 슬롯이 꽤 무겁다.
            yield return null;
        }

        IsSpawning = false;
    }

    /// <summary>이 판의 편성을 한 줄로 남긴다 — 부대 수·병사만 여부·자리 부족을 한눈에.</summary>
    void LogDeployment(List<SpawnEntry> entries)
    {
        int soldiersOnly = 0;
        for (int i = 0; i < entries.Count; i++) if (entries[i].SoldiersOnly) soldiersOnly++;

        string over = entries.Count > SquadPoints.Count
            ? $"  ⚠ 자리 부족 (보유 {SquadPoints.Count})"
            : "";

        Debug.Log($"[HeroSpawner] 부대 {entries.Count}개 " +
                  $"(장수 {entries.Count - soldiersOnly} · 병사만 {soldiersOnly}){over}");
    }

    void SpawnOne(SpawnEntry entry, int slotIndex)
    {
        if (slotIndex >= SquadPoints.Count)
        {
            // ⚠ 에러다 — 이 부대는 통째로 안 나온다 (위 풀 실패와 같은 이유).
            Debug.LogError($"[HeroSpawner] SquadPoints 슬롯 부족 — " +
                           $"필요 {slotIndex + 1}, 보유 {SquadPoints.Count}. " +
                           "씬 셋업 > 인게임 전장 을 실행해 자리를 다시 만드세요.");
            return;
        }

        Transform slot = SquadPoints[slotIndex];

        // 첫 보스 전 스테이지 — 장수 없이 휘하 병사만 세운다 (HeroDeployment.GeneralFromStage).
        if (entry.SoldiersOnly)
        {
            SpawnSoldierSquad(entry, slot);
            return;
        }

        GameObject hero = PoolController.Instance.Spawn(
            PoolType.Unit, entry.PoolKey, slot.position, slot.rotation);

        if (hero == null)
        {
            // ⚠ 경고가 아니라 에러다 (2026-09-07) — 부대 하나가 통째로 사라진다.
            //   노란 경고는 로그에 묻혀 "적이 왜 이것밖에 안 나오지" 로만 보인다.
            Debug.LogError($"[HeroSpawner] 풀 스폰 실패: '{entry.PoolKey}' — " +
                           "이 부대는 통째로 나오지 않습니다. 풀이 모자라거나 " +
                           "그 키의 프리팹이 등록되지 않았습니다.");
            return;
        }

        if (!hero.TryGetComponent<GeneralRuntimeBridge>(out var bridge))
        {
            Debug.LogError($"[HeroSpawner] '{entry.PoolKey}' 프리팹에 " +
                           "GeneralRuntimeBridge 가 없습니다. 용사는 장수 체계를 씁니다.");
            return;
        }

        // ⚠ 순서 고정 — Initialize 안에서 Team 이 박힌다
        bridge.SetTeam(Faction.Hero);

        // unitEntry 는 null 이다. 그건 플레이어가 키운 장수의 세이브 기록이고,
        // 용사는 플레이어가 손댈 수 없는 적이다.
        // 직업·등급·패시브는 전부 이름 시드에서 결정적으로 나온다.
        // 편성이 실은 배율·몸집·병사 여부는 전부 Initialize 안에서 처리된다 —
        // 스탯이 다 굴러 나온 뒤, 엔티티가 만들어지기 전이라야 반경까지 맞는다.
        // 몸집 — 엘리트·보스는 계층만큼 크다 (HeroTierSetup.ScaleFor).
        //   무한 보스처럼 편성이 이미 더 큰 값을 실어 오면 그쪽을 쓴다 (곱하지 않는다).
        float scale = Mathf.Max(entry.ScaleMultiplier,
                                HeroTierSetup.ScaleFor(entry.IsBossHero, entry.IsEliteHero));

        bridge.Initialize(entry.Name, entry.Level, unitEntry: null,
                          statMult: entry.StatMultiplier,
                          scaleMult: scale,
                          loneHero: entry.LoneHero);

        // ⚠ 한 기씩 실제 상태를 남긴다 (사용자 지적, 2026-09-07)
        //   "편성은 장수 3인데 화면에는 병사 둘뿐" 이 나왔다. 편성 수만으로는
        //   ① 스폰이 실패했는지 ② 섰는데 안 보이는지(외형·꺼짐)
        //   ③ 섰는데 안 걸어오는지(이속 0) 가 갈리지 않는다.
        //   셋은 고치는 자리가 전부 다르므로 여기서 한 줄로 구분해 준다.
        Debug.Log($"[HeroSpawner] 장수 '{entry.Name}' Lv{entry.Level} " +
                  $"활성={hero.activeInHierarchy} 자리={slot.position.x:0.#} " +
                  $"체력={bridge.RolledStat.Get(StatType.MaxHp):0} " +
                  $"공격={bridge.RolledStat.Get(StatType.Attack):0.#} " +
                  $"이속={bridge.RolledStat.Get(StatType.MoveSpeed):0.##} " +
                  $"렌더={hero.GetComponentInChildren<SpriteRenderer>(true) != null}");

        // ── 보스·엘리트 승격 (허들 스테이지) ──
        //
        //  ⚠ Initialize 뒤여야 한다 — 그 안에서 엔티티와 스탯이 만들어진다.
        //    HeroTierSetup 은 그 결과 위에 컴포넌트를 얹고 공속을 깎는다.
        //  ⚠ 보스가 이긴다 — 둘 다 켜진 편성이 들어와도 한쪽만 붙는다.
        if (!entry.IsBossHero && !entry.IsEliteHero) return;

        if (!hero.TryGetComponent<EntityLink>(out var link) || link.Entity == Entity.Null)
        {
            Debug.LogError($"[HeroSpawner] '{entry.Name}' 를 승격시키지 못했습니다 — EntityLink 가 없습니다.");
            return;
        }

        if (entry.IsBossHero) HeroTierSetup.ApplyBoss (link.Entity, entry.Name);
        else                  HeroTierSetup.ApplyElite(link.Entity, entry.Name);

        // ── 넉백 완전 면역 (무한 보스) ──
        //
        //  ⚠ ApplyBoss 뒤다 — 그쪽이 주는 것은 내성 0.8 이라 여전히 밀린다.
        //    무한 보스는 아예 안 밀려야 한다. 태그가 있으면 UnitHitSystem 이
        //    넉백 계산을 통째로 건너뛴다.
        //  ⚠ 풀 재사용 — 켜는 자리가 있으면 끄는 자리도 있어야 한다.
        //    GeneralRuntimeBridge.Initialize 가 승격 컴포넌트와 함께 떼어 준다.
        if (entry.KnockbackImmune)
        {
            EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!em.HasComponent<BattleGame.Units.KnockbackImmuneTag>(link.Entity))
                em.AddComponent<BattleGame.Units.KnockbackImmuneTag>(link.Entity);
        }
    }

    // ── 병사만 있는 부대 ─────────────────────────────────────

    /// <summary>병사 간 Y 간격. GeneralRuntimeBridge 의 진형과 같은 값이다.</summary>
    const float SoldierColSpacing = 0.6f;

    /// <summary>행 간 X 간격(장수가 서 있었을 자리에서 오른쪽으로).</summary>
    const float SoldierRowSpacing = 0.7f;

    /// <summary>
    /// 장수 없이 그 부대의 병사만 세운다.
    ///
    /// ■ 장수를 세웠다가 지우지 않는다
    ///   그 방법이 더 짧아 보이지만, 장수를 한 번 세우면 생존 카운트·전투 통계·
    ///   보스 HP 바·HUD 카드가 전부 그 개체를 잡는다. 지우는 순간 카운트가
    ///   어긋나 스테이지가 영영 안 끝나는 쪽이 훨씬 비싸다.
    ///
    /// ■ 스탯은 '있었을 장수' 를 굴려서 낸다
    ///   병사 스탯은 장수 스탯의 비율이다(SoldierRuntimeBridge.StatRatio).
    ///   그래서 장수를 세우지 않더라도 **같은 이름 시드로 장수를 굴려** 그 값을 쓴다.
    ///   덕분에 스테이지가 오를수록 병사도 같은 곡선으로 강해지고,
    ///   5스테이지에서 장수가 등장해도 난이도가 끊기지 않는다.
    ///
    /// ⚠ 소속 장군 엔티티가 없다 (Entity.Null)
    ///   부대 단위 훅(처치 귀속·병사 사망 이벤트)은 전부 Exists 검사를 거치므로
    ///   조용히 건너뛴다. 그 훅들은 플레이어 장수용이고 용사 병사에게는 의미가 없다.
    /// </summary>
    void SpawnSoldierSquad(SpawnEntry entry, Transform slot)
    {
        UnitGrade grade = UnitJobRoller.GetBirthGrade(entry.Name);
        UnitJob   job   = UnitJobRoller.GetJob(entry.Name);
        UnitStat  stat  = GeneralStatRoller.Roll(entry.Name, entry.Level, grade);

        // ⚠ 편성 배율을 여기서도 걸어야 한다 (2026-09-13)
        //   장수가 서는 경로는 GeneralRuntimeBridge.Initialize(statMult) 가 받아
        //   주지만, **병사만 오는 부대는 이 함수가 끝이다.** 빠뜨리면 엘리트 판
        //   (HeroDeployment.EliteStageStatMult)에서 우두머리 한 기만 세지고 옆
        //   부대는 일반 판과 똑같아진다 — 에러 없이 판의 절반만 반영된다.
        //   첫 엘리트(3스테이지)는 나머지 부대가 전부 이 경로라 특히 그렇다.
        //   ⚠ 체력·공격력만이다 — 병사 수·지휘력에 곱하면 물량까지 늘어난다
        //     (Initialize 의 statMult 와 같은 규칙).
        if (!Mathf.Approximately(entry.StatMultiplier, 1f))
        {
            stat.Set(StatType.MaxHp,  stat.Get(StatType.MaxHp)  * entry.StatMultiplier);
            stat.Set(StatType.Attack, stat.Get(StatType.Attack) * entry.StatMultiplier);
        }

        // 장수 전용 성장분은 병사 환산에서 뺀다 — GeneralRuntimeBridge 와 같은 규칙이다.
        UnitStat soldierSource = stat.CloneWithoutGeneralOnly();

        int count = Mathf.RoundToInt(stat.Get(StatType.SoldierCount));
        if (count <= 0)
        {
            Debug.LogWarning($"[HeroSpawner] '{entry.Name}' 의 병사 수가 0 입니다 — 세울 것이 없습니다.");
            return;
        }

        float ratio = SoldierRuntimeBridge.StatRatio(stat.Get(StatType.CommandPower));

        // 격자 열 수 — sqrt(N). GeneralRuntimeBridge.SpawnSoldiers 와 같은 산출이다.
        int colsY = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(count)));

        for (int i = 0; i < count; i++)
        {
            int row     = i / colsY;
            int col     = i % colsY;
            int inRow   = Mathf.Min(colsY, count - row * colsY);

            Vector3 at = new Vector3(
                slot.position.x + row * SoldierRowSpacing,
                slot.position.y + (col - (inRow - 1) * 0.5f) * SoldierColSpacing,
                slot.position.z);

            GameObject go = PoolController.Instance.Spawn(
                PoolType.Unit, SpawnUnitType.Soldier.ToString(), at, Quaternion.identity);

            if (go == null)
            {
                Debug.LogWarning("[HeroSpawner] 병사 풀 스폰 실패: 'Soldier'");
                continue;
            }

            if (!go.TryGetComponent<SoldierRuntimeBridge>(out var soldier))
            {
                Debug.LogError("[HeroSpawner] 'Soldier' 프리팹에 SoldierRuntimeBridge 가 없습니다.");
                return;
            }

            // ⚠ 순서 고정 — Initialize 안에서 Team 이 박힌다
            soldier.SetTeam(Faction.Hero);
            soldier.Initialize(entry.Name, soldierSource, ratio, Entity.Null,
                               job, entry.Name, grade);

            BattleManager.Instance?.OnUnitSpawned(Faction.Hero);
        }
    }

    // ── 에디터 기즈모 (배치 확인용) ──────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.9f);
        for (int i = 0; i < SquadPoints.Count; i++)
        {
            if (SquadPoints[i] == null) continue;

            Gizmos.DrawWireSphere(SquadPoints[i].position, 0.4f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(SquadPoints[i].position + Vector3.up * 0.6f,
                                      $"용사 부대 {i + 1}");
#endif
        }
    }
}
