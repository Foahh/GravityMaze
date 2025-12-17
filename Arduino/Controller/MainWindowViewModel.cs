using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Controller;

public enum MazeShape
{
    Disc,
    Cube
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

public record MazePreset(MazeShape Shape, Difficulty Difficulty)
{
    public override string ToString() => $"{Shape} {Difficulty}";
}

public partial class MainWindowViewModel : ObservableObject, IAsyncDisposable, IDisposable
{
    private const string CmdCalibrate = "CALIBRATE";
    private const string CmdRecenter = "RECENTER";
    private const string CmdMazePrefix = "MAZE:";
    private const string CmdMovePrefix = "MOVE:";
    private const string CmdCameraPrefix = "CAM:";
    private const int BaudRate = 115200;
    private const int SerialTimeout = 1000;
    private const int ArduinoResetDelay = 2000;
    private const int ReconnectDelay = 3000;
    private const int ErrorRecoveryDelay = 1000;
    private readonly Lock _serialLock = new();

    private readonly UdpClient _udpClient = new();
    private bool _disposed;

    private string? _lastPort;
    private CancellationTokenSource? _serialCts;
    private SerialPort? _serialPort;
    private Task? _serialTask;

    [ObservableProperty] private string _statusText = "Ready";

    #region Connection Properties

    [ObservableProperty] private string _targetHost = "127.0.0.1";

    [ObservableProperty] private int _targetPort = 5005;

    [ObservableProperty] private string _arduinoStatus = "Disconnected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArduinoButtonText))]
    private bool _isArduinoConnected;

    public string ArduinoButtonText => IsArduinoConnected ? "Disconnect" : "Connect";

    #endregion

    #region Maze Settings

    [ObservableProperty] private MazeShape _mazeShape = MazeShape.Cube;

    public List<MazeShape> MazeShapes { get; } = [MazeShape.Disc, MazeShape.Cube];

    public List<MazePreset> DiscPresets { get; } =
    [
        new(MazeShape.Disc, Difficulty.Easy),
        new(MazeShape.Disc, Difficulty.Medium),
        new(MazeShape.Disc, Difficulty.Hard)
    ];

    public List<MazePreset> CubePresets { get; } =
    [
        new(MazeShape.Cube, Difficulty.Easy),
        new(MazeShape.Cube, Difficulty.Medium),
        new(MazeShape.Cube, Difficulty.Hard)
    ];

    [RelayCommand]
    private void ApplyPreset(MazePreset? preset)
    {
        if (preset is null) return;

        MazeShape = preset.Shape;
        ApplyDifficultySettings(preset.Shape, preset.Difficulty);

        SendMazeSettings();
        StatusText = $"Applied preset: {preset}";
    }

    private void ApplyDifficultySettings(MazeShape shape, Difficulty difficulty)
    {
        var (gridSize, goalDistance, deadEndRemoval) = (shape, difficulty) switch
        {
            // Disc presets
            (MazeShape.Disc, Difficulty.Easy) => (6, 100, 0),
            (MazeShape.Disc, Difficulty.Medium) => (8, 100, 0),
            (MazeShape.Disc, Difficulty.Hard) => (10, 100, 0),

            // Cube presets
            (MazeShape.Cube, Difficulty.Easy) => (3, 100, 15),
            (MazeShape.Cube, Difficulty.Medium) => (4, 100, 10),
            (MazeShape.Cube, Difficulty.Hard) => (5, 100, 5),

            _ => (6, 50, 15)
        };

        GridSize = gridSize;
        GoalDistance = goalDistance;
        DeadEndRemoval = deadEndRemoval;
    }

    [ObservableProperty] private int _gridSize = 6;

    [ObservableProperty] private double _cellSize = 1.0;

    [ObservableProperty] private double _wallHeight = 1.0;

    [ObservableProperty] private double _wallThickness = 0.15;

    [ObservableProperty] private double _lidThickness = 0.1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSeedEnabled))]
    private bool _useRandomSeed = true;

    public bool IsSeedEnabled => !UseRandomSeed;

    [ObservableProperty] private int _seed = 12345;

    [ObservableProperty] private int _goalDistance = 50;

    [ObservableProperty] private int _deadEndRemoval = 0;

    #endregion

    #region Camera Settings

    [ObservableProperty] private double _cameraAngle = 45.0;

    [ObservableProperty] private double _cameraDistanceMultiplier = 1.0;

    #endregion

    #region Commands

    [RelayCommand]
    private void UpdateTarget()
    {
        StatusText = $"Target updated: {TargetHost}:{TargetPort}";
    }

    [RelayCommand]
    private void ToggleArduino()
    {
        if (IsArduinoConnected)
            StopArduino();
        else
            StartArduino();
    }

    [RelayCommand]
    private void SendMazeSettings()
    {
        var message = BuildMazeSettingsMessage();
        SendUdpMessage(message);
        StatusText = $"Sent maze settings (grid: {GridSize})";
        SendCameraSettings();
    }

    [RelayCommand]
    private void SendCalibrate()
    {
        SendUdpMessage(CmdCalibrate);
        StatusText = "Sent calibration command";
        SendCameraSettings();
    }

    [RelayCommand]
    private void SendRecenter()
    {
        SendUdpMessage(CmdRecenter);
        StatusText = "Sent VR recenter command";
    }

    [RelayCommand]
    private void SendCameraSettings()
    {
        var message = BuildCameraSettingsMessage();
        SendUdpMessage(message);
        StatusText = $"Sent camera settings (angle: {CameraAngle}°, distance: {CameraDistanceMultiplier}x)";
    }

    public void SendMoveKeys(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys)) return;
        SendUdpMessage($"{CmdMovePrefix}{keys}");
        StatusText = $"Sent move: {keys}";
    }

    #endregion

    #region UDP Communication

    private void SendUdpMessage(string message)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(message);
            var endpoint = new IPEndPoint(IPAddress.Parse(TargetHost), TargetPort);
            _udpClient.Send(data, data.Length, endpoint);
        }
        catch (Exception ex)
        {
            StatusText = $"Send error: {ex.Message}";
        }
    }

    private string BuildMazeSettingsMessage()
    {
        var sb = new StringBuilder(CmdMazePrefix);
        var culture = CultureInfo.InvariantCulture;

        sb.Append($"shape={MazeShape}");
        sb.Append($",gridSize={GridSize}");
        sb.Append($",cellSize={CellSize.ToString(culture)}");
        sb.Append($",wallHeight={WallHeight.ToString(culture)}");
        sb.Append($",wallThickness={WallThickness.ToString(culture)}");
        sb.Append($",lidThickness={LidThickness.ToString(culture)}");
        sb.Append($",useRandomSeed={UseRandomSeed.ToString().ToLowerInvariant()}");
        sb.Append($",seed={Seed}");
        sb.Append($",goalDistance={GoalDistance.ToString(culture)}");
        sb.Append($",deadEndRemoval={DeadEndRemoval.ToString(culture)}");

        return sb.ToString();
    }

    private string BuildCameraSettingsMessage()
    {
        var culture = CultureInfo.InvariantCulture;
        return
            $"{CmdCameraPrefix}angle={CameraAngle.ToString(culture)},distance={CameraDistanceMultiplier.ToString(culture)}";
    }

    #endregion

    #region Change Handlers

    partial void OnCameraAngleChanged(double value)
    {
        SendCameraSettings();
    }

    partial void OnCameraDistanceMultiplierChanged(double value)
    {
        SendCameraSettings();
    }

    #endregion

    #region Arduino Serial Communication

    private void StartArduino()
    {
        _serialCts = new CancellationTokenSource();
        IsArduinoConnected = true;
        ArduinoStatus = "Connecting...";
        _serialTask = Task.Run(() => ArduinoLoopAsync(_serialCts.Token));
    }

    private void StopArduino()
    {
        _serialCts?.Cancel();
        CloseSerialPort();
        IsArduinoConnected = false;
        ArduinoStatus = "Disconnected";
    }

    private void CloseSerialPort()
    {
        lock (_serialLock)
        {
            if (_serialPort == null) return;

            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
            }
            catch
            {
                // Ignore close errors during cleanup
            }
            finally
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
    }

    private async Task ArduinoLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
            try
            {
                if (!await EnsureConnectedAsync(ct))
                    continue;

                await ReadAndForwardAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await HandleSerialErrorAsync(ex, ct);
            }
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        lock (_serialLock)
        {
            if (_serialPort is { IsOpen: true })
                return true;
        }

        var port = FindArduinoPort() ?? _lastPort;
        if (port == null)
        {
            await UpdateArduinoStatusAsync("Not found");
            await Task.Delay(ReconnectDelay, ct);
            return false;
        }

        await UpdateArduinoStatusAsync($"Connecting to {port}...");

        try
        {
            var newPort = new SerialPort(port, BaudRate)
            {
                ReadTimeout = SerialTimeout,
                WriteTimeout = SerialTimeout,
                DtrEnable = true,
                RtsEnable = true
            };

            newPort.Open();
            await Task.Delay(ArduinoResetDelay, ct);

            lock (_serialLock)
            {
                _serialPort = newPort;
            }

            _lastPort = port;
            await UpdateArduinoStatusAsync($"Connected ({port})");
            SendUdpMessage(CmdCalibrate);
            return true;
        }
        catch (Exception ex)
        {
            // Shorten error message for UI
            var msg = ex.Message.Length > 20 ? ex.Message.Substring(0, 20) + "..." : ex.Message;
            await UpdateArduinoStatusAsync($"Err({port}): {msg}");
            await Task.Delay(ReconnectDelay, ct);
            return false;
        }
    }

    private async Task ReadAndForwardAsync(CancellationToken ct)
    {
        string? line;

        lock (_serialLock)
        {
            if (_serialPort is not { IsOpen: true })
                return;

            try
            {
                line = _serialPort.ReadLine();
            }
            catch (TimeoutException)
            {
                // Normal - no data available
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(line))
        {
            SendUdpMessage(line.Trim());
            Console.WriteLine(line);
        }

        await Task.Yield();
    }

    private async Task HandleSerialErrorAsync(Exception ex, CancellationToken ct)
    {
        CloseSerialPort();
        await UpdateArduinoStatusAsync("Disconnected");
        await Task.Delay(ErrorRecoveryDelay, ct);
    }

    private async Task UpdateArduinoStatusAsync(string status)
    {
        await Dispatcher.UIThread.InvokeAsync(() => ArduinoStatus = status);
    }

    private static string? FindArduinoPort()
    {
        var ports = SerialPort.GetPortNames();
        // Sort and reverse to prefer higher COM numbers (usually USB devices)
        Array.Sort(ports);
        Array.Reverse(ports);

        foreach (var port in ports)
            if (port.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                port.Contains("ttyUSB", StringComparison.Ordinal) ||
                port.Contains("ttyACM", StringComparison.Ordinal))
                return port;

        return ports.Length > 0 ? ports[0] : null;
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopArduino();
            _serialCts?.Dispose();
            _udpClient.Dispose();
        }

        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        _serialCts?.Cancel();

        if (_serialTask != null)
            try
            {
                await _serialTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

        CloseSerialPort();
        _serialCts?.Dispose();
        _udpClient.Dispose();
    }

    #endregion
}