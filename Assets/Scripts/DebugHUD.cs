using UnityEngine;

/// <summary>
/// 디버그 전용 HUD.
/// - 에디터: 항상 표시
/// - 릴리스 빌드: GameSession.IsTestPlay(연습 모드)일 때만 표시, 기록 모드에서는 숨김
/// 정식 UI는 GameUI.cs (Canvas+TMP)로 이전됨.
/// </summary>
public class DebugHUD : MonoBehaviour
{
    private CatchSystem catchSystem;
    private bool showTestPanel;

    private void Start()
    {
        catchSystem = FindFirstObjectByType<CatchSystem>();
    }

    /// <summary>
    /// 디버그 HUD를 보여야 하는가? 에디터는 항상, 빌드는 연습 모드에서만.
    /// </summary>
    private static bool ShouldShow()
    {
#if UNITY_EDITOR
        return true;
#else
        return GameSession.Instance != null && GameSession.Instance.IsTestPlay;
#endif
    }

    /// <summary>
    /// 우측 상단 "중지"(PauseButton, sizeDelta 500×250 @ 1280×720 ref)와 겹치지 않도록
    /// 우측 디버그 요소들의 시작 y 위치. 참조 해상도 기준 약 35% 이후부터 배치.
    /// </summary>
    private static float RightColumnTop()
    {
        return Mathf.Max(260f, Screen.height * 0.38f);
    }

    private void OnGUI()
    {
        if (!ShouldShow()) return;
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
        int lines = (catchSystem != null && catchSystem.IsCatchPhase) ? 4 : 3;

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
        GUI.Label(new Rect(x, y, 300, lineH), $"Regression: {GameSession.Instance?.RegressionCount ?? 0}", infoStyle);
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
        float btnY = RightColumnTop();

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
        float y = RightColumnTop() + 40f; // 스테이지 버튼(h=30) + gap=10

        GUIStyle style = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = showTestPanel
            ? new Color(1f, 0.4f, 0.3f, 0.8f)
            : new Color(0.4f, 0.4f, 0.4f, 0.6f);

        if (GUI.Button(new Rect(x, y, btnW, btnH), showTestPanel ? "TEST ✕" : "TEST ▼", style))
            showTestPanel = !showTestPanel;

        GUI.backgroundColor = origBg;
    }

    // ── 테스트 패널 (간소화: 버튼 1개만 노출) ──
    private void DrawTestPanel(GameManager gm)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        float btnH = 26f;
        float padding = 6f;
        float panelW = 220f;
        float panelH = btnH + padding * 2f;
        float panelX = Screen.width - panelW - 10f;
        float panelY = RightColumnTop() + 72f; // 스테이지(40+gap10) + 토글(28+gap4) = 40+28+4

        // 배경
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        Color origBg = GUI.backgroundColor;

        float btnW = panelW - padding * 2f;
        float x = panelX + padding;
        float y = panelY + padding;

        // 다음 스테이지로 (+5살) — 내부 동작은 "루프 1회 완료"와 동일 (5단계 자동 클리어)
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "다음 스테이지로 (+5살)", btnStyle))
        {
            for (int i = 0; i < 5; i++)
                session.OnStageComplete(i + 1);
            SidePanelUI.Instance?.Refresh();
            AgeSaturationController.Instance?.UpdateSaturation(session.CurrentAge);
            gm.StartStage(1);
        }

        GUI.backgroundColor = origBg;
    }
}
