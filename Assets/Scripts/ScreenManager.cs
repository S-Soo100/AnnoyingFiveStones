using UnityEngine;
using UnityEngine.InputSystem;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    private const float ResizeDebounceSeconds = 0.4f;
    private const int SizeTolerancePixels = 4;

    private InputAction fullscreenAction;
    private int lastAppliedWidth;
    private int lastAppliedHeight;
    private int pendingWidth;
    private int pendingHeight;
    private float pendingTimer;
    private bool hasPendingResize;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        Screen.SetResolution(1280, 720, false);
        lastAppliedWidth = 1280;
        lastAppliedHeight = 720;

        // F11 전체화면 토글
        fullscreenAction = new InputAction("Fullscreen", InputActionType.Button);
        fullscreenAction.AddBinding("<Keyboard>/f11");
        fullscreenAction.performed += _ => ToggleFullscreen();
        fullscreenAction.Enable();
    }

    private void OnDisable()
    {
        fullscreenAction?.Disable();
    }

    private void Update()
    {
        if (Screen.fullScreen)
        {
            hasPendingResize = false;
            return;
        }

        int curW = Screen.width;
        int curH = Screen.height;

        // 드래그 중인 크기가 이전에 적용한 값과 허용오차 이상 벌어지면 대기 상태로 진입.
        // 이 프레임에서는 아무것도 하지 않고 "안정화"를 기다린다.
        bool differsFromApplied =
            Mathf.Abs(curW - lastAppliedWidth) > SizeTolerancePixels ||
            Mathf.Abs(curH - lastAppliedHeight) > SizeTolerancePixels;

        if (differsFromApplied)
        {
            bool differsFromPending =
                !hasPendingResize ||
                Mathf.Abs(curW - pendingWidth) > SizeTolerancePixels ||
                Mathf.Abs(curH - pendingHeight) > SizeTolerancePixels;

            if (differsFromPending)
            {
                // 아직 크기가 변하고 있음 → 타이머 리셋
                pendingWidth = curW;
                pendingHeight = curH;
                pendingTimer = 0f;
                hasPendingResize = true;
            }
            else
            {
                // 크기가 안정화되는 중 → 타이머 누적
                pendingTimer += Time.unscaledDeltaTime;
                if (pendingTimer >= ResizeDebounceSeconds)
                {
                    EnforceAspectRatio(pendingWidth, pendingHeight);
                    hasPendingResize = false;
                }
            }
        }
        else
        {
            hasPendingResize = false;
        }
    }

    public void ToggleFullscreen()
    {
        if (Screen.fullScreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(1280, 720, false);
            lastAppliedWidth = 1280;
            lastAppliedHeight = 720;
            hasPendingResize = false;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
        Debug.Log($"[ScreenManager] Fullscreen toggled: {Screen.fullScreen}");
    }

    private void EnforceAspectRatio(int requestedW, int requestedH)
    {
        int w = requestedW;
        int h = requestedH;

        // 더 많이 변한 쪽을 기준축으로 삼는다
        int dw = Mathf.Abs(requestedW - lastAppliedWidth);
        int dh = Mathf.Abs(requestedH - lastAppliedHeight);

        if (dw >= dh)
        {
            h = Mathf.RoundToInt(w / (16f / 9f));
        }
        else
        {
            w = Mathf.RoundToInt(h * (16f / 9f));
        }

        w = Mathf.Max(w, 640);
        h = Mathf.Max(h, 360);

        Screen.SetResolution(w, h, false);
        lastAppliedWidth = w;
        lastAppliedHeight = h;
    }
}
