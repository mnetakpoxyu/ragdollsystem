using UnityEngine;

/// <summary>
/// Спавнер клиентов NPC. Максимум 4 NPC одновременно.
/// Спавнит нового, когда кто-то сел за стол или когда сессия NPC закончилась.
/// </summary>
public class ClientNPCSpawner : MonoBehaviour
{
    static ClientNPCSpawner _instance;
    public static ClientNPCSpawner Instance => _instance;

    [Header("Спавн")]
    [Tooltip("Префаб клиента (ClientNPC).")]
    [SerializeField] GameObject clientPrefab;
    [Tooltip("Точка появления нового NPC.")]
    [SerializeField] Transform spawnPoint;

    [Header("Маршрут")]
    [Tooltip("Точка у стойки — куда идёт NPC.")]
    [SerializeField] Transform counterTarget;
    [Tooltip("Двери, которые должны быть открыты. Пусто — идёт сразу.")]
    [SerializeField] InteractableDoor[] doors;

    [Header("Лимиты")]
    [Tooltip("Максимум NPC одновременно.")]
    [SerializeField] int maxClients = 4;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// Текущее количество активных NPC.
    /// </summary>
    public int ActiveClientCount
    {
        get
        {
            var npcs = FindObjectsOfType<ClientNPC>();
            return npcs != null ? npcs.Length : 0;
        }
    }

    /// <summary>
    /// Спавнит нового NPC, если не достигнут лимит. Вызывается ComputerSpot.
    /// </summary>
    /// <param name="oneLeaving">true — один NPC скоро исчезнет (сессия закончилась).</param>
    public void TrySpawn(bool oneLeaving = false)
    {
        int count = ActiveClientCount;
        if (oneLeaving) count--; // Учитываем того, кого уничтожаем
        if (count >= maxClients) return;
        if (clientPrefab == null)
        {
            Debug.LogWarning("ClientNPCSpawner: Префаб клиента не задан!");
            return;
        }

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject go = Instantiate(clientPrefab, pos, rot);

        var npc = go.GetComponent<ClientNPC>();
        if (npc != null)
        {
            npc.InitializeSpawn(counterTarget, doors);
            Debug.Log($"ClientNPCSpawner: Заспавнен NPC. Всего: {ActiveClientCount}");
        }
    }

    void Start()
    {
        // Первый NPC при старте
        TrySpawn();
    }
}
