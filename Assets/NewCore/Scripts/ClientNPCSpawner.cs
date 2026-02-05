using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнер NPC: массив префабов (разные пиплы/цвета), случайный выбор при спавне.
/// Не спавнит в Start. Вызывай извне: когда клиента посадили за комп — OnClientSentToComputer();
/// когда сессия закончилась — OnClientLeftComputer(). Спавн только если есть свободный комп.
/// Префабы могут быть «только модель» (без скриптов и коллайдеров) — тогда Template NPC обязателен:
/// с него копируются NavMeshAgent, Collider, ClientNPC и все настройки.
/// </summary>
public class ClientNPCSpawner : MonoBehaviour
{
    [Header("Шаблон NPC (обязателен для префабов-моделек)")]
    [Tooltip("NPC на сцене, с которого копируются ВСЕ компоненты и настройки. Для префабов «только модель» — обязателен. Перетащи сюда один настроенный NPC из иерархии.")]
    [SerializeField] ClientNPC templateNpc;

    [Header("Префабы NPC")]
    [Tooltip("Массив префабов (модели/цвета). Могут быть только модель без скриптов — тогда всё берётся с Template NPC (агент, коллайдер, логика).")]
    [SerializeField] GameObject[] npcPrefabs;

    [Header("Точки спавна")]
    [Tooltip("Перетащи сюда все точки (пустые объекты). Спавн — случайная из списка.")]
    [SerializeField] Transform[] spawnPoints;

    [Header("Смещение при спавне")]
    [Tooltip("Подъём над точкой (м). Подгони под высоту пола.")]
    [SerializeField] float heightOffset = 0f;
    [Tooltip("Случайный разброс по XZ (м), чтобы не стакаться в одну точку.")]
    [SerializeField] float randomRadius = 0.4f;

    [Header("Куда идут NPC")]
    [SerializeField] Transform adminSpot;
    [SerializeField] InteractableDoor[] doors;

    /// <summary>
    /// Вызвать, когда игрок посадил клиента за компьютер. Из PlayerInteract после SeatClient.
    /// </summary>
    public void OnClientSentToComputer()
    {
        TrySpawn();
    }

    /// <summary>
    /// Вызвать, когда сессия клиента закончилась и он уничтожен. Из ComputerSpot в EndSession.
    /// </summary>
    public void OnClientLeftComputer()
    {
        TrySpawn();
    }

    /// <summary>
    /// Вызвать, когда NPC ушёл от стойки (например получил напиток и пошёл обратно). Даёт шанс заспавнить следующего, если раньше не спавнили из-за занятой стойки.
    /// </summary>
    public void OnClientLeftCounter()
    {
        TrySpawn();
    }

    void TrySpawn()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0 || adminSpot == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        // Спавним по одному: следующий только когда никто не идёт к стойке и не ждёт у стойки (посадили — тогда спавн следующего).
        if (ClientNPC.CountGoingToOrAtCounter() > 0) return;
        // Есть ли место для нового клиента: свободное или сломанное (после починки можно посадить).
        if (ComputerSpot.GetRandomSpotForNewClient() == null) return;

        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
        if (prefab == null) return;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return;

        Vector3 pos = point.position + Vector3.up * heightOffset
            + new Vector3(Random.Range(-randomRadius, randomRadius), 0f, Random.Range(-randomRadius, randomRadius));
        Quaternion rot = point.rotation;

        GameObject go = Instantiate(prefab, pos, rot);
        ClientNPC npc = go.GetComponent<ClientNPC>();

        // Префаб «только модель» — нет ClientNPC. Добавляем все компоненты с шаблона.
        if (npc == null)
        {
            if (templateNpc == null)
            {
                Debug.LogError("[ClientNPCSpawner] Префаб \"" + prefab.name + "\" без ClientNPC. Задай Template NPC в спавнере (перетащи настроенного NPC со сцены).", prefab);
                Object.Destroy(go);
                return;
            }
            AddComponentsFromTemplate(go, templateNpc);
            npc = go.GetComponent<ClientNPC>();
            if (npc == null)
            {
                Debug.LogError("[ClientNPCSpawner] Не удалось добавить ClientNPC на " + prefab.name, prefab);
                Object.Destroy(go);
                return;
            }
        }
        else
        {
            // Префаб уже с ClientNPC — при необходимости добавить агент/коллайдер и скопировать настройки
            if (go.GetComponent<NavMeshAgent>() == null && templateNpc != null)
                CopyNavMeshAgentFrom(templateNpc.gameObject, go);
            if (go.GetComponent<Collider>() == null && templateNpc != null)
                CopyColliderFrom(templateNpc.gameObject, go);
        }

        if (templateNpc != null)
            npc.CopyConfigurationFrom(templateNpc);
        npc.InitializeSpawn(adminSpot, doors);
        npc.ResetStateForSpawn();
        go.transform.SetPositionAndRotation(pos, rot);
        npc.GoToCounterIfReady();
    }

    /// <summary>
    /// Для префаба «только модель»: добавляем NavMeshAgent, Collider и ClientNPC с копированием настроек с шаблона.
    /// </summary>
    void AddComponentsFromTemplate(GameObject go, ClientNPC template)
    {
        GameObject templateGo = template.gameObject;
        CopyNavMeshAgentFrom(templateGo, go);
        CopyColliderFrom(templateGo, go);
        go.AddComponent<ClientNPC>();
    }

    void CopyNavMeshAgentFrom(GameObject source, GameObject dest)
    {
        var src = source.GetComponent<NavMeshAgent>();
        if (src == null) return;
        var dst = dest.GetComponent<NavMeshAgent>();
        if (dst == null) dst = dest.AddComponent<NavMeshAgent>();
        dst.speed = src.speed;
        dst.angularSpeed = src.angularSpeed;
        dst.acceleration = src.acceleration;
        dst.stoppingDistance = src.stoppingDistance;
        dst.radius = src.radius;
        dst.height = src.height;
        dst.obstacleAvoidanceType = src.obstacleAvoidanceType;
        dst.updateRotation = src.updateRotation;
        dst.autoRepath = src.autoRepath;
    }

    void CopyColliderFrom(GameObject source, GameObject dest)
    {
        if (dest.GetComponent<Collider>() != null) return;
        var srcCap = source.GetComponent<CapsuleCollider>();
        var srcBox = source.GetComponent<BoxCollider>();
        var srcSphere = source.GetComponent<SphereCollider>();
        if (srcCap != null)
        {
            var c = dest.AddComponent<CapsuleCollider>();
            c.center = srcCap.center;
            c.radius = srcCap.radius;
            c.height = srcCap.height;
            c.direction = srcCap.direction;
            c.isTrigger = srcCap.isTrigger;
        }
        else if (srcBox != null)
        {
            var c = dest.AddComponent<BoxCollider>();
            c.center = srcBox.center;
            c.size = srcBox.size;
            c.isTrigger = srcBox.isTrigger;
        }
        else if (srcSphere != null)
        {
            var c = dest.AddComponent<SphereCollider>();
            c.center = srcSphere.center;
            c.radius = srcSphere.radius;
            c.isTrigger = srcSphere.isTrigger;
        }
        else
        {
            dest.AddComponent<CapsuleCollider>(); // дефолт для персонажа
        }
    }
}
