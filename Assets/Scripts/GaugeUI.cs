using UnityEngine;

/// <summary>
/// 뿌리기 게이지 — OnGUI로 화면에 직접 렌더링 (viewport 영향 없음).
/// 기획서: "게이지 바 | 보드 좌측 (세로, overlay) | 뿌리기 세기 전용"
/// </summary>
public class GaugeUI : MonoBehaviour
{
    private ScatterSystem scatterSystem;
    private Texture2D whiteTex;

    private void Start()
    {
        scatterSystem = FindFirstObjectByType<ScatterSystem>();

        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
    }

    private void OnGUI()
    {
        if (scatterSystem == null) return;
        if (!scatterSystem.IsGaugeActive) return;

        var korFont = KoreanFont.Get();
        if (korFont != null)
            GUI.skin.font = korFont;

        float value = scatterSystem.CurrentGaugeValue;

        // 화면 좌측 15% 위치, 세로로 크게
        float barWidth = Screen.width * 0.04f;
        float barHeight = Screen.height * 0.5f;
        float barX = Screen.width * 0.08f;
        float barY = Screen.height * 0.25f;

        // 배경 (어두운 바)
        DrawRect(barX, barY, barWidth, barHeight, new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // 채움 (아래에서 위로)
        float fillH = barHeight * value;
        float fillY = barY + barHeight - fillH;

        Color fillColor;
        if (value < 0.5f)
            fillColor = Color.Lerp(Color.green, Color.yellow, value * 2f);
        else
            fillColor = Color.Lerp(Color.yellow, Color.red, (value - 0.5f) * 2f);

        DrawRect(barX + 2, fillY, barWidth - 4, fillH, fillColor);

        // 마커 (현재 위치 수평선)
        DrawRect(barX - 4, fillY - 2, barWidth + 8, 4, Color.white);

        // 레이블
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(Screen.height / 30, 14),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(barX - 10, barY - 30, barWidth + 20, 25), "POWER", labelStyle);
        GUI.Label(new Rect(barX - 10, barY + barHeight + 5, barWidth + 20, 25),
            $"{(int)(value * 100)}%", labelStyle);
    }

    private void DrawRect(float x, float y, float w, float h, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, h), whiteTex);
        GUI.color = Color.white;
    }
}
