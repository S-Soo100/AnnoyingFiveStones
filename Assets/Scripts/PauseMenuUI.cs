using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// P4: ESC 일시정지 메뉴.
/// Screen Space - Overlay Canvas (sortingOrder=200) 런타임 생성.
/// Time.timeScale=0 상태에서 UI 이벤트(Button.onClick)는 정상 작동.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    private Canvas pauseCanvas;
    private CanvasGroup rootGroup;
    private GameObject mainPanel;
    private GameObject quitConfirmPanel;
    private TMP_FontAsset koreanFont;
    private bool isOpen;

    // v10 다국어: 정적 라벨(제목/버튼) → 키 매핑. Open() 때 현재 언어로 재설정.
    private readonly List<(TextMeshProUGUI tmp, string key)> localized = new();
    private TextMeshProUGUI quitMsgTmp;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────────────────────────────
    // 초기화
    // ──────────────────────────────────────────────────────────────────

    private void Init()
    {
        koreanFont = KoreanFont.GetTMP();

        // Canvas — Screen Space Overlay
        var canvasGo = new GameObject("PauseCanvas");
        canvasGo.transform.SetParent(transform);

        pauseCanvas = canvasGo.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 200;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // EventSystem 확인 — 없으면 자동 생성
        EnsureEventSystem();

        // rootGroup: 처음에는 비표시 + 입력 차단
        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        var rootRect = rootGo.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        rootGroup = rootGo.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;

        // 메인 패널 생성
        mainPanel = CreateMainPanel(rootGo.transform);

        // 종료 확인 팝업 생성
        quitConfirmPanel = CreateQuitConfirmPanel(rootGo.transform);
        quitConfirmPanel.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        var existingES = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null) return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // ──────────────────────────────────────────────────────────────────
    // 메인 패널 빌드
    // ──────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────
    // 창 두 장 — 부품은 UIWindow가 그린다.
    // v18: 창 만드는 코드가 여기 안에만 있었더니 홈의 설정창(SettingsPopupUI)이
    //      옛 검정 박스로 남아 있었다. 같은 모양을 두 군데서 각자 그리면
    //      한쪽만 고치는 일이 반드시 또 생긴다.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>제목 라벨까지 다국어 목록에 등록해주는 래퍼.</summary>
    private GameObject CreateWindow(Transform parent, string name, string titleKey,
                                    float designH, UnityAction onClose, out RectTransform window)
    {
        var panel = UIWindow.Create(parent, name, LocalizationManager.L(titleKey), designH,
                                    onClose, koreanFont, out window, out var titleTmp);
        localized.Add((titleTmp, titleKey));
        return panel;
    }

    private GameObject CreateButton(string locKey, Transform parent, UnityAction onClick,
                                    float x, float y, float w, float h)
    {
        var go = UIWindow.MakeButton(LocalizationManager.L(locKey), parent, onClick,
                                     x, y, w, h, koreanFont, out var tmp);
        localized.Add((tmp, locKey));
        return go;
    }

    // ──────────────────────────────────────────────────────────────────
    // 메인 패널 (일시정지 창) — 시안 Settings 680×683
    // ──────────────────────────────────────────────────────────────────

    private GameObject CreateMainPanel(Transform parent)
    {
        var panelGo = CreateWindow(parent, "MainPanel", "pause.title", 683f, OnResume, out var win);

        // 슬라이더 2개 — 시안: Bottom 안에서 (150,61)·(150,200), Bottom은 y=83부터
        bgmVolumeSlider = UIWindow.MakeSlider(win, "BGM", 150f, UIWindow.HeaderH + 61f,
            AudioManager.GetBGMVolume(), v => { AudioManager.SetBGMVolume(v); UpdateBGMVolumeLabel(v); },
            koreanFont, out bgmVolumeLabel);
        sfxVolumeSlider = UIWindow.MakeSlider(win, "SFX", 150f, UIWindow.HeaderH + 200f,
            AudioManager.GetSFXVolume(), v => { AudioManager.SetSFXVolume(v); UpdateSFXVolumeLabel(v); },
            koreanFont, out sfxVolumeLabel);
        UpdateBGMVolumeLabel(bgmVolumeSlider.value);
        UpdateSFXVolumeLabel(sfxVolumeSlider.value);

        CreateButton("pause.resume", win, OnResume, 150f, UIWindow.HeaderH + 339f, 380f, 80f);
        CreateButton("pause.quit",   win, OnQuit,   150f, UIWindow.HeaderH + 459f, 380f, 80f);

        return panelGo;
    }

    private void UpdateBGMVolumeLabel(float v)
    {
        if (bgmVolumeLabel != null)
            bgmVolumeLabel.text = LocalizationManager.L("pause.music");
    }

    private void UpdateSFXVolumeLabel(float v)
    {
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = LocalizationManager.L("pause.sfx");
    }

    private TextMeshProUGUI bgmVolumeLabel;
    private Slider bgmVolumeSlider;       // BGM 슬라이더 (Open()에서 값 갱신용)

    private TextMeshProUGUI sfxVolumeLabel;
    private Slider sfxVolumeSlider;       // SFX 슬라이더 (Open()에서 값 갱신용)


    /// <summary>정적 라벨(제목/버튼/종료문)을 현재 언어로 재설정. Open() 때마다 호출.</summary>
    private void RefreshStaticTexts()
    {
        foreach (var (tmp, key) in localized)
            if (tmp != null) tmp.text = LocalizationManager.L(key);
        if (quitMsgTmp != null) quitMsgTmp.text = LocalizationManager.L("quit.message");
    }

    // ──────────────────────────────────────────────────────────────────
    // 종료 확인 팝업 빌드
    // ──────────────────────────────────────────────────────────────────

    private GameObject CreateQuitConfirmPanel(Transform parent)
    {
        // 시안 Dialog 680×485 — 머리띠 83 + 몸통 402
        var panelGo = CreateWindow(parent, "QuitConfirmPanel", "quit.title", 485f, OnQuitCancel, out var win);

        // 확인 문구 — 시안은 592×102 @(44, 몸통 기준 80), **왼쪽 정렬**이다
        quitMsgTmp = UIWindow.Label(win, "Message", LocalizationManager.L("quit.message"),
                                    UIWindow.BodyPt, TextAlignmentOptions.TopLeft, koreanFont);
        quitMsgTmp.textWrappingMode = TextWrappingModes.Normal;
        UIWindow.Place(quitMsgTmp.rectTransform, 44f, UIWindow.HeaderH + 80f, 592f, 102f);

        // 버튼 2개 — 시안: 284×80이 (44, 몸통 242)에서 308 간격.
        // **안전한 쪽(취소)이 왼쪽, 되돌릴 수 없는 쪽(종료)이 오른쪽**이다.
        // 기존 코드는 확인이 먼저였다 — 시안 순서가 잘못 누를 위험이 더 낮다.
        CreateButton("quit.cancel",  win, OnQuitCancel,  44f,        UIWindow.HeaderH + 242f, 284f, 80f);
        CreateButton("quit.confirm", win, OnQuitConfirm, 44f + 308f, UIWindow.HeaderH + 242f, 284f, 80f);

        return panelGo;
    }

    // ──────────────────────────────────────────────────────────────────
    // 공개 API
    // ──────────────────────────────────────────────────────────────────

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    /// <summary>개발용 — 경고 창을 바로 띄운다. 시안 대조에 클릭이 필요 없게.</summary>
    public void DebugShowQuitConfirm()
    {
        if (!isOpen) Open();
        OnQuit();
    }

    private void Open()
    {
        isOpen = true;

        // 메인 패널 표시, 종료 확인은 숨김
        mainPanel.SetActive(true);
        quitConfirmPanel.SetActive(false);

        // v10 다국어: 열 때마다 현재 언어로 정적 라벨 재설정
        RefreshStaticTexts();

        // BGM 슬라이더 재동기화
        if (bgmVolumeSlider != null)
        {
            float bv = AudioManager.GetBGMVolume();
            bgmVolumeSlider.SetValueWithoutNotify(bv);
            UpdateBGMVolumeLabel(bv);
        }

        // v11: SFX 슬라이더 재동기화
        if (sfxVolumeSlider != null)
        {
            float sv = AudioManager.GetSFXVolume();
            sfxVolumeSlider.SetValueWithoutNotify(sv);
            UpdateSFXVolumeLabel(sv);
        }

        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;

        // 주의: timeScale=0 직전에 Close가 먼저 처리되어야 하므로
        // SetPaused → timeScale 순서를 지킨다
        GameManager.Instance?.SetPaused(true);
        Time.timeScale = 0f;

        // BGM 일시정지 (Time.timeScale=0은 AudioSource를 멈추지 않음)
        AudioManager.Instance?.PauseBGM();

        // 일시정지 중 손 커서 활성화 (메뉴 버튼 호버 피드백)
        HandCursorUI.Instance?.SetActive(true);

        Debug.Log("[PauseMenuUI] Opened. timeScale=0");
    }

    private void Close()
    {
        isOpen = false;

        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;

        // 손 커서 비활성화 (게임 중으로 복귀)
        HandCursorUI.Instance?.SetActive(false);

        // 복원 순서 엄수: timeScale 먼저, SetPaused 나중
        Time.timeScale = 1f;
        GameManager.Instance?.SetPaused(false);

        // BGM 재개
        AudioManager.Instance?.ResumeBGM();

        Debug.Log("[PauseMenuUI] Closed. timeScale=1");
    }

    // ──────────────────────────────────────────────────────────────────
    // 버튼 콜백
    // ──────────────────────────────────────────────────────────────────

    private void OnResume()
    {
        Close();
    }

    private void OnQuit()
    {
        mainPanel.SetActive(false);
        quitConfirmPanel.SetActive(true);
    }

    private void OnQuitConfirm()
    {
        // 일시정지 해제 후 메인 메뉴(타이틀)로 복귀.
        // RestartGame이 세션 리셋 + TitleScreenUI.Show까지 처리.
        Close();
        GameManager.Instance?.RestartGame();
    }

    private void OnQuitCancel()
    {
        quitConfirmPanel.SetActive(false);
        mainPanel.SetActive(true);
    }
}
