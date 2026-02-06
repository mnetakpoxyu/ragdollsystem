using UnityEngine;

/// <summary>
/// Запас кальянов на полке. В инспекторе перетаскиваешь массив кальянов (дочерние объекты) — при взятии один уходит в руку.
/// При сдаче клиенту объект скрывается; можно положить обратно на ту же полку по E.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HookahStock : MonoBehaviour
{
    [Header("Запас")]
    [Tooltip("Начальное количество кальянов в запасе (должно совпадать с размером массива ниже).")]
    [SerializeField] int initialStock = 5;

    [Header("Кальяны на полке")]
    [Tooltip("Перетащи сюда кальяны (дочерние объекты полки). По одному уходят в руку при взятии; при сдаче клиенту скрываются; можно положить обратно по E.")]
    [SerializeField] Transform[] hookahVisuals = new Transform[0];

    int _stock;
    int _nextVisualIndex;

    void Awake()
    {
        _stock = Mathf.Max(0, initialStock);
    }

    public int Stock => _stock;

    /// <summary> Взять один кальян. Возвращает true и выдает visualForHand. Выдаём только объект, который ещё на полке. </summary>
    public bool TryTakeHookah(bool playerAlreadyHolding, out Transform visualForHand)
    {
        visualForHand = null;
        if (playerAlreadyHolding || _stock <= 0) return false;
        if (hookahVisuals == null) return false;

        for (int i = _nextVisualIndex; i < hookahVisuals.Length; i++)
        {
            Transform t = hookahVisuals[i];
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

    /// <summary> Игрок положил кальян обратно на полку. </summary>
    public void ReturnHookah()
    {
        _stock = Mathf.Min(_stock + 1, hookahVisuals != null ? hookahVisuals.Length : initialStock);
        _nextVisualIndex = Mathf.Max(0, _nextVisualIndex - 1);
    }

    public void Restock(int count)
    {
        _stock = Mathf.Clamp(_stock + count, 0, hookahVisuals != null ? hookahVisuals.Length : 0);
        _nextVisualIndex = Mathf.Min(_nextVisualIndex, hookahVisuals != null ? hookahVisuals.Length : 0);
    }

    public void ResetStockToFull()
    {
        _stock = initialStock;
        _nextVisualIndex = 0;
    }
}
