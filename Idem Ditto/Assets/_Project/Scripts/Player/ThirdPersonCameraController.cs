using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float distance = 7.5f;
    [SerializeField] private float minDistance = 1.5f;

    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float stickSensitivity = 140f;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 65f;
    [SerializeField] private bool invertY = false;

    [SerializeField] private float followSmoothSpeed = 18f;
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private float collisionBuffer = 0.35f;
    [SerializeField] private bool lockCursorInPlayMode = true;

    private IdemDittoInput _inputActions;
    private float _yaw;
    private float _pitch = 15f;
    private Vector3 _currentPosition;

    private void Awake()
    {
        _inputActions = new IdemDittoInput();

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        Vector3 startAngles = transform.eulerAngles;
        _yaw = startAngles.y;
        _pitch = Mathf.Clamp(NormalizeAngle(startAngles.x), minPitch, maxPitch);
        _currentPosition = transform.position;
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();

        if (lockCursorInPlayMode && Application.isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();

        if (Application.isPlaying)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDestroy()
    {
        _inputActions?.Dispose();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        ReadLookInput();

        Vector3 pivot = target.position + lookAtOffset;
        Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 desiredDirection = orbitRotation * Vector3.back;

        float desiredDistance = ResolveObstacleDistance(pivot, desiredDirection, distance);
        Vector3 desiredPosition = pivot + desiredDirection * desiredDistance;

        _currentPosition = Vector3.Lerp(_currentPosition, desiredPosition, followSmoothSpeed * Time.deltaTime);
        transform.position = _currentPosition;
        transform.LookAt(pivot);
    }

    private void ReadLookInput()
    {
        Vector2 lookDelta = _inputActions.Player.Look.ReadValue<Vector2>();
        if (lookDelta.sqrMagnitude < 0.0001f) return;

        bool usingMouse = Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.0001f;
        float yawDelta;
        float pitchDelta;

        if (usingMouse)
        {
            yawDelta = lookDelta.x * mouseSensitivity;
            pitchDelta = lookDelta.y * mouseSensitivity;
        }
        else
        {
            yawDelta = lookDelta.x * stickSensitivity * Time.deltaTime;
            pitchDelta = lookDelta.y * stickSensitivity * Time.deltaTime;
        }

        float ySign = invertY ? 1f : -1f;
        _yaw += yawDelta;
        _pitch = Mathf.Clamp(_pitch + pitchDelta * ySign, minPitch, maxPitch);
    }

    private float ResolveObstacleDistance(Vector3 pivot, Vector3 direction, float maxDist)
    {
        if (Physics.Raycast(pivot, direction, out RaycastHit hit, maxDist, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Clamp(hit.distance - collisionBuffer, minDistance, maxDist);
        }

        return maxDist;
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}