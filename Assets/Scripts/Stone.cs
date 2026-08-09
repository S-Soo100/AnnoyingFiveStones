using System.Collections;
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
    private StoneShadow shadow; // v6-1: 낙하 그림자

    public State CurrentState => currentState;
    public Rigidbody Rb => rb;
    public int StoneIndex => stoneIndex;

    // ── v17: 보드 좌표 + 높이 ────────────────────────────────────────────────
    // 화면 y 하나로 "보드 위 위치"와 "높이"를 겸하던 것이 반복 버그의 근원이었다.
    // 코루틴이 이 두 값을 세팅하면 화면 위치는 투영으로 파생된다.
    // (아직 전 경로가 옮겨오지 않아, 세팅된 적 없는 돌은 HasBoardMotion=false)

    /// <summary>보드 평면 위 좌표(중심 원점). 던진 돌은 체공 내내 불변이다.</summary>
    public Vector2 BoardPos { get; private set; }

    /// <summary>보드 평면으로부터의 높이. 0이면 지면.</summary>
    public float Height { get; private set; }

    /// <summary>이 돌이 보드 좌표계로 관리되고 있는가. false면 옛 화면 좌표 경로.</summary>
    public bool HasBoardMotion { get; private set; }

    /// <summary>원근 스케일 이전의 고유 크기. 보드 뒤쪽 돌을 작게 그리려면 기준 크기가 필요하다.</summary>
    private Vector3 baseScale = Vector3.one;
    private bool baseScaleCaptured;

    /// <summary>고유 크기를 다시 잡는다(기믹이 돌 크기를 바꾼 뒤 등).</summary>
    public void CaptureBaseScale(Vector3 scale) { baseScale = scale; baseScaleCaptured = true; }

    // ── v18: 줍기·착지 순간의 짧은 팝 ────────────────────────────────────────
    // 지금까지 돌은 **소리만 나고 그림에는 아무 변화가 없었다.** 0.12초짜리 크기 변화 하나로
    // "잡혔다 / 닿았다"가 손끝에 남는다. 파티클을 뿌리지 않으므로 기획서 v11 §8
    // ("HUD·주석·하이라이트를 얹지 않는다")과 충돌하지 않는다 — 돌 자신이 반응할 뿐이다.
    //
    // ⚠️ 크기는 **원근 스케일과 곱한다.** 직접 대입하면 뒤쪽 돌이 팝 도중 앞쪽 크기로 튄다.

    /// <summary>현재 팝 배율. 1이면 평소 크기.</summary>
    private Vector3 popScale = Vector3.one;

    /// <summary>팝을 곱하기 **전**의 크기. 원근이 이미 반영된 값이다.
    ///
    /// 왜 따로 두는가: 돌의 크기를 정하는 경로가 둘이다 — 보드좌표 경로(SetBoardMotion, 원근 반영)와
    /// 물리 경로(뿌리기 등, 스케일을 건드리지 않음). 팝이 매번 <c>baseScale</c>에서 다시 계산하면
    /// 물리 경로의 돌이 **착지하는 순간 원근을 잃고 앞쪽 크기로 튄다.**
    /// 그래서 "직전에 실제로 그려지던 크기"를 기준으로 삼는다.</summary>
    private Vector3 popAnchor = Vector3.one;
    private Coroutine popRoutine;

    private const float PopDuration = 0.12f;
    private const float PopRiseRatio = 0.3f;  // 앞 30%에 튀고 나머지에 돌아온다 — 타격감은 복귀 곡선에서 나온다
    private static readonly Vector3 PickPeak = new Vector3(1.28f, 1.28f, 1.28f); // 잡힘: 전방향 팽창
    private static readonly Vector3 LandPeak = new Vector3(1.22f, 0.78f, 1.22f); // 착지: 눌림(세로만 축소)

    /// <summary>팝 배율을 반영해 현재 크기를 다시 그린다.</summary>
    private void ApplyScaleWithPop() => transform.localScale = Vector3.Scale(popAnchor, popScale);

    private void StartPop(Vector3 peak)
    {
        if (!baseScaleCaptured || !isActiveAndEnabled) return;

        // 팝이 겹칠 때 현재(이미 부푼) 크기를 기준으로 다시 잡으면 눈덩이처럼 커진다.
        // 진행 중이면 기존 기준을 유지하고 곡선만 새로 시작한다.
        if (popRoutine == null) popAnchor = transform.localScale;
        else StopCoroutine(popRoutine);

        popRoutine = StartCoroutine(PopRoutine(peak));
    }

    private IEnumerator PopRoutine(Vector3 peak)
    {
        float elapsed = 0f;
        while (elapsed < PopDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / PopDuration);
            popScale = k < PopRiseRatio
                ? Vector3.Lerp(Vector3.one, peak, k / PopRiseRatio)
                : Vector3.Lerp(peak, Vector3.one, (k - PopRiseRatio) / (1f - PopRiseRatio));
            ApplyScaleWithPop();
            yield return null;
        }
        popScale = Vector3.one;
        ApplyScaleWithPop();
        popRoutine = null;
    }

    /// <summary>돌이 풀로 돌아가거나 스테이지가 리셋될 때 팝이 남아 크기가 틀어지는 것을 막는다.</summary>
    private void OnDisable()
    {
        // 코루틴은 비활성화와 함께 이미 죽는다. 남은 배율만 되돌려, 다시 켜졌을 때
        // 부푼 크기로 나타나지 않게 한다.
        popRoutine = null;
        if (popScale != Vector3.one)
        {
            popScale = Vector3.one;
            if (baseScaleCaptured) ApplyScaleWithPop();
        }
    }

    /// <summary>보드 좌표 + 높이를 지정하고 화면 위치·크기를 투영으로 갱신한다.</summary>
    public void SetBoardMotion(Vector2 boardPos, float height)
    {
        if (!baseScaleCaptured) CaptureBaseScale(transform.localScale);

        BoardPos = boardPos;
        Height = height;
        HasBoardMotion = true;

        var p = BoardSpace.ToScreen(boardPos, height);
        transform.position = new Vector3(p.x, p.y, 0f);

        // 원근: 뒤에 있을수록 작게, 위로 뜰수록 크게.
        // 내려다보는 시점이라 뜨면 카메라에 가까워진다 — 높이를 빼면 던진 돌이
        // 중간 깊이의 축소율에 묶인 채 하늘에 떠 있어 계속 작아 보인다.
        // v18: 원근으로 계산한 크기를 팝의 기준으로 삼고, 팝 배율을 곱해서 그린다.
        // (기준을 갱신하지 않으면 팝 도중 깊이가 바뀔 때 원근이 멈춘 것처럼 보인다)
        popAnchor = baseScale * BoardSpace.Current.PerspectiveScale(boardPos, height);
        transform.localScale = Vector3.Scale(popAnchor, popScale);
    }

    /// <summary>보드 좌표 관리 해제 — 옛 화면 좌표 경로(뿌리기·5단 등)로 돌아갈 때.</summary>
    public void ClearBoardMotion() => HasBoardMotion = false;

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

        // v6-1: 낙하 그림자 컴포넌트 (기본 비활성)
        shadow = gameObject.AddComponent<StoneShadow>();
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

    /// <summary>임의 RGB 색상 직접 지정 (Stage 4 순서대로 잡기 색 구분용).
    /// StoneColor 열거형과 무관하게 머티리얼 _BaseColor를 직접 설정한다.
    /// ResetColorAndFake()의 SetColor(Default)가 originalMaterialColor로 복원하므로
    /// 스테이지 전환 시(StonePool.Activate) 자동 리셋된다.</summary>
    public void SetColorRGB(Color rgb)
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null) return;
        var mat = renderer.material;
        rgb.a = 1f; // 불투명 보장
        mat.SetColor("_BaseColor", rgb);
        mat.color = rgb;
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
        State prevState = currentState;
        currentState = newState;

        // v17: 상태가 바뀌면 보드 좌표 관리를 해제한다.
        // 보드 좌표 관리를 놓아야 하는 상태에서만 해제한다.
        //   InHand/Caught: 손에 붙어 보드를 떠남.
        //   Bouncing: 물리가 위치를 몰아 보드 좌표가 곧 낡은 값이 됨.
        //   OnBoard/InAir는 유지 — 보드 위 유효한 좌표를 갖고 있고(안착 돌),
        //   던진 돌은 코루틴이 매 프레임 다시 세팅한다. 여기서 무조건 지우면
        //   안착 직후 그림자가 근사 폴백으로 튄다.
        if (newState == State.InHand || newState == State.Caught || newState == State.Bouncing)
            HasBoardMotion = false;

        // 원근 크기 원복은 **손에 들린 상태에서만** 한다.
        //   InHand/Caught: 돌이 보드 평면을 떠나 손에 붙으므로 깊이 개념이 없다 → 고유 크기.
        //   OnBoard/InAir/Bouncing: 여전히 보드 위 어느 깊이에 있으므로 원근을 유지해야 한다.
        //   (여기서 무조건 원복하면 안착·낙 순간 돌이 갑자기 커진다)
        if (baseScaleCaptured && (newState == State.InHand || newState == State.Caught))
        {
            popAnchor = baseScale;
            transform.localScale = Vector3.Scale(popAnchor, popScale);
        }

        // v18: 손끝 피드백 — 상태가 **실제로 바뀐** 순간에만 튄다.
        //   잡힘/받음 → 전방향 팽창.  공중에서 보드로 내려앉음 → 눌림.
        //   ⚠️ OnBoard는 리셋·스폰에서도 불리므로 "직전이 공중"일 때만 착지로 본다.
        //      안 그러면 스테이지 시작마다 돌 다섯 개가 일제히 꿈틀거린다.
        if (prevState != newState)
        {
            if (newState == State.InHand || newState == State.Caught)
                StartPop(PickPeak);
            else if (newState == State.OnBoard && (prevState == State.InAir || prevState == State.Bouncing))
                StartPop(LandPeak);
        }

        // v6-1: InAir/Bouncing일 때 그림자 활성
        shadow?.UpdateVisibility(newState);

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
