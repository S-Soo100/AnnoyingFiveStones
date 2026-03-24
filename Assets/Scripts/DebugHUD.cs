using UnityEngine;

/// <summary>
/// 디버그 전용 HUD (에디터/개발빌드에서만 표시).
/// 정식 UI는 GameUI.cs (Canvas+TMP)로 이전됨.
/// </summary>
public class DebugHUD : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private CatchSystem catchSystem;

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
#endif
}
