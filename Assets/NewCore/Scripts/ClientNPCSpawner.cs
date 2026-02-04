using UnityEngine;

/// <summary>
/// Простой спавнер NPC: один объект в сцене, точки спавна и префаб задаются в инспекторе.
/// Не спавнит в Start. Вызывай извне: когда клиента посадили за комп — вызови OnClientSentToComputer();
/// когда сессия закончилась (клиент ушёл) — вызови OnClientLeftComputer().
/// Спавн только если есть свободный комп. PlayerInteract и ComputerSpot находят спавнер через FindFirstObjectByType.
/// </summary>
public class ClientNPCSpawner : MonoBehaviour
{
    [Header("Префаб")]
    [Tooltip("Перетащи префаб из папки Project, не объект из сцены — иначе клон унаследует состояние «сидит» и будет сломан.")]
    [SerializeField] GameObject npcPrefab;

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

    void TrySpawn()
    {
        if (npcPrefab == null || adminSpot == null) return;
        if (ComputerSpot.GetRandomFreeSpot() == null) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null) return;

        Vector3 pos = point.position + Vector3.up * heightOffset
            + new Vector3(Random.Range(-randomRadius, randomRadius), 0f, Random.Range(-randomRadius, randomRadius));
        Quaternion rot = point.rotation;

        GameObject go = Instantiate(npcPrefab, pos, rot);
        var npc = go.GetComponent<ClientNPC>();
        if (npc != null)
        {
            npc.InitializeSpawn(adminSpot, doors);
            npc.ResetStateForSpawn();
            go.transform.SetPositionAndRotation(pos, rot);
        }
    }
}
