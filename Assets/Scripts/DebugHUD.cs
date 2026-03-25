using UnityEngine;

/// <summary>
/// 디버그 전용 HUD (에디터/개발빌드에서만 표시).
/// 정식 UI는 GameUI.cs (Canvas+TMP)로 이전됨.
/// </summary>
public class DebugHUD : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private CatchSystem catchSystem;
    private bool showTestPanel;

    private void Start()
    {
        catchSystem = FindFirstObjectByType<CatchSystem>();
    }

    private void OnGUI()
    {
        if (GameManager.Instance == null) return;

        var gm = GameManager.Instance;

        var korFont = KoreanFont.Get();
        if (korFont != null)
            GUI.skin.font = korFont;

        DrawDebugInfo(gm);
        DrawStageSkipButtons(gm);
        DrawTestPanelToggle();
        if (showTestPanel) DrawTestPanel(gm);
    }

    private void DrawDebugInfo(GameManager gm)
    {
        int fontSize = Mathf.Max(Screen.height / 35, 12);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Normal
        };
        infoStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);

        float x = 10;
        float y = Screen.height - fontSize * 3 - 10;
        float lineH = fontSize + 2;
        int lines = (catchSystem != null && catchSystem.IsCatchPhase) ? 3 : 2;

        // 검은 반투명 배경
        var whiteTex = Texture2D.whiteTexture;
        Rect bgRect = new Rect(x - 4, y - 2, 180, lineH * lines + 4);
        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(bgRect, whiteTex);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, 300, lineH), $"Stage: {gm.CurrentStage}", infoStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, 300, lineH), $"Phase: {gm.CurrentPhase}", infoStyle);
        y += lineH;

        if (catchSystem != null && catchSystem.IsCatchPhase)
        {
            infoStyle.normal.textColor = new Color(1f, 0.5f, 0.5f, 0.7f);
            GUI.Label(new Rect(x, y, 300, lineH), "CATCH!", infoStyle);
        }
    }

    private void DrawStageSkipButtons(GameManager gm)
    {
        float btnW = 40f, btnH = 30f, gap = 2f;
        float totalW = btnW * 5 + gap * 4;
        float startX = Screen.width - totalW - 10f;
        float btnY = 10f;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        btnStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);

        Color origBg = GUI.backgroundColor;

        for (int i = 1; i <= 5; i++)
        {
            Rect r = new Rect(startX + (i - 1) * (btnW + gap), btnY, btnW, btnH);

            bool isCurrent = gm.CurrentStage == i;
            GUI.backgroundColor = isCurrent
                ? new Color(0.3f, 0.6f, 1f, 0.6f)
                : new Color(0.2f, 0.2f, 0.2f, 0.4f);

            if (GUI.Button(r, $"{i}", btnStyle))
            {
                gm.StartStage(i);
            }
        }

        GUI.backgroundColor = origBg;
    }

    // ── 테스트 패널 토글 버튼 ──
    private void DrawTestPanelToggle()
    {
        float btnW = 70f, btnH = 28f;
        float x = Screen.width - btnW - 10f;
        float y = 46f; // 스테이지 버튼 아래

        GUIStyle style = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = showTestPanel
            ? new Color(1f, 0.4f, 0.3f, 0.8f)
            : new Color(0.4f, 0.4f, 0.4f, 0.6f);

        if (GUI.Button(new Rect(x, y, btnW, btnH), showTestPanel ? "TEST ✕" : "TEST ▼", style))
            showTestPanel = !showTestPanel;

        GUI.backgroundColor = origBg;
    }

    // ── 테스트 패널 ──
    private void DrawTestPanel(GameManager gm)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        float panelW = 220f, panelH = 310f;
        float panelX = Screen.width - panelW - 10f;
        float panelY = 78f;

        // 배경
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        labelStyle.normal.textColor = Color.white;
        GUIStyle headerStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, fontSize = 14 };
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        Color origBg = GUI.backgroundColor;

        float x = panelX + 8f;
        float y = panelY + 6f;
        float lineH = 24f;
        float btnW = panelW - 16f;
        float btnH = 26f;

        // ── 상태 표시 ──
        GUI.Label(new Rect(x, y, btnW, lineH),
            $"나이: {session.CurrentAge}  루프: {session.CurrentLoop}  단계: {session.CurrentStageInLoop}", labelStyle);
        y += lineH;

        // ── 나이 조작 ──
        GUI.Label(new Rect(x, y, btnW, lineH), "— 나이 설정 —", headerStyle);
        y += lineH;

        // 나이 프리셋 버튼들
        int[] agePresets = { 0, 10, 25, 45, 49 };
        float smallBtnW = (btnW - 4 * 4f) / 5f;
        for (int i = 0; i < agePresets.Length; i++)
        {
            Rect r = new Rect(x + i * (smallBtnW + 4f), y, smallBtnW, btnH);
            bool isCurrent = session.CurrentAge == agePresets[i];
            GUI.backgroundColor = isCurrent
                ? new Color(0.3f, 0.8f, 0.3f, 0.8f)
                : new Color(0.3f, 0.3f, 0.3f, 0.7f);

            if (GUI.Button(r, $"{agePresets[i]}", btnStyle))
            {
                session.CurrentAge = agePresets[i];
                // 나이에 맞게 루프/스테이지도 계산
                session.CurrentLoop = agePresets[i] / 5 + 1;
                session.CurrentStageInLoop = agePresets[i] % 5 + 1;
                SidePanelUI.Instance?.Refresh();
            }
        }
        y += btnH + 6f;

        // ── 즉시 이벤트 트리거 ──
        GUI.Label(new Rect(x, y, btnW, lineH), "— 즉시 트리거 —", headerStyle);
        y += lineH;

        // ALL CLEAR (50살 도달)
        GUI.backgroundColor = new Color(1f, 0.84f, 0f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "ALL CLEAR (50살 강제)", btnStyle))
        {
            session.CurrentAge = 49;
            session.CurrentLoop = 10;
            session.CurrentStageInLoop = 5;
            SidePanelUI.Instance?.Refresh();
            // 5단 완료 처리 → OnStageComplete에서 age 50 → IsGameClear
            gm.SetPhase(GameManager.GamePhase.StageComplete);
        }
        y += btnH + 4f;

        // 단계 즉시 클리어
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "현재 단계 즉시 클리어", btnStyle))
        {
            gm.SetPhase(GameManager.GamePhase.StageComplete);
        }
        y += btnH + 4f;

        // 즉시 실패
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "즉시 실패", btnStyle))
        {
            gm.SetFailReason("디버그 실패");
            gm.SetPhase(GameManager.GamePhase.Failed);
        }
        y += btnH + 4f;

        // ── 루프 한 바퀴 (1~5단 자동 클리어) ──
        GUI.Label(new Rect(x, y, btnW, lineH), "— 빠른 진행 —", headerStyle);
        y += lineH;

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "루프 1회 완료 (+5살)", btnStyle))
        {
            for (int i = 0; i < 5; i++)
                session.OnStageComplete(i + 1);
            SidePanelUI.Instance?.Refresh();
            gm.StartStage(1);
        }
        y += btnH + 4f;

        // 5루프 한번에
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "루프 5회 완료 (+25살)", btnStyle))
        {
            for (int loop = 0; loop < 5; loop++)
                for (int i = 0; i < 5; i++)
                    session.OnStageComplete(i + 1);
            SidePanelUI.Instance?.Refresh();
            gm.StartStage(1);
        }
        y += btnH + 4f;

        // 리셋
        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "전체 리셋 (0살)", btnStyle))
        {
            session.ResetAll();
            SidePanelUI.Instance?.Refresh();
            gm.StartStage(1);
        }

        GUI.backgroundColor = origBg;
    }
#endif
}
