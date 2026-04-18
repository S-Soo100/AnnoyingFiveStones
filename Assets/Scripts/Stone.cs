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

    public enum StoneColor
    {
        Default,
        Red,
        Blue,
        Yellow,
        Green,
        Purple,
        Black,
        Gray,
        White
    }

    // 기존 색상 팔레트 (머티리얼에 적용)
    private static readonly Color[] colorPalette = new Color[]
    {
        new Color(0.7f, 0.65f, 0.55f), // Default (기존 돌 색)
        new Color(0.9f, 0.2f, 0.2f),   // Red
        new Color(0.2f, 0.4f, 0.9f),   // Blue
        new Color(0.95f, 0.85f, 0.2f), // Yellow
        new Color(0.2f, 0.8f, 0.3f),   // Green
        new Color(0.6f, 0.2f, 0.8f),   // Purple
        new Color(0.15f, 0.15f, 0.15f), // Black (거의 검정)
        new Color(0.5f, 0.5f, 0.5f),    // Gray (중간 회색)
        new Color(0.92f, 0.92f, 0.92f), // White (거의 흰색)
    };

    [Header("State")]
    [SerializeField] private State currentState = State.OnBoard;

    [Header("v4 Color/Fake")]
    [SerializeField] private StoneColor stoneColor = StoneColor.Default;
    [SerializeField] private bool isFake = false;

    public StoneColor Color => stoneColor;
    public bool IsFake => isFake;

    private Rigidbody rb;
    private Collider col;
    private int stoneIndex;
    private Color originalMaterialColor; // 원본 머티리얼 색상 보존

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
        // 원본 머티리얼 색상 저장 (URP Lit: _BaseColor)
        var r = GetComponent<Renderer>();
        if (r != null)
        {
            originalMaterialColor = r.material.HasProperty("_BaseColor")
                ? r.material.GetColor("_BaseColor")
                : r.material.color;
        }
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

    public void SetColor(StoneColor color)
    {
        stoneColor = color;
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = renderer.material;
            Color targetCol;
            if (color == StoneColor.Default)
            {
                targetCol = originalMaterialColor;
            }
            else
            {
                targetCol = colorPalette[(int)color];
            }
            // URP Lit: _BaseColor 사용 (legacy _Color도 함께 설정)
            mat.SetColor("_BaseColor", targetCol);
            mat.color = targetCol;
        }
    }

    public void SetFake(bool fake)
    {
        isFake = fake;
    }

    /// <summary>투명도 설정 (0=완전투명, 1=불투명). Stage 5 Proximity Reveal용.</summary>
    public void SetAlpha(float alpha)
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = renderer.material;

        // URP Lit: _BaseColor의 알파를 변경 (RGB는 유지)
        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
        }

        // URP Lit 셰이더: Surface Type 전환
        if (alpha < 0.99f)
        {
            mat.SetFloat("_Surface", 1f);    // Transparent
            mat.SetFloat("_Blend", 0f);      // Alpha blend
            mat.SetFloat("_AlphaClip", 0f);  // 알파 클립 비활성
            mat.SetFloat("_SrcBlend", 5f);   // SrcAlpha
            mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            mat.SetFloat("_Surface", 0f);    // Opaque
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", 1f);   // One
            mat.SetFloat("_DstBlend", 0f);   // Zero
            mat.SetFloat("_ZWrite", 1f);
            mat.renderQueue = -1;
            mat.SetOverrideTag("RenderType", "");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }

    /// <summary>색상+가짜 상태 초기화</summary>
    public void ResetColorAndFake()
    {
        SetColor(StoneColor.Default);
        SetFake(false);
        SetAlpha(1f);
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
