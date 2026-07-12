#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum Perspective
{
    PLATFORM,
    UNDER_STATION,
    INSIDE_CARS
}

public partial class HomeworkWorkspace : Node2D
{
    // Core game state matching simulation values
    private Perspective _activePerspective = Perspective.PLATFORM;
    private float _globalCommuterRage = 0.0f;
    private float _dailyBudget = 5000.0f;
    private bool _riotErupted = false;
    private bool _isRunning = true;

    // Platform Metrics
    private float _trainDelayTimer = 0.0f;
    private int _pickpocketCount = 0;
    private float _priorityQueueViolationRate = 0.05f;

    // Under-Station Metrics
    private int _ticketMachineFailures = 0;
    private float _escalatorWeightStrain = 10.0f;

    // Inside-Cars Metrics
    private float _carCrowdDensity = 2.0f;
    private float _acFailureChance = 0.05f;
    private bool _acFailed = false;

    // Window coordinate shakes
    private Vector2 _originalPosition;
    private Vector2 _screenOffset = Vector2.Zero;
    private int _frameCount = 0;

    // HUD Notifications
    private string _notificationText = "SWAP VIEW CHANNELS USING HOTKEYS 1, 2, or 3.";
    private Color _notificationBg = Colors.DarkSlateGray;
    private int _notificationTicks = 60;

    // Loop timer
    private System.Threading.Timer? _gameTimer;
    private SystemFont? _font;

    // Cache of passenger coordinates wiggles
    private readonly List<Vector2> _commuterCoordinates = new List<Vector2>();
    private readonly List<Vector2> _underStationPeople = new List<Vector2>();
    private readonly Random _random = new Random(512);

    public override void _Ready()
    {
        _originalPosition = Position;

        // Default fonts properties
        SystemFont fontObj = new SystemFont();
        fontObj.FontNames = new string[] { "sans-serif", "Segoe UI", "Arial" };
        _font = fontObj;

        // Generate stationary commuter coordinate positions
        for (int i = 0; i < 150; i++)
        {
            float rx = (float)(_random.NextDouble() * 0.8 + 0.1);
            float ry = (float)(_random.NextDouble() * 0.2 + 0.28);
            _commuterCoordinates.Add(new Vector2(rx, ry));
        }

        // Generate concourse commuters
        for (int i = 0; i < 40; i++)
        {
            float rx = (float)(_random.NextDouble() * 0.4 + 0.05);
            float ry = (float)(_random.NextDouble() * 0.15 + 0.35);
            _underStationPeople.Add(new Vector2(rx, ry));
        }

        InitializeGame();
    }

    private void InitializeGame()
    {
        _globalCommuterRage = 0.0f;
        _dailyBudget = 5000.0f;
        _activePerspective = Perspective.PLATFORM;
        _riotErupted = false;

        _trainDelayTimer = 0.0f;
        _pickpocketCount = 0;
        _priorityQueueViolationRate = 0.05f;

        _ticketMachineFailures = 0;
        _escalatorWeightStrain = 10.0f;

        _carCrowdDensity = 2.0f;
        _acFailureChance = 0.05f;
        _acFailed = false;

        _screenOffset = Vector2.Zero;
        Position = _originalPosition;

        _notificationText = "METRIC SWITCHBOARD ONLINE! REDUCE PEAK STATIONS TENSIONS.";
        _notificationBg = Colors.DarkCyan;
        _notificationTicks = 90;

        _gameTimer?.Dispose();
        _gameTimer = new System.Threading.Timer(OnTimerTick, null, 0, 33);
    }

    private void OnTimerTick(object? state)
    {
        if (!_isRunning) return;
        CallDeferred(nameof(GameUpdateStep));
    }

    private void GameUpdateStep()
    {
        if (_riotErupted)
        {
            Random rng = new Random();
            _screenOffset = new Vector2(rng.Next(-5, 6), rng.Next(-3, 4));
            Position = _originalPosition + _screenOffset;
            QueueRedraw();
            return;
        }

        _frameCount++;
        float deltaTime = 1.0f / 30.0f;

        // 1. Platform updates
        _trainDelayTimer += deltaTime;
        if (_frameCount % 180 == 0 && _pickpocketCount < 10)
        {
            _pickpocketCount++;
        }
        _priorityQueueViolationRate = Math.Min(1.0f, _priorityQueueViolationRate + 0.015f * deltaTime);

        // 2. Under-Station Updates
        if (_frameCount % 270 == 0 && _ticketMachineFailures < 4)
        {
            _ticketMachineFailures++;
        }
        _escalatorWeightStrain = Math.Min(100.0f, _escalatorWeightStrain + (2.2f + _ticketMachineFailures * 1.8f) * deltaTime);

        // 3. Inside-Cars Updates
        _carCrowdDensity = Math.Min(10.0f, _carCrowdDensity + 0.18f * deltaTime);
        _acFailureChance = Math.Min(1.0f, _acFailureChance + 0.022f * _carCrowdDensity * deltaTime);
        if (!_acFailed && _acFailureChance > 0.6f && _random.NextDouble() < 0.006f)
        {
            _acFailed = true;
            FlashNotification("💥 ALERT: TRAIN COMPRESSOR OVERLOAD TRIP!", Colors.Crimson);
        }

        // --- EXPONENTIAL RAGE METRICS ADDITIONS ---
        float rageAddition = 0.0f;

        if (_trainDelayTimer > 15.0f)
        {
            float excess = _trainDelayTimer - 15.0f;
            rageAddition += Mathf.Exp(excess * 0.15f) * 0.35f * deltaTime;
        }
        if (_escalatorWeightStrain > 75.0f)
        {
            float excess = _escalatorWeightStrain - 75.0f;
            rageAddition += Mathf.Exp(excess * 0.09f) * 0.45f * deltaTime;
        }
        if (_carCrowdDensity > 8.0f)
        {
            float excess = _carCrowdDensity - 8.0f;
            rageAddition += Mathf.Exp(excess * 0.5f) * 0.65f * deltaTime;
        }

        // flat modifiers
        rageAddition += _pickpocketCount * 0.25f * deltaTime;
        rageAddition += _ticketMachineFailures * 0.4f * deltaTime;
        if (_acFailed)
        {
            rageAddition += 8.0f * deltaTime;
        }

        _globalCommuterRage += rageAddition;

        // cooling down
        if (_trainDelayTimer <= 15.0f && _escalatorWeightStrain <= 75.0f && _carCrowdDensity <= 8.0f && !_acFailed)
        {
            _globalCommuterRage = Math.Max(0.0f, _globalCommuterRage - 1.5f * deltaTime);
        }

        if (_globalCommuterRage > 100.0f) _globalCommuterRage = 100.0f;
        if (_globalCommuterRage >= 100.0f)
        {
            _riotErupted = true;
        }

        // Budget operational costs
        float operatingCost = 8.0f + (_ticketMachineFailures * 3.0f);
        _dailyBudget = Math.Max(0.0f, _dailyBudget - operatingCost * deltaTime);

        // Screen shakes when density is high
        if (_carCrowdDensity > 8.0f || _escalatorWeightStrain > 85.0f)
        {
            Random rng = new Random();
            _screenOffset = new Vector2(rng.Next(-2, 3), rng.Next(-1, 2));
            Position = _originalPosition + _screenOffset;
        }
        else
        {
            _screenOffset = Vector2.Zero;
            Position = _originalPosition;
        }

        // Notifications tickers
        if (_notificationTicks > 0)
        {
            _notificationTicks--;
            if (_notificationTicks <= 0)
            {
                _notificationText = "HOTKEYS: [1] Platform View   |   [2] Under-Station View   |   [3] Inside-Cars View";
                _notificationBg = Colors.DarkSlateGray;
            }
        }

        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (_riotErupted)
            {
                if (keyEvent.Keycode == Key.R)
                {
                    InitializeGame();
                }
                return;
            }

            // Keyboard perspective swappers
            if (keyEvent.Keycode == Key.Key1 || keyEvent.Keycode == Key.Kp1)
            {
                _activePerspective = Perspective.PLATFORM;
                FlashNotification("ACTIVE CAMERA: PLATFORM DECK MONITOR 1", Colors.DarkBlue);
                return;
            }
            if (keyEvent.Keycode == Key.Key2 || keyEvent.Keycode == Key.Kp2)
            {
                _activePerspective = Perspective.UNDER_STATION;
                FlashNotification("ACTIVE CAMERA: UNDER-STATION CONCOURSE 2", Colors.DarkGoldenrod);
                return;
            }
            if (keyEvent.Keycode == Key.Key3 || keyEvent.Keycode == Key.Kp3)
            {
                _activePerspective = Perspective.INSIDE_CARS;
                FlashNotification("ACTIVE CAMERA: METRO CARRIAGE DECK 3", Colors.DarkSlateBlue);
                return;
            }

            // Viewport local action controls
            switch (_activePerspective)
            {
                case Perspective.PLATFORM:
                    if (keyEvent.Keycode == Key.D)
                    {
                        _trainDelayTimer = 0.0f;
                        _carCrowdDensity = Math.Min(10.0f, _carCrowdDensity + 2.0f);
                        _dailyBudget = Math.Max(0.0f, _dailyBudget - 100.0f);
                        FlashNotification("EXPRESS MRT DEPLOYED! DELAY RESET (-$100)", Colors.DarkGreen);
                    }
                    else if (keyEvent.Keycode == Key.P)
                    {
                        if (_pickpocketCount > 0)
                        {
                            _pickpocketCount--;
                            _dailyBudget = Math.Max(0.0f, _dailyBudget - 50.0f);
                            FlashNotification("SECURITY APPREHENDED PICKPOCKET (-$50)", Colors.DarkGreen);
                        }
                    }
                    else if (keyEvent.Keycode == Key.Q)
                    {
                        _priorityQueueViolationRate = Math.Max(0.0f, _priorityQueueViolationRate - 0.15f);
                        _dailyBudget = Math.Max(0.0f, _dailyBudget - 30.0f);
                        FlashNotification("ENFORCED PRIORITY BOARDINGS (-$30)", Colors.DarkGreen);
                    }
                    break;

                case Perspective.UNDER_STATION:
                    if (keyEvent.Keycode == Key.F)
                    {
                        if (_ticketMachineFailures > 0)
                        {
                            _ticketMachineFailures--;
                            _dailyBudget = Math.Max(0.0f, _dailyBudget - 120.0f);
                            FlashNotification("TICKET TERMINAL PARTS REPLACED (-$120)", Colors.DarkGreen);
                        }
                    }
                    else if (keyEvent.Keycode == Key.S)
                    {
                        _escalatorWeightStrain = Math.Max(0.0f, _escalatorWeightStrain - 25.0f);
                        _dailyBudget = Math.Max(0.0f, _dailyBudget - 40.0f);
                        FlashNotification("ESCALATOR FORCE SHIFT DOWN (-$40)", Colors.DarkGreen);
                    }
                    break;

                case Perspective.INSIDE_CARS:
                    if (keyEvent.Keycode == Key.A)
                    {
                        _acFailed = false;
                        _acFailureChance = 0.05f;
                        _dailyBudget = Math.Max(0.0f, _dailyBudget - 150.0f);
                        FlashNotification("REPAIRED CAR AC REFRIGERATOR VENT (-$150)", Colors.DarkGreen);
                    }
                    break;
            }
        }
    }

    private void FlashNotification(string text, Color baseColor)
    {
        _notificationText = text;
        _notificationBg = baseColor;
        _notificationTicks = 60;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 scaleSize = GetViewportRect().Size;
        float W = scaleSize.X;
        float H = scaleSize.Y;

        // ==========================================
        // 1. SPECIFICATION 4: PERSISTENT GLOBAL HUD
        // ==========================================
        DrawRect(new Rect2(0, 0, W, H * 0.18f), Color.Color8(18, 20, 24), true);
        DrawLine(new Vector2(0, H * 0.18f), new Vector2(W, H * 0.18f), Color.Color8(70, 75, 80), 3);

        string viewName = _activePerspective switch
        {
            Perspective.PLATFORM => "PLATFORM CAMERA SCREEN",
            Perspective.UNDER_STATION => "UNDER-STATION CONCOURSE SCREEN",
            Perspective.INSIDE_CARS => "TRAIN PASSENGER CARS SCREEN",
            _ => "SYSTEM PERSPECTIVE"
        };
        DrawText($"ACTIVE VIEW: {viewName}   |   Daily Budget: ${_dailyBudget:F2}", 24, 25, 14, Colors.LightGoldenrod);

        // Global Riot meter bar representation
        DrawText("Global Riot Meter (Rage): ", 24, 55, 12, Colors.LightGray);
        float barW = 280;
        float barH = 14;
        float barX = 220;
        float barY = 44;
        DrawRect(new Rect2(barX, barY, barW, barH), Color.Color8(40, 40, 45), true);
        Color rCol = _globalCommuterRage > 80.0f ? Colors.Crimson : (_globalCommuterRage > 50.0f ? Colors.Gold : Colors.MediumSpringGreen);
        DrawRect(new Rect2(barX, barY, barW * (_globalCommuterRage / 100.0f), barH), rCol, true);
        DrawText($"{_globalCommuterRage:F1}%", barX + barW + 15, 55, 13, rCol);

        // ==========================================
        // 2. SPECIFICATION 4: DYNAMIC VIEWPORT CANVAS
        // ==========================================
        float viewY = H * 0.18f + 3;
        float viewH = H * 0.72f - 6;

        switch (_activePerspective)
        {
            case Perspective.PLATFORM:
                DrawPlatformGraphics(0, viewY, W, viewH);
                break;
            case Perspective.UNDER_STATION:
                DrawUnderStationGraphics(0, viewY, W, viewH);
                break;
            case Perspective.INSIDE_CARS:
                DrawInsideCarsGraphics(0, viewY, W, viewH);
                break;
        }

        // Bottom Operations Notification Banner
        float bannerY = H - 32;
        DrawRect(new Rect2(0, bannerY, W, 32), _notificationBg, true);
        DrawText(_notificationText, W / 2, bannerY + 20, 11, Colors.White, HorizontalAlignment.Center);

        // Win/Loss overlay (Specification 3)
        if (_riotErupted)
        {
            DrawRect(new Rect2(0, 0, W, H), Color.Color8(0, 0, 0, 170), true);

            float panelW = 680f;
            float panelH = 220f;
            Vector2 pTL = new Vector2(W/2 - panelW/2, H/2 - panelH/2);

            DrawRect(new Rect2(pTL.X, pTL.Y, panelW, panelH), Colors.DarkRed, true);
            DrawRect(new Rect2(pTL.X, pTL.Y, panelW, panelH), Colors.Gold, false, 4f);

            DrawText("👮 !!! STATION RIOT TRIGGERED! YOU ARE FIRED. !!! 👮", W/2, pTL.Y + 45, 23, Colors.Yellow, HorizontalAlignment.Center);
            DrawText("Metro rage index hit 100% capacity threshold. Controls collapsed.", W/2, pTL.Y + 95, 15, Colors.White, HorizontalAlignment.Center);
            DrawText("Your operations shift coordinator contract was terminated instantly.", W/2, pTL.Y + 125, 13, Colors.LightGray, HorizontalAlignment.Center);
            DrawText("Press [ R ] to restart shift metrics", W/2, pTL.Y + 180, 15, Colors.Yellow, HorizontalAlignment.Center);
        }
    }

    private void DrawPlatformGraphics(float x, float y, float w, float h)
    {
        // Paint gray concrete platform area
        DrawRect(new Rect2(x, y, w, h), Color.Color8(50, 55, 62), true);
        DrawRect(new Rect2(x, y, w, h), Color.Color8(120, 125, 130), false, 4f);

        // Ceiling Column Beams
        float step = w / 4.0f;
        for (int i = 1; i < 4; i++)
        {
            float beamX = x + i * step;
            DrawRect(new Rect2(beamX - 10, y + 2, 20, 48), Color.Color8(30, 32, 38), true);
            DrawRect(new Rect2(beamX - 10, y + 50, 20, 5), Color.Color8(90, 95, 100), true);
        }

        // Draw railway tracks
        float trackY = y + 75;
        DrawRect(new Rect2(x, trackY, w, 24), Color.Color8(18, 18, 18), true);
        DrawLine(new Vector2(x, trackY + 4), new Vector2(x + w, trackY + 4), Color.Color8(110, 115, 120), 2);
        DrawLine(new Vector2(x, trackY + 20), new Vector2(x + w, trackY + 20), Color.Color8(110, 115, 120), 2);
        // Sleepers
        for (float sx = x + 15; sx < x + w - 10; sx += 45)
        {
            DrawRect(new Rect2(sx, trackY + 4, 10, 16), Color.Color8(72, 50, 36), true);
        }

        // Yellow caution safety border line
        float cautionY = trackY + 26;
        for (float cx = x; cx < x + w; cx += 25)
        {
            DrawRect(new Rect2(cx, cautionY, 15, 6), Colors.Yellow, true);
        }

        // Render arriving train silhouette based on frame timers
        float trainOffset = (_frameCount * 3) % (w + 400) - 200;
        float trW = 400;
        float trH = 50;
        DrawRect(new Rect2(x + trainOffset, trackY - 15, trW, trH), Color.Color8(0, 80, 180), true);
        DrawRect(new Rect2(x + trainOffset, trackY - 15, trW, trH), Colors.White, false, 2f);
        // Train Cab shield
        DrawRect(new Rect2(x + trainOffset + trW - 35, trackY - 8, 30, 22), Color.Color8(20, 20, 20), true);
        // Windows
        for (int k = 0; k < 3; k++)
        {
            DrawRect(new Rect2(x + trainOffset + 30 + k * 110, trackY - 8, 55, 16), Color.Color8(33, 33, 33), true);
        }

        // Display commuters circles wiggling w/ comfort bounds check
        int passCount = Math.Min(120, (int)(_trainDelayTimer * 1.8f) + 12);
        for (int i = 0; i < passCount && i < _commuterCoordinates.Count; i++)
        {
            Vector2 pct = _commuterCoordinates[i];
            float px = pct.X * w;
            float py = y + (pct.Y - 0.28f) * h + 38; // clamp on platform deck

            float wx = Mathf.Sin(_frameCount * 0.1f + i) * 2f;
            float wy = Mathf.Cos(_frameCount * 0.08f + i) * 1.5f;

            Color col = _priorityQueueViolationRate > 0.4f ? Colors.Tomato : Colors.LightGreen;
            DrawCircle(new Vector2(px + wx, py + wy), 5f, col);
            DrawCircle(new Vector2(px + wx, py + wy - 8), 3f, Color.Color8(230, 200, 185)); // head
        }

        // UI text overlays inside Viewport Canvas
        DrawText("CAMERA DECK 01 - METRIC STATS:", x + 25, y + 25, 13, Colors.Yellow);
        
        Color dColor = _trainDelayTimer > 15.0f ? Colors.Crimson : Colors.LightGreen;
        DrawText($"Train Delay Timer  : {_trainDelayTimer:F1}s (Threshold: 15.0s)", x + 25, y + 50, 12, dColor);
        DrawText($"Pickpocket Count   : {_pickpocketCount} active thieves report", x + 25, y + 70, 12, _pickpocketCount > 3 ? Colors.Gold : Colors.White);
        DrawText($"Priority Line Viol : {(_priorityQueueViolationRate * 100f):F1}% rate", x + 25, y + 90, 12, _priorityQueueViolationRate > 0.4f ? Colors.Orange : Colors.White);

        // Control HUD text row
        DrawText("HOTKEYS: [D] Dispatch Train (-$100)  |  [P] Bust Thief (-$50)  |  [Q] Check Queue (-$30)", x + 20, y + h - 22, 11, Colors.LightSkyBlue);
    }

    private void DrawUnderStationGraphics(float x, float y, float w, float h)
    {
        // Underground concrete lobby (Dark Cyan panel)
        DrawRect(new Rect2(x, y, w, h), Color.Color8(30, 52, 60), true);
        DrawRect(new Rect2(x, y, w, h), Color.Color8(80, 160, 180), false, 4f);

        // Columns
        for (int dx = 120; dx < w - 50; dx += 260)
        {
            DrawRect(new Rect2(dx, y + 10, 48, h - 20), Color.Color8(22, 38, 44), true);
        }

        // Draw Ticket Vending Cabinets
        float tvmStartY = y + 45;
        for (int k = 0; k < 4; k++)
        {
            float tvmX = x + 35 + (k * 105);
            DrawRect(new Rect2(tvmX, tvmStartY, 72, 100), Color.Color8(12, 15, 20), true);
            DrawRect(new Rect2(tvmX, tvmStartY, 72, 100), Colors.DeepSkyBlue, false, 2f);

            // TVM monitors
            DrawRect(new Rect2(tvmX + 12, tvmStartY + 12, 48, 38), Color.Color8(40, 40, 40), true);
            if (k < _ticketMachineFailures)
            {
                DrawRect(new Rect2(tvmX + 12, tvmStartY + 12, 48, 38), Colors.Crimson, true);
                DrawText("OUT OF", tvmX + 36, tvmStartY + 26, 9, Colors.White, HorizontalAlignment.Center);
                DrawText("ORDER", tvmX + 36, tvmStartY + 38, 9, Colors.White, HorizontalAlignment.Center);
            }
            else
            {
                DrawText("INSERT CASH", tvmX + 36, tvmStartY + 30, 8, Colors.SpringGreen, HorizontalAlignment.Center);
            }

            // Coin feeder slots
            DrawRect(new Rect2(tvmX + 22, tvmStartY + 65, 28, 8), Colors.Gray, true);
        }

        // Draw Escalators structures
        float escX = w - 190;
        float escY = y + 25;
        DrawRect(new Rect2(escX, escY, 170, h - 50), Color.Color8(15, 25, 30), true);
        DrawRect(new Rect2(escX, escY, 170, h - 50), Colors.Yellow, false, 3f);
        DrawText("ESCALATOR SYSTEMS", escX + 85, escY + 22, 11, Colors.LightGoldenrod, HorizontalAlignment.Center);

        // Draw dynamic steps lines climbing/cycling
        int offset = _frameCount % 4;
        for (int k = 0; k < 12; k++)
        {
            float sy = escY + 45 + k * 14;
            if (sy < escY + h - 70)
            {
                float stepLX = escX + 25 + (k * 4);
                float stepRX = escX + 115 - (k * 4);
                DrawLine(new Vector2(stepLX, sy + offset), new Vector2(stepLX + 28, sy + offset), Colors.Gray, 3);
                DrawLine(new Vector2(stepRX, sy - offset), new Vector2(stepRX + 28, sy - offset), Colors.Gray, 3);
            }
        }

        // Text data
        DrawText("CONCOURSE SYSTEM HUD 02:", x + 25, y + 22, 13, Colors.Yellow);
        
        Color fColor = _ticketMachineFailures > 2 ? Colors.Crimson : Colors.White;
        DrawText($"TVM Failed Terminals : {_ticketMachineFailures} booths offline", x + 25, y + 175, 12, fColor);

        Color sCol = _escalatorWeightStrain > 75.0f ? Colors.Crimson : Colors.LightGreen;
        DrawText($"Escalator Load Strain: {_escalatorWeightStrain:F1}% (Threshold Limit: 75.0%)", x + 25, y + 198, 12, sCol);

        // Control guide row
        DrawText("HOTKEYS: [F] Service Ticket Booth (-$120)  |  [S] Cool Escalator Strain (-$40)", x + 20, y + h - 22, 11, Colors.LightSkyBlue);
    }

    private void DrawInsideCarsGraphics(float x, float y, float w, float h)
    {
        // metallic train interior (Blue panel)
        DrawRect(new Rect2(x, y, w, h), Color.Color8(12, 35, 80), true);
        DrawRect(new Rect2(x, y, w, h), Color.Color8(40, 100, 200), false, 4f);

        // Draw scrolling window frames looking out to dark tunnels
        float winW = 145f;
        float winH = 80f;
        float startX = x + 35f;
        float winY = y + 36f;
        
        int blockOffset = (_frameCount * 4) % 250;

        for (int i = 0; i < 3; i++)
        {
            float curWinX = startX + i * 190;
            // Window bounds
            DrawRect(new Rect2(curWinX, winY, winW, winH), Color.Color8(1, 15, 30), true);
            
            // Scrolling tunnel pillars blocks in background
            float pX = curWinX + 30 + ((blockOffset) % 110);
            if (pX < curWinX + winW - 15)
            {
                DrawRect(new Rect2(pX, winY, 15, winH), Color.Color8(25, 27, 30), true);
            }

            DrawRect(new Rect2(curWinX, winY, winW, winH), Colors.Cyan, false, 3f);
        }

        // Overhead safety handles
        for (float hx = x + 24; hx < x + w - 24; hx += 70)
        {
            DrawLine(new Vector2(hx, y + 1), new Vector2(hx, y + 25), Colors.Gray, 3);
            DrawCircle(new Vector2(hx, y + 25), 8f, Colors.Gold);
        }

        // Passenger Seats rows
        DrawRect(new Rect2(x + 40, y + 145, w - 80, 28), Color.Color8(30, 80, 150), true);
        DrawRect(new Rect2(x + 40, y + 145, w - 80, 28), Colors.Cyan, false, 2f);
        DrawText("[ Row Seat A - Commuters Sector ]", w / 2, y + 163, 11, Colors.LightBlue, HorizontalAlignment.Center);

        // Stats panel data details
        DrawText("INSIDE DECK SYSTEM HUD 03:", x + 25, y + 15, 13, Colors.Yellow);

        Color dc = _carCrowdDensity > 8.0f ? Colors.Crimson : Colors.LightGreen;
        DrawText($"Car crowd Density: {_carCrowdDensity:F2} / 10.0 (Threshold: 8.0)", x + w - 380, y + 15, 12, dc);
        DrawText($"AC Strain Index  : {(_acFailureChance * 100f):F1}%", x + w - 380, y + 215, 11, Colors.LightSkyBlue);

        // Flashing AC warnings (Specification 4)
        if (_acFailed)
        {
            float warnW = 280;
            float warnH = 45;
            float warnX = x + 25;
            float warnY = y + 195;

            DrawRect(new Rect2(warnX, warnY, warnW, warnH), Colors.Crimson, true);
            DrawRect(new Rect2(warnX, warnY, warnW, warnH), Colors.Yellow, false, 2f);
            DrawText("🚨 !!! AIR CONDITIONER FAIL !!! 🚨", warnX + warnW/2, warnY + 20, 12, Colors.Yellow, HorizontalAlignment.Center);
            DrawText("TEMPS OVER CAPACITY - COOL AC IMMEDIATELY!", warnX + warnW/2, warnY + 36, 10, Colors.White, HorizontalAlignment.Center);
        }
        else
        {
            DrawText("Carriage vents cooling status: FUNCTIONAL (21 C)", x + 25, y + 215, 11, Colors.SpringGreen);
        }

        // Action inputs helps
        DrawText("HOTKEYS: [A] Service AC Systems (-$150)", x + 20, y + h - 22, 11, Colors.LightSkyBlue);
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

    public override void _ExitTree()
    {
        _isRunning = false;
        _gameTimer?.Dispose();
    }
}