using UnityEngine;

/// <summary>
/// Stage 2 [15살] 색깔 선택 기믹.
/// 처음 5개 돌에 5색 배정 → 유저가 1개 던지면 타겟 색 확정 → 추가 15개 활성화 + 색 배분.
/// </summary>
public class ColorSelectGimmick : StageGimmick
{
    private Stone.StoneColor targetColor = Stone.StoneColor.Default;
    private bool targetConfirmed = false;
    private Stone[] additionalStones; // 추가 활성화된 15개
    private int completedRounds = 0; // 성공한 줍기-받기 라운드 수

    private static readonly Stone.StoneColor[] allColors = new Stone.StoneColor[]
    {
        Stone.StoneColor.Yellow,
        Stone.StoneColor.Red,
        Stone.StoneColor.Green,
    };

    public override void OnStageStart(int stageInLoop)
    {
        targetColor = Stone.StoneColor.Default;
        targetConfirmed = false;
        additionalStones = null;
        completedRounds = 0;

        // 5개 돌에 3색 배정 (노랑, 빨강, 초록)
        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        for (int i = 0; i < active.Length; i++)
        {
            active[i].SetColor(allColors[i % allColors.Length]);
        }
        Debug.Log("[ColorSelectGimmick] Stage started: 3 colors assigned to 5 stones.");
    }

    /// <summary>
    /// 던진 돌의 색을 타겟으로 확정, 추가 15개 돌 활성화 + 색 배분.
    /// HandController.DoThrow → GameManager.NotifyThrowStart → 여기 호출됨.
    /// </summary>
    /// <summary>타겟 색상의 한글 이름</summary>
    private static string GetColorName(Stone.StoneColor color)
    {
        return color switch
        {
            Stone.StoneColor.Red    => "빨강",
            Stone.StoneColor.Blue   => "파랑",
            Stone.StoneColor.Yellow => "노랑",
            Stone.StoneColor.Green  => "초록",
            Stone.StoneColor.Purple => "보라",
            _ => "???"
        };
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        if (targetConfirmed) return;

        // 타겟 색상: 무조건 노랑
        targetColor = Stone.StoneColor.Yellow;
        targetConfirmed = true;
        thrownStone.SetColor(Stone.StoneColor.Yellow); // 던진 돌도 노랑으로 강제
        Debug.Log($"[ColorSelectGimmick] Target color confirmed: {targetColor}");

        // UI 안내
        GameUI.Instance?.UpdateGuideText($"[ {GetColorName(targetColor)}만 주우세요! ]");

        // 추가 15개 활성화
        var pool = StonePool.Instance;
        if (pool == null) return;

        additionalStones = pool.ActivateAdditional(15);

        // 추가 돌 상태 초기화
        foreach (var s in additionalStones)
            s.SetState(Stone.State.OnBoard);

        // 전체 보드 돌 수집 (기존 4 + 추가 15 = 19개)
        var allBoardStones = new System.Collections.Generic.List<Stone>();
        foreach (var s in pool.ActiveStones)
        {
            if (s.CurrentState == Stone.State.OnBoard)
                allBoardStones.Add(s);
        }

        // 랜덤 배분: T(노랑) + D1(빨강) + D2(초록) = 20
        // T: 현재 단수 클리어 필요 최소(4개) ~ 10개
        int minRequired = 4; // 1단=4회×1개, 2단=2회×2개, 3단=2회×2개, 4단=1회×4개 → 항상 4
        int yellowTotal = Random.Range(minRequired, 11); // 4~10
        int remaining = 20 - yellowTotal;
        int redTotal = Random.Range(1, remaining); // 1 ~ R-1
        int greenTotal = remaining - redTotal;

        // 보드 위 19개 = 던진 돌(노랑 1) 제외
        int yellowBoard = yellowTotal - 1;
        int redBoard = redTotal;
        int greenBoard = greenTotal;

        // 색 배열 생성 + 셔플
        var colorAssign = new Stone.StoneColor[allBoardStones.Count];
        int idx = 0;
        for (int i = 0; i < yellowBoard && idx < colorAssign.Length; i++) colorAssign[idx++] = Stone.StoneColor.Yellow;
        for (int i = 0; i < redBoard && idx < colorAssign.Length; i++) colorAssign[idx++] = Stone.StoneColor.Red;
        while (idx < colorAssign.Length) colorAssign[idx++] = Stone.StoneColor.Green;

        // Fisher-Yates 셔플
        for (int i = colorAssign.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (colorAssign[i], colorAssign[j]) = (colorAssign[j], colorAssign[i]);
        }

        // 전체 보드 돌 위치 재배치 (그리드 기반 분산 — 겹침 방지)
        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 3.5f;
        float halfH = 2.8f;

        // 19개를 5열×4행 그리드에 배치 (1칸 비움) + 랜덤 오프셋
        int cols = 5, rows = 4;
        float cellW = (halfW * 2f) / cols;   // 셀 너비
        float cellH = (halfH * 2f) / rows;   // 셀 높이
        float jitter = 0.55f;                 // 셀 내 랜덤 흔들림 (크게 → 카오스)

        // 그리드 위치 생성 + 셔플
        var gridPositions = new System.Collections.Generic.List<Vector3>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float gx = cx - halfW + cellW * (c + 0.5f) + Random.Range(-jitter, jitter);
                float gy = cy - halfH + cellH * (r + 0.5f) + Random.Range(-jitter, jitter);
                gridPositions.Add(new Vector3(gx, gy, 0f));
            }
        }
        // Fisher-Yates 셔플
        for (int i = gridPositions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (gridPositions[i], gridPositions[j]) = (gridPositions[j], gridPositions[i]);
        }

        for (int i = 0; i < allBoardStones.Count; i++)
        {
            var s = allBoardStones[i];
            // 전체 돌 위치 재배치 (기존 4개 포함)
            s.Rb.linearVelocity = Vector3.zero;
            s.Rb.angularVelocity = Vector3.zero;
            s.transform.position = gridPositions[i];
            s.SetColor(colorAssign[i]);
        }

        Debug.Log($"[ColorSelectGimmick] 20 stones distributed: Yellow={yellowTotal}, Red={redTotal}, Green={greenTotal}. Target={targetColor}, Board={allBoardStones.Count}.");
    }

    public override bool ValidatePick(Stone stone)
    {
        if (!targetConfirmed) return true; // 타겟 미확정 = 던지기 전 단계 → 허용
        bool valid = stone.Color == targetColor;
        if (!valid)
        {
            Debug.Log($"[ColorSelectGimmick] ValidatePick failed: stone={stone.StoneIndex} color={stone.Color}, target={targetColor}");
            TestLogger.Instance?.LogFailure("color_mismatch");
        }
        return valid;
    }

    /// <summary>
    /// 클리어 판정: 기존 룰대로 라운드 수로 판정 (1단=4회, 2단=2회, 3단=2회, 4단=1회).
    /// "바닥에 돌 0개" 대신 "라운드 횟수" 기준.
    /// </summary>
    public override bool IsRoundComplete(int pickedThisRound, int remainingOnBoard)
    {
        completedRounds++;
        int currentStage = gameManager != null ? gameManager.CurrentStage : 1;
        bool complete = (completedRounds >= GetRequiredRounds(currentStage));
        Debug.Log($"[ColorSelectGimmick] Round {completedRounds}, stage={currentStage}, complete={complete}");
        return complete;
    }

    /// <summary>단별 필요 라운드 수</summary>
    private int GetRequiredRounds(int stageInLoop)
    {
        return stageInLoop switch
        {
            1 => 4, // 1개×4회
            2 => 2, // 2개×2회
            3 => 2, // 3+1 or 1+3 = 2회
            4 => 1, // 4개×1회
            _ => 4
        };
    }

    public override void OnStageEnd()
    {
        // 추가 돌 비활성화
        if (additionalStones != null && StonePool.Instance != null)
        {
            StonePool.Instance.DeactivateStones(additionalStones);
            additionalStones = null;
        }

        // 전체 색상 리셋 (비활성 돌은 Activate 시 자동 리셋되므로 활성 돌만)
        var pool = StonePool.Instance;
        if (pool != null)
        {
            foreach (var s in pool.ActiveStones)
                s.ResetColorAndFake();
        }

        targetColor = Stone.StoneColor.Default;
        targetConfirmed = false;
        Debug.Log("[ColorSelectGimmick] Stage ended: colors reset.");
    }
}
