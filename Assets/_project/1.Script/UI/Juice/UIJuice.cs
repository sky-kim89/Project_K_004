using UnityEngine;

// ============================================================
//  UIJuice.cs
//  성장 연출의 공개 창구. 호출부는 이 파일만 보면 된다.
//
//    UIJuice.LevelUp(rt, 12);        // 유닛 레벨업
//    UIJuice.SoldierUp(rt, 9);       // 병사 수 증가
//    UIJuice.EquipEnhance(rt, 3);    // 장비 강화
//    UIJuice.GradeUp(rt, "영웅");     // 장수 등급업
//    UIJuice.RelicUp(rt, 4);         // 유물 레벨업
//
//  ■ 연출의 무게를 성장의 무게에 맞춘다
//    다섯 가지가 다 같은 크기로 터지면 무엇이 큰 성장인지 안 읽힌다.
//    강화·병사처럼 자주 누르는 것은 짧고 가볍게, 등급업처럼 드물고 비싼 것은
//    화면을 한 번 잡는다. 자주 일어나는 일에 큰 연출을 붙이면 3분 만에 피로해진다.
//
//      장비 강화 (수시)  작게 · 0.42초 · 링 없음
//      병사 증가 (수시)  작게 · 0.45초 · 링 1
//      레벨업   (자주)  중간 · 0.55초 · 링 1
//      유물     (드묾)  크게 · 0.7초  · 링 2
//      등급업   (아주 드묾) 최대 · 0.8초 · 링 2 + 화면 섬광
//
//  ■ 색은 그 성장이 쓰는 재화 색을 따른다
//    골드로 올렸으면 금색, 전투석이면 청록 — 무엇을 썼는지가 색으로 남는다.
//    StatColors·GradeStyle 과 같은 계열을 써서 화면 전체의 색 언어를 지킨다.
//
//  ⚠ 상태를 바꾸고 UI 를 갱신한 뒤에 부른다
//    RefreshUI 가 텍스트를 새 값으로 바꾼 다음에 터뜨려야
//    "숫자가 오르면서 터진다" 로 읽힌다. 순서가 뒤집히면 옛 숫자가 튀고
//    한 프레임 뒤에 슬쩍 바뀐다.
//
//  ⚠ 소리는 아직 연결하지 않았다
//    AudioManager 는 클립이 없는 SfxKey 를 LogError 로 잡는다. 키만 먼저 늘리면
//    콘솔이 에러로 덮인다. 클립이 준비되면 각 Play 아래 한 줄씩 넣으면 된다.
// ============================================================

/// <summary>연출 한 벌의 수치. UIJuice 가 종류별로 미리 채워 둔다.</summary>
public struct JuicePreset
{
    public Color Accent;       // 파티클·링·라벨 색
    public float Punch;        // 대상이 커지는 배율 (1 = 안 커짐)
    public float PunchTime;
    public int   Sparks;       // 파티클 개수
    public float SparkDist;    // 뻗어 나가는 거리
    public float SparkSize;
    public float Stretch;      // 진행 방향으로 늘이는 정도 (잔상감)
    public float Gravity;      // 아래로 떨어지는 양
    public int   Rings;        // 충격파 개수 (0~2)
    public float RingSize;
    public float Life;         // 파티클 기준 수명(초)
    public float Flash;        // 화면 섬광 최대 알파 (0 = 없음)
    public float LabelSize;
    public float LabelOffset;  // 대상 중심에서 라벨이 시작하는 높이
    public float LabelRise;    // 떠오르는 거리
}

public static class UIJuice
{
    // ── 색 (재화·등급 계열과 맞춘다) ──────────────────────────
    static readonly Color Gold  = new(1.00f, 0.80f, 0.28f, 1f);   // 골드 — 레벨업
    static readonly Color Cyan  = new(0.40f, 0.85f, 1.00f, 1f);   // 전투석 — 장비 강화
    static readonly Color Green = new(0.45f, 0.92f, 0.55f, 1f);   // 소환석 — 병사
    static readonly Color Amber = new(1.00f, 0.62f, 0.20f, 1f);   // 강화석 — 등급업
    static readonly Color Violet= new(0.72f, 0.55f, 1.00f, 1f);   // 환생 포인트 — 유물

    // ══════════════════════════════════════════════════════════
    //  종류별 진입점
    // ══════════════════════════════════════════════════════════

    /// <summary>장비 강화 — 가장 자주 누르는 버튼이라 가장 가볍게.</summary>
    public static void EquipEnhance(RectTransform anchor, int newLevel)
        => Play(new JuicePreset
        {
            Accent      = Cyan,
            Punch       = 1.16f, PunchTime = 0.24f,
            Sparks      = 9,  SparkDist = 96f,  SparkSize = 22f,
            Stretch     = 1.6f, Gravity = 26f,
            Rings       = 0,
            Life        = 0.42f,
            LabelSize   = UIScale.FontMd, LabelOffset = 34f, LabelRise = 52f,
        }, anchor, $"+{newLevel}");

    /// <summary>
    /// 병사 수 증가 — 강화와 비슷한 빈도지만 부대가 커지는 일이라 링을 하나 준다.
    ///
    /// ⚠ 라벨에 총 병사 수를 쓰지 않는다
    ///   화면에 보이는 병사 수는 장수 스탯·특성·유물이 전부 반영된 값이라
    ///   여기서 다시 계산하면 표시와 어긋난다. 늘어난 양(+1)만 말한다.
    /// </summary>
    public static void SoldierUp(RectTransform anchor)
        => Play(new JuicePreset
        {
            Accent      = Green,
            Punch       = 1.18f, PunchTime = 0.26f,
            Sparks      = 11, SparkDist = 108f, SparkSize = 24f,
            Stretch     = 1.5f, Gravity = 24f,
            Rings       = 1,  RingSize = 210f,
            Life        = 0.45f,
            LabelSize   = UIScale.FontMd, LabelOffset = 36f, LabelRise = 56f,
        }, anchor, "병사 +1");

    /// <summary>
    /// 몬스터 장비 장착 — 장비 강화와 같은 빈도·같은 무게다.
    ///
    /// ⚠ 등급업(GradeUp)을 쓰지 않는다
    ///   장착은 되돌릴 수 있다(벗기면 그만). 화면이 번쩍이는 연출은
    ///   되돌릴 수 없는 지출(품질 개선)의 몫으로 남겨 둔다 — 파일 머리 규칙.
    ///
    /// ⚠ 라벨이 숫자가 아니라 이름이다
    ///   장비에는 레벨이 없다(MonsterGearData). "+3" 을 띄울 것이 없으므로
    ///   무엇을 꼈는지를 그대로 적는다.
    /// </summary>
    public static void GearEquip(RectTransform anchor, string gearName)
        => Play(new JuicePreset
        {
            Accent      = Cyan,
            Punch       = 1.20f, PunchTime = 0.26f,
            Sparks      = 12, SparkDist = 112f, SparkSize = 24f,
            Stretch     = 1.6f, Gravity = 26f,
            Rings       = 1,  RingSize = 230f,
            Life        = 0.46f,
            LabelSize   = UIScale.FontMd, LabelOffset = 36f, LabelRise = 58f,
        }, anchor, gearName);

    /// <summary>유닛 레벨업 — 성장의 기본 단위. 중간 무게.</summary>
    public static void LevelUp(RectTransform anchor, int newLevel)
        => Play(new JuicePreset
        {
            Accent      = Gold,
            Punch       = 1.26f, PunchTime = 0.32f,
            Sparks      = 16, SparkDist = 150f, SparkSize = 28f,
            Stretch     = 1.9f, Gravity = 32f,
            Rings       = 1,  RingSize = 290f,
            Life        = 0.55f,
            LabelSize   = UIScale.FontLg, LabelOffset = 44f, LabelRise = 74f,
        }, anchor, $"Lv.{newLevel}");

    /// <summary>
    /// 유물 레벨업 — 영구 성장이라 무겁게. 링 두 겹.
    ///
    /// ⚠ at 을 반드시 넘길 것 (유물 카드는 예외적으로 자리를 못 믿는다)
    ///   강화하면 RelicPopup 가 카드를 전부 지우고 다시 만든다. 새 카드는
    ///   GridLayoutGroup 이 이번 프레임 끝에 정렬하므로, 그 직후에는 아직
    ///   템플릿 자리에 겹쳐 있다. 그 좌표로 터뜨리면 어느 유물을 올려도
    ///   늘 같은 엉뚱한 자리에서 폭죽이 튄다.
    ///   CapturePointer() 로 잡아 둔 클릭 지점을 넘기면 그 문제를 통째로 피한다.
    /// </summary>
    public static void RelicUp(RectTransform anchor, int newLevel, Vector2? at = null)
        => Play(new JuicePreset
        {
            Accent      = Violet,
            Punch       = 1.30f, PunchTime = 0.36f,
            Sparks      = 20, SparkDist = 178f, SparkSize = 30f,
            Stretch     = 2.0f, Gravity = 30f,
            Rings       = 2,  RingSize = 330f,
            Life        = 0.70f,
            LabelSize   = UIScale.FontLg, LabelOffset = 46f, LabelRise = 84f,
        }, anchor, $"유물 Lv.{newLevel}", at);

    /// <summary>
    /// 장수 등급업 — 이 게임에서 가장 드물고 비싼 성장.
    /// 유일하게 화면 전체가 한 번 번쩍인다.
    /// </summary>
    public static void GradeUp(RectTransform anchor, string gradeLabel)
        => Play(new JuicePreset
        {
            Accent      = Amber,
            Punch       = 1.38f, PunchTime = 0.42f,
            Sparks      = 26, SparkDist = 220f, SparkSize = 34f,
            Stretch     = 2.2f, Gravity = 34f,
            Rings       = 2,  RingSize = 420f,
            Life        = 0.80f,
            Flash       = 0.18f,
            LabelSize   = UIScale.FontXl, LabelOffset = 52f, LabelRise = 96f,
        }, anchor, gradeLabel);

    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 직접 수치를 넘겨 재생한다. 위 다섯 가지로 안 되는 자리에만 쓴다.
    ///
    /// ⚠ anchor 가 null 이면 화면 중앙에서 터진다
    ///   버튼이 눌린 순간 팝업이 닫히는 경우가 있어 예외로 두지 않는다.
    ///   연출이 없는 것보다 자리만 틀린 편이 낫다.
    /// </summary>
    public static void Play(in JuicePreset preset, RectTransform anchor,
                            string label = null, Vector2? at = null)
        => UIJuiceLayer.Ensure().Play(preset, anchor, label, at);

    /// <summary>
    /// 지금 손가락(마우스)이 있는 화면 좌표.
    ///
    /// UI 를 다시 그리기 전에 잡아 두었다가 연출에 넘기면, 그 사이 대상이
    /// 파괴·재생성돼도 터지는 자리는 눌린 자리 그대로 남는다.
    /// </summary>
    public static Vector2 CapturePointer() => UIJuiceLayer.PointerScreenPos();
}
