using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 받기 시스템: Collider 기반 안착/튕김 판정 (Phase C Step 4).
/// HandHitbox → HandController.OnStoneHit() → 이 클래스의 OnPalmCatch / OnFingerBounce 호출.
/// 기획서: "바닥 돌 남음 → 같은 돌로 다시 던지기 / 전부 제거 → 다음 단"
/// v6-1: boardSurfaceY (보드 표면) 기준으로 낙 판정 통일.
/// </summary>
public class CatchSystem : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float bounceSpeed = 5f;
    [SerializeField] private float boardFloorY = -9f;  // 안전망 하한선 (fallback, 평상시 발동 안 됨)

    [Header("State")]
    [SerializeField] private bool isCatchPhase;

    private HandController handController;
    private Stone thrownStone;

    // Bouncing + InAir 상태 돌 추적
    private List<Stone> bouncingStones = new List<Stone>();
    private List<Stone> inAirStones = new List<Stone>();

    // v6-1: 보드 표면 Y (런타임 계산)
    private float boardSurfaceY = -2.45f; // fallback

    public bool IsCatchPhase => isCatchPhase;
    public float BoardSurfaceY => boardSurfaceY;

    private void Start()
    {
        handController = FindFirstObjectByType<HandController>();
        CalculateBoardSurfaceY();
    }

    private void CalculateBoardSurfaceY()
    {
        var cloth = GameObject.Find("Cloth");
        if (cloth == null)
        {
            Debug.LogWarning("[CatchSystem] Cloth not found! Using fallback boardSurfaceY = -2.45");
            boardSurfaceY = -2.45f;
            return;
        }

        // Renderer.bounds.max.y를 우선 사용 (실제 메시 상단)
        var renderer = cloth.GetComponent<Renderer>();
        if (renderer != null)
        {
            boardSurfaceY = renderer.bounds.max.y;
        }
        else
        {
            // fallback: position.y + localScale.y * 0.5
            boardSurfaceY = cloth.transform.position.y + cloth.transform.localScale.y * 0.5f;
        }

        Debug.Log($"[CatchSystem] boardSurfaceY = {boardSurfaceY:F2}");
    }

    private void Update()
    {
        if (!isCatchPhase) return;

        // Bouncing 돌 감시: boardSurfaceY 도달 시 탈락, boardFloorY(-9f)는 안전망
        for (int i = bouncingStones.Count - 1; i >= 0; i--)
        {
            var stone = bouncingStones[i];
            if (stone == null || !stone.gameObject.activeSelf)
            {
                bouncingStones.RemoveAt(i);
                continue;
            }
            if (stone.transform.position.y <= boardSurfaceY)
            {
                OnStoneFell(stone);
                return;
            }
            // 안전망 fallback (비정상 상황)
            if (stone.transform.position.y <= boardFloorY)
            {
                OnStoneFell(stone);
                return;
            }
        }

        // InAir 돌 감시: boardSurfaceY 도달 시 탈락 (isKinematic=false 전환 후)
        for (int i = inAirStones.Count - 1; i >= 0; i--)
        {
            var stone = inAirStones[i];
            if (stone == null || !stone.gameObject.activeSelf)
            {
                inAirStones.RemoveAt(i);
                continue;
            }
            // Kinematic이면 코루틴이 제어 중 — 물리 낙하 전환 후에만 체크
            if (stone.Rb.isKinematic) continue;
            if (stone.CurrentState != Stone.State.InAir)
            {
                // 상태가 바뀌었으면 (Caught 등) 리스트에서 제거
                inAirStones.RemoveAt(i);
                continue;
            }
            if (stone.transform.position.y <= boardSurfaceY)
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
        inAirStones.Clear();

        // 던진 돌을 InAir 감시 리스트에 등록
        if (thrown != null && !inAirStones.Contains(thrown))
            inAirStones.Add(thrown);

        Debug.Log("[CatchSystem] Catch phase started!");
    }

    public void StopCatch()
    {
        isCatchPhase = false;
        thrownStone = null;
        bouncingStones.Clear();
        inAirStones.Clear();
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

        // v4: 기믹이 활성이면 기믹의 클리어 판정 우선
        var gimmick = GameManager.Instance.CurrentGimmick;
        bool stageComplete;
        if (gimmick != null)
        {
            stageComplete = gimmick.IsRoundComplete(picked, remainingOnBoard);
        }
        else
        {
            stageComplete = (remainingOnBoard == 0);
        }

        if (stageComplete)
        {
            // 단계 클리어
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
