using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// OS 커서를 숨기고 손 모양 UI를 커서 위치에 표시.
/// 타이틀/묘지/일시정지 등 비게임 화면에서 활성화.
/// 게임 중에는 HandController의 3D 손이 커서 역할이므로 비활성.
/// </summary>
public class HandCursorUI : MonoBehaviour
{
    public static HandCursorUI Instance { get; private set; }


    private Canvas cursorCanvas;
    private RectTransform cursorRoot;
    private RectTransform palmRect;
    private RectTransform[] fingerPivots;   // 폴백(사각형 손) 전용
    private InputAction pointerAction;

    // ── v17: 게임과 같은 3D 손 모델을 커서로 쓴다 ───────────────────────────────
    // Screen Space Overlay 캔버스 위에는 3D 오브젝트를 얹을 수 없다(항상 UI 뒤).
    // → 손을 화면 밖 먼 곳에 세워 두고 전용 카메라로 RenderTexture에 그려 RawImage로 띄운다.
    //   레이어를 새로 파지 않고 **위치로 격리**한다(게임 카메라 시야 밖).
    private static readonly Vector3 StagePos = new Vector3(0f, 1000f, 0f);
    private const float StageHandSize = 2.0f;   // 촬영용 손 크기(월드)
    private const float StageCamSize = 1.5f;    // 담기는 범위 — 손이 잘리지 않을 만큼
    private const int CursorRTSize = 256;
    /// <summary>화면에 그려질 커서 크기(px). 손은 이 안에서 StageHandSize/(StageCamSize*2) 비율로 보인다.</summary>
    private const float CursorPixelSize = 130f;

    private RawImage handImage;
    private RenderTexture cursorRT;
    private Camera cursorCam;
    private HandRig cursorRig;
    private Transform cursorStage;
    private bool usingModel;
    private readonly float[] currentFolds = new float[HandRig.FingerCount];

    /// <summary>커서 손이 그려지는 RenderTexture. 개발 중 눈으로 확인하기 위한 공개 참조
    /// (커서는 마우스가 게임 뷰 밖이면 화면에 안 나와서 스크린샷으로는 검증이 안 된다).</summary>
    public RenderTexture CursorTexture => cursorRT;

    private HandPose currentPose = HandPose.Open;
    private HandPose targetPose = HandPose.Open;
    private Coroutine poseCoroutine;
    private bool isActive;
    public bool IsActive => isActive;

    // 포즈별 시각 오프셋: 손가락 끝이 마우스 위치에 오도록 전체를 이동
    // 검지 끝: pivot(-8,19) + length(22) = (-8, 41) → 오프셋 (8, -41)
    // 중지 끝: pivot(0,21) + length(24) = (0, 45) → 오프셋 (0, -45)
    private Vector2 currentVisualOffset = Vector2.zero;
    private Vector2 targetVisualOffset = Vector2.zero;

    // 손가락 인덱스: 0=엄지, 1=검지, 2=중지, 3=약지, 4=소지
    // X축 회전: 양수 = 화면 안쪽으로 말림 (주먹 쥐기)
    private const float FoldAngleX = 90f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        pointerAction = new InputAction("CursorPointer", InputActionType.Value);
        pointerAction.AddBinding("<Mouse>/position");
        pointerAction.AddBinding("<Touchscreen>/primaryTouch/position");

        BuildCursorUI();
        SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        pointerAction.Enable();
    }

    private void OnDisable()
    {
        pointerAction.Disable();
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        // 커서 위치 추종 + 포즈별 오프셋 (손가락 끝 = 마우스 위치)
        Vector2 screenPos = pointerAction.ReadValue<Vector2>();
        cursorRoot.position = screenPos + (usingModel ? Vector2.zero : currentVisualOffset);

        // 3D 손: 촬영 카메라를 **핫스팟**(가리키는 지점)에 맞춘다.
        // 그러면 핫스팟이 항상 RenderTexture 정중앙 = 커서 위치가 된다 —
        // 포즈마다 픽셀 오프셋을 따로 재던 방식보다 정확하고, 포즈가 늘어도 안 깨진다.
        // cursorRig는 직렬화되지 않아 에디터 스크립트 리로드 후 null이 될 수 있다(usingModel은 남는다).
        if (usingModel && cursorCam != null && cursorRig != null)
        {
            Vector3 hotspot = HotspotWorld();
            cursorCam.transform.position = new Vector3(hotspot.x, hotspot.y, StagePos.z - 6f);
        }
    }

    /// <summary>커서가 실제로 "가리키는" 지점 — 펼친 손은 손바닥, 가리키는 손은 손끝.</summary>
    private Vector3 HotspotWorld()
    {
        switch (targetPose)
        {
            case HandPose.PointIndex:  return cursorRig.FingerTip(HandRig.Index);
            case HandPose.PointMiddle: return cursorRig.FingerTip(HandRig.Middle);
            default:                   return cursorRig.PalmCenter;
        }
    }

    // ================================================================
    // 공개 API
    // ================================================================

    public void SetActive(bool active)
    {
        isActive = active;
        cursorCanvas.gameObject.SetActive(active);
        Cursor.visible = !active;
        // 안 보일 땐 촬영 카메라도 끈다 — 커서 하나 때문에 매 프레임 RT를 그릴 이유가 없다.
        if (cursorCam != null) cursorCam.enabled = active;

        if (active)
        {
            // 활성화 시 기본 포즈로 리셋
            SetPoseImmediate(HandPose.Open);
        }
    }

    public void SetPose(HandPose pose)
    {
        if (targetPose == pose) return;
        targetPose = pose;
        targetVisualOffset = GetVisualOffset(pose);

        if (poseCoroutine != null)
            StopCoroutine(poseCoroutine);
        poseCoroutine = StartCoroutine(AnimatePose(pose, 0.1f));
    }

    public void SetPoseImmediate(HandPose pose)
    {
        if (poseCoroutine != null)
            StopCoroutine(poseCoroutine);
        targetPose = pose;
        currentPose = pose;
        targetVisualOffset = GetVisualOffset(pose);
        currentVisualOffset = targetVisualOffset;
        ApplyPose(pose);
    }

    private static Vector2 GetVisualOffset(HandPose pose)
    {
        switch (pose)
        {
            case HandPose.PointIndex:
                return new Vector2(8f, -41f);   // 검지 끝 = 마우스 위치
            case HandPose.PointMiddle:
                return new Vector2(0f, -45f);   // 중지 끝 = 마우스 위치
            default:
                return Vector2.zero;            // 손바닥 중심 = 마우스 위치
        }
    }

    // ================================================================
    // UI 빌드
    // ================================================================

    private void BuildCursorUI()
    {
        // 최상위 Canvas — Screen Space Overlay, sortingOrder 최대
        var canvasGo = new GameObject("HandCursorCanvas");
        canvasGo.transform.SetParent(transform);

        cursorCanvas = canvasGo.AddComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = 999;

        // Raycaster 없음 — 커서 자체가 클릭을 가로채면 안 됨
        // CanvasScaler 없음 — 픽셀 단위로 직접 제어

        // 커서 루트 (피벗: 손바닥 중심)
        var rootGo = new GameObject("CursorRoot");
        rootGo.transform.SetParent(canvasGo.transform, false);
        cursorRoot = rootGo.AddComponent<RectTransform>();
        cursorRoot.sizeDelta = Vector2.zero;

        // 손바닥 (사각형)
        var palmGo = new GameObject("Palm");
        palmGo.transform.SetParent(rootGo.transform, false);
        palmRect = palmGo.AddComponent<RectTransform>();
        palmRect.sizeDelta = new Vector2(40f, 34f);
        palmRect.anchoredPosition = Vector2.zero;

        var palmImg = palmGo.AddComponent<Image>();
        palmImg.color = new Color(1f, 0.85f, 0.6f, 1f); // 살색
        palmImg.raycastTarget = false;

        // v17: 3D 손 모델 우선. 성공하면 사각형 손은 만들지 않는다.
        if (BuildModelCursor())
        {
            palmImg.enabled = false;
            usingModel = true;
            return;
        }

        // 손가락 5개
        Vector2[] fingerPositions = {
            new Vector2(-18f, 4f),   // 엄지 (좌측 아래)
            new Vector2(-8f, 19f),   // 검지
            new Vector2(0f, 21f),    // 중지
            new Vector2(8f, 17f),    // 약지
            new Vector2(15f, 13f),   // 소지
        };
        float[] fingerLengths = { 16f, 22f, 24f, 20f, 14f };

        fingerPivots = new RectTransform[5];

        for (int i = 0; i < 5; i++)
        {
            // 피벗 (회전 기준점)
            var pivotGo = new GameObject($"Finger_{i}_Pivot");
            pivotGo.transform.SetParent(rootGo.transform, false);
            var pivotRect = pivotGo.AddComponent<RectTransform>();
            pivotRect.anchoredPosition = fingerPositions[i];
            pivotRect.sizeDelta = Vector2.zero;
            fingerPivots[i] = pivotRect;

            // 손가락 이미지 (피벗 아래쪽이 기준점, 위로 뻗음)
            var fingerGo = new GameObject($"Finger_{i}");
            fingerGo.transform.SetParent(pivotGo.transform, false);
            var fingerRect = fingerGo.AddComponent<RectTransform>();
            fingerRect.pivot = new Vector2(0.5f, 0f); // 하단 중앙이 기준
            fingerRect.anchoredPosition = Vector2.zero;
            fingerRect.sizeDelta = new Vector2(7f, fingerLengths[i]);

            var fingerImg = fingerGo.AddComponent<Image>();
            fingerImg.color = new Color(0.95f, 0.8f, 0.55f, 1f); // 살색 (약간 어두운)
            fingerImg.raycastTarget = false;
        }
    }

    /// <summary>3D 손을 화면 밖에 세우고 전용 카메라로 RenderTexture에 담는다.
    /// 실패하면 false — 호출부가 구 사각형 손으로 폴백해 커서가 사라지지 않게 한다.</summary>
    private bool BuildModelCursor()
    {
        var prefab = Resources.Load<GameObject>("Models/Hand");
        if (prefab == null) return false;

        var stageGo = new GameObject("CursorHandStage");
        stageGo.transform.SetParent(transform, false);
        stageGo.transform.position = StagePos;
        cursorStage = stageGo.transform;

        var go = Instantiate(prefab, cursorStage);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, 90f, -90f); // 게임과 같은 정면 회전

        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Destroy(stageGo); return false; }
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        go.transform.localScale *= StageHandSize / Mathf.Max(b.size.x, 0.0001f);

        // 게임 안의 손과 같은 살색 — 커서만 흰 석고처럼 보이면 다른 손으로 읽힌다.
        var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            skin.material = new Material(shader) { color = new Color(1f, 0.85f, 0.6f, 1f) };
        }

        cursorRig = HandRig.BuildFromBones(go.transform);
        if (cursorRig == null) { Destroy(stageGo); return false; }

        // 씬 조명에 의존하지 않게 전용 광원을 붙인다 — 타이틀/묘지/일시정지에서 조명이 제각각이라
        // 씬 광원만 믿으면 화면에 따라 손이 새까맣게 나온다. 포인트 광원이라 게임 쪽엔 닿지 않는다.
        var lightGo = new GameObject("CursorHandLight");
        lightGo.transform.SetParent(cursorStage, false);
        lightGo.transform.localPosition = new Vector3(-1.5f, 1.5f, -3f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 14f;
        light.intensity = 2.2f;

        cursorRT = new RenderTexture(CursorRTSize, CursorRTSize, 16, RenderTextureFormat.ARGB32);
        var camGo = new GameObject("CursorHandCamera");
        camGo.transform.SetParent(cursorStage, false);
        cursorCam = camGo.AddComponent<Camera>();
        cursorCam.orthographic = true;
        cursorCam.orthographicSize = StageCamSize;
        cursorCam.clearFlags = CameraClearFlags.SolidColor;
        cursorCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 배경
        cursorCam.nearClipPlane = 0.1f;
        cursorCam.farClipPlane = 20f;
        cursorCam.targetTexture = cursorRT;
        camGo.transform.position = StagePos + new Vector3(0f, 0f, -6f);
        camGo.transform.rotation = Quaternion.identity;

        var imgGo = new GameObject("HandCursorImage");
        imgGo.transform.SetParent(cursorRoot, false);
        var rect = imgGo.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(CursorPixelSize, CursorPixelSize);
        rect.anchoredPosition = Vector2.zero;   // 핫스팟이 텍스처 정중앙이라 오프셋이 필요 없다
        handImage = imgGo.AddComponent<RawImage>();
        handImage.texture = cursorRT;
        handImage.raycastTarget = false;

        return true;
    }

    // ================================================================
    // 포즈 적용
    // ================================================================

    /// <summary>손가락별 접힘량 (0 = 펼침, 1 = 접힘). 순서는 **엄지→소지** — 게임 손과 동일 규약.</summary>
    private static float[] GetTargetFolds(HandPose pose)
    {
        switch (pose)
        {
            case HandPose.PointIndex:  return new float[] { 1f, 0f, 1f, 1f, 1f };
            case HandPose.PointMiddle: return new float[] { 1f, 1f, 0f, 1f, 1f };
            default:                   return new float[] { 0f, 0f, 0f, 0f, 0f };
        }
    }

    private void ApplyFolds(float[] folds)
    {
        for (int i = 0; i < HandRig.FingerCount; i++)
        {
            currentFolds[i] = folds[i];
            if (usingModel) cursorRig?.SetFold(i, folds[i]);
            else if (fingerPivots != null) fingerPivots[i].localEulerAngles = new Vector3(folds[i] * FoldAngleX, 0f, 0f);
        }
    }

    private void ApplyPose(HandPose pose) => ApplyFolds(GetTargetFolds(pose));

    private IEnumerator AnimatePose(HandPose pose, float duration)
    {
        float[] target = GetTargetFolds(pose);
        float[] start = (float[])currentFolds.Clone();
        Vector2 startOffset = currentVisualOffset;

        float elapsed = 0f;
        var frame = new float[HandRig.FingerCount];
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale=0 (일시정지)에서도 동작
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * (2f - t); // EaseOut

            for (int i = 0; i < HandRig.FingerCount; i++)
                frame[i] = Mathf.Lerp(start[i], target[i], eased);
            ApplyFolds(frame);

            currentVisualOffset = Vector2.Lerp(startOffset, targetVisualOffset, eased);
            yield return null;
        }

        ApplyFolds(target);
        currentVisualOffset = targetVisualOffset;

        currentPose = pose;
        poseCoroutine = null;
    }
}
