using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Повесь на объект с коллайдером. Когда игрок наводит прицел на этот объект,
/// над ним появляется небольшой текст с балансом (и опционально иконкой).
/// Баланс берётся из PlayerBalance (компонент на игроке или в сцене).
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("NewCore/Balance Display Target")]
public class BalanceDisplayTarget : MonoBehaviour
{
    [Header("Позиция подсказки")]
    [Tooltip("Точка, над которой показывается баланс. Пусто — центр этого объекта.")]
    [SerializeField] Transform anchor;
    [Tooltip("Высота подсказки над якорем (метры).")]
    [SerializeField] float heightOffset = 0.5f;

    [Header("Внешний вид")]
    [Tooltip("Формат строки. {0} — число баланса. Пример: \"Баланс: {0} ₽\"")]
    [SerializeField] string format = "{0} ₽";
    [Tooltip("Иконка баланса (опционально). Если задана — отображается слева от текста.")]
    [SerializeField] Sprite balanceIcon;
    [Tooltip("Размер мирового канваса (ширина панели в метрах).")]
    [SerializeField] float panelWorldWidth = 0.8f;
    [Tooltip("Шрифт подсказки. Пусто — встроенный Unity.")]
    [SerializeField] Font customFont;

    [Header("Дистанция")]
    [Tooltip("Показывать подсказку только если игрок ближе этой дистанции.")]
    [SerializeField] float maxShowDistance = 5f;

    [Header("Обводка при наведении")]
    [Tooltip("Объект, который обводится. Пусто — только этот объект (и его дочерние меши), не родитель.")]
    [SerializeField] Transform outlineTarget;
    [Tooltip("Цвет золотой обводки при наведении прицела.")]
    [SerializeField] Color outlineColor = new Color(0.95f, 0.76f, 0.2f, 1f);
    [Tooltip("Толщина обводки в метрах.")]
    [SerializeField, Range(0.02f, 0.25f)] float outlineWidth = 0.08f;

    static Shader _outlineShader;
    static Shader OutlineShader => _outlineShader != null ? _outlineShader : (_outlineShader = Shader.Find("NewCore/Outline Contour"));

    Renderer[] _outlineRenderers;
    Material _outlineMaterial;
    Canvas _canvas;
    GameObject _panel;
    Text _balanceText;
    Image _iconImage;
    bool _aimedAt;
    Camera _mainCam;

    void Awake()
    {
        CreateOutline();
    }

    void Start()
    {
        _mainCam = Camera.main;
        CreatePopup();
        if (_panel != null)
            _panel.SetActive(false);
    }

    void CreateOutline()
    {
        // outlineTarget пусто → только этот объект (дочерний), не весь родитель
        Transform root = outlineTarget != null ? outlineTarget : transform;
        var sourceRenderers = root.GetComponentsInChildren<Renderer>(true);
        var outlineList = new System.Collections.Generic.List<Renderer>();

        if (OutlineShader == null)
        {
            Debug.LogWarning("BalanceDisplayTarget: шейдер 'NewCore/Outline Contour' не найден. Обводка отключена.");
            _outlineRenderers = new Renderer[0];
            return;
        }

        _outlineMaterial = new Material(OutlineShader);
        _outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
        _outlineMaterial.SetColor("_OutlineColor", outlineColor);
        _outlineMaterial.SetFloat("_RGBSpeed", 0f);

        foreach (Renderer r in sourceRenderers)
        {
            if (r == null) continue;
            Mesh mesh = null;
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
                mesh = smr.sharedMesh;

            if (mesh == null) continue;

            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(r.transform, false);
            outlineGo.transform.localPosition = Vector3.zero;
            outlineGo.transform.localRotation = Quaternion.identity;
            outlineGo.transform.localScale = Vector3.one;
            outlineGo.layer = r.gameObject.layer;

            var outlineMf = outlineGo.AddComponent<MeshFilter>();
            outlineMf.sharedMesh = mesh;
            var outlineMr = outlineGo.AddComponent<MeshRenderer>();
            outlineMr.sharedMaterial = _outlineMaterial;
            outlineMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineMr.receiveShadows = false;
            outlineMr.enabled = false;
            outlineList.Add(outlineMr);
        }

        _outlineRenderers = outlineList.ToArray();
    }

    void SetOutline(bool on)
    {
        if (_outlineRenderers == null || _outlineMaterial == null) return;
        foreach (Renderer r in _outlineRenderers)
        {
            if (r != null)
                r.enabled = on;
        }
    }

    void LateUpdate()
    {
        if (_mainCam == null)
            _mainCam = Camera.main;
        if (!_aimedAt || _panel == null || !_panel.activeSelf) return;

        Transform root = anchor != null ? anchor : transform;
        Vector3 worldPos = root.position + Vector3.up * heightOffset;
        _canvas.transform.position = worldPos;

        if (_mainCam != null)
            _canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - _mainCam.transform.position);

        PlayerBalance pb = PlayerBalance.Instance;
        if (pb != null)
            _balanceText.text = string.Format(format, Mathf.RoundToInt(pb.Balance));
    }

    /// <summary>
    /// Вызывается из PlayerInteract, когда прицел наведён на этот объект (или снят).
    /// </summary>
    public void SetAimedAt(bool aimed)
    {
        if (aimed && _mainCam != null)
        {
            float dist = Vector3.Distance(_mainCam.transform.position, (anchor != null ? anchor : transform).position);
            if (dist > maxShowDistance)
                aimed = false;
        }

        _aimedAt = aimed;
        SetOutline(aimed);
        if (_panel != null)
            _panel.SetActive(aimed);
    }

    void CreatePopup()
    {
        // Canvas НЕ дочерний объекта — позиционируем вручную в LateUpdate
        var canvasObj = new GameObject("BalancePopup_Canvas");

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = _mainCam;
        _canvas.sortingOrder = 100;

        canvasObj.AddComponent<CanvasScaler>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        // Размер в UI-пикселях, scale переводит в метры
        canvasRect.sizeDelta = new Vector2(300f, 60f);
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.localScale = Vector3.one * 0.005f; // 300px * 0.005 = 1.5 м ширина

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(canvasObj.transform, false);
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        float iconWidth = 0f;
        if (balanceIcon != null)
        {
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(_panel.transform, false);
            _iconImage = iconObj.AddComponent<Image>();
            _iconImage.sprite = balanceIcon;
            _iconImage.preserveAspect = true;
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-5f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            iconWidth = 45f;
        }

        var textObj = new GameObject("BalanceText");
        textObj.transform.SetParent(_panel.transform, false);
        _balanceText = textObj.AddComponent<Text>();
        _balanceText.text = string.Format(format, 0);
        _balanceText.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _balanceText.fontSize = 32;
        _balanceText.fontStyle = FontStyle.Bold;
        _balanceText.color = Color.white;
        _balanceText.alignment = TextAnchor.MiddleCenter;
        _balanceText.raycastTarget = false;
        _balanceText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _balanceText.verticalOverflow = VerticalWrapMode.Overflow;

        // Обводка для читаемости на любом фоне
        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(iconWidth * 0.5f, 0f);
        textRect.sizeDelta = new Vector2(250f, 50f);
    }

    void OnDestroy()
    {
        if (_outlineMaterial != null)
            Destroy(_outlineMaterial);
        if (_canvas != null && _canvas.gameObject != null)
            Destroy(_canvas.gameObject);
    }
}
