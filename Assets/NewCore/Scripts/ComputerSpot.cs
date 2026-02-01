using UnityEngine;

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
    [Tooltip("Цвет обводки вокруг стола.")]
    [SerializeField] Color outlineColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    [Tooltip("Толщина обводки в пикселях — ровная линия на экране.")]
    [SerializeField, Range(0.5f, 8f)] float outlineWidth = 2.5f;

    [Header("Место для посадки")]
    [Tooltip("Стул рядом с этим столом — сюда придёт NPC и сядет. Перетащи объект стула сюда.")]
    [SerializeField] Transform chair;

    [Header("Состояние места")]
    [Tooltip("Занято ли место (выставляется автоматически при посадке NPC).")]
    [SerializeField] bool isOccupied;

    static Shader _outlineShader;
    static Shader OutlineShader => _outlineShader != null ? _outlineShader : (_outlineShader = Shader.Find("NewCore/Outline Contour"));

    Renderer[] _outlineRenderers;
    Material _outlineMaterial;
    bool _highlighted;

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

    void OnDestroy()
    {
        if (_outlineMaterial != null)
            Destroy(_outlineMaterial);
    }

    public void SetHighlight(bool on)
    {
        _highlighted = on;
        if (_outlineRenderers == null) return;
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

    /// <summary> Посадить клиента за этот стол. NPC пойдёт к стулу и сядет. Возвращает true, если место было свободно. </summary>
    public bool SeatClient(ClientNPC npc)
    {
        if (npc == null || isOccupied || chair == null) return false;
        npc.GoSitAt(chair);
        isOccupied = true;
        return true;
    }
}
