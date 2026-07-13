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

    private ColorRect _visualPlaceholder;
    private Label _rageLabel;

    public override void _Ready()
    {
        // Setup Visuals
        _visualPlaceholder = new ColorRect();
        _visualPlaceholder.Size = new Vector2(14, 14);
        _visualPlaceholder.Position = new Vector2(-7, -7); // Center the rect around the position
        _visualPlaceholder.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_visualPlaceholder);
        
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_visualPlaceholder == null) return;

        Color bodyColor;

        if (IsPickpocket)
        {
            bodyColor = Colors.Red; 
            _visualPlaceholder.Size = new Vector2(18, 18);
            _visualPlaceholder.Position = new Vector2(-9, -9);
        }
        else if (IsFrozen)
        {
            bodyColor = Colors.DarkGoldenrod;
            _visualPlaceholder.Size = new Vector2(14, 14);
            _visualPlaceholder.Position = new Vector2(-7, -7);
        }
        else if (IsPriority)
        {
            bodyColor = Colors.HotPink;
            _visualPlaceholder.Size = new Vector2(14, 14);
            _visualPlaceholder.Position = new Vector2(-7, -7);
        }
        else
        {
            bodyColor = Colors.DeepSkyBlue;
            _visualPlaceholder.Size = new Vector2(14, 14);
            _visualPlaceholder.Position = new Vector2(-7, -7);
        }

        // Apply rage tinting manually if needed
        if (IndividualRage > 25.0f && !IsPickpocket)
        {
            bodyColor = Colors.DarkRed;
        }
        else if (IndividualRage > 10.0f && !IsPickpocket)
        {
            bodyColor = Colors.Orange;
        }

        _visualPlaceholder.Color = bodyColor;
    }

    public override void _Process(double delta)
    {
        UpdateVisuals(); // In case rage changed from elsewhere

        if (IsFrozen) return;

        Vector2 dir = TargetPosition - GlobalPosition;
        float dist = dir.Length();
        
        if (dist > 8.0f)
        {
            Vector2 norm = dir.Normalized();
            GlobalPosition += norm * MovementSpeed * TargetMultiplier * (float)delta;
        }
    }
}
