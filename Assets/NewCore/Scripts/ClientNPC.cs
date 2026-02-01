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
        SittingAtSeat
    }

    [Header("Точка назначения")]
    [Tooltip("Пустой объект (Empty GameObject) — сюда NPC идёт, обходя стены и препятствия.")]
    [SerializeField] Transform counterTarget;
    [Tooltip("Дистанция до точки, при которой считаем «пришёл».")]
    [SerializeField] float arriveDistance = 0.6f;

    [Header("Условие: двери")]
    [Tooltip("Ровно 2 двери (или любое количество): NPC пойдёт только когда ВСЕ эти двери открыты. Перетащи сюда 2 объекта с InteractableDoor. Пусто — идёт сразу.")]
    [SerializeField] InteractableDoor[] doors;

    [Header("Оплата за игру (при взаимодействии E у стойки)")]
    [Tooltip("Цена за 1 игровой час. Клиент «скажет» случайное кол-во часов и заплатит.")]
    [SerializeField] float pricePerHour = 100f;
    [Tooltip("Мин. часов, которые клиент хочет поиграть (случайное от Min до Max).")]
    [SerializeField] float minSessionHours = 1f;
    [Tooltip("Макс. часов, которые клиент хочет поиграть.")]
    [SerializeField] float maxSessionHours = 3f;

    [Header("Движение")]
    [Tooltip("Скорость ходьбы.")]
    [SerializeField] float moveSpeed = 2.5f;
    [Tooltip("Проверять застревание и пересчитывать путь (сек без движения = застрял). 0 = отключено.")]
    [SerializeField] float stuckCheckInterval = 2f;
    [Tooltip("Минимальное смещение за интервал, чтобы не считать застрявшим (м).")]
    [SerializeField] float stuckMinMove = 0.15f;

    [Header("Голосовые реплики")]
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

    NavMeshAgent _agent;
    State _state = State.WaitingAtDoor;
    float _lastStuckCheckTime;
    Vector3 _lastPositionForStuck;
    Transform _seatChair;
    float _requestedSessionHours;
    float _paymentAmount;
    bool _hasOrdered;

    // Голосовые реплики (привязаны к игровому времени сессии)
    AudioClip _recordedPhrase;
    AudioSource _audioSource;
    GameTime _gameTime;
    float _sessionStartHours;
    float _sessionDurationHours;
    float _nextPhraseElapsedHours; // момент воспроизведения: elapsed часов от начала сессии
    int _phrasesLeftToPlay;
    bool _isPlayingPhrase;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.updateRotation = true;
            _agent.speed = moveSpeed;
            _agent.autoRepath = true;
        }

        // Создаём AudioSource для воспроизведения реплик
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1f; // 3D звук
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = voiceMaxDistance; // 12 корпусов персонажа
        _audioSource.rolloffMode = AudioRolloffMode.Linear; // Плавное затухание
        _audioSource.playOnAwake = false;
        _audioSource.loop = false; // Без зацикливания
    }

    void Start()
    {
        EnsureOnNavMesh();
        if (counterTarget == null) return;
        if (AreAllDoorsOpen())
            GoToTarget();
        _lastPositionForStuck = transform.position;
        _lastStuckCheckTime = Time.time;
    }

    void EnsureOnNavMesh()
    {
        if (_agent == null || _agent.isOnNavMesh) return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
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
            case State.WalkingToSeat:
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= arriveDistance)
                    {
                        _state = State.SittingAtSeat;
                        if (_seatChair != null)
                        {
                            transform.SetPositionAndRotation(_seatChair.position, _seatChair.rotation);
                            _agent.enabled = false;
                        }
                    }
                    else if (stuckCheckInterval > 0f && Time.time - _lastStuckCheckTime >= stuckCheckInterval)
                        TryUnstuckToSeat();
                }
                break;
            case State.SittingAtSeat:
                // Воспроизводим реплики во время игровой сессии (по игровому времени)
                bool isCurrentlyPlaying = _isPlayingPhrase || (_audioSource != null && _audioSource.isPlaying);
                if (_recordedPhrase != null && _phrasesLeftToPlay > 0 && !isCurrentlyPlaying && _gameTime != null)
                {
                    float elapsedHours = GetSessionElapsedHours();
                    // Играем, если пора, и не позже дедлайна
                    if (elapsedHours >= _nextPhraseElapsedHours)
                    {
                        PlayPhrase();
                    }
                }
                break;
        }
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
    /// Инициализация при спавне: точка стойки и двери. Вызывается ClientNPCSpawner.
    /// </summary>
    public void InitializeSpawn(Transform counter, InteractableDoor[] doorList)
    {
        counterTarget = counter;
        doors = doorList != null ? doorList : new InteractableDoor[0];
    }

    /// <summary>
    /// Вызывается игроком по E у стойки. Клиент «говорит», сколько часов хочет поиграть и платит сразу.
    /// После этого игрок должен посадить его за стол.
    /// </summary>
    public void OnInteract()
    {
        if (_state != State.WaitingAtCounter || _hasOrdered) return;

        _requestedSessionHours = Mathf.Clamp(Random.Range(minSessionHours, maxSessionHours), 0.25f, 24f);
        _paymentAmount = _requestedSessionHours * pricePerHour;

        if (PlayerBalance.Instance != null)
            PlayerBalance.Instance.Add(_paymentAmount);

        _hasOrdered = true;
    }

    /// <summary>
    /// Отправить NPC к стулу и посадить. Вызывается из ComputerSpot.SeatClient.
    /// </summary>
    public void GoSitAt(Transform chair)
    {
        if (chair == null || _agent == null) return;
        _seatChair = chair;
        EnsureOnNavMesh();
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(chair.position);
            _state = State.WalkingToSeat;
            _lastPositionForStuck = transform.position;
            _lastStuckCheckTime = Time.time;
        }
    }

    public State CurrentState => _state;
    public Transform Target => counterTarget;

    /// <summary> Сколько часов клиент хочет поиграть (задаётся при взаимодействии E). </summary>
    public float RequestedSessionHours => _requestedSessionHours;

    /// <summary> Сумма, которую клиент заплатил (зачислена при E). </summary>
    public float PaymentAmount => _paymentAmount;

    /// <summary> Клиент уже «заказал» сессию и ждёт посадки. </summary>
    public bool HasOrdered => _hasOrdered;

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
