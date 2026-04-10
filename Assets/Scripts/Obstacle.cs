using UnityEngine;

/// <summary>
/// Stage 6 [35살] 방해물 컴포넌트.
/// 고정형(Static) 또는 왕복 이동형(Moving).
/// 거리 기반으로 손과의 충돌 감지 (물리 레이어 문제 회피).
/// shape == Elongated 일 때는 점-선분 최단거리로 감지 (볼펜 양 끝 커버).
/// </summary>

public enum ObstacleShape { Point, Elongated }

public class Obstacle : MonoBehaviour
{
    public enum ObstacleType { Static, Moving }

    [Header("Type")]
    public ObstacleType type = ObstacleType.Static;
    public ObstacleShape shape = ObstacleShape.Point;

    [Header("Moving Settings")]
    public Vector3 startPos;
    public Vector3 endPos;
    public float moveSpeed = 0.75f;

    [Header("Collision")]
    public float hitRadius = 0.5f;

    // Elongated(볼펜) 전용: 로컬 좌표 양 끝점
    // Cylinder 로컬 Y축이 높이 방향이므로 ±1이 scale Y 배수만큼 늘어남
    [HideInInspector] public Vector3 localEndA;
    [HideInInspector] public Vector3 localEndB;

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
            float dist01 = Vector3.Distance(startPos, endPos);
            if (dist01 > 0.001f)
            {
                float delta = moveSpeed * Time.deltaTime / dist01;
                moveProgress += goingForward ? delta : -delta;

                if (moveProgress >= 1f) { moveProgress = 1f; goingForward = false; }
                else if (moveProgress <= 0f) { moveProgress = 0f; goingForward = true; }

                transform.position = Vector3.Lerp(startPos, endPos, moveProgress);
            }
        }

        // 손과의 거리 기반 충돌 감지
        if (failed) return;
        if (handController == null) return;

        // IsOnBoard 대신 Phase 기반 감지
        // PickStones/PickThrowStone 단계(돌 줍는 중)에서만 감지
        var phase = GameManager.Instance?.CurrentPhase ?? GameManager.GamePhase.Scatter;
        bool shouldCheck = (phase == GameManager.GamePhase.PickStones
                         || phase == GameManager.GamePhase.PickThrowStone);
        if (!shouldCheck) return;

        Vector2 handPos = new Vector2(
            handController.transform.position.x,
            handController.transform.position.y);

        float distance;
        if (shape == ObstacleShape.Elongated)
        {
            // 로컬 끝점을 월드 좌표로 변환 후 XY 평면 거리 계산
            Vector2 worldA = transform.TransformPoint(localEndA);
            Vector2 worldB = transform.TransformPoint(localEndB);
            distance = DistanceToSegment(handPos, worldA, worldB);
        }
        else
        {
            distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                handPos);
        }

        if (distance < hitRadius)
        {
            failed = true;
            Debug.Log($"[Obstacle] Hand hit obstacle '{gameObject.name}' at dist={distance:F2}");
            TestLogger.Instance?.LogFailure("obstacle_collision");
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetFailReason("방해물에 걸렸다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
            }
        }
    }

    /// <summary>2D 점과 선분 사이의 최단 거리</summary>
    private float DistanceToSegment(Vector2 point, Vector2 segA, Vector2 segB)
    {
        Vector2 ab = segB - segA;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen < 0.0001f) return Vector2.Distance(point, segA);
        float t = Mathf.Clamp01(Vector2.Dot(point - segA, ab) / sqrLen);
        Vector2 closest = segA + t * ab;
        return Vector2.Distance(point, closest);
    }
}
