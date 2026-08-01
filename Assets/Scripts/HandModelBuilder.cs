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
    /// <summary>각 손가락의 첫 마디 뼈 이름. dedo = 스페인어 '손가락'.</summary>
    private static readonly string[] FingerBoneNames =
        { "dedo1", "dedo1.000", "dedo1.001", "dedo1.002", "dedo1.003" };

    /// <summary>3D 모델을 쓰는가. false면 구 프리미티브 경로.</summary>
    public bool UsingModel { get; private set; }

    /// <summary>손가락 뼈의 **기본 자세**. 접기 회전은 여기에 곱해서 적용해야 한다
    /// (localEulerAngles를 통째로 덮으면 뼈의 rest pose가 파괴된다).</summary>
    public Quaternion[] FingerRest { get; private set; }

    /// <summary>손가락을 접는 회전축 — 화면 가로축(world right)을 각 뼈의 로컬 공간으로 옮긴 것.</summary>
    public Vector3[] FingerFoldAxis { get; private set; }

    // 시각 참조
    public Renderer PalmRenderer { get; private set; }
    public Transform[] Fingers { get; private set; }

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

        var go = Instantiate(prefab, transform);
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

        // 손가락 뼈 연결 + 기본 자세/회전축 캐시
        Fingers = new Transform[FingerBoneNames.Length];
        FingerRest = new Quaternion[FingerBoneNames.Length];
        FingerFoldAxis = new Vector3[FingerBoneNames.Length];
        int found = 0;
        var all = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < FingerBoneNames.Length; i++)
        {
            foreach (var t in all)
            {
                if (t.name != FingerBoneNames[i]) continue;
                Fingers[i] = t;
                FingerRest[i] = t.localRotation;
                // 화면 가로축을 뼈 로컬로 — 이 축으로 돌려야 손가락이 접히는 방향이 된다.
                FingerFoldAxis[i] = t.InverseTransformDirection(Vector3.right).normalized;
                found++;
                break;
            }
        }
        if (found < FingerBoneNames.Length)
            Debug.LogWarning($"[HandModelBuilder] 손가락 뼈 {found}/{FingerBoneNames.Length}개만 찾음 — 접기 연출이 일부만 동작");

        UsingModel = true;
        Debug.Log($"[HandModelBuilder] 3D 손 모델 연결 (뼈 {found}개, 스케일 {go.transform.localScale.x:F3})");
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

        Fingers = new Transform[5];

        for (int i = 0; i < 5; i++)
        {
            // 피벗 (회전 기준점 — 접힘 애니메이션용)
            var pivot = new GameObject($"{names[i]}_Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = positions[i];
            pivot.transform.localRotation = Quaternion.identity;
            Fingers[i] = pivot.transform;

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

    /// <summary>손바닥 중심(월드). 3D 모델은 손가락까지 bounds에 들어가 중심이 위로 치우치므로
    /// 아래쪽(손바닥 쪽)으로 보정한다. 줍기 판정 기준점.</summary>
    public Vector3 GetPalmCenter()
    {
        if (PalmRenderer == null) return transform.position;
        var b = PalmRenderer.bounds;
        return UsingModel ? b.center - new Vector3(0f, b.size.y * 0.18f, 0f) : b.center;
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
