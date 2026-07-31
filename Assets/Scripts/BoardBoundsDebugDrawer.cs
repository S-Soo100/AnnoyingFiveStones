using UnityEngine;

/// <summary>
/// 디버그 전용 보드 영역 시각화 (v16).
///
/// 실제 판정에 쓰이는 <see cref="BoardBounds"/> 폴리곤을 빨간 선으로 그린다.
/// "보이는 테이블"과 "판정 영역"이 어긋나는 문제를 눈으로 잡기 위한 도구.
///
/// ⚠️ 반드시 LineRenderer로 그린다 — URP에서는 OnRenderObject + GL 즉시모드가
///    렌더링되지 않는다(구 BoardDebugLines가 안 보였던 원인, donts/game#21).
///
/// 표시 조건 (DebugHUD.ShouldShow와 동일 게이트):
///  - 에디터: 항상
///  - 릴리스 빌드: 연습 모드(GameSession.IsTestPlay)에서만
///  - 추가로 <see cref="Enabled"/> 토글이 꺼져 있으면 숨김 (TEST 패널에서 조작)
///
/// 모든 스테이지를 자동 지원한다. BoardBounds를 매 프레임 읽어 값이 바뀌면
/// 선을 다시 만들므로 스테이지 전환·라이브 튜닝이 즉시 반영된다.
/// quad가 없는 스테이지는 MatRect(AABB)가 그대로 그려진다.
/// </summary>
public class BoardBoundsDebugDrawer : MonoBehaviour
{
    /// <summary>TEST 패널에서 켜고 끄는 토글. 게이트를 통과해도 이게 false면 숨김.</summary>
    public static bool Enabled = true;

    /// <summary>보드 평면(z=0)보다 살짝 앞. 돌·손에 가려지지 않게 확실히 보이도록.</summary>
    private const float LineZ = -0.5f;
    private const float LineWidth = 0.06f;

    private LineRenderer line;
    private Vector2[] lastCorners;
    private bool lastHasQuad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!GateOpen()) return;
        var go = new GameObject("BoardBoundsDebugDrawer");
        go.AddComponent<BoardBoundsDebugDrawer>();
        DontDestroyOnLoad(go);
    }

    /// <summary>DebugHUD.ShouldShow와 동일한 표시 게이트.</summary>
    private static bool GateOpen()
    {
#if UNITY_EDITOR
        return true;
#else
        return GameSession.Instance != null && GameSession.Instance.IsTestPlay;
#endif
    }

    private void Awake()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.widthMultiplier = LineWidth;
        line.numCapVertices = 0;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // URP 전용 Unlit — Sprites/Default 같은 빌트인 셰이더는 URP에서 분홍으로 깨진다.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.red);
        line.material = mat;
        line.startColor = Color.red;
        line.endColor = Color.red;
    }

    private void LateUpdate()
    {
        bool show = GateOpen() && Enabled;
        if (line.enabled != show) line.enabled = show;
        if (!show) return;

        // BL → BR → FR → FL (loop=true라 마지막→처음은 자동 연결).
        // QuadPoint는 quad가 없으면 MatRect AABB를 같은 규약으로 반환하므로
        // 사다리꼴 스테이지·사각형 스테이지 모두 이 한 경로로 처리된다.
        var corners = new[]
        {
            BoardBounds.QuadPoint(0f, 0f),
            BoardBounds.QuadPoint(1f, 0f),
            BoardBounds.QuadPoint(1f, 1f),
            BoardBounds.QuadPoint(0f, 1f),
        };

        if (!Changed(corners)) return;

        for (int i = 0; i < 4; i++)
            line.SetPosition(i, new Vector3(corners[i].x, corners[i].y, LineZ));

        lastCorners = corners;
        lastHasQuad = BoardBounds.HasQuad;
    }

    /// <summary>스테이지 전환·라이브 튜닝으로 폴리곤이 바뀌었는지. 매 프레임 재생성 방지용.</summary>
    private bool Changed(Vector2[] corners)
    {
        if (lastCorners == null || lastHasQuad != BoardBounds.HasQuad) return true;
        for (int i = 0; i < 4; i++)
            if ((lastCorners[i] - corners[i]).sqrMagnitude > 1e-6f) return true;
        return false;
    }
}
