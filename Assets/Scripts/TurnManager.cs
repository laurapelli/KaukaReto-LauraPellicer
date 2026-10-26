using System;
using UnityEngine;

/// <summary>
/// Turnos de disparo: el jugador y la IA disparan por turnos alternos
/// Cuando uno dispara, pierde el permiso de disparo (CanFire) y se lo pasa al otro
/// </summary>
public class TurnManager : MonoBehaviour
{
    public PlayerTankController player;
    public AITankController enemy;
    public bool playerStarts = true;

    public bool IsPlayerTurn { get; private set; }

    // Se lanza cada vez que cambia el turno
    public event Action<bool> TurnChanged;

    private void OnEnable()
    {
        if (player != null) player.Fired += OnPlayerFired;
        if (enemy != null) enemy.Fired += OnEnemyFired;
    }

    private void OnDisable()
    {
        if (player != null) player.Fired -= OnPlayerFired;
        if (enemy != null) enemy.Fired -= OnEnemyFired;
    }

    private void Start()
    {
        SetTurn(playerStarts);
    }

    private void OnPlayerFired(Rigidbody bullet) => SetTurn(false);
    private void OnEnemyFired(Rigidbody bullet) => SetTurn(true);

    private void SetTurn(bool playerTurn)
    {
        IsPlayerTurn = playerTurn;
        player.canFire = playerTurn;
        enemy.canFire = !playerTurn;
        TurnChanged?.Invoke(playerTurn);
    }
}