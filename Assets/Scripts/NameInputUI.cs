using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// 이름 입력 팝업 싱글톤.
/// Screen Space Overlay Canvas (sortingOrder=300).
/// GameManager.Start()에서 자동 생성.
/// </summary>
public class NameInputUI : MonoBehaviour
{
    public static NameInputUI Instance { get; private set; }

    private Canvas canvas;
    private GameObject panel;
    private TMP_InputField inputField;
    private Toggle testPlayToggle;
    private Action<string, bool> onNameConfirmed;
    private TMP_FontAsset koreanFont;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // UI 구조 빌드 (런타임 생성)
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        koreanFont = KoreanFont.GetTMP();

        // Canvas — Screen Space Overlay, sortingOrder=300
        var canvasGo = new GameObject("NameInputCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // 전체화면 반투명 배경
        var bgGo = CreateUIObject("Background", canvasGo.transform);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);
        StretchFull(bgGo.GetComponent<RectTransform>());

        // 중앙 패널
        var panelGo = CreateUIObject("Panel", canvasGo.transform);
        panel = panelGo;
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(300, 240);
        panelRt.anchoredPosition = Vector2.zero;

        // 라벨: "이름을 입력하세요"
        var labelGo = CreateUIObject("Label", panelGo.transform);
        var labelText = labelGo.AddComponent<TextMeshProUGUI>();
        labelText.text = "이름을 입력하세요";
        labelText.fontSize = 20;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) labelText.font = koreanFont;
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0, -20);
        labelRt.sizeDelta = new Vector2(-20, 36);

        // TMP_InputField
        var fieldGo = CreateUIObject("InputField", panelGo.transform);
        var fieldBg = fieldGo.AddComponent<Image>();
        fieldBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var fieldRt = fieldGo.GetComponent<RectTransform>();
        fieldRt.anchorMin = new Vector2(0.5f, 0.5f);
        fieldRt.anchorMax = new Vector2(0.5f, 0.5f);
        fieldRt.pivot = new Vector2(0.5f, 0.5f);
        fieldRt.sizeDelta = new Vector2(240, 40);
        fieldRt.anchoredPosition = new Vector2(0, 30);

        inputField = fieldGo.AddComponent<TMP_InputField>();
        inputField.characterLimit = 10;

        // InputField 텍스트 영역
        var textAreaGo = CreateUIObject("Text Area", fieldGo.transform);
        var textAreaRt = textAreaGo.GetComponent<RectTransform>();
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.sizeDelta = new Vector2(-10, -6);
        textAreaRt.anchoredPosition = Vector2.zero;
        var textAreaMask = textAreaGo.AddComponent<RectMask2D>();

        // Placeholder
        var placeholderGo = CreateUIObject("Placeholder", textAreaGo.transform);
        var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Player";
        placeholderText.fontSize = 18;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
        if (koreanFont != null) placeholderText.font = koreanFont;
        StretchFull(placeholderGo.GetComponent<RectTransform>());

        // 입력 텍스트
        var inputTextGo = CreateUIObject("Text", textAreaGo.transform);
        var inputText = inputTextGo.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 18;
        inputText.color = Color.white;
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        if (koreanFont != null) inputText.font = koreanFont;
        StretchFull(inputTextGo.GetComponent<RectTransform>());

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.textViewport = textAreaRt;

        // Toggle: "Test Play" — InputField 아래, 버튼 위 중간 영역
        var toggleContainerGo = CreateUIObject("ToggleContainer", panelGo.transform);
        var toggleContainerRt = toggleContainerGo.GetComponent<RectTransform>();
        toggleContainerRt.anchorMin = new Vector2(0.5f, 0.5f);
        toggleContainerRt.anchorMax = new Vector2(0.5f, 0.5f);
        toggleContainerRt.pivot = new Vector2(0.5f, 0.5f);
        toggleContainerRt.sizeDelta = new Vector2(160, 24);
        toggleContainerRt.anchoredPosition = new Vector2(0, -10);

        var toggleGo = CreateUIObject("Toggle", toggleContainerGo.transform);
        var toggleRt = toggleGo.GetComponent<RectTransform>();
        toggleRt.anchorMin = Vector2.zero;
        toggleRt.anchorMax = Vector2.one;
        toggleRt.offsetMin = Vector2.zero;
        toggleRt.offsetMax = Vector2.zero;
        testPlayToggle = toggleGo.AddComponent<Toggle>();
        testPlayToggle.isOn = false;

        // Background (체크박스 배경)
        var bgCheckGo = CreateUIObject("Background", toggleGo.transform);
        var bgCheckRt = bgCheckGo.GetComponent<RectTransform>();
        bgCheckRt.anchorMin = new Vector2(0f, 0.5f);
        bgCheckRt.anchorMax = new Vector2(0f, 0.5f);
        bgCheckRt.pivot = new Vector2(0f, 0.5f);
        bgCheckRt.sizeDelta = new Vector2(20, 20);
        bgCheckRt.anchoredPosition = Vector2.zero;
        var bgCheckImg = bgCheckGo.AddComponent<Image>();
        bgCheckImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Checkmark
        var checkmarkGo = CreateUIObject("Checkmark", bgCheckGo.transform);
        var checkmarkRt = checkmarkGo.GetComponent<RectTransform>();
        checkmarkRt.anchorMin = new Vector2(0.1f, 0.1f);
        checkmarkRt.anchorMax = new Vector2(0.9f, 0.9f);
        checkmarkRt.offsetMin = Vector2.zero;
        checkmarkRt.offsetMax = Vector2.zero;
        var checkmarkImg = checkmarkGo.AddComponent<Image>();
        checkmarkImg.color = Color.white;

        // Label "Test Play"
        var toggleLabelGo = CreateUIObject("Label", toggleGo.transform);
        var toggleLabelRt = toggleLabelGo.GetComponent<RectTransform>();
        toggleLabelRt.anchorMin = new Vector2(0f, 0f);
        toggleLabelRt.anchorMax = new Vector2(1f, 1f);
        toggleLabelRt.offsetMin = new Vector2(26, 0);
        toggleLabelRt.offsetMax = Vector2.zero;
        var toggleLabelTmp = toggleLabelGo.AddComponent<TextMeshProUGUI>();
        toggleLabelTmp.text = "Test Play";
        toggleLabelTmp.fontSize = 14;
        toggleLabelTmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        toggleLabelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (koreanFont != null) toggleLabelTmp.font = koreanFont;

        testPlayToggle.targetGraphic = bgCheckImg;
        testPlayToggle.graphic = checkmarkImg;

        // 버튼: "시작"
        var btnGo = CreateUIObject("StartButton", panelGo.transform);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.6f, 1f, 1f);
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.sizeDelta = new Vector2(120, 40);
        btnRt.anchoredPosition = new Vector2(0, 20);

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(OnConfirm);

        var btnTextGo = CreateUIObject("Text", btnGo.transform);
        var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
        btnText.text = "시작";
        btnText.fontSize = 18;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) btnText.font = koreanFont;
        StretchFull(btnTextGo.GetComponent<RectTransform>());

        // Enter 키 → 확인
        inputField.onSubmit.AddListener(_ => OnConfirm());

        // 초기 비활성화
        canvasGo.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 공개 API
    // ------------------------------------------------------------------

    /// <summary>
    /// 이름 입력 팝업 표시.
    /// onNameConfirmed(name, isTestPlay) 콜백 — 이름이 비어있으면 "Player" 사용.
    /// </summary>
    public void Show(Action<string, bool> onNameConfirmed)
    {
        this.onNameConfirmed = onNameConfirmed;
        inputField.text = "";
        testPlayToggle.isOn = false;
        canvas.gameObject.SetActive(true);
        IsOpen = true;
        Time.timeScale = 0f;

        // 입력 필드에 포커스
        inputField.Select();
        inputField.ActivateInputField();
    }

    // ------------------------------------------------------------------
    // 내부 처리
    // ------------------------------------------------------------------

    private void OnConfirm()
    {
        if (!IsOpen) return;

        string name = inputField.text.Trim();
        if (string.IsNullOrEmpty(name))
            name = "Player";

        bool isTestPlay = testPlayToggle != null && testPlayToggle.isOn;

        canvas.gameObject.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;

        onNameConfirmed?.Invoke(name, isTestPlay);
    }

    // ------------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------------

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
