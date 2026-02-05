using UnityEngine;

/// <summary>
/// Что игрок несёт в руках (например газировка). Один экземпляр в сцене.
/// </summary>
public class PlayerCarry : MonoBehaviour
{
    static PlayerCarry _instance;
    public static PlayerCarry Instance => _instance;

    [Header("Настройки")]
    [Tooltip("Оплата за напиток клиенту (игрок получает эту сумму).")]
    [SerializeField] float drinkPaymentAmount = 25f;

    [Header("Вид от первого лица")]
    [Tooltip("Визуал банки/напитка (префаб или объект на сцене). При взятии напитка переносится к камере и показывается перед лицом.")]
    [SerializeField] GameObject drinkVisualInHand;
    [Tooltip("Локальная позиция относительно камеры: справа (X), вниз (Y), вперёд (Z).")]
    [SerializeField] Vector3 holdLocalPosition = new Vector3(0.32f, -0.22f, 0.48f);
    [Tooltip("Локальный поворот в градусах (Euler): наклон банки перед камерой.")]
    [SerializeField] Vector3 holdLocalRotationEuler = new Vector3(5f, 0f, -20f);
    [Tooltip("Локальный масштаб визуала в руке (если банка слишком большая/маленькая).")]
    [SerializeField] Vector3 holdLocalScale = new Vector3(1f, 1f, 1f);

    bool _hasDrink;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (drinkVisualInHand != null)
        {
            drinkVisualInHand.SetActive(false);
            ReparentDrinkToCamera();
        }
    }

    void Start()
    {
        if (drinkVisualInHand != null && drinkVisualInHand.transform.parent == null)
            ReparentDrinkToCamera();
    }

    void ReparentDrinkToCamera()
    {
        if (drinkVisualInHand == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        drinkVisualInHand.transform.SetParent(cam.transform, false);
        drinkVisualInHand.transform.localPosition = holdLocalPosition;
        drinkVisualInHand.transform.localRotation = Quaternion.Euler(holdLocalRotationEuler);
        drinkVisualInHand.transform.localScale = holdLocalScale;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public bool HasDrink => _hasDrink;
    public float DrinkPaymentAmount => drinkPaymentAmount;

    public void TakeDrink()
    {
        _hasDrink = true;
        if (drinkVisualInHand != null)
        {
            ReparentDrinkToCamera();
            drinkVisualInHand.SetActive(true);
        }
    }

    public void GiveDrink()
    {
        _hasDrink = false;
        if (drinkVisualInHand != null)
            drinkVisualInHand.SetActive(false);
    }
}
