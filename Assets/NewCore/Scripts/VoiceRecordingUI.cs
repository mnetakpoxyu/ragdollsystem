using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// UI для записи голоса клиента. Показывает окно с кнопками записи/остановки.
/// Блокирует управление игроком во время записи.
/// </summary>
public class VoiceRecordingUI : MonoBehaviour
{
    [Header("UI элементы")]
    [Tooltip("Панель записи (создаётся автоматически если не задана).")]
    [SerializeField] GameObject recordingPanel;
    [Tooltip("Текст статуса записи.")]
    [SerializeField] Text statusText;
    [Tooltip("Кнопка начала записи.")]
    [SerializeField] Button recordButton;
    [Tooltip("Кнопка остановки записи.")]
    [SerializeField] Button stopButton;
    [Tooltip("Кнопка отмены.")]
    [SerializeField] Button cancelButton;

    [Header("Настройки")]
    [Tooltip("Шрифт для UI (если не задан, используется стандартный).")]
    [SerializeField] Font uiFont;

    VoiceRecorder _voiceRecorder;
    Action<AudioClip> _onRecordingComplete;
    Action _onRecordingCancelled;
    bool _isUIActive;

    void Awake()
    {
        // Создаём VoiceRecorder если его нет
        _voiceRecorder = GetComponent<VoiceRecorder>();
        if (_voiceRecorder == null)
            _voiceRecorder = gameObject.AddComponent<VoiceRecorder>();

        // Создаём UI если не задан
        if (recordingPanel == null)
            CreateRecordingUI();

        // Настраиваем кнопки
        if (recordButton != null)
            recordButton.onClick.AddListener(OnRecordButtonClicked);
        if (stopButton != null)
            stopButton.onClick.AddListener(OnStopButtonClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);

        HideUI();
    }

    void CreateRecordingUI()
    {
        // Создаём Canvas
        var canvasObj = new GameObject("VoiceRecordingCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Поверх всего

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Затемнённый фон
        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);
        var bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Панель записи
        var panelObj = new GameObject("RecordingPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        var panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500, 300);
        recordingPanel = panelObj;

        Font font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Заголовок
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = "Запись голоса клиента";
        titleText.font = font;
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(-40, 50);
        titleRect.anchoredPosition = new Vector2(0, -20);

        // Текст статуса
        var statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(panelObj.transform, false);
        statusText = statusObj.AddComponent<Text>();
        statusText.text = "Нажмите 'Записать' чтобы начать";
        statusText.font = font;
        statusText.fontSize = 20;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
        var statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0.5f);
        statusRect.anchorMax = new Vector2(1, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.sizeDelta = new Vector2(-40, 60);
        statusRect.anchoredPosition = new Vector2(0, 20);

        // Кнопка записи
        recordButton = CreateButton("RecordButton", "Записать", new Color(0.2f, 0.8f, 0.2f), font, panelObj.transform);
        var recordRect = recordButton.GetComponent<RectTransform>();
        recordRect.anchorMin = new Vector2(0.5f, 0);
        recordRect.anchorMax = new Vector2(0.5f, 0);
        recordRect.pivot = new Vector2(0.5f, 0);
        recordRect.sizeDelta = new Vector2(200, 50);
        recordRect.anchoredPosition = new Vector2(0, 80);

        // Кнопка остановки
        stopButton = CreateButton("StopButton", "Остановить", new Color(0.8f, 0.2f, 0.2f), font, panelObj.transform);
        var stopRect = stopButton.GetComponent<RectTransform>();
        stopRect.anchorMin = new Vector2(0.5f, 0);
        stopRect.anchorMax = new Vector2(0.5f, 0);
        stopRect.pivot = new Vector2(0.5f, 0);
        stopRect.sizeDelta = new Vector2(200, 50);
        stopRect.anchoredPosition = new Vector2(0, 80);
        stopButton.gameObject.SetActive(false);

        // Кнопка отмены
        cancelButton = CreateButton("CancelButton", "Отмена", new Color(0.5f, 0.5f, 0.5f), font, panelObj.transform);
        var cancelRect = cancelButton.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.5f, 0);
        cancelRect.anchorMax = new Vector2(0.5f, 0);
        cancelRect.pivot = new Vector2(0.5f, 0);
        cancelRect.sizeDelta = new Vector2(200, 50);
        cancelRect.anchoredPosition = new Vector2(0, 20);

        canvasObj.transform.SetParent(transform, false);
    }

    Button CreateButton(string name, string text, Color color, Font font, Transform parent)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        
        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = color;
        
        var button = btnObj.AddComponent<Button>();
        button.targetGraphic = btnImage;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var btnText = textObj.AddComponent<Text>();
        btnText.text = text;
        btnText.font = font;
        btnText.fontSize = 22;
        btnText.fontStyle = FontStyle.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;
        
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    void OnRecordButtonClicked()
    {
        // Подписываемся на событие готовности микрофона
        _voiceRecorder.OnMicrophoneReady += OnMicrophoneReady;

        _voiceRecorder.StartRecording();
        recordButton.gameObject.SetActive(false);

        // Показываем статус "Подготовка..." пока микрофон инициализируется
        if (statusText != null)
            statusText.text = "Подготовка микрофона...";
    }

    void OnMicrophoneReady()
    {
        // Отписываемся от события
        _voiceRecorder.OnMicrophoneReady -= OnMicrophoneReady;

        // Теперь можно говорить!
        stopButton.gameObject.SetActive(true);
        if (statusText != null)
            statusText.text = "Идёт запись... Говорите в микрофон!";
    }

    void OnStopButtonClicked()
    {
        AudioClip clip = _voiceRecorder.StopRecording();
        HideUI();
        _onRecordingComplete?.Invoke(clip);
    }

    void OnCancelButtonClicked()
    {
        // Отписываемся от события если ещё подписаны
        _voiceRecorder.OnMicrophoneReady -= OnMicrophoneReady;

        _voiceRecorder.CancelRecording();
        HideUI();
        _onRecordingCancelled?.Invoke();
    }

    /// <summary>
    /// Показать UI записи голоса.
    /// </summary>
    /// <param name="onComplete">Callback при завершении записи (передаётся AudioClip).</param>
    /// <param name="onCancel">Callback при отмене записи.</param>
    public void ShowUI(Action<AudioClip> onComplete, Action onCancel = null)
    {
        if (recordingPanel != null)
            recordingPanel.SetActive(true);

        _onRecordingComplete = onComplete;
        _onRecordingCancelled = onCancel;
        _isUIActive = true;

        // Сброс UI в начальное состояние
        if (recordButton != null)
            recordButton.gameObject.SetActive(true);
        if (stopButton != null)
            stopButton.gameObject.SetActive(false);
        if (statusText != null)
            statusText.text = "Нажмите 'Записать' чтобы начать";

        // Блокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Скрыть UI записи голоса.
    /// </summary>
    public void HideUI()
    {
        if (recordingPanel != null)
            recordingPanel.SetActive(false);

        _isUIActive = false;

        // Возвращаем курсор в игровой режим
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public bool IsUIActive => _isUIActive;

    void OnDestroy()
    {
        if (recordButton != null)
            recordButton.onClick.RemoveListener(OnRecordButtonClicked);
        if (stopButton != null)
            stopButton.onClick.RemoveListener(OnStopButtonClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
    }
}
