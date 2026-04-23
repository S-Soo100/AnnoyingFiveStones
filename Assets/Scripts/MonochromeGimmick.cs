using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Stage 10 [55살] 모노톤 기억력 기믹.
/// 3종류 공기 디자인 → 기억의 시간 → 전역 흑백 전환 → 기억으로 타겟 줍기.
/// </summary>
public class MonochromeGimmick : StageGimmick
{
    private enum DesignType { A, B, C }

    // A=Yellow "1", B=Green "2", C=Blue "3"
    private static readonly Stone.StoneColor[] designColors = {
        Stone.StoneColor.Yellow,  // A
        Stone.StoneColor.Green,   // B
        Stone.StoneColor.Blue     // C
    };
    private static readonly string[] designLabels = { "1", "2", "3" };

    // 단수별 기억의 시간 (초)
    private static readonly float[] memoryTimePerRound = { 1.6f, 1.0f, 0.5f, 0.1f };

    // T/D1/D2 배정: T=8, D1=6, D2=6 (총 20 = 기존 5 + 추가 15)
    private const int TargetCount = 8;

    private DesignType targetDesign;
    private bool targetConfirmed = false;
    private int completedRounds = 0;
    private int currentStageInLoop;

    // 기억의 시간 타이머
    private float memoryTimer = -1f;
    private bool isMemoryPhase = false;
    private bool isMonochrome = false;

    // 돌별 디자인 매핑 (StoneIndex → DesignType)
    private Dictionary<int, DesignType> stoneDesigns = new Dictionary<int, DesignType>();

    // 정리용 참조
    private List<GameObject> textLabels = new List<GameObject>();
    private Stone[] additionalStones;

    public override void OnStageStart(int stageInLoop)
    {
        currentStageInLoop = stageInLoop;
        targetConfirmed = false;
        completedRounds = 0;
        isMemoryPhase = false;
        isMonochrome = false;
        memoryTimer = -1f;
        stoneDesigns.Clear();
        textLabels.Clear();
        additionalStones = null;

        // 5개 돌에 3종류 랜덤 배정
        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        AssignDesigns(active);

        Debug.Log($"[MonochromeGimmick] Stage {stageInLoop} started: {active.Length} stones with 3 design types.");
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        if (targetConfirmed) return;

        // 타겟 확정
        if (stoneDesigns.TryGetValue(thrownStone.StoneIndex, out var design))
            targetDesign = design;
        else
            targetDesign = DesignType.A; // 폴백

        targetConfirmed = true;
        Debug.Log($"[MonochromeGimmick] Target confirmed: design={targetDesign} (stone {thrownStone.StoneIndex})");

        // 추가 15개 스폰
        var pool = StonePool.Instance;
        if (pool == null) return;

        additionalStones = pool.ActivateAdditional(15);

        // 추가 돌에 3종류 배정
        AssignAdditionalDesigns(additionalStones);

        // 추가 돌 보드 범위 내 랜덤 배치
        PlaceAdditionalStones(additionalStones);

        // GameManager에 돌 목록 갱신 알림
        gameManager?.RefreshStones();

        // 기억의 시간 시작
        int timerIndex = Mathf.Clamp(currentStageInLoop - 1, 0, memoryTimePerRound.Length - 1);
        memoryTimer = memoryTimePerRound[timerIndex];
        isMemoryPhase = true;
        isMonochrome = false;

        // UI 안내
        GameUI.Instance?.UpdateGuideText($"[ 타겟을 확인하세요 — {memoryTimer:F1}초 후 흑백 ]");

        Debug.Log($"[MonochromeGimmick] Additional {additionalStones.Length} stones spawned. Memory time: {memoryTimer}s");
    }

    public override void OnUpdate()
    {
        if (!isMemoryPhase || memoryTimer < 0f) return;

        memoryTimer -= Time.deltaTime;
        if (memoryTimer <= 0f)
        {
            // 기억의 시간 종료 → 흑백 전환
            TransitionToMonochrome();
        }
    }

    public override bool ValidatePick(Stone stone)
    {
        if (!targetConfirmed) return true; // 던지기 전 = 허용

        if (stoneDesigns.TryGetValue(stone.StoneIndex, out var design))
        {
            bool valid = design == targetDesign;
            if (!valid)
            {
                Debug.Log($"[MonochromeGimmick] ValidatePick failed: stone {stone.StoneIndex} design={design}, target={targetDesign}");
                TestLogger.Instance?.LogFailure("monochrome_wrong_design");
            }
            return valid;
        }

        return false; // 매핑 없는 돌 = 거부
    }

    public override bool IsRoundComplete(int pickedThisRound, int remainingOnBoard)
    {
        completedRounds++;
        int required = GetRequiredRounds(currentStageInLoop);
        bool complete = completedRounds >= required;
        Debug.Log($"[MonochromeGimmick] Round {completedRounds}/{required}, complete={complete}");
        return complete;
    }

    public override void OnStageEnd()
    {
        // TextMesh 라벨 정리
        foreach (var label in textLabels)
        {
            if (label != null) Object.Destroy(label);
        }
        textLabels.Clear();

        // 추가 스폰 돌 비활성화
        if (additionalStones != null && additionalStones.Length > 0)
        {
            var pool = StonePool.Instance;
            if (pool != null)
                pool.DeactivateStones(additionalStones);
            additionalStones = null;
        }

        // 흑백 상태였으면 채도 복귀
        if (isMonochrome)
        {
            var satCtrl = AgeSaturationController.Instance;
            if (satCtrl != null)
                satCtrl.RestoreFromMonochrome(55);
        }

        // 모든 활성 돌 색상 리셋
        var stonePool = StonePool.Instance;
        if (stonePool != null)
        {
            foreach (var s in stonePool.ActiveStones)
                s.ResetColorAndFake();
        }

        stoneDesigns.Clear();
        targetConfirmed = false;
        isMemoryPhase = false;
        isMonochrome = false;
        memoryTimer = -1f;

        Debug.Log("[MonochromeGimmick] Stage ended: cleanup complete.");
    }

    // === Private 메서드 ===

    /// <summary>돌 배열에 3종류 랜덤 배정 + 색상/라벨 적용</summary>
    private void AssignDesigns(Stone[] stones)
    {
        // Fisher-Yates 셔플로 3종류 균등 배정
        var designs = new DesignType[stones.Length];
        for (int i = 0; i < designs.Length; i++)
            designs[i] = (DesignType)(i % 3); // A, B, C 순환

        for (int i = designs.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (designs[i], designs[j]) = (designs[j], designs[i]);
        }

        for (int i = 0; i < stones.Length; i++)
        {
            var stone = stones[i];
            var design = designs[i];
            stoneDesigns[stone.StoneIndex] = design;
            ApplyDesignVisual(stone, design);
        }
    }

    /// <summary>추가 15개에 T/D1/D2 배정</summary>
    private void AssignAdditionalDesigns(Stone[] stones)
    {
        // 기존 5개 중 타겟 개수 확인
        int existingTargetCount = 0;
        foreach (var kvp in stoneDesigns)
        {
            if (kvp.Value == targetDesign) existingTargetCount++;
        }

        // 추가 돌의 타겟 수 = TargetCount(8) - 기존 타겟 수
        int additionalTarget = Mathf.Max(0, TargetCount - existingTargetCount);
        // 나머지를 D1, D2로 균등 배분
        int remaining = stones.Length - additionalTarget;

        // 두 방해 타입 결정
        GetDistractorTypes(out DesignType d1Type, out DesignType d2Type);

        int d1Count = remaining / 2;
        int d2Count = remaining - d1Count;

        // 디자인 배열 생성
        var designs = new DesignType[stones.Length];
        int idx = 0;
        for (int i = 0; i < additionalTarget && idx < designs.Length; i++)
            designs[idx++] = targetDesign;
        for (int i = 0; i < d1Count && idx < designs.Length; i++)
            designs[idx++] = d1Type;
        for (int i = 0; i < d2Count && idx < designs.Length; i++)
            designs[idx++] = d2Type;

        // Fisher-Yates 셔플
        for (int i = designs.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (designs[i], designs[j]) = (designs[j], designs[i]);
        }

        for (int i = 0; i < stones.Length; i++)
        {
            stoneDesigns[stones[i].StoneIndex] = designs[i];
            ApplyDesignVisual(stones[i], designs[i]);
        }
    }

    /// <summary>타겟이 아닌 2종류 반환</summary>
    private void GetDistractorTypes(out DesignType d1, out DesignType d2)
    {
        var distractors = new List<DesignType>();
        foreach (DesignType dt in System.Enum.GetValues(typeof(DesignType)))
        {
            if (dt != targetDesign) distractors.Add(dt);
        }
        d1 = distractors.Count > 0 ? distractors[0] : DesignType.B;
        d2 = distractors.Count > 1 ? distractors[1] : DesignType.C;
    }

    /// <summary>돌에 색상 + TextMeshPro 숫자 라벨 적용</summary>
    private void ApplyDesignVisual(Stone stone, DesignType design)
    {
        int idx = (int)design;
        stone.SetColor(designColors[idx]);
        string text = designLabels[idx];

        // 돌 중앙에 숫자 라벨 1개 (Z=-0.5로 메시 앞면 밖, Stone 회전 무관)
        CreateLabel(stone, text, new Vector3(0f, 0f, -0.5f), "Center");
    }

    private void CreateLabel(Stone stone, string text, Vector3 worldOffset, string suffix)
    {
        var labelGo = new GameObject($"DesignLabel_{stone.StoneIndex}_{suffix}");
        // SetParent 제거: Stone의 Z축 회전과 독립 (StoneLabel.LateUpdate로 position만 추적)
        labelGo.transform.position = stone.transform.position + worldOffset;
        // Y축 180°: TMP 앞면(+Z)을 카메라 쪽(-Z)으로 반전
        labelGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        // ScaleX=-1: Y 180° 회전이 local X를 뒤집는 부작용(좌우반전) 상쇄
        labelGo.transform.localScale = new Vector3(-0.6f, 0.6f, 0.3f);

        var tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.sortingOrder = 10;

        // 프로젝트 패턴 준수: 한글 폰트 에셋 통일 (숫자지만 렌더 파이프라인 일관성)
        var koreanFont = KoreanFont.GetTMP();
        if (koreanFont != null) tmp.font = koreanFont;

        // 가독성: 흰 글자 + 검정 외곽선 (컬러 돌 위 + 흑백 전환 전 강한 대비)
        tmp.outlineColor = Color.black;
        tmp.outlineWidth = 0.2f;

        // Billboard 컴포넌트: Stone 회전 무시하고 position만 추적
        var billboard = labelGo.AddComponent<StoneLabel>();
        billboard.target = stone.transform;
        billboard.worldOffset = worldOffset;

        textLabels.Add(labelGo);
    }

    /// <summary>추가 돌을 보드 범위 내 랜덤 배치</summary>
    private void PlaceAdditionalStones(Stone[] stones)
    {
        Rect boardRect = GetBoardRect();

        foreach (var stone in stones)
        {
            // OnBoard 상태로 설정
            stone.SetState(Stone.State.OnBoard);

            // 보드 범위 내 랜덤 위치
            float x = Random.Range(boardRect.xMin + 0.5f, boardRect.xMax - 0.5f);
            float y = Random.Range(boardRect.yMin + 0.5f, boardRect.yMax - 0.5f);

            // Kinematic으로 텔레포트 후 복원
            stone.Rb.isKinematic = true;
            stone.transform.position = new Vector3(x, y, 0f);
            stone.Rb.isKinematic = false;
            stone.Rb.useGravity = false;
            stone.Rb.linearDamping = 3f;
            stone.Rb.angularDamping = 5f;
        }
    }

    /// <summary>기억의 시간 종료 → 흑백 전환</summary>
    private void TransitionToMonochrome()
    {
        isMemoryPhase = false;
        isMonochrome = true;

        // 전역 흑백 전환
        var satCtrl = AgeSaturationController.Instance;
        if (satCtrl != null)
            satCtrl.SetFullMonochrome();

        GameUI.Instance?.UpdateGuideText("[ 타겟 숫자를 골라 주우세요 ]");
        Debug.Log("[MonochromeGimmick] Monochrome transition complete.");
    }

    /// <summary>보드 Rect 반환 (FleeGimmick과 동일 패턴)</summary>
    private Rect GetBoardRect()
    {
        if (gameManager == null || gameManager.BoardTransform == null)
            return new Rect(-4.8f, -8.3f, 9.6f, 6.1f);

        var boardPos = gameManager.BoardTransform.position;
        float halfW = 4.8f;
        float halfH = 3.05f;
        return new Rect(
            boardPos.x - halfW,
            boardPos.y - halfH,
            halfW * 2f,
            halfH * 2f
        );
    }

    /// <summary>단별 필요 라운드 수</summary>
    private int GetRequiredRounds(int stageInLoop)
    {
        return stageInLoop switch
        {
            1 => 4,
            2 => 2,
            3 => 2,
            4 => 1,
            _ => 4
        };
    }
}
