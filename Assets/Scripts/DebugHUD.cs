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
        float panelH = btnH * 5f + padding * 6f; // 버튼 5개 (연습 토글 + 스테이지 스킵 + 주마등 + 이름입력 + 묘지 미리보기)
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

        // 연습 모드 토글 — IsTestPlay 플립. ON이면 랭킹(묘지) 미저장. 홈 단일 Play 통일 후 연습 진입 경로.
        bool testPlay = session.IsTestPlay;
        GUI.backgroundColor = testPlay
            ? new Color(1f, 0.7f, 0.2f, 0.85f)
            : new Color(0.45f, 0.45f, 0.45f, 0.7f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), testPlay ? "연습 모드: ON (기록 안 함)" : "연습 모드: OFF", btnStyle))
            session.IsTestPlay = !session.IsTestPlay;
        y += btnH + padding;

        // 다음 스테이지로 (+5살) — 내부 동작은 "루프 1회 완료"와 동일 (5단계 자동 클리어)
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "다음 스테이지로 (+5살)", btnStyle))
        {
            // 1~4단은 GameSession에만 반영 (부작용 없음)
            for (int i = 1; i <= 4; i++)
                session.OnStageComplete(i);
            // 5단은 GameManager.DebugCompleteCurrentLoop() 경유 — IsGameClear 분기/전환 코루틴 정상 작동
            // v12-fix: GameManager 측 메서드가 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 가드 안에 정의되므로
            //          Release Player 빌드에서 stripping → 호출부도 동일 가드로 차단해 CS1061 회피.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gm.DebugCompleteCurrentLoop();
#endif
            SidePanelUI.Instance?.Refresh();
            AgeSaturationController.Instance?.UpdateSaturation(session.CurrentAge);
        }
        y += btnH + padding;

        // 주마등(라이프 파노라마) 미리보기 — 엔딩 카드 스크롤 연출을 즉시 재생
        GUI.backgroundColor = new Color(0.6f, 0.7f, 1f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "주마등 미리보기", btnStyle))
        {
            LifePanoramaUI.Instance?.Show(() => Debug.Log("[DEBUG] 주마등 미리보기 종료"));
        }
        y += btnH + padding;

        // 이름입력(묘비명) 미리보기 — Figma 재설계된 전체화면 입력 화면을 즉시 표시
        GUI.backgroundColor = new Color(0.8f, 0.7f, 1f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "이름입력 미리보기", btnStyle))
        {
            NameInputUI.Instance?.Show(182f, (nm, tp) => Debug.Log($"[DEBUG] 이름입력 미리보기: {nm}, test={tp}"));
        }
        y += btnH + padding;

        // 묘지(랭킹보드) 미리보기 — Figma 재설계된 흰 배경 3열 그리드를 즉시 표시 (Supabase 불필요)
        GUI.backgroundColor = new Color(0.7f, 0.9f, 0.8f, 0.8f);
        if (GUI.Button(new Rect(x, y, btnW, btnH), "묘지 미리보기", btnStyle))
            GraveyardUI.Instance?.ShowPreview();

        GUI.backgroundColor = origBg;
    }
}
