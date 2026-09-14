// ============================================================
//  ISaveSection.cs
//  저장 가능한 데이터 섹션 인터페이스.
//
//  UserDataManager 에 등록된 모든 섹션은 이 인터페이스를 구현한다.
//  SaveAll() 호출 시 등록된 섹션들이 순서대로 직렬화·저장된다.
//
//  새 데이터 섹션 추가 방법:
//  1. ISaveSection 을 구현하는 클래스 생성
//  2. UserDataManager.RegisterSection() 으로 등록
// ============================================================

// ── 저장 키 ──────────────────────────────────────────────────
// 새 섹션 추가 시 여기에 값을 추가한다.
public enum SaveKey
{
    UserData = 0,
    ItemData        = 2,
    StageProgress   = 3,
    // ⚠ 비워 둔 번호 — 재사용하지 말 것. 옛 세이브에 남아 있어 다른 섹션으로 되살아난다.
    //   1 = UnitData · 4 = EquipInventory · 5 = DeploymentData · 6 = RunAbility · 7 = RelicInventory · 9 = RunTrait · 10 = RunShop · 11 = RunEventBonus
    //   (원작 장수 · 장비 가방 · 장수 배치 · 어빌리티 · 구 유물 · 원작 특성 · 런 상점 · 이벤트 보너스 — 시스템째 제거됐다)
    Reincarnation   = 8,
    Codex           = 12,  // 도감 — 만나 본 런 특성·장비 (영구)
    BattleSettings  = 13,  // 전투 조작 설정 — 자동 스킬 / 배속 (영구, 환생 무관)
    Difficulty      = 14,  // 난이도 선택·해금 기록 (영구, 환생 무관)
    Tutorial        = 15,  // 튜토리얼 노출 기록 (영구, 환생 무관)
    RelicTree       = 16,  // 유물 테크트리 노드 레벨 (영구, 환생 무관)

    // ── 소환 반전판(v3) 신규 섹션 ─────────────────────────────
    SummonMana      = 17,  // 소환 마나 — 런 스코프 (환생 시 초기화)
    MonsterCodex    = 18,  // 몬스터 도감 — 해금 + 종족별 영구 품질 (영구, 환생 무관)
    SummonDeck      = 19,  // 손에 든 카드 — 런 스코프 (환생 시 초기화)
    RunPerk         = 20,  // 런 중 얻은 특성 — 런 스코프 (환생 시 초기화)
    SummonRun       = 21,  // 진행 중인 런의 소환사·스테이지 (이어하기용, 환생 시 초기화)
    SummonQueue     = 22,  // 라인별 소환 대기열 — 런 스코프 (환생 시 초기화)
    RunGold         = 23,  // 런 골드 — 용사 처치로 벌고 갈림길에서 쓴다 (환생 시 초기화)
                           // ⚠ ItemData 의 eItem.Gold(영구 재화)와 다른 축이다
    RunCore         = 24,  // 마왕성 체력 — 성벽을 통과한 용사 1기당 1 감소 (환생 시 초기화)
                           // ⚠ 소환사의 HP 스탯과 다른 축이다
    RunBoon         = 25,  // 런 중 얹힌 영구 보너스 스탯 블록 (시너지 카운트·최대 마나)

    MonsterGear     = 26,  // 몬스터 장비 — 보유 수 + 종족별 장착 (영구, 환생 무관)
                           // ⚠ 런 종료 보상 상자로만 들어온다. 환생해도 남는다
}

public interface ISaveSection
{
    /// <summary>이 섹션의 저장 키.</summary>
    SaveKey SaveKey { get; }

    /// <summary>데이터를 JSON 문자열로 직렬화해 반환.</summary>
    string Serialize();

    /// <summary>JSON 문자열로부터 데이터를 복원.</summary>
    void Deserialize(string json);

    /// <summary>데이터가 없을 때 기본값으로 초기화.</summary>
    void SetDefaults();
}
