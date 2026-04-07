using UnityEngine;
using UnityEngine.InputSystem;

public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }

    private InputAction fullscreenAction;
    private int lastWidth;
    private int lastHeight;

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
        lastWidth = 1280;
        lastHeight = 720;

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
        // 창 크기 변경 감지 → 16:9 비율 강제
        if (!Screen.fullScreen && (Screen.width != lastWidth || Screen.height != lastHeight))
        {
            EnforceAspectRatio();
        }
    }

    public void ToggleFullscreen()
    {
        if (Screen.fullScreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(1280, 720, false);
            lastWidth = 1280;
            lastHeight = 720;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
        Debug.Log($"[ScreenManager] Fullscreen toggled: {Screen.fullScreen}");
    }

    private void EnforceAspectRatio()
    {
        int w = Screen.width;
        int h = Screen.height;

        // 어느 쪽이 변경되었는지 감지
        if (w != lastWidth)
        {
            // 너비 기준으로 높이 조정
            h = Mathf.RoundToInt(w / (16f / 9f));
        }
        else if (h != lastHeight)
        {
            // 높이 기준으로 너비 조정
            w = Mathf.RoundToInt(h * (16f / 9f));
        }

        // 최소 크기 제한
        w = Mathf.Max(w, 640);
        h = Mathf.Max(h, 360);

        Screen.SetResolution(w, h, false);
        lastWidth = w;
        lastHeight = h;
    }
}
