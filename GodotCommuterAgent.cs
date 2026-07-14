using Godot;
using System;

public partial class GodotCommuterAgent : Node2D
{
    public int LaneIndex { get; set; } = -1;
    public int LaneSlotIndex { get; set; } = -1;
    public int DragSourceLaneIndex { get; set; } = -1;
    public int TargetLaneIndex { get; set; } = -1;
    public int SpawnOrder { get; set; } = 0;
    public bool IsTrainPassenger { get; set; } = true;
    public bool LegacyMovementEnabled { get; set; } = false;
    public bool IsDragging { get; set; } = false;
    public bool IsWalkingToLane { get; set; } = false;

    public Perspective CurrentPerspective { get; set; } = Perspective.UNDER_STATION;
    public float MovementSpeed { get; set; } = 240.0f;
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
    public Vector2 WalkTargetPosition { get; set; }
    public float TargetMultiplier { get; set; } = 1.0f;

    [Export]
    public Sprite2D AgentSprite { get; set; }

    [Export]
    public Vector2 AgentCustomScale { get; set; } = new Vector2(0.35f, 0.35f);

    [Export]
    public string[] SideSkins { get; set; } = new string[] 
    { 
        "res://walkingside3.png", 
        "res://Walkingside2.png", 
        "res://commuter_2_right.png" 
    };

    [Export]
    public string[] UpSkins { get; set; } = new string[] 
    { 
        "res://walk_up.png", 
        "res://walk_up.png", 
        "res://commuter_2_forward.png" 
    };

    private Texture2D _assignedSideTexture;
    private Texture2D _assignedUpTexture;
    private bool _skinDefaultFacingLeft = true;

    public override void _Ready()
    {
        Scale = AgentCustomScale;
        
        int skinCount = Math.Min(SideSkins?.Length ?? 0, UpSkins?.Length ?? 0);
        if (skinCount > 0)
        {
            var random = new Random();
            int skinIdx = random.Next(skinCount);
            
            _assignedSideTexture = GD.Load<Texture2D>(SideSkins[skinIdx]);
            _assignedUpTexture = GD.Load<Texture2D>(UpSkins[skinIdx]);

            string sidePath = SideSkins[skinIdx].ToLower();
            if (sidePath.Contains("commuter_2") || sidePath.Contains("right"))
            {
                _skinDefaultFacingLeft = false;
            }
            else
            {
                _skinDefaultFacingLeft = true;
            }
        }

        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null && _assignedSideTexture != null)
        {
            sprite.Texture = _assignedSideTexture;
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (AgentSprite == null)
        {
            return;
        }

        if (IsDragging)
        {
            AgentSprite.Modulate = Colors.LightYellow;
        }
        else if (IsPriority)
        {
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

    public override void _Process(double delta)
    {
        var sim = SimulationManager.Instance;
        if (sim != null && CurrentPerspective != sim.ActivePerspective)
        {
            Visible = false;
            return;
        }
        Visible = true;

        UpdateVisuals();

        bool isMoving = false;
        Vector2 velocity = Vector2.Zero;

        if (IsWalkingToLane && !IsDragging && !IsFrozen)
        {
            Vector2 oldPos = Position;
            UpdateLaneWalk((float)delta);
            isMoving = IsWalkingToLane;
            velocity = Position - oldPos;
        }
        else if (LegacyMovementEnabled && !IsFrozen && !IsDragging)
        {
            Vector2 dir = TargetPosition - Position;
            float dist = dir.Length();
            
            if (dist > 8.0f)
            {
                Vector2 norm = dir.Normalized();
                Vector2 oldPos = Position;
                Position += norm * MovementSpeed * TargetMultiplier * (float)delta;
                isMoving = true;
                velocity = Position - oldPos;
            }
        }

        var anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (anim != null)
        {
            var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (isMoving)
            {
                bool isUpwardDominant = velocity.Y < 0.0f && MathF.Abs(velocity.Y) > MathF.Abs(velocity.X);
                if (isUpwardDominant)
                {
                    anim.Play("walk_up");
                    if (sprite != null && _assignedUpTexture != null && sprite.Texture != _assignedUpTexture)
                    {
                        sprite.Texture = _assignedUpTexture;
                    }
                }
                else
                {
                    anim.Play("walk_side");
                    if (sprite != null && _assignedSideTexture != null && sprite.Texture != _assignedSideTexture)
                    {
                        sprite.Texture = _assignedSideTexture;
                    }
                }

                if (sprite != null)
                {
                    if (velocity.X < -0.01f)
                    {
                        sprite.FlipH = _skinDefaultFacingLeft ? false : true;
                    }
                    else if (velocity.X > 0.01f)
                    {
                        sprite.FlipH = _skinDefaultFacingLeft ? true : false;
                    }
                }
            }
            else
            {
                anim.Stop();
                if (sprite != null)
                {
                    sprite.Frame = 0;
                    if (_assignedSideTexture != null && sprite.Texture != _assignedSideTexture)
                    {
                        sprite.Texture = _assignedSideTexture;
                    }
                }
            }
        }
    }

    public void BeginLaneWalk(Vector2 startPosition, Vector2 targetPosition)
    {
        Position = startPosition;
        WalkTargetPosition = targetPosition;
        IsWalkingToLane = true;
        LegacyMovementEnabled = false;
    }

    public void SetLaneWalkTarget(Vector2 targetPosition)
    {
        WalkTargetPosition = targetPosition;
    }

    private void UpdateLaneWalk(float delta)
    {
        Vector2 target = WalkTargetPosition;
        float step = MovementSpeed * TargetMultiplier * delta;
        float xDelta = MathF.Abs(Position.X - target.X);
        float yDelta = MathF.Abs(Position.Y - target.Y);

        if (xDelta > 2.0f)
        {
            Position = new Vector2(MoveToward(Position.X, target.X, step), Position.Y);
            return;
        }

        if (yDelta > 2.0f)
        {
            Position = new Vector2(target.X, MoveToward(Position.Y, target.Y, step));
            return;
        }

        Position = target;
        IsWalkingToLane = false;
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        float delta = MathF.Min(MathF.Abs(target - current), MathF.Max(0.0f, maxDelta));
        return current < target ? current + delta : current - delta;
    }

    public bool ContainsPoint(Vector2 point, float hitRadius)
    {
        return GlobalPosition.DistanceTo(point) <= hitRadius;
    }
}
