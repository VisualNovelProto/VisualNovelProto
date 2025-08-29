# NaniPro — VN Runner (Open, Extended)

> Naninovel의 워크플로우에서 영감을 받은 **오픈 구현**입니다. 상용 에셋의 소스나 리소스는 포함하지 않으며, 자유롭게 커스터마이즈 가능한 확장 엔진 스캐폴드입니다.

## 핵심 기능
- `.nani` 유사 스크립트 파서
- 라벨/점프/선택지(조건부) & if/elseif/else/endif
- 변수 저장소(@set, 대사 내 {var} 치환)
- 타입라이터 효과, Auto/Skip
- 배경/캐릭터 표시·숨김·이동·페이드, 간단 트랜지션
- 간단 저장/불러오기(JSON, 슬롯)
- 다국어 스크립트 경로(`Resources/Scripts/<lang>` 선택)
- 런타임 UI 자동 생성

## 빠른 시작
1. 이 폴더를 Unity 프로젝트에 추가.
2. 빈 씬에 `RuntimeInitializerPro` 컴포넌트를 추가하고 Play.
3. 배경/캐릭터 스프라이트를 `Resources/Backgrounds`, `Resources/Characters`에 추가.
4. 샘플 스크립트: `Resources/Scripts/ko/ProDemo.nani`

## 스크립트 문법
```
# Start
@lang ko                       // 언어 선택(ko/en)
@set player = "준우"           // 변수
@back city_sunset fade:0.5     // 배경, 페이드
@char KANA show at:left        // 캐릭터 표시
KANA: "안녕, {player}! 시작해볼까?"

* "왼쪽" -> Left if score >= 1
* "오른쪽" -> Right

# Left
"왼쪽을 택했다."
@goto End

# Right
"오른쪽을 택했다."
@goto End

# End
"데모 끝!"
```

### 명령어 요약
- `# Label` : 라벨
- `@goto Label` : 점프
- `@set a = 3`, `@set flag = true`, `@set name = "카나"`
- `@if a > 2` ... `@elseif flag` ... `@else` ... `@endif`
- `* "문구" -> Label [if expr]`
- `@back path [fade:sec]`
- `@char NAME show [appearance:SpriteName] [at:left|center|right] [fade:sec]`
- `@char NAME hide [fade:sec]`
- `@move NAME to:left|center|right [time:sec]`
- `@lang ko|en` : 언어 리소스 선택(`Resources/Scripts/<lang>`)
- 대사: `NAME: "텍스트"` 또는 `"나레이터"` (변수 `{var}` 치환)

## 저장/불러오기 API
`ScriptPlayerPro.Save(slot)` / `ScriptPlayerPro.Load(slot)` — 간단 상태 저장/복구.
