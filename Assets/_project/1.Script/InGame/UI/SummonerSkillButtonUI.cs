using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  SummonerSkillButtonUI.cs
//  소환사 시그니처 스킬 버튼. 하단 카드 바 오른쪽 끝 칸이다.
//
//  ■ 왜 카드처럼 생겼나 — 카드와 같은 격자에 앉는다 (사용자 확정, 2026-09-04)
//    누르면 전장에 무언가 일어나는 물건이라 하는 일이 카드와 같다. 크기·높이·
//    간격을 카드와 맞추면 "이것도 낼 수 있는 것" 으로 읽힌다.
//    화면 오른쪽 아래 구석에 두어 시작 버튼(말풍선)과 같은 세로선에 세운다.
//
//  ■ 그림 하나 + 남은 횟수뿐이다 (사용자 확정, 2026-09-07)
//    한때 스킬 이름("권속 소환")과 제한 종류("판당")도 함께 적었다. 150px
//    짜리 칸에 글자가 둘이면 그림이 76px 로 쪼그라들어, 정작 무엇을 누르는지가
//    제일 안 보였다. 시그니처는 **소환사당 하나뿐**이라 이름을 읽고 고를 일이
//    없다 — 카드처럼 여럿 중에서 고르는 물건이 아니다.
//    ⚠ 숫자는 남긴다. 몇 번 남았는지가 없으면 잠긴 이유를 화면이 말하지 못한다.
//    ⚠ 마나 배지는 애초에 없다 — 무료 스킬이라 뜻이 없다.
//
//  ■ 다 쓰면 흐려진다 — 사라지지 않는다
//    자리가 비면 카드 격자에 구멍이 생기고, 무엇이 있었는지도 잊는다.
//
//  ■ ⚠ 이 컴포넌트가 붙은 오브젝트를 끄지 않는다
//    소환사는 HUD 보다 **늦게** 선다 (RunBootstrap: BuildStarterDeck →
//    SpawnSummoner). 그래서 첫 Refresh 는 "스킬 없음" 으로 보인다.
//    거기서 자기 오브젝트를 꺼 버리면 Update 도 함께 멎어 **소환사가 선 뒤에도
//    영영 다시 켜지지 않는다** — 화면에서 버튼이 통째로 사라진다.
//    껐다 켜는 것은 자식(_content) 이고, 뿌리는 늘 살아 있다.
//    (EnemyInfoButtonUI 가 같은 이유로 같은 구조를 쓴다)
// ============================================================

public class SummonerSkillButtonUI : MonoBehaviour
{
    [SerializeField] Button          _button;
    [SerializeField] Image           _icon;

    [Tooltip("남은 횟수.")]
    [SerializeField] TextMeshProUGUI _countText;

    [Tooltip("쓸 수 없을 때 덮는 어두운 면.")]
    [SerializeField] GameObject _disabledOverlay;

    [Tooltip("겨냥 중임을 알리는 테두리. 다음 탭이 이 스킬을 쓴다는 뜻이다.")]
    [SerializeField] GameObject _armedMark;

    [Tooltip("스킬이 없는 소환사일 때 통째로 끌 자식. ⚠ 이 컴포넌트가 붙은 뿌리가 아니어야 한다.")]
    [SerializeField] GameObject _content;

    void Awake()
    {
        _button.onClick.RemoveListener(HandleClick);
        _button.onClick.AddListener(HandleClick);
    }

    void OnEnable()
    {
        SummonerSkillRule.Changed      += Refresh;
        SummonerSkillRule.ArmedChanged += Refresh;
        StageLoopDirector.OnStageReady += HandleStageReady;
        StageLoopDirector.OnStageStart += HandleStageStart;

        Refresh();
    }

    void OnDisable()
    {
        SummonerSkillRule.Changed      -= Refresh;
        SummonerSkillRule.ArmedChanged -= Refresh;
        StageLoopDirector.OnStageReady -= HandleStageReady;
        StageLoopDirector.OnStageStart -= HandleStageStart;
    }

    void HandleStageReady(int stage) => Refresh();
    void HandleStageStart(int stage) => Refresh();

    // ⚠ 소환사는 카드보다 늦게 선다 (RunBootstrap: BuildStarterDeck → SpawnSummoner).
    //   그래서 첫 Refresh 때는 스킬 정보가 아직 없다. 잡힐 때까지 본다 —
    //   카드 바(SummonDeckUI)가 데이터를 기다리는 것과 같은 이유다.
    //
    // ⚠ 쓸 수 있는지도 **매 프레임** 본다 (2026-09-06)
    //   예전에는 이벤트(Changed·StageReady·StageStart)에서만 다시 그렸다.
    //   그런데 StageLoopDirector.StartStage 는 IsStageReady 를 끈 **직후**에
    //   OnStageStart 를 쏘는데, 그 시점에 BattleManager 는 아직 InWave 가
    //   아니다. 그래서 CanSummon 이 잠깐 false 가 되고, 그 순간의 값이 그대로
    //   굳어 **판이 끝날 때까지 버튼이 잠긴 채로 남았다.**
    //   판 상태는 이벤트 없이도 바뀐다 — 상태를 물어보는 쪽이 맞다.
    void Update()
    {
        if (SummonerSkillRule.HasSkill != _shown) { Refresh(); return; }
        if (!_shown) return;

        if (SummonerSkillRule.CanUse  != _usable ||
            SummonerSkillRule.IsArmed != _armed) Refresh();
    }

    bool _shown;
    bool _usable;
    bool _armed;

    void Refresh()
    {
        bool has = SummonerSkillRule.HasSkill;
        _shown = has;

        // 스킬이 없는 소환사면 칸을 비운다 — 빈 버튼은 눌러 볼 것을 만든다.
        // ⚠ 끄는 것은 **자식**이다. 뿌리를 끄면 Update 가 멎어 다시 못 켠다.
        if (_content.activeSelf != has) _content.SetActive(has);
        if (!has) return;

        _countText.text = SummonerSkillRule.Remaining.ToString();

        // ⚠ 그림의 정본은 SignatureSkillDisplay 다 (사용자 지시, 2026-09-11)
        //   '권속 소환' 은 여덟 소환사가 나눠 쓰는데 그림이 하나라, 무엇이 나오는지
        //   알 수 없었다. 소환형은 **부르는 종족의 초상화**를 쓴다 — 나머지는 스킬 그림 그대로.
        //   선택 화면(SummonerCandidateCardUI)과 같은 그림이다.
        Sprite art = SignatureSkillDisplay.IconOf(SummonerRuntimeBridge.Current.Data);

        _icon.sprite  = art;
        _icon.enabled = art != null;

        _usable = SummonerSkillRule.CanUse;
        _armed  = SummonerSkillRule.IsArmed;

        // 겨냥 중에는 눌러서 취소할 수 있어야 하므로 계속 살려 둔다.
        _button.interactable = _usable || _armed;
        if (_disabledOverlay != null) _disabledOverlay.SetActive(!_usable && !_armed);

        // 겨냥 중이라는 표시 — 카드가 선택됐을 때와 같은 뜻이다.
        if (_armedMark != null) _armedMark.SetActive(_armed);
    }

    /// <summary>
    /// 누르면 <b>겨냥</b>이 켜진다 — 그 자리에서 바로 나가지 않는다.
    ///
    /// ⚠ 카드와 같은 두 단계다 (사용자 확정, 2026-09-06)
    ///   누르는 즉시 나가면 어디에 떨어질지 고를 수 없다. 다음 탭이
    ///   소환 위치를 정한다 (SummonController.ReadTap).
    /// </summary>
    void HandleClick() => SummonerSkillRule.ToggleArm();
}
