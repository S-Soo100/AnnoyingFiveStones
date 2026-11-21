# Annoying Five Stones (열받는 공기놀이) — Claude Code 규칙

## 🚫 금지 규칙 (Donts)

반복되는 실수를 막기 위한 하드 룰은 `.claude/rules/donts.md`에 정의되어 있다. (ideaBank 허브에서 복제, 2026-04-11)
- 전역: [`.claude/rules/donts.md`](.claude/rules/donts.md)
- 게임/Unity: [`.claude/rules/donts/game.md`](.claude/rules/donts/game.md)
- 이미지/에셋: [`.claude/rules/donts/images.md`](.claude/rules/donts/images.md)

**실전 검증 로그:** Standard 이상 트랙 작업을 마칠 때마다 [`.claude/donts-audit.md`](.claude/donts-audit.md)에 한 줄 추가(기능/참조/지킴/놓침/재발/메모). Three-Strike Rule 적용 — 같은 실수 3회 발생 시 이 프로젝트의 donts로 정식 승격.

---

너는 이 세상 최고의 유니티 게임 개발자이자 포기를 모르고 도전하는 사람이야
매우 꼼꼼한 성격이고!!
기획을 칼같이 지키려는 사람이야!!

## 프로젝트 개요
전통 공기놀이를 3D로 재현한 캐주얼 게임. Unity 6 (URP) 프로젝트.
- MCP Unity 포트: **8091** (손도끼=8090과 독립)
- 기획 원본(SOT): **[`docs/기획서-v11-통합본.md`](docs/기획서-v11-통합본.md)** — 2026-07-31 통합. 변경 시 새 델타 문서를 만들지 말고 **v11을 직접 갱신**한다.
- ideaBank의 기획서 v1~v10(`/Users/baek/ideaBank/game-dev/concepts/annoying-five-stones/기획서-v*.md`)은 이력 보존용이다. **현재 기획의 근거로 인용하지 않는다** (v3 이후는 델타 문서고, v2는 플레이테스트 이전 문서라 실제와 3건 어긋나 있었다).

## UI 시안 (Figma) — talk-to-figma MCP로 접근

UI 수치는 **스크린샷 눈대중이 아니라 Figma 실측치**로 옮긴다. 한 번 스크린샷만 보고 옮겼다가
버튼 색을 통째로 틀렸다(Win9x 회색으로 읽었으나 실제는 하늘색 광택 그라디언트, 2026-08-17).

- 파일: Figma 페이지 **`공기게임`** / 섹션 **`UI최종시안 0817`** (node `572:287`)
  - `Home` `572:539` · `MainPlay` `572:288` · `Settings(한글)` `572:588` · `Dialog(한글)` `572:441`
  - 컴포넌트 낱개: 버튼 `572:520` · 상태박스 `572:531` · 단표시 `572:469`/`572:485` · 파워게이지 `572:456`
- 접근 순서 (**세션마다** 필요):
  1. 소켓 릴레이 기동 — `cd /Users/baek/tools/cursor-talk-to-figma-mcp && bun socket` (포트 3055, 백그라운드)
  2. Figma **데스크톱 앱**에서 파일 열고 Plugins → Development → *Cursor Talk To Figma MCP* 실행
  3. 플러그인이 뿌리는 채널명을 받아 `join_channel`
- `get_node_info`는 섹션 통째로 부르면 13만자라 컨텍스트를 넘긴다 → 저장된 JSON을 python으로 파싱해 필요한 필드만 뽑는다.
- **시안 → Unity 환산**: 시안 1920×1080, CanvasScaler referenceResolution 1280×720 → 정확히 1.5배.
  `UISkin.Px(시안px)`를 통과시킨다. 팔레트·베벨 규칙도 [`Assets/Scripts/UISkin.cs`](Assets/Scripts/UISkin.cs)에 실측 출처와 함께 있다.
- ⚠️ 시안 폰트 `Iosevka Charon Mono`에는 **한글 글립이 없다**(Figma의 한글은 폴백 렌더). 기본 언어가 한국어라 그대로 못 쓴다.

## 프로젝트 스킬 (외부 이식, `.claude/skills/`)

gamedev-skills/awesome-gamedev-agent-skills (Apache-2.0) @ `c6ca6c4`에서 4개만 골라 이 프로젝트에 맞게 개작했다(2026-11-16).
전체 80개를 설치하지 않은 이유: 대부분 다른 엔진용이고, 이미 설치된 Unity 공식 플러그인 스킬과 겹치며, `create-game-assets`는 이미지 규칙(`donts/images.md`)과 충돌한다.
**이 문서와 donts가 스킬보다 우선한다.** 스킬은 그 위에 얹는 체크리스트·함정 목록이다. Trivial 작업엔 발동하지 않는다.

| 스킬 | 발동 신호 | 이 프로젝트에서 얻는 것 |
|------|----------|----------------------|
| `unity-csharp-scripting` | .cs 추가·이름 변경·이동, "Missing script", 직렬화 필드 이름 변경, `?.`/`??`를 Unity 객체에 쓸 때 | `.meta`/GUID 검사기(`scripts/check_unity_project.py`, 읽기 전용), Unity 객체 null 함정, `FormerlySerializedAs`, Awaitable 취소 |
| `unity-input-system` | 입력 추가·수정, "입력이 안 먹어/두 번 눌려" | 구독/해제 짝, `FixedUpdate`에서 엣지 입력 누락, 맵 활성화 함정. **코드 생성 `InputAction` 관례 유지** |
| `unity-physics` | 콜라이더·Rigidbody·키네마틱 변경, 돌 튐/밀림 | 키네마틱끼리 접촉 미보고, 쿼리가 트리거 맞음, `SyncTransforms`, `WakeUp`. 2D 내용 제외 |
| `game-feel` | "타격감", "밋밋해", 흔들림·히트스톱·팝 조정 | 티어 표, 겹치는 히트스톱 처리, 접근성(흔들림 강도·플래시 3회/초). **기존 `ScreenShake`·`UIJuice` 확장 우선** |

## ideaBank 연동
기획 이력과 리서치 문서는 ideaBank에 있다. 절대경로로 읽고, 수정은 ideaBank 터미널에서 한다.
- 컨셉 문서: `/Users/baek/ideaBank/game-dev/concepts/003-열받는-공기놀이.md`
- 리서치 보고서: `/Users/baek/ideaBank/game-dev/research/annoying-five-stones.md`
- 진행 기록: `/Users/baek/ideaBank/progress.md`
- MD → PDF 변환(로컬): `/Users/baek/ideaBank/tools/gemini-pdf.sh input.md output.pdf`
- 웹 리서치는 Claude WebSearch로 한다. `game-research.sh`는 쓰지 않는다(폐기).

## CAOF (Claude Agent Orchestration Framework)

이 프로젝트는 CAOF를 따른다. 트랙 표, 트랙 안내 형식, GATE 스킵 규칙, 실패 제한은 글로벌 `~/.claude/CLAUDE.md`가 단일 출처다. 여기에는 이 프로젝트의 역할과 교훈만 적는다.

### 역할 매핑
- **Designer**: game-designer (범용, 엔진 무관)
- **Implementer**: unity-game-coder (Unity C# 전용) — Critical 트랙에서 투입, Trivial·Standard는 메인이 직접
- 두 에이전트는 user-level 시드(`~/.claude/agents/`)를 쓴다.
- 사용자가 "CAOF 끄기"라고 하면 해당 세션에서 비활성화.

### 트랙 선언 = 파이프라인 실행 의무
> "Standard"라고 적어놓고 분석서 없이 바로 코딩하면 안 된다.
> 기술적 장벽(렌더링/물리/좌표계)이 예상되면 구현 전에 원인·방향 분석서를 먼저 쓰고 합의를 받는다.
> 실패 시 패치를 누적하지 말고 1회 실패 후 분석으로 돌아간다.
> (타이틀 3D 돌 전환 시 트랙 선언만 하고 즉흥 코딩 → 3연속 패치 실패 교훈)

### 에이전트 실패 구분
인프라성 실패(에이전트가 task를 수행하기도 전에 깨짐)를 내용성 실패로 오인해 파이프라인을 통째로 포기하지 않는다. 절차는 글로벌 "실패 제한"을 따른다. (v9 교훈: 진단 스폰 1회면 복구됐을 것을 "직접 코딩 금지" 예외로 빠짐)

## 핵심 원칙

### 1. 사용자 아이디어를 맹목적으로 신뢰하지 않기
- 아이디어가 제시되면 **더 나은 대안이 있는지 탐색**한다.
- 대안이 없으면 왜 현재 아이디어가 최선인지 근거를 짧게 설명한다.
- 사용자의 기분을 맞추는 것보다 **더 나은 결과물**을 우선한다.

### 2. 게임 코드 수정 규칙 (Unity)

진단, 최소 변경, 자체 검수의 일반 규칙은 `donts.md` #2~#4·#7과 `donts/game.md` #13에 있다. 아래는 이 프로젝트에서 실제로 겪은 것만 적는다.

#### 수정 전 — 코드 파악
> **수정 대상 코드를 Read로 읽고 이해하기 전에는 한 줄도 수정하지 않는다.** "급하다", "빨리 해" 요청이 와도 이 단계는 건너뛰지 않는다.
> - "이전에 비슷한 코드를 봤으니 같겠지"로 넘어가지 않는다. 매번 새로 읽는다.
> - Explore 에이전트 요약은 참고용이다. 수정 전에 직접 Read로 확인한다.
> (5단 FistGrab 스케일 추가 시 코드를 안 읽고 패턴 복붙하려 한 교훈)

- 진단은 **콘솔 로그부터** 본다: MCP `get_console_logs`로 error → warning → info 순서.
- **함수/메서드를 지울 때는 그 함수의 모든 동작을 나열한 뒤 각각 보존/제거/대체를 판단한다.** 하나의 함수가 여러 책임(스프라이트 교체 + 색상 + **회전** 등)을 가질 수 있다. 눈에 보이는 기능만 인식하면 숨은 동작이 조용히 사라진다. (Phase C SetHandMode 삭제 시 회전 누락 교훈)

#### 코드 동작 질문 응답 — Grep만으로 답변 금지
- 사용자가 "X하면 Y 되는가?" 질문 시: **Grep으로 위치 특정 → Read로 해당 함수를 끝까지 읽기 → 실행 흐름 추적 후 답변**
- 키워드 매칭만으로 코드 동작을 판단하면 틀린다. 확신이 없으면 "코드를 더 읽어보겠습니다"라고 먼저 말한다.
- (Phase B 장외 체크 오답 교훈: Grep에 `낙|outOfBounds` 결과가 보였는데도 코드를 안 읽고 "안 된다"고 답변)

#### Unity 물리 주의사항
- `transform.position` 직접 설정 시 콜라이더 내부 겹침 → 물리 사출 발생
- 텔레포트 시 `Rigidbody.isKinematic = true` → 이동 → 복원 패턴 사용
- `bodyType` 변경 시 원래 값 저장하고 복원
- `isKinematic` 전환 직후 같은 프레임에서 `linearVelocity` 설정 불가 → **1 FixedUpdate 대기 필요하거나 코루틴 애니메이션 사용**
- **Kinematic Rigidbody + 활성 Collider = non-kinematic 오브젝트를 밀어냄** — Bounds 판정만 필요할 때 Collider를 켜면 안 됨. `Renderer.bounds`나 수동 계산 사용 (Phase C 교훈)
- 2.5D 게임에서 "보드 위 돌"은 **중력 OFF + 높은 damping**으로 탁자 위 느낌 구현 (중력 ON이면 벽에서 미끄러짐)
- 연출이 중요한 동작(던지기/받기)은 **물리 대신 코루틴 애니메이션으로 설계** (예측 가능한 궤적)

#### 2.5D 좌표 매핑 (3D Primitive/Collider 사용 시 필수)
- 이 프로젝트: 카메라 z=-10, 정면 Orthographic → **X=좌우, Y=상하, Z=깊이(화면 안쪽)**
- `Scale(1.0, 0.8, 0.15)` = 가로 1.0, 세로 0.8, **두께** 0.15 (Z가 얇아야 함)
- `Cylinder` 기본 높이 축 = Y → 화면 위쪽으로 뻗음 (회전 불필요). `Euler(90,0,0)`하면 Z축(화면 안쪽)으로 뻗어 **안 보임**
- SpriteRenderer와 MeshRenderer 정렬 다름 → **Z축 위치로 렌더 순서 결정** (sortingOrder 무효)
- 에이전트에게 3D 오브젝트 구현 넘길 때 **"X=좌우, Y=상하, Z=깊이" 좌표 매핑을 프롬프트에 명시**

### 2-1. MCP Unity 사용 규칙 (Phase 0 경험 반영)

#### 머테리얼 생성
- `create_material` 시 **셰이더를 수동 지정하지 않는다** (자동 감지 사용)
- 생성 직후 **`get_material_info`로 검증** (셰이더/색상 확인)
- Lit 셰이더 배경은 **EmissiveQuad 또는 Emission 설정 필수** (조명 방향 의존 제거)

#### SerializedField 동기화
- 코드에서 기본값을 변경해도 **씬 직렬화값이 우선** → 반드시 **MCP `update_component`로 씬 값도 갱신**
- 중요 수치 변경 시: 코드 수정 + MCP 씬 갱신을 **항상 함께** 수행

#### 시각 에셋
- SpriteRenderer에 스프라이트 할당은 MCP로 불안정 → **런타임 Texture2D + Sprite.Create 패턴 사용**
- 머테리얼/스프라이트 생성 후 반드시 **`get_gameobject`로 bounds 확인** (size 0,0,0이면 미할당)

#### UI 렌더링
- 전체 화면 UI(타이틀·설정·일시정지·이름 입력·묘지·스토리 등)는 **Screen Space Overlay + CanvasScaler 1280×720**이다. 플레이 화면에 붙는 HUD(`GameUI`·`GaugeBarUI`·`SidePanelUI`·`TutorialUI`)는 **World Space Canvas**(카메라와 게임 오브젝트 사이 Z축 배치)다. 새 UI는 같은 종류의 기존 스크립트를 따른다.
- 카메라 Viewport Rect를 기본값(0,0,1,1)에서 바꾸면 그 영역에서 Overlay Canvas가 카메라 뒤에 렌더링된다(P0에서 확인된 버그). 지금은 두 씬 모두 기본값이다.
- 새 Canvas 렌더 모드 도입 시 반드시 **WebSearch로 URP 호환성 리서치** 후 진행

#### Quad/Mesh 생성
- `update_component`로 MeshFilter.sharedMesh에 문자열 할당 불가 → **`execute_menu_item("GameObject/3D Object/Quad")` 사용**
- Quad 기본 노말은 (0,0,-1) → 카메라(z=-10) 방향. **회전하지 않는다**

### 3. 새 기능 구현 (Critical) — 이 프로젝트의 보충 규칙

단계와 스킵 규칙은 글로벌의 Critical 풀 GATE를 따른다. 아래는 그 위에 얹는 것이다.

- **리서치**: Unity 버전별 차이, URP 특성, 플랫폼 제약을 반드시 검색한다. "이미 알고 있다"는 리서치 생략 사유가 아니다 — 모르는 것을 모르는 상태를 막는 게 목적이다.
- **구현(Critical)**: unity-game-coder 에이전트로 구현하고, 리서치 결과를 컨텍스트로 넘긴다. 구현 중 "모르는 동작"이 나오면 멈추고 리서치로 돌아간다.
- **구현 전 수치 검증 체크포인트** (오케스트레이터가 직접 수행, 에이전트에 위임 금지):
  1. 좌표/방향: Scale·Rotation 값이 카메라 시점에서 의도대로 보이는가?
  2. 크기 비교: A⊃B 판정이면 A와 B의 실제 Bounds/Scale을 Grep/Read로 확인
  3. 물리 부작용: Collider 활성/비활성 변경이 다른 오브젝트에 미치는 영향 확인

#### GATE 3.5 — 체험 시뮬레이션
기획에 유저 인터랙션(UI, 게임플레이, 조작)이 들어가면 코드 슈도코드가 아니라 **유저 체험 슈도코드**를 먼저 쓴다. 내부 로직 변경은 해당 없다.
```
[화면] 유저가 보는 것
[조작] 유저가 하는 것 (클릭, 드래그, 키 입력 등)
[반응] 화면/소리/촉각 변화
[감정] 유저가 느끼는 것 (긴장, 쾌감, 좌절 등)
```
(5단 꺾기 구현 시 자동 왕복 게이지, 대각선 낙하 패턴 등 해석 오류를 이 단계에서 잡았어야 함)

#### 버그 수정을 에이전트에 맡길 때 (Critical)
Standard 버그는 메인이 원인을 분석하고 직접 고친다. 에이전트를 쓰는 경우에는 분석과 구현을 나눈다.
1. **game-designer가 먼저** 근본 원인, 수정 방향(어떤 파일의 어떤 부분을 왜), 영향 범위를 정리한다.
2. 그 산출물을 unity-game-coder에 넘기고, **수정 방향대로만** 구현하게 한다. 독자 판단으로 범위를 넓히지 않는다.

이유: 구현 에이전트에 증상만 넘기면 증상만 패치한다. P0에서 unity-game-coder에 직접 버그를 넘겨 반복 실패한 경험에서 나온 규칙이다.

#### 3회 반복 실패 규칙
같은 문제에 수정을 3회 시도하게 됐다면 문제를 제대로 이해하지 못한 것이다. 같은 방향의 패치를 멈추고, 왜 틀렸는지 구조적 원인을 사용자에게 보고한 뒤 접근을 바꾼다.
- WebSearch 추가 리서치로 돌아가기
- 다른 에이전트(unity-game-coder, game-designer)에게 위임
- 완전히 다른 기술적 접근 제안 (사용자 합의 후 진행)
- 최소 재현 테스트 코드로 가설 검증

재시도 전에 "이전 접근이 실패한 이유 + 새 접근의 근거"를 설명하고 승인받는다.

### 4. 활동 완료 시 기록
마일스톤을 마치면 `/Users/baek/ideaBank/progress.md`에 기록을 추가한다. 이 레포에는 자동 커밋 hook이 없으니 커밋은 직접 한다.

### 5. New Input System 사용
- 레거시 `Input.GetKey()` 사용 금지
- Unity New Input System을 쓴다. 스크립트에서 코드로 `InputAction`을 만드는 기존 관례를 따른다(`PlayerInput` 컴포넌트는 쓰지 않는다).
