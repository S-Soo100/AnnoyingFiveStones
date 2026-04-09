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

        targetColor = thrownStone.Color;
        targetConfirmed = true;
        Debug.Log($"[ColorSelectGimmick] Target color confirmed: {targetColor}");

        // UI 안내: 타겟 색 표시
        GameUI.Instance?.UpdateGuideText($"[ {GetColorName(targetColor)}만 주우세요! ]");

        // 추가 15개 활성화
        var pool = StonePool.Instance;
        if (pool == null) return;

        additionalStones = pool.ActivateAdditional(15);

        // 전체 보드 돌 수집 (기존 4 + 추가 15 = 19개)
        var allBoardStones = new System.Collections.Generic.List<Stone>();
        foreach (var s in pool.ActiveStones)
        {
            if (s.CurrentState == Stone.State.OnBoard)
                allBoardStones.Add(s);
        }

        // 고정 배분: 노랑 7, 빨강 6, 초록 7 = 20 (던진 돌 1 포함)
        // 보드 위 19개 + 던진 돌 1개(타겟) = 20
        // 타겟 색의 보드 할당 = 전체 할당 - 1(던진 돌)
        int yellowTotal = 7, redTotal = 6, greenTotal = 7;
        int yellowBoard, redBoard, greenBoard;

        if (targetColor == Stone.StoneColor.Yellow)
            { yellowBoard = yellowTotal - 1; redBoard = redTotal; greenBoard = greenTotal; }
        else if (targetColor == Stone.StoneColor.Red)
            { yellowBoard = yellowTotal; redBoard = redTotal - 1; greenBoard = greenTotal; }
        else // Green
            { yellowBoard = yellowTotal; redBoard = redTotal; greenBoard = greenTotal - 1; }

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

        // 추가 돌 위치 + 전체 색 배분
        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 3.5f;
        float halfH = 2.8f;

        for (int i = 0; i < allBoardStones.Count; i++)
        {
            var s = allBoardStones[i];
            // 추가 돌은 위치 재배치
            if (System.Array.IndexOf(additionalStones, s) >= 0)
            {
                s.SetState(Stone.State.OnBoard);
                s.Rb.linearVelocity = Vector3.zero;
                s.Rb.angularVelocity = Vector3.zero;
                s.transform.position = new Vector3(
                    cx + Random.Range(-halfW, halfW),
                    cy + Random.Range(-halfH, halfH), 0f);
            }
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
