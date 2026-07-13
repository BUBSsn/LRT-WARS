using Godot;
using System;

public partial class GodotCommuterAgent : Node2D
{
    public Perspective CurrentPerspective { get; set; } = Perspective.UNDER_STATION;
    public float MovementSpeed { get; set; } = 80.0f;
    public float IndividualRage { get; set; } = 0.0f;
    
    private bool _isPriority = false;
    public bool IsPriority 
    { 
        get => _isPriority; 
        set 
        {
            _isPriority = value;
            UpdateVisuals();
        }
    }
    
    private bool _isPickpocket = false;
    public bool IsPickpocket 
    { 
        get => _isPickpocket;
        set
        {
            _isPickpocket = value;
            UpdateVisuals();
        }
    }
    
    private bool _isFrozen = false;
    public bool IsFrozen 
    { 
        get => _isFrozen;
        set
        {
            _isFrozen = value;
            UpdateVisuals();
        }
    }

    public Vector2 TargetPosition { get; set; }
    public float TargetMultiplier { get; set; } = 1.0f; // For density speed debuffs

    [Export] 
    public Sprite2D AgentSprite { get; set; }

    public override void _Ready()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (AgentSprite != null)
        {
            if (IsPriority)
            {
                // Tint priority passenger sprite pink
                AgentSprite.Modulate = Colors.HotPink;
            }
            else if (IsPickpocket)
            {
                AgentSprite.Modulate = Colors.Red;
            }
            else if (IsFrozen)
            {
                AgentSprite.Modulate = Colors.DarkGoldenrod;
            }
            else if (IndividualRage > 25.0f)
            {
                AgentSprite.Modulate = Colors.DarkRed;
            }
            else if (IndividualRage > 10.0f)
            {
                AgentSprite.Modulate = Colors.Orange;
            }
            else
            {
                AgentSprite.Modulate = Colors.White;
            }
        }
    }

    public override void _Process(double delta)
    {
        UpdateVisuals();

        if (IsFrozen) return;

        Vector2 dir = TargetPosition - Position;
        float dist = dir.Length();
        
        if (dist > 8.0f)
        {
            Vector2 norm = dir.Normalized();
            Position += norm * MovementSpeed * TargetMultiplier * (float)delta;
        }
    }
}
