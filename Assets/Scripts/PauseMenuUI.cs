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
        boxRect.sizeDelta = new Vector2(320f, 300f);
        boxRect.anchoredPosition = Vector2.zero;

        var layout = boxGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 16f;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 제목 텍스트
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(boxGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(280f, 60f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "일시정지";
        titleTmp.fontSize = 48f;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) titleTmp.font = koreanFont;

        var titleLE = titleGo.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 60f;

        // 버튼 3개
        CreateButton("게임 재개", boxGo.transform, OnResume);
        CreateButton("게임 초기화", boxGo.transform, OnReset);
        CreateButton("게임 종료", boxGo.transform, OnQuit);

        return panelGo;
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
        msgTmp.text = "정말 종료?\n자신없어요?";
        msgTmp.fontSize = 36f;
        msgTmp.color = Color.white;
        msgTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) msgTmp.font = koreanFont;

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

        CreateButton("확인", btnRowGo.transform, OnQuitConfirm, new Vector2(130f, 56f));
        CreateButton("취소", btnRowGo.transform, OnQuitCancel, new Vector2(130f, 56f));

        return panelGo;
    }

    // ──────────────────────────────────────────────────────────────────
    // 버튼 생성 헬퍼
    // ──────────────────────────────────────────────────────────────────

    private GameObject CreateButton(string text, Transform parent, UnityAction onClick,
        Vector2 size = default)
    {
        if (size == default) size = new Vector2(240f, 56f);

        var btnGo = new GameObject($"Btn_{text}");
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

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 32f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;

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

        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;

        // 주의: timeScale=0 직전에 Close가 먼저 처리되어야 하므로
        // SetPaused → timeScale 순서를 지킨다
        GameManager.Instance?.SetPaused(true);
        Time.timeScale = 0f;

        Debug.Log("[PauseMenuUI] Opened. timeScale=0");
    }

    private void Close()
    {
        isOpen = false;

        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;

        // 복원 순서 엄수: timeScale 먼저, SetPaused 나중
        Time.timeScale = 1f;
        GameManager.Instance?.SetPaused(false);

        Debug.Log("[PauseMenuUI] Closed. timeScale=1");
    }

    // ──────────────────────────────────────────────────────────────────
    // 버튼 콜백
    // ──────────────────────────────────────────────────────────────────

    private void OnResume()
    {
        Close();
    }

    private void OnReset()
    {
        // timeScale 복원 먼저 — 이후 StartStage 코루틴이 WaitForSeconds를 제대로 소화
        Close();
        GameSession.Instance?.ResetAll();
        SidePanelUI.Instance?.Refresh();
        GameManager.Instance?.StartStage(1);
    }

    private void OnQuit()
    {
        mainPanel.SetActive(false);
        quitConfirmPanel.SetActive(true);
    }

    private void OnQuitConfirm()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnQuitCancel()
    {
        quitConfirmPanel.SetActive(false);
        mainPanel.SetActive(true);
    }
}
