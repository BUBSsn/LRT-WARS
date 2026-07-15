using Godot;
using System;
using System.Collections.Generic;

public partial class WireMinigame : Control
{
    [Signal]
    public delegate void WireMinigameCompletedEventHandler();

    [Export]
    public ColorRect[] LeftPlugs { get; set; } = new ColorRect[0];

    [Export]
    public ColorRect[] RightPlugs { get; set; } = new ColorRect[0];

    [Export]
    public Button EmergencyRepairButton { get; set; }

    [Export]
    public Line2D ActiveLine { get; set; }

    // Permanent lines for completed connections
    [Export]
    public Line2D[] MatchLines { get; set; } = new Line2D[0];

    private static readonly Color[] PlugColorsBase = new Color[]
    {
        Colors.Red,
        Colors.Blue,
        Colors.Yellow,
        Colors.Green
    };

    private Color[] _leftColors = new Color[4];
    private Color[] _rightColors = new Color[4];
    private bool[] _matched = new bool[4]; // indexed by left plug index

    private int _draggedLeftIndex = -1;
    private Random _random = new Random();

    public override void _Ready()
    {
        // Auto-resolve child references if not set in Inspector
        if (LeftPlugs == null || LeftPlugs.Length == 0)
        {
            var leftList = new List<ColorRect>();
            for (int i = 0; i < 4; i++)
            {
                var node = FindChild($"LeftPlug_{i}", true, false) as ColorRect;
                if (node == null) node = FindChild($"LeftPlug{i}", true, false) as ColorRect;
                if (node != null) leftList.Add(node);
            }
            LeftPlugs = leftList.ToArray();
        }

        if (RightPlugs == null || RightPlugs.Length == 0)
        {
            var rightList = new List<ColorRect>();
            for (int i = 0; i < 4; i++)
            {
                var node = FindChild($"RightPlug_{i}", true, false) as ColorRect;
                if (node == null) node = FindChild($"RightPlug{i}", true, false) as ColorRect;
                if (node != null) rightList.Add(node);
            }
            RightPlugs = rightList.ToArray();
        }

        if (EmergencyRepairButton == null)
        {
            EmergencyRepairButton = FindChild("EmergencyRepairButton", true, false) as Button;
        }

        if (ActiveLine == null)
        {
            ActiveLine = FindChild("ActiveLine", true, false) as Line2D;
            if (ActiveLine == null)
            {
                ActiveLine = new Line2D { Width = 8.0f };
                AddChild(ActiveLine);
            }
        }

        if (MatchLines == null || MatchLines.Length == 0)
        {
            var matchLinesList = new List<Line2D>();
            for (int i = 0; i < 4; i++)
            {
                var line = FindChild($"MatchLine_{i}", true, false) as Line2D;
                if (line == null) line = FindChild($"MatchLine{i}", true, false) as Line2D;
                if (line == null)
                {
                    line = new Line2D { Width = 8.0f };
                    AddChild(line);
                }
                matchLinesList.Add(line);
            }
            MatchLines = matchLinesList.ToArray();
        }

        if (EmergencyRepairButton != null)
        {
            EmergencyRepairButton.Pressed += OnEmergencyRepairPressed;
        }

        // Hide by default
        Visible = false;
    }

    public void ResetMinigame()
    {
        var sim = SimulationManager.Instance;
        if (sim != null && EmergencyRepairButton != null)
        {
            if (sim.Budget < 1200.0f)
            {
                EmergencyRepairButton.Disabled = true;
                EmergencyRepairButton.Text = "Emergency Repair ($1200) - Insufficient!";
            }
            else
            {
                EmergencyRepairButton.Disabled = false;
                EmergencyRepairButton.Text = "Emergency Repair ($1200)";
            }
        }

        // Clear matches
        for (int i = 0; i < 4; i++)
        {
            _matched[i] = false;
            if (i < MatchLines.Length && MatchLines[i] != null)
            {
                MatchLines[i].ClearPoints();
            }
        }
        if (ActiveLine != null)
        {
            ActiveLine.ClearPoints();
        }
        _draggedLeftIndex = -1;

        // Shuffle Colors for Left
        var leftIndices = new List<int> { 0, 1, 2, 3 };
        Shuffle(leftIndices);
        for (int i = 0; i < 4; i++)
        {
            if (i < LeftPlugs.Length && LeftPlugs[i] != null)
            {
                _leftColors[i] = PlugColorsBase[leftIndices[i]];
                LeftPlugs[i].Color = _leftColors[i];
            }
        }

        // Shuffle Colors for Right
        var rightIndices = new List<int> { 0, 1, 2, 3 };
        Shuffle(rightIndices);
        for (int i = 0; i < 4; i++)
        {
            if (i < RightPlugs.Length && RightPlugs[i] != null)
            {
                _rightColors[i] = PlugColorsBase[rightIndices[i]];
                RightPlugs[i].Color = _rightColors[i];
            }
        }
    }

    private void Shuffle<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                Vector2 localMousePos = GetLocalMousePosition();
                if (mouseButton.Pressed)
                {
                    // Check if clicked near/on any LeftPlug
                    for (int i = 0; i < LeftPlugs.Length; i++)
                    {
                        if (LeftPlugs[i] != null && !_matched[i])
                        {
                            Rect2 plugRect = LeftPlugs[i].GetRect();
                            Vector2 plugMousePos = LeftPlugs[i].GetLocalMousePosition();
                            if (new Rect2(Vector2.Zero, plugRect.Size).HasPoint(plugMousePos))
                            {
                                _draggedLeftIndex = i;
                                ActiveLine.ClearPoints();
                                // Start line from center of the left plug
                                Vector2 startPos = LeftPlugs[i].Position + plugRect.Size / 2.0f;
                                ActiveLine.AddPoint(startPos);
                                ActiveLine.AddPoint(localMousePos);
                                ActiveLine.DefaultColor = _leftColors[i];
                                break;
                            }
                        }
                    }
                }
                else if (_draggedLeftIndex != -1)
                {
                    // Released button: check if released on matching RightPlug
                    int matchedRightIndex = -1;
                    for (int j = 0; j < RightPlugs.Length; j++)
                    {
                        if (RightPlugs[j] != null)
                        {
                            Rect2 plugRect = RightPlugs[j].GetRect();
                            Vector2 plugMousePos = RightPlugs[j].GetLocalMousePosition();
                            if (new Rect2(Vector2.Zero, plugRect.Size).HasPoint(plugMousePos))
                            {
                                matchedRightIndex = j;
                                break;
                            }
                        }
                    }

                    if (matchedRightIndex != -1 && _rightColors[matchedRightIndex] == _leftColors[_draggedLeftIndex])
                    {
                        // Match! Lock line
                        _matched[_draggedLeftIndex] = true;
                        if (_draggedLeftIndex < MatchLines.Length && MatchLines[_draggedLeftIndex] != null)
                        {
                            Vector2 start = LeftPlugs[_draggedLeftIndex].Position + LeftPlugs[_draggedLeftIndex].GetRect().Size / 2.0f;
                            Vector2 end = RightPlugs[matchedRightIndex].Position + RightPlugs[matchedRightIndex].GetRect().Size / 2.0f;
                            MatchLines[_draggedLeftIndex].ClearPoints();
                            MatchLines[_draggedLeftIndex].AddPoint(start);
                            MatchLines[_draggedLeftIndex].AddPoint(end);
                            MatchLines[_draggedLeftIndex].DefaultColor = _leftColors[_draggedLeftIndex];
                        }
                        
                        CheckCompletion();
                    }

                    ActiveLine.ClearPoints();
                    _draggedLeftIndex = -1;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _draggedLeftIndex != -1)
        {
            // Update line end point to follow mouse
            if (ActiveLine.GetPointCount() > 1)
            {
                ActiveLine.SetPointPosition(1, GetLocalMousePosition());
            }
        }
    }

    private void CheckCompletion()
    {
        bool allMatched = true;
        for (int i = 0; i < 4; i++)
        {
            if (!_matched[i])
            {
                allMatched = false;
                break;
            }
        }

        if (allMatched)
        {
            EmitCompleted();
        }
    }

    private void OnEmergencyRepairPressed()
    {
        var sim = SimulationManager.Instance;
        if (sim != null && sim.Budget >= 1200.0f)
        {
            sim.Budget -= 1200.0f;
            EmitCompleted();
        }
    }

    private void EmitCompleted()
    {
        Visible = false;
        EmitSignal(SignalName.WireMinigameCompleted);
    }
}
