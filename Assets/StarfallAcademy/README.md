# Starfall 화면 구조

로비는 `Scenes/Lobby.unity`에서 실행됩니다. Unity에서 이 씬을 열고 Play 하세요.
캐릭터 도감, 편성, 모집, 상점, 스테이지 선택, 턴제 전투는 각각
`CharacterArchive.unity`, `Formation.unity`, `Gacha.unity`, `Shop.unity`, `StageSelect.unity`, `TurnBattle.unity` 독립 씬으로 이동합니다.

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
- 도감 목록·상세 패널: `Scripts/CharacterArchive/Views/`
- 보유·레벨·스킬 성장 저장: `Scripts/CharacterArchive/CharacterProgressionService.cs`
- 기본 스킬 아이콘 아틀라스: `Resources/CharacterArchive/UI/default_skill_icons_v1.png`
- 가챠 화면: `Scripts/Gacha/GachaScreen.cs`
- 상점 화면: `Scripts/Shop/ShopScreen.cs`
- 가챠 공용 색상·테두리: `Scripts/Gacha/GachaGothicStyle.cs`
- 픽업 목록·모집 버튼·결과창: `Scripts/Gacha/Views/`
- 픽업·확률 데이터: `Resources/Data/GachaConfig.asset`
- 스테이지 선택 화면: `Scripts/Battle/StageSelectScreen.cs`
- 턴제 전투 화면·규칙: `Scripts/Battle/TurnBattleScreen.cs`, `Scripts/Battle/TurnBattleModel.cs`
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
├─ Scenes/                 Lobby·Archive·Formation·Gacha·Shop·StageSelect·TurnBattle 독립 씬
├─ Resources/Lobby/Art/    런타임에서 읽는 로비 원화
├─ Resources/Lobby/UI/     AI 생성 버튼·심플 아이콘 PNG 아틀라스
├─ Resources/Data/         캐릭터·가챠·스테이지 런타임 데이터베이스
├─ Data/Characters/        캐릭터별 ScriptableObject 에셋
├─ Data/Stages/            스테이지별 적·권장 전투력·보상 에셋
├─ Scripts/
│  ├─ Battle/              스테이지 선택·턴제 전투·클리어 저장
│  ├─ Core/                씬 진입점과 화면 조립
│  ├─ Data/                대사·배너·메뉴 데이터
│  ├─ Formation/           편성 씬 화면과 저장 상태
│  ├─ CharacterArchive/    캐릭터 도감 목록과 상세 정보
│  ├─ Gacha/               픽업 선택·뽑기·결과 화면
│  ├─ Settings/            음량·그래픽·텍스트·자동 전투 설정
│  ├─ Shop/                무료 테스트 상품과 상점 화면
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

1. Unity 상단 메뉴에서 `Starfall > Character Database`를 엽니다.
2. `＋ 새 캐릭터`를 누릅니다.
3. 오른쪽 상세 영역에서 이름, 소속, 역할, 레어도, 레벨, 전투력을 입력합니다.
4. `Portrait`에는 Texture Type이 `Sprite (2D and UI)`인 초상화 이미지를 넣습니다.
5. 저장 후 로비의 `편성` 버튼을 누르면 `Formation.unity`로 이동하며 자동으로 목록에 표시됩니다.

캐릭터 상세의 `Skill` 영역에서는 스킬 이름, 커스텀 아이콘, 기본 아이콘 스타일,
스킬 최대 레벨과 강화 비용을 캐릭터별로 설정할 수 있습니다. 커스텀 아이콘을 비우면
캐릭터 ID에 따라 기본 아이콘 5종 중 하나가 자동으로 배정됩니다.

가챠에서 처음 획득한 캐릭터만 보유 상태가 되며, 도감은 보유 캐릭터를 위에,
미보유 캐릭터를 잠금 상태로 아래에 표시합니다. 레벨업은 크레딧, 스킬 강화는 스킬 코어를 소비합니다.

로비와 모집 화면의 `별의 결정`은 같은 `PremiumCurrency` 지갑을 사용합니다.
모집을 실행하면 로비에 표시되는 별의 결정도 같은 값으로 차감됩니다.
로비 상단의 별의 결정을 누르면 모집 화면으로 이동합니다.

초상화가 없어도 이름 첫 글자로 임시 카드가 표시됩니다. 캐릭터 ID는 생성 툴이 자동으로 발급하므로 직접 바꿀 필요가 없습니다.

## 씬 다시 만들기

씬 파일을 실수로 삭제했거나 초기화하려면 Unity 메뉴에서
`Starfall > Rebuild Lobby Scene` 또는 `Rebuild Formation Scene`을 실행하세요.

도감 씬은 `Starfall > Rebuild Character Archive Scene`으로 다시 만들 수 있습니다.

가챠 픽업과 확률은 `Starfall > Gacha Configuration`에서 수정하고,
가챠 씬은 `Starfall > Rebuild Gacha Scene`으로 다시 만들 수 있습니다.

상점 씬은 `Starfall > Rebuild Shop Scene`으로 다시 만들 수 있습니다.
현재는 횟수 제한 없이 별의 결정 1,600개를 무료로 받는 개발용 상품 하나만 제공합니다.

스테이지 데이터는 `Starfall > Stage Database`에서 열 수 있습니다.
목록의 각 `StageData` 에셋을 선택하면 적 이름·수·레벨·체력·공격력·권장 전투력·보상을 수정할 수 있습니다.
스테이지 선택/턴제 전투 씬은 각각 `Rebuild Stage Select Scene`, `Rebuild Turn Battle Scene`으로 재생성합니다.

로비 우측 상단 톱니바퀴에서는 마스터/BGM/SFX 음량, 그래픽 품질,
대사 텍스트 속도, 전투 진입 시 자동 전투 기본값을 바꿀 수 있습니다.
