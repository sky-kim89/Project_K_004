using UnityEngine;

// ============================================================
//  InGameHUD.cs
//  인게임 HUD 루트 표식.
//
//  InGameUIPrefabCreator 가 씬에서 HUD 루트를 이 컴포넌트로 찾아 굽는다.
//  원작의 장군 카드 줄(GeneralPanelUI)은 걷어냈다 — 플레이어의 장수가 없다.
// ============================================================

public class InGameHUD : MonoBehaviour
{
    [Header("서브 UI")]
    [SerializeField] TopBarUI _topBar;

    void Start()
    {
        UIClickSfx.Bind(gameObject);   // 상단바 버튼(배속·일시정지)에 클릭음
    }
}
