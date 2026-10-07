using UnityEngine;

/// <summary>
/// El Animator del jugador vive en el GameObject hijo "sprite", separado de
/// CharacterCombat (que está en el padre). Los AnimationEvent de Unity solo
/// mandan el mensaje al GameObject que tiene el Animator, nunca al padre, así
/// que este componente vive en "sprite" y reenvía el evento hacia arriba.
/// También va en "WhipPivot": los eventos del clip WhipSlash (hitbox, ventana
/// de cancelación, fin) se reenvían a WhipAttack en el root.
/// </summary>
public class PlayerAnimationEventRelay : MonoBehaviour
{
    private CharacterCombat combat;
    private WhipAttack whipAttack;

    private void Awake()
    {
        combat = GetComponentInParent<CharacterCombat>();
        whipAttack = GetComponentInParent<WhipAttack>();
    }

    public void ExecuteWhipDamage()
    {
        if (combat != null)
        {
            combat.ExecuteWhipDamage();
        }
    }

    public void WhipHitboxOn()
    {
        if (whipAttack != null) whipAttack.OnWhipHitboxOn();
    }

    public void WhipHitboxOff()
    {
        if (whipAttack != null) whipAttack.OnWhipHitboxOff();
    }

    public void WhipOpenCancel()
    {
        if (whipAttack != null) whipAttack.OnWhipOpenCancel();
    }

    public void WhipEnd()
    {
        if (whipAttack != null) whipAttack.OnWhipEnd();
    }
}
