# Starfall 화면 구조

로비는 `Scenes/Lobby.unity`에서 실행됩니다. Unity에서 이 씬을 열고 Play 하세요.
캐릭터 도감, 기록실, 편성, 모집, 상점, 스테이지 선택, 턴제 전투는 각각
`CharacterArchive.unity`, `StoryArchive.unity`, `Formation.unity`, `Gacha.unity`, `Shop.unity`, `StageSelect.unity`, `TurnBattle.unity` 독립 씬으로 이동합니다.

Unity 에디터에서는 어떤 씬을 편집 중이든 Play를 누르면 항상 `Lobby.unity`에서 시작합니다.
Play를 정지하면 원래 편집하던 씬으로 돌아옵니다.

기본 플레이 흐름은 `로비 > 출격 > 스테이지 선택 > 턴제 전투 > 보상·다음 스테이지 해금`입니다.

## 자주 수정할 파일

- 대사·배너·메뉴 문구: `Scripts/Data/LobbyContent.cs`
- 전체 폰트·기본 색상: `Scripts/UI/LobbyTheme.cs`
- 어반 판타지 공용 패널·테두리: `Scripts/UI/UrbanFantasyStyle.cs`
- UI 아이콘 무채색 셰이더: `Shaders/UrbanFantasyIcon.shader`
- 로비 전체 배치·메뉴 문구·흑백 스타일: `Scripts/Views/GothicLobbyView.cs`
- 왼쪽 아래 대화창 동작: `Scripts/UI/Components/LobbySpeechBubble.cs`
- 캐릭터 편성 화면: `Scripts/Formation/FormationScreen.cs`
- 캐릭터 도감 화면: `Scripts/CharacterArchive/CharacterArchiveScreen.cs`
- 기록실·비주얼노벨 재생: `Scripts/Story/Runtime/`
- 스토리 에피소드·라인 데이터: `Scripts/Story/Data/`, `Resources/Data/StoryDatabase.asset`
- 비주얼노벨 편집기·Excel 가져오기: `Editor/Story/`
- 도감 목록·상세 패널: `Scripts/CharacterArchive/Views/`
- 보유·레벨·스킬 성장 저장: `Scripts/CharacterArchive/CharacterProgressionService.cs`
- 기본 스킬 아이콘 아틀라스: `Resources/CharacterArchive/UI/default_skill_icons_v1.png`
- 가챠 화면: `Scripts/Gacha/GachaScreen.cs`
- 상점 화면: `Scripts/Shop/ShopScreen.cs`
- 가챠 공용 색상·테두리: `Scripts/Gacha/GachaGothicStyle.cs`
- 픽업 목록·모집 버튼·결과창: `Scripts/Gacha/Views/`
- 픽업·확률 데이터: `Resources/Data/GachaConfig.asset`
- 스테이지 선택 화면: `Scripts/Battle/StageSelectScreen.cs`
- 턴제 전투 화면·게임 데이터 연결: `Scripts/Battle/TurnBattleScreen.cs`, `Scripts/Battle/TurnBattleModel.cs`
- 행동 수치·피해·브레이크·상태·AI 코어: `Scripts/Battle/MvpCore/`
- 자동 전투 프리셋·의사결정 규칙: `Scripts/Battle/Auto/`
- 스테이지 데이터베이스: `Resources/Data/StageDatabase.asset`
- 스테이지별 적·보상 데이터: `Data/Stages/`
- 환경 설정 저장·적용: `Scripts/Settings/GameSettings.cs`
- BGM/SFX AudioSource 채널 연동: `Scripts/Settings/GameAudioChannel.cs`
- 로비 설정 창: `Scripts/Settings/LobbySettingsPanel.cs`
- 팝업 동작: `Scripts/UI/Components/LobbyModal.cs`
- 토스트 메시지: `Scripts/UI/Components/LobbyToastOverlay.cs`
- 전체 화면 조립: `Scripts/Core/LobbyScreen.cs`
- 버튼·아이콘 아틀라스 분할: `Scripts/UI/LobbyUiAssets.cs`

## 폴더 역할

```text
StarfallAcademy/
├─ Scenes/                 Lobby·CharacterArchive·StoryArchive·Formation·Gacha·Shop·Battle 독립 씬
├─ Resources/Lobby/Art/    런타임에서 읽는 로비 원화
├─ Resources/Lobby/UI/     AI 생성 버튼·심플 아이콘 PNG 아틀라스
├─ Resources/Data/         캐릭터·가챠·스테이지 런타임 데이터베이스
├─ Data/Characters/        캐릭터별 ScriptableObject 에셋
├─ Data/Stages/            스테이지별 적·권장 전투력·보상 에셋
├─ Scripts/
│  ├─ Battle/              스테이지 선택·턴제 전투·클리어 저장
│  │  ├─ Auto/             자동 전투 5종 전략·행동/필살기 의사결정
│  │  └─ MvpCore/          행동 수치·SP·에너지·브레이크·상태·AI·필살기 큐
│  ├─ Core/                씬 진입점과 화면 조립
│  ├─ Data/                대사·배너·메뉴 데이터
│  ├─ Formation/           편성 씬 화면과 저장 상태
│  ├─ CharacterArchive/    캐릭터 도감 목록과 상세 정보
│  ├─ Gacha/               픽업 선택·뽑기·결과 화면
│  ├─ Meta/                계정·행동력·임무·보상·기본 장비
│  ├─ Settings/            음량·그래픽·텍스트·자동 전투 설정
│  ├─ Shop/                무료 테스트 상품과 상점 화면
│  ├─ Story/
│  │  ├─ Data/             에피소드·라인·캐릭터 표정·연출 데이터
│  │  └─ Runtime/          4분할 기록실·비주얼노벨 플레이어·완독 저장
│  ├─ UI/                  테마와 UI 생성 공통 코드
│  │  └─ Components/       말풍선·팝업·토스트·버튼 효과
│  └─ Views/               로비 화면 영역별 레이아웃
└─ Editor/                 씬 재생성·데이터 편집·원화 임포트 설정
```

현재 로비와 편성 씬의 어반 판타지 배경 원화는
`Resources/Lobby/Art/lobby_urban_fantasy_v1.png`입니다.

## UI 스타일 수정

런타임 UI는 먹색 반투명 패널, 은색 1px 테두리, 적색 알림점 규칙을 공유합니다.
전체 색상은 `UrbanFantasyStyle.cs`, 폰트 우선순위는 `LobbyTheme.cs`에서 바꾸고,
각 화면의 실제 배치는 `GothicLobbyView.cs`, `FormationScreen.cs`, `GachaScreen.cs`에서 조정합니다.

## 캐릭터 추가 방법

1. Unity 상단 메뉴에서 `Starfall > Data > Character Database`를 엽니다.
2. `＋ 새 캐릭터`를 누릅니다.
3. 오른쪽 상세 영역에서 이름, 소속, 역할, 레어도, 레벨, 전투력을 입력합니다.
4. `Portrait`에는 Texture Type이 `Sprite (2D and UI)`인 초상화 이미지를 넣습니다.
5. 저장 후 로비의 `편성` 버튼을 누르면 `Formation.unity`로 이동하며 자동으로 목록에 표시됩니다.

캐릭터 상세의 `Skill` 영역에서는 스킬 이름, 커스텀 아이콘, 기본 아이콘 스타일,
스킬 최대 레벨과 강화 비용을 캐릭터별로 설정할 수 있습니다. 커스텀 아이콘을 비우면
캐릭터 ID에 따라 기본 아이콘 5종 중 하나가 자동으로 배정됩니다.
`Battle MVP Actions`에서는 일반 공격·스킬·필살기 수치를, `Battle Audio`에서는 각 행동의
효과음과 복수 음성 클립을 설정합니다. 음성 클립이 여러 개면 전투 중 무작위로 재생됩니다.

가챠에서 처음 획득한 캐릭터만 보유 상태가 되며, 도감은 보유 캐릭터를 위에,
미보유 캐릭터를 잠금 상태로 아래에 표시합니다. 레벨업은 크레딧, 스킬 강화는 스킬 코어를 소비합니다.

로비와 모집 화면의 `별의 결정`은 같은 `PremiumCurrency` 지갑을 사용합니다.
모집을 실행하면 로비에 표시되는 별의 결정도 같은 값으로 차감됩니다.
로비 상단의 별의 결정을 누르면 모집 화면으로 이동합니다.

초상화가 없어도 이름 첫 글자로 임시 카드가 표시됩니다. 캐릭터 ID는 생성 툴이 자동으로 발급하므로 직접 바꿀 필요가 없습니다.

## 씬 다시 만들기

씬 파일을 실수로 삭제했거나 초기화하려면 Unity 메뉴에서
`Starfall > Rebuild > Lobby Scene` 또는 `Formation Scene`을 실행하세요.

도감 씬은 `Starfall > Rebuild > Character Archive Scene`으로 다시 만들 수 있습니다.

가챠 픽업과 확률은 `Starfall > Data > Gacha Configuration`에서 수정하고,
가챠 씬은 `Starfall > Rebuild > Gacha Scene`으로 다시 만들 수 있습니다.

상점 씬은 `Starfall > Rebuild > Shop Scene`으로 다시 만들 수 있습니다.
기존 개발용 별의 결정 1,600개 무료 보급은 횟수 제한 없이 유지됩니다.
추가로 별의 결정으로 행동력·크레딧을, 크레딧으로 스킬 코어를 교환할 수 있습니다.

스테이지 데이터는 `Starfall > Data > Stage Database` 편집 창에서 열 수 있습니다.
왼쪽 목록에서 스테이지를 고른 뒤 오른쪽 `Audio > Battle Bgm`과 분류, 행동력 비용, 적 이름·수·레벨·체력·공격력,
권장 전투력, 계정 경험치와 보상을 수정할 수 있습니다. 기본 데이터는 캐릭터 6종과
메인·성장·장비 던전을 포함한 스테이지 10개입니다.
스테이지 선택/턴제 전투 씬은 각각 `Starfall > Rebuild > Stage Select Scene`,
`Starfall > Rebuild > Turn Battle Scene`으로 재생성합니다.

## 계정 성장·행동력·임무·장비

계정 경험치는 전투와 일일 임무 보상으로 얻습니다. 성장 던전은 계정 LV.6,
소탕은 LV.8, 장비와 장비 던전은 LV.10에 실제 해금됩니다. 주간 보스와 도전 탑의
후속 해금 레벨도 `PlayerProfileService`에서 확인할 수 있습니다. 기존 자동 전투와
배속은 개발 중이던 기능을 보존하기 위해 잠그지 않습니다.

행동력은 계정 레벨에 따라 최대치가 증가하고 로컬 UTC 기준 6분마다 1 회복됩니다.
스테이지 입장과 소탕에서 소비되며 상점의 유료 충전은 최대치를 넘어도 표시된 60을
그대로 지급합니다. 승리하면 최대 별 3개를
기록하고, 별 3개와 계정 LV.8을 달성한 스테이지는 선택 화면에서 1회 소탕할 수 있습니다.

메인 작전과 성장·장비 던전의 진행 트랙은 분리됩니다. `다음 작전`은 파밍 던전을
건너뛰고 다음 메인 작전으로 이동하며, 전투 씬을 직접 실행한 경우에는 기존 디버그 진입을
유지하되 연습 전투로 처리하여 행동력·보상·별·해금·임무 진행을 지급하지 않습니다.

로비의 `업적` 카드에서 로그인·행동력 사용·강화·전투 완료 일일 임무를 확인하고
개별 또는 일괄 수령할 수 있습니다. 캐릭터 도감에는 무기·방어구·장신구·보조 장치
4슬롯의 기본 장비, 추천 장착과 슬롯 강화 기능이 추가되어 계정 LV.10부터 전투력에
즉시 반영됩니다.

메타 경계값은 Unity 메뉴 `Starfall > Diagnostics > Meta Core Diagnostics`에서 실제 저장값을
건드리지 않고 검사할 수 있습니다.

## 기록실·스토리 제작

로비의 `기록실` 또는 메인 스토리 배너를 누르면 `StoryArchive.unity`로 이동합니다.
기록실은 왼쪽의 큰 `MAIN STORY` 영역과 오른쪽의 `EVENT STORY`, `CHARACTER STORY`,
`SIDE STORY` 세로 3분할 목록으로 구성됩니다. 에피소드에는 대표 캐릭터, 썸네일/배너,
줄거리, 해금 조건과 완독 진행률을 지정할 수 있습니다.

Unity 메뉴 `Starfall > Story > Visual Novel Editor`에서 다음을 편집합니다.

- 에피소드 추가·삭제·분류·정렬·해금 조건
- 라인 추가·삽입·복제·삭제·위/아래 이동
- 화자와 좌/중/우 캐릭터, 표정 Sprite 또는 표정 키, 반전·색상·위치 보정
- 배경·CG·BGM·SFX·대사 음성
- 컷/크로스페이드/암전/화이트/슬라이드 전환과 흔들림·플래시·비네트 효과
- 글자 속도·자동 진행 시간과 최대 4개 선택지

선택한 라인은 에디터 오른쪽에서 캐릭터·표정·배경·대사까지 즉시 미리볼 수 있습니다.
게임 재생 중에는 이전/다음, AUTO, SKIP, 대화 LOG를 사용할 수 있고 진행 위치와 완독 여부가 저장됩니다.

Excel로 작성한 스토리는 `Starfall > Story > Import Excel`에서 `.xlsx`, `.csv`, `.tsv`로 가져옵니다.
한 행이 대사 한 줄이며 `episode_id`와 `line_id`로 Merge하거나 전체 등록 목록을 Replace할 수 있습니다.
가져오기 창의 `Excel 양식 다운로드`로 서식과 예시가 포함된 `.xlsx`를 만들 수 있으며, `CSV 양식 저장`과 `템플릿 헤더 복사`도 지원합니다.
스토리 오디오는 행별 `bgm`, `sfx`, `voice` 열에서 AudioClip 경로, GUID 또는 에셋 이름으로 지정합니다.
열 이름과 에셋 경로 규칙은 `Editor/Story/STORY_IMPORT_SCHEMA.md`에 정리되어 있습니다.
캐릭터/표정/배경 셀에는 `Assets/...` 경로, GUID 또는 에셋 이름을 넣을 수 있습니다.
표정 Sprite를 줄마다 직접 지정하지 않고 키로 관리하려면 이미지를
`Resources/Story/Expressions/{캐릭터 ID}/{표정 키}.png`에 넣고 `expression_key`만 입력하세요.
`Resources/Story/` 아래의 PNG/JPG는 자동으로 `Sprite (2D and UI)`로 가져옵니다.
이미 먼저 넣어 둔 이미지가 Sprite 선택창에 보이지 않으면
`Starfall > Art > Reimport Story Art`를 한 번 실행하세요.

기록실 씬을 다시 만들려면 `Starfall > Rebuild > Story Archive Scene`을 실행하세요.

## 턴제 전투 설정

전투는 속도에 따른 연속 행동 수치(`10000 / 속도`)를 사용하며 상단에 다음 행동 8칸을 표시합니다.
파티는 공용 SP를 3개로 시작해 최대 5개까지 보유합니다. 일반 공격은 SP와 에너지를 회복하고,
전투 스킬은 SP를 소비합니다. 에너지가 찬 캐릭터의 `ULT` 버튼은 현재 턴과 관계없이 예약할 수 있으며,
예약된 필살기는 현재 판정 종료 후 입력 순서대로 실행됩니다.

전투 화면 조작은 다음과 같습니다.

- 캐릭터 행동: `일반 공격` 또는 `전투 스킬` 선택 후 빛나는 유효 대상 클릭
- 필살기: 아군 카드의 `ULT READY` 클릭 후 대상 선택
- `AUTO`: 자동 행동 전환
- `전략`: 균형 → 공격 우선 → 격파 우선 → 생존 우선 → 필살기 보존 순환
- `×1 / ×2`: 전투 연출 속도 전환
- `ESC` 또는 `Ⅱ`: 일시정지, 재개, 작전 퇴각

자동 전투는 수동 조작과 같은 행동 생성·필살기 대기열을 사용합니다. 선택한 전략에 따라
SP 부족과 유효 대상을 확인하고, 회복·보호 효과 중복을 피하면서 약점 격파, 보스,
적 3체 이상, 아군 위험도를 판단합니다. 선택 이유는 매 행동의 전투 로그에 표시되며
마지막으로 고른 전략은 `PlayerPrefs`에 저장되어 다음 전투에서도 유지됩니다.

캐릭터 에셋의 `Battle MVP Stats / Actions`에서 속성, 체력·공격·방어·속도,
일반 공격·스킬·필살기의 대상·배율·SP·에너지·브레이크 수치를 캐릭터마다 바꿀 수 있습니다.
값을 비우거나 0으로 두면 기존 역할과 전투력에 맞춘 안전한 기본값을 사용합니다.

스테이지 에셋의 `Battle MVP Enemies`에서는 최대 5명의 개별 적, 약점 1~3개,
방어력·브레이크 게이지·행동 지연/상태 저항·위험 공격 표시를 설정합니다.
보스 스테이지는 2페이즈 진입 체력, 공격 배율, 속도 증가량도 수정할 수 있습니다.
개별 적 목록을 비우면 기존 단일 적 설정을 최대 5명까지 반복해 사용합니다.

코어 규칙을 빠르게 확인하려면 Unity 메뉴 `Starfall > Diagnostics > Battle Core Smoke Test`를 실행하세요.
SP, 행동 순서, 실드, 약점 브레이크, 상태 지속, 필살기 FIFO와 보스 패턴을 검사하고
정상이면 Console에 `Core smoke test passed`를 출력합니다.

로비 우측 상단 톱니바퀴에서는 마스터/BGM/SFX 음량, 그래픽 품질,
대사 텍스트 속도, 전투 진입 시 자동 전투 기본값을 바꿀 수 있습니다.
