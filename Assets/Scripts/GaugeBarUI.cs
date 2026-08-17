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
/// 같은 초록으로 그려뒀다). 그래서 둘 다 뺐다 — 되돌리려면 이 주석 아래 구조에
/// 라벨을 다시 붙이면 된다.
/// </summary>
public class GaugeBarUI : MonoBehaviour
{
    public static GaugeBarUI Instance { get; private set; }

    // 시안 실측(1920×1080, 좌상단 원점)
    private const float BarX = 628f, BarY = 956f, BarW = 664f, BarH = 64f;
    private const float TrackInsetX = 12f, TrackInsetY = 12f;   // innerBox 640×40 @(12,12)

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
    public void SetValue(float value)
    {
        if (barFill == null) return;
        // Type.Filled로 왼쪽부터 드러낸다. 폭을 직접 줄이면 시안의 "가운데가 밝은" 심지가
        // 같이 압축돼 색이 달라 보인다.
        barFill.fillAmount = Mathf.Clamp01(value);
    }

    /// <summary>게이지 표시</summary>
    public void Show()
    {
        if (barRoot == null) return;
        barRoot.SetActive(true);
        SetValue(0f);
    }

    /// <summary>게이지 숨김</summary>
    public void Hide()
    {
        if (barRoot != null) barRoot.SetActive(false);
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
            canvasRt.sizeDelta = new Vector2(UISkin.GamePx(BarW), UISkin.GamePx(BarH));
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
        trackRt.offsetMin = new Vector2(UISkin.GamePx(TrackInsetX), UISkin.GamePx(TrackInsetY));
        trackRt.offsetMax = new Vector2(-UISkin.GamePx(TrackInsetX), -UISkin.GamePx(TrackInsetY));
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
