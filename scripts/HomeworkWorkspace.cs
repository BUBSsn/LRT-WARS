#nullable enable
using Godot;

public partial class HomeworkWorkspace : Node2D
{
    private GodotCommuterAgent? _draggedPassenger;
    private GuardHouseHighlightNode? _highlightNode;

    /// <summary>
    /// The screen-space rectangle that acts as the Guard House drop zone.
    /// Adjust Position (X, Y) and Size (Z, W) in the Godot Inspector.
    /// Default: X=900, Y=150, W=252, H=370
    /// </summary>
    [Export]
    public Rect2 GuardHouseHitbox { get; set; } = new Rect2(900.0f, 150.0f, 252.0f, 370.0f);

    public override void _Ready()
    {
        CallDeferred(nameof(InitManagerEvents));
        CallDeferred(nameof(SetupHighlightNode));
    }

    private void SetupHighlightNode()
    {
        var platformScreen = GetTree().CurrentScene?.FindChild("Platform_Screen", true, false) as Node2D;
        if (platformScreen != null)
        {
            _highlightNode = new GuardHouseHighlightNode(this);
            platformScreen.AddChild(_highlightNode);
        }
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
        var hud = GetNodeOrNull<GameHUD>("HUD");
        if (hud != null)
        {
            hud.ShowAnnouncement(text, baseColor);
        }
    }

    public override void _Process(double delta)
    {
        var sim = SimulationManager.Instance;
        if (sim == null)
        {
            return;
        }

        if (_draggedPassenger != null)
        {
            if (sim.CurrentState != TrainRoundState.WaitingForTrain && sim.CurrentState != TrainRoundState.Arriving && !sim.IsFixingStandby && !sim.IsStandbyBufferActive)
            {
                Vector2 dropPos = GetGlobalMousePosition();
                if (sim.ActivePerspective == Perspective.PLATFORM && GuardHouseHitbox.HasPoint(dropPos))
                {
                    sim.EliminatePassenger(_draggedPassenger);
                }
                else
                {
                    sim.CommitDraggedPassenger(_draggedPassenger, dropPos);
                }
                _draggedPassenger = null;
                _highlightNode?.QueueRedraw();
            }
            else
            {
                sim.UpdateDraggedPassenger(_draggedPassenger, GetGlobalMousePosition());
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        var sim = SimulationManager.Instance;
        if (sim == null)
        {
            return;
        }

        if (sim.ActivePerspective == Perspective.UNDER_STATION)
        {
            var minigame = GetTree().CurrentScene?.FindChild("WireMinigame", true, false) as WireMinigame;
            if (minigame != null && minigame.Visible)
            {
                return;
            }

            if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                Vector2 clickPos = GetGlobalMousePosition();

                var esc = sim.EscalatorDevice;
                if (esc != null && IsInstanceValid(esc))
                {
                    Vector2 localPos = esc.ToLocal(clickPos);
                    Rect2 clickBox = new Rect2(-120, -250, 240, 500);
                    if (clickBox.HasPoint(localPos))
                    {
                        esc.OnClickTriggered();
                        return;
                    }
                }

                foreach (var tvm in sim.TicketMachines)
                {
                    if (IsInstanceValid(tvm))
                    {
                        Vector2 localPos = tvm.ToLocal(clickPos);
                        Rect2 clickBox = new Rect2(-60, -60, 120, 120);
                        if (clickBox.HasPoint(localPos))
                        {
                            if (tvm.IsBroken)
                            {
                                tvm.Repair();
                            }
                            return;
                        }
                    }
                }
            }
            return;
        }

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                if (_draggedPassenger == null && sim.TryStartDraggingPassenger(GetGlobalMousePosition(), out var passenger))
                {
                    _draggedPassenger = passenger;
                    _highlightNode?.QueueRedraw();
                }
            }
            else if (_draggedPassenger != null)
            {
                Vector2 dropPos = GetGlobalMousePosition();
                if (sim.ActivePerspective == Perspective.PLATFORM && GuardHouseHitbox.HasPoint(dropPos))
                {
                    sim.EliminatePassenger(_draggedPassenger);
                }
                else
                {
                    sim.CommitDraggedPassenger(_draggedPassenger, dropPos);
                }
                _draggedPassenger = null;
                _highlightNode?.QueueRedraw();
            }
        }
    }

    private partial class GuardHouseHighlightNode : Node2D
    {
        private HomeworkWorkspace _parent;

        public GuardHouseHighlightNode(HomeworkWorkspace parent)
        {
            _parent = parent;
            ZIndex = 100; // Draw on top of background
        }

        public override void _Draw()
        {
            var sim = SimulationManager.Instance;
            if (sim != null && _parent._draggedPassenger != null && sim.ActivePerspective == Perspective.PLATFORM)
            {
                Rect2 hitbox = _parent.GuardHouseHitbox;
                DrawRect(hitbox, new Color(1.0f, 0.0f, 0.0f, 0.25f), true);
                DrawRect(hitbox, new Color(1.0f, 0.0f, 0.0f, 0.8f), false, 4.0f);
            }
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
