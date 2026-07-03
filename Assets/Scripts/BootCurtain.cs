using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 → 게임 전환 시 약 0.5초 동안 노출되는 미초기화 화면(회색 배경/placeholder/돌 일렬)을
/// 가리는 풀스크린 검은 막. BeforeSceneLoad 부트스트랩으로 가장 이른 시점에 준비된다.
/// sortingOrder=200 (StoryMentUI=220 아래, TitleScreenUI=250 아래).
/// v10: SmoothStep 곡선 + 페이드인/페이드아웃 분리.
/// </summary>
public class BootCurtain : MonoBehaviour
{
    public static BootCurtain Instance { get; private set; }

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    // ── 부트스트랩 ───────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("BootCurtain");
        DontDestroyOnLoad(go);
        go.AddComponent<BootCurtain>();
    }

    // ── Unity 생명주기 ───────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildCanvas();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Canvas 자동 생성 ─────────────────────────────────────────
    private void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var imgGo = new GameObject("BG");
        imgGo.transform.SetParent(transform, false);

        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        gameObject.SetActive(false);
    }

    // ── 공개 API ─────────────────────────────────────────────────

    /// <summary>
    /// 검은 막 표시. duration이 0이면 즉시, &gt;0이면 SmoothStep 페이드인.
    /// placeholder 즉시 가림이 필요하면 작은 duration(0.1~0.15s) 권장.
    /// </summary>
    public void Raise(float duration = 0.15f)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        gameObject.SetActive(true);
        canvasGroup.blocksRaycasts = true;

        if (duration <= 0f)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        canvasGroup.alpha = 0f;
        fadeCoroutine = StartCoroutine(DoFadeIn(duration));
    }

    /// <summary>호환성: 즉시 검은 막 표시 (페이드 없음).</summary>
    public void RaiseInstant() => Raise(0f);

    /// <summary>SmoothStep 곡선으로 페이드 아웃 후 비활성화. duration &lt;= 0이면 즉시 처리.</summary>
    public void FadeOut(float duration)
    {
        // 비활성 상태(Raise 미호출)면 페이드 의미 없음 — 스테이지 전환 등 무관 흐름에서 호출돼도 무시
        if (!gameObject.activeSelf) return;
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(DoFadeOut(duration));
    }

    // ── 내부 코루틴 ──────────────────────────────────────────────
    private IEnumerator DoFadeIn(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // UI 페이드는 timeScale 영향 받지 않도록
            canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        fadeCoroutine = null;
    }

    private IEnumerator DoFadeOut(float duration)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            fadeCoroutine = null;
            yield break;
        }

        float startAlpha = canvasGroup.alpha; // 현재 알파에서 시작 (페이드인 중간일 수 있음)
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // UI 페이드는 timeScale 영향 받지 않도록
            canvasGroup.alpha = startAlpha * (1f - Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
        fadeCoroutine = null;
    }
}
