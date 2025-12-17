using MazeGenerator.Scripts;
using UnityEngine;

/// <summary>
///     Camera controller for cube maze.
///     Positions the camera at a fixed angle above the cube to capture the entire maze.
/// </summary>
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [Header("References")]

    [Tooltip("Reference to the MazeGenerator. If null, will try to find it.")]
    [SerializeField] private DynamicMazeGenerator dynamicMazeGenerator;

    [Header("Camera Settings")]

    [Tooltip("The angle in degrees above the horizontal plane.")]
    [SerializeField] [Range(30f, 90f)] private float fixedAngle = 45f;

    [Tooltip("Horizontal direction offset in degrees.")]
    [SerializeField] [Range(0f, 360f)] private float fixedHorizontalAngle = 180f;

    [Tooltip("Distance multiplier based on maze size.")]
    [SerializeField] private float fixedDistanceMultiplier = 1.0f;

    /// <summary>
    /// Gets or sets the camera angle above the horizontal plane (30-90 degrees).
    /// </summary>
    public float FixedAngle
    {
        get => fixedAngle;
        set => fixedAngle = Mathf.Clamp(value, 30f, 90f);
    }

    /// <summary>
    /// Gets or sets the distance multiplier based on maze size (0.5-5.0).
    /// </summary>
    public float FixedDistanceMultiplier
    {
        get => fixedDistanceMultiplier;
        set => fixedDistanceMultiplier = Mathf.Clamp(value, 0.5f, 5f);
    }

    [Header("Movement")]
    [Tooltip("Step size for movement input.")]
    [SerializeField] private float moveStep = 0.1f;

    [Header("Smoothing Settings")]

    [Tooltip("Whether to smoothly transition to position or snap immediately.")]
    [SerializeField] private bool smoothTransition = true;

    [Tooltip("Time it takes to reach the target position. Larger value = slower.")]
    [SerializeField] private float positionSmoothTime = 0.5f;

    [Tooltip("Speed of camera rotation.")]
    [SerializeField] private float rotationSpeed = 2.0f;

    private Vector3 _currentVelocity;
    private Vector3 _positionOffset = Vector3.zero;

    /// <summary>
    /// Repositions the camera to ensure the entire maze is visible.
    /// Call this when the maze regenerates.
    /// </summary>
    public void RepositionForMaze()
    {
        _positionOffset = Vector3.zero;
        UpdateCameraPosition(immediate: true);
    }

    /// <summary>
    /// Applies a movement step given 2D input (x = horizontal, y = forward/back).
    /// Used for remote input (e.g., WASD over UDP) to move the camera in the maze.
    /// </summary>
    public void ApplyMoveInput(Vector2 input)
    {
        if (input.sqrMagnitude <= 0f) return;

        var direction = new Vector3(input.x, 0f, input.y);
        _positionOffset += direction * moveStep;
    }

    /// <summary>
    /// Resets the position offset to zero.
    /// </summary>
    public void ResetPositionOffset()
    {
        _positionOffset = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (dynamicMazeGenerator == null) {
            Debug.LogWarning("[CameraController] MazeGenerator reference not set.");
            return;
        }

        UpdateCameraPosition(immediate: !smoothTransition);
    }

    /// <summary>
    /// Updates camera position - positions camera at a fixed angle above the cube.
    /// </summary>
    /// <param name="immediate">If true, snap to position immediately without smoothing.</param>
    private void UpdateCameraPosition(bool immediate = false)
    {
        Transform mazeTransform = dynamicMazeGenerator.transform;

        // Calculate the maze size based on shape
        float mazeSize = dynamicMazeGenerator.Settings.gridSize * dynamicMazeGenerator.Settings.cellSize;
        float mazeDiagonal;

        if (dynamicMazeGenerator.Settings.mazeShape == MazeGenerator.Core.MazeShape.Cube)
        {
            // Cube maze: 3D diagonal, plus wall height extending from all 6 faces
            float totalSize = mazeSize + (2f * dynamicMazeGenerator.Settings.wallHeight);
            mazeDiagonal = totalSize * Mathf.Sqrt(3f); // 3D space diagonal
        }
        else
        {
            // Flat maze: 2D diagonal only (walls extend upward, not outward)
            mazeDiagonal = mazeSize * Mathf.Sqrt(2f); // 2D diagonal
        }

        // Calculate distance based on maze size and multiplier
        float distance = mazeDiagonal * fixedDistanceMultiplier;

        // Convert angles to radians
        float verticalRad = fixedAngle * Mathf.Deg2Rad;
        float horizontalRad = fixedHorizontalAngle * Mathf.Deg2Rad;

        // Calculate camera position offset from cube center
        // Horizontal distance (on XZ plane) = distance * cos(verticalAngle)
        // Vertical height (Y) = distance * sin(verticalAngle)
        float horizontalDist = distance * Mathf.Cos(verticalRad);
        float verticalHeight = distance * Mathf.Sin(verticalRad);

        // Calculate horizontal direction based on horizontalAngle
        // 0 degrees = looking from front (-Z direction), 90 = from right (-X), etc.
        float offsetX = horizontalDist * Mathf.Sin(horizontalRad);
        float offsetZ = horizontalDist * Mathf.Cos(horizontalRad);

        Vector3 offset = new Vector3(offsetX, verticalHeight, offsetZ);
        Vector3 desiredPos = mazeTransform.position + offset + _positionOffset;

        // Apply position (with or without smoothing)
        if (immediate)
        {
            transform.position = desiredPos;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _currentVelocity, positionSmoothTime);
        }

        // Rotation: Look at the maze center plus offset
        Vector3 lookTarget = mazeTransform.position + _positionOffset;
        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            if (immediate)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
}