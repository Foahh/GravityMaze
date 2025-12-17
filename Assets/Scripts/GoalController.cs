using UnityEngine;

/// <summary>
///     Handles goal collision detection and triggers events when the goal is reached.
/// </summary>
[DisallowMultipleComponent]
public class GoalController : MonoBehaviour
{
    #region Private Fields

    private Collider goalCollider;
    private bool _goalTriggered;

    #endregion

    #region Events

    /// <summary>
    ///     Fired when the goal is triggered by the player ball.
    /// </summary>
    public event System.Action OnGoalTriggered;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (goalCollider == null)
            goalCollider = GetComponent<Collider>();
    }

    private void Reset()
    {
        if (goalCollider != null)
            goalCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_goalTriggered) return;
        if (!other.CompareTag("PlayerBall")) return;

        _goalTriggered = true;
        OnGoalTriggered?.Invoke();

        Debug.Log("[GoalController] Goal triggered by PlayerBall.");
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Resets the goal state, allowing it to be triggered again.
    /// </summary>
    public void ResetGoal()
    {
        _goalTriggered = false;
        SetGoalColliderEnabled(true);
    }

    /// <summary>
    ///     Enables or disables the goal collider.
    /// </summary>
    public void SetGoalColliderEnabled(bool isEnabled)
    {
        if (goalCollider != null)
            goalCollider.enabled = isEnabled;
    }

    #endregion
}

