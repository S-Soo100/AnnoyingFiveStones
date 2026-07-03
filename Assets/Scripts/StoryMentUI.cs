using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class StoryMentUI : MonoBehaviour
{
    public static StoryMentUI Instance { get; private set; }
    public bool IsShowing { get; private set; }

    private enum MentState { Idle, Typing, WaitingForTap, FadingOut }
    private MentState state = MentState.Idle;

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private TextMeshProUGUI mentText;
    private TextMeshProUGUI hintText;
    private TextMeshProUGUI titleSplashText; // v10: 게임 첫 진입 시 "Catch Five Stones" 인트로

    private string currentMessage;
    private Action pendingCallback;
    private bool skipRequested;

    private InputAction tapAction;

    private const float typingInterval = 0.04f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Init();
    }

    private void Init()
    {
        // --- World Space Canvas ---
        var canvasGo = new GameObject("StoryMentCanvas");
        canvasGo.transform.SetParent(transform);

        canvas = canvasGo.AddComponent<Canvas>();
        // v10: BootCurtain(Overlay,200) 위로 보이게 Overlay+sortingOrder=220
        // (Unity 렌더 순서: Overlay > WorldSpace, sortingOrder는 같은 렌더모드끼리만 비교됨)
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;

        canvasGo.AddComponent<GraphicRaycaster>();

        var rt = canvasGo.GetComponent<RectTransform>();
        // v10: Overlay 모드 — 풀스크린 anchor (자식들은 이미 anchor 비율 기반이라 비례 확장)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // --- CanvasGroup (rootGroup) ---
        rootGroup = canvasGo.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;

        // --- 반투명 검은 배경 ---
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.6f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // --- 멘트 텍스트 ---
        var mentGo = new GameObject("MentText");
        mentGo.transform.SetParent(canvasGo.transform, false);
        mentText = mentGo.AddComponent<TextMeshProUGUI>();
        mentText.fontSize = 36;
        mentText.color = Color.white;
        mentText.alignment = TextAlignmentOptions.Center;
        mentText.textWrappingMode = TextWrappingModes.Normal;

        var koreanFont = KoreanFont.GetTMP();
        if (koreanFont != null)
        {
            mentText.font = koreanFont;
        }

        var mentRt = mentGo.GetComponent<RectTransform>();
        mentRt.anchorMin = new Vector2(0.1f, 0.35f);
        mentRt.anchorMax = new Vector2(0.9f, 0.65f);
        mentRt.offsetMin = Vector2.zero;
        mentRt.offsetMax = Vector2.zero;

        // --- "탭하여 계속" 안내 ---
        var hintGo = new GameObject("HintText");
        hintGo.transform.SetParent(canvasGo.transform, false);
        hintText = hintGo.AddComponent<TextMeshProUGUI>();
        hintText.fontSize = 20;
        hintText.color = new Color(1f, 1f, 1f, 0.4f);
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.text = "탭하여 계속";

        if (koreanFont != null)
        {
            hintText.font = koreanFont;
        }

        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.3f, 0.15f);
        hintRt.anchorMax = new Vector2(0.7f, 0.22f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;

        // 초기에는 힌트 숨김
        hintText.alpha = 0f;

        // --- 게임 인트로 splash ("Catch Five Stones") ---
        var splashGo = new GameObject("TitleSplash");
        splashGo.transform.SetParent(canvasGo.transform, false);
        titleSplashText = splashGo.AddComponent<TextMeshProUGUI>();
        titleSplashText.fontSize = 72;
        titleSplashText.color = Color.white;
        titleSplashText.alignment = TextAlignmentOptions.Center;
        titleSplashText.text = "Catch Five Stones";
        titleSplashText.alpha = 0f;
        if (koreanFont != null)
        {
            titleSplashText.font = koreanFont;
        }

        var splashRt = splashGo.GetComponent<RectTransform>();
        splashRt.anchorMin = new Vector2(0.1f, 0.4f);
        splashRt.anchorMax = new Vector2(0.9f, 0.6f);
        splashRt.offsetMin = Vector2.zero;
        splashRt.offsetMax = Vector2.zero;

        IsShowing = false;
    }

    private void OnEnable()
    {
        tapAction = new InputAction("Tap", InputActionType.Button);
        tapAction.AddBinding("<Mouse>/leftButton");
        tapAction.AddBinding("<Touchscreen>/primaryTouch/press");
        tapAction.performed += OnTap;
        tapAction.Enable();
    }

    private void OnDisable()
    {
        if (tapAction != null)
        {
            tapAction.performed -= OnTap;
            tapAction.Disable();
        }
    }

    public void Show(string message, Action onComplete = null, bool showTitleSplash = false)
    {
        pendingCallback = onComplete;
        currentMessage = message;
        skipRequested = false;
        hintText.alpha = 0f;

        // v10: 페이드인 동안 풀텍스트 잔상 방지 — 즉시 비움
        mentText.maxVisibleCharacters = 0;
        mentText.text = "";
        titleSplashText.alpha = 0f;

        IsShowing = true;
        rootGroup.blocksRaycasts = true;

        StopAllCoroutines();
        if (showTitleSplash)
            StartCoroutine(DoSplashThenTyping());
        else
            StartCoroutine(DoFadeIn());
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(DoFadeOut());
    }

    private IEnumerator DoFadeIn()
    {
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            rootGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.3f);
            yield return null;
        }
        rootGroup.alpha = 1f;

        StartCoroutine(DoTyping());
    }

    /// <summary>
    /// v10: 게임 첫 진입 시 인트로 — "Catch Five Stones" splash → 빈 검은화면 → 타이핑.
    /// 순서: rootGroup+splash 페이드인 0.3s → splash 1.2s 노출 → splash 페이드아웃 0.4s → 빈 화면 0.3s → DoTyping
    /// </summary>
    private IEnumerator DoSplashThenTyping()
    {
        // 1) rootGroup + splash 동시 페이드인 (SmoothStep)
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.3f);
            rootGroup.alpha = t;
            titleSplashText.alpha = t;
            yield return null;
        }
        rootGroup.alpha = 1f;
        titleSplashText.alpha = 1f;

        // 2) splash 노출 유지
        yield return new WaitForSeconds(1.2f);

        // 3) splash 페이드아웃 (SmoothStep)
        elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.deltaTime;
            titleSplashText.alpha = Mathf.SmoothStep(1f, 0f, elapsed / 0.4f);
            yield return null;
        }
        titleSplashText.alpha = 0f;

        // 4) 빈 검은화면 (호흡)
        yield return new WaitForSeconds(0.3f);

        // 5) 기존 타이핑 시작
        StartCoroutine(DoTyping());
    }

    private IEnumerator DoTyping()
    {
        state = MentState.Typing;
        // v10: 잔상 방지 — 0 먼저, 그 다음 text 세팅
        mentText.maxVisibleCharacters = 0;
        mentText.text = currentMessage;
        mentText.ForceMeshUpdate();
        int totalChars = mentText.textInfo.characterCount;

        for (int i = 0; i < totalChars; i++)
        {
            if (skipRequested)
                break;
            mentText.maxVisibleCharacters = i + 1;

            if (i < totalChars)
            {
                char c = mentText.textInfo.characterInfo[i].character;
                float delay = (c == '.' || c == '!' || c == '?') ? typingInterval * 4f
                            : (c == ',') ? typingInterval * 2f
                            : typingInterval;
                yield return new WaitForSeconds(delay);
            }
        }

        mentText.maxVisibleCharacters = totalChars;
        state = MentState.WaitingForTap;
        StartCoroutine(BlinkHint());
    }

    private IEnumerator BlinkHint()
    {
        while (state == MentState.WaitingForTap)
        {
            hintText.alpha = Mathf.PingPong(Time.time * 2f, 1f) * 0.6f + 0.2f;
            yield return null;
        }
    }

    private void OnTap(InputAction.CallbackContext ctx)
    {
        if (!IsShowing) return;

        switch (state)
        {
            case MentState.Typing:
                skipRequested = true;
                break;
            case MentState.WaitingForTap:
                StartCoroutine(DoFadeOut());
                break;
        }
    }

    private IEnumerator DoFadeOut()
    {
        state = MentState.FadingOut;
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            rootGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.3f);
            yield return null;
        }
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        IsShowing = false;
        state = MentState.Idle;
        pendingCallback?.Invoke();
        pendingCallback = null;
    }
}
