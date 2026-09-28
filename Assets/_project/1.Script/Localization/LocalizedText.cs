using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  LocalizedText
//  TMP 의 현재 한국어 문구를 키로 보관하고 언어 변경·동적 갱신 때 번역한다.
//  Creator 가 만든 고정 텍스트에는 EditorUIBuilder 가 이 컴포넌트를 붙인다.
//  기존 프리팹과 런타임 생성 TMP 는 TEXT_CHANGED_EVENT 에서 자동 보완한다.
// ============================================================

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class LocalizedText : MonoBehaviour
{
    public static void FitLabel(TextMeshProUGUI text)
    {
        text.fontSizeMax = Mathf.Max(UIScale.FontSm, text.fontSize);
        text.fontSizeMin = UIScale.FontSm;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Keep fixed popup bounds while allowing translated descriptions to use more lines.
    public static void ScrollDescription(TextMeshProUGUI text)
    {
        var rect = text.rectTransform;
        var viewport = new GameObject(text.name + "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D))
            .GetComponent<RectTransform>();
        viewport.SetParent(rect.parent, false);
        viewport.SetSiblingIndex(rect.GetSiblingIndex());
        viewport.anchorMin = rect.anchorMin;
        viewport.anchorMax = rect.anchorMax;
        viewport.pivot = rect.pivot;
        viewport.sizeDelta = rect.sizeDelta;
        viewport.anchoredPosition = rect.anchoredPosition;
        viewport.GetComponent<Image>().color = Color.clear;
        rect.SetParent(viewport, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        text.enableAutoSizing = false;
        text.fontSize = UIScale.FontSm;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = rect;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = UIScale.RowMd;
    }

    TextMeshProUGUI _text;
    string _sourceText;
    string _renderedText;
    bool _isSplash;
    bool _needsMeshRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallRuntimeHook()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnAnyTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnAnyTextChanged);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            EnsureIn(root);
    }

    /// <summary>
    /// Creator 재생성 전의 기존 씬·프리팹까지 포함해 하위 TMP를 모두 로컬라이징한다.
    /// 이미 붙어 있는 컴포넌트는 그대로 두므로 반복 호출해도 안전하다.
    /// </summary>
    public static void EnsureIn(GameObject root)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (!text.TryGetComponent(out LocalizedText _))
                text.gameObject.AddComponent<LocalizedText>();
    }

    static void OnAnyTextChanged(UnityEngine.Object changed)
    {
        if (!Application.isPlaying || changed is not TextMeshProUGUI text) return;

        var localized = text.GetComponent<LocalizedText>();
        if (localized == null)
        {
            localized = text.gameObject.AddComponent<LocalizedText>();
            localized._needsMeshRefresh = true;
        }
        // Existing components capture assignments in LateUpdate, before TMP builds meshes.
    }

    void Awake()
    {
        _text       = GetComponent<TextMeshProUGUI>();
        _sourceText = LocalizationManager.Instance.SourceForRendered(_text.text);
        _isSplash   = gameObject.scene.name == "Splash";
    }

    void OnEnable()
    {
        LocalizationManager.Instance.LanguageChanged += Refresh;

        // ⚠ 꺼져 있는 동안 코드가 바꿔 놓은 문구를 여기서 다시 잡아야 한다
        //   _sourceText 는 TMP 의 TEXT_CHANGED_EVENT 로만 갱신되는데,
        //   그 이벤트는 메시를 다시 그릴 때 = 활성 상태에서만 온다.
        //   꺼진 채로 .text 를 바꿔 두고 켜는 코드가 많다 —
        //     · InfoTooltipUI.Show()      Fill() 로 내용을 채운 뒤 SetActive(true)
        //     · CurrencyWidget.OnEnable() 부모가 먼저 켜지며 수량을 찍는다
        //   그 상태에서 곧장 Refresh() 하면 옛 _sourceText 로 덮어써
        //   "이전 툴팁 내용" · "전투 전 골드" 가 그대로 남는다.
        if (_text.text != _renderedText)
            _sourceText = LocalizationManager.Instance.SourceForRendered(_text.text);

        Refresh();
    }

    void OnDisable()
    {
        LocalizationManager.Instance.LanguageChanged -= Refresh;
    }

    void CaptureExternalText()
    {
        if (_text.text == _renderedText) return;

        _sourceText = LocalizationManager.Instance.SourceForRendered(_text.text);
        Refresh();
    }

    // TEXT_CHANGED_EVENT runs during TMP mesh generation. Changing text there can
    // lose the dirty flag; capture runtime assignments before the canvas rebuild.
    void LateUpdate()
    {
        CaptureExternalText();
        if (_needsMeshRefresh)
        {
            _needsMeshRefresh = false;
            _text.SetAllDirty();
        }
    }

    void Refresh()
    {
        string translated = LocalizationManager.Instance.LocalizeText(_sourceText, _isSplash);
        _renderedText = translated;

        // ⚠ 글을 넣기 **전에** 맞춤을 정한다 — 넣은 뒤 바꾸면 한 프레임 넘쳐 보인다
        ApplyFit(translated != _sourceText);

        if (_text.text != translated)
            _text.text = translated;
    }

    // ── 번역문 넘침 대응 (2026-09-17, 사용자 지적) ─────────────
    //
    //  칸은 한국어 길이에 맞춰 구워졌다. 영어·독일어·프랑스어는 흔히 1.5~2배라
    //  두 줄로 넘어가거나 칸 밖으로 그려졌다. Creator 를 하나하나 고치는 대신
    //  **번역된 글이 들어간 동안만** 여기서 맞춘다.
    //    · 자동 축소를 켠다 — 상한은 원래 크기, 하한은 원래의 ShrinkFloor 배
    //    · 칸 높이가 한 줄뿐이면 줄바꿈을 끈다 — 두 줄로 넘기지 않고 줄여서 맞춘다
    //  한국어로 돌아오면 원래 설정을 그대로 되돌린다 (한국어 화면은 전과 픽셀 단위로 같다).
    //
    //  ⚠ UI 규칙 4 의 "FontSm 미만 금지" 는 한국어 기준이다. 번역문은 그보다 작아질 수 있다 —
    //    넘쳐서 안 보이는 것보다 작게라도 보이는 쪽이 낫다. 하한이 너무 작으면 칸을 넓힐 것.
    //  ⚠ ContentSizeFitter 가 붙은 글(스크롤 설명 등)은 건드리지 않는다 — 글이 칸을 키우는 구조라
    //    자동 축소와 서로 싸운다.

    //  ■ 2026-09-17 고침 (사용자 스크린샷 — 도감 툴팁·패시브 설명이 한 줄로 쪼그라들어 칸 밖으로 샜다)
    //    ① **레이아웃이 높이를 정하는 글은 건드리지 않는다** — ContentSizeFitter 가 붙었거나,
    //       부모 LayoutGroup 이 자식 높이를 잡는 글(툴팁 설명이 그렇다)은 글이 칸을 키우는 구조다.
    //       레이아웃 전에는 칸이 한 줄 높이라, 예전 규칙이 "한 줄 칸" 으로 오판해 줄바꿈을 껐다.
    //    ② **칸 높이가 바뀌면 다시 판단한다** — 도감 칸은 재사용되며 이름 칸이 한 줄/두 줄로 바뀐다.
    //       예전엔 번역 여부가 바뀔 때만 판단해서 한 번 끈 줄바꿈이 그대로 남았다.
    //    ③ **최소 크기까지 줄여도 넘치면 말줄임(…)** — 칸 밖으로 그리지 않는다.
    //       잘린 전문이 필요한 자리는 그 화면이 툴팁을 준다 (몬스터 상세 패시브 줄 참고).

    const float ShrinkFloorOneLine   = 0.35f;  // 한 줄 칸 — 줄바꿈이 없으니 더 줄여야 담긴다
    const float ShrinkFloorMultiLine = 0.6f;   // 여러 줄 칸 — 줄바꿈이 먼저 받아 준다
    const float MinFontSize          = 18f;
    const float MinFontSizeOneLine   = 12f;
    const float TwoLineRatio         = 2.4f;   // 칸 높이가 글자 크기의 이 배 미만이면 한 줄짜리 칸이다

    bool                   _fitCaptured;
    bool                   _fitApplied;
    bool                   _origAutoSize;
    float                  _origSize, _origMin, _origMax;
    TextWrappingModes      _origWrap;
    TextOverflowModes      _origOverflow;

    void ApplyFit(bool localized)
    {
        if (!localized && !_fitApplied) return;

        if (!_fitCaptured)
        {
            _origAutoSize = _text.enableAutoSizing;
            _origSize     = _text.fontSize;
            _origMin      = _text.fontSizeMin;
            _origMax      = _text.fontSizeMax;
            _origWrap     = _text.textWrappingMode;
            _origOverflow = _text.overflowMode;
            _fitCaptured  = true;
        }

        if (!localized || LayoutDrivesHeight())
        {
            if (_fitApplied) RestoreFit();
            return;
        }

        _fitApplied = true;

        float baseSize = _origAutoSize ? _origMax : _origSize;
        float h        = _text.rectTransform.rect.height;
        bool  oneLine  = _origWrap == TextWrappingModes.NoWrap ||
                         (h > 0f && h < baseSize * TwoLineRatio);

        // ⚠ 한 줄짜리 작은 칸(배지·등급 표시)은 바닥을 더 낮춘다 (사용자 지적, 2026-09-17)
        //   72px 배지에 "Afinidad" 같은 한 단어가 18px 바닥에 걸려 "Afinid..." 로 잘렸다.
        //   한 단어가 잘리면 뜻이 통째로 사라진다 — 작게라도 전부 보이는 쪽이 낫다.
        //   구울 때 자동 축소 바닥을 더 낮게 잡아 둔 칸은 그 값을 따른다.
        float floor = oneLine
            ? Mathf.Max(MinFontSizeOneLine, baseSize * ShrinkFloorOneLine)
            : Mathf.Max(MinFontSize,        baseSize * ShrinkFloorMultiLine);
        if (_origAutoSize && _origMin > 0f) floor = Mathf.Min(floor, _origMin);

        _text.enableAutoSizing = true;
        _text.fontSizeMax      = baseSize;
        _text.fontSizeMin      = floor;
        _text.textWrappingMode = oneLine ? TextWrappingModes.NoWrap : _origWrap;

        if (_origOverflow == TextOverflowModes.Overflow || _origOverflow == TextOverflowModes.Truncate)
            _text.overflowMode = TextOverflowModes.Ellipsis;
    }

    void RestoreFit()
    {
        _fitApplied            = false;
        _text.enableAutoSizing = _origAutoSize;
        _text.fontSizeMin      = _origMin;
        _text.fontSizeMax      = _origMax;
        _text.fontSize         = _origSize;
        _text.textWrappingMode = _origWrap;
        _text.overflowMode     = _origOverflow;
    }

    bool LayoutDrivesHeight()
    {
        if (TryGetComponent(out ContentSizeFitter _)) return true;

        Transform parent = transform.parent;
        return parent != null &&
               parent.TryGetComponent(out HorizontalOrVerticalLayoutGroup group) &&
               group.childControlHeight;
    }
}
