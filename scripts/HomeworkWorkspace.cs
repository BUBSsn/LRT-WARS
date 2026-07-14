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

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
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
