using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private CharacterCombat combat;

    void Awake()
    {
        // Buscar el CharacterCombat en el padre
        combat = GetComponentInParent<CharacterCombat>();

        if (combat == null)
        {
            Debug.LogError("AnimationEventRelay no encontró CharacterCombat en el padre!");
        }
    }

    // Este método será llamado por el Animation Event
    public void ExecuteWhipDamage()
    {
        if (combat != null)
        {
            combat.ExecuteWhipDamage();
        }
    }
}