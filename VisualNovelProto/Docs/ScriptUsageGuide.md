# 스크립트 제작 목적 및 사용 가이드

> 이 문서는 내일 회의용으로 VisualNovelProto 프로젝트 내 주요 C# 스크립트의 제작 의도, 사용 시점, 핵심 기능을 한눈에 정리한 것입니다. 디렉터리 구조에 맞추어 정리했으며, 각 항목은 현행 코드 기준으로 서술했습니다.

## Assets/NaniPro/Scripts - 경량 비주얼 노벨 엔진 구성요소

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| Scripting/ScriptPro.cs | 대본 텍스트를 파싱해 실행 가능한 명령 목록으로 바꾸기 | `ScriptPro.Parse`로 텍스트를 읽은 뒤 `ScriptPlayerPro`에 전달 | 라벨, 선택지, 배경/캐릭터 연출, 변수 제어 등의 커맨드를 정의하고 실행 결과를 돌려줍니다. 【F:Assets/NaniPro/Scripts/Scripting/ScriptPro.cs†L9-L233】|
| Core/EnginePro.cs | 나니엔진 하위 매니저를 한 GameObject에 묶어 초기화 | 빈 GameObject에 부착하면 필요한 매니저 컴포넌트를 자동 추가 | 배경/캐릭터/텍스트/선택지/스크립트/저장소를 서로 연결해 실행 준비를 마칩니다. 【F:Assets/NaniPro/Scripts/Core/EnginePro.cs†L5-L33】|
| Core/RuntimeInitializerPro.cs | Resources에 있는 나니 대본을 불러와 자동 실행 | 씬 진입 시 `language`, `scriptName`, `startLabel`을 설정하고 부착 | 엔진을 보장적으로 생성하고 스크립트 자산을 로드해 첫 라벨부터 재생합니다. 【F:Assets/NaniPro/Scripts/Core/RuntimeInitializerPro.cs†L8-L27】|
| Managers/ScriptPlayerPro.cs | 파싱된 명령을 순차 실행하는 런타임 | `Play` 코루틴을 호출하거나 Save/Load API를 사용 | 조건문, 변수 평가, 선택지 처리, 저장 슬롯 스냅샷을 담당하는 메인 실행기입니다. 【F:Assets/NaniPro/Scripts/Managers/ScriptPlayerPro.cs†L11-L124】|
| Managers/TextPrinterManagerPro.cs | 타이핑 효과가 있는 텍스트 출력기 | `PrintLine(author, text, resolver)` 코루틴을 호출 | 변수를 치환해 백로그를 남기고, 자동/스킵 모드, 클릭 대기 등을 처리합니다. 【F:Assets/NaniPro/Scripts/Managers/TextPrinterManagerPro.cs†L10-L66】|
| Managers/CharacterManagerPro.cs | 화면 위 캐릭터(스탠딩 CG) 관리 | `Show/Hide/Move` 코루틴으로 위치 및 연출 제어 | 좌/중/우 앵커와 페이드/이동 연출, 세이브용 스냅샷 생성을 지원합니다. 【F:Assets/NaniPro/Scripts/Managers/CharacterManagerPro.cs†L19-L98】|
| Managers/ChoiceHandlerManagerPro.cs | 선택지 버튼 UI 풀링 및 입력 처리 | `ShowChoices` 코루틴으로 선택지를 넘기면 인덱스를 기록 | 선택지 버튼을 동적으로 생성하고 클릭될 때까지 기다립니다. 【F:Assets/NaniPro/Scripts/Managers/ChoiceHandlerManagerPro.cs†L9-L39】|
| Managers/BackgroundManagerPro.cs | 배경 이미지와 크로스페이드 처리 | `SetBackground(path, fade)` 코루틴으로 호출 | Resources에서 스프라이트를 찾아 씬에 없으면 UI를 생성하고 페이드 전환을 수행합니다. 【F:Assets/NaniPro/Scripts/Managers/BackgroundManagerPro.cs†L9-L53】|
| Managers/VariableStore.cs | 간단한 키-값 변수 저장소 | `Set/Get/Snapshot/Restore` 메서드 사용 | 문자열, 숫자, 불린을 자동 변환하며 세이브 시 딕셔너리를 복제해 보관합니다. 【F:Assets/NaniPro/Scripts/Managers/VariableStore.cs†L7-L24】|
| Managers/SaveManagerPro.cs | PlayerPrefs 기반 경량 세이브 슬롯 | `Save(slot, state)`와 `Load(slot)` 호출 | 배경·캐릭터 스냅샷과 변수 딕셔너리를 Json으로 감싸 저장합니다. 【F:Assets/NaniPro/Scripts/Managers/SaveManagerPro.cs†L8-L33】|
| Util/UIBuilder.cs | 런타임에 기본 UI 구성을 자동 생성 | `UIBuilder.EnsureUI(engine)` 호출 | EventSystem과 Canvas, 대사창/선택지/캐릭터 앵커를 생성해 초기 셋업을 단순화합니다. 【F:Assets/NaniPro/Scripts/Util/UIBuilder.cs†L8-L82】|

## Assets/1.Scripts/GameSystems - 공통 시스템/서비스

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| GameSystems/GameRoot.cs | 전역 싱글턴 허브 및 초기화 루틴 | 타이틀 씬의 DDOL 오브젝트에 부착 | 세팅·오디오·세이브·데이터 매니저를 보장하고, 글로서리/캐릭터 DB 로드 및 전역 플래그를 묶습니다. 【F:Assets/1.Scripts/GameSystems/GameRoot.cs†L1-L61】|
| GameSystems/DataManager.cs | Resources 기반 데이터 캐시 | `DataManager.Instance`로 접근하여 필요시 `LoadIfNeeded()` 호출 | 글로서리·캐릭터·공개 범위 CSV를 한번만 로드해 공유합니다. 【F:Assets/1.Scripts/GameSystems/DataManager.cs†L4-L22】|
| GameSystems/DataBootstrap.cs | 씬 진입 시 UI에 데이터바인딩 | 씬의 Canvas 허브에서 DialogueUI/CollectionsPanel 참조 연결 | GameRoot 또는 Resources에서 데이터베이스를 가져와 UI에 주입합니다. 【F:Assets/1.Scripts/GameSystems/DataBootstrap.cs†L4-L29】|
| GameSystems/StoryGameManager.cs | 스토리 씬용 초기화 지휘 | 인게임 스토리 씬 루트에서 실행 | CSV 경로를 지정해 러너/UI에 바인딩하고 파우즈 메뉴/사전 뷰어를 연결합니다. 【F:Assets/1.Scripts/GameSystems/StoryGameManager.cs†L5-L36】|
| GameSystems/SceneRefHub.cs | 씬 안 주요 컴포넌트 참조 묶음 | 씬 루트에 두고 다른 시스템에서 찾아 사용 | DialogueUI, Runner, PauseMenu 등을 Inspector로 모아 제공합니다. 【F:Assets/1.Scripts/GameSystems/SceneRefHub.cs†L1-L8】|
| GameSystems/InputRouter.cs | 입력(Action Map)에서 스토리 진행으로 라우팅 | PlayerInput을 가진 오브젝트에 부착 | Advance/Back 입력을 캡쳐해 DialogueUI 진행, 모달 닫기, 일시정지를 토글합니다. 【F:Assets/1.Scripts/GameSystems/InputRouter.cs†L1-L61】|
| GameSystems/SettingsManager.cs | 그래픽/사운드/타이핑 설정 저장 및 적용 | GameRoot에 포함되어 `Load`, `ApplyAll` 호출 | JSON으로 디스크에 저장하고 AudioManager, ResolutionManager, TypingConfig에 반영합니다. 【F:Assets/1.Scripts/GameSystems/SettingsManager.cs†L5-L109】|
| GameSystems/ResolutionManager.cs | 해상도 프리셋과 화면 모드 적용 | DDOL로 두고 `Apply(preset, mode)` 호출 | 16:9 프리셋을 기준으로 가장 가까운 실제 해상도를 찾아 적용하며 이벤트를 발행합니다. 【F:Assets/1.Scripts/GameSystems/ResolutionManager.cs†L1-L45】|
| GameSystems/Letterboxer.cs | 카메라에 레터박스 적용 | 카메라에 부착 | 목표 종횡비와 현재 화면 비율을 비교해 카메라 Viewport Rect를 조정합니다. 【F:Assets/1.Scripts/GameSystems/Letterboxer.cs†L1-L28】|
| GameSystems/TransitionManager.cs | 화면 페이드/마스크/쉐이크 및 배우 연출 | TransitionManager를 씬에 배치하고 `TransitionManager.Play` 등 호출 | 다중 트랜지션을 파싱해 풀 기반으로 업데이트하며 진행 중 상태를 브로드캐스트합니다. 【F:Assets/1.Scripts/GameSystems/TransitionManager.cs†L1-L89】|
| GameSystems/GlobalFlags.cs | 스토리 누적 플래그를 전역으로 저장 | `GlobalFlags.Add/Has/AddRange` 호출 | PlayerPrefs에 영구 저장하며 새 플래그 추가 이벤트를 발행해 업적과 연동합니다. 【F:Assets/1.Scripts/GameSystems/GlobalFlags.cs†L1-L44】|
| GameSystems/SteamIntegrationManager.cs | Steamworks 초기화/업적/클라우드 동기화 | DDOL 오브젝트에 붙여 `InitializeSteam` 호출 또는 자동 초기화 | Steam API 유무에 따라 업적, 클라우드 파일 업/다운로드를 래핑합니다. 【F:Assets/1.Scripts/GameSystems/SteamIntegrationManager.cs†L1-L87】|
| GameSystems/SteamAchievementBinder.cs | 스토리 플래그를 Steam 업적에 매핑 | GameRoot와 함께 실행해 두고 매핑 테이블 입력 | Awake에서 기존 플래그를 동기화하고, GlobalFlags 이벤트를 받아 업적을 해제합니다. 【F:Assets/1.Scripts/GameSystems/SteamAchievementBinder.cs†L1-L47】|

## Assets/1.Scripts/Dialogue - 본편 대사 시스템

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| Dialogue/DialogueDatabase.cs | CSV 기반 스토리 데이터 파서 | `DialogueDatabase.LoadFromCsvText`로 로드 후 `DialogueRunner`에 전달 | 노드, 선택지, 플래그 참조 풀을 구축하고 인덱스를 유지합니다. 【F:Assets/1.Scripts/Dialogue/DialogueDatabase.cs†L1-L91】|
| Dialogue/DialogueTypes.cs | 노드/선택지/플래그 구조체 정의 | 데이터 직렬화나 런너 로직에서 직접 사용 | `DialogueNode`, `Choice`, `FlagSet`을 정의하고 플래그 집합 유틸을 제공합니다. 【F:Assets/1.Scripts/Dialogue/DialogueTypes.cs†L1-L63】|
| Dialogue/DialogueRunner.cs | CSV 노드를 순차 실행하는 메인 루프 | 씬 내 DialogueRunner에 CSV, UI를 연결해 사용 | 플래그 조건을 확인하며 노드 진입, 선택지, 다음 노드 이동을 처리합니다. 【F:Assets/1.Scripts/Dialogue/DialogueRunner.cs†L1-L78】|
| Dialogue/DialogueUI.cs | 화면 텍스트/캐릭터/선택지 표현과 링크 처리 | DialogueRunner에서 `Bind` 호출 후 `ShowNode` 사용 | 타이핑, 링크 하이라이트, 배우 연출, 글로서리 자동 해금, CTC 인디케이터 제어 등을 담당합니다. 【F:Assets/1.Scripts/Dialogue/DialogueUI.cs†L1-L96】|
| Dialogue/TypingConfig.cs | 타자 속도 기본값 및 PlayerPrefs 연동 | UI에서 Typing 속도 변경 시 `TypingConfig.Apply` 호출 | 글자당 속도와 문장부호 지연을 저장하고 전역 설정을 제공합니다. 【F:Assets/1.Scripts/Dialogue/TypingConfig.cs†L1-L42】|
| Dialogue/AutoAdvanceManager.cs | 자동 진행 타이머 관리 | DialogueRunner와 DialogueUI를 연결해 `SetAuto` 사용 | 대사 길이에 비례한 대기 시간을 계산하고 조건이 맞으면 `runner.Step`을 호출합니다. 【F:Assets/1.Scripts/Dialogue/AutoAdvanceManager.cs†L1-L45】|
| Dialogue/StoryFlags.cs | 러너 전용 플래그 조회 위임 | `StoryFlags.Bind(provider)`로 연결 | DialogueRunner가 내부 플래그 콜렉션을 외부에서 조회할 수 있게 합니다. 【F:Assets/1.Scripts/Dialogue/StoryFlags.cs†L1-L10】|
| Dialogue/ChatLogManager.cs | 대사 백로그 저장 | 씬에 하나 두고 `Push` 호출, 뷰어에서 `CopyLatest` 사용 | 고정 길이 버퍼로 최근 기록을 보존하고 UI가 슬라이스를 꺼낼 수 있게 합니다. 【F:Assets/1.Scripts/Dialogue/ChatLogManager.cs†L1-L45】|
| Dialogue/GlossaryDatabase.cs | 글로서리 CSV 로더 및 보유 상태 추적 | Resources CSV를 읽어 `GlossaryDatabase.LoadFromResources` 호출 | 항목 배열과 소유 비트셋을 채우고 색상 코드를 정규화합니다. 【F:Assets/1.Scripts/Dialogue/GlossaryDatabase.cs†L1-L70】|
| Dialogue/GlossaryTypes.cs | 글로서리 데이터 구조 정의 | Glossary DB, UI에서 직접 사용 | 항목/보유 세트 구조체를 정의하고 비트 연산 기반 포함 여부를 제공합니다. 【F:Assets/1.Scripts/Dialogue/GlossaryTypes.cs†L1-L53】|
| Dialogue/GlossaryHighlighter.cs | 본문에 글로서리 링크 삽입 | `GlossaryHighlighter.InjectLinks` 호출 | &id 패턴을 찾아 `<link>` 태그와 색상으로 감싸줍니다. 【F:Assets/1.Scripts/Dialogue/GlossaryHighlighter.cs†L1-L34】|
| Dialogue/GlossaryService.cs | 글로서리 DB 전역 싱글턴 | 빈 GameObject에 부착해 DDOL로 유지 | Awake에서 DB를 로드하고 자신을 유지합니다. 【F:Assets/1.Scripts/Dialogue/GlossaryService.cs†L1-L14】|
| Dialogue/CtcIndicator.cs | Click-to-Continue 인디케이터 제어 | DialogueUI에서 인스펙터로 연결 후 상태 콜백 호출 | 모드/입력/오토 모드에 따라 모양과 점멸, 지연을 조절하며 외부 이벤트에 반응합니다. 【F:Assets/1.Scripts/Dialogue/CtcIndicator.cs†L1-L87】|
| Dialogue/CtcIndicatorConfig.cs | CTC 인디케이터 설정 자산 | `CreateAssetMenu`로 ScriptableObject 생성 | 레이아웃, 타이밍, 색상, 입력 아이콘 스프라이트 프리셋을 제공합니다. 【F:Assets/1.Scripts/Dialogue/CtcIndicatorConfig.cs†L1-L33】|
| Dialogue/GlossaryViewer.cs | 글로서리 UI 패널 | `Open(glossary, focusId)`로 열고, 선택 시 상세 표시 | 페이지 단위 버튼, 소유 여부에 따라 ??? 처리, 패널 애니메이션과 모달 게이트를 관리합니다. 【F:Assets/1.Scripts/Dialogue/GlossaryViewer.cs†L1-L83】|

## Assets/1.Scripts/SaveLoad - 세이브/로드 UI 및 매니저

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| SaveLoad/SaveLoadManager.cs | 수동/오토세이브 통합 관리 | GameRoot에 포함해 `SaveManual`, `RequestLoadFromLobby` 등 호출 | JSON 파일에 메타데이터·플래그·글로서리·썸네일을 저장하고 씬 전환 시 로드 요청을 처리합니다. 【F:Assets/1.Scripts/SaveLoad/SaveLoadManager.cs†L1-L91】|
| SaveLoad/SaveLoadPanel.cs | 세이브/로드 UI 패널 | `Open(Mode.Save/Load)` 호출 | 슬롯 프리팹을 풀링하고 탭, 덮어쓰기 확인, 썸네일 캡처를 담당합니다. 【F:Assets/1.Scripts/SaveLoad/SaveLoadPanel.cs†L1-L86】|
| SaveLoad/SaveSlotView.cs | 각 슬롯의 UI 표현 | `Bind`로 메타와 썸네일 전달 | 슬롯 번호/타임스탬프/플레이타임을 표시하고 버튼 상호작용을 설정합니다. 【F:Assets/1.Scripts/SaveLoad/SaveSlotView.cs†L1-L44】|
| SaveLoad/SaveLoadButton.cs | 단일 버튼용 저장/로드 트리거 | UI 버튼 OnClick에 연결 | 지정 슬롯을 SaveLoadManager를 통해 저장하거나 로드 요청을 보냅니다. 【F:Assets/1.Scripts/SaveLoad/SaveLoadButton.cs†L1-L14】|

## Assets/1.Scripts/UI - 공통 UI 구성 요소

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| UI/PauseMenu.cs | 일시정지 메뉴 및 서브패널 호출 | PauseMenu 오브젝트를 InputRouter나 UI 버튼과 연결 | TimeScale을 조정하고 저장/로드/옵션/컬렉션 패널을 직접 열어줍니다. 【F:Assets/1.Scripts/UI/PauseMenu.cs†L1-L94】|
| UI/OptionsPanel.cs | 옵션 메뉴 조작 | `Open/Close`와 각 OnChange 핸들러 사용 | SettingsManager, ResolutionManager, AudioManager에 값을 반영합니다. 【F:Assets/1.Scripts/UI/OptionsPanel.cs†L1-L96】|
| UI/ConfirmDialog.cs | Yes/No 확인 팝업 | `Open(message, onYes)` 호출 | 모달 게이트에 등록하고 버튼으로 콜백을 실행합니다. 【F:Assets/1.Scripts/UI/ConfirmDialog.cs†L1-L27】|
| UI/PanelAnimator.cs | 패널 오픈/클로즈 트윈 연출 | UI 패널에 부착 후 `PlayOpen`, `PlayClose` 사용 | 페이드/슬라이드/팝 모드와 커브를 설정해 모달창 애니메이션을 제공합니다. 【F:Assets/1.Scripts/UI/PanelAnimator.cs†L1-L76】|
| UI/UiModalGate.cs | UI 모달 상태 전역 스택 | 모달 오픈 시 `Push`, 닫을 때 `Pop`, ESC 처리에 `TryCloseTop` 사용 | 모달 열림 여부를 추적해 입력 차단 및 CTC, 자동 진행 등이 참고합니다. 【F:Assets/1.Scripts/UI/UiModalGate.cs†L1-L39】|
| UI/Codex/CollectionsPanel.cs | 컬렉션 탭 UI | `Open/Close` 호출 | 글로서리와 캐릭터 뷰어를 탭으로 토글하며 GameRoot DB를 재결합합니다. 【F:Assets/1.Scripts/UI/Codex/CollectionsPanel.cs†L1-L49】|
| UI/Codex/GlobalCodex.cs | 도감 소유 정보 PlayerPrefs 저장소 | `GlobalCodex.LoadInto/SaveFrom/Add*` 호출 | 글로서리/캐릭터 소유 여부를 비트셋과 PlayerPrefs로 관리합니다. 【F:Assets/1.Scripts/UI/Codex/GlobalCodex.cs†L1-L65】|
| UI/Codex/LinkButtonOverlay.cs | TMP 링크 위 투명 버튼 생성 | 타겟 TextMeshPro와 버튼 풀을 바인딩 후 `Rebuild` 사용 | 링크 위치를 계산해 버튼을 배치하고 보이는 글자 수에 따라 온/오프합니다. 【F:Assets/1.Scripts/UI/Codex/LinkButtonOverlay.cs†L1-L101】|
| UI/Codex/CharacterHighlighter.cs | 캐릭터 링크 문자열 변환 | `InjectLinks` 사용 | #숫자 토큰을 캐릭터 데이터와 연결된 `<link>` 태그로 변환합니다. 【F:Assets/1.Scripts/UI/Codex/CharacterHighlighter.cs†L1-L28】|
| UI/Codex/CharacterDatabase.cs | 캐릭터 도감 DB 로더 | `LoadFromResources` 사용 | CSV를 파싱해 캐릭터 정보/소유 상태를 채웁니다. 【F:Assets/1.Scripts/UI/Codex/CharacterDatabase.cs†L1-L57】|
| UI/Codex/CharacterVisibilityDatabase.cs | 캐릭터 공개 조건 DB | `LoadFromResources` 사용 | 항목별 공개 플래그를 저장하며 TryGet으로 조회합니다. 【F:Assets/1.Scripts/UI/Codex/CharacterVisibilityDatabase.cs†L1-L49】|
| UI/Codex/CharacterViewer.cs | 캐릭터 도감 UI | `Open(db, focusId)` 호출 | 페이지 단위 버튼, 상세 표시, 소유 자동 포커스를 제공합니다. 【F:Assets/1.Scripts/UI/Codex/CharacterViewer.cs†L1-L83】|
| UI/Codex/GlossaryViewer.cs | 글로서리 UI | `Open(gdb, focusId)` 호출 | 항목 버튼 갱신, 상세 패널, 페이지 이동을 관리합니다. 【F:Assets/1.Scripts/Dialogue/GlossaryViewer.cs†L1-L83】|

## Assets/1.Scripts/Logs - 로그 창 관련

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| Logs/LogViewer.cs | 간단한 텍스트 로그 창 | `Open/Close` 호출, `Rebuild`로 새로고침 | ChatLogManager에서 최신 항목을 가져와 TMP로 렌더링합니다. 【F:Assets/1.Scripts/Logs/LogViewer.cs†L1-L56】|
| Logs/LogViewerList.cs | 스크롤 가능한 로그 리스트 | `Open`으로 패널 활성화 후 자동 업데이트 | 풀링된 항목을 재사용하며 슬라이더와 스크롤 위치를 동기화합니다. 【F:Assets/1.Scripts/Logs/LogViewerList.cs†L1-L105】|
| Logs/LogItemView.cs | 로그 항목 UI 표시 | `Bind(LogEntry, showNodeId, zebra)` 호출 | 스피커/본문 텍스트와 지브라 배경을 설정합니다. 【F:Assets/1.Scripts/Logs/LogItemView.cs†L1-L29】|

## Assets/1.Scripts/Audio - 오디오 시스템

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| Audio/AudioManager.cs | BGM/SFX 재생 및 볼륨 제어 | GameRoot에 포함하여 `PlayBgm/PlaySfx` 등 호출 | 페이드 크로스페이드, 마스터/세부 볼륨, 리소스 폴백을 관리합니다. 【F:Assets/1.Scripts/Audio/AudioManager.cs†L1-L88】|
| Audio/AudioBank.cs | 키-오디오 자산 매핑 | ScriptableObject로 생성해 AudioManager에 연결 | 키와 기본 볼륨을 정의해 빠르게 클립을 찾습니다. 【F:Assets/1.Scripts/Audio/AudioBank.cs†L1-L16】|

## Assets/1.Scripts/MainMenu - 타이틀 및 이동 버튼

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| MainMenu/MoveScene.cs | 스토리 씬 전환 버튼 | UI 버튼 OnClick에 `GoToStage1` 연결 | `InGameStoryScene`으로 씬을 전환합니다. 【F:Assets/1.Scripts/MainMenu/MoveScene.cs†L1-L13】|
| MainMenu/MenuButton.cs | 로비 씬 이동 버튼 | UI 버튼 OnClick에 `GoToLobby` 연결 | `InGameLobby` 씬으로 전환합니다. 【F:Assets/1.Scripts/MainMenu/MenuButton.cs†L1-L12】|
| MainMenu/OpenLoad.cs | 메인메뉴에서 세이브 불러오기 패널 열기 | 버튼 OnClick에 `Open` 연결 | `SaveLoadPanel`을 로드 모드로 열어줍니다. 【F:Assets/1.Scripts/MainMenu/OpenLoad.cs†L1-L13】|

## 기타 공통 스크립트

| 파일 | 제작 목적 | 사용 방법 | 간단한 설명 |
| --- | --- | --- | --- |
| Dialogue/GlossaryService.cs | 글로서리 DB 전역 공유 | DDOL 오브젝트에 부착 | 최초 로드시 DB를 생성하고 이후 인스턴스를 유지합니다. 【F:Assets/1.Scripts/Dialogue/GlossaryService.cs†L1-L14】|

> **비고**: 일부 스크립트는 서로 긴밀하게 연결되어 있으므로 회의 시에는 GameRoot → StoryGameManager → DialogueRunner/DialogueUI → SaveLoad/Collections/Options 순으로 구조를 설명하면 이해가 빠릅니다.
