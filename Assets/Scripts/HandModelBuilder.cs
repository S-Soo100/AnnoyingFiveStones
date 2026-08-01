using UnityEngine;

/// <summary>
/// 런타임에 Hand 하위 오브젝트 구조 생성.
/// 시각(Primitive, Collider 없음)과 물리(보이지 않는 Hitbox)를 분리.
/// HandController.Awake()에서 Build() 호출.
/// </summary>
public class HandModelBuilder : MonoBehaviour
{
    [Header("Palm Settings")]
    [SerializeField] private Vector3 palmScale = new Vector3(1.0f, 0.8f, 0.14f);
    [SerializeField] private Color palmColor = new Color(1f, 0.85f, 0.6f, 1f);

    [Header("Finger Settings")]
    [SerializeField] private float fingerRadius = 0.08f;
    [SerializeField] private Color fingerColor = new Color(0.95f, 0.8f, 0.55f, 1f);

    // ── v17: 실제 3D 손 모델 (BlendSwap CC0) ─────────────────────────────────
    /// <summary>Resources 경로. 없으면 구 프리미티브(Cube+Cylinder)로 자동 폴백한다.</summary>
    private const string HandModelResource = "Models/Hand";
    /// <summary>화면에서 손의 세로 길이(월드). 프리뷰로 확정한 값.</summary>
    private const float HandTargetSize = 2.09f;
    /// <summary>손바닥이 카메라를 보고 손가락이 위를 향하는 회전. 프리뷰로 확정.</summary>
    private static readonly Vector3 HandFrontEuler = new Vector3(0f, 90f, -90f);

    /// <summary>3D 모델을 쓰는가. false면 구 프리미티브 경로.</summary>
    public bool UsingModel { get; private set; }

    /// <summary>손가락 굽힘 리그. 접기는 전부 여기를 통한다 (뼈든 프리미티브든 동일 API).</summary>
    public HandRig Rig { get; private set; }

    /// <summary>모델을 담는 피벗 — 원근 스케일 전용. 손 루트 스케일(5단이 조작)과 분리한다.</summary>
    private Transform modelPivot;

    // 시각 참조
    public Renderer PalmRenderer { get; private set; }
    /// <summary>손가락 첫 마디 (엄지→소지 순). 시각 처리용 — 굽힘은 <see cref="Rig"/>가 담당.</summary>
    public Transform[] Fingers => Rig?.Proximal;

    // 물리 참조 (보이지 않는 Hitbox)
    public BoxCollider PalmCollider { get; private set; }
    public BoxCollider FingerColliderL { get; private set; }
    public BoxCollider FingerColliderR { get; private set; }
    public SphereCollider FistCollider { get; private set; }

    public void Build()
    {
        if (!CreateModelHand())      // v17: 3D 모델 우선
        {
            CreateVisualPalm();      // 폴백: 구 프리미티브
            CreateVisualFingers();
        }
        CreatePhysicsHitboxes();
        CreateFistCollider();
        SetCollidersEnabled(false); // 기본 비활성
    }

    /// <summary>v17 — Resources의 손 FBX를 붙이고 손가락 뼈를 찾아 연결한다.
    /// 실패하면 false를 반환해 구 프리미티브 경로로 폴백한다(게임이 멈추지 않게).</summary>
    private bool CreateModelHand()
    {
        var prefab = Resources.Load<GameObject>(HandModelResource);
        if (prefab == null)
        {
            Debug.LogWarning($"[HandModelBuilder] 손 모델 없음({HandModelResource}) → 프리미티브 폴백");
            return false;
        }

        // 피벗을 한 겹 둔다 — 원근 스케일은 **피벗**에 걸어야 손바닥이 손 루트에 붙어 있는다.
        // (모델을 직접 스케일하면 정렬 오프셋까지 같이 곱해져 손바닥이 커서에서 떨어진다)
        // 5단이 transform.localScale을 직접 만지므로 손 루트 스케일과도 분리된다.
        var pivotGo = new GameObject("HandModelPivot");
        pivotGo.transform.SetParent(transform, false);
        modelPivot = pivotGo.transform;

        var go = Instantiate(prefab, modelPivot);
        go.name = "HandModel";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(HandFrontEuler);

        var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null)
        {
            Debug.LogWarning("[HandModelBuilder] SkinnedMeshRenderer 없음 → 프리미티브 폴백");
            Destroy(go);
            return false;
        }
        PalmRenderer = skin;
        PalmRenderer.material = CreateURPMaterial(palmColor);

        // 렌더러 실제 크기로 목표 세로에 맞춘다 (FBX 단위를 추측하지 않기 위해).
        var b = skin.bounds;
        float longest = Mathf.Max(b.size.x, b.size.y, 0.0001f);
        go.transform.localScale *= HandTargetSize / longest;

        // 손가락 굽힘 리그 — 뼈대에서 축까지 유도한다. 실패하면 프리미티브로 폴백해
        // "손은 보이는데 안 접히는" 어정쩡한 상태를 만들지 않는다.
        Rig = HandRig.BuildFromBones(go.transform);
        if (Rig == null)
        {
            Debug.LogWarning("[HandModelBuilder] 손가락 리그 구성 실패 → 프리미티브 폴백");
            Destroy(pivotGo);
            PalmRenderer = null;
            modelPivot = null;
            return false;
        }

        // ── 손바닥 중심을 손 루트에 맞춘다 ──
        // 모델 원점은 손 전체(손가락 포함)의 중앙이라 손바닥보다 위에 있다. 그대로 두면
        // 커서·판정은 손바닥을 가리키는데 화면의 손바닥은 그 아래에 그려져,
        // "손바닥을 올렸는데 안 잡히고 손가락을 올려야 잡히는" 어긋남이 생긴다.
        go.transform.localPosition -= modelPivot.InverseTransformPoint(Rig.PalmCenter);
        PalmRadiusBase = Rig.PalmRadius;
        TipReachBase = Rig.TipReach;

        UsingModel = true;
        Debug.Log($"[HandModelBuilder] 3D 손 모델 연결 (마디 {Rig.JointCount}개, " +
                  $"스케일 {go.transform.localScale.x:F3}, 손바닥반경 {PalmRadiusBase:F2}, 손끝거리 {TipReachBase:F2})");
        return true;
    }

    // ==========================================
    // 시각 (Collider 없음, MeshRenderer만) — 구 프리미티브 폴백
    // ==========================================

    private void CreateVisualPalm()
    {
        var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        palm.name = "Palm_Visual";
        palm.transform.SetParent(transform, false);
        palm.transform.localPosition = Vector3.zero;
        palm.transform.localScale = palmScale;

        // Collider 제거 (시각 전용)
        var col = palm.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        PalmRenderer = palm.GetComponent<MeshRenderer>();
        PalmRenderer.material = CreateURPMaterial(palmColor);
    }

    private void CreateVisualFingers()
    {
        // 5개 손가락 위치 (Palm 상대, 2.5D 정면 카메라 기준: X=좌우, Y=위아래, Z=깊이)
        Vector3[] positions = new Vector3[]
        {
            new Vector3(-0.55f, 0.1f, 0f),   // 엄지
            new Vector3(-0.2f, 0.45f, 0f),    // 검지
            new Vector3(0f, 0.5f, 0f),         // 중지
            new Vector3(0.2f, 0.4f, 0f),      // 약지
            new Vector3(0.38f, 0.3f, 0f),     // 소지
        };
        float[] lengths = { 0.4f, 0.55f, 0.6f, 0.5f, 0.35f };
        string[] names = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        var pivots = new Transform[5];

        for (int i = 0; i < 5; i++)
        {
            // 피벗 (회전 기준점 — 접힘 애니메이션용)
            var pivot = new GameObject($"{names[i]}_Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = positions[i];
            pivot.transform.localRotation = Quaternion.identity;
            pivots[i] = pivot.transform;

            // Cylinder (시각 전용)
            var finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            finger.name = $"{names[i]}_Visual";
            finger.transform.SetParent(pivot.transform, false);
            float halfLen = lengths[i] * 0.5f;
            finger.transform.localPosition = new Vector3(0, halfLen, 0);
            finger.transform.localScale = new Vector3(fingerRadius * 2, halfLen, fingerRadius * 2);

            // Collider 제거 (시각 전용)
            var col = finger.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            finger.GetComponent<MeshRenderer>().material = CreateURPMaterial(fingerColor);
        }

        Rig = HandRig.BuildFromPivots(pivots);
        PalmRadiusBase = palmScale.x * 0.5f;
    }

    /// <summary>URP Lit 셰이더로 머테리얼 생성 (빌드 시 Standard 셰이더 스트리핑 방지)</summary>
    private static Material CreateURPMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard"); // fallback
        var mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    // ==========================================
    // 물리 (보이지 않는 Hitbox, MeshRenderer 없음)
    // ==========================================

    // Hitbox 루트 (hand의 자식이 아닌 독립 오브젝트 — 회전 영향 안 받음)
    public Transform HitboxRoot { get; private set; }

    private void CreatePhysicsHitboxes()
    {
        // Hitbox 루트: hand와 독립 (회전 영향 안 받음)
        // Rigidbody 필요 (compound collider — OnCollisionEnter가 부모에서 발생)
        var rootGo = new GameObject("HandHitboxRoot");
        HitboxRoot = rootGo.transform;
        HitboxRoot.position = transform.position;
        hitboxRb = rootGo.AddComponent<Rigidbody>();
        hitboxRb.isKinematic = true;
        hitboxRb.useGravity = false;
        hitboxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rootGo.layer = Stone.AirLayer;

        // ★ HitboxCollisionForwarder: OnCollisionEnter는 Rigidbody 부모에서 발생하므로
        // 여기서 자식 Collider의 HandHitbox를 찾아 HandController에 전달
        rootGo.AddComponent<HitboxCollisionForwarder>();

        // --- Palm Hitbox (유일한 hitbox — 시각 Palm보다 살짝 작게) ---
        // 시각 Palm scale: (1.0, 0.8, 0.2)
        // Hitbox: 그 안에 여백을 두고 들어감 → 돌이 살짝 밀려들어오는 느낌
        var palmGo = new GameObject("PalmHitbox");
        palmGo.transform.SetParent(HitboxRoot, false);
        palmGo.transform.localPosition = Vector3.zero;
        palmGo.layer = Stone.AirLayer;
        PalmCollider = palmGo.AddComponent<BoxCollider>();
        PalmCollider.size = new Vector3(0.85f, 0.65f, 2f); // 시각 Palm 안에 약간 여백, Z만 넓게
        var palmHitbox = palmGo.AddComponent<HandHitbox>();
        palmHitbox.SetZone(HandHitbox.HitZone.Palm);

        // --- Finger Hitbox Left (Palm 왼쪽 가장자리, 받기 모드 전용) ---
        var fingerLGo = new GameObject("FingerHitbox_L");
        fingerLGo.transform.SetParent(HitboxRoot, false);
        fingerLGo.transform.localPosition = new Vector3(-0.65f, 0f, 0f);
        fingerLGo.layer = Stone.AirLayer;
        FingerColliderL = fingerLGo.AddComponent<BoxCollider>();
        FingerColliderL.size = new Vector3(0.4f, 0.65f, 2f);
        FingerColliderL.enabled = false; // 받기 모드에서만 활성화
        var fingerLHitbox = fingerLGo.AddComponent<HandHitbox>();
        fingerLHitbox.SetZone(HandHitbox.HitZone.Finger);

        // --- Finger Hitbox Right (Palm 오른쪽 가장자리) ---
        var fingerRGo = new GameObject("FingerHitbox_R");
        fingerRGo.transform.SetParent(HitboxRoot, false);
        fingerRGo.transform.localPosition = new Vector3(0.65f, 0f, 0f);
        fingerRGo.layer = Stone.AirLayer;
        FingerColliderR = fingerRGo.AddComponent<BoxCollider>();
        FingerColliderR.size = new Vector3(0.4f, 0.65f, 2f);
        FingerColliderR.enabled = false; // 받기 모드에서만 활성화
        var fingerRHitbox = fingerRGo.AddComponent<HandHitbox>();
        fingerRHitbox.SetZone(HandHitbox.HitZone.Finger);
    }

    private Rigidbody hitboxRb;

    /// <summary>Hitbox를 hand 위치에 동기화 (매 프레임 호출)</summary>
    public void SyncHitboxPosition(Vector3 handWorldPos)
    {
        if (hitboxRb != null)
        {
            // MovePosition: 물리 엔진 경유 → 충돌 감지 정상 작동
            // transform.position 직접 설정은 물리 우회하여 OnCollisionEnter 미발생
            hitboxRb.MovePosition(handWorldPos);
        }
    }

    private void CreateFistCollider()
    {
        var fistGo = new GameObject("FistZone");
        fistGo.transform.SetParent(transform, false);
        fistGo.transform.localPosition = new Vector3(0, 0.2f, 0);
        FistCollider = fistGo.AddComponent<SphereCollider>();
        FistCollider.radius = 0.5f;
        FistCollider.isTrigger = true;
        FistCollider.enabled = false;
    }

    // ==========================================
    // 공개 API
    // ==========================================

    /// <summary>시각 파트 투명도 설정 (줍기=반투명, 받기=불투명)</summary>
    public void SetVisualAlpha(float alpha)
    {
        SetRendererAlpha(PalmRenderer, alpha);
        if (UsingModel) return; // 모델은 스킨드 메시 하나 → PalmRenderer만으로 충분
        if (Fingers != null)
        {
            foreach (var pivot in Fingers)
            {
                if (pivot == null) continue;
                var r = pivot.GetComponentInChildren<MeshRenderer>();
                SetRendererAlpha(r, alpha);
            }
        }
    }

    private void SetRendererAlpha(Renderer r, float alpha)
    {
        if (r == null) return;
        var mat = r.material;
        var c = mat.color;
        c.a = alpha;
        mat.color = c;
        // URP 투명도: Surface Type을 Transparent로 전환
        if (alpha < 1f)
        {
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // 0 = Alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
        }
        else
        {
            mat.SetFloat("_Surface", 0); // 0 = Opaque
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = 2000;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
        }
    }

    /// <summary>물리 Hitbox Collider 활성/비활성 (받기 모드에서만 ON)</summary>
    public void SetCollidersEnabled(bool enabled)
    {
        if (PalmCollider != null) PalmCollider.enabled = enabled;
        if (FingerColliderL != null) FingerColliderL.enabled = enabled;
        if (FingerColliderR != null) FingerColliderR.enabled = enabled;
        // FistCollider는 5단 전용, 별도 관리
    }

    /// <summary>줍기 판정용: Palm 영역 Bounds (Collider 비활성 상태에서도 동작)</summary>
    public Bounds GetPalmPickupBounds()
    {
        if (PalmRenderer == null) return new Bounds();
        var b = PalmRenderer.bounds;
        b.Expand(new Vector3(0, 0, 2f)); // Z축 확장
        return b;
    }

    /// <summary>손바닥 중심(월드) — 줍기·받기 판정의 기준점.
    /// 모델은 뼈에서 정확히 구한다(렌더러 bounds는 손가락까지 포함해 위로 치우친다).</summary>
    public Vector3 GetPalmCenter()
    {
        if (UsingModel && Rig != null) return Rig.PalmCenter;
        if (PalmRenderer == null) return transform.position;
        return PalmRenderer.bounds.center;
    }

    /// <summary>원근 스케일 1일 때의 손바닥 반경(월드). 줍기 판정 반경을 이 값에서 끌어온다 —
    /// 판정 숫자를 따로 두면 눈에 보이는 손과 어긋난다. 스케일이 걸린 뒤 재면 값이 흔들리므로
    /// 빌드 시점에 한 번만 잰다.</summary>
    public float PalmRadiusBase { get; private set; } = 0.5f;

    /// <summary>원근 스케일 1일 때 손 중심에서 손끝까지의 거리(월드).
    /// 받기 판정 반경을 "그려진 손끝"에 맞출 때 기준이 된다.</summary>
    public float TipReachBase { get; private set; } = 1f;

    /// <summary>지금 자세에서 **화면상** 손끝 반경(월드) — 손바닥 중심에서 가장 먼 손끝까지의 x/y 거리.
    ///
    /// TipReachBase(정면·펼친 손)와 달리 회전·접힘이 반영된다. 받기 모드는 손을 눕혀서 잡으므로
    /// 정면 기준 길이를 쓰면 화면에 보이는 것보다 크게 잡힌다. 판정 원에 손끝을 정확히 맞추려면
    /// 지금 보이는 값을 재야 한다.</summary>
    public float CurrentScreenTipReach
    {
        get
        {
            if (!UsingModel || Rig == null) return TipReachBase;
            Vector3 c = Rig.PalmCenter;
            float max = 0f;
            for (int i = 0; i < HandRig.FingerCount; i++)
            {
                Vector3 t = Rig.FingerTip(i);
                max = Mathf.Max(max, new Vector2(t.x - c.x, t.y - c.y).magnitude);
            }
            return max;
        }
    }

    /// <summary>모델 크기 배율 (1 = 기본). 줍기에선 원근, 받기에선 판정 반경 맞춤에 쓴다.
    /// 손 루트가 아니라 모델 피벗에 걸어 5단의 손 크기 연출과 충돌하지 않는다.</summary>
    public void SetPerspectiveScale(float k)
    {
        if (modelPivot == null) return;
        modelPivot.localScale = Vector3.one * Mathf.Max(0.01f, k);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (HitboxRoot == null) return;

        // Palm Hitbox — 초록색
        if (PalmCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(PalmCollider.transform.position + PalmCollider.center, PalmCollider.size);
            Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
            Gizmos.DrawWireCube(PalmCollider.transform.position + PalmCollider.center, PalmCollider.size);
        }

        // Finger Hitbox L — 빨간색
        if (FingerColliderL != null && FingerColliderL.enabled)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawCube(FingerColliderL.transform.position + FingerColliderL.center, FingerColliderL.size);
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireCube(FingerColliderL.transform.position + FingerColliderL.center, FingerColliderL.size);
        }

        // Finger Hitbox R — 빨간색
        if (FingerColliderR != null && FingerColliderR.enabled)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawCube(FingerColliderR.transform.position + FingerColliderR.center, FingerColliderR.size);
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireCube(FingerColliderR.transform.position + FingerColliderR.center, FingerColliderR.size);
        }
    }
#endif
}
