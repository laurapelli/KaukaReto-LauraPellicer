using UnityEngine;

/// <summary>
/// Tanque del jugador:
///   W / S         avanzar / retroceder
///   A / D         girar el tanque
///   Flecha ↑ / ↓  subir / bajar el cañón
///   Espacio       mantener para cargar la fuerza, soltar para disparar
/// </summary>

public class PlayerTankController : TankController
{
    [Header("Movement")]
    public float moveSpeed = 4f;

    [Header("Aiming and Shooting")]
    public float elevationSpeed = 30f;
    public float chargeSpeed = 0.6f;
    public bool showPowerBar = true;

    private float moveInput;
    private float turnInput;
    public bool charging;
    private float chargeTime;

    // TurnManager puede desactivar la entrada del jugador mientras no sea su turno, o si está muerto.
    public bool InputEnabled { get; set; } = true;

    // Fuerza actual de 0 a 1 (mientras se carga, sube y baja).
    public float power { get; private set; }

    // Teclado
    private static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

    protected override void Awake()
    {
        base.Awake();
        Body.isKinematic = false;   // un Rigidbody cinemático no se detendría al chocar
        Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Update()
    {
        moveInput = 0f;
        turnInput = 0f;
        if (!InputEnabled || isDead)
        {
            charging = false;
            return;
        }

        moveInput = Axis(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.S));
        turnInput = Axis(Input.GetKey(KeyCode.D), Input.GetKey(KeyCode.A));

        float elevationInput = Axis(Input.GetKey(KeyCode.UpArrow), Input.GetKey(KeyCode.DownArrow));
        if (elevationInput != 0f)
        {
            SetElevation(elevation + elevationInput * elevationSpeed * Time.deltaTime);
        }

        UpdateCharging();
    }

    private void UpdateCharging()
    {
        // Turno de disparar
        if (!canFire)
        {
            charging = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            charging = true;
            chargeTime = 0f;
        }

        if (!charging)
        {
            return;
        }

        // La fuerza sube de 0 a 1 y vuelve a bajar mientras se mantiene pulsado
        chargeTime += Time.deltaTime * chargeSpeed;
        power = Mathf.PingPong(chargeTime, 1f);

        if (Input.GetKeyUp(KeyCode.Space))
        {
            charging = false;
            Fire(power);
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (turnInput != 0f)
        {
            Body.MoveRotation(Body.rotation * Quaternion.Euler(0f, turnInput * turnSpeed * dt, 0f));
        }

        // Velocidad de avance
        float speed = moveInput * moveSpeed;

        Vector3 forward = Body.rotation * localForward;
        forward.y = 0f;
        SetVelocity(Body, forward.normalized * speed);
        Body.angularVelocity = Vector3.zero;
    }
}