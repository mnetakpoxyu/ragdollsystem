using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Мини-игра починки: ввести трёхзначный код. Цель показывается сверху, три цифры внизу — A/D выбор позиции, W/S значение. Q — отмена.
/// </summary>
public class RepairMinigameUI : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [Header("Блокировка движения")]
    [Tooltip("При открытии: LockInput() + отключение PlayerController, чтобы персонаж не двигался и камера не крутилась. При закрытии — всё включается обратно.")]

    [Header("Внешний вид")]
    [SerializeField] Font font;
    [SerializeField] int targetFontSize = 24;
    [SerializeField] int digitFontSize = 72;
    [SerializeField] Color digitColor = Color.white;
    [SerializeField] Color selectedColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] Color successColor = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField] float successShowDuration = 1f;

    Canvas _canvas;
    Text _targetText;
    Text[] _digitTexts = new Text[3];
    int _targetCode;
    int[] _digits = new int[3];
    int _selectedIndex;
    bool _successShown;
    float _successTimer;
    System.Action _onSuccess;
    System.Action _onCancel;
    List<MonoBehaviour> _disabledForMinigame = new List<MonoBehaviour>();

    void Awake()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateUI();
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    void CreateUI()
    {
        var canvasObj = new GameObject("RepairMinigameCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        float centerY = 0.4f;
        float digitSpacing = 140f;

        var targetObj = new GameObject("TargetText");
        targetObj.transform.SetParent(canvasObj.transform, false);
        _targetText = targetObj.AddComponent<Text>();
        _targetText.font = font;
        _targetText.fontSize = targetFontSize;
        _targetText.color = Color.white;
        _targetText.alignment = TextAnchor.MiddleCenter;
        _targetText.raycastTarget = false;
        var targetRect = targetObj.GetComponent<RectTransform>();
        targetRect.anchorMin = new Vector2(0.5f, 0.5f);
        targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.pivot = new Vector2(0.5f, 0.5f);
        targetRect.anchoredPosition = new Vector2(0, 120);
        targetRect.sizeDelta = new Vector2(400, 50);

        for (int i = 0; i < 3; i++)
        {
            var textObj = new GameObject("Digit" + i);
            textObj.transform.SetParent(canvasObj.transform, false);
            var t = textObj.AddComponent<Text>();
            t.font = font;
            t.fontSize = digitFontSize;
            t.color = digitColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            float x = (i - 1) * digitSpacing;
            textRect.anchoredPosition = new Vector2(x, -20);
            textRect.sizeDelta = new Vector2(100, 90);
            _digitTexts[i] = t;
        }

        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(canvasObj.transform, false);
        var hint = hintObj.AddComponent<Text>();
        hint.font = font;
        hint.fontSize = 16;
        hint.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        hint.text = "A / D — выбор цифры   W / S — значение   Q — отмена";
        hint.alignment = TextAnchor.MiddleCenter;
        hint.raycastTarget = false;
        var hintRect = hintObj.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0.5f);
        hintRect.anchorMax = new Vector2(0.5f, 0.5f);
        hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0, -100);
        hintRect.sizeDelta = new Vector2(500, 30);
    }

    /// <summary> Открыть мини-игру. targetCode — какой код нужно ввести (100–999). onSuccess/onCancel — колбэки по завершении. </summary>
    public void Open(int targetCode, System.Action onSuccess, System.Action onCancel)
    {
        _targetCode = Mathf.Clamp(targetCode, 100, 999);
        _onSuccess = onSuccess;
        _onCancel = onCancel;
        _digits[0] = Random.Range(0, 10);
        _digits[1] = Random.Range(0, 10);
        _digits[2] = Random.Range(0, 10);
        _selectedIndex = 1;
 // центральная по умолчанию
        _successShown = false;
        _successTimer = 0f;

        _targetText.text = "Введите код: " + _targetCode;
        RefreshDigitDisplay();
        UpdateSelectionHighlight();

        _canvas.gameObject.SetActive(true);
        IsActive = true;
        _disabledForMinigame.Clear();

        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.LockInput();

        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var mb in all)
        {
            if (mb != null && mb.GetType().Name == "PlayerController" && mb.enabled)
            {
                mb.enabled = false;
                _disabledForMinigame.Add(mb);
                break;
            }
        }
    }

    void RefreshDigitDisplay()
    {
        for (int i = 0; i < 3; i++)
        {
            _digitTexts[i].text = _digits[i].ToString();
            _digitTexts[i].color = _successShown ? successColor : (i == _selectedIndex ? selectedColor : digitColor);
        }
    }

    void UpdateSelectionHighlight()
    {
        for (int i = 0; i < 3; i++)
            _digitTexts[i].color = _successShown ? successColor : (i == _selectedIndex ? selectedColor : digitColor);
    }

    int GetCurrentCode()
    {
        return _digits[0] * 100 + _digits[1] * 10 + _digits[2];
    }

    void Close(bool success)
    {
        _canvas.gameObject.SetActive(false);
        IsActive = false;
        foreach (var mb in _disabledForMinigame)
        {
            if (mb != null) mb.enabled = true;
        }
        _disabledForMinigame.Clear();
        if (PlayerInputManager.Instance != null)
            PlayerInputManager.Instance.UnlockInput();
        if (success && _onSuccess != null) _onSuccess();
        if (!success && _onCancel != null) _onCancel();
    }

    void Update()
    {
        if (!IsActive || _canvas == null || !_canvas.gameObject.activeSelf) return;

        if (_successShown)
        {
            _successTimer += Time.deltaTime;
            if (_successTimer >= successShowDuration)
                Close(true);
            return;
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.qKey.wasPressedThisFrame)
        {
            Close(false);
            return;
        }
        if (kb.aKey.wasPressedThisFrame)
        {
            _selectedIndex = (_selectedIndex - 1 + 3) % 3;
            UpdateSelectionHighlight();
        }
        if (kb.dKey.wasPressedThisFrame)
        {
            _selectedIndex = (_selectedIndex + 1) % 3;
            UpdateSelectionHighlight();
        }
        if (kb.wKey.wasPressedThisFrame)
        {
            _digits[_selectedIndex] = (_digits[_selectedIndex] + 1) % 10;
            RefreshDigitDisplay();
            if (GetCurrentCode() == _targetCode) { _successShown = true; RefreshDigitDisplay(); }
        }
        if (kb.sKey.wasPressedThisFrame)
        {
            _digits[_selectedIndex] = (_digits[_selectedIndex] - 1 + 10) % 10;
            RefreshDigitDisplay();
            if (GetCurrentCode() == _targetCode) { _successShown = true; RefreshDigitDisplay(); }
        }
    }
}
