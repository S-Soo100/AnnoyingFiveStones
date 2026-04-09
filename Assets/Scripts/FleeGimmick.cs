using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 3 [20살] 도망가는 공기 기믹.
/// 첫 번째 줍기 시 나머지 OnBoard 돌에 FleeMovement를 붙여 도망가게 함.
/// </summary>
public class FleeGimmick : StageGimmick
{
    private bool firstPickDone = false;
    private List<FleeMovement> activeFleers = new List<FleeMovement>();

    public override void OnStageStart(int stageInLoop)
    {
        firstPickDone = false;
        activeFleers.Clear();
    }

    public override void OnStonePicked(Stone stone)
    {
        if (firstPickDone) return;
        firstPickDone = true;

        // 보드 경계 Rect 계산
        Rect boardRect = GetBoardRect();

        // 나머지 OnBoard 돌에 FleeMovement 추가
        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        float delay = 0f;
        foreach (var s in active)
        {
            if (s == stone) continue;
            if (s.CurrentState != Stone.State.OnBoard) continue;
            if (!s.gameObject.activeSelf) continue;

            var flee = s.gameObject.AddComponent<FleeMovement>();
            flee.boardBounds = boardRect;
            flee.Activate(delay);
            activeFleers.Add(flee);
            delay += 0.2f; // 0.2초 시간차
        }

        Debug.Log($"[FleeGimmick] {activeFleers.Count} stones started fleeing.");
        TestLogger.Instance?.LogPhysics("flee_triggered", $"{activeFleers.Count} stones fleeing after first pick: {stone.StoneIndex}");
    }

    public override void OnStageEnd()
    {
        // FleeMovement 컴포넌트 모두 제거 (OnDestroy에서 isKinematic 복원됨)
        foreach (var flee in activeFleers)
        {
            if (flee != null)
                Object.Destroy(flee);
        }
        activeFleers.Clear();
        firstPickDone = false;
        Debug.Log("[FleeGimmick] Stage ended: FleeMovement components removed.");
    }

    private Rect GetBoardRect()
    {
        if (gameManager == null || gameManager.BoardTransform == null)
            return new Rect(-4f, -12f, 8f, 6.4f);

        var boardPos = gameManager.BoardTransform.position;
        float halfW = 4f;
        float halfH = 3.2f;
        return new Rect(
            boardPos.x - halfW,
            boardPos.y - halfH,
            halfW * 2f,
            halfH * 2f
        );
    }
}
