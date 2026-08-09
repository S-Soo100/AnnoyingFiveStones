using System.Collections;
using UnityEngine;

/// <summary>
/// v18 — 실패 순간의 히트스톱 + 짧은 화면 흔들림.
///
/// 왜 필요한가:
/// 이 게임은 죽는 것이 콘텐츠라 실패를 수십 번 본다. 그런데 지금까지 실패는 **소리와 글자뿐**이라
/// 몸에 남는 게 없었다. 0.07초 정지 + 0.28초 흔들림이면 "졌다"가 손끝으로 전달되고,
/// 그만큼 **실패 연출 시간을 늘리지 않고도** 실패가 확실히 읽힌다.
///
/// 기획서 v11 §8: 실패는 즉시 재시작. 그래서 이 연출은 짧아야 하고, 기존 대기 시간을
/// 늘리지 않는다(실패 표시 1.5초 안에서 끝난다).
///
/// ⚠️ 흔들리는 동안 카메라가 움직이므로 <c>ScreenToWorldPoint</c> 결과도 흔들린다.
///    실패 전환 중에는 GameManager.isTransitioning으로 입력이 막혀 있어 문제가 없지만,
///    플레이 중에 쓰려면 손 위치 계산과의 간섭을 먼저 확인해야 한다.
/// </summary>
public class ScreenShake : MonoBehaviour
{
    private static ScreenShake instance;

    private Vector3 restPosition;
    private Coroutine routine;

    /// <summary>메인 카메라에 붙은 인스턴스를 얻는다(없으면 만든다).</summary>
    public static ScreenShake Instance
    {
        get
        {
            if (instance != null) return instance;
            var cam = Camera.main;
            if (cam == null) return null;
            instance = cam.GetComponent<ScreenShake>() ?? cam.gameObject.AddComponent<ScreenShake>();
            return instance;
        }
    }

    /// <summary>실패 타격감 — 짧게 멈췄다가 흔들린다.</summary>
    public static void PlayFailImpact() => Instance?.Play(0.28f, 0.22f, 0.07f);

    /// <param name="duration">흔들림 길이(초, 실시간).</param>
    /// <param name="magnitude">최대 흔들림 폭(월드 유닛). ortho 7 기준 화면 높이가 14다.</param>
    /// <param name="hitStopSeconds">흔들림 직전 정지 길이(초, 실시간).</param>
    public void Play(float duration, float magnitude, float hitStopSeconds)
    {
        if (!isActiveAndEnabled) return;
        if (routine != null) { StopCoroutine(routine); Restore(); }
        routine = StartCoroutine(Routine(duration, magnitude, hitStopSeconds));
    }

    private void Restore()
    {
        transform.localPosition = restPosition;
    }

    private IEnumerator Routine(float duration, float magnitude, float hitStopSeconds)
    {
        restPosition = transform.localPosition;

        // ── 히트스톱 ──
        // 일시정지 중이면 건드리지 않는다. timeScale을 되돌릴 때 일시정지를 풀어버리기 때문.
        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        if (hitStopSeconds > 0f && !paused)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopSeconds);
            // 정지 대기 중에 유저가 일시정지했을 수 있다 → 그때는 0을 유지한다.
            if (GameManager.Instance == null || !GameManager.Instance.IsPaused)
                Time.timeScale = 1f;
        }

        // ── 흔들림 ──
        // 실시간으로 돈다. 히트스톱이나 일시정지에 물려 멈추면 카메라가 어긋난 채 굳는다.
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - Mathf.Clamp01(elapsed / duration);   // 끝으로 갈수록 잦아든다
            float amp = magnitude * decay * decay;                  // 제곱 감쇠 — 첫 순간만 세게
            transform.localPosition = restPosition + new Vector3(
                Random.Range(-amp, amp), Random.Range(-amp, amp), 0f);
            yield return null;
        }

        Restore();
        routine = null;
    }

    /// <summary>씬/오브젝트가 꺼질 때 카메라가 어긋난 위치로 굳는 것을 막는다.</summary>
    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
            Restore();
        }
    }
}
