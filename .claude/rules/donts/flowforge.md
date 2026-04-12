# Don'ts — FlowForge (React / Supabase / React Flow)

> FlowForge 웹앱 작업 시 반복된 실수. 루트 [`donts.md`](../donts.md)의 제너럴 규칙과 함께 적용.
> CAOF 라우팅은 [`../flowforge-caof.md`](../flowforge-caof.md) 참조.

## ⚛️ React Flow

1. **Controlled 모드에서 `rfSetNodes`/`rfSetEdges` 직접 호출 금지** — hook state만 변경해야 동기 유지. React Flow 내부 setter로 건드리면 상태 꼬임 발생.
2. **노드 타입별 Handle(source/target) 유무 확인** — 엣지 만들기 전에 양쪽 노드 모두 해당 Handle 있어야 함. 없으면 엣지 무효.

## 📱 UX

3. **태블릿 터치 환경 고려** — FlowForge는 태블릿에서 쓰이므로 hover 전용 UI 금지. 탭/롱프레스로 동등 기능 제공.

## 🚀 배포 / 디버깅

4. **배포 문제 시 `git status` 먼저** — 캐시/설정 의심하기 전에 commit 안 된 파일, 추적 안 된 파일부터 확인. 대부분 여기서 원인 나옴.
5. **DB 스키마 변경은 Critical 트랙** — 되돌리기 매우 어려움. 풀 GATE(리서치→설계→승인→구현→검수) 필수.
6. **RLS 정책 변경은 Standard 이상** — 보안 영향 = 되돌리기 비용 높음. 반드시 교차 검증.

## 🔧 작업 범위

7. **새 도구가 앱과 데이터 주고받을 때 "앱 코드 수정 불필요" 가정 금지** — 데이터 소비 경로 전체(생성→import→DB→로드→렌더링) 추적 후 트랙 확정. 2026-03-27 교훈.
8. **외부 API로 우회 전에 "Claude가 직접 할 수 있나?" 먼저 판단** — 구독 내에서 되는 작업을 유료/무료 API로 우회하면 복잡도만 늘어남.

## 🛠️ 개발 환경

9. **Claude Code 안에서 `npm run build` 금지** — 리소스 경합으로 세션 불안정. 타입 체크는 `tsc --noEmit`, 실제 빌드는 사용자 터미널.

---
**출처 메모리:** `feedback_reactflow_controlled`, `feedback_flowforge_tablet_ux`, `feedback_deploy_check_git_status`, `feedback_claude_build_ban`
**연관 규칙:** `../flowforge-caof.md` (트랙 판단 / Designer 분석 체크리스트)
