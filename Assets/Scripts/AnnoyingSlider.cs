using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// "열받는 공기놀이" 테마 음량 슬라이더.
/// 반복 드래그할수록 저항이 커지다가 5회차에 극단으로 스냅.
/// 극단 상태에서 포인터를 떼면 1초 후 0.5로 자동 복귀.
/// Time.unscaledTime/DeltaTime 사용 → timeScale=0 일시정지 메뉴에서도 정상 작동.
/// </summary>
public class AnnoyingSlider : Slider
{
    // ── 튜닝 상수 ──────────────────────────────────────────────────────────
    private static readonly float[] DampingCurve = { 0.15f, 0.35f, 0.8f, 2.5f };
    private const int   SnapGesture     = 5;      // 5회차부터 극단 스냅
    private const float GestureWindow   = 3f;     // 연속 제스처 판정 윈도우 (초)
    private const float ExtremeLow      = 0.05f;
    private const float ExtremeHigh     = 0.95f;
    private const float AutoReturnDelay = 1.0f;   // 포인터 뗀 뒤 복귀 시작 대기 (초)
    private const float AutoReturnSpeed = 0.30f;  // 복귀 속도 (unit/sec, 0~1 스케일)
    private const float ReturnTarget    = 0.5f;   // 자동 복귀 목표값

    // ── 상태 ───────────────────────────────────────────────────────────────
    private int   gestureCount;         // 연속 제스처 회차 (1부터 카운트, 0 = 비활성)
    private float lastGestureEndTime;   // 마지막 OnPointerUp 시각 (unscaledTime)
    private bool  isDragging;
    private float releaseTime;          // 마지막 OnPointerUp 시각 — 복귀 대기 기준
    private bool  returning;            // 자동 복귀 진행 중

    // ── 포인터 다운 — 제스처 카운트 갱신 ─────────────────────────────────
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);

        // GestureWindow 초과 시 연속성 리셋
        if (Time.unscaledTime - lastGestureEndTime > GestureWindow)
            gestureCount = 0;

        gestureCount++;
        isDragging = true;
        returning  = false; // 드래그 시작 시 복귀 중단
    }

    // ── Set 오버라이드 — 저항/스냅 로직 ──────────────────────────────────
    protected override void Set(float input, bool sendCallback)
    {
        // 복귀 중이거나 드래그 외부 세팅(초기화 등)은 그대로 통과
        if (returning || !isDragging)
        {
            base.Set(input, sendCallback);
            return;
        }

        // 5회차 이상 — 극단 스냅
        if (gestureCount >= SnapGesture)
        {
            float target = input > value ? 1f : 0f;
            base.Set(target, sendCallback);
            return;
        }

        // 1~4회차 — 감쇠 저항
        int   idx    = Mathf.Clamp(gestureCount - 1, 0, DampingCurve.Length - 1);
        float damp   = DampingCurve[idx];
        float damped = value + (input - value) * damp;
        base.Set(damped, sendCallback);
    }

    // ── 포인터 업 — 저장 + 복귀 트리거 기록 ──────────────────────────────
    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);

        isDragging           = false;
        lastGestureEndTime   = Time.unscaledTime;
        releaseTime          = Time.unscaledTime;

        // 현재 값 PlayerPrefs 저장
        AudioManager.SetMasterVolume(value);

        // 극단 판정은 Update에서 releaseTime 기준으로 처리 — 여기서는 returning 플래그를 건드리지 않음
    }

    // ── Update — 극단 감지 + 자동 복귀 ──────────────────────────────────
    private void Update()
    {
        if (isDragging) return;

        // 극단 범위 && 대기 시간 경과 && 아직 복귀 시작 전
        if (!returning
            && (value <= ExtremeLow || value >= ExtremeHigh)
            && Time.unscaledTime - releaseTime >= AutoReturnDelay)
        {
            returning = true;
        }

        if (returning)
        {
            float newVal = Mathf.MoveTowards(value, ReturnTarget, AutoReturnSpeed * Time.unscaledDeltaTime);
            // returning=true 이므로 Set 오버라이드 내에서 base.Set 직통 호출
            Set(newVal, true);

            if (Mathf.Approximately(newVal, ReturnTarget))
            {
                returning     = false;
                gestureCount  = 0;
                AudioManager.SetMasterVolume(newVal); // 복귀 완료 저장
            }
        }
    }
}
