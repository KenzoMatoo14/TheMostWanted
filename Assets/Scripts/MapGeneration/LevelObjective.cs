using System;
using UnityEngine;

public enum ObjectiveType
{
    None,            // the exit is unlocked from the start (platformer)
    KillAllEnemies,  // normal and elite maps
    DefeatBoss       // boss maps
}

/// <summary>
/// Tracks the end condition of the current map and unlocks the exit when it is met.
/// One instance lives in the map generator (MapGenerationBase.Objective) and is reused when a new map is generated.
/// Enemies and the exit register themselves while the rooms are spawned; <see cref="Seal"/> is called once everything exists.
/// </summary>
public class LevelObjective
{
    public ObjectiveType Type { get; private set; }

    /// <summary>Increases with every new map, so enemies of an old map that get destroyed late are ignored.</summary>
    public int Generation { get; private set; }

    /// <summary>Normal and elite enemies that exist in the map (the boss is counted apart).</summary>
    public int TotalEnemies { get; private set; }
    public int DefeatedEnemies { get; private set; }
    public int EnemiesLeft => Mathf.Max(0, TotalEnemies - DefeatedEnemies);

    public bool BossRegistered { get; private set; }
    public bool BossDefeated { get; private set; }
    public bool IsCompleted { get; private set; }

    /// <summary>Raised when the map starts and whenever the counts change.</summary>
    public event Action Changed;
    /// <summary>Raised once, when the condition is met (the exit is unlocked just before).</summary>
    public event Action Completed;

    private LevelExit exit;
    private bool isSealed;

    public void Begin(ObjectiveType type)
    {
        Generation++;
        Type = type;
        TotalEnemies = 0;
        DefeatedEnemies = 0;
        BossRegistered = false;
        BossDefeated = false;
        IsCompleted = false;
        isSealed = false;
        exit = null;
        Changed?.Invoke();
    }

    public void RegisterEnemy(LevelEnemy enemy)
    {
        if (enemy.IsBoss) BossRegistered = true;
        else TotalEnemies++;
    }

    public void RegisterExit(LevelExit levelExit)
    {
        exit = levelExit;
        exit.SetLocked(true);
    }

    /// <summary>Everything has been spawned: the counts are final, so the condition can be checked.</summary>
    public void Seal()
    {
        isSealed = true;

        if (Type == ObjectiveType.DefeatBoss && !BossRegistered)
            Debug.LogWarning("LevelObjective: this is a boss map but no boss was spawned (is the boss prefab assigned in the RoomManager's spawn settings?). Unlocking the exit.");
        if (Type == ObjectiveType.KillAllEnemies && TotalEnemies == 0)
            Debug.LogWarning("LevelObjective: this map requires killing all enemies but none were spawned (are the enemy prefabs assigned?). Unlocking the exit.");

        Changed?.Invoke();
        Evaluate();
    }

    public void NotifyDefeated(LevelEnemy enemy)
    {
        if (enemy.Generation != Generation) return;   // belongs to a previous map

        if (enemy.IsBoss) BossDefeated = true;
        else DefeatedEnemies++;

        Changed?.Invoke();
        Evaluate();
    }

    private void Evaluate()
    {
        if (!isSealed || IsCompleted) return;

        bool met;
        switch (Type)
        {
            case ObjectiveType.KillAllEnemies: met = DefeatedEnemies >= TotalEnemies; break;
            case ObjectiveType.DefeatBoss: met = BossDefeated || !BossRegistered; break;
            default: met = true; break;
        }
        if (!met) return;

        IsCompleted = true;
        if (exit != null) exit.SetLocked(false);
        Completed?.Invoke();
        Changed?.Invoke();
    }
}