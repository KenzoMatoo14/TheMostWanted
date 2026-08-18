using UnityEngine;

public class EnemyTutorial : MonoBehaviour
{
    [Header("Tutorial Settings")]
    public GameObject bloqueParaCaer; // Arrastra aquí el GameObject del bloque morado

    private EnemyBase enemyBase;

    private void Awake()
    {
        enemyBase = GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            enemyBase.OnDeath.AddListener(ActivarCaidaBloque);
        }
    }

    private void OnDestroy()
    {
        if (enemyBase != null)
        {
            enemyBase.OnDeath.RemoveListener(ActivarCaidaBloque);
        }
    }

    private void ActivarCaidaBloque()
    {
        if (bloqueParaCaer != null)
        {
            BloqueMovil bloque = bloqueParaCaer.GetComponent<BloqueMovil>();
            if (bloque != null)
            {
                bloque.IniciarCaida();
            }
        }
    }
}
