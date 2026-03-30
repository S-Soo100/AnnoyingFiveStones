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
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 110;

        canvasGo.AddComponent<GraphicRaycaster>();

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(0f, -1.5f, -1f);
        rt.sizeDelta = new Vector2(2500f, 1400f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

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

    public void Show(string message, Action onComplete = null)
    {
        pendingCallback = onComplete;
        currentMessage = message;
        skipRequested = false;
        hintText.alpha = 0f;

        IsShowing = true;
        rootGroup.blocksRaycasts = true;

        StopAllCoroutines();
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

    private IEnumerator DoTyping()
    {
        state = MentState.Typing;
        mentText.text = currentMessage;
        mentText.ForceMeshUpdate();
        int totalChars = mentText.textInfo.characterCount;
        mentText.maxVisibleCharacters = 0;

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
