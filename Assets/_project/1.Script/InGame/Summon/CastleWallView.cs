using UnityEngine;

// ============================================================
//  CastleWallView.cs
//  마왕성 성벽 그림 — **맞는 연출을 대신 받는 곳**.
//
//  ■ 왜 이게 필요한가
//    이 게임에서 캐릭터 HP 가 곧 마왕성 HP 다. 그래서 용사의 공격은
//    소환사 엔티티로 들어온다 — 계산상으로는 맞다.
//    그런데 화면에서 번쩍이고 흔들려야 하는 것은 **성벽**이다.
//    본체가 번쩍이면 "성벽 뒤에 숨어 있는 마왕" 이라는 그림이 무너지고,
//    성벽은 아무 일 없다는 듯 서 있어 어디가 깎이는지 읽히지 않는다.
//
//  ■ 연결
//    UnitHitSystem  — WallCoverComponent 를 가진 유닛의 피격 플래시를 끈다
//    BattleStatCollectorSystem — 같은 피해 기록을 읽어 여기 Hit() 을 부른다
//                                (그 시스템이 DamageResultElement 를 비우는
//                                 유일한 곳이라 다른 데서는 읽을 수 없다)
//
//  ■ 왜 흔들기까지 하나
//    성벽은 화면 왼쪽에 세로로 길게 뻗은 큰 그림이라, 색만 살짝 바꾸면
//    큰 면적이 균일하게 밝아져 오히려 눈에 안 띈다. 짧게 밀어 주면
//    "맞았다" 가 즉시 읽힌다.
//
//  ⚠ 씬에 손으로 붙이지 않는다 — InGameSceneSetup.BuildCastle 이 붙인다.
// ============================================================

[DisallowMultipleComponent]
public class CastleWallView : MonoBehaviour
{
    public static CastleWallView Instance { get; private set; }

    [Tooltip("성벽 스프라이트. 비워 두면 자기 자신에게서 찾는다.")]
    [SerializeField] SpriteRenderer _sprite;

    [Header("연출")]
    [Tooltip("피격 시 덧씌우는 색.")]
    [SerializeField] Color _flashColor = new(1f, 0.62f, 0.55f);

    [Tooltip("한 번 맞았을 때 번쩍임이 사라지는 데 걸리는 시간(초).")]
    [SerializeField] float _flashDecay = 0.18f;

    [Tooltip("피격 시 성벽이 밀리는 거리(월드 단위).")]
    [SerializeField] float _shakeDistance = 0.12f;

    Color   _baseColor;
    Vector3 _basePosition;

    /// <summary>0~1. 1 이면 방금 맞았다.</summary>
    float _flash;

    void Awake()
    {
        Instance = this;

        if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();

        _baseColor    = _sprite.color;
        _basePosition = transform.position;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 성벽이 맞았다.
    ///
    /// ⚠ 피해량으로 세기를 나누지 않는다
    ///   후반 스테이지에서는 한 대가 초반의 수십 배다. 세기를 비례시키면
    ///   초반에는 아무 반응이 없고 후반에는 성벽이 화면 밖으로 튄다.
    ///   맞았다는 사실만 같은 세기로 알린다 — 남은 체력은 HP 바가 말한다.
    /// </summary>
    public void Hit() => _flash = 1f;

    void LateUpdate()
    {
        if (_flash <= 0f) return;

        // ⚠ unscaledDeltaTime 이 아니라 deltaTime 이다
        //   배속·일시정지를 그대로 따라야 전투와 같은 시간 위에 있다.
        _flash = Mathf.Max(0f, _flash - Time.deltaTime / _flashDecay);

        _sprite.color = Color.Lerp(_baseColor, _flashColor, _flash);

        // 오른쪽(전장 쪽)에서 맞았으니 왼쪽으로 밀린다.
        transform.position = _basePosition + Vector3.left * (_shakeDistance * _flash);
    }
}
