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
        Stone.StoneColor.Red,
        Stone.StoneColor.Blue,
        Stone.StoneColor.Yellow,
        Stone.StoneColor.Green,
        Stone.StoneColor.Purple,
    };

    public override void OnStageStart(int stageInLoop)
    {
        targetColor = Stone.StoneColor.Default;
        targetConfirmed = false;
        additionalStones = null;
        completedRounds = 0;

        // 5개 돌에 각각 다른 색 배정 (Red, Blue, Yellow, Green, Purple)
        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        for (int i = 0; i < active.Length && i < allColors.Length; i++)
        {
            active[i].SetColor(allColors[i]);
        }
        Debug.Log("[ColorSelectGimmick] Stage started: 5 colors assigned.");
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

        // 추가 돌을 보드 위 무작위 위치에 배치
        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 3.5f;
        float halfH = 2.8f;

        foreach (var s in additionalStones)
        {
            s.SetState(Stone.State.OnBoard);
            s.Rb.linearVelocity = Vector3.zero;
            s.Rb.angularVelocity = Vector3.zero;
            float rx = cx + Random.Range(-halfW, halfW);
            float ry = cy + Random.Range(-halfH, halfH);
            s.transform.position = new Vector3(rx, ry, 0f);
        }

        // 추가 15개에만 색 배분 (기존 4개는 원래 색 유지 = 방해 색 역할)
        // T: 추가 돌 중 타겟 색 개수 (최소 4, 최대 10)
        int T = Random.Range(4, 11);
        int remaining = additionalStones.Length - T; // 방해 색 개수
        int D1 = remaining > 1 ? Random.Range(1, remaining) : remaining;
        int D2 = remaining - D1;

        // 방해 색상 2개 선택 (타겟 제외 4색 중 랜덤 2개)
        var distractorColors = new Stone.StoneColor[2];
        // 셔플된 순서로 선택
        var candidates = new System.Collections.Generic.List<Stone.StoneColor>();
        foreach (var c in allColors)
        {
            if (c != targetColor) candidates.Add(c);
        }
        // 랜덤 2개
        int pick1 = Random.Range(0, candidates.Count);
        distractorColors[0] = candidates[pick1];
        candidates.RemoveAt(pick1);
        distractorColors[1] = candidates[Random.Range(0, candidates.Count)];

        // 색 배열 준비 (추가 15개용)
        var colorAssign = new Stone.StoneColor[additionalStones.Length];
        int idx = 0;
        for (int i = 0; i < T && idx < colorAssign.Length; i++) colorAssign[idx++] = targetColor;
        for (int i = 0; i < D1 && idx < colorAssign.Length; i++) colorAssign[idx++] = distractorColors[0];
        while (idx < colorAssign.Length) colorAssign[idx++] = distractorColors[1];

        // Fisher-Yates 셔플
        for (int i = colorAssign.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (colorAssign[i], colorAssign[j]) = (colorAssign[j], colorAssign[i]);
        }

        // 추가 15개에만 배분
        for (int i = 0; i < additionalStones.Length; i++)
        {
            additionalStones[i].SetColor(colorAssign[i]);
        }

        Debug.Log($"[ColorSelectGimmick] Additional 15 distributed: T={T}({targetColor}), D1={D1}({distractorColors[0]}), D2={D2}({distractorColors[1]}). Original 4 kept.");
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
