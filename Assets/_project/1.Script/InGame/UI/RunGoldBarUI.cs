using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  RunGoldBarUI.cs
//  인게임 **런 골드** 표시 — [금화][1,240].
//
//  ■ 왜 인게임에 있어야 하나 (사용자 지시, 2026-09-13)
//    골드를 버는 곳은 전장(용사 처치)인데, 얼마나 벌었는지는 갈림길에 들어가
//    시설 화면을 열어야만 보였다. 버는 자리와 읽는 자리가 갈려 있으면
//    "이번 판을 더 끌어야 하나" 를 판단할 수가 없다.
//
//  ■ 자리 — 마왕성 체력 막대의 오른쪽, 같은 줄
//    왼쪽 줄기(특성 → 마왕성 체력 → 시너지)와 같은 격자에 얹는다.
//    ⚠ 자리의 정본은 Creator 다 (InGameUIPrefabCreator.BuildRunGoldBar).
//
//  ■ 숫자는 **따라 올라간다**
//    지갑 값이 즉시 바뀌어도 표시는 몇 프레임에 걸쳐 쫓아간다. 동전이 날아와
//    꽂히는 순간과 숫자가 오르는 순간이 맞아떨어져야 "저 동전이 이 숫자다" 로
//    읽힌다.
//    ⚠ 데이터는 언제나 즉시다 — 이건 표시만 늦추는 것이다. 실제 잔액은
//      RunGoldData 가 이미 갖고 있고, 상점은 그 값을 본다.
//
//  ■ ⚠ 동전은 연출일 뿐이다 (장비 상자와 같은 규칙)
//    도착할 때 골드를 넣지 않는다. 지급은 처치 시점(UnitDeathDespawnSystem)에
//    이미 끝났다 — 그래야 연출 도중 판이 끝나도 골드가 새지 않는다.
// ============================================================

public class RunGoldBarUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _valueText;

    [Tooltip("동전이 날아와 꽂히는 자리. 배지 루트다 — 튕김 연출도 여기에 건다.")]
    [SerializeField] RectTransform _badge;

    [Tooltip("날아다니는 동전에 쓸 그림. 배지의 금화와 같은 것이다.")]
    [SerializeField] Sprite _coinSprite;

    // ── 숫자가 따라 오르는 속도 ──────────────────────────────

    /// <summary>표시값이 실제값을 따라잡는 데 걸리는 대략적인 시간(초).</summary>
    const float CatchUpSeconds = 0.35f;

    /// <summary>이 차이 아래로 좁혀지면 그냥 붙인다 — 1 골드가 영영 안 따라붙는 것을 막는다.</summary>
    const float SnapEpsilon = 0.75f;

    float _shown;

    // ── 배지 튕김 ────────────────────────────────────────────

    const float BumpScale   = 1.18f;
    const float BumpSeconds = 0.18f;

    float _bumpAge = -1f;

    // ── 싱글턴 ───────────────────────────────────────────────
    //
    //  ⚠ 부르는 쪽이 ECS 시스템(UnitDeathDespawnSystem)이다. 씬에서 찾게 하면
    //    HUD 를 다시 구울 때마다 참조가 끊긴다 — 스스로 등록하고 스스로 지운다.

    static RunGoldBarUI _instance;

    void OnEnable()
    {
        _instance = this;

        var data = UserDataManager.Instance?.Get<RunGoldData>();
        if (data != null) data.OnGoldChanged += HandleGoldChanged;

        // 켜질 때는 따라 오르지 않는다 — 판을 여는 순간 0 에서 굴러 올라가면
        // 방금 번 것처럼 보인다.
        _shown = RunGoldRule.Current;
        Draw();
    }

    void OnDisable()
    {
        var data = UserDataManager.Instance?.Get<RunGoldData>();
        if (data != null) data.OnGoldChanged -= HandleGoldChanged;

        if (_instance == this) _instance = null;
    }

    void HandleGoldChanged()
    {
        // 값이 줄어드는 것(상점에서 쓴 것)은 굴리지 않는다 — 쓴 결과는 그 화면이
        // 이미 말한다. 여기서 천천히 내려가면 잔액을 잘못 읽게 된다.
        if (RunGoldRule.Current < _shown) _shown = RunGoldRule.Current;
    }

    void Update()
    {
        Follow();
        Bump();
        UpdateCoins();
    }

    void Follow()
    {
        float target = RunGoldRule.Current;
        if (Mathf.Approximately(_shown, target)) return;

        if (Mathf.Abs(target - _shown) <= SnapEpsilon) _shown = target;
        else _shown = Mathf.Lerp(_shown, target,
                                 1f - Mathf.Exp(-Time.unscaledDeltaTime / CatchUpSeconds));

        Draw();
    }

    void Draw() => _valueText.text = $"{Mathf.RoundToInt(_shown):N0}";

    void Bump()
    {
        if (_bumpAge < 0f) return;

        _bumpAge += Time.unscaledDeltaTime;

        float t = _bumpAge / BumpSeconds;
        if (t >= 1f)
        {
            _badge.localScale = Vector3.one;
            _bumpAge = -1f;
            return;
        }

        // 한 번 부풀었다 돌아온다.
        float s = 1f + (BumpScale - 1f) * Mathf.Sin(t * Mathf.PI);
        _badge.localScale = new Vector3(s, s, 1f);
    }

    // ══════════════════════════════════════════════════════════
    //  날아오는 동전
    // ══════════════════════════════════════════════════════════
    //
    //  ■ 왜 UI 인가 (피해 숫자와 반대다)
    //    피해 숫자는 한 판에 수백 개가 뜨므로 캔버스를 피해 월드 TMP 로 그렸다.
    //    동전은 **UI 배지로 빨려 들어가는 것**이 전부라 도착점이 캔버스 좌표다.
    //    월드로 그리면 매 프레임 배지 위치를 역변환해야 한다.
    //
    //  ⚠ 개수를 묶는다 — 한 마리당 한 닢이 아니다
    //    30스테이지 한 판이 170기다. 한 마리마다 동전을 띄우면 화면이 금화로
    //    덮인다. 같은 자리 가까이에서 짧은 시간에 겹쳐 죽으면 **날고 있는
    //    동전에 합친다** (피해 숫자의 합산과 같은 장치).

    /// <summary>동시에 날 수 있는 최대 개수. 넘치면 가장 오래된 것을 뺏는다.</summary>
    const int MaxCoins = 18;

    /// <summary>동전 한 닢이 배지까지 가는 시간(초).</summary>
    const float FlightSeconds = 0.55f;

    /// <summary>날아가기 전에 잠깐 튀어 오르는 시간(초).</summary>
    const float PopSeconds = 0.16f;

    /// <summary>튀어 오르는 높이(캔버스 px).</summary>
    const float PopHeight = 54f;

    /// <summary>동전 한 변(캔버스 px).</summary>
    const float CoinSize = 46f;

    /// <summary>이 거리(캔버스 px) 안에서 아직 튀어 오르는 중인 동전이 있으면 합친다.</summary>
    const float MergeRadius = 150f;

    class Coin
    {
        public RectTransform Tr;
        public Image         Img;
        public bool          Active;
        public float         Age;
        public Vector2       Start;
        public Vector2       Control;   // 튀어 오른 꼭짓점
        public int           Amount;
    }

    readonly List<Coin> _coins = new(MaxCoins);

    RectTransform _coinRoot;
    Camera        _camera;

    /// <summary>
    /// 처치 자리에 금화 한 닢을 띄운다. <b>연출만</b> 한다 — 골드는 이미 들어갔다.
    ///
    /// HUD 가 없으면(에디터에서 전투만 돌릴 때) 조용히 아무것도 하지 않는다.
    /// </summary>
    public static void Drop(Vector3 worldPos, int amount)
    {
        if (_instance == null || amount <= 0) return;
        _instance.Spawn(worldPos, amount);
    }

    void Spawn(Vector3 worldPos, int amount)
    {
        if (!TryToCanvas(worldPos, out Vector2 from)) return;

        // ① 아직 튀어 오르는 중인 이웃에 합친다 — 난전에서 동전이 쏟아지는 것을 막는다.
        for (int i = 0; i < _coins.Count; i++)
        {
            Coin c = _coins[i];
            if (!c.Active || c.Age > PopSeconds) continue;
            if (Vector2.Distance(c.Start, from) > MergeRadius) continue;

            c.Amount += amount;
            return;
        }

        Coin coin = Take();

        coin.Active = true;
        coin.Age    = 0f;
        coin.Start  = from;
        coin.Amount = amount;

        // 꼭짓점은 살짝 흩는다 — 같은 자리에서 여러 닢이 겹쳐 뜨는 것을 막는다.
        coin.Control = from + new Vector2(Random.Range(-40f, 40f), PopHeight);

        coin.Tr.anchoredPosition = from;
        coin.Tr.localScale       = Vector3.one;
        coin.Img.enabled         = true;
        coin.Tr.gameObject.SetActive(true);
    }

    Coin Take()
    {
        for (int i = 0; i < _coins.Count; i++)
            if (!_coins[i].Active) return _coins[i];

        if (_coins.Count < MaxCoins)
        {
            Coin fresh = Create();
            _coins.Add(fresh);
            return fresh;
        }

        // 상한에 닿으면 가장 오래된 것을 뺏는다 (피해 숫자와 같은 규칙).
        Coin oldest = _coins[0];
        for (int i = 1; i < _coins.Count; i++)
            if (_coins[i].Age > oldest.Age) oldest = _coins[i];

        return oldest;
    }

    Coin Create()
    {
        EnsureRoot();

        var go = new GameObject("GoldCoin", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_coinRoot, worldPositionStays: false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CoinSize, CoinSize);

        var img = go.GetComponent<Image>();
        img.sprite         = _coinSprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;

        go.SetActive(false);

        return new Coin { Tr = rt, Img = img };
    }

    /// <summary>
    /// 동전을 담을 칸. 배지가 아니라 <b>캔버스 바로 아래</b>에 만든다 —
    /// 배지 밑에 두면 배지가 튕길 때 날고 있는 동전까지 같이 커진다.
    /// </summary>
    void EnsureRoot()
    {
        if (_coinRoot != null) return;

        var canvas = GetComponentInParent<Canvas>();

        var go = new GameObject("GoldCoins", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, worldPositionStays: false);

        _coinRoot = go.GetComponent<RectTransform>();
        _coinRoot.anchorMin = _coinRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _coinRoot.offsetMin = _coinRoot.offsetMax = Vector2.zero;

        // 카드 바·상단바보다 앞이다 — 동전이 UI 밑으로 사라지면 어디로 갔는지 모른다.
        _coinRoot.SetAsLastSibling();
    }

    /// <summary>월드 좌표 → 동전 칸의 로컬 좌표. 카메라가 없으면 false.</summary>
    bool TryToCanvas(Vector3 worldPos, out Vector2 local)
    {
        local = default;

        EnsureRoot();

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return false;

        Vector2 screen = _camera.WorldToScreenPoint(worldPos);

        // ⚠ 캔버스가 Screen Space - Overlay 라 카메라 인자는 null 이다 (InGame.unity).
        //   여기에 Camera.main 을 넘기면 좌표가 통째로 어긋난다.
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _coinRoot, screen, null, out local);
    }

    void UpdateCoins()
    {
        if (_coins.Count == 0) return;

        // 배지의 자리는 매 프레임 잰다 — 튕김·해상도 변화로 움직인다.
        Vector2 target = _coinRoot.InverseTransformPoint(_badge.position);

        for (int i = 0; i < _coins.Count; i++)
        {
            Coin coin = _coins[i];
            if (!coin.Active) continue;

            // ⚠ unscaled 다 — 배속·일시정지와 무관하게 같은 속도로 빨려 들어간다.
            //   피해 숫자와 반대인 이유: 저건 전투의 일부고 이건 UI 피드백이다.
            coin.Age += Time.unscaledDeltaTime;

            if (coin.Age <= PopSeconds)
            {
                // ① 튀어 오른다 — 어디서 나왔는지가 먼저 읽혀야 한다.
                float t = coin.Age / PopSeconds;
                coin.Tr.anchoredPosition = Vector2.Lerp(coin.Start, coin.Control, t);
                coin.Tr.localScale       = Vector3.one * Mathf.Lerp(0.6f, 1f, t);
                continue;
            }

            float f = (coin.Age - PopSeconds) / FlightSeconds;

            if (f >= 1f)
            {
                coin.Active = false;
                coin.Tr.gameObject.SetActive(false);

                // 도착 — 숫자가 이미 그 값을 향해 올라가고 있다. 배지만 튕긴다.
                _bumpAge = 0f;
                continue;
            }

            // ② 배지로 빨려 든다. 뒤로 갈수록 빨라져 "빨려 간다" 로 보인다.
            float ease = f * f;

            coin.Tr.anchoredPosition = Vector2.Lerp(coin.Control, target, ease);
            coin.Tr.localScale       = Vector3.one * Mathf.Lerp(1f, 0.55f, ease);
        }
    }
}
