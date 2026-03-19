using UnityEngine;

/// <summary>
/// 게임 HUD — 진행도 아이콘 + 안내 텍스트 + 전환 연출 (OnGUI 기반)
/// </summary>
public class DebugHUD : MonoBehaviour
{
    private CatchSystem catchSystem;
    private Texture2D whiteTex;
    private Texture2D circleTex;
    private Texture2D circleOutlineTex;

    // 안내 텍스트 펄스
    private string lastGuideText = "";
    private float guideChangeTime;

    private void Start()
    {
        catchSystem = FindFirstObjectByType<CatchSystem>();
        whiteTex = CreateWhiteTexture();
        circleTex = CreateCircleTexture(true);
        circleOutlineTex = CreateCircleTexture(false);
    }

    private void OnGUI()
    {
        if (GameManager.Instance == null) return;

        var gm = GameManager.Instance;

        GUI.color = Color.white;

        // 한글 폰트 적용 (크로스 플랫폼)
        var korFont = KoreanFont.Get();
        if (korFont != null)
            GUI.skin.font = korFont;

        // viewport 밖 영역(좌우 검은 여백)을 매 프레임 검은색으로 덮어쓰기
        // — 오버레이 잔상 방지
        ClearViewportMargins();

        DrawProgressIcons(gm);
        DrawGuideText(gm);
        DrawTransitionOverlay(gm);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DrawDebugInfo(gm);
        DrawStageSkipButtons(gm);
#endif
    }

    // === 진행도 아이콘 (상단 중앙) ===
    private void DrawProgressIcons(GameManager gm)
    {
        float dotSize = Mathf.Max(Screen.height / 30f, 16f);
        float gap = dotSize * 0.6f;
        float totalW = dotSize * 5 + gap * 4;
        float startX = (Screen.width - totalW) / 2f;
        float y = 12f;

        for (int i = 1; i <= 5; i++)
        {
            bool completed = i < gm.CurrentStage;
            bool current = i == gm.CurrentStage;

            float size = current ? dotSize * 1.3f : dotSize;
            float offsetY = current ? y - (size - dotSize) / 2f : y;
            float offsetX = startX + (i - 1) * (dotSize + gap) + (dotSize - size) / 2f;

            Rect r = new Rect(offsetX, offsetY, size, size);

            if (completed)
            {
                // 금색 채워진 원
                GUI.color = new Color(1f, 0.84f, 0f, 0.9f);
                GUI.DrawTexture(r, circleTex);
            }
            else if (current)
            {
                // 흰색 원 (현재 단계)
                GUI.color = Color.white;
                GUI.DrawTexture(r, circleTex);
            }
            else
            {
                // 회색 윤곽선 (미완료)
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                GUI.DrawTexture(r, circleOutlineTex);
            }
        }

        GUI.color = Color.white;

        // 5단 아이콘 위에 별 표시
        float starX = startX + 4 * (dotSize + gap) + dotSize * 0.3f;
        float starY = y - 6f;
        GUIStyle starStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max((int)(dotSize * 0.5f), 10),
            alignment = TextAnchor.MiddleCenter
        };
        starStyle.normal.textColor = new Color(1f, 0.84f, 0f, 0.7f);
        GUI.Label(new Rect(starX, starY, dotSize, dotSize * 0.5f), "*", starStyle);
    }

    // === 안내 텍스트 (하단 중앙, 펄스 효과) ===
    private void DrawGuideText(GameManager gm)
    {
        // 전환 연출 중이면 안내 텍스트 숨김
        if (gm.IsTransitioning) return;

        string guide = GetGuideText(gm);

        // 텍스트 변경 감지 → 펄스 시작
        if (guide != lastGuideText)
        {
            lastGuideText = guide;
            guideChangeTime = Time.time;
        }

        float elapsed = Time.time - guideChangeTime;
        int baseFontSize = Mathf.Max(Screen.height / 25 - 4, 14);

        // 펄스: 처음 0.3초간 1.5배 → 1.0배
        float scale = 1f;
        if (elapsed < 0.3f)
            scale = Mathf.Lerp(1.5f, 1f, elapsed / 0.3f);

        // 3초 후 alpha 감소
        float alpha = elapsed > 3f ? 0.5f : 1f;

        GUIStyle guideStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)(baseFontSize * scale),
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        guideStyle.normal.textColor = new Color(1f, 1f, 0.6f, alpha);

        // viewport 내부에 배치 (좌우 여백 밖으로 넘치지 않게)
        Rect vpRect = GetViewportRect();
        float guideW = Mathf.Min(vpRect.width * 0.9f, 500f);
        float guideH = 44;
        float guideX = vpRect.x + (vpRect.width - guideW) / 2f;
        float guideY = vpRect.y + vpRect.height - guideH - 20;

        Rect guideRect = new Rect(guideX, guideY, guideW, guideH);

        // 검은 반투명 배경
        GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
        GUI.DrawTexture(guideRect, whiteTex);
        GUI.color = Color.white;

        GUI.Label(guideRect, guide, guideStyle);
    }

    private string GetGuideText(GameManager gm)
    {
        return gm.CurrentPhase switch
        {
            GameManager.GamePhase.Scatter => "[ 꾹 눌러서 게이지 조절, 놓으면 뿌리기 ]",
            GameManager.GamePhase.PickThrowStone => "[ 커서를 돌 위로 이동 ]",
            GameManager.GamePhase.Throw => "[ 클릭하여 던지기 ]",
            GameManager.GamePhase.PickStones => $"[ 돌 {gm.RequiredPickCount}개를 주우세요 ]",
            GameManager.GamePhase.Catch => "[ 커서를 움직여 돌을 받으세요! ]",
            GameManager.GamePhase.Stage5Throw => "[ 클릭하여 5개 모두 던지기! ]",
            GameManager.GamePhase.Stage5Catch => gm.Stage5Step == 0
                ? "[ 손등으로 5개 모두 받기! ]"
                : "[ 뒤집어서 손바닥으로 받기! ]",
            _ => ""
        };
    }

    // === 전환 연출 오버레이 ===
    private void DrawTransitionOverlay(GameManager gm)
    {
        if (!gm.IsTransitioning && !gm.IsAllClear) return;

        float elapsed = Time.time - gm.TransitionStartTime;
        string type = gm.TransitionType;

        switch (type)
        {
            case "stage_intro":
                DrawStageIntro(gm, elapsed);
                break;
            case "clear":
                DrawClearOverlay(elapsed);
                break;
            case "fail":
                DrawFailOverlay(gm, elapsed);
                break;
            case "all_clear":
                DrawAllClearOverlay(elapsed);
                break;
        }
    }

    private void DrawStageIntro(GameManager gm, float elapsed)
    {
        int stage = gm.CurrentStage;
        bool isStage5 = stage == 5;

        float holdTime = isStage5 ? 0.5f : 0.3f;
        float fadeTime = isStage5 ? 1.5f : 0.9f;

        // 배경 오버레이 (5단만)
        if (isStage5)
        {
            float bgAlpha = elapsed < holdTime
                ? 0.4f
                : Mathf.Lerp(0.4f, 0f, (elapsed - holdTime) / fadeTime);
            GUI.color = new Color(0f, 0f, 0f, bgAlpha);
            GUI.DrawTexture(GetViewportRect(), whiteTex);
        }

        // 메인 텍스트
        float alpha = elapsed < holdTime
            ? 1f
            : Mathf.Clamp01(1f - (elapsed - holdTime) / fadeTime);

        string mainText = isStage5 ? "꺾기" : $"{stage}단";
        int mainSize = isStage5 ? Screen.height / 5 : Screen.height / 6;
        Color mainColor = isStage5
            ? new Color(1f, 0.84f, 0f, alpha)  // 금색
            : new Color(1f, 1f, 1f, alpha);     // 흰색

        GUIStyle mainStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = mainSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        mainStyle.normal.textColor = mainColor;

        float centerY = Screen.height * 0.4f;
        GUI.Label(new Rect(0, centerY - mainSize / 2f, Screen.width, mainSize + 10), mainText, mainStyle);

        // 5단 부제
        if (isStage5)
        {
            int subSize = Screen.height / 18;
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = subSize,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(1f, 1f, 1f, alpha * 0.8f);
            GUI.Label(new Rect(0, centerY + mainSize / 2f + 5, Screen.width, subSize + 10),
                "5단 — 최종", subStyle);
        }

        GUI.color = Color.white;
    }

    private void DrawClearOverlay(float elapsed)
    {
        // 반투명 검정 오버레이
        float bgAlpha = 0.3f;
        GUI.color = new Color(0f, 0f, 0f, bgAlpha);
        GUI.DrawTexture(GetViewportRect(), whiteTex);

        // "CLEAR!" 텍스트 — 밝기 펄스
        float pulse = 0.8f + 0.2f * Mathf.Sin(elapsed * 4f);
        int fontSize = Screen.height / 8;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = new Color(1f, 0.84f, 0f, pulse); // 금색

        float centerY = Screen.height * 0.4f;
        GUI.Label(new Rect(0, centerY - fontSize / 2f, Screen.width, fontSize + 10), "CLEAR!", style);

        GUI.color = Color.white;
    }

    private void DrawFailOverlay(GameManager gm, float elapsed)
    {
        // 빨간 flash (0~0.3초)
        if (elapsed < 0.3f)
        {
            float flashAlpha = elapsed < 0.15f
                ? Mathf.Lerp(0f, 0.4f, elapsed / 0.15f)
                : Mathf.Lerp(0.4f, 0f, (elapsed - 0.15f) / 0.15f);
            GUI.color = new Color(1f, 0f, 0f, flashAlpha);
            GUI.DrawTexture(GetViewportRect(), whiteTex);
        }

        // "FAIL" 텍스트
        float textAlpha = Mathf.Clamp01(elapsed / 0.2f); // 빠르게 페이드인
        int mainSize = Screen.height / 7;

        GUIStyle failStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = mainSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        failStyle.normal.textColor = new Color(1f, 0.27f, 0.27f, textAlpha);

        float centerY = Screen.height * 0.38f;
        GUI.Label(new Rect(0, centerY - mainSize / 2f, Screen.width, mainSize + 10), "FAIL", failStyle);

        // 실패 원인
        if (elapsed > 0.3f && !string.IsNullOrEmpty(gm.LastFailReason))
        {
            float reasonAlpha = Mathf.Clamp01((elapsed - 0.3f) / 0.2f);
            int reasonSize = Screen.height / 20;

            GUIStyle reasonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = reasonSize,
                alignment = TextAnchor.MiddleCenter
            };
            reasonStyle.normal.textColor = new Color(1f, 1f, 1f, reasonAlpha * 0.9f);

            GUI.Label(new Rect(0, centerY + mainSize / 2f + 10, Screen.width, reasonSize + 10),
                gm.LastFailReason, reasonStyle);
        }

        GUI.color = Color.white;
    }

    private void DrawAllClearOverlay(float elapsed)
    {
        // 반투명 검정 오버레이
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(GetViewportRect(), whiteTex);

        // "ALL CLEAR!" 금색 펄스
        float pulse = 0.7f + 0.3f * Mathf.Sin(elapsed * Mathf.PI); // 2초 주기
        int fontSize = Screen.height / 5;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = new Color(1f, 0.84f, 0f, pulse);

        float centerY = Screen.height * 0.38f;
        GUI.Label(new Rect(0, centerY - fontSize / 2f, Screen.width, fontSize + 10), "ALL CLEAR!", style);

        // "탭하여 다시 시작" 깜빡임
        float blink = Mathf.Sin(elapsed * 3f) > 0f ? 0.7f : 0.3f;
        int subSize = Mathf.Max(Screen.height / 25, 14);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = subSize,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(1f, 1f, 1f, blink);

        GUI.Label(new Rect(0, centerY + fontSize / 2f + 30, Screen.width, subSize + 10),
            "탭하여 다시 시작", subStyle);

        GUI.color = Color.white;
    }

    // === 디버그 정보 (개발 빌드 전용) ===
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

    // === Viewport 유틸리티 ===

    /// <summary>
    /// 카메라 viewport 밖 영역(좌우 검은 여백)을 검은색으로 매 프레임 덮어쓰기.
    /// 오버레이 flash 잔상이 viewport 밖에 남는 것을 방지.
    /// </summary>
    private void ClearViewportMargins()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Rect vp = cam.pixelRect;

        GUI.color = Color.black;

        // 좌측 여백
        if (vp.x > 0)
            GUI.DrawTexture(new Rect(0, 0, vp.x, Screen.height), whiteTex);

        // 우측 여백
        float rightStart = vp.x + vp.width;
        if (rightStart < Screen.width)
            GUI.DrawTexture(new Rect(rightStart, 0, Screen.width - rightStart, Screen.height), whiteTex);

        // 상단 여백
        if (vp.y > 0)
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height - (vp.y + vp.height)), whiteTex);

        // 하단 여백
        float bottomGUIY = Screen.height - vp.y;
        if (vp.y > 0)
            GUI.DrawTexture(new Rect(0, bottomGUIY, Screen.width, vp.y), whiteTex);

        GUI.color = Color.white;
    }

    /// <summary>
    /// 카메라 viewport 영역을 OnGUI 좌표계로 변환
    /// </summary>
    private Rect GetViewportRect()
    {
        var cam = Camera.main;
        if (cam == null) return new Rect(0, 0, Screen.width, Screen.height);

        Rect vp = cam.pixelRect;
        // OnGUI는 좌상단 원점, pixelRect는 좌하단 원점 → Y 반전
        return new Rect(vp.x, Screen.height - vp.y - vp.height, vp.width, vp.height);
    }

    // === 텍스처 유틸리티 ===

    private Texture2D CreateWhiteTexture()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateCircleTexture(bool filled)
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 1;

        for (int px = 0; px < size; px++)
            for (int py = 0; py < size; py++)
            {
                float dist = Vector2.Distance(new Vector2(px, py), new Vector2(center, center));
                if (filled)
                {
                    tex.SetPixel(px, py, dist <= radius ? Color.white : Color.clear);
                }
                else
                {
                    // 윤곽선만
                    tex.SetPixel(px, py, dist <= radius && dist > radius - 2
                        ? Color.white : Color.clear);
                }
            }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
