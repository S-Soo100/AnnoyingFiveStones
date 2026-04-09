using System.Collections;
using UnityEngine;

/// <summary>
/// Stage 3 [20살] 도망가는 돌 개별 이동 컴포넌트.
/// FleeGimmick이 OnBoard 돌에 AddComponent.
/// </summary>
[RequireComponent(typeof(Stone))]
public class FleeMovement : MonoBehaviour
{
    public enum FleeState { Idle, Flee, Rest }

    [Header("Settings")]
    public Rect boardBounds; // FleeGimmick에서 설정

    private FleeState state = FleeState.Idle;
    private Rigidbody rb;
    private Stone stone;

    private Vector2 fleeDirection;
    private float fleeSpeed;
    private bool originalKinematic;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stone = GetComponent<Stone>();
    }

    private void OnDestroy()
    {
        // 컴포넌트 제거 시 Kinematic 원상 복구
        if (rb != null)
        {
            rb.isKinematic = originalKinematic;
            rb.linearVelocity = Vector3.zero;
        }
    }

    /// <summary>delay초 후 도망 시작</summary>
    public void Activate(float delay)
    {
        originalKinematic = rb != null ? rb.isKinematic : false;
        Debug.Log($"[FleeMovement] {gameObject.name} activated with delay {delay}s");
        StartCoroutine(ActivateAfterDelay(delay));
    }

    private IEnumerator ActivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        state = FleeState.Flee;
        StartCoroutine(FleeRestLoop());
    }

    private IEnumerator FleeRestLoop()
    {
        while (true)
        {
            // ─── Flee 상태 ───
            state = FleeState.Flee;
            Debug.Log($"[FleeMovement] {gameObject.name} → Flee");
            fleeDirection = Random.insideUnitCircle.normalized;
            fleeSpeed = Random.Range(3f, 6f);
            float fleeDuration = Random.Range(1.5f, 2f);

            // Kinematic으로 전환 (다른 돌을 밀지 않도록)
            if (rb != null) rb.isKinematic = true;

            float elapsed = 0f;
            while (elapsed < fleeDuration)
            {
                elapsed += Time.fixedDeltaTime;

                // 보드 경계 확인 → 벽에 닿으면 방향 반전
                Vector3 pos = transform.position;
                Vector3 next = pos + (Vector3)(fleeDirection * fleeSpeed * Time.fixedDeltaTime);

                if (next.x < boardBounds.xMin || next.x > boardBounds.xMax)
                    fleeDirection.x = -fleeDirection.x;
                if (next.y < boardBounds.yMin || next.y > boardBounds.yMax)
                    fleeDirection.y = -fleeDirection.y;

                // 경계 클램프
                Vector3 move = pos + (Vector3)(fleeDirection * fleeSpeed * Time.fixedDeltaTime);
                move.x = Mathf.Clamp(move.x, boardBounds.xMin, boardBounds.xMax);
                move.y = Mathf.Clamp(move.y, boardBounds.yMin, boardBounds.yMax);
                move.z = 0f;

                if (rb != null)
                    rb.MovePosition(move);

                yield return new WaitForFixedUpdate();
            }

            // ─── Rest 상태 ───
            state = FleeState.Rest;
            Debug.Log($"[FleeMovement] {gameObject.name} → Rest");
            if (rb != null) rb.linearVelocity = Vector3.zero;
            float restDuration = Random.Range(1f, 1.5f);
            yield return new WaitForSeconds(restDuration);
        }
    }

    public FleeState CurrentState => state;
}
