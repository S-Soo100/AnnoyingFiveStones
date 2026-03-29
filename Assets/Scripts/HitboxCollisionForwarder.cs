using UnityEngine;

/// <summary>
/// HitboxRoot에 부착. Compound collider의 OnCollisionEnter는
/// Rigidbody가 있는 부모(HitboxRoot)에서 발생하므로,
/// 충돌한 자식 Collider의 HandHitbox를 찾아 HandController에 전달.
/// </summary>
public class HitboxCollisionForwarder : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.TryGetComponent<Stone>(out var stone)) return;

        // 충돌한 자식 Collider 확인
        var hitCollider = collision.contacts[0].thisCollider;
        var hitbox = hitCollider.GetComponent<HandHitbox>();

        if (hitbox == null)
        {
            Debug.Log($"[HitboxForwarder] Collision with stone {stone.StoneIndex} but no HandHitbox on {hitCollider.name}");
            return;
        }

        var hand = FindFirstObjectByType<HandController>();
        if (hand != null)
        {
            hand.OnStoneHit(stone, hitbox.Zone, collision);
        }
    }
}
