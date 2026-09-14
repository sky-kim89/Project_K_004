// ============================================================
//  DamageFontNames.cs
//  피해 숫자 폰트의 이름·문자 집합 정본.
//
//  ■ 왜 런타임 쪽에 두는가
//    이 값을 아는 곳이 둘이다 — 굽는 쪽(DamageFontCreator, 에디터)과
//    쓰는 쪽(DamageNumberLayer, 런타임). 에디터 스크립트에 두면 런타임이
//    참조할 수 없어 문자열이 두 벌이 되고, 언젠가 한쪽만 고쳐진다.
//
//  ■ ⚠ 문자 집합이 곧 계약이다
//    이 폰트는 **정적 아틀라스**다 — 여기 없는 글자는 런타임에 채워지지
//    않고 □ 로 나온다. 표기를 바꿔 새 글자를 쓰게 됐다면(예: "치명"),
//    반드시 Charset 에 먼저 넣고 폰트를 다시 구워야 한다.
//
//    지금 쓰는 글자
//      0-9   숫자
//      . ,   축약 소수점·자리 구분
//      K M   천·백만 축약 (DamageNumberLayer.Format)
//      + - ! % 앞으로 회복·감소·치명 표기에 쓸 여지
// ============================================================

public static class DamageFontNames
{
    /// <summary>Resources.Load 키. 에셋 경로는 Assets/Resources/ 아래여야 한다.</summary>
    public const string ResourceKey = "DamageNumberFont";

    /// <summary>정적 아틀라스에 구울 글자 전부.</summary>
    public const string Charset = "0123456789.,+-!%KM";
}
