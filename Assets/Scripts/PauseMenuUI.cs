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
    // 시안 창(Figma Settings 572:588 / Dialog 572:441) 공통 부품
    //   창 = 어둠막(#000 70%) + 둥근 몸통(#CCD9DD, r12, 테두리 #5A717F)
    //        + 머리띠 83(그라디언트, 제목 좌측 44, ✕ 우측)
    // 좌표는 전부 시안 px(1920×1080)이고, UISkin.Px()가 캔버스 단위로 옮긴다.
    // ──────────────────────────────────────────────────────────────────

    private const float WinW = 680f, HeaderH = 83f;
    private const float TitleX = 44f, TitleY = 16f;
    private const float CloseX = 610f, CloseY = 20f, CloseSize = 44f;
    private const float TitlePt = 40f, BodyPt = 40f;

    /// <summary>창 한 장을 만든다. 반환값은 화면 전체를 덮는 패널, out은 창 본체(자식 배치용).</summary>
    private GameObject CreateWindow(Transform parent, string name, string titleKey,
                                    float designH, UnityAction onClose, out RectTransform window)
    {
        var panelGo = new GameObject(name);
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelGo.AddComponent<Image>().color = UISkin.WindowDim;

        // 창 몸통
        var winGo = new GameObject("Window");
        winGo.transform.SetParent(panelGo.transform, false);
        window = winGo.AddComponent<RectTransform>();
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(UISkin.Px(WinW), UISkin.Px(designH));
        window.anchoredPosition = Vector2.zero;
        var bodyImg = winGo.AddComponent<Image>();
        bodyImg.sprite = UISkin.WindowBody;
        bodyImg.type = Image.Type.Sliced;

        // 머리띠 — 위 모서리만 둥글다. 아래 테두리선이 몸통과의 구분선이 된다.
        var headGo = new GameObject("Header");
        headGo.transform.SetParent(winGo.transform, false);
        var headRt = headGo.AddComponent<RectTransform>();
        headRt.anchorMin = new Vector2(0f, 1f);
        headRt.anchorMax = new Vector2(1f, 1f);
        headRt.pivot = new Vector2(0.5f, 1f);
        headRt.offsetMin = new Vector2(0f, -UISkin.Px(HeaderH));
        headRt.offsetMax = Vector2.zero;
        var headImg = headGo.AddComponent<Image>();
        headImg.sprite = UISkin.WindowHeader;
        headImg.type = Image.Type.Sliced;

        // 제목 — 시안은 왼쪽 정렬이다(가운데가 아니다)
        var titleTmp = CreateLabel(headGo.transform, "Title", LocalizationManager.L(titleKey),
                                   TitlePt, TextAlignmentOptions.Left);
        Place(titleTmp.rectTransform, TitleX, TitleY, WinW - TitleX * 2f, 51f);
        localized.Add((titleTmp, titleKey));

        // ✕ — 시안에는 회색 자리표시만 있어 아이콘은 코드로 그린다
        var closeGo = new GameObject("Close");
        closeGo.transform.SetParent(headGo.transform, false);
        var closeRt = closeGo.AddComponent<RectTransform>();
        Place(closeRt, CloseX, CloseY, CloseSize, CloseSize);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.sprite = UISkin.CloseIcon;
        closeImg.color = UISkin.Ink;
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(onClose);
        closeGo.AddComponent<HandCursorHoverTrigger>().HoverPose = HandPose.PointIndex;

        return panelGo;
    }

    /// <summary>부모 좌상단을 원점으로 시안 px 배치.</summary>
    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(UISkin.Px(w), UISkin.Px(h));
        rt.anchoredPosition = new Vector2(UISkin.Px(x), -UISkin.Px(y));
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
                                        float designPt, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = UISkin.Px(designPt);
        tmp.color = UISkin.Ink;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        if (koreanFont != null) tmp.font = koreanFont;
        return tmp;
    }

    // ──────────────────────────────────────────────────────────────────
    // 메인 패널 (일시정지 창) — 시안 Settings 680×683
    // ──────────────────────────────────────────────────────────────────

    private GameObject CreateMainPanel(Transform parent)
    {
        var panelGo = CreateWindow(parent, "MainPanel", "pause.title", 683f, OnResume, out var win);

        // 슬라이더 2개 — 시안: Bottom 안에서 (150,61)·(150,200), Bottom은 y=83부터
        bgmVolumeSlider = CreateSlider(win, "BGM", 150f, HeaderH + 61f,
            AudioManager.GetBGMVolume(), v => { AudioManager.SetBGMVolume(v); UpdateBGMVolumeLabel(v); },
            out bgmVolumeLabel);
        sfxVolumeSlider = CreateSlider(win, "SFX", 150f, HeaderH + 200f,
            AudioManager.GetSFXVolume(), v => { AudioManager.SetSFXVolume(v); UpdateSFXVolumeLabel(v); },
            out sfxVolumeLabel);
        UpdateBGMVolumeLabel(bgmVolumeSlider.value);
        UpdateSFXVolumeLabel(sfxVolumeSlider.value);

        CreateButton("pause.resume", win, OnResume, 150f, HeaderH + 339f, 380f, 80f);
        CreateButton("pause.quit",   win, OnQuit,   150f, HeaderH + 459f, 380f, 80f);

        return panelGo;
    }

    // ──────────────────────────────────────────────────────────────────
    // 슬라이더 — 시안 Slide 380×99 (제목 51 + 홈 36)
    // v18: BGM/SFX가 색만 다른 복붙이었다. 시안이 둘을 같은 부품으로 그려서 하나로 합쳤다.
    // ──────────────────────────────────────────────────────────────────

    private const float SlideW = 380f, TrackH = 36f, TrackY = 63f;
    private const float FillInset = 3f, HandleW = 20f;

    private Slider CreateSlider(Transform parent, string name, float x, float y,
                                float initial, UnityAction<float> onChanged,
                                out TextMeshProUGUI label)
    {
        var rowGo = new GameObject($"{name}Row", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        Place(rowGo.GetComponent<RectTransform>(), x, y, SlideW, 99f);

        label = CreateLabel(rowGo.transform, "Title", "", BodyPt, TextAlignmentOptions.Left);
        Place(label.rectTransform, 0f, 0f, SlideW, 51f);

        var sliderGo = new GameObject($"{name}Slider", typeof(RectTransform));
        sliderGo.transform.SetParent(rowGo.transform, false);
        Place(sliderGo.GetComponent<RectTransform>(), 0f, TrackY, SlideW, TrackH);

        // 홈 (흰 바탕 + 먹색 테두리)
        var trackImg = sliderGo.AddComponent<Image>();
        trackImg.sprite = UISkin.InsetBoxOf((int)TrackH);
        trackImg.type = Image.Type.Sliced;

        // 채움 — 홈 안쪽으로 3px
        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(UISkin.Px(FillInset), UISkin.Px(FillInset));
        fillAreaRt.offsetMax = new Vector2(-UISkin.Px(FillInset), -UISkin.Px(FillInset));

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fillGo.AddComponent<Image>().color = UISkin.SliderFill;

        // 손잡이
        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(UISkin.Px(HandleW * 0.5f), 0f);
        handleAreaRt.offsetMax = new Vector2(-UISkin.Px(HandleW * 0.5f), 0f);

        var handleGo = new GameObject("Handle", typeof(RectTransform));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(UISkin.Px(HandleW), UISkin.Px(TrackH));
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.sprite = UISkin.SliderHandle;
        handleImg.type = Image.Type.Simple;

        var slider = sliderGo.AddComponent<Slider>();
        slider.targetGraphic = handleImg;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initial;
        slider.onValueChanged.AddListener(onChanged);

        sliderGo.AddComponent<HandCursorHoverTrigger>().HoverPose = HandPose.PointIndex;
        return slider;
    }

    private void UpdateBGMVolumeLabel(float v)
    {
        if (bgmVolumeLabel != null)
            bgmVolumeLabel.text = LocalizationManager.LF("pause.music", Mathf.RoundToInt(v * 100f));
    }

    private void UpdateSFXVolumeLabel(float v)
    {
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = LocalizationManager.LF("pause.sfx", Mathf.RoundToInt(v * 100f));
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
        quitMsgTmp = CreateLabel(win, "Message", LocalizationManager.L("quit.message"),
                                 BodyPt, TextAlignmentOptions.TopLeft);
        quitMsgTmp.textWrappingMode = TextWrappingModes.Normal;
        Place(quitMsgTmp.rectTransform, 44f, HeaderH + 80f, 592f, 102f);

        // 버튼 2개 — 시안: 284×80이 (44, 몸통 242)에서 308 간격.
        // **안전한 쪽(취소)이 왼쪽, 되돌릴 수 없는 쪽(종료)이 오른쪽**이다.
        // 기존 코드는 확인이 먼저였다 — 시안 순서가 잘못 누를 위험이 더 낮다.
        CreateButton("quit.cancel",  win, OnQuitCancel,  44f,        HeaderH + 242f, 284f, 80f);
        CreateButton("quit.confirm", win, OnQuitConfirm, 44f + 308f, HeaderH + 242f, 284f, 80f);

        return panelGo;
    }

    // ──────────────────────────────────────────────────────────────────
    // 버튼 생성 헬퍼 — 시안 Button_outline(광택 베벨 + 먹색 라벨)
    // ──────────────────────────────────────────────────────────────────

    private GameObject CreateButton(string locKey, Transform parent, UnityAction onClick,
                                    float x, float y, float w, float h)
    {
        var btnGo = new GameObject($"Btn_{locKey}");
        btnGo.transform.SetParent(parent, false);
        Place(btnGo.AddComponent<RectTransform>(), x, y, w, h);

        var img = btnGo.AddComponent<Image>();
        img.sprite = UISkin.Raised;
        img.type = Image.Type.Sliced;
        img.color = Color.white;

        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.spriteState = new SpriteState
        {
            highlightedSprite = UISkin.RaisedHover,
            pressedSprite     = UISkin.Sunken,
            selectedSprite    = UISkin.RaisedHover,
            disabledSprite    = UISkin.Raised,
        };
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        // 호버 시 검지 가리킴 포즈
        btnGo.AddComponent<HandCursorHoverTrigger>().HoverPose = HandPose.PointIndex;

        var tmp = CreateLabel(btnGo.transform, "Label", LocalizationManager.L(locKey),
                              BodyPt, TextAlignmentOptions.Center);
        var lr = tmp.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        localized.Add((tmp, locKey));

        return btnGo;
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
