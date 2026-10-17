using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뿌리기 세기 게이지. ScatterSystem / HandController에서 Show/Hide/SetValue 호출.
///
/// v18: UI 시안(Figma Power 572:456) 실측치로 **세로 → 가로** 전환.
/// 이전에는 보드 왼쪽에 세로로 서 있어서 놀이판 위를 덮었다. 시안 위치를 계산해보니
/// 게이지 윗변이 돗자리 앞턱(world y −6.90 = 시안 y 956)과 정확히 맞닿아,
/// 가로로 눕히면 **판을 한 픽셀도 가리지 않는다**. 시선도 손 바로 아래에 머문다.
///
/// 시안에는 퍼센트 숫자도, 값에 따른 색 변화도 없다(채움 길이만 다른 두 상태를
/// 같은 초록으로 그려뒀다). 한때 둘 다 넣었지만 — 세기 피드백이 막대 길이 하나로
/// 줄어들기 때문이었다 — 디자이너 확인 결과 "시안대로"로 확정되어 다시 뺐다.
/// **위험 구간은 보드 위 링(ScatterRangeIndicator)이 계속 색으로 알려준다.**
/// </summary>
public class GaugeBarUI : MonoBehaviour
{
    public static GaugeBarUI Instance { get; private set; }

    // 시안 실측(1920×1080, 좌상단 원점)
    private const float BarX = 628f, BarY = 956f, BarW = 664f, BarH = 64f;
    private const float TrackInsetX = 12f, TrackInsetY = 12f;   // innerBox 640×40 @(12,12)
    /// <summary>시안 664×64는 **면 기준** — 어두운 테두리가 그 바깥에 그려진다.
    /// 틀을 사방으로 넓히면 안쪽 홈도 같은 만큼 더 들어가야 제자리에 남는다.</summary>
    private static float O => UISkin.Outset;

    private Canvas canvas;
    private GameObject barRoot;
    private Image barFill;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CreateUI();
        barRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>게이지 값 갱신 (0~1)</summary>
    /// <param name="showRisk">
    /// 시안이 값별 색 변화를 쓰지 않기로 확정돼 지금은 무시된다.
    /// 호출부(ScatterSystem·HandController)를 건드리지 않으려고 인자만 남겼다.
    /// </param>
    public void SetValue(float value, bool showRisk = true)
    {
        value = Mathf.Clamp01(value);
        // Type.Filled로 왼쪽부터 드러낸다. 폭을 직접 줄이면 시안의 "가운데가 밝은" 심지가
        // 같이 압축돼 색이 달라 보인다.
        if (barFill != null)
        {
            barFill.fillAmount = value;
            barFill.color = UISkin.SafeGreen;   // 시안은 항상 같은 초록
        }
    }

    /// <summary>게이지 표시</summary>
    public void Show()
    {
        if (barRoot == null) return;
        StopPunch();
        barRoot.SetActive(true);
        SetValue(0f);
    }

    /// <summary>게이지 숨김</summary>
    public void Hide()
    {
        StopPunch();
        if (barRoot != null) barRoot.SetActive(false);
    }

    // ── v20: 확정 펀치 ──────────────────────────────────────────────────────
    // 게이지 확정은 이 게임에서 가장 자주 하는 "결정"인데, 지금까지 소리(gauge_confirm)만
    // 나고 게이지는 그 자리에서 뚝 사라졌다. 살짝 부풀었다 숨으면 "내 입력이 박혔다"가
    // 눈에도 남는다. 값·판정·타이밍에는 아무 영향이 없다 — 숨는 시점만 0.14초 늦어진다.

    private Coroutine punchRoutine;

    /// <summary>확정 순간의 Hide 대체 — 펀치 후 스스로 숨는다.</summary>
    public void ConfirmPunch()
    {
        if (barRoot == null || !barRoot.activeSelf) { Hide(); return; }
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(DoConfirmPunch());
    }

    private void StopPunch()
    {
        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }
        if (barRoot != null) barRoot.transform.localScale = Vector3.one;
    }

    private System.Collections.IEnumerator DoConfirmPunch()
    {
        var rt = barRoot.transform;
        const float Duration = 0.14f;
        const float Peak = 1.07f;
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float s = t < 0.35f ? Mathf.Lerp(1f, Peak, t / 0.35f)             // 튀고
                                : Mathf.Lerp(Peak, 1f, (t - 0.35f) / 0.65f);  // 돌아온다
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
        punchRoutine = null;
        Hide();
    }

    private void CreateUI()
    {
        // 전용 World Space Canvas — 시안 좌표 그대로.
        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("GaugeCanvas");
            canvasGo.transform.SetParent(transform);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 99;
            canvas.worldCamera = Camera.main;

            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.position = UISkin.DesignToWorld(BarX + BarW * 0.5f, BarY + BarH * 0.5f, -0.1f);
            canvasRt.sizeDelta = new Vector2(UISkin.GamePx(BarW + O * 2f), UISkin.GamePx(BarH + O * 2f));
            canvasRt.localScale = Vector3.one * 0.01f;
        }

        barRoot = new GameObject("BarRoot", typeof(RectTransform));
        barRoot.transform.SetParent(canvas.transform, false);
        Stretch(barRoot.GetComponent<RectTransform>());

        // 바깥 알약 틀
        var frameGo = new GameObject("Frame", typeof(RectTransform));
        frameGo.transform.SetParent(barRoot.transform, false);
        Stretch(frameGo.GetComponent<RectTransform>());
        var frame = frameGo.AddComponent<Image>();
        frame.sprite = UISkin.GaugeFrame;
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = false;

        // 안쪽 홈 — 틀 안으로 12px씩 들어간다
        var trackGo = new GameObject("Track", typeof(RectTransform));
        trackGo.transform.SetParent(barRoot.transform, false);
        var trackRt = trackGo.GetComponent<RectTransform>();
        Stretch(trackRt);
        trackRt.offsetMin = new Vector2(UISkin.GamePx(TrackInsetX + O), UISkin.GamePx(TrackInsetY + O));
        trackRt.offsetMax = new Vector2(-UISkin.GamePx(TrackInsetX + O), -UISkin.GamePx(TrackInsetY + O));
        var track = trackGo.AddComponent<Image>();
        track.sprite = UISkin.GaugeTrack;
        track.type = Image.Type.Sliced;
        track.raycastTarget = false;

        // 채움 — 홈을 꽉 채우고 fillAmount로 왼쪽부터 드러난다
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(trackGo.transform, false);
        Stretch(fillGo.GetComponent<RectTransform>());
        barFill = fillGo.AddComponent<Image>();
        barFill.sprite = UISkin.GaugeFill;
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 0f;
        barFill.color = UISkin.SafeGreen;   // SetValue가 부르기 전까지의 기본색
        barFill.raycastTarget = false;

    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
