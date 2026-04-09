using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }
    public bool IsShowing { get; private set; }

    private Canvas canvas;
    private CanvasGroup rootGroup;
    private TextMeshProUGUI slideText;
    private TextMeshProUGUI hintText;

    private int currentSlide;
    private Action pendingCallback;

    private InputAction tapAction;

    private static readonly string[] slides = new string[]
    {
        "꾹 눌러서 게이지를 조절하세요.\n놓으면 돌이 퍼집니다.",
        "돌 위로 커서를 이동하면\n자동으로 줍습니다.",
        "하늘에서 떨어지는 돌을\n손바닥으로 받으세요!",
    };

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
        var canvasGo = new GameObject("TutorialCanvas");
        canvasGo.transform.SetParent(transform);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 120;

        canvasGo.AddComponent<GraphicRaycaster>();

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(0f, -1.5f, -1f);
        rt.sizeDelta = new Vector2(2500f, 1400f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        // --- CanvasGroup ---
        rootGroup = canvasGo.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;

        // --- 반투명 검은 배경 ---
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.7f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // --- 슬라이드 텍스트 ---
        var slideGo = new GameObject("SlideText");
        slideGo.transform.SetParent(canvasGo.transform, false);
        slideText = slideGo.AddComponent<TextMeshProUGUI>();
        slideText.fontSize = 34;
        slideText.color = Color.white;
        slideText.alignment = TextAlignmentOptions.Center;
        slideText.textWrappingMode = TextWrappingModes.Normal;

        var koreanFont = KoreanFont.GetTMP();
        if (koreanFont != null)
            slideText.font = koreanFont;

        var slideRt = slideGo.GetComponent<RectTransform>();
        slideRt.anchorMin = new Vector2(0.15f, 0.35f);
        slideRt.anchorMax = new Vector2(0.85f, 0.65f);
        slideRt.offsetMin = Vector2.zero;
        slideRt.offsetMax = Vector2.zero;

        // --- "탭하여 계속" 힌트 ---
        var hintGo = new GameObject("HintText");
        hintGo.transform.SetParent(canvasGo.transform, false);
        hintText = hintGo.AddComponent<TextMeshProUGUI>();
        hintText.fontSize = 18;
        hintText.color = new Color(1f, 1f, 1f, 0.4f);
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.text = "탭하여 계속";

        if (koreanFont != null)
            hintText.font = koreanFont;

        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.3f, 0.18f);
        hintRt.anchorMax = new Vector2(0.7f, 0.24f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;

        IsShowing = false;
    }

    private void OnEnable()
    {
        tapAction = new InputAction("TutorialTap", InputActionType.Button);
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

    public void Show(Action onComplete = null)
    {
        pendingCallback = onComplete;
        currentSlide = 0;
        slideText.text = slides[currentSlide];
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
    }

    private void OnTap(InputAction.CallbackContext ctx)
    {
        if (!IsShowing) return;

        currentSlide++;
        if (currentSlide >= slides.Length)
        {
            StartCoroutine(DoFadeOut());
        }
        else
        {
            slideText.text = slides[currentSlide];
        }
    }

    private IEnumerator DoFadeOut()
    {
        IsShowing = false;
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            rootGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.3f);
            yield return null;
        }
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        pendingCallback?.Invoke();
        pendingCallback = null;
    }
}
