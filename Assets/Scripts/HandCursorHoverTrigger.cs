using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 버튼에 붙이는 컴포넌트. 호버 시 손 포즈를 변경.
/// HandCursorUI(타이틀/묘지)가 활성이면 UI 손에, 비활성이면 3D HandController에 전달.
/// </summary>
public class HandCursorHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private HandPose hoverPose = HandPose.PointIndex;

    public HandPose HoverPose
    {
        get => hoverPose;
        set => hoverPose = value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyPose(hoverPose);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyPose(HandPose.Open);
    }

    private void ApplyPose(HandPose pose)
    {
        // UI 손 커서가 활성이면 그쪽에 전달
        if (HandCursorUI.Instance != null && HandCursorUI.Instance.gameObject.activeInHierarchy
            && HandCursorUI.Instance.IsActive)
        {
            HandCursorUI.Instance.SetPose(pose);
        }
        else
        {
            // 게임 중: 3D HandController에 전달 (포즈만, 불투명도는 SetCatchMode가 관리)
            var hand = FindFirstObjectByType<HandController>();
            if (hand != null)
                hand.SetHandPose(pose);
        }
    }
}
