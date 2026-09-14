using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  GeneralStatRowUI.cs
//  환생 팝업에서 장수 1명의 통계 행.
//  ExpRowUI와 동일 구조이지만 경험치 필드 없음.
// ============================================================

public class GeneralStatRowUI : MonoBehaviour
{
    [SerializeField] Image                _portraitBg;
    [SerializeField] Image                _portraitImage;
    [SerializeField] UnitAppearanceBridge _portraitBridge;
    [SerializeField] TextMeshProUGUI      _nameText;
    [SerializeField] StatBarUI            _statBar;
    [SerializeField] TextMeshProUGUI      _totalText;
    [SerializeField] TextMeshProUGUI      _legendText;
    [SerializeField] TextMeshProUGUI      _dpsText;

    Texture2D        _portraitTexture;
    GeneralStatEntry _stats;
    float            _elapsedSec;

    /// <summary>
    /// 통계 항목 하나를 그대로 받는다.
    ///
    ///   Portrait 가 있으면        → 그 그림 (몬스터 초상화 · 스킬 아이콘)
    ///   AppearanceSeed 가 있으면  → 그 자리에서 합성 (소환사 — MainPanel 과 같은 입력)
    ///   둘 다 없으면              → 이름만
    /// </summary>
    public void Setup(GeneralStatEntry entry)
    {
        if (_nameText != null) _nameText.text = entry.GeneralName;

        if (entry.Portrait != null)
        {
            // ⚠ 합성 브릿지를 꺼야 한다
            //   켜져 있으면 다음 프레임에 제가 만든 텍스처로 이 그림을 덮어쓴다.
            if (_portraitBridge != null) _portraitBridge.gameObject.SetActive(false);

            if (_portraitImage != null)
            {
                _portraitImage.sprite         = entry.Portrait;
                _portraitImage.enabled        = true;
                _portraitImage.preserveAspect = true;
            }
        }
        else if (!string.IsNullOrEmpty(entry.AppearanceSeed))
        {
            // 소환사 — 전투에 선 것과 **같은 입력**으로 합성한다.
            // (SummonerRuntimeBridge.ApplyAppearance · MainPanel 과 같은 세 값)
            UnitPortraitHelper.Render(entry.AppearanceSeed,
                                      entry.AppearanceJob, entry.AppearanceGrade,
                                      _portraitBridge, _portraitBg, _portraitImage,
                                      ref _portraitTexture);
        }

        if (_statBar   != null) _statBar.Clear();
        if (_totalText != null) _totalText.text = "";
    }

    public void SetStats(GeneralStatEntry stats) => _stats = stats;

    public void SetDPS(float elapsedSec)
    {
        _elapsedSec = elapsedSec;
    }

    public void RefreshTab(CombatStatTab tab, float maxValue)
    {
        UpdateLegend(tab);

        if (_statBar == null) return;
        if (_stats == null || maxValue <= 0f)
        {
            _statBar.Clear();
            if (_totalText != null) _totalText.text = "";
            return;
        }

        RefreshDpsText(tab);

        switch (tab)
        {
            case CombatStatTab.Damage:
            {
                // 가운데 칸은 **상태 이상**이다 (사용자 지시, 2026-09-12) —
                //   원작의 '병사' 칸 자리다. 이 게임에는 아군 병사가 없어 늘 0 이었다.
                float total = _stats.TotalDamageDealt;
                _statBar.Setup(
                    new[] { _stats.GeneralDamageDealt, _stats.DotDamageDealt, _stats.SkillDamageDealt },
                    StatBarUI.DamageColors,
                    total / maxValue);
                if (_totalText != null) _totalText.text = FormatTotal(total);
                break;
            }
            case CombatStatTab.Tank:
            {
                float taken    = _stats.DamageTaken + _stats.SoldierDamageTaken;
                float absorbed = _stats.DamageAbsorbed;
                _statBar.Setup(
                    new[] { taken, absorbed },
                    StatBarUI.TankColors,
                    (taken + absorbed) / maxValue);
                if (_totalText != null) _totalText.text = FormatTotal(taken);
                break;
            }
            case CombatStatTab.Heal:
            {
                float heal = _stats.HealingDone;
                _statBar.Setup(
                    new[] { heal },
                    StatBarUI.HealColors,
                    heal / maxValue);
                if (_totalText != null) _totalText.text = FormatTotal(heal);
                break;
            }
        }
    }

    void RefreshDpsText(CombatStatTab tab)
    {
        if (_dpsText == null) return;
        if (tab != CombatStatTab.Damage || _stats == null || _elapsedSec <= 0f)
        {
            _dpsText.gameObject.SetActive(false);
            return;
        }
        _dpsText.gameObject.SetActive(true);
        _dpsText.text = $"DPS {FormatTotal(_stats.TotalDamageDealt / _elapsedSec)}";
    }

    void UpdateLegend(CombatStatTab tab)
    {
        if (_legendText == null) return;
        _legendText.text = tab switch
        {
            // 이 게임에서 딜의 출처는 카드의 **평타 · 상태 이상 · 스킬** 셋이다.
            // (0 인 칸은 StatBarUI 가 알아서 감춘다 — 도트가 없는 카드는 두 칸으로 보인다)
            CombatStatTab.Damage => "<color=#4D8CF2>■</color> 평타  " +
                                    "<color=#59D98C>■</color> 상태이상  " +
                                    "<color=#F28C33>■</color> 스킬",
            CombatStatTab.Tank   => "<color=#E64040>■</color> 받은피해  <color=#4D8CF2>■</color> 감소피해",
            CombatStatTab.Heal   => "<color=#59D98C>■</color> 치유",
            _                    => "",
        };
    }

    static string FormatTotal(float value)
    {
        if (value >= 1_000_000f) return $"{value / 1_000_000f:0.0}M";
        if (value >= 10_000f)    return $"{value / 1_000f:0.#}K";
        return $"{(int)value:N0}";
    }
}
