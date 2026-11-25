using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform visualModel;

    [SerializeField] private float moveSpeed = 9f;
    [SerializeField] private float airSpeedMultiplier = 0.85f;
    [SerializeField] private float rotationSpeed = 14f;
    [SerializeField] private float gravity = -24f;
    [SerializeField] private float groundedStickForce = -2f;

    [SerializeField] private float jumpHeight = 2.4f;
    [SerializeField] private int bonusAirJumps = 0;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 1.1f;
    [SerializeField] private bool preserveMomentumOnDashEnd = true;

    private CharacterController _controller;
    private IdemDittoInput _inputActions;

    private Vector2 _moveInput;
    private Vector3 _velocity;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private int _airJumpsRemaining;

    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    public bool IsDashing => _isDashing;
    public bool IsGrounded => _controller.isGrounded;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _inputActions = new IdemDittoInput();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (visualModel == null)
        {
            visualModel = transform;
        }
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Jump.performed += OnJumpPerformed;
        _inputActions.Player.Dash.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        _inputActions.Player.Jump.performed -= OnJumpPerformed;
        _inputActions.Player.Dash.performed -= OnDashPerformed;
        _inputActions.Player.Disable();
    }

    private void OnDestroy()
    {
        _inputActions?.Dispose();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        _jumpBufferTimer = jumpBufferTime;
    }

    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        if (_dashCooldownTimer <= 0f && !_isDashing)
        {
            StartDash();
        }
    }

    private void Update()
    {
        _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();

        HandleTimers();

        if (_isDashing)
        {
            UpdateDash();
            return;
        }

        UpdateGroundedAndJump();
        UpdateMovement();
        ApplyGravityAndMove();
    }

    private void HandleTimers()
    {
        if (_controller.isGrounded)
        {
            _coyoteTimer = coyoteTime;
            _airJumpsRemaining = bonusAirJumps;
        }
        else
        {
            _coyoteTimer = Mathf.Max(0f, _coyoteTimer - Time.deltaTime);
        }

        if (_jumpBufferTimer > 0f)
        {
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - Time.deltaTime);
        }

        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer = Mathf.Max(0f, _dashCooldownTimer - Time.deltaTime);
        }
    }

    private void StartDash()
    {
        Vector3 inputDir = GetCameraRelativeInputDirection();
        _dashDirection = inputDir.sqrMagnitude > 0.01f
            ? inputDir.normalized
            : (visualModel.forward.sqrMagnitude > 0.01f ? visualModel.forward.normalized : transform.forward);

        _isDashing = true;
        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _velocity.y = 0f;

        RotateToward(_dashDirection, rotationSpeed * 1.5f);
    }

    private void UpdateDash()
    {
        _dashTimer -= Time.deltaTime;
        _controller.Move(_dashDirection * (dashSpeed * Time.deltaTime));

        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            if (!preserveMomentumOnDashEnd)
            {
                _velocity.x = 0f;
                _velocity.z = 0f;
            }
            else
            {
                Vector3 carry = _dashDirection * (dashSpeed * 0.45f);
                _velocity.x = carry.x;
                _velocity.z = carry.z;
            }
        }
    }

    private void UpdateGroundedAndJump()
    {
        if (_controller.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = groundedStickForce;
        }

        if (_jumpBufferTimer > 0f)
        {
            if (_coyoteTimer > 0f)
            {
                ExecuteJump();
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }
            else if (_airJumpsRemaining > 0)
            {
                _airJumpsRemaining--;
                ExecuteJump();
                _jumpBufferTimer = 0f;
            }
        }
    }

    private void ExecuteJump()
    {
        _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void UpdateMovement()
    {
        Vector3 moveDir = GetCameraRelativeInputDirection();
        float targetSpeed = moveSpeed * (_controller.isGrounded ? 1f : airSpeedMultiplier);

        Vector3 desiredHorizontalVelocity = moveDir * targetSpeed;
        float acceleration = _controller.isGrounded ? 18f : 9f;

        _velocity.x = Mathf.Lerp(_velocity.x, desiredHorizontalVelocity.x, acceleration * Time.deltaTime);
        _velocity.z = Mathf.Lerp(_velocity.z, desiredHorizontalVelocity.z, acceleration * Time.deltaTime);

        if (moveDir.sqrMagnitude > 0.01f)
        {
            RotateToward(moveDir, rotationSpeed);
        }
    }

    private Vector3 GetCameraRelativeInputDirection()
    {
        if (_moveInput.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        if (cameraTransform == null)
        {
            return new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
        }

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
    }

    private void RotateToward(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, speed * Time.deltaTime);
    }

    private void ApplyGravityAndMove()
    {
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    public void AddBonusAirJumps(int amount)
    {
        bonusAirJumps = Mathf.Max(0, bonusAirJumps + amount);
        _airJumpsRemaining = Mathf.Max(_airJumpsRemaining, bonusAirJumps);
    }

    public void ReduceDashCooldownMultiplier(float multiplier)
    {
        dashCooldown = Mathf.Max(0.2f, dashCooldown * multiplier);
    }
}