# Project K 004 — Summoner's Keep (가칭)

`Project_K_001`의 **진영 반전 파생작**. 원작 코드베이스를 그대로 이어받아 시작한다.

> 런 시작 시 1회 부여되고 **다시는 채워지지 않는 마나**로, 마왕성에 몰려오는 용사
> 웨이브를 막아내는 실시간 소환 디펜스 로그라이트.

- **엔진**: Unity 6 (6000.0.71f1) · URP 17.0.4 · Entities 1.4.5
- **정본 기획서**: [Docs/GameDesign.md](Docs/GameDesign.md) (v2)
- **AI 컨텍스트**: [CLAUDE.md](CLAUDE.md) / [AGENTS.md](AGENTS.md)

## 현재 상태 (2026-08-26)

원작에서 코드·에셋 전량을 복사한 직후다. **아직 원작과 동일하게 동작하며,
v2 신규 시스템은 하나도 구현되지 않았다.**

구현 대기 중인 신규 시스템:

| 시스템 | 위치 | 기획서 |
|---|---|---|
| 소환 마나 (회복 없음) | `Assets/_project/1.Script/InGame/Summon/` | 5장 |
| 소환 덱 · 소환 실행 | `Assets/_project/1.Script/InGame/Summon/` | 4장 |
| 몬스터 도감 (= 덱 편성 화면) | `Assets/_project/1.Script/InGame/MonsterCodex/` | 8.3절 |
| 마왕성 코어 HP (패배 조건) | `Assets/_project/1.Script/InGame/Battle/` | 2.3절 |
| RunSetup 씬 (로비 대체) | `Assets/_project/1.Script/RunSetup/` | 1.2절 |

## 원작 대비 핵심 차이

1. **진영 반전** — 원작 아군 체계(General/Soldier: 직업·등급·액티브33·패시브40·장비)는
   **적(Hero)** 이 가져가고, 원작 적 체계(Enemy/Elite/Boss: 무장비·무등급)는
   **플레이어의 소환수(MonsterSummon)** 가 가져간다.
2. **마나 무회복** — 런당 1회 부여가 전부. 회복 경로를 만들지 않는다.
3. **패배 조건** — 아군 전멸이 아니라 마왕성 코어 HP 소진.
4. **로비 없음** — Splash → RunSetup → InGame. 세로 1080×1920 전용.

## 원작 참조 문서

- [Docs/GameDesign_ProjectK001.md](Docs/GameDesign_ProjectK001.md) — 원작 기획서 (대조용)
- [Docs/README_ProjectK001.md](Docs/README_ProjectK001.md) — 원작 README
- [Docs/DESIGN_GrowthSystems_ProjectK001.md](Docs/DESIGN_GrowthSystems_ProjectK001.md)
- `Docs/*_Icon_Spec.md` — 아이콘 생성 스펙 (그대로 유효)

원작 프로젝트: `D:\project\Project_K_001` (읽기 전용 참고)
