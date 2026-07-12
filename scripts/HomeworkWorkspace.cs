#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public partial class HomeworkWorkspace : Node2D
{
    // Global Tycoon Metrics
    private float _commuterRage = 0.0f;       // 0.0 to 100.0
    private float _dailyBudget = 5000.0f;     // Starts at 5000.0
    private float _platformDensity = 2.0f;    // Crowd Congestion index

    // Upgrades and toggles
    private bool _turnstileGated = false;
    private int _securityGuards = 0;
    private int _ventilationLevel = 0;
    private int _passengersServed = 0;

    // Simulation status flags
    private bool _riotErupted = false;
    private bool _dayCompleted = false;
    private bool _isRunning = true;

    // Shift clocks
    private float _timeRemaining = 120.0f;    // 2-minute day shift
    private float _trainTimer = 10.0f;        // Automatic train arrivals
    private float _trainX = -600.0f;          // Train horizontal offset
    private bool _trainArrived = false;

    // Baseline coordinates for shaking effects
    private Vector2 _originalPosition;
    private Vector2 _screenOffset = Vector2.Zero;
    private int _frameCount = 0;

    // Active notification banner details
    private string _notificationText = "SHIFT COMMENCING! MONITOR METRIC STATIONS UNDER OVERLOAD.";
    private Color _notificationBg = Colors.DarkSlateGray;
    private int _notificationTicksRemaining = 60; // 2 seconds

    // Thread Timers & Fonts
    private System.Threading.Timer? _gameTimer;
    private SystemFont? _font;

    // Commuter visual coordinates cache (anchors passengers between draws)
    private readonly List<Vector2> _commuterCoordinates = new List<Vector2>();
    private readonly Random _random = new Random(256);

    public override void _Ready()
    {
        // Save initial pos for shake snapbacks
        _originalPosition = Position;

        // Load default SystemFont for canvas rendering
        SystemFont fontObj = new SystemFont();
        fontObj.FontNames = new string[] { "sans-serif", "Segoe UI", "Arial" };
        _font = fontObj;

        // Generate stationary commuter coordinate positions
        for (int i = 0; i < 200; i++)
        {
            float rx = (float)(_random.NextDouble() * 0.82 + 0.06); // scale across platform width
            float ry = (float)(_random.NextDouble() * 0.22 + 0.24); // scale across platform depth (horizon matches 0.24 to 0.46)
            _commuterCoordinates.Add(new Vector2(rx, ry));
        }

        InitializeGame();
    }

    private void InitializeGame()
    {
        _commuterRage = 0.0f;
        _dailyBudget = 5000.0f;
        _platformDensity = 2.0f;
        _turnstileGated = false;
        _securityGuards = 0;
        _ventilationLevel = 0;
        _passengersServed = 0;
        _riotErupted = false;
        _dayCompleted = false;

        _timeRemaining = 120.0f;
        _trainTimer = 10.0f;
        _trainX = -650.0f;
        _trainArrived = false;
        _screenOffset = Vector2.Zero;
        Position = _originalPosition;

        _notificationText = "HUD CONTROL ONLINE - MONITOR CROWD INTENSITIES";
        _notificationBg = Colors.DarkCyan;
        _notificationTicksRemaining = 60;

        // Core Game Loop: running on a standard thread timer at 30 FPS
        _gameTimer?.Dispose();
        _gameTimer = new System.Threading.Timer(OnTimerTick, null, 0, 33);
    }

    private void OnTimerTick(object? state)
    {
        if (!_isRunning) return;

        // Synchronize thread ticker back to Godot's safe execution thread
        CallDeferred(nameof(GameUpdateTick));
    }

    private void GameUpdateTick()
    {
        if (_riotErupted || _dayCompleted)
        {
            if (_riotErupted)
            {
                // Violent shake coordinates for Riot Eruptions
                Random rng = new Random();
                _screenOffset = new Vector2(rng.Next(-5, 6), rng.Next(-3, 4));
                Position = _originalPosition + _screenOffset;
            }
            QueueRedraw();
            return;
        }

        _frameCount++;
        float deltaTime = 1.0f / 30.0f;

        // Shift clock depletion
        _timeRemaining -= deltaTime;
        if (_timeRemaining <= 0)
        {
            _timeRemaining = 0;
            _dayCompleted = true;
        }

        // Passive commuter influx
        float arrivalRate = _turnstileGated ? 0.35f : 0.85f;
        _platformDensity += arrivalRate * deltaTime;
        if (_platformDensity > 10.0f) _platformDensity = 10.0f;

        // Passive Rage calculation (PlatformDensity > 4.5 comfort threshold)
        if (_platformDensity > 4.5f)
        {
            float excess = _platformDensity - 4.5f;
            float ventCooling = 1.0f / (1.0f + _ventilationLevel * 0.4f);
            
            _commuterRage += excess * 1.6f * ventCooling * deltaTime;
        }
        else
        {
            // Passive cooldown when density is low
            _commuterRage = Math.Max(0.0f, _commuterRage - 1.5f * deltaTime);
        }

        // Turnstiles gated increases rage due to road frustration outside
        if (_turnstileGated)
        {
            _commuterRage += 0.5f * deltaTime;
        }

        // Security guards active mitigation factor
        if (_securityGuards > 0)
        {
            _commuterRage = Math.Max(0.0f, _commuterRage - (0.4f * _securityGuards * deltaTime));
        }

        // Clamp Rage
        if (_commuterRage > 100.0f) _commuterRage = 100.0f;

        // Trigger Game Over loss threshold
        if (_commuterRage >= 100.0f)
        {
            _riotErupted = true;
        }

        // Daily Operating cost deductions
        float operatingCost = 15.0f + (_securityGuards * 4.0f) + (_ventilationLevel * 5.0f);
        _dailyBudget = Math.Max(0.0f, _dailyBudget - operatingCost * deltaTime);

        // Train Arrival Simulation
        _trainTimer -= deltaTime;
        if (_trainTimer <= 0)
        {
            _trainTimer = 12.0f;
            _trainArrived = true;
            _trainX = -650.0f; // Slide from left

            // Compute passenger clearing
            float boarders = Math.Min(_platformDensity, 4.0f);
            _platformDensity = Math.Max(0.0f, _platformDensity - boarders);

            // Ticking revenues
            int boardsCount = (int)(boarders * 18);
            _passengersServed += boardsCount;
            _dailyBudget += boardsCount * 12.50f;
        }

        // Train sliding coordinates updates
        if (_trainArrived)
        {
            if (_trainX < 0)
            {
                _trainX += 800.0f * deltaTime;
                if (_trainX > 0) _trainX = 0;
            }
            else
            {
                if (_trainTimer < 8.0f)
                {
                    _trainX += 900.0f * deltaTime;
                    if (_trainX > 850.0f)
                    {
                        _trainArrived = false;
                    }
                }
            }
        }

        // Visual overcrowding screen shakes (> 7.0 platform density)
        if (_platformDensity > 7.0f)
        {
            Random rng = new Random();
            float force = (_platformDensity - 7.0f) * 1.5f;
            _screenOffset = new Vector2(rng.Next((int)-force, (int)force + 1), rng.Next((int)-force, (int)force + 1));
            Position = _originalPosition + _screenOffset;
        }
        else
        {
            _screenOffset = Vector2.Zero;
            Position = _originalPosition;
        }

        // Clear active notification durations
        if (_notificationTicksRemaining > 0)
        {
            _notificationTicksRemaining--;
            if (_notificationTicksRemaining <= 0)
            {
                _notificationText = "MONITOR EDSA CHOKE POINTS AND DEPLOY TRAIN SERVICE.";
                _notificationBg = Colors.DarkSlateGray;
            }
        }

        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (_riotErupted || _dayCompleted)
            {
                if (keyEvent.Keycode == Key.R)
                {
                    InitializeGame();
                }
                return;
            }

            switch (keyEvent.Keycode)
            {
                case Key.Key1:
                case Key.Kp1:
                    // Deploy Extra Train
                    if (_dailyBudget >= 400.0f)
                    {
                        _dailyBudget -= 400.0f;
                        _trainTimer = 0.5f; // Force train arrival
                        FlashNotification("EXTRA SERVICE TRAIN DIPATCHED! (-$400)", Colors.DarkGreen);
                    }
                    else
                    {
                        FlashNotification("ERR: INSUFFICIENT BUDGET TO ACCELERATE TRAIN!", Colors.DarkRed);
                    }
                    break;

                case Key.Key2:
                case Key.Kp2:
                    // Hire Custom Guards
                    if (_dailyBudget >= 180.0f)
                    {
                        _dailyBudget -= 180.0f;
                        _securityGuards++;
                        _commuterRage = Math.Max(0.0f, _commuterRage - 15.0f);
                        FlashNotification($"GUARDS ASSIGNED (-$180) • TOTAL SECURITY: {_securityGuards}", Colors.DarkGreen);
                    }
                    else
                    {
                        FlashNotification("ERR: INSUSFFICIENT BUDGET TO HIRE OFFICER!", Colors.DarkRed);
                    }
                    break;

                case Key.Key3:
                case Key.Kp3:
                    // Staff Apology
                    if (_dailyBudget >= 40.0f)
                    {
                        _dailyBudget -= 40.0f;
                        _commuterRage = Math.Max(0.0f, _commuterRage - 8.0f);
                        FlashNotification("STAFF DELAY APOLOGY DIRECTED OVER SPEAKER (-$40)", Colors.DarkGoldenrod);
                    }
                    else
                    {
                        FlashNotification("ERR: INSUFFICIENT BUDGET FOR STAFF BROADCASTS!", Colors.DarkRed);
                    }
                    break;

                case Key.Key4:
                case Key.Kp4:
                    // Toggle Gated Entry
                    _turnstileGated = !_turnstileGated;
                    string gateStr = _turnstileGated ? "ENTRY TURNSTILES RESTRICTED (SLOW INFLOW, BUILD QUEUES)" : "ENTRY TURNSTILES RELEASED TO MAX INFLOW";
                    FlashNotification(gateStr, _turnstileGated ? Colors.DarkGoldenrod : Colors.DarkBlue);
                    break;

                case Key.Key5:
                case Key.Kp5:
                    // Upgrade Station Ventilation
                    if (_dailyBudget >= 800.0f)
                    {
                        _dailyBudget -= 800.0f;
                        _ventilationLevel++;
                        FlashNotification($"VENT SYSTEM UPGRADED (-$800) • LEVEL: {_ventilationLevel}", Colors.DarkGreen);
                    }
                    else
                    {
                        FlashNotification("ERR: INSUFFICIENT BUDGET FOR INFRASTRUCTURE UPGRADE!", Colors.DarkRed);
                    }
                    break;
            }
        }
    }

    private void FlashNotification(string text, Color baseColor)
    {
        _notificationText = text;
        _notificationBg = baseColor;
        _notificationTicksRemaining = 60; // 2 seconds flash
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        float W = viewportSize.X;
        float H = viewportSize.Y;

        // ==========================================
        // 1. DRAW PLATFORM DEPTH (Top 62% of Screen)
        // ==========================================
        
        // Floor asphalt
        DrawRect(new Rect2(0, 0, W, H * 0.62f), Color.Color8(40, 45, 52), true);

        // Ambient ceiling support column beams
        float spacing = W / 4f;
        for (int i = 0; i <= 4; i++)
        {
            float beamX = i * spacing;
            DrawRect(new Rect2(beamX - 15, 0, 30, H * 0.22f), Color.Color8(25, 28, 33), true);
            DrawRect(new Rect2(beamX - 15, H * 0.22f - 10, 30, 10), Color.Color8(80, 85, 95), true);
        }

        // MRT Tracks horizontal overlay
        float trackY = H * 0.22f;
        DrawRect(new Rect2(0, trackY, W, 22), Color.Color8(20, 20, 20), true);
        DrawLine(new Vector2(0, trackY + 3), new Vector2(W, trackY + 3), Color.Color8(120, 125, 130), 2);
        DrawLine(new Vector2(0, trackY + 19), new Vector2(W, trackY + 19), Color.Color8(120, 125, 130), 2);
        
        // Track sleepers (wooden ties)
        for (float sx = 0; sx < W; sx += 40)
        {
            DrawRect(new Rect2(sx + 15, trackY + 3, 10, 16), Color.Color8(75, 50, 35), true);
        }

        // Platform yellow safety warning edge line
        float platEdgeY = trackY + 23;
        Color edgeColor = _platformDensity > 7.0f ? Colors.Red : Colors.Orange;
        DrawRect(new Rect2(0, platEdgeY, W, 8), edgeColor, true);

        // Animated arrivals of the MRT train
        if (_trainArrived)
        {
            float trW = 750.0f;
            float trH = 80.0f;
            Vector2 trTL = new Vector2(W / 2 - trW / 2 + _trainX, trackY - 45);

            // Main body
            DrawRect(new Rect2(trTL.X, trTL.Y, trW, trH), Color.Color8(0, 102, 204), true); // Cyan Blue
            DrawRect(new Rect2(trTL.X, trTL.Y, trW, trH), Color.Color8(220, 220, 220), false, 4f); // border
            DrawRect(new Rect2(trTL.X, trTL.Y + 20, trW, 10), Colors.Yellow, true); // Yellow stripe

            // Cab Windows
            for (int k = 0; k < 6; k++)
            {
                float winX = trTL.X + 45 + k * 120;
                DrawRect(new Rect2(winX, trTL.Y + 12, 60, 24), Color.Color8(30, 30, 30), true);
                
                // Doors underneath
                DrawRect(new Rect2(winX + 15, trTL.Y + 44, 30, 36), Color.Color8(140, 145, 150), true);
                DrawLine(new Vector2(winX + 30, trTL.Y + 44), new Vector2(winX + 30, trTL.Y + 80), Colors.Black, 2);
            }
            // Control Cab visor
            DrawRect(new Rect2(trTL.X + trW - 55, trTL.Y + 12, 45, 32), Color.Color8(20, 20, 20), true);
            DrawText("MRT-3 BLUE", trTL.X + 80, trTL.Y + 18, 12, Colors.White);
        }
        else
        {
            // Indicator
            DrawText("TRAIN APPROACHING IN: " + string.Format("{0:0.0}s", _trainTimer), W / 2, trackY - 15, 15, Colors.LightSkyBlue, HorizontalAlignment.Center);
        }

        // Draw crowded commuter dots based on density metric
        // Max capacity representational dots = 180
        int showCount = (int)((_platformDensity / 10.0f) * 180);
        for (int i = 0; i < showCount && i < _commuterCoordinates.Count; i++)
        {
            Vector2 screenPct = _commuterCoordinates[i];
            Vector2 basePos = new Vector2(screenPct.X * W, screenPct.Y * H);
            
            // Subtle breathing wiggle movement
            float wiggleX = Mathf.Sin(_frameCount * 0.12f + i) * 2.5f;
            float wiggleY = Mathf.Cos(_frameCount * 0.08f + i) * 1.5f;
            Vector2 finalPos = basePos + new Vector2(wiggleX, wiggleY);

            Color passColor = Colors.MediumSpringGreen;
            if (_platformDensity > 7.0f)
            {
                passColor = Colors.Crimson;
            }
            else if (_platformDensity > 4.5f)
            {
                passColor = Colors.Orange;
            }

            // Draw passenger body and head circles programmatically
            DrawCircle(finalPos, 5f, passColor);
            DrawCircle(finalPos - new Vector2(0, 8), 3f, Color.Color8(230, 200, 180)); // Skin head
        }

        // Turnstiles gated queue areas
        if (_turnstileGated)
        {
            float gateX = W - 90;
            float gateY = H * 0.38f;
            DrawRect(new Rect2(gateX, gateY, 70, 45), Color.Color8(120, 60, 20), true);
            DrawText("GATED ENTRY", gateX + 35, gateY + 28, 11, Colors.White, HorizontalAlignment.Center);

            // Draw queue stacks
            for (int k = 0; k < 15; k++)
            {
                float qx = gateX - 20 - (k % 5) * 15;
                float qy = gateY + 10 + (k / 5) * 14;
                DrawCircle(new Vector2(qx, qy), 4.5f, Colors.OrangeRed);
            }
        }

        // ==========================================
        // 2. CONTROL PANEL HUD (Bottom 38% of Screen)
        // ==========================================
        float hudY = H * 0.62f;
        DrawRect(new Rect2(0, hudY, W, H * 0.38f), Color.Color8(25, 27, 30), true);
        DrawLine(new Vector2(0, hudY), new Vector2(W, hudY), Color.Color8(70, 75, 80), 4);

        // Header Title
        DrawRect(new Rect2(0, hudY + 4, W, 26), Color.Color8(15, 17, 20), true);
        DrawText("⚡ EDSA METRO TRAFFIC CONTROL CENTER - TYCOON HUD ⚡", W / 2, hudY + 22, 14, Colors.LightGoldenrod, HorizontalAlignment.Center);

        // Column 1 Box: Financial Status Card
        float cardY = hudY + 42;
        float cardH = H - cardY - 45;
        DrawRect(new Rect2(25, cardY, 220, cardH), Color.Color8(10, 12, 15), true);
        DrawRect(new Rect2(25, cardY, 220, cardH), Colors.DarkGreen, false, 2f);
        DrawText("DAILY BUDGET", 135, cardY + 24, 13, Colors.DarkSeaGreen, HorizontalAlignment.Center);
        DrawText($"${_dailyBudget:F2}", 135, cardY + 54, 20, Colors.White, HorizontalAlignment.Center);
        DrawText($"-${(15.0f + _securityGuards * 4.0f + _ventilationLevel * 5.0f):F1}/s Overhead fee", 135, cardY + 80, 11, Colors.IndianRed, HorizontalAlignment.Center);

        // Column 2 Box: Platform density gauge
        float card2X = 270;
        DrawRect(new Rect2(card2X, cardY, 230, cardH), Color.Color8(10, 12, 15), true);
        Color capColor = _platformDensity > 7.0f ? Colors.DimGray : (_platformDensity > 4.5f ? Colors.Goldenrod : Colors.Green);
        DrawRect(new Rect2(card2X, cardY, 230, cardH), capColor, false, 2f);
        DrawText("PLATFORM DENSITY", card2X + 115, cardY + 24, 13, Colors.LightSkyBlue, HorizontalAlignment.Center);
        DrawText($"{_platformDensity:F2} / 10.00", card2X + 115, cardY + 54, 20, Colors.White, HorizontalAlignment.Center);
        
        // Gauge bar inside card
        float barW = 180;
        float barH = 12;
        float barX = card2X + 25;
        float barY = cardY + 70;
        DrawRect(new Rect2(barX, barY, barW, barH), Color.Color8(30, 30, 30), true);
        DrawRect(new Rect2(barX, barY, barW * (_platformDensity / 10.0f), barH), _platformDensity > 4.5f ? Colors.Red : Colors.Green, true);

        // Column 3 Box: Riot Meter (Commuter Rage) progress bar
        float card3X = 525;
        float card3W = W - card3X - 25;
        DrawRect(new Rect2(card3X, cardY, card3W, cardH), Color.Color8(10, 12, 15), true);
        Color rBarColor = _commuterRage > 80.0f ? Colors.Crimson : (_commuterRage > 50.0f ? Colors.DarkOrange : Colors.MediumSpringGreen);
        DrawRect(new Rect2(card3X, cardY, card3W, cardH), rBarColor, false, 2f);
        DrawText("RIOT METER (RAGE)", card3X + card3W/2, cardY + 24, 13, Colors.IndianRed, HorizontalAlignment.Center);
        DrawText($"{_commuterRage:F1}%", card3X + card3W/2, cardY + 54, 20, Colors.White, HorizontalAlignment.Center);

        // Progress bar percentage inside card
        float rageBarW = card3W - 50;
        float rBarX = card3X + 25;
        DrawRect(new Rect2(rBarX, barY, rageBarW, barH), Color.Color8(30, 30, 30), true);
        DrawRect(new Rect2(rBarX, barY, rageBarW * (_commuterRage / 100.0f), barH), rBarColor, true);

        // Operational upgrades text row
        float statusRowsY = cardY + cardH + 12;
        string gateStatusStr = _turnstileGated ? "RESTRICTED (GATED)" : "RELEASED (OPEN)";
        DrawText($"VENTILATION SYSTEM: Lvl {_ventilationLevel}   |   OFFICERS DEPLOYED: {_securityGuards}   |   TURNSTYLE CONTROL: {gateStatusStr}", 25, statusRowsY, 12, Colors.LightGray);
        DrawText($"Timer: {FormatShiftTime(_timeRemaining)}   |   Total Commuters Dispatched: {_passengersServed}", W - 25, statusRowsY, 12, Colors.Yellow, HorizontalAlignment.Right);

        // Operations Hotkeys Alerts Banner
        float bannerY = H - 32;
        DrawRect(new Rect2(0, bannerY, W, 32), _notificationBg, true);
        DrawText("HOTKEYS: [1] Extra Train ($400) | [2] Officer Staff ($180) | [3] Speaker Apology ($40) | [4] Toggle Gate | [5] Vent Aircon ($800)", 20, bannerY + 20, 11, Colors.White);
        DrawText(_notificationText, W - 20, bannerY + 20, 11, Colors.White, HorizontalAlignment.Right);

        // ==========================================
        // 3. OVERLAYS (Win/GameOver Riot panels)
        // ==========================================
        if (_riotErupted)
        {
            float boxW = 660f;
            float boxH = 220f;
            Vector2 boxTL = new Vector2(W/2 - boxW/2, H/2 - boxH/2 - 40);

            // Shaded backdrop
            DrawRect(new Rect2(0, 0, W, H), Color.Color8(0, 0, 0, 150), true);

            FillRect(boxTL.X, boxTL.Y, boxW, boxH, Color.Color8(128, 0, 0));
            DrawRect(new Rect2(boxTL.X, boxTL.Y, boxW, boxH), Colors.Yellow, false, 4f);

            DrawText("🚨 !!! GAME OVER / SYSTEM LOSS !!! 🚨", W/2, boxTL.Y + 45, 24, Colors.Yellow, HorizontalAlignment.Center);
            DrawText("THE STATION INCURRED A MASSIVE PUBLIC COMMUTER RIOT!", W/2, boxTL.Y + 95, 17, Colors.White, HorizontalAlignment.Center);
            DrawText("Rage levels hit 100%. Commuter loading was completely halted.", W/2, boxTL.Y + 125, 14, Colors.LightGray, HorizontalAlignment.Center);
            DrawText("Press [ R ] to Restart Tyler Shift", W/2, boxTL.Y + 180, 16, Colors.Gold, HorizontalAlignment.Center);
        }

        if (_dayCompleted && !_riotErupted)
        {
            float boxW = 660f;
            float boxH = 220f;
            Vector2 boxTL = new Vector2(W/2 - boxW/2, H/2 - boxH/2 - 40);

            DrawRect(new Rect2(0, 0, W, H), Color.Color8(0, 0, 0, 150), true);

            FillRect(boxTL.X, boxTL.Y, boxW, boxH, Color.Color8(0, 80, 40));
            DrawRect(new Rect2(boxTL.X, boxTL.Y, boxW, boxH), Colors.Gold, false, 4f);

            DrawText("🏆 !!! SHIFT COMPLETED !!! 🏆", W/2, boxTL.Y + 45, 24, Colors.Gold, HorizontalAlignment.Center);
            DrawText("YOU CLEANLY SURVIVED THE EDSA MANAGER SHIFT!", W/2, boxTL.Y + 95, 17, Colors.White, HorizontalAlignment.Center);
            DrawText($"Final Budget Remaining: ${_dailyBudget:F2}  |  Boarders Served: {_passengersServed}", W/2, boxTL.Y + 125, 14, Colors.LightGray, HorizontalAlignment.Center);
            DrawText("Press [ R ] to Play Shift Again", W/2, boxTL.Y + 180, 16, Colors.Yellow, HorizontalAlignment.Center);
        }
    }

    private string FormatShiftTime(float t)
    {
        int min = (int)t / 60;
        int sec = (int)t % 60;
        return $"{min:D2}:{sec:D2}";
    }

    private void DrawText(string text, float x, float y, int size, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        if (_font == null) return;
        DrawString(_font, new Vector2(x, y), text, alignment, -1, size, color);
    }

    private void FillRect(float x, float y, float w, float h, Color color)
    {
        DrawRect(new Rect2(x, y, w, h), color, true);
    }

    public override void _ExitTree()
    {
        _isRunning = false;
        _gameTimer?.Dispose();
    }
}