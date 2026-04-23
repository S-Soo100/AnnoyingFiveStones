using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 타이틀/기타 화면에서 호출 가능한 반투명 설정 팝업.
/// 현재 항목: 마스터 음량 슬라이더.
/// Screen Space Overlay Canvas (sortingOrder=260) — 타이틀(250)·일시정지(200) 위에 표시.
/// </summary>
public class SettingsPopupUI : MonoBehaviour
{
    public static SettingsPopupUI Instance { get; private set; }

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private TMP_FontAsset koreanFont;
    private TextMeshProUGUI volumeLabel;
    private AnnoyingSlider volumeSlider;  // Open()에서 값 갱신하기 위한 참조
    private bool isOpen;

    public static SettingsPopupUI EnsureInstance()
    {
        if (Instance == null)
            new GameObject("SettingsPopupUI").AddComponent<SettingsPopupUI>();
        return Instance;
    }

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

        var canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

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

        BuildPanel(rootGo.transform);
    }

    private void EnsureEventSystem()
    {
        var existingES = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existingES != null) return;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void BuildPanel(Transform parent)
    {
        // 반투명 배경 (클릭 시 닫기)
        var panelGo = new GameObject("Dim");
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);

        var dimBtn = panelGo.AddComponent<Button>();
        dimBtn.targetGraphic = bg;
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        // 가운데 박스
        var boxGo = new GameObject("Box");
        boxGo.transform.SetParent(panelGo.transform, false);
        var boxRect = boxGo.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(380f, 260f);
        boxRect.anchoredPosition = Vector2.zero;

        var boxImg = boxGo.AddComponent<Image>();
        boxImg.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

        // 박스 클릭이 뒷배경 닫기로 전파되지 않도록 빈 버튼 추가
        var eat = boxGo.AddComponent<Button>();
        eat.targetGraphic = boxImg;
        eat.transition = Selectable.Transition.None;

        var layout = boxGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 제목
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(boxGo.transform, false);
        titleGo.AddComponent<RectTransform>();

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "설정";
        titleTmp.fontSize = 40f;
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) titleTmp.font = koreanFont;

        var titleLE = titleGo.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 50f;

        // 음량 슬라이더
        CreateVolumeSlider(boxGo.transform);

        // 닫기 버튼
        CreateCloseButton(boxGo.transform);
    }

    private void CreateVolumeSlider(Transform parent)
    {
        var wrap = new GameObject("VolumeRow");
        wrap.transform.SetParent(parent, false);
        var wrapRect = wrap.AddComponent<RectTransform>();
        wrapRect.sizeDelta = new Vector2(320f, 60f);

        var wrapLE = wrap.AddComponent<LayoutElement>();
        wrapLE.preferredHeight = 60f;
        wrapLE.preferredWidth = 320f;

        // 라벨
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(wrap.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 26f);

        volumeLabel = labelGo.AddComponent<TextMeshProUGUI>();
        volumeLabel.fontSize = 24f;
        volumeLabel.color = Color.white;
        volumeLabel.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) volumeLabel.font = koreanFont;

        // 슬라이더
        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(wrap.transform, false);
        var sliderRect = sliderGo.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 4f);
        sliderRect.sizeDelta = new Vector2(0f, 26f);

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(sliderGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.22f, 1f);

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
        fillImg.color = new Color(0.45f, 0.75f, 0.95f, 1f);

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
        handleRect.sizeDelta = new Vector2(22f, 32f);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;

        volumeSlider = sliderGo.AddComponent<AnnoyingSlider>();
        volumeSlider.targetGraphic = handleImg;
        volumeSlider.fillRect = fillRect;
        volumeSlider.handleRect = handleRect;
        volumeSlider.direction = Slider.Direction.LeftToRight;
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = AudioManager.GetMasterVolume();

        UpdateVolumeLabel(volumeSlider.value);
        volumeSlider.onValueChanged.AddListener(v =>
        {
            AudioManager.ApplyVolume(v);
            UpdateVolumeLabel(v);
        });

        var hover = sliderGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointIndex;
    }

    private void UpdateVolumeLabel(float v)
    {
        if (volumeLabel != null)
            volumeLabel.text = $"음량 {Mathf.RoundToInt(v * 100f)}%";
    }

    private void CreateCloseButton(Transform parent)
    {
        var btnGo = new GameObject("CloseBtn");
        btnGo.transform.SetParent(parent, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(200f, 54f);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(0.30f, 0.30f, 0.30f, 0.90f);
        colors.highlightedColor = new Color(0.50f, 0.50f, 0.50f, 0.95f);
        colors.pressedColor     = new Color(0.20f, 0.20f, 0.20f, 1.00f);
        colors.selectedColor    = new Color(0.35f, 0.35f, 0.35f, 0.90f);
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(Close);

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
        tmp.text = "닫기";
        tmp.fontSize = 30f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;

        var le = btnGo.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;
        le.preferredHeight = 54f;
    }

    public void Open()
    {
        isOpen = true;
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;

        // v6-2: 현재 저장된 볼륨 값을 슬라이더에 재동기화
        if (volumeSlider != null)
        {
            float v = AudioManager.GetMasterVolume();
            volumeSlider.SetValueWithoutNotify(v);
            UpdateVolumeLabel(v);
        }
    }

    public void Close()
    {
        isOpen = false;
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }
}
