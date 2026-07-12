# Starfall Story Spreadsheet Schema

`Starfall > Story > Import Excel`에서 `.xlsx`, `.csv`, `.tsv`를 가져옵니다. 첫 번째 비어 있지 않은 행을 헤더로 읽고, 나머지 한 행을 대사 한 줄로 처리합니다. 같은 `episode_id`를 연속해서 쓰지 않아도 자동으로 묶입니다.

## 필수/기본 열

| 열 | 설명 | 예시 |
|---|---|---|
| `episode_id` | 에피소드 고유 ID | `main_01` |
| `episode_title` | 기록실 표시 제목 | `프롤로그` |
| `category` | `Main`, `Event`, `Character`, `Side` | `Main` |
| `sort_order` | 같은 분류 안의 순서 | `0` |
| `summary` | 기록실 줄거리 | `별이 내린 날의 이야기` |
| `focus_character` | CharacterData ID/이름/경로 | `aria` |
| `thumbnail`, `banner` | Sprite 에셋 경로/GUID/이름 | `Assets/.../thumb.png` |
| `initially_unlocked` | 기본 해금 여부 | `true` |
| `unlock_key` | 별도 해금 키 | `chapter_1_clear` |
| `prerequisite_episode` | 선행 에피소드 ID | `main_00` |
| `line_id` | 에피소드 안의 라인 고유 ID | `line_001` |
| `speaker` | 화자 CharacterData ID/이름/경로 | `aria` |
| `speaker_name` | 표시 화자명 덮어쓰기 | `아리아` |
| `speaker_position` | `Narrator`, `Left`, `Center`, `Right` | `Center` |
| `text` | 실제 대사. 셀 안 줄바꿈 지원 | `오늘은 별이 밝네.` |

## 캐릭터/표정

`left_`, `center_`, `right_` 접두사 뒤에 아래 열을 붙입니다.

- `character`: CharacterData ID/이름/경로
- `expression_key`: 런타임 표정 키 (`default`, `smile`, `angry` 등). 직접 Sprite를 비우면
  `Resources/Story/Expressions/{characterId}/{expression_key}` 또는
  `Resources/Story/Expressions/{expression_key}` 순서로 찾아 사용합니다.
- `expression_sprite`: 이 라인에서 바로 사용할 표정 Sprite
- `visible`, `flip`: 표시/좌우 반전 (`true`, `false`, `1`, `0`)
- `tint`: HTML 색상 (`#FFFFFF`, `#FFFFFF80`)
- `offset_x`, `offset_y`: 화면 위치 보정

`Assets/StarfallAcademy/Resources/Story/` 아래에 넣은 PNG/JPG는 자동으로
`Sprite (2D and UI)`로 임포트됩니다. 기존 이미지가 Sprite 선택창에 보이지 않으면
`Starfall > Story > Reimport Story Art`를 실행합니다.

## 배경/연출/오디오

- `background`, `cg`: Sprite 에셋 경로/GUID/이름
- `bgm`, `sfx`: AudioClip 에셋 경로/GUID/이름
- `transition`: `None`, `Cut`, `CrossFade`, `FadeToBlack`, `FadeToWhite`, `SlideLeft`, `SlideRight`
- `effects`: `Shake|FlashWhite|FlashBlack|FadeIn|FadeOut|Vignette`처럼 여러 항목 연결
- `shake_strength`, `effect_duration`, `text_speed`, `auto_duration`: 초/강도 실수값

## 선택지

최대 네 개까지 `choice1_` ~ `choice4_` 접두사로 지정합니다.

- `text`: 선택지 문구
- `next_episode`: 이동할 에피소드 ID
- `next_line`: 이동할 라인 ID
- `condition`: 선택지 표시 조건 키

에디터 창의 **템플릿 헤더 복사** 또는 **CSV 템플릿 저장** 버튼으로 전체 열을 바로 만들 수 있습니다. Merge는 같은 `episode_id`/`line_id`를 갱신하고 다른 라인은 보존합니다. Replace는 데이터베이스 등록 목록을 가져온 에피소드로 교체하지만 기존 `.asset` 파일을 지우지 않습니다.
