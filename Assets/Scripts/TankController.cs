using System;
using UnityEngine;

/// <summary>
/// Clase padre de los dos tanques (PlayerTankController y AITankController). Contiene lo que comparten:
///   - el Rigidbody y la orientación del tanque,
///   - la elevación del cañón (CannonPivot),
///   - el disparo de balas físicas desde el Fire Point,
///   - la conversión entre fuerza (0..1) y velocidad de salida,
///   - la vida: 100 por defecto, recibir daño y avisar cuando cambia o llega a 0
/// Es abstracta: en el Inspector se añade PlayerTankController o AITankController
/// </summary>

// Añadir el Rigidbody al GameObject
[RequireComponent(typeof(Rigidbody))]

public abstract class TankController : MonoBehaviour
{
    // Orientación del morro del tanque
    [Header("Tank")]
    protected Vector3 localForward = Vector3.right.normalized;
    [SerializeField] protected float turnSpeed = 90f;

    // health
    public float maxHealth = 100f;

    // Elevación del cañon
    [Header("Cannon")]
    public float minElevation = 0f;
    public float maxElevation = 60f;
    private Transform cannonPivot;

    // Disparo de balas
    [Header("Disparo")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float minShotSpeed = 6f;
    public float maxShotSpeed = 24f;
    public float bulletLifetime = 10f;

    private Quaternion pivotRestRotation;
    private Vector3 elevationAxis; // en el espacio local del pivote

    protected Rigidbody Body { get; private set; }

    public float currentHealth { get; private set; }
    public bool isDead => currentHealth <= 0f;

    // para alternar los disparos.
    public bool canFire { get; set; } = true;
    public float elevation { get; private set; }

    // Se lanza al disparar, con la bala creada (para cámara, turnos, etc.)
    public event Action<Rigidbody> Fired;

    // Se lanza cada vez que cambia la vida. Lo usa la barra de vida
    public event Action<float, float> HealthChanged;

    // Vida = 0
    public event Action Died;

    // Inicialización común
    protected virtual void Awake()
    {
        Body = GetComponent<Rigidbody>();
        currentHealth = maxHealth;

        Body.useGravity = false;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        // Que las colisiones no lo vuelquen ni lo levanten del suelo
        Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ |
                           RigidbodyConstraints.FreezePositionY;

        // Asociar los transform del cañon y el punto de disparo.
        cannonPivot = this.gameObject.transform.GetChild(1).transform;
        firePoint = this.gameObject.transform.GetChild(1).GetChild(0).transform;

        if (cannonPivot != null)
        {
            pivotRestRotation = cannonPivot.localRotation;
            // Eje lateral del tanque (perpendicular al morro y a la vertical), pasado al espacio del pivote.
            // Así funciona aunque el pivote venga girado, como el CannonPivot de Tank_pref (90º en Y)
            Vector3 sideWorld = transform.TransformDirection(Vector3.Cross(localForward, Vector3.up));
            elevationAxis = cannonPivot.InverseTransformDirection(sideWorld).normalized;
        }
        else
        {
            Debug.LogWarning($"{name}: el cañón no se elevará.");
        }
    }

    // Health Management:
    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (isDead)
        {
            Debug.Log($"{name} ha sido destruido.");
            OnDied();
            Died?.Invoke();
        }
    }

    // Para que el navmesh pare al morir
    protected virtual void OnDied() { }

    // Cannon Elevation:
    public void SetElevation(float degrees)
    {
        elevation = Mathf.Clamp(degrees, minElevation, maxElevation);
        if (cannonPivot != null)
        {
            // La torre (Tower) es hija del pivote, así que rota con él
            cannonPivot.localRotation = pivotRestRotation * Quaternion.AngleAxis(elevation, elevationAxis);
        }
    }

    public float PowerToSpeed(float power01) => Mathf.Lerp(minShotSpeed, maxShotSpeed, Mathf.Clamp01(power01));
    public float SpeedToPower(float speed) => Mathf.InverseLerp(minShotSpeed, maxShotSpeed, speed);

    // Dispara una bala con la fuerza indicada (0..1)
    public Rigidbody Fire(float power01)
    {
        if (isDead || !canFire)
        {
            return null;
        }

        if (firePoint == null || bulletPrefab == null)
        {
            Debug.LogWarning($"{name}: falta asignar Fire Point o Bullet Prefab.");
            return null;
        }

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Rigidbody bulletBody = bullet.GetComponent<Rigidbody>();
        if (bulletBody == null)
        {
            bulletBody = bullet.AddComponent<Rigidbody>();
        }

        bulletBody.isKinematic = false;
        bulletBody.useGravity = true;
        bulletBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Que la bala no choque con el propio tanque al salir del cañón
        Collider bulletCollider = bullet.GetComponent<Collider>();
        if (bulletCollider != null)
        {
            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(bulletCollider, own);
            }
        }

        SetVelocity(bulletBody, firePoint.forward * PowerToSpeed(power01));
        Destroy(bullet, bulletLifetime);
        Fired?.Invoke(bulletBody);
        return bulletBody;
    }

    // Rotación del cañon
    protected Quaternion RotationFacing(Vector3 direction)
    {
        direction.y = 0f;
        return Quaternion.LookRotation(direction.normalized, Vector3.up) *
               Quaternion.Inverse(Quaternion.LookRotation(localForward, Vector3.up));
    }

    protected static void SetVelocity(Rigidbody target, Vector3 velocity)
    {
        target.linearVelocity = velocity;
    }

    protected static Vector3 GetVelocity(Rigidbody target)
    {
        return target.linearVelocity;
    }

    protected static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}