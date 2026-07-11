using System;
using System.Collections;
using System.Collections.Generic;
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

    // 스크롤 시작/끝 X (첫/마지막 카드가 화면 중앙에 오도록 BuildUI에서 계산).
    private float scrollStartX, scrollEndX;

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
        // 시작 위치: 첫 카드가 화면 중앙(scrollStartX). 왼쪽으로 이동 → 오른→왼 흐름.
        content.anchoredPosition = new Vector2(scrollStartX, 0f);
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

        // 스크롤: 첫 카드 중앙(scrollStartX) → 마지막 카드 중앙(scrollEndX)
        float duration = Mathf.Max((imageCount - 1) * PerImageSeconds, PerImageSeconds);
        float endX = scrollEndX;
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            content.anchoredPosition = new Vector2(Mathf.Lerp(scrollStartX, endX, p), 0f);
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
        float screenH = Screen.height;

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

        // 카드형: 검은 배경 위에 가로 직사각형 카드들이 오른쪽→왼쪽으로 흐른다 (레터박스).
        float cursorX = 0f;
        var cardCenters = new List<float>();
        int idx = 0;
        for (int i = 0; i < bgPaths.Length; i++)
        {
            var tex = Resources.Load<Texture2D>(bgPaths[i]);
            if (tex == null)
            {
                Debug.LogWarning($"[LifePanoramaUI] 배경 로드 실패: {bgPaths[i]}");
                continue; // 로드 실패 → 카드·커서 모두 건너뜀. 인덱스 정합 유지.
            }
            var imgGo = new GameObject($"Bg_{idx}", typeof(RectTransform));
            imgGo.transform.SetParent(contentGo.transform, false);
            var raw = imgGo.AddComponent<RawImage>();
            raw.texture = tex;
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);

            float cardH = screenH * 0.45f;
            float cardW = cardH * (tex.width / (float)tex.height); // 실제 비율 → 왜곡 방지
            float gap = cardW * 0.18f;
            rt.sizeDelta = new Vector2(cardW, cardH);
            rt.anchoredPosition = new Vector2(cursorX, 0f);

            cardCenters.Add(cursorX + cardW * 0.5f);
            cursorX += cardW + gap;

            idx++;
        }
        imageCount = idx;

        // 스크롤 시작/끝: 첫/마지막 카드가 화면 중앙에 오도록. 1장이면 start==end (정지 표시).
        if (imageCount > 0)
        {
            scrollStartX = screenW * 0.5f - cardCenters[0];
            scrollEndX   = screenW * 0.5f - cardCenters[cardCenters.Count - 1];
        }

        canvasGo.SetActive(false);
    }
}
