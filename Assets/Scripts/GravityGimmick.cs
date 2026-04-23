using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 7 [40살] 중력 변화 기믹.
/// 돌의 명도(검정/회색/흰색)에 따라 낙하 속도가 다름.
/// throwDownDuration을 오버라이드하여 구현.
/// </summary>
public class GravityGimmick : StageGimmick
{
    // 무게별 낙하 시간 배율 (throwDownDuration 기준)
    private const float HeavyMultiplier  = 0.5f;   // 검정: 50% 빠른 낙하 (기존 0.65f → 대비 강화)
    private const float NormalMultiplier = 1.0f;   // 회색: 기본
    private const float LightMultiplier  = 1.8f;   // 흰색: 80% 느린 낙하 (기존 1.5f → 대비 강화)

    private enum StoneWeight { Heavy, Normal, Light }

    // 단수별 바닥 돌 구성 (던지는 돌 1개 제외한 4개)
    // 1~2단: 검정1 회색2 흰색1
    // 3~4단: 검정1 회색1 흰색2
    private static readonly StoneWeight[][] stageCompositions = new StoneWeight[][]
    {
        new[] { StoneWeight.Heavy, StoneWeight.Normal, StoneWeight.Normal, StoneWeight.Light },  // 1~2단
        new[] { StoneWeight.Heavy, StoneWeight.Normal, StoneWeight.Light, StoneWeight.Light },   // 3~4단
    };

    private Dictionary<int, StoneWeight> stoneWeights = new Dictionary<int, StoneWeight>();
    private HandController handController;

    public override void OnStageStart(int stageInLoop)
    {
        stoneWeights.Clear();

        var pool = StonePool.Instance;
        if (pool == null) return;
        var active = pool.ActiveStones;

        handController = gameManager?.GetComponentInChildren<HandController>();
        if (handController == null)
            handController = Object.FindFirstObjectByType<HandController>();

        // 단수별 구성 선택 (1~2단 = 인덱스0, 3~4단 = 인덱스1)
        int compIndex = (stageInLoop <= 2) ? 0 : 1;
        var composition = stageCompositions[compIndex];

        // 5개 돌에 무게 배정: 4개는 구성표, 1개(던지는 돌 후보)는 랜덤
        var weights = new List<StoneWeight>(composition);
        // 5번째 돌: 3가지 중 랜덤
        StoneWeight[] allWeights = { StoneWeight.Heavy, StoneWeight.Normal, StoneWeight.Light };
        weights.Add(allWeights[Random.Range(0, allWeights.Length)]);

        // Fisher-Yates 셔플
        for (int i = weights.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (weights[i], weights[j]) = (weights[j], weights[i]);
        }

        // 돌에 적용
        for (int i = 0; i < active.Length && i < weights.Count; i++)
        {
            var stone = active[i];
            var weight = weights[i];
            stoneWeights[stone.StoneIndex] = weight;

            // 시각적 색상 적용
            Stone.StoneColor color = weight switch
            {
                StoneWeight.Heavy => Stone.StoneColor.Black,
                StoneWeight.Normal => Stone.StoneColor.Gray,
                StoneWeight.Light => Stone.StoneColor.White,
                _ => Stone.StoneColor.Gray
            };
            stone.SetColor(color);
        }

        Debug.Log($"[GravityGimmick] Stage {stageInLoop} started: {weights.Count} stones assigned weights.");
    }

    public override void OnThrowStart(Stone thrownStone)
    {
        if (handController == null) return;

        // 던진 돌의 무게에 따라 낙하 시간 오버라이드
        float multiplier = NormalMultiplier;
        if (stoneWeights.TryGetValue(thrownStone.StoneIndex, out var weight))
        {
            multiplier = weight switch
            {
                StoneWeight.Heavy => HeavyMultiplier,
                StoneWeight.Normal => NormalMultiplier,
                StoneWeight.Light => LightMultiplier,
                _ => NormalMultiplier
            };
        }

        // HandController의 기본 throwDownDuration은 1.0f
        float overrideValue = 1.0f * multiplier;
        handController.SetThrowDownDurationOverride(overrideValue);

        // 무게별 낙하 커브 지정 (배율과 함께 체감 무게 차이 극대화)
        var curveMode = weight switch
        {
            StoneWeight.Heavy  => HandController.ThrowDownCurveMode.EaseIn,   // 검정: 끝에서 가속 (쾅)
            StoneWeight.Normal => HandController.ThrowDownCurveMode.Linear,   // 회색: 일정 속도
            StoneWeight.Light  => HandController.ThrowDownCurveMode.EaseOut,  // 흰색: 시작에서 빠르고 끝에서 감속 (깃털)
            _ => HandController.ThrowDownCurveMode.EaseIn
        };
        handController.SetThrowDownCurveMode(curveMode);
        Debug.Log($"[GravityGimmick] Thrown stone {thrownStone.StoneIndex} weight={weight}, multiplier={multiplier}, override={overrideValue}, curve={curveMode}");
    }

    public override void OnStageEnd()
    {
        // throwDownDuration 오버라이드 해제 + 커브 리셋
        if (handController != null)
        {
            handController.SetThrowDownDurationOverride(-1f);
            handController.SetThrowDownCurveMode(HandController.ThrowDownCurveMode.EaseIn);
        }

        // 모든 돌 색상 리셋
        var pool = StonePool.Instance;
        if (pool != null)
        {
            foreach (var s in pool.ActiveStones)
                s.ResetColorAndFake();
        }

        stoneWeights.Clear();
        Debug.Log("[GravityGimmick] Stage ended: weights and colors reset.");
    }
}
