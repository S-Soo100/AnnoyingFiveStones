using UnityEngine;

/// <summary>
/// Stage 8 [45살] 노화된 손 기믹.
/// 손 이동 속도 제한 + 입력 감쇠 + 잔상 효과.
/// </summary>
public class AgedHandGimmick : StageGimmick
{
    private const float MaxSpeed = 8f;        // 초당 월드 유닛 (밸런싱 대상)
    private const float SmoothFactor = 5f;    // 감쇠 계수 (밸런싱 대상)
    private const float Stage5MaxSpeed = 12f;  // 5단 꺾기: 약간만 느리게
    private const float Stage5SmoothFactor = 3f;

    private HandController handController;
    private HandGhostPool ghostPool;

    public override void OnStageStart(int stageInLoop)
    {
        handController = gameManager?.GetComponentInChildren<HandController>();
        if (handController == null)
            handController = Object.FindFirstObjectByType<HandController>();
        if (handController == null) return;

        // 속도 제한 + 감쇠 설정
        handController.SetMoveSpeedOverride(MaxSpeed);
        handController.SetMoveSmoothOverride(SmoothFactor);

        // 잔상 풀 생성/활성화
        var existing = Object.FindFirstObjectByType<HandGhostPool>();
        if (existing != null)
        {
            ghostPool = existing;
        }
        else
        {
            var go = new GameObject("HandGhostPool");
            ghostPool = go.AddComponent<HandGhostPool>();
        }
        ghostPool.Activate();
        handController.SetGhostPool(ghostPool);

        Debug.Log($"[AgedHandGimmick] Stage {stageInLoop} started: maxSpeed={MaxSpeed}, smooth={SmoothFactor}");
    }

    public override void OnStageEnd()
    {
        // 5단 꺾기 전환인지 판별: OnStageEnd 호출 시점에 currentStage는 이미 다음 단 번호
        bool goingToStage5 = gameManager != null && gameManager.CurrentStage == 5;

        if (goingToStage5)
        {
            // 5단 꺾기: 잔상 유지 + 속도 제한 완화 (기획: 시각효과 유지, 약간 느리게)
            if (handController != null)
            {
                handController.SetMoveSpeedOverride(Stage5MaxSpeed);
                handController.SetMoveSmoothOverride(Stage5SmoothFactor);
                // ghostPool 유지 — 잔상 계속 표시
            }
            Debug.Log("[AgedHandGimmick] Transitioning to stage 5: partial effects kept.");
        }
        else
        {
            // 진짜 스테이지 종료: 전부 해제
            if (handController != null)
            {
                handController.SetMoveSpeedOverride(-1f);
                handController.SetMoveSmoothOverride(-1f);
                handController.SetGhostPool(null);
            }

            if (ghostPool != null)
            {
                ghostPool.Cleanup();
            }

            Debug.Log("[AgedHandGimmick] Stage ended: overrides cleared.");
        }
    }
}
