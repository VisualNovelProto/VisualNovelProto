# Time Loop Schedule Data Workflow Considerations

이 문서는 `TimeLoopSheet` 기반 루프 스케줄 데이터를 CSV/시트로 운용할 수 있도록 하기 위해 필요한 파이프라인과 작업 편의성 개선 방안을 정리합니다.

## 현재 구조의 복잡도가 높게 느껴지는 이유
- `TimeLoopSheet` ScriptableObject 내부에는 슬롯(`TimeLoopSlot`) → 분기(`TimeLoopSlotBranch`) → 요구 지식 키 배열까지 3중 중첩 구조로 데이터가 저장되어 있습니다. 인스펙터에서 이 배열을 수동으로 편집해야 하므로, 데이터가 많아질수록 편집과 검증이 어려워집니다.
- `TimeLoopManager`는 인스펙터에서 이 ScriptableObject 자산을 직접 참조한다고 가정하며, 런타임에 다른 소스(CSV 등)를 로딩하는 코드가 없습니다. 따라서 자산을 제거하면 게임이 즉시 깨집니다.
- 스케줄 데이터를 참조하는 다른 컴포넌트들도 ScriptableObject 자산이 존재한다는 전제를 공유합니다. 자산 구조를 바꾸면 참조를 전부 다시 세팅해야 하므로 작업자 입장에서는 부담이 커집니다.

## 작업자 편의를 높이기 위한 방향
1. **시트에서 작성 → ScriptableObject 자동 생성**
   - 구글 시트에서 슬롯/분기/조건 정보를 입력하고 CSV로 내보낸 뒤, 에디터 메뉴 스크립트가 이를 읽어 `TimeLoopSheet` 자산을 자동으로 재생성하도록 합니다.
   - 자동 생성 스크립트가 헤더 검증, 지식 키 존재 여부 검사 등을 수행하면 수동 편집보다 오류 가능성이 크게 줄어듭니다.
   - 기존 런타임 코드를 유지할 수 있어 이 방식이 가장 빠르게 도입 가능한 개선안입니다.
   - **2024년 5월 업데이트:** `Tools/Time Loop/Import Sheet From CSV...` 메뉴와 `TimeLoopSheetCsvImporter.ImportFromCsv(csv, asset)` 정적 메서드가 추가되어, 시트에서 뽑은 CSV를 선택하면 기존 `TimeLoopSheet` 자산을 자동으로 덮어씌울 수 있습니다. CI나 커맨드라인에서 `-executeMethod` 옵션으로 호출해도 동일하게 동작합니다.

2. **런타임 CSV 파싱으로 완전 전환**
   - `TimeLoopManager`가 `Resources` 폴더의 CSV를 읽어 동적으로 `TimeLoopSheet` 인스턴스를 만들도록 코드를 수정하면, ScriptableObject 자산 없이도 동작합니다.
   - 이 경우에는 매니저가 스스로 데이터를 채우므로, 작업자는 시트 → CSV 내보내기만 반복하면 됩니다.
   - 다만 런타임 초기화 순서와 참조 주입 방식을 재구성해야 하며, 저장/로드 시스템도 CSV 기반 구조를 인식하도록 조정이 필요합니다.

3. **데이터 표현 포맷의 단계적 개선**
   - 슬롯과 분기처럼 계층 구조가 깊은 데이터를 단일 CSV에 담기 어렵다면, 여러 시트를 사용하거나 JSON/TSV 등 계층 표현이 쉬운 포맷으로 바꾸는 것을 고려할 수 있습니다.
   - 어떤 포맷을 선택하든, 결과적으로 `TimeLoopSlot`/`TimeLoopSlotBranch` 배열을 만들어 주는 자동화 단계가 있어야 작업자는 복잡한 ScriptableObject 구조를 직접 만지지 않아도 됩니다.

## 결론
현재 `PrototypeSchedule.asset`을 직접 편집하는 방식은 실제로 작업자에게 복잡하게 느껴지는 것이 맞습니다. 작업 편의성을 높이려면 "시트에서 편집 → 자동 변환" 또는 "런타임 로더" 중 하나를 도입해 ScriptableObject 자산 편집을 최소화해야 합니다. 두 방법 모두 초기 개발자는 다소의 코드를 새로 작성해야 하지만, 한 번 구축해 두면 이후 시나리오 작가는 시트만 다루면 되므로 전체 작업 경험이 훨씬 단순해집니다.

## 부록: CSV 포맷 가이드

자동 변환 스크립트는 아래와 같은 헤더를 가진 CSV를 기대합니다.

| slotIndex | slotLabel | slotMinute | slotNotes | branchName | branchDescription | storyIndexKey | explicitNodeId | requirements |
|-----------|-----------|------------|-----------|------------|------------------|---------------|----------------|--------------|
| 필수      | 선택      | 선택       | 선택      | 선택       | 선택             | 선택          | 선택           | 선택         |

- `slotIndex`: 0부터 시작하는 정수. 같은 슬롯에 대한 여러 분기를 추가하려면 동일한 slotIndex를 가진 행을 여러 개 추가하면 됩니다.
- `slotLabel`: 시계 UI에 노출할 표시 이름. 비워두면 시각(`slotMinute`)을 기반으로 자동 생성됩니다.
- `slotMinute`: 하루 중 분 단위(0~1439). 비워두면 `slotIndex * 30`이 사용됩니다.
- `slotNotes`: 디자이너 메모용 내부 필드.
- `branchName` / `branchDescription`: 분기 UI에 노출할 텍스트.
- `storyIndexKey`: 분기가 선택되었을 때 점프할 스토리 CSV의 Index 키. `explicitNodeId`가 0 이상일 경우 우선합니다.
- `explicitNodeId`: 직접 노드 ID를 지정하고 싶을 때 사용. 음수면 무시됩니다.
- `requirements`: 필요한 지식 키를 `|`, `;`, `,`로 구분해 나열합니다. 비워두면 기본 타임라인 분기로 취급합니다.

CSV를 저장한 뒤 에디터 메뉴 또는 `TimeLoopSheetCsvImporter.ImportFromCsv(<csv>, <assetPath>)`를 실행하면 해당 자산이 최신 데이터로 갱신됩니다.
