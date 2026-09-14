using System.Collections.Generic;

// ============================================================
//  LocalizationManager.cs
//  enum.ToString() 을 key 로 현지화 텍스트를 반환.
//
//  사용:
//    LocalizationManager.Instance.Get(stat.ToString())
//    LocalizationManager.Instance.Get(skillId.ToString())
//    LocalizationManager.Instance.Get(grade.ToString())
//
//  다국어 대응:
//    BuildTable() 을 언어별로 교체하거나
//    JSON 파일에서 Dictionary 로드 하도록 확장.
// ============================================================

public class LocalizationManager : SingletonPure<LocalizationManager>
{
    readonly Dictionary<string, string> _table;

    public LocalizationManager()
    {
        _table = BuildKorean();
    }

    public string Get(string key)
        => _table.TryGetValue(key, out var v) ? v : key;

    static Dictionary<string, string> BuildKorean() => new()
    {
        // ── StatType ──────────────────────────────────────────
        { "MaxHp",        "체력"   },
        { "Attack",       "공격"   },
        { "Defense",      "방어율" },
        { "MoveSpeed",    "이속"   },
        { "AttackSpeed",  "공속"   },
        { "AttackRange",  "사거리"   },
        { "CritChance",   "치명타율" },
        { "CritDamage",   "치명타배율" },
        { "SoldierCount",        "용병수"     },
        { "CommandPower",        "지휘력"     },
        { "SkillCooldownReduce", "쿨감소"     },
        { "GeneralSlotBonus",    "장수슬롯"   },
        { "AllStatPenalty",      "전체스텟"   },   // 값은 음수로 표시된다
        { "EquipSlotBonus",      "장비슬롯"   },
        { "ExpGainBonus",        "경험치획득" },
        { "DefensePenetration",  "방어관통"   },

        // ── AbilityGrade ──────────────────────────────────────
        { "Advanced", "고급"  },
        { "Special",  "특수"  },
        { "Mastery",  "달인"  },

        // ── AbilityTarget ─────────────────────────────────────
        { "All",              "전체"   },
        { "Job_Knight",       "기사"   },
        { "Job_Archer",       "궁수"   },
        { "Job_Mage",         "마법사" },
        { "Job_ShieldBearer", "방패병" },
        { "Range_Melee",      "근거리" },
        { "Range_Ranged",     "원거리" },
        { "Unit_General",     "장군"   },
        { "Unit_Soldier",     "병사"   },

        // ── PassiveTrigger ────────────────────────────────────
        { "None",           "즉시"           },
        { "OnAttack",       "공격 시"        },
        { "OnHit",          "피격 시"        },
        { "OnEnemyKill",    "처치 시"        },
        { "OnSoldierDeath", "병사 사망 시"   },
        { "OnSkillUse",     "스킬 사용 시"   },
        { "OnBattleStart",  "전투 시작 시"   },
        { "StageClear",     "스테이지 클리어 시" },

        // ── ActiveSkillId ─────────────────────────────────────
        { "HeavyStrike",      "강타"          },
        { "VolleyFire",       "일제 사격"     },
        { "LeapStrike",       "도약 강타"     },
        { "HealAura",         "치유 오라"     },
        { "TargetHeal",       "집중 치유"     },
        { "ChargeSoldier",    "돌격 병사"     },
        { "SummonSkeleton",   "스켈레톤 소환" },
        { "PoisonZone",       "독성 지대"     },
        { "Meteor",           "메테오"        },
        { "Blizzard",         "블리자드"      },
        { "SacrificeSoldier", "병사 희생"     },
        { "Bind",             "속박"          },
        { "SuicideSoldier",   "자폭 병사"     },
        { "Berserker",        "광전사"        },
        { "IronShield",       "철벽 방어"     },
        { "ArrowRain",        "화살 비"       },
        { "BattleCry",        "전투 함성"     },
        { "Shockwave",        "충격파"        },
        { "SwiftStrike",      "신속 연격"     },
        { "SummonElite",      "정예 소환"     },

        // ── 희귀 액티브 스킬 (직업당 부대에 1명) ───────────────
        { "Bisect",           "일도양단"      },
        { "ArrowStorm",       "화살 폭풍"     },
        { "GravityCollapse",  "중력 붕괴"     },
        { "Bulwark",          "불멸의 방벽"   },
        { "ChainLightning",   "연쇄 번개"     },
        { "DeathSentence",    "사형 선고"     },
        { "BloodPrice",       "피의 대가"     },
        { "PiercingDash",     "관통 돌진"     },
        { "WarBanner",        "군기 강림"     },
        { "Gravestone",       "비석 강림"     },

        // ── 소환사 시그니처 (이 프로젝트 신규) ─────────────────
        { "SummonSignature",  "권속 소환"     },

        // ── 몬스터 고유 (이 프로젝트 신규) ─────────────────────
        { "SlimeMend",        "치유 점액"     },

        // ── 우두머리 행동 패턴 (적 전용) ───────────────────────
        { "BossCharge",       "돌진"          },
        { "BossSlam",         "분쇄 강타"     },
        { "BossEnrage",       "광폭화"        },

        // ── UnitGrade ─────────────────────────────────────────
        { "Normal",   "일반" },
        { "Uncommon", "비범" },
        { "Rare",     "희귀" },
        { "Unique",   "고유" },
        { "Epic",     "영웅" },

        // ── UnitJob ───────────────────────────────────────────
        { "Knight",       "기사"   },
        { "Archer",       "궁수"   },
        { "Mage",         "마법사" },
        { "ShieldBearer", "방패병" },
    };
}
