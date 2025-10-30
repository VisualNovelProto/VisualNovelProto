# 스토리 데이터 파이프라인 가이드

이 문서는 시나리오 라이터와 엔지니어가 CSV 기반 데이터 자산을 일관성 있게 관리할 수 있도록 프로젝트의 핵심 시트 구조와 저장/로드 규칙을 정리합니다.

## 1. 대사 CSV (`StoryText/main.csv`)

| 컬럼 | 필수 | 설명 |
|------|------|------|
| Index | 선택 | 사람 읽기용 식별자. 타임루프 목적지 등 외부 데이터가 노드를 참조할 때 사용합니다. |
| nodeId | 필수 | 정수 노드 ID. 저장/로드 및 분기 이동의 기준 값입니다. |
| rowType | 필수 | `Node` 또는 `Choice`. `Choice` 행은 바로 앞 Node의 선택지로 묶입니다. |
| speaker | 선택 | 발화자 표시 문자열. 비워두면 이름이 숨겨집니다. |
| text | 선택 | 본문 텍스트. 리치 텍스트와 링크 태그 사용 가능. |
| voice | 선택 | (Legacy) 음성 키. 현재 파이프라인에서는 사용하지 않지만, 호환을 위해 남겨둡니다. |
| actors | 선택 | `key@Anchor(in=fx)` 형식의 배우 연출 명령. 여러 명은 `;`로 구분합니다. |
| bgm / sfx / cg / transition | 선택 | 문자열 키로 정의된 연출 명령. 공백이면 유지. |
| advancePolicy | 선택 | `block` / `fast` 등 진행 방식 힌트. |
| nextNodeId | 선택 | 자동 진행 시 이동할 다음 노드. 음수면 종료. |
| choiceLabel | (Choice) 필수 | 선택지에 표시될 문자열. |
| choiceGoto | (Choice) 선택 | 선택 시 이동할 노드 ID. 비우면 해당 노드의 `nextNodeId` 사용. |
| choiceSet | (Choice) 선택 | 선택 시 세트할 플래그 ID를 `&`로 연결. |
| flagsSet | 선택 | 노드 진입 시 세트할 플래그 ID. `&`로 연결. |
| flagsReq | 선택 | 노드가 잠금 해제되기 위한 플래그 ID. 모두 만족해야 합니다. |
| timeLoopKnowledge | 선택 | 타임루프 시스템에서 획득하는 지식 키 목록. `|` 구분. |

### 작성 규칙
- `nodeId`는 CSV 전체에서 유일해야 하며, 삭제된 ID도 재사용하지 않는 것을 권장합니다.
- `flagsSet`/`flagsReq`에 기입한 값은 아래 플래그 도메인 규칙을 따라야 합니다.
- 선택지(`Choice`) 행은 반드시 직전에 등장한 `Node`를 부모로 인식합니다. `nodeId`가 `123-1`처럼 하이픈을 포함해도 숫자 앞부분(`123`)이 부모로 매핑됩니다.

## 2. 플래그 도메인 규칙

`DialogueRunner`는 세션 플래그와 영구 플래그를 동시에 관리합니다. 어떤 플래그가 어느 영역으로 저장되는지 명확히 하기 위해 `FlagDomainCatalog` ScriptableObject를 도입했습니다.

- `FlagDomainCatalog`는 `persistentFlags`(영구 저장)와 `sessionOnlyFlags`(세션 한정) 배열을 제공합니다.
- `treatUnlistedAsPersistent`가 `true`이면 목록에 없는 플래그는 기본적으로 영구 저장 대상으로 간주됩니다. 프로젝트 초창기와의 호환성을 위해 기본값은 `true`입니다.
- 세션 전용 플래그를 만들고 싶다면 `sessionOnlyFlags`에 ID를 명시하세요. 해당 플래그는 저장 슬롯과 로드 사이클에서는 유지되지만, `GlobalFlags`에는 기록되지 않습니다.
- CSV에서 새 플래그를 추가할 때는 반드시 `FlagDomainCatalog`도 함께 갱신해 협업자에게 의도를 공유하세요.

## 3. 타임루프 목적지 (`Story.csv` Index)

- `TimeLoopManager`는 `Story.csv`의 `Index` 컬럼을 읽어 자동으로 목적지를 구성하며, 하나의 행에서 `|` 구분자를 사용해 표시 정보를 함께 적을 수 있습니다.
- 기본 포맷은 `키|표시라벨|분기라벨|세부설명`이며, 첫 번째 토큰(키)만 필수입니다. 나머지 값은 비워도 되며, 비워둘 경우에는 키를 사람이 읽기 쉬운 형태(언더바 제거 등)로 변환해 사용합니다.
- `0823-0923`, `2024-08-23 09:23`, `09:23` 등 표준 날짜/시간 포맷은 자동으로 인식되어 타임스탬프 기반으로 정렬/표시에 활용됩니다.
- 분기 라벨이 비어 있으면 키 문자열이 그대로 표시되며, 세부 설명이 비어 있을 경우에는 원본 인덱스 문자열이 필요 시 보조 정보로 사용됩니다.
- 각 목적지 키는 고유해야 하며 빈 문자열은 무시됩니다. CSV만 수정하면 시계 UI 및 루프 매니저가 즉시 갱신되므로 별도의 프리팹이나 ScriptableObject를 편집할 필요가 없습니다.

## 4. 글로서리 & 캐릭터 데이터

| 파일 | 기본 경로 | 비고 |
|------|-----------|------|
| glossary.csv | `Resources/StoryText/glossary.csv` | 용어집 항목. `GlossaryDatabase`가 자동 로드합니다. |
| characters.csv | `Resources/StoryText/characters.csv` | 캐릭터 도감. `CharacterDatabase`가 자동 로드하며, DataBootstrap이 UI에 주입합니다. |

- 두 CSV 모두 첫 행을 헤더로 사용하며, 필드 사이에 쉼표/따옴표 규칙은 일반적인 RFC 4180을 따릅니다.
- `CharacterDatabase`는 CSV에서 등장한 최대 ID까지만 배열을 할당하므로, 불필요한 메모리 낭비를 피할 수 있습니다.
- 컬렉션 UI가 자동으로 최신 데이터를 로드하려면 `DataBootstrap`을 씬에 배치하고, 필요한 경우 UI 참조를 연결하면 됩니다.

## 5. 씬 의존성 주입 및 세이브/로드 안전성

- `SceneRefHub`는 씬에 존재하는 `DialogueRunner`, `DialogueUI`, `CollectionsPanel`, `TimeLoopWatchUI` 등을 한 번에 주입합니다.
- `SaveLoadManager`와 `TimeLoopManager`는 `SceneRefHub`에서 제공하는 참조만 사용하며, 더 이상 `FindObjectOfType`에 의존하지 않습니다. 씬 전환 시에는 `SceneRefHub`가 활성화되는 순간 자동으로 재바인딩됩니다.
- 세이브 매니저는 `SceneManager.sceneLoaded` 이벤트를 중복 구독하지 않도록 변경되었으며, 로드 중에 의도치 않은 중복 호출이 발생하지 않습니다.

## 6. 체크리스트

- [ ] `Story.csv`의 `Index` 값이 최신 상태인지, 동일한 키가 의도한 노드 집합을 가리키는지 확인합니다.
- [ ] 새로운 플래그를 추가했으면 `FlagDomainCatalog`를 업데이트합니다.
- [ ] Glossary/Character CSV를 변경했다면 `DataBootstrap`이 참조하는 경로(`StoryText/*`)가 일치하는지 확인합니다.
- [ ] 씬에 `SceneRefHub`가 존재하는지, 그리고 필요한 UI/매니저 참조가 모두 연결되어 있는지 검토합니다.

이 가이드는 계속 업데이트될 예정이며, 개선이 필요하면 `Docs/story_data_pipeline.md`에 변경 사항을 기록해 주세요.
