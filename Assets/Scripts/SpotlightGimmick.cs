using UnityEngine;

/// <summary>
/// Stage 9 [50살] 시야 제한 기믹.
/// 화면을 어둡게 하고 손 위치 주변만 스포트라이트처럼 밝게 보여줌.
/// Scatter 완료 후 1.5초간 전체 보이다가 서서히 어두워짐.
/// </summary>
public class SpotlightGimmick : StageGimmick
{
    // 단수별 스포트라이트 반경 (뷰포트 비율). 단이 올라갈수록 좁아짐.
    private static readonly float[] radiusPerRound = { 0.139f, 0.111f, 0.083f, 0.056f, 0.056f };

    private const float BrightDuration = 1.5f;    // scatter 후 밝은 시간
    private const float FadeInDuration = 1.0f;    // 어둠 페이드인 시간
    private const float SoftEdgeWidth = 0.04f;
    private const float MaxDarknessAlpha = 0.92f;

    private GameObject overlayQuad;
    private Material overlayMaterial;
    private HandController handController;
    private Camera mainCamera;

    private float targetRadius;
    private float fadeStartTime = -1f;
    private bool isFading = false;
    private bool isActive = false;

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
        // scatter 완료 → BrightDuration 후 페이드인 시작
        fadeStartTime = Time.time + BrightDuration;
        isFading = false;
        isActive = true;
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
        if (elapsed < 0f) return; // 아직 BrightDuration 대기 중

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
