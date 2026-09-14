using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  TraitIconUI.cs
//  아이콘 1칸 + 눌러서 툴팁 — 재사용 컴포넌트.
//
//  ■ 쓰는 곳
//    런 특성 줄(RunPerkBarUI) · 난이도 디버프 · 소환사 개성 등
//    "그림 하나 + 이름/설명 툴팁" 이 필요한 모든 칸.
//
//  ■ 이름이 Trait 인 이유
//    원작 특성(TraitData) 전용 칸이었다. 원작 특성 축은 걷어냈고
//    표시(SetupCustom)만 남았다. 프리팹이 이 컴포넌트를 들고 있어 이름은 그대로 둔다.
//
//  Inspector 연결 (TraitIconSlotBuilder · MainPanelCreator 자동):
//    _iconImage : 아이콘 이미지
//    _iconBtn   : 클릭 버튼 — 클릭 시 툴팁 표시
//    _tooltip   : InfoTooltipUI (이름/설명/스탯 공용 툴팁, 기본 비활성)
// ============================================================

public class TraitIconUI : MonoBehaviour
{
    [SerializeField] Image         _iconImage;
    [SerializeField] Button        _iconBtn;
    [SerializeField] InfoTooltipUI _tooltip;

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>그림과 툴팁 내용을 채운다. 그림이 없으면 빈 칸 색으로 둔다.</summary>
    public void SetupCustom(Sprite icon, string title, string desc, string stat = "")
    {
        CloseTooltip();

        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            _iconImage.color  = icon != null ? Color.white : new Color(0.25f, 0.25f, 0.38f);
        }

        if (_iconBtn != null)
        {
            _iconBtn.onClick.RemoveAllListeners();
            _iconBtn.onClick.AddListener(() => _tooltip.Show(title, desc, stat));
        }
    }

    public void CloseTooltip()
    {
        if (_tooltip != null) _tooltip.Close();
    }

    // 칸이 꺼질 때(목록 갱신 등) 툴팁만 화면에 남지 않게 한다.
    void OnDisable() => CloseTooltip();
}
