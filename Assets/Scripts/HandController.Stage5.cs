using System.Collections;
using UnityEngine;

/// <summary>
/// 5단 "꺾기" 전용 시퀀스 — HandController에서 분리한 partial.
///
/// 왜 분리했나: 이 코드가 654줄로 HandController(1,610줄)의 40%를 차지했고,
/// 5단을 건드릴 때마다 1~4단 코드와 같은 파일에서 충돌했다.
/// (재설계 계획서가 지목한 "5단이 전 계층에 특수 분기로 누출" 문제의 물리적 분리 단계)
///
/// ⚠️ partial이라 **동작은 하나도 바뀌지 않는다.** 같은 클래스이므로 필드·메서드 접근도 그대로다.
/// 다음 단계는 이 시퀀스를 일반 기믹 구조로 흡수해 "5단 전용 분기" 자체를 없애는 것이다.
/// </summary>
public partial class HandController
{
    /// <summary>
    /// GameManager에서 호출: 5단 시퀀스 시작 (코루틴이 이미 실행 중이면 중복 시작 방지)
    /// </summary>
    public void BeginStage5Throw()
    {
        if (stage5Coroutine != null) return; // 이미 실행 중 — 3단계 SetPhase 재호출 방지
        stage5ClickPending = false;
        stage5Coroutine = StartCoroutine(DoStage5Sequence());
    }

    /// <summary>
    /// 5단 꺾기 전체 시퀀스 (4단계):
    /// [1단계] 손바닥 던지기 (게이지) → [2단계] 손등 받기 → [3단계] 손등 던지기 (게이지) → [4단계] 주먹 낚아채기
    /// </summary>
    private IEnumerator DoStage5Sequence()
    {
        var gm = GameManager.Instance;
        var allStones = gm.Stones;
        int count = allStones.Length;

        // v6-1: boardSurfaceY 캐시 (DoStage5Catch/FistGrab에서 멤버 변수로 참조)
        var catchSys = FindFirstObjectByType<CatchSystem>();
        stage5BoardSurfaceY = catchSys != null ? catchSys.BoardSurfaceY : -8.2f;

        // ============ [1단계] 손바닥 던지기 ============
        // SetPhase(Stage5Throw)는 GameManager.DoStageIntro에서 이미 호출됨
        // 돌도 DoStageIntro에서 InHand + SetParent(handController)로 설정됨
        SetCatchMode(false);

        // 손 위치 세팅 (돌은 hand 자식이므로 따라감)
        transform.position = new Vector3(0f, catchAreaY - 2f, -0.5f);

        // Press/Release 게이지 대기
        stage5GaugeWaiting = true;
        stage5GaugeActive = false;
        stage5GaugePending = false;
        yield return new WaitUntil(() => stage5GaugePending);
        stage5GaugeWaiting = false;
        stage5GaugePending = false;

        // 게이지 값으로 높이 결정
        float peakY1 = Mathf.Lerp(stage5MinPeakY, stage5MaxPeakY, stage5GaugeValue);

        // 1차 던지기 (X 퍼짐)
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, count, peakY1, true);

        // ============ [2단계] 손등으로 받기 ============
        gm.AdvanceStage5Step(); // step 0→1
        SetCatchMode(false);
        stage5CatchActive = false;
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        bool catch1Success = false;
        yield return DoStage5Catch(allStones, count, success => catch1Success = success);

        if (!catch1Success)
        {
            stage5Coroutine = null;
            yield break;
        }

        // 돌을 손에 부착
        Debug.Log("[Stage5] Back-hand catch SUCCESS!");
        for (int i = 0; i < count; i++)
        {
            allStones[i].SetState(Stone.State.Caught);
            allStones[i].transform.SetParent(transform);
            allStones[i].transform.localPosition = new Vector3((i - 2) * 0.3f, 0f, 0f);
        }

        // ============ [3단계] 손등 던지기 ============
        gm.AdvanceStage5Step(); // step 1→2
        gm.SetPhase(GameManager.GamePhase.Stage5Throw);
        SetCatchMode(false);
        stage5CatchActive = false;

        yield return new WaitForSeconds(0.3f);

        // Press/Release 게이지 대기
        stage5GaugeWaiting = true;
        stage5GaugeActive = false;
        stage5GaugePending = false;
        yield return new WaitUntil(() => stage5GaugePending);
        stage5GaugeWaiting = false;
        stage5GaugePending = false;

        float peakY2 = Mathf.Lerp(stage5MinPeakY, stage5MaxPeakY, stage5GaugeValue);

        // 2차 던지기 (X 고정, 수직)
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, count, peakY2, false);

        // ============ [4단계] 최종 낚아채기 ============
        gm.AdvanceStage5Step(); // step 2→3
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        bool grabSuccess = false;
        yield return DoStage5FistGrab(allStones, count, success => grabSuccess = success);

        if (!grabSuccess)
        {
            stage5Coroutine = null;
            yield break;
        }

        // 성공! 돌 정리
        Debug.Log("[Stage5] Fist grab SUCCESS! ALL STAGES CLEARED!");
        for (int i = 0; i < count; i++)
        {
            allStones[i].SetState(Stone.State.Caught);
            allStones[i].transform.SetParent(null);
            allStones[i].gameObject.SetActive(false);
        }

        stage5Coroutine = null;
        gm.SetPhase(GameManager.GamePhase.StageComplete);
    }

    /// <summary>
    /// 5개 돌을 동시에 하늘로 던지는 코루틴.
    /// spreadX=true: GenerateSpreadPositions로 X 퍼짐 (1단계)
    /// spreadX=false: 각 돌의 현재 X 위치 유지 — 수직 던지기 (3단계)
    /// </summary>
    private IEnumerator DoStage5Toss(Stone[] stones, int count, float peakY, bool spreadX)
    {
        // v17: 5단도 보드 좌표 + 높이로. 화면 y로 계산하면 원근 스케일이 안 붙고
        //   1~4단과 궤적 규칙이 달라진다. 깊이는 손이 있는 깊이에 고정하고(수직 토스)
        //   퍼짐은 **보드 x**로만 준다.
        Vector2 handBoard = BoardSpace.ClampToBoard(
            BoardSpace.ToBoard(new Vector2(transform.position.x, transform.position.y)));

        float[] targetBX = spreadX ? GenerateSpreadPositions(count) : new float[count];
        float[] startBX  = new float[count];
        float[] peakH    = new float[count];

        for (int i = 0; i < count; i++)
        {
            var bp = BoardSpace.ClampToBoard(BoardSpace.ToBoard(
                new Vector2(stones[i].transform.position.x, stones[i].transform.position.y)));
            startBX[i] = bp.x;

            if (spreadX) targetBX[i] += handBoard.x; // 손 기준 좌우 퍼짐
            else         targetBX[i]  = bp.x;        // X 고정: 현재 위치에서 수직

            // 화면 기준 peakY를 그 보드 깊이의 지면 대비 '높이'로 환산.
            float groundY = BoardSpace.ToScreen(new Vector2(bp.x, handBoard.y), 0f).y;
            peakH[i] = Mathf.Max(0.1f, peakY + Random.Range(-stage5HeightStep, stage5HeightStep) - groundY);

            stones[i].transform.SetParent(null);
            stones[i].SetState(Stone.State.InAir); // layer=AirLayer, col=true
            stones[i].Rb.isKinematic = true;       // SetState 후 덮어쓰기 (코루틴 위치 제어용)
            stones[i].Rb.useGravity = false;
        }

        // 올라가기 (EaseOut — 중력에 거슬러 감속하는 상승. 물리적으로 맞다)
        float elapsed = 0f;
        while (elapsed < throwUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwUpDuration);
            float eased = 1f - (1f - t) * (1f - t); // EaseOut

            for (int i = 0; i < count; i++)
            {
                float bx = spreadX ? Mathf.Lerp(startBX[i], targetBX[i], eased) : targetBX[i];
                stones[i].SetBoardMotion(new Vector2(bx, handBoard.y), Mathf.Lerp(0f, peakH[i], eased));
            }
            yield return null;
        }

        // 최고점 고정
        for (int i = 0; i < count; i++)
            stones[i].SetBoardMotion(new Vector2(targetBX[i], handBoard.y), peakH[i]);

        Debug.Log($"[Stage5] Toss complete — stones at peak. peakY={peakY:F1}, spreadX={spreadX}");
    }

    /// <summary>
    /// 5개 돌의 하강 + 캐치 판정 코루틴.
    /// 플레이어는 커서 좌우 이동으로 손을 움직여 받는다.
    /// </summary>
    private IEnumerator RestoreHandScale(Vector3 originalScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * (2f - t); // EaseOut
            transform.localScale = Vector3.Lerp(startScale, originalScale, eased);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    private IEnumerator DoStage5Catch(Stone[] stones, int count, System.Action<bool> onResult)
    {
        // === 슬라이드 인 (0.3초) ===
        // isCatchMode=false 상태 (호출 전 설정), LateUpdate 무동작
        stage5CatchActive = false;

        float slideStartX = 8f;       // 화면 오른쪽 밖 (boardMax.x=4 + 여유)
        float slideEndX = 0f;          // 화면 중앙
        float slideDuration = 0.3f;

        transform.position = new Vector3(slideStartX, catchAreaY, -0.5f);

        float slideElapsed = 0f;
        while (slideElapsed < slideDuration)
        {
            slideElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(slideElapsed / slideDuration);
            float eased = t * (2f - t);  // EaseOut 감속 도착
            float x = Mathf.Lerp(slideStartX, slideEndX, eased);
            transform.position = new Vector3(x, catchAreaY, -0.5f);
            yield return null;
        }
        transform.position = new Vector3(slideEndX, catchAreaY, -0.5f);

        // 슬라이드 인 완료 — 이제부터 LateUpdate가 X축 조작 처리
        SetCatchMode(true);
        stage5CatchActive = true;

        // === 손등 받기: 손 크기 2배로 확대 (0.3초 보간) ===
        float originalCatchRadius = stage5CatchRadius;
        float scaleDuration = 0.3f;
        float scaleElapsed = 0f;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * stage5BackhandScaleMultiplier;
        float targetRadius = originalCatchRadius * stage5BackhandScaleMultiplier;

        while (scaleElapsed < scaleDuration)
        {
            scaleElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(scaleElapsed / scaleDuration);
            float eased = t * (2f - t); // EaseOut
            transform.localScale = Vector3.Lerp(originalScale, targetScale, eased);
            stage5CatchRadius = Mathf.Lerp(originalCatchRadius, targetRadius, eased);
            yield return null;
        }
        transform.localScale = targetScale;
        stage5CatchRadius = targetRadius;

        // === 독립 낙하 시간 계산 ===
        bool[] caught = new bool[count];
        int caughtCount = 0;

        float[] stoneStartY = new float[count];
        float[] stoneX = new float[count];
        // v17: 5단 낙하도 보드 좌표 + 높이로. 토스가 SetBoardMotion으로 끝나므로
        //   돌이 이미 BoardPos/Height를 들고 있다 — 그대로 이어받는다.
        var stoneBoard = new Vector2[count];
        var startH = new float[count];
        for (int i = 0; i < count; i++)
        {
            stoneBoard[i] = stones[i].BoardPos;
            startH[i] = stones[i].Height;
            stoneStartY[i] = stones[i].transform.position.y;
            stoneX[i] = stones[i].transform.position.x;
        }

        float baseFallDuration = throwDownDuration * 1.2f;

        // 최고점/최저점 계산 (독립 낙하 시간 정규화용)
        float maxStartY = stoneStartY[0];
        float minStartY = stoneStartY[0];
        for (int i = 1; i < count; i++)
        {
            if (stoneStartY[i] > maxStartY) maxStartY = stoneStartY[i];
            if (stoneStartY[i] < minStartY) minStartY = stoneStartY[i];
        }

        float[] stoneElapsed = new float[count];
        float[] downDuration = new float[count];
        float maxDownDuration = 0f;

        for (int i = 0; i < count; i++)
        {
            float normalizedH = (maxStartY > minStartY)
                ? (stoneStartY[i] - minStartY) / (maxStartY - minStartY)
                : 0f;
            downDuration[i] = baseFallDuration + normalizedH * baseFallDuration;
            if (downDuration[i] > maxDownDuration) maxDownDuration = downDuration[i];
            Debug.Log($"[Stage5] Stone {stones[i].StoneIndex}: startY={stoneStartY[i]:F1}, normalizedH={normalizedH:F2}, downDuration={downDuration[i]:F2}s");
        }

        // v6-1: landY를 boardSurfaceY 기준으로 통일 (기존: catchAreaY - stage5MissThreshold)
        float landY = stage5BoardSurfaceY; // 보드 표면까지 내려오면 놓침

        // === 독립 낙하 루프 (EaseIn — 가속) ===
        float globalElapsed = 0f;
        while (globalElapsed < maxDownDuration)
        {
            globalElapsed += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                if (caught[i]) continue;

                stoneElapsed[i] += Time.deltaTime;
                float t = Mathf.Clamp01(stoneElapsed[i] / downDuration[i]);
                float eased = t * t;  // EaseIn 가속 낙하

                // v17: 화면 y가 아니라 높이를 보간하고 위치·원근 크기는 투영으로 파생.
                //   보드 좌표는 고정(수직 낙하) — 1~4단 던지기와 같은 규칙이다.
                stones[i].SetBoardMotion(stoneBoard[i], Mathf.Lerp(startH[i], -1f, eased));
                float y = stones[i].transform.position.y;

                // v11-fix5 (옵션 C): catch window를 손 위치(palmTopY) 기반으로 변경.
                //   원인 (v11-fix4 진단 오류 후 재분석): 기존 절대 y(catchAreaY+0.5) 기준은 손이 어디 있든 돌이 y≤2.5에서 잡힘
                //         → 손 안 올렸는데도 catch 발동 + 직후 SetParent 텔레포트(L924-925)로 "공중에서 받힘" 인지 발생.
                //   해결: palmTopY = transform.position.y + 0.4 * localScale.y (Palm 시각 윗면, backhand 2x 스케일 자동 보정).
                //         catchUpper = palmTopY + 0.3 (손등 위 ~30px 여유), catchLower = palmTopY - 0.5 (손 중심 살짝 아래).
                //         손을 올리면 catch 영역도 같이 올라가서 "돌이 손등에 닿는 느낌" 구현 + 텔레포트 거리 자연 단축.
                // handRaised: 보드 영역 진입 차단용 안전망 (옵션 C 본질은 palmTopY 자체에 있음).
                float palmTopY = transform.position.y + 0.4f * transform.localScale.y;
                if (y <= palmTopY + 0.3f && y >= palmTopY - 0.5f && y >= BoardBounds.SkyFloorY)
                {
                    bool handRaised = transform.position.y >= BoardBounds.SkyFloorY;
                    // v17: 화면 x 거리 → **보드 x 거리**. 돌과 같은 깊이에서 손의 화면 x를
                    //   해석해 보드 x를 얻는다(손은 하늘에 들려 있어 지면 역투영이 불가).
                    //   보드 단위라 돌이 앞/뒤 어디에 있든 관대함이 같다 — 1~4단 받기와 동일 원리.
                    float groundY5 = BoardSpace.ToScreen(stoneBoard[i], 0f).y;
                    float handBX = BoardSpace.ToBoard(new Vector2(transform.position.x, groundY5)).x;
                    float distX = Mathf.Abs(stoneBoard[i].x - handBX);
                    if (handRaised && distX <= stage5CatchRadius)
                    {
                        caught[i] = true;
                        caughtCount++;
                        AudioManager.Instance?.PlayStage5CatchStone(caughtCount);
                        stones[i].SetState(Stone.State.Caught);
                        stones[i].Rb.isKinematic = true;
                        // 손에 부착 — 손이 움직이면 돌도 함께 이동 (그릇 안 담긴 느낌)
                        stones[i].transform.SetParent(transform);
                        stones[i].transform.localPosition = new Vector3((caughtCount - 1) * 0.25f - 0.5f, 0.1f, 0f);
                        Debug.Log($"[Stage5] Caught stone {stones[i].StoneIndex}! ({caughtCount}/{count})");
                    }
                }

                // 놓침 판정: 손 아래로 지나감
                if (y < landY && !caught[i])
                {
                    Debug.Log($"[Stage5] MISSED stone {stones[i].StoneIndex}!");
                    TestLogger.Instance?.LogFailure($"stage5_miss_stone_{stones[i].StoneIndex}");
                    stage5CatchActive = false;
                    // 실패 시 즉시 원래 크기로 복원
                    transform.localScale = originalScale;
                    stage5CatchRadius = originalCatchRadius;
                    SetCatchMode(false);
                    onResult?.Invoke(false);
                    GameManager.Instance.SetFailReason("돌을 놓쳤다!");
                    GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                    yield break;
                }
            }

            // 모두 잡으면 조기 종료
            if (caughtCount >= count)
            {
                stage5CatchActive = false;
                // 손 크기 복원 (0.2초 보간)
                yield return RestoreHandScale(originalScale, 0.2f);
                stage5CatchRadius = originalCatchRadius;
                SetCatchMode(false);
                onResult?.Invoke(true);
                yield break;
            }

            yield return null;
        }

        // 시간 초과
        stage5CatchActive = false;
        // 시간 초과 시 즉시 원래 크기로 복원
        transform.localScale = originalScale;
        stage5CatchRadius = originalCatchRadius;
        SetCatchMode(false);
        if (caughtCount < count)
        {
            Debug.Log($"[Stage5] Time up! Only caught {caughtCount}/{count}");
            TestLogger.Instance?.LogFailure($"stage5_timeout_{caughtCount}_of_{count}");
            GameManager.Instance.SetFailReason("시간 초과!");
            onResult?.Invoke(false);
        }
        else
        {
            onResult?.Invoke(true);
        }
    }

    /// <summary>
    /// 4단계: 한붓그리기 낚아채기.
    /// 홀드(Press) + 드래그로 떨어지는 돌을 스쳐 지나가며 하나씩 낚아챔.
    /// Release 시 5개 모두 잡혔으면 성공, 아니면 실패.
    /// </summary>
    private IEnumerator DoStage5FistGrab(Stone[] stones, int count, System.Action<bool> callback)
    {
        // 슬라이드 인
        float slideStartX = 8f;
        float slideDuration = 0.3f;
        float slideElapsed = 0f;

        transform.position = new Vector3(slideStartX, catchAreaY, -0.5f);

        while (slideElapsed < slideDuration)
        {
            slideElapsed += Time.deltaTime;
            float t = slideElapsed / slideDuration;
            float eased = t * (2f - t);
            float x = Mathf.Lerp(slideStartX, 0f, eased);
            transform.position = new Vector3(x, catchAreaY, -0.5f);
            yield return null;
        }

        SetCatchMode(true);
        stage5CatchActive = true;

        // === 손바닥 받기: 손 크기 2배로 확대 (0.3초 보간) ===
        float originalGrabRadius = stage5FistGrabRadius;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * stage5BackhandScaleMultiplier;
        float targetGrabRadius = originalGrabRadius * stage5BackhandScaleMultiplier;
        {
            float scaleElapsed = 0f;
            float scaleDuration = 0.3f;
            while (scaleElapsed < scaleDuration)
            {
                scaleElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(scaleElapsed / scaleDuration);
                float eased = t * (2f - t);
                transform.localScale = Vector3.Lerp(originalScale, targetScale, eased);
                stage5FistGrabRadius = Mathf.Lerp(originalGrabRadius, targetGrabRadius, eased);
                yield return null;
            }
            transform.localScale = targetScale;
            stage5FistGrabRadius = targetGrabRadius;
        }

        // 각 돌의 낙하 시작 위치
        float[] stoneStartY = new float[count];
        float[] stoneX = new float[count];
        float[] downDuration = new float[count];
        float[] stoneElapsed = new float[count];
        bool[] caught = new bool[count];
        int caughtCount = 0;

        float minStartY = float.MaxValue, maxStartY = float.MinValue;
        // v17: 5단 낙하도 보드 좌표 + 높이로. 토스가 SetBoardMotion으로 끝나므로
        //   돌이 이미 BoardPos/Height를 들고 있다 — 그대로 이어받는다.
        var stoneBoard = new Vector2[count];
        var startH = new float[count];
        for (int i = 0; i < count; i++)
        {
            stoneBoard[i] = stones[i].BoardPos;
            startH[i] = stones[i].Height;
            stoneStartY[i] = stones[i].transform.position.y;
            stoneX[i] = stones[i].transform.position.x;
            stoneElapsed[i] = 0f;
            caught[i] = false;
            if (stoneStartY[i] < minStartY) minStartY = stoneStartY[i];
            if (stoneStartY[i] > maxStartY) maxStartY = stoneStartY[i];
        }

        float baseFallDuration = 1.8f;
        float heightRange = maxStartY - minStartY;
        for (int i = 0; i < count; i++)
        {
            float normalizedH = heightRange > 0.01f
                ? (stoneStartY[i] - minStartY) / heightRange
                : 0f;
            downDuration[i] = baseFallDuration * (1f + normalizedH);
        }

        // v6-1: landY를 boardSurfaceY 기준으로 통일 (기존: catchAreaY - 3.5f)
        float landY = stage5BoardSurfaceY;
        float maxDownDuration = baseFallDuration * 3f;
        float globalElapsed = 0f;
        bool isGrabbing = false; // 홀드 중 (한붓그리기 활성)

        // 낙하 + 한붓그리기 루프
        while (globalElapsed < maxDownDuration)
        {
            globalElapsed += Time.deltaTime;
            bool anyReachedFloor = false;

            // 돌 위치 업데이트 (잡히지 않은 돌만)
            for (int i = 0; i < count; i++)
            {
                if (caught[i]) continue;
                stoneElapsed[i] += Time.deltaTime;
                float t = Mathf.Clamp01(stoneElapsed[i] / downDuration[i]);
                // v17: 높이를 보간하고 위치·원근 크기는 투영으로 파생 (보드 좌표 고정 = 수직 낙하).
                stones[i].SetBoardMotion(stoneBoard[i], Mathf.Lerp(startH[i], -1f, t * t));
                float y = stones[i].transform.position.y;

                if (y <= landY)
                    anyReachedFloor = true;
            }

            // 홀드 감지: clickAction이 눌려있는지
            bool pressed = clickAction.IsPressed();

            if (pressed && !isGrabbing)
            {
                // Press 시작 → 한붓그리기 시작
                isGrabbing = true;
                AnimateFingerFold(true);
                Debug.Log("[Stage5] Fist grab started (hold)");
            }

            if (isGrabbing && pressed)
            {
                // 홀드 중 → 매 프레임 손 근처 돌 체크
                // ⚠️ 여기는 보드 거리로 바꾸지 않는다. 낚아채기는 "손으로 쓸어 담는" 제스처라
                //    화면에서 손과 돌이 겹치는지가 곧 판정이다(받기처럼 '밑에 대는' 동작이 아님).
                //    돌들은 모두 손과 같은 깊이에서 던져지므로 깊이에 따른 편차도 없다.
                Vector2 handPos = new Vector2(transform.position.x, transform.position.y);
                for (int i = 0; i < count; i++)
                {
                    if (caught[i]) continue;
                    Vector2 stonePos = new Vector2(stones[i].transform.position.x, stones[i].transform.position.y);
                    float dist = Vector2.Distance(handPos, stonePos);
                    if (dist <= stage5FistGrabRadius)
                    {
                        caught[i] = true;
                        caughtCount++;
                        stones[i].Rb.isKinematic = true;
                        stones[i].SetState(Stone.State.Caught);
                        stones[i].transform.SetParent(transform);
                        stones[i].transform.localPosition = new Vector3((caughtCount - 3) * 0.15f, 0f, 0f);
                        AudioManager.Instance?.PlayStage5CatchStone(caughtCount);
                        Debug.Log($"[Stage5] Grabbed stone {stones[i].StoneIndex}! ({caughtCount}/{count})");
                    }
                }

                // 홀드 중 5개 모두 잡으면 즉시 성공
                if (caughtCount >= count)
                {
                    AudioManager.Instance?.PlayStageClear();
                    yield return new WaitForSeconds(0.5f);
                    stage5CatchActive = false;
                    yield return RestoreHandScale(originalScale, 0.2f);
                    stage5FistGrabRadius = originalGrabRadius;
                    SetCatchMode(false);
                    AnimateFingerFold(false);
                    callback?.Invoke(true);
                    yield break;
                }
            }

            if (!pressed && isGrabbing)
            {
                // Release → 한붓그리기 종료
                isGrabbing = false;
                Debug.Log($"[Stage5] Fist grab released: {caughtCount}/{count} caught");

                if (caughtCount >= count)
                {
                    // 전부 잡음 (Release와 동시)
                    AudioManager.Instance?.PlayStageClear();
                    yield return new WaitForSeconds(0.5f);
                    stage5CatchActive = false;
                    yield return RestoreHandScale(originalScale, 0.2f);
                    stage5FistGrabRadius = originalGrabRadius;
                    SetCatchMode(false);
                    AnimateFingerFold(false);
                    callback?.Invoke(true);
                    yield break;
                }
                else
                {
                    // 미완성 → 실패
                    yield return new WaitForSeconds(0.5f);
                    stage5CatchActive = false;
                    transform.localScale = originalScale;
                    stage5FistGrabRadius = originalGrabRadius;
                    SetCatchMode(false);
                    AnimateFingerFold(false);
                    GameManager.Instance.SetFailReason("돌을 놓쳤다!");
                    GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                    callback?.Invoke(false);
                    yield break;
                }
            }

            // 미입력 + 바닥 도달 = 실패
            if (anyReachedFloor && !isGrabbing)
            {
                stage5CatchActive = false;
                transform.localScale = originalScale;
                stage5FistGrabRadius = originalGrabRadius;
                SetCatchMode(false);
                AnimateFingerFold(false);
                AudioManager.Instance?.PlayCatchFail();
                GameManager.Instance.SetFailReason("돌을 놓쳤다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                callback?.Invoke(false);
                yield break;
            }

            yield return null;
        }

        // 타임아웃
        stage5CatchActive = false;
        transform.localScale = originalScale;
        stage5FistGrabRadius = originalGrabRadius;
        SetCatchMode(false);
        AnimateFingerFold(false);
        AudioManager.Instance?.PlayCatchFail();
        GameManager.Instance.SetFailReason("시간 초과!");
        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        callback?.Invoke(false);
    }

    /// <summary>
    /// 최소 간격을 보장하며 랜덤 X 위치를 생성
    /// </summary>
    private float[] GenerateSpreadPositions(int count)
    {
        float[] positions = new float[count];
        int maxAttempts = 50;

        for (int i = 0; i < count; i++)
        {
            bool valid = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = Random.Range(-stage5SpreadRange, stage5SpreadRange);
                bool tooClose = false;

                for (int j = 0; j < i; j++)
                {
                    if (Mathf.Abs(x - positions[j]) < stage5MinSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    positions[i] = x;
                    valid = true;
                    break;
                }
            }

            // 못 찾으면 균등 분배 fallback
            if (!valid)
            {
                positions[i] = Mathf.Lerp(-stage5SpreadRange, stage5SpreadRange,
                    (float)i / (count - 1));
            }
        }

        return positions;
    }
}
