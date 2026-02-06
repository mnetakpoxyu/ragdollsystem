using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Умный NPC: ждёт, пока откроются нужные двери (например 2), затем идёт к точке (пустой объект)
/// в обход препятствий по NavMesh. Не идёт, пока не открыты ВСЕ двери из списка.
/// Нужен NavMeshAgent + запечённый NavMesh (Window → AI → Navigation → Bake).
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshAgent))]
public class ClientNPC : MonoBehaviour
{
    public enum State
    {
        WaitingAtDoor,
        WalkingToCounter,
        WaitingAtCounter,
        WalkingToSeat,
        SittingAtSeat,
        WalkingToCounterForDrink,
        WaitingForDrink,
        WalkingToCounterForFood,
        WaitingForFood
    }

    [Header("Точка назначения")]
    [Tooltip("Пустой объект (Empty GameObject) — сюда NPC идёт, обходя стены и препятствия.")]
    [SerializeField] Transform counterTarget;
    [Tooltip("Дистанция до точки, при которой считаем «пришёл».")]
    [SerializeField] float arriveDistance = 0.6f;

    [Header("Условие: двери")]
    [Tooltip("Ровно 2 двери (или любое количество): NPC пойдёт только когда ВСЕ эти двери открыты. Перетащи сюда 2 объекта с InteractableDoor. Пусто — идёт сразу.")]
    [SerializeField] InteractableDoor[] doors;

    [Header("Посадка за стол")]
    [Tooltip("Подъём над точкой стула при посадке (м). Используется только если у ComputerSpot не задан Npc Sit Point.")]
    [SerializeField] float sitHeightOffset = 0.55f;
    [Tooltip("Точка в сцене = где должна быть попа. Смещение вниз от точки до pivot модели (м): расстояние от корня до попы в сидячей позе. Подгони: если попа выше точки — увеличь (0.5–0.7), если провалился — уменьши.")]
    [SerializeField] float sitPointOffsetDown = 0.55f;
    [Tooltip("Смещение вперёд/назад при посадке (м). По направлению «взгляда» точки посадки: положительное = вперёд (к столу), отрицательное = назад (в глубь стула).")]
    [SerializeField] float sitPointOffsetForward = 0f;

    [Header("Анимации (Bool — скрипт выставляет true/false, связи в Animator настраиваешь сам)")]
    [Tooltip("Перетащи сюда Animator этого NPC. Пусто — ищется на объекте или в детях автоматически.")]
    [SerializeField] Animator animator;
    [Tooltip("Bool: true = идёт (к стойке или к компьютеру). Добавь параметр в Animator, переход в состояние Walking — по условию этого Bool.")]
    [SerializeField] string walkingParam = "Walking";
    [Tooltip("Bool: true = стоит и ждёт (у двери или у стойки). Переход в состояние Idle/Waiting — по этому Bool.")]
    [SerializeField] string idleParam = "Idle";
    [Tooltip("Bool: true = сидит за компом. Переход в состояние Sitting / Sit Idle — по этому Bool.")]
    [SerializeField] string sittingParam = "Sitting";

    [Header("Движение")]
    [Tooltip("Скорость ходьбы.")]
    [SerializeField] float moveSpeed = 2.5f;
    [Tooltip("Проверять застревание и пересчитывать путь (сек без движения = застрял). 0 = отключено.")]
    [SerializeField] float stuckCheckInterval = 2f;
    [Tooltip("Минимальное смещение за интервал, чтобы не считать застрявшим (м).")]
    [SerializeField] float stuckMinMove = 0.15f;

    [Header("Голосовые реплики")]
    [Tooltip("Громкость воспроизведения записанной фразы (1 = нормально, 2 = громче).")]
    [SerializeField, Range(0.5f, 3f)] float phraseVolume = 2f;
    [Tooltip("Минимальный интервал между репликами (реальные секунды).")]
    [SerializeField] float minPhraseInterval = 30f;
    [Tooltip("Максимальный интервал между репликами (реальные секунды).")]
    [SerializeField] float maxPhraseInterval = 120f;
    [Tooltip("Минимальное количество раз воспроизведения реплики за сессию.")]
    [SerializeField] int minPhraseCount = 3;
    [Tooltip("Максимальное количество раз воспроизведения реплики за сессию.")]
    [SerializeField] int maxPhraseCount = 5;
    [Tooltip("Расстояние слышимости голоса (в корпусах персонажа, ~1м каждый).")]
    [SerializeField] float voiceMaxDistance = 12f;
    [Tooltip("Последние N минут игровой сессии — крайний момент для старта реплики. Реплика не запустится позже.")]
    [SerializeField] float lastPhraseBufferMinutes = 10f;

    [Header("Напиток (рандом за сессию)")]
    [Tooltip("Вероятность, что NPC захочет попить за сессию (0–1). Только один NPC одновременно может идти за напитком.")]
    [SerializeField, Range(0f, 1f)] float wantDrinkChancePerSession = 0.4f;
    [Tooltip("Минимальное время в реальных секундах, что NPC должен посидеть, прежде чем может захотеть попить.")]
    [SerializeField] float wantDrinkMinRealSeconds = 15f;
    [Tooltip("Проверять желание попить каждые N реальных секунд (чтобы не проверять каждый кадр).")]
    [SerializeField] float wantDrinkCheckInterval = 5f;

    [Header("Еда (рандом за сессию)")]
    [Tooltip("Вероятность, что NPC захочет поесть за сессию (0–1). Только один NPC одновременно может идти за едой.")]
    [SerializeField, Range(0f, 1f)] float wantFoodChancePerSession = 0.35f;
    [Tooltip("Минимальное время в реальных секундах, что NPC должен посидеть, прежде чем может захотеть поесть.")]
    [SerializeField] float wantFoodMinRealSeconds = 20f;
    [Tooltip("Проверять желание поесть каждые N реальных секунд.")]
    [SerializeField] float wantFoodCheckInterval = 6f;

    NavMeshAgent _agent;
    Animator _anim;
    State _state = State.WaitingAtDoor;
    State _prevAnimState = (State)(-1);
    float _lastStuckCheckTime;
    Vector3 _lastPositionForStuck;
    Transform _seatChair;
    Transform _seatSitPoint;
    float _requestedSessionHours;
    float _paymentAmount;
    bool _hasOrdered;
    ComputerSpot _assignedSpot;
    int _ensureOffNavMeshFrames;

    // Голосовые реплики (привязаны к игровому времени сессии)
    AudioClip _recordedPhrase;
    AudioSource _audioSource;
    GameTime _gameTime;
    float _sessionStartHours;
    float _sessionDurationHours;
    float _nextPhraseElapsedHours; // момент воспроизведения: elapsed часов от начала сессии
    int _phrasesLeftToPlay;
    bool _isPlayingPhrase;
    bool _drinkRolled;
    float _sitDownRealTime = -1f;
    float _lastDrinkCheckTime = -1f;
    static ClientNPC _currentThirstyNpc;
    bool _foodRolled;
    float _lastFoodCheckTime = -1f;
    static ClientNPC _currentHungryNpc;

    public static ClientNPC CurrentThirstyNpc => _currentThirstyNpc;
    public static ClientNPC CurrentHungryNpc => _currentHungryNpc;

    /// <summary> Сколько NPC сейчас идут к стойке, ждут у стойки, ждут напиток или еду. Спавнер не спавнит второго, пока стойка «занята». </summary>
    public static int CountGoingToOrAtCounter()
    {
        int count = 0;
        var all = FindObjectsByType<ClientNPC>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i].CurrentState;
            if (s == State.WalkingToCounter || s == State.WaitingAtCounter ||
                s == State.WalkingToCounterForDrink || s == State.WaitingForDrink ||
                s == State.WalkingToCounterForFood || s == State.WaitingForFood)
                count++;
        }
        return count;
    }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.updateRotation = true;
            _agent.speed = moveSpeed;
            _agent.autoRepath = true;
        }

        _anim = animator != null ? animator : GetComponentInChildren<Animator>();
        if (_anim == null)
            _anim = GetComponent<Animator>();

        // Создаём AudioSource для воспроизведения реплик
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.volume = phraseVolume;
        _audioSource.spatialBlend = 1f; // 3D звук
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = voiceMaxDistance; // 12 корпусов персонажа
        _audioSource.rolloffMode = AudioRolloffMode.Linear; // Плавное затухание
        _audioSource.playOnAwake = false;
        _audioSource.loop = false; // Без зацикливания
    }

    void OnDestroy()
    {
        if (_currentThirstyNpc == this)
            _currentThirstyNpc = null;
        if (_currentHungryNpc == this)
            _currentHungryNpc = null;
    }

    void Start()
    {
        int id = gameObject.GetInstanceID();
        Vector3 posBefore = transform.position;
        bool onNavBefore = _agent != null && _agent.isOnNavMesh;
        Debug.Log($"[NPC {id}] Start: pos={posBefore}, pos.y={posBefore.y:F3}, isOnNavMesh={onNavBefore}");

        EnsureOnNavMesh();

        Vector3 posAfter = transform.position;
        bool onNavAfter = _agent != null && _agent.isOnNavMesh;
        Debug.Log($"[NPC {id}] После EnsureOnNavMesh: pos={posAfter}, pos.y={posAfter.y:F3}, isOnNavMesh={onNavAfter}");

        if (counterTarget == null) return;
        if (AreAllDoorsOpen())
            GoToTarget();
        // Если агент ещё не на NavMesh (часто у 3–4 NPC после спавна) — повторные попытки с нарастающей задержкой
        if (_state == State.WaitingAtDoor && _agent != null && !_agent.isOnNavMesh)
        {
            Invoke(nameof(RetryGoToTargetAfterSpawn), 0.2f);
            Invoke(nameof(RetryGoToTargetAfterSpawn), 0.6f);
            Invoke(nameof(RetryGoToTargetAfterSpawn), 1.2f);
        }
        _lastPositionForStuck = transform.position;
        _lastStuckCheckTime = Time.time;
    }

    /// <summary>
    /// Вызывается по таймеру после спавна: повторно ставим на NavMesh и отправляем к стойке (решает проблему, когда 4-й и далее NPC не шли к поинту).
    /// </summary>
    void RetryGoToTargetAfterSpawn()
    {
        if (this == null || _agent == null) return;
        if (_state != State.WaitingAtDoor || counterTarget == null || !AreAllDoorsOpen()) return;
        EnsureOnNavMesh();
        GoToTarget();
    }

    /// <summary>
    /// Ставит агента на NavMesh. Варпим почти на поверхность (0.05f), иначе Unity не считает агента onNavMesh
    /// (особенно когда несколько NPC спавнятся в одной точке — 4-й и далее получали isOnNavMesh=false).
    /// </summary>
    void EnsureOnNavMesh()
    {
        if (_agent == null) return;
        if (_agent.isOnNavMesh)
        {
            _ensureOffNavMeshFrames = 0;
            return;
        }
        _ensureOffNavMeshFrames++;
        Vector3 from = transform.position;
        const float maxWarpDistance = 2f;
        // Минимальный подъём над поверхностью — иначе Unity считает агента "над" мешем и isOnNavMesh=false
        const float heightOffset = 0.05f;
        bool found = NavMesh.SamplePosition(from, out NavMeshHit hit, maxWarpDistance, NavMesh.AllAreas);
        if (!found)
        {
            // Повтор с небольшой случайной точкой — если спавн занят другими агентами
            Vector3 fallback = from + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f));
            found = NavMesh.SamplePosition(fallback, out hit, maxWarpDistance, NavMesh.AllAreas);
        }
        if (!found) return;
        float dist = Vector3.Distance(hit.position, from);
        if (dist > maxWarpDistance) return;
        Vector3 to = hit.position + Vector3.up * heightOffset;
        // Не отказываемся от варпа вниз при спавне (агент 0.4 м над полом) — иначе остаётся в воздухе и не идёт. Запрещаем только сильное падение (> 1.5 м).
        if (from.y - to.y > 1.5f) return;
        _agent.Warp(to);
    }

    /// <summary>
    /// Один раз подправить позицию на NavMesh при отправке к стулу (клиент у стойки мог быть чуть не на NavMesh).
    /// Радиус больше, чем в EnsureOnNavMesh, но не телепортируем вниз.
    /// </summary>
    void EnsureOnNavMeshForSeat()
    {
        if (_agent == null || _agent.isOnNavMesh) return;
        const float maxRadius = 2f;
        const float heightOffset = 0.2f;
        bool found = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxRadius, NavMesh.AllAreas);
        if (!found) return;
        Vector3 to = hit.position + Vector3.up * heightOffset;
        if (to.y < transform.position.y - 0.25f) return;
        _agent.Warp(to);
    }

    void Update()
    {
        switch (_state)
        {
            case State.WaitingAtDoor:
                if (AreAllDoorsOpen())
                {
                    EnsureOnNavMesh();
                    GoToTarget();
                }
                break;
            case State.WalkingToCounter:
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= arriveDistance)
                        _state = State.WaitingAtCounter;
                    else if (stuckCheckInterval > 0f && Time.time - _lastStuckCheckTime >= stuckCheckInterval)
                        TryUnstuck();
                }
                break;
            case State.WaitingAtCounter:
                break;
            case State.WalkingToCounterForDrink:
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= arriveDistance)
                        _state = State.WaitingForDrink;
                    else if (stuckCheckInterval > 0f && Time.time - _lastStuckCheckTime >= stuckCheckInterval)
                        TryUnstuck();
                }
                break;
            case State.WaitingForDrink:
                break;
            case State.WalkingToCounterForFood:
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= arriveDistance)
                        _state = State.WaitingForFood;
                    else if (stuckCheckInterval > 0f && Time.time - _lastStuckCheckTime >= stuckCheckInterval)
                        TryUnstuck();
                }
                break;
            case State.WaitingForFood:
                break;
            case State.WalkingToSeat:
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= arriveDistance)
                    {
                        _state = State.SittingAtSeat;
                        if (_seatChair != null)
                        {
                            Transform pose = _seatSitPoint != null ? _seatSitPoint : _seatChair;
                            Vector3 sitPos;
                            Quaternion sitRot = pose.rotation;
                            if (_seatSitPoint != null)
                            {
                                sitPos = pose.position - Vector3.up * sitPointOffsetDown + pose.forward * sitPointOffsetForward;
                            }
                            else
                            {
                                sitPos = _seatChair.position + Vector3.up * sitHeightOffset;
                                sitRot = _seatChair.rotation;
                            }
                            transform.SetPositionAndRotation(sitPos, sitRot);
                            _agent.enabled = false;
                        }
                    }
                    else if (stuckCheckInterval > 0f && Time.time - _lastStuckCheckTime >= stuckCheckInterval)
                        TryUnstuckToSeat();
                }
                break;
            case State.SittingAtSeat:
                if (_sitDownRealTime < 0f)
                    _sitDownRealTime = Time.time;
                // Нельзя идти за напитком/едой, если уже сломался компьютер и ждём ремонт — только один ивент.
                if (_assignedSpot != null && !_assignedSpot.IsClientGoneForDrink && !_assignedSpot.IsClientGoneForFood && !_assignedSpot.IsBreakdownInProgress)
                {
                    if (!_drinkRolled && _currentThirstyNpc == null && _currentHungryNpc == null)
                    {
                        float satFor = Time.time - _sitDownRealTime;
                        if (satFor >= wantDrinkMinRealSeconds && (Time.time - _lastDrinkCheckTime) >= wantDrinkCheckInterval)
                        {
                            _lastDrinkCheckTime = Time.time;
                            if (Random.value < wantDrinkChancePerSession)
                            {
                                _drinkRolled = true;
                                RequestDrink();
                            }
                        }
                    }
                    if (!_foodRolled && _currentThirstyNpc == null && _currentHungryNpc == null)
                    {
                        float satFor = Time.time - _sitDownRealTime;
                        if (satFor >= wantFoodMinRealSeconds && (Time.time - _lastFoodCheckTime) >= wantFoodCheckInterval)
                        {
                            _lastFoodCheckTime = Time.time;
                            if (Random.value < wantFoodChancePerSession)
                            {
                                _foodRolled = true;
                                RequestFood();
                            }
                        }
                    }
                }
                // Воспроизводим реплики во время игровой сессии (по игровому времени)
                bool isCurrentlyPlaying = _isPlayingPhrase || (_audioSource != null && _audioSource.isPlaying);
                if (_recordedPhrase != null && _phrasesLeftToPlay > 0 && !isCurrentlyPlaying && _gameTime != null)
                {
                    float elapsedHours = GetSessionElapsedHours();
                    if (elapsedHours >= _nextPhraseElapsedHours)
                        PlayPhrase();
                }
                break;
        }

        if (_anim != null && _state != _prevAnimState)
        {
            UpdateAnimator();
            _prevAnimState = _state;
        }
    }

    void UpdateAnimator()
    {
        bool walking = _state == State.WalkingToCounter || _state == State.WalkingToSeat || _state == State.WalkingToCounterForDrink || _state == State.WalkingToCounterForFood;
        bool idle = _state == State.WaitingAtDoor || _state == State.WaitingAtCounter || _state == State.WaitingForDrink || _state == State.WaitingForFood;
        bool sitting = _state == State.SittingAtSeat;

        if (!string.IsNullOrEmpty(walkingParam)) _anim.SetBool(walkingParam, walking);
        if (!string.IsNullOrEmpty(idleParam)) _anim.SetBool(idleParam, idle);
        if (!string.IsNullOrEmpty(sittingParam)) _anim.SetBool(sittingParam, sitting);
    }

    void TryUnstuck()
    {
        _lastStuckCheckTime = Time.time;
        float moved = Vector3.Distance(transform.position, _lastPositionForStuck);
        _lastPositionForStuck = transform.position;
        if (moved < stuckMinMove && counterTarget != null && _agent != null && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
            _agent.SetDestination(counterTarget.position);
        }
    }

    void TryUnstuckToSeat()
    {
        _lastStuckCheckTime = Time.time;
        float moved = Vector3.Distance(transform.position, _lastPositionForStuck);
        _lastPositionForStuck = transform.position;
        if (moved < stuckMinMove && _seatChair != null && _agent != null && _agent.isOnNavMesh)
        {
            _agent.ResetPath();
            _agent.SetDestination(_seatChair.position);
        }
    }

    bool AreAllDoorsOpen()
    {
        if (doors == null || doors.Length == 0) return true;
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && !doors[i].IsOpen)
                return false;
        }
        return true;
    }

    void GoToTarget()
    {
        if (counterTarget == null) return;
        if (_agent == null) return;
        EnsureOnNavMesh();
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(counterTarget.position);
            _state = State.WalkingToCounter;
            _lastPositionForStuck = transform.position;
            _lastStuckCheckTime = Time.time;
        }
    }

    /// <summary>
    /// Задать точку назначения из кода (например другой пустой объект).
    /// </summary>
    public void SetTarget(Transform target)
    {
        counterTarget = target;
        if (_state == State.WalkingToCounter && _agent != null && _agent.isOnNavMesh && target != null)
            _agent.SetDestination(target.position);
    }

    /// <summary>
    /// Инициализация при спавне: точка стойки и двери. Вызывается спавнером при создании NPC.
    /// </summary>
    public void InitializeSpawn(Transform counter, InteractableDoor[] doorList)
    {
        counterTarget = counter;
        doors = doorList != null ? doorList : new InteractableDoor[0];
    }

    /// <summary>
    /// Скопировать настройки (аниматор, параметры движения, реплик и т.д.) с другого NPC.
    /// Вызывается спавнером, чтобы префабы из ассета брали конфиг с «шаблонного» NPC на сцене.
    /// Точку стойки и двери задаёт InitializeSpawn — их не копируем.
    /// </summary>
    public void CopyConfigurationFrom(ClientNPC other)
    {
        if (other == null) return;
        arriveDistance = other.arriveDistance;
        sitHeightOffset = other.sitHeightOffset;
        sitPointOffsetDown = other.sitPointOffsetDown;
        sitPointOffsetForward = other.sitPointOffsetForward;
        // animator не копируем — у спавненного префаба свой Animator (ищется в Awake по GetComponentInChildren)
        walkingParam = other.walkingParam;
        idleParam = other.idleParam;
        sittingParam = other.sittingParam;
        moveSpeed = other.moveSpeed;
        stuckCheckInterval = other.stuckCheckInterval;
        stuckMinMove = other.stuckMinMove;
        phraseVolume = other.phraseVolume;
        minPhraseInterval = other.minPhraseInterval;
        maxPhraseInterval = other.maxPhraseInterval;
        minPhraseCount = other.minPhraseCount;
        maxPhraseCount = other.maxPhraseCount;
        voiceMaxDistance = other.voiceMaxDistance;
        lastPhraseBufferMinutes = other.lastPhraseBufferMinutes;
        wantDrinkChancePerSession = other.wantDrinkChancePerSession;
        wantDrinkMinRealSeconds = other.wantDrinkMinRealSeconds;
        wantDrinkCheckInterval = other.wantDrinkCheckInterval;
        wantFoodChancePerSession = other.wantFoodChancePerSession;
        wantFoodMinRealSeconds = other.wantFoodMinRealSeconds;
        wantFoodCheckInterval = other.wantFoodCheckInterval;
        // Заново взять агент и применить настройки (важно для спавненных префабов: агент мог быть null в Awake или добавлен позже)
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.updateRotation = true;
            _agent.autoRepath = true;
        }
    }

    /// <summary>
    /// Вызвать после инита со спавнера: если двери открыты и есть точка стойки — сразу идёт к стойке.
    /// Решает проблему, когда у спавненного префаба Start() уже отработал с counterTarget == null.
    /// </summary>
    public void GoToCounterIfReady()
    {
        if (_state != State.WaitingAtDoor || counterTarget == null) return;
        EnsureOnNavMesh();
        if (AreAllDoorsOpen())
            GoToTarget();
        else if (_agent != null && !_agent.isOnNavMesh)
        {
            Invoke(nameof(RetryGoToTargetAfterSpawn), 0.2f);
            Invoke(nameof(RetryGoToTargetAfterSpawn), 0.6f);
            Invoke(nameof(RetryGoToTargetAfterSpawn), 1.2f);
        }
    }

    /// <summary>
    /// Сброс состояния после спавна. Обязательно вызывать из спавнера после Instantiate:
    /// иначе клон может унаследовать «сидячее» состояние (выключенный агент, поза) от исходного объекта.
    /// </summary>
    public void ResetStateForSpawn()
    {
        if (_agent != null)
            _agent.enabled = true;
        _state = State.WaitingAtDoor;
        _seatChair = null;
        _seatSitPoint = null;
        _hasOrdered = false;
        _requestedSessionHours = 0f;
        _paymentAmount = 0f;
        _assignedSpot = null;
        _lastPositionForStuck = transform.position;
        _lastStuckCheckTime = Time.time;
        _recordedPhrase = null;
        _phrasesLeftToPlay = 0;
        _gameTime = null;
        _drinkRolled = false;
        _foodRolled = false;
        _lastDrinkCheckTime = -1f;
        _lastFoodCheckTime = -1f;
        _prevAnimState = (State)(-1);
        if (_anim != null)
        {
            _anim.Rebind();
            if (!string.IsNullOrEmpty(walkingParam)) _anim.SetBool(walkingParam, false);
            if (!string.IsNullOrEmpty(idleParam)) _anim.SetBool(idleParam, true);
            if (!string.IsNullOrEmpty(sittingParam)) _anim.SetBool(sittingParam, false);
        }
    }

    /// <summary>
    /// Назначить место за компьютером. Вызывается до OnInteract(), чтобы часы и тариф брались из спота.
    /// </summary>
    public void AssignSpot(ComputerSpot spot)
    {
        _assignedSpot = spot;
    }

    /// <summary>
    /// Вызывается игроком по E у стойки. Часы и тариф берутся из назначенного ComputerSpot.
    /// Клиент «заказывает» сессию (сумма зачислится только когда игрок отправит его за комп — в SeatClient).
    /// </summary>
    public void OnInteract()
    {
        if (_state != State.WaitingAtCounter || _hasOrdered) return;
        if (_assignedSpot == null) return;

        float minH = _assignedSpot.MinSessionHours;
        float maxH = _assignedSpot.MaxSessionHours;
        float tariff = _assignedSpot.PricePerHour;

        _requestedSessionHours = Mathf.Clamp(Random.Range(minH, maxH), 0.25f, 24f);
        _paymentAmount = _requestedSessionHours * tariff;

        _hasOrdered = true;
    }

    /// <summary> NPC захотел попить: встаёт, сессия на паузе, идёт к стойке. Только один NPC в игре может быть «жаждущим». </summary>
    void RequestDrink()
    {
        if (_currentThirstyNpc != null || _assignedSpot == null || _seatChair == null || counterTarget == null || _agent == null) return;
        _currentThirstyNpc = this;
        _assignedSpot.PauseSessionForDrink();
        _agent.enabled = true;
        Vector3 standPos = _seatChair.position + _seatChair.forward * 0.5f;
        transform.SetPositionAndRotation(standPos, _seatChair.rotation);
        EnsureOnNavMesh();
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(counterTarget.position);
            _state = State.WalkingToCounterForDrink;
            _lastPositionForStuck = transform.position;
            _lastStuckCheckTime = Time.time;
        }
        else
        {
            _currentThirstyNpc = null;
            _assignedSpot.ResumeSessionForClient(this);
        }
    }

    /// <summary> Игрок отдал напиток. NPC получает, игрок — оплату, NPC идёт обратно на место. После ухода от стойки — даём спавнеру шанс заспавнить следующего. </summary>
    public void OnReceiveDrink()
    {
        if (_state != State.WaitingForDrink) return;
        if (PlayerCarry.Instance != null)
            PlayerCarry.Instance.GiveDrink();
        if (PlayerBalance.Instance != null && PlayerCarry.Instance != null)
            PlayerBalance.Instance.Add(PlayerCarry.Instance.DrinkPaymentAmount);
        _currentThirstyNpc = null;
        _assignedSpot?.ResumeSessionForClient(this);
        GoSitAt(_seatChair, _seatSitPoint);
        var spawner = UnityEngine.Object.FindFirstObjectByType<ClientNPCSpawner>();
        spawner?.OnClientLeftCounter();
    }

    /// <summary> NPC захотел поесть: встаёт, сессия на паузе, идёт к стойке. Только один NPC в игре может быть «голодным». </summary>
    void RequestFood()
    {
        if (_currentHungryNpc != null || _assignedSpot == null || _seatChair == null || counterTarget == null || _agent == null) return;
        _currentHungryNpc = this;
        _assignedSpot.PauseSessionForFood(this);
        _agent.enabled = true;
        Vector3 standPos = _seatChair.position + _seatChair.forward * 0.5f;
        transform.SetPositionAndRotation(standPos, _seatChair.rotation);
        EnsureOnNavMesh();
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(counterTarget.position);
            _state = State.WalkingToCounterForFood;
            _lastPositionForStuck = transform.position;
            _lastStuckCheckTime = Time.time;
        }
        else
        {
            _currentHungryNpc = null;
            _assignedSpot.ResumeSessionForFood();
        }
    }

    /// <summary> Игрок принял заказ (E): клиент платит за еду, идёт обратно на место, запускается таймер доставки. </summary>
    public void OnAcceptFoodOrder()
    {
        if (_state != State.WaitingForFood) return;
        if (PlayerBalance.Instance != null && PlayerCarry.Instance != null)
            PlayerBalance.Instance.Add(PlayerCarry.Instance.BurgerPaymentAmount);
        _assignedSpot?.OnFoodOrderAccepted(this);
        GoSitAt(_seatChair, _seatSitPoint);
        var spawner = UnityEngine.Object.FindFirstObjectByType<ClientNPCSpawner>();
        spawner?.OnClientLeftCounter();
    }

    /// <summary> Игрок отказался от заказа (Q): клиент просто идёт обратно на место, без оплаты. </summary>
    public void OnDeclineFoodOrder()
    {
        if (_state != State.WaitingForFood) return;
        _currentHungryNpc = null;
        _assignedSpot?.ResumeSessionForFood();
        GoSitAt(_seatChair, _seatSitPoint);
        var spawner = UnityEngine.Object.FindFirstObjectByType<ClientNPCSpawner>();
        spawner?.OnClientLeftCounter();
    }

    /// <summary> Игрок принёс еду к столу. NPC получает, сессия возобновляется. </summary>
    public void OnReceiveFood()
    {
        if (_state != State.SittingAtSeat) return;
        if (PlayerCarry.Instance != null)
            PlayerCarry.Instance.GiveBurger();
        _currentHungryNpc = null;
        _assignedSpot?.OnFoodDelivered();
    }

    /// <summary>
    /// Отправить NPC к стулу и посадить. Вызывается из ComputerSpot.SeatClient.
    /// sitPoint — дочерний объект стула, куда ставить NPC (позиция/поворот). Пусто — использовать стул + смещение.
    /// </summary>
    public void GoSitAt(Transform chair, Transform sitPoint = null)
    {
        if (chair == null || _agent == null) return;
        int id = gameObject.GetInstanceID();
        _seatChair = chair;
        _seatSitPoint = sitPoint;
        EnsureOnNavMesh();
        if (!_agent.isOnNavMesh)
            EnsureOnNavMeshForSeat(); // клиент у стойки мог быть чуть не на NavMesh — один раз подправляем
        Debug.Log($"[NPC {id}] GoSitAt: chair.y={chair.position.y:F3}, после Ensure isOnNavMesh={_agent.isOnNavMesh}, pos.y={transform.position.y:F3}");
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(chair.position);
            _state = State.WalkingToSeat;
            _lastPositionForStuck = transform.position;
            _lastStuckCheckTime = Time.time;
        }
        else
            Debug.LogWarning($"[NPC {id}] GoSitAt: агент не на NavMesh — не идёт к стулу! pos={transform.position}", this);
    }

    public State CurrentState => _state;
    public Transform Target => counterTarget;

    /// <summary> Сколько часов клиент хочет поиграть (задаётся при взаимодействии E). </summary>
    public float RequestedSessionHours => _requestedSessionHours;

    /// <summary> Сумма, которую клиент заплатил (зачислена при E). </summary>
    public float PaymentAmount => _paymentAmount;

    /// <summary> Клиент уже «заказал» сессию и ждёт посадки. </summary>
    public bool HasOrdered => _hasOrdered;

    /// <summary> Назначенное место за компьютером (баланс — часы и тариф — берётся отсюда). </summary>
    public ComputerSpot AssignedSpot => _assignedSpot;

    /// <summary> Есть ли назначенное место (нужно для приёма заказа). </summary>
    public bool HasAssignedSpot => _assignedSpot != null;

    /// <summary>
    /// Установить записанную голосовую реплику для этого клиента.
    /// </summary>
    public void SetRecordedPhrase(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("ClientNPC: Попытка установить пустой AudioClip!");
            return;
        }

        _recordedPhrase = clip;

        // Минимум 1 воспроизведение, максимум — без спама
        _phrasesLeftToPlay = UnityEngine.Random.Range(minPhraseCount, maxPhraseCount + 1);

        Debug.Log($"ClientNPC: Реплика записана. Будет воспроизведена {_phrasesLeftToPlay} раз(а).");
    }

    /// <summary>
    /// Задать параметры сессии (вызывается ComputerSpot при посадке).
    /// Планирует реплики в игровом времени — гарантированно успеют за сессию.
    /// </summary>
    public void SetSessionInfo(GameTime gameTime, float sessionStartHours, float sessionDurationHours)
    {
        _gameTime = gameTime;
        _sessionStartHours = sessionStartHours;
        _sessionDurationHours = sessionDurationHours;

        if (_recordedPhrase == null || _phrasesLeftToPlay <= 0 || _gameTime == null) return;

        // Дедлайн: последние N минут сессии — реплика уже не должна стартовать
        float deadlineHours = sessionDurationHours - (lastPhraseBufferMinutes / 60f);
        deadlineHours = Mathf.Max(0.02f, deadlineHours);

        // Первая реплика — в первой трети окна (ранний старт, 100% успеет)
        float firstPhraseMax = deadlineHours * 0.33f;
        _nextPhraseElapsedHours = UnityEngine.Random.Range(0.005f, firstPhraseMax);

        Debug.Log($"ClientNPC: Сессия {sessionDurationHours:F2}ч. Дедлайн реплик: {deadlineHours:F2}ч. Первая реплика в {_nextPhraseElapsedHours:F2}ч.");
    }

    float GetSessionElapsedHours()
    {
        if (_gameTime == null) return 0f;
        float elapsed = _gameTime.CurrentTimeHours - _sessionStartHours;
        if (elapsed < 0f) elapsed += 24f;
        return elapsed;
    }

    /// <summary>
    /// Воспроизвести записанную реплику.
    /// </summary>
    void PlayPhrase()
    {
        if (_recordedPhrase == null || _audioSource == null) return;
        if (_isPlayingPhrase || _audioSource.isPlaying) return;

        _audioSource.clip = _recordedPhrase;
        _audioSource.volume = phraseVolume;
        _audioSource.Play();
        _isPlayingPhrase = true;
        _phrasesLeftToPlay--;

        float clipLength = _recordedPhrase.length;
        Debug.Log($"ClientNPC: Воспроизведение реплики. Осталось: {_phrasesLeftToPlay}");

        // Планируем следующую реплику в игровом времени (не позже дедлайна)
        if (_phrasesLeftToPlay > 0 && _gameTime != null)
        {
            float elapsed = GetSessionElapsedHours();
            float phraseLengthGameHours = clipLength * _gameTime.HoursPerRealSecond;
            float intervalHours = UnityEngine.Random.Range(minPhraseInterval, maxPhraseInterval) * _gameTime.HoursPerRealSecond;

            // Дедлайн: последние N минут — реплика не должна стартовать
            float deadlineHours = _sessionDurationHours - (lastPhraseBufferMinutes / 60f);
            deadlineHours = Mathf.Max(0.05f, deadlineHours);

            // Следующая реплика: после окончания текущей + интервал
            float nextElapsed = elapsed + phraseLengthGameHours + intervalHours;

            // Не планируем позже дедлайна — реплика должна успеть до конца
            float maxStartElapsed = deadlineHours - phraseLengthGameHours;
            if (nextElapsed > maxStartElapsed)
            {
                // Укладываем в оставшееся окно (равномерно или сразу после текущей)
                nextElapsed = Mathf.Min(elapsed + phraseLengthGameHours + minPhraseInterval * _gameTime.HoursPerRealSecond, maxStartElapsed);
            }

            _nextPhraseElapsedHours = nextElapsed;
        }

        StartCoroutine(ResetPlayingFlag(clipLength + 0.1f));
    }

    /// <summary>
    /// Сбросить флаг воспроизведения после окончания аудио.
    /// </summary>
    System.Collections.IEnumerator ResetPlayingFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isPlayingPhrase = false;
    }

    /// <summary> Есть ли записанная реплика у этого клиента. </summary>
    public bool HasRecordedPhrase => _recordedPhrase != null;
}
