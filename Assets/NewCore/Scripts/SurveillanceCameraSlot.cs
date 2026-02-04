using UnityEngine;

/// <summary>
/// Точка обзора конкретной камеры наблюдения.
/// </summary>
[AddComponentMenu("NewCore/Surveillance/Camera Slot")]
public class SurveillanceCameraSlot : MonoBehaviour
{
    [Tooltip("Название, отображаемое в UI (например «Камера 1»).")]
    public string label = "Камера";

    [Tooltip("Дополнительный текст локации (например «Холл»).")]
    public string locationNote = "";

    [Tooltip("Порядок сортировки, если монитор автоматически собирает слоты.")]
    public int order = 0;

    [Tooltip("Дочерний Transform, указывающий точное положение/поворот точки обзора. Если пусто — используется transform камеры.")]
    public Transform viewPoint;

    [Tooltip("Переопределённый FOV для этой камеры. <= 0 — использовать FOV игрока.")]
    public float overrideFov = 45f;

    void Reset()
    {
        if (viewPoint == null)
            viewPoint = transform;
        if (string.IsNullOrEmpty(label))
            label = gameObject.name;
    }

    /// <summary>
    /// Возвращает точку обзора. Гарантирует, что viewPoint не null.
    /// </summary>
    public Transform GetViewPoint()
    {
        if (viewPoint == null)
            viewPoint = transform;
        return viewPoint;
    }
}
