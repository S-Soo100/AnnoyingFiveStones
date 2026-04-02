using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 받기 시스템: Collider 기반 안착/튕김 판정 (Phase C Step 4).
/// HandHitbox → HandController.OnStoneHit() → 이 클래스의 OnPalmCatch / OnFingerBounce 호출.
/// 기획서: "바닥 돌 남음 → 같은 돌로 다시 던지기 / 전부 제거 → 다음 단"
/// </summary>
public class CatchSystem : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float bounceSpeed = 5f;
    [SerializeField] private float boardFloorY = -5f;  // 이 아래로 떨어지면 탈락

    [Header("State")]
    [SerializeField] private bool isCatchPhase;

    private HandController handController;
    private Stone thrownStone;

    // Bouncing 상태 돌 추적
    private List<Stone> bouncingStones = new List<Stone>();

    public bool IsCatchPhase => isCatchPhase;

    private void Start()
    {
        handController = FindFirstObjectByType<HandController>();
    }

    private void Update()
    {
        if (!isCatchPhase) return;

        // Bouncing 돌 바닥 도달 감시
        for (int i = bouncingStones.Count - 1; i >= 0; i--)
        {
            var stone = bouncingStones[i];
            if (stone == null || !stone.gameObject.activeSelf)
            {
                bouncingStones.RemoveAt(i);
                continue;
            }
            if (stone.transform.position.y <= boardFloorY)
            {
                OnStoneFell(stone);
                return;
            }
        }
    }

    public void BeginCatch(Stone thrown)
    {
        thrownStone = thrown;
        isCatchPhase = true;
        bouncingStones.Clear();
        Debug.Log("[CatchSystem] Catch phase started!");
    }

    public void StopCatch()
    {
        isCatchPhase = false;
        thrownStone = null;
        bouncingStones.Clear();
    }

    /// <summary>Palm 안착: 돌을 Caught 상태로 전환 → OnCatchSuccess()</summary>
    public void OnPalmCatch(Stone stone)
    {
        if (!isCatchPhase) return;
        if (stone != thrownStone) return;  // 던진 돌만 받기 대상

        Debug.Log($"[CatchSystem] Palm catch: stone {stone.StoneIndex}");
        OnCatchSuccess();
    }

    /// <summary>Finger 튕김: 돌을 Bouncing 상태로 전환 + 반사 velocity</summary>
    public void OnFingerBounce(Stone stone, Vector3 reflectDir)
    {
        if (!isCatchPhase) return;
        if (stone.CurrentState == Stone.State.Bouncing) return;  // 이미 튕김 중

        stone.SetState(Stone.State.Bouncing);
        stone.Rb.linearVelocity = reflectDir * bounceSpeed;

        if (!bouncingStones.Contains(stone))
            bouncingStones.Add(stone);

        Debug.Log($"[CatchSystem] Finger bounce: stone {stone.StoneIndex}, dir={reflectDir}");
    }

    /// <summary>Bouncing 돌이 바닥에 도달: 탈락 처리</summary>
    private void OnStoneFell(Stone stone)
    {
        Debug.Log($"[CatchSystem] Stone {stone.StoneIndex} fell to floor!");
        StopCatch();
        AudioManager.Instance?.PlayCatchFail();
        TestLogger.Instance?.LogFailure($"stone_fell_{stone.StoneIndex}");
        GameManager.Instance.SetFailReason("공기가 바닥에 떨어졌다!");
        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
    }

    private void OnCatchSuccess()
    {
        // === B1+B2: 미달 검증 (v3) ===
        int picked = handController.PickedStones.Count;
        int required = GameManager.Instance.RequiredPickCount;

        // 보드에 남은 돌 수 (모든 분기에서 필요)
        int remainingOnBoard = 0;
        foreach (var stone in GameManager.Instance.Stones)
        {
            if (stone.CurrentState == Stone.State.OnBoard)
                remainingOnBoard++;
        }

        // 3단 첫 줍기: required=-1 → 1 or 3만 허용
        if (required < 0)
        {
            if (picked != 1 && picked != 3)
            {
                StopCatch();
                AudioManager.Instance?.PlayCatchFail();
                Debug.Log($"[CatchSystem] STAGE3 INVALID PICK: {picked} (must be 1 or 3)");
                TestLogger.Instance?.LogFailure($"stage3_invalid_{picked}");
                GameManager.Instance.SetFailReason("돌을 잘못 집었다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                return;
            }
            // 첫 줍기 결과 기록 → 다음 RequiredPickCount가 결정됨
            GameManager.Instance.SetStage3FirstPick(picked);
        }
        else
        {
            int stonesAvailableAtPickTime = remainingOnBoard + picked;
            int actualRequired = Mathf.Min(required, stonesAvailableAtPickTime);

            if (picked < actualRequired)
            {
                StopCatch();
                AudioManager.Instance?.PlayCatchFail();
                Debug.Log($"[CatchSystem] UNDERPICK FAIL: picked={picked}, required={actualRequired} (req={required}, boardRemain={remainingOnBoard})");
                TestLogger.Instance?.LogFailure($"underpick_{picked}_of_{actualRequired}");
                GameManager.Instance.SetFailReason("돌을 덜 주웠다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                return;
            }
        }
        // === 미달 검증 끝 ===

        isCatchPhase = false;
        AudioManager.Instance?.PlayCatchSuccess();
        TestLogger.Instance?.LogCatch(true, 0f);

        // 던진 돌 = 캐치 완료, 손에 복귀
        thrownStone.SetState(Stone.State.Caught);
        thrownStone.transform.SetParent(handController.transform);
        thrownStone.transform.localPosition = Vector3.zero;
        Debug.Log("[CatchSystem] CATCH SUCCESS!");

        // 주운 돌들은 "수거됨" — 비활성화 (보드에서 제거)
        foreach (var stone in handController.PickedStones)
        {
            stone.transform.SetParent(null);
            stone.SetState(Stone.State.Caught);
            stone.gameObject.SetActive(false);
        }

        Debug.Log($"[CatchSystem] Remaining on board: {remainingOnBoard}");

        if (remainingOnBoard == 0)
        {
            // 모든 돌 제거 → 단계 클리어
            GameManager.Instance.SetPhase(GameManager.GamePhase.StageComplete);
        }
        else
        {
            // 돌 남음 → 같은 던지기 돌로 다시 던지기 (Throw 직행)
            // picked만 클리어, throwStone은 손에 유지
            handController.ClearPickedButKeepThrow();

            GameManager.Instance.SetPhase(GameManager.GamePhase.Throw);
        }
    }
}
