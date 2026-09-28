using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  DifficultyUnlockPopup.cs
//  새 난이도 해금 — 자물쇠가 흔들리다 깨지고 난이도 이름이 튀어나온다.
//
//  ■ 언제 뜨나 (사용자 지시, 2026-09-11)
//    한 난이도에서 20스테이지 이상을 처음 깨면 그 자리에서는 **기록만** 한다
//    (DifficultyData.TryBreakthrough → PendingUnlock). 런이 끝나 보상 상자를
//    **닫은 뒤** 이 창이 뜬다 (RunBootstrap.GrantGearBox → ShowPending).
//    ⚠ 그 사이 앱이 꺼졌으면 다음 실행 때 로비에서 뜬다 (LobbyManager.Start).
//
//  ■ 연출 순서
//    ① 잠긴 아이콘(어둡게) + 자물쇠
//    ② 자물쇠가 점점 세게 흔들린다
//    ③ 자물쇠가 튀어 오르며 사라지고, 아이콘이 난이도 색으로 밝아지며 터진다
//    ④ 난이도 이름이 튀어나오고 설명·보상 배율이 떠오른다 → [확인]
//    ⚠ [확인] 은 ④ 뒤에야 켜진다 — 연출 도중 닫으면 무엇이 열렸는지 모른다.
//    ⚠ Time.unscaledDeltaTime — 런이 끝난 직후라 timeScale 을 믿을 수 없다.
//
//  Inspector 연결은 DifficultyUnlockPopupCreator 가 전부 한다.
// ============================================================

public class DifficultyUnlockPopup : PopupBase
{
    public override bool BlockBackgroundClose => true;

    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] Image           _glow;
    [SerializeField] RectTransform   _iconFrame;
    [SerializeField] Image           _iconFrameImage;
    [SerializeField] Image           _icon;
    [SerializeField] RectTransform   _lock;
    [SerializeField] Graphic[]       _lockGraphics;
    [SerializeField] TextMeshProUGUI _tierName;
    [SerializeField] TextMeshProUGUI _summary;
    [SerializeField] TextMeshProUGUI _reward;
    [SerializeField] Button          _closeBtn;

    static readonly Color LockedTint = new(0.30f, 0.32f, 0.40f, 1f);
    static readonly Color RewardOn   = new(0.45f, 0.86f, 0.62f);

    Vector2   _lockHome;
    Coroutine _play;

    // ── 여는 곳 ──────────────────────────────────────────────

    /// <summary>
    /// 보여 줄 해금이 있으면 연다. 없으면 false.
    ///
    /// ⚠ 창을 실제로 연 **뒤에** 기록을 지운다 — 못 열었으면 다음 실행 때
    ///   로비에서 다시 시도한다(해금 자체는 이미 되어 있다).
    /// </summary>
    public static bool ShowPending()
    {
        var data = UserDataManager.Instance.Get<DifficultyData>();
        if (!data.PeekPendingUnlock(out DifficultyTier tier)) return false;

        var popup = PopupManager.Instance?.Open<DifficultyUnlockPopup>(PopupType.DifficultyUnlock);
        if (popup == null)
        {
            Debug.LogWarning("[DifficultyUnlockPopup] 창을 열지 못했습니다 — 난이도는 이미 열렸습니다. " +
                             "Tools > Project K > 프리팹 생성 > 팝업 > 난이도 해금 → [Load Popup Prefabs]");
            return false;
        }

        data.ClearPendingUnlock();
        UserDataManager.Instance.RequestSave();

        popup.Setup(tier);
        return true;
    }

    // ── 설정 ─────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _lockHome = _lock.anchoredPosition;
        _closeBtn.onClick.AddListener(() => Close());
    }

    void Setup(DifficultyTier tier)
    {
        Color col = DifficultySelectorUI.TierColors[(int)tier];

        _titleText.text = "새 난이도 해금";

        _icon.sprite          = SpriteManager.Instance.Get(tier.IconKey());
        _icon.color           = LockedTint;
        _iconFrameImage.color = LockedTint;

        // ⚠ 매번 되돌린다 — 팝업은 재사용된다
        _lock.gameObject.SetActive(true);
        _lock.anchoredPosition = _lockHome;
        _lock.localRotation    = Quaternion.identity;
        _lock.localScale       = Vector3.one;
        SetLockAlpha(1f);

        _glow.color = new Color(col.r, col.g, col.b, 0f);
        _glow.rectTransform.localScale = Vector3.one * 0.6f;

        _tierName.text  = tier.Label();
        _tierName.color = col;
        _tierName.rectTransform.localScale = Vector3.one;
        SetAlpha(_tierName, 0f);

        _summary.text = tier.Summary();
        SetAlpha(_summary, 0f);

        float mul = DifficultyConfig.Current.Get(tier).ReincarnationMultiplier;
        _reward.text  = LocalizationManager.Instance.Format("환생 포인트 ×{0:0.0#}", mul);
        _reward.color = RewardOn;
        SetAlpha(_reward, 0f);

        _closeBtn.gameObject.SetActive(false);

        if (_play != null) StopCoroutine(_play);
        _play = StartCoroutine(PlayRoutine(col));
    }

    // ── 연출 ─────────────────────────────────────────────────

    IEnumerator PlayRoutine(Color col)
    {
        // 창이 열리는 애니메이션(0.2초)이 끝난 뒤 — 커지는 중에 흔들면 어긋나 보인다
        yield return Wait(0.45f);

        // ② 흔들림 — 뒤로 갈수록 세진다 (GearBoxPopup 과 같은 결)
        const float ShakeDur = 0.75f;
        float t = 0f;
        while (t < ShakeDur)
        {
            t += Time.unscaledDeltaTime;
            float k   = Mathf.Clamp01(t / ShakeDur);
            float amp = Mathf.Lerp(3f, 16f, k);
            float ph  = t * 36f;

            _lock.anchoredPosition = _lockHome + new Vector2(Mathf.Sin(ph) * amp, Mathf.Sin(ph * 1.6f) * amp * 0.3f);
            _lock.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Sin(ph * 0.8f) * amp * 0.9f);

            SetGlow(col, k * 0.25f, Mathf.Lerp(0.6f, 0.9f, k));
            yield return null;
        }

        // ③ 깨짐 — 자물쇠가 튀어 올라 사라지고 아이콘이 밝아진다
        UIJuice.Play(new JuicePreset
        {
            Accent    = col,
            Punch     = 1.32f, PunchTime = 0.42f,
            Sparks    = 30, SparkDist = 250f, SparkSize = 34f,
            Stretch   = 2.2f, Gravity = 30f,
            Rings     = 2,  RingSize = 460f,
            Life      = 0.85f,
            Flash     = 0.22f,
        }, _iconFrame);

        float spin = Random.Range(160f, 280f) * (Random.value < 0.5f ? -1f : 1f);

        const float BreakDur = 0.45f;
        t = 0f;
        while (t < BreakDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / BreakDur);

            _lock.anchoredPosition = _lockHome + new Vector2(0f, 220f * (k * 2f - k * k * 1.6f));
            _lock.localRotation    = Quaternion.Euler(0f, 0f, spin * k);
            _lock.localScale       = Vector3.one * Mathf.Lerp(1f, 1.4f, k);
            SetLockAlpha(1f - k);

            _icon.color           = Color.Lerp(LockedTint, Color.white, k);
            _iconFrameImage.color = Color.Lerp(LockedTint, col, k);
            SetGlow(col, Mathf.Lerp(0.25f, 0.55f, k), Mathf.Lerp(0.9f, 1.25f, k));
            yield return null;
        }
        _lock.gameObject.SetActive(false);

        // ④ 이름이 튀어나온다 — 작게 → 넘치게 → 제자리
        const float PopDur = 0.32f;
        t = 0f;
        var nameRt = _tierName.rectTransform;
        while (t < PopDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / PopDur);
            float s = k < 0.7f ? Mathf.Lerp(0.5f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);

            nameRt.localScale = Vector3.one * s;
            SetAlpha(_tierName, Mathf.Clamp01(k * 2f));
            yield return null;
        }
        nameRt.localScale = Vector3.one;

        const float FadeDur = 0.3f;
        t = 0f;
        while (t < FadeDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeDur);
            SetAlpha(_summary, k);
            SetAlpha(_reward,  k);
            yield return null;
        }

        _closeBtn.gameObject.SetActive(true);

        // 닫기 전까지 빛무리가 숨 쉰다 — 멈춰 있으면 끝난 화면으로 안 읽힌다
        t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float b = Mathf.Sin(t * 2.2f);
            SetGlow(col, 0.45f + b * 0.08f, 1.2f + b * 0.04f);
            yield return null;
        }
    }

    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    void SetGlow(Color col, float alpha, float scale)
    {
        _glow.color = new Color(col.r, col.g, col.b, alpha);
        _glow.rectTransform.localScale = Vector3.one * scale;
    }

    void SetLockAlpha(float a)
    {
        for (int i = 0; i < _lockGraphics.Length; i++) SetAlpha(_lockGraphics[i], a);
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        g.color = new Color(c.r, c.g, c.b, a);
    }

    protected override void OnAfterClose()
    {
        if (_play != null) { StopCoroutine(_play); _play = null; }
    }
}
