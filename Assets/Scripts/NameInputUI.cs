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
    private TextMeshProUGUI timeLabel;
    private TextMeshProUGUI promptLabel;    // v10 다국어: "묘비에 새길 이름을…"
    private TextMeshProUGUI confirmBtnLabel; // v10 다국어: "이 이름으로 저장"
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
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 전체화면 흰색 불투명 배경 (Figma: 순수 흰 전체화면)
        var bgGo = CreateUIObject("Background", canvasGo.transform);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = Color.white;
        StretchFull(bgGo.GetComponent<RectTransform>());

        // 풀스크린 투명 컨테이너 패널 (자식은 이 패널 = 화면 기준 중앙 앵커)
        var panelGo = CreateUIObject("Panel", canvasGo.transform);
        panel = panelGo;
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0);
        var panelRt = panelGo.GetComponent<RectTransform>();
        StretchFull(panelRt);

        // 타이틀: "묘비에 새길 이름을…" (Figma: 검은 굵은 대제목)
        var labelGo = CreateUIObject("Label", panelGo.transform);
        var labelText = labelGo.AddComponent<TextMeshProUGUI>();
        labelText.text = LocalizationManager.L("grave.name_prompt"); // v10: 다국어
        promptLabel = labelText;
        labelText.fontSize = 44;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = Color.black;
        labelText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) labelText.font = koreanFont;
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 0.5f);
        labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(900, 70);
        labelRt.anchoredPosition = new Vector2(0, 120);

        // v10: 소요 시간 표시 (기획 5-2)
        var timeGo = CreateUIObject("TimeLabel", panelGo.transform);
        timeLabel = timeGo.AddComponent<TextMeshProUGUI>();
        timeLabel.fontSize = 26;
        timeLabel.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        timeLabel.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) timeLabel.font = koreanFont;
        var timeRt = timeGo.GetComponent<RectTransform>();
        timeRt.anchorMin = new Vector2(0.5f, 0.5f);
        timeRt.anchorMax = new Vector2(0.5f, 0.5f);
        timeRt.pivot = new Vector2(0.5f, 0.5f);
        timeRt.anchoredPosition = new Vector2(0, 50);
        timeRt.sizeDelta = new Vector2(600, 40);

        // TMP_InputField (Figma: 회색 필드)
        var fieldGo = CreateUIObject("InputField", panelGo.transform);
        var fieldBg = fieldGo.AddComponent<Image>();
        fieldBg.color = new Color(0.83f, 0.83f, 0.83f, 1f);
        var fieldRt = fieldGo.GetComponent<RectTransform>();
        fieldRt.anchorMin = new Vector2(0.5f, 0.5f);
        fieldRt.anchorMax = new Vector2(0.5f, 0.5f);
        fieldRt.pivot = new Vector2(0.5f, 0.5f);
        fieldRt.sizeDelta = new Vector2(520, 60);
        fieldRt.anchoredPosition = new Vector2(0, -30);

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

        // Placeholder (Figma: 빈 회색 박스 — 안내문 없음)
        var placeholderGo = CreateUIObject("Placeholder", textAreaGo.transform);
        var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "";
        placeholderText.fontSize = 26;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        placeholderText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) placeholderText.font = koreanFont;
        StretchFull(placeholderGo.GetComponent<RectTransform>());

        // 입력 텍스트 (Figma: 검은 텍스트 중앙 정렬)
        var inputTextGo = CreateUIObject("Text", textAreaGo.transform);
        var inputText = inputTextGo.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 26;
        inputText.color = Color.black;
        inputText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) inputText.font = koreanFont;
        StretchFull(inputTextGo.GetComponent<RectTransform>());

        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.textViewport = textAreaRt;

        // 버튼: "이 이름으로 저장" (Figma: 검은 버튼 + 흰 굵은 글씨)
        var btnGo = CreateUIObject("StartButton", panelGo.transform);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = Color.black;
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta = new Vector2(260, 66);
        btnRt.anchoredPosition = new Vector2(0, -130);

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(OnConfirm);

        var btnTextGo = CreateUIObject("Text", btnGo.transform);
        var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
        btnText.text = LocalizationManager.L("grave.save"); // Figma: 이 이름으로 저장
        confirmBtnLabel = btnText;
        btnText.fontSize = 26;
        btnText.fontStyle = FontStyles.Bold;
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
    public void Show(float clearTime, Action<string, bool> onNameConfirmed)
    {
        this.onNameConfirmed = onNameConfirmed;
        // v10 다국어: 표시 시점에 현재 언어로 갱신
        if (promptLabel != null) promptLabel.text = LocalizationManager.L("grave.name_prompt");
        if (confirmBtnLabel != null) confirmBtnLabel.text = LocalizationManager.L("grave.save");
        if (timeLabel != null) timeLabel.text = LocalizationManager.LF("grave.elapsed", FormatTime(clearTime));
        inputField.text = "";
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

        bool isTestPlay = GameSession.Instance != null && GameSession.Instance.IsTestPlay;

        canvas.gameObject.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;

        onNameConfirmed?.Invoke(name, isTestPlay);
    }

    // ------------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------------

    private static string FormatTime(float seconds)
    {
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        int s = (int)(seconds % 60);
        return $"{h:00}:{m:00}:{s:00}";
    }

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
