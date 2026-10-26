using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///   - Panel de inicio con botón Start: hasta pulsarlo, ningún tanque se mueve ni dispara
///   - Barra de vida del jugador (azul, derecha) y del enemigo (roja, izquierda)
///   - Barra de fuerza mientras el jugador carga el disparo
///   - Indicador de turno: texto "You" / "Computer" sobre una imagen roja / azul
///   - Menú de pausa (Esc) con Resume, Retry y Exit
///   - Panel de fin de partida (WIN / LOSE) con botones para volver a jugar y para salir
/// </summary>
public class CanvasController : MonoBehaviour
{
    [Header("Tanques (desde la Hierarchy)")]
    public PlayerTankController player;
    public AITankController enemy;

    [Header("Barras de vida")]
    public RectTransform playerHealthFill;
    public TMP_Text playerHealthText;
    public RectTransform enemyHealthFill;
    public TMP_Text enemyHealthText;
    public float healthSmoothSpeed = 2f;

    [Header("Barra de fuerza")]
    public GameObject powerBar;
    public RectTransform powerFill;
    public TMP_Text powerText;
    public Vector2 powerBarPosition = new Vector2(20f, 26f);
    public Vector2 powerBarSize = new Vector2(300f, 24f);

    [Header("Turno")]
    public TurnManager turnManager;
    public TMP_Text turnText;
    public Image turnBackground;
    public string playerTurnMessage = "You";
    public string enemyTurnMessage = "Computer";
    public Color playerTurnColor = new Color(0.85f, 0.15f, 0.15f);   // rojo
    public Color enemyTurnColor = new Color(0.15f, 0.35f, 0.9f);     // azul
    [Header("Inicio")]
    public GameObject startPanel;
    public Button startButton;

    [Header("Pausa")]
    public GameObject pausePanel;
    public Button resumeButton;
    public Button restartButton;
    public Button pauseExitButton;

    [Header("Fin de partida")]
    public GameObject gameOverPanel;
    public TMP_Text resultText;
    public Button tryAgainButton;
    public Button gameOverExitButton;
    private string winMessage = "¡WIN!";
    private string loseMessage = "LOSE";

    private float playerShown = 1f;
    private float playerTarget = 1f;
    private float enemyShown = 1f;
    private float enemyTarget = 1f;
    private bool gameOver;
    private bool gameStarted;
    private bool paused;

    // Static: sobrevive a la recarga de escena. Al pulsar "Try Again" no se vuelve a mostrar el inicio.
    private static bool skipStartPanel;

    private void Awake()
    {
        // Por si se recargó la escena estando en pausa: el tiempo vuelve a correr.
        Time.timeScale = 1f;

        // Antes de nada, se congela la partida: nadie se mueve hasta pulsar Start.
        LockTanks(true);
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.HealthChanged += OnPlayerHealthChanged;
            player.Died += OnPlayerDied;
        }

        if (enemy != null)
        {
            enemy.HealthChanged += OnEnemyHealthChanged;
            enemy.Died += OnEnemyDied;
        }

        if (turnManager != null)
        {
            turnManager.TurnChanged += OnTurnChanged;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.HealthChanged -= OnPlayerHealthChanged;
            player.Died -= OnPlayerDied;
        }

        if (enemy != null)
        {
            enemy.HealthChanged -= OnEnemyHealthChanged;
            enemy.Died -= OnEnemyDied;
        }

        if (turnManager != null)
        {
            turnManager.TurnChanged -= OnTurnChanged;
        }
    }

    private void Start()
    {

        SetActive(gameOverPanel, false);
        SetActive(pausePanel, false);
        SetActive(powerBar, false);

        // Botones: cada uno llama a su método.
        if (tryAgainButton != null) tryAgainButton.onClick.AddListener(Restart);
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (pauseExitButton != null) pauseExitButton.onClick.AddListener(ExitGame);
        if (gameOverExitButton != null) gameOverExitButton.onClick.AddListener(ExitGame);

        if (skipStartPanel || startPanel == null)
        {
            // Revancha (o no hay panel de inicio): se empieza directamente.
            StartGame();
        }
        else
        {
            SetActive(startPanel, true);
        }

        if (player != null) SetText(playerHealthText, player.currentHealth, player.maxHealth);
        if (enemy != null) SetText(enemyHealthText, enemy.currentHealth, enemy.maxHealth);
    }

    private void Update()
    {
        // Esc abre o cierra la pausa (solo durante la partida, no en el inicio ni en el final).
        if (Input.GetKeyDown(KeyCode.Escape) && gameStarted && !gameOver)
        {
            if (paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        // Barras de vida: bajan suavemente hasta su valor
        playerShown = Approach(playerShown, playerTarget);
        enemyShown = Approach(enemyShown, enemyTarget);
        SetFill(playerHealthFill, playerShown);
        SetFill(enemyHealthFill, enemyShown);

        // Barra de fuerza: solo mientras el jugador mantiene Espacio
        bool charging = player != null && player.charging && gameStarted && !gameOver && !paused;
        SetActive(powerBar, charging);
        if (charging)
        {
            SetFill(powerFill, player.power);
            if (powerText != null)
            {
                powerText.text = $"{player.power * 100f:0}%";
            }
        }
    }

    // Tanks
    private void OnPlayerHealthChanged(float current, float max)
    {
        playerTarget = max > 0f ? current / max : 0f;
        SetText(playerHealthText, current, max);
    }

    private void OnEnemyHealthChanged(float current, float max)
    {
        enemyTarget = max > 0f ? current / max : 0f;
        SetText(enemyHealthText, current, max);
    }

    private void OnTurnChanged(bool playerTurn)
    {
        if (gameOver)
        {
            return;
        }

        if (turnText != null)
        {
            turnText.text = playerTurn ? playerTurnMessage : enemyTurnMessage;
        }

        if (turnBackground != null)
        {
            turnBackground.color = playerTurn ? playerTurnColor : enemyTurnColor;
        }
    }

    private void OnPlayerDied() => ShowGameOver(false);
    private void OnEnemyDied() => ShowGameOver(true);

    private void ShowGameOver(bool playerWon)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        SetActive(gameOverPanel, true);
        // Al acabar la partida se oculta el indicador de turno
        if (turnText != null)
        {
            turnText.text = string.Empty;
        }

        if (turnBackground != null)
        {
            turnBackground.gameObject.SetActive(false);
        }
        if (resultText != null)
        {
            resultText.text = playerWon ? winMessage : loseMessage;
        }

        // Se acabó: el jugador deja de controlar su tanque y la IA deja de actuar
        if (player != null) player.InputEnabled = false;
        if (enemy != null) enemy.enabled = false;
    }


    // Lo llama el botón Start: oculta el panel de inicio y deja jugar a los dos tanques.
    public void StartGame()
    {
        if (gameStarted)
        {
            return;
        }

        gameStarted = true;
        SetActive(startPanel, false);
        LockTanks(false);
    }

    // Pausa: congela el tiempo (física, balas, movimiento) y bloquea a los dos tanques.
    public void Pause()
    {
        if (paused || gameOver)
        {
            return;
        }

        paused = true;
        Time.timeScale = 0f;
        LockTanks(true);
        SetActive(pausePanel, true);
    }

    // Lo llama el botón Resume (o Esc otra vez): todo sigue donde se quedó.
    public void Resume()
    {
        if (!paused)
        {
            return;
        }

        paused = false;
        Time.timeScale = 1f;
        LockTanks(false);
        SetActive(pausePanel, false);
    }

    // Bloquea o desbloquea a los dos tanques (el teclado del jugador y toda la IA)
    private void LockTanks(bool locked)
    {
        if (player != null) player.InputEnabled = !locked;
        if (enemy != null) enemy.enabled = !locked;
    }

    // Lo llaman "Try Again" (fin de partida) y "Restart" (pausa)
    public void Restart()
    {
        Time.timeScale = 1f;   // si venimos de la pausa, el tiempo estaba parado
        skipStartPanel = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Lo llaman los botones Exit
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private float Approach(float shown, float target)
    {
        return healthSmoothSpeed > 0f ? Mathf.MoveTowards(shown, target, healthSmoothSpeed * Time.deltaTime) : target;
    }

    private static void SetFill(RectTransform fill, float fraction)
    {
        if (fill != null)
        {
            fill.localScale = new Vector3(Mathf.Clamp01(fraction), 1f, 1f);
        }
    }

    private static void SetText(TMP_Text label, float current, float max)
    {
        if (label != null)
        {
            label.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}