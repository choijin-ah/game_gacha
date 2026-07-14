# Starfall Academy UI 구현 가이드

## 1. 구현 기준

- 런타임 UI는 프로젝트의 기존 구조와 동일하게 코드 기반 `uGUI`를 사용한다.
- 프로젝트의 전투·씬 구성이 가로 화면을 전제로 하므로 기준 해상도는 `1920 x 1080`이다.
- `CanvasScaler.ScaleWithScreenSize`와 `SafeAreaFitter`를 함께 사용해 화면 비율과 노치 영역에 대응한다.
- 화면은 상단 상태, 핵심 정보, 콘텐츠, 하단 액션, 오버레이의 5개 계층을 유지한다.
- 콘텐츠 데이터 편집은 `EditorWindow.OnGUI` 기반 도구에서 수행하고 런타임 화면과 분리한다.

## 2. 비주얼 토큰

| 역할 | 용도 |
| --- | --- |
| Backdrop / Panel | 딥 네이비 기반 배경과 반투명 정보 패널 |
| Violet | 메인 액션, 마도·오컬트 강조 |
| Cyan | 선택, 포커스, 정보 상태 |
| Gold | 5성, 프리미엄, 핵심 보상 |
| Success | 수령·저장·클리어 성공 |
| Warning | 비용 부족, 만료 임박, 편성 경고 |
| Danger | 실패, 삭제, 초기화 |

실제 색상 값은 `UrbanFantasyStyle`을 단일 원본으로 사용한다. 가챠와 로비의 스타일 클래스도 이 토큰을 참조하므로 화면별 임의 색상 추가를 피한다.

## 3. 공통 컴포넌트

- 버튼: Standard, Primary, Secondary, Warning, Danger, Tab, Icon
- 카드: 공통 패널, 희귀도 색상, 선택·잠금·NEW 상태
- 피드백: 의미별 Toast, 확인/취소 Modal, 봉인형 Badge
- 진행 표시: Progress Bar, 상태 Pill, 비동기 씬 로딩 오버레이
- 입력 보조: 포인터 Hover와 모바일 Long Press를 함께 지원하는 Tooltip
- 접근성: 비활성 상태의 명도 차이, 텍스트 그림자, 기능성 색상과 문구의 동시 사용

## 4. 화면 구현 현황

| 화면 | 핵심 구현 |
| --- | --- |
| Lobby | 계정·경험치·4종 재화, 동적 우편/출석 배지, 미션, 배너, 콘텐츠 진입 |
| Stage Select | 카테고리, 잠금, 별, 보상, 편성, 전투, 소탕 |
| Formation | 최대 5개 프리셋, 역할 필터, 4종 정렬, 평균 레벨·속성·회복 역할 요약 |
| Character Archive | 보유/미보유·속성 필터, 정렬, 레벨·스킬·장비·각성 |
| Gacha | 다중 배너, 종료 시각, 천장, 확률 상세, 최근 200건 기록과 필터, 1/10회 결과 |
| Shop | 재화·행동력·성장 재료 교환과 결과 피드백 |
| Story Archive | 카테고리, 잠금·클리어, 에피소드, 비주얼 노벨 재생 |
| Turn Battle | 턴 순서, 약점·격파, 5종 AUTO 전략, 상태 툴팁, 승패·보상 |
| Weekly Boss | 주간 일정, 난이도, 최고 점수, 보상 구간, 도전 횟수 |
| Challenge Tower | 층 목록, 특수 규칙, 별 조건, 최초·누적 보상 |
| Attendance / Mail | 7일 기본 캠페인, 수령 상태, 우편 200개·7개 단위 페이지, 전체 수령 |

## 5. 사용자 흐름

```text
Lobby
├─ Story ─ Story Archive ─ Visual Novel
├─ Formation ─ Preset Save/Load
├─ Character ─ Growth / Skill / Equipment / Awakening
├─ Gacha ─ Rate Detail / History / Result
├─ Shop
└─ Stage Select
   ├─ Weekly Boss
   ├─ Challenge Tower
   └─ Turn Battle ─ Result ─ Retry / Formation / Stage Select
```

모든 씬 이동은 중복 입력을 막는 비동기 로딩 오버레이를 통과한다. `Escape`는 가장 위의 오버레이부터 닫고, 오버레이가 없을 때만 이전 씬으로 이동한다.

## 6. 상태·피드백 규칙

- 저장이나 보상 지급은 성공이 확정된 뒤에만 성공 Toast를 표시한다.
- 재화·행동력·보상·장비·가챠 기록은 같은 트랜잭션 안에서 처리하고 실패 시 모두 복원한다.
- 메인 액션은 조건 미충족 시 비활성화하며, 원인은 인접 문구나 Warning Toast로 설명한다.
- `NEW`, 미수령, 우편 수량은 동적 Badge로 표시하고 0이면 숨긴다.
- 파괴적 동작과 전체 초기화는 Danger 스타일의 확인 Modal을 사용한다.
- 긴 목록은 필터·정렬 상태를 보존하고 비어 있을 때 명시적인 Empty State를 보여준다.

## 7. 제작·검증 도구

- `Starfall > Content Dashboard`: 데이터, LiveOps, UI, 씬 재생성, 진단의 통합 진입점
- `Starfall > UI > Style Guide`: 팔레트, 컴포넌트 규칙, 화면 코드, 이미지 연결 상태
- `Starfall > Validate > All Content`: 캐릭터, 장비, 가챠, LiveOps, 주간 보스, 탑, 스테이지, 스토리 검증
- `Starfall > Debug > Player Data Viewer`: 저장 데이터 11개 탭, 내보내기·가져오기·백업·초기화

## 8. 주요 소스 위치

- 공통 스타일·팩토리: `Assets/StarfallAcademy/Scripts/UI`
- 공통 오버레이: `Assets/StarfallAcademy/Scripts/UI/Components`
- 런타임 화면: `Assets/StarfallAcademy/Scripts`
- 커스텀 에디터: `Assets/StarfallAcademy/Editor`
- LiveOps 데이터: `Assets/StarfallAcademy/Data/LiveOps`
- 런타임 데이터베이스: `Assets/StarfallAcademy/Resources/Data`

