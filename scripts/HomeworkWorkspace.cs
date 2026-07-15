#nullable enable
using Godot;

public partial class HomeworkWorkspace : Node2D
{
    private GodotCommuterAgent? _draggedPassenger;

    public override void _Ready()
    {
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
            if (sim.CurrentState != TrainRoundState.WaitingForTrain && sim.CurrentState != TrainRoundState.Arriving)
            {
                sim.CommitDraggedPassenger(_draggedPassenger, GetGlobalMousePosition());
                _draggedPassenger = null;
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
                }
            }
            else if (_draggedPassenger != null)
            {
                sim.CommitDraggedPassenger(_draggedPassenger, GetGlobalMousePosition());
                _draggedPassenger = null;
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
