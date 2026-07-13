#nullable enable
using Godot;
using System;
using System.Collections.Generic;

public partial class HomeworkWorkspace : Node2D
{
    // Window coordinate shakes
    private Vector2 _originalPosition;
    private Vector2 _screenOffset = Vector2.Zero;

    // HUD Notifications
    private string _notificationText = "SWAP VIEW CHANNELS USING HOTKEYS 1, 2, or 3.";
    private Color _notificationBg = Colors.DarkSlateGray;
    private int _notificationTicks = 60;

    private Random _random = new Random();

    public override void _Ready()
    {
        _originalPosition = Position;
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
            return;
        }
        
        HandleGameInputs(sim);

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
    }

    private void HandleGameInputs(SimulationManager sim)
    {
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
                        agent.Position = new Vector2(100f + _random.Next((int)scaleSize.X - 200), viewY + viewH - 40f);
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
                        if (agent.CurrentPerspective == Perspective.PLATFORM && agent.IsPriority && agent.Position.Y <= viewY + 125)
                        {
                            hasPriorityWaiting = true;
                            break;
                        }
                    }

                    for (int i = sim.Commuters.Count - 1; i >= 0; i--)
                    {
                        var agent = sim.Commuters[i];
                        if (agent.CurrentPerspective == Perspective.PLATFORM && agent.Position.Y <= viewY + 125)
                        {
                            if (!agent.IsPriority && hasPriorityWaiting && !priorityBoarded)
                                normalBoardedFirst = true;
                            if (agent.IsPriority) priorityBoarded = true;

                            agent.CurrentPerspective = Perspective.INSIDE_CARS;
                            agent.IsPickpocket = false;
                            agent.Position = new Vector2(100f + _random.Next((int)scaleSize.X - 200), viewY + viewH - 40f);
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
                        if (agent.CurrentPerspective == Perspective.INSIDE_CARS && agent.Position.Y <= viewY + 165f)
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

    public override void _ExitTree()
    {
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.OnFlashNotification -= FlashNotification;
        }
    }
}