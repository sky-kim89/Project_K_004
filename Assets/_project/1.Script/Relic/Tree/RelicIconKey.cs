using System.Text;

// ============================================================
//  RelicIconKey.cs
//  유물 트리 노드 → 아이콘 파일명(= 스프라이트 이름) 변환.
//
//  규칙:  RelicNodeId.N_Blade  →  "node_blade"
//         RelicNodeId.N_OneVsThousand → "node_one_vs_thousand"
//
//  ■ 왜 별도 파일인가
//    이 규칙을 세 곳이 같이 써야 한다 —
//      · RelicTreePopup      : 런타임에 스프라이트를 찾는다
//      · RelicTreeIconStubs  : 더미 PNG 를 이 이름으로 굽는다 (에디터)
//      · Docs/RelicTree_Icon_Spec.md : 생성 명세의 파일명 표
//    한 곳이라도 어긋나면 그림이 조용히 안 붙는다 (에러가 안 난다).
//    규칙을 바꾸려면 여기만 고치고 더미를 다시 구울 것.
//
//  ⚠ 파일명은 PNG 파일명(확장자 제외)과 반드시 같아야 한다
//    SpriteAtlas 는 스프라이트를 "PNG 파일명" 으로 색인한다.
//    SpriteManager.Get(key) 가 그 이름으로 찾으므로,
//    node_blade.png 를 blade.png 로 저장하면 못 찾는다.
// ============================================================

public static class RelicIconKey
{
    /// <summary>아이콘 PNG 가 사는 폴더 (프로젝트 루트 기준).</summary>
    public const string FolderPath = "Assets/_project/3.Textures/Icons/RelicTree";

    /// <summary>노드 ID → 스프라이트 이름. 예: N_Blade → "node_blade"</summary>
    public static string Of(RelicNodeId id) => "node_" + ToSnake(id.ToString());

    /// <summary>노드 ID → PNG 경로. 예: ".../RelicTree/node_blade.png"</summary>
    public static string PathOf(RelicNodeId id) => $"{FolderPath}/{Of(id)}.png";

    /// <summary>
    /// "N_OneVsThousand" → "one_vs_thousand"
    ///
    /// 앞의 "N_" 를 떼고, 대문자 앞마다 '_' 를 넣어 소문자로 내린다.
    /// 연속 대문자(약어)는 하나로 묶는다 — "N_HPBoost" → "hp_boost".
    /// </summary>
    static string ToSnake(string enumName)
    {
        if (string.IsNullOrEmpty(enumName)) return "";
        if (enumName.StartsWith("N_")) enumName = enumName.Substring(2);

        var sb = new StringBuilder(enumName.Length + 8);
        for (int i = 0; i < enumName.Length; i++)
        {
            char c = enumName[i];

            // 대문자이고, 앞 글자가 소문자거나 / 다음 글자가 소문자면 새 단어의 시작이다.
            // (연속 대문자 약어 안에서는 끊지 않는다)
            bool boundary = char.IsUpper(c) && i > 0 &&
                            (!char.IsUpper(enumName[i - 1]) ||
                             (i + 1 < enumName.Length && char.IsLower(enumName[i + 1])));

            if (boundary) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
