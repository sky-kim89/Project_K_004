# 아이콘 재생성 명세 (유물 제외)

작성일: 2026-08-25  
대상: `Assets/_project/3.Textures/Icons/` 중 `Relics/`를 제외한 241개 PNG

## 적용 원칙

- 기존 **경로, 파일명, PNG 해상도, `.meta` 파일**을 유지한다. 파일 바이트만 교체하므로
  ScriptableObject의 Sprite 참조와 Sprite Atlas 연결은 유지된다.
- 유물(`Icons/Relics/`, 29개)은 개편 중이므로 생성·수정·재임포트 대상에서 제외한다.
- 모든 새 아이콘은 투명 배경 PNG, 텍스트 없음, 워터마크 없음, 작은 크기에서도 식별되는 하나의
  주 실루엣을 사용한다.
- 48px 아이콘은 중앙 주제 28~34px, 외곽 여백 5~7px을 유지한다. 64px 직업/로비 아이콘은
  중앙 주제 40~46px을 사용한다. 192px 아이템은 48px 축소 시에도 읽히도록 큰 실루엣을 우선한다.
- 현재 아이콘의 유리질 테두리·그라데이션을 그대로 복제하지 않는다. 대신 **어두운 바탕 + 선명한
  주제 + 직업/효과별 색상 + 단순 프레임**이라는 정보 구조만 유지한다.

## 공통 시각 언어

| 요소 | 재생성 방향 | 의도 |
|---|---|---|
| 배경 | 낮은 대비의 어두운 원형/사각 방사 배경 | HUD·팝업의 어두운 카드 위에서 실루엣을 분리 |
| 주제 | 한 아이콘에 하나의 읽히는 행위/도구/상태 | 48px에서도 이름을 읽지 않고 기능 파악 |
| 색 | 피해=주황·적색, 회복=녹색, 제어=청록·빙청, 마법=보라, 소환=뼈색·보라, 강화=금색 | 전투 판단과 분류 속도 |
| 테두리 | 등급·희귀·적 전용을 제외하고 과도한 장식 금지 | 아이콘 내부 정보량을 우선 |
| 보조표식 | 의미가 필요할 때만 작고 명확한 `+`, 화살표, 시계, 번개, 별을 사용 | 같은 주제의 변형을 구분 |

## 묶음별 제작 명세

| 묶음 | 수량 / 해상도 | 제작 방향 | 데이터·의도 근거 |
|---|---:|---|---|
| Classes | 4 / 64px | 직업 고유 도구를 큰 실루엣으로: 검·방패, 활, 지팡이, 방패. 기사=적색, 궁수=녹색, 마법사=보라, 방패병=청록은 고정한다. | `IconGenerator.cs`의 직업별 팔레트 |
| Skills | 32 / 48px | 행위가 핵심이다. 타격은 무기와 충돌점, 광역은 범위/낙하, 회복은 생명/빛, 소환은 호출 대상, 버프는 군기·함성·방패로 표현한다. 희귀 스킬은 이중 프레임 또는 금색 포인트, 보스 패턴은 혈색으로 적 전용임을 분리한다. | `ActiveSkillData.cs` enum 주석, `IconGenerator.cs` 스킬 테이블 |
| Passives | 40 / 48px | 스탯/트리거를 구분한다. 병사·장군·교환·처치·피격·사망·스킬사용·전투시작을 서로 다른 주제와 보조표식으로 표현한다. | `PassiveIconGenerator.cs`의 glyph/background/frame/badge 표 |
| Abilities | 46 / 48px | 일반(A)은 스탯 중심의 깨끗한 상징, 고급(B)은 같은 축의 강화판, 특수(C)는 발동 메커니즘을 직접 묘사, 마스터리(D)는 직업 전용 상징을 쓴다. 대상(전체/직업/근접/원거리/장군/병사)을 실루엣에 반영한다. | `AbilityCreator.cs`의 이름·대상·스탯, `IconGenerator.cs`의 A~D 도안 |
| Equipments | 36 / 48px | 장비 종류와 등급을 동시에 읽는다. 갑옷·검·단검·지휘 장비의 기본 축을 유지하고, 특수 효과 장비는 서로 다른 물체(고서·인장·낙인·나팔·북·맹세 등)로 분리한다. 등급은 테두리/광원으로만 보조한다. | `EquipmentIconGenerator.cs`의 36종 목록과 ‘트리거 장비도 자기 도안을 갖는다’ 규칙 |
| Items | 13 / 192px | 보상 카드 전면에 크게 쓰이므로 재화·상자·책·돌을 사실적인 소품 하나로 단순화한다. 실루엣과 재질로 금화·보석·에너지·강화석을 구분한다. | `ItemIconGenerator.cs`, `ItemMeta.cs` |
| Traits | 52 / 48px | 직업 특성, 공통 특성, 전환, 치명, 공속, 이벤트, 시너지의 차이를 주제와 색으로 분리한다. 시너지 특성은 두 역할/두 도구의 결합으로 표현한다. | `TraitCreator.cs`의 이름·설명·효과 및 `IconGenerator.cs`의 도안 |
| Difficulty | 9 / 48px | 난이도 5종은 위협 수준이 올라가는 단일 문장, 디버프 4종은 해당 제약(군세·흉포·각성·광란)을 부정적 표식으로 표현한다. | `DifficultyIconGenerator.cs`, `DifficultyEnums.cs` |
| StageNodes | 4 / 48px | 일반=검, 정예=보석/문장, 상점=좌판+금화, 이벤트=물음표가 아닌 사건의 두루마리/표지판처럼 목적이 읽히는 상징을 사용한다. | `IconGenerator.cs`의 stage draw 함수와 `StageProgressBarUI.cs` |
| LobbyBtns | 5 / 64px | 분해=망치+검, 어빌리티=책+스탯, 유물=보석, 도감=수집 칸이 보이는 책, 상점=차양 좌판+금화. 버튼 내부에서 선명히 읽히는 큰 실루엣을 유지한다. | `IconGenerator.cs`의 각 버튼 설명 |

## 액티브 스킬 개별 주제

| 파일 키 | 이름 | 새 아이콘의 주제 |
|---|---|---|
| heavy_strike | 강타 | 전방으로 내리꽂는 대검과 충돌 파편 |
| volley_fire | 일제 사격 | 같은 방향으로 쏘는 화살 세 발 |
| leap_strike | 도약 강타 | 공중 도약 궤적과 착지 충격파 |
| heal_aura / target_heal | 치유 오라 / 집중 치유 | 아군 둘레의 회복 고리 / 한 대상에 모이는 치유광 |
| charge_soldier / suicide_soldier | 돌격 병사 / 자폭 병사 | 방패 든 돌격병 / 폭발 직전의 돌격병 |
| summon_skeleton / summon_elite | 스켈레톤 소환 / 정예 소환 | 무덤에서 나온 해골 손 / 깃발 든 정예 분대 |
| poison_zone / blizzard | 독성 지대 / 블리자드 | 독 웅덩이와 기포 / 회오리 눈보라 |
| meteor / gravity_collapse | 메테오 / 중력 붕괴 | 낙하하는 불덩이 / 안쪽으로 휘는 파편과 붕괴점 |
| sacrifice_soldier / blood_price | 병사 희생 / 피의 대가 | 흡수되는 병사 생명력 / 불타는 심장과 전방 폭발 |
| bind / death_sentence | 속박 / 사형 선고 | 묶인 사슬 / 낙인이 찍힌 적 실루엣 |
| berserker / battle_cry / war_banner | 광전사 / 전투 함성 / 군기 강림 | 불꽃 난투, 외치는 투구, 빛나는 군기 |
| iron_shield / bulwark | 철벽 방어 / 불멸의 방벽 | 철 방패 / 아군을 감싼 거대 방벽 |
| arrow_rain / arrow_storm | 화살 비 / 화살 폭풍 | 수직 낙하 화살 / 소용돌이치는 연속 화살 |
| shockwave / bisect | 충격파 / 일도양단 | 부채꼴 압력파 / 화면을 가르는 한 줄 참격 |
| swift_strike / piercing_dash | 신속 연격 / 관통 돌진 | 잔상이 남는 연속 타격 / 적을 통과하는 직선 돌진 |
| chain_lightning | 연쇄 번개 | 세 대상 사이를 연결하는 번개 |
| gravestone | 비석 강림 | 낙하하는 비석과 소환 균열 |
| boss_charge / boss_slam | 돌진 / 분쇄 강타 | 붉은 돌진 실루엣 / 지면을 깨는 거대 주먹 |

`BossEnrage`는 의도적으로 `skill_berserker`를 재사용한다. 별도 파일을 만들지 않는다.

## 하단 우측 ‘Gemini 마크’ 확인 결과

샘플(스킬·어빌리티·장비·특성)을 실제 확인하고 생성 코드를 대조했다. 하단 우측에 보이는 작은
기호는 Gemini 워터마크로 확인되지 않았다. 프로젝트의 `IconArt.cs`가 직접 그리는 **의미 뱃지**다.

- 피해=번개, 회복=`+`, 소환=별, 강화=상승 화살표, 약화=하락 화살표, 제어=시계.
- `IconGenerator.cs`는 이 뱃지를 “같은 주제의 변형을 구분하는 정보”로 명시한다.

따라서 기존 아이콘에서 그 기호를 일괄 삭제하면 기능 구분이 손상된다. 재생성 시에는 Gemini처럼
보이는 네 꼭짓점 장식은 사용하지 않고, 위의 단순 보조표식 또는 주제 자체의 차이로 의미를 유지한다.
실제 외부 생성 이미지에 별도 워터마크가 있는지는 생성본을 받는 각 묶음에서 원본 대조로 다시 판정한다.

## 적용·검수 순서

1. Classes + Skills 32장을 첫 검수 묶음으로 생성한다.
2. Passives + Abilities를 두 번째 묶음으로 생성한다.
3. Equipments + Items, Traits, Difficulty/Stage/Lobby 순서로 진행한다.
4. 각 묶음은 임시 파일로 검수한 뒤 확정본만 원래 파일에 반영한다. 원본 PNG와 `.meta`는 백업한다.
5. Unity에서 해당 아이콘 폴더를 재임포트하고 `Tools > Project K > 데이터 생성 > SpriteManager + SpriteAtlas`를 실행해 아틀라스 패킹을 확인한다.

## 참조 코드

- `Assets/_project/1.Script/InGame/Battle/Editor/IconGenerator.cs`
- `Assets/_project/1.Script/InGame/Battle/Editor/IconArt.cs`
- `Assets/_project/1.Script/InGame/Battle/Editor/PassiveIconGenerator.cs`
- `Assets/_project/1.Script/InGame/Battle/Editor/EquipmentIconGenerator.cs`
- `Assets/_project/1.Script/InGame/Battle/Editor/ItemIconGenerator.cs`
- `Assets/_project/1.Script/InGame/Battle/Editor/AbilityCreator.cs`
- `Assets/_project/1.Script/InGame/Trait/Editor/TraitCreator.cs`
- `Assets/_project/1.Script/InGame/Skill/ActiveSkillData.cs`
