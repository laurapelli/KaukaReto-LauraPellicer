using UnityEngine;

/// <summary>
/// Va en el prefab de la bala. En cuanto choca con cualquier cosa (suelo, rocas, paredes o un tanque)
/// la bala desaparece. Si lo que toca es un tanque, le quita siempre la misma vida.
/// Si no choca con nada, TankController la destruye al acabar su tiempo de vida.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BulletController : MonoBehaviour
{
    [SerializeField] private float damage = 25f;

    private bool hasHit;

    private void OnCollisionEnter(Collision collision)
    {
        // Evita procesar dos choques si toca dos colliders en el mismo paso de física.
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        // El collider puede estar en un hijo del tanque: se busca el TankController hacia arriba.
        TankController tank = collision.collider.GetComponentInParent<TankController>();
        if (tank != null)
        {
            tank.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}