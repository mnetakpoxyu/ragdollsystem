using UnityEngine;

/// <summary>
/// Отключает автоматический подъём персонажа на низкие препятствия (столы, стулья, бордюры).
/// CharacterController по умолчанию «шагает» на объекты до ~0.3 м — из-за этого персонаж залазит на мебель.
/// Вешай на тот же объект, где висит CharacterController (обычно Player).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DisableStepClimb : MonoBehaviour
{
    [Tooltip("Высота «шага» в метрах. 0 = не залазит на столы/стулья. Оставь 0 для ровного пола.")]
    [SerializeField] float stepOffset = 0f;

    void Start()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.stepOffset = stepOffset;
    }
}
