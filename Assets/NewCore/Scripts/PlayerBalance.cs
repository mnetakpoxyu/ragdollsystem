using System;
using UnityEngine;

/// <summary>
/// Хранит баланс игрока. Повесь на игрока или любой постоянный объект (например, GameManager).
/// BalanceDisplayTarget берёт значение отсюда для отображения над объектами.
/// При изменении баланса вызывается OnBalanceChanged(delta) — для поп-апов над кассой.
/// </summary>
[AddComponentMenu("NewCore/Player Balance")]
public class PlayerBalance : MonoBehaviour
{
    /// <summary> Вызывается при каждом изменении баланса. Аргумент — прирост (положительный или отрицательный). </summary>
    public static event Action<float> OnBalanceChanged;
    [Header("Баланс")]
    [Tooltip("Текущий баланс игрока.")]
    [SerializeField] float balance = 1000f;

    static PlayerBalance _instance;

    /// <summary> Текущий баланс. </summary>
    public float Balance
    {
        get => balance;
        set => balance = Mathf.Max(0f, value);
    }

    /// <summary> Единственный экземпляр в сцене (для быстрого доступа). </summary>
    public static PlayerBalance Instance => _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("PlayerBalance: найден второй экземпляр, оставляем первый.");
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary> Добавить сумму к балансу (отрицательная — списание). </summary>
    public void Add(float amount)
    {
        Balance += amount;
        OnBalanceChanged?.Invoke(amount);
    }

    /// <summary> Списать сумму. Возвращает true, если баланса хватило. </summary>
    public bool TrySpend(float amount)
    {
        if (balance < amount) return false;
        Balance -= amount;
        return true;
    }
}
