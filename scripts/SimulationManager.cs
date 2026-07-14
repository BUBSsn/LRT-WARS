using Godot;
using System;
using System.Collections.Generic;

public enum Perspective
{
    PLATFORM,
    UNDER_STATION,
    INSIDE_CARS
}

public enum TrainRoundState
{
    WaitingForTrain,
    Boarding,
    Scoring,
    Transition
}

public partial class SimulationManager : Node
{
    public static SimulationManager Instance { get; private set; }

    public const int LaneCount = 5;

    private const float BaseRoundDuration = 25.0f;
    private const float RoundDurationDecrement = 2.0f;
    private const float MinimumRoundDuration = 10.0f;
    private const float TrainArrivalSeconds = 1.35f;
    private const float TrainDepartureSeconds = 0.85f;
    private const float LaneSpacing = 34.0f;
    private const float LaneTopOffset = 184.0f;
    private const float LaneLeftMargin = 120.0f;
    private const float LaneRightMargin = 120.0f;
    private const float SpawnOffset = 48.0f;
    private const float PassengerSpawnDelayMin = 0.5f;
    private const float PassengerSpawnDelayMax = 2.0f;

    public PackedScene PassengerScene { get; private set; }

    public TrainRoundState CurrentState { get; private set; } = TrainRoundState.WaitingForTrain;
    public int CurrentRound { get; private set; } = 1;
    public float CurrentRoundDuration { get; private set; } = BaseRoundDuration;
    public float RoundTimeRemaining { get; private set; } = BaseRoundDuration;
    public float RoundElapsed { get; private set; } = 0.0f;
    public float CurrentRoundScore { get; private set; } = 0.0f;
    public float CumulativeScore { get; private set; } = 0.0f;
    public bool CurrentRoundBalanced { get; private set; } = false;
    public string RoundResultText { get; private set; } = "";
    public float BalanceMeter { get; private set; } = 100.0f;

    public Action<string, Color> OnFlashNotification;

    public List<GodotCommuterAgent> Passengers { get; } = new List<GodotCommuterAgent>();

    private readonly int[] _laneCounts = new int[LaneCount];
    private readonly Random _random = new Random();

    private bool _roundEnding = false;
    private float _transitionTimer = 0.0f;
    private Node2D _platformScreen;
    private Sprite2D _trainVehicle;
    private bool _trainParkPositionCaptured = false;
    private Vector2 _trainParkPosition = Vector2.Zero;
    private Vector2 _trainOffscreenLeft = Vector2.Zero;
    private Vector2 _trainOffscreenRight = Vector2.Zero;
    private Vector2 _viewportSize = new Vector2(800f, 600f);
    private int _nextSpawnOrder = 0;
    private int _pendingPassengerSpawns = 0;
    private float _nextPassengerSpawnDelay = 0.0f;

    public override void _Ready()
    {
        Instance = this;
        PassengerScene = GD.Load<PackedScene>("res://GodotCommuterAgent.tscn");
        CallDeferred(nameof(BeginFirstRound));
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        UpdateViewportBounds();
        ResolveSceneReferences();

        if (CurrentState == TrainRoundState.WaitingForTrain)
        {
            return;
        }

        if (CurrentState == TrainRoundState.Boarding)
        {
            RoundElapsed += d;
            RoundTimeRemaining = Math.Max(0.0f, RoundTimeRemaining - d);
            UpdateTrainPosition(false);
            ProcessPassengerSpawns(d);
            UpdateWalkingPassengers();
            LayoutPassengers();

            if (RoundTimeRemaining <= 0.0f)
            {
                FinalizeRound();
            }
        }
        else if (CurrentState == TrainRoundState.Scoring)
        {
            _transitionTimer += d;
            UpdateTrainPosition(true);

            if (_transitionTimer >= TrainDepartureSeconds)
            {
                StartNextRound();
            }
        }
    }

    public void ResetSimulation()
    {
        ClearPassengers();
        CurrentRound = 1;
        CurrentState = TrainRoundState.WaitingForTrain;
        CurrentRoundDuration = BaseRoundDuration;
        RoundTimeRemaining = BaseRoundDuration;
        RoundElapsed = 0.0f;
        CurrentRoundScore = 0.0f;
        CumulativeScore = 0.0f;
        CurrentRoundBalanced = false;
        RoundResultText = "";
        BalanceMeter = 100.0f;
        _roundEnding = false;
        _transitionTimer = 0.0f;
        _nextSpawnOrder = 0;
        _pendingPassengerSpawns = 0;
        _nextPassengerSpawnDelay = 0.0f;
        OnFlashNotification?.Invoke("TRAIN SYSTEM RESET", Colors.DarkCyan);
        BeginRound();
    }

    private void BeginFirstRound()
    {
        ResolveSceneReferences();
        CaptureTrainParkPosition();
        BeginRound();
    }

    private void BeginRound()
    {
        ClearPassengers();
        ResolveSceneReferences();
        CaptureTrainParkPosition();

        CurrentRoundDuration = Math.Max(MinimumRoundDuration, BaseRoundDuration - ((CurrentRound - 1) * RoundDurationDecrement));
        RoundTimeRemaining = CurrentRoundDuration;
        RoundElapsed = 0.0f;
        CurrentRoundScore = 0.0f;
        CurrentRoundBalanced = false;
        RoundResultText = $"ROUND {CurrentRound} IN SESSION";
        BalanceMeter = 100.0f;
        _roundEnding = false;
        _transitionTimer = 0.0f;

        PreparePassengerSpawns();
        LayoutPassengers();
        PositionTrainForArrival();

        CurrentState = TrainRoundState.Boarding;
        OnFlashNotification?.Invoke($"🚉 ROUND {CurrentRound} STARTED - BALANCE THE 5 LINES", Colors.SteelBlue);
    }

    private void StartNextRound()
    {
        CurrentRound++;
        BeginRound();
    }

    private void FinalizeRound()
    {
        if (_roundEnding)
        {
            return;
        }

        _roundEnding = true;
        CurrentState = TrainRoundState.Scoring;
        _transitionTimer = 0.0f;

        EvaluateRound();
        RoundResultText = CurrentRoundBalanced
            ? $"ROUND {CurrentRound} BALANCED | SCORE {CurrentRoundScore:0}"
            : $"ROUND {CurrentRound} UNBALANCED | SCORE {CurrentRoundScore:0}";

        CumulativeScore += CurrentRoundScore;
        OnFlashNotification?.Invoke(
            CurrentRoundBalanced
                ? $"✅ TRAIN BALANCED - SCORE {CurrentRoundScore:0}"
                : $"⚠️ TRAIN UNBALANCED - SCORE {CurrentRoundScore:0}",
            CurrentRoundBalanced ? Colors.DarkGreen : Colors.DarkOrange);
    }

    private void EvaluateRound()
    {
        int passengerCount = Passengers.Count;
        int maxCount = 0;
        int minCount = int.MaxValue;
        int totalCount = 0;

        for (int i = 0; i < LaneCount; i++)
        {
            int count = _laneCounts[i];
            totalCount += count;
            maxCount = Math.Max(maxCount, count);
            minCount = Math.Min(minCount, count);
        }

        float average = passengerCount > 0 ? (float)totalCount / LaneCount : 0.0f;
        float totalDeviation = 0.0f;

        for (int i = 0; i < LaneCount; i++)
        {
            totalDeviation += MathF.Abs(_laneCounts[i] - average);
        }

        float worstCaseDeviation = Math.Max(1.0f, passengerCount * 1.6f);
        float normalizedDeviation = Math.Clamp(totalDeviation / worstCaseDeviation, 0.0f, 1.0f);

        CurrentRoundScore = MathF.Round((1.0f - normalizedDeviation) * 100.0f);
        CurrentRoundBalanced = (maxCount - minCount) <= 1;
        BalanceMeter = Math.Clamp(100.0f - (normalizedDeviation * 100.0f), 0.0f, 100.0f);
    }

    private void PreparePassengerSpawns()
    {
        _pendingPassengerSpawns = LaneCount * 3;
        _nextPassengerSpawnDelay = GetRandomPassengerSpawnDelay();
    }

    private void ProcessPassengerSpawns(float delta)
    {
        if (CurrentState != TrainRoundState.Boarding || _pendingPassengerSpawns <= 0)
        {
            return;
        }

        _nextPassengerSpawnDelay -= delta;

        while (_pendingPassengerSpawns > 0 && _nextPassengerSpawnDelay <= 0.0f)
        {
            SpawnPassenger();
            _pendingPassengerSpawns--;

            if (_pendingPassengerSpawns > 0)
            {
                _nextPassengerSpawnDelay += GetRandomPassengerSpawnDelay();
            }
        }
    }

    private int GetNextSlotIndex(int laneIndex)
    {
        int reservedSlot = -1;
        for (int i = 0; i < Passengers.Count; i++)
        {
            var p = Passengers[i];
            if (IsInstanceValid(p) && p.IsDragging && p.DragSourceLaneIndex == laneIndex)
            {
                reservedSlot = p.LaneSlotIndex;
                break;
            }
        }

        List<GodotCommuterAgent> lanePassengers = GetLanePassengers(laneIndex);
        int currentSlot = 0;
        for (int i = 0; i < lanePassengers.Count; i++)
        {
            if (reservedSlot >= 0 && currentSlot == reservedSlot)
            {
                currentSlot++;
            }
            currentSlot++;
        }
        if (reservedSlot >= 0 && currentSlot == reservedSlot)
        {
            currentSlot++;
        }
        return currentSlot;
    }

    private void SpawnPassenger()
    {
        if (PassengerScene == null)
        {
            return;
        }

        var passenger = PassengerScene.Instantiate<GodotCommuterAgent>();
        passenger.SpawnOrder = _nextSpawnOrder++;
        passenger.LaneIndex = -1;
        passenger.TargetLaneIndex = _random.Next(LaneCount);
        passenger.LaneSlotIndex = -1;
        passenger.LegacyMovementEnabled = false;
        passenger.IsTrainPassenger = true;
        passenger.IsDragging = false;
        passenger.IsWalkingToLane = true;

        Vector2 startPosition = GetLaneEntryPosition();
        float laneX = GetLaneSlotPosition(passenger.TargetLaneIndex, 0).X;
        Vector2 targetPosition = new Vector2(laneX, startPosition.Y);
        passenger.BeginLaneWalk(startPosition, targetPosition);

        AddPassengerToWorld(passenger);
        Passengers.Add(passenger);
    }

    private void UpdateWalkingPassengers()
    {
        for (int i = 0; i < Passengers.Count; i++)
        {
            var passenger = Passengers[i];
            if (!IsInstanceValid(passenger) || passenger.IsDragging || passenger.LaneIndex != -1)
            {
                continue;
            }

            if (passenger.IsWalkingToLane && passenger.TargetLaneIndex >= 0 && passenger.TargetLaneIndex < LaneCount)
            {
                float laneX = GetLaneSlotPosition(passenger.TargetLaneIndex, 0).X;
                if (MathF.Abs(passenger.Position.X - laneX) <= 2.0f)
                {
                    passenger.LaneIndex = passenger.TargetLaneIndex;
                    passenger.LaneSlotIndex = GetNextSlotIndex(passenger.LaneIndex);
                    _laneCounts[passenger.LaneIndex]++;
                    
                    Vector2 slotPosition = GetLaneSlotPosition(passenger.LaneIndex, passenger.LaneSlotIndex);
                    passenger.SetLaneWalkTarget(slotPosition);
                }
            }
        }
    }

    private float GetRandomPassengerSpawnDelay()
    {
        return (float)(_random.NextDouble() * (PassengerSpawnDelayMax - PassengerSpawnDelayMin) + PassengerSpawnDelayMin);
    }

    private void AddPassengerToWorld(GodotCommuterAgent passenger)
    {
        Node parent = _platformScreen ?? GetTree().CurrentScene;
        parent?.AddChild(passenger);
    }

    private void ClearPassengers()
    {
        foreach (var passenger in Passengers)
        {
            if (IsInstanceValid(passenger))
            {
                passenger.QueueFree();
            }
        }

        Passengers.Clear();
        Array.Clear(_laneCounts, 0, _laneCounts.Length);
    }

    private void LayoutPassengers()
    {
        UpdateViewportBounds();
        int preservedLaneIndex = GetActiveDraggingSourceLaneIndex();

        for (int lane = 0; lane < LaneCount; lane++)
        {
            RebuildLaneOrdering(lane, lane == preservedLaneIndex);
        }
    }

    private void RebuildLaneOrdering(int laneIndex, bool preserveSlotPositions)
    {
        List<GodotCommuterAgent> lanePassengers = GetLanePassengers(laneIndex);

        int reservedSlot = -1;
        for (int i = 0; i < Passengers.Count; i++)
        {
            var p = Passengers[i];
            if (IsInstanceValid(p) && p.IsDragging && p.DragSourceLaneIndex == laneIndex)
            {
                reservedSlot = p.LaneSlotIndex;
                break;
            }
        }

        int currentSlot = 0;
        for (int i = 0; i < lanePassengers.Count; i++)
        {
            var passenger = lanePassengers[i];

            if (reservedSlot >= 0 && currentSlot == reservedSlot)
            {
                currentSlot++;
            }

            int slotIndex = currentSlot;

            passenger.LaneSlotIndex = slotIndex;
            passenger.ZIndex = slotIndex;

            Vector2 slotPosition = GetLaneSlotPosition(laneIndex, slotIndex);

            if (passenger.IsWalkingToLane)
            {
                passenger.SetLaneWalkTarget(slotPosition);
            }
            else if (!passenger.IsDragging)
            {
                passenger.Position = slotPosition;
            }

            currentSlot++;
        }
    }

    private List<GodotCommuterAgent> GetLanePassengers(int laneIndex)
    {
        var lanePassengers = new List<GodotCommuterAgent>();

        for (int i = 0; i < Passengers.Count; i++)
        {
            var passenger = Passengers[i];
            if (!IsInstanceValid(passenger) || passenger.IsDragging || passenger.LaneIndex != laneIndex)
            {
                continue;
            }

            lanePassengers.Add(passenger);
        }

        lanePassengers.Sort((left, right) =>
        {
            int laneSlotComparison = left.LaneSlotIndex.CompareTo(right.LaneSlotIndex);
            if (laneSlotComparison != 0)
            {
                return laneSlotComparison;
            }

            return left.SpawnOrder.CompareTo(right.SpawnOrder);
        });

        return lanePassengers;
    }

    private int GetActiveDraggingSourceLaneIndex()
    {
        for (int i = 0; i < Passengers.Count; i++)
        {
            var passenger = Passengers[i];
            if (IsInstanceValid(passenger) && passenger.IsDragging)
            {
                return passenger.DragSourceLaneIndex;
            }
        }

        return -1;
    }

    private readonly float[] _doorOffsetsUnscaled = new float[] { -515.0f, -289.0f, -40.0f, 294.0f, 512.0f };

    public Vector2 GetLaneSlotPosition(int laneIndex, int slotIndex)
    {
        float x = 0.0f;
        if (_trainVehicle != null && _trainParkPositionCaptured)
        {
            x = _trainParkPosition.X + (_doorOffsetsUnscaled[laneIndex] * _trainVehicle.Scale.X);
        }
        else
        {
            float availableWidth = Math.Max(120.0f, _viewportSize.X - (LaneLeftMargin + LaneRightMargin));
            float laneSpacing = availableWidth / (LaneCount - 1);
            x = LaneLeftMargin + (laneIndex * laneSpacing);
        }

        float y = LaneTopOffset + (slotIndex * LaneSpacing);
        return new Vector2(x, y);
    }

    public int GetLaneFromScreenX(float x)
    {
        if (_trainVehicle != null && _trainParkPositionCaptured)
        {
            int bestLane = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < LaneCount; i++)
            {
                float laneX = _trainParkPosition.X + (_doorOffsetsUnscaled[i] * _trainVehicle.Scale.X);
                float dist = MathF.Abs(x - laneX);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestLane = i;
                }
            }
            return bestLane;
        }

        float availableWidth = Math.Max(120.0f, _viewportSize.X - (LaneLeftMargin + LaneRightMargin));
        float laneSpacing = availableWidth / (LaneCount - 1);
        float clampedX = Math.Clamp(x, LaneLeftMargin, LaneLeftMargin + availableWidth);

        int lane = (int)MathF.Round((clampedX - LaneLeftMargin) / laneSpacing);
        return Math.Clamp(lane, 0, LaneCount - 1);
    }

    public int GetLowestLaneIndex()
    {
        int lowestLane = 0;
        int lowestCount = int.MaxValue;

        for (int i = 0; i < LaneCount; i++)
        {
            if (_laneCounts[i] < lowestCount)
            {
                lowestCount = _laneCounts[i];
                lowestLane = i;
            }
        }

        return lowestLane;
    }

    public int[] GetLaneCounts()
    {
        int[] copy = new int[LaneCount];
        Array.Copy(_laneCounts, copy, LaneCount);
        return copy;
    }

    public bool TryStartDraggingPassenger(Vector2 mousePosition, out GodotCommuterAgent passenger)
    {
        passenger = null;

        if (CurrentState != TrainRoundState.Boarding)
        {
            return false;
        }

        GodotCommuterAgent bestCandidate = null;
        float bestDistance = float.MaxValue;

        for (int i = Passengers.Count - 1; i >= 0; i--)
        {
            var candidate = Passengers[i];
            if (!IsInstanceValid(candidate) || candidate.IsDragging)
            {
                continue;
            }

            if (!candidate.ContainsPoint(mousePosition, 28.0f))
            {
                continue;
            }

            float distance = candidate.GlobalPosition.DistanceTo(mousePosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestCandidate = candidate;
            bestDistance = distance;
        }

        if (bestCandidate == null)
        {
            return false;
        }

        passenger = bestCandidate;
        if (bestCandidate.LaneIndex >= 0 && bestCandidate.LaneIndex < LaneCount)
        {
            bestCandidate.DragSourceLaneIndex = bestCandidate.LaneIndex;
            _laneCounts[bestCandidate.LaneIndex] = Math.Max(0, _laneCounts[bestCandidate.LaneIndex] - 1);
        }

        bestCandidate.LaneIndex = -1;
        bestCandidate.IsDragging = true;
        bestCandidate.IsWalkingToLane = false;
        bestCandidate.LegacyMovementEnabled = false;
        bestCandidate.ZIndex = 1000;
        LayoutPassengers();
        return true;
    }

    public void UpdateDraggedPassenger(GodotCommuterAgent passenger, Vector2 mousePosition)
    {
        if (passenger == null || !IsInstanceValid(passenger))
        {
            return;
        }

        passenger.Position = mousePosition;
        passenger.ZIndex = 1000;
    }

    public void CommitDraggedPassenger(GodotCommuterAgent passenger, Vector2 mousePosition)
    {
        if (passenger == null || !IsInstanceValid(passenger))
        {
            return;
        }

        int previousLane = passenger.DragSourceLaneIndex;
        int targetLane = GetLaneFromScreenX(mousePosition.X);
        int targetSlot = GetLaneInsertIndex(targetLane, mousePosition.Y);

        passenger.LaneIndex = targetLane;
        passenger.LaneSlotIndex = targetSlot;
        passenger.DragSourceLaneIndex = -1;
        passenger.IsDragging = false;
        passenger.IsWalkingToLane = false;
        passenger.Position = GetLaneSlotPosition(targetLane, targetSlot);
        _laneCounts[targetLane]++;

        if (previousLane >= 0 && previousLane < LaneCount && previousLane != targetLane)
        {
            RebuildLaneOrdering(previousLane, false);
        }

        RebuildLaneOrdering(targetLane, false);

        LayoutPassengers();
    }

    public void CancelDraggedPassenger(GodotCommuterAgent passenger)
    {
        if (passenger == null || !IsInstanceValid(passenger))
        {
            return;
        }

        int lane = GetLowestLaneIndex();
        passenger.LaneIndex = lane;
        passenger.IsDragging = false;
        _laneCounts[lane]++;
        LayoutPassengers();
    }

    private int GetLaneInsertIndex(int laneIndex, float dropY)
    {
        List<GodotCommuterAgent> lanePassengers = GetLanePassengers(laneIndex);

        if (lanePassengers.Count == 0)
        {
            return 0;
        }

        int insertIndex = 0;

        for (int i = 0; i < lanePassengers.Count; i++)
        {
            float slotCenterY = GetLaneSlotPosition(laneIndex, lanePassengers[i].LaneSlotIndex).Y;
            float midpointY = slotCenterY + (LaneSpacing * 0.5f);
            if (dropY > midpointY)
            {
                insertIndex = i + 1;
            }
            else
            {
                break;
            }
        }

        return Math.Clamp(insertIndex, 0, lanePassengers.Count);
    }

    private Vector2 GetLaneEntryPosition()
    {
        UpdateViewportBounds();
        return new Vector2(_viewportSize.X + SpawnOffset, _viewportSize.Y - (SpawnOffset * 1.5f));
    }

    private void ResolveSceneReferences()
    {
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null)
        {
            return;
        }

        _platformScreen ??= currentScene.FindChild("Platform_Screen", true, false) as Node2D;
        _trainVehicle ??= currentScene.FindChild("TrainVehicle", true, false) as Sprite2D;
        CaptureTrainParkPosition();
    }

    private void CaptureTrainParkPosition()
    {
        if (_trainVehicle == null || _trainParkPositionCaptured)
        {
            return;
        }

        _trainParkPosition = _trainVehicle.Position;
        _trainOffscreenLeft = new Vector2(_trainParkPosition.X - Math.Max(700.0f, _viewportSize.X), _trainParkPosition.Y);
        _trainOffscreenRight = new Vector2(_trainParkPosition.X + Math.Max(700.0f, _viewportSize.X), _trainParkPosition.Y);
        _trainVehicle.Position = _trainOffscreenLeft;
        _trainParkPositionCaptured = true;
    }

    private void PositionTrainForArrival()
    {
        if (_trainVehicle == null)
        {
            return;
        }

        _trainVehicle.Position = _trainOffscreenLeft;
    }

    private void UpdateTrainPosition(bool leaving)
    {
        if (_trainVehicle == null)
        {
            return;
        }

        if (!leaving)
        {
            float progress = Math.Clamp(RoundElapsed / TrainArrivalSeconds, 0.0f, 1.0f);
            _trainVehicle.Position = _trainOffscreenLeft.Lerp(_trainParkPosition, progress);
        }
        else
        {
            float progress = Math.Clamp(_transitionTimer / TrainDepartureSeconds, 0.0f, 1.0f);
            _trainVehicle.Position = _trainParkPosition.Lerp(_trainOffscreenRight, progress);
        }
    }

    private void UpdateViewportBounds()
    {
        _viewportSize = GetViewport().GetVisibleRect().Size;
    }
}
