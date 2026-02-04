using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Позволяет временно превратить камеру игрока в камеру наблюдения.
/// Блокирует управление, пока активен режим видеонаблюдения, и даёт переключаться между точками.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("NewCore/Player Surveillance Viewer")]
public class PlayerSurveillanceViewer : MonoBehaviour
{
    public static PlayerSurveillanceViewer Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Текст поверх экрана с названием активной камеры. Если пусто — создаётся автоматически.")]
    [SerializeField] Text overlayText;
    [Tooltip("Шрифт для overlay. Если пусто — используется встроенный.")]
    [SerializeField] Font overlayFont;

    [Header("Управление")]
    [Tooltip("Клавиша предыдущей камеры. None — отключено.")]
    [SerializeField] KeyCode prevKey = KeyCode.None;
    [Tooltip("Клавиша следующей камеры (отображается в подсказке на экране). По умолчанию Q.")]
    [SerializeField] KeyCode nextKey = KeyCode.Q;
    [Tooltip("Клавиша выхода из видеонаблюдения. По умолчанию Escape.")]
    [SerializeField] KeyCode exitKey = KeyCode.Escape;

    [Header("Плавность движения камеры")]
    [SerializeField, Range(1f, 20f)] float positionLerpSpeed = 12f;
    [SerializeField, Range(1f, 20f)] float rotationLerpSpeed = 12f;
    [SerializeField, Range(1f, 20f)] float fovLerpSpeed = 12f;

    Camera _camera;
    Transform _originalParent;
    Vector3 _originalLocalPosition;
    Quaternion _originalLocalRotation;
    float _originalFov;
    UnityEngine.Events.UnityAction _onCancelAction;
    ElmanGameDevTools.PlayerSystem.PlayerController _playerController;

    SurveillanceMonitor _activeMonitor;
    int _currentIndex;
    bool _isActive;

    Vector3 _targetPosition;
    Quaternion _targetRotation;
    float _targetFov;
    CursorLockMode _prevCursorLockMode = CursorLockMode.Locked;
    bool _prevCursorVisible;
    bool _pendingCursorRestore;
    int _forceCursorHiddenFrames;

    InputAction _actionNextCamera;
    InputAction _actionExit;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlayerSurveillanceViewer: второй экземпляр уничтожен.");
            Destroy(this);
            return;
        }

        Instance = this;
        _camera = GetComponent<Camera>();
        _originalParent = transform.parent;
        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;
        _originalFov = _camera != null ? _camera.fieldOfView : 60f;

        EnsureOverlay();
        SetOverlayVisible(false);
    }

    void OnDestroy()
    {
        _actionNextCamera?.Dispose();
        _actionExit?.Dispose();
        if (Instance == this)
            Instance = null;
    }

    void EnsureSurveillanceActions()
    {
        if (_actionNextCamera != null) return;
        _actionNextCamera = new InputAction(type: InputActionType.Button, binding: GetBindingPath(nextKey));
        _actionExit = new InputAction(type: InputActionType.Button, binding: GetBindingPath(exitKey));
    }

    static string GetBindingPath(KeyCode key)
    {
        if (key == KeyCode.Escape) return "<Keyboard>/escape";
        string name = key.ToString().ToLowerInvariant();
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            name = "digit" + ((int)key - (int)KeyCode.Alpha0);
        return "<Keyboard>/" + name;
    }

    void Update()
    {
        // После выхода принудительно скрываем курсор несколько кадров (другие скрипты по Escape показывают его)
        if (!_isActive && _forceCursorHiddenFrames > 0)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _forceCursorHiddenFrames--;
        }

        if (!_isActive) return;

        EnsureSurveillanceActions();
        if (_actionExit.triggered)
        {
            ExitSurveillance();
            return;
        }
        if (_actionNextCamera.triggered)
        {
            MoveToCamera(_currentIndex + 1);
            return;
        }
        if (prevKey != KeyCode.None && WasKeyPressed(prevKey))
            MoveToCamera(_currentIndex - 1);

        if (_isActive && Cursor.lockState != CursorLockMode.Locked)
            ExitSurveillance();
    }

    void LateUpdate()
    {
        if (_pendingCursorRestore)
        {
            Cursor.lockState = _prevCursorLockMode;
            Cursor.visible = _prevCursorVisible;
            _pendingCursorRestore = false;
        }

        if (!_isActive) return;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * positionLerpSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * rotationLerpSpeed);

        if (_camera != null)
        {
            float targetFov = _targetFov > 0f ? _targetFov : _originalFov;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeed);
        }
    }



    public bool IsActive => _isActive;

    public void EnterSurveillance(SurveillanceMonitor monitor)
    {
        if (monitor == null || monitor.CameraCount == 0)
        {
            Debug.LogWarning("PlayerSurveillanceViewer: монитор без камер.");
            return;
        }

        if (_activeMonitor != monitor)
        {
            _currentIndex = 0;
            _activeMonitor = monitor;
        }

            if (!_isActive)
        {
            CacheOriginalTransform();
            _isActive = true;
            // Фиксируем поворот тела и головы в сторону монитора — при смене камер персонаж не будет дёргать головой (важно для мультиплеера).
            var visualBody = _originalParent != null ? _originalParent.GetComponentInParent<ElmanGameDevTools.PlayerSystem.PlayerVisualBody>() : null;
            if (visualBody != null)
                visualBody.FreezeCurrentLook();
            // Не блокируем весь ввод (LockInput): иначе отключённые action maps могут мешать обновлению Keyboard.current,
            // и Q/Escape перестают срабатывать. Достаточно отключить PlayerController — персонаж не двигается.
            DisablePlayerController();
            _prevCursorLockMode = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetOverlayVisible(true);
            EnsureSurveillanceActions();
            _actionNextCamera?.Enable();
            _actionExit?.Enable();
        }

        UpdateTargetFromSlot();
        UpdateOverlayText();
    }

    public void ExitSurveillance()
    {
        if (!_isActive) return;

        _isActive = false;
        _activeMonitor = null;
        var visualBody = _originalParent != null ? _originalParent.GetComponentInParent<ElmanGameDevTools.PlayerSystem.PlayerVisualBody>() : null;
        if (visualBody != null)
            visualBody.UnfreezeLook();
        RestoreOriginalTransform();
        RestorePlayerController();
        SetOverlayVisible(false);
        _actionNextCamera?.Disable();
        _actionExit?.Disable();

        Cursor.lockState = _prevCursorLockMode;
        Cursor.visible = _prevCursorVisible;
        _pendingCursorRestore = true;
        _forceCursorHiddenFrames = 4;
    }

    void MoveToCamera(int newIndex)
    {
        if (_activeMonitor == null || _activeMonitor.CameraCount == 0) return;
        int count = _activeMonitor.CameraCount;
        _currentIndex = (newIndex % count + count) % count;
        UpdateTargetFromSlot();
        UpdateOverlayText();
    }

    void CacheOriginalTransform()
    {
        _originalParent = transform.parent;
        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;
        if (_camera != null)
            _originalFov = _camera.fieldOfView;
    }

    void RestoreOriginalTransform()
    {
        if (_originalParent != null)
            transform.SetParent(_originalParent);
        transform.localPosition = _originalLocalPosition;
        transform.localRotation = _originalLocalRotation;

        if (_camera != null)
            _camera.fieldOfView = _originalFov;
    }

    void DisablePlayerController()
    {
        if (_playerController == null)
            _playerController = FindFirstObjectByType<ElmanGameDevTools.PlayerSystem.PlayerController>();

        if (_playerController != null && _playerController.playerCamera == transform)
        {
            _playerController.enabled = false;
        }
    }

    void RestorePlayerController()
    {
        if (_playerController != null)
        {
            _playerController.enabled = true;
        }
    }

    void UpdateTargetFromSlot()
    {
        if (_activeMonitor == null) return;

        if (!_activeMonitor.TryGetCameraView(_currentIndex, out var view) || view.viewPoint == null)
        {
            Debug.LogWarning("PlayerSurveillanceViewer: слот камеры не задан.");
            return;
        }

        transform.SetParent(null);
        _targetPosition = view.viewPoint.position;
        _targetRotation = view.viewPoint.rotation;
        _targetFov = view.overrideFov;
        transform.position = _targetPosition;
        transform.rotation = _targetRotation;
    }

    void UpdateOverlayText()
    {
        if (overlayText == null || _activeMonitor == null) return;

        string nextText = nextKey != KeyCode.None ? $"{nextKey} — следующая камера" : "";
        string exitText = exitKey != KeyCode.None ? $"{exitKey} — выйти" : "";
        string instructions = string.Join("    ", new[] { nextText, exitText }.Where(s => !string.IsNullOrEmpty(s)));
        overlayText.text = $"{_activeMonitor.DisplayName} — {_activeMonitor.GetCameraLabel(_currentIndex)}\n{instructions}";
    }

    void EnsureOverlay()
    {
        if (overlayText != null) return;

        var canvasObj = new GameObject("SurveillanceOverlayCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        var textObj = new GameObject("OverlayText");
        textObj.transform.SetParent(canvasObj.transform, false);
        overlayText = textObj.AddComponent<Text>();
        overlayText.alignment = TextAnchor.UpperCenter;
        overlayText.fontSize = 32;
        overlayText.fontStyle = FontStyle.Bold;
        overlayText.raycastTarget = false;
        overlayText.color = Color.white;
        overlayText.text = "";

        overlayFont = overlayFont != null ? overlayFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overlayText.font = overlayFont;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -60f);
        rect.sizeDelta = new Vector2(900f, 80f);
    }

    void SetOverlayVisible(bool on)
    {
        if (overlayText == null) return;
        overlayText.gameObject.SetActive(on);
        if (on && overlayFont != null)
            overlayText.font = overlayFont;
    }

    bool WasKeyPressed(KeyCode key)
    {
        if (key == KeyCode.None) return false;
        if (Keyboard.current != null)
        {
            if (key == KeyCode.Escape && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
            if (TryConvertKeyCode(key, out Key k))
            {
                var control = Keyboard.current[k];
                if (control != null && control.wasPressedThisFrame)
                    return true;
            }
        }
        return Input.GetKeyDown(key);
    }

    static bool TryConvertKeyCode(KeyCode keyCode, out Key key)
    {
        // Прямая конвертация по имени, если enum совпадает
        if (System.Enum.TryParse(keyCode.ToString(), true, out key))
            return true;

        switch (keyCode)
        {
            case KeyCode.Escape: key = Key.Escape; return true;
            case KeyCode.Q: key = Key.Q; return true;
            case KeyCode.E: key = Key.E; return true;
            case KeyCode.Alpha0: key = Key.Digit0; return true;
            case KeyCode.Alpha1: key = Key.Digit1; return true;
            case KeyCode.Alpha2: key = Key.Digit2; return true;
            case KeyCode.Alpha3: key = Key.Digit3; return true;
            case KeyCode.Alpha4: key = Key.Digit4; return true;
            case KeyCode.Alpha5: key = Key.Digit5; return true;
            case KeyCode.Alpha6: key = Key.Digit6; return true;
            case KeyCode.Alpha7: key = Key.Digit7; return true;
            case KeyCode.Alpha8: key = Key.Digit8; return true;
            case KeyCode.Alpha9: key = Key.Digit9; return true;
            case KeyCode.LeftArrow: key = Key.LeftArrow; return true;
            case KeyCode.RightArrow: key = Key.RightArrow; return true;
            case KeyCode.UpArrow: key = Key.UpArrow; return true;
            case KeyCode.DownArrow: key = Key.DownArrow; return true;
            case KeyCode.Return: key = Key.Enter; return true;
            case KeyCode.BackQuote: key = Key.Backquote; return true;
            case KeyCode.Minus: key = Key.Minus; return true;
            case KeyCode.Equals: key = Key.Equals; return true;
            case KeyCode.LeftBracket: key = Key.LeftBracket; return true;
            case KeyCode.RightBracket: key = Key.RightBracket; return true;
            case KeyCode.Backslash: key = Key.Backslash; return true;
            case KeyCode.Semicolon: key = Key.Semicolon; return true;
            case KeyCode.Quote: key = Key.Quote; return true;
            case KeyCode.Comma: key = Key.Comma; return true;
            case KeyCode.Period: key = Key.Period; return true;
            case KeyCode.Slash: key = Key.Slash; return true;
            default:
                key = Key.None;
                return false;
        }
    }
}
