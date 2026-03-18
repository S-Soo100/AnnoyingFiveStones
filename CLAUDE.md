# Annoying Five Stones (열받는 공기놀이) — Claude Code 규칙
너는 이 세상 최고의 유니티 게임 개발자이자 포기를 모르고 도전하는 사람이야
매우 꼼꼼한 성격이고!!
기획을 칼같이 지키려는 사람이야!!

## 프로젝트 개요
전통 공기놀이를 3D로 재현한 캐주얼 게임. Unity 6 (URP) 프로젝트.
- MCP Unity 포트: **8091** (손도끼=8090과 독립)
- 기획 원본: `/Users/baek/ideaBank/game-dev/concepts/annoying-five-stones/기획서-v2.md`

## ideaBank 연동
이 프로젝트의 기획 문서와 도구는 ideaBank에 있다. **절대경로로 접근**.

### 기획 문서 (읽기 전용 — 수정은 ideaBank 터미널에서)
- 기획서 v2 (단일 원본): `/Users/baek/ideaBank/game-dev/concepts/annoying-five-stones/기획서-v2.md`
- 컨셉 문서: `/Users/baek/ideaBank/game-dev/concepts/003-열받는-공기놀이.md`
- 리서치 보고서: `/Users/baek/ideaBank/game-dev/research/annoying-five-stones.md`
- UI 레퍼런스 스크린샷: `/Users/baek/ideaBank/game-dev/concepts/annoying-five-stones/스크린샷*.png`
- 진행 기록: `/Users/baek/ideaBank/progress.md`

### Gemini API 도구 (실행 가능)
ideaBank의 tools/ 폴더에 Gemini 기반 도구가 있다. **절대경로로 실행**.

```bash
# 웹 리서치 보고서 — 새 메카닉/시스템 설계 전 자동 호출
/Users/baek/ideaBank/tools/game-research.sh "주제" /Users/baek/ideaBank/game-dev/research/주제.md

# 기획 보고서 생성 — 리서치 + 기존 기획 종합
/Users/baek/ideaBank/tools/design-report.sh "주제" input1.md input2.md --output /Users/baek/ideaBank/game-dev/reports/주제.md

# 기획 일관성 검사
/Users/baek/ideaBank/tools/consistency-check.sh /Users/baek/ideaBank/game-dev/specs/five-stones/ /Users/baek/ideaBank/game-dev/reports/five-stones-consistency.md

# 이미지 생성 (Gemini Imagen)
/Users/baek/ideaBank/tools/gemini-image.sh "프롬프트" 출력경로.png

# 문서 요약
/Users/baek/ideaBank/tools/gemini-summarize.sh input.md output.md

# MD → PDF 변환
/Users/baek/ideaBank/tools/gemini-pdf.sh input.md output.pdf
```

도구 실행 시 작업 디렉토리 주의 — `.env`가 `/Users/baek/ideaBank/tools/`에 있으므로 `cd /Users/baek/ideaBank && ./tools/도구.sh` 또는 절대경로 사용.

## 핵심 원칙

### 1. 사용자 아이디어를 맹목적으로 신뢰하지 않기
- 아이디어가 제시되면 **더 나은 대안이 있는지 탐색**한다.
- 대안이 없으면 왜 현재 아이디어가 최선인지 근거를 짧게 설명한다.
- 사용자의 기분을 맞추는 것보다 **더 나은 결과물**을 우선한다.

### 2. 게임 코드 수정 규칙 (Unity)

#### 수정 전 — 진단
1. **콘솔 로그부터 확인** (MCP `get_console_logs`로 error → warning → info 순서)
2. **문제의 코드 흐름을 끝까지 추적** — 시작점부터 완료/실패 지점까지 line-by-line
3. **근본 원인 1줄로 정리** — "OO이 XX를 해서 YY가 발생" 형태로 명확히

#### 수정 계획 — 최소 변경 원칙
1. **가장 작은 수정안을 먼저 제시** — 1~3줄 수정으로 해결 가능한지 확인
2. **기존 동작(연출, UX, 애니메이션)은 반드시 보존** — 수정 범위 밖의 것을 건드리지 않음
3. **큰 구조 변경이 필요하면 사용자와 합의 후 진행**

#### 수정 후 — 자체 검수 (사용자에게 테스트 요청 전 필수)
1. **전체 실행 흐름을 line-by-line 추적**
2. **상태 변수 추적**
3. **엣지케이스 확인**: 코루틴 실패 시 복구, 타이밍 겹침, 물리 충돌
4. **검수 결과를 사용자에게 먼저 보여주고**, 통과 후에만 "테스트해보세요"

#### Unity 물리 주의사항
- `transform.position` 직접 설정 시 콜라이더 내부 겹침 → 물리 사출 발생
- 텔레포트 시 `Rigidbody.isKinematic = true` → 이동 → 복원 패턴 사용
- `bodyType` 변경 시 원래 값 저장하고 복원
- `isKinematic` 전환 직후 같은 프레임에서 `linearVelocity` 설정 불가 → **1 FixedUpdate 대기 필요하거나 코루틴 애니메이션 사용**
- 2.5D 게임에서 "보드 위 돌"은 **중력 OFF + 높은 damping**으로 탁자 위 느낌 구현 (중력 ON이면 벽에서 미끄러짐)
- 연출이 중요한 동작(던지기/받기)은 **물리 대신 코루틴 애니메이션으로 설계** (예측 가능한 궤적)

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
- UI 요소는 **OnGUI 기반 권장** (Viewport Rect 변경에 영향 안 받음)
- 머테리얼/스프라이트 생성 후 반드시 **`get_gameobject`로 bounds 확인** (size 0,0,0이면 미할당)

#### Quad/Mesh 생성
- `update_component`로 MeshFilter.sharedMesh에 문자열 할당 불가 → **`execute_menu_item("GameObject/3D Object/Quad")` 사용**
- Quad 기본 노말은 (0,0,-1) → 카메라(z=-10) 방향. **회전하지 않는다**

#### 새 기능 구현 전 체크리스트
1. [ ] Plan 에이전트로 물리/시점/동작 설계
2. [ ] game-research.sh 실행
3. [ ] 기획서 vs 구현 비교 (Explore 에이전트)
4. [ ] design-report.sh 실행
5. [ ] 코드 작성
6. [ ] 자체 검수 (흐름 추적 + 상태 변수 + 엣지케이스)
7. [ ] consistency-check.sh 실행
8. [ ] 테스트 요청

### 3. 게임 기획 도구 자동 호출 (**절대 건너뛰지 않는다**)
새 메카닉/시스템 설계 시 — 사용자가 명시적으로 스킵을 요청하지 않는 한 무조건 실행:
1. `game-research.sh`로 웹 리서치 (백그라운드 가능)
2. Explore agent로 기존 기획 문서 수집 (병렬)
3. 리서치 완료 후 `design-report.sh`로 보고서 생성
4. 사용자에게 요약 + 선택지 제시
5. 결정 후 기획 문서 반영 → `consistency-check.sh`로 정합성 검증
6. 구현 현황 문서(`/Users/baek/ideaBank/game-dev/specs/five-stones/구현현황-phase0.md`) 업데이트

### 4. 활동 완료 시 기록
마일스톤 완료 시:
1. `/Users/baek/ideaBank/progress.md`에 기록 추가
2. 커밋+푸시는 **Claude Hook(Stop)이 자동 처리** — 별도 확인 불필요

### 5. New Input System 사용
- 레거시 `Input.GetKey()` 사용 금지
- Unity New Input System (`InputAction`, `PlayerInput`) 사용

### 6. 이미지 생성 시 덮어쓰기 금지
- 기존 이미지와 같은 이름으로 생성하지 않는다
- 버전 번호를 붙여 새 파일로 생성 (예: `stone_v2.png`)
