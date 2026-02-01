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

    [Header("Движение")]
    [Tooltip("Скорость ходьбы.")]
    [SerializeField] float moveSpeed = 2.5f;
    [Tooltip("Проверять застревание и пересчитывать путь (сек без движения = застрял). 0 = отключено.")]
    [SerializeField] float stuckCheckInterval = 2f;
    [Tooltip("Минимальное смещение за интервал, чтобы не считать застрявшим (м).")]
    [SerializeField] float stuckMinMove = 0.15f;

    NavMeshAgent _agent;
    State _state = State.WaitingAtDoor;
    float _lastStuckCheckTime;
    Vector3 _lastPositionForStuck;
    Transform _seatChair;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.updateRotation = true;
            _agent.speed = moveSpeed;
            _agent.autoRepath = true;
        }
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
    /// Вызывается игроком по E у стойки — клиент хочет поиграть, появляется задача посадить его за стол.
    /// </summary>
    public void OnInteract()
    {
        if (_state != State.WaitingAtCounter) return;
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
}
