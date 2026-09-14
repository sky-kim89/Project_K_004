// ============================================================
//  RunPerkIconKey.cs
//  특성 아이콘의 **조회 키**를 만드는 단 한 곳. 런타임·에디터 공용이다.
//
//  ■ 왜 따로 두는가
//    파일명(RunPerkIconAssets.PathOf)과 런타임 조회(SpriteManager.Get)가
//    같은 문자열이어야 한다. 두 곳에서 각자 조립하면 특성 하나를 개명하는
//    순간 한쪽만 따라가고, 화면에는 "그 특성만 그림이 없다" 로만 보인다.
//
//  ■ 왜 SpriteManager 인가 — 무엇이 뜰지 런타임에 정해진다
//    특성 3택은 30종 중 셋을 굴려 뽑고, 상단 보유 줄은 이번 런에 주운 것만
//    띄운다. Creator 가 미리 Sprite 를 박아 둘 수가 없다.
//    (종족 패시브·시너지는 목록이 고정이라 배열을 박아 둔다 — 축이 다르다)
// ============================================================

public static class RunPerkIconKey
{
    /// <summary>"perk_DeepVessel" 같은 아틀라스 키.</summary>
    public static string Of(RunPerk perk) => $"perk_{perk}";
}

public static class RunPerkIconExtensions
{
    /// <summary>
    /// 이 특성의 아이콘 키. 화면은 이 값으로 SpriteManager 에 물어본다.
    ///
    /// ⚠ None 에도 키를 준다 — 부르는 쪽이 분기하지 않게
    ///   조회는 어차피 실패하고 null 이 나온다. 화면은 그림이 없으면
    ///   칸을 끄면 그만이라, 여기서 특별 취급할 이유가 없다.
    /// </summary>
    public static string IconKey(this RunPerk perk) => RunPerkIconKey.Of(perk);
}
