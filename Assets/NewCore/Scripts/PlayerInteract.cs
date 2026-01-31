using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Луч из центра экрана: при наведении — подсказка «Взаимодействовать [E]» и подсветка объекта.
/// Вешать на Main Camera. Нужен Input Action Asset (InputSystem_Actions) в поле ввода.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("NewCore/Player Interact")]
public class PlayerInteract : MonoBehaviour
{
    [Header("Луч")]
    [Tooltip("Максимальная дистанция взаимодействия (в упор).")]
    [SerializeField] float maxDistance = 2.5f;
    [SerializeField] LayerMask interactLayers = ~0;

    [Header("Ввод")]
    [Tooltip("InputSystem_Actions или другой asset с картой Player и действием Interact (E).")]
    [SerializeField] InputActionAsset inputActionAsset;

    [Header("Подсказка (опционально)")]
    [Tooltip("Текст подсказки в UI. Если не задан — создаётся автоматически.")]
    [SerializeField] Text hintText;
    [Tooltip("Шрифт подсказки. Если задан HintSettings в Resources — используется он; иначе этот шрифт.")]
    [SerializeField] Font hintFont;

    Camera _cam;
    InputAction _interactAction;
    InteractableDoor _currentDoor;
    ClientNPC _currentClient;
    const string HintMessage = "Взаимодействовать  [E]";

    void Start()
    {
        _cam = GetComponent<Camera>();
        if (inputActionAsset != null)
        {
            var map = inputActionAsset.FindActionMap("Player");
            _interactAction = map?.FindAction("Interact");
            if (_interactAction != null)
                _interactAction.Enable();
        }

        // Единый шрифт для всех подсказок: из HintSettings в Resources или из поля в инспекторе
        HintSettings hs = Resources.Load<HintSettings>("HintSettings");
        if (hs != null && hs.defaultHintFont != null)
            hintFont = hs.defaultHintFont;

        if (hintText == null)
            CreateDefaultHint();
        if (hintText != null)
        {
            if (hintFont != null) hintText.font = hintFont;
            hintText.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        _interactAction?.Disable();
    }

    void CreateDefaultHint()
    {
        var canvasObj = new GameObject("InteractHintCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100;
        canvasObj.AddComponent<GraphicRaycaster>();

        var textObj = new GameObject("HintText");
        textObj.transform.SetParent(canvasObj.transform, false);
        hintText = textObj.AddComponent<Text>();
        hintText.text = HintMessage;
        hintText.font = hintFont != null ? hintFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hintText.fontSize = 32;
        hintText.fontStyle = FontStyle.Bold;
        hintText.color = Color.white;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.raycastTarget = false;
        hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hintText.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.08f);
        rect.anchorMax = new Vector2(0.5f, 0.08f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520, 56);
        rect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        InteractableDoor hitDoor = null;
        ClientNPC hitClient = null;
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactLayers))
        {
            hitDoor = hit.collider.GetComponentInParent<InteractableDoor>();
            hitClient = hit.collider.GetComponentInParent<ClientNPC>();
        }

        if (hitDoor != _currentDoor)
        {
            if (_currentDoor != null)
                _currentDoor.SetHighlight(false);
            _currentDoor = hitDoor;
            if (_currentDoor != null)
                _currentDoor.SetHighlight(true);
        }

        if (hitClient != _currentClient)
            _currentClient = hitClient;

        bool showHint = _currentDoor != null || _currentClient != null;
        if (hintText != null)
            hintText.gameObject.SetActive(showHint);

        if (_interactAction != null && _interactAction.triggered)
        {
            if (_currentDoor != null)
                _currentDoor.Open();
            else if (_currentClient != null)
                _currentClient.OnInteract();
        }
    }
}
