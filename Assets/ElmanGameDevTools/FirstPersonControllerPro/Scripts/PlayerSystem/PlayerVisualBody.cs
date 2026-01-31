using UnityEngine;

namespace ElmanGameDevTools.PlayerSystem
{
    /// <summary>
    /// Привязывает 3D модель (например, рыцаря) к персонажу от первого лица.
    /// Камера не рисует голову — видно только тело. Смещение опускает модель относительно камеры.
    /// </summary>
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Visual Body")]
    public class PlayerVisualBody : MonoBehaviour
    {
        [Header("Модель")]
        [Tooltip("3D модель персонажа (рыцарь и т.д.). Будет привязана к этому объекту и двигаться вместе с игроком.")]
        [SerializeField] private Transform visualBody;

        [Tooltip("Опустить модель вниз относительно камеры (если рыцарь «парит» — увеличь отрицательный Y, например -1.5 или -2).")]
        [SerializeField] private Vector3 localPositionOffset = new Vector3(0f, -1.5f, 0f);

        [Tooltip("Скрыть стандартную капсулу/меш на этом объекте.")]
        [SerializeField] private bool hideDefaultBody = true;

        [Tooltip("Если включено: своё тело не видно в первом лице (только мир и руки/оружие). Рекомендуется для классического FPS.")]
        [SerializeField] private bool hideBodyInFirstPerson = true;

        [Header("Голова (только тело от первого лица)")]
        [Tooltip("Перетащи сюда голову (например Knight_Head). Будет скрыта от камеры через слой, но останется привязанной к телу — не отрывается.")]
        [SerializeField] private Transform head;

        [Tooltip("Шлем, визор и т.д. — перетащи сюда Knight_Helmet и объект визора. Будут скрыты от камеры, остаются на модели.")]
        [SerializeField] private Transform[] headPartsToHide;

        [Tooltip("Если включено: голова и шлем остаются дочерними к модели (не отрываются от тела). Рекомендуется для скинов с общим скелетом (Knight и т.д.).")]
        [SerializeField] private bool keepHeadAttachedToBody = true;

        [Tooltip("Кость головы (сразу следует за камерой). Перетащи сюда head из скелета (Knight → Rig_Medium → root → hips → spine → chest → head). Если пусто — берётся из Animator.")]
        [SerializeField] private Transform headBone;

        [Tooltip("Кость тела (грудь/плечи), которая подтягивается за головой. Перетащи сюда chest из скелета (Knight → Rig_Medium → root → hips → spine → chest). Если пусто — берётся из Animator (Chest или Spine).")]
        [SerializeField] private Transform bodyBone;

        [Tooltip("Как быстро тело подтягивается за головой. Сначала поворачивается голова, потом тело.")]
        [SerializeField] [Min(0.1f)] private float bodyFollowHeadSpeed = 2.5f;

        [Tooltip("Когда угол между телом и взглядом большой (быстрый поворот 360°), тело догоняет быстрее. Множитель к скорости (2 = в 2 раза быстрее при большом угле).")]
        [SerializeField] [Min(1f)] private float bodyCatchUpWhenFarMultiplier = 3f;

        [Tooltip("Ограничить наклон головы вверх (градусы), чтобы голова не входила в тело. Например 60.")]
        [SerializeField] [Range(20f, 90f)] private float maxHeadPitchUp = 60f;

        [Tooltip("Ограничить наклон головы вниз (градусы, отрицательное), чтобы голова не входила в тело. Например -50.")]
        [SerializeField] [Range(-90f, -20f)] private float maxHeadPitchDown = -50f;

        [Tooltip("Имя слоя для головы (Edit → Project Settings → Tags and Layers).")]
        [SerializeField] private string firstPersonHeadLayerName = "FirstPersonHead";

        [Tooltip("Камера от первого лица (если не задана — ищется среди дочерних объектов).")]
        [SerializeField] private Camera firstPersonCamera;

        [Tooltip("Включи, если голова всё равно видна (полностью выключит рендер головы для этой камеры).")]
        [SerializeField] private bool forceHideHeadRenderers;

        private Camera _cachedCamera;
        private int _headLayerIndex = -1;
        private bool _headSetupDone;
        private Animator _bodyAnimator;
        private Transform _headBone;
        private Transform _chestBone;
        private float _bodyYaw;           // свой угол тела — подтягивается к камере с задержкой
        private bool _bodyYawInitialized;

        private void Start()
        {
            if (visualBody != null)
            {
                visualBody.SetParent(transform);
                visualBody.localPosition = localPositionOffset;
                visualBody.localRotation = Quaternion.identity;
                visualBody.localScale = Vector3.one;
            }

            if (hideDefaultBody)
            {
                var r = GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }

            SetupHeadHiddenFromCamera();
        }

        private void LateUpdate()
        {
            if (!_headSetupDone || _cachedCamera == null) return;

            if (keepHeadAttachedToBody)
            {
                Transform cam = _cachedCamera.transform;
                float cameraYaw = cam.eulerAngles.y;

                if (!_bodyYawInitialized)
                {
                    _bodyYaw = cameraYaw;
                    _bodyYawInitialized = true;
                }

                // 1) Тело поворачивается с задержкой; при большом угле (быстрый 360°) догоняет быстрее
                if (visualBody != null && bodyFollowHeadSpeed > 0f)
                {
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(_bodyYaw, cameraYaw));
                    float speedMultiplier = Mathf.Lerp(1f, bodyCatchUpWhenFarMultiplier, Mathf.Clamp01(angleDiff / 180f));
                    float effectiveSpeed = bodyFollowHeadSpeed * speedMultiplier;
                    _bodyYaw = Mathf.LerpAngle(_bodyYaw, cameraYaw, effectiveSpeed * Time.deltaTime);
                    visualBody.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
                }

                // 2) Голова — сразу за камерой, но pitch (вверх/вниз) ограничен, чтобы не входила в тело
                if (_headBone != null)
                {
                    Vector3 camEuler = cam.eulerAngles;
                    float pitch = camEuler.x;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, maxHeadPitchDown, maxHeadPitchUp);
                    _headBone.rotation = Quaternion.Euler(pitch, camEuler.y, camEuler.z);
                }
                return;
            }

            Transform camTransform = _cachedCamera.transform;

            if (head != null)
            {
                if (head.parent != camTransform) head.SetParent(camTransform);
                head.localPosition = Vector3.zero;
                head.localRotation = Quaternion.identity;
                head.localScale = Vector3.one;
            }

            foreach (Transform t in headPartsToHide ?? System.Array.Empty<Transform>())
            {
                if (t == null) continue;
                if (t.parent != camTransform) t.SetParent(camTransform);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }
        }

        private void SetupHeadHiddenFromCamera()
        {
            _cachedCamera = firstPersonCamera != null ? firstPersonCamera : GetComponentInChildren<Camera>();
            if (_cachedCamera == null)
            {
                Debug.LogWarning("PlayerVisualBody: камера от первого лица не найдена.", this);
                return;
            }

            if (keepHeadAttachedToBody && visualBody != null)
            {
                _bodyAnimator = visualBody.GetComponent<Animator>();
                if (_bodyAnimator != null)
                {
                    _headBone = headBone != null ? headBone : _bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
                    _chestBone = bodyBone != null ? bodyBone : _bodyAnimator.GetBoneTransform(HumanBodyBones.Chest);
                    if (_chestBone == null)
                        _chestBone = _bodyAnimator.GetBoneTransform(HumanBodyBones.Spine);
                }
                if (_headBone == null)
                    Debug.LogWarning("PlayerVisualBody: у модели (Visual Body) не найден Humanoid-скелет или кость Head. Голова не будет поворачиваться за камерой.", this);
            }

            _headLayerIndex = LayerMask.NameToLayer(firstPersonHeadLayerName);
            if (_headLayerIndex < 0)
            {
                Debug.LogWarning(
                    "PlayerVisualBody: слой \"" + firstPersonHeadLayerName + "\" не найден. " +
                    "Edit → Project Settings → Tags and Layers. Включи «Force Hide Head Renderers» как запасной вариант.", this);
            }

            // Скрыть всё тело от камеры первого лица (не видно ни тела, ни головы)
            if (hideBodyInFirstPerson && visualBody != null && _headLayerIndex >= 0)
            {
                SetLayerRecursively(visualBody.gameObject, _headLayerIndex);
                _cachedCamera.cullingMask &= ~(1 << _headLayerIndex);
            }

            if (head != null)
            {
                // Слой: камера не рисует голову
                if (_headLayerIndex >= 0)
                    SetLayerRecursively(head.gameObject, _headLayerIndex);

                if (!keepHeadAttachedToBody)
                {
                    head.SetParent(_cachedCamera.transform);
                    head.localPosition = Vector3.zero;
                    head.localRotation = Quaternion.identity;
                    head.localScale = Vector3.one;
                    DisableAnimatorsRecursively(head.gameObject);
                }

                if (forceHideHeadRenderers)
                    SetHeadRenderersEnabled(false);

                if (_headLayerIndex >= 0)
                    _cachedCamera.cullingMask &= ~(1 << _headLayerIndex);
                _headSetupDone = true;
            }

            foreach (Transform t in headPartsToHide ?? System.Array.Empty<Transform>())
            {
                if (t == null) continue;
                if (_headLayerIndex >= 0) SetLayerRecursively(t.gameObject, _headLayerIndex);
                if (!keepHeadAttachedToBody)
                {
                    t.SetParent(_cachedCamera.transform);
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.identity;
                    t.localScale = Vector3.one;
                    DisableAnimatorsRecursively(t.gameObject);
                }
                if (forceHideHeadRenderers) SetRenderersEnabledRecursively(t.gameObject, false);
            }
        }

        private void SetHeadRenderersEnabled(bool enabled)
        {
            if (head == null) return;
            SetRenderersEnabledRecursively(head.gameObject, enabled);
        }

        private static void SetRenderersEnabledRecursively(GameObject go, bool enabled)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = enabled;
            for (int i = 0; i < go.transform.childCount; i++)
                SetRenderersEnabledRecursively(go.transform.GetChild(i).gameObject, enabled);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        private static void DisableAnimatorsRecursively(GameObject go)
        {
            var anim = go.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            for (int i = 0; i < go.transform.childCount; i++)
                DisableAnimatorsRecursively(go.transform.GetChild(i).gameObject);
        }
    }
}
