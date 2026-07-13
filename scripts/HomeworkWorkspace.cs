#nullable enable
using Godot;
using System;
using System.Collections.Generic;

public partial class HomeworkWorkspace : Node2D
{
    // Window coordinate shakes
    private Vector2 _originalPosition;
    private Vector2 _screenOffset = Vector2.Zero;
    private int _frameCount = 0;

    // HUD Notifications
    private string _notificationText = "SWAP VIEW CHANNELS USING HOTKEYS 1, 2, or 3.";
    private Color _notificationBg = Colors.DarkSlateGray;
    private int _notificationTicks = 60;

    // Crisis alerts (Week 3)
    private string _crisisAlert1 = "";
    private string _crisisAlert2 = "";
    private Color _crisisColor1 = Colors.Red;
    private Color _crisisColor2 = Colors.Red;

    private SystemFont? _font;
    private Random _random = new Random();

    public override void _Ready()
    {
        _originalPosition = Position;
        SystemFont fontObj = new SystemFont();
        fontObj.FontNames = new string[] { "sans-serif", "Segoe UI", "Arial" };
        _font = fontObj;

        // Subscribe to flash notification events from SimulationManager
        // Ensure this happens after SimulationManager _Ready if using call deferred or directly
        CallDeferred(nameof(InitManagerEvents));
    }

    private void InitManagerEvents()
    {
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.OnFlashNotification += FlashNotification;
        }
    }

    public void FlashNotification(string text, Color baseColor)
    {
        _notificationText = text;
        _notificationBg = baseColor;
        _notificationTicks = 60;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var sim = SimulationManager.Instance;
        if (sim == null) return;

        if (sim.RiotErupted)
        {
            HandleGameInputs(sim);
            _screenOffset = new Vector2(_random.Next(-5, 6), _random.Next(-3, 4));
            Position = _originalPosition + _screenOffset;
            QueueRedraw();
            return;
        }

        _frameCount++;
        
        HandleGameInputs(sim);

        // Update crisis alerts for HUD
        _crisisAlert1 = "";
        _crisisAlert2 = "";
        _crisisColor1 = Colors.Red;
        _crisisColor2 = Colors.Red;

        switch (sim.ActivePerspective)
        {
            case Perspective.PLATFORM:
                if (sim.PickpocketCount > 0)
                {
                    _crisisAlert1 = $"⚠️ {sim.PickpocketCount} PICKPOCKET(S) ACTIVE!";
                    _crisisColor1 = (_frameCount % 10 < 5) ? Colors.Red : Colors.Yellow;
                }
                if (sim.FanActiveOnPlatform)
                {
                    _crisisAlert2 = $"🌀 FANS ON ({sim.FanCooldownTimer:F0}s)";
                    _crisisColor2 = Colors.Cyan;
                }
                else if (sim.TrainDelayTimer > 15.0f)
                {
                    _crisisAlert2 = "⚠️ DELAY CRITICAL!";
                    _crisisColor2 = Colors.Red;
                }
                break;
            case Perspective.UNDER_STATION:
                if (sim.TicketMachineFailures > 0)
                {
                    _crisisAlert1 = $"⚠️ MACHINES DOWN: {sim.TicketMachineFailures}";
                    _crisisColor1 = (_frameCount % 10 < 5) ? Colors.Red : Colors.Yellow;
                }
                if (sim.EscalatorWeightStrain > 75.0f)
                {
                    _crisisAlert2 = "⚠️ ESCALATOR OVERLOAD!";
                    _crisisColor2 = Colors.Red;
                }
                break;
            case Perspective.INSIDE_CARS:
                if (sim.ACFailed)
                {
                    _crisisAlert1 = "⚠️ AC SYSTEM FAILURE!";
                    _crisisColor1 = (_frameCount % 8 < 4) ? Colors.Red : Colors.Yellow;
                }
                if (sim.FanActiveOnCars)
                {
                    _crisisAlert2 = $"🌀 FANS ON ({sim.FanCooldownTimer:F0}s)";
                    _crisisColor2 = Colors.Cyan;
                }
                else if (sim.CarCrowdDensity > 8.0f)
                {
                    _crisisAlert2 = "⚠️ OVERLOADED!";
                    _crisisColor2 = Colors.Red;
                }
                break;
        }

        // Screen shake triggers
        if (_screenOffset != Vector2.Zero && sim.CarCrowdDensity <= 8.0f && sim.EscalatorWeightStrain <= 85.0f)
        {
            _screenOffset = Vector2.Zero;
            Position = _originalPosition;
        }
        else if (sim.CarCrowdDensity > 8.0f || sim.EscalatorWeightStrain > 85.0f)
        {
            _screenOffset = new Vector2(_random.Next(-2, 3), _random.Next(-1, 2));
            Position = _originalPosition + _screenOffset;
        }

        if (_notificationTicks > 0)
        {
            _notificationTicks--;
            if (_notificationTicks <= 0)
            {
                _notificationText = "[1]Platform [2]Under-Station [3]Cars | [F]Fan [G]Guard [E]Push [D]Train";
                _notificationBg = Colors.DarkSlateGray;
            }
        }

        // Toggle visibility of agents based on perspective
        foreach (var agent in sim.Commuters)
        {
            if (IsInstanceValid(agent))
            {
                agent.Visible = (agent.CurrentPerspective == sim.ActivePerspective);
            }
        }

        QueueRedraw();
    }

    private void HandleGameInputs(SimulationManager sim)
    {
        if (sim.RiotErupted && Input.IsActionJustPressed("action_d")) // Wait, restart key usually R
        {
            // Fallback for riot restart, typically R, binding not strict here, but let's just listen to whatever
            // Or just map action_r
        }
        
        // Manual override for restart
        if (sim.RiotErupted && Input.IsPhysicalKeyPressed(Key.R))
        {
            sim.ResetSimulation();
            return;
        }

        if (Input.IsActionJustPressed("view_1"))
        {
            sim.ActivePerspective = Perspective.PLATFORM;
            FlashNotification("CAMERA: PLATFORM DECK 1", Colors.DarkBlue);
        }
        if (Input.IsActionJustPressed("view_2"))
        {
            sim.ActivePerspective = Perspective.UNDER_STATION;
            FlashNotification("CAMERA: UNDER-STATION CONCOURSE 2", Colors.DarkGoldenrod);
        }
        if (Input.IsActionJustPressed("view_3"))
        {
            sim.ActivePerspective = Perspective.INSIDE_CARS;
            FlashNotification("CAMERA: METRO CARRIAGE 3", Colors.DarkSlateBlue);
        }

        // F: Fans
        if (Input.IsActionJustPressed("action_f") && (sim.ActivePerspective == Perspective.PLATFORM || sim.ActivePerspective == Perspective.INSIDE_CARS))
        {
            if (sim.DailyBudget >= 150.0f && sim.FanCooldownTimer <= 0.0f)
            {
                sim.DailyBudget -= 150.0f;
                sim.FanCooldownTimer = 10.0f;
                if (sim.ActivePerspective == Perspective.PLATFORM)
                {
                    sim.FanActiveOnPlatform = true;
                    FlashNotification("🌀 FANS DEPLOYED ON PLATFORM! Rage -50% 10s (-$150)", Colors.DarkGreen);
                }
                else
                {
                    sim.FanActiveOnCars = true;
                    FlashNotification("🌀 FANS DEPLOYED IN CARS! Rage -50% 10s (-$150)", Colors.DarkGreen);
                }
            }
            else if (sim.DailyBudget < 150.0f)
            {
                FlashNotification("⛔ INSUFFICIENT BUDGET! Need $150", Colors.Crimson);
            }
        }

        // G: Guard
        if (Input.IsActionJustPressed("action_g"))
        {
            if (sim.DailyBudget >= 300.0f)
            {
                sim.DailyBudget -= 300.0f;
                switch (sim.ActivePerspective)
                {
                    case Perspective.UNDER_STATION:
                        if (sim.TicketMachineFailures > 0)
                        {
                            sim.TicketMachineFailures--;
                            foreach (var agent in sim.Commuters)
                            {
                                if (agent.IsFrozen && agent.CurrentPerspective == Perspective.UNDER_STATION)
                                    agent.IsFrozen = false;
                            }
                            FlashNotification("🛡️ GUARD FIXED TICKET MACHINE! (-$300)", Colors.DarkGreen);
                        }
                        break;
                    case Perspective.PLATFORM:
                        int removed = 0;
                        foreach (var agent in sim.Commuters)
                        {
                            if (agent.IsPickpocket && agent.CurrentPerspective == Perspective.PLATFORM)
                            {
                                agent.IsPickpocket = false;
                                removed++;
                            }
                        }
                        sim.PickpocketCount = 0;
                        FlashNotification($"🛡️ SWEEP! {removed} pickpockets gone! (-$300)", Colors.DarkGreen);
                        break;
                    case Perspective.INSIDE_CARS:
                        foreach (var agent in sim.Commuters)
                        {
                            if (agent.CurrentPerspective == Perspective.INSIDE_CARS)
                                agent.IndividualRage = Math.Max(0.0f, agent.IndividualRage - 15.0f);
                        }
                        FlashNotification("🛡️ GUARD CALMED PASSENGERS! (-$300)", Colors.DarkGreen);
                        break;
                }
            }
            else
            {
                FlashNotification("⛔ INSUFFICIENT BUDGET! Need $300", Colors.Crimson);
            }
        }

        // E: Vanguard Push
        if (Input.IsActionJustPressed("action_e") && sim.ActivePerspective == Perspective.PLATFORM)
        {
            if (sim.DailyBudget >= 50.0f)
            {
                sim.DailyBudget -= 50.0f;
                
                Vector2 scaleSize = GetViewportRect().Size;
                float viewY = scaleSize.Y * 0.18f + 3;
                float viewH = scaleSize.Y * 0.72f - 6;

                int pushed = 0;
                for (int i = sim.Commuters.Count - 1; i >= 0 && pushed < 5; i--)
                {
                    var agent = sim.Commuters[i];
                    if (agent.CurrentPerspective == Perspective.PLATFORM)
                    {
                        agent.CurrentPerspective = Perspective.INSIDE_CARS;
                        agent.GlobalPosition = new Vector2(100f + _random.Next((int)scaleSize.X - 200), viewY + viewH - 40f);
                        agent.TargetPosition = new Vector2(60f + _random.Next((int)scaleSize.X - 120), viewY + 155f);
                        agent.IndividualRage += 15.0f;
                        pushed++;
                    }
                }
                FlashNotification($"⚡ PUSH! {pushed} forced into cars! (-$50)", Colors.DarkGoldenrod);
            }
        }

        // Perspective specific controls
        switch (sim.ActivePerspective)
        {
            case Perspective.PLATFORM:
                if (Input.IsActionJustPressed("action_d"))
                {
                    sim.TrainDelayTimer = 0.0f;
                    int boarded = 0;
                    bool priorityBoarded = false;
                    bool normalBoardedFirst = false;
                    bool hasPriorityWaiting = false;

                    Vector2 scaleSize = GetViewportRect().Size;
                    float viewY = scaleSize.Y * 0.18f + 3;
                    float viewH = scaleSize.Y * 0.72f - 6;
                        
                    foreach (var agent in sim.Commuters)
                    {
                        if (agent.CurrentPerspective == Perspective.PLATFORM && agent.IsPriority && agent.GlobalPosition.Y <= viewY + 125)
                        {
                            hasPriorityWaiting = true;
                            break;
                        }
                    }

                    for (int i = sim.Commuters.Count - 1; i >= 0; i--)
                    {
                        var agent = sim.Commuters[i];
                        if (agent.CurrentPerspective == Perspective.PLATFORM && agent.GlobalPosition.Y <= viewY + 125)
                        {
                            if (!agent.IsPriority && hasPriorityWaiting && !priorityBoarded)
                                normalBoardedFirst = true;
                            if (agent.IsPriority) priorityBoarded = true;

                            agent.CurrentPerspective = Perspective.INSIDE_CARS;
                            agent.IsPickpocket = false;
                            agent.GlobalPosition = new Vector2(100f + _random.Next((int)scaleSize.X - 200), viewY + viewH - 40f);
                            agent.TargetPosition = new Vector2(60f + _random.Next((int)scaleSize.X - 120), viewY + 155f);
                            boarded++;
                            if (boarded >= 8) break;
                        }
                    }

                    if (normalBoardedFirst && hasPriorityWaiting)
                    {
                        sim.GlobalCommuterRage = Math.Min(100.0f, sim.GlobalCommuterRage + 5.0f);
                        sim.PriorityQueueViolationRate = Math.Min(1.0f, sim.PriorityQueueViolationRate + 0.1f);
                    }

                    int transported = 0;
                    for (int i = sim.Commuters.Count - 1; i >= 0; i--)
                    {
                        var agent = sim.Commuters[i];
                        if (agent.CurrentPerspective == Perspective.INSIDE_CARS && agent.GlobalPosition.Y <= viewY + 165f)
                        {
                            agent.QueueFree();
                            sim.Commuters.RemoveAt(i);
                            transported++;
                        }
                    }

                    float fareRevenue = transported * 15.0f;
                    sim.DailyBudget += fareRevenue;
                    sim.TotalPassengersTransported += transported;
                    sim.TotalFareRevenue += fareRevenue;
                    sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 100.0f);

                    FlashNotification($"🚄 MRT! +{boarded} boarded, {transported} away (+${fareRevenue:F0})", Colors.DarkGreen);
                }
                else if (Input.IsActionJustPressed("action_p"))
                {
                    bool found = false;
                    for (int i = 0; i < sim.Commuters.Count; i++)
                    {
                        if (sim.Commuters[i].IsPickpocket && sim.Commuters[i].CurrentPerspective == Perspective.PLATFORM)
                        {
                            sim.Commuters[i].IsPickpocket = false;
                            sim.PickpocketCount = Math.Max(0, sim.PickpocketCount - 1);
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        FlashNotification("🔒 PICKPOCKET ARRESTED! (-$50)", Colors.DarkGreen);
                        sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 50.0f);
                    }
                }
                else if (Input.IsActionJustPressed("action_q"))
                {
                    sim.PriorityQueueViolationRate = Math.Max(0.0f, sim.PriorityQueueViolationRate - 0.15f);
                    sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 30.0f);
                    FlashNotification("PRIORITY QUEUE ENFORCED (-$30)", Colors.DarkGreen);
                }
                break;

            case Perspective.UNDER_STATION:
                if (Input.IsActionJustPressed("action_s"))
                {
                    sim.EscalatorWeightStrain = Math.Max(0.0f, sim.EscalatorWeightStrain - 25.0f);
                    sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 40.0f);
                    FlashNotification("ESCALATOR LOAD RESET (-$40)", Colors.DarkGreen);
                }
                break;

            case Perspective.INSIDE_CARS:
                if (Input.IsActionJustPressed("action_a"))
                {
                    sim.ACFailed = false;
                    sim.ACFailureChance = 0.05f;
                    sim.ACBreakdownTimer = 25.0f;
                    sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 150.0f);
                    FlashNotification("❄️ AC RESTORED! (-$150)", Colors.DarkGreen);
                }
                break;
        }
    }

    public override void _Draw()
    {
        var sim = SimulationManager.Instance;
        if (sim == null) return;

        Vector2 scaleSize = GetViewportRect().Size;
        float W = scaleSize.X;
        float H = scaleSize.Y;

        // ===== PERSISTENT GLOBAL HUD =====
        DrawRect(new Rect2(0, 0, W, H * 0.18f), Color.Color8(18, 20, 24), true);
        DrawLine(new Vector2(0, H * 0.18f), new Vector2(W, H * 0.18f), Color.Color8(70, 75, 80), 3);

        string viewName = sim.ActivePerspective switch
        {
            Perspective.PLATFORM => "PLATFORM",
            Perspective.UNDER_STATION => "UNDER-STATION",
            Perspective.INSIDE_CARS => "INSIDE CARS",
            _ => "SYSTEM"
        };
        DrawText($"[{viewName}] Budget:${sim.DailyBudget:F0} Fares:${sim.TotalFareRevenue:F0} Transported:{sim.TotalPassengersTransported}", 24, 25, 13, Colors.LightGoldenrod);

        // Rage bar
        DrawText("Rage: ", 24, 55, 12, Colors.LightGray);
        float barW = 240;
        float barH = 14;
        float barX = 75;
        float barY = 44;
        DrawRect(new Rect2(barX, barY, barW, barH), Color.Color8(40, 40, 45), true);
        Color rCol = sim.GlobalCommuterRage > 80.0f ? Colors.Crimson : (sim.GlobalCommuterRage > 50.0f ? Colors.Gold : Colors.MediumSpringGreen);
        DrawRect(new Rect2(barX, barY, barW * (sim.GlobalCommuterRage / 100.0f), barH), rCol, true);
        DrawText($"{sim.GlobalCommuterRage:F1}%", barX + barW + 15, 55, 13, rCol);

        if (!string.IsNullOrEmpty(_crisisAlert1)) DrawText(_crisisAlert1, barX + barW + 80, 25, 12, _crisisColor1);
        if (!string.IsNullOrEmpty(_crisisAlert2)) DrawText(_crisisAlert2, barX + barW + 80, 50, 11, _crisisColor2);

        // ===== VIEWPORT CANVAS =====
        float viewY = H * 0.18f + 3;
        float viewH = H * 0.72f - 6;

        switch (sim.ActivePerspective)
        {
            case Perspective.PLATFORM:
                DrawPlatformGraphics(0, viewY, W, viewH, sim);
                break;
            case Perspective.UNDER_STATION:
                DrawUnderStationGraphics(0, viewY, W, viewH, sim);
                break;
            case Perspective.INSIDE_CARS:
                DrawInsideCarsGraphics(0, viewY, W, viewH, sim);
                break;
        }

        // Notification banner
        float bannerY = H - 32;
        DrawRect(new Rect2(0, bannerY, W, 32), _notificationBg, true);
        DrawText(_notificationText, W / 2, bannerY + 20, 11, Colors.White, HorizontalAlignment.Center);

        // Riot overlay
        if (sim.RiotErupted)
        {
            DrawRect(new Rect2(0, 0, W, H), Color.Color8(0, 0, 0, 170), true);
            float panelW = 680f;
            float panelH = 240f;
            Vector2 pTL = new Vector2(W / 2 - panelW / 2, H / 2 - panelH / 2);

            DrawRect(new Rect2(pTL.X, pTL.Y, panelW, panelH), Colors.DarkRed, true);
            DrawRect(new Rect2(pTL.X, pTL.Y, panelW, panelH), Colors.Gold, false, 4f);

            DrawText("👮 !!! STATION RIOT TRIGGERED! !!! 👮", W / 2, pTL.Y + 45, 23, Colors.Yellow, HorizontalAlignment.Center);
            DrawText($"Transported: {sim.TotalPassengersTransported} passengers | Fares earned: ${sim.TotalFareRevenue:F0}", W / 2, pTL.Y + 95, 15, Colors.White, HorizontalAlignment.Center);
            DrawText("Rage hit 100%. Your shift coordinator contract is terminated.", W / 2, pTL.Y + 130, 13, Colors.LightGray, HorizontalAlignment.Center);
            DrawText("Press [ R ] to restart", W / 2, pTL.Y + 195, 15, Colors.Yellow, HorizontalAlignment.Center);
        }
    }

    private void DrawPlatformGraphics(float x, float y, float w, float h, SimulationManager sim)
    {
        DrawRect(new Rect2(x, y, w, h), Color.Color8(50, 55, 62), true);
        DrawRect(new Rect2(x, y, w, h), Color.Color8(120, 125, 130), false, 4f);

        float step = w / 4.0f;
        for (int i = 1; i < 4; i++)
        {
            float beamX = x + i * step;
            DrawRect(new Rect2(beamX - 10, y + 2, 20, 48), Color.Color8(30, 32, 38), true);
            DrawRect(new Rect2(beamX - 10, y + 50, 20, 5), Color.Color8(90, 95, 100), true);
        }

        float trackY = y + 75;
        DrawRect(new Rect2(x, trackY, w, 24), Color.Color8(18, 18, 18), true);
        DrawLine(new Vector2(x, trackY + 4), new Vector2(x + w, trackY + 4), Color.Color8(110, 115, 120), 2);
        DrawLine(new Vector2(x, trackY + 20), new Vector2(x + w, trackY + 20), Color.Color8(110, 115, 120), 2);
        
        float cautionY = trackY + 26;
        for (float cx = x; cx < x + w; cx += 25)
            DrawRect(new Rect2(cx, cautionY, 15, 6), Colors.Yellow, true);

        float trainOffset = (_frameCount * 3) % (w + 400) - 200;
        DrawRect(new Rect2(x + trainOffset, trackY - 15, 400, 50), Color.Color8(0, 80, 180), true);
        DrawRect(new Rect2(x + trainOffset, trackY - 15, 400, 50), Colors.White, false, 2f);
        DrawRect(new Rect2(x + trainOffset + 365, trackY - 8, 30, 22), Color.Color8(20, 20, 20), true);

        if (sim.FanActiveOnPlatform)
        {
            for (float fx = x + 50; fx < x + w - 50; fx += 120)
            {
                DrawCircle(new Vector2(fx, y + h - 80), 15f, Color.Color8(0, 200, 255, 120));
                DrawText("~≈~", fx - 12, y + h - 75, 10, Colors.Cyan);
            }
        }

        DrawText("CAMERA DECK 01:", x + 25, y + 25, 13, Colors.Yellow);
        Color dColor = sim.TrainDelayTimer > 15.0f ? Colors.Crimson : Colors.LightGreen;
        DrawText($"Delay:{sim.TrainDelayTimer:F1}s  Thieves:{sim.PickpocketCount}  QueueViol:{(sim.PriorityQueueViolationRate * 100f):F0}%", x + 25, y + 50, 11, dColor);

        DrawText("[D]Train [P]Bust [Q]Queue [F]Fan [G]Guard [E]Push", x + 20, y + h - 22, 10, Colors.LightSkyBlue);
    }

    private void DrawUnderStationGraphics(float x, float y, float w, float h, SimulationManager sim)
    {
        DrawRect(new Rect2(x, y, w, h), Color.Color8(30, 52, 60), true);
        DrawRect(new Rect2(x, y, w, h), Color.Color8(80, 160, 180), false, 4f);

        for (int dx = 120; dx < w - 50; dx += 260)
            DrawRect(new Rect2(dx, y + 10, 48, h - 20), Color.Color8(22, 38, 44), true);

        float tvmStartY = y + 45;
        for (int k = 0; k < 4; k++)
        {
            float tvmX = x + 35 + (k * 105);
            DrawRect(new Rect2(tvmX, tvmStartY, 72, 100), Color.Color8(12, 15, 20), true);
            DrawRect(new Rect2(tvmX, tvmStartY, 72, 100), Colors.DeepSkyBlue, false, 2f);
            DrawRect(new Rect2(tvmX + 12, tvmStartY + 12, 48, 38), Color.Color8(40, 40, 40), true);

            if (k < sim.TicketMachineFailures)
            {
                Color failCol = (_frameCount % 10 < 5) ? Colors.Crimson : Colors.DarkRed;
                DrawRect(new Rect2(tvmX + 12, tvmStartY + 12, 48, 38), failCol, true);
                DrawText("FAIL", tvmX + 36, tvmStartY + 30, 11, Colors.White, HorizontalAlignment.Center);
            }
            else
            {
                DrawText("INSERT", tvmX + 36, tvmStartY + 30, 8, Colors.SpringGreen, HorizontalAlignment.Center);
            }
        }

        float escX = w - 190;
        float escY = y + 25;
        DrawRect(new Rect2(escX, escY, 170, h - 50), Color.Color8(15, 25, 30), true);
        DrawRect(new Rect2(escX, escY, 170, h - 50), Colors.Yellow, false, 3f);
        DrawText("ESCALATOR", escX + 85, escY + 22, 11, Colors.LightGoldenrod, HorizontalAlignment.Center);

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

        int frozenCount = 0;
        foreach (var agent in sim.Commuters)
            if (agent.IsFrozen && agent.CurrentPerspective == Perspective.UNDER_STATION) frozenCount++;

        DrawText("CONCOURSE HUD:", x + 25, y + 22, 13, Colors.Yellow);
        Color tColor = sim.TicketMachineFailures > 0 ? Colors.Crimson : Colors.White;
        DrawText($"Machines Down: {sim.TicketMachineFailures}/4", x + 25, y + 175, 12, tColor);
        if (frozenCount > 0)
        {
            Color frzCol = (_frameCount % 8 < 4) ? Colors.DarkGoldenrod : Colors.Yellow;
            DrawText($"Stuck: {frozenCount} commuters!", x + 200, y + 175, 12, frzCol);
        }
        Color sCol = sim.EscalatorWeightStrain > 75.0f ? Colors.Crimson : Colors.LightGreen;
        DrawText($"Escalator: {sim.EscalatorWeightStrain:F1}%", x + 25, y + 198, 12, sCol);

        DrawText("[G]Guard(fix machine -$300) | [S]Escalator(-$40)", x + 20, y + h - 22, 10, Colors.LightSkyBlue);
    }

    private void DrawInsideCarsGraphics(float x, float y, float w, float h, SimulationManager sim)
    {
        DrawRect(new Rect2(x, y, w, h), Color.Color8(12, 35, 80), true);
        DrawRect(new Rect2(x, y, w, h), Color.Color8(40, 100, 200), false, 4f);

        float winW = 145f;
        float winH = 80f;
        float startX = x + 35f;
        float winY = y + 36f;
        int blockOffset = (_frameCount * 4) % 250;

        for (int i = 0; i < 3; i++)
        {
            float curWinX = startX + i * 190;
            DrawRect(new Rect2(curWinX, winY, winW, winH), Color.Color8(1, 15, 30), true);
            float pX = curWinX + 30 + ((blockOffset) % 110);
            if (pX < curWinX + winW - 15)
                DrawRect(new Rect2(pX, winY, 15, winH), Color.Color8(25, 27, 30), true);
            DrawRect(new Rect2(curWinX, winY, winW, winH), Colors.Cyan, false, 3f);
        }

        DrawRect(new Rect2(x + 40, y + 145, w - 80, 28), Color.Color8(30, 80, 150), true);
        DrawRect(new Rect2(x + 40, y + 145, w - 80, 28), Colors.Cyan, false, 2f);
        DrawText("[ Seat Row A ]", w / 2, y + 163, 11, Colors.LightBlue, HorizontalAlignment.Center);

        if (sim.FanActiveOnCars)
        {
            for (float fx = x + 50; fx < x + w - 50; fx += 100)
            {
                DrawCircle(new Vector2(fx, y + 130), 12f, Color.Color8(0, 200, 255, 100));
                DrawText("~≈~", fx - 12, y + 135, 10, Colors.Cyan);
            }
        }

        DrawText("CARRIAGE STATUS:", x + 25, y + 15, 13, Colors.Yellow);
        Color dc = sim.CarCrowdDensity > 8.0f ? Colors.Crimson : Colors.LightGreen;
        DrawText($"Density:{sim.CarCrowdDensity:F1}/10 | AC:{(sim.ACFailed ? "38C FAIL" : "21C OK")}", x + w - 380, y + 15, 12, dc);

        if (sim.ACFailed)
        {
            Color acFlash = (_frameCount % 6 < 3) ? Colors.Crimson : Colors.DarkRed;
            float warnW = 280;
            float warnH = 45;
            float warnX = x + 25;
            float warnY2 = y + 195;
            DrawRect(new Rect2(warnX, warnY2, warnW, warnH), acFlash, true);
            DrawRect(new Rect2(warnX, warnY2, warnW, warnH), Colors.Yellow, false, 2f);
            DrawText("⚠️ AC COMPRESSOR FAILURE ⚠️", warnX + warnW / 2, warnY2 + 20, 12, Colors.Yellow, HorizontalAlignment.Center);
            DrawText("TEMP CRITICAL!", warnX + warnW / 2, warnY2 + 36, 10, Colors.White, HorizontalAlignment.Center);
        }

        DrawText("[A]Fix AC(-$150) [F]Fan(-$150) [G]Guard(-$300)", x + 20, y + h - 22, 10, Colors.LightSkyBlue);
    }

    private void DrawText(string text, float x, float y, int size, Color color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        if (_font == null) return;
        DrawString(_font, new Vector2(x, y), text, alignment, -1, size, color);
    }

    public override void _ExitTree()
    {
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.OnFlashNotification -= FlashNotification;
        }
    }
}