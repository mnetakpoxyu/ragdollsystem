using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Точка туалета. Вешается на объект туалета в сцене.
/// NPC подходит сюда, «исчезает» на время (имитация внутри), затем выходит и идёт обратно.
/// Один туалет = один NPC одновременно. Если занят — другой NPC не может войти.
/// </summary>
public class ToiletSpot : MonoBehaviour
{
    [Header("Маршрут (важно для корректного пути)")]
    [Tooltip("Точка входа в туалетную комнату (Empty в дверном проёме). NPC сначала идёт сюда, потом к туалету — так не прёт через лампы и стены. Пусто — идёт напрямую.")]
    [SerializeField] Transform entrancePoint;
    [Tooltip("Точка, к которой идёт NPC (перед дверью/кабинкой). Пусто — центр этого объекта.")]
    [SerializeField] Transform approachPoint;

    /// <summary> Есть ли точка входа (NPC должен пройти через неё). </summary>
    public bool HasEntrancePoint => entrancePoint != null;

    /// <summary> Позиция входа в туалетную комнату (на NavMesh). </summary>
    public Vector3 EntrancePosition => GetPositionOnNavMesh(entrancePoint != null ? entrancePoint.position : transform.position);

    /// <summary> Позиция, к которой NPC идёт (подход к туалету, на NavMesh). </summary>
    public Vector3 ApproachPosition => GetPositionOnNavMesh(approachPoint != null ? approachPoint.position : transform.position);

    static Vector3 GetPositionOnNavMesh(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return hit.position;
        return pos;
    }

    /// <summary> Занят ли туалет сейчас. </summary>
    public bool IsOccupied => _occupyingNpc != null;

    /// <summary> NPC, который сейчас в этом туалете. </summary>
    public ClientNPC OccupyingNpc => _occupyingNpc;

    ClientNPC _occupyingNpc;

    /// <summary>
    /// Попытаться занять туалет. Возвращает true, если успешно (туалет был свободен).
    /// </summary>
    public bool TryOccupy(ClientNPC npc)
    {
        if (npc == null || _occupyingNpc != null) return false;
        _occupyingNpc = npc;
        return true;
    }

    /// <summary>
    /// Освободить туалет (NPC вышел).
    /// </summary>
    public void Release()
    {
        _occupyingNpc = null;
    }

    /// <summary>
    /// Найти случайный свободный туалет. null если все заняты.
    /// </summary>
    public static ToiletSpot GetRandomFreeToilet()
    {
        var all = FindObjectsByType<ToiletSpot>(FindObjectsSortMode.None);
        var free = new System.Collections.Generic.List<ToiletSpot>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].IsOccupied)
                free.Add(all[i]);
        }
        if (free.Count == 0) return null;
        return free[Random.Range(0, free.Count)];
    }

    /// <summary>
    /// Количество свободных туалетов.
    /// </summary>
    public static int GetFreeToiletCount()
    {
        int count = 0;
        var all = FindObjectsByType<ToiletSpot>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].IsOccupied)
                count++;
        }
        return count;
    }
}
