using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Клиент: стоит у двери, ждёт открытия → идёт к стойке админа (в обход стен) → ждёт взаимодействия.
/// Нужен NavMeshAgent + запечённый NavMesh, чтобы не проходил сквозь стены.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshAgent))]
public class ClientNPC : MonoBehaviour
{
    public enum State
    {
        WaitingAtDoor,
        WalkingToCounter,
        WaitingAtCounter
    }

    [Header("Логика")]
    [Tooltip("Двери: клиент пойдёт к стойке только когда ВСЕ эти двери открыты. Пусто — идёт сразу.")]
    [SerializeField] InteractableDoor[] doors;
    [Tooltip("Точка у стойки админа (пустой объект) — сюда клиент приходит и ждёт.")]
    [SerializeField] Transform counterTarget;
    [Tooltip("Дистанция до стойки, при которой считаем «пришёл».")]
    [SerializeField] float arriveDistance = 0.6f;
    [Tooltip("Скорость ходьбы (используется NavMeshAgent.speed).")]
    [SerializeField] float moveSpeed = 2.5f;

    NavMeshAgent _agent;
    State _state = State.WaitingAtDoor;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.updateRotation = true;
            _agent.speed = moveSpeed;
        }
    }

    void Start()
    {
        EnsureOnNavMesh();
        if (counterTarget == null) return;
        if (doors == null || doors.Length == 0)
            GoToCounter();
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
                    GoToCounter();
                }
                break;
            case State.WalkingToCounter:
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= arriveDistance)
                        _state = State.WaitingAtCounter;
                }
                break;
            case State.WaitingAtCounter:
                break;
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

    void GoToCounter()
    {
        if (counterTarget == null) return;
        if (_agent == null) return;
        EnsureOnNavMesh();
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(counterTarget.position);
            _state = State.WalkingToCounter;
        }
    }

    /// <summary>
    /// Вызывается игроком по E у стойки. Дальше — вести к компу, брать деньги и т.д.
    /// </summary>
    public void OnInteract()
    {
        if (_state != State.WaitingAtCounter) return;
        // TODO: вести к компу, диалог, оплата и т.д.
    }

    public State CurrentState => _state;
}
