using UnityEngine;

/// <summary>
/// Stage 10 Monochrome 전용 숫자 라벨 빌보드.
/// Stone의 Z축 회전을 무시하고 월드 rotation 고정 + position만 추적.
/// </summary>
public class StoneLabel : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset;

    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        transform.position = target.position + worldOffset;
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }
}
