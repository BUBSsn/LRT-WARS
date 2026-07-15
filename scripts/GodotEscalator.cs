using Godot;
using System;
using System.Collections.Generic;

public partial class GodotEscalator : Node2D
{
    private HashSet<GodotCommuterAgent> _riders = new HashSet<GodotCommuterAgent>();
    private float _overloadTimer = 0.0f;
    private bool _isBroken = false;
    private float _speedMultiplier = 1.0f;

    [Export]
    public Area2D DetectionArea { get; set; }

    [Export]
    public float NormalSpeed { get; set; } = 100.0f;

    public bool IsBroken => _isBroken;
    public float SpeedMultiplier => _speedMultiplier;
    public float OverloadTimer => _overloadTimer;
    public int ActiveRiderCount => GetActiveRiderCountCombined();

    public override void _Ready()
    {
        // Programmatic compose standard nodes as fallback, keeping C# logic based
        var bg = GetNodeOrNull<ColorRect>("Background");
        if (bg == null)
        {
            bg = new ColorRect {
                Name = "Background",
                Size = new Vector2(120.0f, 400.0f),
                Color = Colors.SlateGray
            };
            AddChild(bg);
        }

        var statusLabel = GetNodeOrNull<Label>("StatusLabel");
        if (statusLabel == null)
        {
            statusLabel = new Label {
                Name = "StatusLabel",
                Position = new Vector2(10.0f, 10.0f),
                Text = "Escalator OK"
            };
            AddChild(statusLabel);
        }

        if (DetectionArea == null)
        {
            DetectionArea = FindChild("DetectionArea", true, false) as Area2D;
        }

        if (DetectionArea != null)
        {
            DetectionArea.AreaEntered += OnAreaEntered;
            DetectionArea.AreaExited += OnAreaExited;
            DetectionArea.BodyEntered += OnBodyEntered;
            DetectionArea.BodyExited += OnBodyExited;
        }

        var minigame = GetMinigameRef();
        if (minigame != null)
        {
            minigame.WireMinigameCompleted += OnMinigameCompleted;
        }
        else
        {
            CallDeferred(nameof(DelayedMinigameConnection));
        }

        ResetEscalator();
    }

    private void DelayedMinigameConnection()
    {
        var minigame = GetMinigameRef();
        if (minigame != null)
        {
            minigame.WireMinigameCompleted += OnMinigameCompleted;
        }
    }

    public void ResetEscalator()
    {
        _riders.Clear();
        _overloadTimer = 0.0f;
        _isBroken = false;
        _speedMultiplier = 1.0f;
        SelfModulate = Colors.White;
        Modulate = Colors.White;
        UpdateStatusDisplay();
    }

    private int GetActiveRiderCountCombined()
    {
        _riders.RemoveWhere(agent => !IsInstanceValid(agent));
        int countFromArea = _riders.Count;

        int countFromState = 0;
        var sim = SimulationManager.Instance;
        if (sim != null)
        {
            foreach (var passenger in sim.Passengers)
            {
                if (IsInstanceValid(passenger) && 
                    passenger.CurrentPerspective == Perspective.UNDER_STATION && 
                    passenger.CurrentConcourseState == GodotCommuterAgent.ConcourseState.RidingEscalator)
                {
                    countFromState++;
                }
            }
        }

        return Math.Max(countFromArea, countFromState);
    }

    public override void _Process(double delta)
    {
        int ridersCount = ActiveRiderCount;

        if (_isBroken)
        {
            _speedMultiplier = 0.0f;
            SelfModulate = Colors.Red;

            var sim = SimulationManager.Instance;
            if (sim != null)
            {
                foreach (var passenger in sim.Passengers)
                {
                    if (IsInstanceValid(passenger) && passenger.CurrentPerspective == Perspective.UNDER_STATION)
                    {
                        if (passenger.CurrentConcourseState == GodotCommuterAgent.ConcourseState.RidingEscalator ||
                            passenger.CurrentConcourseState == GodotCommuterAgent.ConcourseState.WalkingToEscalator)
                        {
                            passenger.IndividualRage = Math.Min(100.0f, passenger.IndividualRage + 2.5f * (float)delta);
                            sim.BalanceMeter = Math.Max(0.0f, sim.BalanceMeter - 0.1f * (float)delta);
                        }
                    }
                }
            }
            UpdateStatusDisplay();
            return;
        }

        if (ridersCount >= 20)
        {
            _speedMultiplier = Math.Clamp(1.0f - (ridersCount - 20) * 0.05f, 0.2f, 1.0f);
            _overloadTimer += (float)delta;
            SelfModulate = Colors.Orange;

            if (_overloadTimer >= 200.0f)
            {
                _isBroken = true;
                _speedMultiplier = 0.0f;
                var sim = SimulationManager.Instance;
                sim?.OnFlashNotification?.Invoke("⚠️ ESCALATOR SURCHARGED AND BROKE DOWN!", Colors.Red);
            }
        }
        else
        {
            _speedMultiplier = 1.0f;
            _overloadTimer = Math.Max(0.0f, _overloadTimer - (float)delta * 0.5f);
            SelfModulate = Colors.White;
        }

        UpdateStatusDisplay();
    }

    public void OnClickTriggered()
    {
        if (_isBroken)
        {
            var minigame = GetMinigameRef();
            if (minigame != null)
            {
                minigame.Visible = true;
                minigame.ResetMinigame();
                SimulationManager.Instance?.OnFlashNotification?.Invoke("🔧 INITIATING REPAIRS - MATCH THE WIRES!", Colors.Yellow);
            }
        }
        else if (ActiveRiderCount >= 20)
        {
            var sim = SimulationManager.Instance;
            if (sim != null && sim.Budget >= 40.0f)
            {
                sim.Budget -= 40.0f;
                _overloadTimer = 0.0f;
                sim.OnFlashNotification?.Invoke("🔄 ESCALATOR LOAD BALANCED (-$40)", Colors.Green);
            }
            else if (sim != null)
            {
                sim.OnFlashNotification?.Invoke("INSUFFICIENT FUNDS TO RESET LOAD!", Colors.Red);
            }
        }
    }

    private void OnMinigameCompleted()
    {
        _isBroken = false;
        _overloadTimer = 0.0f;
        _speedMultiplier = 1.0f;
        SelfModulate = Colors.White;
        SimulationManager.Instance?.OnFlashNotification?.Invoke("✅ ESCALATOR REPAIRED & OPERATIONAL!", Colors.Green);
    }

    private WireMinigame GetMinigameRef()
    {
        var root = GetTree().CurrentScene;
        if (root == null) return null;

        var minigame = root.FindChild("WireMinigame", true, false) as WireMinigame;
        return minigame;
    }

    private void UpdateStatusDisplay()
    {
        var statusLabel = GetNodeOrNull<Label>("StatusLabel");
        if (statusLabel != null)
        {
            if (_isBroken)
            {
                statusLabel.Text = "BROKEN!";
            }
            else if (ActiveRiderCount >= 20)
            {
                statusLabel.Text = $"OVERLOAD!\nTime: {200.0f - _overloadTimer:F0}s\nRiders: {ActiveRiderCount}";
            }
            else
            {
                statusLabel.Text = $"OK\nRiders: {ActiveRiderCount}";
            }
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        var parent = area.GetParent();
        if (parent is GodotCommuterAgent agent)
        {
            _riders.Add(agent);
        }
    }

    private void OnAreaExited(Area2D area)
    {
        var parent = area.GetParent();
        if (parent is GodotCommuterAgent agent)
        {
            _riders.Remove(agent);
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (body is GodotCommuterAgent agent)
        {
            _riders.Add(agent);
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body is GodotCommuterAgent agent)
        {
            _riders.Remove(agent);
        }
    }

    public override void _ExitTree()
    {
        var minigame = GetMinigameRef();
        if (minigame != null)
        {
            minigame.WireMinigameCompleted -= OnMinigameCompleted;
        }
    }
}
