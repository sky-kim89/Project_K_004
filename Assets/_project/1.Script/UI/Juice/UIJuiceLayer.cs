using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  UIJuiceLayer.cs
//  성장 연출 전용 UI 레이어 — 파티클·링·라벨을 화면 최상단에 그린다.
//
//  ■ 왜 전용 레이어인가
//    연출은 팝업 위로 넘쳐야 한다. 팝업 안에 붙이면 패널 경계에서 잘리고,
//    팝업이 닫히는 순간 연출도 같이 사라진다 (강화하고 바로 닫으면 아무것도 못 본다).
//    sortingOrder 900 — 로비(0)·인게임(10)·팝업(200) 위, 튜토리얼(1000) 아래다.
//    튜토리얼이 도는 중에는 설명이 우선이므로 그 아래에 둔다.
//
//  ■ 프리팹도 텍스처도 요구하지 않는다
//    점·링 두 장을 코드로 그려 정적으로 캐시한다. 아티스트 손이 안 닿아도
//    돌아가고, 에셋 참조가 끊겨 연출만 조용히 사라지는 일이 없다.
//
//  ■ 파티클은 풀에서 돌려 쓴다
//    한 번 강화에 20개씩 만들고 버리면 연타할 때 GC 가 튄다.
//    Image 를 비활성으로 재워 두고 꺼내 쓴다.
//
//  ⚠ 시간은 전부 unscaledDeltaTime 이다
//    팝업은 timeScale 0 위에서도 뜬다 (PausePopup·튜토리얼). 스케일 시간을 쓰면
//    연출이 멈춘 채로 남아 파티클이 화면에 얼어붙는다.
//
//  ⚠ 목표의 스케일은 반드시 되돌린다
//    punch 는 대상의 localScale 을 직접 건드린다. 도중에 팝업이 닫히거나
//    RefreshUI 가 돌아도 원래 값으로 돌아가야 한다. 원본 스케일을 정적 표에
//    적어 두고, 같은 대상에 연출이 겹쳐 들어와도 원본을 덮어쓰지 않는다.
//    (덮어쓰면 1.2배가 원본으로 굳어 카드가 점점 커진다)
// ============================================================

public class UIJuiceLayer : MonoBehaviour
{
    const int   SortingOrder = 900;
    const float RefW = 1920f, RefH = 1080f;

    // ── 싱글턴 (씬 배치 불필요) ───────────────────────────────
    static UIJuiceLayer _instance;

    public static UIJuiceLayer Ensure()
    {
        if (_instance != null) return _instance;

        var go = new GameObject(nameof(UIJuiceLayer), typeof(RectTransform));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<UIJuiceLayer>();
        _instance.Build();
        return _instance;
    }

    // ── 구성 ─────────────────────────────────────────────────
    Canvas        _canvas;
    RectTransform _root;
    Image         _flash;

    readonly Stack<Image>           _pool  = new();
    readonly List<Image>            _live  = new();
    TextMeshProUGUI                 _labelTemplate;
    readonly Stack<TextMeshProUGUI> _labelPool = new();

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        // ⚠ GraphicRaycaster 를 붙이지 않는다
        //   연출은 보여 주기만 한다. 레이캐스터가 있으면 이 레이어가 화면 전체를
        //   덮고 있는 동안 아래 버튼이 안 눌린다.
        _root = GetComponent<RectTransform>();

        _flash = MakeImage("Flash", null);
        Stretch(_flash.rectTransform);
        _flash.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════
    //  재생
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 연출 하나를 재생한다. anchor 가 없으면 화면 중앙에서 터진다.
    ///
    /// screenPos 를 주면 그 자리에서 터진다 — anchor 가 아직 제자리에 놓이기 전일 때
    /// (레이아웃 그룹이 이번 프레임 끝에 정렬하는 카드 등) 쓴다.
    /// </summary>
    public void Play(in JuicePreset p, RectTransform anchor, string label, Vector2? screenPos = null)
    {
        Vector2 center = screenPos.HasValue ? LocalFromScreen(screenPos.Value)
                                            : ResolveOrigin(anchor);

        if (anchor != null && p.Punch > 1f)
            StartCoroutine(PunchRoutine(anchor, p.Punch, p.PunchTime));

        for (int i = 0; i < p.Rings; i++)
            StartCoroutine(RingRoutine(center, p, i * 0.09f));

        StartCoroutine(SparkBurst(center, p));

        if (p.Flash > 0f) StartCoroutine(FlashRoutine(p));

        if (!string.IsNullOrEmpty(label))
            StartCoroutine(LabelRoutine(center, p, label));
    }

    // ── 목표 펀치 ────────────────────────────────────────────
    //
    //  ⚠ 원본 스케일은 "처음 잡힌 값" 만 믿는다
    //    연타하면 이미 1.2배로 늘어난 상태에서 두 번째 연출이 들어온다.
    //    그때 현재 값을 원본으로 잡으면 되돌릴 자리가 계속 커진다.

    static readonly Dictionary<Transform, Vector3> s_baseScale = new();

    // 지금 그 대상에서 돌고 있는 펀치 수.
    //
    //  ⚠ 먼저 끝난 연출이 원본 기록을 지우면 안 된다
    //    예전에는 코루틴이 끝날 때마다 s_baseScale 에서 키를 지웠다. 연타하면
    //    A 가 먼저 끝나면서 키를 지우는데 B 는 아직 커진 상태로 돌고 있어서,
    //    그때 들어온 C 가 **부풀어 있는 값**을 원본으로 기록했다.
    //    C 가 끝나며 그 값으로 되돌리므로 대상이 영구히 커진 채로 남고,
    //    누를수록 조금씩 더 커졌다 (레벨업 버튼이 커지던 원인).
    //    마지막 하나가 끝날 때만 되돌리고 지운다.
    static readonly Dictionary<Transform, int> s_punchCount = new();

    static IEnumerator PunchRoutine(RectTransform target, float peak, float dur)
    {
        if (!s_baseScale.TryGetValue(target, out var baseScale))
        {
            baseScale = target.localScale;
            s_baseScale[target] = baseScale;
        }

        s_punchCount.TryGetValue(target, out int running);
        s_punchCount[target] = running + 1;

        float t = 0f;
        while (t < dur)
        {
            // 대상이 파괴됐거나 꺼졌다 — 되돌릴 대상이 없다.
            // 풀에 반납된 팝업이 다음에 열릴 때 커진 채로 나오지 않게 기록도 지운다.
            if (target == null || !target.gameObject.activeInHierarchy) break;

            t += Time.unscaledDeltaTime;
            float k = Punch01(Mathf.Clamp01(t / dur));
            target.localScale = baseScale * Mathf.LerpUnclamped(1f, peak, k);
            yield return null;
        }

        if (target == null) { s_baseScale.Remove(target); s_punchCount.Remove(target); yield break; }

        int left = s_punchCount[target] - 1;
        if (left > 0) { s_punchCount[target] = left; yield break; }

        target.localScale = baseScale;
        s_baseScale.Remove(target);
        s_punchCount.Remove(target);
    }

    // ── 스파크 ───────────────────────────────────────────────
    //
    //  ⚠ 전부 같은 값이면 파티클이 아니라 도형으로 보인다
    //    각도·속도·크기·수명을 개별로 흔들어야 "터졌다" 로 읽힌다.
    //  ⚠ 살짝 아래로 떨어뜨린다
    //    직선으로만 뻗으면 폭죽이 아니라 방사형 아이콘처럼 보인다.

    IEnumerator SparkBurst(Vector2 center, JuicePreset p)
    {
        int n = p.Sparks;
        var dir   = new Vector2[n];
        var dist  = new float[n];
        var size  = new float[n];
        var life  = new float[n];
        var imgs  = new Image[n];

        float step = 360f / n;
        for (int i = 0; i < n; i++)
        {
            float ang = (i * step + Random.Range(-step * 0.4f, step * 0.4f)) * Mathf.Deg2Rad;
            dir[i]  = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            dist[i] = p.SparkDist * Random.Range(0.62f, 1.28f);
            size[i] = p.SparkSize * Random.Range(0.65f, 1.3f);
            life[i] = p.Life      * Random.Range(0.75f, 1.15f);

            imgs[i] = Rent(UIJuiceTex.Dot);
            imgs[i].color = Color.Lerp(p.Accent, Color.white, Random.Range(0f, 0.55f));
        }

        float maxLife = 0f;
        foreach (float l in life) maxLife = Mathf.Max(maxLife, l);

        float t = 0f;
        while (t < maxLife)
        {
            t += Time.unscaledDeltaTime;

            for (int i = 0; i < n; i++)
            {
                var img = imgs[i];
                if (img == null) continue;

                float k = Mathf.Clamp01(t / life[i]);
                float e = EaseOutCubic(k);

                Vector2 pos = center + dir[i] * dist[i] * e;
                pos.y -= p.Gravity * k * k;

                var rt = img.rectTransform;
                rt.anchoredPosition = pos;

                // 날아가는 방향으로 늘여 준다 — 점이 아니라 잔상으로 읽힌다
                float stretch = 1f + (1f - e) * p.Stretch;
                float s = size[i] * (1f - k * 0.7f);
                rt.sizeDelta      = new Vector2(s * stretch, s);
                rt.localRotation  = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir[i].y, dir[i].x) * Mathf.Rad2Deg);

                var c = img.color;
                c.a = k < 0.25f ? Mathf.InverseLerp(0f, 0.25f, k) : 1f - EaseInCubic(Mathf.InverseLerp(0.25f, 1f, k));
                img.color = c;
            }

            yield return null;
        }

        for (int i = 0; i < n; i++) Return(imgs[i]);
    }

    // ── 충격파 링 ────────────────────────────────────────────

    IEnumerator RingRoutine(Vector2 center, JuicePreset p, float delay)
    {
        if (delay > 0f)
        {
            float d = 0f;
            while (d < delay) { d += Time.unscaledDeltaTime; yield return null; }
        }

        var img = Rent(UIJuiceTex.Ring);
        var rt  = img.rectTransform;
        rt.anchoredPosition = center;
        rt.localRotation    = Quaternion.identity;

        float dur = p.Life * 0.85f;
        float t   = 0f;
        while (t < dur)
        {
            if (img == null) yield break;
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / dur);
            float e = EaseOutCubic(k);

            float s = Mathf.LerpUnclamped(p.RingSize * 0.3f, p.RingSize, e);
            rt.sizeDelta = new Vector2(s, s);

            var c = p.Accent;
            c.a = (1f - e) * 0.9f;
            img.color = c;

            yield return null;
        }

        Return(img);
    }

    // ── 화면 섬광 ────────────────────────────────────────────
    //
    //  ⚠ 세게 넣지 않는다
    //    성장 한 번에 화면이 하얗게 번쩍이면 두 번째부터는 피로해진다.
    //    가장 큰 순간(등급업)에만, 그것도 알파 0.2 아래로 스친다.

    IEnumerator FlashRoutine(JuicePreset p)
    {
        _flash.gameObject.SetActive(true);
        _flash.transform.SetAsLastSibling();

        const float Dur = 0.3f;
        float t = 0f;
        while (t < Dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Dur);

            var c = p.Accent;
            c.a = Mathf.Sin(k * Mathf.PI) * p.Flash;
            _flash.color = c;
            yield return null;
        }

        _flash.gameObject.SetActive(false);
    }

    // ── 떠오르는 라벨 ────────────────────────────────────────

    IEnumerator LabelRoutine(Vector2 center, JuicePreset p, string text)
    {
        var tmp = RentLabel();
        tmp.text     = text;
        tmp.fontSize = p.LabelSize;
        tmp.color    = p.Accent;

        var rt = tmp.rectTransform;
        rt.sizeDelta = new Vector2(600f, UIScale.Line(p.LabelSize));

        float dur = p.Life * 1.5f;
        float t   = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);

            // 튀어나왔다가 천천히 떠오른다
            float pop   = k < 0.22f ? EaseOutBack(k / 0.22f) : 1f;
            float rise  = EaseOutCubic(k) * p.LabelRise;

            rt.anchoredPosition = center + new Vector2(0f, p.LabelOffset + rise);
            rt.localScale       = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, pop);

            var c = p.Accent;
            c.a = k < 0.6f ? 1f : 1f - EaseInCubic(Mathf.InverseLerp(0.6f, 1f, k));
            tmp.color = c;

            yield return null;
        }

        ReturnLabel(tmp);
    }

    // ══════════════════════════════════════════════════════════
    //  풀 · 유틸
    // ══════════════════════════════════════════════════════════

    Image Rent(Sprite sprite)
    {
        Image img = _pool.Count > 0 ? _pool.Pop() : MakeImage("Particle", null);
        img.sprite            = sprite;
        img.rectTransform.localScale = Vector3.one;
        img.gameObject.SetActive(true);
        img.transform.SetAsLastSibling();
        _live.Add(img);
        return img;
    }

    void Return(Image img)
    {
        if (img == null) return;
        img.gameObject.SetActive(false);
        _live.Remove(img);
        _pool.Push(img);
    }

    Image MakeImage(string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_root, false);

        var img = go.GetComponent<Image>();
        img.sprite        = sprite;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return img;
    }

    TextMeshProUGUI RentLabel()
    {
        TextMeshProUGUI tmp;
        if (_labelPool.Count > 0) tmp = _labelPool.Pop();
        else
        {
            var go = new GameObject("JuiceLabel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.fontStyle        = FontStyles.Bold;
            tmp.raycastTarget    = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode     = TextOverflowModes.Overflow;

            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
        }

        tmp.gameObject.SetActive(true);
        tmp.transform.SetAsLastSibling();
        return tmp;
    }

    void ReturnLabel(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        tmp.gameObject.SetActive(false);
        _labelPool.Push(tmp);
    }

    // ── 터지는 자리 ──────────────────────────────────────────
    //
    //  ⚠ 손가락이 닿은 자리에서 터져야 "내가 눌러서 일어난 일" 로 읽힌다
    //    버튼 중앙에서만 터뜨리면 큰 버튼일수록 손가락과 어긋나 남의 연출처럼 보인다.
    //
    //  ⚠ 그래도 포인터를 그대로 믿지는 않는다
    //    키보드·치트·코드 호출로 들어오면 포인터는 엉뚱한 곳(직전에 만진 자리)에
    //    멈춰 있다. 그 좌표로 터뜨리면 화면 반대편에서 폭죽이 튄다.
    //    포인터가 그 버튼 위일 때만 쓰고, 아니면 버튼 중앙으로 떨어뜨린다.

    Vector2 ResolveOrigin(RectTransform anchor)
    {
        if (anchor == null) return Vector2.zero;

        Vector2 pointer = PointerScreenPos();
        if (RectTransformUtility.RectangleContainsScreenPoint(anchor, pointer, null))
            return LocalFromScreen(pointer);

        return LocalCenterOf(anchor);
    }

    /// <summary>화면 좌표를 이 레이어의 로컬 좌표로 옮긴다.</summary>
    Vector2 LocalFromScreen(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        return local;
    }

    /// <summary>
    /// 마지막으로 화면을 만진 자리.
    ///
    /// ⚠ 터치가 이미 끝난 뒤에 불린다
    ///   onClick 은 손을 뗄 때 발생하므로 touchCount 가 0 인 경우가 많다.
    ///   Unity 는 그때 mousePosition 에 마지막 터치 좌표를 남겨 두므로 그걸 쓴다.
    /// </summary>
    public static Vector2 PointerScreenPos()
    {
        if (Input.touchCount > 0) return Input.GetTouch(Input.touchCount - 1).position;
        return Input.mousePosition;
    }

    /// <summary>대상의 화면 중심을 이 레이어의 로컬 좌표로 옮긴다.</summary>
    Vector2 LocalCenterOf(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 world = (corners[0] + corners[2]) * 0.5f;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        return local;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── 이징 ─────────────────────────────────────────────────
    //
    //  선형으로 움직이는 UI 는 기계처럼 보인다. 시작은 빠르고 끝은 느리게 —
    //  그 차이가 "손맛" 의 대부분이다.

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    static float EaseInCubic(float t)  => t * t * t;

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>0 → 1 → 0. 빠르게 커졌다 천천히 돌아온다.</summary>
    static float Punch01(float t)
    {
        const float Up = 0.3f;
        return t < Up
            ? EaseOutCubic(t / Up)
            : 1f - EaseOutCubic((t - Up) / (1f - Up));
    }
}
