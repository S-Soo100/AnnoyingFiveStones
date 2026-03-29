using UnityEngine;

public class Stone : MonoBehaviour
{
    public enum State
    {
        OnBoard,    // 보드 위에 놓여있음
        InHand,     // 손에 잡힘
        InAir,      // 공중에 던져짐
        Caught,     // 받기 성공
        Bouncing    // 손가락에 튕겨 아직 공중, 바닥 닿으면 탈락
    }

    [Header("State")]
    [SerializeField] private State currentState = State.OnBoard;

    private Rigidbody rb;
    private Collider col;
    private int stoneIndex;

    public State CurrentState => currentState;
    public Rigidbody Rb => rb;
    public int StoneIndex => stoneIndex;

    // 레이어 상수: InAir/Bouncing 돌과 손은 layer 8, OnBoard 돌은 Default(0)
    // Layer 8 ↔ Default(0) 충돌 비활성 → 공중 돌이 보드 돌을 밀지 않음
    // Layer 8 ↔ Layer 8 충돌 활성 → 공중 돌끼리 + 공중 돌↔손 충돌 가능
    public const int AirLayer = 8; // User Layer 8 (비어있는 레이어)

    private static bool layerCollisionConfigured;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        // 빠른 낙하 시 터널링 방지
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // 2.5D: Z축 이동/회전 고정
        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY;

        // Physics Layer 충돌 설정 (1회만)
        if (!layerCollisionConfigured)
        {
            // AirLayer(8) ↔ Default(0) 충돌 비활성 — 공중 돌이 보드 돌 밀지 않음
            Physics.IgnoreLayerCollision(AirLayer, 0, true);
            // AirLayer ↔ AirLayer 충돌 활성 (기본값이 활성이므로 설정 불필요)
            layerCollisionConfigured = true;
        }
    }

    public void Initialize(int index)
    {
        stoneIndex = index;
    }

    public void SetState(State newState)
    {
        currentState = newState;

        switch (newState)
        {
            case State.OnBoard:
                // 보드 위 = 탁자 위에서 내려다보는 시점. 중력 없이 마찰로 정지.
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.linearDamping = 3f;
                rb.angularDamping = 5f; // 회전 억제 → 굴러가지 않음
                if (col != null) col.enabled = true; // 보드 위에서만 충돌 활성
                gameObject.layer = 0; // Default 레이어 복원
                break;
            case State.InHand:
                rb.isKinematic = true;
                rb.useGravity = false;
                if (col != null) col.enabled = false; // 손에 든 돌은 충돌 비활성
                break;
            case State.InAir:
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearDamping = 0.2f;
                if (col != null) col.enabled = true;  // Phase C: 공중 돌도 충돌 활성 (손과 부딪혀야 함)
                gameObject.layer = AirLayer; // 보드 위 돌과 충돌 방지
                break;
            case State.Caught:
                rb.isKinematic = true;
                rb.useGravity = false;
                if (col != null) col.enabled = false; // 받은 돌도 충돌 비활성
                break;
            case State.Bouncing:
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 1f;
                if (col != null) col.enabled = true;  // 다른 돌/손과 충돌 가능
                gameObject.layer = AirLayer; // 보드 돌과 충돌 방지
                break;
        }
    }

    private void LateUpdate()
    {
        // 2.5D Z축 보정: 물리 충돌로 Z가 밀렸을 때 0으로 복귀
        if (currentState == State.Bouncing || currentState == State.InAir)
        {
            var pos = transform.position;
            if (Mathf.Abs(pos.z) > 0.01f)
            {
                transform.position = new Vector3(pos.x, pos.y, 0f);
            }
        }
    }

    /// <summary>
    /// 보드 밖으로 나갔는지 판정 (장외 탈락)
    /// </summary>
    public bool IsOutOfBounds(Bounds boardBounds)
    {
        return !boardBounds.Contains(transform.position);
    }
}
