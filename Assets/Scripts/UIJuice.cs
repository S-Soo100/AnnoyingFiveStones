using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// v21 — UI 쫀득함 공용 부품 2종. 생성은 UIWindow(창·버튼 빌더)와 TitleScreenUI가 붙인다.
///
/// 왜 컴포넌트인가: 창·버튼을 그리는 곳은 UIWindow 하나로 모아뒀지만(그 파일 주석 참고),
/// "열림/눌림의 움직임"까지 각 화면 코드에 흩어 넣으면 또 한쪽만 고치는 일이 생긴다.
/// OnEnable/포인터 이벤트에 걸어두면 호출부는 AddComponent 한 줄이면 끝난다.
/// </summary>

/// <summary>
/// 창이 활성화될 때마다 0.12초 스케일 팝(0.94→1). 한 프레임에 뚝 나타나던 창에
/// "자리잡는" 순간을 준다. ⚠️ unscaled로 돈다 — 일시정지 창은 timeScale=0에서 열린다.
/// 창 본체(피벗 중앙)에 붙일 것 — 전체 화면 어둠막에 붙이면 막까지 줄어든다.
/// </summary>
public class WindowOpenPop : MonoBehaviour
{
    private const float Duration = 0.12f;
    private const float From = 0.94f;
    private Coroutine routine;

    private void OnEnable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Pop());
    }

    private void OnDisable()
    {
        // 중간에 닫히면 줄어든 채 굳는다 — 다음 열림은 OnEnable이 다시 팝을 시작한다.
        routine = null;
        transform.localScale = Vector3.one;
    }

    private IEnumerator Pop()
    {
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(From, 1f, eased);
            yield return null;
        }
        transform.localScale = Vector3.one;
        routine = null;
    }
}

/// <summary>
/// 버튼 눌림 스케일(0.965) — Sunken 스프라이트 교체와 겹쳐 "들어갔다"가 손끝에 남는다.
/// 버튼 피벗이 좌상단(UIWindow.Place)이라, 그대로 줄이면 구석에 들러붙는다 →
/// Awake에서 시각 위치를 보존한 채 피벗을 중앙으로 옮긴다.
/// </summary>
public class ButtonPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const float Pressed = 0.965f;

    private void Awake()
    {
        var rt = (RectTransform)transform;
        Vector2 d = new Vector2(0.5f, 0.5f) - rt.pivot;
        if (rt.anchorMin == rt.anchorMax)
        {
            // 점 앵커: 피벗 이동만큼 anchoredPosition을 되밀어야 rect가 제자리에 남는다.
            rt.anchoredPosition += new Vector2(d.x * rt.rect.width, d.y * rt.rect.height);
        }
        // 스트레치 앵커는 offset이 rect를 정하므로 피벗만 바꾸면 된다.
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    public void OnPointerDown(PointerEventData eventData) => transform.localScale = Vector3.one * Pressed;
    public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;

    /// <summary>눌린 채 창이 닫히면 줄어든 채 굳는다 — 복원.</summary>
    private void OnDisable() => transform.localScale = Vector3.one;
}
