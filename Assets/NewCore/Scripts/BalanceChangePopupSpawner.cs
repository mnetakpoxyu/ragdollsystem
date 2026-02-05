using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// При изменении баланса над кассой (в мире) появляется поп-ап «+25» / «-30», поднимается вверх и исчезает.
/// Повесь на любой активный объект (например PlayerBalance). Обязательно укажи «Якорь (касса)» — объект кассы.
/// </summary>
[AddComponentMenu("NewCore/Balance Change Popup Spawner")]
public class BalanceChangePopupSpawner : MonoBehaviour
{
    [Header("Позиция в мире")]
    [Tooltip("Объект кассы — над ним появляется плюс/минус. Перетащи сюда кассу (тот же объект, что с BalanceDisplayTarget).")]
    [SerializeField] Transform popupAnchor;
    [Tooltip("Высота появления над кассой (метры).")]
    [SerializeField] float heightOffset = 0.5f;

    [Header("Анимация")]
    [Tooltip("На сколько метров поп-ап поднимается вверх за время жизни.")]
    [SerializeField] float floatUpDistance = 0.4f;
    [Tooltip("Время жизни поп-апа до исчезновения (сек).")]
    [SerializeField] float lifetime = 1.4f;
    [Tooltip("Время за которое поп-ап полностью исчезает (фейд в конце).")]
    [SerializeField] float fadeOutDuration = 0.5f;
    [Tooltip("Время появления (плавное появление в начале).")]
    [SerializeField] float fadeInDuration = 0.15f;

    [Header("Внешний вид")]
    [Tooltip("Размер текста в мире (масштаб канваса в метрах).")]
    [SerializeField] float worldScale = 0.004f;
    [Tooltip("Цвет для прироста (+).")]
    [SerializeField] Color colorPlus = new Color(0.2f, 0.9f, 0.35f, 1f);
    [Tooltip("Цвет для списания (-).")]
    [SerializeField] Color colorMinus = new Color(0.95f, 0.3f, 0.25f, 1f);
    [Tooltip("Размер шрифта.")]
    [SerializeField] int fontSize = 42;

    Transform _anchor;
    Camera _mainCam;

    void Awake()
    {
        _anchor = popupAnchor != null ? popupAnchor : transform;
        _mainCam = Camera.main;
    }

    void OnEnable()
    {
        PlayerBalance.OnBalanceChanged += OnBalanceChanged;
    }

    void OnDisable()
    {
        PlayerBalance.OnBalanceChanged -= OnBalanceChanged;
    }

    void OnBalanceChanged(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        if (_anchor == null) _anchor = popupAnchor != null ? popupAnchor : transform;
        if (_mainCam == null) _mainCam = Camera.main;
        StartCoroutine(SpawnAndAnimatePopup(delta));
    }

    IEnumerator SpawnAndAnimatePopup(float amount)
    {
        Vector3 startPos = _anchor.position + Vector3.up * heightOffset;
        bool isPlus = amount > 0f;
        string text = isPlus ? $"+{Mathf.RoundToInt(amount)}" : $"{Mathf.RoundToInt(amount)}";
        Color color = isPlus ? colorPlus : colorMinus;

        var canvasObj = new GameObject("BalanceChangePopup");
        canvasObj.transform.position = startPos;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = _mainCam;
        canvas.sortingOrder = 110;
        canvasObj.AddComponent<CanvasScaler>();

        var rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 80f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * worldScale;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        var textComp = textObj.AddComponent<Text>();
        textComp.text = text;
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComp.fontSize = fontSize;
        textComp.fontStyle = FontStyle.Bold;
        textComp.color = color;
        textComp.alignment = TextAnchor.MiddleCenter;
        textComp.raycastTarget = false;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        float elapsed = 0f;
        float fadeStart = Mathf.Max(0f, lifetime - fadeOutDuration);

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            canvasObj.transform.position = startPos + Vector3.up * (floatUpDistance * t);
            if (_mainCam != null)
                canvasObj.transform.rotation = Quaternion.LookRotation(canvasObj.transform.position - _mainCam.transform.position);

            float alpha = 1f;
            if (elapsed < fadeInDuration)
                alpha = elapsed / fadeInDuration;
            else if (elapsed > fadeStart)
                alpha = 1f - (elapsed - fadeStart) / fadeOutDuration;
            color.a = alpha;
            textComp.color = color;

            yield return null;
        }

        Destroy(canvasObj);
    }
}
