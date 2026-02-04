using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>
/// Внутриигровое время в формате 24 часов. Идёт ускоренно.
/// Режим «на экране» — время в углу HUD. Режим «в мире» — время на объекте, к которому привязан этот скрипт (часы на стене и т.п.).
/// </summary>
public class GameTime : MonoBehaviour
{
    public enum DisplayMode
    {
        [Tooltip("Время в углу экрана (как раньше).")]
        ScreenSpace,
        [Tooltip("Время на этом объекте в сцене — повесь GameTime на пустой объект или на часы.")]
        WorldSpace
    }

    public enum ScreenPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    [Header("Время")]
    [Tooltip("Стартовое время в часах (0–24). Например 9 = 09:00.")]
    [SerializeField, Range(0f, 24f)] float startTimeHours = 9f;
    [Header("Запуск по дверям")]
    [Tooltip("Время не идёт, пока не открыты ВСЕ эти двери. Перетащи сюда 2 двери (для двойных — оба створки). Пусто — время идёт сразу.")]
    [SerializeField] InteractableDoor[] doorsToOpen;
    [Tooltip("Скорость: сколько игровых часов за 1 реальную секунду. Меньше = медленнее время. 0.00125 ≈ 1 игр. час за ~13 мин реального времени.")]
    [SerializeField] float hoursPerRealSecond = 0.00125f;

    [Header("Режим отображения")]
    [Tooltip("Screen Space — время в углу экрана. World Space — время на этом объекте в мире (часы на стене).")]
    [SerializeField] DisplayMode displayMode = DisplayMode.WorldSpace;

    [Header("Отображение")]
    [Tooltip("Текст для времени. Если пусто — создаётся автоматически.")]
    [SerializeField] Text timeDisplayText;
    [Tooltip("Размер шрифта времени. Задаёшь сам.")]
    [SerializeField] int fontSize = 48;
    [Tooltip("Шрифт. Если не задан — стандартный.")]
    [SerializeField] Font font;
    [Tooltip("Цвет времени.")]
    [SerializeField] Color textColor = Color.white;
    [Header("Только для Screen Space")]
    [Tooltip("Где на экране показывать время.")]
    [SerializeField] ScreenPosition screenPosition = ScreenPosition.TopRight;
    [Tooltip("Смещение от выбранной точки (в пикселях).")]
    [SerializeField] Vector2 offsetPixels = new Vector2(-24f, -24f);
    [Header("Только для World Space")]
    [Tooltip("Масштаб времени в мире (чем меньше — тем мельче надпись в сцене).")]
    [SerializeField] float worldScale = 0.005f;
    [Tooltip("Поворачивать ли время к камере (всегда читаемо при взгляде).")]
    [SerializeField] bool billboard = true;
    [Header("Подложка")]
    [Tooltip("Небольшая подложка под текст для читаемости.")]
    [SerializeField] bool useBackground = true;
    [Tooltip("Цвет подложки (полупрозрачная).")]
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

    float _currentTimeHours;
    RectTransform _timeRect;
    Image _backgroundImage;
    Transform _worldCanvasTransform;
    Canvas _worldCanvas;
    Camera _cam;

    void Start()
    {
        _currentTimeHours = startTimeHours % 24f;
        if (timeDisplayText == null)
        {
            if (displayMode == DisplayMode.WorldSpace)
                CreateWorldSpaceDisplay();
            else
                CreateDefaultDisplay();
        }
        ApplyDisplaySettings();
        if (timeDisplayText != null)
            timeDisplayText.text = GetTimeString();
        if (displayMode == DisplayMode.WorldSpace)
        {
            _cam = Camera.main;
            var canvas = timeDisplayText.GetComponentInParent<Canvas>();
            if (canvas != null)
                DisableShadowsOnWorldSpaceCanvas(canvas.gameObject);
        }
    }

    void Update()
    {
        if (!AreAllRequiredDoorsOpen())
            return;
        _currentTimeHours += Time.deltaTime * hoursPerRealSecond;
        if (_currentTimeHours >= 24f) _currentTimeHours -= 24f;
        if (_currentTimeHours < 0f) _currentTimeHours += 24f;
        if (timeDisplayText != null)
            timeDisplayText.text = GetTimeString();
        if (displayMode == DisplayMode.WorldSpace)
        {
            if (_worldCanvas != null && _worldCanvas.worldCamera == null)
                _worldCanvas.worldCamera = Camera.main;
            if (billboard && _worldCanvasTransform != null && _cam != null)
                _worldCanvasTransform.forward = _cam.transform.position - _worldCanvasTransform.position;
        }
    }

    void ApplyDisplaySettings()
    {
        if (timeDisplayText == null) return;
        // Убираем Outline/Shadow — оставляем только основной белый текст
        var outline = timeDisplayText.GetComponent<Outline>();
        if (outline != null) Destroy(outline);
        var shadow = timeDisplayText.GetComponent<Shadow>();
        if (shadow != null) Destroy(shadow);
        timeDisplayText.fontSize = fontSize;
        timeDisplayText.color = textColor;
        if (font != null) timeDisplayText.font = font;
        if (displayMode == DisplayMode.ScreenSpace && _timeRect != null)
            ApplyPosition(_timeRect, screenPosition, offsetPixels);
        if (useBackground && _backgroundImage != null)
        {
            _backgroundImage.enabled = true;
            _backgroundImage.color = backgroundColor;
        }
        else if (_backgroundImage != null)
            _backgroundImage.enabled = false;
    }

    void CreateWorldSpaceDisplay()
    {
        var canvasObj = new GameObject("TimeDisplayCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = Vector3.zero;
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = new Vector3(worldScale, worldScale, worldScale);
        _worldCanvasTransform = canvasObj.transform;

        _worldCanvas = canvasObj.AddComponent<Canvas>();
        _worldCanvas.renderMode = RenderMode.WorldSpace;
        _worldCanvas.worldCamera = Camera.main;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;
        scaler.referencePixelsPerUnit = 100f;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.scaleFactor = 1f;
        canvasObj.AddComponent<GraphicRaycaster>();

        var canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 80f);
        canvasRect.anchoredPosition = Vector2.zero;

        GameObject root = new GameObject("TimePanel");
        root.transform.SetParent(canvasObj.transform, false);
        _timeRect = root.AddComponent<RectTransform>();
        _timeRect.anchorMin = Vector2.zero;
        _timeRect.anchorMax = Vector2.one;
        _timeRect.offsetMin = Vector2.zero;
        _timeRect.offsetMax = Vector2.zero;
        _timeRect.sizeDelta = new Vector2(200f, 80f);

        if (useBackground)
        {
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(root.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-8f, -6f);
            bgRect.offsetMax = new Vector2(8f, 6f);
            _backgroundImage = bgObj.AddComponent<Image>();
            _backgroundImage.color = backgroundColor;
            _backgroundImage.raycastTarget = false;
        }

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(root.transform, false);
        timeDisplayText = textObj.AddComponent<Text>();
        timeDisplayText.text = GetTimeString();
        timeDisplayText.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeDisplayText.fontSize = fontSize;
        timeDisplayText.fontStyle = FontStyle.Bold;
        timeDisplayText.color = textColor;
        timeDisplayText.alignment = TextAnchor.MiddleCenter;
        timeDisplayText.raycastTarget = false;
        timeDisplayText.horizontalOverflow = HorizontalWrapMode.Overflow;
        timeDisplayText.verticalOverflow = VerticalWrapMode.Overflow;
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    static Material _unlitUiMaterial;

    /// <summary>
    /// Отключает тени на World Space Canvas — часы не отбрасывают тень на стену
    /// и не получают тени от других источников света.
    /// </summary>
    void DisableShadowsOnWorldSpaceCanvas(GameObject canvasRoot)
    {
        foreach (var r in canvasRoot.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        var unlitShader = Shader.Find("UI/Default") ?? Shader.Find("Legacy/UI/Default");
        if (unlitShader != null)
        {
            if (_unlitUiMaterial == null)
                _unlitUiMaterial = new Material(unlitShader);
            foreach (var g in canvasRoot.GetComponentsInChildren<Graphic>(true))
                g.material = _unlitUiMaterial;
        }
    }

    void CreateDefaultDisplay()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("GameTimeCanvas");
            var c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            canvas = c;
        }

        GameObject root = new GameObject("TimeDisplay");
        root.transform.SetParent(canvas.transform, false);
        _timeRect = root.AddComponent<RectTransform>();

        if (useBackground)
        {
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(root.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-12f, -8f);
            bgRect.offsetMax = new Vector2(12f, 8f);
            _backgroundImage = bgObj.AddComponent<Image>();
            _backgroundImage.color = backgroundColor;
            _backgroundImage.raycastTarget = false;
        }

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(root.transform, false);
        timeDisplayText = textObj.AddComponent<Text>();
        timeDisplayText.text = GetTimeString();
        timeDisplayText.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeDisplayText.fontSize = fontSize;
        timeDisplayText.fontStyle = FontStyle.Bold;
        timeDisplayText.color = textColor;
        timeDisplayText.alignment = TextAnchor.MiddleCenter;
        timeDisplayText.raycastTarget = false;
        timeDisplayText.horizontalOverflow = HorizontalWrapMode.Overflow;
        timeDisplayText.verticalOverflow = VerticalWrapMode.Overflow;
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        float width = Mathf.Max(140f, fontSize * 3.2f);
        _timeRect.sizeDelta = new Vector2(width, fontSize + 24f);
        ApplyPosition(_timeRect, screenPosition, offsetPixels);
    }

    static void ApplyPosition(RectTransform rect, ScreenPosition pos, Vector2 offset)
    {
        switch (pos)
        {
            case ScreenPosition.TopLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                break;
            case ScreenPosition.TopCenter:
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                break;
            case ScreenPosition.TopRight:
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                break;
            case ScreenPosition.BottomLeft:
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                break;
            case ScreenPosition.BottomCenter:
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                break;
            case ScreenPosition.BottomRight:
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                break;
        }
        rect.anchoredPosition = offset;
    }

    bool AreAllRequiredDoorsOpen()
    {
        if (doorsToOpen == null || doorsToOpen.Length == 0) return true;
        for (int i = 0; i < doorsToOpen.Length; i++)
        {
            if (doorsToOpen[i] != null && !doorsToOpen[i].IsOpen)
                return false;
        }
        return true;
    }

    /// <summary> Текущее время в формате "HH:MM" (24 часа). </summary>
    public string GetTimeString()
    {
        int h = Mathf.FloorToInt(_currentTimeHours) % 24;
        int m = Mathf.Clamp(Mathf.FloorToInt((_currentTimeHours - h) * 60f), 0, 59);
        return h.ToString("D2") + ":" + m.ToString("D2");
    }

    /// <summary> Текущее время в часах (0–24). </summary>
    public float CurrentTimeHours => _currentTimeHours;

    /// <summary> Сколько игровых часов проходит за 1 реальную секунду. </summary>
    public float HoursPerRealSecond => hoursPerRealSecond;

    /// <summary> Установить время в часах (0–24). </summary>
    public void SetTimeHours(float hours)
    {
        _currentTimeHours = hours % 24f;
        if (_currentTimeHours < 0f) _currentTimeHours += 24f;
    }

    /// <summary> Обновить настройки отображения из инспектора (размер шрифта, позиция и т.д.). Вызови после смены настроек в редакторе. </summary>
    public void RefreshDisplaySettings()
    {
        ApplyDisplaySettings();
    }
}
