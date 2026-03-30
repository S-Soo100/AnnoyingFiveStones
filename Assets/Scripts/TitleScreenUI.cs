using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitleScreenUI : MonoBehaviour
{
    public static TitleScreenUI Instance { get; private set; }
    public bool IsShowing { get; private set; }

    private CanvasGroup rootGroup;
    private TMP_FontAsset koreanFont;

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

    private void Init()
    {
        koreanFont = KoreanFont.GetTMP();

        // Canvas — Screen Space Overlay (전체 화면 커버)
        var canvasGo = new GameObject("TitleCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Root with CanvasGroup
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

        BuildUI(rootGo.transform);
    }

    private void EnsureEventSystem()
    {
        var existingES = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null) return;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void BuildUI(Transform parent)
    {
        // 전체 배경: 와인색 (보드와 동일한 색상)
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(parent, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.5f, 0.08f, 0.08f, 1f); // 와인색 (Cloth emissionColor과 유사)

        // 타이틀: "Catch Five Stones" — 상단 40% 영역
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(parent, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(900f, 100f);
        titleRect.anchoredPosition = new Vector2(0f, 80f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Catch Five Stones";
        titleTmp.fontSize = 72f;
        titleTmp.color = new Color(1f, 0.95f, 0.8f, 1f); // 크림색
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;
        if (koreanFont != null) titleTmp.font = koreanFont;

        // "플레이" 버튼 — 중앙 아래
        CreateMenuButton("플레이", parent, new Vector2(0f, -40f), 36, OnStartClicked);

        // "설정" 버튼 — 플레이 아래
        CreateMenuButton("설정", parent, new Vector2(0f, -100f), 30, () => {
            PauseMenuUI.Instance?.Toggle();
        });

        // "나가기" — 우측 상단
        var exitGo = new GameObject("ExitText");
        exitGo.transform.SetParent(parent, false);
        var exitRect = exitGo.AddComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(1f, 1f);
        exitRect.anchorMax = new Vector2(1f, 1f);
        exitRect.pivot = new Vector2(1f, 1f);
        exitRect.sizeDelta = new Vector2(120f, 50f);
        exitRect.anchoredPosition = new Vector2(-30f, -20f);

        var exitImg = exitGo.AddComponent<Image>();
        exitImg.color = new Color(0f, 0f, 0f, 0f); // 투명 클릭 영역

        var exitBtn = exitGo.AddComponent<Button>();
        exitBtn.targetGraphic = exitImg;
        exitBtn.onClick.AddListener(() => {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        var exitLabel = new GameObject("Label");
        exitLabel.transform.SetParent(exitGo.transform, false);
        var exitLabelRect = exitLabel.AddComponent<RectTransform>();
        exitLabelRect.anchorMin = Vector2.zero;
        exitLabelRect.anchorMax = Vector2.one;
        exitLabelRect.offsetMin = Vector2.zero;
        exitLabelRect.offsetMax = Vector2.zero;

        var exitTmp = exitLabel.AddComponent<TextMeshProUGUI>();
        exitTmp.text = "나가기";
        exitTmp.fontSize = 24f;
        exitTmp.color = new Color(1f, 1f, 1f, 0.6f);
        exitTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) exitTmp.font = koreanFont;
    }

    private void CreateMenuButton(string text, Transform parent, Vector2 pos, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject($"Btn_{text}");
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(240f, 50f);
        btnRect.anchoredPosition = pos;

        // 반투명 어두운 배경 (버튼 느낌)
        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.3f, 0.05f, 0.05f, 0.6f); // 어두운 와인색

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.3f, 0.05f, 0.05f, 0.6f);
        colors.highlightedColor = new Color(0.5f, 0.1f, 0.1f, 0.8f);
        colors.pressedColor = new Color(0.2f, 0.02f, 0.02f, 0.9f);
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
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;
    }

    // === 공개 API ===

    public void Show()
    {
        StopAllCoroutines();
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        IsShowing = true;
        Debug.Log("[TitleScreenUI] Show.");
    }

    public void Hide(System.Action onComplete = null)
    {
        StartCoroutine(DoFadeOut(onComplete));
    }

    private IEnumerator DoFadeOut(System.Action onComplete)
    {
        float elapsed = 0f;
        const float duration = 0.5f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rootGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        IsShowing = false;
        Debug.Log("[TitleScreenUI] Hidden.");
        onComplete?.Invoke();
    }

    private void OnStartClicked()
    {
        Hide(() => GameManager.Instance?.StartGameFromTitle());
    }
}
