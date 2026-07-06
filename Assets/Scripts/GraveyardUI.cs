using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 묘지 파노라마 싱글톤.
/// ALL CLEAR 후 5초 뒤 표시. 전체 기록을 비석으로 시각화, 자동 스크롤.
/// Screen Space Overlay Canvas (sortingOrder=200).
/// </summary>
public class GraveyardUI : MonoBehaviour
{
    public static GraveyardUI Instance { get; private set; }

    private Canvas canvas;
    private ScrollRect scrollRect;
    private RectTransform content;
    private TextMeshProUGUI statusText;
    private GameObject restartHintWrapper;
    private TMP_FontAsset koreanFont;

    private Coroutine scrollCoroutine;
    private Coroutine blinkCoroutine;
    private bool isShowing;
    private bool hasReachedEnd;

    private InputAction tapAction;

    public bool IsShowing => isShowing;

    // ------------------------------------------------------------------
    // 생명주기
    // ------------------------------------------------------------------

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

    private void OnEnable()
    {
        tapAction = new InputAction("GraveyardTap", InputActionType.Button);
        tapAction.AddBinding("<Mouse>/leftButton");
        tapAction.AddBinding("<Touchscreen>/primaryTouch/press");
        tapAction.performed += OnTap;
        tapAction.Enable();
    }

    private void OnDisable()
    {
        tapAction.performed -= OnTap;
        tapAction.Disable();
    }

    // ------------------------------------------------------------------
    // 공개 API
    // ------------------------------------------------------------------

    public void Show(float myTime, string myName, int myRegressionCount = 0, bool isTestPlay = false)
    {
        canvas.gameObject.SetActive(true);
        isShowing = true;
        hasReachedEnd = false;
        restartHintWrapper.SetActive(false);
        statusText.text = "불러오는 중...";

        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);
        scrollCoroutine = StartCoroutine(CoLoadAndScroll(myTime, myName, myRegressionCount, isTestPlay));
    }

    public void Hide()
    {
        if (scrollCoroutine != null)
        {
            StopCoroutine(scrollCoroutine);
            scrollCoroutine = null;
        }
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        // Content 자식 전부 Destroy
        if (content != null)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        canvas.gameObject.SetActive(false);
        isShowing = false;
        hasReachedEnd = false;
    }

    // ------------------------------------------------------------------
    // 탭 입력 처리
    // ------------------------------------------------------------------

    private void OnTap(InputAction.CallbackContext ctx)
    {
        if (!isShowing) return;

        if (!hasReachedEnd)
        {
            // 스크롤 스킵
            if (scrollCoroutine != null)
            {
                StopCoroutine(scrollCoroutine);
                scrollCoroutine = null;
            }
            scrollCoroutine = StartCoroutine(CoSkipScroll());
        }
        // v9(260703): 스크롤 완료 후에는 화면 버튼(Play Again/Go Home)으로 처리 — 탭 재시작 제거
    }

    // ------------------------------------------------------------------
    // 로드 + 스크롤 코루틴
    // ------------------------------------------------------------------

    private IEnumerator CoLoadAndScroll(float myTime, string myName, int myRegressionCount, bool isTestPlay = false)
    {
        List<RecordEntry> records = null;
        bool done = false;

        SupabaseManager.Instance?.GetAllRecords(result =>
        {
            records = result;
            done = true;
        });

        float waited = 0f;
        while (!done && waited < 15f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (records == null)
        {
            statusText.text = "기록을 불러올 수 없습니다";
            yield return new WaitForSecondsRealtime(5f);
            GameManager.Instance?.RestartGame();
            yield break;
        }

        statusText.text = "";

        // Content 기존 자식 정리
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        yield return null; // Destroy 반영 대기

        float padWidth = Mathf.Max(Screen.width / 2f, 320f);

        // LeftPadding
        var leftPad = CreatePadding("LeftPadding", padWidth);

        // 내 비석 제외한 다른 플레이어 비석
        foreach (var rec in records)
        {
            CreateTombstone(rec.player_name, rec.clear_time_seconds, rec.regression_count, false);
        }

        // 마지막에 내 비석 (테스트 플레이 시 생략)
        if (!isTestPlay)
        {
            CreateTombstone(myName, myTime, myRegressionCount, true);
        }

        // RightPadding
        var rightPad = CreatePadding("RightPadding", padWidth);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        yield return null; // 1프레임 대기

        scrollRect.horizontalNormalizedPosition = 0f;

        // 자동 스크롤 시작
        int totalTombstones = isTestPlay ? records.Count : records.Count + 1;
        scrollCoroutine = StartCoroutine(CoAutoScroll(totalTombstones));
    }

    private IEnumerator CoAutoScroll(int tombstoneCount)
    {
        float totalDuration = Mathf.Max(tombstoneCount * 1.5f, 3f);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(elapsed / totalDuration);
            yield return null;
        }

        scrollRect.horizontalNormalizedPosition = 1f;
        hasReachedEnd = true;
        scrollCoroutine = null;

        // 재시작 힌트 깜빡임
        blinkCoroutine = StartCoroutine(CoBlinkHint());
    }

    private IEnumerator CoSkipScroll()
    {
        float startPos = scrollRect.horizontalNormalizedPosition;
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(startPos, 1f, elapsed / duration);
            yield return null;
        }

        scrollRect.horizontalNormalizedPosition = 1f;
        hasReachedEnd = true;
        scrollCoroutine = null;

        blinkCoroutine = StartCoroutine(CoBlinkHint());
    }

    private IEnumerator CoBlinkHint()
    {
        // v9(260703): 스크롤 완료 → Play Again / Go Home 버튼 표시
        restartHintWrapper.SetActive(true);
        yield break;
    }

    // ------------------------------------------------------------------
    // 비석 생성
    // ------------------------------------------------------------------

    private void CreateTombstone(string playerName, float clearTimeSeconds, int regressionCount, bool isMe)
    {
        // 비석 루트
        var tombGo = new GameObject("Tombstone", typeof(RectTransform));
        tombGo.transform.SetParent(content, false);

        var tombRt = tombGo.GetComponent<RectTransform>();
        tombRt.sizeDelta = new Vector2(160f, 220f);

        var tombLe = tombGo.AddComponent<LayoutElement>();
        tombLe.preferredWidth = 160f;
        tombLe.preferredHeight = 220f;

        // StoneBody (높이의 75% = 165)
        var bodyGo = new GameObject("StoneBody", typeof(RectTransform));
        bodyGo.transform.SetParent(tombGo.transform, false);

        var bodyRt = bodyGo.GetComponent<RectTransform>();
        // 앵커: 상단 stretch, 높이 75%
        bodyRt.anchorMin = new Vector2(0.1f, 0.25f);
        bodyRt.anchorMax = new Vector2(0.9f, 1f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        var bodyImg = bodyGo.AddComponent<Image>();
        bodyImg.color = isMe
            ? new Color(1f, 0.84f, 0f, 1f)   // 금색 (내 비석)
            : new Color(0.45f, 0.45f, 0.5f, 1f); // 회색

        // StoneBase (높이의 25% = 55, 하단)
        var baseGo = new GameObject("StoneBase", typeof(RectTransform));
        baseGo.transform.SetParent(tombGo.transform, false);

        var baseRt = baseGo.GetComponent<RectTransform>();
        baseRt.anchorMin = new Vector2(0f, 0f);
        baseRt.anchorMax = new Vector2(1f, 0.25f);
        baseRt.offsetMin = Vector2.zero;
        baseRt.offsetMax = Vector2.zero;

        var baseImg = baseGo.AddComponent<Image>();
        baseImg.color = isMe
            ? new Color(0.7f, 0.58f, 0f, 1f)   // 어두운 금색
            : new Color(0.3f, 0.3f, 0.35f, 1f); // 어두운 회색

        // NameText (비석 몸체 상부 40%)
        var nameGo = new GameObject("NameText", typeof(RectTransform));
        nameGo.transform.SetParent(bodyGo.transform, false);

        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.05f, 0.55f);
        nameRt.anchorMax = new Vector2(0.95f, 0.95f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;

        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = playerName;
        nameTmp.fontSize = 15f;
        nameTmp.color = isMe ? Color.black : Color.white;
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.textWrappingMode = TextWrappingModes.Normal;
        nameTmp.overflowMode = TextOverflowModes.Truncate;
        if (koreanFont != null) nameTmp.font = koreanFont;

        // TimeText (비석 몸체 하부 30%)
        var timeGo = new GameObject("TimeText", typeof(RectTransform));
        timeGo.transform.SetParent(bodyGo.transform, false);

        var timeRt = timeGo.GetComponent<RectTransform>();
        timeRt.anchorMin = new Vector2(0.05f, 0.1f);
        timeRt.anchorMax = new Vector2(0.95f, 0.5f);
        timeRt.offsetMin = Vector2.zero;
        timeRt.offsetMax = Vector2.zero;

        var timeTmp = timeGo.AddComponent<TextMeshProUGUI>();
        timeTmp.text = $"회귀 {regressionCount}번\n{FormatTime(clearTimeSeconds)}";
        timeTmp.fontSize = 13f;
        timeTmp.color = isMe ? new Color(0.2f, 0.1f, 0f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
        timeTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) timeTmp.font = koreanFont;
    }

    private GameObject CreatePadding(string name, float width)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(content, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 220f;
        return go;
    }

    // ------------------------------------------------------------------
    // UI 구조 빌드 (런타임 생성)
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        koreanFont = KoreanFont.GetTMP();

        // Canvas — Screen Space Overlay, sortingOrder=200
        var canvasGo = new GameObject("GraveyardCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Background — 전체화면 어두운 색
        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.08f, 0.05f, 1f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // ScrollRect — 수평, 전체화면
        var scrollGo = new GameObject("ScrollRect", typeof(RectTransform));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 0f;

        // Viewport
        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();

        scrollRect.viewport = viewportRt;

        // Content
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;

        var hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 40f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.content = content;

        // StatusText — 중앙
        var statusGo = new GameObject("StatusText", typeof(RectTransform));
        statusGo.transform.SetParent(canvasGo.transform, false);
        var statusRt = statusGo.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.3f, 0.4f);
        statusRt.anchorMax = new Vector2(0.7f, 0.6f);
        statusRt.offsetMin = Vector2.zero;
        statusRt.offsetMax = Vector2.zero;
        statusText = statusGo.AddComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.fontSize = 20f;
        statusText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        statusText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) statusText.font = koreanFont;

        // v9(260703): Play Again / Go Home 버튼 (하단 중앙)
        restartHintWrapper = new GameObject("EndButtons", typeof(RectTransform));
        restartHintWrapper.transform.SetParent(canvasGo.transform, false);
        var btnsRt = restartHintWrapper.GetComponent<RectTransform>();
        btnsRt.anchorMin = new Vector2(0.5f, 0.03f);
        btnsRt.anchorMax = new Vector2(0.5f, 0.13f);
        btnsRt.pivot = new Vector2(0.5f, 0f);
        btnsRt.sizeDelta = new Vector2(460f, 72f);
        btnsRt.anchoredPosition = Vector2.zero;

        var hlgBtns = restartHintWrapper.AddComponent<HorizontalLayoutGroup>();
        hlgBtns.spacing = 40f;
        hlgBtns.childAlignment = TextAnchor.MiddleCenter;
        hlgBtns.childForceExpandWidth = false;
        hlgBtns.childForceExpandHeight = false;

        CreateEndButton("Play Again", () => GameManager.Instance?.RestartGame(false));
        CreateEndButton("Go Home", () => GameManager.Instance?.RestartGame(true));

        restartHintWrapper.SetActive(false);

        // 초기 비활성화
        canvasGo.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------------

    private void CreateEndButton(string label, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject($"Btn_{label}", typeof(RectTransform));
        btnGo.transform.SetParent(restartHintWrapper.transform, false);
        var le = btnGo.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;
        le.preferredHeight = 64f;

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.25f, 0.95f);
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var hover = btnGo.AddComponent<HandCursorHoverTrigger>();
        hover.HoverPose = HandPose.PointIndex;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(btnGo.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;
    }

    private static string FormatTime(float seconds)
    {
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{mins:00}:{secs:00}";
    }
}
