using UnityEngine;

/// <summary>
/// Stage 6 [35살] 방해물 컴포넌트.
/// 고정형(Static) 또는 왕복 이동형(Moving).
/// 거리 기반으로 손과의 충돌 감지 (물리 레이어 문제 회피).
/// </summary>
public class Obstacle : MonoBehaviour
{
    public enum ObstacleType { Static, Moving }

    [Header("Type")]
    public ObstacleType type = ObstacleType.Static;

    [Header("Moving Settings")]
    public Vector3 startPos;
    public Vector3 endPos;
    public float moveSpeed = 0.75f;

    [Header("Collision")]
    public float hitRadius = 0.5f; // 손과의 충돌 거리 임계값

    // OnStageStart에서 설정
    [HideInInspector] public bool onlyCheckOnBoard = true; // 보드 위에서만 감지

    private float moveProgress = 0f;
    private bool goingForward = true;
    private HandController handController;
    private bool failed = false;

    private void Start()
    {
        handController = Object.FindFirstObjectByType<HandController>();
    }

    private void Update()
    {
        // 이동형: startPos ↔ endPos 왕복
        if (type == ObstacleType.Moving)
        {
            float delta = moveSpeed * Time.deltaTime / Vector3.Distance(startPos, endPos);
            moveProgress += goingForward ? delta : -delta;

            if (moveProgress >= 1f) { moveProgress = 1f; goingForward = false; }
            else if (moveProgress <= 0f) { moveProgress = 0f; goingForward = true; }

            transform.position = Vector3.Lerp(startPos, endPos, moveProgress);
        }

        // 손과의 거리 기반 충돌 감지
        if (failed) return;
        if (handController == null) return;

        // 보드 위(IsOnBoard)에서만 감지
        if (onlyCheckOnBoard && !handController.IsOnBoard) return;

        float dist = Vector3.Distance(
            new Vector3(transform.position.x, transform.position.y, 0f),
            new Vector3(handController.transform.position.x, handController.transform.position.y, 0f)
        );

        if (dist < hitRadius)
        {
            failed = true;
            Debug.Log($"[Obstacle] Hand hit obstacle '{gameObject.name}' at dist={dist:F2}");
            TestLogger.Instance?.LogFailure("obstacle_collision");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetFailReason("방해물에 걸렸다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
            }
        }
    }
}
