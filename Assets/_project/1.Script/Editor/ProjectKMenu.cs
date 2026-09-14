// ============================================================
//  ProjectKMenu.cs  [Editor Only]
//  프로젝트 전체 에디터 메뉴 경로·정렬 우선순위의 단일 정의처.
//
// ============================================================
//  ⚠⚠ 메뉴 루트 규칙 — 이 프로젝트의 확정 규칙이다 ⚠⚠
// ============================================================
//
//  루트는 둘이다. 어디에 다는지가 곧 "이 도구를 믿어도 되는가" 의 표시다.
//
//    Tools/Project K/   ← 이 프로젝트에 실제로 쓰는 도구만
//    Tools/올드Tools/    ← 원작(Project_K_001)에서 그대로 넘어온 도구
//
//  ■ 왜 나누나
//    이 프로젝트는 원작 코드베이스를 통째로 복사해 시작했다. 그래서 에디터 도구
//    수십 개가 처음부터 메뉴에 꽂혀 있는데, 그중 상당수는 이 게임에 맞지 않는다.
//    로비를 굽는 도구, 장수 배치 팝업을 만드는 도구처럼 진영이 뒤집히며 의미가
//    달라진 것들이 섞여 있다.
//
//    전부 한 루트에 두면 "이거 눌러도 되나?" 를 매번 코드를 열어 확인해야 한다.
//    분리해 두면 메뉴 위치만 보고 안다.
//
//  ■ 규칙
//    1. 원작에서 넘어온 도구는 전부 Legacy* 상수를 쓴다 (= 올드Tools 아래).
//       기본값이 이쪽이다. 판단이 서지 않으면 올드Tools 다.
//
//    2. Tools/Project K/ 로 올릴 수 있는 것은 둘뿐이다.
//         · 이 프로젝트를 위해 **새로 만든** 도구
//         · 원작 도구를 이 프로젝트에 맞게 **검증·수정해 이관한** 것
//
//    3. 이관은 "잘 도는 것 같다" 로 하지 않는다.
//       실제로 돌려서 산출물이 이 게임에 맞는지 확인한 뒤에 옮긴다.
//       옮길 때 Legacy* → 일반 상수로 바꾸고, 원작 위치에는 남기지 않는다
//       (같은 도구가 두 루트에 동시에 보이면 분리한 의미가 없다).
//
//    4. 올드Tools 에 있는 도구를 고칠 일이 생기면, 고치는 김에 검증해서
//       이관하는 쪽이 낫다. 올드Tools 는 "언젠가 정리할 것" 의 대기열이다.
//
// ============================================================
//
//  ⚠ 기존 규칙 (그대로 유효)
//    1. 루트를 새로 만들지 말 것. 위의 둘뿐이다.
//    2. 모든 [MenuItem] 은 이 파일의 상수를 조합해서 쓴다.
//       문자열을 직접 적으면 다시 어긋나므로 금지.
//         [MenuItem(ProjectKMenu.Popup + "BattleResult",
//                   priority = ProjectKMenu.PrefabPrio + 11)]
//    3. 그룹 첫 항목은 "▶ 전체 생성", priority 는 그룹 기준값 그대로.
//       개별 항목은 기준값 + 11 부터 시작한다.
//       (Unity 는 priority 차이가 11 이상일 때 구분선을 넣는다.)
//
//  메뉴 구조 (두 루트가 같은 하위 그룹을 갖는다)
//    Tools/Project K/  ·  Tools/올드Tools/
//      ├─ 씬 이동/          씬 로드 단축키
//      ├─ 씬 셋업/          씬 계층 구성 (프리팹은 만들지 않음)
//      ├─ 프리팹 생성/      로비 · 팝업 · 인게임 · 이펙트
//      ├─ 데이터 생성/      ScriptableObject · Database
//      ├─ 아이콘·텍스처/    PNG · 머티리얼 · 일러스트
//      └─ 도구/             에디터 윈도우 · 링커
//
//  ■ 2026-09-07 이관 — "쓰는데 올드에 있던" 13개를 옮겼다
//    판단 기준은 **이 게임 런타임이 산출물을 읽는가** 하나다.
//      씬 이동 3       씬 로드 단축키
//      직업·스킬       skill_* ← ActiveSkillData.IconKey (몬스터 고유 스킬 · 시그니처)
//      패시브          passive_* ← 데이터 생성 > 패시브 스킬(이미 이관됨)이 읽는다
//      스테이지 노드   stage_* ← StageNodeUI
//      아이템          item_*  ← eItem.IconKey (품질 개선 화면의 보유 골드)
//      난이도          difficulty_*/debuff_* ← 특성 아틀라스에 얹혀 있다
//      이펙트 3        텍스처·머티리얼 · Effect 프리팹 · 희귀/보스 이펙트
//      스킬 SO 이펙트 키 연결 · 사운드 임포트 · 아이콘 임포트
//      유물 트리       이 게임의 시스템이다 — 애초에 올드에 있을 이유가 없었다
//                      ⚠ 메뉴 다섯을 둘로 갈랐다 — 그림(아이콘·텍스처) / 표 점검(데이터 생성)
// ============================================================

public static class ProjectKMenu
{
    // ──────────────────────────────────────────────────────────
    // ■ 이 프로젝트 도구 — 신규 또는 검증·이관 완료
    // ──────────────────────────────────────────────────────────

    public const string Root = "Tools/Project K/";

    public const string Scene  = Root + "씬 이동/";
    public const string Setup  = Root + "씬 셋업/";
    public const string Prefab = Root + "프리팹 생성/";
    public const string UI     = Root + "UI/";
    public const string Data   = Root + "데이터 생성/";
    public const string Icon   = Root + "아이콘·텍스처/";
    public const string Tool   = Root + "도구/";

    // 프리팹 생성 하위 그룹
    public const string Lobby  = Prefab + "로비/";
    public const string Popup  = Prefab + "팝업/";
    public const string InGame = Prefab + "인게임/";
    public const string Fx     = Prefab + "이펙트/";

    // ──────────────────────────────────────────────────────────
    // ■ 올드 도구 — 원작에서 그대로 넘어왔고 아직 검증되지 않았다
    //
    //   여기 있다는 건 "이 게임 기준으로 확인한 적 없다" 는 뜻이다.
    //   돌리기 전에 무엇을 만드는 도구인지 코드를 확인할 것.
    // ──────────────────────────────────────────────────────────

    public const string LegacyRoot = "Tools/올드Tools/";

    public const string LegacyScene  = LegacyRoot + "씬 이동/";
    public const string LegacySetup  = LegacyRoot + "씬 셋업/";
    public const string LegacyPrefab = LegacyRoot + "프리팹 생성/";
    public const string LegacyData   = LegacyRoot + "데이터 생성/";
    public const string LegacyIcon   = LegacyRoot + "아이콘·텍스처/";
    public const string LegacyTool   = LegacyRoot + "도구/";

    // 프리팹 생성 하위 그룹
    public const string LegacyLobby  = LegacyPrefab + "로비/";
    public const string LegacyPopup  = LegacyPrefab + "팝업/";
    public const string LegacyInGame = LegacyPrefab + "인게임/";
    public const string LegacyFx     = LegacyPrefab + "이펙트/";

    // ──────────────────────────────────────────────────────────
    // ■ 정렬 우선순위 — 두 루트가 함께 쓴다
    //   경로만 갈릴 뿐 그룹 안 정렬 규칙은 같아야 한다.
    // ──────────────────────────────────────────────────────────

    public const int ScenePrio  = 0;
    public const int SetupPrio  = 20;
    public const int PrefabPrio = 40;
    public const int UIPrio     = 80;
    public const int DataPrio   = 100;
    public const int IconPrio   = 200;
    public const int ToolPrio   = 300;
}
