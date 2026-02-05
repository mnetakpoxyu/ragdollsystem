using UnityEngine;

/// <summary>
/// Запас напитков (газировка) в холодильнике. Вешать на объект с коллайдером (банки/бутылки).
/// Игрок наводится, видит «Газировка [E] взять (в запасе: N)», по E забирает одну, если не держит уже и есть запас.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DrinkStock : MonoBehaviour
{
    [Header("Запас")]
    [Tooltip("Начальное количество напитков в запасе.")]
    [SerializeField] int initialStock = 10;

    int _stock;

    void Awake()
    {
        _stock = Mathf.Max(0, initialStock);
    }

    public int Stock => _stock;

    /// <summary> Взять один напиток. Возвращает true, если взял (был запас и игрок не держал уже). </summary>
    public bool TryTakeDrink(bool playerAlreadyHolding)
    {
        if (playerAlreadyHolding || _stock <= 0) return false;
        _stock--;
        return true;
    }
}
