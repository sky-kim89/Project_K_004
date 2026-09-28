using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  GearBoxPopup.cs
//  런 종료 보상 상자 — 누르면 흔들리고, 뚜껑이 날아가고, 장비가 튀어나온다.
//
//  ■ ⚠ 이 화면은 연출이다 — 보상은 이미 들어가 있다
//    무엇이 나올지는 열기 전에 MonsterGearRewardRule.GrantForStage 가 정해서
//    인벤토리에 넣는다. 여기서 다시 뽑지 않는다.
//    그래서 연출 도중에 앱이 꺼져도 보상이 사라지지 않고, '안 연 상자' 라는
//    상태를 세이브에 들고 다닐 이유도 없다.
//
//  ■ 등급이 곧 연출의 무게다 (사용자 요청, 2026-09-09)
//    세기의 정본은 **LookOf 표 하나**다 — 흔들림·빛살·불꽃·섬광이 전부 거기서
//    나온다. 여기저기에 "등급이 X 이상이면" 을 흩어 두면 "영웅인데 왜 일반과
//    비슷하지" 를 고칠 때 다섯 군데를 찾아야 한다.
//      일반 : 조용히 흔들리다 열린다 (빛살 없음)
//      영웅 : 빛살이 돌고 화면이 하얗게 터지며 불꽃 24개가 퍼진다
//    ⚠ 표를 손볼 때는 한 값이 아니라 **한 행**을 옮길 것 — 다섯 값이 한 인상을
//      만든다. 섬광만 올리면 번쩍이기만 하고 무게는 그대로다.
//
//  ■ 같은 등급이어도 매번 조금씩 다르다 (사용자 요청 — 랜덤성)
//    흔들림 위상·길이(±8%) · 뚜껑이 도는 방향과 날아가는 쪽 · 불꽃의 각도와
//    속도와 크기 · 빛살의 시작 각도가 매번 굴러 나온다. 고정이면 두 번째부터는
//    "봤던 것" 이 된다.
//    ⚠ **무엇이 나오는지는 굴리지 않는다** — 그건 이미 정해져 들어와 있다.
//      연출이 결과를 정하는 것처럼 보이면 상자를 다시 열고 싶어진다.
//
//  ■ 흔들림은 코루틴이다 — 트윈 라이브러리를 쓰지 않는다
//    이 프로젝트에 트윈 라이브러리가 없다. 성장 연출(UIJuice)도 코루틴이다.
//
//    ⚠ Time.unscaledDeltaTime 이다
//      런이 끝난 직후라 timeScale 이 어떤 값일지 보장할 수 없다.
//      PopupBase 의 열기·닫기 애니메이션도 같은 이유로 unscaled 를 쓴다.
//
//  ■ 세 단계로 나뉜다
//    ① 상자만 보인다 — 숨 쉬듯 커졌다 작아진다. "눌러서 열기"
//    ② 흔들림 → 섬광 → 뚜껑이 날고 불꽃이 퍼진다
//    ③ 장비 카드가 튀어나온다 (작게 → 넘치게 → 제자리)
//    ⚠ 흔드는 동안 다시 못 누르게 막는다. 두 번 누르면 코루틴이 겹쳐
//      상자가 제자리로 돌아오지 못한다.
//
//  ⚠ 색·이름을 손으로 적지 않는다 — GradeStyle 이 정본이다.
//
//  Inspector 연결은 GearBoxPopupCreator 가 전부 자동으로 한다.
//  ⚠ 필드를 늘리면 Creator 도 함께 고치고 **프리팹을 다시 구울 것** —
//    연결이 비면 열자마자 예외가 난다 (방어 null 체크를 두지 않는다).
// ============================================================

public class GearBoxPopup : PopupBase
{
    // 상자를 열기 전에 닫아 버리면 무엇을 받았는지 모른 채 넘어간다.
    public override bool BlockBackgroundClose => true;

    [Header("헤더")]
    [SerializeField] TextMeshProUGUI _titleText;    // "스테이지 12 보상"
    [SerializeField] TextMeshProUGUI _gradeLine;    // "희귀 상자" — 열기 전에도 무게는 안다

    [Header("상자 (열기 전)")]
    [SerializeField] GameObject      _boxRoot;
    [SerializeField] Button          _boxButton;
    [SerializeField] Image           _boxImage;
    [SerializeField] RectTransform   _lid;          // 뚜껑 — 열릴 때 날아간다
    [SerializeField] Image           _lidImage;
    [SerializeField] TextMeshProUGUI _hintText;     // "눌러서 열기"

    [Header("연출")]
    [SerializeField] Image           _boxGlow;      // 상자 뒤 빛무리
    [SerializeField] RectTransform   _rays;         // 도는 빛살 뭉치
    [SerializeField] Image[]         _rayImages;    // 빛살 하나하나 (진하기를 등급이 정한다)
    [SerializeField] RectTransform[] _sparks;       // 터질 때 퍼지는 불꽃
    [SerializeField] Image[]         _sparkImages;
    [SerializeField] Image           _flash;        // 화면 전체 섬광

    [Header("장비 (열린 뒤)")]
    [SerializeField] GameObject      _gearRoot;
    [SerializeField] Image           _gearFrame;    // 등급색 테두리
    [SerializeField] Image           _gearIcon;
    [SerializeField] TextMeshProUGUI _gearName;
    [SerializeField] TextMeshProUGUI _gearGrade;    // "희귀 · 갑옷 · 인간형"
    [SerializeField] TextMeshProUGUI _gearStat;
    [SerializeField] TextMeshProUGUI _gearDesc;

    [Header("닫기")]
    // ── 여러 상자 (2개 이상일 때만 쓴다, 2026-09-15) ──────────
    [SerializeField] GameObject        _multiRoot;
    [SerializeField] Button            _multiButton;
    [SerializeField] TextMeshProUGUI   _multiHint;
    [SerializeField] RectTransform[]   _multiSlots;
    [SerializeField] Image[]           _multiBoxes;
    [SerializeField] RectTransform[]   _multiLids;
    [SerializeField] Image[]           _multiLidImages;
    [SerializeField] GameObject[]      _multiGearRoots;
    [SerializeField] Image[]           _multiFrames;
    [SerializeField] Image[]           _multiIcons;
    [SerializeField] TextMeshProUGUI[] _multiNames;

    [SerializeField] Button          _closeBtn;
    [SerializeField] TextMeshProUGUI _closeLabel;

    // ── 등급이 정하는 것 — 이 표가 정본이다 ──────────────────
    //
    //  ⚠ 한 행이 한 인상이다. 값 하나만 옮기지 말 것 (파일 머리 주석).

    readonly struct BoxLook
    {
        public readonly float ShakeDur;    // 흔드는 시간(초)
        public readonly float ShakeAmp;    // 흔들림 진폭(px)
        public readonly float RaySpin;     // 빛살이 도는 속도(°/초)
        public readonly float RayAlpha;    // 빛살 진하기 (0 = 아예 없다)
        public readonly float FlashAlpha;  // 터질 때 화면이 하얘지는 정도
        public readonly int   Sparks;      // 퍼지는 불꽃 수

        public BoxLook(float dur, float amp, float spin, float rayA, float flash, int sparks)
        {
            ShakeDur = dur; ShakeAmp = amp; RaySpin = spin;
            RayAlpha = rayA; FlashAlpha = flash; Sparks = sparks;
        }
    }

    static BoxLook LookOf(int tier) => tier switch
    {
        0 => new BoxLook(0.55f, 10f,  40f, 0.00f, 0.18f,  6),   // 일반
        1 => new BoxLook(0.72f, 15f,  75f, 0.05f, 0.28f, 10),   // 고급
        2 => new BoxLook(0.92f, 21f, 115f, 0.09f, 0.40f, 15),   // 희귀
        3 => new BoxLook(1.12f, 27f, 165f, 0.13f, 0.52f, 19),   // 유일
        _ => new BoxLook(1.38f, 34f, 235f, 0.18f, 0.68f, 24),   // 영웅
    };

    /// <summary>터지는 시간(초). 이 뒤에 장비가 나온다.</summary>
    const float BurstTime = 0.55f;

    /// <summary>장비 카드가 튀어나오는 시간(초).</summary>
    const float PopTime = 0.30f;

    MonsterGearData _gear;
    bool            _opening;     // 흔드는 중 — 두 번 눌리지 않게 막는다
    Coroutine       _idle;        // 누르기 전의 숨쉬기

    // ── 열기 ─────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _closeBtn.onClick.AddListener(OnConfirm);
        _boxButton.onClick.AddListener(OpenBox);

        // ⚠ 프리팹을 아직 다시 굽지 않았을 수 있다 — 없으면 단일 모드로만 돈다
        if (_multiButton != null) _multiButton.onClick.AddListener(OpenAllBoxes);
    }

    // ── 여러 상자 — 한 창이 차례로 연다 (난이도가 높으면 최대 5개 + 유물) ──
    //
    //  ⚠ 상자마다 팝업을 새로 열지 않는다
    //    닫기 콜백에서 같은 PopupType 을 다시 열면 닫기가 끝나기 전이라
    //    PopupManager 가 인스턴스를 한 벌 더 만든다. 같은 창을 그 자리에서 갈아 끼운다.

    readonly List<MonsterGearData> _queue = new();
    int _total = 1;
    int _stage;

    /// <summary>상자 하나. gear 는 이미 인벤토리에 들어가 있다.</summary>
    public GearBoxPopup Setup(MonsterGearData gear, int stage)
    {
        _queue.Clear();
        _total = 1;
        _stage = stage;
        if (_multiRoot != null) _multiRoot.SetActive(false);
        Show(gear);
        return this;
    }

    /// <summary>
    /// 상자 여럿. 전부 이미 지급돼 있다.
    ///
    /// ■ 개수가 방식을 고른다 (사용자 지시, 2026-09-15)
    ///   하나면 지금까지의 큰 상자 하나, 둘 이상이면 나란히 놓고 **한 번에** 연다.
    ///   전에는 둘 이상도 [다음 상자] 로 차례로 넘겼다 — 다섯 개를 받으면 같은 연출을
    ///   다섯 번 보고 다섯 번 눌러야 해서, 보상이 아니라 절차가 됐다.
    ///
    /// ⚠ 하나일 때 굳이 격자를 쓰지 않는다
    ///   큰 상자 하나가 빛살·불꽃·섬광까지 다 쓰는 연출이고, 그게 이 창의 무게다.
    ///   한 칸짜리 격자로 대신하면 상자 하나를 받은 런이 초라해진다.
    /// </summary>
    public GearBoxPopup SetupMany(IReadOnlyList<MonsterGearData> gears, int stage)
    {
        if (gears == null || gears.Count == 0) { Close(); return this; }
        if (gears.Count == 1 || _multiRoot == null) return Setup(gears[0], stage);

        _queue.Clear();
        _total = gears.Count;
        _stage = stage;
        ShowMany(gears);
        return this;
    }

    /// <summary>[확인] / [다음 상자] — 남은 상자가 있으면 창을 닫지 않고 갈아 끼운다.</summary>
    void OnConfirm()
    {
        if (_queue.Count == 0) { Close(); return; }

        MonsterGearData next = _queue[0];
        _queue.RemoveAt(0);
        Show(next);
    }

    // ══════════════════════════════════════════════════════════
    //  여러 상자 — 나란히 놓고 한 번에 연다
    // ══════════════════════════════════════════════════════════

    /// <summary>격자 한 칸의 크기·간격 — 정본은 GearBoxPopupCreator 다. 여기는 읽기만 한다.</summary>
    const int MultiCols = 3;

    readonly List<MonsterGearData> _many = new();
    bool _multiOpening;

    void ShowMany(IReadOnlyList<MonsterGearData> gears)
    {
        _many.Clear();
        for (int i = 0; i < gears.Count && i < _multiSlots.Length; i++) _many.Add(gears[i]);

        _multiOpening = false;

        // 단일 모드 물건은 전부 끈다 — 같은 무대를 쓴다.
        _boxRoot.SetActive(false);
        _gearRoot.SetActive(false);
        _rays.gameObject.SetActive(false);
        SetGlow(Color.white, 0f, 1f);
        SetAlpha(_flash, 0f);
        for (int i = 0; i < _sparks.Length; i++) _sparks[i].gameObject.SetActive(false);

        _multiRoot.SetActive(true);
        _multiButton.interactable = true;
        _multiHint.text = "눌러서 모두 열기";
        _multiHint.gameObject.SetActive(true);

        _titleText.text = LocalizationManager.Instance.Format("스테이지 {0} 보상  ({1}개)", _stage, _many.Count);

        // 등급 줄은 **가장 좋은 상자**를 말한다 — 여럿이라 하나로 줄여야 한다.
        MonsterGearData best = _many[0];
        for (int i = 1; i < _many.Count; i++)
            if ((int)_many[i].Grade > (int)best.Grade) best = _many[i];

        _gradeLine.text  = LocalizationManager.Instance.Format("최고 {0} 상자", GradeStyle.GetLabel(best.Grade));
        _gradeLine.color = GradeStyle.GetColor(best.Grade);

        LayoutMulti(_many.Count);

        for (int i = 0; i < _multiSlots.Length; i++)
        {
            bool used = i < _many.Count;
            _multiSlots[i].gameObject.SetActive(used);
            if (!used) continue;

            Color grade = GradeStyle.GetColor(_many[i].Grade);

            // ⚠ 매번 되돌린다 — 팝업은 재사용된다 (단일 모드와 같은 이유)
            _multiBoxes[i].gameObject.SetActive(true);
            _multiBoxes[i].color = grade;
            _multiBoxes[i].rectTransform.anchoredPosition = Vector2.zero;
            _multiBoxes[i].rectTransform.localScale       = Vector3.one;

            _multiLids[i].gameObject.SetActive(true);
            _multiLids[i].anchoredPosition = Vector2.zero;
            _multiLids[i].localRotation    = Quaternion.identity;
            _multiLidImages[i].color       = grade;

            _multiGearRoots[i].SetActive(false);
            _multiGearRoots[i].transform.localScale = Vector3.one;

            _multiFrames[i].color = grade;

            _multiIcons[i].sprite  = _many[i].Icon;
            _multiIcons[i].enabled = _many[i].Icon != null;

            _multiNames[i].text    = _many[i].DisplayName;
            _multiNames[i].color   = grade;
            _multiNames[i].enabled = false;          // 열린 뒤에 나온다
        }

        _closeBtn.gameObject.SetActive(false);
        _closeLabel.text = "확인";

        if (_idle != null) StopCoroutine(_idle);
        _idle = StartCoroutine(MultiIdleRoutine());
    }

    /// <summary>
    /// 개수만큼 칸을 가운데로 모은다.
    ///
    /// ⚠ 굽는 시점에는 개수를 모른다 — 난이도·유물이 정한다.
    ///   그래서 Creator 는 6칸을 만들어 두고 자리는 여기서 잡는다
    ///   (ChoicePopup.Recenter 와 같은 문법).
    /// </summary>
    void LayoutMulti(int count)
    {
        int rows = Mathf.CeilToInt(count / (float)MultiCols);

        float slotW = _multiSlots[0].sizeDelta.x;
        float slotH = _multiSlots[0].sizeDelta.y;
        float gapY  = 14f;

        float totalH = rows * slotH + (rows - 1) * gapY;
        float topY   = totalH * 0.5f - slotH * 0.5f;

        for (int i = 0; i < count; i++)
        {
            int row = i / MultiCols;
            int col = i % MultiCols;

            // 마지막 줄은 남은 개수만큼만 — 4개면 [3][1] 이 아니라 [3] 아래 [1]이 가운데다.
            int inRow = Mathf.Min(MultiCols, count - row * MultiCols);

            float x = (col - (inRow - 1) * 0.5f) * slotW;
            float y = topY - row * (slotH + gapY);

            _multiSlots[i].anchoredPosition = new Vector2(x, y);
        }
    }

    /// <summary>누르기 전 — 상자 전부가 같이 숨을 쉰다.</summary>
    IEnumerator MultiIdleRoutine()
    {
        float t = 0f;

        while (!_multiOpening)
        {
            t += Time.unscaledDeltaTime;

            for (int i = 0; i < _many.Count; i++)
            {
                // ⚠ 위상을 칸마다 어긋낸다 — 같이 뛰면 한 덩어리로 보인다
                float breathe = Mathf.Sin(t * 2.2f + i * 0.5f);
                _multiSlots[i].localScale = Vector3.one * (1f + breathe * 0.025f);
            }

            yield return null;
        }
    }

    /// <summary>[눌러서 모두 열기] — 전부 한 번에 터진다.</summary>
    void OpenAllBoxes()
    {
        if (_multiOpening) return;

        _multiOpening = true;
        _multiButton.interactable = false;
        _multiHint.gameObject.SetActive(false);

        if (_idle != null) { StopCoroutine(_idle); _idle = null; }

        StartCoroutine(OpenAllRoutine());
    }

    IEnumerator OpenAllRoutine()
    {
        // ── ① 전부 함께 흔들린다 ──
        //
        //  ⚠ 흔드는 것은 **상자**지 칸이 아니다 — 칸을 흔들면 이름표까지 같이 떨려
        //    무엇을 받았는지 읽는 줄이 흔들린다.
        //  ⚠ 단일 상자는 등급마다 세기가 다르다(BoxLook). 여기는 한 창에 등급이
        //    섞여 있어 하나로 잡는다 — 칸마다 다르게 흔들면 그냥 어수선해 보인다.
        const float MultiShakeTime = 0.45f;

        float t = 0f;
        while (t < MultiShakeTime)
        {
            t += Time.unscaledDeltaTime;
            float amp = Mathf.Lerp(3f, 12f, t / MultiShakeTime);

            for (int i = 0; i < _many.Count; i++)
            {
                var rt = _multiBoxes[i].rectTransform;
                rt.anchoredPosition = new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp));
            }

            yield return null;
        }

        // ── ② 뚜껑이 날아가고 장비가 선다 ──
        //
        //  ⚠ 칸마다 아주 작은 시차를 준다 (StaggerStep)
        //    완전히 동시에 터지면 여섯 개가 한 번 번쩍이고 끝이라 무엇이 나왔는지
        //    눈이 못 따라간다. 0.05초면 "한 번에 열렸다" 는 느낌은 그대로면서
        //    시선이 왼쪽에서 오른쪽으로 흐른다.
        const float StaggerStep = 0.05f;

        SetAlpha(_flash, 0.5f);

        for (int i = 0; i < _many.Count; i++)
        {
            StartCoroutine(BurstOne(i));
            yield return new WaitForSecondsRealtime(StaggerStep);
        }

        // 섬광이 사라진다
        float f = 0.5f;
        while (f > 0f)
        {
            f -= Time.unscaledDeltaTime * 1.6f;
            SetAlpha(_flash, Mathf.Max(0f, f));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(PopTime);

        _closeBtn.gameObject.SetActive(true);
    }

    /// <summary>한 칸이 터진다 — 뚜껑이 날고 장비가 튀어나온다.</summary>
    IEnumerator BurstOne(int i)
    {
        var box = _multiBoxes[i].rectTransform;
        var lid = _multiLids[i];

        box.anchoredPosition = Vector2.zero;

        float dir = Random.value < 0.5f ? -1f : 1f;
        float t   = 0f;

        _multiGearRoots[i].SetActive(true);
        _multiNames[i].enabled = true;

        while (t < PopTime)
        {
            t += Time.unscaledDeltaTime;
            float k = t / PopTime;

            // 뚜껑 — 위로 날며 돈다
            lid.anchoredPosition = new Vector2(dir * 40f * k, 70f * k);
            lid.localRotation    = Quaternion.Euler(0f, 0f, dir * 90f * k);

            // 상자 — 쪼그라들며 사라진다
            box.localScale = Vector3.one * Mathf.Max(0f, 1f - k * 1.4f);

            // 장비 — 작게 튀어나온다 (살짝 넘겼다가 제자리)
            float pop = k < 0.7f ? Mathf.Lerp(0.4f, 1.12f, k / 0.7f)
                                 : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            _multiGearRoots[i].transform.localScale = Vector3.one * pop;

            yield return null;
        }

        box.gameObject.SetActive(false);
        lid.gameObject.SetActive(false);
        _multiGearRoots[i].transform.localScale = Vector3.one;
    }

    void Show(MonsterGearData gear)
    {
        _gear    = gear;
        _opening = false;

        int index = _total - _queue.Count;   // 1부터
        _titleText.text = _total > 1 ? LocalizationManager.Instance.Format("스테이지 {0} 보상  ({1}/{2})", _stage, index, _total)
                                     : LocalizationManager.Instance.Format("스테이지 {0} 보상", _stage);

        Color   grade = GradeStyle.GetColor(gear.Grade);
        BoxLook look  = LookOf((int)gear.Grade);

        _gradeLine.text  = LocalizationManager.Instance.Format("{0} 상자", GradeStyle.GetLabel(gear.Grade));
        _gradeLine.color = grade;

        // 상자 자체가 등급색이다 — 열기 전에도 "좋은 것인가" 는 알 수 있다.
        // 무엇인지는 열어야 안다.
        _boxImage.color = grade;
        _lidImage.color = grade;
        _hintText.text  = "눌러서 열기";

        // ⚠ 매번 되돌린다 — 팝업은 풀에서 재사용된다
        //   지난번에 열어 둔 채로 두면 다음 상자가 열린 채로 뜬다.
        _boxRoot.SetActive(true);
        _gearRoot.SetActive(false);

        var boxRt = (RectTransform)_boxRoot.transform;
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.localRotation    = Quaternion.identity;
        boxRt.localScale       = Vector3.one;

        _lid.anchoredPosition = Vector2.zero;
        _lid.localRotation    = Quaternion.identity;
        _lid.gameObject.SetActive(true);

        SetGlow(grade, 0.22f, 1f);
        ResetRays(grade, look.RayAlpha);
        ResetSparks(grade);
        SetAlpha(_flash, 0f);

        _boxButton.interactable = true;
        _closeBtn.gameObject.SetActive(false);

        FillGear(gear, grade);

        // 누르기 전에는 숨을 쉰다 — 멈춰 있으면 눌러도 되는 것으로 안 읽힌다.
        if (_idle != null) StopCoroutine(_idle);
        _idle = StartCoroutine(IdleRoutine(look));
    }

    void FillGear(MonsterGearData gear, Color grade)
    {
        _gearFrame.color = grade;

        _gearIcon.sprite  = gear.Icon;
        _gearIcon.enabled = gear.Icon != null;

        _gearName.text  = gear.DisplayName;
        _gearName.color = grade;

        // "희귀 · 갑옷 · 인간형" — 등급/부위/몸 형태 셋이 이 장비가 무엇인지의 전부다.
        // ⚠ 이름표를 손으로 적지 않는다 — GradeStyle · MonsterGearData.NameOf 가 정본이다.
        string body = gear.Body == MonsterGearBody.Humanoid ? "인간형" : "비인간형";
        _gearGrade.text  = $"{GradeStyle.GetLabel(gear.Grade)} · {MonsterGearData.NameOf(gear.Part)} · {body}";
        _gearGrade.color = grade;

        _gearStat.text = gear.StatLine();
        _gearDesc.text = gear.Description;
    }

    // ── 연출 조각 ────────────────────────────────────────────

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        g.color = new Color(c.r, c.g, c.b, a);
    }

    void SetGlow(Color grade, float alpha, float scale)
    {
        _boxGlow.color = new Color(grade.r, grade.g, grade.b, alpha);
        _boxGlow.rectTransform.localScale = Vector3.one * scale;
    }

    /// <summary>빛살은 등급이 켠다 — 일반 상자에는 아예 없다.</summary>
    void ResetRays(Color grade, float alpha)
    {
        _rays.gameObject.SetActive(alpha > 0f);

        // 시작 각도를 굴린다 — 고정이면 두 번째 상자부터 같은 그림이 된다.
        _rays.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _rays.localScale    = Vector3.one * 0.8f;

        for (int i = 0; i < _rayImages.Length; i++)
            _rayImages[i].color = new Color(grade.r, grade.g, grade.b, alpha);
    }

    void ResetSparks(Color grade)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            _sparks[i].anchoredPosition = Vector2.zero;
            _sparks[i].gameObject.SetActive(false);
            _sparkImages[i].color = grade;
        }
    }

    /// <summary>누르기 전 — 숨 쉬듯 커졌다 작아진다. 빛무리도 함께 오간다.</summary>
    IEnumerator IdleRoutine(BoxLook look)
    {
        var   boxRt = (RectTransform)_boxRoot.transform;
        Color grade = _boxImage.color;
        float t     = Random.value * 3f;   // 시작 위상도 굴린다

        while (!_opening)
        {
            t += Time.unscaledDeltaTime;

            float breathe = Mathf.Sin(t * 2.2f);
            boxRt.localScale = Vector3.one * (1f + breathe * 0.022f);

            SetGlow(grade, 0.20f + breathe * 0.06f, 1f + breathe * 0.04f);

            // 빛살은 등급이 높을수록 빨리 돈다 — 누르기 전부터 무게가 보인다.
            if (_rays.gameObject.activeSelf)
                _rays.Rotate(0f, 0f, look.RaySpin * 0.25f * Time.unscaledDeltaTime);

            yield return null;
        }

        _idle = null;
    }

    // ── 열기 ─────────────────────────────────────────────────

    void OpenBox()
    {
        if (_opening || _gear == null) return;

        _opening = true;                 // IdleRoutine 이 이 값을 보고 스스로 끝난다
        _boxButton.interactable = false;
        _hintText.text = "";

        StartCoroutine(OpenRoutine());
    }

    /// <summary>
    /// 흔들림 → 섬광 → 뚜껑이 날고 불꽃이 퍼짐 → 장비 카드.
    ///
    /// ⚠ 진폭이 뒤로 갈수록 커진다
    ///   일정하게 흔들면 그냥 떨리는 물건이다. 점점 세지다가 터져야
    ///   "안에서 뭔가 나오려 한다" 로 읽힌다.
    /// </summary>
    IEnumerator OpenRoutine()
    {
        var     boxRt = (RectTransform)_boxRoot.transform;
        Color   grade = GradeStyle.GetColor(_gear.Grade);
        BoxLook look  = LookOf((int)_gear.Grade);

        // ── ① 흔들림 ──
        //   길이·위상을 조금 굴린다 — 같은 등급이어도 매번 같지 않게.
        float dur    = look.ShakeDur * Random.Range(0.92f, 1.08f);
        float phase0 = Random.value * 10f;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / dur);
            float a = look.ShakeAmp * (0.25f + k * 0.75f);   // 뒤로 갈수록 커진다

            // 34Hz — 사람이 "덜덜" 로 읽는 대역.
            // 더 느리면 흔들리는 게 아니라 움직이는 것으로 보인다.
            float phase = phase0 + t * 34f;

            boxRt.anchoredPosition = new Vector2(Mathf.Sin(phase) * a,
                                                 Mathf.Sin(phase * 1.7f) * a * 0.35f);
            boxRt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.8f) * a * 0.35f);
            boxRt.localScale       = Vector3.one * (1f + k * 0.14f);   // 부풀어 오른다

            SetGlow(grade, 0.22f + k * 0.45f, 1f + k * 0.45f);

            if (_rays.gameObject.activeSelf)
            {
                _rays.Rotate(0f, 0f, look.RaySpin * (0.3f + k * 1.4f) * Time.unscaledDeltaTime);
                _rays.localScale = Vector3.one * (0.8f + k * 0.45f);
            }

            yield return null;
        }

        // ── ② 터짐 — 섬광 · 뚜껑 · 불꽃 ──
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.localRotation    = Quaternion.identity;
        boxRt.localScale       = Vector3.one;

        LaunchSparks(look.Sparks);

        // 뚜껑이 도는 방향·날아가는 쪽도 굴린다.
        float lidSpin  = Random.Range(140f, 260f) * (Random.value < 0.5f ? -1f : 1f);
        float lidUp    = 180f + look.ShakeAmp * 4f;
        float lidDrift = Random.Range(-40f, 40f);

        SetAlpha(_flash, look.FlashAlpha);

        t = 0f;
        while (t < BurstTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / BurstTime);

            // 섬광은 빠르게 빠진다 — 오래 남으면 화면이 흐려 보인다.
            SetAlpha(_flash, look.FlashAlpha * (1f - Mathf.Clamp01(t / 0.22f)));

            // 뚜껑 — 위로 솟았다가 떨어진다 (포물선).
            float lidY = lidUp * (k * 2f - k * k * 2.2f);
            _lid.anchoredPosition = new Vector2(lidDrift * k, lidY);
            _lid.localRotation    = Quaternion.Euler(0f, 0f, lidSpin * k);

            // 상자 몸통은 쪼그라들며 사라진다.
            boxRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, k);
            SetAlpha(_boxImage, 1f - k);

            StepSparks(k);

            SetGlow(grade, Mathf.Lerp(0.67f, 0.18f, k), Mathf.Lerp(1.45f, 1f, k));

            if (_rays.gameObject.activeSelf)
            {
                _rays.Rotate(0f, 0f, look.RaySpin * 1.6f * Time.unscaledDeltaTime);
                _rays.localScale = Vector3.one * Mathf.Lerp(1.25f, 1.05f, k);
            }

            yield return null;
        }

        SetAlpha(_flash, 0f);
        SetAlpha(_boxImage, 1f);          // 다음 상자를 위해 되돌린다 (풀 재사용)
        boxRt.localScale = Vector3.one;

        _boxRoot.SetActive(false);
        ResetSparks(grade);

        // ── ③ 장비 카드 ──
        _gearRoot.SetActive(true);
        _closeBtn.gameObject.SetActive(true);
        _closeLabel.text = _queue.Count > 0 ? "다음 상자" : "확인";

        var cardRt = (RectTransform)_gearRoot.transform;

        t = 0f;
        while (t < PopTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / PopTime);

            // 작게 → 넘치게 → 제자리.
            // 곧장 1 로 가면 '나온' 것이 아니라 '켜진' 것으로 보인다.
            float s = k < 0.7f ? Mathf.Lerp(0.62f, 1.07f, k / 0.7f)
                               : Mathf.Lerp(1.07f, 1f, (k - 0.7f) / 0.3f);

            cardRt.localScale = Vector3.one * s;
            yield return null;
        }

        cardRt.localScale = Vector3.one;

        // ⚠ 갱신이 끝난 뒤에 터뜨린다 (UIJuice 파일 머리 주석)
        //   등급업과 같은 무게다 — 영구 데이터가 하나 늘어나는 자리다.
        UIJuice.GradeUp(_gearFrame.rectTransform, GradeStyle.GetLabel(_gear.Grade));

        _opening = false;
    }

    // ── 불꽃 ─────────────────────────────────────────────────
    //
    //  ⚠ 프리팹이 굽는 수가 상한이다 — 모자라면 그 등급만 초라해진다
    //    (GearBoxPopupCreator.SparkCount 가 LookOf 표의 최댓값 이상이어야 한다).

    Vector2[] _sparkVel;
    int       _sparkLive;

    void LaunchSparks(int count)
    {
        _sparkVel ??= new Vector2[_sparks.Length];
        _sparkLive = Mathf.Min(count, _sparks.Length);

        // 각도를 고르게 나눈 뒤 조금씩 흔든다 — 완전 무작위면 한쪽에 뭉친다.
        float step = 360f / Mathf.Max(1, _sparkLive);

        for (int i = 0; i < _sparks.Length; i++)
        {
            bool live = i < _sparkLive;
            _sparks[i].gameObject.SetActive(live);
            if (!live) continue;

            float ang = (step * i + Random.Range(-step * 0.35f, step * 0.35f)) * Mathf.Deg2Rad;
            float spd = Random.Range(260f, 520f);

            _sparkVel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;

            _sparks[i].anchoredPosition = Vector2.zero;
            _sparks[i].localScale       = Vector3.one * Random.Range(0.7f, 1.5f);
        }
    }

    /// <summary>k = 0→1. 퍼지면서 느려지고 흐려진다.</summary>
    void StepSparks(float k)
    {
        float travel = 1f - Mathf.Pow(1f - k, 2.2f);   // 처음 빠르고 뒤로 갈수록 느리다

        for (int i = 0; i < _sparkLive; i++)
        {
            // 살짝 아래로 처지게 — 곧게 뻗기만 하면 그림이 아니라 도형으로 보인다.
            Vector2 p = _sparkVel[i] * travel * 0.55f;
            p.y -= 90f * travel * travel;

            _sparks[i].anchoredPosition = p;
            SetAlpha(_sparkImages[i], 1f - k);
        }
    }

    protected override void OnAfterClose()
    {
        if (_idle != null) { StopCoroutine(_idle); _idle = null; }
        _gear = null;
    }
}
