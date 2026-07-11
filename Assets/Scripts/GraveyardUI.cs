using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 묘지 랭킹보드 싱글톤 (Figma Image#13).
/// ALL CLEAR 후 표시. 전체 기록을 흰 배경 세로 3열 그리드 카드로 시각화, 세로 스크롤.
/// 우하단 검정 버튼(Play Again/Go Home). Screen Space Overlay Canvas (sortingOrder=200).
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

    // v10 다국어: 하단 버튼(Play Again/Go Home) 라벨 → 키. Show() 때 현재 언어로 재설정.
    private readonly List<(TextMeshProUGUI tmp, string key)> endButtonLabels = new();

    private Coroutine scrollCoroutine;
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
        RefreshEndButtons(); // v10 다국어: 현재 언어로 하단 버튼 라벨 갱신
        statusText.text = LocalizationManager.L("grave.loading");

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

        // 그리드 대기(0.6초) 중 탭 시 버튼 즉시 표시. 완료 후에는 화면 버튼(Play Again/Go Home)으로 처리.
        if (!hasReachedEnd)
        {
            hasReachedEnd = true;
            restartHintWrapper.SetActive(true);
        }
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

        // 로드 실패(오프라인/타임아웃) 시 튕김 방지 — 빈 리스트로 대체해 빈 흰 화면 + 버튼만 표시
        if (records == null)
        {
            records = new List<RecordEntry>();
        }

        statusText.text = "";

        // Content 기존 자식 정리
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        yield return null; // Destroy 반영 대기

        // 다른 플레이어 비석 (그리드 배치는 GridLayoutGroup이 처리)
        foreach (var rec in records)
        {
            CreateTombstone(rec.player_name, rec.clear_time_seconds, rec.regression_count, false);
        }

        // 마지막에 내 비석 (테스트 플레이 시 생략)
        if (!isTestPlay)
        {
            CreateTombstone(myName, myTime, myRegressionCount, true);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.verticalNormalizedPosition = 1f; // 맨 위

        // 그리드 완성 후 잠시 뒤 버튼 노출 (가로 자동스크롤 대체)
        yield return new WaitForSecondsRealtime(0.6f);
        hasReachedEnd = true;
        restartHintWrapper.SetActive(true);
        scrollCoroutine = null;
    }

    // ------------------------------------------------------------------
    // 비석 생성
    // ------------------------------------------------------------------

    private void CreateTombstone(string playerName, float clearTimeSeconds, int regressionCount, bool isMe)
    {
        // 비석 루트 — 크기는 GridLayoutGroup의 cellSize(220×200)가 제어
        var tombGo = new GameObject("Tombstone", typeof(RectTransform));
        tombGo.transform.SetParent(content, false);

        // Card — 단일 회색 사각 카드 (stretch full)
        var cardGo = new GameObject("Card", typeof(RectTransform));
        cardGo.transform.SetParent(tombGo.transform, false);

        var cardRt = cardGo.GetComponent<RectTransform>();
        cardRt.anchorMin = Vector2.zero;
        cardRt.anchorMax = Vector2.one;
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;

        var cardImg = cardGo.AddComponent<Image>();
        cardImg.color = isMe
            ? new Color(0.98f, 0.90f, 0.55f, 1f)  // 옅은 금색 (내 카드)
            : new Color(0.82f, 0.82f, 0.82f, 1f); // 회색

        // NameText (카드 상부)
        var nameGo = new GameObject("NameText", typeof(RectTransform));
        nameGo.transform.SetParent(cardGo.transform, false);

        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.08f, 0.45f);
        nameRt.anchorMax = new Vector2(0.92f, 0.90f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;

        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = playerName;
        nameTmp.fontSize = 20f;
        nameTmp.color = isMe ? Color.black : new Color(0.15f, 0.15f, 0.15f, 1f);
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.textWrappingMode = TextWrappingModes.Normal;
        nameTmp.overflowMode = TextOverflowModes.Truncate;
        if (koreanFont != null) nameTmp.font = koreanFont;

        // TimeText (카드 하부)
        var timeGo = new GameObject("TimeText", typeof(RectTransform));
        timeGo.transform.SetParent(cardGo.transform, false);

        var timeRt = timeGo.GetComponent<RectTransform>();
        timeRt.anchorMin = new Vector2(0.08f, 0.10f);
        timeRt.anchorMax = new Vector2(0.92f, 0.45f);
        timeRt.offsetMin = Vector2.zero;
        timeRt.offsetMax = Vector2.zero;

        var timeTmp = timeGo.AddComponent<TextMeshProUGUI>();
        timeTmp.text = $"{LocalizationManager.LF("grave.regression", regressionCount)}\n{FormatTime(clearTimeSeconds)}";
        timeTmp.fontSize = 15f;
        timeTmp.color = isMe ? new Color(0.25f, 0.15f, 0f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);
        timeTmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) timeTmp.font = koreanFont;
    }

    // ------------------------------------------------------------------
    // 디버그 미리보기 (에디터 확인용 — Supabase 불필요)
    // ------------------------------------------------------------------

    public void ShowPreview()  // 더미 그리드 즉시 표시 (Supabase 불필요, 에디터 확인용)
    {
        canvas.gameObject.SetActive(true);
        isShowing = true; hasReachedEnd = true;
        restartHintWrapper.SetActive(false);
        RefreshEndButtons();
        statusText.text = "";
        if (scrollCoroutine != null) { StopCoroutine(scrollCoroutine); scrollCoroutine = null; }
        scrollCoroutine = StartCoroutine(CoShowPreview());
    }

    private IEnumerator CoShowPreview()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        yield return null; // Destroy 반영
        string[] names = {"홍길동","김철수","이영희","박민수","최지우","정해인","강동원","한소희"};
        for (int i = 0; i < names.Length; i++) CreateTombstone(names[i], 180f + i*20f, i % 3, false);
        CreateTombstone("나", 200f, 1, true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.verticalNormalizedPosition = 1f;
        restartHintWrapper.SetActive(true);
        scrollCoroutine = null;
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

        // Background — 전체화면 흰색 (Figma Image#13)
        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = Color.white;
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // ScrollRect — 수직 3열 그리드, 전체화면 (Figma Image#13)
        var scrollGo = new GameObject("ScrollRect", typeof(RectTransform));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

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

        // Content — 상단 stretch, 세로 3열 그리드
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(220f, 200f);
        grid.spacing = new Vector2(36f, 36f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(0, 0, 90, 120);

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
        statusText.color = new Color(0.3f, 0.3f, 0.3f, 1f); // 흰 배경 위 가독
        statusText.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) statusText.font = koreanFont;

        // v9(260703): Play Again / Go Home 버튼 (하단 중앙)
        restartHintWrapper = new GameObject("EndButtons", typeof(RectTransform));
        restartHintWrapper.transform.SetParent(canvasGo.transform, false);
        var btnsRt = restartHintWrapper.GetComponent<RectTransform>();
        btnsRt.anchorMin = new Vector2(1f, 0f); // 우하단 (Figma Image#13)
        btnsRt.anchorMax = new Vector2(1f, 0f);
        btnsRt.pivot = new Vector2(1f, 0f);
        btnsRt.sizeDelta = new Vector2(440f, 72f);
        btnsRt.anchoredPosition = new Vector2(-40f, 40f);

        var hlgBtns = restartHintWrapper.AddComponent<HorizontalLayoutGroup>();
        hlgBtns.spacing = 40f;
        hlgBtns.childAlignment = TextAnchor.MiddleCenter;
        hlgBtns.childForceExpandWidth = false;
        hlgBtns.childForceExpandHeight = false;

        CreateEndButton("grave.play_again", () => GameManager.Instance?.RestartGame(false));
        CreateEndButton("grave.go_home", () => GameManager.Instance?.RestartGame(true));

        restartHintWrapper.SetActive(false);

        // 초기 비활성화
        canvasGo.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------------

    private void CreateEndButton(string locKey, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject($"Btn_{locKey}", typeof(RectTransform));
        btnGo.transform.SetParent(restartHintWrapper.transform, false);
        var le = btnGo.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;
        le.preferredHeight = 64f;

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 1f); // 검정 (Figma Image#13)
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
        tmp.text = LocalizationManager.L(locKey);
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (koreanFont != null) tmp.font = koreanFont;
        endButtonLabels.Add((tmp, locKey));
    }

    /// <summary>하단 버튼(Play Again/Go Home) 라벨을 현재 언어로 재설정.</summary>
    private void RefreshEndButtons()
    {
        foreach (var (tmp, key) in endButtonLabels)
            if (tmp != null) tmp.text = LocalizationManager.L(key);
    }

    private static string FormatTime(float seconds)
    {
        // v10: 기획 형식 00:00:00 (HH:MM:SS)
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        int s = (int)(seconds % 60);
        return $"{h:00}:{m:00}:{s:00}";
    }
}
