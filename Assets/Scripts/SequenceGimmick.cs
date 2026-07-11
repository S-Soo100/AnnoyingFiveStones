using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Stage 4 [25살] 순서대로 잡기 기믹.
/// v12b: 스캐터 직후 5돌 고정색 배정(던지는돌=검정, 1~4=마젠타·초록·노랑·파랑) +
///       던지는 돌 지정(검정만 집어 던질 수 있음). 던지는돌·순서 오집 = 즉시 실패.
/// </summary>
public class SequenceGimmick : StageGimmick
{
    private bool sequenceAssigned = false;
    private int currentExpected = 1;
    private int currentStageInLoop;
    private int throwStoneIndex = -1; // v12b: 지정된 던지는 돌(검정)의 StoneIndex
    private Dictionary<int, int> stoneSequence = new Dictionary<int, int>(); // stoneIndex → 번호(1~4)
    private Dictionary<int, GameObject> stoneLabels = new Dictionary<int, GameObject>(); // stoneIndex → 라벨 GO
    private List<GameObject> allLabels = new List<GameObject>();

    public override void OnStageStart(int stageInLoop)
    {
        sequenceAssigned = false;
        currentExpected = 1;
        throwStoneIndex = -1;
        currentStageInLoop = stageInLoop;
        stoneSequence.Clear();
        stoneLabels.Clear();
        allLabels.Clear();

        GameUI.Instance?.ShowComposition(); // v12: 상단 "공기 구성" 헤더 표시 (Figma 260710)

        Debug.Log($"[SequenceGimmick] Stage {stageInLoop} started. Colors assigned on scatter.");
    }

    // v12b: 색상·순서·던지는 돌을 스캐터 직후 확정 (기존: 던진 후 랜덤 배정 → 변경).
    // activeStones[0] = 던지는 돌(검정), [1..4] = 순서 1~4 (마젠타·초록·노랑·파랑).
    // 위치는 스캐터가 랜덤화하므로 색·순서만 고정 = "우리가 지정".
    public override void OnScatterComplete(Stone[] activeStones)
    {
        if (sequenceAssigned) return;
        if (activeStones == null || activeStones.Length < 5)
        {
            Debug.LogError($"[SequenceGimmick] OnScatterComplete: expected 5 stones, got {activeStones?.Length ?? 0}. Not assigned.");
            return;
        }

        // [0] 던지는 돌(검정) 지정
        var throwStone = activeStones[0];
        throwStoneIndex = throwStone.StoneIndex;
        throwStone.SetColorRGB(SequencePalette.ThrowBall);

        // [1..4] 순서 1~4: 색상 + 번호 라벨
        for (int i = 1; i <= 4; i++)
        {
            var stone = activeStones[i];
            stoneSequence[stone.StoneIndex] = i;
            stone.SetColorRGB(SequencePalette.NumberColors[i]);
            CreateLabel(stone, i.ToString());
        }

        sequenceAssigned = true;
        currentExpected = 1;

        GameUI.Instance?.UpdateGuideText("[ 검은 돌을 집어 던지세요 ]");
        Debug.Log($"[SequenceGimmick] Scatter assigned: throwStone={throwStoneIndex}, 4 ordered stones colored.");
    }

    // v12b: 던지는 돌 검증 — 지정된 검정 돌만 허용. HandController.TryHoldPickThrowStone에서 호출.
    public override bool ValidateThrowPick(Stone stone)
    {
        if (!sequenceAssigned) return true; // 배정 전에는 제약 없음(안전값)
        return stone != null && stone.StoneIndex == throwStoneIndex;
    }

    // v12b: 순서대로 잡기 가이드("검은 돌 던지세요"/"1→2→3→4")를 기믹이 직접 제어.
    // Age 25는 PushGuideText가 기본 가이드를 숨기므로, 이 플래그로 덮어쓰기를 막아 안내가 표시되게 한다.
    public override bool OverridesGuideText => true;

    public override void OnThrowStart(Stone thrownStone)
    {
        // v12b: 색 배정은 OnScatterComplete로 이동. 던진 뒤 순서 안내만 갱신.
        GameUI.Instance?.UpdateGuideText("[ 1 → 2 → 3 → 4 순서대로 잡으세요! ]");
    }

    public override bool ValidatePick(Stone stone)
    {
        if (!sequenceAssigned) return true; // 던지기 전 허용

        if (!stoneSequence.TryGetValue(stone.StoneIndex, out int n))
            return false; // 매핑 없는 돌 거부

        if (n != currentExpected)
        {
            Debug.LogWarning($"[SequenceGimmick] Wrong order: expected {currentExpected}, got {n} (stone {stone.StoneIndex})");
            return false;
        }

        // 정확한 순서 → 라벨 제거
        int idx = stone.StoneIndex;
        if (stoneLabels.TryGetValue(idx, out var labelGo))
        {
            allLabels.Remove(labelGo);
            Object.Destroy(labelGo);
            stoneLabels.Remove(idx);
        }

        currentExpected++;
        return true;
    }

    public override bool IsRoundComplete(int pickedThisRound, int remainingOnBoard)
    {
        return currentExpected > 4;
    }

    public override void OnStageEnd()
    {
        foreach (var label in allLabels)
        {
            if (label != null) Object.Destroy(label);
        }
        allLabels.Clear();
        stoneLabels.Clear();
        stoneSequence.Clear();
        sequenceAssigned = false;
        currentExpected = 1;
        throwStoneIndex = -1;

        GameUI.Instance?.HideComposition(); // v12: 상단 "공기 구성" 헤더 숨김
        GameUI.Instance?.HideGuideText();   // v12b: OverridesGuideText로 PushGuideText가 안 숨기므로, 종료(특히 실패) 시 직접 정리

        Debug.Log("[SequenceGimmick] Stage ended: cleanup complete.");
    }

    // === Private ===

    private void CreateLabel(Stone stone, string text)
    {
        Vector3 worldOffset = new Vector3(0f, 0f, -0.5f);

        var labelGo = new GameObject($"SequenceLabel_{stone.StoneIndex}");
        // SetParent 금지: StoneLabel.LateUpdate로 position만 추적
        labelGo.transform.position = stone.transform.position + worldOffset;
        labelGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        // ScaleX=-1: Y 180° 회전의 좌우반전 상쇄 (MonochromeGimmick 동일 패턴)
        labelGo.transform.localScale = new Vector3(-0.6f, 0.6f, 0.3f);

        var tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.sortingOrder = 10;

        var koreanFont = KoreanFont.GetTMP();
        if (koreanFont != null) tmp.font = koreanFont;

        tmp.outlineColor = Color.black;
        tmp.outlineWidth = 0.2f;

        var billboard = labelGo.AddComponent<StoneLabel>();
        billboard.target = stone.transform;
        billboard.worldOffset = worldOffset;

        stoneLabels[stone.StoneIndex] = labelGo;
        allLabels.Add(labelGo);
    }
}

/// <summary>
/// Stage 4 "순서대로 잡기" 색상 SOT (Figma 260710 수정사항 준수).
/// 돌 색상(SequenceGimmick.OnThrowStart)과 공기 구성 헤더(GameUI.CreateCompositionHeader)가 이 표를 공유한다.
/// index 1~4 = 번호별 색(1=마젠타·2=초록·3=노랑·4=파랑), ThrowBall = 던지는 공(검정).
/// </summary>
public static class SequencePalette
{
    public static readonly Color[] NumberColors =
    {
        Color.clear,                     // 0 미사용 (번호는 1부터)
        new Color(0.90f, 0.28f, 0.58f),  // 1 마젠타
        new Color(0.30f, 0.75f, 0.35f),  // 2 초록
        new Color(0.97f, 0.80f, 0.22f),  // 3 노랑
        new Color(0.28f, 0.55f, 0.88f),  // 4 파랑
    };

    public static readonly Color ThrowBall = new Color(0.13f, 0.13f, 0.13f); // 던지는 공 검정
}
