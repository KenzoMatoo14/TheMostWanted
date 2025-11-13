using UnityEngine;

public interface IDamageable
{
    /// Aplica daño a la entidad
    /// <param name="amount">Cantidad de daño a aplicar</param>
    void TakeDamage(int amount, Vector2 damageSourcePosition = default);

    /// Verifica si la entidad está muerta
    /// <returns>True si está muerta, false si está viva</returns>
    bool IsDead();

    /// Obtiene la vida actual
    /// <returns>Puntos de vida actuales</returns>
    int GetCurrentHealth();

    /// Obtiene la vida máxima
    /// <returns>Puntos de vida máximos</returns>
    int GetMaxHealth();

    /// Obtiene el porcentaje de vida actual (0.0 a 1.0)
    /// <returns>Porcentaje de vida</returns>
    float GetHealthPercentage();
}