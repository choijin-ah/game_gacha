# Starfall Academy

> 어반 판타지 세계관을 배경으로 한 Unity 기반 수집형 턴제 RPG 프로토타입

![Starfall Academy 로비](Assets/StarfallAcademy/Resources/Lobby/Art/lobby_urban_fantasy_v1.png)

`Starfall Academy`는 캐릭터 수집, 파티 편성, 스테이지 공략, 성장과 스토리 감상을 하나의 플레이 흐름으로 구성한 수집형 RPG 프로젝트입니다. 현재 Unity Editor에서 로비부터 실제 전투와 보상 정산까지 이어지는 핵심 루프를 플레이할 수 있습니다.

## 핵심 플레이 흐름

```text
로비 → 캐릭터 수집·성장 → 파티 편성 → 스테이지 선택
     → 턴제 전투 → 보상·계정 성장 → 다음 스테이지
```

## 주요 기능

### 턴제 전투

- 속도 기반 행동 순서와 다음 행동 순서 표시
- 일반 공격, 전투 스킬, 파티 공용 SP, 에너지와 궁극기 예약
- 약점·격파, 버프·디버프·지속 피해·행동 지연
- 최대 5명의 적과 보스 2페이즈 전투
- 균형, 공격, 격파, 생존, 궁극기 보존의 AUTO 전략 5종
- 1배속·2배속, 일시정지, 전투 포기
- 턴 수와 전투 불능 여부를 반영한 별 3개 평가

### 캐릭터와 성장

- 6종의 플레이어 캐릭터 데이터
- 캐릭터 레벨 및 스킬 강화
- 무기, 방어구, 장신구, 보조 장비의 4부위 장비 시스템
- 추천 장착과 장비 강화, 전투력 반영
- 계정 경험치와 계정 레벨 기반 콘텐츠 해금

### 콘텐츠

- 메인, 성장, 장비, 보스 카테고리로 구성된 10개 스테이지
- 행동력 소비, 최초 클리어 보상, 별 기록, 소탕
- 캐릭터 도감과 파티 편성
- 메인·이벤트·캐릭터·사이드 스토리 아카이브
- 비주얼 노벨 재생과 에디터, CSV/TSV/XLSX 가져오기

### 경제와 메타 시스템

- 프리미엄 재화, 크레딧, 스킬 코어
- 뽑기와 상점 교환
- 6분당 1씩 회복되는 행동력
- 로그인, 행동력 사용, 강화, 전투 일일 미션
- 중복 보상 방지 및 저장 실패 시 재화·진행도 롤백

## 실행 환경

- Unity `6000.5.3f1`
- C#
- Unity Hub 권장
- Windows 환경에서 개발 및 검증

## 시작하기

1. 저장소를 복제합니다.

   ```powershell
   git clone https://github.com/choijin-ah/game_gacha.git
   ```

2. Unity Hub에서 저장소 폴더를 프로젝트로 추가합니다.
3. Unity `6000.5.3f1`로 프로젝트를 엽니다.
4. `Assets/StarfallAcademy/Scenes/Lobby.unity`를 엽니다.
5. Play 버튼을 눌러 실행합니다.

Editor에서 다른 씬을 연 상태로 Play하더라도 시작 씬은 로비로 전환되며, Play를 종료하면 기존 편집 씬으로 돌아옵니다.

## 주요 씬

| 씬 | 역할 |
| --- | --- |
| `Lobby.unity` | 계정 정보, 재화, 일일 미션과 전체 콘텐츠 진입점 |
| `Formation.unity` | 보유 캐릭터를 이용한 파티 편성 |
| `CharacterArchive.unity` | 캐릭터 도감, 성장, 스킬과 장비 관리 |
| `Gacha.unity` | 캐릭터 모집과 결과 확인 |
| `Shop.unity` | 재화 지급 및 행동력·크레딧·스킬 코어 교환 |
| `StoryArchive.unity` | 스토리 목록과 비주얼 노벨 재생 |
| `StageSelect.unity` | 스테이지 선택, 해금 조건, 보상과 소탕 |
| `TurnBattle.unity` | 수동·자동 턴제 전투와 결과 정산 |

## 콘텐츠 해금

| 계정 레벨 | 해금 기능 |
| ---: | --- |
| 6 | 성장 던전 |
| 8 | 소탕 |
| 10 | 장비와 장비 던전 |

개발 및 디버깅 편의를 위해 기존 AUTO와 2배속 기능은 계정 레벨과 관계없이 유지됩니다.

## Unity Editor 메뉴

Unity 상단의 `Starfall` 메뉴는 작업 성격별로 분류되어 있습니다.

- `Starfall > Data > Character Database`: 캐릭터 데이터 관리
- `Starfall > Data > Stage Database`: 스테이지 데이터 관리
- `Starfall > Data > Gacha Configuration`: 모집 확률과 비용 관리
- `Starfall > Story > Visual Novel Editor`: 에피소드와 대사 편집
- `Starfall > Story > Import Excel`: 스토리 시트 가져오기
- `Starfall > Art`: 캐릭터와 스토리 이미지 다시 가져오기
- `Starfall > Rebuild`: 로비, 전투, 스토리 등 대응하는 씬 재생성
- `Starfall > Play Mode > Use Lobby As Start Scene`: Play Mode 시작 씬을 로비로 설정

## 검증

Unity Editor 메뉴에서 핵심 로직 진단을 실행할 수 있습니다.

- `Starfall > Diagnostics > Battle Core Smoke Test`
- `Starfall > Diagnostics > Meta Core Diagnostics`

솔루션 빌드는 프로젝트 루트에서 다음 명령으로 확인합니다.

```powershell
dotnet build game.slnx --no-restore -p:UseSharedCompilation=false
```

## 프로젝트 구조

```text
Assets/StarfallAcademy/
├─ Scenes/                  게임 화면 씬
├─ Data/Characters/         캐릭터 ScriptableObject 데이터
├─ Data/Stages/             스테이지 ScriptableObject 데이터
├─ Resources/Data/          런타임 데이터베이스
├─ Scripts/Battle/          스테이지, 전투 모델과 AUTO 전략
├─ Scripts/CharacterArchive/캐릭터 성장과 도감
├─ Scripts/Formation/       파티 편성
├─ Scripts/Gacha/           모집 시스템
├─ Scripts/Meta/            계정, 행동력, 미션, 장비와 보상
├─ Scripts/Shop/            상점과 재화 교환
├─ Scripts/Story/           스토리 데이터와 비주얼 노벨 런타임
└─ Editor/                  데이터 편집, 씬 생성과 진단 도구
```

## 현재 상태

게임 시스템 기획서와 Unity 1차 개발 설계안을 기준으로 제작된 플레이 가능한 1차 프로토타입입니다. 기존 로비, 무료 재화, 뽑기, 스토리, 수동 전투, AUTO와 배속 기능을 유지하면서 전투·성장·콘텐츠·경제 시스템을 확장했습니다.
