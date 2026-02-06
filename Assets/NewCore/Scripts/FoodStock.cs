using UnityEngine;

/// <summary>
/// Запас бургеров на полке. В инспекторе перетаскиваешь массив из 10 бургеров (дочерние объекты) — при взятии один уходит в руку и с полки пропадает.
/// При сдаче клиенту бургер просто скрывается (не удаляется), чтобы потом можно было пополнять полки.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FoodStock : MonoBehaviour
{
    [Header("Запас")]
    [Tooltip("Начальное количество бургеров в запасе (должно совпадать с размером массива ниже).")]
    [SerializeField] int initialStock = 10;

    [Header("Бургеры на полке")]
    [Tooltip("Перетащи сюда все 10 бургеров (дочерние объекты полки). По одному будут уходить в руку при взятии; при сдаче клиенту объект просто скрывается для будущего пополнения.")]
    [SerializeField] Transform[] burgerVisuals = new Transform[0];

    int _stock;
    int _nextVisualIndex;

    void Awake()
    {
        _stock = Mathf.Max(0, initialStock);
    }

    public int Stock => _stock;

    /// <summary> Взять один бургер. Возвращает true и выдает visualForHand (объект с полки для переноса в руку), если был запас. Выдаём только объект, который ещё на полке (не дубликат и не уже взятый). </summary>
    public bool TryTakeBurger(bool playerAlreadyHolding, out Transform visualForHand)
    {
        visualForHand = null;
        if (playerAlreadyHolding || _stock <= 0) return false;
        if (burgerVisuals == null) return false;

        for (int i = _nextVisualIndex; i < burgerVisuals.Length; i++)
        {
            Transform t = burgerVisuals[i];
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

    /// <summary> Игрок положил бургер обратно на полку — увеличиваем запас и «освобождаем» слот. </summary>
    public void ReturnBurger()
    {
        _stock = Mathf.Min(_stock + 1, burgerVisuals != null ? burgerVisuals.Length : initialStock);
        _nextVisualIndex = Mathf.Max(0, _nextVisualIndex - 1);
    }

    /// <summary> Для будущего пополнения: вернуть запас и индекс выдачи (объекты бургеров нужно самому включить и вернуть на полку). </summary>
    public void Restock(int count)
    {
        _stock = Mathf.Clamp(_stock + count, 0, burgerVisuals != null ? burgerVisuals.Length : 0);
        _nextVisualIndex = Mathf.Min(_nextVisualIndex, burgerVisuals != null ? burgerVisuals.Length : 0);
    }

    /// <summary> Сбросить полку в начальное состояние (все бургеры снова «в запасе»). Объекты в массиве нужно самому включить и поставить на полку. </summary>
    public void ResetStockToFull()
    {
        _stock = initialStock;
        _nextVisualIndex = 0;
    }
}
