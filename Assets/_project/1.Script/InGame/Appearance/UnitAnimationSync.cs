using BattleGame.Units;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

// ============================================================
//  UnitAnimationSync.cs
//  ECS UnitStateComponent → Animator 직접 연동 + 피격/사망 연출 컴포넌트.
//
//  ■ 상태 매핑
//    Idle              → bool "Idle"
//    Moving / Chasing  → bool "Run"
//    Attacking (대기)  → bool "Ready"
//    Attacking (발사)  → trigger "Slash"  ← 모든 직업 통일
//    Hit               → trigger "Hit"
//    Dead              → bool "Die"
//
//  ■ 공격 trigger 발동 시점
//    AttackCooldown 증가(= 공격 발생) 감지 → Slash trigger.
//    ApplyState 에서는 trigger 를 발동하지 않아 더블 트리거를 방지.
//
//  ■ 피격 연출
//    Hit 상태 진입 감지 → 스프라이트 색 플래시 (빨강 → 원래 색)
//    틴트는 '원래 색 × 배수' 다 (SetTint). 몸·무기가 함께 물들고, 검정 반투명인
//    그림자는 곱해도 그대로다. 되돌릴 때는 ClearTint() 한 번이면 된다 —
//    풀에서 꺼내는 쪽(UnitRuntimeBridge.SpawnEntity)도 이걸 부른다.
//
//  ■ 사망 연출 (UnitDeathDespawnSystem 이 TriggerDeath() 를 호출)
//    1. EntityLink.SyncPosition = false → ECS 위치 덮어쓰기 중단
//    2. 현재 바라보는 반대 방향으로 ease-out 이동
//    3. _deathHoldDuration 동안 Die 애니메이션 대기
//    4. PoolController.Despawn() 로 풀 반납
//
//  ■ ECS Job 완료 보장
//    EntityLink 와 동일한 static 프레임 캐시로 1회만 CompleteAllTrackedJobs 호출.
//    DefaultExecutionOrder(10) 으로 EntityLink(0) 보다 늦게 실행해 안전성 확보.
//
//  ■ 스프라이트 방향
//    ① 움직이는 중  → MovementComponent.Velocity.x
//    ② 멈춰 있으면  → 타겟 방향 (transform.localScale.x 반전)
//
//    ⚠ ② 가 없으면 등을 보인 채로 때린다
//      Velocity 만 보면 "마지막으로 걸어간 방향" 에 얼어붙는다. 멈춰서 싸우는
//      동안에는 Velocity 가 0 이라 그 값이 갱신되지 않기 때문이다.
//      보스 돌진(ActiveBossCharge)이 타겟을 8유닛 지나쳐 착지하면 적은 등 뒤에
//      있는데, 그 자리가 이미 사거리 안이라 추격을 하지 않아 방향이 영영 안 바뀐다.
//      넉백에 밀려 적을 지나친 경우도 똑같다.
// ============================================================

[DefaultExecutionOrder(10)]  // EntityLink(0) 이후 실행 보장
[RequireComponent(typeof(EntityLink))]
public class UnitAnimationSync : MonoBehaviour
{
    [SerializeField] Animator _animator;

    [Header("피격 플래시")]
    [SerializeField] float _hitFlashDuration = 0.18f;
    [SerializeField] Color _hitFlashColor    = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("지속 피해 — 도트 종류별 색")]
    [Tooltip("독·역병에 물드는 색. 흰색이면 아무 일도 안 한다.")]
    [SerializeField] Color _poisonTintColor = new Color(0.45f, 1f, 0.45f, 1f);

    [Tooltip("화상에 물드는 색. 불이라 붉다. 둘 다 걸리면 독(초록)이 이긴다 — DotKind 참고.")]
    [SerializeField] Color _burnTintColor = new Color(1f, 0.5f, 0.35f, 1f);

    [Header("사망 연출")]
    [Tooltip("뒤로 날아가는 거리 (월드 단위)")]
    [SerializeField] float _deathFlyDistance  = 3.5f;
    [Tooltip("뒤로 날아가는 시간 (초)")]
    [SerializeField] float _deathFlyDuration  = 0.35f;
    [Tooltip("날아간 뒤 Die 애니메이션을 기다리는 추가 시간 (초)")]
    [SerializeField] float _deathHoldDuration = 0.75f;

    EntityLink       _link;
    SpriteRenderer[] _renderers;
    Color[]          _baseColors;

    UnitState _prevState;
    float     _prevCooldown;
    float     _lastFacingX = 1f;
    bool      _isDying;
    bool      _isRising;    // 소환 직후 '땅에서 일어나는' 연출 중
    Coroutine _hitCoroutine;
    bool      _isPoisoned;
    DotKind   _dotLook;

    UnitJob   _job;
    bool      _jobCached;

    Coroutine _doubleStrikeCoroutine;

    // EntityLink 와 공유하는 CompleteAllTrackedJobs 프레임 캐시
    static int _lastCompletedFrame = -1;

    /// <summary>이 값을 넘는 x 속도라야 '움직이는 중' 으로 본다.</summary>
    const float MoveEpsilon = 0.01f;

    /// <summary>타겟과 x 가 이만큼은 벌어져야 방향을 바꾼다 (근접전 떨림 방지).</summary>
    const float FaceDeadzone = 0.35f;

    static readonly string[] BoolParams =
    {
        "Idle", "Ready", "Walk", "Run", "Crouch", "Crawl",
        "Jump", "Fall", "Land", "Block", "Climb", "Die",
    };

    // ── Unity 생명주기 ────────────────────────────────────────

    void Awake()
    {
        _link = GetComponent<EntityLink>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        // 클립이 들고 있는 AnimationEvent 를 받아 줄 자리를 Animator 옆에 세운다.
        // ⚠ 여기(루트)에 메서드를 추가해도 소용없다 — Unity 는 Animator 가 붙은
        //   GameObject 에서만 수신자를 찾는다. 자세한 이유는 AnimationEventSink 참고.
        if (_animator != null && !_animator.TryGetComponent<AnimationEventSink>(out _))
            _animator.gameObject.AddComponent<AnimationEventSink>();

        // ⚠ 스프라이트는 하나가 아니다 (Body · 무기 Renderer · 그림자 Square)
        //   예전엔 GetComponentInChildren 으로 하나만 잡아 그것만 물들이고 되돌렸다.
        //   되돌리는 쪽이 한 장뿐이면, 그 한 장이 아닌 곳에 물이 들었을 때
        //   빨간 채로 풀에 들어가 다음 유닛이 그대로 물려받는다.
        //
        // ⚠ 원래 색을 기억해 둔다 — 흰색으로 되돌리면 안 된다
        //   그림자(Square)는 검정 반투명이다. 일괄로 흰색을 칠하면 그림자가 하얘진다.
        //   틴트는 '원래 색 × 배수' 로 걸고, 되돌릴 때는 배수를 흰색(=1)으로 준다.
        CaptureBaseColors();
    }

    /// <summary>
    /// 지금 렌더러들이 입고 있는 색을 <b>기준색</b>으로 다시 잡는다.
    ///
    /// ■ ⚠ 종족 색조가 이걸 부른다 (사용자 지적, 2026-09-09)
    ///   피격 플래시는 '기준색 × 배수' 로 걸고 끝나면 배수를 흰색으로 되돌린다.
    ///   기준색을 Awake 에서 한 번만 잡으면 그건 <b>프리팹의 흰색</b>이라,
    ///   MonsterAppearanceBridge 가 입힌 종족 색조가
    ///     · 스폰 직후 ClearTint (UnitRuntimeBridge.SpawnEntity)
    ///     · 첫 피격 플래시가 끝나는 순간
    ///   에 통째로 지워졌다. 도감·카드에는 색이 보이는데 <b>필드에서만</b>
    ///   원래 색으로 서 있던 것이 이것이다.
    ///
    /// ⚠ 렌더러 목록도 다시 잡는다 — 인간형은 합성 뒤에 무기 렌더러가 늘어난다.
    /// ⚠ 플래시 중에 부르면 빨간색을 기준으로 굳힌다. 그래서 먼저 끊는다.
    /// </summary>
    public void CaptureBaseColors()
    {
        if (_hitCoroutine != null)
        {
            StopCoroutine(_hitCoroutine);
            _hitCoroutine = null;
        }

        _renderers  = GetComponentsInChildren<SpriteRenderer>(true);
        _baseColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
            _baseColors[i] = _renderers[i].color;
    }

    void OnEnable()
    {
        _prevState    = (UnitState)255; // 첫 프레임 강제 갱신
        _prevCooldown = 0f;
        _lastFacingX  = 1f;
        _isDying               = false;
        _isRising              = false;
        _hitCoroutine          = null;
        _doubleStrikeCoroutine = null;
        _jobCached             = false;

        if (_animator  != null) _animator.speed = 1f;
        ClearTint();
    }

    void LateUpdate()
    {
        // ⚠ 기상 중이라고 여기서 return 하면 안 된다
        //   피격 플래시·공격 트리거·쿨다운 추적이 전부 이 안에 있다. 통째로 막으면
        //   웅크린 채로 맞고 때리며, 쿨다운 값이 낡아 일어난 직후 헛스윙이 나간다.
        //   막는 것은 '자세를 덮어쓰는 한 줄'(ApplyState)뿐이고, 실제로 무슨 일이
        //   벌어지면(피격·공격) 기상 연출을 취소한다.
        if (_isDying) return;

        // ── 색을 매 프레임 다시 입힌다 (사용자 지적, 2026-09-11) ──
        //   ⚠ 벤더 애니메이션 Hit.anim · Heal.anim 이 SpriteRenderer 색(m_Color)을 움직인다.
        //     컨트롤러의 어느 클립이든 색을 건드리면 Animator 가 **모든 상태에서 매 프레임**
        //     그 속성의 기본값(바인드 시점의 흰색)을 되써 넣는다 (Write Defaults).
        //     그래서 종족 색조·중독 초록·피격 번쩍임이 필드에서 전부 지워졌다 —
        //     애니메이터가 없는 초상화(카드)에만 색이 보이던 것이 이것이다.
        //   LateUpdate 는 Animator 평가 **뒤**라 여기서 입힌 색이 그 프레임에 남는다.
        //   ⚠ 물든 개체만 한다 — 흰색 그대로인 유닛은 되써도 같은 값이라 비용만 든다.
        if (_keepTint || _currentTint != Color.white) SetTint(_currentTint);
        if (_animator == null) return;
        if (_link == null || _link.Entity == Entity.Null) return;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        EntityManager em = world.EntityManager;

        // ── ECS Job 완료 보장 (프레임당 1회) ─────────────────
        if (_lastCompletedFrame != Time.frameCount)
        {
            em.CompleteAllTrackedJobs();
            _lastCompletedFrame = Time.frameCount;
        }

        if (!em.Exists(_link.Entity)) return;

        // ── 현재 상태 읽기 ───────────────────────────────────
        if (!em.HasComponent<UnitStateComponent>(_link.Entity)) return;
        UnitState current = em.GetComponentData<UnitStateComponent>(_link.Entity).Current;

        // ── 공격 감지: 쿨다운 증가 = 공격 발생 ──────────────
        if (em.HasComponent<AttackComponent>(_link.Entity))
        {
            float cooldown = em.GetComponentData<AttackComponent>(_link.Entity).AttackCooldown;

            if (current == UnitState.Attacking && cooldown > _prevCooldown + 0.05f)
            {
                CancelRise();   // 때리기 시작했으면 일어나는 연출은 끝이다
                if (!_jobCached)
                {
                    _job = em.HasComponent<BattleGame.Units.UnitJobComponent>(_link.Entity)
                        ? em.GetComponentData<BattleGame.Units.UnitJobComponent>(_link.Entity).Job
                        : UnitJob.Knight;
                    _jobCached = true;
                }

                string triggerName = _job == UnitJob.Archer ? "Shot" : "Slash";

                if (em.HasComponent<DoubleStrikeTag>(_link.Entity))
                {
                    if (_doubleStrikeCoroutine != null) StopCoroutine(_doubleStrikeCoroutine);
                    _doubleStrikeCoroutine = StartCoroutine(DoubleStrikeRoutine(triggerName));
                }
                else
                {
                    _animator.SetTrigger(triggerName);
                }
            }

            _prevCooldown = cooldown;
        }

        // ── NeedsFlash 확인: 스턴 없는 낮은 데미지(독,존 등)도 플래시 발동 ──
        if (em.HasComponent<HitReactionComponent>(_link.Entity))
        {
            var reaction = em.GetComponentData<HitReactionComponent>(_link.Entity);
            if (reaction.NeedsFlash)
            {
                CancelRise();   // 맞았으면 웅크린 자세를 붙들고 있을 이유가 없다
                TriggerHitFlash();
                reaction.NeedsFlash = false;
                em.SetComponentData(_link.Entity, reaction);
            }

            // ── 도트를 받는 동안 몸이 초록빛으로 물든다 ──
            //   ⚠ 바뀐 순간에만 다시 칠한다. 매 프레임 SetTint 를 걸면
            //     피격 플래시가 도는 중에도 덮어써서 번쩍임이 사라진다.
            if (reaction.IsPoisoned != _isPoisoned || _dotLook != reaction.DotLook)
            {
                _isPoisoned = reaction.IsPoisoned;
                _dotLook    = reaction.DotLook;
                if (_hitCoroutine == null) SetTint(RestTint);
            }
        }

        // ── 상태 전환 처리 ───────────────────────────────────
        if (current != _prevState)
        {
            // Hit 상태 진입 → 색 플래시 (스턴 동반 강타 등)
            if (current == UnitState.Hit)
            {
                CancelRise();
                TriggerHitFlash();
            }

            // 기상 중에는 자세만 유지한다 — 이동·대기 상태가 웅크림을 덮지 않게.
            // 취소는 위(피격·공격)에서 이미 처리됐다.
            if (!_isRising) ApplyState(current);
            _prevState = current;
        }

        // ── 스프라이트 반전 — 이동 방향, 멈췄으면 타겟 방향 ──
        float vx = 0f;
        if (em.HasComponent<MovementComponent>(_link.Entity))
            vx = em.GetComponentData<MovementComponent>(_link.Entity).Velocity.x;

        if (Mathf.Abs(vx) > MoveEpsilon)
        {
            _lastFacingX = vx;
        }
        else if (em.HasComponent<AttackComponent>(_link.Entity)
              && !em.HasComponent<SkillCastLock>(_link.Entity))
        {
            // 제자리에서 싸우는 동안은 타겟이 방향을 정한다 (파일 상단 주석 참고)
            //
            // ⚠ 시전 중(SkillCastLock)에는 끼어들지 않는다
            //   돌진 연출은 이동 잡이 아니라 BossChargeRunner 가 transform 을 직접 몬다.
            //   그동안 Velocity 는 0 으로 굳어 있으므로 여기가 열려 있으면,
            //   타겟을 지나치는 순간 부호가 뒤집혀 **달리던 도중에 몸이 홱 돈다**.
            //   방향을 바로잡는 것은 잠금이 풀린 뒤(착지·경직 후)로 충분하다.
            var attack = em.GetComponentData<AttackComponent>(_link.Entity);
            if (attack.HasTarget)
            {
                float dx = TargetX(em, attack) - transform.position.x;

                // ⚠ 데드존 없이 부호만 보면 안 된다
                //   서로 겹쳐 붙은 근접전에서는 dx 가 0 근처를 오가므로
                //   매 프레임 좌우가 뒤집혀 스프라이트가 떤다.
                if (Mathf.Abs(dx) > FaceDeadzone) _lastFacingX = dx;
            }
        }

        float absX    = Mathf.Abs(transform.localScale.x);
        float targetX = _lastFacingX >= 0f ? absX : -absX;
        if (!Mathf.Approximately(transform.localScale.x, targetX))
        {
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(targetX, s.y, s.z);
        }
    }

    /// <summary>
    /// 타겟의 현재 x. 살아 있으면 실제 위치를, 아니면 마지막 캐시를 쓴다.
    ///
    /// ⚠ AttackComponent.TargetPosition 만 믿으면 안 된다
    ///   그 값을 매 공격마다 갱신하는 것은 UnitAttackSystem 뿐이다.
    ///   보스는 BossAttackSystem 이 따로 처리하는데 거기서는 지역 변수로만 쓰므로,
    ///   보스의 캐시는 **타겟을 처음 잡은 순간의 좌표**에서 멈춰 있다.
    ///   하필 방향이 틀어지는 것도 보스(돌진)라 캐시로는 이 버그를 못 고친다.
    /// </summary>
    static float TargetX(EntityManager em, in AttackComponent attack)
    {
        if (em.Exists(attack.TargetEntity) && em.HasComponent<LocalTransform>(attack.TargetEntity))
            return em.GetComponentData<LocalTransform>(attack.TargetEntity).Position.x;

        return attack.TargetPosition.x;
    }

    // ── 공개 API (UnitDeathDespawnSystem 에서 호출) ───────────

    /// <summary>
    /// 소환 직후 '땅에서 일어나는' 연출 — 웅크린 자세로 나타났다가 일어선다.
    /// 스켈레톤 소환(SkeletonSpawner)이 부른다.
    ///
    /// ⚠ 연출일 뿐 전투는 그대로 돈다
    ///   ECS 쪽은 소환 즉시 살아 있는 유닛이다. 여기서 막는 것은 애니메이터 상태
    ///   반영뿐이라, 일어나는 동안에도 맞고 때린다. 무적 구간을 만들려면
    ///   ECS 에 태그를 붙여야 한다 — 그건 연출이 아니라 규칙이다.
    /// </summary>
    // 기본값은 부르는 쪽이 값을 안 줄 때의 안전망일 뿐이다.
    // 실제 길이의 정본은 SkeletonSpawner.RiseDuration — 두 값이 어긋나 보이지 않게 맞춰 둔다.
    public void PlayRise(float duration = 0.3f)
    {
        if (_isDying || _isRising || _animator == null) return;
        StartCoroutine(RiseRoutine(duration));
    }

    System.Collections.IEnumerator RiseRoutine(float duration)
    {
        _isRising = true;
        SetBool("Crouch");

        // 실시간으로 센다 — 배속·일시정지에 연출이 갇히지 않게 (HitFlashRoutine 과 같은 이유)
        // _isRising 을 함께 본다 — 피격·공격으로 이미 취소됐으면 여기서도 즉시 빠진다.
        float end = Time.unscaledTime + duration;
        while (Time.unscaledTime < end && _isRising && !_isDying)
            yield return null;

        if (_isRising) EndRise();
    }

    /// <summary>맞거나 때리면 기상 연출을 즉시 접는다.</summary>
    void CancelRise()
    {
        if (_isRising) EndRise();
    }

    /// <summary>
    /// 기상 연출 종료 — 다음 LateUpdate 가 실제 상태를 다시 씌우게 만든다.
    ///
    /// ⚠ 여기서 Idle 을 박으면 안 된다
    ///   이미 달리거나 때리는 중일 수 있다. _prevState 를 없는 값으로 돌려놓아
    ///   '상태가 바뀐 것' 으로 보이게 하면, 다음 프레임이 진짜 상태를 씌운다.
    /// </summary>
    void EndRise()
    {
        _isRising  = false;
        _prevState = (UnitState)255;
    }

    /// <summary>
    /// 사망 연출을 시작한다. 연출 완료 후 자동으로 PoolController.Despawn() 을 호출한다.
    /// </summary>
    public void TriggerDeath()
    {
        if (_isDying) return;
        _isDying = true;

        if (_hitCoroutine != null)
        {
            StopCoroutine(_hitCoroutine);
            _hitCoroutine = null;
        }
        if (_doubleStrikeCoroutine != null)
        {
            StopCoroutine(_doubleStrikeCoroutine);
            _doubleStrikeCoroutine = null;
            if (_animator != null) _animator.speed = 1f;
        }

        ClearTint();

        // EntityLink 의 ECS→Transform 위치 동기화를 중단해 코루틴이 자유롭게 이동
        if (_link != null) _link.SyncPosition = false;

        StartCoroutine(DeathSequence());
    }

    // ── 상태 → Animator ──────────────────────────────────────

    void ApplyState(UnitState state)
    {
        switch (state)
        {
            case UnitState.Idle:
                SetBool("Idle");
                break;

            case UnitState.Moving:
            case UnitState.Chasing:
                SetBool("Run");
                break;

            case UnitState.Attacking:
                // 공격 사이 대기 자세. trigger 는 쿨다운 모니터링에서만 발동.
                SetBool("Ready");
                break;

            case UnitState.Hit:
                _animator.SetTrigger("Hit");
                break;

            case UnitState.Dead:
                SetBool("Die");
                break;
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    /// <summary>
    /// 피격 플래시를 시작한다.
    /// 스턴 여부와 무관하게 데미지를 받으면 호출된다.
    /// 이미 플래시 중이라면 처음부터 다시 시작해 다중 틱 데미지도 정상 표시.
    /// </summary>
    void TriggerHitFlash()
    {
        if (_hitCoroutine != null) StopCoroutine(_hitCoroutine);
        _hitCoroutine = StartCoroutine(HitFlashRoutine());
    }

    /// <summary>
    /// 연출이 없을 때 몸이 입고 있어야 할 색.
    ///
    /// ⚠ 플래시가 돌아갈 자리도 이것이다 — 흰색으로 되돌리면 도트가 남아
    ///   있는데도 초록이 벗겨져, 맞을 때마다 중독이 풀린 것처럼 보인다.
    /// </summary>
    Color RestTint
    {
        get
        {
            if (!_isPoisoned) return Color.white;
            return _dotLook == DotKind.Burn ? _burnTintColor : _poisonTintColor;
        }
    }

    // ── 틴트 ─────────────────────────────────────────────────

    /// <summary>
    /// 모든 스프라이트에 '원래 색 × tint' 를 건다. 흰색(1,1,1,1)이면 원래 색 그대로다.
    /// 곱셈이라 검정 반투명인 그림자는 어떤 틴트를 줘도 그대로 남는다.
    /// </summary>
    /// <summary>지금 입고 있어야 할 배수 색. LateUpdate 가 Animator 뒤에 다시 입힌다.</summary>
    Color _currentTint = Color.white;

    /// <summary>기준색이 흰색이 아닌가 (종족 색조) — 그러면 매 프레임 다시 입힌다.</summary>
    bool _keepTint;

    /// <summary>
    /// 기준색(종족 색조)이 입혀졌는지 알린다. MonsterAppearanceBridge.Tint 가 부른다.
    /// ⚠ 이게 켜져 있어야 Animator 의 기본값 되쓰기(LateUpdate 주석)를 이긴다.
    /// </summary>
    public void KeepTint(bool on) => _keepTint = on;

    void SetTint(Color tint)
    {
        _currentTint = tint;
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].color = _baseColors[i] * tint;
        }
    }

    /// <summary>
    /// 물든 색을 원래대로 되돌린다.
    ///
    /// ⚠ 풀에서 꺼낼 때 반드시 한 번 지나야 한다 (UnitRuntimeBridge.SpawnEntity)
    ///   피격 플래시가 도는 도중에 죽으면 코루틴이 중간에 끊긴 채 오브젝트가
    ///   비활성화된다 — 빨간 색이 그대로 굳은 채 풀에 들어간다.
    ///   OnEnable 에도 같은 호출이 있지만, 켜지는 시점을 타지 않는 재사용 경로가
    ///   있어서 꺼내 쓰는 쪽에서도 한 번 지운다. 두 번 지워도 비용은 없다.
    /// </summary>
    public void ClearTint()
    {
        // ⚠ 중독 상태도 함께 내린다 — 풀에 초록색이 굳은 채로 들어가면
        //   다음에 꺼내 쓰는 유닛이 멀쩡한 몸으로 물든 채 선다.
        //   ECS 쪽 IsPoisoned 는 다음 프레임에 제 값을 다시 써 준다.
        _isPoisoned = false;
        _dotLook    = DotKind.Poison;
        SetTint(Color.white);
    }

    // ── 코루틴 ───────────────────────────────────────────────

    System.Collections.IEnumerator HitFlashRoutine()
    {
        SetTint(_hitFlashColor);

        float t = 0f;
        while (t < _hitFlashDuration)
        {
            // ⚠ 실시간(unscaled)으로 센다 — Time.deltaTime 을 쓰면 안 된다
            //   일시정지·튜토리얼·배속 0 은 timeScale 을 0 으로 만든다. 그러면
            //   deltaTime 이 0 이라 이 루프가 영영 안 끝나고, 맞는 순간 멈춘 유닛이
            //   **빨간 채로 굳는다** (에디터 일시정지에서 보이던 그 현상).
            //   피격 번쩍임은 연출이라 게임 시간과 무관하게 흘러야 한다.
            t += Time.unscaledDeltaTime;
            SetTint(Color.Lerp(_hitFlashColor, RestTint, t / _hitFlashDuration));
            yield return null;
        }

        SetTint(RestTint);
        _hitCoroutine = null;
    }

    // 쌍신 공격(DoubleStrikeTag): 2배 속도로 공격 모션을 0.15초 간격으로 2회 재생
    System.Collections.IEnumerator DoubleStrikeRoutine(string triggerName)
    {
        _animator.speed = 2f;
        _animator.SetTrigger(triggerName);
        yield return new WaitForSeconds(0.15f);
        _animator.SetTrigger(triggerName);
        yield return new WaitForSeconds(0.15f);
        _animator.speed          = 1f;
        _doubleStrikeCoroutine   = null;
    }

    System.Collections.IEnumerator DeathSequence()
    {
        // 현재 바라보는 방향의 반대로 날아감 (scale.x 로 방향 저장됨)
        float facingSign = transform.localScale.x >= 0f ? 1f : -1f;
        float flyDirX    = -facingSign;

        Vector3 startPos = transform.position;
        Vector3 endPos   = startPos + new Vector3(flyDirX * _deathFlyDistance, 0f, 0f);

        float elapsed = 0f;
        while (elapsed < _deathFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / _deathFlyDuration;
            float et = 1f - (1f - t) * (1f - t);   // ease-out quad
            transform.position = Vector3.Lerp(startPos, endPos, et);
            yield return null;
        }
        transform.position = endPos;

        // Die 애니메이션이 끝날 때까지 대기
        yield return new WaitForSeconds(_deathHoldDuration);

        PoolController.Instance?.Despawn(gameObject);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    void SetBool(string param)
    {
        ClearBools();
        _animator.SetBool(param, true);
    }

    void ClearBools()
    {
        foreach (string p in BoolParams)
            _animator.SetBool(p, false);
    }
}
