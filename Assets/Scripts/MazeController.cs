using UnityEngine;

/// <summary>
///     Handles rotation/orientation logic for the maze object.
///     Receives quaternion data via events and applies axis mapping, calibration, and smoothing.
/// </summary>
[DisallowMultipleComponent]
public class MazeController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Smoothing")]

    [SerializeField] private float smoothing = 25f;
    [SerializeField] private float dataTimeoutSeconds = 1.5f;
    [SerializeField] private bool autoLevelWhenTimedOut = true;
    [SerializeField] private float autoLevelSpeed = 2f;

    [Header("Axis Mapping")]

    [SerializeField] private bool invertX = true;
    [SerializeField] private bool invertY;
    [SerializeField] private bool invertZ;
    [SerializeField] private bool swapYZ = true;

    #endregion

    #region Private Fields

    private readonly object _dataLock = new();

    private Quaternion _latestQuaternion = Quaternion.identity;
    private Quaternion _calibrationOffset = Quaternion.identity;
    private float _lastUpdateTime = float.MinValue;

    #endregion

    #region Public Properties

    /// <summary>
    ///     The current displayed orientation after smoothing.
    /// </summary>
    public Quaternion CurrentOrientation { get; private set; } = Quaternion.identity;

    /// <summary>
    ///     Whether orientation data has timed out.
    /// </summary>
    public bool IsTimedOut => Time.time - _lastUpdateTime > dataTimeoutSeconds;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        ResetOrientationState();
    }

    private void OnDisable()
    {
        ResetOrientationState();
        transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        UpdateRotation();
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Sets the orientation from external quaternion data (e.g., from UdpNetworkManager).
    /// </summary>
    public void SetOrientation(Quaternion quaternion)
    {
        var finalQuaternion = ApplyAxisMapping(quaternion);

        lock (_dataLock)
        {
            _latestQuaternion = finalQuaternion;
            _lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    ///     Calibrates the sensor so the current orientation becomes the identity (flat/level).
    /// </summary>
    public void Calibrate()
    {
        lock (_dataLock)
        {
            _calibrationOffset = Quaternion.Inverse(_latestQuaternion);
        }

        Debug.Log("[OrientationController] Orientation calibrated (zeroed).");
    }

    /// <summary>
    ///     Resets all orientation state to identity.
    /// </summary>
    public void ResetOrientationState()
    {
        lock (_dataLock)
        {
            _latestQuaternion = Quaternion.identity;
            _calibrationOffset = Quaternion.identity;
            CurrentOrientation = Quaternion.identity;
            _lastUpdateTime = float.MinValue;
        }
    }

    #endregion

    #region Private Methods

    private void UpdateRotation()
    {
        Quaternion targetQuaternion;
        float dataAge;

        lock (_dataLock)
        {
            targetQuaternion = _calibrationOffset * _latestQuaternion;
            dataAge = Time.time - _lastUpdateTime;
        }

        if (dataAge <= dataTimeoutSeconds)
        {
            var lerpFactor = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            CurrentOrientation = Quaternion.Slerp(CurrentOrientation, targetQuaternion, lerpFactor);
        }
        else if (autoLevelWhenTimedOut)
        {
            var step = autoLevelSpeed * Time.deltaTime;
            CurrentOrientation = Quaternion.Slerp(CurrentOrientation, Quaternion.identity, step);
        }

        transform.localRotation = CurrentOrientation;
    }

    private Quaternion ApplyAxisMapping(Quaternion q)
    {
        var qx = q.x;
        var qy = q.y;
        var qz = q.z;
        var qw = q.w;

        if (swapYZ) (qy, qz) = (qz, qy);
        if (invertX) qx = -qx;
        if (invertY) qy = -qy;
        if (invertZ) qz = -qz;

        return new Quaternion(qx, qy, qz, qw);
    }

    #endregion
}