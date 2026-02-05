using UnityEngine;

/// <summary>
/// Монитор, у которого можно посмотреть камеры наблюдения.
/// Хранит список точек, между которыми переключается игрок.
/// </summary>
[AddComponentMenu("NewCore/Surveillance/Monitor")]
public class SurveillanceMonitor : MonoBehaviour
{
    [System.Serializable]
    public struct ManualCameraSlot
    {
        [Tooltip("Transform точки обзора (обычно пустышка в сцене).")]
        public Transform viewPoint;
        [Tooltip("Название камеры в UI. Пусто — имя объекта.")]
        public string label;
        [Tooltip("Доп. подпись локации.")]
        public string locationNote;
        [Tooltip("Переопределённый FOV. <= 0 — использовать FOV игрока.")]
        public float overrideFov;
        [Tooltip("Порядок сортировки для ручных камер.")]
        public int order;
    }

    public struct CameraView
    {
        public Transform viewPoint;
        public string label;
        public string locationNote;
        public float overrideFov;
        public int order;
    }

    [Tooltip("Имя в UI, например «Монитор охраны».")]
    [SerializeField] string displayName = "Монитор";

    [Header("Автосбор слотов")]
    [Tooltip("Если true, список камер собирается из дочерних компонентов SurveillanceCameraSlot.")]
    [SerializeField] bool autoCollectChildSlots = true;
    [Tooltip("Учитывать отключённые объекты при автосборе.")]
    [SerializeField] bool includeInactiveChildren = false;
    [Tooltip("Корень, в котором ищем камеры. Пусто — сам монитор.")]
    [SerializeField] Transform slotsRoot;

    [Header("Ручной список камер")]
    [Tooltip("Если нужно указать камеры вручную (без компонентов) — заполните этот список.")]
    [SerializeField] ManualCameraSlot[] manualSlots;

    [Tooltip("Слоты, заданные вручную компонентами (если autoCollectChildSlots отключён).")]
    [SerializeField] SurveillanceCameraSlot[] cameraSlots;

    [Header("Управление")]
    [Tooltip("Клавиша предыдущей камеры.")]
    [SerializeField] KeyCode prevCameraKey = KeyCode.Q;
    [Tooltip("Клавиша следующей камеры.")]
    [SerializeField] KeyCode nextCameraKey = KeyCode.E;
    [Tooltip("Клавиша выхода из видеонаблюдения.")]
    [SerializeField] KeyCode exitKey = KeyCode.R;

    readonly System.Collections.Generic.List<CameraView> _views = new System.Collections.Generic.List<CameraView>(8);

    public string DisplayName => displayName;
    public KeyCode PrevCameraKey => prevCameraKey;
    public KeyCode NextCameraKey => nextCameraKey;
    public KeyCode ExitKey => exitKey;
    public int CameraCount => _views.Count;

    void Awake()
    {
        RefreshSlots();
    }

    void OnEnable()
    {
        RefreshSlots();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            RefreshSlots();
    }

    public bool TryGetCameraView(int index, out CameraView view)
    {
        if (_views.Count == 0 || index < 0 || index >= _views.Count)
        {
            view = default;
            return false;
        }
        view = _views[index];
        return true;
    }

    public string GetCameraLabel(int index)
    {
        if (TryGetCameraView(index, out var view))
        {
            if (!string.IsNullOrEmpty(view.locationNote))
                return $"{view.label} ({view.locationNote})";
            return view.label;
        }
        return "Камера";
    }

    public void BeginViewing()
    {
        if (PlayerSurveillanceViewer.Instance == null)
        {
            Debug.LogWarning("SurveillanceMonitor: нет PlayerSurveillanceViewer на камере игрока.");
            return;
        }

        if (_views.Count == 0)
            RefreshSlots();

        if (_views.Count == 0)
        {
            Debug.LogWarning("SurveillanceMonitor: камеры не найдены. Добавьте пустышки с компонентом SurveillanceCameraSlot или заполните Manual Slots.");
            return;
        }

        PlayerSurveillanceViewer.Instance.EnterSurveillance(this);
    }

    [ContextMenu("Refresh Slots")]
    public void RefreshSlots()
    {
        _views.Clear();

        if (autoCollectChildSlots)
        {
            var root = slotsRoot != null ? slotsRoot : transform;
            cameraSlots = root.GetComponentsInChildren<SurveillanceCameraSlot>(includeInactiveChildren);
        }

        if (cameraSlots != null && cameraSlots.Length > 0)
        {
            foreach (var slot in cameraSlots)
            {
                if (slot == null) continue;
                var viewPoint = slot.GetViewPoint();
                if (viewPoint == null) continue;

                _views.Add(new CameraView
                {
                    viewPoint = viewPoint,
                    label = string.IsNullOrEmpty(slot.label) ? viewPoint.name : slot.label,
                    locationNote = slot.locationNote,
                    overrideFov = slot.overrideFov,
                    order = slot.order
                });
            }
        }

        if (manualSlots != null && manualSlots.Length > 0)
        {
            foreach (var manual in manualSlots)
            {
                if (manual.viewPoint == null) continue;
                _views.Add(new CameraView
                {
                    viewPoint = manual.viewPoint,
                    label = string.IsNullOrEmpty(manual.label) ? manual.viewPoint.name : manual.label,
                    locationNote = manual.locationNote,
                    overrideFov = manual.overrideFov,
                    order = manual.order
                });
            }
        }

        _views.Sort((a, b) =>
        {
            int orderCompare = a.order.CompareTo(b.order);
            if (orderCompare != 0) return orderCompare;
            return string.Compare(a.label, b.label, System.StringComparison.Ordinal);
        });
    }
}
