using UnityEngine;

/// <summary>
/// 손 자식 Collider에 부착. 충돌 이벤트를 부모 HandController에 전달.
/// 자식에 Rigidbody 절대 금지 (compound collider 원칙).
/// </summary>
public class HandHitbox : MonoBehaviour
{
    public enum HitZone { Palm, Finger }

    [SerializeField] private HitZone zone;
    public HitZone Zone => zone;

    public void SetZone(HitZone hitZone)
    {
        zone = hitZone;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[HandHitbox] OnCollisionEnter! zone={zone}, other={collision.gameObject.name}, layer={collision.gameObject.layer}");

        if (collision.gameObject.TryGetComponent<Stone>(out var stone))
        {
            var hand = FindFirstObjectByType<HandController>();
            if (hand != null)
            {
                hand.OnStoneHit(stone, zone, collision);
            }
            else
            {
                Debug.LogWarning("[HandHitbox] HandController not found!");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[HandHitbox] OnTriggerEnter! zone={zone}, other={other.gameObject.name}");
    }
}
