using UnityEngine;

public interface IStunnable
{
    /// <summary>
    /// Añade una cantidad de aturdimiento (0-100)
    /// </summary>
    /// <param name="amount">Cantidad de stun a añadir</param>
    void AddStunned(float amount);

    /// <summary>
    /// Reduce una cantidad de aturdimiento
    /// </summary>
    /// <param name="amount">Cantidad de stun a reducir</param>
    void ReduceStunned(float amount);

    /// <summary>
    /// Establece el nivel de aturdimiento a un valor específico
    /// </summary>
    /// <param name="value">Valor de stun (0-100)</param>
    void SetStunned(float value);

    /// <summary>
    /// Limpia completamente el efecto de aturdimiento
    /// </summary>
    void ClearStunned();

    /// <summary>
    /// Verifica si la entidad tiene algún nivel de aturdimiento
    /// </summary>
    /// <returns>True si tiene stun mayor a 0</returns>
    bool IsStunned();

    /// <summary>
    /// Verifica si la entidad está completamente aturdida (por encima del umbral)
    /// </summary>
    /// <returns>True si el stun está por encima del umbral de detención completa</returns>
    bool IsFullyStunned();

    /// <summary>
    /// Obtiene el valor actual de aturdimiento (0-100)
    /// </summary>
    /// <returns>Valor actual de stun</returns>
    float GetCurrentStunned();

    /// <summary>
    /// Obtiene el porcentaje de aturdimiento (0-1)
    /// </summary>
    /// <returns>Porcentaje normalizado del stun</returns>
    float GetStunnedPercentage();

    /// <summary>
    /// Obtiene el multiplicador de velocidad de movimiento basado en el stun actual (0-1)
    /// </summary>
    /// <returns>Multiplicador de velocidad (0 = detenido, 1 = velocidad normal)</returns>
    float GetMovementSpeedMultiplier();
}