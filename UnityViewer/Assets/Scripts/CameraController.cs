using UnityEngine;

/// <summary>
/// Orbit camera controller for viewing 3D character
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.2f, 0); // Eye level

    [Header("Orbit Settings")]
    public float distance = 3f;
    public float minDistance = 1f;
    public float maxDistance = 10f;
    public float height = 1.5f;
    public float minHeight = 0.5f;
    public float maxHeight = 3f;

    [Header("Rotation")]
    public float rotationSpeed = 200f;
    public float autoRotateSpeed = 10f;
    public bool autoRotate = false;

    [Header("Zoom")]
    public float zoomSpeed = 2f;
    public float smoothTime = 0.1f;

    [Header("Input")]
    public bool enableMouseInput = true;
    public KeyCode rotateKey = KeyCode.Mouse0;
    public KeyCode panKey = KeyCode.Mouse2;

    private float currentAngle = 0f;
    private float currentHeight;
    private float currentDistance;
    private Vector3 velocity = Vector3.zero;

    private void Start()
    {
        currentHeight = height;
        currentDistance = distance;
        UpdateCameraPosition();
    }

    private void Update()
    {
        if (target == null) return;

        HandleInput();
        
        if (autoRotate)
        {
            currentAngle += autoRotateSpeed * Time.deltaTime;
        }

        UpdateCameraPosition();
    }

    private void HandleInput()
    {
        if (!enableMouseInput) return;

        // Rotation with mouse drag
        if (Input.GetKey(rotateKey))
        {
            float mouseX = Input.GetAxis("Mouse X");
            currentAngle += mouseX * rotationSpeed * Time.deltaTime;
        }

        // Zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance -= scroll * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        // Height adjustment with right-click drag
        if (Input.GetKey(KeyCode.Mouse1))
        {
            float mouseY = Input.GetAxis("Mouse Y");
            currentHeight += mouseY * 0.5f;
            currentHeight = Mathf.Clamp(currentHeight, minHeight, maxHeight);
        }
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPosition = target != null ? target.position + targetOffset : targetOffset;

        // Calculate orbit position
        float angleRad = currentAngle * Mathf.Deg2Rad;
        Vector3 orbitPosition = new Vector3(
            Mathf.Sin(angleRad) * currentDistance,
            currentHeight,
            Mathf.Cos(angleRad) * currentDistance
        );

        // Smooth camera movement
        Vector3 desiredPosition = targetPosition + orbitPosition;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

        // Look at target
        transform.LookAt(targetPosition);
    }

    /// <summary>
    /// Set camera distance and height (API call)
    /// </summary>
    public void SetPosition(float newDistance, float newHeight)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        height = Mathf.Clamp(newHeight, minHeight, maxHeight);
        currentDistance = distance;
        currentHeight = height;
    }

    /// <summary>
    /// Rotate camera by angle (API call)
    /// </summary>
    public void RotateBy(float angle)
    {
        currentAngle += angle;
    }

    /// <summary>
    /// Set camera angle directly (API call)
    /// </summary>
    public void SetAngle(float angle)
    {
        currentAngle = angle;
    }

    /// <summary>
    /// Focus on a specific point
    /// </summary>
    public void FocusOn(Vector3 point)
    {
        targetOffset = point;
    }

    /// <summary>
    /// Reset camera to default position
    /// </summary>
    public void ResetCamera()
    {
        currentAngle = 0f;
        currentDistance = distance;
        currentHeight = height;
        targetOffset = new Vector3(0, 1.2f, 0);
    }
}
