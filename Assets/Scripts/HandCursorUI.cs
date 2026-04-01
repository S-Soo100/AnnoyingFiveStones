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
    private RectTransform[] fingerPivots;
    private InputAction pointerAction;

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
        cursorRoot.position = screenPos + currentVisualOffset;
    }

    // ================================================================
    // 공개 API
    // ================================================================

    public void SetActive(bool active)
    {
        isActive = active;
        cursorCanvas.gameObject.SetActive(active);
        Cursor.visible = !active;

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

    // ================================================================
    // 포즈 적용
    // ================================================================

    private float[] GetTargetAngles(HandPose pose)
    {
        // 반환값: [엄지, 검지, 중지, 약지, 소지]의 X축 회전 각도
        // 0 = 펼침, FoldAngleX = 접힘 (화면 안쪽으로 말림)
        switch (pose)
        {
            case HandPose.Open:
                return new float[] { 0f, 0f, 0f, 0f, 0f };

            case HandPose.PointIndex:
                // 검지(1)만 펼침, 나머지 접힘
                return new float[] { FoldAngleX, 0f, FoldAngleX, FoldAngleX, FoldAngleX };

            case HandPose.PointMiddle:
                // 중지(2)만 펼침, 나머지 접힘
                return new float[] { FoldAngleX, FoldAngleX, 0f, FoldAngleX, FoldAngleX };

            default:
                return new float[] { 0f, 0f, 0f, 0f, 0f };
        }
    }

    private void ApplyPose(HandPose pose)
    {
        float[] angles = GetTargetAngles(pose);
        for (int i = 0; i < 5; i++)
        {
            fingerPivots[i].localEulerAngles = new Vector3(angles[i], 0f, 0f);
        }
    }

    private IEnumerator AnimatePose(HandPose pose, float duration)
    {
        float[] targetAngles = GetTargetAngles(pose);
        float[] startAngles = new float[5];
        Vector2 startOffset = currentVisualOffset;

        for (int i = 0; i < 5; i++)
        {
            startAngles[i] = fingerPivots[i].localEulerAngles.x;
            if (startAngles[i] > 180f) startAngles[i] -= 360f;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale=0 (일시정지)에서도 동작
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * (2f - t); // EaseOut

            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.Lerp(startAngles[i], targetAngles[i], eased);
                fingerPivots[i].localEulerAngles = new Vector3(angle, 0f, 0f);
            }
            currentVisualOffset = Vector2.Lerp(startOffset, targetVisualOffset, eased);
            yield return null;
        }

        // 최종 보정
        for (int i = 0; i < 5; i++)
        {
            fingerPivots[i].localEulerAngles = new Vector3(targetAngles[i], 0f, 0f);
        }
        currentVisualOffset = targetVisualOffset;

        currentPose = pose;
        poseCoroutine = null;
    }
}
