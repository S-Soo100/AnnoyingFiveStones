# Don'ts 실전 검증 로그

> 목적: `.claude/rules/donts.md` 및 기능별 파일이 실제 작업에서 얼마나 작동하는지 누적 추적.
> 운영 기간: 2026-04-11 시작 ~ 2주 후 첫 회고 (2026-04-25)
> 회고 담당: 메인 Claude + 사용자
> 연관: [`../../ideaBank/ai-playbook/harness-engineering-video-shin.md`](../../ideaBank/ai-playbook/harness-engineering-video-shin.md) P0 후속

---

## 기록 방법

작업 종료 시 메인 Claude가 아래 한 줄을 이 파일에 추가한다.

```
YYYY-MM-DD {기능} | 작업: {한줄요약} | 참조: {donts 항목} | 지킴: {번호} | 놓침: {번호+이유} | 재발: {있으면 기록} | 메모: {애매했던 점}
```

**필드 설명:**
- **기능**: 게임 / FlowForge / 이미지 / 마케팅 / 제너럴
- **참조**: 이번 작업에서 의식적으로 읽은 donts 항목 (예: `game#1,3` 또는 `general#2,4`)
- **지킴**: 실제로 지킨 항목
- **놓침**: 위반했거나 잊은 항목 + 이유 (없으면 `-`)
- **재발**: 기존 feedback 메모리에 있거나 과거에 발생했던 실수 패턴이 이번에 또 나왔나? (없으면 `-`)
- **메모**: 룰이 애매했거나, 새로 추가해야 할 패턴 (없으면 생략)

## 🔁 Three-Strike Rule 추적

새 실수 패턴은 아래 단계로 추적한다. (룰 출처: `.claude/rules/donts.md`)

| 상태 | 기준 | 저장 위치 |
|------|------|----------|
| **1차 발생** | 첫 발생 | `~/.claude/projects/.../memory/feedback_*.md` |
| **2차 발생 (승격 후보)** | 같은 패턴 재발 (비슷한 맥락 포함) | 아래 "승격 후보" 섹션 |
| **3차 발생 (정식 룰)** | 2차 발생 후 또 재발 | `donts.md` 또는 `donts/{기능}.md`로 승격 |

**승격 판단은 회고 시점이 아니라 발생 즉시** 진행한다.

### 승격 후보 (Pending Promotion)

<!-- 2차 발생한 패턴을 여기에 누적. 3차 발생 시 정식 룰로 이동하고 여기서 제거. -->

- **Canvas RenderMode 검증 누락** (2회째) — 새 Canvas 도입 시 다른 Canvas와의 RenderMode 우선순위(Overlay > Camera > World)를 sortingOrder 비교 전에 먼저 확인. 사례: Phase 0 ScreenSpaceOverlay+URP Viewport Rect 충돌 / v10 BootCurtain Overlay가 StoryMent WorldSpace를 sortingOrder 무관 가림.
<!-- 2026-06-05: SOT 다운스트림 검증 누락 패턴 3회 누적 → `donts/game.md#18`로 정식 승격 완료. 후속 재발 시 game/#18 위반으로 기록. -->

---

## 로그

<!-- 아래부터 누적 -->

2026-04-11 제너럴 | 작업: donts 체계 구축 + 회고 로그 신설 | 참조: - | 지킴: - | 놓침: - | 메모: 첫 엔트리 (메타 작업)
2026-04-11 게임 | 작업: Windows 창 리사이즈 튕김 버그 수정 (ScreenManager 디바운스) | 참조: general#1,2,3,4 game#13 | 지킴: general#3(콘솔/코드/diff 중 코드 Read 진단), general#2(단일 파일 최소 변경), general#4(자체 검수 후 테스트 요청), game#13(진단→최소수정→자체검수 3단) | 놓침: - | 재발: - | 메모: Standard 트랙 선언 후 원인이 코드 Read 단계에서 명확히 특정되어 Designer 분석 생략. 원인—Update()에서 Screen.SetResolution을 매 프레임 재호출 → Windows DX 스왑체인 재생성 폭주로 크래시. 해결—0.4s 디바운스 + 4px 허용오차.
2026-04-11 게임 | 작업: ESC 일시정지 패널 — 전체화면 버튼 삭제 + 게임 종료가 메인 메뉴로 복귀 | 참조: general#2,4 game#13 | 지킴: general#2(단일 파일 최소 변경, 3줄 수준), general#4(자체 검수 — Close→RestartGame 순서 검증), game#13(흐름 추적: RestartGame 내부 ResetAll/GraveyardUI.Hide/TitleScreen.Show까지 확인) | 놓침: - | 재발: - | 메모: Trivial에 가까운 단일 파일 수정이지만 기존 Quit 동작 변경이라 Standard로 처리. OnReset의 "Close() 먼저 → timeScale 복원 → 코루틴 실행" 패턴을 재사용.
2026-04-11 게임 | 작업: Stage 2 돌 개수 20→15 축소 + yellow 상한 10→8 조정(B안) | 참조: general#2,4 game#13 general#8 | 지킴: general#2(단일 파일 수정), general#4(흐름+엣지케이스 자체 검수), game#13(진단→설계→구현→검수), general#8(사용자 아이디어 맹신 금지 — 수치만 축소하면 난이도 붕괴 위험을 보고하고 B안 대안 제시 후 승인) | 놓침: - | 재발: - | 메모: 사용자가 "기획하고 보고"를 명시하여 구현 전 밸런스 임팩트 분석 수행. A안(최소변경)과 B안(상한 조정)의 비율·난이도 곡선을 표로 비교 제시 → 유저가 B안 채택. 수치 변경 자체는 3~4줄이지만 밸런스 검증이 본 작업의 핵심.
2026-04-12 게임 | 작업: DebugHUD — 빌드에서 연습모드 노출 + 우측 레이아웃 시프트 + 테스트 패널 1버튼으로 간소화 + OnReset IsTestPlay 보존 | 참조: general#2,4 game#13 | 지킴: general#4(자체 검수 중 ResetAll→IsTestPlay 회귀 발견해서 PauseMenuUI까지 동반 수정), game#13(흐름 추적: PauseButton sizeDelta(500,250)가 OnGUI 픽셀 공간에서 실제로 얼마나 차지하는지 계산 후 RightColumnTop 산정) | 놓침: - | 재발: - | 메모: #if DEVELOPMENT_BUILD 컴파일 가드를 런타임 IsTestPlay 게이트로 전환. 그 과정에서 GameSession.ResetAll()이 IsTestPlay=false로 되돌리는 부작용을 발견 → PauseMenuUI.OnReset에서 백업/복원 추가. 레이아웃은 max(260, H*0.38)로 720p/1080p 모두 PauseButton 아래 안전하게 배치. 후속 요청으로 테스트 패널은 "다음 스테이지로(+5살)" 버튼 1개만 남기고 340→38 높이로 축소.
2026-04-24 게임 | 작업: v6-1 전역 낙하 시스템 (Critical) — 공중 판정 → 보드 표면 충돌 판정 전환 + StoneShadow 그림자 + 테이블 기울기 강화 | 참조: general#1,2,3,4,7 game#13 | 지킴: general#3(콘솔+코드+기획서 3단 진단), general#7(기존 catchAreaY 물리 전환 유지 — Designer "Option a 제거" 권고에도 Agent가 "Collision 판정 필수"로 Option b 하이브리드 선택, 이를 사용자에게 투명 보고 후 승인받음), game#13(진단→설계→구현→검수 풀 파이프라인), CAOF Critical 트랙(리서치→Designer→승인→체험시뮬→Coder→검수→테스트 6 GATE 모두 수행) | 놓침: - | 재발: - | 메모: Agent가 Designer 권고를 벗어나 Option b 하이브리드로 구현한 부분은 "투명 보고 후 사용자 승인" 패턴이 잘 작동. 반대로 Agent에 위임 시 독자 판단이 들어갈 수 있다는 사실을 재확인. 6/6 테스트 시나리오 통과. boardSurfaceY를 Cloth Renderer.bounds.max.y에서 런타임 계산하는 방식이 하드코딩보다 안정적.
2026-04-24 게임 | 작업: v6-2 음량 통일성 버그 (Standard) — PauseMenuUI/SettingsPopupUI Open()에 볼륨 재동기화 추가 | 참조: general#2,4 game#13 | 지킴: general#2(두 파일에 동일 3줄 블록만 추가, 최소 변경), general#4(SetValueWithoutNotify 사용해 루프백 이벤트 미발생 확인), game#13(OnEnable vs Open() 중 Open()이 정답 — 슬라이더 초기화 타이밍과 AudioManager 값 읽기 타이밍 분리 확인) | 놓침: - | 재발: - | 메모: 동일한 3줄 수정이 두 파일에 반복됐는데 공통화 유혹을 참았음(general#2 최소 변경 원칙 준수). 3번째 UI에서 또 필요해지면 그때 헬퍼 분리 고려.
2026-04-24 게임 | 작업: v6-3 Stage 7 무게 대비 강화 (Standard) — 배율 0.65/1.0/1.5 → 0.5/1.0/1.8 + 커브 EaseIn/Linear/EaseOut 분리 | 참조: general#2,3,4,7 game#13 | 지킴: general#3(Designer가 "배율만 바꾸면 대비 안 살아남" 근본 원인 정확히 진단 — 셋 다 EaseIn 같은 커브가 진짜 문제), general#7(회색 EaseIn→Linear 변경이 기본 돌 체감에 영향 있음을 사전 보고하고 사용자 승인받음), game#13(구현 전 수학 검증 — 커브별 미분값 2t/1/2(1-t)을 Designer가 명시해 catchAreaY 물리 전환 시 속도 점프 방지), CAOF Standard(Designer→승인→Coder→자체검수→컴파일 확인) | 놓침: - | 재발: - | 메모: **기술적으로 가장 섬세한 변경**. catchAreaY에서 isKinematic 해제 시 `instantSpeed = derivative/dur × distance`인데 derivative를 커브와 무관하게 상수로 두면 전환 순간 속도 점프 발생 → 흰색 깃털감 파괴. Designer가 이 부분을 구현 전에 미분 공식과 함께 경고해준 것이 unity-game-coder의 정확한 구현으로 이어짐. 향후 커브 확장 시(예: 스프링/바운스) 동일 미분 분기 필요 — 이 패턴을 잊지 않도록 HandController.cs의 derivative switch에 주석으로 수학 근거 남겨둠.
2026-05-11 게임 | 작업: Stage 2 시작 분할 (Standard) — OnStageStart 5개(노2빨1초2) + OnScatterComplete 13개(노4빨5초4) 자동 배치 | 참조: general#2,3,4,7 game#13 | 지킴: general#2(단일 파일 ColorSelectGimmick.cs만 수정), general#3(현재 v6 코드 Read 후 흐름 추적 → API 시그니처 확인 후 코더 위임), general#4(컴파일 0 에러 + 흐름 line-by-line + 엣지케이스 점검 후 테스트 요청), general#7(v6 게임플레이/라운드 시스템 보존, 시작 시점만 분할), game#13(진단→설계→구현→자체검수 워크플로우) | 놓침: - | 재발: - | 메모: 직전 v7-1 ScatterSystem 통합 안이 사용자 테스트에서 "이상함"으로 철회된 직후 작업. "복잡하게 하지 말고" 사용자 시그널을 강하게 받아 Designer 분석을 짧게 종료하고 사양 합의 후 바로 unity-game-coder 위임. `GameManager.RefreshStones()` 호출이 활성 돌 변경 후 필수임을 사전에 확인하여 코더 프롬프트에 명시 — 누락 시 ScatterSystem/CatchSystem이 stale 배열을 봐서 무한 디버깅이 됐을 위험. 4×4 그리드+jitter+거리체크 배치, fallback center 패턴은 향후 다른 기믹(추가 돌 스폰)에도 재사용 가능.
2026-05-20 게임 | 작업: 빌드 마젠타 헛다리 후 롤백 + donts 승격 (메타) | 참조: general#1,3 game#13 | 지킴: general#1(롤백 후 Player.log Read로 사실 확인 — 셰이더 에러 0건), general#3(이전 세션 진단 무근거였음을 빌드 로그로 검증) | 놓침: **general#3(진단 없는 수정 금지)** — 이전 세션에서 Player.log 한 번도 안 본 채 "Cloth/Table/Sky 마젠타" 가설 만들어 12개 파일(머테리얼 8개 + 스크립트 2개 + URP Strip 설정 + 신규 템플릿) 변경 → 전부 롤백. **general#2(최소 변경)** 도 동시 위반. | 재발: 있음 — `feedback_gate_system`(P0 GATE 위반)과 동질. 가설부터 만들고 대형 변경 진입하는 패턴이 빌드 도메인에서 재발. | 메모: 사용자 명시 요청으로 1회만에 donts/game.md#15-17로 정식 승격(빌드 진단 Player.log 우선, URP Strip 끄지 말 것, 사용자 "빌드해도 돼?"는 예방 점검). `feedback_build_diagnosis_log_first.md` 메모리 신설. **핵심 교훈: "빌드해도 돼? 이미지 문제 없어?"는 예방 점검 질문이지 사고 보고가 아니다 — 사용자 질문 의도 구분 필수.**
2026-05-28 게임 | 작업: v10 ScatterSystem anisotropic 보정 (Standard) — max gauge 흩뿌리기에서 돌이 책상 뒤로 직선 분사되어 SafeZone 즉시 초과 → X 우세 anisotropic 계수로 보드 사다리꼴 perspective 정합 (앞폭 16.1 × 깊이 3.15 ≈ 5:1) | 참조: general#1,2,3,4,6 game#13 game#17(빌드 예방 점검) | 지킴: general#1(돌·보드·SafeZone 수치를 코드/로그로 직접 확인 후 단정), general#2(L255-257 3줄 anisotropic 계수 교체 외 무수정 — 코더 자체 검수 line-by-line 보존 확인), general#3(콘솔 로그에서 power 직렬화값 min≈0.4/max≈4.86 역산 → mass=0.1·damping=3 증폭 J×3.33 계산 후 변위 목표 도출), general#4(GATE 3.5 체험 시뮬레이션 — 살짝/중간/세게 3단계 유저 경험 사전 검증 후 코더 위임), general#6(Gemini 교차 리뷰 Approved — 🔴0/🟡1/🟢4), game#13(진단→최소수정→자체검수+교차검수+컴파일 통과까지 3단계 강제), game#17("잘 되는거 같아 빌드해볼까?"는 예방 점검 — 가설 만들지 말고 콘솔/git/로그 상태만 확인) | 놓침: - | 재발: - | 메모: 오케스트레이터-직접 수치 검증(에이전트 위임 금지) 룰이 정확히 작동. designer가 mass×damping 증폭 J×3.33을 식으로 명시 → orchestrator가 console 로그에서 power 직렬화값 역산 → 변위 목표(X±5.5 / Y±1.7)와 계수(X 1→0.34 / Y 0.6→0.105 / offset 0.5→0.12) 산출 → coder는 수치만 그대로 적용. 사용자가 "장외 실패 유지(열받는 패널티)" 선택해서 clamp 추가 안 함 — 의도된 fail mechanic 보존. dropHeightAdd>0 분기에서 X만 1.0 유지(Stage 5 30살 클러스터 악화 방지)한 비대칭 처리가 핵심 디테일.
2026-05-29 게임 | 작업: v11-fix3 SkyFloorY 단일 SOT 통합 (Critical) — Stage 1 "보드 위에 손 둬도 받기 모드 안 됨" + Stage 2 "사다리꼴 위에서 손이 보드 통과처럼 보임" 데드존 제거. SkyFloorY -2.45→-3.95(사다리꼴 뒷변) + CatchSystem.boardSurfaceY = SkyFloorY 단일 SOT + 모든 SetQuadOverride/SetOverride/ClearOverride 진입점이 SkyFloorY 자동 동기화. | 참조: general#1,2,3,4,6,7 game#13 CAOF Critical | 지킴: general#1(BoardDebugLines.cs 시각화 + 콘솔 로그로 SkyFloorY/BoardSurfaceY/MatRect.yMax 실측 후 단정), general#3(콘솔 로그+코드 흐름+Designer 분석 3단 진단 — "HasQuad=False"는 BoardDebugLines.Start가 StartStage 이전에 1회 실행한 stale log임을 Read로 검증), general#4(자체 검수 후 Gemini Polish + Codex Hold 교차 리뷰까지 통과), general#6(Gemini ↔ Codex 교차 리뷰 — Gemini가 quad 외 경로 RecalculateBoardSurface 트리거 누락 발견, Codex가 ClearOverride/SetOverride(Rect) SkyFloorY 갱신 누락 + Stage 5 catch window 1.5 unit 확장 발견), general#7(Stage 5 catch window 확장은 의도된 결과로 보존, Stage 2 사다리꼴 동작은 fix3 완료 후 별도 점검 보류), game#13(3회 실패 후 진단 도구 BoardDebugLines.cs 신설 → 시각화로 근본 원인 특정 → A-lite 최소 패치) | 놓침: - | 재발: v11-fix1/fix2 2회 패치 실패 후 3회 실패 룰에 진입 — 동일 패턴(SkyFloorY 하드코딩 미스매치)을 다른 접근(boardSurfaceY 조정)으로 우회 시도하다 근본 원인 못 잡음. **3회 실패 시 진단 도구 전환**이 효과적으로 작동. | 메모: v11-fix4 추가 패치 — 5단 첫 손등 받기 catch window 상한 catchAreaY+0.5f→+0.2f 축소(Trivial 1줄). 손 transform.y=2.0(palm 시각 중심) vs 기존 catch upper bound 2.5 → 0.5 unit≈50px 갭으로 유저가 "손등 위 공중에서 받힘" 인지. designer 분석 + Read로 L912/L1058 둘 다 확인(L1058은 거리 기반이라 동일 패턴 없음 → 미수정). Y창 0.3 unit 축소(4.6%)이나 X-radius가 지배적이라 난이도 영향 미미. **교훈: "20-30px 위" 같은 유저 체감 보고는 좌표 unit(0.2~0.3)으로 즉시 환산해 catch window 수치와 매핑하면 1줄로 해결.**
2026-05-29 게임 | 작업: v11-fix4 진단 오류 인정 + 즉시 롤백 + v11-fix5 (옵션 C) 손 위치 기반 재설계 (Standard) — "+0.2f 축소"가 도리어 악화(빨간 선 보고). 진짜 원인: catch 직후 SetParent+localPosition 텔레포트(L924-925) + catch 조건이 절대 y 기준이라 손이 어디 있든 돌 y≤2.5 도달 시 잡힘 → 손 위로 즉시 점프. catch upper 축소는 발동 시점만 늦춰 텔레포트 거리 증가(역효과). 옵션 C: catch 조건을 palmTopY = transform.position.y + 0.4*localScale.y 기반(backhand 2x scale 자동 보정). | 참조: general#1,2,3,4 game#13 game#14 CAOF Standard | 지킴: general#1(LateUpdate L571-602 직접 Read로 손이 마우스 추종 자유 이동 확인 → 텔레포트 거리 가설 검증), general#3(스크린샷 빨간 선 위치를 catch 발동 y와 매칭 시도 → 모순 → "빨간 선=catch 후 손 위치" 가설 도출 → LateUpdate Read로 확정), general#4(designer 시나리오 A/B/C + 직접 엣지케이스 표 추적 후 사용자 테스트 요청), game#13(진단→롤백→재설계→자체검수 4단), game#14("CAOF 트랙 선언 후 반드시 실행" — Standard 트랙 fix4 분석을 너무 가볍게 잡았던 점 인정, fix5는 designer 정식 위임) | 놓침: general#3(fix4 시 진단 부족) — 스크린샷 "20-30px 위" 보고를 catch 조건 수치로 직역하다 LateUpdate 손 자유 이동 + 텔레포트 메커니즘을 놓침. **재발 위험**: v9 "직접 코딩 금지" 예외와 동일 패턴(분석 단순화로 시스템 상호작용 누락). | 재발: 있음 — 단순 수치 조정이 시스템 상호작용(LateUpdate 손 자유 이동 + catch 텔레포트) 무시. `feedback_verify_assumptions`(Phase C 3연속 실패 교훈)과 동질. **다음 캐치 조정 시 LateUpdate 거동 + SetParent 영향 사전 검토 필수**. | 메모: v11-fix5 변경 코드 — `float palmTopY = transform.position.y + 0.4f * transform.localScale.y; if (y <= palmTopY + 0.3f && y >= palmTopY - 0.5f && y >= BoardBounds.SkyFloorY)`. catch window 폭 0.8 unit(약 7프레임)이라 검출 충분. 텔레포트 자체(L924-925)는 손대지 않음 — catch 발동 시점이 손 위로 강제되어 텔레포트 거리 자연 단축. 사이드 이펙트 confirmed: DoStage5FistGrab(L1100 거리기반)·1-4단 catch(L515 물리전환)·throw catch는 영향 없음. **핵심 교훈: "이상한 위치에서 catch"가 보고되면 catch 조건 수치 조정 전 ① LateUpdate에서 손이 어떻게 움직이는지 ② catch 직후 부착 위치(SetParent + localPosition)가 어디인지 두 가지를 반드시 Read로 확인.**

---

## 🧪 드라이런 — 전 룰 커버리지 테스트 (2026-04-11)

실제 작업 전에 **가상 시나리오 7개**로 51개 룰을 전수 커버. 목적: 룰이 실전 맥락에서 말이 되는지 검증.

### 시나리오 A — 게임: 퀘스트 전환 시 보상 2회 지급 버그
> 사용자 보고: "수렵 퀘스트 끝내면 토끼 고기가 2번 들어와"

**적용 donts:**
- `general#1` 기억 단정 금지 → 먼저 콘솔 로그로 재현
- `general#3` 진단 3단 → 로그 + QuestManager 코드 추적 + git diff
- `game#1` isCompleting 가드 누락 여부 체크
- `game#2` CurrentQuest 전환 시 클리어 순서 체크
- `game#13` 3단계 워크플로우 (진단→최소수정→자체검수)
- `general#2` 최소 변경 (1~3줄로 해결 가능?)
- `general#4` 자체 검수 전 "테스트해보세요" 금지

**예상 로그 한 줄:**
`2026-04-11 게임 | 작업: 퀘스트 보상 2회 지급 버그 수정 | 참조: game#1,2,13 general#1,2,3,4 | 지킴: (실전) | 놓침: (실전) | 메모: -`

**드라이런 발견:** ✅ 모든 항목 자연스럽게 해당. 문구 명확.

---

### 시나리오 B — 게임: 밤에 주민이 집으로 걸어가는 NPC 추가
> 요청: "마을 주민이 밤 되면 알아서 집으로 돌아가게"

**적용 donts:**
- `game#3` AutoWalk는 장애물 없는 경로만 → 마을 건물 사이 동선이라 **AutoWalk 부적합**, A* 필요
- `game#5` 맵 재설계 시 기존 구조 보존 (건물 배치 유지)
- `game#11` 시각 문제는 에셋 먼저 (주민 스프라이트 방향 확인)
- `general#7` 기존 구조 보존 기본값

**드라이런 발견:** ✅ game#3이 정확히 이 상황을 거르는 역할. 핵심 룰임이 검증됨.

---

### 시나리오 C — 게임+이미지 크로스: 새 퀘스트 UI + 보상 스프라이트
> 요청: "신규 퀘스트 '열매 수집' — 한글 제목 UI + 나무 열매 아이템 스프라이트"

**적용 donts:**
- `game#7` TMP 한글 폰트 명시 할당 + 아틀라스 sub-asset
- `game#8` 퀘스트 시작 시 플레이어 텔레포트면 Kinematic→이동→복원
- `game#12` 나무 에셋 규칙 (2×2 금지, 픽셀 고집 금지, 크기 기획 시트 기준)
- `images#1` Gemini 이미지 명시 요청 없으면 OpenAI 사용
- `images#2` API 역할 분담
- `images#3` 사전 승인 필수 (제안서 → 승인 → 생성)
- `images#7` 스프라이트 2×2 그리드 방식
- `images#9` 덮어쓰기 금지 (`berry_v1.png` → 재생성 시 `berry_v2.png`)

**드라이런 발견:** ✅ 크로스 도메인에서도 두 파일 병행 참조 자연스러움.

---

### 시나리오 D — 게임: 밤 콘텐츠 부족 + 입력 키 + 드롭 테이블 확장
> 사용자 피드백: "밤에 할 게 없네. 숙면 시스템 만들까?"

**적용 donts:**
- `game#4` 빈 시간은 시스템이 아닌 콘텐츠로 → **숙면 시스템 추가 반대**, 이벤트/대사/랜덤 조우로 채움
- `game#6` New Input System (레거시 Input.GetKey 금지) → 새 키 바인딩은 `InputAction` 래핑
- `game#10` C# 중첩 배열 초기화 `new[]` 필수 → 드롭 테이블 리터럴
- `general#8` AI 생성 기획 맹신 금지 (Gemini에게 숙면 시스템 설계 돌렸다고 가정)

**드라이런 발견:** ✅ game#4가 없으면 "숙면 시스템"을 그냥 만들 위험. 핵심 철학 룰.

---

### 시나리오 E — FlowForge: 새 이벤트 노드 타입 + DB 스키마 추가
> 요청: "시나리오 분기 기록용 '결정' 노드 추가, DB에 테이블 하나 늘림"

**적용 donts:**
- `flowforge#5` DB 스키마 변경은 Critical 트랙 → 풀 GATE
- `flowforge#7` 데이터 소비 경로 전체 추적 (생성→import→DB→로드→렌더링)
- `flowforge#2` 노드 Handle(source/target) 유무 확인
- `flowforge#1` React Flow Controlled 모드에서 rfSetNodes 금지, hook state만
- `flowforge#3` 태블릿 터치 환경 (hover 전용 UI 금지)
- `flowforge#9` / `general#9` Claude Code 안에서 `npm run build` 금지 → `tsc --noEmit`
- `general#10` 파괴적 git 금지 (마이그레이션 롤백 시)

**드라이런 발견:** ✅ flowforge#7이 트랙 판단의 핵심 방어선. 룰 없으면 "앱 코드 수정 불필요" 가정하고 넘어갈 위험.

---

### 시나리오 F — FlowForge 배포 디버깅 + 외부 API 결정
> 사용자: "Vercel 배포했는데 빈 페이지 나옴. OpenAI로 자동 요약 기능 넣을까?"

**적용 donts:**
- `flowforge#4` 배포 문제 시 `git status` 먼저 (캐시/설정 의심 전에)
- `flowforge#6` RLS 정책 변경 의심 시 Standard 이상 (보안 영향)
- `flowforge#8` 외부 API 우회 전 "Claude가 직접 할 수 있나?" 먼저 → 요약은 사용자 세션에서 Claude로 충분, OpenAI API 불필요
- `general#3` 진단 3단 (추측 금지)

**드라이런 발견:** ✅ flowforge#8이 "무조건 API 부르기" 반사를 차단. 핵심 룰.

---

### 시나리오 G — 이미지: 캐릭터 액션 포즈 + favicon 제작 + 품질 검수
> 요청: "주인공 설정화로 전투 포즈 3종 생성하고, 웹사이트 favicon도 만들어줘. 기존 스프라이트 품질도 한번 봐줘"

**적용 donts:**
- `images#3` 사전 승인 (제안서 → "ㅇㅇ" → 생성)
- `images#4` 예외: "바로 만들어" 명시되면 승인 생략 가능
- `images#5` 캐릭터 설정화 있으면 참조 필수
- `images#6` 캐릭터 ref 1장만 + 도구(무기)는 텍스트로
- `images#8` 직선 아티팩트/실루엣 뭉개짐 → `art-reviewer` 품질 체크
- `images#10` 투명 배경 `-alpha on` + `PNG32:` 필수
- `images#11` favicon은 `.ico` 대신 `.png`
- `images#12` SOT(OpenAI) → 포즈(Gemini ref 1장) 파이프라인

**드라이런 발견:** ⚠️ `images#8` "직선 아티팩트" 문구가 다소 모호 — 신규 작업자(나 포함)가 "어떤 경우가 직선 아티팩트인지" 재확인 필요. **메모: art-reviewer 보고서 예시 링크 추가 검토.**

---

### 시나리오 H — 제너럴: 대규모 리팩토링 PR 리뷰 + 마케팅 발행
> 요청: "이 PR(15 파일 변경) 리뷰해줘. 그리고 블로그 글을 LinkedIn에 올리려고 해"

**적용 donts:**
- `general#5` 대규모 변경 통째 읽기 금지 → `git diff --stat` → 그룹핑 → 순차
- `general#6` 같은 모델 자기 리뷰 금지 → Gemini + Claude 교차
- `general#11` 병렬 에이전트 경계 (새 파일만, 같은 파일 수정 금지)
- `general#12` Claude.ai 구독 모드 (API key 전환 금지)
- `marketing#1` LinkedIn 한글 URL → bit.ly 단축
- `marketing#2` 사업계획서 폴더 분리 (블로그 글과 섞지 말 것)
- `marketing#3` 편집장 → 콘텐츠 어댑터 → 브랜드 매니저 순서
- `marketing#4` 브랜드 매니저 검수 필수

**드라이런 발견:** ✅ 전체 커버. marketing 4개 전수 적용.

---

## 📊 드라이런 집계

### 커버리지
| 파일 | 총 룰 | 커버된 룰 | 미커버 |
|------|------|---------|-------|
| `donts.md` (general) | 12 | 12 | 0 |
| `donts/game.md` | 14 | 14 | 0 |
| `donts/images.md` | 12 | 12 | 0 |
| `donts/flowforge.md` | 9 | 9 | 0 |
| `donts/marketing.md` | 4 | 4 | 0 |
| **합계** | **51** | **51** | **0** |

### 발견된 개선 포인트
1. ✅ **`images#8`** — 2026-04-11 보강 완료. 4대 불량 패턴(직선 아티팩트 / 실루엣 뭉개짐 / 프레임 간 불일치 / 업스케일 번짐) 명시.
2. ✅ **`game#3,4`** — AutoWalk와 "빈 시간 콘텐츠 원칙"은 각각 기술/철학 방어선으로 핵심 역할 검증됨.
3. ✅ **`flowforge#7,8`** — "데이터 소비 경로 추적"과 "외부 API 전 Claude 먼저"가 트랙 판단 오류를 막는 핵심.
4. 💡 **시나리오 H처럼 크로스 도메인**이 많이 나옴 — 실전 로그에서 `general#` + `{기능}#`을 함께 쓰는 게 기본 패턴이 될 듯.

### 결론
- 51개 룰 모두 **현실적 시나리오에 자연스럽게 해당** → 죽은 룰 없음.
- 문구 모호한 항목 1개(`images#8`) 발견 — 2주 회고 전에 보강 여부 판단.
- 실전 로그 포맷은 작동함. 한 줄로 요약 가능.


---

## 2주 회고 체크리스트 (2026-04-25 예정)

회고 시 아래를 집계한다:

### 1. 빈도 분석
- [ ] **자주 놓친 룰 TOP 3** — Hook으로 강제화 후보
- [ ] **한 번도 안 걸린 룰** — 죽은 룰, 삭제 또는 통합 후보
- [ ] **"애매" 표시 누적된 룰** — 문구 재작성 후보

### 2. 커버리지 분석
- [ ] 신규 작업 중 `donts/{기능}.md`가 없는 영역이 있었나? → 새 파일 필요
- [ ] 기능별 파일 간 중복 룰이 있나? → 루트 `donts.md`로 승격

### 3. 신규 패턴 발견
- [ ] 로그 "메모" 필드에서 누적된 새 실수 패턴
- [ ] 기존 feedback_*.md 메모리 중 아직 룰화 안 된 것 (승격 후보)

### 4. Hook 승격 판단
- [ ] 자주 놓치는 룰 중 자동 검증 가능한 것 → Stop hook에 `/검수` 트리거 조건 추가

---

## 회고 결과 섹션 (회고 후 누적)

<!-- 2026-04-25 첫 회고 결과 여기에 -->
2026-05-28 게임 | 작업: v10 빌드 후속 통합 정리 (Standard ×3 hot-fix 라운드) — (1) BoardBoundsDebugDrawer 빨간 사다리꼴 제거 + GaugeBarUI X -3.2→-7.0 좌측 이동, (2) BootCurtain 신설(BeforeSceneLoad+DontDestroyOnLoad+풀스크린 Overlay 검은 막) + activeSelf 가드로 StartStage 9곳 호출 회귀 차단 + Time.unscaledDeltaTime/SmoothStep 곡선 + Raise(duration) 메서드 분리, (3) StoryMentUI RenderMode WorldSpace→ScreenSpaceOverlay + sortingOrder 110→220 (BootCurtain 위) + 풀스크린 anchor 변환, (4) "Catch Five Stones" 인트로 splash + Show(...) 시그니처 showTitleSplash 추가 + DoSplashThenTyping 코루틴 신설 + mentText.text=""/maxVisibleCharacters=0 잔상 차단 | 참조: general#1,2,3,4,6,7 game#6,13 game#17 | 지킴: general#1(코드 Read로 Canvas RenderMode·sortingOrder 확인 후 단정), general#2(라운드별 최소 변경 — 1파일/4파일/3파일 hot-fix), general#3(증상→Canvas 모드 충돌 정확 진단→line-by-line 추적), general#4(컴파일 0 errors + 흐름 추적 + 엣지케이스 점검 후 테스트 요청), general#6(BootCurtain은 Gemini 교차 리뷰 Approved 🔴0/🟡1/🟢4 → 🟡 unscaledDeltaTime 적용), general#7(StoryMent 자식 UI anchor 비율 기반 보존 + RaiseInstant 호환성 wrapper 유지로 시각/API 회귀 최소화), game#13(진단→최소수정→자체검수 3단), game#17("잘 되는거 같아 빌드해볼까?" 예방 점검 정상 응대 — 가설 안 만들고 콘솔/로그만 확인) | 놓침: **라운드 2 발생 — 초기 BootCurtain 계획서에 sortingOrder만 비교(200 vs 110)하고 RenderMode 충돌(Overlay는 sortingOrder 무관하게 WorldSpace 위) 누락**. general#3 진단 단계에서 두 Canvas의 renderMode를 모두 확인했어야 함. 사용자 보고("스토리먼트 안 나옴")로 발견 후 정정. | 재발: 있음 — Phase 0의 "URP Viewport+ScreenSpaceOverlay 충돌" 교훈("새 Canvas 렌더 모드 도입 시 호환성 먼저 리서치")이 이번에도 적용됐어야 함. Canvas 레이어 가정 실수가 2회째 → 승격 후보 추가. | 메모: BootCurtain activeSelf 가드는 Gemini Critical 진단 전에 orchestrator가 사전 발견 — StartStage 9곳 호출(Stage 1→2 자동/AllClear 재시작/Pause 재시작 등)을 Grep으로 확인 후 미리 가드 추가. Designer↔Coder↔교차리뷰 패턴 + orchestrator 직접 Canvas 분석이 결합한 사례. **새 패턴**: TMP 풀텍스트 잔상은 ForceMeshUpdate 후 maxVisibleCharacters 적용 순서가 불안정 → text 세팅 전에 maxVisibleCharacters=0 먼저(DoTyping 라인 순서 정리). RaiseInstant→Raise(0.15s) 메서드명 의미 일관성 위해 호출처도 동시 변경.
2026-05-29 게임 | 작업: v11-fix3 시각/판정 SOT 통합 (Critical, **3회 실패 후 진단툴 전환으로 진짜 원인 적출**) — v11-fix2 후 사용자 "보드 메시 상단에 손을 둬도 받기 안 됨" 3차 실패 → CLAUDE.md "3회 반복 실패 규칙" 발동 → **추가 패치 중단 + BoardDebugLines.cs 진단 시각화 도구 신설** (GL.Lines로 SkyFloorY/boardSurfaceY/catchAreaY/quad 4개 라인 + OnGUI 라벨 + Console 진단 로그). 사용자 콘솔 캡처 `[BoardDebug] SkyFloorY=-2.45 \| BoardSurfaceY=-2.35 \| HasQuad=False` 확보 → Designer가 stale log 분석(BoardDebugLines.Start는 StartStage 전 실행, 실제 플레이 단계는 HasQuad=True) → **단일 원인 적출**: `BoardBounds.SkyFloorY=-2.45f` 하드코딩이 시각적 사다리꼴 윗변(-3.95)과 1.5 unit 어긋남. 사용자가 본 "한참 위" = 매트 AABB(-2.35)와 사다리꼴(-3.95) 사이 시각적 갭. **A-lite 자동 동기화 패치**: (1) `BoardBounds.SetQuadOverride`에서 `SkyFloorY = Mathf.Max(quad[0].y, quad[1].y)` 자동 동기화, (2) `CatchSystem.CalculateBoardSurfaceY` 맨 앞 HasQuad 분기 → `boardSurfaceY = SkyFloorY`, (3) `BackgroundManager.ApplyBoardOverride` 메서드 끝 단일 `RecalculateBoardSurface()` 트리거 (Gemini 보강 반영 — quad/Rect/Clear 3경로 공통). 3파일 ~10줄 변경. | 참조: general#1,2,3,4,6 game#13 CAOF Critical | 지킴: general#1(BoardDebug 콘솔 진단 후 코드 단정), general#2(3파일 최소 변경 — 자동 동기화로 호출처 0곳 변경), general#3(추측 금지 → 시각화 도구로 사용자가 직접 진단 데이터 제공), general#4(자체 검수 + Gemini Approved with Minor Polish + 보강 반영 후 테스트 요청), general#6(Gemini 교차 리뷰 1건 누락 지적 → 즉시 반영), game#13(GATE 1-6 풀 사이클 재실행 — fix2 후 fix3로 또 한 번), CAOF Critical(3회 실패 후 접근 전환 = CLAUDE.md "3회 반복 실패 규칙" 정상 발동). | 놓침: **3회 연속 잘못된 가정** — v11/fix1/fix2 모두 "현재 SkyFloorY 수치가 시각적 사다리꼴과 일치한다"는 미검증 가정 유지. fix1(-3.95)에서 boardSurfaceY=-2.45와 데드존 → fix2(-2.45)로 boardSurfaceY와 정렬했지만 이번엔 시각(-3.95)과 1.5 어긋남. **시각=판정 일치 검증 누락**이 매번 다음 사이클 실패 유발. 시각화 도구를 v11 출발 시점에 만들었어야 함(현재는 fix2 실패 후에야 만듦). | 재발: 있음 — `feedback_verify_assumptions.md`("구현 전 가정 검증: 좌표/크기/물리 부작용") **3회째 반복 위반**. 1회·2회는 다른 부작용 가정 검증 누락이었으나 이번엔 **임계값(magic number)과 시각적 좌표의 일치 검증**이라는 새 패턴. → **승격 후보**: "여러 시스템이 공유하는 임계값을 도입/변경할 때, 시각적 좌표(또는 다른 시스템 좌표)와의 일치를 디버그 라인/콘솔 로그로 *반드시* 시각화 검증." game/#18 후보. | 메모: **3회 실패 → 진단툴 전환의 위력** — 추측 패치 3회보다 디버그 시각화 1회 + 콘솔 로그 1줄이 진짜 원인 즉시 적출. CLAUDE.md "3회 반복 실패 규칙"의 "최소 재현 테스트 코드 작성으로 가설 검증"이 정확히 작동. **자동 동기화 패턴**의 가치: 매직 넘버를 호출처에서 hardcode하지 말고 SOT에서 자동 계산(SetQuadOverride 안에서 SkyFloorY 갱신). 한 군데 바뀌면 자동 정렬 → 미래 재발 차단. **Gemini 교차 리뷰의 가치**: Approved와 동시에 누락 1건(Rect/Clear 경로) 지적 → 사용자 테스트 전에 즉시 보강. **stale log 함정**: 진단 도구 자체도 라이프사이클 검증 필요 — BoardDebugLines.Start가 StartStage 이전이라 HasQuad=False로 찍혀 처음 가설을 흐림. 디버그 도구도 stage 전환 시점에 재로깅하도록 후속 보강 필요. **Stage 2 모순**: 메모리(`project_stage2_trapezoid_quad.md`)는 "Stage 2만 사다리꼴"인데 StageConfig.cs는 1~10단 동일 quad → 별도 후속 점검 항목.

2026-06-05 게임 | 작업: v12 통합 사이클 (Critical 본체 + Standard ×4 fix) — 5개 결정사항(로비 BGM/책상 swap/Flee quad/Monochrome 사다리꼴/60살 종료) 구현 + 사용자 테스트 후 4건 fix(DebugCompleteCurrentLoop 재진입 가드 / AudioManager 콜드부팅 큐잉 / **Stage 3 Flee +Y 탈출 즉시 낙** / **Stage 10 Monochrome 손바닥 픽업 충돌**). Fix #3,#4가 모두 좌표/영역 회귀 패턴 → BoardBounds.IsOutsideMatStrict 분리(SkyFloor 예외 제거) + MonochromeGimmick.PlaceAdditionalStones 최소거리 1.2 + 30회 재시도 + fallback. | 참조: general#1,2,3,4,6,7 game#13 CAOF Critical/Standard | 지킴: general#1(BoardBounds.IsOutsideMat SkyFloor 예외 + Stage 10 quad 좌표 + 손바닥 Bounds 1.0×0.8 모두 Read로 확인 후 단정), general#2(BoardBounds 새 helper + Strict 메서드 추가 + FleeMovement 2줄 호출 교체 + MonochromeGimmick 본문 40→73줄 — 호출처 0곳 변경), general#3(콘솔 로그+코드 흐름+Designer 분석 3단 — 사용자 "알고 있었으며 그대로 했는데 실패" 보고를 ValidatePick 표면 해석으로 끝내지 않고 손바닥 Bounds×다중 돌 겹침 메커니즘까지 line-by-line 추적), general#4(자체 검수 + Read 직접 검증 + recompile 0 errors + 콘솔 경고 5건 빈도 자가 보고 후 테스트 요청), general#6(/검수 code 4모델 교차), general#7(Stage 10 사다리꼴 좁힘은 사용자 명시 기획 — 보존하고 회피 알고리즘만 추가), game#13(GATE 1-6 풀 사이클 + 버그 fix 라운드마다 designer→coder 재실행), CAOF(designer→coder 파이프라인 엄수, 직접 코딩 금지) | 놓침: **v12 좌표/영역 변경 다운스트림 검증 누락 2건 누적** — Stage 10 quad 좁힘 결정 시 손바닥 픽업 충돌 가능성 사전 검토 없음, IsOutsideMat SkyFloor 예외가 OnBoard 돌(Flee)에도 부작용 일으킨다는 점 사전 분석 없음. GATE 4 "구현 전 수치 검증" 체크포인트가 *영역/SOT 변경*에서는 효과 발휘 못함(현 체크리스트는 좌표/크기/물리 부작용 3개만 명시). | 재발: 있음 — **3회째 도달**. `feedback_verify_assumptions.md` 패턴이 v9 Stage 2 quad → v11-fix3 SkyFloorY → v12 두 fix까지 누적. 위 "승격 후보" 섹션에서 game/#18 후보로 표기 중. **다음 작업 시작 전 정식 룰 승격 합의 단계**. | 메모: **v12 누적 교훈**: 좌표/영역 SOT 변경은 단일 시스템 수정으로 끝나지 않음 — BoardBounds 변경은 모든 소비자(CatchSystem/HandController/Flee/Monochrome 추가 돌 배치 등) 전수 검토 필요. 사용자 "테스트 1,2,3 성공" 후 placed warning 5/20 빈도까지 콘솔로 자가 확인 → 게임플레이 영향 없음 확인 + v13 튜닝 후보로 보고. `lastCandidate` fallback 패턴 + 자가 회피(placedPositions에 새 좌표도 add)는 향후 다른 다수 배치 시스템에 재사용 가능. **Three-Strike Rule 정상 발동** — 다음 좌표/SOT 변경 작업 직전에 game/#18 정식 룰화 사용자 합의 필요.

2026-05-29 게임 | 작업: v11-fix2 받기 영역 재정의 (Critical, 2회차 진단 후 성공) — v11(SkyFloorY=-3.3) → v11-fix(-3.95) → **v11-fix2(-2.45)** 3사이클. 핵심 깨달음: SkyFloorY(받기 모드 토글 + outside 면제)와 CatchSystem.boardSurfaceY(낙 판정선, =-2.45) 정렬 안 하면 그 사이 데드존 발생 → 시각적 "하늘"인데 잡기 실패. 1단/5단/그림자 3개 버그 동시 해결: (1) BoardBounds.SkyFloorY -3.95→-2.45, (2) HandController DoStage5Catch catch window 하한·handRaised 모두 BoardBounds.SkyFloorY 참조 통일 (catchAreaY-0.8/catchAreaY-1.0 → SkyFloorY), (3) StoneShadow.LateUpdate 매 프레임 ComputeShadowY(stoneX) — BoardBounds.QuadPoint(u, 0.5) perspective 매핑 (좌우 대칭이라 결과는 일정 -5.525지만 API 활용) | 참조: general#1,2,3,4,6 game#13 | 지킴: general#1(BoardBounds·HandController·StoneShadow·CatchSystem 코드 Read로 정확한 boardSurfaceY=-2.45 수치 확인 후 단정), general#2(3파일 ~40줄 최소 변경, CatchSystem·boardMin/boardMax·LateUpdate·DoCatchLoop·DoStage5FistGrab 모두 보존), general#3(콘솔 로그 부재 상황에서 코드 line-by-line 추적 + Designer 재분석으로 1차 진단(5.95 unit 갭) 오류 정정 → 진짜 원인(boardSurfaceY race 데드존 1.5 unit) 도출), general#4(흐름 추적 + race condition 분석 + 엣지케이스 5개 점검 후 테스트 요청), general#6(Gemini 클린 Approved — race·확장 윈도우·IsOutsideMat·그림자 4개 항목 모두 OK), game#13(GATE 1-6 풀 사이클: 진단→Designer→사용자 합의 3옵션→체험 시뮬레이션→Coder 위임→자체+교차 검수→테스트 요청), CAOF Critical(사용자 "치명적 결함, 직업 잃을 위기" 명시 → 풀 GATE 강제 + 즉시 코딩 금지) | 놓침: **v11-fix 1차 진단 오류** — 처음에 "5.95 unit 갭(SkyFloorY -3.95 vs catchAreaY 2)"으로 진단했으나 Designer가 더 깊게 추적해 실제 데드존은 1.5 unit(SkyFloorY=-3.95 vs boardSurfaceY=-2.45)임을 정정. 코드 흐름 끝까지 추적 안 한 결과(`feedback_read_before_answer` 위반). | 재발: 있음 — `feedback_read_before_answer` 메모리에 명시된 "Grep만으로 동작 판단 금지, Read로 흐름 끝까지"가 v11-fix 1차 진단에서 부분 위반. CatchSystem.Update L106 fall-detection 로직을 처음에 무게 안 두고 catchAreaY만 봄. Designer가 정정. → 후속 룰 강화 후보: "여러 시스템이 같은 Y 임계값을 다르게 정의하면 race/데드존 의심 1순위" | 메모: **v11 사이클 핵심 패턴**: 단일 매직 넘버(SkyFloorY)가 3개 시스템에 걸쳐있을 때 그 값을 옮기면 한 시스템(받기 모드 토글)은 OK여도 다른 시스템(낙 판정 race)에서 데드존 발생. 해결 = "한 단어, 한 의미" 원칙으로 SkyFloorY를 boardSurfaceY와 정렬. 5단 catch window를 SkyFloorY 기반으로 통일한 것도 같은 원칙. **2회차 진단의 가치**: v11-fix 시점 Gemini 리뷰는 통과했지만(테스트 전), 실제 사용자 테스트 후 Designer 재분석에서 데드존 원인 발견 → 1회 추가 사이클로 정확한 해결. CAOF "버그 수정은 designer→coder" 룰이 2회차에서 더 빛남.

2026-07-18 게임 | 작업: v13 오디오·튜토리얼·스킵 정비 + 준비하세요 제거 (Standard/Critical 혼합, 다중 요청 배치) — (1) 오디오: 마스터 볼륨(음량)+AnnoyingSlider(0→50% 자동복귀) 제거 → BGM/효과음만, 기본 50%, "음악"→"BGM", 일회성 PlayerPrefs 마이그레이션(저장 0% 무음 복구, /code-review 후 조건부화 GetFloat<=0만), AudioListener.volume=1f 고정. plist 실측(BGMVolume=0/sfx_volume=0)으로 무음 근본원인 적출 — 초기 "마스터=0" 가설을 스크린샷+plist로 정정. (2) 튜토리얼: 시작 3슬라이드 인트로(TutorialUI) 260703 문구 교체 — 인게임 guide.* 자막은 이미 반영, 별도 채널(tutorial.slide) 누락분만. (3) 스킵: DebugHUD 1~5 릴리즈 상시 노출 + 전환/일시정지 게이팅(IsTransitioning/IsPaused) + 스킵 판 PostRecord 제외(usedStageSkip 랭킹 치팅 방지). (4) Supabase keep-alive: 인게임 핑(Start 1회) + GitHub Actions 데일리 크론(PR #1로 main 반영 대기). (5) "준비하세요" 스테이지 인트로 제거(일반단 1.2→0.3s 코드+씬 직렬화값 MCP 갱신, 5단 "꺾기"만) — v11 "완전 제거" 미적용분 완결. (6) 데드코드 정리(마스터 API 4종 + settings.volume 키). | 참조: general#1,2,3,4,5,6,10 game#13 game#18 CAOF Critical/Standard | 지킴: general#1(오디오 핵심파일 직접 Read 후 단정 — 에이전트 요약만으로 수정 금지), general#3(무음: 스크린샷+plist 실측 3단 진단, 추측 가설 정정), general#2(각 요청 최소 변경 + 코더 정밀 스펙), general#4(매 라운드 MCP recompile 0 errors + 흐름 추적 후 테스트 요청), general#6(/code-review max effort 4-finder 교차 — Codex 인프라 실패 단일 모델 폴백 명시), general#10(main 직접 push 안전가드 거부 → PR 전환), game#18(stageIntroDuration 코드+씬 둘 다 갱신). | 놓침: 초기 무음 "마스터=0" 오진단(스크린샷 전) — plist 실측 전 코드 가설 단정 순간, 사용자 스크린샷이 정정. 튜토리얼/준비하세요 둘 다 "병렬 채널 반영 누락" 패턴(한 채널만 반영, 다른 채널 방치). | 재발: 가능성 — "동일 결정이 여러 독립 채널에 걸칠 때 일부만 반영" 패턴 2건 동시 관찰(튜토리얼·준비하세요). 3회째 시 donts 승격 후보("다채널 결정 전수 반영"). | 메모: plist 실측(plutil로 unity.DefaultCompany.Catch Five Stones.plist 직접 읽기)이 코드 가설을 뒤집음 — 저장상태 버그는 실측이 최선. 마이그레이션 조건부화(GetFloat<=0만)로 커스텀 볼륨 보존. 릴리즈 디버그 스킵 노출 시 리더보드 무결성 별도 가드 필요. Codex CLI 낙후(gpt-5.6-sol 요구)로 이종 교차검수 불가 → codex 업데이트 필요. 크론 main 반영은 PR(직접 push 안전가드).

2026-07-31 게임 | 작업: v16 미완 항목 일괄 정리 (Standard, 중단된 병행 세션 인수) — (1) 20살 배경을 `20살배경newrender.png`(1472×686 클레이 렌더)로 교체 + Assets/Refresh 임포트, (2) 타이틀 말풍선 2종("마참내"/"즐겁다") 로컬라이즈 — `title.bubble_left/right` 키 신설 + `RegisterLocalized`로 라이브 언어전환 편입 + NoWrap+오토사이즈로 한/영 글자폭 차 흡수, (3) `jingleVolume` 데드 필드 제거(참조 0 확인), (4) `BoardDebugLines.cs(+meta)` 삭제(씬·코드 참조 0 확인), (5) 크레딧 `<mspace=0.62em>` 고정폭 정렬(4줄 전부 17자 → Center 정렬에서 열 일치). | 참조: general#1,2,3 game#18,#19,#21 | 지킴: general#1(배경 정합을 "이전 세션이 쟀다"로 믿지 않고 PIL로 재측정 — 뒷변 py261→y+0.17 / 앞변 py600→y−6.75 / 앞변 반폭 ±8.1, 기존 quad ⊆ 새 상판 독립 확인), general#2(각 항목 최소 변경, quad 수치는 손대지 않음), game#19(BoardQuad·IsOutsideMat 동일 폴리곤 유지 — 배경만 갈고 판정 SOT는 불변), game#21(좌표 추측 금지 — 이미지 실측 + 카메라 매핑(ortho 7, cam y=−1.5, quad는 카메라 자식)으로 계산). | 놓침: 없음(컴파일 0 err/0 warn, 콘솔 error 0). | 재발: 없음. | 메모: **배경 quad는 카메라 자식 + 카메라 크기로 스트레치**라 텍스처 종횡비(구 16:9 → 신 2.146:1)가 배치에 영향 없음 — 이미지 "비율 좌표"로 재면 정합이 그대로 유지된다. 다만 신 렌더는 세로로 약 21% 늘어나 보임(연출 판단 필요). 새 테이블은 앞변 반폭 ±8.1로 플레이영역(±5.2)보다 훨씬 넓음 — 넓히려면 Flee SafeZone(±5.3) 재검토가 선행돼야 해 이번엔 보류. 자산 공백 3쌍 잔존(age10=age15, age40=age45, age50=age55, bgm_age10=bgm_age20) — 코드 아닌 에셋 대기. 크레딧 실명·20대 BGM은 사용자가 "현행 유지" 결정.
  ↳ (같은 날 추가) v16-b Stage 3 앞변 X 캡 해제 (Standard) — 사용자 "플레이 영역이 테이블보다 좁은 건 왜?" 질문에서 출발. **원인은 낡은 제약(stale constraint)**: `BoardQuad` 앞변 ±5.20은 v11에 "quad ⊆ GameManager.SafeZone(±5.3)"을 지키려 넣은 캡인데, v14에서 낙 판정이 SafeZone → `BoardBounds.IsOutsideMat`(quad)으로 이관되며 근거가 사라졌다. `SafeZoneMin/Max` 전수 grep 결과 잔존 소비자는 `MonochromeGimmick`(10단 clamp) 단 하나 — Stage 3 경로(FleeGimmick·FleeMovement·ScatterSystem·ScatterRangeIndicator·GameManager 낙판정)는 전부 quad만 읽음. → 앞변 ±5.20 → ±7.20 (뒷변·앞변 Y·SkyFloorY 전부 불변, X 2개 값만 변경). | 참조: general#1,2,3 game#18,#19,#21 | 지킴: game#18(SOT 변경 전 `BoardBounds.` 소비자 전수 나열 후 영향 판정 — MatRect는 center.y만 쓰거나 !HasQuad 폴백뿐임을 확인), game#21(±5.2/6.4/7.2/7.8 네 후보의 앞변 코너를 픽셀로 재검증 — ±7.8은 좌측 코너가 테이블 곡면으로 이탈해 기각), general#1(종횡비를 가정하지 않고 `ScreenManager.EnforceAspectRatio`가 16:9를 강제함 + 디스플레이 2560x1440을 실측 확인). | 놓침: 없음(0 error, 신규 warning 0 — 잔존 12건은 기존 obsolete TMP/미사용 필드). | 재발: 없음. | 메모: **"캡의 근거가 사라졌는데 캡만 남는" 패턴** — SOT를 이관(SafeZone→quad)할 때 그 SOT에 맞춰 미리 눌러둔 상수들을 함께 풀어주지 않으면 조용히 남는다. v14 이관 시 StageConfig의 "X SafeZone 캡" 주석 4곳(Stage 2·3·4·5)이 전부 그 흔적 — **Stage 4·5도 같은 캡이 남아 있어 동일 점검 대상**. 다음 라운드 후보. 또한 배경은 카메라 비율로 스트레치되는데 BoardQuad는 월드 고정이라 16:9 이탈 시 어긋남 — 근본 해결은 quad를 이미지 비율좌표에서 런타임 산출하는 것(Critical 규모, 별건).
  ↳ (이어서) v16-c Stage 4·5 동일 캡 해제 (Standard) — v16-b 메모의 "Stage 4·5도 같은 캡" 후보를 실행. age25/age30 마우스패드를 행별 스캔으로 실측(뒷변 py548 / 앞변 py~811). **Y는 재측정 결과 기존 값이 정확**(패드 범위와 일치) → 불변, X만 조정. Stage 4: 뒷변 ±4.60→±5.25, 앞변 ±5.20→±6.95 (실측 -5.45/+5.69, -7.16/+7.63의 좁은 쪽 -0.2). Stage 5: 패드가 좌로 0.3 치우쳐 뒷변 실측 우측 +4.83 ≈ 현재 4.60이라 **뒷변 유지**, 앞변 ±5.20→±6.25. 대칭 quad 유지(centroid x=0을 전제하는 Flee/Obstacle 중심 계산 보호 — 비대칭 도입 안 함). 전수 스윕 결과 1·2·6·7·8단은 원래 ±8.05 무캡, 9·10단 ±5.30/+5.50은 매트리스에 맞춘 **비대칭 아트 정렬이라 캡이 아님**(대칭이 아닌 게 증거) → 미변경. | 참조: general#1,2 game#18,#21 | 지킴: game#18(ObstacleGimmick이 MatRect.width 기반임을 먼저 확인 → halfW 3.38→4.06(+20%) 영향을 주석에 명시), game#21(패드 위치를 추정하지 않고 행별 밝기 스캔으로 사다리꼴 좌/우 경계를 20px 간격으로 전부 출력해 형태 확인 — 첫 시도의 "py=547/813에 패드 없음"은 1~2px 오프셋 착시였고 스캔으로 정정), general#1(9·10단을 "5.30이니 캡"이라 단정하지 않고 주석·비대칭값 확인 후 제외). | 놓침: 없음(0 error, 신규 warning 0). | 재발: 없음. | 메모: 면적 변화 — 3단 46.3→55.9(+21%), 4단 38.4→47.8(+24%), 5단 37.7→41.8(+11%). 세 단 모두 난이도가 올라가므로 플레이 검증 필요. **되돌리기는 전부 X값 복구만**(3단 5.20 / 4단 4.60·5.20 / 5단 5.20) — Y·SkyFloorY·뒷변Y는 한 번도 건드리지 않았음.
  ↳ (이어서) v16-d age20 배경 프레이밍 교정 (Trivial, 사용자 플레이 피드백) — 사용자 "20살 배경 하늘 영역이 거의 없다" 보고. **처음엔 HUD "Stage: 1"과 age20 배경을 보고 "1단에 20살 배경이 뜬 버그"로 오독했으나 콘솔로 정정** — `[GameManager] Stage 3 started! (Age=20)`(나이 스테이지)와 `Stage 1 started. Phase: Scatter`(공기놀이 단수)는 **서로 다른 두 개념**이고 DebugHUD 1~5 버튼은 단수 스킵(나이는 "다음 스테이지로 +5살"). 배경은 정상이었고 진짜 원인은 **프레이밍**: 신 렌더의 테이블 뒷변이 화면 38.0%인데 age25/age30 플레이면은 57.6% → 머리 위 여유 부재. 렌더 상단 0~78px가 순백(255)임을 확인하고 **상단 흰 여백 247px 패딩**(1472×686→1472×933)으로 해결 — 테이블 뒷변 54.4%(y=-2.123)로 내려와 quad 뒷변(-2.10) 기준 0.023 안쪽, 다른 단(0.02)과 동일 관례. **코드 변경 0**(quad·SkyFloorY 불변). | 참조: general#1,#3 game#21 images#9 | 지킴: general#3(추측 수정 금지 — 콘솔 먼저 보고 "버그 아님" 판정), game#21(좌표 추측 금지 — 후보 2안을 인게임 16:9 스트레치 + quad 오버레이로 렌더해 눈으로 대조 후 선택), images#9(원본 덮어쓰기 금지 — `_v2_pad247` 버전 파일로 보존). | 놓침: **선택지에 제시한 패딩 260px가 방향 오류** — 더 채우면 테이블이 더 내려가 quad가 뒷변 밖으로 나간다는 걸 뒤늦게 계산, 247로 정정 후 적용. 선택지를 내밀기 전에 양쪽 경계를 다 검산했어야 함. | 재발: 없음(신규 패턴). | 메모: **"Stage" 용어 이중화가 오독을 부른다** — 로그·HUD·StageConfig가 전부 "Stage"를 쓰는데 나이 스테이지(1~10, 배경/기믹 결정)와 공기놀이 단수(1~5)가 섞여 있음. 다음에 로그 문구를 `LifeStage`/`Level`로 분리하면 진단이 빨라진다. **배경 프레이밍 체크리스트**: 새 배경 투입 시 "플레이면 뒷변이 화면 몇 %인가"를 기존 단(57.6%)과 비교하는 것만으로 하늘 부족을 사전 적발 가능.
  ↳ (이어서) v16-e 보드 외곽선 디버그 도구 + 뿌리기 낙 판정 오면제 수정 (Standard) — 사용자 스크린샷에서 돌 2개가 테이블 뒷변 **위**(y≈-1.35, quad 뒷변 -2.10)에 안착. (1) **BoardBoundsDebugDrawer.cs 신설** — `BoardBounds.QuadPoint(0/1,0/1)` 4점을 **LineRenderer**(loop)로 빨간 외곽선. URP에서 GL 즉시모드 미렌더(구 BoardDebugLines 실패 원인)를 반복하지 않음. 게이트는 DebugHUD.ShouldShow와 동일(에디터 항상 / 빌드는 연습모드) + `Enabled` static 토글을 TEST 패널 6번째 버튼으로 노출. 매 프레임 폴리곤 비교 후 변경 시에만 갱신 → 전 스테이지·라이브튜닝 자동 반영, quad 없는 단은 MatRect AABB가 같은 경로로 그려짐. (2) **근본원인**: `ScatterSystem`이 안착 판정에 `IsOutsideMat`을 써서 "뒷변 위=하늘이라 outside 면제"(던져서 **날아가는 중**인 돌 보호용)를 **안착하는 돌**에도 적용 → 보이는 보드 밖에 앉아도 낙이 아님 = "판정 영역이 보이는 것보다 커 보임". 3곳(L338 회수루프 / L345 최종낙 / L451 hop)을 `IsOutsideMatStrict`로 교체(마진 동일). | 참조: general#1,#2,#3,#4 game#19,#21 | 지킴: general#3(콘솔+코드 추적으로 원인 적출 — 화면만 보고 quad 수치를 또 만지지 않음), general#1(수정 전 ScatterSystem L300~460 직접 Read), general#4(**불변식 방향 검증** — 뿌리기가 감시보다 *엄격*해졌으므로 통과한 돌은 감시에서도 통과 → 안착 직후 오낙 없음. 반대 방향이었으면 버그), general#2(3줄 교체 + 주석, 로직 재작성 없음), game#21(LineRenderer 강제). | 놓침: 없음(에러 0/워닝 0). 단 recompile이 2패스 누적으로 워닝을 이중집계(12→22)해 순간 오인 → 재실행으로 0 확인. | 재발: 없음 — **v12 Flee의 `IsOutsideMatStrict` 신설과 완전히 동일한 패턴의 2회째**. 소비자만 달랐음(Flee → Scatter). **승격 후보**: "`IsOutsideMat`의 SkyFloor 면제는 *비행 중* 돌 전용 — 보드 면에 놓이거나 이동하는 돌은 전부 Strict." 3회째 시 donts/game.md 정식 룰화. | 메모: 낡은 주석("게임 낙 감시(IsOutsideMat)와 동일 → 3자 완전 일치")이 그대로면 다음 작업자가 Strict를 되돌릴 위험이 있어 L250에 "되돌리지 말 것 + 엄격해야 안전한 이유"를 명시. **디버그 도구는 처음부터 만들었어야 함** — v11-fix3 교훈("추측 패치 3회보다 시각화 1회")과 동일 구조인데, 이번에도 사용자 스크린샷이 나온 뒤에야 만들었다.
  ↳ (이어서) v17 Phase 0-a 불변식 테스트 인프라 (Critical 슬라이스 1) — `.asmdef` 2종(FiveStones.Core / FiveStones.Tests) + `BoardGeometry` 순수 struct + EditMode 불변식 테스트 11개. TDD 준수(스텁→빨강 10/11→구현→초록 11/11). **기존 58스크립트는 미변경** — 새 순수 코드에만 asmdef를 붙이고 autoReferenced=true로 두면 Unity 기본 어셈블리가 자동 참조하므로 무변경 통합 가능(에러 0). 테스트는 **수치 스냅샷이 아니라 관계 불변식**만 잠금(v17이 모든 수치를 재튜닝하므로 스냅샷은 전부 빨간불이 됨): Project↔Unproject 왕복=「보이는 것=판정」의 수학적 표현 · height 변해도 화면x 불변=수직 궤적 · Contains가 height 인자를 안 받음(리플렉션 검사)=「하늘 면제」 예외의 구조적 차단 · Board Space 이동 균일성. | 참조: general#1,#4 game#21 TDD스킬 | 지킴: general#4(자체 검수 중 **내가 쓴 테스트 1개가 가짜**임을 발견 — `그림자는_돌의_보드좌표에_고정된다`가 Project(p,0)을 자기 자신과 비교해 구현이 뭐든 통과 → 돌.x==그림자.x && 돌.y>그림자.y 로 교체), general#1(asmdef가 기존 코드를 깨는지 추측하지 않고 recompile로 확인 후 진행). | 놓침: **편집 후 리컴파일 없이 run_tests를 돌려 낡은 어셈블리로 채점**됨. 이름 바꾼 테스트가 옛 이름으로 나온 것이 단서. → **run_tests 전에는 항상 Assets/Refresh + recompile.** game#21의 "Play 중 재컴파일 미반영"과 동일 계열(테스트 러너 판). | 재발: 있음 — 「Unity가 최신 코드로 돌고 있는가」 미확인이 2회째(v14 Play 중 재컴파일 → 이번 테스트 러너). **승격 후보**: "MCP로 Unity에 뭔가 실행시키기 전에 recompile 선행". 3회째 시 donts/game.md 정식 룰화. | 메모: 리플렉션으로 **API 형태 자체를 불변식으로 잠그는** 패턴이 유효했다(`Contains`의 파라미터 수). 나중에 누가 height 인자를 추가하려 하면 테스트가 막는다 — 주석보다 강한 방어. 스텁 단계에서 이 테스트만 통과한 것도 테스트가 정상 작동한다는 증거.
