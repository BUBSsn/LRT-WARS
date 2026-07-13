using Godot;
using System;
using System.Collections.Generic;

public enum Perspective
{
    PLATFORM,
    UNDER_STATION,
    INSIDE_CARS
}

public partial class SimulationManager : Node
{
    public static SimulationManager Instance { get; private set; }

    public Node2D ConcourseScreen { get; set; }
    public Node2D PlatformScreen { get; set; }
    public Node2D TrainScreen { get; set; }

    private Perspective _activePerspective = Perspective.PLATFORM;
    public Perspective ActivePerspective
    {
        get => _activePerspective;
        set
        {
            _activePerspective = value;
            UpdateScreenVisibilities();
        }
    }
    
    // Core game state
    public float GlobalCommuterRage { get; set; } = 0.0f;
    public float DailyBudget { get; set; } = 5000.0f;
    public bool RiotErupted { get; set; } = false;
    public float ShiftTimer { get; set; } = 0.0f;
    
    // Platform Metrics
    public float PlatformDensity { get; set; } = 0.0f;
    public float TrainDelayTimer { get; set; } = 0.0f;
    public int PickpocketCount { get; set; } = 0;
    public float PriorityQueueViolationRate { get; set; } = 0.05f;

    // Under-Station Metrics
    public int TicketMachineFailures { get; set; } = 0;
    public float EscalatorWeightStrain { get; set; } = 10.0f;

    // Inside-Cars Metrics
    public float CarCrowdDensity { get; set; } = 2.0f;
    public float ACFailureChance { get; set; } = 0.05f;
    public bool ACFailed { get; set; } = false;

    public PackedScene AgentScene { get; set; }

    // Agents
    public List<GodotCommuterAgent> Commuters { get; } = new List<GodotCommuterAgent>();
    private float _spawnerTimer = 0.0f;
    private Random _random = new Random(512);

    // Crisis timers
    public float TicketMachineBreakTimer { get; set; } = 15.0f;
    public float ACBreakdownTimer { get; set; } = 25.0f;
    public float PickpocketSpawnTimer { get; set; } = 12.0f;

    // Tactical interventions
    public float FanCooldownTimer { get; set; } = 0.0f;
    public bool FanActiveOnPlatform { get; set; } = false;
    public bool FanActiveOnCars { get; set; } = false;

    // Revenue
    public int TotalPassengersTransported { get; set; } = 0;
    public float TotalFareRevenue { get; set; } = 0.0f;

    private float _viewportW = 800f;
    private float _viewportH = 600f;

    // For sending alerts to the UI
    public Action<string, Color> OnFlashNotification;

    public override void _Ready()
    {
        Instance = this;
        
        AgentScene = GD.Load<PackedScene>("res://GodotCommuterAgent.tscn");
        
        // Register InputMap dynamically
        BindInput("view_1", Key.Key1, Key.Kp1);
        BindInput("view_2", Key.Key2, Key.Kp2);
        BindInput("view_3", Key.Key3, Key.Kp3);
        BindInput("action_d", Key.D);
        BindInput("action_f", Key.F);
        BindInput("action_g", Key.G);
        BindInput("action_e", Key.E);
        BindInput("action_p", Key.P);
        BindInput("action_q", Key.Q);
        BindInput("action_s", Key.S);
        BindInput("action_a", Key.A);
        
        TicketMachineBreakTimer = 15.0f + (float)_random.NextDouble() * 5.0f;
        CallDeferred(nameof(UpdateScreenVisibilities));
    }

    private void BindInput(string actionName, Key key1, Key? key2 = null)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }
        
        var evt1 = new InputEventKey();
        evt1.Keycode = key1;
        InputMap.ActionAddEvent(actionName, evt1);

        if (key2.HasValue)
        {
            var evt2 = new InputEventKey();
            evt2.Keycode = key2.Value;
            InputMap.ActionAddEvent(actionName, evt2);
        }
    }
    
    public void ResetSimulation()
    {
        GlobalCommuterRage = 0.0f;
        DailyBudget = 5000.0f;
        ActivePerspective = Perspective.PLATFORM;
        RiotErupted = false;
        ShiftTimer = 0.0f;

        PlatformDensity = 0.5f;
        TrainDelayTimer = 0.0f;
        PickpocketCount = 0;
        PriorityQueueViolationRate = 0.05f;

        TicketMachineFailures = 0;
        EscalatorWeightStrain = 10.0f;

        CarCrowdDensity = 2.0f;
        ACFailureChance = 0.05f;
        ACFailed = false;

        foreach (var c in Commuters)
        {
            c.QueueFree();
        }
        Commuters.Clear();
        _spawnerTimer = 0.0f;

        TicketMachineBreakTimer = 15.0f + (float)_random.NextDouble() * 5.0f;
        ACBreakdownTimer = 25.0f;
        PickpocketSpawnTimer = 12.0f;
        FanCooldownTimer = 0.0f;
        FanActiveOnPlatform = false;
        FanActiveOnCars = false;
        TotalPassengersTransported = 0;
        TotalFareRevenue = 0.0f;
        
        OnFlashNotification?.Invoke("SWITCHBOARD ONLINE! [1]Platform [2]Under-Station [3]Cars", Colors.DarkCyan);
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        
        if (RiotErupted) return;

        UpdateViewportBounds();
        
        ProcessSpawning(d);
        UpdateMetrics(d);
        ProcessCrisisCycles(d);
        
        // Remove freed instances if any
        Commuters.RemoveAll(c => !IsInstanceValid(c));
    }

    private void UpdateViewportBounds()
    {
        Vector2 scaleSize = GetViewport().GetVisibleRect().Size;
        _viewportW = scaleSize.X;
        _viewportH = scaleSize.Y;
    }

    private void ProcessSpawning(float delta)
    {
        float viewY = _viewportH * 0.18f + 3;
        float viewH = _viewportH * 0.72f - 6;

        _spawnerTimer -= delta;
        if (_spawnerTimer <= 0.0f)
        {
            if (AgentScene != null)
            {
                var newAgent = AgentScene.Instantiate<GodotCommuterAgent>();
                newAgent.Position = new Vector2(25f, viewY + viewH * 0.5f + _random.Next(-40, 40));
                newAgent.CurrentPerspective = Perspective.UNDER_STATION;
                newAgent.MovementSpeed = 80.0f + (float)_random.NextDouble() * 70.0f;
                newAgent.IndividualRage = 0.0f;
                newAgent.IsPriority = _random.NextDouble() < 0.15;
                newAgent.TargetPosition = new Vector2(_viewportW - 190, viewY + viewH * 0.5f + _random.Next(-30, 30));
                
                AddChild(newAgent);
                Commuters.Add(newAgent);
            }
            
            _spawnerTimer = 1.5f + (float)_random.NextDouble() * 1.5f;
        }
    }

    private void UpdateMetrics(float delta)
    {
        int underStationCount = 0;
        int platformCount = 0;
        int insideCarsCount = 0;
        
        foreach (var agent in Commuters)
        {
            if (agent.CurrentPerspective == Perspective.UNDER_STATION) underStationCount++;
            else if (agent.CurrentPerspective == Perspective.PLATFORM) platformCount++;
            else if (agent.CurrentPerspective == Perspective.INSIDE_CARS) insideCarsCount++;
        }

        PlatformDensity = Mathf.Min(10.0f, 0.5f + platformCount * 0.35f);
        CarCrowdDensity = Mathf.Min(10.0f, 0.5f + insideCarsCount * 0.35f);

        bool underStationOverloaded = underStationCount > 10;
        bool platformOverloaded = PlatformDensity > 5.0f;
        bool insideCarsOverloaded = CarCrowdDensity > 7.5f;

        float viewY = _viewportH * 0.18f + 3;
        float viewH = _viewportH * 0.72f - 6;

        foreach (var agent in Commuters)
        {
            if (agent.IsFrozen)
            {
                if (TicketMachineFailures == 0) agent.IsFrozen = false;
                continue;
            }

            float multiplier = 1.0f;
            if (agent.CurrentPerspective == Perspective.UNDER_STATION && (underStationOverloaded || TicketMachineFailures > 0))
                multiplier = 0.30f;
            else if (agent.CurrentPerspective == Perspective.PLATFORM && platformOverloaded)
                multiplier = 0.30f;
            else if (agent.CurrentPerspective == Perspective.INSIDE_CARS && insideCarsOverloaded)
                multiplier = 0.30f;
                
            agent.TargetMultiplier = multiplier;

            Vector2 dir = agent.TargetPosition - agent.Position;
            if (dir.Length() <= 8.0f)
            {
                if (agent.CurrentPerspective == Perspective.UNDER_STATION)
                {
                    agent.CurrentPerspective = Perspective.PLATFORM;
                    int targetX = 50 + _random.Next((int)_viewportW - 150);
                    int targetY = (int)(viewY + 115);
                    agent.Position = new Vector2(targetX, viewY + viewH - 45);
                    agent.TargetPosition = new Vector2(targetX, targetY);
                }
            }
        }

        TrainDelayTimer += delta;
        ShiftTimer += delta;
        PriorityQueueViolationRate = Math.Min(1.0f, PriorityQueueViolationRate + 0.015f * delta);
        EscalatorWeightStrain = Math.Min(100.0f, EscalatorWeightStrain + (2.5f + TicketMachineFailures * 1.5f) * delta);

        // Rage calculation
        float rageAddition = 0.0f;
        if (TrainDelayTimer > 15.0f)
        {
            float excess = TrainDelayTimer - 15.0f;
            float r = Mathf.Exp(excess * 0.15f) * 0.35f * delta;
            if (FanActiveOnPlatform) r *= 0.5f;
            rageAddition += r;
        }
        if (EscalatorWeightStrain > 75.0f)
        {
            float excess = EscalatorWeightStrain - 75.0f;
            rageAddition += Mathf.Exp(excess * 0.09f) * 0.45f * delta;
        }
        if (CarCrowdDensity > 8.0f)
        {
            float excess = CarCrowdDensity - 8.0f;
            float r = Mathf.Exp(excess * 0.5f) * 0.65f * delta;
            if (FanActiveOnCars) r *= 0.5f;
            rageAddition += r;
        }
        rageAddition += PickpocketCount * 0.25f * delta;
        rageAddition += TicketMachineFailures * 0.4f * delta;

        GlobalCommuterRage += rageAddition;

        if (TrainDelayTimer <= 15.0f && EscalatorWeightStrain <= 75.0f
            && CarCrowdDensity <= 8.0f && !ACFailed && PickpocketCount == 0)
        {
            GlobalCommuterRage = Math.Max(0.0f, GlobalCommuterRage - 1.5f * delta);
        }

        if (GlobalCommuterRage >= 100.0f) 
        {
            GlobalCommuterRage = 100.0f;
            RiotErupted = true;
        }

        DailyBudget = Math.Max(0.0f, DailyBudget - 8.0f * delta);
    }

    private void ProcessCrisisCycles(float delta)
    {
        // Ticket Machine
        TicketMachineBreakTimer -= delta;
        if (TicketMachineBreakTimer <= 0.0f && TicketMachineFailures < 6)
        {
            TicketMachineFailures++;
            TicketMachineBreakTimer = 15.0f + (float)_random.NextDouble() * 5.0f;
            int frozenCount = 0;
            foreach (var agent in Commuters)
            {
                if (agent.CurrentPerspective == Perspective.UNDER_STATION && !agent.IsFrozen && frozenCount < 3)
                {
                    agent.IsFrozen = true;
                    frozenCount++;
                }
            }
            OnFlashNotification?.Invoke($"⚠️ TICKET MACHINE #{TicketMachineFailures} DOWN! {frozenCount} stuck!", Colors.Crimson);
        }

        // Pickpockets
        PickpocketSpawnTimer -= delta;
        if (PickpocketSpawnTimer <= 0.0f)
        {
            PickpocketSpawnTimer = 10.0f + (float)_random.NextDouble() * 5.0f;
            List<int> candidates = new List<int>();
            for (int i = 0; i < Commuters.Count; i++)
            {
                var a = Commuters[i];
                if (a.CurrentPerspective == Perspective.PLATFORM && !a.IsPriority && !a.IsPickpocket)
                    candidates.Add(i);
            }
            if (candidates.Count > 0)
            {
                int idx = candidates[_random.Next(candidates.Count)];
                Commuters[idx].IsPickpocket = true;
                PickpocketCount++;
            }
        }

        for (int i = 0; i < Commuters.Count; i++)
        {
            var thief = Commuters[i];
            if (!thief.IsPickpocket || thief.CurrentPerspective != Perspective.PLATFORM) continue;
            for (int j = 0; j < Commuters.Count; j++)
            {
                if (i == j) continue;
                var victim = Commuters[j];
                if (victim.CurrentPerspective != Perspective.PLATFORM) continue;
                if (thief.Position.DistanceTo(victim.Position) < 30.0f)
                {
                    GlobalCommuterRage = Math.Min(100.0f, GlobalCommuterRage + 10.0f * delta);
                    victim.IndividualRage += 5.0f * delta;
                    break;
                }
            }
        }

        // AC Failure
        if (!ACFailed)
        {
            ACBreakdownTimer -= delta;
            if (ACBreakdownTimer <= 0.0f)
            {
                ACFailed = true;
                ACBreakdownTimer = 25.0f;
                OnFlashNotification?.Invoke("🔥 AC COMPRESSOR FAILURE! Temps rising!", Colors.Crimson);
            }
        }
        else
        {
            int insideCarsCount = 0;
            foreach (var a in Commuters) if (a.CurrentPerspective == Perspective.INSIDE_CARS) insideCarsCount++;
            float acRage = 1.5f * insideCarsCount * delta;
            GlobalCommuterRage = Math.Min(100.0f, GlobalCommuterRage + acRage);
        }

        // Fans
        if (FanCooldownTimer > 0.0f)
        {
            FanCooldownTimer -= delta;
            if (FanCooldownTimer <= 0.0f)
            {
                FanActiveOnPlatform = false;
                FanActiveOnCars = false;
                FanCooldownTimer = 0.0f;
            }
        }
    }

    private void ResolveScreenReferences()
    {
        if (ConcourseScreen == null || !IsInstanceValid(ConcourseScreen))
        {
            ConcourseScreen = GetTree().Root.FindChild("Concourse_Screen", true, false) as Node2D;
        }
        if (PlatformScreen == null || !IsInstanceValid(PlatformScreen))
        {
            PlatformScreen = GetTree().Root.FindChild("Platform_Screen", true, false) as Node2D;
        }
        if (TrainScreen == null || !IsInstanceValid(TrainScreen))
        {
            TrainScreen = GetTree().Root.FindChild("Train_Screen", true, false) as Node2D;
        }
    }

    public void UpdateScreenVisibilities()
    {
        ResolveScreenReferences();
        if (ConcourseScreen != null) ConcourseScreen.Visible = (_activePerspective == Perspective.UNDER_STATION);
        if (PlatformScreen != null) PlatformScreen.Visible = (_activePerspective == Perspective.PLATFORM);
        if (TrainScreen != null) TrainScreen.Visible = (_activePerspective == Perspective.INSIDE_CARS);

        foreach (var agent in Commuters)
        {
            if (IsInstanceValid(agent))
            {
                agent.Visible = (agent.CurrentPerspective == _activePerspective);
            }
        }
    }
}
