using UnityEngine;

/// <summary>
/// El Animator del jugador vive en el GameObject hijo "sprite", separado de
/// CharacterCombat (que está en el padre). Los AnimationEvent de Unity solo
/// mandan el mensaje al GameObject que tiene el Animator, nunca al padre, así
/// que este componente vive en "sprite" y reenvía el evento hacia arriba.
/// </summary>
public class PlayerAnimationEventRelay : MonoBehaviour
{
    private CharacterCombat combat;

    private void Awake()
    {
        combat = GetComponentInParent<CharacterCombat>();
    }

    public void ExecuteWhipDamage()
    {
        if (combat != null)
        {
            combat.ExecuteWhipDamage();
        }
    }
}
