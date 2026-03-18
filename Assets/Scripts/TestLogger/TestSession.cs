using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 테스트 세션 데이터. 플레이 시작~종료까지의 모든 이벤트.
/// </summary>
[Serializable]
public class TestSession
{
    public string sessionId;
    public string startTimeUtc;
    public string endTimeUtc;
    public float durationSeconds;
    public string platform;
    public string buildType;        // "Editor" | "Development" | "Release"
    public string screenResolution;
    public int targetFrameRate;

    // 게임 결과 요약
    public int highestStageReached;
    public int totalAttempts;       // 1단 리셋 횟수 + 1
    public int totalFailures;
    public Dictionary<string, int> failureReasons = new();  // 실패 원인별 횟수

    // 단계별 통계
    public List<StageAttempt> stageAttempts = new();

    // 전체 이벤트 로그
    public List<TestEvent> events = new();

    public TestSession()
    {
        sessionId = Guid.NewGuid().ToString("N")[..8];
        startTimeUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public void Initialize()
    {
        platform = Application.platform.ToString();
        screenResolution = $"{Screen.width}x{Screen.height}";
        targetFrameRate = Application.targetFrameRate;

#if UNITY_EDITOR
        buildType = "Editor";
#elif DEVELOPMENT_BUILD
        buildType = "Development";
#else
        buildType = "Release";
#endif
    }

    public void Finish()
    {
        endTimeUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        durationSeconds = Time.time;
    }
}

[Serializable]
public class StageAttempt
{
    public int stage;
    public int attemptNumber;   // 이 스테이지의 몇 번째 시도
    public float startTime;
    public float endTime;
    public bool succeeded;
    public string failReason;   // 실패 시 원인
    public float scatterPower;  // 뿌리기 세기
    public int stonesPickedCorrectly;
    public float catchTimeRemaining; // 받기 시 남은 시간
}
