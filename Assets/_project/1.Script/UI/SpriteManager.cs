using UnityEngine;
using UnityEngine.U2D;

// ============================================================
//  SpriteManager.cs
//  게임 전역 스프라이트 조회 ScriptableObject 싱글턴.
//
//  초기화:
//    Assets/Resources/SpriteManager.asset 에 배치.
//    씬 로드 전 자동 로드.
//
//  사용법:
//    Sprite icon = SpriteManager.Instance.Get("item_gold");
//
//  아틀라스 → 폴더 매핑:
//    _itemAtlas       ← Icons/Items/
//    _generalAtlas    ← Icons/Classes/ + Icons/Skills/
//    _traitAtlas      ← Icons/Difficulty/ + Icons/RunPerks/   (이름만 원작 그대로)
//    _relicTreeAtlas  ← Icons/RelicTree/   (트리 노드 — 개수는 RelicTreeCatalog 가 정본)
//    _stageNodeAtlas  ← Icons/StageNodes/
//    _lobbyBtnAtlas   ← Icons/LobbyBtns/
//
//  스프라이트 이름은 PNG 파일명(확장자 제외)과 동일해야 한다.
// ============================================================

[CreateAssetMenu(fileName = "SpriteManager", menuName = "ProjectK/SpriteManager")]
public class SpriteManager : ScriptableObject
{
    public static SpriteManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad() => Instance = Resources.Load<SpriteManager>("SpriteManager");

    [Header("아이템 / 재화")]
    [SerializeField] SpriteAtlas _itemAtlas;

    [Header("장군 (직업 + 스킬)")]
    [SerializeField] SpriteAtlas _generalAtlas;

    [Header("난이도 · 런 특성")]
    [SerializeField] SpriteAtlas _traitAtlas;

    // ⚠ 구 유물 아틀라스(_relicAtlas / Icons/Relics/ 29장)는 지웠다 (2026-09-07)
    //   구 RelicId 기준이라 트리 노드와 1:1로 맞지 않았고, 그걸 참조하던
    //   RelicData SO 가 이미 없어서 **아무도 안 찾는 그림 29장**이 빌드에 실려 있었다.
    [Header("유물 트리 노드")]
    [SerializeField] SpriteAtlas _relicTreeAtlas;

    [Header("스테이지 노드")]
    [SerializeField] SpriteAtlas _stageNodeAtlas;

    [Header("로비 버튼")]
    [SerializeField] SpriteAtlas _lobbyBtnAtlas;

    // 전체 아틀라스를 순서대로 검색. 없으면 null.
    public Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        Sprite s;
        if (_itemAtlas      != null && (s = _itemAtlas.GetSprite(name))      != null) return s;
        if (_generalAtlas   != null && (s = _generalAtlas.GetSprite(name))   != null) return s;
        if (_traitAtlas     != null && (s = _traitAtlas.GetSprite(name))     != null) return s;
        if (_relicTreeAtlas != null && (s = _relicTreeAtlas.GetSprite(name)) != null) return s;
        if (_stageNodeAtlas != null && (s = _stageNodeAtlas.GetSprite(name)) != null) return s;
        if (_lobbyBtnAtlas  != null && (s = _lobbyBtnAtlas.GetSprite(name))  != null) return s;

        return null;
    }
}
