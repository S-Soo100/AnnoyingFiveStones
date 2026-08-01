using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// 연출 구간을 **프레임 단위로 연속 촬영**하는 개발용 도구 (v17).
///
/// 왜 필요한가: "게임 시작 때 0.1초 깜빡한다" 같은 현상은 한 장짜리 스크린샷으로는 못 잡는다.
/// 전환 연출은 여러 캔버스(타이틀·커튼·스토리 멘트)가 겹쳐 페이드하므로,
/// 어느 순간 무엇이 안 가려지는지 프레임을 늘어놓고 봐야 한다.
///
/// ⚠️ Screen Space Overlay UI까지 담아야 하므로 카메라 RenderTexture가 아니라
///    ScreenCapture를 쓴다(오버레이는 카메라에 안 잡힌다).
/// 결과: Screenshots/intro/000.png ...
/// </summary>
public class IntroFrameCapture : MonoBehaviour
{
    private const string OutDir = "Screenshots/intro";

    /// <summary>frames장을 everyN 프레임 간격으로 찍는다.</summary>
    public static void Begin(int frames = 40, int everyN = 3)
    {
        var go = new GameObject("IntroFrameCapture");
        DontDestroyOnLoad(go);
        go.AddComponent<IntroFrameCapture>().StartCoroutine(
            go.GetComponent<IntroFrameCapture>().Run(frames, everyN));
    }

    private IEnumerator Run(int frames, int everyN)
    {
        // 에디터가 포커스를 잃어도 프레임이 돌아야 한다(MCP로 조작할 땐 항상 포커스가 없다).
        Application.runInBackground = true;

        var dir = Path.Combine(Directory.GetCurrentDirectory(), OutDir);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        for (int i = 0; i < frames; i++)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{i:D3}.png"));
            for (int k = 0; k < everyN; k++) yield return new WaitForEndOfFrame();
        }
        Debug.Log($"[IntroFrameCapture] {frames}장 저장: {dir}");
        Destroy(gameObject);
    }
}
