using UnityEngine;

/// <summary>
/// Added automatically to every enemy / boss the room spawner creates (or put it on your prefab yourself).
/// An enemy counts as defeated when you call <see cref="NotifyDefeated"/> from your health script,
/// or automatically when the enemy object is destroyed. Reporting twice is safe.
/// If your enemies are pooled or just disabled on death, call NotifyDefeated() yourself.
/// </summary>
public class LevelEnemy : MonoBehaviour
{
    private LevelObjective objective;
    private bool reported;

    public bool IsBoss { get; private set; }
    public int Generation { get; private set; }

    public void Bind(LevelObjective levelObjective, bool isBoss)
    {
        objective = levelObjective;
        IsBoss = isBoss;
        Generation = levelObjective.Generation;
        reported = false;
    }

    public void NotifyDefeated()
    {
        if (reported || objective == null) return;
        reported = true;
        objective.NotifyDefeated(this);
    }

    private void OnDestroy()
    {
        NotifyDefeated();
    }
}