using UnityEngine;

[AddComponentMenu("NewCore/Player Tunnel Light")]
public class PlayerTunnelLight : MonoBehaviour
{
        [Header("REFERENCES")]
        [Tooltip("Точка, за которой следует свет (обычно камера игрока).")]
        public Transform target;
        [Tooltip("Можно назначить существующий Light. Если пусто, будет создан автоматически.")]
        public Light playerLight;

        [Header("LIGHT SETTINGS")]
        public LightType lightType = LightType.Point;
        [Range(0f, 8f)] public float intensity = 1.2f;
        [Range(1f, 30f)] public float range = 10f;
        public Color color = new Color(1f, 0.95f, 0.8f);
        public bool castShadows = false;

        [Header("POSITIONING")]
        [Tooltip("Смещение света относительно target.")]
        public Vector3 localOffset = new Vector3(0f, 0f, 0.2f);
        [Tooltip("Следовать повороту target (обычно камеры).")]
        public bool followRotation = true;
        [Tooltip("Создать свет автоматически, если не найден в дочерних объектах.")]
        public bool createIfMissing = true;

        private void Awake()
        {
            if (target == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) target = cam.transform;
            }

            if (playerLight == null)
            {
                playerLight = GetComponentInChildren<Light>();
            }

            if (playerLight == null && createIfMissing)
            {
                var lightObject = new GameObject("PlayerTunnelLight");
                lightObject.transform.SetParent(transform, false);
                playerLight = lightObject.AddComponent<Light>();
            }

            ApplySettings();
        }

        private void LateUpdate()
        {
            if (playerLight == null) return;

            var followTarget = target != null ? target : transform;
            var lightTransform = playerLight.transform;
            lightTransform.position = followTarget.TransformPoint(localOffset);

            if (followRotation)
                lightTransform.rotation = followTarget.rotation;
        }

        private void OnValidate()
        {
            ApplySettings();
        }

    private void ApplySettings()
    {
        if (playerLight == null) return;

        playerLight.type = lightType;
        playerLight.intensity = intensity;
        playerLight.range = range;
        playerLight.color = color;
        playerLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
    }
}
