using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 3 [20살] 도망가는 공기 기믹.
/// 첫 번째 줍기 시 나머지 OnBoard 돌에 FleeMovement를 붙여 도망가게 함.
/// </summary>
public class FleeGimmick : StageGimmick
{
    private bool fleeTriggered = false;
    private List<FleeMovement> activeFleers = new List<FleeMovement>();

    public override void OnStageStart(int stageInLoop)
    {
        fleeTriggered = false;
        activeFleers.Clear();
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        if (fleeTriggered) return;
        fleeTriggered = true;

        // 보드 경계 Rect 계산
        Rect boardRect = GetBoardRect();

        // 던진 돌을 제외한 나머지 OnBoard 돌에 FleeMovement 추가
        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        float delay = 0f;
        foreach (var s in active)
        {
            if (s == thrownStone) continue;
            if (s.CurrentState != Stone.State.OnBoard) continue;
            if (!s.gameObject.activeSelf) continue;

            var flee = s.gameObject.AddComponent<FleeMovement>();
            flee.boardBounds = BoardBounds.InnerRect(0.05f); // 5% 안쪽 마진 (SOT 사용)
            flee.Activate(delay);
            activeFleers.Add(flee);
            delay += 0.2f; // 0.2초 시간차
        }

        Debug.Log($"[FleeGimmick] {activeFleers.Count} stones started fleeing.");
        TestLogger.Instance?.LogPhysics("flee_triggered", $"{activeFleers.Count} stones fleeing after throw: {thrownStone.StoneIndex}");
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
        fleeTriggered = false;
        Debug.Log("[FleeGimmick] Stage ended: FleeMovement components removed.");
    }

    private Rect GetBoardRect()
    {
        if (gameManager == null || gameManager.BoardTransform == null)
            return new Rect(-4.8f, -8.3f, 9.6f, 6.1f);

        var boardPos = gameManager.BoardTransform.position;
        float halfW = 4.8f;
        float halfH = 3.05f;
        return new Rect(
            boardPos.x - halfW,
            boardPos.y - halfH,
            halfW * 2f,
            halfH * 2f
        );
    }
}
