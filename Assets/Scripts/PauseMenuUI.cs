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

    private GameObject CreateMainPanel(Transform parent)
    {
        // 반투명 배경 (전체 화면 덮기)
        var panelGo = new GameObject("MainPanel");
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);

        // 가운데 컨텐츠 박스
        var boxGo = new GameObject("ContentBox");
        boxGo.transform.SetParent(panelGo.transform, false);
        var boxRect = boxGo.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(320f, 540f);
        boxRect.anchoredPosition = Vector2.zero;

        var layout = boxGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 제목 텍스트
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(boxGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(280f, 60f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = LocalizationManager.L("pause.title");
        titleTmp.fontSize = 48f;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) titleTmp.font = koreanFont;
        localized.Add((titleTmp, "pause.title"));

        var titleLE = titleGo.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 60f;

        // v13: 마스터(음량) 슬라이더 제거 — BGM/SFX가 유일한 볼륨 제어

        // BGM 슬라이더
        CreateBGMSlider(boxGo.transform);

        // v11: 효과음(SFX) 슬라이더 — VerticalLayoutGroup이 BGM 아래에 자동 배치
        CreateSFXSlider(boxGo.transform);

        // 버튼 3개
        CreateButton("pause.resume", boxGo.transform, OnResume);
        CreateButton("pause.quit", boxGo.transform, OnQuit);

        return panelGo;
    }

    private TextMeshProUGUI bgmVolumeLabel;
    private Slider bgmVolumeSlider;       // BGM 슬라이더 (Open()에서 값 갱신용)

    private TextMeshProUGUI sfxVolumeLabel;
    private Slider sfxVolumeSlider;       // SFX 슬라이더 (Open()에서 값 갱신용)

    private void CreateBGMSlider(Transform parent)
    {
        var wrap = new GameObject("BGMVolumeRow");
        wrap.transform.SetParent(parent, false);
        var wrapRect = wrap.AddComponent<RectTransform>();
        wrapRect.sizeDelta = new Vector2(280f, 54f);

        var wrapLE = wrap.AddComponent<LayoutElement>();
        wrapLE.preferredHeight = 54f;
        wrapLE.preferredWidth = 280f;

        // 라벨 (상단)
        var labelGo = new GameObject("BGMLabel");
        labelGo.transform.SetParent(wrap.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 24f);

        bgmVolumeLabel = labelGo.AddComponent<TextMeshProUGUI>();
        bgmVolumeLabel.fontSize = 22f;
        bgmVolumeLabel.color = Color.white;
        bgmVolumeLabel.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) bgmVolumeLabel.font = koreanFont;

        // 슬라이더
        var sliderGo = new GameObject("BGMSlider");
        sliderGo.transform.SetParent(wrap.transform, false);
        var sliderRect = sliderGo.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 4f);
        sliderRect.sizeDelta = new Vector2(0f, 22f);

        // Background
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        // Fill Area + Fill
        var fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.45f, 0.85f, 0.65f, 1f); // 녹색 계열로 Master와 구분

        // Handle Slide Area + Handle
        var handleAreaGo = new GameObject("Handle Slide Area");
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRect = handleGo.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 28f);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;

        bgmVolumeSlider = sliderGo.AddComponent<Slider>();
        bgmVolumeSlider.targetGraphic = handleImg;
        bgmVolumeSlider.fillRect = fillRect;
        bgmVolumeSlider.handleRect = handleRect;
        bgmVolumeSlider.direction = Slider.Direction.LeftToRight;
        bgmVolumeSlider.minValue = 0f;
        bgmVolumeSlider.maxValue = 1f;
        bgmVolumeSlider.value = AudioManager.GetBGMVolume();

        UpdateBGMVolumeLabel(bgmVolumeSlider.value);
        bgmVolumeSlider.onValueChanged.AddListener(v =>
        {
            AudioManager.SetBGMVolume(v);
            UpdateBGMVolumeLabel(v);
        });

        // 호버 시 검지
        var hover = sliderGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointIndex;
    }

    private void UpdateBGMVolumeLabel(float v)
    {
        if (bgmVolumeLabel != null)
            bgmVolumeLabel.text = LocalizationManager.LF("pause.music", Mathf.RoundToInt(v * 100f));
    }

    // v11: 효과음(SFX) 슬라이더 — CreateBGMSlider 미러링. Fill 색상만 보라/하늘 톤으로 구분.
    private void CreateSFXSlider(Transform parent)
    {
        var wrap = new GameObject("SFXVolumeRow");
        wrap.transform.SetParent(parent, false);
        var wrapRect = wrap.AddComponent<RectTransform>();
        wrapRect.sizeDelta = new Vector2(280f, 54f);

        var wrapLE = wrap.AddComponent<LayoutElement>();
        wrapLE.preferredHeight = 54f;
        wrapLE.preferredWidth = 280f;

        // 라벨 (상단)
        var labelGo = new GameObject("SFXLabel");
        labelGo.transform.SetParent(wrap.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 24f);

        sfxVolumeLabel = labelGo.AddComponent<TextMeshProUGUI>();
        sfxVolumeLabel.fontSize = 22f;
        sfxVolumeLabel.color = Color.white;
        sfxVolumeLabel.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) sfxVolumeLabel.font = koreanFont;

        // 슬라이더
        var sliderGo = new GameObject("SFXSlider");
        sliderGo.transform.SetParent(wrap.transform, false);
        var sliderRect = sliderGo.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 4f);
        sliderRect.sizeDelta = new Vector2(0f, 22f);

        // Background
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        // Fill Area + Fill
        var fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-8f, 0f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.55f, 0.6f, 0.9f, 1f); // 보라/하늘 계열로 BGM(녹색)과 구분

        // Handle Slide Area + Handle
        var handleAreaGo = new GameObject("Handle Slide Area");
        handleAreaGo.transform.SetParent(sliderGo.transform, false);
        var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRect = handleGo.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 28f);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;

        sfxVolumeSlider = sliderGo.AddComponent<Slider>();
        sfxVolumeSlider.targetGraphic = handleImg;
        sfxVolumeSlider.fillRect = fillRect;
        sfxVolumeSlider.handleRect = handleRect;
        sfxVolumeSlider.direction = Slider.Direction.LeftToRight;
        sfxVolumeSlider.minValue = 0f;
        sfxVolumeSlider.maxValue = 1f;
        sfxVolumeSlider.value = AudioManager.GetSFXVolume();

        UpdateSFXVolumeLabel(sfxVolumeSlider.value);
        sfxVolumeSlider.onValueChanged.AddListener(v =>
        {
            AudioManager.SetSFXVolume(v);
            UpdateSFXVolumeLabel(v);
        });

        // 호버 시 검지
        var hover = sliderGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointIndex;
    }

    private void UpdateSFXVolumeLabel(float v)
    {
        if (sfxVolumeLabel != null)
            sfxVolumeLabel.text = LocalizationManager.LF("pause.sfx", Mathf.RoundToInt(v * 100f));
    }

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
        var panelGo = new GameObject("QuitConfirmPanel");
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);

        // 가운데 컨텐츠 박스
        var boxGo = new GameObject("ContentBox");
        boxGo.transform.SetParent(panelGo.transform, false);
        var boxRect = boxGo.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(360f, 240f);
        boxRect.anchoredPosition = Vector2.zero;

        var layout = boxGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20f;
        layout.padding = new RectOffset(20, 20, 24, 20);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 확인 텍스트
        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(boxGo.transform, false);
        var msgRect = msgGo.AddComponent<RectTransform>();
        msgRect.sizeDelta = new Vector2(320f, 70f);

        var msgTmp = msgGo.AddComponent<TextMeshProUGUI>();
        msgTmp.text = LocalizationManager.L("quit.message"); // v10: 확정문 + 다국어 첫 소비자
        msgTmp.fontSize = 36f;
        msgTmp.color = Color.white;
        msgTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) msgTmp.font = koreanFont;
        quitMsgTmp = msgTmp;

        var msgLE = msgGo.AddComponent<LayoutElement>();
        msgLE.preferredHeight = 70f;

        // 버튼 가로 배치용 HorizontalLayoutGroup
        var btnRowGo = new GameObject("ButtonRow");
        btnRowGo.transform.SetParent(boxGo.transform, false);
        var btnRowRect = btnRowGo.AddComponent<RectTransform>();
        btnRowRect.sizeDelta = new Vector2(320f, 60f);

        var hLayout = btnRowGo.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.spacing = 16f;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        var btnRowLE = btnRowGo.AddComponent<LayoutElement>();
        btnRowLE.preferredHeight = 60f;

        CreateButton("quit.confirm", btnRowGo.transform, OnQuitConfirm, new Vector2(130f, 56f));
        CreateButton("quit.cancel", btnRowGo.transform, OnQuitCancel, new Vector2(130f, 56f));

        return panelGo;
    }

    // ──────────────────────────────────────────────────────────────────
    // 버튼 생성 헬퍼
    // ──────────────────────────────────────────────────────────────────

    private GameObject CreateButton(string locKey, Transform parent, UnityAction onClick,
        Vector2 size = default)
    {
        if (size == default) size = new Vector2(240f, 56f);

        var btnGo = new GameObject($"Btn_{locKey}");
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.sizeDelta = size;

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.85f);

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor    = new Color(0.30f, 0.30f, 0.30f, 0.85f);
        colors.highlightedColor = new Color(0.50f, 0.50f, 0.50f, 0.90f);
        colors.pressedColor   = new Color(0.20f, 0.20f, 0.20f, 1.00f);
        colors.selectedColor  = new Color(0.35f, 0.35f, 0.35f, 0.90f);
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        // 호버 시 검지 가리킴 포즈
        var hover = btnGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointIndex;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = LocalizationManager.L(locKey);
        tmp.fontSize = 32f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;
        localized.Add((tmp, locKey));

        var le = btnGo.AddComponent<LayoutElement>();
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;

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
