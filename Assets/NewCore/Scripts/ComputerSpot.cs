using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
    [Tooltip("Цвет обводки когда клиент ждёт доставку еды (не красный).")]
    [SerializeField] Color waitingForFoodOutlineColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("Цвет обводки когда комп сломан (пустой или с клиентом ждёт починки).")]
    [SerializeField] Color brokenOutlineColor = new Color(1f, 0.5f, 0f, 1f);
    [Tooltip("Толщина обводки в метрах — ровная скорлупа вокруг стола.")]
    [SerializeField, Range(0.02f, 0.25f)] float outlineWidth = 0.08f;

    [Header("Место для посадки")]
    [Tooltip("Стул рядом с этим столом — к нему идёт NPC. Перетащи объект стула сюда.")]
    [SerializeField] Transform chair;
    [Tooltip("Дочерний объект стула: поставь точку там, где должна быть ПОПА NPC когда он сидит. Поворот = куда смотрит. Анимация «садясь» может съезжать в эту точку. Пусто — используется центр стула + смещение.")]
    [SerializeField] Transform npcSitPoint;

    [Header("Баланс места (часы и тариф)")]
    [Tooltip("Минимальное кол-во часов, которые клиент может взять за этим компом.")]
    [SerializeField] float minSessionHours = 1f;
    [Tooltip("Максимальное кол-во часов, которые клиент может взять за этим компом.")]
    [SerializeField] float maxSessionHours = 8f;
    [Tooltip("Тариф за 1 игровой час (цена за комп). Разные столы — разная цена для геймификации.")]
    [SerializeField] float pricePerHour = 50f;

    [Header("Игровое время")]
    [Tooltip("Игровое время (GameTime). Пусто — ищется автоматически в сцене.")]
    [SerializeField] GameTime gameTime;

    [Header("Таймер игровой сессии")]
    [Tooltip("Пустой объект, куда ставить таймер (позиция в мире). Создай Empty, выставь над клиентом — сюда привяжется таймер. Пусто — таймер над NPC по смещению ниже.")]
    [SerializeField] Transform timerAnchor;
    [Tooltip("Высота таймера над головой клиента (метры). Используется только если Timer Anchor не задан.")]
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

    [Header("Поломка компьютера")]
    [Tooltip("Вероятность поломки за сессию (0–1). Может не выпасть ни разу за игру.")]
    [SerializeField, Range(0f, 1f)] float breakdownChancePerSession = 0.4f;
    [Tooltip("Сколько секунд (реального времени) у админа на починку, пока клиент ждёт. Не успел — клиент уходит, комп сломан.")]
    [SerializeField] float repairTimeLimitSeconds = 45f;
    [Tooltip("Минимальная доля сессии (0–1), после которой может выпасть поломка (чтобы клиент успел сесть).")]
    [SerializeField, Range(0.05f, 0.9f)] float breakdownMinSessionElapsed = 0.1f;
    [Tooltip("Текст над головой при поломке (без восклицательного знака — выводится как есть).")]
    [SerializeField] string breakdownLabel = "Ремонт";
    [Tooltip("Размер шрифта надписи «Ремонт» (таймер сессии использует Timer Font Size).")]
    [SerializeField] int breakdownFontSize = 18;

    [Header("Возгорание компьютера (редко)")]
    [Tooltip("Вероятность возгорания за сессию (0–1). Ивент редкий, при срабатывании клиент встаёт и уходит, комп становится сломанным.")]
    [SerializeField, Range(0f, 1f)] float fireChancePerSession = 0.03f;
    [Tooltip("Минимальная доля сессии (0–1), после которой может выпасть возгорание.")]
    [SerializeField, Range(0.05f, 0.95f)] float fireMinSessionElapsed = 0.15f;
    [Tooltip("Корпус компьютера в сцене — перетащи сюда объект компа. Эффект появится в его позиции.")]
    [SerializeField] Transform fireVfxPoint;
    [Tooltip("Опционально: префаб дыма/огня. Если пусто — создаётся огненный дым в коде (идёт вверх).")]
    [SerializeField] GameObject fireVfxPrefab;
    [Tooltip("Сколько затяжек вейпа (пар в сторону компьютера) нужно для тушения. Игрок должен быть близко и направлять пар в комп.")]
    [SerializeField, Min(1)] int extinguishHitsRequired = 3;
    [Tooltip("Максимальная дистанция до компьютера для засчитывания затяжки (м).")]
    [SerializeField] float maxExtinguishDistance = 2.5f;
    [Tooltip("Угол: пар должен идти в сторону компьютера (градусы). 55 = примерно в сторону компа.")]
    [SerializeField, Range(10f, 90f)] float maxExtinguishAngleDeg = 55f;

    [Header("Доставка еды")]
    [Tooltip("Сколько секунд (реального времени) у игрока на доставку еды клиенту. Не успел — клиент уходит, возврат за сессию и бургер.")]
    [SerializeField] float foodDeliveryTimeLimitSeconds = 60f;
    [Tooltip("Текст над местом, когда клиент ушёл (попить/поесть).")]
    [SerializeField] string goneLabel = "Ушёл";
    [Tooltip("Текст над клиентом, когда он вернулся и ждёт доставку еды.")]
    [SerializeField] string waitingForFoodLabel = "Ждёт еду";
    [Tooltip("Текст над клиентом, когда он вернулся и ждёт кальян.")]
    [SerializeField] string waitingForHookahLabel = "Ждёт кальян";
    [Tooltip("Пустой объект рядом со столом (например HookahPlace). Сюда ставится кальян при доставке — он будет виден рядом со столом.")]
    [SerializeField] Transform hookahPlace;
    [Tooltip("Пустой объект на столе, куда ставится еда при доставке (бургер будет виден на столе).")]
    [SerializeField] Transform foodPlaceOnTable;
    [Tooltip("Пустой объект в месте «рта» клиента за этим столом. Дым кальяна идёт из этой точки. Синяя стрелка (Forward/Z+) задаёт направление выдувания — поверни объект так, чтобы стрелка указывала вперёд (в сторону монитора).")]
    [SerializeField] Transform hookahMouthPoint;

    static Shader _outlineShader;
    static Shader OutlineShader => _outlineShader != null ? _outlineShader : (_outlineShader = Shader.Find("NewCore/Outline Contour"));

    static readonly List<ComputerSpot> _allSpots = new List<ComputerSpot>();

    void OnEnable()
    {
        if (!_allSpots.Contains(this))
            _allSpots.Add(this);
    }

    void OnDisable()
    {
        _allSpots.Remove(this);
    }

    /// <summary> Может ли это место принять клиента (есть стул). </summary>
    public bool CanSeatClient => chair != null;

    /// <summary> Количество мест, которые могут принять клиентов (есть стул). </summary>
    public static int GetSeatableSpotCount()
    {
        int count = 0;
        for (int i = 0; i < _allSpots.Count; i++)
        {
            if (_allSpots[i] != null && _allSpots[i].CanSeatClient)
                count++;
        }
        return count;
    }

    /// <summary> Найти случайное свободное место за компьютером. null если все заняты или сломаны, или нет мест со стулом. </summary>
    public static ComputerSpot GetRandomFreeSpot()
    {
        var free = GetFreeSpotsList();
        if (free.Count == 0) return null;
        return free[Random.Range(0, free.Count)];
    }

    /// <summary> Проверить все места: если клиент уничтожен без EndSession — освободить место. Спавнер вызывает перед проверкой свободных мест. </summary>
    public static void ReconcileAllSpots()
    {
        for (int i = 0; i < _allSpots.Count; i++)
        {
            if (_allSpots[i] != null)
                _allSpots[i].ReconcileSeatedClient();
        }
    }

    /// <summary> Если место помечено как занятое, но клиент уничтожен — освободить место и очистить состояние (самовосстановление спавна). </summary>
    public void ReconcileSeatedClient()
    {
        if (!isOccupied) return;
        if (_seatedClient != null) return;
        ClearSpotBecauseClientLost();
    }

    void ClearSpotBecauseClientLost()
    {
        DestroyTimer();
        _breakdownInProgress = false;
        _breakdownRolled = false;
        ClearPlacedHookah();
        ClearPlacedFood();
        _seatedClient = null;
        isOccupied = false;
        _clientGoneForDrink = false;
        _clientGoneForFood = false;
        _clientGoneForHookah = false;
        _foodDeliveryTimeRemaining = -1f;
        SetHighlight(_highlighted);
    }

    /// <summary> Место, куда можно посадить нового клиента: свободное или сломанное (после починки). Для спавна: есть ли хотя бы одно такое место. </summary>
    public static ComputerSpot GetRandomSpotForNewClient()
    {
        var list = new List<ComputerSpot>(_allSpots.Count);
        for (int i = 0; i < _allSpots.Count; i++)
        {
            var s = _allSpots[i];
            if (s != null && s.CanSeatClient && !s.IsOccupied)
                list.Add(s);
        }
        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary> Список всех свободных мест со стулом (сломанные места не считаются свободными). </summary>
    public static List<ComputerSpot> GetFreeSpotsList()
    {
        var free = new List<ComputerSpot>(_allSpots.Count);
        for (int i = 0; i < _allSpots.Count; i++)
        {
            if (_allSpots[i] != null && !_allSpots[i].IsOccupied && !_allSpots[i].IsBroken && _allSpots[i].CanSeatClient)
                free.Add(_allSpots[i]);
        }
        return free;
    }

    /// <summary> Самое дешёвое свободное место (минимальный тариф). null если нет свободных. </summary>
    public static ComputerSpot GetCheapestFreeSpot()
    {
        var free = GetFreeSpotsList();
        if (free.Count == 0) return null;
        ComputerSpot best = free[0];
        for (int i = 1; i < free.Count; i++)
        {
            if (free[i].PricePerHour < best.PricePerHour)
                best = free[i];
        }
        return best;
    }

    /// <summary> Самое дорогое свободное место (максимальный тариф). null если нет свободных. </summary>
    public static ComputerSpot GetMostExpensiveFreeSpot()
    {
        var free = GetFreeSpotsList();
        if (free.Count == 0) return null;
        ComputerSpot best = free[0];
        for (int i = 1; i < free.Count; i++)
        {
            if (free[i].PricePerHour > best.PricePerHour)
                best = free[i];
        }
        return best;
    }

    /// <summary> Свободное место с распределением: 50% — подешевле, 50% — подороже (для баланса и разнообразия). </summary>
    public static ComputerSpot GetFreeSpotWithPriceDistribution()
    {
        var free = GetFreeSpotsList();
        if (free.Count == 0) return null;
        if (free.Count == 1) return free[0];
        var cheap = GetCheapestFreeSpot();
        var expensive = GetMostExpensiveFreeSpot();
        if (cheap == expensive) return cheap;
        return Random.value < 0.5f ? cheap : expensive;
    }

    /// <summary> Минимальное кол-во часов за этим компом. </summary>
    public float MinSessionHours => minSessionHours;
    /// <summary> Максимальное кол-во часов за этим компом. </summary>
    public float MaxSessionHours => maxSessionHours;
    /// <summary> Тариф за 1 игровой час на этом компе. </summary>
    public float PricePerHour => pricePerHour;

    Renderer[] _outlineRenderers;
    Material _outlineMaterial;
    bool _highlighted;
    ClientNPC _seatedClient;
    float _sessionStartTimeHours;
    float _sessionDurationHours;
    bool _clientGoneForDrink;
    float _remainingSessionHours;
    Canvas _timerCanvas;
    Text _timerText;
    Camera _mainCam;
    bool _clientGoneForFood;
    float _foodDeliveryTimeRemaining = -1f;
    bool _clientGoneForHookah;
    Transform _placedHookahVisual;
    Transform _placedFoodVisual;
    GameObject _registeredHookahSmoke;

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
            gameTime = FindFirstObjectByType<GameTime>();
        _mainCam = Camera.main;
        if (chair == null)
            Debug.LogWarning($"ComputerSpot на '{gameObject.name}' не имеет стула (chair). NPC не смогут сесть сюда.", this);
    }

    void LateUpdate()
    {
        ReconcileSeatedClient();

        if (_mainCam == null)
            _mainCam = Camera.main;

        // Поломка в процессе: тикаем таймер; истёк — клиент уходит, комп сломан. Пока клиент за напитком — таймер не тикает.
        if (_breakdownInProgress && !_clientGoneForDrink)
        {
            _breakdownTimerRemaining -= Time.deltaTime;
            if (_breakdownTimerRemaining <= 0f)
            {
                EndSessionDueToBreakdown();
                return;
            }
        }

        // Доставка еды: таймер; истёк — клиент уходит, возврат за сессию и бургер.
        if (_clientGoneForFood && _foodDeliveryTimeRemaining >= 0f)
        {
            _foodDeliveryTimeRemaining -= Time.deltaTime;
            if (_foodDeliveryTimeRemaining <= 0f)
            {
                EndSessionDueToFoodTimeout();
                return;
            }
        }

        if (!isOccupied || _seatedClient == null || gameTime == null) return;

        float elapsed = GetElapsedHours();
        if (!_clientGoneForDrink && !_clientGoneForFood && !_clientGoneForHookah)
        {
            if (elapsed >= _sessionDurationHours)
            {
                EndSession();
                return;
            }
            // Случайная поломка за сессию. Пока клиент за напитком/едой — не ломаем.
            if (!_breakdownRolled && _seatedClient.CurrentState == ClientNPC.State.SittingAtSeat && elapsed >= _sessionDurationHours * breakdownMinSessionElapsed)
            {
                _breakdownRolled = true;
                if (Random.value < breakdownChancePerSession)
                {
                    _breakdownInProgress = true;
                    _breakdownTimerRemaining = repairTimeLimitSeconds;
                }
            }

            // Редкий ивент: возгорание компьютера. Клиент уходит, комп становится сломанным, появляется дым/огонь.
            if (!_fireRolled && !_breakdownInProgress && _seatedClient.CurrentState == ClientNPC.State.SittingAtSeat && elapsed >= _sessionDurationHours * fireMinSessionElapsed)
            {
                _fireRolled = true;
                if (Random.value < fireChancePerSession)
                {
                    TriggerFireEvent();
                    return;
                }
            }
        }

        // Надпись над местом: «Ушёл», «Ждёт еду» или таймер сессии
        bool showLabel = _seatedClient.CurrentState == ClientNPC.State.SittingAtSeat || _clientGoneForDrink || _clientGoneForFood || _clientGoneForHookah;
        if (showLabel)
        {
            if (_timerCanvas == null)
                CreateTimerAboveClient();

            if (_timerCanvas != null && _timerText != null)
            {
                Vector3 labelPos;
                if (_clientGoneForDrink)
                {
                    _timerText.text = goneLabel;
                    _timerText.fontSize = timerFontSize;
                    _timerText.color = timerTextColor;
                    labelPos = GetSpotLabelPosition();
                }
                else if (_clientGoneForFood)
                {
                    if (_seatedClient.CurrentState == ClientNPC.State.SittingAtSeat)
                    {
                        _timerText.text = waitingForFoodLabel;
                        _timerText.fontSize = timerFontSize;
                        _timerText.color = timerTextColor;
                        labelPos = timerAnchor != null ? timerAnchor.position : _seatedClient.transform.position + Vector3.up * timerHeightOffset;
                    }
                    else
                    {
                        _timerText.text = goneLabel;
                        _timerText.fontSize = timerFontSize;
                        _timerText.color = timerTextColor;
                        labelPos = GetSpotLabelPosition();
                    }
                }
                else if (_clientGoneForHookah)
                {
                    if (_seatedClient.CurrentState == ClientNPC.State.SittingAtSeat)
                    {
                        _timerText.text = waitingForHookahLabel;
                        _timerText.fontSize = timerFontSize;
                        _timerText.color = timerTextColor;
                        labelPos = timerAnchor != null ? timerAnchor.position : _seatedClient.transform.position + Vector3.up * timerHeightOffset;
                    }
                    else
                    {
                        _timerText.text = goneLabel;
                        _timerText.fontSize = timerFontSize;
                        _timerText.color = timerTextColor;
                        labelPos = GetSpotLabelPosition();
                    }
                }
                else if (_breakdownInProgress)
                {
                    string label = breakdownLabel.Trim().TrimEnd('!');
                    _timerText.text = label;
                    _timerText.fontSize = breakdownFontSize;
                    _timerText.color = Color.Lerp(Color.red, timerTextColor, 0.5f);
                    labelPos = timerAnchor != null ? timerAnchor.position : _seatedClient.transform.position + Vector3.up * timerHeightOffset;
                }
                else
                {
                    float remainingHours = _sessionDurationHours - elapsed;
                    int h = Mathf.FloorToInt(remainingHours);
                    int m = Mathf.Clamp(Mathf.FloorToInt((remainingHours - h) * 60f), 0, 59);
                    _timerText.text = string.Format("{0}:{1:D2}", h, m);
                    _timerText.fontSize = timerFontSize;
                    _timerText.color = timerTextColor;
                    labelPos = timerAnchor != null ? timerAnchor.position : _seatedClient.transform.position + Vector3.up * timerHeightOffset;
                }

                _timerCanvas.transform.position = labelPos;
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

    Vector3 GetSpotLabelPosition()
    {
        if (timerAnchor != null) return timerAnchor.position;
        if (chair != null) return chair.position + Vector3.up * timerHeightOffset;
        return transform.position + Vector3.up * timerHeightOffset;
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
        _breakdownInProgress = false;
        ClearPlacedHookah();
        ClearPlacedFood();
        ClientNPC client = _seatedClient;
        _seatedClient = null;
        isOccupied = false;
        SetHighlight(_highlighted);
        if (client != null)
            client.LeaveAndGoToExit();
        var spawner = FindFirstObjectByType<ClientNPCSpawner>();
        spawner?.OnClientLeftComputer();
    }

    /// <summary> Таймаут починки: клиент ушёл, комп сломан. Клиенту возвращаем деньги за сессию. Запускаем спавн нового клиента. </summary>
    void EndSessionDueToBreakdown()
    {
        float refund = _seatedClient != null ? _seatedClient.PaymentAmount : 0f;
        DestroyTimer();
        ClearPlacedHookah();
        ClearPlacedFood();
        ClientNPC client = _seatedClient;
        _seatedClient = null;
        isOccupied = false;
        isBroken = true;
        _breakdownInProgress = false;
        SetHighlight(_highlighted);
        if (refund > 0f && PlayerBalance.Instance != null)
            PlayerBalance.Instance.Add(-refund);
        if (client != null)
            client.LeaveAndGoToExit();
        var spawner = FindFirstObjectByType<ClientNPCSpawner>();
        spawner?.OnClientLeftComputer();
    }

    /// <summary> Клиент ушёл за напитком — ставим сессию на паузу. </summary>
    public void PauseSessionForDrink()
    {
        if (_seatedClient == null || !isOccupied) return;
        _clientGoneForDrink = true;
        _remainingSessionHours = _sessionDurationHours - GetElapsedHours();
        _remainingSessionHours = Mathf.Max(0.01f, _remainingSessionHours);
    }

    /// <summary> Клиент вернулся с напитком — возобновляем сессию. </summary>
    public void ResumeSessionForClient(ClientNPC npc)
    {
        if (_seatedClient != npc || !_clientGoneForDrink || gameTime == null) return;
        _clientGoneForDrink = false;
        _sessionStartTimeHours = gameTime.CurrentTimeHours;
        _sessionDurationHours = _remainingSessionHours;
    }

    /// <summary> Клиент ушёл за едой — ставим сессию на паузу. </summary>
    public void PauseSessionForFood(ClientNPC npc)
    {
        if (_seatedClient != npc || !isOccupied) return;
        _clientGoneForFood = true;
        _remainingSessionHours = _sessionDurationHours - GetElapsedHours();
        _remainingSessionHours = Mathf.Max(0.01f, _remainingSessionHours);
    }

    /// <summary> Игрок отказался от заказа еды (Q) — клиент возвращается, возобновляем сессию. </summary>
    public void ResumeSessionForFood()
    {
        if (!_clientGoneForFood || gameTime == null) return;
        _clientGoneForFood = false;
        _foodDeliveryTimeRemaining = -1f;
        _sessionStartTimeHours = gameTime.CurrentTimeHours;
        _sessionDurationHours = _remainingSessionHours;
    }

    /// <summary> Игрок принял заказ (E): клиент пошёл на место, запускаем таймер доставки. </summary>
    public void OnFoodOrderAccepted(ClientNPC npc)
    {
        if (_seatedClient != npc || !_clientGoneForFood) return;
        _foodDeliveryTimeRemaining = foodDeliveryTimeLimitSeconds;
    }

    /// <summary> Игрок принёс еду к столу — возобновляем сессию. </summary>
    public void OnFoodDelivered()
    {
        if (!_clientGoneForFood || gameTime == null) return;
        _clientGoneForFood = false;
        _foodDeliveryTimeRemaining = -1f;
        _sessionStartTimeHours = gameTime.CurrentTimeHours;
        _sessionDurationHours = _remainingSessionHours;
    }

    /// <summary> Клиент ушёл за кальяном — ставим сессию на паузу. </summary>
    public void PauseSessionForHookah(ClientNPC npc)
    {
        if (_seatedClient != npc || !isOccupied) return;
        _clientGoneForHookah = true;
        _remainingSessionHours = _sessionDurationHours - GetElapsedHours();
        _remainingSessionHours = Mathf.Max(0.01f, _remainingSessionHours);
    }

    /// <summary> Игрок отказался от заказа кальяна (Q) — клиент возвращается, возобновляем сессию. </summary>
    public void ResumeSessionForHookah()
    {
        if (!_clientGoneForHookah || gameTime == null) return;
        _clientGoneForHookah = false;
        _sessionStartTimeHours = gameTime.CurrentTimeHours;
        _sessionDurationHours = _remainingSessionHours;
    }

    /// <summary> Игрок принял заказ кальяна (E): клиент пошёл на место. </summary>
    public void OnHookahOrderAccepted(ClientNPC npc)
    {
        if (_seatedClient != npc || !_clientGoneForHookah) return;
    }

    /// <summary> Игрок принёс кальян к столу — возобновляем сессию. </summary>
    public void OnHookahDelivered()
    {
        if (!_clientGoneForHookah || gameTime == null) return;
        _clientGoneForHookah = false;
        _sessionStartTimeHours = gameTime.CurrentTimeHours;
        _sessionDurationHours = _remainingSessionHours;
    }

    /// <summary> Попытаться отдать кальян клиенту (игрок на столе с кальяном). Если задан Hookah Place — кальян ставится туда и виден рядом со столом. </summary>
    public bool TryDeliverHookah()
    {
        if (!_clientGoneForHookah || _seatedClient == null) return false;
        if (PlayerCarry.Instance == null || !PlayerCarry.Instance.HasHookah) return false;
        if (hookahPlace != null)
        {
            _placedHookahVisual = PlayerCarry.Instance.GiveHookahTo(hookahPlace);
        }
        else
        {
            PlayerCarry.Instance.GiveHookah();
        }
        _seatedClient.OnReceiveHookah();
        return true;
    }

    void ClearPlacedHookah()
    {
        if (_placedHookahVisual != null)
        {
            Destroy(_placedHookahVisual.gameObject);
            _placedHookahVisual = null;
        }
        if (_registeredHookahSmoke != null)
        {
            Destroy(_registeredHookahSmoke);
            _registeredHookahSmoke = null;
        }
    }

    void ClearFireVfx()
    {
        if (_fireVfxInstance != null)
        {
            Destroy(_fireVfxInstance);
            _fireVfxInstance = null;
        }
        _isOnFire = false;
    }

    /// <summary> Создать огненный дым вверх (имитация огня: оранж/жёлтый/красный), если префаб не задан. </summary>
    GameObject CreateFireSmokeInstance()
    {
        if (fireVfxPoint == null) return null;
        var go = new GameObject("ComputerFireSmoke");
        go.transform.SetParent(fireVfxPoint);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // конус смотрит вверх
        go.transform.localScale = Vector3.one;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = 2.8f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        main.startColor = new Color(0.88f, 0.48f, 0.28f, 0.5f); // тёплый янтарь, не кричащий
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 320;
        main.gravityModifier = -0.02f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 20f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.1f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.z = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.9f, 0.55f, 0.32f), 0f), new GradientColorKey(new Color(0.75f, 0.38f, 0.22f), 0.5f), new GradientColorKey(new Color(0.35f, 0.2f, 0.15f), 1f) },
            new[] { new GradientAlphaKey(0.48f, 0f), new GradientAlphaKey(0.3f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply") ?? Shader.Find("Particles/Standard Unlit"));
            if (renderer.material != null && renderer.material.HasProperty("_Color"))
                renderer.material.SetColor("_Color", new Color(0.85f, 0.5f, 0.3f, 0.45f));
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        ps.Play();
        return go;
    }

    /// <summary> Вызвать при выдохе вейпа: от позиции exhalePosition в направлении exhaleDirection. Если игрок близко и направляет пар в этот комп — засчитывается затяжка; после 2–3 тушит. </summary>
    public bool TryExtinguishWithVape(Vector3 exhalePosition, Vector3 exhaleDirection)
    {
        if (!_isOnFire || fireVfxPoint == null) return false;
        Vector3 toComputer = fireVfxPoint.position - exhalePosition;
        float dist = toComputer.magnitude;
        if (dist > maxExtinguishDistance || dist < 0.01f) return false;
        Vector3 toComputerNorm = toComputer / dist;
        float cosAngle = Vector3.Dot(exhaleDirection.normalized, toComputerNorm);
        float cosMin = Mathf.Cos(maxExtinguishAngleDeg * Mathf.Deg2Rad);
        if (cosAngle < cosMin) return false;

        _extinguishHitsCount++;
        if (_extinguishHitsCount >= extinguishHitsRequired)
        {
            ClearFireVfx();
            isBroken = false;
            SetHighlight(_highlighted);
            var spawner = FindFirstObjectByType<ClientNPCSpawner>();
            spawner?.OnClientLeftComputer();
        }
        return true;
    }

    /// <summary> Вызвать при выдохе вейпа (из PlayerHQDVape). Позиция и направление пара — в мировых координатах. Возвращает true, если хотя бы один горящий комп получил затяжку. </summary>
    public static bool TryExtinguishAny(Vector3 exhalePosition, Vector3 exhaleDirection)
    {
        bool any = false;
        foreach (var spot in _allSpots)
        {
            if (spot == null) continue;
            if (spot.TryExtinguishWithVape(exhalePosition, exhaleDirection))
                any = true;
        }
        return any;
    }

    void TriggerFireEvent()
    {
        if (_isOnFire) return;
        _isOnFire = true;
        _extinguishHitsCount = 0;
        isBroken = true;
        DestroyTimer();
        ClearPlacedHookah();
        ClearPlacedFood();

        // Визуал дыма: префаб или серый дым в коде (идёт вверх, видно что комп сломан)
        if (fireVfxPoint != null)
        {
            if (fireVfxPrefab != null)
            {
                _fireVfxInstance = Instantiate(fireVfxPrefab, fireVfxPoint.position, fireVfxPoint.rotation, fireVfxPoint);
            }
            else
            {
                _fireVfxInstance = CreateFireSmokeInstance();
            }
        }

        // Клиент уходит
        ClientNPC client = _seatedClient;
        _seatedClient = null;
        isOccupied = false;
        _breakdownInProgress = false;
        _clientGoneForDrink = false;
        _clientGoneForFood = false;
        _clientGoneForHookah = false;
        _foodDeliveryTimeRemaining = -1f;
        SetHighlight(_highlighted);
        if (client != null)
            client.LeaveAndGoToExit();
    }

    /// <summary> Таймаут доставки еды: клиент уходит, возврат за сессию и бургер. </summary>
    void EndSessionDueToFoodTimeout()
    {
        float sessionRefund = _seatedClient != null ? _seatedClient.PaymentAmount : 0f;
        float burgerRefund = (PlayerCarry.Instance != null) ? PlayerCarry.Instance.BurgerPaymentAmount : 0f;
        DestroyTimer();
        ClearPlacedHookah();
        ClearPlacedFood();
        ClientNPC client = _seatedClient;
        _seatedClient = null;
        isOccupied = false;
        _clientGoneForFood = false;
        _foodDeliveryTimeRemaining = -1f;
        SetHighlight(_highlighted);
        if (PlayerBalance.Instance != null)
            PlayerBalance.Instance.Add(-(sessionRefund + burgerRefund));
        if (client != null)
            client.LeaveAndGoToExit();
        var spawner = FindFirstObjectByType<ClientNPCSpawner>();
        spawner?.OnClientLeftComputer();
    }

    /// <summary> Попытаться отдать еду клиенту (игрок на столе с бургером). Если задан Food Place On Table — бургер ставится туда. Принимается и когда NPC ещё идёт на место. </summary>
    public bool TryDeliverFood()
    {
        if (!_clientGoneForFood || _seatedClient == null) return false;
        if (PlayerCarry.Instance == null || !PlayerCarry.Instance.HasBurger) return false;
        if (foodPlaceOnTable != null)
            _placedFoodVisual = PlayerCarry.Instance.GiveBurgerTo(foodPlaceOnTable);
        else
            PlayerCarry.Instance.GiveBurger();
        _seatedClient.OnReceiveFood();
        return true;
    }

    /// <summary> Убрать еду со стола (после окончания времени «еды» или при очистке места). Вызывается из ClientNPC при окончании таймера еды. </summary>
    public void ClearPlacedFood()
    {
        if (_placedFoodVisual != null)
        {
            Destroy(_placedFoodVisual.gameObject);
            _placedFoodVisual = null;
        }
    }

    /// <summary> Игрок починил комп (удерживал E). Если была поломка при клиенте — клиент остаётся; если пустой сломанный — место снова свободно. </summary>
    public void CompleteRepair()
    {
        if (_breakdownInProgress)
        {
            _breakdownInProgress = false;
            SetHighlight(_highlighted); // сразу обновить обводку: занято (красный), а не оранжевый
        }
        else if (isBroken)
        {
            isBroken = false;
            ClearFireVfx();
            SetHighlight(_highlighted);
            var spawner = FindFirstObjectByType<ClientNPCSpawner>();
            spawner?.OnClientLeftComputer();
        }
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
        rect.sizeDelta = new Vector2(160f, 40f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * timerWorldScale;

        var textObj = new GameObject("TimerText");
        textObj.transform.SetParent(canvasObj.transform, false);
        _timerText = textObj.AddComponent<Text>();
        _timerText.text = "0:00";
        _timerText.font = timerFont != null ? timerFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _timerText.fontSize = timerFontSize;
        _timerText.fontStyle = FontStyle.Bold;
        _timerText.color = timerTextColor;
        _timerText.alignment = TextAnchor.MiddleCenter;
        _timerText.raycastTarget = false;
        _timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _timerText.verticalOverflow = VerticalWrapMode.Overflow;

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
        Color c;
        if (isBroken || _breakdownInProgress)
            c = brokenOutlineColor;
        else if (_clientGoneForFood || _clientGoneForHookah)
            c = waitingForFoodOutlineColor;
        else if (isOccupied)
            c = occupiedOutlineColor;
        else
            c = outlineColor;
        _outlineMaterial.SetColor("_OutlineColor", c);
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

    /// <summary> Комп сломан (клиент ушёл по таймауту или сломан пустой). Пока не починишь — новое место не даётся. </summary>
    public bool IsBroken => isBroken;
    /// <summary> Горит (после ивента возгорания). Тушится паром вейпа, не починкой по E. </summary>
    public bool IsOnFire => _isOnFire;
    /// <summary> Сколько ещё затяжек вейпа в сторону компьютера нужно для тушения. </summary>
    public int ExtinguishHitsRemaining => Mathf.Max(0, extinguishHitsRequired - _extinguishHitsCount);
    /// <summary> Клиент ушёл за напитком — сессия на паузе. </summary>
    public bool IsClientGoneForDrink => _clientGoneForDrink;
    /// <summary> Клиент ждёт доставку еды — сессия на паузе. </summary>
    public bool IsClientGoneForFood => _clientGoneForFood;
    /// <summary> Клиент ждёт доставку кальяна — сессия на паузе. </summary>
    public bool IsClientGoneForHookah => _clientGoneForHookah;
    /// <summary> Точка «рта» для дыма кальяна (пустой GameObject у стола). Если задана — дым идёт из неё. </summary>
    public Transform HookahMouthPoint => hookahMouthPoint;

    /// <summary> Вызывается клиентом, когда он создаёт дым кальяна под этой точкой рта (чтобы очистить при уходе клиента). </summary>
    public void RegisterHookahSmoke(GameObject smokeObject)
    {
        _registeredHookahSmoke = smokeObject;
    }

    /// <summary> Сейчас идёт поломка: клиент сидит, над головой таймер починки. Успел починить — клиент остаётся. </summary>
    public bool IsBreakdownInProgress => _breakdownInProgress;

    bool isBroken;
    bool _breakdownInProgress;
    float _breakdownTimerRemaining;
    bool _breakdownRolled;
    bool _fireRolled;
    bool _isOnFire;
    int _extinguishHitsCount;
    GameObject _fireVfxInstance;

    /// <summary> Посадить клиента за этот стол. NPC пойдёт к стулу и сядет. Возвращает true, если место было свободно. За сломанный стол сажать нельзя — сначала починить. Баланс зачисляется при посадке. </summary>
    public bool SeatClient(ClientNPC npc)
    {
        if (npc == null || isOccupied || isBroken || chair == null || !npc.HasOrdered) return false;
        npc.GoSitAt(chair, npcSitPoint);
        _seatedClient = npc;
        _sessionDurationHours = npc.RequestedSessionHours;
        if (gameTime == null)
            gameTime = FindFirstObjectByType<GameTime>();
        _sessionStartTimeHours = gameTime != null ? gameTime.CurrentTimeHours : 0f;
        isOccupied = true;
        _breakdownRolled = false;
        _fireRolled = false;
        // Зачисляем оплату за сессию только когда клиента отправили за комп
        if (npc.PaymentAmount > 0f && PlayerBalance.Instance != null)
            PlayerBalance.Instance.Add(npc.PaymentAmount);
        // Передаём NPC данные сессии для планирования реплик в игровом времени
        if (gameTime != null && npc.HasRecordedPhrase)
            npc.SetSessionInfo(gameTime, _sessionStartTimeHours, _sessionDurationHours);
        SetHighlight(_highlighted); // обновить цвет обводки на красный (занято)

        return true;
    }
}
