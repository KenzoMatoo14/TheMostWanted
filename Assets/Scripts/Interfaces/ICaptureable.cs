using UnityEngine;

public interface ICaptureable
{
    /// <summary>
    /// Intenta iniciar el proceso de captura
    /// </summary>
    /// <returns>True si se puede iniciar la captura</returns>
    bool StartCapture();

    /// <summary>
    /// Completa el proceso de captura
    /// </summary>
    /// <returns>True si la captura fue exitosa</returns>
    bool CompleteCapture();

    /// <summary>
    /// Cancela el proceso de captura actual
    /// </summary>
    void CancelCapture();

    /// <summary>
    /// Verifica si la entidad puede ser capturada actualmente
    /// </summary>
    /// <returns>True si puede ser capturada</returns>
    bool CanBeCaptured();

    /// <summary>
    /// Verifica si la entidad está siendo capturada en este momento
    /// </summary>
    /// <returns>True si está en proceso de captura</returns>
    bool IsBeingCaptured();

    /// <summary>
    /// Verifica si la entidad ya fue capturada
    /// </summary>
    /// <returns>True si ya está capturada</returns>
    bool IsCaptured();

    /// <summary>
    /// Obtiene el progreso inicial de captura basado en el stun actual (0-1)
    /// Un enemigo más aturdido empieza con más progreso
    /// </summary>
    /// <returns>Progreso inicial (0-1), donde 1 es captura instantánea</returns>
    float GetCaptureStartProgress();

    /// <summary>
    /// Obtiene la dificultad de captura base (0.0 = imposible, 1.0 = muy fácil)
    /// </summary>
    /// <returns>Dificultad de captura</returns>
    float GetCaptureDifficulty();

    /// <summary>
    /// Obtiene el multiplicador de velocidad de captura basado en condiciones actuales
    /// Tiene en cuenta stun, vida, y otros factores
    /// </summary>
    /// <returns>Multiplicador de velocidad (1.0 = normal, >1.0 = más rápido)</returns>
    float GetCaptureSpeedMultiplier();

    bool Release(Vector2 releaseVelocity = default);
}