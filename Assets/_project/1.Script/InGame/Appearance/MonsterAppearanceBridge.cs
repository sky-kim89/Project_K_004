using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;
using UnityEngine.U2D.Animation;

// ============================================================
//  MonsterAppearanceBridge.cs
//  소환 몬스터의 외형을 적용한다. 신체 형태에 따라 경로가 갈린다.
//
//  ■ 인간형 → 기존 UnitAppearanceBridge 에 그대로 위임
//    CharacterBuilder 가 Body/Head/Eyes/Ears/무기를 합성한다.
//    종족 16종 × 무기 20종이라 변형이 사실상 무한하고, 장비도 입는다.
//
//  ■ 비인간형 → CharacterBuilder 를 건너뛰고 완성 라이브러리를 직접 꽂는다
//    Wolf / Hog / Slug / Troll 은 레이어가 없는 통짜 스프라이트다.
//    합성할 게 없으므로 CharacterBuilder 를 돌릴 이유가 없다 —
//    미리 구워 둔 SpriteLibraryAsset 을 Body 의 SpriteLibrary 에 그대로 넣으면 끝이다.
//
//    이게 성립하는 이유: CharacterBuilder 도 결국 마지막에
//      Character.Body.GetComponent<SpriteLibrary>().spriteLibraryAsset = 합성결과
//    를 할 뿐이다. 우리는 그 '합성결과' 를 미리 준비해 둔 것으로 대체한다.
//    Animator·UnitAnimationSync·전투 로직은 아무것도 달라지지 않는다.
//
//  ⚠ 원본 라이브러리를 그대로 넣으면 안 된다
//    Bonus/Monsters/*/SpriteLibrary.asset 의 카테고리는 Attack / Death 다.
//    우리 Animator 는 Slash / Shot / Die / Hit 을 찾는다.
//    MonsterLibraryCreator 로 이름을 맞춰 다시 구운 것을 넣어야 한다.
//
//  ⚠ 비인간형은 장비 스프라이트를 **얹지** 못한다 — 대신 색으로 입는다
//    레이어가 없어 갑옷을 그릴 자리가 없다. 설계 선택이 아니라 자산 제약이다.
//    그래서 몬스터 장비(MonsterGearData)는 몸 형태별로 갈린다 —
//      인간형   : 레이어를 갈아 끼운다 (정말로 다른 모습이 된다)
//      비인간형 : 가죽=몸 색조 · 덩치=크기 · 장식=불빛
//    ⚠ 색조는 SpriteRenderer.color(정점 색)다. 머티리얼을 건드리면 배칭이 깨진다.
// ============================================================

[DefaultExecutionOrder(-100)]
public class MonsterAppearanceBridge : MonoBehaviour
{
    CharacterBuilder      _builder;
    UnitAppearanceBridge  _humanoidBridge;

    // 마지막으로 꽂은 비인간형 라이브러리 — 같은 것이면 다시 꽂지 않는다.
    // (풀에서 같은 몬스터가 반복해 나오는 게 정상이라 이 검사가 대부분 걸린다)
    SpriteLibraryAsset _lastLibrary;

    /// <summary>
    /// 참조를 잡는다. 이미 잡혀 있으면 아무것도 하지 않는다.
    ///
    /// ⚠ Awake 에만 맡기지 않는다
    ///   에디터 도구(초상화 스냅샷)가 프리뷰 씬에 세워 Apply 를 바로 부른다.
    ///   그 경로에서는 Awake 가 돌았다고 보장할 수 없다.
    /// </summary>
    void EnsureRefs()
    {
        if (_builder == null)        _builder        = GetComponent<CharacterBuilder>();
        if (_humanoidBridge == null) _humanoidBridge = GetComponent<UnitAppearanceBridge>();
    }

    void Awake() => EnsureRefs();

    /// <summary>
    /// 종족 정의에 맞는 외형을 적용한다.
    /// </summary>
    /// <param name="species">소환할 종족</param>
    /// <param name="unitName">외형 랜덤 시드 (인간형에서만 쓴다)</param>
    /// <param name="gear">
    /// 도감에서 그 종족에 끼워 둔 장비의 겉모습 (MonsterGearRule.BuildVisual).
    /// 인간형은 레이어를 갈아 끼우고, 비인간형은 색조로 받는다.
    /// </param>
    public void Apply(MonsterSpeciesData species, string unitName, in MonsterGearVisual gear)
    {
        EnsureRefs();

        if (species.BodyType == MonsterBodyType.Humanoid)
        {
            ApplyHumanoid(species, unitName, gear);
            return;
        }

        ApplyNonHumanoid(species, gear);
    }

    // ── 인간형 — 기존 경로에 위임 ────────────────────────────

    void ApplyHumanoid(MonsterSpeciesData species, string unitName, in MonsterGearVisual gear)
    {
        // CharacterBuilder 를 다시 켠다 — 직전에 비인간형으로 쓰였을 수 있다.
        _builder.enabled = true;
        _lastLibrary     = null;

        // ⚠ 공격 형태를 함께 넘긴다 (사용자 지적, 2026-09-12)
        //   안 넘기면 고블린 궁수가 낫을, 해골 술사가 녹슨 칼을 들고 선다 —
        //   무기 풀이 공격 형태로 갈린다 (EnemyAppearanceRoller.RangedWeaponsFor).
        _humanoidBridge.ApplyEnemy(species.Race, unitName, species.AttackKind, gear);

        // ⚠ 합성 **뒤에** 물들인다 (2026-09-09)
        //   인간형도 색조를 쓴다 — 진화체는 뿌리와 같은 EnemyRace 라 그림이
        //   같아서, 색이 없으면 고블린 궁수와 두목이 한 마리로 보인다.
        //   장비는 레이어로 입으므로 gear.Tint 는 여기서 쓰지 않는다.
        //   (Rebuild 는 같은 SpriteRenderer 에 라이브러리만 갈아 끼우므로
        //    정점 색은 살아남는다 — 순서만 뒤로 두면 된다)
        Tint(species.BodyTint);
    }

    // ── 비인간형 — 완성 라이브러리 직접 꽂기 ─────────────────

    void ApplyNonHumanoid(MonsterSpeciesData species, in MonsterGearVisual gear)
    {
        if (species.NonHumanoidLibrary == null)
        {
            Debug.LogError(
                $"[MonsterAppearanceBridge] '{species.Id}' 는 비인간형인데 " +
                "NonHumanoidLibrary 가 비어 있습니다. " +
                "Tools > Project K > 몬스터 라이브러리 굽기 로 만든 에셋을 넣으세요.");
            return;
        }

        // CharacterBuilder 를 꺼 둔다.
        // 켜져 있으면 Rebuild 가 돌아 우리가 꽂은 라이브러리를 덮어쓴다.
        _builder.enabled = false;

        // ⚠ 색조는 라이브러리 검사보다 **먼저**, 그리고 **매번** 건다
        //   아래 조기 반환에 걸리면 라이브러리는 그대로여도 장비는 바뀌었을 수 있다.
        //   같은 종족이 풀에서 반복해 나오는 게 정상이라 이 경로가 대부분이다.
        //
        //   ⚠ 종족 색조와 장비 색조를 **곱한다** (2026-09-09)
        //     둘은 다른 것을 말한다 — 종족 색은 "무엇인가"(힐/독/강철 슬라임),
        //     장비 색은 "무엇을 입었나" 다. 한쪽이 다른 쪽을 덮으면 그 축이 죽는다.
        //     그래서 양쪽 값 모두 옅어야 한다 (MonsterCodexCreator.Rgb 주석).
        Tint(gear.Tint * species.BodyTint);

        if (_lastLibrary == species.NonHumanoidLibrary) return;

        var library = _builder.Character.Body.GetComponent<SpriteLibrary>();
        library.spriteLibraryAsset = species.NonHumanoidLibrary;

        // 캐시된 스프라이트를 버리게 만든다 —
        // 라이브러리만 바꾸면 SpriteResolver 가 직전 스프라이트를 계속 들고 있다.
        foreach (var resolver in _builder.Character.Body.GetComponentsInChildren<SpriteResolver>(true))
            resolver.ResolveSpriteToSpriteRenderer();

        _lastLibrary = species.NonHumanoidLibrary;
    }

    // ── 색조 ─────────────────────────────────────────────────
    //
    //  ■ 비인간형이 장비를 "입는" 유일한 방법이다
    //    늑대·멧돼지·슬라임·트롤은 레이어가 없어 갑옷을 얹을 자리가 없다.
    //    대신 몸 전체에 색을 입힌다 — 강철은 회청, 화염은 주홍.
    //    라인에 다섯 마리가 늘어섰을 때 "쟤만 다르다" 가 바로 읽힌다.
    //
    //  ⚠ SpriteRenderer.color 만 건드린다 (정점 색)
    //    머티리얼 속성을 만지면 인스턴스가 갈라져 배칭이 깨진다.
    //    정점 색은 그 문제가 없다 — UnitBuffAuraView 와 같은 규율이다.
    //
    //  ⚠ 흰색으로 되돌리는 경로가 반드시 있어야 한다
    //    풀에서 재사용되므로, 장비를 낀 개체가 쓰던 몸을 맨몸 개체가
    //    물려받으면 물든 채로 나온다.

    // 마지막으로 입힌 색 — 같으면 렌더러를 다시 훑지 않는다.
    Color _lastTint = Color.white;

    void Tint(Color color)
    {
        if (_lastTint == color) return;
        _lastTint = color;

        foreach (var sr in _builder.Character.Body.GetComponentsInChildren<SpriteRenderer>(true))
            sr.color = color;

        // ⚠ 입힌 색을 **기준색으로 못 박는다** (사용자 지적, 2026-09-09)
        //   피격 플래시는 '기준색 × 배수' 로 돌고 끝나면 배수를 흰색으로 되돌린다.
        //   기준색이 프리팹의 흰색인 채로 두면, 스폰 직후의 ClearTint 와 첫 피격에
        //   종족 색조가 통째로 지워진다 — 도감에는 색이 있는데 필드에서만
        //   원래 색으로 서 있던 것이 이것이다.
        //   ⚠ 초상화 합성기에는 이 컴포넌트가 없다 (전용 GameObject 라 애니메이션이
        //     없다). 그래서 TryGetComponent 다 — 선택적 연결이라 방어가 아니다.
        if (TryGetComponent<UnitAnimationSync>(out var sync))
        {
            sync.CaptureBaseColors();

            // ⚠ 물든 개체는 Animator 뒤에서 매 프레임 다시 입혀야 한다 (UnitAnimationSync.LateUpdate)
            sync.KeepTint(color != Color.white);
        }
    }

    /// <summary>
    /// 마지막으로 꽂은 라이브러리를 잊는다 — 이 몸이 인간형으로 다시 합성되면 부른다.
    /// ⚠ 안 잊으면 다음에 같은 비인간형으로 돌아올 때 "이미 꽂혀 있다" 로 건너뛴다 (소환사 외형 교체).
    /// </summary>
    public void ForgetLibrary() => _lastLibrary = null;
}
