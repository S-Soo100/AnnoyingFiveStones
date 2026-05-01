using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 5 [30살] 분신 가짜 잡기 기믹.
/// 뿌리기부터 10개 배치 (진짜 5개 인덱스 0~4, 가짜 5개 인덱스 5~9).
/// 가짜 돌을 집으면 즉시 실패.
/// </summary>
public class FakeStoneGimmick : StageGimmick
{
    private List<ProximityReveal> revealers = new List<ProximityReveal>();
    private int completedRounds = 0;

    public override void OnStageStart(int stageInLoop)
    {
        var pool = StonePool.Instance;
        if (pool == null) return;

        var active = pool.ActiveStones; // 10개 (StageConfig.TotalStones=10)

        revealers.Clear();
        completedRounds = 0;

        // StoneIndex >= 5인 돌 = 가짜
        foreach (var s in active)
        {
            if (s.StoneIndex >= 5)
            {
                s.SetFake(true);
                var reveal = s.gameObject.AddComponent<ProximityReveal>();
                revealers.Add(reveal);
            }
        }

        Debug.Log($"[FakeStoneGimmick] OnStageStart: {revealers.Count} fake stones marked. Total active={active.Length}");
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        // 빈 본문 — 던지기 후 추가 스폰/위치 셔플 없음
    }

    public override bool ValidatePick(Stone stone)
    {
        // 가짜 돌이면 즉시 실패
        if (stone.IsFake)
        {
            Debug.Log($"[FakeStoneGimmick] ValidatePick failed: fake stone picked (index={stone.StoneIndex})");
            TestLogger.Instance?.LogFailure("fake_stone_picked");
            return false;
        }
        return true;
    }

    public override bool IsRoundComplete(int pickedThisRound, int remainingOnBoard)
    {
        completedRounds++;
        int currentStage = gameManager != null ? gameManager.CurrentStage : 1;
        int requiredRounds = currentStage switch
        {
            1 => 4, 2 => 2, 3 => 2, 4 => 1, _ => 4
        };
        bool complete = completedRounds >= requiredRounds;
        Debug.Log($"[FakeStoneGimmick] Round {completedRounds}/{requiredRounds}, complete={complete}");
        return complete;
    }

    public override void OnStageEnd()
    {
        completedRounds = 0;
        // ProximityReveal 제거
        foreach (var reveal in revealers)
        {
            if (reveal != null)
                Object.Destroy(reveal);
        }
        revealers.Clear();

        // 활성 돌 isFake 리셋
        var pool = StonePool.Instance;
        if (pool != null)
        {
            foreach (var s in pool.ActiveStones)
                s.ResetColorAndFake();
        }

        Debug.Log("[FakeStoneGimmick] Stage ended: fake stones removed.");
    }
}
