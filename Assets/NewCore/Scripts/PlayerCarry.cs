using System.Collections;
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

    [Header("Бургер")]
    [Tooltip("Оплата за бургер (игрок получает при принятии заказа; при таймауте доставки эта сумма возвращается клиенту).")]
    [SerializeField] float burgerPaymentAmount = 80f;

    [Header("Кальян")]
    [Tooltip("Оплата за кальян (игрок получает при принятии заказа).")]
    [SerializeField] float hookahPaymentAmount = 120f;

    [Header("Вид от первого лица")]
    [Tooltip("Визуал банки/напитка (префаб или объект на сцене). При взятии напитка переносится к камере и показывается перед лицом.")]
    [SerializeField] GameObject drinkVisualInHand;
    [Tooltip("Локальная позиция относительно камеры: справа (X), вниз (Y), вперёд (Z).")]
    [SerializeField] Vector3 holdLocalPosition = new Vector3(0.32f, -0.22f, 0.48f);
    [Tooltip("Локальный поворот в градусах (Euler): наклон банки перед камерой.")]
    [SerializeField] Vector3 holdLocalRotationEuler = new Vector3(5f, 0f, -20f);
    [Tooltip("Локальный масштаб визуала в руке (если банка слишком большая/маленькая).")]
    [SerializeField] Vector3 holdLocalScale = new Vector3(1f, 1f, 1f);
    [Tooltip("Визуал бургера в руке (опционально). При взятии бургера показывается перед камерой.")]
    [SerializeField] GameObject burgerVisualInHand;
    [Tooltip("Локальная позиция бургера относительно камеры.")]
    [SerializeField] Vector3 burgerHoldLocalPosition = new Vector3(0.3f, -0.2f, 0.45f);
    [Tooltip("Локальный поворот бургера (Euler).")]
    [SerializeField] Vector3 burgerHoldLocalRotationEuler = new Vector3(10f, 0f, -15f);
    [Tooltip("Локальный масштаб бургера в руке.")]
    [SerializeField] Vector3 burgerHoldLocalScale = new Vector3(1f, 1f, 1f);
    [Tooltip("Локальная позиция кальяна в руке относительно камеры.")]
    [SerializeField] Vector3 hookahHoldLocalPosition = new Vector3(0.25f, -0.25f, 0.5f);
    [Tooltip("Локальный поворот кальяна (Euler).")]
    [SerializeField] Vector3 hookahHoldLocalRotationEuler = new Vector3(5f, 0f, -10f);
    [Tooltip("Локальный масштаб кальяна в руке.")]
    [SerializeField] Vector3 hookahHoldLocalScale = new Vector3(1f, 1f, 1f);

    bool _hasDrink;
    bool _hasBurger;
    bool _hasHookah;
    /// <summary> Напиток со стойки (из массива DrinkStock): при сдаче клиенту скрывается; при «положить обратно» возвращается на полку. </summary>
    Transform _borrowedDrinkVisual;
    DrinkStock _borrowedDrinkStock;
    Transform _borrowedDrinkOriginalParent;
    Vector3 _borrowedDrinkLocalPos;
    Quaternion _borrowedDrinkLocalRot;
    Vector3 _borrowedDrinkLocalScale;
    Vector3 _borrowedDrinkWorldPos;
    /// <summary> Бургер со стойки (из массива FoodStock): при сдаче клиенту скрывается; при «положить обратно» возвращается на полку. </summary>
    Transform _borrowedBurgerVisual;
    FoodStock _borrowedFoodStock;
    Transform _borrowedBurgerOriginalParent;
    Vector3 _borrowedBurgerLocalPos;
    Quaternion _borrowedBurgerLocalRot;
    Vector3 _borrowedBurgerLocalScale;
    Vector3 _borrowedBurgerWorldPos;
    Transform _borrowedHookahVisual;
    HookahStock _borrowedHookahStock;
    Transform _borrowedHookahOriginalParent;
    Vector3 _borrowedHookahLocalPos;
    Quaternion _borrowedHookahLocalRot;
    Vector3 _borrowedHookahLocalScale;
    Vector3 _borrowedHookahWorldPos;

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
        if (burgerVisualInHand != null)
        {
            burgerVisualInHand.SetActive(false);
            ReparentBurgerToCamera();
        }
    }

    void Start()
    {
        if (drinkVisualInHand != null && drinkVisualInHand.transform.parent == null)
            ReparentDrinkToCamera();
        if (burgerVisualInHand != null && burgerVisualInHand.transform.parent == null)
            ReparentBurgerToCamera();
    }

    void ReparentBurgerToCamera()
    {
        if (burgerVisualInHand == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        burgerVisualInHand.transform.SetParent(cam.transform, false);
        burgerVisualInHand.transform.localPosition = burgerHoldLocalPosition;
        burgerVisualInHand.transform.localRotation = Quaternion.Euler(burgerHoldLocalRotationEuler);
        burgerVisualInHand.transform.localScale = burgerHoldLocalScale;
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
    public bool HasBurger => _hasBurger;
    public float BurgerPaymentAmount => burgerPaymentAmount;
    public bool HasHookah => _hasHookah;
    public float HookahPaymentAmount => hookahPaymentAmount;
    /// <summary> С какой полки взят бургер (для «положить обратно» только на ту же полку). </summary>
    public FoodStock TakenFromFoodStock => _borrowedFoodStock;
    /// <summary> С какой полки взят кальян. </summary>
    public HookahStock TakenFromHookahStock => _borrowedHookahStock;
    /// <summary> С какой полки взят напиток (для «положить обратно» только на ту же полку). </summary>
    public DrinkStock TakenFromDrinkStock => _borrowedDrinkStock;

    /// <summary> Взять напиток. visualFromStock — объект с полки; fromStock — полка (для «положить обратно»). </summary>
    public void TakeDrink(Transform visualFromStock = null, DrinkStock fromStock = null)
    {
        _hasDrink = true;
        _borrowedDrinkVisual = null;
        _borrowedDrinkStock = null;

        if (visualFromStock != null)
        {
            _borrowedDrinkVisual = visualFromStock;
            _borrowedDrinkStock = fromStock;
            _borrowedDrinkOriginalParent = visualFromStock.parent;
            _borrowedDrinkLocalPos = visualFromStock.localPosition;
            _borrowedDrinkLocalRot = visualFromStock.localRotation;
            _borrowedDrinkLocalScale = visualFromStock.localScale;
            _borrowedDrinkWorldPos = visualFromStock.position;
            ReparentDrinkToCamera(visualFromStock);
            visualFromStock.gameObject.SetActive(true);
            if (drinkVisualInHand != null)
                drinkVisualInHand.SetActive(false);
            return;
        }

        if (drinkVisualInHand != null)
        {
            ReparentDrinkToCamera();
            drinkVisualInHand.SetActive(true);
        }
    }

    /// <summary> Положить стаканчик обратно на полку (только на ту же, с которой взяли). Возвращает true, если положили. </summary>
    public bool PutDrinkBack(DrinkStock toStock)
    {
        if (!_hasDrink || _borrowedDrinkVisual == null || toStock != _borrowedDrinkStock) return false;
        Transform visual = _borrowedDrinkVisual;
        visual.SetParent(toStock.transform);
        visual.position = _borrowedDrinkWorldPos;
        visual.localRotation = _borrowedDrinkLocalRot;
        visual.localScale = _borrowedDrinkLocalScale;
        visual.gameObject.SetActive(true);
        StartCoroutine(DisableCollisionTemporarily(visual.gameObject, 0.25f));
        toStock.ReturnDrink();
        _hasDrink = false;
        _borrowedDrinkVisual = null;
        _borrowedDrinkStock = null;
        return true;
    }

    void ReparentDrinkToCamera(Transform drinkTransform)
    {
        if (drinkTransform == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        drinkTransform.SetParent(cam.transform, false);
        drinkTransform.localPosition = holdLocalPosition;
        drinkTransform.localRotation = Quaternion.Euler(holdLocalRotationEuler);
        drinkTransform.localScale = holdLocalScale;
    }

    public void GiveDrink()
    {
        _hasDrink = false;
        _borrowedDrinkStock = null;

        if (_borrowedDrinkVisual != null)
        {
            _borrowedDrinkVisual.gameObject.SetActive(false);
            _borrowedDrinkVisual = null;
            return;
        }

        if (drinkVisualInHand != null)
            drinkVisualInHand.SetActive(false);
    }

    /// <summary> Взять бургер. visualFromStock — объект с полки; fromStock — полка (для «положить обратно»). </summary>
    public void TakeBurger(Transform visualFromStock = null, FoodStock fromStock = null)
    {
        _hasBurger = true;
        _borrowedBurgerVisual = null;
        _borrowedFoodStock = null;

        if (visualFromStock != null)
        {
            _borrowedBurgerVisual = visualFromStock;
            _borrowedFoodStock = fromStock;
            _borrowedBurgerOriginalParent = visualFromStock.parent;
            _borrowedBurgerLocalPos = visualFromStock.localPosition;
            _borrowedBurgerLocalRot = visualFromStock.localRotation;
            _borrowedBurgerLocalScale = visualFromStock.localScale;
            _borrowedBurgerWorldPos = visualFromStock.position;
            ReparentBurgerToCamera(visualFromStock);
            visualFromStock.gameObject.SetActive(true);
            if (burgerVisualInHand != null)
                burgerVisualInHand.SetActive(false);
            return;
        }

        if (burgerVisualInHand != null)
        {
            ReparentBurgerToCamera();
            burgerVisualInHand.SetActive(true);
        }
    }

    /// <summary> Положить бургер обратно на полку (только на ту же, с которой взяли). Возвращает true, если положили. </summary>
    public bool PutBurgerBack(FoodStock toStock)
    {
        if (!_hasBurger || _borrowedBurgerVisual == null || toStock != _borrowedFoodStock) return false;
        Transform visual = _borrowedBurgerVisual;
        visual.SetParent(toStock.transform);
        visual.position = _borrowedBurgerWorldPos;
        visual.localRotation = _borrowedBurgerLocalRot;
        visual.localScale = _borrowedBurgerLocalScale;
        visual.gameObject.SetActive(true);
        StartCoroutine(DisableCollisionTemporarily(visual.gameObject, 0.25f));
        toStock.ReturnBurger();
        _hasBurger = false;
        _borrowedBurgerVisual = null;
        _borrowedFoodStock = null;
        return true;
    }

    /// <summary> Временно отключает коллайдеры и физику объекта, чтобы при возврате на полку не отталкивало игрока. </summary>
    static IEnumerator DisableCollisionTemporarily(GameObject go, float seconds)
    {
        if (go == null) yield break;
        var colliders = go.GetComponentsInChildren<Collider>(true);
        var rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
        bool[] colliderEnabled = new bool[colliders.Length];
        bool[] rigidbodyKinematic = new bool[rigidbodies.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderEnabled[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodyKinematic[i] = rigidbodies[i].isKinematic;
            rigidbodies[i].isKinematic = true;
        }
        yield return new WaitForSeconds(seconds);
        if (go == null) yield break;
        for (int i = 0; i < colliders.Length && i < colliderEnabled.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = colliderEnabled[i];
        }
        for (int i = 0; i < rigidbodies.Length && i < rigidbodyKinematic.Length; i++)
        {
            if (rigidbodies[i] != null)
                rigidbodies[i].isKinematic = rigidbodyKinematic[i];
        }
    }

    void ReparentBurgerToCamera(Transform burgerTransform)
    {
        if (burgerTransform == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        burgerTransform.SetParent(cam.transform, false);
        burgerTransform.localPosition = burgerHoldLocalPosition;
        burgerTransform.localRotation = Quaternion.Euler(burgerHoldLocalRotationEuler);
        burgerTransform.localScale = burgerHoldLocalScale;
    }

    /// <summary> Отдать бургер клиенту — объект просто скрывается (не удаляется; для будущего пополнения). </summary>
    public void GiveBurger()
    {
        _hasBurger = false;
        _borrowedFoodStock = null;

        if (_borrowedBurgerVisual != null)
        {
            _borrowedBurgerVisual.gameObject.SetActive(false);
            _borrowedBurgerVisual = null;
            return;
        }

        if (burgerVisualInHand != null)
            burgerVisualInHand.SetActive(false);
    }

    /// <summary> Поставить бургер на стол (точка еды). Возвращает объект бургера. Коллайдеры временно отключаются. </summary>
    public Transform GiveBurgerTo(Transform place)
    {
        if (!_hasBurger || _borrowedBurgerVisual == null || place == null) return null;
        Transform visual = _borrowedBurgerVisual;
        _hasBurger = false;
        _borrowedFoodStock = null;
        _borrowedBurgerVisual = null;

        Vector3 worldScale = visual.lossyScale;
        visual.SetParent(place);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        if (place.lossyScale.x != 0f && place.lossyScale.y != 0f && place.lossyScale.z != 0f)
            visual.localScale = new Vector3(worldScale.x / place.lossyScale.x, worldScale.y / place.lossyScale.y, worldScale.z / place.lossyScale.z);
        visual.gameObject.SetActive(true);
        StartCoroutine(DisableCollisionTemporarily(visual.gameObject, 0.25f));
        return visual;
    }

    /// <summary> Взять кальян. visualFromStock — объект с полки; fromStock — полка (для «положить обратно»). </summary>
    public void TakeHookah(Transform visualFromStock = null, HookahStock fromStock = null)
    {
        _hasHookah = true;
        _borrowedHookahVisual = null;
        _borrowedHookahStock = null;

        if (visualFromStock != null)
        {
            _borrowedHookahVisual = visualFromStock;
            _borrowedHookahStock = fromStock;
            _borrowedHookahOriginalParent = visualFromStock.parent;
            _borrowedHookahLocalPos = visualFromStock.localPosition;
            _borrowedHookahLocalRot = visualFromStock.localRotation;
            _borrowedHookahLocalScale = visualFromStock.localScale;
            _borrowedHookahWorldPos = visualFromStock.position;
            ReparentHookahToCamera(visualFromStock);
            visualFromStock.gameObject.SetActive(true);
            return;
        }
    }

    void ReparentHookahToCamera(Transform hookahTransform)
    {
        if (hookahTransform == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        hookahTransform.SetParent(cam.transform, false);
        hookahTransform.localPosition = hookahHoldLocalPosition;
        hookahTransform.localRotation = Quaternion.Euler(hookahHoldLocalRotationEuler);
        hookahTransform.localScale = hookahHoldLocalScale;
    }

    /// <summary> Положить кальян обратно на полку (только на ту же, с которой взяли). </summary>
    public bool PutHookahBack(HookahStock toStock)
    {
        if (!_hasHookah || _borrowedHookahVisual == null || toStock != _borrowedHookahStock) return false;
        Transform visual = _borrowedHookahVisual;
        visual.SetParent(toStock.transform);
        visual.position = _borrowedHookahWorldPos;
        visual.localRotation = _borrowedHookahLocalRot;
        visual.localScale = _borrowedHookahLocalScale;
        visual.gameObject.SetActive(true);
        StartCoroutine(DisableCollisionTemporarily(visual.gameObject, 0.25f));
        toStock.ReturnHookah();
        _hasHookah = false;
        _borrowedHookahVisual = null;
        _borrowedHookahStock = null;
        return true;
    }

    /// <summary> Отдать кальян клиенту — объект скрывается. </summary>
    public void GiveHookah()
    {
        _hasHookah = false;
        _borrowedHookahStock = null;
        if (_borrowedHookahVisual != null)
        {
            _borrowedHookahVisual.gameObject.SetActive(false);
            _borrowedHookahVisual = null;
        }
    }

    /// <summary> Поставить кальян в точку у стола (HookahPlace). Возвращает объект кальяна, который теперь у места. Коллайдеры временно отключаются, чтобы не отталкивало игрока. </summary>
    public Transform GiveHookahTo(Transform place)
    {
        if (!_hasHookah || _borrowedHookahVisual == null || place == null) return null;
        Transform visual = _borrowedHookahVisual;
        _hasHookah = false;
        _borrowedHookahStock = null;
        _borrowedHookahVisual = null;

        Vector3 worldScale = visual.lossyScale;
        visual.SetParent(place);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        if (place.lossyScale.x != 0f && place.lossyScale.y != 0f && place.lossyScale.z != 0f)
            visual.localScale = new Vector3(worldScale.x / place.lossyScale.x, worldScale.y / place.lossyScale.y, worldScale.z / place.lossyScale.z);
        visual.gameObject.SetActive(true);
        StartCoroutine(DisableCollisionTemporarily(visual.gameObject, 0.25f));
        return visual;
    }
}
