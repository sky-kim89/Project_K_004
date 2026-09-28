# 이미지 목록 (코드에서 뽑은 것)

> 자동 생성 — `python Docs/tools/extract_image_manifest.py` 를 `Assets/_project/1.Script` 에서 돌려 다시 만든다 (PYTHONIOENCODING=utf-8).
> 이름·의미 칸은 **게임 코드 원문**이다. 그림이 이 뜻과 어긋나면 그림이 틀린 것이다. `{0}` 은 런타임에 숫자가 들어가는 자리다.
> 제작 규칙은 `Docs/Image_Production_Brief.md` 가 정본이다 — 이 파일은 목록일 뿐이다.

### 종족 패시브 (계보·특이·각성)

이름 뒤의 분류: 계보 = 종족이 타고남 · 특이 = 장비 패시브 · 각성 ← 원본 = 같은 패시브 둘이 모이면 바뀌는 상위판

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `SpeciesPassives/passive_Alpha.png` | 64x64 | 우두머리 · 각성 ← 무리 사냥 | 처치할 때마다 이동속도 누적 + 공격·이동 속도 +{0} |
| `SpeciesPassives/passive_Anchor.png` | 64x64 | 무게추 · 특이 (장비) | 넉백을 받지 않는다, 이동 속도 -{0} |
| `SpeciesPassives/passive_Barrage.png` | 64x64 | 탄막 · 각성 ← 연사 | 한 번에 두 발을 쏘고 공격 속도 +{0} |
| `SpeciesPassives/passive_Berserker.png` | 64x64 | 광전사 · 각성 ← 피의 갈망 | 처치할 때마다 공격력 누적 + 공격력 +{0} |
| `SpeciesPassives/passive_Bloodlust.png` | 64x64 | 피의 갈망 · 계보 (종족) | 처치할 때마다 공격력이 누적된다 |
| `SpeciesPassives/passive_Bravado.png` | 64x64 | 허세 · 특이 (장비) | 체력이 {0} 이상이면 공격력 +{1} |
| `SpeciesPassives/passive_Bulwark.png` | 64x64 | 방벽 · 계보 (종족) | 피격 시 방어율이 잠시 오른다 |
| `SpeciesPassives/passive_BurnOnAttack.png` | 64x64 | 화염 · 계보 (종족) | 자기가 때린 적을 태운다 |
| `SpeciesPassives/passive_BurstBody.png` | 64x64 | 터지는 몸 · 특이 (장비) | 죽을 때 주변 적을 밀쳐 낸다 |
| `SpeciesPassives/passive_Cataclysm.png` | 64x64 | 대폭발 · 각성 ← 자폭 | 죽을 때 더 넓게, 두 배로 터진다 |
| `SpeciesPassives/passive_ChillOnHit.png` | 64x64 | 냉기 · 계보 (종족) | 자기를 때린 적을 둔화시킨다 |
| `SpeciesPassives/passive_Cleave.png` | 64x64 | 휩쓸기 · 계보 (종족) | 공격 속도 {0} 느려지는 대신, 평타가 대상 주변의 적에게도 {1} 피해를 준다 |
| `SpeciesPassives/passive_Colossus.png` | 64x64 | 거상 · 각성 ← 튼튼함 | 최대 체력 +{0}, 넉백을 받지 않는다 |
| `SpeciesPassives/passive_Embers.png` | 64x64 | 잔불 · 특이 (장비) | 죽을 때 주변 적을 {0:0}초간 태운다 |
| `SpeciesPassives/passive_Executioner.png` | 64x64 | 처형 · 특이 (장비) | 체력이 {0} 이하인 적에게는 치명타가 확정된다 |
| `SpeciesPassives/passive_ExplodeOnDeath.png` | 64x64 | 자폭 · 계보 (종족) | 죽을 때 주변 적에게 피해를 준다 |
| `SpeciesPassives/passive_Fortress.png` | 64x64 | 난공불락 · 각성 ← 방벽 | 피격 시 방어율이 오르고, 방어율 +{0}, 넉백을 받지 않는다 |
| `SpeciesPassives/passive_Frostbite.png` | 64x64 | 동결 · 각성 ← 냉기 | 때린 적을 {0} 둔화시킨다 ({1:0}초) |
| `SpeciesPassives/passive_Gale.png` | 64x64 | 질풍 · 각성 ← 신속 | 공격속도와 이동속도가 {0} 오른다 |
| `SpeciesPassives/passive_GoldRush.png` | 64x64 | 황금 약탈 · 각성 ← 약탈 | 죽을 때 골드를 {0}배 떨군다 |
| `SpeciesPassives/passive_GreatSplit.png` | 64x64 | 대분열 · 각성 ← 분열 | 죽으면 {0}마리로 나뉜다 |
| `SpeciesPassives/passive_HealOnDeath.png` | 64x64 | 치유의 잔재 · 계보 (종족) | 죽을 때 주변 아군을 자기 최대 체력의 {0}만큼 회복시킨다 |
| `SpeciesPassives/passive_Hellfire.png` | 64x64 | 업화 · 각성 ← 화염 | 때린 적을 두 배 넘게, 더 오래 태운다 |
| `SpeciesPassives/passive_Hunger.png` | 64x64 | 굶주림 · 특이 (장비) | 공격 속도 +{0}, 최대 체력 -{1} |
| `SpeciesPassives/passive_IronThorns.png` | 64x64 | 강철 가시 · 각성 ← 가시 껍질 | 받은 피해의 {0}를 되돌려준다 |
| `SpeciesPassives/passive_KingSplit.png` | 64x64 | 왕의 분열 · 계보 (종족) | 죽을 때 권속 {0}마리로 흩어진다 |
| `SpeciesPassives/passive_LifeSeed.png` | 64x64 | 생명의 씨앗 · 각성 ← 치유의 잔재 | 죽을 때 넓은 범위의 아군을 최대 체력의 {0}만큼 회복시킨다 |
| `SpeciesPassives/passive_LoneWolf.png` | 64x64 | 외톨이 · 특이 (장비) | 소환될 때 같은 라인에 다른 아군이 없으면 공격력 +{0} |
| `SpeciesPassives/passive_Loot.png` | 64x64 | 약탈 · 계보 (종족) | 죽을 때 골드를 떨군다 |
| `SpeciesPassives/passive_ManaRelease.png` | 64x64 | 마나 방출 · 계보 (종족) | 죽을 때 마나 +{0} (한 판에 최대 {1}) |
| `SpeciesPassives/passive_ManaResonance.png` | 64x64 | 마나 공명 · 계보 (종족) | 최대 마나 1당 공격력·최대 체력 +{0:0.#}% (소환될 때 정해짐) |
| `SpeciesPassives/passive_PackHunt.png` | 64x64 | 무리 사냥 · 계보 (종족) | 처치할 때마다 이동속도가 누적된다 |
| `SpeciesPassives/passive_Pandemic.png` | 64x64 | 역병 창궐 · 각성 ← 역병 | 죽을 때 더 넓게, 더 오래, 두 배로 중독시킨다 |
| `SpeciesPassives/passive_Photosynthesis.png` | 64x64 | 광합성 · 특이 (장비) | 스킬을 쓸 때마다 최대 체력의 {0}를 회복한다 |
| `SpeciesPassives/passive_PlagueBurst.png` | 64x64 | 역병 · 계보 (종족) | 죽을 때 주변 적을 중독시킨다 |
| `SpeciesPassives/passive_PoisonOnHit.png` | 64x64 | 맹독 피부 · 계보 (종족) | 자기를 때린 적을 중독시킨다 |
| `SpeciesPassives/passive_RallyOnDeath.png` | 64x64 | 최후의 함성 · 계보 (종족) | 죽을 때 주변 아군의 공격력을 올린다 |
| `SpeciesPassives/passive_Rampart.png` | 64x64 | 성벽 · 특이 (장비) | 이동 속도 -{0}, 받는 피해 -{1} |
| `SpeciesPassives/passive_Reassemble.png` | 64x64 | 재조립 · 계보 (종족) | 죽어도 일정 확률로 그 자리에서 한 번 다시 일어난다 |
| `SpeciesPassives/passive_Recoil.png` | 64x64 | 반동 · 특이 (장비) | 평타로 적을 밀쳐 낼 때마다 최대 체력의 {0}를 회복한다 |
| `SpeciesPassives/passive_Regrow.png` | 64x64 | 재생 · 계보 (종족) | 초당 최대 체력의 {0}를 재생한다 |
| `SpeciesPassives/passive_SoulDrain.png` | 64x64 | 생명 흡수 · 계보 (종족) | 준 피해의 일부를 회복한다 |
| `SpeciesPassives/passive_SplitOnDeath.png` | 64x64 | 분열 · 계보 (종족) | 죽으면 절반 크기로 둘로 나뉜다 |
| `SpeciesPassives/passive_Sturdy.png` | 64x64 | 튼튼함 · 계보 (종족) | 최대 체력이 {0} 늘어난다 |
| `SpeciesPassives/passive_Swiftness.png` | 64x64 | 신속 · 계보 (종족) | 공격속도와 이동속도가 {0} 오른다 |
| `SpeciesPassives/passive_ThornOnHit.png` | 64x64 | 가시 껍질 · 계보 (종족) | 피격 시 받은 피해의 일부를 되돌려준다 |
| `SpeciesPassives/passive_TrollBlood.png` | 64x64 | 트롤의 피 · 각성 ← 재생 | 초당 최대 체력의 {0}를 재생, 체력이 {1} 이하면 두 배 |
| `SpeciesPassives/passive_Undying.png` | 64x64 | 불사의 뼈 · 각성 ← 재조립 | 죽으면 반드시 그 자리에서 한 번 다시 일어난다 |
| `SpeciesPassives/passive_Vampire.png` | 64x64 | 흡혈귀 · 각성 ← 생명 흡수 | 준 피해를 흡수하고, 처치할 때마다 체력을 회복한다 |
| `SpeciesPassives/passive_Vengeance.png` | 64x64 | 복수 · 특이 (장비) | 같은 종족 아군이 죽을 때마다 공격력 +{0} (최대 {1}번) |
| `SpeciesPassives/passive_Venom.png` | 64x64 | 맹독 · 각성 ← 맹독 피부 | 때린 적을 강하게 중독시키고 받은 피해 일부를 되돌려준다 |
| `SpeciesPassives/passive_VitalStrike.png` | 64x64 | 급소 찌르기 · 특이 (장비) | 치명타는 방어율을 무시한다 |
| `SpeciesPassives/passive_Volley.png` | 64x64 | 연사 · 계보 (종족) | 한 번에 두 발을 쏜다 |
| `SpeciesPassives/passive_WarDrum.png` | 64x64 | 전쟁의 북 · 각성 ← 최후의 함성 | 죽을 때 주변 아군 공격력 +{0} · 공격 속도 +{1} |

**단계 패시브 (장비 Lv4·Lv5 가 여는 스탯 I·II·III)** — 한 계열 세 장은 같은 그림에 단계 표식만 다르다

| 계열 | 파일 |
|---|---|
| AttackSpeedUp | `passive_AttackSpeedUp1`, `passive_AttackSpeedUp2`, `passive_AttackSpeedUp3` |
| AttackUp | `passive_AttackUp1`, `passive_AttackUp2`, `passive_AttackUp3` |
| CooldownUp | `passive_CooldownUp1`, `passive_CooldownUp2`, `passive_CooldownUp3` |
| CritDmgUp | `passive_CritDmgUp1`, `passive_CritDmgUp2`, `passive_CritDmgUp3` |
| CritUp | `passive_CritUp1`, `passive_CritUp2`, `passive_CritUp3` |
| DefenseUp | `passive_DefenseUp1`, `passive_DefenseUp2`, `passive_DefenseUp3` |
| HpUp | `passive_HpUp1`, `passive_HpUp2`, `passive_HpUp3` |
| PierceUp | `passive_PierceUp1`, `passive_PierceUp2`, `passive_PierceUp3` |

### 런 특성

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `RunPerks/perk_AffinityExpand.png` | 64x64 | 친화 확장 | 덱의 비친화 종족 하나가 친화가 된다 |
| `RunPerks/perk_AffinityReinforce.png` | 64x64 | 친화 증원 | 친화 종족 카드가 부르는 마릿수 +2 |
| `RunPerks/perk_Ambush.png` | 64x64 | 매복 | 판 시작 {0:0}초간 배출을 멈추고, 라인마다 처음 {1}마리 공/체 +{2:0}% |
| `RunPerks/perk_Appraisal.png` | 64x64 | 감식안 | 카드 보상 선택지 3개 → 4개 |
| `RunPerks/perk_BloodPact.png` | 64x64 | 피의 계약 | 마나가 모자라면 모자란 만큼 마왕성 체력으로 낸다 (체력 1 은 남긴다) |
| `RunPerks/perk_CenterPush.png` | 64x64 | 주공 | 가운데(3번) 라인 몬스터의 공/체 +35% |
| `RunPerks/perk_CheapAffinity.png` | 64x64 | 친화 할인 | 친화 종족의 소환 비용 −2 |
| `RunPerks/perk_CheapAll.png` | 64x64 | 만물 할인 | 모든 몬스터의 소환 비용 −1 |
| `RunPerks/perk_Crystallize.png` | 64x64 | 마력 결정화 | 스테이지를 넘길 때 남은 마나의 {0:0}%가 최대 마나로 쌓인다 (최대 +{1:0}) |
| `RunPerks/perk_CursedGold.png` | 64x64 | 저주받은 금화 | 런 골드 획득 +{0:0}%, 스테이지를 넘길 때마다 마왕성 체력 −{1} |
| `RunPerks/perk_DeepChannel.png` | 64x64 | 심연 공명 | 친화 종족 소환력 ×1.2 → ×1.6, 비친화 종족 ×1.0 → ×0.8 |
| `RunPerks/perk_DeepVessel.png` | 64x64 | 깊은 그릇 | 최대 마나 +25% |
| `RunPerks/perk_ExtraSlots.png` | 64x64 | 확장 편성 | 카드 칸 +2 (최대 8칸) |
| `RunPerks/perk_Extremity.png` | 64x64 | 극단 | 켜진 시너지가 3종 이하면 시너지의 스탯 효과 +50% |
| `RunPerks/perk_FewButElite.png` | 64x64 | 소수정예 | 몬스터가 가장 적은 라인에 나오면 공/체 +100% (나올 때 정해짐) |
| `RunPerks/perk_Flank.png` | 64x64 | 측면 | 바깥(1·5번) 라인 몬스터의 이동속도 +30%, 공격력 +15% |
| `RunPerks/perk_Focus.png` | 64x64 | 집중 | 시그니처 스킬의 스테이지당 사용 횟수 +1 |
| `RunPerks/perk_FreeFirstSummon.png` | 64x64 | 첫 소환 무료 | 스테이지마다 첫 몬스터 카드 1회는 마나를 쓰지 않는다 |
| `RunPerks/perk_GlassKeep.png` | 64x64 | 유리 성채 | 얻는 순간 마왕성 최대 체력 −{0:0}%, 소환력 +{1:0}% |
| `RunPerks/perk_Hoard.png` | 64x64 | 비축 | 마나를 한 번도 쓰지 않고 스테이지를 넘기면 최대 마나 +10 (최대 +50) |
| `RunPerks/perk_Homecoming.png` | 64x64 | 귀환 | 카드로 소환한 몬스터가 죽으면 30% 확률로 제 라인 대기열에 돌아간다 |
| `RunPerks/perk_Horde.png` | 64x64 | 군세 | 나올 때 같은 라인에 선 몬스터 1마리당 공/체 +0.1% |
| `RunPerks/perk_LateBloom.png` | 64x64 | 대기만성 | 스테이지를 넘길 때마다 모든 몬스터 공/체 +1.5% (최대 +45%) |
| `RunPerks/perk_ManaSurge.png` | 64x64 | 마력 폭주 | 마나를 0까지 쓰면 그 스테이지 동안 새로 나오는 몬스터의 소환력 +30% |
| `RunPerks/perk_Meditation.png` | 64x64 | 명상 | 스테이지마다 회복되는 마나 +{0:0}% |
| `RunPerks/perk_Menagerie.png` | 64x64 | 백귀 | 덱에 든 몬스터 종족 1종당 모든 몬스터 공/체 +5% |
| `RunPerks/perk_Overflow.png` | 64x64 | 넘치는 그릇 | 몬스터가 나올 때 보유 마나 10당 그 몬스터 공/체 +{0:0}% (나올 때 정해짐) |
| `RunPerks/perk_OverloadFrenzy.png` | 64x64 | 뒤집힌 과부하 | 이번 판 과부하 1단계마다 해당 몬스터 전부 공/체 +{0:0}% (대기열에서 나올 때 정해짐) |
| `RunPerks/perk_Patience.png` | 64x64 | 기다림의 미학 | 판이 열린 뒤 늦게 나올수록 1초당 공/체 +{0:0}% (최대 +{1:0}%) |
| `RunPerks/perk_QuickStudy.png` | 64x64 | 속성 | 새로 얻는 카드가 Lv2 로 들어온다 |
| `RunPerks/perk_RapidDrain.png` | 64x64 | 과잉 소환 | 라인에서 몬스터가 나오는 간격 −40% |
| `RunPerks/perk_Reinforce.png` | 64x64 | 증원 | 카드 한 장이 부르는 마릿수 +1 |
| `RunPerks/perk_Reinforcements.png` | 64x64 | 원군 | 전투가 시작된 뒤 소환하면 비용 +2, 그 몬스터의 공/체 +100% |
| `RunPerks/perk_Rend.png` | 64x64 | 파쇄 | 공격이 맞을 때마다 대상 최대 체력의 2% 추가 피해 (공격력의 3배까지) |
| `RunPerks/perk_Resonance.png` | 64x64 | 공명 | 켜진 시너지 하나당 모든 몬스터 공/체 +3% |
| `RunPerks/perk_Scales.png` | 64x64 | 저울 | 보유 마나가 50% 이하면 소환 비용 −2, 50%를 넘으면 +1 |
| `RunPerks/perk_SealedSlot.png` | 64x64 | 봉인된 칸 | 빈 카드 칸 하나를 없앤다. 모든 몬스터 공/체 +{0:0}% |
| `RunPerks/perk_SingleWell.png` | 64x64 | 한 우물 | 필드·대기열에 한 가지 몬스터만 있으면 그 몬스터의 시너지 전부 카운트 +{0} (스킬 소환 제외) |
| `RunPerks/perk_TrophyMana.png` | 64x64 | 강적의 정수 | 엘리트·보스를 쓰러뜨릴 때마다 마나 +{0} |
| `RunPerks/perk_TwinLane.png` | 64x64 | 쌍둥이 라인 | 카드를 내면 옆 라인에도 절반(내림)이 선다. 소환 비용 +{0:0} |
| `RunPerks/perk_Vanguard.png` | 64x64 | 선발대 | 스테이지마다 라인별 처음 10마리는 나오는 간격 −90% |
| `RunPerks/perk_Weighted.png` | 64x64 | 편중 | 카운트가 가장 높은 시너지의 카운트 +1 |

### 소환사 개성

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `RunPerks/sperk_AffinityGrade.png` | 64x64 | 품질 각인 **(쓰는 소환사 없음 — 만들지 말 것)** | 친화 종족의 품질이 {0}단계 높게 나온다 |
| `RunPerks/sperk_ArcaneMight.png` | 64x64 | 마력 증폭 | 최대 마나 10당 모든 몬스터 공/체 +{0}% (소환될 때 정해짐) |
| `RunPerks/sperk_BoneLegion.png` | 64x64 | 뼈의 군단 | 친화 종족 카드가 부르는 마릿수 +{0} |
| `RunPerks/sperk_CheapAffinity.png` | 64x64 | 친화 할인 | 친화 종족의 소환 마나 −{0:0.#} |
| `RunPerks/sperk_Crystallize.png` | 64x64 | 결정화 | 스테이지를 넘길 때 남은 마나의 {0}%가 최대 마나로 쌓인다 (최대 +{1:0}) |
| `RunPerks/sperk_DeepChannel.png` | 64x64 | 심연 공명 | 친화 종족에게 소환력이 ×{0:0.##} 로 더 실린다 |
| `RunPerks/sperk_FleshGolem.png` | 64x64 | 시체 합성 | 아군 시체가 쌓이면 거인이 일어난다 |
| `RunPerks/sperk_Muster.png` | 64x64 | 총동원 | 친화 종족 카드가 부르는 마릿수 +{0} |
| `RunPerks/sperk_NatureRestore.png` | 64x64 | 자연의 회복 | 스테이지를 넘길 때 최대 마나의 {0}%를 더 회복한다 |
| `RunPerks/sperk_PackBond.png` | 64x64 | 무리 결속 | 친화 종족이 사냥할수록 강해진다 |
| `RunPerks/sperk_PlagueRise.png` | 64x64 | 역병 | 친화 종족이 적을 쓰러뜨리면 {0}% 확률로 그 자리에 좀비가 일어난다 |
| `RunPerks/sperk_Plunder.png` | 64x64 | 약탈 | 런 골드를 ×{0:0.##} 번다 |
| `RunPerks/sperk_RaiseOnDeath.png` | 64x64 | 사자 부활 | 몬스터가 죽은 자리에 스켈레톤이 공짜로 일어난다 |
| `RunPerks/sperk_Regenerate.png` | 64x64 | 재생 | 친화 종족이 맞으면서 체력을 회복한다 |
| `RunPerks/sperk_SlimeSpit.png` | 64x64 | 점액 사격 | 부른 친화 종족 {0}마리 중 1마리는 점액을 뱉는 원거리로 선다 |
| `RunPerks/sperk_SwellOnRepeat.png` | 64x64 | 증식 **(쓰는 소환사 없음 — 만들지 말 것)** | 친화 종족을 연달아 부르면 점점 커진다 |
| `RunPerks/sperk_TwinCall.png` | 64x64 | 쌍둥이 소집 | 카드를 낼 때 옆 라인에도 {0}마리가 공짜로 선다 |
| `RunPerks/sperk_WarCry.png` | 64x64 | 전쟁 함성 **(쓰는 소환사 없음 — 만들지 말 것)** | 친화 종족이 싸울수록 공격력이 누적된다 |
| `RunPerks/sperk_WildSprint.png` | 64x64 | 야성 질주 **(쓰는 소환사 없음 — 만들지 말 것)** | 친화 종족의 이동속도 ×{0:0.##} |

### 시너지

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `Synergies/synergy_Beast.png` | 64x64 | 야수 | 금: 넉백에 밀리지 않는다 |
| `Synergies/synergy_Ferocity.png` | 64x64 | 투지 | 금: 처치 누적 상한이 {0}회로 풀리고, 스테이지를 넘겨 유지된다 |
| `Synergies/synergy_Forest.png` | 64x64 | 숲 | 금: 죽어도 제 라인 대기열로 돌아온다 |
| `Synergies/synergy_Hunt.png` | 64x64 | 사냥 | 금: 치명타가 방어율을 무시한다 |
| `Synergies/synergy_Marksman.png` | 64x64 | 사격 | 금: 투사체가 하나 더 나간다 (피해 {0}) |
| `Synergies/synergy_Plague.png` | 64x64 | 역병 | 금: 죽을 때 주변 적에게 역병이 퍼진다 |
| `Synergies/synergy_Regrowth.png` | 64x64 | 재생 | 금: 치명상을 입어도 한 번은 체력 1 로 버틴다 |
| `Synergies/synergy_Royal.png` | 64x64 | 왕권 | 금: 권속이 하나 더 나온다 |
| `Synergies/synergy_Sorcery.png` | 64x64 | 술법 | 금: 스킬을 준비한 채로 소환된다 |
| `Synergies/synergy_Steel.png` | 64x64 | 강철 | 금: 한 번에 최대 체력의 {0} 를 넘게 잃지 않는다 |
| `Synergies/synergy_Swarm.png` | 64x64 | 무리 | 금: 무리 카드에서 한 마리씩 더 나온다 |
| `Synergies/synergy_Undead.png` | 64x64 | 언데드 | 금: 부활한 개체가 한 번 더 부활한다 |
| `Synergies/synergy_Vanguard.png` | 64x64 | 선봉 | 금: 첫 공격이 주변 적을 {0:0.#}초 기절시킨다 |

### 갈림길 칸

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `RunNodes/node_Altar.png` | 256x256 | 제단 | 카드 하나를 제물로 바쳐 시너지 카운트를 남긴다 |
| `RunNodes/node_Camp.png` | 256x256 | 야영지 | 마왕성 수리(무료) 또는 증축 {0} G |
| `RunNodes/node_EliteBattle.png` | 256x256 | 험로 | 엘리트 용사가 섞이고 부대가 한 줄 는다. 대신 특성 1택 + 처치 골드 ×{0} |
| `RunNodes/node_Event.png` | 256x256 | 이벤트 | 무엇이 일어날지 모른다. 값은 마왕성 체력으로 치른다 |
| `RunNodes/node_Forge.png` | 256x256 | 강화소 | 골드 {0} 로 카드 하나를 강화한다 |
| `RunNodes/node_NormalBattle.png` | 256x256 | 진군 | 평범한 용사 부대. 잡은 만큼 골드가 들어온다 |
| `RunNodes/node_Shop.png` | 256x256 | 상점 | 카드·특성·마력의 정수를 골드로 산다 |

### 갈림길 이벤트

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `RunEvents/event_BloodMoon.png` | 256x256 | 핏빛 달 | 달이 붉다. 성벽 아래 것들이 평소보다 크게 운다. |
| `RunEvents/event_BrokenCircle.png` | 256x256 | 깨진 소환진 | 금 간 마법진이 아직 희미하게 돈다. 한 획만 다시 그으면 될 것 같다. |
| `RunEvents/event_BrokenNest.png` | 256x256 | 버려진 둥지 | 우리 세 개가 나란히 놓여 있다. 아직 온기가 남았다. |
| `RunEvents/event_BrokenPens.png` | 256x256 | 버려진 사육장 | 부서진 우리가 늘어서 있다. 바닥에 발자국이 겹겹이 남았다. |
| `RunEvents/event_HeroGraves.png` | 256x256 | 용사들의 무덤 | 비석도 없는 흙더미가 줄지어 있다. 아직 갑옷 냄새가 난다. |
| `RunEvents/event_HungryIdol.png` | 256x256 | 굶주린 우상 | 입을 벌린 돌 우상이 서 있다. 무엇이든 삼킬 것처럼 생겼다. |
| `RunEvents/event_InvertedGlass.png` | 256x256 | 뒤집힌 모래시계 | 모래가 위로 흐른다. 들여다보면 성벽이 조금씩 얇아지는 것이 보인다. |
| `RunEvents/event_LostLibrary.png` | 256x256 | 잊힌 서고 | 곰팡내 나는 서가가 줄지어 섰다. 펼치면 무언가 읽는 쪽도 읽는다. |
| `RunEvents/event_MinerShaft.png` | 256x256 | 광부의 갱도 | 버려진 갱도가 어둠 속으로 이어진다. 아래쪽에서 쇳내가 올라온다. |
| `RunEvents/event_OfferingMark.png` | 256x256 | 제물의 흔적 | 누군가 먼저 다녀갔다. 마른 자국이 아직 표식의 모양을 하고 있다. |
| `RunEvents/event_RoamingSmith.png` | 256x256 | 떠돌이 대장장이 | 화덕도 없이 망치만 든 자가 앉아 있다. \ |
| `RunEvents/event_SealedStaff.png` | 256x256 | 봉인된 지팡이 | 돌무더기 아래 지팡이 한 자루가 박혀 있다. 뽑으면 대가를 요구할 눈치다. |
| `RunEvents/event_StrayMonster.png` | 256x256 | 길 잃은 몬스터 | 무리에서 떨어진 것이 성문 앞에 웅크려 있다. 굶었다. |
| `RunEvents/event_SupplyCart.png` | 256x256 | 버려진 보급 수레 | 용사들이 두고 간 수레가 옆으로 넘어져 있다. 바퀴가 아직 성하다. |
| `RunEvents/event_WanderingSoul.png` | 256x256 | 떠도는 혼 | 형체 없는 것이 성벽 주위를 돈다. 들어오고 싶어 하는 눈치다. |

### 유물 트리 노드

효과 문구는 `RelicTreeCatalog.cs` 의 해당 줄과 `Docs/RelicTree.md` 를 본다.

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `RelicTree/node_attunement.png` | 128x128 | 조율 | Dominion · SynergyStackBonus |
| `RelicTree/node_bespoke.png` | 128x128 | 맞춤 제작 | Brood · GearStatBonus |
| `RelicTree/node_call_horn.png` | 128x128 | 부름의 나팔 | Dominion · DrainSpeedBonus |
| `RelicTree/node_carapace.png` | 128x128 | 굳은 껍질 | Brood · MaxHp |
| `RelicTree/node_claw.png` | 128x128 | 날카로운 발톱 | Brood · Attack |
| `RelicTree/node_death_toll.png` | 128x128 | 죽음의 대가 | Brood · DeathTriggerPower |
| `RelicTree/node_deep_spring.png` | 128x128 | 깊은 샘 | Font · ManaCapacityBonus |
| `RelicTree/node_demon_horde.png` | 128x128 | 마왕의 군세 | Brood · Attack+MaxHp |
| `RelicTree/node_demon_majesty.png` | 128x128 | 마왕의 위엄 | Keep · SummonerStrikeBonus |
| `RelicTree/node_disarm.png` | 256x256 | 무력화 | Keep · EnemyAttackReduction |
| `RelicTree/node_doom_prophecy.png` | 256x256 | 몰락의 예언 | Keep · EnemyMaxHpReduction |
| `RelicTree/node_early_bloom.png` | 128x128 | 조기 성장 | Dominion · NewCardLevel |
| `RelicTree/node_endless_swarm.png` | 128x128 | 끝없는 무리 | Brood · SummonCountBonus |
| `RelicTree/node_endless_well.png` | 128x128 | 마르지 않는 우물 | Font · ManaCapacityBonus |
| `RelicTree/node_eternal_throne.png` | 128x128 | 영원한 옥좌 | Keep · CoreHpBonus |
| `RelicTree/node_fang.png` | 128x128 | 사나운 이빨 | Brood · Attack |
| `RelicTree/node_fear_brand.png` | 256x256 | 공포의 각인 | Keep · EnemyAttackReduction |
| `RelicTree/node_feral_memory.png` | 128x128 | 치유의 기억 | Brood · SpeciesPassivePower |
| `RelicTree/node_flowing_mana.png` | 128x128 | 흐르는 마력 | Font · ManaRegenBonus |
| `RelicTree/node_forge_memory.png` | 128x128 | 대장간의 기억 | Brood · GearBoxBonus |
| `RelicTree/node_frugality.png` | 128x128 | 절약 | Font · SummonCostCut |
| `RelicTree/node_infinite_vessel.png` | 128x128 | 무한의 그릇 | Font · ManaCapacityBonus |
| `RelicTree/node_insight.png` | 128x128 | 안목 | Dominion · ShopPriceCut |
| `RelicTree/node_last_drop.png` | 128x128 | 마지막 한 방울 | Font · SummonCostCut |
| `RelicTree/node_legacy_price.png` | 128x128 | 전승의 대가 | Dominion · PerkChoiceCount |
| `RelicTree/node_meditation_crystal.png` | 128x128 | 명상의 결정 | Font · ManaRegenBonus |
| `RelicTree/node_moment_mastery.png` | 256x256 | 찰나의 지배 | Dominion · BattleSpeedUnlock |
| `RelicTree/node_old_pact.png` | 128x128 | 오래된 계약 | Dominion · StartingPerkCount |
| `RelicTree/node_open_gate.png` | 128x128 | 성문 개방 | Dominion · DrainSpeedBonus |
| `RelicTree/node_origin.png` | 256x256 | 마왕의 각인 | Root · Attack+MaxHp |
| `RelicTree/node_patience.png` | 128x128 | 인내 | Font · OverloadRelief |
| `RelicTree/node_plague_lore.png` | 128x128 | 역병의 지혜 | Brood · DotDamageBonus |
| `RelicTree/node_plunder_hand.png` | 128x128 | 약탈의 손 | Dominion · GoldGainBonus |
| `RelicTree/node_predator.png` | 128x128 | 포식자 | Brood · CritChance+CritDamage |
| `RelicTree/node_rebuild.png` | 128x128 | 재건 | Keep · CoreRegenPerStage |
| `RelicTree/node_resonance_tome.png` | 128x128 | 공명의 서 | Dominion · SynergyStackBonus |
| `RelicTree/node_sealed_wall.png` | 128x128 | 봉인된 성벽 | Keep · CoreHpBonus |
| `RelicTree/node_slow_march.png` | 128x128 | 느려진 진군 | Keep · EnemyMoveReduction |
| `RelicTree/node_soul_urn.png` | 256x256 | 영혼의 항아리 | Dominion · ReincarnPointBonus |
| `RelicTree/node_split_legacy.png` | 128x128 | 분열의 유산 | Brood · DerivedScaleBonus |
| `RelicTree/node_sprint.png` | 128x128 | 질주 | Dominion · MoveSpeed |
| `RelicTree/node_swarm_call.png` | 128x128 | 무리의 부름 | Brood · SummonCountBonus |
| `RelicTree/node_thick_gate.png` | 128x128 | 두꺼운 성문 | Keep · CoreHpBonus |
| `RelicTree/node_thick_hide.png` | 128x128 | 두꺼운 가죽 | Brood · Defense |
| `RelicTree/node_time_reins.png` | 256x256 | 시간의 고삐 | Dominion · BattleSpeedUnlock |
| `RelicTree/node_trial_baptism.png` | 256x256 | 시련의 세례 | Keep · EnemyMaxHpReduction |
| `RelicTree/node_unbroken_keep.png` | 128x128 | 불락의 성 | Keep · CoreHpBonus |
| `RelicTree/node_undying_flesh.png` | 128x128 | 불사의 살점 | Brood · MaxHp |
| `RelicTree/node_war_camp.png` | 128x128 | 전열 확장 | Dominion · DeckSlotBonus |
| `RelicTree/node_wide_vessel.png` | 128x128 | 넓은 그릇 | Font · ManaCapacityBonus |
| `RelicTree/node_wither_curse.png` | 256x256 | 쇠약의 저주 | Keep · EnemyMaxHpReduction |

### 액티브 스킬

한 파일을 여러 스킬이 빌려 쓰면 이름이 `/` 로 이어진다.

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `Skills/skill_arrow_rain.png` | 48x48 | 화살 비 | 타겟 위치에 5초 동안 화살 비를 내려 0.4초마다 공격력 50% 범위 지속 피해. |
| `Skills/skill_arrow_storm.png` | 48x48 | 화살 폭풍 | 전방으로 화살 산탄을 3연발 퍼붓는다. 발사 중에는 그 자리에 서며, |
| `Skills/skill_battle_cry.png` | 48x48 | 전투 함성 | 반경 5m 내 모든 아군의 공격력을 8초 동안 1.3배로 증가. |
| `Skills/skill_berserker.png` | 48x48 | 광전사 / 광폭화 | 시전자와 소속 병사 전체의 공격속도를 8초 동안 1.8배로 증가. / 1분마다 광폭화가 한 겹씩 영구히 쌓인다. 겹당 공격력 +50%, |
| `Skills/skill_bind.png` | 48x48 | 속박 | 현재 타겟을 3초 동안 행동불능으로 만들고 매초 공격력 30% 지속 피해를 가한다. |
| `Skills/skill_bisect.png` | 48x48 | 일도양단 | 전방 직선 위의 적을 2초간 얼어붙게 한 뒤 한 번에 베어 넘긴다. |
| `Skills/skill_blizzard.png` | 48x48 | 블리자드 | 타겟 위치에 블리자드 지대. 이동속도·공격속도 감소 + 0.5초마다 공격력 40% 지속 피해. |
| `Skills/skill_blood_price.png` | 48x48 | 피의 대가 | 현재 체력의 30%를 태워 전방을 쓸어버린다. 잃은 체력의 250% + |
| `Skills/skill_boss_charge.png` | 48x48 | 돌진 | 몸을 날려 적진을 관통한다. 경로 위의 적을 한 번씩 밀어내며 |
| `Skills/skill_boss_slam.png` | 48x48 | 분쇄 강타 | 도약해 내려찍으며 발밑을 분쇄한다. 반경 7 안의 적에게 |
| `Skills/skill_bulwark.png` | 48x48 | 불멸의 방벽 | 5초간 아군 전체의 방어율을 45%p 끌어올린다. 방벽이 무너질 때 |
| `Skills/skill_chain_lightning.png` | 48x48 | 연쇄 번개 | 번개가 맞은 적마다 둘로 갈라지며 4번 번진다 (최대 15명). |
| `Skills/skill_charge_soldier.png` | 48x48 | 돌격 병사 | 후방에 돌격 병사 3명을 소환해 전방으로 돌진. 경로 위 적에게 공격력×200% 피해 + 넉백. |
| `Skills/skill_death_sentence.png` | 48x48 | 사형 선고 | 범위 내 적을 즉시 선고·처형한다. |
| `Skills/skill_gravestone.png` | 48x48 | 비석 강림 | 비석 12기가 순서대로 우수수 떨어져 꽂힌다. 착탄 지점마다 |
| `Skills/skill_gravity_collapse.png` | 48x48 | 중력 붕괴 / 마나 폭발 | 붕괴점을 만들어 2.5초간 적을 한곳으로 빨아들이며 발을 묶는다.  / 화면 전체의 적에게 최대 체력의 30% 피해를 주고, 남은 마나의 절반을 태워 |
| `Skills/skill_heal_aura.png` | 48x48 | 치유 오라 / 치유 점액 | 피해 입은 아군 장군 중 랜덤 1명과 그 휘하 병사 전체의 체력을 최대 HP의 25% 즉시 회복. / 주변의 다친 몬스터 아군 최대 6마리를 체력이 낮은 순으로 |
| `Skills/skill_heavy_strike.png` | 48x48 | 강타 | 사정거리 내 적을 즉시 강타. 공격력 500% 단일 타격 + 강한 넉백. |
| `Skills/skill_iron_shield.png` | 48x48 | 철벽 방어 | 시전자의 방어율을 8초 동안 +30% 증가. 지속 시간 동안 도발 상태가 되어 적의 우선 타겟이 됨. |
| `Skills/skill_leap_strike.png` | 48x48 | 도약 강타 | 전방으로 도약하여 착지 반경 내 모든 적을 공격력 250% 강타 + 넉백. |
| `Skills/skill_meteor.png` | 48x48 | 메테오 / 화염 오라 | 1.5초 후 타겟 위치에 메테오 낙하. 공격력 500% AoE 피해 + 강한 넉백. / 몸 주위에 불길이 번져 따라다닌다. 범위 안의 적에게 |
| `Skills/skill_piercing_dash.png` | 48x48 | 관통 돌진 | 전방으로 짧게 돌진하며 직선 위의 적을 모두 관통 타격한다. |
| `Skills/skill_poison_zone.png` | 48x48 | 독성 지대 | 타겟 위치에 독성 지대 생성. 이동속도 50% 감소 + 0.5초마다 공격력 30% 지속 피해. |
| `Skills/skill_sacrifice_soldier.png` | 48x48 | 병사 희생 | 체력 최저 병사를 즉사시키고, 그 공격력의 80%를 시전자 공격력으로 흡수한다.\n |
| `Skills/skill_shockwave.png` | 48x48 | 충격파 | 전방 120도 부채꼴 범위의 모든 적에게 공격력 150% 피해 + 강한 넉백. |
| `Skills/skill_suicide_soldier.png` | 48x48 | 자폭 병사 | 병사를 포물선 궤도로 던져 착탄 시 공격력 300% 범위 폭발 피해 + 넉백. |
| `Skills/skill_summon_elite.png` | 48x48 | 정예 소환 | 시전자 스텟 200% 수준의 정예 병사 3기를 소환한다. |
| `Skills/skill_summon_signature.png` | 48x48 | 권속 소환 / 권속 소환 | 소환사의 대표 권속을 마나 없이 불러낸다.  / 제 발밑에 하위 종족을 불러낸다. 마나를 쓰지 않는다.\n |
| `Skills/skill_summon_skeleton.png` | 48x48 | 스켈레톤 소환 | 시전자 스텟 70% 수준의 스켈레톤 2기를 소환한다. |
| `Skills/skill_swift_strike.png` | 48x48 | 신속 연격 | 시전자와 소속 병사 전체의 공격속도를 6초 동안 2배로 증가. |
| `Skills/skill_target_heal.png` | 48x48 | 집중 치유 | 아군 장군 중 체력 비율이 가장 낮은 장군 1명을 최대 HP의 40% 집중 치유. |
| `Skills/skill_volley_fire.png` | 48x48 | 일제 사격 | 제너럴과 소속 병사 전체가 공격력 150%로 현재 타겟에 즉시 공격을 발동한다. |
| `Skills/skill_war_banner.png` | 48x48 | 군기 강림 | 군기를 세워 주변 아군의 공격력·공격속도를 40%, 이동속도를 15% |

### 원작 패시브 (카드 레벨 패시브 · 용사 패시브)

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `Passives/passive_BerserkerPact.png` | 48x48 | 광전사의 맹약 | 전체 공격력·공격속도 +25%. 방어율 -15%. |
| `Passives/passive_BloodPact.png` | 48x48 | 피의 계약 | 장군 체력이 낮을수록 공격력 최대 +50%. |
| `Passives/passive_CommanderFury.png` | 48x48 | 지휘관의 분노 | 장군 크리티컬 확률 +15%, 크리티컬 배율 +0.5. |
| `Passives/passive_CounterStrike.png` | 48x48 | 피격 반격 | 피격 시 40% 확률로 공격력 +20%를 5초 동안 버프. |
| `Passives/passive_DefenseShield.png` | 48x48 | 방어 강화 | 피격 시 방어율 +10%를 3초 동안 버프. |
| `Passives/passive_ExtraSoldiers.png` | 48x48 | 병사 수 +5명 | 장군 소속 병사 수를 5명 추가합니다. |
| `Passives/passive_FocusedFire.png` | 48x48 | 집중 사격 | 공격속도 0.1당 치명타 확률 +0.5%. |
| `Passives/passive_GeneralCombatBoost.png` | 48x48 | 장군 전투 강화 | 장군 공격력·이동속도 +15%. |
| `Passives/passive_GoldenPower.png` | 48x48 | 황금의 힘 | 전투 시작 시 보유 골드 300당 공격력·최대체력 +1%. |
| `Passives/passive_IronWill.png` | 48x48 | 강철 의지 | 체력 50% 이하가 되면 체력 +20% (늘어난 만큼 회복)·공격력 +10% (1회). |
| `Passives/passive_KillEmpower.png` | 48x48 | 처치 강화 | 처치마다 공격력 +10 누적 (최대 5스택). |
| `Passives/passive_KillHeal.png` | 48x48 | 처치 회복 | 처치 시 최대체력의 5%를 즉시 회복. |
| `Passives/passive_KillMomentum.png` | 48x48 | 처치 가속 | 처치마다 이동속도 +0.15 누적 (최대 5스택). |
| `Passives/passive_LastStand.png` | 48x48 | 최후의 항전 | 병사 수가 초기의 50% 이하 시 남은 병사 공격력·체력 +30%·+20% (1회). |
| `Passives/passive_LootHunter.png` | 48x48 | 전리품 사냥 | 적 처치 시 골드 +15 획득. |
| `Passives/passive_QuickRecovery.png` | 48x48 | 긴급 회복 | 피격 시 최대체력의 0.5%를 즉시 회복. |
| `Passives/passive_SacrificeAbsorb.png` | 48x48 | 희생 흡수 | 병사 사망 시 사망 1명당 즉시 체력 +30 회복. |
| `Passives/passive_SacrificeRitual.png` | 48x48 | 희생 의식 | 병사 5명 희생 → 장군 공격력·체력 +20%. |
| `Passives/passive_ShieldEdge.png` | 48x48 | 방패의 날 | 방어율 10%당 공격력 +4%. |
| `Passives/passive_SkillAdrenaline.png` | 48x48 | 스킬 아드레날린 | 스킬 사용 시 공격력 +20·공격속도 +0.3을 5초 동안 버프. |
| `Passives/passive_SkillInstinct.png` | 48x48 | 생존 본능 | 스킬 사용 시 50% 확률로 최대체력의 8%를 즉시 회복. |
| `Passives/passive_SkillRally.png` | 48x48 | 전투 집결 | 스킬 사용 시 병사 전체 공격력 +20·이동속도 +0.5를 6초 동안 버프. |
| `Passives/passive_Slaughterer.png` | 48x48 | 도살자 | 처치마다 현재 공격력의 3% 추가 누적 (최대 10스택). |
| `Passives/passive_SoldierCombatBoost.png` | 48x48 | 병사 전투 강화 | 병사의 공격력과 이동속도를 각각 20%·10% 증가시킵니다. |
| `Passives/passive_SoldierDeathEmpower.png` | 48x48 | 병사의 유산 | 병사 사망 1명당 장군 공격력 +2%, 체력 +1%. |
| `Passives/passive_SoldierEmpowerGeneral.png` | 48x48 | 병력의 힘 | 병사 1명당 장군 공격력·체력 +1%. |
| `Passives/passive_SoldierHorde.png` | 48x48 | 군중 전술 | 병사 수 +10명. 단, 병사 공격력·체력 -10%. |
| `Passives/passive_SoldierMorale.png` | 48x48 | 병사 고무 | 공격 시 30% 확률로 병사 전체 공격력 +12를 3초 동안 버프. |
| `Passives/passive_SoldierVigor.png` | 48x48 | 병사 결의 | 처치 시 병사 전체 공격력 +15를 4초 동안 버프. |
| `Passives/passive_SteelBody.png` | 48x48 | 강철 체력 | 전투 시작 시 최대체력의 3%를 공격력으로 전환. |
| `Passives/passive_StrengthStack.png` | 48x48 | 연격 스택 | 연속 공격마다 공격력 +8 누적 (최대 5스택). |
| `Passives/passive_StrongGeneralWeakSoldier.png` | 48x48 | 강한 장군, 약한 병사 | 병사 공격력·체력 -20%. 장군 공격력·체력 +30%. |
| `Passives/passive_SwiftAssault.png` | 48x48 | 속전속결 | 이동속도 증가분만큼 공격속도도 동일하게 증가. |
| `Passives/passive_TitanGeneral.png` | 48x48 | 거인 장군 | 장군 체력·공격력 +30%·+20%. 공격·이동속도 -15%. 크기 +30%. |
| `Passives/passive_UnityStrength.png` | 48x48 | 결속의 힘 | 병사 1명당 장군 공격력·체력 +1.5%. |
| `Passives/passive_VampiricStrike.png` | 48x48 | 흡혈 공격 | 공격 시 가한 피해의 10%를 즉시 체력 회복. |
| `Passives/passive_VanguardAura.png` | 48x48 | 선봉 오라 | 병사 방어율 +10%. |
| `Passives/passive_WeakGeneralMoreSoldiers.png` | 48x48 | 희생의 지휘 | 장군 공격력·체력 -15%. 병사 수 +8명. |
| `Passives/passive_WeakGeneralStrongSoldier.png` | 48x48 | 약한 장군, 강한 병사 | 장군 공격력·체력 -20%. 병사 공격력·체력 +30%. |
| `Passives/passive_WideRange.png` | 48x48 | 사거리 확장 | 사정거리 +10%. |

### 몬스터 장비 부위

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `Gear/gear_Armor.png` | 64x64 | 갑옷 |  |
| `Gear/gear_Back.png` | 64x64 | 등짐 |  |
| `Gear/gear_Bulk.png` | 64x64 | 덩치 |  |
| `Gear/gear_Cape.png` | 64x64 | 망토 |  |
| `Gear/gear_Charm.png` | 64x64 | 장식 |  |
| `Gear/gear_Helmet.png` | 64x64 | 투구 |  |
| `Gear/gear_Hide.png` | 64x64 | 가죽 |  |
| `Gear/gear_Shield.png` | 64x64 | 방패 |  |

### 난이도

| 파일 | 현재 | 이름 | 의미(코드 원문) |
|---|---|---|---|
| `Difficulty/debuff_awakening.png` | 48x48 | 각성 | 난이도 제약 |
| `Difficulty/debuff_ferocity.png` | 48x48 | 광포 | 난이도 제약 |
| `Difficulty/debuff_frenzy.png` | 48x48 | 폭주 | 난이도 제약 |
| `Difficulty/debuff_horde.png` | 48x48 | 물량 | 난이도 제약 |
| `Difficulty/difficulty_easy.png` | 48x48 | 쉬움 | 기본 난이도. 추가 제약이 없다. |
| `Difficulty/difficulty_hard.png` | 48x48 | 어려움 | 적이 더 강해지고 수도 늘어난다. |
| `Difficulty/difficulty_hell.png` | 48x48 | 지옥 | 우두머리가 스킬을 훨씬 자주 쓴다. |
| `Difficulty/difficulty_inferno.png` | 48x48 | 불지옥 | 우두머리가 새로운 공격 패턴을 쓴다. |
| `Difficulty/difficulty_normal.png` | 48x48 | 보통 | 적이 더 단단하고 아프다. |

### (확장) 장비 개별 아이콘 — 지금은 부위 아이콘 8장을 나눠 쓴다

| 새 파일 | 이름 | 등급 | 몸 | 부위 | 설명 |
|---|---|---|---|---|---|
| `Gear/Items/gear_tunic_farmer.png` | 낡은 작업복 | Normal | Humanoid | Armor | 밭에서 벗겨 온 옷. 없는 것보단 낫다. |
| `Gear/Items/gear_tunic_thief.png` | 도둑의 옷 | Uncommon | Humanoid | Armor | 가볍다. 무겁게 입은 쪽을 먼저 찌른다. |
| `Gear/Items/gear_armor_iron.png` | 무쇠 갑옷 | Rare | Humanoid | Armor | 용사에게서 벗겨 낸 것. 크기가 맞을 리 없지만 두껍다. |
| `Gear/Items/gear_armor_dark.png` | 흑기사의 판금 | Unique | Humanoid | Armor | 안에 있던 자가 누구였는지는 아무도 모른다. |
| `Gear/Items/gear_armor_demigod.png` | 반신의 흉갑 | Epic | Humanoid | Armor | 신이 되다 만 자의 갑옷. 걸친 것만으로 숨이 달라진다. |
| `Gear/Items/gear_hood_bandit.png` | 산적 두건 | Normal | Humanoid | Helmet | 얼굴을 가리면 조금 더 대담해진다. |
| `Gear/Items/gear_helm_militia.png` | 민병 투구 | Uncommon | Humanoid | Helmet | 찌그러진 자리마다 누군가의 마지막이 있다. |
| `Gear/Items/gear_helm_viking.png` | 뿔투구 | Rare | Humanoid | Helmet | 뿔은 장식이 아니다. 먼저 닿는 쪽이 뿔이다. |
| `Gear/Items/gear_helm_executioner.png` | 처형인의 복면 | Unique | Humanoid | Helmet | 쓴 자의 표정이 보이지 않아, 맞는 쪽이 먼저 무너진다. |
| `Gear/Items/gear_helm_king.png` | 왕관 | Epic | Humanoid | Helmet | 성을 잃은 왕의 것. 이제 마왕성에 있다. |
| `Gear/Items/gear_shield_wood.png` | 나무 소방패 | Normal | Humanoid | Shield | 한 번은 막는다. 두 번은 장담 못 한다. |
| `Gear/Items/gear_shield_iron.png` | 무쇠 소방패 | Uncommon | Humanoid | Shield | 가볍고 단단하다. 앞줄에 세울 것. |
| `Gear/Items/gear_shield_tower.png` | 탑 방패 | Rare | Humanoid | Shield | 들고 있으면 그 자리가 성벽이 된다. |
| `Gear/Items/gear_shield_crusader.png` | 성전사의 방패 | Unique | Humanoid | Shield | 성스러운 문양이 새겨져 있다. 든 것은 몬스터지만. |
| `Gear/Items/gear_shield_royal.png` | 왕실 대방패 | Epic | Humanoid | Shield | 왕을 지키던 것. 지금은 왕을 무너뜨리러 간다. |
| `Gear/Items/gear_hide_mud.png` | 진흙 범벅 | Normal | NonHumanoid | Hide | 굳은 진흙이 한 겹. 보기엔 지저분해도 한 대는 덜 아프다. |
| `Gear/Items/gear_hide_moss.png` | 이끼 껍질 | Normal | NonHumanoid | Hide | 물기를 머금은 이끼가 한 겹 앉았다. 생각보다 질기다. |
| `Gear/Items/gear_hide_ash.png` | 잿빛 가죽 | Uncommon | NonHumanoid | Hide | 불에 그을려 질겨졌다. |
| `Gear/Items/gear_hide_tanned.png` | 무두질한 가죽 | Uncommon | NonHumanoid | Hide | 손이 많이 간 가죽. 칼끝이 한 번은 미끄러진다. |
| `Gear/Items/gear_hide_steel.png` | 강철 비늘 | Rare | NonHumanoid | Hide | 쇳가루를 먹여 기른 가죽. 몸이 차갑게 빛난다. |
| `Gear/Items/gear_hide_ember.png` | 잉걸 가죽 | Unique | NonHumanoid | Hide | 속에서 불씨가 식지 않는다. |
| `Gear/Items/gear_hide_void.png` | 심연의 껍질 | Epic | NonHumanoid | Hide | 빛이 닿으면 그 자리만 조금 어두워진다. |
| `Gear/Items/gear_bulk_feed.png` | 잘 먹인 몸 | Normal | NonHumanoid | Bulk | 한 끼를 더 줬다. 그만큼 커졌고, 그만큼 멀리 닿는다. |
| `Gear/Items/gear_bulk_swollen.png` | 불린 몸집 | Uncommon | NonHumanoid | Bulk | 물을 잔뜩 먹여 부풀렸다. 오래가지는 않는다. |
| `Gear/Items/gear_bulk_gorge.png` | 포식의 흔적 | Rare | NonHumanoid | Bulk | 무엇을 먹었는지는 묻지 않는 편이 좋다. |
| `Gear/Items/gear_bulk_beast.png` | 괴수의 골격 | Unique | NonHumanoid | Bulk | 뼈가 굵어졌다. 걸음마다 땅이 조금씩 눌린다. |
| `Gear/Items/gear_bulk_titan.png` | 거인의 뼈대 | Epic | NonHumanoid | Bulk | 골격이 통째로 바뀌었다. 라인 하나를 혼자 메운다. |
| `Gear/Items/gear_charm_wisp.png` | 도깨비불 | Rare | NonHumanoid | Charm | 따라다니는 불빛. 주인을 대신해 앞을 본다. |
| `Gear/Items/gear_charm_soul.png` | 혼불 | Unique | NonHumanoid | Charm | 삼킨 것들의 마지막이 아직 타고 있다. |
| `Gear/Items/gear_charm_crown.png` | 짐승의 관 | Epic | NonHumanoid | Charm | 무리에서 하나에게만 주어진다. |
| `Gear/Items/gear_cape_tattered.png` | 해진 망토 | Normal | Humanoid | Cape | 바람에 너덜너덜해졌다. 가벼워서 발이 빨라진다. |
| `Gear/Items/gear_back_satchel.png` | 잡동사니 보따리 | Normal | Humanoid | Back | 무엇이 들었는지 모르지만 휘두르면 제법 묵직하다. |
| `Gear/Items/gear_helm_archer.png` | 궁수 두건 | Uncommon | Humanoid | Helmet | 눈가에 그늘이 지면 겨냥이 차분해진다. |
| `Gear/Items/gear_armor_miner.png` | 광부의 갑주 | Uncommon | Humanoid | Armor | 낙석을 버티려고 만든 옷. 칼끝쯤이야. |
| `Gear/Items/gear_back_quiver.png` | 가죽 화살통 | Uncommon | Humanoid | Back | 화살촉이 전부 쇠를 뚫는 모양으로 벼려져 있다. |
| `Gear/Items/gear_helm_horns.png` | 황소뿔 투구 | Rare | Humanoid | Helmet | 쓰는 순간 어깨가 펴진다. 다치기 전까지는. |
| `Gear/Items/gear_shield_steel.png` | 강철 방패 | Rare | Humanoid | Shield | 무겁다. 든 자리에서 한 걸음도 물러나지 않는다. |
| `Gear/Items/gear_armor_plague.png` | 역병 의사의 옷 | Unique | Humanoid | Armor | 고치러 왔던 자의 옷. 이제는 옮기는 쪽이 입는다. |
| `Gear/Items/gear_helm_cleric.png` | 사제의 두건 | Unique | Humanoid | Helmet | 쓰러질 때 기도가 새어 나온다. 곁에 있던 자가 그걸 듣는다. |
| `Gear/Items/gear_back_sword.png` | 등에 멘 대검 | Unique | Humanoid | Back | 뽑을 일이 없다. 멘 것만으로도 피가 끓는다. |
| `Gear/Items/gear_back_expedition.png` | 원정대 배낭 | Unique | Humanoid | Back | 용사 원정대가 두고 간 짐. 식량이 넉넉하다. |
| `Gear/Items/gear_helm_warlord.png` | 군주의 투구 | Epic | Humanoid | Helmet | 쓴 자가 쓰러지면 부하들이 그 이름을 외친다. |
| `Gear/Items/gear_shield_ancient.png` | 고대의 대방패 | Epic | Humanoid | Shield | 성이 무너져도 이 방패는 남았다. 그래서 여기 있다. |
| `Gear/Items/gear_hide_frost.png` | 서리 낀 가죽 | Normal | NonHumanoid | Hide | 만지면 손끝이 얼얼하다. 때린 쪽도 마찬가지다. |
| `Gear/Items/gear_bulk_lean.png` | 다부진 근육 | Uncommon | NonHumanoid | Bulk | 군살을 걷어 냈다. 크지는 않아도 한 방이 무겁다. |
| `Gear/Items/gear_hide_scar.png` | 흉터투성이 가죽 | Uncommon | NonHumanoid | Hide | 살아남은 싸움만큼 흉이 졌다. 어디를 물어야 하는지 안다. |
| `Gear/Items/gear_hide_starscale.png` | 별빛 비늘 | Uncommon | NonHumanoid | Hide | 밤마다 희미하게 빛난다. 기운이 빨리 돈다. |
| `Gear/Items/gear_charm_fang.png` | 송곳니 목걸이 | Rare | NonHumanoid | Charm | 사냥한 것의 이빨을 꿰었다. 무리가 그 냄새를 따른다. |
| `Gear/Items/gear_bulk_brute.png` | 억센 몸통 | Rare | NonHumanoid | Bulk | 통나무 같은 몸통. 부딪친 쪽이 튕겨 나간다. |
| `Gear/Items/gear_hide_blood.png` | 핏빛 가죽 | Unique | NonHumanoid | Hide | 베인 자리마다 남의 피가 스며들어 굳었다. |
| `Gear/Items/gear_charm_howl.png` | 울음 뼈피리 | Unique | NonHumanoid | Charm | 동족이 쓰러질 때마다 저 혼자 운다. |
| `Gear/Items/gear_charm_warbrand.png` | 전쟁의 낙인 | Epic | NonHumanoid | Charm | 달군 쇠로 찍은 표식. 찍힌 것은 싸우는 법만 기억한다. |
