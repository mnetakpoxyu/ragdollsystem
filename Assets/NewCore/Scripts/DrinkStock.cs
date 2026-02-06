using UnityEngine;

/// <summary>
/// Запас напитков (стаканчики) на полке. В инспекторе перетаскиваешь массив из 10 стаканчиков — при взятии один уходит в руку и с полки пропадает.
/// При сдаче клиенту стаканчик просто скрывается; можно положить обратно на ту же полку по E.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DrinkStock : MonoBehaviour
{
    [Header("Запас")]
    [Tooltip("Начальное количество напитков в запасе (должно совпадать с размером массива ниже).")]
    [SerializeField] int initialStock = 10;

    [Header("Стаканчики на полке")]
    [Tooltip("Перетащи сюда все 10 стаканчиков (дочерние объекты полки). По одному будут уходить в руку при взятии; при сдаче клиенту объект скрывается; можно положить обратно по E.")]
    [SerializeField] Transform[] drinkVisuals = new Transform[0];

    int _stock;
    int _nextVisualIndex;

    void Awake()
    {
        _stock = Mathf.Max(0, initialStock);
    }

    public int Stock => _stock;

    /// <summary> Взять один напиток. Возвращает true и выдает visualForHand (объект с полки), если был запас. Если массив стаканчиков пуст — visualForHand = null (используется визуал с PlayerCarry). </summary>
    public bool TryTakeDrink(bool playerAlreadyHolding, out Transform visualForHand)
    {
        visualForHand = null;
        if (playerAlreadyHolding || _stock <= 0) return false;
        if (drinkVisuals == null || drinkVisuals.Length == 0)
        {
            _stock--;
            return true;
        }
        for (int i = _nextVisualIndex; i < drinkVisuals.Length; i++)
        {
            Transform t = drinkVisuals[i];
            if (t == null) continue;
            if (!t.IsChildOf(transform))
                continue;
            _stock--;
            _nextVisualIndex = i + 1;
            visualForHand = t;
            return true;
        }
        return false;
    }

    /// <summary> Игрок положил стаканчик обратно на полку — увеличиваем запас и «освобождаем» слот. </summary>
    public void ReturnDrink()
    {
        _stock = Mathf.Min(_stock + 1, drinkVisuals != null ? drinkVisuals.Length : initialStock);
        _nextVisualIndex = Mathf.Max(0, _nextVisualIndex - 1);
    }

    /// <summary> Для будущего пополнения. </summary>
    public void Restock(int count)
    {
        _stock = Mathf.Clamp(_stock + count, 0, drinkVisuals != null ? drinkVisuals.Length : 0);
        _nextVisualIndex = Mathf.Min(_nextVisualIndex, drinkVisuals != null ? drinkVisuals.Length : 0);
    }

    /// <summary> Сбросить полку в начальное состояние. Объекты в массиве нужно самому включить и поставить на полку. </summary>
    public void ResetStockToFull()
    {
        _stock = initialStock;
        _nextVisualIndex = 0;
    }
}
