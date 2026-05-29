using UnityEngine;

/// <summary>
/// 디버그용 보드 영역 시각화 (v11-fix3 진단 도구).
/// - 에디터: 항상 표시
/// - 릴리스 빌드: GameSession.IsTestPlay(연습 모드)일 때만 표시
///
/// 표시 라인 (월드 좌표 수평선, 카메라 viewport 좌→우 확장):
///   - 🟥 빨강: BoardBounds.SkyFloorY  (받기 모드 진입선 + 하늘 면제선)
///   - 🟧 주황: CatchSystem.BoardSurfaceY (낙 판정선 — 돌이 이 Y 통과 시 OnStoneFell)
///   - 🟨 노랑: catchAreaY = 2 (1단/5단 돌 catch 라인)
///   - 🟦 파랑: BoardBounds quad (사다리꼴 BL-BR-FR-FL)
///
/// OnGUI 라벨로 라인별 Y값 + 실시간 손 Y값 표시. 사용자가 시각적으로 "여기가 어디"를 확인 후 정확한 진단 가능.
///
/// 자동 부착: 씬 로드 후 BoardDebugLines GameObject 자동 생성. 별도 inspector 작업 불필요.
/// </summary>
public class BoardDebugLines : MonoBehaviour
{
    private Material lineMaterial;
    private CatchSystem catchSystem;
    private HandController hand;
    private const float CatchAreaY = 2f; // HandController.catchAreaY (SerializeField라 직접 접근 불가, 동일 하드코딩)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        if (FindFirstObjectByType<BoardDebugLines>() != null) return;
        var go = new GameObject("BoardDebugLines");
        go.AddComponent<BoardDebugLines>();
    }

    private void Start()
    {
        catchSystem = FindFirstObjectByType<CatchSystem>();
        hand = FindFirstObjectByType<HandController>();

        // 핵심: 사다리꼴 변형 후 Cloth Renderer.bounds.max.y가 실제로 어떤 값인지 콘솔 검증
        float skyY = BoardBounds.SkyFloorY;
        float boardY = catchSystem != null ? catchSystem.BoardSurfaceY : float.NaN;
        var rect = BoardBounds.MatRect;
        Debug.Log($"[BoardDebug] SkyFloorY={skyY:F2} | CatchSystem.BoardSurfaceY={boardY:F2} | MatRect.yMax={rect.yMax:F2} yMin={rect.yMin:F2} | HasQuad={BoardBounds.HasQuad}");
    }

    // v11-fix3 진단 종료: 사용자 요청으로 표시 OFF. 향후 진단 필요 시 true로 변경.
    // (자동 부착은 유지 — 컴포넌트 비용 거의 0, 토글로 즉시 재활성 가능)
    private const bool ShowDebugLines = false;

    private static bool ShouldShow()
    {
        if (!ShowDebugLines) return false;
#if UNITY_EDITOR
        return true;
#else
        return GameSession.Instance != null && GameSession.Instance.IsTestPlay;
#endif
    }

    private void EnsureMaterial()
    {
        if (lineMaterial != null) return;
        var shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;
        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnRenderObject()
    {
        if (!ShouldShow()) return;
        if (Camera.current == null || Camera.current != Camera.main) return;

        EnsureMaterial();
        if (lineMaterial == null) return;
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);

        // 화면 좌우 충분히 확장 (orthographic 카메라 가시 영역 커버)
        const float xLeft = -10f, xRight = 10f;
        const float z = -1f; // 카메라(-10) 앞, 보드(-0.05) 위

        // 🟥 SkyFloorY
        float skyY = BoardBounds.SkyFloorY;
        GL.Color(new Color(1f, 0.25f, 0.25f, 0.9f));
        GL.Vertex3(xLeft, skyY, z); GL.Vertex3(xRight, skyY, z);

        // 🟧 boardSurfaceY (CatchSystem)
        if (catchSystem != null)
        {
            float bsY = catchSystem.BoardSurfaceY;
            GL.Color(new Color(1f, 0.55f, 0.15f, 0.9f));
            GL.Vertex3(xLeft, bsY, z); GL.Vertex3(xRight, bsY, z);
        }

        // 🟨 catchAreaY
        GL.Color(new Color(1f, 0.95f, 0.2f, 0.9f));
        GL.Vertex3(xLeft, CatchAreaY, z); GL.Vertex3(xRight, CatchAreaY, z);

        // 🟦 BoardBounds quad (사다리꼴)
        if (BoardBounds.HasQuad)
        {
            Vector2 bl = BoardBounds.QuadPoint(0f, 0f);
            Vector2 br = BoardBounds.QuadPoint(1f, 0f);
            Vector2 fl = BoardBounds.QuadPoint(0f, 1f);
            Vector2 fr = BoardBounds.QuadPoint(1f, 1f);
            GL.Color(new Color(0.35f, 0.65f, 1f, 0.9f));
            GL.Vertex3(bl.x, bl.y, z); GL.Vertex3(br.x, br.y, z);
            GL.Vertex3(br.x, br.y, z); GL.Vertex3(fr.x, fr.y, z);
            GL.Vertex3(fr.x, fr.y, z); GL.Vertex3(fl.x, fl.y, z);
            GL.Vertex3(fl.x, fl.y, z); GL.Vertex3(bl.x, bl.y, z);
        }
        else
        {
            // AABB fallback (Cloth Renderer bounds)
            var r = BoardBounds.MatRect;
            GL.Color(new Color(0.5f, 0.5f, 0.8f, 0.7f));
            GL.Vertex3(r.xMin, r.yMin, z); GL.Vertex3(r.xMax, r.yMin, z);
            GL.Vertex3(r.xMax, r.yMin, z); GL.Vertex3(r.xMax, r.yMax, z);
            GL.Vertex3(r.xMax, r.yMax, z); GL.Vertex3(r.xMin, r.yMax, z);
            GL.Vertex3(r.xMin, r.yMax, z); GL.Vertex3(r.xMin, r.yMin, z);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void OnGUI()
    {
        if (!ShouldShow()) return;
        if (Camera.main == null) return;

        var korFont = KoreanFont.Get();
        if (korFont != null) GUI.skin.font = korFont;

        int fontSize = Mathf.Max(Screen.height / 55, 11);
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = fontSize };

        // 좌측 컬럼: 각 라인 Y 값
        DrawLineLabel("Sky", BoardBounds.SkyFloorY, new Color(1f, 0.25f, 0.25f), style, fontSize);
        if (catchSystem != null)
            DrawLineLabel("Board", catchSystem.BoardSurfaceY, new Color(1f, 0.55f, 0.15f), style, fontSize);
        DrawLineLabel("Catch", CatchAreaY, new Color(1f, 0.95f, 0.2f), style, fontSize);

        // 손 Y 실시간 표시
        if (hand != null)
        {
            DrawHandLabel(hand.transform.position.y, style, fontSize);
        }
    }

    private void DrawLineLabel(string name, float worldY, Color color, GUIStyle style, int fontSize)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(new Vector3(-5f, worldY, 0f));
        if (screen.z < 0f) return; // 카메라 뒤
        float guiY = Screen.height - screen.y;
        string text = $" {name} y={worldY:F2}";
        Rect rect = new Rect(10f, guiY - fontSize * 0.6f, fontSize * 7.5f, fontSize + 4f);

        // 배경
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var oldColor = style.normal.textColor;
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
        style.normal.textColor = oldColor;
    }

    private void DrawHandLabel(float handY, GUIStyle style, int fontSize)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(new Vector3(0f, handY, 0f));
        if (screen.z < 0f) return;
        float guiY = Screen.height - screen.y;
        string text = $" Hand y={handY:F2}";
        Rect rect = new Rect(Screen.width * 0.5f - fontSize * 4f, guiY - fontSize * 0.6f, fontSize * 8f, fontSize + 4f);

        GUI.color = new Color(0f, 0.5f, 0.5f, 0.75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var oldColor = style.normal.textColor;
        style.normal.textColor = new Color(0.7f, 1f, 1f);
        GUI.Label(rect, text, style);
        style.normal.textColor = oldColor;
    }
}
