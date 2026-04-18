using UnityEngine;

/// <summary>
/// Stage 2 [15살] 색깔 선택 기믹 v5.
/// 뿌리기 시점에 18개(3색×6)를 배치. 던진 후 추가 스폰/위치 셔플 없음.
/// 타겟 색 = 던진 돌의 색 (노랑 고정 제거).
/// </summary>
public class ColorSelectGimmick : StageGimmick
{
    private Stone.StoneColor targetColor = Stone.StoneColor.Default;
    private bool targetConfirmed = false;
    private int completedRounds = 0; // 성공한 줍기-받기 라운드 수

    private static readonly Stone.StoneColor[] allColors = new Stone.StoneColor[]
    {
        Stone.StoneColor.Yellow,
        Stone.StoneColor.Red,
        Stone.StoneColor.Green,
    };

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

    public override void OnStageStart(int stageInLoop)
    {
        targetColor = Stone.StoneColor.Default;
        targetConfirmed = false;
        completedRounds = 0;

        // 18개 돌에 3색 균등 배정: Yellow 6, Red 6, Green 6
        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        // 색 배열 생성: 각 6개씩
        var colorAssign = new Stone.StoneColor[active.Length];
        int perColor = active.Length / allColors.Length; // 6
        int idx = 0;
        foreach (var color in allColors)
        {
            for (int i = 0; i < perColor && idx < colorAssign.Length; i++)
                colorAssign[idx++] = color;
        }
        // 나머지 남은 슬롯(18이 3으로 나누어 떨어지므로 없음, 방어 처리)
        while (idx < colorAssign.Length)
            colorAssign[idx++] = allColors[(idx - 1) % allColors.Length];

        // Fisher-Yates 셔플
        for (int i = colorAssign.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (colorAssign[i], colorAssign[j]) = (colorAssign[j], colorAssign[i]);
        }

        // 배정 적용
        for (int i = 0; i < active.Length; i++)
            active[i].SetColor(colorAssign[i]);

        Debug.Log($"[ColorSelectGimmick] Stage started: 3 colors assigned to {active.Length} stones (Yellow={perColor}, Red={perColor}, Green={perColor}).");
    }

    /// <summary>
    /// 던진 돌의 색을 타겟으로 확정. 추가 스폰/위치 재배치 없음.
    /// HandController.DoThrow → GameManager.NotifyThrowStart → 여기 호출됨.
    /// </summary>
    public override void OnThrowStart(Stone thrownStone)
    {
        if (targetConfirmed) return;

        // 타겟 색상 = 던진 돌의 실제 색
        targetColor = thrownStone.Color;
        targetConfirmed = true;
        Debug.Log($"[ColorSelectGimmick] Target color confirmed: {targetColor} (from thrown stone)");

        // UI 안내
        GameUI.Instance?.UpdateGuideText($"[ {GetColorName(targetColor)}만 주우세요! ]");
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
    /// 클리어 판정: 라운드 횟수 기준 (1단=4회, 2단=2회, 3단=2회, 4단=1회).
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
        // 전체 색상 리셋 (활성 돌만)
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
