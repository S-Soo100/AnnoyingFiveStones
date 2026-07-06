using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주마등(라이프 파노라마) 싱글톤. v9(260703).
/// ALL CLEAR 엔딩 멘트 후, 인생 스테이지 배경들을 오른쪽→왼쪽으로 흘려보낸다.
/// Screen Space Overlay Canvas (sortingOrder=210). 완료 후 onComplete 콜백.
/// </summary>
public class LifePanoramaUI : MonoBehaviour
{
    public static LifePanoramaUI Instance { get; private set; }

    // 인생 순서대로 흘려보낼 배경 (Resources 경로). 학창 → 청년 → 중년.
    // v10: 나이대별 배경 10장 (5년 단위, figma-export → Resources/StageBackgrounds/Life/)
    private static readonly string[] bgPaths = new string[]
    {
        "StageBackgrounds/Life/age10",
        "StageBackgrounds/Life/age15",
        "StageBackgrounds/Life/age20",
        "StageBackgrounds/Life/age25",
        "StageBackgrounds/Life/age30",
        "StageBackgrounds/Life/age35",
        "StageBackgrounds/Life/age40",
        "StageBackgrounds/Life/age45",
        "StageBackgrounds/Life/age50",
        "StageBackgrounds/Life/age55",
    };

    private const float PerImageSeconds = 1.4f; // 이미지당 흐름 시간 (10장 → 총 ~12.6초)
    private const float FadeSeconds     = 0.5f;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform content;
    private float screenW;
    private int imageCount;
    private Coroutine playCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>주마등 재생. 완료 시 onComplete 호출.</summary>
    public void Show(Action onComplete)
    {
        if (imageCount == 0) { onComplete?.Invoke(); return; } // 배경 로드 실패 시 스킵
        canvas.gameObject.SetActive(true);
        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = StartCoroutine(CoPlay(onComplete));
    }

    private IEnumerator CoPlay(Action onComplete)
    {
        // 시작 위치: content X=0 → 첫 이미지 화면. 왼쪽으로 이동 → 오른→왼 흐름.
        content.anchoredPosition = Vector2.zero;
        group.alpha = 0f;

        // 페이드인
        float t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / FadeSeconds);
            yield return null;
        }
        group.alpha = 1f;

        // 스크롤: X 0 → -(n-1)*w
        float duration = Mathf.Max((imageCount - 1) * PerImageSeconds, PerImageSeconds);
        float endX = -(imageCount - 1) * screenW;
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            content.anchoredPosition = new Vector2(Mathf.Lerp(0f, endX, p), 0f);
            yield return null;
        }
        content.anchoredPosition = new Vector2(endX, 0f);

        // 페이드아웃
        t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / FadeSeconds);
            yield return null;
        }
        group.alpha = 0f;

        canvas.gameObject.SetActive(false);
        playCoroutine = null;
        onComplete?.Invoke();
    }

    private void BuildUI()
    {
        screenW = Screen.width;

        var canvasGo = new GameObject("LifePanoramaCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210; // GraveyardUI(200)보다 위, 엔딩 오버레이 흐름 전용
        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // 검정 배경 (letterbox)
        var bgGo = new GameObject("Bg", typeof(RectTransform));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = Color.black;
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        // Content — 세로 stretch, 가로 왼쪽 기준. 이미지들을 가로로 이어붙임.
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(canvasGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;

        int idx = 0;
        foreach (var path in bgPaths)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[LifePanoramaUI] 배경 로드 실패: {path}");
                continue;
            }
            var imgGo = new GameObject($"Bg_{idx}", typeof(RectTransform));
            imgGo.transform.SetParent(contentGo.transform, false);
            var raw = imgGo.AddComponent<RawImage>();
            raw.texture = tex;
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(screenW, 0f); // 폭=화면, 높이=stretch
            rt.anchoredPosition = new Vector2(idx * screenW, 0f);
            idx++;
        }
        imageCount = idx;

        canvasGo.SetActive(false);
    }
}
