using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CoreHpBarUI.cs
//  마왕성 체력 — 이 게임의 **유일한 패배 조건**을 화면에 세운다.
//
//  ■ 왜 만들었나 (사용자 지적, 2026-09-07)
//    RunCoreData 를 읽는 UI 가 하나도 없었다. 용사가 성벽에 닿으면
//    CoreBreachSystem 이 Debug.Log 한 줄만 찍고 그 용사를 조용히 지웠다.
//    플레이어 눈에는 "적이 왼쪽 끝에 가면 그냥 사라진다" 로만 보였다 —
//    지고 있다는 사실 자체가 화면에 없으니, 막아 내는 것도 아무 느낌이 없었다.
//
//  ■ **짧은 가로 막대**다 (사용자 지적, 2026-09-07 재조정)
//    세로 기둥으로 두 번 시도했고 둘 다 "체력처럼 안 생겼다" 였다.
//    체력은 가로 막대가 관습이고 세로는 게이지·슬라이더로 읽힌다.
//    자리는 왼쪽 위 — 보유 특성 줄 바로 아래(옛 시너지 자리)다.
//    시너지는 반대로 왼쪽 가장자리 세로 줄로 내려갔다. 둘을 맞바꿨다.
//
//    ⚠ 짧게 둔다 — 길면 보스 HP 바와 헷갈린다
//      보스 바는 가운데에 760, 이건 왼쪽에 360 이다. 자리·길이·색 셋이
//      다 달라야 한 화면의 두 막대가 갈린다.
//
//  ■ 수치는 막대 **한가운데**에 얹는다
//    ⚠ 글자색은 흰색 하나다 — 바탕이 두 가지(빈 칸의 검정 · 채움의 보라/붉은)
//      인데 둘 다 어두워서 흰 글자가 양쪽에서 읽힌다.
//
//  ■ 색이 곧 남은 여유다
//    보라 → 주황 → 붉은. 보스 HP 바가 붉은색이라 **가득 찬 상태를 붉게 두지
//    않는다** — 같은 색 막대 둘이 화면에 있으면 어느 쪽이 내 것인지 헷갈린다.
//    위험 구간에서만 붉어지고, 그때 맥동이 함께 붙는다.
//
//  ■ 뚫리면 화면이 반응한다
//    숫자만 줄면 못 본다. 성 쪽 화면 가장자리가 붉게 번쩍이고, 기둥이 한 번
//    빛나고, 카메라가 흔들린다(CameraShaker). 셋 다 성 쪽에서 일어나므로
//    "어디가 뚫렸나" 를 눈이 저절로 찾는다.
//
//    ⚠ 회복에는 번쩍이지 않는다
//      갈림길 야영지가 체력을 판다(RunCoreData.Heal). 늘어나는 것까지 같은
//      연출로 알리면 경고가 경고로 안 읽힌다. 줄어들 때만 터뜨린다.
// ============================================================

public class CoreHpBarUI : MonoBehaviour
{
    [Header("막대 (왼쪽 위, 특성 줄 아래)")]
    [Tooltip("짧은 가로 막대. 자리는 프리팹이 정한다.")]
    [SerializeField] RectTransform   _column;

    [SerializeField] Image           _fill;
    [SerializeField] TextMeshProUGUI _text;

    [Tooltip("위험할 때 맥동하고, 뚫릴 때 번쩍이는 덧빛.")]
    [SerializeField] Image           _glow;

    [Header("뚫렸을 때")]
    [Tooltip("성 쪽 화면 가장자리를 덮는 붉은 띠. 평소에는 완전히 투명하다.")]
    [SerializeField] Image           _breachFlash;

    // ── 색 ───────────────────────────────────────────────────
    //  ⚠ 보스 HP 바(0.90, 0.20, 0.18)와 겹치지 않게 잡았다.
    //    가득 찬 상태가 붉으면 화면에 붉은 막대가 둘이 되어 갈리지 않는다.
    static readonly Color Safe   = new(0.62f, 0.34f, 0.98f);   // 마왕성 보라
    static readonly Color Warn   = new(0.98f, 0.66f, 0.22f);
    static readonly Color Danger = new(0.98f, 0.24f, 0.24f);

    /// <summary>이 아래로 내려가면 주황, 그 절반 아래면 붉은색 + 맥동.</summary>
    const float WarnRatio   = 0.5f;
    const float DangerRatio = 0.25f;

    /// <summary>뚫렸을 때 가장자리 띠가 올라가는 최대 투명도.</summary>
    const float FlashPeak = 0.55f;

    /// <summary>번쩍임이 사라지는 데 걸리는 시간(초).</summary>
    const float FlashFade = 0.45f;

    /// <summary>카메라 흔들림 세기. 보스 스킬(1.0 안팎)보다 약하게 — 매 판 여러 번 난다.</summary>
    const float ShakeAmount = 0.45f;

    RunCoreData _core;

    /// <summary>직전에 그린 값. 줄었을 때만 연출을 터뜨리기 위해 들고 있는다.</summary>
    int _shown = -1;

    float _flash;      // 남은 번쩍임 (0~1)
    float _pulse;      // 위험 구간 맥동 위상

    // ── 수명 ─────────────────────────────────────────────────

    void OnEnable()
    {
        // ⚠ 스프라이트 없는 Filled 는 fillAmount 를 무시한다 — 막대가 가득 찬 채로 안 줄었다
        FillSprite.Ensure(_fill);

        Bind();
        Refresh();
    }

    void OnDisable()
    {
        if (_core != null) _core.OnCoreChanged -= Refresh;
        _core = null;
    }

    /// <summary>
    /// 세이브 섹션을 잡고 구독한다.
    ///
    /// ⚠ Awake 에서 잡지 않는다 — UserDataManager 가 아직 안 섰을 수 있다
    ///   (HUD 는 씬에 미리 놓여 있고 런 데이터는 RunBootstrap 이 채운다).
    ///   잡힐 때까지 Update 에서 다시 시도한다.
    /// </summary>
    void Bind()
    {
        if (_core != null) return;

        var user = UserDataManager.Instance;
        if (user == null) return;

        _core = user.Get<RunCoreData>();
        if (_core == null) return;

        _core.OnCoreChanged += Refresh;
    }

    // ── 매 프레임 ────────────────────────────────────────────

    void Update()
    {
        if (_core == null)
        {
            Bind();
            if (_core != null) Refresh();
            return;
        }

        Animate();
    }

    void Animate()
    {
        float ratio = _core.Fill;

        // ── 뚫림 번쩍임 ──
        if (_flash > 0f)
        {
            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime / FlashFade);

            Color c = _breachFlash.color;
            c.a = _flash * FlashPeak;
            _breachFlash.color = c;
        }

        // ── 위험 구간 맥동 ──
        //   ⚠ unscaledDeltaTime 이다 — 일시정지·배속에 맥동이 끌려다니면
        //     "위험하다" 가 배속 표시처럼 읽힌다.
        float glowAlpha = 0f;

        if (ratio <= DangerRatio && ratio > 0f)
        {
            _pulse += Time.unscaledDeltaTime * 3.2f;
            glowAlpha = 0.18f + Mathf.PingPong(_pulse, 0.26f);
        }

        // 번쩍임이 있으면 그쪽이 이긴다 — 방금 맞은 것이 더 급한 소식이다.
        glowAlpha = Mathf.Max(glowAlpha, _flash * 0.75f);

        Color g = _glow.color;
        g.a = glowAlpha;
        _glow.color = g;
    }

    // ── 값 갱신 ──────────────────────────────────────────────

    void Refresh()
    {
        if (_core == null) return;

        // 런이 아직 안 시작됐다 (Max 0) — 막대를 통째로 감춘다.
        // ⚠ 0/0 을 그리면 "이미 함락됨" 으로 읽힌다.
        bool live = _core.Max > 0;
        if (_column.gameObject.activeSelf != live) _column.gameObject.SetActive(live);
        if (!live)
        {
            _shown = -1;
            return;
        }

        int   cur   = _core.Current;
        float ratio = _core.Fill;

        _fill.fillAmount = ratio;
        _fill.color      = ratio <= DangerRatio ? Danger
                         : ratio <= WarnRatio   ? Warn
                         : Safe;

        // ⚠ 숫자는 흰색 고정이다 — 게이지 색을 따라가지 않는다
        //   글자가 바로 뒤의 채움과 같은 색이면 채워진 구간에서 사라진다.
        //   위험은 게이지 색과 맥동이 이미 말한다.
        _text.text = cur.ToString();

        // 줄었을 때만 터뜨린다 — 회복(야영지)은 조용히 넘어간다.
        if (_shown >= 0 && cur < _shown) Breach();

        _shown = cur;
    }

    /// <summary>성벽이 뚫렸다 — 화면이 알아차리게 한다.</summary>
    void Breach()
    {
        _flash = 1f;
        CameraShaker.Impulse(ShakeAmount);
    }
}
