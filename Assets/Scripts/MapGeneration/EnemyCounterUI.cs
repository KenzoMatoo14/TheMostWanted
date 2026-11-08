using TMPro;
using UnityEngine;

/// <summary>
/// Shows how many enemies are left on maps that require killing them all (normal and elite).
/// On other maps the counter is hidden. Put it on an object that is always active (e.g. the Canvas)
/// and assign the TextMeshPro label.
/// </summary>
public class EnemyCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("{0} = enemies left, {1} = total enemies of the map")]
    [SerializeField] private string format = "Enemies left: {0}";

    [Tooltip("Optional text once every enemy is dead. Leave empty to keep showing the count")]
    [SerializeField] private string completedText = "All enemies defeated! The exit is open";

    [Tooltip("Optional object to show/hide with the counter (e.g. a panel). Defaults to the label itself")]
    [SerializeField] private GameObject container;

    private LevelObjective objective;

    private void Start()
    {
        MapGenerationBase generator = MapGenerationBase.instance;
        if (generator == null)
        {
            Debug.LogWarning("EnemyCounterUI: no map generator in the scene.");
            enabled = false;
            return;
        }

        objective = generator.Objective;
        objective.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (objective != null) objective.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (label == null) return;

        bool show = objective.Type == ObjectiveType.KillAllEnemies;
        GameObject target = container != null ? container : label.gameObject;
        target.SetActive(show);
        if (!show) return;

        if (objective.IsCompleted && !string.IsNullOrEmpty(completedText))
            label.text = completedText;
        else
            label.text = string.Format(format, objective.EnemiesLeft, objective.TotalEnemies);
    }
}