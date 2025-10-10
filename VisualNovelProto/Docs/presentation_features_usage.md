# 연출 기능 사용 가이드

이 문서는 새로 추가된 연출 스크립트를 어떻게 설정하고 CSV 시나리오에서 호출하는지 정리합니다.

## 1. 사전 준비
- `DialogueUI` 인스턴스에 감정 스타일, 상호작용, QTE 프롬프트 레퍼런스를 연결합니다. `emotionStyles`, `interactionLibrary`, `qtePrompt` 필드를 모두 인스펙터에서 채워야 각 기능이 활성화됩니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L38-L115】
- 감정별 스타일은 `Create > VN > Emotion Style Library`로 에셋을 생성한 뒤, 감정 키마다 글꼴·색상·패널 이미지를 지정합니다.【F:Assets/1.Scripts/Dialogue/EmotionStyleLibrary.cs†L6-L68】
- 캐릭터 합성 포즈는 `Create > VN > Character Interaction Library` 에셋을 만든 뒤, 포즈 키와 두 캐릭터의 위치/회전/스케일 키프레임을 정의합니다.【F:Assets/1.Scripts/Dialogue/CharacterInteractionLibrary.cs†L5-L70】
- 카메라 경로를 재사용하려면 `Create > VN > Cinematic Camera Path` 에셋을 만들고 경로 노드를 등록하세요. `relative`를 체크하면 현재 위치 대비 이동, 해제하면 절대 좌표로 이동합니다.【F:Assets/1.Scripts/GameSystems/CinematicCameraPath.cs†L4-L16】

## 2. 감정별 텍스트 스타일 적용
1. 화자 이름 또는 본문 텍스트의 맨 앞에 `[[emotion=키]]` 또는 `<emotion=키>` 태그를 넣습니다. 태그는 표시되지 않고 해당 감정 키가 추출됩니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L210-L248】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L388-L394】
2. 키가 `EmotionStyleLibrary`에 존재하면 글꼴, 텍스트 색, 패널 이미지/색상이 자동으로 덮어씌워집니다. 존재하지 않으면 기본 스타일로 되돌립니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L176-L208】

## 3. 배우(초상) 연출 명령어
CSV `actors` 컬럼은 `스프라이트키@위치(옵션)` 형식의 세미콜론 구분 목록입니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L967-L1074】

### 3.1 위치 지정
- `@L`, `@C`, `@R`는 기본 앵커에 배치합니다. `@x,y` 형태를 쓰면 좌표로 직접 배치하고 해당 슬롯은 `X`로 취급됩니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L993-L1006】
- `z=번호` 옵션으로 동일 슬롯 내 정렬 순서를 제어합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1032-L1038】

### 3.2 입·퇴장 및 교체 옵션
- `in=`: 입장 연출(`fade`, `pop`, `slide`). 명시하지 않으면 페이드.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1035-L1038】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L651-L773】
- `t=` 또는 `time=`: 입장 시간. 미지정 시 `actorDefaultInTime`을 사용합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1032-L1039】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L651-L715】
- `out=`: 퇴장 연출(`fade`, `slide`, `pop/shrink`). 지정이 없으면 입장과 동일한 연출을 재사용합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L596-L599】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L717-L773】
- `outT=`/`outTime=`: 퇴장 시간. 없으면 입장과 동일 시간으로 계산합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L596-L600】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L799-L804】
- `swap=wait` 또는 `wait`: 기존 캐릭터가 완전히 사라질 때까지 기다렸다가 새 캐릭터를 넣습니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L582-L609】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1042-L1048】
- `swap=cross`: 기존 초상을 임시 복제해 크로스 페이드 후 교체합니다. 자동으로 퇴장 대기까지 설정됩니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L582-L599】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L775-L825】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1042-L1072】
- 캐릭터를 명시하지 않은 슬롯은 자동으로 퇴장 명령이 생성되어 이전 입장 정보와 동일한 연출/시간으로 빠져나옵니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L947-L963】

### 3.3 강조 효과
- `pulse=지속시간`, `pulseAmp=강조비`, `pulseFreq=진동수` 옵션으로 라이트 펄스를 추가할 수 있습니다. 지속시간 동안 밝기가 사인 곡선으로 출렁이며 기본 색을 유지합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L624-L626】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1049-L1051】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L827-L848】

### 3.4 캐릭터 상호작용 포즈
- `pose=포즈키`를 지정하면 `CharacterInteractionLibrary`에서 해당 키를 찾아 두 캐릭터의 위치/스케일/회전을 애니메이션합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L627-L632】【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L850-L913】
- 기본적으로 같은 슬롯과 합성하지만 `pose=키:대상슬롯` 또는 별도 `with=` 옵션으로 대상 슬롯(`L`,`C`,`R`)을 지정할 수 있습니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1052-L1067】
- 포즈 재생 시 라이브러리의 이징 커브와 듀레이션이 그대로 적용됩니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L871-L899】【F:Assets/1.Scripts/Dialogue/CharacterInteractionLibrary.cs†L17-L70】

## 4. 배경 전환 및 카메라/포커스 연출
- CSV `transition` 컬럼에 세미콜론으로 구분된 명령을 작성하면 노드 진입 시 `TransitionManager.Play`가 호출되어 화면 연출이 실행됩니다.【F:Assets/1.Scripts/Dialogue/DialogueRunner.cs†L64-L82】【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L242-L306】
- 지원 명령과 주요 파라미터:
  - `fade_in`, `fade_out`, `blackout`, `clearout`: 화면 페이드. `t=`(또는 `time=`), `delay=` 지원.【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L268-L309】
  - `shake(t=0.3,amp=18)`: 화면 흔들림.【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L268-L301】【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L329-L349】
  - `mask(name=asset, soft=0.05, invert=1, color=#000000)`: 마스크 와이프. `maskResourcesFolder`에서 텍스처를 찾습니다.【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L268-L305】【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L350-L399】
  - `camera_path(name=키, t=2.0, delay=0.2, zoom=1.2, mode=absolute)` 또는 `camera_path(points=x1,y1,z1|x2,y2,z2, ...)`: 지정한 경로를 따라 `cameraTarget`를 이동/줌합니다. 에셋이나 인라인 포인트를 모두 지원합니다.【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L268-L305】【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L411-L485】
  - `focus(t=0.5, zoom=1.15, color=#00000080)`: 화면에 어둡게 비네팅을 깔고 지정한 배율로 확대합니다. `focusTarget`이 없으면 카메라 타깃을 사용합니다.【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L487-L509】
  - `perspective(mode=reset, zoom=1.3, color=#55000080)`: `focus`와 동일하지만 `mode=reset`으로 호출하면 확대와 틴트를 해제하는 복귀 연출을 만들 수 있습니다.【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L512-L537】

## 5. QTE(Quick Time Event) 프롬프트
1. `DialogueUI`의 `qtePrompt` 필드에 `QtePrompt` 컴포넌트를 연결합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L81-L115】【F:Assets/1.Scripts/UI/QtePrompt.cs†L7-L72】
2. CSV `advancePolicy` 컬럼에 `qte(...)`를 입력하면 해당 노드에서 자동으로 QTE가 시작됩니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L250-L306】
   - `timeout=`: 제한 시간(초). 기본 5초.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L260-L277】
   - `default=`: 시간 초과 시 선택할 버튼 인덱스.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L261-L306】
   - `pulsePeriod=` / `pulseStrength=`: QTE 타이머의 펄스 애니메이션 주기와 강도.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L262-L279】【F:Assets/1.Scripts/UI/QtePrompt.cs†L17-L66】
3. 시간 내에 입력이 없으면 기본 인덱스를 선택하고, 선택지가 없으면 아무 동작 없이 종료합니다.【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L287-L307】

위 규칙을 조합하면 감정 스타일, 배우 교체, 합동 포즈, 라이트 펄스, 카메라 연출, 포커스 전환, QTE까지 시나리오 CSV 한 줄로 트리거할 수 있습니다.
