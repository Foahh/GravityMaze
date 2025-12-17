using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MazeGenerator.Core;
using Networking;
using TMPro;
using UnityEngine;

/// <summary>
///     Pure networking layer that manages UDP communication.
///     Receives UDP packets, parses commands, and fires events for other components to handle.
/// </summary>
[DisallowMultipleComponent]
public class NetworkManager : MonoBehaviour
{
    #region Update Loop

    private void ProcessPendingOperations()
    {
        // Process calibration
        if (_pendingCalibration)
        {
            _pendingCalibration = false;
            OnCalibrateCommand?.Invoke();
        }

        // Process VR recenter
        if (_pendingRecenter)
        {
            _pendingRecenter = false;
            OnRecenterCommand?.Invoke();
        }

        // Process maze generation
        if (_pendingMazeGeneration)
        {
            _pendingMazeGeneration = false;
            MazeGenerationSettings settings;
            lock (_pendingLock)
            {
                settings = _pendingMazeSettings;
                _pendingMazeSettings = null;
            }

            if (settings != null) OnMazeCommandReceived?.Invoke(settings);
        }

        // Process camera settings
        if (_hasPendingCameraSettings)
        {
            CameraSettings settings;
            lock (_pendingLock)
            {
                settings = _pendingCameraSettings;
                _hasPendingCameraSettings = false;
            }
            OnCameraSettingsReceived?.Invoke(settings);
        }

        // Process orientation data
        lock (_pendingLock)
        {
            while (_pendingOrientations.Count > 0)
            {
                var orientation = _pendingOrientations.Dequeue();
                OnOrientationDataReceived?.Invoke(orientation);
            }

            while (_pendingMoves.Count > 0)
            {
                var move = _pendingMoves.Dequeue();
                OnMoveInput?.Invoke(move);
            }
        }
    }

    #endregion

    #region UI Display

    private void UpdateIpDisplay()
    {
        if (ipAddressTextTMP == null) return;

        var displayText = string.Join("\n", _localIpAddresses.Select(ip => $"{ip}:{serverPort}"));
        ipAddressTextTMP.text = displayText;
    }

    #endregion

    #region Constants

    private const string CommandCalibrate = "CALIBRATE";
    private const string CommandRecenter = "RECENTER";
    private const string CommandMazePrefix = "MAZE:";
    private const string CommandMovePrefix = "MOVE:";
    private const string CommandCameraPrefix = "CAM:";

    private const int ReceiveBufferSize = 65536;
    private const int ServerRestartDelayMs = 1000;
    private const int ThreadJoinTimeoutMs = 500;

    #endregion

    #region Serialized Fields

    [Header("UDP Server Settings")]

    [SerializeField] private int serverPort = 5005;
    [SerializeField] private float socketTimeoutSeconds = 0.2f;
    [SerializeField] private float clientTimeoutSeconds = 3.0f;

    [Header("UI References")]

    [SerializeField] private TextMeshProUGUI ipAddressTextTMP;

    #endregion

    #region Private Fields

    private readonly object _clientsLock = new();
    private readonly object _pendingLock = new();

    private UdpClient _udpServer;
    private Thread _workerThread;
    private CancellationTokenSource _cancellationSource;

    private readonly Dictionary<IPEndPoint, DateTime> _activeClients = new();
    private List<string> _localIpAddresses = new() { "Unknown" };

    // Thread-safe pending data
    private volatile bool _pendingCalibration;
    private volatile bool _pendingRecenter;
    private volatile bool _pendingMazeGeneration;
    private MazeGenerationSettings _pendingMazeSettings;

    private readonly Queue<Quaternion> _pendingOrientations = new();
    private readonly Queue<Vector2> _pendingMoves = new();
    
    private volatile bool _hasPendingCameraSettings;
    private CameraSettings _pendingCameraSettings;

    #endregion

    #region Events

    /// <summary>
    ///     Fired when orientation data is received. Subscribe to update orientation.
    /// </summary>
    public event Action<Quaternion> OnOrientationDataReceived;

    /// <summary>
    ///     Fired when a calibrate command is received.
    /// </summary>
    public event Action OnCalibrateCommand;

    /// <summary>
    ///     Fired when a maze generation command is received.
    /// </summary>
    public event Action<MazeGenerationSettings> OnMazeCommandReceived;

    /// <summary>
    ///     Fired when remote movement input (e.g., WASD) is received.
    ///     The Vector2 represents (x = horizontal, y = forward/back).
    /// </summary>
    public event Action<Vector2> OnMoveInput;

    /// <summary>
    ///     Fired when camera settings are received.
    /// </summary>
    public event Action<CameraSettings> OnCameraSettingsReceived;

    /// <summary>
    ///     Fired when a VR recenter command is received.
    /// </summary>
    public event Action OnRecenterCommand;

    #endregion

    #region Public Properties

    /// <summary>
    ///     The UDP server port number.
    /// </summary>
    public int ServerPort => serverPort;

    /// <summary>
    ///     Number of currently connected clients.
    /// </summary>
    public int ConnectedClientCount
    {
        get
        {
            lock (_clientsLock)
            {
                return _activeClients.Count;
            }
        }
    }

    /// <summary>
    ///     List of local IP addresses for this machine.
    /// </summary>
    public IReadOnlyList<string> LocalIpAddresses => _localIpAddresses;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        RefreshLocalIpAddresses();
        StartServer();
    }

    private void OnDisable()
    {
        StopServer();
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    private void Update()
    {
        ProcessPendingOperations();
        UpdateIpDisplay();
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Starts the UDP server.
    /// </summary>
    public void StartServer()
    {
        if (_workerThread is { IsAlive: true }) return;

        _cancellationSource = new CancellationTokenSource();
        _workerThread = new Thread(ServerLoop)
        {
            IsBackground = true,
            Name = "UdpNetworkManager.ServerLoop"
        };
        _workerThread.Start();
    }

    /// <summary>
    ///     Stops the UDP server.
    /// </summary>
    public void StopServer()
    {
        _cancellationSource?.Cancel();
        CloseUdpSocket();

        if (_workerThread is { IsAlive: true })
            _workerThread.Join(ThreadJoinTimeoutMs);

        _workerThread = null;
        _cancellationSource?.Dispose();
        _cancellationSource = null;

        lock (_clientsLock)
        {
            _activeClients.Clear();
        }
    }

    /// <summary>
    ///     Refreshes the list of local IP addresses.
    /// </summary>
    public void RefreshLocalIpAddresses()
    {
        try
        {
            var ips = GetLocalIpAddresses();
            _localIpAddresses = ips.Count > 0 ? ips : new List<string> { "Not Available" };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UdpNetworkManager] Failed to get local IP: {ex.Message}");
            _localIpAddresses = new List<string> { "Error" };
        }
    }

    #endregion

    #region Private Methods

    private static List<string> GetLocalIpAddresses()
    {
        var results = new HashSet<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsUsableInterface(ni)) continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                    if (IsValidIpv4Address(addr.Address))
                        results.Add(addr.Address.ToString());
            }
        }
        catch
        {
            // Silently continue to next method
        }

        return results.ToList();
    }

    private static bool IsUsableInterface(NetworkInterface ni)
    {
        return (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
               ni.OperationalStatus == OperationalStatus.Up;
    }

    private static bool IsValidIpv4Address(IPAddress address)
    {
        return address.AddressFamily == AddressFamily.InterNetwork &&
               !IPAddress.IsLoopback(address);
    }

    #endregion

    #region UDP Server

    private void ServerLoop()
    {
        var timeoutMs = Mathf.Max(1, Mathf.RoundToInt(socketTimeoutSeconds * 1000f));
        var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

        while (!_cancellationSource.IsCancellationRequested)
        {
            if (!EnsureServerStarted(timeoutMs))
            {
                Thread.Sleep(ServerRestartDelayMs);
                continue;
            }

            try
            {
                var receivedData = _udpServer.Receive(ref remoteEndpoint);
                ProcessReceivedData(receivedData, remoteEndpoint);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                // Expected timeout, continue polling
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[UdpNetworkManager] Socket error: {ex.SocketErrorCode}");
                CloseUdpSocket();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UdpNetworkManager] Server exception: {ex.Message}");
            }

            CleanupStaleClients();
        }
    }

    private void ProcessReceivedData(byte[] data, IPEndPoint sender)
    {
        var text = Encoding.UTF8.GetString(data).Trim();

        if (TryHandleCommand(text))
            return;

        TrackClient(sender);
        TryQueueOrientation(text);
    }

    private bool TryHandleCommand(string text)
    {
        if (text.Equals(CommandCalibrate, StringComparison.OrdinalIgnoreCase))
        {
            _pendingCalibration = true;
            return true;
        }

        if (text.Equals(CommandRecenter, StringComparison.OrdinalIgnoreCase))
        {
            _pendingRecenter = true;
            return true;
        }

        if (text.StartsWith(CommandMazePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var mazeData = text.Substring(CommandMazePrefix.Length);
            if (MazeSettingsParser.TryParse(mazeData, out var mazeSettings))
                lock (_pendingLock)
                {
                    _pendingMazeSettings = mazeSettings;
                    _pendingMazeGeneration = true;
                }

            return true;
        }

        if (text.StartsWith(CommandMovePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var moveData = text.Substring(CommandMovePrefix.Length);
            if (TryParseMove(moveData, out var move))
                lock (_pendingLock)
                {
                    _pendingMoves.Enqueue(move);
                }

            return true;
        }

        if (text.StartsWith(CommandCameraPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var cameraData = text.Substring(CommandCameraPrefix.Length);
            if (CameraSettingsParser.TryParse(cameraData, out var cameraSettings))
                lock (_pendingLock)
                {
                    _pendingCameraSettings = cameraSettings;
                    _hasPendingCameraSettings = true;
                }

            return true;
        }

        return false;
    }

    private static bool TryParseMove(string text, out Vector2 move)
    {
        move = Vector2.zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var upper = text.Trim().ToUpperInvariant();

        foreach (var c in upper)
            switch (c)
            {
                case 'W':
                    move += Vector2.up;
                    break;
                case 'S':
                    move += Vector2.down;
                    break;
                case 'A':
                    move += Vector2.left;
                    break;
                case 'D':
                    move += Vector2.right;
                    break;
            }

        return move != Vector2.zero;
    }

    private void TrackClient(IPEndPoint sender)
    {
        lock (_clientsLock)
        {
            _activeClients[sender] = DateTime.UtcNow;
        }
    }

    private void TryQueueOrientation(string text)
    {
        if (!QuaternionParser.TryParse(text, out var parsedQuaternion))
            return;

        lock (_pendingLock)
        {
            // Only keep the latest orientation to avoid lag
            _pendingOrientations.Clear();
            _pendingOrientations.Enqueue(parsedQuaternion);
        }
    }

    private bool EnsureServerStarted(int timeoutMs)
    {
        if (_udpServer != null) return true;

        try
        {
            _udpServer = new UdpClient(serverPort)
            {
                Client =
                {
                    Blocking = true,
                    ReceiveTimeout = timeoutMs,
                    ReceiveBufferSize = ReceiveBufferSize
                }
            };

            Debug.Log($"[UdpNetworkManager] Server started on port {serverPort}");
            return true;
        }
        catch (SocketException ex)
        {
            Debug.LogError($"[UdpNetworkManager] Failed to start server on port {serverPort}: {ex.Message}");
            CloseUdpSocket();
            return false;
        }
    }

    private void CloseUdpSocket()
    {
        try
        {
            _udpServer?.Close();
        }
        catch
        {
            // Ignore errors during shutdown
        }

        _udpServer = null;
    }

    private void CleanupStaleClients()
    {
        var now = DateTime.UtcNow;

        lock (_clientsLock)
        {
            var staleClients = _activeClients
                .Where(kvp => (now - kvp.Value).TotalSeconds > clientTimeoutSeconds)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var client in staleClients)
                _activeClients.Remove(client);
        }
    }

    #endregion
}