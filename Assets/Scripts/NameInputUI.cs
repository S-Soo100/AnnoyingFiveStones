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

    // 시안 0822: ✕를 누르면 뜨는 "주의" 창. 라벨은 Show() 때 현재 언어로 다시 세팅한다.
    private GameObject discardPanel;
    private readonly System.Collections.Generic.List<(TextMeshProUGUI tmp, string key)> discardLabels = new();
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

        // ── 배경: 납골당 벽 + Dim70 (시안 Lanking 593:645/646/647) ───────────
        // 시안은 이 팝업을 **납골당 벽 위**에 띄운다. 예전엔 배경이 없어서 플레이하던
        // 방이 팝업 뒤로 그대로 보였다(2026-08-25 지적). 벽 타일은 납골당 화면과 같은 파일이다.
        BuildColumbariumBackdrop(canvasGo.transform);

        // ── 시안 "기록 저장" 창 (0822 Lanking 593:648) ────────────────────────
        // 설정/경고와 같은 창 부품이다. 몸통 안 배치(전부 시안 실측, 머리띠 83 아래 기준):
        //   안내문 560×51 @(60,137) / 입력칸 560×56 @(60,212) / 소요시간 560×51 @(60,292)
        //   저장 버튼 380×80 @(150,383)
        // (0817은 Slide가 540폭 @70이고 y가 145/212/284/375였다 — 0822에서 칸이 20 넓어지고
        //  안내문은 8 올라가고 소요시간·버튼은 8 내려갔다.)
        // ✕는 이제 **저장이 아니다** — 0822가 "기록 저장에서 엑스 누르면" 주의 창을 붙였다.
        panel = UIWindow.Create(canvasGo.transform, "NameInputPanel",
                                LocalizationManager.L("grave.record_title"), 686f,
                                OnCloseRequested, koreanFont, out var win, out _);

        const float H = UIWindow.HeaderH;
        const float SlideX = 60f, SlideW = 560f;

        promptLabel = UIWindow.Label(win, "Prompt", LocalizationManager.L("grave.name_prompt"),
                                     UIWindow.BodyPt, TextAlignmentOptions.Center, koreanFont);
        UIWindow.Place(promptLabel.rectTransform, SlideX, H + 137f, SlideW, 51f);
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
        inputField.characterLimit = 15; // 시안 0822 주석 "닉네임 15자 제한" — 유골함 칸(137px)에 3줄로 딱 들어가는 길이
        inputField.onSubmit.AddListener(_ => OnConfirm());

        timeLabel = UIWindow.Label(win, "Elapsed", "", UIWindow.BodyPt,
                                   TextAlignmentOptions.Center, koreanFont);
        UIWindow.Place(timeLabel.rectTransform, SlideX, H + 292f, SlideW, 51f);
        FitOneLine(timeLabel);   // 나눔고딕이 시안 폰트보다 넓어 "소요 시간 00:03:02"이 칸을 넘을 수 있다

        UIWindow.MakeButton(LocalizationManager.L("grave.save"), win, OnConfirm,
                            150f, H + 383f, 380f, 80f, koreanFont, out confirmBtnLabel);

        BuildDiscardPanel(canvasGo.transform, H);

        canvasGo.SetActive(false);
    }


    /// <summary>
    /// ✕가 부르는 "주의" 창 (시안 0822 — 주의 593:441 / Warning 593:456).
    /// 규격은 일시정지의 종료 확인 창과 **같은 부품**이다: Dialog 680×485, 문구 592×102 @(44,80),
    /// 버튼 284×80이 (44, 242)에서 308 간격.
    /// **안전한 쪽(아니오)이 왼쪽, 되돌릴 수 없는 쪽(게임 종료)이 오른쪽** — 시안 순서 그대로.
    /// </summary>
    private void BuildDiscardPanel(Transform parent, float headerH)
    {
        discardPanel = UIWindow.Create(parent, "DiscardConfirmPanel",
                                       LocalizationManager.L("discard.title"), 485f,
                                       OnDiscardCancel, koreanFont, out var win, out var titleLabel);
        discardLabels.Add((titleLabel, "discard.title"));

        var msg = UIWindow.Label(win, "Message", LocalizationManager.L("discard.message"),
                                 UIWindow.BodyPt, TextAlignmentOptions.TopLeft, koreanFont);
        msg.textWrappingMode = TextWrappingModes.Normal;
        UIWindow.Place(msg.rectTransform, 44f, headerH + 80f, 592f, 102f);
        discardLabels.Add((msg, "discard.message"));

        UIWindow.MakeButton(LocalizationManager.L("discard.cancel"), win, OnDiscardCancel,
                            44f, headerH + 242f, 284f, 80f, koreanFont, out var cancelLabel);
        discardLabels.Add((cancelLabel, "discard.cancel"));

        UIWindow.MakeButton(LocalizationManager.L("discard.confirm"), win, OnDiscardConfirm,
                            44f + 308f, headerH + 242f, 284f, 80f, koreanFont, out var confirmLabel);
        discardLabels.Add((confirmLabel, "discard.confirm"));

        discardPanel.SetActive(false);
    }

    /// <summary>
    /// 납골당 벽(1920×988 타일) 2장 + Dim70(순검정 0.698). 시안 Lanking 593:645~647 그대로다.
    /// 화면이 1080이라 타일 한 장으로는 92px가 모자라서 시안도 두 장을 쌓았다.
    /// Dim은 균일 알파 순검정이라 텍스처 없이 색으로 처리한다(에셋 픽셀 확인함).
    /// </summary>
    private void BuildColumbariumBackdrop(Transform parent)
    {
        var root = new GameObject("Backdrop", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var tex = Resources.Load<Texture2D>("Columbarium/wall_tile");
        if (tex == null)
        {
            Debug.LogWarning("[NameInputUI] 납골당 벽 없음: Resources/Columbarium/wall_tile — 검정으로 대체");
        }
        else
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 2; i++)
            {
                var tileGo = new GameObject($"WallTile{i}", typeof(RectTransform));
                tileGo.transform.SetParent(root.transform, false);
                UIWindow.Place(tileGo.GetComponent<RectTransform>(), 0f, i * 988f, 1920f, 988f);
                var img = tileGo.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
            }
        }

        var dimGo = new GameObject("Dim70", typeof(RectTransform));
        dimGo.transform.SetParent(root.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dim = dimGo.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, tex == null ? 1f : 0.698f);
        dim.raycastTarget = true;   // 창 밖 클릭이 뒤로 새지 않게 막는다
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
        // 이전 판에서 ✕를 눌렀다가 "아니오"로 돌아온 상태가 남아 있을 수 있다.
        panel.SetActive(true);
        if (discardPanel != null) discardPanel.SetActive(false);
        foreach (var (tmp, key) in discardLabels)
            if (tmp != null) tmp.text = LocalizationManager.L(key);
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

    /// <summary>✕ — 저장하지 않고 나가려는 시도. 바로 닫지 않고 주의 창을 세운다.</summary>
    private void OnCloseRequested()
    {
        if (!IsOpen) return;
        // 입력 포커스를 놓지 않으면 주의 창 위에서 타이핑이 계속 먹는다.
        inputField.DeactivateInputField();
        panel.SetActive(false);
        discardPanel.SetActive(true);
    }

    /// <summary>"아니오" — 기록 저장 창으로 돌아간다. ✕와 같은 자리로 복귀.</summary>
    private void OnDiscardCancel()
    {
        discardPanel.SetActive(false);
        panel.SetActive(true);
        inputField.Select();
        inputField.ActivateInputField();
    }

    /// <summary>
    /// "게임 종료" — 기록을 남기지 않고 타이틀로 돌아간다.
    ///
    /// ⚠️ onNameConfirmed를 **부르지 않는다**. 그래서 GameManager의 엔딩 코루틴은
    ///    `WaitUntil(nameConfirmed)`에 멈춰 있는데, RestartGame이 transitionCoroutine을
    ///    정지시키고 isTransitioning=false로 되돌리므로 그대로 정리된다.
    ///    (엔딩 코루틴은 GameManager L771에서 transitionCoroutine에 담긴다 — 확인함.)
    /// 이 프로젝트에서 창 안의 "게임 종료"는 앱 종료가 아니라 타이틀 복귀다
    /// (일시정지 창의 quit.confirm도 RestartGame()을 부른다). 홈 메뉴의 "게임 종료"만 앱 종료다.
    /// </summary>
    private void OnDiscardConfirm()
    {
        discardPanel.SetActive(false);
        panel.SetActive(true);      // 다음 판을 위해 기본 상태로 되돌려 둔다
        canvas.gameObject.SetActive(false);
        IsOpen = false;
        Time.timeScale = 1f;
        onNameConfirmed = null;     // 늦게 남아 호출되는 일이 없도록 끊는다
        GameManager.Instance?.RestartGame(true);
    }

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
