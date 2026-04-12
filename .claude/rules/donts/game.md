# Don'ts — 게임 / Unity

> Unity/게임 작업 시 반복된 실수. 루트 [`donts.md`](../donts.md)의 제너럴 규칙과 함께 적용.

## 🎮 게임플레이 / 시스템

1. **퀘스트 조건 체크 시 `isCompleting` 가드 필수** — 중복 완료 호출 방지. 가드 없이 조건 체크 → 보상 2회 지급 버그.
2. **퀘스트 전환 시 `CurrentQuest` 즉시 클리어** — 전환 직후 이전 퀘스트 참조 남으면 트리거 오발. 새 퀘스트 할당 전에 반드시 null.
3. **AutoWalk는 장애물 없는 경로에만** — A* 없이 직선 이동이므로 벽/장애물 있는 맵에서 끼임. 내부 통로/직선 구간에만 사용.
4. **빈 시간은 시스템이 아닌 콘텐츠로 채우기** — "할 게 없네" → 새 시스템 추가 유혹 금지. 서사·이벤트·대사로 먼저 채운다.
5. **맵 재설계 시 기존 구조 보존** — 호수 형태, 동선, 크기는 명시 요청 없으면 그대로. 재배치만 허용, 리셋 금지.

## 🧰 Unity 엔진

6. **New Input System 사용** — 레거시 Input.GetKey 금지. `InputAction` 래핑해서 쓴다.
7. **TMP 한글 폰트는 명시 할당 + 아틀라스 sub-asset** — 기본 폰트로 두면 한글 깨짐. Font Asset 명시 할당 필수.
8. **텔레포트는 Kinematic→이동→복원** — Dynamic 상태로 position 변경 시 물리 꼬임. Rigidbody `isKinematic` 토글 패턴 필수.
9. **병렬 에이전트 투입 시 같은 파일 수정 금지** — 새 파일만 쓰게 분리. 끝나면 각자 자체 검수 후 합류.

## 💻 C# 코드

10. **중첩 배열 초기화 시 `new[]` 필수** — `int[][] = { {1,2}, {3,4} }` 는 컴파일 오류. `new[] { new[]{1,2}, ... }` 써야 함.

## 🎨 시각 문제 진단

11. **시각 문제는 에셋(PNG) 먼저 확인** — 색상/형태 이상 → 코드 의심 전에 스프라이트 파일을 `Read`로 열어본다. (Sand.png 사례)
12. **나무 에셋 규칙** — 2×2 금지, 픽셀 고집 금지, 크기는 기획 시트 기준. 임의 리사이즈 금지.

## 🔁 작업 워크플로우

13. **게임 코드 수정은 3단계 강제** — 진단(콘솔+코드+diff) → 최소 수정(1~3줄 우선) → 자체 검수(흐름+엣지케이스). 검수 통과 전 "테스트해보세요" 금지.
14. **AI 생성 기획 비판 검수** — Gemini 제련소 사례. 외부 AI 설계안은 톤/스코프/기존 시스템 정합성 검수 후 반영.

---
**출처 메모리:** `feedback_unity_quest_guard`, `feedback_quest_advance`, `feedback_autowalk_pathfinding`, `feedback_content_not_system`, `feedback_map_redesign`, `feedback_unity_input_system`, `feedback_unity_tmp_font`, `feedback_parallel_agents`, `feedback_csharp_array_init`, `feedback_visual_check_asset_first`, `feedback_tree_rules`, `feedback_game_code_workflow`, `feedback_ai_design_review`
