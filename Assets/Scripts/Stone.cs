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
    private int stoneIndex;

    public State CurrentState => currentState;
    public Rigidbody Rb => rb;
    public int StoneIndex => stoneIndex;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
                break;
            case State.InHand:
                rb.isKinematic = true;
                rb.useGravity = false;
                break;
            case State.InAir:
                // 하늘로 던진 돌만 중력 적용
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearDamping = 0.2f; // 공기저항 낮게
                break;
            case State.Caught:
                rb.isKinematic = true;
                rb.useGravity = false;
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
