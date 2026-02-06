using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнер NPC: рандомно по таймеру спавнит 1–2 NPC, они идут к админ-стойке. Не привязан к свободным слотам.
/// Если слотов нет — игрок нажимает E на NPC у стойки, NPC уходит обратно по траектории спавна и исчезает.
/// </summary>
public class ClientNPCSpawner : MonoBehaviour
{
    [Header("Шаблон NPC (обязателен для префабов-моделек)")]
    [Tooltip("NPC на сцене, с которого копируются настройки. Перетащи сюда один настроенный NPC из иерархии.")]
    [SerializeField] ClientNPC templateNpc;

    [Header("Префабы NPC")]
    [SerializeField] GameObject[] npcPrefabs;

    [Header("Точки спавна (отсюда приходят и сюда уходят при «отправить обратно»)")]
    [SerializeField] Transform[] spawnPoints;

    [Header("Смещение при спавне")]
    [SerializeField] float heightOffset = 0f;
    [SerializeField] float randomRadius = 0.4f;

    [Header("Куда идут NPC")]
    [SerializeField] Transform adminSpot;
    [SerializeField] InteractableDoor[] doors;

    [Header("Лимит очереди у стойки")]
    [Tooltip("Не спавним, если столько NPC уже идут к стойке или ждут у стойки. 0 = без лимита. Так спавн не блокируется: кто сел — освободил очередь.")]
    [SerializeField] int maxNpcsAtCounter = 6;

    [Header("Таймер спавна (реальное время)")]
    [Tooltip("Задержка перед первым спавном (сек).")]
    [SerializeField] Vector2 firstSpawnDelay = new Vector2(3f, 8f);
    [Tooltip("Интервал между волнами: каждые 15–40 сек (рандом).")]
    [SerializeField] Vector2 spawnInterval = new Vector2(15f, 40f);
    [Tooltip("Сколько NPC спавнить за раз: 1 или 2 (рандом).")]
    [SerializeField] Vector2Int spawnCountPerWave = new Vector2Int(1, 2);

    float _nextSpawnTime;

    void Start()
    {
        _nextSpawnTime = Time.time + Random.Range(firstSpawnDelay.x, firstSpawnDelay.y);
    }

    void Update()
    {
        if (Time.time < _nextSpawnTime) return;

        ComputerSpot.ReconcileAllSpots();

        int count = Mathf.Clamp(Random.Range(spawnCountPerWave.x, spawnCountPerWave.y + 1), 1, 2);
        for (int i = 0; i < count; i++)
            TrySpawnOne();

        _nextSpawnTime = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
    }

    public void OnClientSentToComputer() { }
    public void OnClientLeftComputer() { }
    public void OnClientLeftCounter() { }

    /// <summary> Спавн одного NPC. Идёт к стойке. Блокируем только если очередь у стойки переполнена (maxNpcsAtCounter). </summary>
    bool TrySpawnOne()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0 || adminSpot == null) return false;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;
        if (maxNpcsAtCounter > 0 && ClientNPC.CountGoingToOrAtCounter() >= maxNpcsAtCounter) return false;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return false;

        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
        if (prefab == null) return false;

        Vector3 pos = point.position + Vector3.up * heightOffset
            + new Vector3(Random.Range(-randomRadius, randomRadius), 0f, Random.Range(-randomRadius, randomRadius));
        Quaternion rot = point.rotation;

        GameObject go = Object.Instantiate(prefab, pos, rot);
        ClientNPC npc = go.GetComponent<ClientNPC>();

        if (npc == null)
        {
            if (templateNpc == null)
            {
                Debug.LogError("[ClientNPCSpawner] Префаб без ClientNPC. Задай Template NPC.", prefab);
                Object.Destroy(go);
                return false;
            }
            AddComponentsFromTemplate(go, templateNpc);
            npc = go.GetComponent<ClientNPC>();
            if (npc == null)
            {
                Debug.LogError("[ClientNPCSpawner] Не удалось добавить ClientNPC.", prefab);
                Object.Destroy(go);
                return false;
            }
        }
        else
        {
            if (go.GetComponent<NavMeshAgent>() == null && templateNpc != null)
                CopyNavMeshAgentFrom(templateNpc.gameObject, go);
            if (go.GetComponent<Collider>() == null && templateNpc != null)
                CopyColliderFrom(templateNpc.gameObject, go);
        }

        if (templateNpc != null)
            npc.CopyConfigurationFrom(templateNpc);
        npc.InitializeSpawn(adminSpot, doors);
        npc.ResetStateForSpawn();
        npc.SetExitPoint(point);
        go.transform.SetPositionAndRotation(pos, rot);
        npc.GoToCounterIfReady();
        return true;
    }

    void AddComponentsFromTemplate(GameObject go, ClientNPC template)
    {
        CopyNavMeshAgentFrom(template.gameObject, go);
        CopyColliderFrom(template.gameObject, go);
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
            dest.AddComponent<CapsuleCollider>();
    }
}
