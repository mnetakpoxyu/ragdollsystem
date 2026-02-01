using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одно игровое место за столом. При наведении курсора весь стол обводится зелёной обводкой (пока смотришь).
/// Вешай на стол или на дочерний объект с коллайдером.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ComputerSpot : MonoBehaviour
{
    [Header("Обводка при наведении")]
    [Tooltip("Корень стола — обводка вокруг всего этого объекта. Пусто — родитель этого объекта или сам объект.")]
    [SerializeField] Transform highlightTarget;
    [Tooltip("Цвет обводки вокруг стола (свободное место).")]
    [SerializeField] Color outlineColor = new Color(0.15f, 1f, 0.4f, 1f);
    [Tooltip("Цвет обводки когда место занято.")]
    [SerializeField] Color occupiedOutlineColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [Tooltip("Толщина обводки в метрах — ровная скорлупа вокруг стола.")]
    [SerializeField, Range(0.02f, 0.25f)] float outlineWidth = 0.08f;

    [Header("Место для посадки")]
    [Tooltip("Стул рядом с этим столом — сюда придёт NPC и сядет. Перетащи объект стула сюда.")]
    [SerializeField] Transform chair;

    [Header("Оплата за игру")]
    [Tooltip("Игровое время (GameTime). Пусто — ищется автоматически в сцене.")]
    [SerializeField] GameTime gameTime;

    [Header("Таймер над клиентом")]
    [Tooltip("Высота таймера над головой клиента (метры).")]
    [SerializeField] float timerHeightOffset = 1.2f;
    [Tooltip("Масштаб таймера в мире.")]
    [SerializeField] float timerWorldScale = 0.008f;
    [Tooltip("Шрифт таймера. Если не задан — используется стандартный.")]
    [SerializeField] Font timerFont;
    [Tooltip("Размер шрифта таймера.")]
    [SerializeField] int timerFontSize = 28;
    [Tooltip("Цвет текста таймера.")]
    [SerializeField] Color timerTextColor = Color.white;
    [Tooltip("Цвет обводки текста таймера.")]
    [SerializeField] Color timerOutlineColor = Color.black;

    [Header("Состояние места")]
    [Tooltip("Занято ли место (выставляется автоматически при посадке NPC).")]
    [SerializeField] bool isOccupied;

    static Shader _outlineShader;
    static Shader OutlineShader => _outlineShader != null ? _outlineShader : (_outlineShader = Shader.Find("NewCore/Outline Contour"));

    Renderer[] _outlineRenderers;
    Material _outlineMaterial;
    bool _highlighted;
    ClientNPC _seatedClient;
    float _sessionStartTimeHours;
    float _sessionDurationHours;
    Canvas _timerCanvas;
    Text _timerText;
    Camera _mainCam;

    void Awake()
    {
        Transform tableRoot = highlightTarget != null ? highlightTarget : (transform.parent != null ? transform.parent : transform);
        Renderer[] sourceRenderers = tableRoot.GetComponentsInChildren<Renderer>(true);
        var outlineList = new System.Collections.Generic.List<Renderer>();

        if (OutlineShader == null)
        {
            Debug.LogWarning("ComputerSpot: шейдер 'NewCore/Outline Contour' не найден. Обводка отключена.");
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
            if (r is MeshRenderer mr)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
                mesh = smr.sharedMesh;

            if (mesh == null) continue;

            GameObject outlineGo = new GameObject("Outline");
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

    void Start()
    {
        if (gameTime == null)
            gameTime = FindObjectOfType<GameTime>();
        _mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (_mainCam == null)
            _mainCam = Camera.main;
        if (!isOccupied || _seatedClient == null || gameTime == null) return;

        float elapsed = GetElapsedHours();
        if (elapsed >= _sessionDurationHours)
        {
            EndSession();
            return;
        }

        // Таймер только когда клиент уже сел за стол
        if (_seatedClient.CurrentState == ClientNPC.State.SittingAtSeat)
        {
            if (_timerCanvas == null)
                CreateTimerAboveClient();

            if (_timerCanvas != null && _timerText != null)
            {
                float remainingHours = _sessionDurationHours - elapsed;
                int h = Mathf.FloorToInt(remainingHours);
                int m = Mathf.Clamp(Mathf.FloorToInt((remainingHours - h) * 60f), 0, 59);
                _timerText.text = string.Format("{0}:{1:D2}", h, m);

                _timerCanvas.transform.position = _seatedClient.transform.position + Vector3.up * timerHeightOffset;
                if (_mainCam != null)
                    _timerCanvas.transform.rotation = Quaternion.LookRotation(_timerCanvas.transform.position - _mainCam.transform.position);
            }
        }
        else if (_timerCanvas != null)
        {
            Destroy(_timerCanvas.gameObject);
            _timerCanvas = null;
            _timerText = null;
        }
    }

    float GetElapsedHours()
    {
        float now = gameTime.CurrentTimeHours;
        float elapsed = now - _sessionStartTimeHours;
        if (elapsed < 0f) elapsed += 24f;
        return elapsed;
    }

    void EndSession()
    {
        DestroyTimer();
        // Оплата уже получена при взаимодействии E у стойки
        if (_seatedClient != null)
        {
            Destroy(_seatedClient.gameObject);
            // Спавним нового NPC на замену
            ClientNPCSpawner.Instance?.TrySpawn(oneLeaving: true);
        }

        _seatedClient = null;
        isOccupied = false;
        SetHighlight(_highlighted);
    }

    void CreateTimerAboveClient()
    {
        var canvasObj = new GameObject("ClientTimerCanvas");
        _timerCanvas = canvasObj.AddComponent<Canvas>();
        _timerCanvas.renderMode = RenderMode.WorldSpace;
        _timerCanvas.worldCamera = _mainCam;
        _timerCanvas.sortingOrder = 50;

        canvasObj.AddComponent<CanvasScaler>();
        var rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 40f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * timerWorldScale;

        var textObj = new GameObject("TimerText");
        textObj.transform.SetParent(canvasObj.transform, false);
        _timerText = textObj.AddComponent<Text>();
        _timerText.text = "0:00";
        // Используем шрифт из настроек или стандартный
        _timerText.font = timerFont != null ? timerFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _timerText.fontSize = timerFontSize;
        _timerText.fontStyle = FontStyle.Bold;
        _timerText.color = timerTextColor;
        _timerText.alignment = TextAnchor.MiddleCenter;
        _timerText.raycastTarget = false;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = timerOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void DestroyTimer()
    {
        if (_timerCanvas != null)
        {
            Destroy(_timerCanvas.gameObject);
            _timerCanvas = null;
            _timerText = null;
        }
    }

    void OnDestroy()
    {
        DestroyTimer();
        if (_outlineMaterial != null)
            Destroy(_outlineMaterial);
    }

    public void SetHighlight(bool on)
    {
        _highlighted = on;
        if (_outlineRenderers == null || _outlineMaterial == null) return;
        _outlineMaterial.SetColor("_OutlineColor", isOccupied ? occupiedOutlineColor : outlineColor);
        foreach (Renderer r in _outlineRenderers)
        {
            if (r != null)
                r.enabled = on;
        }
    }

    /// <summary> Занято ли место (при посадке NPC — true, при уходе — false). </summary>
    public bool IsOccupied
    {
        get => isOccupied;
        set => isOccupied = value;
    }

    /// <summary> Посадить клиента за этот стол. NPC пойдёт к стулу и сядет. Возвращает true, если место было свободно. </summary>
    public bool SeatClient(ClientNPC npc)
    {
        if (npc == null || isOccupied || chair == null || !npc.HasOrdered) return false;
        npc.GoSitAt(chair);
        _seatedClient = npc;
        _sessionDurationHours = npc.RequestedSessionHours;
        if (gameTime == null)
            gameTime = FindObjectOfType<GameTime>();
        _sessionStartTimeHours = gameTime != null ? gameTime.CurrentTimeHours : 0f;
        isOccupied = true;
        // Передаём NPC данные сессии для планирования реплик в игровом времени
        if (gameTime != null && npc.HasRecordedPhrase)
            npc.SetSessionInfo(gameTime, _sessionStartTimeHours, _sessionDurationHours);
        SetHighlight(_highlighted); // обновить цвет обводки на красный (занято)

        // Спавним нового NPC (кто-то сел — освободилось место в очереди)
        ClientNPCSpawner.Instance?.TrySpawn();

        return true;
    }
}
