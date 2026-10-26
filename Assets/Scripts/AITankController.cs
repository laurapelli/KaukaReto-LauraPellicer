using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tanque de la IA:
///   - Cada Retarget Interval segundos vuelve a mirar dónde está el jugador y hacia dónde se mueve
///   -  Le persigue siempre, buscando quedarse a Keep Distance de él: si está lejos se acerca,
///     si está demasiado cerca retrocede. Si se atasca, prueba a rodearle
///   - Dispara siempre a máxima potencia, en tiro tenso: calcula la elevación baja que alcanza
///     al jugador a esa velocidad. La bala llega rápido y es difícil de esquivar.
///   - Dispara en cuanto está alineado y ha pasado el tiempo de recarga, apuntando a donde
///     el jugador ESTARÁ cuando llegue la bala.
///   - Mira dónde cae cada bala y corrige la elevación y la dirección del siguiente disparo
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AITankController : TankController
{
    [Header("Objetivo")]
    public GameObject player;

    [Header("Seguimiento")]
    public float retargetInterval = 0.25f;
    public float keepDistance = 12f;
    public float maxFireDistance = 25f;
    public float stuckTime = 1f;

    [Header("Disparo")]
    public float elevationSpeed = 90f;
    public float fireCooldown = 1f;
    public float fireAngleTolerance = 1.5f;

    [Header("Puntería (dificultad)")]
    [Range(0f, 1f)] public float leadFactor = 1f;
    public float directionError = 0.75f;
    public float powerError = 0.015f;
    [Range(0f, 1f)] public float learningRate = 1f;
    [Range(0f, 1f)] public float directionLearningRate = 0.8f;

    private NavMeshAgent agent;
    private Rigidbody playerBody;

    private float nextRetargetTime;
    private float nextFireTime;
    private float stuckTimer;
    private float flankAngle;          // si se atasca, prueba a colocarse a un lado del jugador
    private Vector3 aimPoint;          // donde apunta ahora mismo (se refresca cada retargetInterval)
    private float aimElevation;        // elevación calculada para alcanzar aimPoint a máxima potencia

    // Aprendizaje: correcciones de elevación y de dirección que se ajustan con cada bala
    private float elevationBias;
    private float yawBias;

    // Seguimiento de la última bala para ver dónde cae
    private Rigidbody trackedBullet;
    private bool tracking;
    private Vector3 bulletLastPosition;
    private Vector3 shotOrigin;
    private Vector3 shotTarget;
    private float shotSpeed;
    private float shotElevation;

    private float Gravity => Mathf.Max(0.1f, -Physics.gravity.y);

    // posición actual del jugador: se lee de su Transform cada vez, así siempre está al día
    private Vector3 playerPosition => player.transform.position;

    protected override void OnDied()
    {
        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;   // muerto: se queda quieto
        }
    }

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;   // le dice al NavMeshAgent que no gire el tanque él solo
        agent.stoppingDistance = 0.3f;
        Body.isKinematic = true;        // lo mueve el NavMeshAgent
    }

    private void Start()
    {
        if (player != null)
        {
            playerBody = player.GetComponentInParent<Rigidbody>();
            aimPoint = playerPosition;
        }

        nextFireTime = Time.time + fireCooldown;
    }

    private void Update()
    {
        if (player == null || isDead)
        {
            return;
        }

        // cada 0,25 s llama a Retarget()
        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + retargetInterval;
            Retarget();
        }

        //  Mantiene una distancia del player
        float distance = FlatDistance(transform.position, playerPosition);

        // La mira apunta hacia donde avanza y encara al jugador.
        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        bool moving = velocity.sqrMagnitude > 0.25f;
        bool backingOff = distance < keepDistance - 1f;
        if (moving && !backingOff)
        {
            FaceDirection(velocity);
        }
        else
        {
            // Retrocediendo o parado
            FaceDirection(aimPoint - transform.position);
        }

        // elevación del cañon calculada 
        if (Mathf.Abs(elevation - aimElevation) > 0.1f)
        {
            SetElevation(Mathf.MoveTowards(elevation, aimElevation, elevationSpeed * Time.deltaTime));
        }

        // Disparar en cuanto pueda
        bool reloaded = Time.time >= nextFireTime;
        bool inRange = distance <= maxFireDistance;
        bool aligned = Mathf.Abs(AngleTo(aimPoint)) <= fireAngleTolerance;
        bool cannonReady = Mathf.Abs(elevation - Mathf.Clamp(aimElevation, minElevation, maxElevation)) <= 0.5f;
        if (canFire && reloaded && inRange && aligned && cannonReady)
        {
            Retarget();   // calcula la distancia justo antes de disparar
            FireAtAimPoint();
            nextFireTime = Time.time + fireCooldown;
        }
    }

    private void FixedUpdate()
    {
        TrackBullet();
    }

    // Decisiones de movimiento y disparo

    private void Retarget()
    {
        agent.isStopped = false;
        agent.SetDestination(KeepDistancePoint());

        // Predicción: dónde estará el jugador cuando llegue la bala
        // Se repite dos veces porque el tiempo de vuelo depende de la distancia, y la distancia del punto predicho
        Vector3 target = playerPosition;
        if (playerBody != null && leadFactor > 0f)
        {
            Vector3 playerVelocity = GetVelocity(playerBody);
            playerVelocity.y = 0f;
            for (int i = 0; i < 2; i++)
            {
                float flightTime = EstimateFlightTime(FlatDistance(firePoint.position, target));
                target = playerPosition + playerVelocity * (flightTime * leadFactor);
            }
        }

        aimPoint = target;
        aimElevation = ElevationFor(FlatDistance(firePoint.position, aimPoint));
    }

    // Calcula el punto que está a 12 m de ti,
    // Si se había atascado, gira ese punto 60° para rodearte por un lado
    private Vector3 KeepDistancePoint()
    {
        Vector3 fromPlayer = transform.position - playerPosition;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude < 0.01f)
        {
            fromPlayer = -player.transform.forward;
        }

        Vector3 direction = Quaternion.Euler(0f, flankAngle, 0f) * fromPlayer.normalized;
        Vector3 desired = playerPosition + direction * keepDistance;

        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return desired;
    }

    private float EstimateFlightTime(float distance)
    {
        float elevationRad = ElevationFor(distance) * Mathf.Deg2Rad;
        float horizontalSpeed = Mathf.Max(0.5f, maxShotSpeed * Mathf.Cos(elevationRad));
        return distance / horizontalSpeed;
    }

    // Elevación (grados) para alcanzar una distancia disparando a máxima potencia
    // Tiro parabólico en llano: alcance = v² · sin(2θ) / g  →  θ = ½ · asin(g · d / v²)
    private float ElevationFor(float distance)
    {
        float ratio = Gravity * distance / (maxShotSpeed * maxShotSpeed);
        float elevationDeg = ratio >= 1f ? 45f : 0.5f * Mathf.Asin(ratio) * Mathf.Rad2Deg;
        return elevationDeg + elevationBias;
    }

    // Disparo

    private void FireAtAimPoint()
    {
        // Siempre a máxima potencia
        float power = 1f - Random.Range(0f, powerError);
        float yawOffset = yawBias + Random.Range(-directionError, directionError);

        transform.Rotate(0f, yawOffset, 0f, Space.World);
        Vector3 origin = firePoint.position;
        Rigidbody bullet = Fire(power);
        transform.Rotate(0f, -yawOffset, 0f, Space.World);

        if (bullet != null)
        {
            trackedBullet = bullet;
            tracking = true;
            bulletLastPosition = bullet.position;
            shotOrigin = origin;
            shotTarget = aimPoint;
            shotSpeed = PowerToSpeed(power);
            shotElevation = elevation;
        }
    }

    // Sigue la bala y aprende de la distancia que alcanzó
    private void TrackBullet()
    {
        if (!tracking)
        {
            return;
        }

        // última posición es el punto de impacto
        if (trackedBullet == null)
        {
            tracking = false;
            Learn(bulletLastPosition);
            return;
        }

        Vector3 position = trackedBullet.position;
        bool falling = position.y < bulletLastPosition.y;
        bool landed = falling && position.y <= playerPosition.y + 0.2f;
        bulletLastPosition = position;

        if (landed)
        {
            tracking = false;
            Learn(position);
        }
    }

    // Aprendizaje: corrige la elevación y la dirección según dónde cayó la bala
    private void Learn(Vector3 landing)
    {
        float range = FlatDistance(shotOrigin, landing);
        if (range < 0.5f)
        {
            return;
        }

        // Si chocó con una roca antes de llegar, esa distancia no sirve para aprender
        bool blockedHigh = landing.y > playerPosition.y + 1.5f &&
                           range < FlatDistance(shotOrigin, shotTarget) - 1f;
        if (blockedHigh)
        {
            return;
        }

        // Dirección = diferencia entre el rumbo al que cayó la bala y el rumbo al que apuntaba
        Vector3 toTarget = shotTarget - shotOrigin;
        Vector3 toLanding = landing - shotOrigin;
        float bearingError = Mathf.DeltaAngle(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg,
                                              Mathf.Atan2(toLanding.x, toLanding.z) * Mathf.Rad2Deg);
        yawBias = Mathf.Clamp(yawBias - directionLearningRate * bearingError, -10f, 10f);

        // Alcance: si se quedó corta sube la elevación, si se pasó la baja
        // Cuánto cambia el alcance por cada grado de elevación: dR/dθ = 2·v²·cos(2θ) / g
        float targetRange = FlatDistance(shotOrigin, shotTarget);
        float rangeError = range - targetRange;
        float metersPerRadian = 2f * shotSpeed * shotSpeed * Mathf.Cos(2f * shotElevation * Mathf.Deg2Rad) / Gravity;
        if (metersPerRadian > 0.1f)
        {
            float correctionDeg = rangeError / metersPerRadian * Mathf.Rad2Deg;
            elevationBias = Mathf.Clamp(elevationBias - learningRate * correctionDeg, -10f, 10f);
        }
    }

    // Gira el morro hacia una dirección y devuelve los grados que aún faltan
    private float FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        Quaternion desired = RotationFacing(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, desired);
    }

    // Grados entre el morro actual y un punto
    private float AngleTo(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ? 0f : Quaternion.Angle(transform.rotation, RotationFacing(direction));
    }
}