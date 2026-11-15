using UnityEngine;

/// <summary>
/// Stage 9 [50살] 시야 제한 기믹.
/// 화면을 어둡게 하고 손 위치 주변만 스포트라이트처럼 밝게 보여줌.
/// 뿌리기 후 던질 돌을 고르는 동안은 전체가 보이고, **첫 던지기 순간** 빠르게 어두워진다(그 단 끝까지 유지).
/// 2026-11-15: 기존엔 scatter 후 1.5초 밝음 + 1초 페이드(시간 기준)라, 어두워지기 전에 빠르게 다 주울 수 있었다.
///   던지기에 묶으면 줍기는 반드시 어둠 속에서 한다 — 배치는 먼저 다 보여주고(정보는 완전하게) 실행을 어렵게.
/// </summary>
public class SpotlightGimmick : StageGimmick
{
    // 단수별 스포트라이트 반경 (뷰포트 비율). 단이 올라갈수록 좁아짐.
    private static readonly float[] radiusPerRound = { 0.139f, 0.111f, 0.083f, 0.056f, 0.056f };

    private const float FadeInDuration = 0.3f;    // 던지는 순간부터 어둠 페이드인 시간
    private const float SoftEdgeWidth = 0.04f;
    private const float MaxDarknessAlpha = 1.0f;

    private GameObject overlayQuad;
    private Material overlayMaterial;
    private HandController handController;
    private Camera mainCamera;

    private float targetRadius;
    private float fadeStartTime = -1f;
    private bool isFading = false;
    private bool isActive = false;
    private bool darkTriggered = false;  // 이번 단에서 어둠이 시작됐는가 (첫 던지기 1회만)

    public override void OnStageStart(int stageInLoop)
    {
        mainCamera = Camera.main;
        handController = gameManager?.GetComponentInChildren<HandController>();
        if (handController == null)
            handController = Object.FindFirstObjectByType<HandController>();

        // 단수별 반경 (1~5단, 인덱스 0~4)
        int radiusIndex = Mathf.Clamp(stageInLoop - 1, 0, radiusPerRound.Length - 1);
        targetRadius = radiusPerRound[radiusIndex];

        // World Space Quad 생성 — 카메라 전체를 덮는 크기
        CreateOverlayQuad();

        // 처음에는 완전 투명 (밝은 상태)
        if (overlayMaterial != null)
        {
            overlayMaterial.SetFloat("_DarknessAlpha", 0f);
        }

        Debug.Log($"[SpotlightGimmick] Stage {stageInLoop} started: radius={targetRadius}");
    }

    public override void OnScatterComplete(Stone[] activeStones)
    {
        // scatter 완료 → 아직 밝음. 어둠은 첫 던지기(OnThrowStart)에서 시작한다.
        fadeStartTime = -1f;
        isFading = false;
        darkTriggered = false;
        isActive = true;
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        // isActive = 이번 단에 뿌리기가 있었다(1~4단). 뿌리기 없는 5단은 기존대로 어둠 없음.
        if (!isActive || darkTriggered) return;
        darkTriggered = true;
        fadeStartTime = Time.time;
        Debug.Log("[SpotlightGimmick] First throw → darkness fade-in.");
    }

    public override void OnUpdate()
    {
        if (!isActive || overlayMaterial == null || mainCamera == null) return;

        // 손 위치를 뷰포트 좌표로 변환
        UpdateSpotlightPosition();

        // 페이드인 처리
        UpdateFadeIn();
    }

    private void UpdateSpotlightPosition()
    {
        if (handController == null) return;

        Vector3 handWorldPos = handController.transform.position;
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(handWorldPos);

        overlayMaterial.SetVector("_SpotlightCenter", new Vector4(viewportPos.x, viewportPos.y, 0, 0));
        overlayMaterial.SetFloat("_SpotlightRadius", targetRadius);
        overlayMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
    }

    private void UpdateFadeIn()
    {
        if (fadeStartTime < 0f) return;

        float elapsed = Time.time - fadeStartTime;

        if (!isFading)
        {
            isFading = true;
        }

        float t = Mathf.Clamp01(elapsed / FadeInDuration);
        float currentAlpha = Mathf.Lerp(0f, MaxDarknessAlpha, t);
        overlayMaterial.SetFloat("_DarknessAlpha", currentAlpha);

        if (t >= 1f)
        {
            fadeStartTime = -1f; // 페이드 완료
            isFading = false;
        }
    }

    private void CreateOverlayQuad()
    {
        if (mainCamera == null) return;

        // Orthographic 카메라의 뷰 크기 계산
        float orthoSize = mainCamera.orthographicSize;
        float aspect = mainCamera.aspect;
        float quadHeight = orthoSize * 2f;
        float quadWidth = quadHeight * aspect;

        // Quad 생성
        overlayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlayQuad.name = "SpotlightOverlay";

        // Collider 제거 (물리 간섭 방지)
        var collider = overlayQuad.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        // z=-1.0: 손(z=-0.5)보다 카메라(-10) 쪽 → 손·돌 위에 오버레이 렌더링
        overlayQuad.transform.position = new Vector3(
            mainCamera.transform.position.x,
            mainCamera.transform.position.y,
            -1.0f
        );
        overlayQuad.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);

        // 커스텀 셰이더 머테리얼 적용
        var shader = Shader.Find("Custom/SpotlightOverlay");
        if (shader == null)
        {
            Debug.LogError("[SpotlightGimmick] Custom/SpotlightOverlay shader not found!");
            // 폴백: 기본 URP Unlit 투명
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        overlayMaterial = new Material(shader);
        overlayMaterial.SetVector("_SpotlightCenter", new Vector4(0.5f, 0.5f, 0, 0));
        overlayMaterial.SetFloat("_SpotlightRadius", targetRadius);
        overlayMaterial.SetFloat("_SoftEdgeWidth", SoftEdgeWidth);
        overlayMaterial.SetFloat("_DarknessAlpha", 0f); // 시작 시 투명
        overlayMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);

        // 투명 렌더링 설정
        overlayMaterial.renderQueue = 3100; // Transparent + 100

        var renderer = overlayQuad.GetComponent<MeshRenderer>();
        renderer.material = overlayMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public override void OnStageEnd()
    {
        isActive = false;
        isFading = false;
        darkTriggered = false;
        fadeStartTime = -1f;

        if (overlayQuad != null)
        {
            Object.Destroy(overlayQuad);
            overlayQuad = null;
        }
        overlayMaterial = null;

        Debug.Log("[SpotlightGimmick] Stage ended: overlay destroyed.");
    }
}
