using Godot;
using System;

public partial class GodotTVM : Node2D
{
    private int _usesLeft;
    private bool _isBroken = false;
    private static Random _random = new Random();

    [Export]
    public int RepairCost { get; set; } = 300;

    public bool IsBroken => _isBroken;

    public override void _Ready()
    {
        // Programmatic compose standard nodes as fallback, keeping C# logic based
        var bg = GetNodeOrNull<ColorRect>("Background");
        if (bg == null)
        {
            bg = new ColorRect {
                Name = "Background",
                Size = new Vector2(70.0f, 100.0f),
                Color = Colors.Teal
            };
            AddChild(bg);
        }

        var lblId = GetNodeOrNull<Label>("TvmIdLabel");
        if (lblId == null)
        {
            lblId = new Label {
                Name = "TvmIdLabel",
                Position = new Vector2(5.0f, 5.0f),
                Text = Name
            };
            AddChild(lblId);
        }

        var lblStatus = GetNodeOrNull<Label>("TvmStatusLabel");
        if (lblStatus == null)
        {
            lblStatus = new Label {
                Name = "TvmStatusLabel",
                Position = new Vector2(5.0f, 40.0f),
                Text = "Uses: --"
            };
            AddChild(lblStatus);
        }

        ResetMachine();
    }

    public void ResetMachine()
    {
        _usesLeft = _random.Next(40, 51); // 40 to 50 inclusive
        _isBroken = false;
        SelfModulate = Colors.White;
        Modulate = Colors.White;
        var bg = GetNodeOrNull<ColorRect>("Background");
        if (bg != null)
        {
            bg.Color = new Color(0.0f, 0.0f, 0.0f, 0.0f); // transparent
        }
        UpdateStatusLabel();
    }

    public bool TryBuyTicket()
    {
        if (_isBroken)
        {
            return false;
        }

        _usesLeft--;
        if (_usesLeft <= 0)
        {
            _isBroken = true;
            SelfModulate = Colors.Red;
            var bg = GetNodeOrNull<ColorRect>("Background");
            if (bg != null)
            {
                bg.Color = new Color(1.0f, 0.0f, 0.0f, 0.4f); // semi-transparent red overlay
            }
        }
        UpdateStatusLabel();
        return true;
    }

    public bool Repair()
    {
        var sim = SimulationManager.Instance;
        if (sim != null && sim.Budget >= RepairCost)
        {
            sim.Budget -= RepairCost;
            ResetMachine();
            sim.OnFlashNotification?.Invoke($"REPAIRED TICKET MACHINE (-${RepairCost})", Colors.Green);
            return true;
        }
        else if (sim != null)
        {
            sim.OnFlashNotification?.Invoke("INSUFFICIENT FUNDS TO REPAIR TICKET MACHINE!", Colors.Red);
        }
        return false;
    }

    private void UpdateStatusLabel()
    {
        var lblStatus = GetNodeOrNull<Label>("TvmStatusLabel");
        if (lblStatus != null)
        {
            lblStatus.Text = _isBroken ? "BROKEN" : $"Uses: {_usesLeft}";
        }
    }
}
