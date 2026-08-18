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

        // Canvas — Screen Space Overlay, sortingOrder=300 (설정 260·일시정지 200 위)
        var canvasGo = new GameObject("NameInputCanvas");
        canvasGo.transform.SetParent(transform);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ── 시안 "기록 저장" 창 (Figma 572:628) ──────────────────────────────
        // 설정/경고와 같은 창 부품이다. 몸통 안 배치(전부 시안 실측, 머리띠 83 아래 기준):
        //   안내문 540×51 @(70,145) / 입력칸 540×56 @(70,212) / 소요시간 540×51 @(70,284)
        //   저장 버튼 380×80 @(150,375)
        // ✕는 저장과 같은 동작으로 묶었다 — 이 창은 "취소"가 성립하지 않는다.
        // 기록을 남기지 않고 나갈 길이 없고, 이름이 비면 기본값이 들어간다.
        panel = UIWindow.Create(canvasGo.transform, "NameInputPanel",
                                LocalizationManager.L("grave.record_title"), 686f,
                                OnConfirm, koreanFont, out var win, out _);

        const float H = UIWindow.HeaderH;
        const float SlideX = 70f, SlideW = 540f;

        promptLabel = UIWindow.Label(win, "Prompt", LocalizationManager.L("grave.name_prompt"),
                                     UIWindow.BodyPt, TextAlignmentOptions.Center, koreanFont);
        UIWindow.Place(promptLabel.rectTransform, SlideX, H + 145f, SlideW, 51f);
        // 시안 폰트(Iosevka)보다 나눔고딕이 넓어 40px 그대로면 "지어주세 / 요"로 접힌다.
        // 시안은 한 줄이므로 줄바꿈을 막고 칸(540) 안에 들어오도록 줄인다. 로고와 같은 처리다.
        FitOneLine(promptLabel);

        // 입력칸 — 시안 InBox(흰 바탕 + 먹색 테두리). 상태박스 흰 칸과 같은 부품이다.
        var fieldGo = new GameObject("Field", typeof(RectTransform));
        fieldGo.transform.SetParent(win, false);
        UIWindow.Place(fieldGo.GetComponent<RectTransform>(), SlideX, H + 212f, SlideW, 56f);
        var fieldImg = fieldGo.AddComponent<Image>();
        fieldImg.sprite = UISkin.InsetBoxOf(56);
        fieldImg.type = Image.Type.Sliced;

        var textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(fieldGo.transform, false);
        var taRt = textArea.GetComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero;
        taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(UISkin.Px(23f), 0f);
        taRt.offsetMax = new Vector2(-UISkin.Px(23f), 0f);
        textArea.AddComponent<RectMask2D>();

        var placeholder = UIWindow.Label(textArea.transform, "Placeholder", "",
                                         35.4f, TextAlignmentOptions.Center, koreanFont);
        placeholder.color = new Color(UISkin.Ink.r / 255f, UISkin.Ink.g / 255f, UISkin.Ink.b / 255f, 0.35f);
        StretchFull(placeholder.rectTransform);

        var inputText = UIWindow.Label(textArea.transform, "Text", "",
                                       35.4f, TextAlignmentOptions.Center, koreanFont);
        StretchFull(inputText.rectTransform);

        inputField = fieldGo.AddComponent<TMP_InputField>();
        inputField.textViewport = taRt;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.targetGraphic = fieldImg;
        inputField.characterLimit = 16;
        inputField.onSubmit.AddListener(_ => OnConfirm());

        timeLabel = UIWindow.Label(win, "Elapsed", "", UIWindow.BodyPt,
                                   TextAlignmentOptions.Center, koreanFont);
        UIWindow.Place(timeLabel.rectTransform, SlideX, H + 284f, SlideW, 51f);
        FitOneLine(timeLabel);   // 영문 "Time taken 00:03:02"이 더 길다

        UIWindow.MakeButton(LocalizationManager.L("grave.save"), win, OnConfirm,
                            150f, H + 375f, 380f, 80f, koreanFont, out confirmBtnLabel);

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

    /// <summary>한 줄 유지 — 넘치면 글자를 줄인다. 시안은 이 문구들이 모두 한 줄이다.</summary>
    private static void FitOneLine(TextMeshProUGUI tmp)
    {
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = UISkin.Px(UIWindow.BodyPt);
        tmp.fontSizeMin = UISkin.Px(24f);
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
