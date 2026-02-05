using UnityEngine;

/// <summary>
/// Вешается на дочерний объект стола (например, модель ПК). Позволяет чинить комп при наведении на этот объект, а не только на стол.
/// Перетащи сюда тот же ComputerSpot, что и на столе.
/// </summary>
public class ComputerSpotLink : MonoBehaviour
{
    [Tooltip("Ссылка на место за компьютером (стол с ComputerSpot). Перетащи объект со столом сюда.")]
    [SerializeField] ComputerSpot computerSpot;

    public ComputerSpot Spot => computerSpot;
}
