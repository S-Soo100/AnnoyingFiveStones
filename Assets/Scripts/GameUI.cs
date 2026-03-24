using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas+TMP 기반 통합 게임 UI.
/// 가이드 텍스트, 진행도 도트, 전환 연출 (인트로/클리어/실패/올클리어).
/// GameManager에서 직접 호출 (풀링 아님).
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;

    [Header("Guide Text")]
    private TextMeshProUGUI guideText;
    private Image guideBackground;
    private CanvasGroup guideGroup;

    [Header("Progress Dots")]
    private Image[] progressDots = new Image[5];
    private TextMeshProUGUI starLabel;

    [Header("Overlay")]
    private Image overlayBg;
    private TextMeshProUGUI overlayMainText;
    private TextMeshProUGUI overlaySubText;
    private CanvasGroup overlayGroup;

    private Coroutine guideCoroutine;
    private Coroutine overlayCoroutine;
    private TMP_FontAsset koreanTmpFont;
    private static Sprite circleSprite; // 진행 도트용 원형 스프라이트 (공유)

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

    private void OnDisable()
    {
        StopOverlay();
        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
            guideCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 런타임 생성 텍스처 해제
        if (circleSprite != null && circleSprite.texture != null)
        {
            Destroy(circleSprite.texture);
            circleSprite = null;
        }
    }

    private void Init()
    {
        koreanTmpFont = KoreanFont.GetTMP();

        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
                canvas = CreateCanvas();
        }

        CreateProgressDots();
        CreateGuideText();
        CreateOverlay();
    }

    // ==========================================================
    // Canvas 구조 코드 생성
    // ==========================================================

    private Canvas CreateCanvas()
    {
        var canvasGo = new GameObject("GameUICanvas");
        canvasGo.transform.SetParent(transform);

        var c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 100;

        // World Space Canvas: 카메라(z=-10)와 게임 오브젝트(z=0) 사이에 배치
        // 카메라: position=(0,-1.5,-10), ortho size=7, viewport width≈0.3*aspect
        // 가시 영역: Y = -1.5 ± 7 → 높이=14 units, X ≈ ±3.73 → 너비≈7.5 units
        // localScale=0.01로 설정하여 sizeDelta를 픽셀 기준(750×1400)으로 유지
        // → TMP fontSize(pt 단위)가 Screen Space와 동일하게 렌더됨
        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(0f, -1.5f, -1f);
        rt.sizeDelta = new Vector2(750f, 1400f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f); // 750px × 0.01 = 7.5 world units

        canvasGo.AddComponent<GraphicRaycaster>();
        return c;
    }

    private void CreateProgressDots()
    {
        var container = CreateUIObject("ProgressDots", canvas.transform);
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -12f);
        rt.sizeDelta = new Vector2(500, 80);

        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        for (int i = 0; i < 5; i++)
        {
            var dot = CreateUIObject($"Dot_{i}", container.transform);
            var img = dot.AddComponent<Image>();
            var le = dot.AddComponent<LayoutElement>();
            le.preferredWidth = 56;
            le.preferredHeight = 56;
            img.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            MakeCircle(img);
            progressDots[i] = img;
        }

        // 5단 별 표시
        var starGo = CreateUIObject("Star", container.transform);
        starLabel = starGo.AddComponent<TextMeshProUGUI>();
        starLabel.text = "*";
        starLabel.fontSize = 28;
        starLabel.color = new Color(1f, 0.84f, 0f, 0.7f);
        starLabel.alignment = TextAlignmentOptions.Center;
        if (koreanTmpFont != null) starLabel.font = koreanTmpFont;
        var starLe = starGo.AddComponent<LayoutElement>();
        starLe.preferredWidth = 16;
        starLe.preferredHeight = 56;
        starLe.ignoreLayout = true;
        var starRt = starGo.GetComponent<RectTransform>();
        starRt.anchoredPosition = new Vector2(120, 10);
    }

    private void CreateGuideText()
    {
        var container = CreateUIObject("GuideContainer", canvas.transform);
        guideGroup = container.AddComponent<CanvasGroup>();
        var rt = container.GetComponent<RectTransform>();
        // World Space Canvas 기준: 앵커를 전체(0~1)로 설정해도 viewport 충돌 없음
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 20f);
        rt.sizeDelta = new Vector2(0, 88);

        // 배경
        var bgGo = CreateUIObject("GuideBg", container.transform);
        guideBackground = bgGo.AddComponent<Image>();
        guideBackground.color = new Color(0, 0, 0, 0.5f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 텍스트
        var textGo = CreateUIObject("GuideText", container.transform);
        guideText = textGo.AddComponent<TextMeshProUGUI>();
        guideText.fontSize = 44;
        guideText.color = new Color(1f, 1f, 0.6f, 1f);
        guideText.alignment = TextAlignmentOptions.Center;
        guideText.textWrappingMode = TextWrappingModes.NoWrap;
        guideText.overflowMode = TextOverflowModes.Truncate;
        guideText.enableAutoSizing = true;
        guideText.fontSizeMin = 28;
        guideText.fontSizeMax = 44;
        if (koreanTmpFont != null) guideText.font = koreanTmpFont;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        guideGroup.alpha = 0f;
    }

    private void CreateOverlay()
    {
        var container = CreateUIObject("Overlay", canvas.transform);
        overlayGroup = container.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        // World Space Canvas: viewport 충돌 없으므로 앵커를 전체(0~1)로 복원
        var bgGo = CreateUIObject("OverlayBg", container.transform);
        overlayBg = bgGo.AddComponent<Image>();
        overlayBg.color = new Color(0, 0, 0, 0);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 메인 텍스트 — Canvas 중앙 기준으로 복원
        var mainGo = CreateUIObject("OverlayMain", container.transform);
        overlayMainText = mainGo.AddComponent<TextMeshProUGUI>();
        overlayMainText.enableAutoSizing = true;
        overlayMainText.fontSizeMin = 60;
        overlayMainText.fontSizeMax = 200;
        overlayMainText.fontStyle = FontStyles.Bold;
        overlayMainText.alignment = TextAlignmentOptions.Center;
        overlayMainText.textWrappingMode = TextWrappingModes.NoWrap;
        overlayMainText.overflowMode = TextOverflowModes.Truncate;
        if (koreanTmpFont != null) overlayMainText.font = koreanTmpFont;
        var mainRt = mainGo.GetComponent<RectTransform>();
        mainRt.anchorMin = new Vector2(0f, 0.35f);
        mainRt.anchorMax = new Vector2(1f, 0.65f);
        mainRt.sizeDelta = Vector2.zero;

        // 서브 텍스트
        var subGo = CreateUIObject("OverlaySub", container.transform);
        overlaySubText = subGo.AddComponent<TextMeshProUGUI>();
        overlaySubText.fontSize = 56;
        overlaySubText.alignment = TextAlignmentOptions.Center;
        overlaySubText.textWrappingMode = TextWrappingModes.NoWrap;
        overlaySubText.overflowMode = TextOverflowModes.Overflow;
        if (koreanTmpFont != null) overlaySubText.font = koreanTmpFont;
        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0.2f);
        subRt.anchorMax = new Vector2(1f, 0.35f);
        subRt.sizeDelta = Vector2.zero;
    }

    // ==========================================================
    // 공개 API — GameManager에서 호출
    // ==========================================================

    /// <summary>하단 안내 텍스트 갱신 (펄스 + 페이드)</summary>
    public void UpdateGuideText(string text)
    {
        if (guideCoroutine != null)
            StopCoroutine(guideCoroutine);
        guideCoroutine = StartCoroutine(DoGuideText(text));
    }

    /// <summary>안내 텍스트 즉시 숨김</summary>
    public void HideGuideText()
    {
        if (guideCoroutine != null)
        {
            StopCoroutine(guideCoroutine);
            guideCoroutine = null;
        }
        guideGroup.alpha = 0f;
    }

    /// <summary>상단 진행 도트 갱신</summary>
    public void UpdateProgressDots(int currentStage)
    {
        for (int i = 0; i < 5; i++)
        {
            int stage = i + 1;
            if (stage < currentStage)
            {
                // 완료: 금색
                progressDots[i].color = new Color(1f, 0.84f, 0f, 0.9f);
                progressDots[i].transform.localScale = Vector3.one;
            }
            else if (stage == currentStage)
            {
                // 현재: 흰색, 약간 크게
                progressDots[i].color = Color.white;
                progressDots[i].transform.localScale = Vector3.one * 1.3f;
            }
            else
            {
                // 미완료: 회색 반투명
                progressDots[i].color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                progressDots[i].transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>"N단" / "꺾기" 중앙 인트로 연출</summary>
    public void ShowStageIntro(int stage)
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoStageIntro(stage));
    }

    /// <summary>"CLEAR!" 연출</summary>
    public void ShowClear()
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoClear());
    }

    /// <summary>"FAIL" + 실패 사유 연출</summary>
    public void ShowFail(string reason)
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoFail(reason));
    }

    /// <summary>"ALL CLEAR!" + "탭하여 다시 시작"</summary>
    public void ShowAllClear()
    {
        HideGuideText();
        StopOverlay();
        overlayCoroutine = StartCoroutine(DoAllClear());
    }

    /// <summary>오버레이 즉시 숨김 (ALL CLEAR 탭 재시작 시)</summary>
    public void HideOverlay()
    {
        StopOverlay();
        overlayGroup.alpha = 0f;
    }

    // ==========================================================
    // 코루틴 연출
    // ==========================================================

    private IEnumerator DoGuideText(string text)
    {
        guideText.text = text;
        guideGroup.alpha = 1f;

        // 펄스: 0.3초간 1.5배 → 1배
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(1.5f, 1f, elapsed / 0.3f);
            guideText.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        guideText.transform.localScale = Vector3.one;

        // 3초 유지
        yield return new WaitForSeconds(3f);

        // 알파 감소 (0.5로)
        guideGroup.alpha = 0.5f;
        guideCoroutine = null;
    }

    private IEnumerator DoStageIntro(int stage)
    {
        bool isStage5 = stage == 5;
        string mainText = isStage5 ? "꺾기" : $"{stage}단";
        Color mainColor = isStage5
            ? new Color(1f, 0.84f, 0f, 1f)
            : Color.white;

        overlayMainText.text = mainText;
        overlayMainText.color = mainColor;
        overlayMainText.fontSize = isStage5 ? 100 : 80;
        overlaySubText.text = isStage5 ? "5단 — 최종" : "";
        overlaySubText.color = new Color(1f, 1f, 1f, 0.8f);
        overlayBg.color = isStage5 ? new Color(0, 0, 0, 0.4f) : new Color(0, 0, 0, 0);
        overlayGroup.alpha = 1f;

        float holdTime = isStage5 ? 0.5f : 0.3f;
        float fadeTime = isStage5 ? 1.5f : 0.9f;

        // 홀드
        yield return new WaitForSeconds(holdTime);

        // 페이드 아웃
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            overlayMainText.color = new Color(mainColor.r, mainColor.g, mainColor.b, alpha);
            overlaySubText.color = new Color(1f, 1f, 1f, alpha * 0.8f);
            if (isStage5)
                overlayBg.color = new Color(0, 0, 0, 0.4f * alpha);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        overlayCoroutine = null;
    }

    private IEnumerator DoClear()
    {
        overlayMainText.text = "CLEAR!";
        overlayMainText.fontSize = 80;
        overlaySubText.text = "";
        overlayBg.color = new Color(0, 0, 0, 0.3f);
        overlayGroup.alpha = 1f;

        // 금색 밝기 펄스
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.8f + 0.2f * Mathf.Sin(elapsed * 4f);
            overlayMainText.color = new Color(1f, 0.84f, 0f, pulse);
            yield return null;
        }

        overlayGroup.alpha = 0f;
        overlayCoroutine = null;
    }

    private IEnumerator DoFail(string reason)
    {
        overlayMainText.text = "FAIL";
        overlayMainText.fontSize = 80;
        overlayMainText.color = new Color(1f, 0.27f, 0.27f, 0f);
        overlaySubText.text = "";
        overlaySubText.color = Color.clear;
        overlayBg.color = new Color(0, 0, 0, 0);
        overlayGroup.alpha = 1f;

        // 빨간 flash (0~0.3초)
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            float flashAlpha = elapsed < 0.15f
                ? Mathf.Lerp(0f, 0.4f, elapsed / 0.15f)
                : Mathf.Lerp(0.4f, 0f, (elapsed - 0.15f) / 0.15f);
            overlayBg.color = new Color(1f, 0f, 0f, flashAlpha);

            float textAlpha = Mathf.Clamp01(elapsed / 0.2f);
            overlayMainText.color = new Color(1f, 0.27f, 0.27f, textAlpha);
            yield return null;
        }

        overlayBg.color = new Color(0, 0, 0, 0);
        overlayMainText.color = new Color(1f, 0.27f, 0.27f, 1f);

        // 실패 사유 (0.3초 후 페이드인)
        if (!string.IsNullOrEmpty(reason))
        {
            overlaySubText.text = reason;
            elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / 0.2f);
                overlaySubText.color = new Color(1f, 1f, 1f, a * 0.9f);
                yield return null;
            }
        }

        // 나머지 시간 유지 (~1.0초)
        yield return new WaitForSeconds(1.0f);

        overlayGroup.alpha = 0f;
        overlayCoroutine = null;
    }

    private IEnumerator DoAllClear()
    {
        overlayBg.color = new Color(0, 0, 0, 0.5f);
        overlayMainText.text = "ALL CLEAR!";
        overlayMainText.fontSize = 100;
        overlaySubText.text = "탭하여 다시 시작";
        overlayGroup.alpha = 1f;

        // 무한 펄스 (외부에서 HideOverlay로 종료)
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.7f + 0.3f * Mathf.Sin(elapsed * Mathf.PI);
            overlayMainText.color = new Color(1f, 0.84f, 0f, pulse);

            float blink = Mathf.Sin(elapsed * 3f) > 0f ? 0.7f : 0.3f;
            overlaySubText.color = new Color(1f, 1f, 1f, blink);
            yield return null;
        }
    }

    // ==========================================================
    // 유틸리티
    // ==========================================================

    private void StopOverlay()
    {
        if (overlayCoroutine != null)
        {
            StopCoroutine(overlayCoroutine);
            overlayCoroutine = null;
        }
        // 방어적 리셋 — 코루틴이 중단될 때 색상값이 남아 잔상 발생하므로
        // overlayGroup.alpha 뿐 아니라 개별 색상도 즉시 초기화.
        if (overlayGroup != null)
            overlayGroup.alpha = 0f;
        if (overlayBg != null)
            overlayBg.color = new Color(0, 0, 0, 0);
        if (overlayMainText != null)
            overlayMainText.color = Color.white;
        if (overlaySubText != null)
            overlaySubText.color = Color.white;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void MakeCircle(Image img)
    {
        if (circleSprite == null)
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f - 1;

            for (int px = 0; px < size; px++)
                for (int py = 0; py < size; py++)
                {
                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(center, center));
                    tex.SetPixel(px, py, dist <= radius ? Color.white : Color.clear);
                }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
        img.sprite = circleSprite;
    }
}
