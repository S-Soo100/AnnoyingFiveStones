using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 5 [30살] 가짜 돌 근접 투명 컴포넌트.
/// FakeStoneGimmick이 가짜 돌에 AddComponent.
/// 마우스가 가까워지면 투명해져서 가짜임을 암시.
/// </summary>
[RequireComponent(typeof(Stone))]
public class ProximityReveal : MonoBehaviour
{
    [Header("Settings")]
    public float revealRadius = 1.5f;  // 마우스와 이 거리 이내면 투명해짐
    public float fadeSpeed = 5f;       // 투명도 전환 속도
    public float minAlpha = 0.15f;     // 최저 투명도 (거의 보이지 않음)

    private Stone stone;
    private float currentAlpha = 1f;
    private Camera mainCamera;

    private void Awake()
    {
        stone = GetComponent<Stone>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (stone == null || mainCamera == null) return;

        // 마우스 월드 좌표 계산 (New Input System)
        Vector2 mouseScreen = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(mainCamera.transform.position.z))
        );

        float dist = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(mouseWorld.x, mouseWorld.y)
        );

        float targetAlpha = dist < revealRadius ? minAlpha : 1f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
        stone.SetAlpha(currentAlpha);
    }
}
