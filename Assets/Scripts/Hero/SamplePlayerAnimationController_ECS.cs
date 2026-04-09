// Refactored version of SamplePlayerAnimationController to work with ECS input system
// Removes unnecessary features (jump, crouch, lock-on, aiming) for tactical game

using ConquestTactics.Animation;
using ConquestTactics.Visual;
using Unity.Entities;
using UnityEngine;

namespace Synty.AnimationBaseLocomotion.Samples
{
    public class SamplePlayerAnimationController_ECS : MonoBehaviour
    {
        #region Enum (Simplified)

        private enum AnimationState
        {
            Base,
            Locomotion
            // REMOVED: Jump, Fall, Crouch
        }

        private enum GaitState
        {
            Idle,
            Walk,
            Run,
            Sprint
        }

        #endregion

        // Animation parameter hashes are centralized in AnimationHashes.cs



        #region Player Settings Variables

        #region Scripts/Objects

        [Header("External Components")]
        [Tooltip("Script controlling camera behavior")]
        [SerializeField]
        private HeroCameraController _cameraController;
        
        [Tooltip("When true, rotation is managed externally (e.g. NavMeshAgent for remote AI heroes)")]
        [SerializeField]
        public bool ExternalRotationControl;
        
        [Tooltip("ECS Input Adapter handles player input from ECS system")]
        [SerializeField]
        private EcsAnimationInputAdapter _inputAdapter; // CHANGED: InputReader -> EcsAnimationInputAdapter

        // Cached reference to write head look values back to ECS (BUG-002)
        private EntityVisualSync _entityVisualSync;
        private EntityManager _ecsEntityManager;
        private bool _ecsReady;
        
        [Tooltip("Animator component for controlling player animations")]
        [SerializeField]
        private Animator _animator;
        
        [Tooltip("Character Controller component for controlling player movement")]
        [SerializeField]
        private CharacterController _controller;

        #endregion

        #region Locomotion Settings

        [Header("Player Locomotion")]
        [Header("Main Settings")]
        [Tooltip("Whether the character always faces the camera facing direction")]
        [SerializeField]
        private bool _alwaysStrafe = true;
        
        [Tooltip("Slowest movement speed of the player when set to a walk state")]
        [SerializeField]
        private float _walkSpeed = 1.4f;
        
        [Tooltip("Default movement speed of the player")]
        [SerializeField]
        private float _runSpeed = 2.5f;
        
        [Tooltip("Top movement speed of the player")]
        [SerializeField]
        private float _sprintSpeed = 7f;
        
        [Tooltip("Damping factor for changing speed")]
        [SerializeField]
        private float _speedChangeDamping = 10f;
        
        [Tooltip("Rotation smoothing factor.")]
        [SerializeField]
        private float _rotationSmoothing = 10f;
        
        [Tooltip("Offset for camera rotation.")]
        [SerializeField]
        private float _cameraRotationOffset;

        #endregion

        #region Shuffle Settings

        [Header("Shuffles")]
        [Tooltip("Threshold for button hold duration.")]
        [SerializeField]
        private float _buttonHoldThreshold = 0.15f;
        
        [Tooltip("Direction of shuffling on the X-axis.")]
        [SerializeField]
        private float _shuffleDirectionX;
        
        [Tooltip("Direction of shuffling on the Z-axis.")]
        [SerializeField]
        private float _shuffleDirectionZ;

        #endregion

        #region Strafing

        [Header("Player Strafing")]
        [Tooltip("Minimum threshold for forward strafing angle.")]
        [SerializeField]
        private float _forwardStrafeMinThreshold = -55.0f;
        
        [Tooltip("Maximum threshold for forward strafing angle.")]
        [SerializeField]
        private float _forwardStrafeMaxThreshold = 125.0f;
        
        [Tooltip("Current forward strafing value.")]
        [SerializeField]
        private float _forwardStrafe = 1f;

        #endregion

        #region Head Look Settings

        [Header("Player Head Look")]
        [Tooltip("Flag indicating if head turning is enabled.")]
        [SerializeField]
        private bool _enableHeadTurn = true;
        
        [Tooltip("Delay for head turning.")]
        [SerializeField]
        private float _headLookDelay;
        
        [Tooltip("X-axis value for head turning.")]
        [SerializeField]
        private float _headLookX;
        
        [Tooltip("Y-axis value for head turning.")]
        [SerializeField]
        private float _headLookY;
        
        [Tooltip("Curve for X-axis head turning.")]
        [SerializeField]
        private AnimationCurve _headLookXCurve;

        #endregion

        #region Body Look Settings

        [Header("Player Body Look")]
        [Tooltip("Flag indicating if body turning is enabled.")]
        [SerializeField]
        private bool _enableBodyTurn = true;
        
        [Tooltip("Delay for body turning.")]
        [SerializeField]
        private float _bodyLookDelay;
        
        [Tooltip("X-axis value for body turning.")]
        [SerializeField]
        private float _bodyLookX;
        
        [Tooltip("Y-axis value for body turning.")]
        [SerializeField]
        private float _bodyLookY;
        
        [Tooltip("Curve for X-axis body turning.")]
        [SerializeField]
        private AnimationCurve _bodyLookXCurve;

        #endregion

        #region Lean Settings

        [Header("Player Lean")]
        [Tooltip("Flag indicating if leaning is enabled.")]
        [SerializeField]
        private bool _enableLean = true;
        
        [Tooltip("Delay for leaning.")]
        [SerializeField]
        private float _leanDelay;
        
        [Tooltip("Current value for leaning.")]
        [SerializeField]
        private float _leanValue;
        
        [Tooltip("Curve for leaning.")]
        [SerializeField]
        private AnimationCurve _leanCurve;
        
        [Tooltip("Delay for head leaning looks.")]
        [SerializeField]
        private float _leansHeadLooksDelay;
        
        [Tooltip("Flag indicating if an animation clip has ended.")]
        [SerializeField]
        private bool _animationClipEnd;

        #endregion

        // REMOVED: Capsule, Grounded, In-Air, Lock-on sections

        #endregion

        #region Runtime Properties (Simplified)

        private AnimationState _currentState = AnimationState.Base;
        
        // Keep locomotion variables
        private bool _isSprinting;
        private bool _isStarting;
        private bool _isStopped = true;
        private bool _isStrafing;
        private bool _isTurningInPlace;
        private bool _isWalking;
        private bool _movementInputHeld;
        private bool _movementInputPressed;
        private bool _movementInputTapped;
        
        private float _currentMaxSpeed;
        private float _locomotionStartDirection;
        private float _locomotionStartTimer;
        private float _newDirectionDifferenceAngle;
        private float _speed2D;
        private float _strafeAngle;
        private float _strafeDirectionX;
        private float _strafeDirectionZ;
        
        private GaitState _currentGait;
        private Vector3 _currentRotation = new Vector3(0f, 0f, 0f);
        private Vector3 _moveDirection;
        private Vector3 _previousRotation;
    

        #endregion

        #region Base State Variables (Simplified)

        private const float _ANIMATION_DAMP_TIME = 5f;
        private const float _STRAFE_DIRECTION_DAMP_TIME = 20f;
        private const float _INPUT_DEADZONE = 0.01f;
        private const float _VELOCITY_STOP_THRESHOLD = 0.1f;
        private const float _TURN_IN_PLACE_ANGLE = 10f;
        private const float _LOCOMOTION_START_DELAY = 0.2f;
        private const float _MAX_LEAN_ROTATION_RATE = 275.0f;
        private float _targetMaxSpeed;
        private float _rotationRate;
        private float _initialLeanValue;
        private float _initialTurnValue;
        private Vector3 _cameraForward;

        // REMOVED: Falling, gravity variables

        #endregion

        #region Animation Controller

        #region Start (Simplified)

        private void Start()
        {
            // Validate required components
            if (_inputAdapter == null)
            {
                Debug.LogError("[SamplePlayerAnimationController_ECS] EcsAnimationInputAdapter is required!");
                enabled = false;
                return;
            }

            // Auto-find HeroCameraController if not assigned
            if (_cameraController == null)
            {
                _cameraController = FindObjectOfType<HeroCameraController>();
                if (_cameraController == null)
                {
                    Debug.LogWarning("[SamplePlayerAnimationController_ECS] HeroCameraController not found - camera-related animations will be disabled");
                    // No disable the component, just continue without camera features
                }
                else
                {
                    // Auto-found HeroCameraController
                }
            }

            // REMOVED: Unnecessary event subscriptions
            // Only keep necessary events:
            _inputAdapter.onWalkToggled += ToggleWalk;
            _inputAdapter.onSprintActivated += ActivateSprint;
            _inputAdapter.onSprintDeactivated += DeactivateSprint;

            _isStrafing = _alwaysStrafe;
            SwitchState(AnimationState.Locomotion);

            if (_animator != null)
                _animator.SetBool(AnimationHashes.IsGrounded, true);

            // Cache ECS references for head look write-back (BUG-002)
            _entityVisualSync = GetComponentInParent<EntityVisualSync>(true);
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _ecsEntityManager = world.EntityManager;
                _ecsReady = true;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_inputAdapter != null)
            {
                _inputAdapter.onWalkToggled -= ToggleWalk;
                _inputAdapter.onSprintActivated -= ActivateSprint;
                _inputAdapter.onSprintDeactivated -= DeactivateSprint;
            }
        }

        #endregion

        #region Walking State

        private void ToggleWalk()
        {
            EnableWalk(!_isWalking);
        }

        private void EnableWalk(bool enable)
        {
            _isWalking = enable;
        }

        #endregion

        #region Sprinting State

        private void ActivateSprint()
        {
            EnableWalk(false);
            _isSprinting = true;
            _isStrafing = false;
        }

        private void DeactivateSprint()
        {
            _isSprinting = false;
            if (_alwaysStrafe)
            {
                _isStrafing = true;
            }
        }

        #endregion

        // REMOVED: All Aim, Lock-on, Crouch, Jump sections

        #endregion

        #region Shared State

        #region State Change (Simplified)

        private void SwitchState(AnimationState newState)
        {
            ExitCurrentState();
            EnterState(newState);
        }

        private void EnterState(AnimationState stateToEnter)
        {
            _currentState = stateToEnter;
            switch (_currentState)
            {
                case AnimationState.Base:
                    EnterBaseState();
                    break;
                case AnimationState.Locomotion:
                    EnterLocomotionState();
                    break;
                // REMOVED: Jump, Fall, Crouch cases
            }
        }

        private void ExitCurrentState()
        {
            switch (_currentState)
            {
                case AnimationState.Locomotion:
                    ExitLocomotionState();
                    break;
                // REMOVED: Jump, Fall, Crouch cases
            }
        }

        #endregion

        #region Updates (Simplified)

        private void Update()
        {
            switch (_currentState)
            {
                case AnimationState.Locomotion:
                    UpdateLocomotionState();
                    break;
                // REMOVED: Jump, Fall, Crouch cases
            }
        }

        private void UpdateAnimatorController()
        {
            // Set only necessary animator parameters
            _animator.SetFloat(AnimationHashes.LeanValue, _leanValue);
            _animator.SetFloat(AnimationHashes.HeadLookX, _headLookX);
            _animator.SetFloat(AnimationHashes.HeadLookY, _headLookY);
            _animator.SetFloat(AnimationHashes.BodyLookX, _bodyLookX);
            _animator.SetFloat(AnimationHashes.BodyLookY, _bodyLookY);

            // Write head look values to ECS so EntityVisualSync can replicate them to remote heroes (BUG-002)
            if (_ecsReady && _entityVisualSync != null)
            {
                var entity = _entityVisualSync.GetHeroEntity();
                if (entity != Entity.Null && _ecsEntityManager.Exists(entity)
                    && _ecsEntityManager.HasComponent<HeroAnimationComponent>(entity))
                {
                    var animComp = _ecsEntityManager.GetComponentData<HeroAnimationComponent>(entity);
                    animComp.headLookX = _headLookX;
                    animComp.headLookY = _headLookY;
                    _ecsEntityManager.SetComponentData(entity, animComp);
                }
            }

            _animator.SetFloat(AnimationHashes.IsStrafing, _isStrafing ? 1.0f : 0.0f);

            // MODIFICADO: Si no hay input, asegurarse de que MoveSpeed es 0 inmediatamente
            if (_inputAdapter._moveComposite.sqrMagnitude < _INPUT_DEADZONE)
            {
                _animator.SetFloat(AnimationHashes.MoveSpeed, 0);
            }
            else
            {
                _animator.SetFloat(AnimationHashes.MoveSpeed, _speed2D);
            }

            _animator.SetInteger(AnimationHashes.CurrentGait, (int) _currentGait);

            _animator.SetFloat(AnimationHashes.StrafeDirectionX, _strafeDirectionX);
            _animator.SetFloat(AnimationHashes.StrafeDirectionZ, _strafeDirectionZ);
            _animator.SetFloat(AnimationHashes.ForwardStrafe, _forwardStrafe);
            _animator.SetFloat(AnimationHashes.CameraRotationOffset, _cameraRotationOffset);

            _animator.SetBool(AnimationHashes.MovementInputHeld, _movementInputHeld);
            _animator.SetBool(AnimationHashes.MovementInputPressed, _movementInputPressed);
            _animator.SetBool(AnimationHashes.MovementInputTapped, _movementInputTapped);
            _animator.SetFloat(AnimationHashes.ShuffleDirectionX, _shuffleDirectionX);
            _animator.SetFloat(AnimationHashes.ShuffleDirectionZ, _shuffleDirectionZ);

            _animator.SetBool(AnimationHashes.IsTurningInPlace, _isTurningInPlace);
            _animator.SetBool(AnimationHashes.IsWalking, _isWalking);
            _animator.SetBool(AnimationHashes.IsStopped, _isStopped);
            _animator.SetFloat(AnimationHashes.LocomotionStartDirection, _locomotionStartDirection);

            // ADDED: Siempre establecer IsGrounded en true para evitar que el Animator quede en "Fall" state
            _animator.SetBool(AnimationHashes.IsGrounded, true);

            // REMOVED: Jump, fall, crouch, grounded, aiming parameters
        }

        #endregion

        #endregion

        #region Base State (Adapted for ECS)

        #region Setup

        private void EnterBaseState()
        {
            _previousRotation = transform.forward;
        }

        private void CalculateInput()
        {
            // ADAPTED: Use _inputAdapter instead of _inputReader
            if (_inputAdapter._movementInputDetected)
            {
                if (_inputAdapter._movementInputDuration == 0)
                {
                    _movementInputTapped = true;
                }
                else if (_inputAdapter._movementInputDuration > 0 && 
                         _inputAdapter._movementInputDuration < _buttonHoldThreshold)
                {
                    _movementInputTapped = false;
                    _movementInputPressed = true;
                    _movementInputHeld = false;
                }
                else
                {
                    _movementInputTapped = false;
                    _movementInputPressed = false;
                    _movementInputHeld = true;
                }

                _inputAdapter._movementInputDuration += Time.deltaTime;
            }
            else
            {
                _inputAdapter._movementInputDuration = 0;
                _movementInputTapped = false;
                _movementInputPressed = false;
                _movementInputHeld = false;
            }

            // ADAPTED: Use _inputAdapter._moveComposite
            if (_cameraController != null)
            {
                _moveDirection = (_cameraController.GetCameraForwardZeroedYNormalised() * _inputAdapter._moveComposite.y)
                    + (_cameraController.GetCameraRightZeroedYNormalised() * _inputAdapter._moveComposite.x);
            }
            else
            {
                // Fallback to world-space movement if no camera controller
                _moveDirection = (Vector3.forward * _inputAdapter._moveComposite.y) + (Vector3.right * _inputAdapter._moveComposite.x);
            }
        }

        #endregion

        #region Movement (Simplified)

        private void Move()
        {
            if (_inputAdapter._moveComposite.sqrMagnitude < _INPUT_DEADZONE)
            {
                _speed2D = 0f;
                return;
            }

            _speed2D = Mathf.Round(_currentMaxSpeed * 1000f) / 1000f;
        }

        // REMOVED: ApplyGravity() - not needed without jump/fall

        private void CalculateMoveDirection()
        {
            CalculateInput();

            // SIMPLIFIED: Without grounded, crouching checks
            if (_isSprinting)
            {
                _targetMaxSpeed = _sprintSpeed;
            }
            else if (_isWalking)
            {
                _targetMaxSpeed = _walkSpeed;
            }
            else
            {
                _targetMaxSpeed = _runSpeed;
            }

            _currentMaxSpeed = Mathf.Lerp(_currentMaxSpeed, _targetMaxSpeed, _ANIMATION_DAMP_TIME * Time.deltaTime);

            Vector3 playerForwardVector = transform.forward;
            _newDirectionDifferenceAngle = playerForwardVector != _moveDirection
                ? Vector3.SignedAngle(playerForwardVector, _moveDirection, Vector3.up)
                : 0f;

            CalculateGait();
        }

        private void CalculateGait()
        {
            float runThreshold = (_walkSpeed + _runSpeed) / 2;
            float sprintThreshold = (_runSpeed + _sprintSpeed) / 2;

            if (_speed2D < 0.01)
            {
                _currentGait = GaitState.Idle;
            }
            else if (_speed2D < runThreshold)
            {
                _currentGait = GaitState.Walk;
            }
            else if (_speed2D < sprintThreshold)
            {
                _currentGait = GaitState.Run;
            }
            else
            {
                _currentGait = GaitState.Sprint;
            }
        }

        private void FaceMoveDirection()
        {
            if (ExternalRotationControl) return;
            Vector3 characterForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 characterRight = new Vector3(transform.right.x, 0f, transform.right.z).normalized;
            Vector3 directionForward = new Vector3(_moveDirection.x, 0f, _moveDirection.z).normalized;

            if (_cameraController != null)
            {
                _cameraForward = _cameraController.GetCameraForwardZeroedYNormalised();
            }
            else
            {
                // Fallback to transform forward if no camera controller
                _cameraForward = transform.forward;
            }
            
            Quaternion strafingTargetRotation = Quaternion.LookRotation(_cameraForward);

            _strafeAngle = characterForward != directionForward ? Vector3.SignedAngle(characterForward, directionForward, Vector3.up) : 0f;

            _isTurningInPlace = false;

            if (_isStrafing)
            {
                if (_moveDirection.magnitude > 0.01)
                {
                    if (_cameraForward != Vector3.zero)
                    {
                        _shuffleDirectionZ = Vector3.Dot(characterForward, directionForward);
                        _shuffleDirectionX = Vector3.Dot(characterRight, directionForward);

                        UpdateStrafeDirection(
                            Vector3.Dot(characterForward, directionForward),
                            Vector3.Dot(characterRight, directionForward)
                        );
                        _cameraRotationOffset = Mathf.Lerp(_cameraRotationOffset, 0f, _rotationSmoothing * Time.deltaTime);

                        float targetValue = _strafeAngle > _forwardStrafeMinThreshold && _strafeAngle < _forwardStrafeMaxThreshold ? 1f : 0f;

                        if (Mathf.Abs(_forwardStrafe - targetValue) <= 0.001f)
                        {
                            _forwardStrafe = targetValue;
                        }
                        else
                        {
                            float t = Mathf.Clamp01(_STRAFE_DIRECTION_DAMP_TIME * Time.deltaTime);
                            _forwardStrafe = Mathf.SmoothStep(_forwardStrafe, targetValue, t);
                        }
                    }

                    transform.rotation = Quaternion.Slerp(transform.rotation, strafingTargetRotation, _rotationSmoothing * Time.deltaTime);
                }
                else
                {
                    UpdateStrafeDirection(1f, 0f);

                    float t = 20 * Time.deltaTime;
                    float newOffset = 0f;

                    if (characterForward != _cameraForward)
                    {
                        newOffset = Vector3.SignedAngle(characterForward, _cameraForward, Vector3.up);
                    }

                    _cameraRotationOffset = Mathf.Lerp(_cameraRotationOffset, newOffset, t);

                    if (Mathf.Abs(_cameraRotationOffset) > _TURN_IN_PLACE_ANGLE)
                    {
                        _isTurningInPlace = true;
                    }
                }
            }
            else
            {
                UpdateStrafeDirection(1f, 0f);
                _cameraRotationOffset = Mathf.Lerp(_cameraRotationOffset, 0f, _rotationSmoothing * Time.deltaTime);

                _shuffleDirectionZ = 1;
                _shuffleDirectionX = 0;

                Vector3 faceDirection = new Vector3(_moveDirection.x, 0f, _moveDirection.z);

                if (faceDirection == Vector3.zero)
                {
                    return;
                }

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(faceDirection),
                    _rotationSmoothing * Time.deltaTime
                );
            }
        }

        private void CheckIfStopped()
        {
            // MODIFICADO: Usar threshold más bajo y considerar el input directamente
            bool noInput = _inputAdapter._moveComposite.sqrMagnitude < _INPUT_DEADZONE;
            bool noVelocity = _speed2D < _VELOCITY_STOP_THRESHOLD;
            
            _isStopped = noInput || (noVelocity && _moveDirection.magnitude == 0);
            
            if (noInput && !noVelocity)
            {
                // Si no hay input pero todavía hay velocidad, forzar la detención
                _speed2D = 0;
            }
        }

        private void CheckIfStarting()
        {
            _locomotionStartTimer = VariableOverrideDelayTimer(_locomotionStartTimer);

            bool isStartingCheck = false;

            if (_locomotionStartTimer <= 0.0f)
            {
                if (_moveDirection.magnitude > 0.01 && _speed2D < 1 && !_isStrafing)
                {
                    isStartingCheck = true;
                }

                if (isStartingCheck)
                {
                    if (!_isStarting)
                    {
                        _locomotionStartDirection = _newDirectionDifferenceAngle;
                        _animator.SetFloat(AnimationHashes.LocomotionStartDirection, _locomotionStartDirection);
                    }

                    _leanDelay = _LOCOMOTION_START_DELAY;
                    _headLookDelay = _LOCOMOTION_START_DELAY;
                    _bodyLookDelay = _LOCOMOTION_START_DELAY;
                    _locomotionStartTimer = _LOCOMOTION_START_DELAY;
                }
            }
            else
            {
                isStartingCheck = true;
            }

            _isStarting = isStartingCheck;
            _animator.SetBool(AnimationHashes.IsStarting, _isStarting);
        }

        private void UpdateStrafeDirection(float TargetZ, float TargetX)
        {
            _strafeDirectionZ = Mathf.Lerp(_strafeDirectionZ, TargetZ, _ANIMATION_DAMP_TIME * Time.deltaTime);
            _strafeDirectionX = Mathf.Lerp(_strafeDirectionX, TargetX, _ANIMATION_DAMP_TIME * Time.deltaTime);
            _strafeDirectionZ = Mathf.Round(_strafeDirectionZ * 1000f) / 1000f;
            _strafeDirectionX = Mathf.Round(_strafeDirectionX * 1000f) / 1000f;
        }

        #endregion

        #region Checks

        private void CheckEnableTurns()
        {
            _headLookDelay = VariableOverrideDelayTimer(_headLookDelay);
            _enableHeadTurn = _headLookDelay == 0.0f && !_isStarting;
            _bodyLookDelay = VariableOverrideDelayTimer(_bodyLookDelay);
            _enableBodyTurn = _bodyLookDelay == 0.0f && !(_isStarting || _isTurningInPlace);
        }

        private void CheckEnableLean()
        {
            _leanDelay = VariableOverrideDelayTimer(_leanDelay);
            _enableLean = _leanDelay == 0.0f && !(_isStarting || _isTurningInPlace);
        }

        #endregion

        #region Lean and Offsets

        private void CalculateRotationalAdditives(bool leansActivated, bool headLookActivated, bool bodyLookActivated)
        {
            if (headLookActivated || leansActivated || bodyLookActivated)
            {
                _currentRotation = transform.forward;

                _rotationRate = _currentRotation != _previousRotation
                    ? Vector3.SignedAngle(_currentRotation, _previousRotation, Vector3.up) / Time.deltaTime * -1f
                    : 0f;
            }

            _initialLeanValue = leansActivated ? _rotationRate : 0f;

            float leanSmoothness = 5;

            float referenceValue = _speed2D / _sprintSpeed;
            _leanValue = CalculateSmoothedValue(
                _leanValue,
                _initialLeanValue,
                _MAX_LEAN_ROTATION_RATE,
                leanSmoothness,
                _leanCurve,
                referenceValue,
                true
            );

            float headTurnSmoothness = 5f;

            if (headLookActivated && _isTurningInPlace)
            {
                _initialTurnValue = _cameraRotationOffset;
                _headLookX = Mathf.Lerp(_headLookX, _initialTurnValue / 200, 5f * Time.deltaTime);
            }
            else
            {
                _initialTurnValue = headLookActivated ? _rotationRate : 0f;
                _headLookX = CalculateSmoothedValue(
                    _headLookX,
                    _initialTurnValue,
                    _MAX_LEAN_ROTATION_RATE,
                    headTurnSmoothness,
                    _headLookXCurve,
                    _headLookX,
                    false
                );
            }

            float bodyTurnSmoothness = 5f;

            _initialTurnValue = bodyLookActivated ? _rotationRate : 0f;

            _bodyLookX = CalculateSmoothedValue(
                _bodyLookX,
                _initialTurnValue,
                _MAX_LEAN_ROTATION_RATE,
                bodyTurnSmoothness,
                _bodyLookXCurve,
                _bodyLookX,
                false
            );

            float cameraTilt = 0f;
            if (_cameraController != null)
            {
                cameraTilt = _cameraController.GetCameraTiltX();
                cameraTilt = (cameraTilt > 180f ? cameraTilt - 360f : cameraTilt) / -180;
                cameraTilt = Mathf.Clamp(cameraTilt, -0.1f, 1.0f);
            }
            _headLookY = cameraTilt;
            _bodyLookY = cameraTilt;

            _previousRotation = _currentRotation;
        }

        private float CalculateSmoothedValue(
            float mainVariable,
            float newValue,
            float maxRateChange,
            float smoothness,
            AnimationCurve referenceCurve,
            float referenceValue,
            bool isMultiplier
        )
        {
            float changeVariable = newValue / maxRateChange;

            changeVariable = Mathf.Clamp(changeVariable, -1.0f, 1.0f);

            if (isMultiplier)
            {
                float multiplier = referenceCurve.Evaluate(referenceValue);
                changeVariable *= multiplier;
            }
            else
            {
                changeVariable = referenceCurve.Evaluate(changeVariable);
            }

            if (!changeVariable.Equals(mainVariable))
            {
                changeVariable = Mathf.Lerp(mainVariable, changeVariable, smoothness * Time.deltaTime);
            }

            return changeVariable;
        }

        private float VariableOverrideDelayTimer(float timeVariable)
        {
            if (timeVariable > 0.0f)
            {
                timeVariable -= Time.deltaTime;
                timeVariable = Mathf.Clamp(timeVariable, 0.0f, 1.0f);
            }
            else
            {
                timeVariable = 0.0f;
            }

            return timeVariable;
        }

        #endregion

        #endregion

        #region Locomotion State (Simplified)

        private void EnterLocomotionState()
        {
            // REMOVED: Jump event subscription - not needed
        }

        private void UpdateLocomotionState()
        {
            // REMOVED: UpdateBestTarget(), GroundedCheck()
            // REMOVED: Transitions to Jump/Fall/Crouch

            CheckEnableTurns();
            CheckEnableLean();
            CalculateRotationalAdditives(_enableLean, _enableHeadTurn, _enableBodyTurn);

            CalculateMoveDirection();
            CheckIfStarting();
            CheckIfStopped();
            FaceMoveDirection();
            Move();
            UpdateAnimatorController();
        }

        private void ExitLocomotionState()
        {
            // REMOVED: Jump event unsubscription - not needed
        }

        #endregion

        // REMOVED: All Jump State, Fall State, Crouch State sections
    }
}
