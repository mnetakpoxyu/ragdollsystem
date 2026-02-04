using UnityEngine;
using UnityEngine.InputSystem;

namespace ElmanGameDevTools.PlayerSystem
{
    /// <summary>
    /// Advanced First Person Controller.
    /// Handles movement, crouching, jumping, and camera effects like HeadBob and Tilt.
    /// Uses Input System package; assign InputActionAsset (e.g. InputSystem_Actions) in the inspector.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Controller")]
    public class PlayerController : MonoBehaviour
    {
        [Header("REFERENCES")]
        [Tooltip("The CharacterController component used for physics-based movement.")]
        public CharacterController controller;
        [Tooltip("The Transform of the camera, usually a child of the player object.")]
        public Transform playerCamera;

        [Header("ANIMATOR")]
        [Tooltip("Ссылка на Animator 3D персонажа. Параметры Bool: isRun, isBackward (S), isStrafeLeft (A), isStrafeRight (D), Jump.")]
        public Animator animator;

        [Header("INPUT")]
        [Tooltip("Input Action Asset с картой 'Player' (Move, Look, Jump, Crouch). Бег по Shift отключён.")]
        public InputActionAsset inputActionAsset;

        [Header("MOVEMENT SETTINGS")]
        [Tooltip("Скорость ходьбы. Бег по Shift отключён — одна скорость.")]
        public float speed = 3.5f;
        [Tooltip("Высота прыжка. Увеличено для агрессивного «невесомого» полёта.")]
        public float jumpHeight = 4f;
        [Tooltip("Гравитация. Меньше по модулю = более парящее, «невесомое» ощущение в воздухе.")]
        public float gravity = -8f;
        [Tooltip("С какой скоростью достигается целевая скорость на земле (ускорение/замедление). Выше — отзывчивее, ниже — инерция.")]
        public float groundAcceleration = 35f;
        [Tooltip("Насколько можно менять горизонтальную траекторию в воздухе. Выше = больше контроля «в полёте», ощущение невесомости.")]
        [Range(0f, 1f)] public float airControlFactor = 0.45f;
        [Tooltip("Множитель скорости при движении назад (S). 1 = как вперёд, 0.6 = на 40% медленнее.")]
        [Range(0.3f, 1f)] public float backwardSpeedMultiplier = 0.6f;
        [Tooltip("Множитель скорости при стрейфе влево/вправо (A/D). 1 = как вперёд, 0.8 = на 20% медленнее.")]
        [Range(0.3f, 1f)] public float strafeSpeedMultiplier = 0.8f;
        [Tooltip("Чувствительность мыши (горизонталь — влево/вправо).")]
        [Range(0.05f, 1f)] public float sensitivity = 0.28f;
        [Tooltip("Множитель чувствительности по вертикали. 1 = одинаковая скорость вверх-вниз и влево-вправо.")]
        [Range(0.2f, 1.5f)] public float sensitivityVertical = 1f;

        [Header("CAMERA SETTINGS")]
        public float maxLookUpAngle = 90f;
        public float maxLookDownAngle = -90f;
        public bool enableHeadBob = true;
        [Range(0.01f, 0.15f)] public float bobAmountX = 0.04f;
        [Range(0.01f, 0.15f)] public float bobAmountY = 0.05f;
        public float walkBobFrequency = 12f;
        public float runBobFrequency = 16f;
        public float crouchBobFrequency = 8f;
        public float bobSmoothness = 10f;

        [Header("CAMERA INERTIA & WEIGHT")]
        [Tooltip("Время сглаживания поворота камеры (сек). 0 = мгновенный отклик мыши, без задержки.")]
        [Range(0f, 0.15f)] public float lookSmoothTime = 0f;
        [Tooltip("Макс. градусов поворота за кадр (убирает рывки от всплесков ввода).")]
        public float maxLookDeltaPerFrame = 120f;
        private float _targetYaw;
        private float _targetPitch;
        private float _currentYaw;
        private float _currentPitch;
        private float _smoothInputX;
        private float _yawVelocity;
        private float _pitchVelocity;

        [Header("CAMERA EFFECTS")]
        [Tooltip("Наклон камеры при движении влево/вправо (A/D). Включи для кинематографичного эффекта.")]
        public bool enableCameraTilt = true;
        public float tiltAmount = 2f;
        public float tiltSmoothness = 8f;
        public float runTiltMultiplier = 1.2f;
        public float crouchTiltMultiplier = 0.5f;
        [Space]
        [Tooltip("Наклон горизонта при повороте мыши. 0 = без наклона (рекомендуется для прицеливания). Больше 0 — мир «заваливается» при повороте, сложнее целиться.")]
        public float turnTiltAmount = 0f;
        public float maxTotalTilt = 5f;

        [Header("CROUCH SETTINGS")]
        public float crouchHeight = 1.2f;
        public float crouchSmoothTime = 0.1f;

        [Header("FOV SETTINGS")]
        public bool enableRunFov = true;
        public float normalFov = 60f;
        public float runFov = 70f;
        public float fovChangeSpeed = 8f;

        [Header("STANDING DETECTION & GROUND CHECK")]
        public GameObject standingHeightMarker;
        public float standingCheckRadius = 0.2f;
        public LayerMask obstacleLayerMask = ~0;
        public float minStandingClearance = 0.01f;
        public LayerMask groundLayer = 1;
        public float groundCheckDistance = 0.5f;

        private Vector3 _velocity;
        private Vector3 _horizontalVelocity;
        private float _currentTilt;
        private float _timer;
        private float _originalHeight;
        private float _targetHeight;
        private float _currentMovementSpeed;
        private float _cameraBaseHeight;
        private float _markerHeightOffset;

        private bool _isGrounded;
        private bool _isCrouching;
#pragma warning disable 0414
        private bool _hasJumped;
#pragma warning restore 0414
        private MovementState _currentMovementState = MovementState.Walking;

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _jumpTriggered;
        private bool _crouchPressed;
        private bool _isBackwardState;
        private bool _lastBackwardSent;
        private bool _lastRunSent;
        private bool _isStrafeLeftState;
        private bool _isStrafeRightState;
        private bool _lastStrafeLeftSent;
        private bool _lastStrafeRightSent;

        public enum MovementState { Walking, Running, Crouching, Jumping }

        public bool IsGrounded => _isGrounded;
        public bool IsCrouching => _isCrouching;
        public MovementState CurrentState => _currentMovementState;
        /// <summary>Current move input (Horizontal, Vertical) for external use (e.g. PlayerMusic).</summary>
        public Vector2 MoveInputVector => _moveInput;

        private void Start()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            _originalHeight = controller.height;
            _targetHeight = _originalHeight;
            _cameraBaseHeight = playerCamera.localPosition.y;

            _targetYaw = transform.eulerAngles.y;
            _targetPitch = playerCamera.localEulerAngles.x;
            _currentYaw = _targetYaw;
            _currentPitch = _targetPitch;

            if (standingHeightMarker != null)
                _markerHeightOffset = standingHeightMarker.transform.position.y - transform.position.y;

            // Если Animator не назначен вручную — ищем у себя или у дочерних (например, модель рыцаря)
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (inputActionAsset != null)
            {
                _playerMap = inputActionAsset.FindActionMap("Player");
                _moveAction = _playerMap.FindAction("Move");
                _lookAction = _playerMap.FindAction("Look");
                _jumpAction = _playerMap.FindAction("Jump");
                _crouchAction = _playerMap.FindAction("Crouch");
                _playerMap.Enable();
            }
        }

        private void OnDestroy()
        {
            _playerMap?.Disable();
        }

        private void Update()
        {
            ReadInput();
            CheckGroundStatus();
            HandleCrouchLogic();
            UpdateMovementState();
            HandleMovement();
            UpdateAnimatorIsRun();
            HandleHeightAndCamera();
            HandleCameraControl();
            HandleCameraTilt();
            HandleFovChange();

            if (enableHeadBob) HandleHeadBob();
        }

        private void ReadInput()
        {
            if (_playerMap == null)
            {
                ResetInputState();
                return;
            }

            if (IsInputBlocked())
            {
                ResetInputState();
                return;
            }

            _moveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            _lookInput = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            _jumpTriggered = _jumpAction?.triggered ?? false;
            _crouchPressed = _crouchAction?.IsPressed() ?? false;
        }

        private bool IsInputBlocked()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return true;
            PlayerInputManager inputManager = PlayerInputManager.Instance;
            return inputManager != null && inputManager.IsInputLocked;
        }

        private void ResetInputState()
        {
            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _jumpTriggered = false;
            _crouchPressed = false;
            _smoothInputX = 0f;
            _horizontalVelocity = Vector3.zero;
        }

        /// <summary>
        /// SphereCast based ground detection to ensure stability on slopes and stairs.
        /// </summary>
        private void CheckGroundStatus()
        {
            Vector3 origin = transform.position + Vector3.up * controller.radius;
            bool groundHit = Physics.SphereCast(origin, controller.radius * 0.8f, Vector3.down, out _, groundCheckDistance, groundLayer);
            _isGrounded = groundHit || controller.isGrounded;

            if (_isGrounded && _velocity.y < 0)
            {
                _hasJumped = false;
                _velocity.y = -5f;
                if (animator != null) animator.SetBool("Jump", false);
            }
        }

        private void UpdateMovementState()
        {
            // Бег по Shift отключён — всегда одна скорость (speed)
            if (!_isGrounded)
            {
                _currentMovementState = MovementState.Jumping;
                _currentMovementSpeed = speed;
                return;
            }

            if (_isCrouching)
            {
                _currentMovementState = MovementState.Crouching;
                _currentMovementSpeed = speed * 0.5f;
            }
            else
            {
                _currentMovementState = MovementState.Walking;
                _currentMovementSpeed = speed;
            }
        }

        /// <summary>Обновляет параметры аниматора: isRun, isBackward, isStrafeLeft (A), isStrafeRight (D), Jump. Гистерезис + отправка только при изменении.</summary>
        private void UpdateAnimatorIsRun()
        {
            if (animator == null) return;
            // Назад (S): держишь S — isBackward true, отпустил — сразу false
            if (_moveInput.y < -0.1f) _isBackwardState = true;
            else _isBackwardState = false;
            // isRun только от вперёд (W); A и D — только стрейф (isStrafeLeft / isStrafeRight)
            bool isRun = _moveInput.y > 0.1f && !_isBackwardState;

            // Стрейф влево (A): гистерезис по x; при отпускании (x близок к 0) — сразу false
            if (_moveInput.x < -0.15f) _isStrafeLeftState = true;
            else if (_moveInput.x >= -0.15f) _isStrafeLeftState = false;
            // Стрейф вправо (D): гистерезис по x; при отпускании (x близок к 0) — сразу false
            if (_moveInput.x > 0.15f) _isStrafeRightState = true;
            else if (_moveInput.x <= 0.15f) _isStrafeRightState = false;

            // В аниматор только при изменении
            if (_isBackwardState != _lastBackwardSent)
            {
                animator.SetBool("isBackward", _isBackwardState);
                _lastBackwardSent = _isBackwardState;
            }
            if (isRun != _lastRunSent)
            {
                animator.SetBool("isRun", isRun);
                _lastRunSent = isRun;
            }
            if (_isStrafeLeftState != _lastStrafeLeftSent)
            {
                animator.SetBool("isStrafeLeft", _isStrafeLeftState);
                _lastStrafeLeftSent = _isStrafeLeftState;
            }
            if (_isStrafeRightState != _lastStrafeRightSent)
            {
                animator.SetBool("isStrafeRight", _isStrafeRightState);
                _lastStrafeRightSent = _isStrafeRightState;
            }
        }

        private void HandleMovement()
        {
            // Направление движения по взгляду (WASD относительно камеры)
            Vector3 lookForward = Quaternion.Euler(0f, _currentYaw, 0f) * Vector3.forward;
            Vector3 lookRight = Quaternion.Euler(0f, _currentYaw, 0f) * Vector3.right;

            // Вперёд — полная скорость, назад — медленнее, стрейф — медленнее
            float forwardSpeed = _moveInput.y >= 0 ? _currentMovementSpeed : _currentMovementSpeed * backwardSpeedMultiplier;
            float strafeSpeed = _currentMovementSpeed * strafeSpeedMultiplier;
            Vector3 targetHorizontal = lookForward * (_moveInput.y * forwardSpeed) + lookRight * (_moveInput.x * strafeSpeed);

            if (_jumpTriggered && _isGrounded && !_isCrouching)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _hasJumped = true;
                _isGrounded = false;
                if (animator != null) animator.SetBool("Jump", true);
            }

            if (standingHeightMarker != null)
                standingHeightMarker.transform.position = new Vector3(transform.position.x, transform.position.y + _markerHeightOffset, transform.position.z);

            // На земле — ускорение/замедление к цели (инерция). В воздухе — почти не меняем траекторию.
            if (_isGrounded)
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetHorizontal, groundAcceleration * Time.deltaTime);
            else
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, targetHorizontal, airControlFactor);

            controller.Move(_horizontalVelocity * Time.deltaTime);
            _velocity.y += gravity * Time.deltaTime;
            controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleCrouchLogic()
        {
            _isCrouching = _crouchPressed || !CanStandUp();
            _targetHeight = _isCrouching ? crouchHeight : _originalHeight;
        }

        private void HandleHeightAndCamera()
        {
            float prevHeight = controller.height;
            controller.height = Mathf.Lerp(controller.height, _targetHeight, Time.deltaTime * (1f / crouchSmoothTime));

            if (_isGrounded)
            {
                float heightDiff = controller.height - prevHeight;
                if (heightDiff > 0) controller.Move(Vector3.up * heightDiff);
            }

            float currentRelativeHeight = _cameraBaseHeight * (controller.height / _originalHeight);
            Vector3 camPos = playerCamera.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, currentRelativeHeight, Time.deltaTime * (1f / crouchSmoothTime));
            playerCamera.localPosition = camPos;
        }

        private void HandleCameraControl()
        {
            // Нормализация: одинаковая скорость поворота в любом направлении (вверх-вниз = влево-вправо)
            float mag = _lookInput.magnitude;
            if (mag < 0.0001f)
            {
                _smoothInputX = 0f;
                if (lookSmoothTime <= 0f) { _currentYaw = _targetYaw; _currentPitch = _targetPitch; _yawVelocity = _pitchVelocity = 0f; }
                else
                {
                    _currentYaw = Mathf.SmoothDamp(_currentYaw, _targetYaw, ref _yawVelocity, lookSmoothTime, Mathf.Infinity, Time.deltaTime);
                    _currentPitch = Mathf.SmoothDamp(_currentPitch, _targetPitch, ref _pitchVelocity, lookSmoothTime, Mathf.Infinity, Time.deltaTime);
                }
                playerCamera.localRotation = Quaternion.Euler(_currentPitch, _currentYaw, _currentTilt);
                return;
            }
            Vector2 dir = _lookInput.normalized;
            float delta = mag * sensitivity * sensitivityVertical;
            delta = Mathf.Min(delta, maxLookDeltaPerFrame);
            float rawX = dir.x * delta;
            float rawY = dir.y * delta;
            _smoothInputX = rawX;

            _targetYaw += rawX;
            _targetPitch -= rawY;
            _targetPitch = Mathf.Clamp(_targetPitch, maxLookDownAngle, maxLookUpAngle);

            // Только плавное доведение камеры до цели (SmoothDamp)
            if (lookSmoothTime <= 0f)
            {
                _currentYaw = _targetYaw;
                _currentPitch = _targetPitch;
                _yawVelocity = _pitchVelocity = 0f;
            }
            else
            {
                _currentYaw = Mathf.SmoothDamp(_currentYaw, _targetYaw, ref _yawVelocity, lookSmoothTime, Mathf.Infinity, Time.deltaTime);
                _currentPitch = Mathf.SmoothDamp(_currentPitch, _targetPitch, ref _pitchVelocity, lookSmoothTime, Mathf.Infinity, Time.deltaTime);
            }

            playerCamera.localRotation = Quaternion.Euler(_currentPitch, _currentYaw, _currentTilt);
        }

        private void HandleCameraTilt()
        {
            if (!enableCameraTilt) { _currentTilt = 0; return; }

            float keyboardTilt = -_moveInput.x * tiltAmount;
            float mouseTilt = -_smoothInputX * turnTiltAmount;
            float targetTiltTotal = keyboardTilt + mouseTilt;

            if (_currentMovementState == MovementState.Running) targetTiltTotal *= runTiltMultiplier;
            if (_isCrouching) targetTiltTotal *= crouchTiltMultiplier;

            targetTiltTotal = Mathf.Clamp(targetTiltTotal, -maxTotalTilt, maxTotalTilt);
            _currentTilt = Mathf.Lerp(_currentTilt, targetTiltTotal, Time.deltaTime * tiltSmoothness);
        }

        private void HandleFovChange()
        {
            if (!enableRunFov || playerCamera.GetComponent<Camera>() == null) return;
            // Бег по Shift отключён — всегда обычный FOV
            Camera cam = playerCamera.GetComponent<Camera>();
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, normalFov, Time.deltaTime * fovChangeSpeed);
        }

        private void HandleHeadBob()
        {
            float moveMag = _moveInput.magnitude;
            float currentCamH = _cameraBaseHeight * (controller.height / _originalHeight);

            if (!_isGrounded || moveMag <= 0.1f)
            {
                _timer = 0;
                playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, new Vector3(0, currentCamH, 0), Time.deltaTime * bobSmoothness);
                return;
            }

            float freq = (_currentMovementState == MovementState.Running) ? runBobFrequency : (_isCrouching ? crouchBobFrequency : walkBobFrequency);
            _timer += Time.deltaTime * freq;

            Vector3 newPos = new Vector3(
                Mathf.Cos(_timer * 0.5f) * bobAmountX,
                currentCamH + Mathf.Sin(_timer) * bobAmountY,
                0
            );
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, newPos, Time.deltaTime * bobSmoothness);
        }

        /// <summary>
        /// Checks for obstacles above the player when trying to stand up.
        /// </summary>
        /// <returns>True if there is enough space to stand.</returns>
        public bool CanStandUp()
        {
            if (standingHeightMarker == null) return true;
            Collider[] hits = Physics.OverlapSphere(standingHeightMarker.transform.position, standingCheckRadius, obstacleLayerMask);
            foreach (Collider col in hits)
            {
                if (col.transform.IsChildOf(transform) || col.transform == transform || col.isTrigger) continue;
                if (col.bounds.min.y < standingHeightMarker.transform.position.y + minStandingClearance) return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (standingHeightMarker != null)
            {
                Gizmos.color = CanStandUp() ? Color.green : Color.red;
                Gizmos.DrawWireSphere(standingHeightMarker.transform.position, standingCheckRadius);
            }
        }
    }
}