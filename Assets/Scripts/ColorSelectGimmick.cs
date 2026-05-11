using UnityEngine;

/// <summary>
/// Stage 2 [15살] 색깔 선택 기믹 v6-split.
/// 시작 시 5개만 활성화(낙 확률 감소). ScatterComplete 후 나머지 13개를 매트 위 빈 곳에 배치.
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

        // 18개 중 5개만 활성화 (낙 확률 감소)
        // ResetAllStones()가 이미 18개를 Activate했으므로 DeactivateAll → Activate(5) 재설정
        var pool = StonePool.Instance;
        if (pool == null) return;

        pool.DeactivateAll();
        var active = pool.Activate(5);

        // 5개 색 배정: 노랑2, 빨강1, 초록2
        // [0]=Yellow, [1]=Yellow, [2]=Red, [3]=Green, [4]=Green
        active[0].SetColor(Stone.StoneColor.Yellow);
        active[1].SetColor(Stone.StoneColor.Yellow);
        active[2].SetColor(Stone.StoneColor.Red);
        active[3].SetColor(Stone.StoneColor.Green);
        active[4].SetColor(Stone.StoneColor.Green);

        // GameManager.stones를 5개로 갱신
        GameManager.Instance?.RefreshStones();

        Debug.Log("[ColorSelectGimmick] Stage started: 5 stones activated (Yellow=2, Red=1, Green=2). Remaining 13 spawn after scatter.");
    }

    /// <summary>
    /// 5개가 매트에 정착한 뒤 자동 호출. 나머지 13개를 매트 안 빈 곳에 배치.
    /// </summary>
    public override void OnScatterComplete(Stone[] activeStones)
    {
        var pool = StonePool.Instance;
        if (pool == null) return;

        // 1) 13개 추가 활성화
        var added = pool.ActivateAdditional(13);
        if (added == null || added.Length == 0) return;

        // 2) 색 배정: 노4 빨5 초4 (5+13=18, 합계 노6 빨6 초6)
        var colorSequence = new Stone.StoneColor[]
        {
            Stone.StoneColor.Yellow, Stone.StoneColor.Yellow,
            Stone.StoneColor.Yellow, Stone.StoneColor.Yellow,
            Stone.StoneColor.Red,    Stone.StoneColor.Red,
            Stone.StoneColor.Red,    Stone.StoneColor.Red,
            Stone.StoneColor.Red,
            Stone.StoneColor.Green,  Stone.StoneColor.Green,
            Stone.StoneColor.Green,  Stone.StoneColor.Green,
        };
        for (int i = 0; i < added.Length && i < colorSequence.Length; i++)
            added[i].SetColor(colorSequence[i]);

        // 3) Z 기준점: 기존 5개 중 첫 번째 돌의 Z값 참조
        float boardZ = (activeStones != null && activeStones.Length > 0)
            ? activeStones[0].transform.position.z
            : 0f;

        // 4) 위치 배치: InnerRect(0.1f) 안에서 그리드+jitter 방식
        //    4×4=16 슬롯 중 13개 랜덤 선택, 기존 5개와 최소 거리 0.35f 체크
        var inner = BoardBounds.InnerRect(0.1f);
        int cols = 4, rows = 4;
        float cellW = inner.width  / cols;
        float cellH = inner.height / rows;

        // 슬롯 인덱스 셔플
        var slotIndices = new int[cols * rows];
        for (int i = 0; i < slotIndices.Length; i++) slotIndices[i] = i;
        for (int i = slotIndices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slotIndices[i], slotIndices[j]) = (slotIndices[j], slotIndices[i]);
        }

        const float minDist = 0.35f;
        int placed = 0;
        for (int si = 0; si < slotIndices.Length && placed < added.Length; si++)
        {
            int slot = slotIndices[si];
            int col = slot % cols;
            int row = slot / cols;

            // 셀 중심 + jitter
            float x = inner.xMin + (col + 0.5f) * cellW + Random.Range(-cellW * 0.3f, cellW * 0.3f);
            float y = inner.yMin + (row + 0.5f) * cellH + Random.Range(-cellH * 0.3f, cellH * 0.3f);

            // 기존 5개와 거리 체크
            bool tooClose = false;
            if (activeStones != null)
            {
                foreach (var s in activeStones)
                {
                    float dx = s.transform.position.x - x;
                    float dy = s.transform.position.y - y;
                    if (dx * dx + dy * dy < minDist * minDist) { tooClose = true; break; }
                }
            }
            if (tooClose) continue;

            var stone = added[placed];
            var rb = stone.Rb;

            // 물리 안정화: Kinematic → 이동 → 복원
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            stone.transform.position = new Vector3(x, y, boardZ);
            rb.linearVelocity   = Vector3.zero;
            rb.angularVelocity  = Vector3.zero;
            rb.isKinematic = wasKinematic;

            stone.SetState(Stone.State.OnBoard);
            placed++;
        }

        // 슬롯이 부족해 배치 못한 돌은 inner 중심에 fallback
        Vector3 center = new Vector3(inner.x + inner.width * 0.5f, inner.y + inner.height * 0.5f, boardZ);
        for (int i = placed; i < added.Length; i++)
        {
            var stone = added[i];
            var rb = stone.Rb;
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            stone.transform.position = center + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0f);
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = wasKinematic;
            stone.SetState(Stone.State.OnBoard);
        }

        // 5) GameManager.stones를 18개로 갱신 (CatchSystem 등이 전체 돌을 인식하도록)
        GameManager.Instance?.RefreshStones();

        Debug.Log($"[ColorSelectGimmick] OnScatterComplete: +{added.Length} stones placed ({placed} grid, {added.Length - placed} fallback). Total active={pool.ActiveStones.Length}.");
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
