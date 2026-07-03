using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Stage 4 [25살] 순서대로 잡기 기믹.
/// 던지기 전 보드의 4개 돌에 1~4 번호를 랜덤 배정 → 순서대로만 줍기 가능.
/// </summary>
public class SequenceGimmick : StageGimmick
{
    private bool sequenceAssigned = false;
    private int currentExpected = 1;
    private int currentStageInLoop;
    private Dictionary<int, int> stoneSequence = new Dictionary<int, int>(); // stoneIndex → 번호(1~4)
    private Dictionary<int, GameObject> stoneLabels = new Dictionary<int, GameObject>(); // stoneIndex → 라벨 GO
    private List<GameObject> allLabels = new List<GameObject>();

    public override void OnStageStart(int stageInLoop)
    {
        sequenceAssigned = false;
        currentExpected = 1;
        currentStageInLoop = stageInLoop;
        stoneSequence.Clear();
        stoneLabels.Clear();
        allLabels.Clear();

        Debug.Log($"[SequenceGimmick] Stage {stageInLoop} started. Waiting for throw to assign sequence.");
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        if (sequenceAssigned) return;

        // thrownStone 제외 OnBoard 상태 돌 4개 수집
        var pool = StonePool.Instance;
        if (pool == null)
        {
            Debug.LogError("[SequenceGimmick] StonePool.Instance is null.");
            return;
        }

        var candidates = new List<Stone>();
        foreach (var s in pool.ActiveStones)
        {
            if (s == thrownStone) continue;
            if (s.CurrentState == Stone.State.OnBoard)
                candidates.Add(s);
        }

        if (candidates.Count != 4)
        {
            Debug.LogError($"[SequenceGimmick] Expected 4 OnBoard stones (excl. thrown), found {candidates.Count}. Sequence not assigned.");
            return;
        }

        // Fisher-Yates 셔플로 1~4 배정
        var numbers = new int[] { 1, 2, 3, 4 };
        for (int i = numbers.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = numbers[i];
            numbers[i] = numbers[j];
            numbers[j] = tmp;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            var stone = candidates[i];
            stoneSequence[stone.StoneIndex] = numbers[i];
            CreateLabel(stone, numbers[i].ToString());
        }

        sequenceAssigned = true;

        GameUI.Instance?.UpdateGuideText("[ 1 → 2 → 3 → 4 순서대로 잡으세요! ]");
        Debug.Log("[SequenceGimmick] Sequence assigned to 4 stones.");
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
