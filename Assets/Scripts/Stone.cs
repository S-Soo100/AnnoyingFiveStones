using UnityEngine;

public class Stone : MonoBehaviour
{
    public enum State
    {
        OnBoard,    // 보드 위에 놓여있음
        InHand,     // 손에 잡힘
        InAir,      // 공중에 던져짐
        Caught      // 받기 성공
    }

    [Header("State")]
    [SerializeField] private State currentState = State.OnBoard;

    private Rigidbody rb;
    private Collider col;
    private int stoneIndex;

    public State CurrentState => currentState;
    public Rigidbody Rb => rb;
    public int StoneIndex => stoneIndex;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        // 2.5D: Z축 이동/회전 고정
        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY;
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
                if (col != null) col.enabled = false; // 공중 돌도 충돌 비활성
                break;
            case State.Caught:
                rb.isKinematic = true;
                rb.useGravity = false;
                if (col != null) col.enabled = false; // 받은 돌도 충돌 비활성
                break;
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
