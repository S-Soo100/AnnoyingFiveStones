using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 배경 무드를 관리하는 placeholder 시스템 (v8-2).
/// Sky 그라데이션 / Table / Cloth 색상 갱신 + 주변 Primitive props 스폰.
/// 향후 실제 asset으로 교체 예정.
/// </summary>
public class BackgroundManager : MonoBehaviour
{
    [SerializeField] private SkyGradient skyGradient;
    [SerializeField] private Renderer tableRenderer;
    [SerializeField] private Renderer clothRenderer;
    [SerializeField] private Transform propsRoot;

    private List<GameObject> spawnedProps = new List<GameObject>();
    private MaterialPropertyBlock tableBlock;
    private MaterialPropertyBlock clothBlock;

    // v8-2b: 풀스크린 배경 이미지 quad (이미지가 지정된 stage에서만 활성)
    private GameObject bgImageQuad;

    // v8-2e: 매트(책상 상판) 별도 레이어 quad
    private GameObject matImageQuad;
    [SerializeField] private Vector2 liveMatOffset = Vector2.zero;
    [SerializeField] private Vector2 liveMatScale  = Vector2.one;

    // v8-2f: 매트(책상) base — config.MatCenter/MatSize. Size.zero면 Cloth.bounds 폴백.
    private Vector2 matBaseCenter;
    private Vector2 matBaseSize;
    // v8-2f: 보드 영역 base — config.BoardCenter/BoardSize.
    private Vector2 boardBaseCenter;
    private Vector2 boardBaseSize;
    // v9: 사다리꼴 보드 영역 base.
    private Vector2[] boardBaseQuad;

    [Header("Live Tuning — 보드(플레이 영역)")]
    [SerializeField] private Vector2 liveBoardOffset = Vector2.zero; // 보드 중심 가산(월드)
    [SerializeField] private Vector2 liveBoardScale  = Vector2.one;  // 보드 크기 배율

    // v8-2c: 라이브 튜닝 (Play 중 Inspector로 quad offset/scale 미세조정). 만족 시 값을 StageConfig에 옮긴다.
    [Header("Live Tuning — Play 중 Inspector로 조정")]
    [SerializeField] private bool liveTuningEnabled = false; // v12 침대 라이브튜닝 세션 완료 → 복귀
    [SerializeField] private Vector2 liveOffset = Vector2.zero;
    [SerializeField] private Vector2 liveScale  = Vector2.one;

    private StageConfig lastImageConfig; // 최근 이미지 모드 stage 기록

    private void Awake()
    {
        // 씬 참조 자동 해결
        if (skyGradient == null)
            skyGradient = FindFirstObjectByType<SkyGradient>();
        if (tableRenderer == null)
            tableRenderer = GameObject.Find("Table")?.GetComponent<Renderer>();
        if (clothRenderer == null)
            clothRenderer = GameObject.Find("Cloth")?.GetComponent<Renderer>();
        if (propsRoot == null)
        {
            var rootGo = new GameObject("StageProps");
            propsRoot = rootGo.transform;
        }

        tableBlock = new MaterialPropertyBlock();
        clothBlock = new MaterialPropertyBlock();
    }

    /// <summary>스테이지 전환 시 GameManager.StartStage에서 호출.</summary>
    public void ApplyStage(StageConfig config)
    {
        if (config == null) return;

        // v8-2f: 보드 영역 override (책상 wood = 플레이 영역). 미설정 stage는 Cloth.bounds 폴백.
        boardBaseCenter = config.BoardCenter;
        boardBaseSize   = config.BoardSize;
        // v17 보드 통일: 스테이지별 config.BoardQuad를 더 이상 쓰지 않는다.
        // 배경 아트마다 손으로 재서 맞추던 것이 v11·v12·v14·v16 버그의 뿌리였다.
        // 이제 전 스테이지가 BoardSpace 하나를 공유한다 → 1단에서 맞춘 감각이 10단까지 간다.
        // (config.BoardQuad는 되돌리기용으로 StageConfig에 남겨둔다)
        boardBaseQuad   = BoardSpace.UnifiedQuad;
        if (Application.isPlaying) { liveBoardOffset = Vector2.zero; liveBoardScale = Vector2.one; }
        ApplyBoardOverride();

        bool useImage = !string.IsNullOrEmpty(config.BackgroundImage);
        bool hasMat = !string.IsNullOrEmpty(config.MatImage);
        bool hideTableCloth = useImage || hasMat;

        if (useImage)
        {
            // 풀스크린 이미지 사용 — Sky/Props 숨김 (이미지가 이미 다 그려진 풀배경)
            ApplyBgImage(config.BackgroundImage, config.BgImageOffset, config.BgImageScale);
            if (skyGradient != null) skyGradient.gameObject.SetActive(false);
            ClearProps();

            lastImageConfig = config;
            // 라이브 튜닝 시작 값 = stage 기본값(편의). bg 이미지만. 매트는 공통 블록에서 처리.
            if (Application.isPlaying)
            {
                liveOffset = config.BgImageOffset;
                liveScale  = config.BgImageScale;
            }
        }
        else
        {
            // placeholder 모드 — 이미지 quad 숨기고 기존 색상/Props 사용
            if (bgImageQuad != null) bgImageQuad.SetActive(false);
            lastImageConfig = null;
            if (skyGradient != null) skyGradient.gameObject.SetActive(true);
            skyGradient?.ApplyColors(config.SkyBottom, config.SkyTop);

            // 색상 갱신 (placeholder 모드에서만 의미 있음 — Renderer가 꺼져도 미래 대비 안전망으로 유지)
            if (tableRenderer != null)
            {
                tableRenderer.GetPropertyBlock(tableBlock);
                tableBlock.SetColor("_BaseColor", config.TableColor);
                tableRenderer.SetPropertyBlock(tableBlock);
            }
            if (clothRenderer != null)
            {
                clothRenderer.GetPropertyBlock(clothBlock);
                clothBlock.SetColor("_BaseColor", config.ClothColor);
                clothRenderer.SetPropertyBlock(clothBlock);
            }

            ClearProps();
            if (config.Props != null)
            {
                foreach (var prop in config.Props)
                    SpawnProp(prop);
            }
        }

        // ── 공통: Table Renderer 가시성 ──
        if (tableRenderer != null) tableRenderer.enabled = !hideTableCloth;

        // ── v17 보드 통일: Cloth를 "전 스테이지 공통 돗자리"로 되살린다 ──────────
        // 기획서 v11 §2: 배경은 분위기 전용, 보드는 그 위에 깔린 별도 오브젝트.
        // Cloth는 원래 기획서 v2의 "빨간 보드"였는데 배경 이미지 도입 후 꺼둔 것이었다.
        // 이제 BoardSpace에 맞춰 항상 보이게 하면, 배경이 무엇이든 노는 자리가 하나가 된다.
        SyncBoardMat();

        // v17: MatImage(빗금 매트)는 Cloth 돗자리와 겹치므로 쓰지 않는다.
        if (matImageQuad != null)
        {
            matImageQuad.SetActive(false);
        }
    }

    /// <summary>v17 — Cloth를 BoardSpace(전 스테이지 공통 보드)에 맞춰 배치하고 항상 보이게 한다.
    ///
    /// Cloth에는 이미 <c>TrapezoidQuad</c>가 붙어 있어 윗변이 좁은 사다리꼴 메시를 만든다.
    /// 그 비율(topNarrow)과 BoardSpace의 뒷변/앞변 반폭 비가 거의 일치하므로,
    /// 위치·크기만 맞추면 "보이는 돗자리 = 판정 영역"이 성립한다.</summary>
    private void SyncBoardMat()
    {
        if (clothRenderer == null) return;

        var t = clothRenderer.transform;
        float width  = BoardSpace.FrontHalfWidth * 2f;                              // 앞변 기준(사다리꼴 밑변)
        float depth  = Mathf.Abs(BoardSpace.FrontScreenY - BoardSpace.BackScreenY);
        float centerY = (BoardSpace.BackScreenY + BoardSpace.FrontScreenY) * 0.5f;

        // ⚠️ 판정 quad(ApplyBoardOverride)와 **똑같은 live 보정**을 적용해야 한다.
        // 한쪽에만 적용하면 라이브 튜닝을 켜는 순간 "보이는 돗자리 ≠ 판정 영역"이 되어
        // 방금 없앤 버그 클래스가 그대로 되살아난다. 튜닝 중에도 둘은 항상 같아야 한다.
        t.position   = new Vector3(BoardSpace.CenterScreenX + liveBoardOffset.x,
                                   centerY + liveBoardOffset.y,
                                   t.position.z);
        t.localScale = new Vector3(width * liveBoardScale.x, depth * liveBoardScale.y, 1f);

        // 사다리꼴 비율도 BoardSpace에서 파생시킨다. 씬 값(topNarrow)을 그대로 두면
        // 뒷변 폭을 바꿨을 때 "보이는 사다리꼴 ≠ 판정 사다리꼴"이 된다.
        var trap = clothRenderer.GetComponent<TrapezoidQuad>();
        if (trap != null) trap.Rebuild(TrapezoidQuad.NarrowFromBoardSpace);

        // v17: 돗자리 비주얼(테두리·짜임·밑그림자). 배경마다 원근이 달라 변이 나란해질 수 없으므로,
        // "위에 놓인 물건"으로 읽히게 만들어 그 차이를 흡수한다.
        if (clothRenderer.GetComponent<BoardMatVisual>() == null)
            clothRenderer.gameObject.AddComponent<BoardMatVisual>();

        clothRenderer.enabled = true; // 배경 이미지 유무와 무관하게 항상 보이는 "돗자리"
    }

    private void EnsureBgImageQuad()
    {
        if (bgImageQuad != null) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        bgImageQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgImageQuad.name = "BgImageQuad";
        var col = bgImageQuad.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // 카메라 자식 — 카메라 따라다닐 수 있도록.
        bgImageQuad.transform.SetParent(cam.transform, false);
        bgImageQuad.transform.localRotation = Quaternion.identity;
        // 위치/스케일은 ApplyQuadTransform에서 stage별로 갱신.

        var rd = bgImageQuad.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        rd.material = mat;

        bgImageQuad.SetActive(false);
    }

    // ── v8-2f 보드 영역 override ─────────────────────────────────────────────

    /// <summary>boardBase + live델타로 BoardBounds override 설정. quad 있으면 quad 경로, Size.zero면 해제.</summary>
    private void ApplyBoardOverride()
    {
        // v9: quad 경로 — 4꼭짓점을 centroid 기준 scale + offset
        if (boardBaseQuad != null && boardBaseQuad.Length == 4)
        {
            Vector2 c = (boardBaseQuad[0] + boardBaseQuad[1] + boardBaseQuad[2] + boardBaseQuad[3]) * 0.25f;
            Vector2[] q = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = boardBaseQuad[i];
                q[i] = new Vector2(
                    (p.x - c.x) * liveBoardScale.x + c.x,
                    (p.y - c.y) * liveBoardScale.y + c.y
                ) + liveBoardOffset;
            }
            BoardBounds.SetQuadOverride(q);
        }
        else if (boardBaseSize == Vector2.zero)
        {
            BoardBounds.ClearOverride();
        }
        else
        {
            Vector2 center = boardBaseCenter + liveBoardOffset;
            Vector2 s = new Vector2(boardBaseSize.x * liveBoardScale.x, boardBaseSize.y * liveBoardScale.y);
            BoardBounds.SetOverride(new Rect(center.x - s.x * 0.5f, center.y - s.y * 0.5f, s.x, s.y));
        }
        // v11-fix3 보강 (Gemini 리뷰): 모든 경로 공통 — quad/Rect/Clear 후 CatchSystem 재계산
        FindFirstObjectByType<CatchSystem>()?.RecalculateBoardSurface();
    }

    // ── v8-2e 매트 레이어 ─────────────────────────────────────────────────────

    private void EnsureMatImageQuad()
    {
        if (matImageQuad != null) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        matImageQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        matImageQuad.name = "MatImageQuad";
        var col = matImageQuad.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // 카메라 자식 — 카메라 따라다닐 수 있도록
        matImageQuad.transform.SetParent(cam.transform, false);
        matImageQuad.transform.localRotation = Quaternion.identity; // Quad 노말 (0,0,-1) → 카메라(z=-10) 향함. 회전 금지.

        // URP/Unlit Transparent — Stage02_Desk.png 투명 배경 처리 필수
        var rd = matImageQuad.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);   // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend", 0f);     // 0=Alpha
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        // 밝기 일관성: bgImageQuad처럼 Emission 켜기
        // Transparent에서 alpha=0인 영역은 emission rgb가 더해져도 블렌딩 기여 0 → 투명 정상 작동
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        rd.material = mat;

        matImageQuad.SetActive(false);
    }

    /// <summary>매트 quad = base(matBaseCenter/Size, 월드) + live델타(offset 가산, scale 배율). matBaseSize.zero면 Cloth.bounds 폴백.</summary>
    private void ApplyMatQuadTransform(Vector2 liveOffset, Vector2 liveScale)
    {
        if (matImageQuad == null) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        Vector3 camPos = cam.transform.position;

        Vector2 center, size;
        if (matBaseSize == Vector2.zero)
        {
            // 폴백: 기존 Cloth.bounds 기반
            if (clothRenderer == null) return;
            Bounds b = clothRenderer.bounds;
            center = new Vector2(b.center.x, b.center.y);
            size   = new Vector2(b.size.x, b.size.y);
        }
        else { center = matBaseCenter; size = matBaseSize; }

        float localX = center.x + liveOffset.x - camPos.x;
        float localY = center.y + liveOffset.y - camPos.y;
        matImageQuad.transform.localPosition = new Vector3(localX, localY, 10.03f); // world z=0.03: 돌(z=0) 뒤 + Sky(z=0.05) 앞
        matImageQuad.transform.localScale    = new Vector3(size.x * liveScale.x, size.y * liveScale.y, 1f);
    }

    private void ApplyMatImage(string resourcePath, Vector2 baseCenter, Vector2 baseSize)
    {
        matBaseCenter = baseCenter;
        matBaseSize   = baseSize;
        EnsureMatImageQuad();
        if (matImageQuad == null) return;

        ApplyMatQuadTransform(liveMatOffset, liveMatScale);

        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null)
        {
            Debug.LogWarning($"[BackgroundManager] Mat image not found: {resourcePath}");
            matImageQuad.SetActive(false);
            return;
        }

        var rd = matImageQuad.GetComponent<Renderer>();
        var mat = rd.material;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", Color.white);

        matImageQuad.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>quad 위치/스케일을 stage별 offset/scale로 갱신.</summary>
    private void ApplyQuadTransform(Vector2 offset, Vector2 scale)
    {
        if (bgImageQuad == null) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float h = 2f * cam.orthographicSize;
        float w = h * cam.aspect;
        // 매트(z=0)보다 멀리(z=50)에 배치. offset.xy로 미세조정.
        bgImageQuad.transform.localPosition = new Vector3(offset.x, offset.y, 50f);
        bgImageQuad.transform.localScale = new Vector3(w * scale.x, h * scale.y, 1f);
    }

    private void ApplyBgImage(string resourcePath, Vector2 offset, Vector2 scale)
    {
        EnsureBgImageQuad();
        if (bgImageQuad == null) return;

        ApplyQuadTransform(offset, scale);

        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null)
        {
            Debug.LogWarning($"[BackgroundManager] Background image not found: {resourcePath}");
            bgImageQuad.SetActive(false);
            return;
        }

        var rd = bgImageQuad.GetComponent<Renderer>();
        var mat = rd.material;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", Color.white);

        bgImageQuad.SetActive(true);
    }

    // 라이브 튜닝: Play 중 Inspector에서 liveOffset/liveScale 바꾸면 즉시 quad에 반영.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (!liveTuningEnabled) return;
        if (lastImageConfig == null || bgImageQuad == null || !bgImageQuad.activeSelf) return;
        ApplyQuadTransform(liveOffset, liveScale);

        // 매트 라이브 튜닝
        if (lastImageConfig != null && !string.IsNullOrEmpty(lastImageConfig.MatImage)
            && matImageQuad != null && matImageQuad.activeSelf)
            ApplyMatQuadTransform(liveMatOffset, liveMatScale);

        // 보드 override 라이브 갱신 (BoardBoundsDebugDrawer 빨간 박스가 즉시 반영)
        ApplyBoardOverride();
        SyncBoardMat(); // v17: 돗자리도 함께 — 보이는 것과 판정이 튜닝 중에도 어긋나지 않게
    }

    private void ClearProps()
    {
        foreach (var go in spawnedProps)
        {
            if (go != null)
                Object.Destroy(go);
        }
        spawnedProps.Clear();
    }

    private void SpawnProp(BackgroundProp p)
    {
        PrimitiveType type = p.Shape switch
        {
            BackgroundPropShape.Cube     => PrimitiveType.Cube,
            BackgroundPropShape.Sphere   => PrimitiveType.Sphere,
            BackgroundPropShape.Cylinder => PrimitiveType.Cylinder,
            BackgroundPropShape.Capsule  => PrimitiveType.Capsule,
            _                           => PrimitiveType.Cube,
        };

        var go = GameObject.CreatePrimitive(type);
        go.name = $"BgProp_{type}";
        go.transform.SetParent(propsRoot, false);
        go.transform.position = p.Position;
        go.transform.localScale = p.Scale;

        // Collider 제거 — 낙 판정 / 물리에 영향을 주지 않도록
        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        // 머테리얼 색 + Emission (조명 의존 제거, URP _BaseColor)
        var rd = go.GetComponent<Renderer>();
        if (rd != null)
        {
            // 새 머테리얼 인스턴스에 색 적용 (다른 prop과 공유 방지)
            var mat = rd.material;
            mat.SetColor("_BaseColor", p.Color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", p.Color * 0.4f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        spawnedProps.Add(go);
    }
}
