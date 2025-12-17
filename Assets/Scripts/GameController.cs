using System;
using EasyTextEffects;
using MazeGenerator.Core;
using Networking;
using TMPro;
using UnityEngine;

/// <summary>
///     Central game state coordinator.
///     Manages game state, timer, goal detection, and coordinates between systems via events.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    #region Enums

    public enum GameState
    {
        Idle,
        Playing,
        Finished,
        Paused
    }

    #endregion

    #region Serialized Fields

    [Header("Component References")] [SerializeField]
    private MazeGenerator.Scripts.DynamicMazeGenerator dynamicMazeGenerator;

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private MazeController mazeController;
    
    [Tooltip("Reference to the camera controller for positioning and movement.")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Reference to the Cardboard controller for VR functions.")]
    [SerializeField] private CardboardController cardboardController;

    [Header("Goal Components")]
    [Tooltip("The goal controller that handles goal collision detection.")] [SerializeField]
    private GoalController goalController;

    [Tooltip("Child object containing goal visual renderers.")] [SerializeField]
    private GameObject goalRenderObject;

    [Tooltip("Particle system for the goal explosion effect.")] [SerializeField]
    private ParticleSystem goalBoomEffect;

    [Header("Timer UI")] [Tooltip("TextMeshPro component to display the timer. Format: MM:SS.mm")] [SerializeField]
    private TextMeshProUGUI timerText;

    [Header("Finish")]

    [Tooltip("Root object containing finish UI elements.")] [SerializeField]
    private GameObject finishUI;

    [Tooltip("Sound clip on finish.")] [SerializeField]
    private AudioClip goalReachedSound;

    #endregion

    #region Private Fields

    private TextEffect _timerTextEffect;
    private bool _timerRunning;
    private AudioSource _audioSource;
    private Renderer[] _cachedGoalRenderers;
    private bool _goalTriggered;

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets the current game state.
    /// </summary>
    public GameState CurrentState { get; private set; } = GameState.Idle;

    /// <summary>
    ///     Gets the current elapsed time.
    /// </summary>
    public float ElapsedTime { get; private set; }

    /// <summary>
    ///     Gets whether the goal has been triggered.
    /// </summary>
    public bool IsGoalTriggered => _goalTriggered;

    #endregion

    #region Events

    /// <summary>
    ///     Fired when the game state changes.
    /// </summary>
    public event Action<GameState> OnGameStateChanged;

    /// <summary>
    ///     Fired when a new game starts.
    /// </summary>
    public event Action OnGameStarted;

    /// <summary>
    ///     Fired when the game is completed (goal reached).
    /// </summary>
    public event Action<float> OnGameCompleted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        CacheGoalRenderers();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        StartNewGame();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void Update()
    {
        if (_timerRunning)
        {
            ElapsedTime += Time.deltaTime;
            UpdateTimerDisplay();
        }
    }


    #endregion

    #region Public Methods

    /// <summary>
    ///     Starts a new game session.
    /// </summary>
    public void StartNewGame()
    {
        ResetTimer();
        SetFinishUIVisible(false);
        ResetGoal();

        SetState(GameState.Playing);
        _timerRunning = true;

        OnGameStarted?.Invoke();

        Debug.Log("[GameController] New game started.");
    }

    /// <summary>
    ///     Resets the game (regenerates maze and starts fresh).
    /// </summary>
    public void ResetGame()
    {
        StartNewGame();
    }

    /// <summary>
    ///     Pauses the current game.
    /// </summary>
    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;

        _timerRunning = false;
        SetState(GameState.Paused);

        Debug.Log("[GameController] Game paused.");
    }

    /// <summary>
    ///     Resumes a paused game.
    /// </summary>
    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;

        _timerRunning = true;
        SetState(GameState.Playing);

        Debug.Log("[GameController] Game resumed.");
    }

    #endregion

    #region Event Handlers

    private void HandleOrientationData(Quaternion quaternion)
    {
        if (mazeController != null)
            mazeController.SetOrientation(quaternion);
    }

    private void HandleMoveInput(Vector2 input)
    {
        if (playerController != null)
            playerController.ApplyMoveInput(input);
    }

    private void HandleCalibrateCommand()
    {
        if (mazeController != null)
            mazeController.Calibrate();
    }

    private void HandleRecenterCommand()
    {
        if (cardboardController != null)
            cardboardController.Recenter();
        
        Debug.Log("[GameController] VR recenter command executed.");
    }

    private void HandleMazeCommand(MazeGenerationSettings settings)
    {
        if (dynamicMazeGenerator == null)
        {
            Debug.LogWarning("[GameController] MazeGenerator reference not set.");
            return;
        }

        PreserveMaterialSettings(settings);

        dynamicMazeGenerator.Settings = settings;
        dynamicMazeGenerator.GenerateMaze();

        // Reset the game after maze regeneration
        ResetGame();

        Debug.Log($"[GameController] Generated maze: Shape={settings.mazeShape}, Grid={settings.gridSize}");
    }

    private void HandleDynamicMazeGenerated(GameObject mazeRoot)
    {
        if (playerController != null)
        {
            playerController.RepositionForMaze();
        }
        
        Debug.Log("[GameController] Maze generation completed.");
    }

    private void HandleCameraSettings(CameraSettings settings)
    {
        if (playerController == null)
        {
            Debug.LogWarning("[GameController] CameraController reference not set.");
            return;
        }

        playerController.FixedAngle = settings.Angle;
        playerController.FixedDistanceMultiplier = settings.DistanceMultiplier;
        playerController.RepositionForMaze();

        Debug.Log($"[GameController] Camera settings updated: Angle={settings.Angle}°, Distance={settings.DistanceMultiplier}x");
    }

    private void HandleGoalTriggered()
    {
        TriggerGoal();
    }

    #endregion

    #region Private Methods

    private void TriggerGoal()
    {
        if (_goalTriggered) return;
        if (CurrentState != GameState.Playing) return;

        _goalTriggered = true;
        _timerRunning = false;

        PlayGoalBoomEffect();
        ShowGoalCompleteState();

        if (_timerTextEffect != null)
            _timerTextEffect.StartManualEffects();

        SetFinishUIVisible(true);
        PlayGoalReachedSound();
        SetState(GameState.Finished);

        OnGameCompleted?.Invoke(ElapsedTime);

        Debug.Log($"[GameController] Goal reached! Time: {FormatTime(ElapsedTime)}");
    }

    private void ResetGoal()
    {
        _goalTriggered = false;

        if (goalController != null)
            goalController.ResetGoal();

        SetGoalVisualsVisible(true);
        StopGoalBoomEffect();
    }

    private void PlayGoalBoomEffect()
    {
        if (goalBoomEffect == null) return;
        goalBoomEffect.Play();
    }

    private void StopGoalBoomEffect()
    {
        if (goalBoomEffect == null) return;
        goalBoomEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ShowGoalCompleteState()
    {
        if (goalController != null)
            goalController.SetGoalColliderEnabled(false);
        SetGoalVisualsVisible(false);
    }

    private void SetGoalVisualsVisible(bool visible)
    {
        EnsureGoalRenderersAreCached();

        foreach (var r in _cachedGoalRenderers)
            if (r != null)
                r.enabled = visible;
    }

    private void EnsureGoalRenderersAreCached()
    {
        if (_cachedGoalRenderers == null || _cachedGoalRenderers.Length == 0)
            CacheGoalRenderers();
    }

    private void CacheGoalRenderers()
    {
        _cachedGoalRenderers = goalRenderObject != null
            ? goalRenderObject.GetComponentsInChildren<Renderer>(true)
            : Array.Empty<Renderer>();
    }

    private void PlayGoalReachedSound()
    {
        if (goalReachedSound == null || _audioSource == null) return;
        _audioSource.PlayOneShot(goalReachedSound);
    }

    private void CacheComponents()
    {
        if (timerText != null)
            _timerTextEffect = timerText.GetComponent<TextEffect>();

        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        if (playerController == null) {
            Debug.LogWarning("[GameController] CameraController reference not set.");
        }
    }

    private void SubscribeToEvents()
    {
        if (networkManager != null)
        {
            networkManager.OnOrientationDataReceived += HandleOrientationData;
            networkManager.OnCalibrateCommand += HandleCalibrateCommand;
            networkManager.OnRecenterCommand += HandleRecenterCommand;
            networkManager.OnMazeCommandReceived += HandleMazeCommand;
            networkManager.OnMoveInput += HandleMoveInput;
            networkManager.OnCameraSettingsReceived += HandleCameraSettings;
        }

        if (dynamicMazeGenerator != null)
            dynamicMazeGenerator.OnMazeGenerated += HandleDynamicMazeGenerated;

        if (goalController != null)
            goalController.OnGoalTriggered += HandleGoalTriggered;
    }

    private void UnsubscribeFromEvents()
    {
        if (networkManager != null)
        {
            networkManager.OnOrientationDataReceived -= HandleOrientationData;
            networkManager.OnCalibrateCommand -= HandleCalibrateCommand;
            networkManager.OnRecenterCommand -= HandleRecenterCommand;
            networkManager.OnMazeCommandReceived -= HandleMazeCommand;
            networkManager.OnMoveInput -= HandleMoveInput;
            networkManager.OnCameraSettingsReceived -= HandleCameraSettings;
        }

        if (dynamicMazeGenerator != null)
            dynamicMazeGenerator.OnMazeGenerated -= HandleDynamicMazeGenerated;

        if (goalController != null)
            goalController.OnGoalTriggered -= HandleGoalTriggered;
    }

    private void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }

    private void ResetTimer()
    {
        ElapsedTime = 0f;
        _timerRunning = false;

        if (_timerTextEffect != null)
            _timerTextEffect.StopManualEffects();

        UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
            timerText.text = FormatTime(ElapsedTime);
    }

    private void SetFinishUIVisible(bool visible)
    {
        if (finishUI != null)
            finishUI.SetActive(visible);
    }

    private void PreserveMaterialSettings(MazeGenerationSettings targetSettings)
    {
        if (dynamicMazeGenerator == null) return;

        var currentSettings = dynamicMazeGenerator.Settings;
        targetSettings.wallMaterial = currentSettings.wallMaterial;
        targetSettings.groundMaterial = currentSettings.groundMaterial;
        targetSettings.lidMaterial = currentSettings.lidMaterial;
    }

    private static string FormatTime(float timeInSeconds)
    {
        var minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        var seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        var milliseconds = Mathf.FloorToInt(timeInSeconds * 100f % 100f);

        return $"{minutes:00}:{seconds:00}.{milliseconds:00}";
    }

    #endregion
}