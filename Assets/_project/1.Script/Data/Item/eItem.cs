// ============================================================
//  eItem.cs
//  영구 재화 타입.
//
//  ⚠ 비워 둔 번호 — 재사용하지 말 것. 옛 세이브(ItemData)에 수량이 남아 있다.
//    1~4 잼·에너지·스태미나·명예 · 100~105 원작 성장 재료 · 200 장비 상자
//    900~903 장군·장비·특성·어빌리티 지급 (원작 보상 체계 — 전부 걷어냈다)
// ============================================================

public enum eItem
{
    None = -1,

    Gold               = 0,   // 영구 골드 — 품질 개선 · 장비 레벨업 (런 골드 RunGoldData 와 다른 축)
    ReincarnationPoint = 5,   // 환생 포인트 — 유물 강화 (잔액 정본은 ReincarnationData)
}

public static class ItemExtensions
{
    /// <summary>SpriteManager 조회용 스프라이트 이름 (PNG 파일명 기준).</summary>
    public static string IconKey(this eItem item) => item switch
    {
        eItem.Gold               => "item_gold",
        eItem.ReincarnationPoint => "item_reincarnation_point",
        _                        => "",
    };
}
