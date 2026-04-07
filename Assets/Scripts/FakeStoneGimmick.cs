using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 5 [30살] 분신 가짜 잡기 기믹.
/// 던지기 시작 시 추가 15개 활성화, 그 중 일부를 가짜로 설정 + ProximityReveal 추가.
/// 가짜 돌을 집으면 즉시 실패.
/// </summary>
public class FakeStoneGimmick : StageGimmick
{
    private Stone[] additionalStones;
    private List<ProximityReveal> revealers = new List<ProximityReveal>();
    private int completedRounds = 0;

    public override void OnThrowStart(Stone thrownStone)
    {
        var pool = StonePool.Instance;
        if (pool == null) return;

        // 추가 15개 활성화
        additionalStones = pool.ActivateAdditional(15);

        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 3.5f;
        float halfH = 2.8f;

        // 추가 15개 OnBoard 상태로 설정
        foreach (var s in additionalStones)
        {
            s.SetState(Stone.State.OnBoard);
            s.Rb.linearVelocity = Vector3.zero;
            s.Rb.angularVelocity = Vector3.zero;
        }

        // 진짜 4개(기존 OnBoard) + 가짜 15개 = 19개 전부 위치 랜덤 재배치
        // → 원래 돌 위치를 기억해도 소용없게 만듦
        var allActive = pool.ActiveStones;
        foreach (var s in allActive)
        {
            if (s == thrownStone) continue; // 던진 돌은 공중에 있으므로 제외
            if (s.CurrentState != Stone.State.OnBoard) continue;

            float rx = cx + Random.Range(-halfW, halfW);
            float ry = cy + Random.Range(-halfH, halfH);
            s.transform.position = new Vector3(rx, ry, 0f);
            s.Rb.linearVelocity = Vector3.zero;
            s.Rb.angularVelocity = Vector3.zero;
        }

        // 15개를 모두 가짜로 설정 + ProximityReveal 추가
        revealers.Clear();
        foreach (var s in additionalStones)
        {
            s.SetFake(true);
            var reveal = s.gameObject.AddComponent<ProximityReveal>();
            revealers.Add(reveal);
        }

        Debug.Log($"[FakeStoneGimmick] {additionalStones.Length} fake stones spawned. All 19 positions randomized.");
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

        // 추가 돌 비활성화
        if (additionalStones != null && StonePool.Instance != null)
        {
            StonePool.Instance.DeactivateStones(additionalStones);
            additionalStones = null;
        }

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
