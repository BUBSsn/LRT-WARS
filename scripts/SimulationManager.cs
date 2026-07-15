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
	Arriving,
	Stopped,
	Scoring,
	Transition
}

public partial class SimulationManager : Node
{
	public static SimulationManager Instance { get; private set; }

	public Node2D ConcourseScreen { get; set; }
	public Node2D PlatformScreen { get; set; }
	public Node2D TrainScreen { get; set; }

	private Perspective _activePerspective = Perspective.PLATFORM;
	public Perspective ActivePerspective
	{
		get => _activePerspective;
		set
		{
			_activePerspective = value;
			UpdateScreenVisibilities();
		}
	}

	public const int LaneCount = 5;

	private const float BaseRoundDuration = 20.0f;
	private const float RoundDurationDecrement = 2.0f;
	private const float MinimumRoundDuration = 10.0f;
	private const float TrainArrivalSeconds = 2.0f;
	private const float TrainDepartureSeconds = 2.2f;
	private const float LaneSpacing = 34.0f;
	[Export]
	public float LaneTopOffset { get; set; } = 240.0f;
	[Export]
	public float AirconBreakChance { get; set; } = 0.1f; // 10% chance per train arrival.
	private bool _isAirconDialogOpen = false;
	private bool _isFixingStandby = false;
	private bool _isStandbyBufferActive = false;
	private float _standbyBufferTimer = 0.0f;

	public bool IsFixingStandby => _isFixingStandby;
	public bool IsStandbyBufferActive => _isStandbyBufferActive;
	public float StandbyBufferTimer => _standbyBufferTimer;
	public int PendingPassengerSpawns => _pendingPassengerSpawns;
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

	public float Budget
	{
		get => CumulativeScore;
		set => CumulativeScore = value;
	}

	public readonly List<GodotTVM> TicketMachines = new List<GodotTVM>();
	public GodotEscalator EscalatorDevice { get; private set; }
	private WireMinigame _wireMinigameNode;
	public bool CurrentRoundBalanced { get; private set; } = false;
	public string RoundResultText { get; private set; } = "";
	public float BalanceMeter { get; set; } = 100.0f;
	public int CurrentRoundPassengerCount { get; private set; } = 15;

	private float _currentRageScore = 100.0f;
	private bool _hasCompletedAnyRound = false;

	public float AverageRage
	{
		get
		{
			if (!_hasCompletedAnyRound)
			{
				return 100.0f; 
			}
			return _currentRageScore;
		}
	}

	public Action<string, Color> OnFlashNotification;

	public List<GodotCommuterAgent> Passengers { get; } = new List<GodotCommuterAgent>();

	private readonly int[] _laneCounts = new int[LaneCount];
	private readonly int[] _initialLaneCounts = new int[LaneCount];
	private readonly float[] _laneBoardingTimers = new float[LaneCount];
	private readonly Random _random = new Random();
	private readonly List<float> _completedRoundScores = new List<float>();
	private readonly List<int> _precalculatedTargetLanes = new List<int>();

	private bool _roundEnding = false;
	private float _transitionTimer = 0.0f;
	private float _arrivalTimer = 0.0f;
	private Node2D _platformScreen;
	private Node2D _concourseScreen;
	private Node2D _trainScreen;
	private Sprite2D _trainVehicle;
	private bool _trainParkPositionCaptured = false;
	private Vector2 _trainParkPosition = Vector2.Zero;
	private Vector2 _trainOffscreenLeft = Vector2.Zero;
	private Vector2 _trainOffscreenRight = Vector2.Zero;
	private Vector2 _viewportSize = new Vector2(800f, 600f);
	private int _nextSpawnOrder = 0;
	private int _pendingPassengerSpawns = 0;
	private float _nextPassengerSpawnDelay = 0.0f;
	private float _spawnInterval = 0.5f;

	public override void _Ready()
	{
		Instance = this;
		PassengerScene = GD.Load<PackedScene>("res://GodotCommuterAgent.tscn");
		CallDeferred(nameof(BeginFirstRound));
		CallDeferred(nameof(UpdateScreenVisibilities));
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;
		UpdateViewportBounds();
		ResolveSceneReferences();

		if (_isAirconDialogOpen)
		{
			return;
		}

		if (CurrentState == TrainRoundState.WaitingForTrain)
		{
			RoundElapsed += d;
			RoundTimeRemaining = Math.Max(0.0f, RoundTimeRemaining - d);
			
			if (_trainVehicle != null)
			{
				_trainVehicle.Position = _trainOffscreenLeft;
				_trainVehicle.Visible = false;
			}

			ProcessPassengerSpawns(d);
			UpdateWalkingPassengers();
			LayoutPassengers();

			if (RoundTimeRemaining <= 0.0f)
			{
				CurrentState = TrainRoundState.Arriving;
				_arrivalTimer = 0.0f;
				OnFlashNotification?.Invoke("⚠️ TRAIN ARRIVING - FINALIZE LINES", Colors.YellowGreen);
			}
		}
		else if (CurrentState == TrainRoundState.Arriving)
		{
			RoundElapsed += d;
			_arrivalTimer += d;
			RoundTimeRemaining = Math.Max(0.0f, TrainArrivalSeconds - _arrivalTimer);

			if (_trainVehicle != null)
			{
				_trainVehicle.Visible = true;
				float progress = Math.Clamp(_arrivalTimer / TrainArrivalSeconds, 0.0f, 1.0f);
				float easedProgress = progress * (2.0f - progress); // Deceleration (Ease-Out)
				_trainVehicle.Position = _trainOffscreenLeft.Lerp(_trainParkPosition, easedProgress);
			}

			ProcessPassengerSpawns(d);
			UpdateWalkingPassengers();
			LayoutPassengers();

			if (_arrivalTimer >= TrainArrivalSeconds)
			{
				float breakRoll = (float)_random.NextDouble();
				if (breakRoll < AirconBreakChance)
				{
					TriggerAirconMalfunction();
				}
				else
				{
					StartTrainStoppedState();
				}
			}
		}
		else if (CurrentState == TrainRoundState.Stopped)
		{
			if (_trainVehicle != null)
			{
				_trainVehicle.Visible = true;
				_trainVehicle.Position = _trainParkPosition;
			}

			if (_isFixingStandby)
			{
				ProcessPassengerSpawns(d);
				UpdateWalkingPassengers();
				LayoutPassengers();

				if (AreAllPassengersLinedUp())
				{
					_isFixingStandby = false;
					_isStandbyBufferActive = true;
					_standbyBufferTimer = 5.0f;
					OnFlashNotification?.Invoke("⏳ AIRCON FIXED! 5s TO ARRANGE PASSENGERS!", Colors.Yellow);
				}
				return;
			}

			if (_isStandbyBufferActive)
			{
				_standbyBufferTimer = Math.Max(0.0f, _standbyBufferTimer - d);
				UpdateWalkingPassengers();
				LayoutPassengers();

				if (_standbyBufferTimer <= 0.0f)
				{
					_isStandbyBufferActive = false;
					StartTrainStoppedState();
				}
				return;
			}

			RoundElapsed += d;
			RoundTimeRemaining = Math.Max(0.0f, RoundTimeRemaining - d);

			ProcessPassengerSpawns(d);
			UpdateWalkingPassengers();
			// LayoutPassengers(); // Disabled to allow boarding animation positions

			for (int l = 0; l < LaneCount; l++)
			{
				if (_initialLaneCounts[l] > 0)
				{
					_laneBoardingTimers[l] += d;
					float boardingInterval = 3.0f / _initialLaneCounts[l];
					while (_laneBoardingTimers[l] >= boardingInterval)
					{
						_laneBoardingTimers[l] -= boardingInterval;
						
						var lanePassengers = GetLanePassengers(l);
						if (lanePassengers.Count > 0)
						{
							var frontPassenger = lanePassengers[0];
							Passengers.Remove(frontPassenger);
							_laneCounts[l] = Math.Max(0, _laneCounts[l] - 1);
							if (IsInstanceValid(frontPassenger))
							{
								if (_trainScreen != null)
								{
									frontPassenger.CurrentPerspective = Perspective.INSIDE_CARS;
									frontPassenger.IsWalkingToLane = false;
									frontPassenger.LegacyMovementEnabled = false;

									// Position inside train cabin (horizontal offset based on lane, vertical center inside train car)
									float x = GetLaneSlotPosition(l, 0).X + (float)(_random.NextDouble() * 80.0 - 40.0);
									float y = 300.0f + (float)(_random.NextDouble() * 100.0 - 50.0);
									frontPassenger.Position = new Vector2(x, y);

									var anim = frontPassenger.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
									if (anim != null)
									{
										anim.Stop();
									}
									var sprite = frontPassenger.GetNodeOrNull<Sprite2D>("Sprite2D");
									if (sprite != null)
									{
										sprite.Frame = 0;
									}

									frontPassenger.CallDeferred("reparent", _trainScreen, false);
								}
								else
								{
									frontPassenger.QueueFree();
								}
							}
						}

						StartNextBoardingStep(l);
					}
				}
			}

			if (RoundTimeRemaining <= 0.0f)
			{
				FinalizeRound();
			}
		}
		else if (CurrentState == TrainRoundState.Scoring)
		{
			_transitionTimer += d;
			RoundTimeRemaining = Math.Max(0.0f, TrainDepartureSeconds - _transitionTimer);

			if (_trainVehicle != null)
			{
				_trainVehicle.Visible = true;
				float progress = Math.Clamp(_transitionTimer / TrainDepartureSeconds, 0.0f, 1.0f);
				float easedProgress = progress * progress; // Acceleration (Ease-In)
				_trainVehicle.Position = _trainParkPosition.Lerp(_trainOffscreenRight, easedProgress);
			}

			ProcessPassengerSpawns(d);
			UpdateWalkingPassengers();

			if (_transitionTimer >= TrainDepartureSeconds)
			{
				StartNextRound();
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Key1 || keyEvent.Keycode == Key.Kp1)
			{
				if (ActivePerspective != Perspective.PLATFORM)
				{
					ActivePerspective = Perspective.PLATFORM;
					OnFlashNotification?.Invoke("CAMERA: PLATFORM DECK 1", Colors.DarkBlue);
				}
			}
			else if (keyEvent.Keycode == Key.Key2 || keyEvent.Keycode == Key.Kp2)
			{
				if (ActivePerspective != Perspective.UNDER_STATION)
				{
					ActivePerspective = Perspective.UNDER_STATION;
					OnFlashNotification?.Invoke("CAMERA: UNDER-STATION CONCOURSE 2", Colors.DarkGoldenrod);
				}
			}
			else if (keyEvent.Keycode == Key.Key3 || keyEvent.Keycode == Key.Kp3)
			{
				if (ActivePerspective != Perspective.INSIDE_CARS)
				{
					ActivePerspective = Perspective.INSIDE_CARS;
					OnFlashNotification?.Invoke("CAMERA: METRO CARRIAGE 3", Colors.DarkSlateBlue);
				}
			}
		}
	}

	public void ResetSimulation()
	{
		ClearPassengers(true);
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
		CurrentRoundPassengerCount = 15;
		_completedRoundScores.Clear();
		_currentRageScore = 100.0f;
		_hasCompletedAnyRound = false;
		_precalculatedTargetLanes.Clear();
		_roundEnding = false;
		_transitionTimer = 0.0f;
		_arrivalTimer = 0.0f;
		_nextSpawnOrder = 0;
		_pendingPassengerSpawns = 0;
		_nextPassengerSpawnDelay = 0.0f;
		_isAirconDialogOpen = false;
		_isFixingStandby = false;
		_isStandbyBufferActive = false;
		_standbyBufferTimer = 0.0f;
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
		ClearPassengers(false);
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
		_arrivalTimer = 0.0f;

		PreparePassengerSpawns();
		LayoutPassengers();
		PositionTrainForArrival();

		CurrentState = TrainRoundState.WaitingForTrain;
		OnFlashNotification?.Invoke($"🚉 ROUND {CurrentRound} STARTED - BALANCE THE 5 LINES", Colors.SteelBlue);
	}

	private void StartNextRound()
	{
		CurrentRound++;
		BeginRound();
	}

	private void TriggerAirconMalfunction()
	{
		CurrentState = TrainRoundState.Stopped;
		if (_trainVehicle != null)
		{
			_trainVehicle.Visible = true;
			_trainVehicle.Position = _trainParkPosition;
		}

		_isAirconDialogOpen = true;

		var dialog = new AirconDialog();
		dialog.OnChoiceMade = (bool fixIt) =>
		{
			_isAirconDialogOpen = false;
			if (fixIt)
			{
				StartAirconFixStandby();
			}
			else
			{
				AdjustRage(25.0f);
				OnFlashNotification?.Invoke("🚪 LET THEM IN - RAGE SPIKED BY 25!", Colors.OrangeRed);
				StartTrainStoppedState();
			}
		};

		GetTree().CurrentScene.AddChild(dialog);
	}

	private void StartTrainStoppedState()
	{
		CurrentState = TrainRoundState.Stopped;
		RoundTimeRemaining = 3.0f;
		OnFlashNotification?.Invoke("🛑 TRAIN STOPPED - ARRANGING CLOSED", Colors.OrangeRed);

		for (int l = 0; l < LaneCount; l++)
		{
			var lanePassengers = GetLanePassengers(l);
			_initialLaneCounts[l] = lanePassengers.Count;
			_laneBoardingTimers[l] = 0.0f;
			StartNextBoardingStep(l);
		}
	}

	private void StartAirconFixStandby()
	{
		_isFixingStandby = true;
		CurrentState = TrainRoundState.Stopped;
		
		CurrentRound++;
		PreparePassengerSpawns();

		OnFlashNotification?.Invoke($"🔧 FIXING AIRCON - STANDBY FOR WAVE {CurrentRound}!", Colors.Orange);
	}

	private bool AreAllPassengersLinedUp()
	{
		if (_pendingPassengerSpawns > 0)
		{
			return false;
		}

		foreach (var p in Passengers)
		{
			if (IsInstanceValid(p) && !p.IsDragging && p.LaneIndex == -1)
			{
				return false;
			}
		}

		return true;
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

		// Spike rage of passengers who did not make it onto the train
		for (int i = 0; i < Passengers.Count; i++)
		{
			var p = Passengers[i];
			if (IsInstanceValid(p))
			{
				p.IndividualRage = 100.0f;
			}
		}

		EvaluateRound();

		int boardedPickpockets = 0;
		if (_trainScreen != null)
		{
			foreach (var child in _trainScreen.GetChildren())
			{
				if (child is GodotCommuterAgent agent && IsInstanceValid(agent) && agent.IsPickpocket)
				{
					boardedPickpockets++;
				}
			}
		}

		if (boardedPickpockets > 0)
		{
			AdjustRage(boardedPickpockets * 5.0f);
		}

		RoundResultText = CurrentRoundBalanced
			? $"ROUND {CurrentRound} BALANCED | SCORE {CurrentRoundScore:0} | RAGE AVG {AverageRage:0}"
			: $"ROUND {CurrentRound} UNBALANCED | SCORE {CurrentRoundScore:0} | RAGE AVG {AverageRage:0}";

		CumulativeScore += CurrentRoundScore;
		_completedRoundScores.Add(CurrentRoundScore);

		string notificationText = CurrentRoundBalanced
			? $"✅ TRAIN BALANCED - SCORE {CurrentRoundScore:0} | RAGE AVG {AverageRage:0}"
			: $"⚠️ TRAIN UNBALANCED - SCORE {CurrentRoundScore:0} | RAGE AVG {AverageRage:0}";
		Color notificationColor = CurrentRoundBalanced ? Colors.DarkGreen : Colors.DarkOrange;

		if (boardedPickpockets > 0)
		{
			notificationText += $" | ⚠️ {boardedPickpockets} PICKPOCKETS BOARDED! (Rage +{boardedPickpockets * 5})";
			notificationColor = Colors.OrangeRed;
		}

		OnFlashNotification?.Invoke(notificationText, notificationColor);
	}

	private void EvaluateRound()
	{
		float balanceScore = CalculatePotentialScore(out bool isBalanced);
		CurrentRoundScore = balanceScore;
		CurrentRoundBalanced = isBalanced;
		BalanceMeter = balanceScore;

		// Calculate average individual rage of all active round passengers
		float totalIndividualRage = 0.0f;
		int activeCount = 0;
		foreach (var p in Passengers)
		{
			if (IsInstanceValid(p))
			{
				totalIndividualRage += p.IndividualRage;
				activeCount++;
			}
		}
		float avgIndividualRage = activeCount > 0 ? (totalIndividualRage / activeCount) : 0.0f;

		// Commuter satisfaction score (100 = 0 rage, 0 = 100 max rage)
		float satisfactionScore = 100.0f - avgIndividualRage;

		// Combined performance of balance and satisfaction
		float roundPerformance = (0.5f * balanceScore) + (0.5f * satisfactionScore);

		if (!_hasCompletedAnyRound)
		{
			_currentRageScore = roundPerformance;
			_hasCompletedAnyRound = true;
		}
		else
		{
			_currentRageScore = (0.35f * roundPerformance) + (0.65f * _currentRageScore);
		}
	}

	/// <summary>
	/// Scores the arrangement that was locked in when the train stopped.
	/// Uses _initialLaneCounts (the snapshot taken at train-stop time) so that
	/// the score is unaffected by passengers boarding during the 3-second window.
	/// N is derived from the snapshot sum so the ideal perfectly matches reality.
	/// </summary>
	private float CalculatePotentialScore(out bool isBalanced)
	{
		// Derive N from the snapshot so partial rounds still score correctly.
		int n = 0;
		for (int i = 0; i < LaneCount; i++) n += _initialLaneCounts[i];

		if (n == 0)
		{
			isBalanced = true;
			return 100.0f;
		}

		int base_ = n / LaneCount;
		int remainder = n % LaneCount;

		// Build the ideal sorted distribution (descending).
		int[] ideal = new int[LaneCount];
		for (int i = 0; i < LaneCount; i++)
		{
			ideal[i] = (i < remainder) ? (base_ + 1) : base_;
		}

		// Copy the locked-in counts and sort descending.
		int[] actual = new int[LaneCount];
		for (int i = 0; i < LaneCount; i++)
		{
			actual[i] = _initialLaneCounts[i];
		}
		Array.Sort(actual);
		Array.Reverse(actual);

		// Sum of absolute deviations between sorted actual and sorted ideal.
		float totalDeviation = 0.0f;
		for (int i = 0; i < LaneCount; i++)
		{
			totalDeviation += MathF.Abs(actual[i] - ideal[i]);
		}

		// Worst case: all n passengers on one lane.
		int[] worst = new int[LaneCount];
		worst[0] = n;
		float worstDeviation = 0.0f;
		for (int i = 0; i < LaneCount; i++)
		{
			worstDeviation += MathF.Abs(worst[i] - ideal[i]);
		}
		worstDeviation = Math.Max(1.0f, worstDeviation);

		float normalizedDeviation = Math.Clamp(totalDeviation / worstDeviation, 0.0f, 1.0f);
		float score = MathF.Round((1.0f - normalizedDeviation) * 100.0f);

		// Balanced = every lane is within 1 of every other lane.
		isBalanced = (actual[0] - actual[LaneCount - 1]) <= 1;

		return score;
	}

	private void PreparePassengerSpawns()
	{
		// Dynamically scale passenger counts as the round progresses
		int minCount = 10 + (CurrentRound * 2);
		int maxCount = 15 + (CurrentRound * 3);
		CurrentRoundPassengerCount = _random.Next(minCount, maxCount + 1);
		_pendingPassengerSpawns = CurrentRoundPassengerCount;

		// Calculate dynamic spawn window buffer so passengers finish spawning early in early rounds,
		// but spawn tighter as the difficulty scales up
		float spawnEndBuffer = Math.Max(5.0f, 9.0f - (CurrentRound - 1) * 0.8f);
		float spawnWindow = Math.Max(2.0f, CurrentRoundDuration - spawnEndBuffer);
		_spawnInterval = spawnWindow / _pendingPassengerSpawns;
		_nextPassengerSpawnDelay = 0.0f; // Start spawning the first one immediately!

		// Pre-calculate evenly balanced target lanes
		_precalculatedTargetLanes.Clear();
		int baseCount = CurrentRoundPassengerCount / LaneCount;
		int remainder = CurrentRoundPassengerCount % LaneCount;

		// Fill lanes: first 'remainder' lanes get one extra passenger
		for (int lane = 0; lane < LaneCount; lane++)
		{
			int count = (lane < remainder) ? (baseCount + 1) : baseCount;
			for (int j = 0; j < count; j++)
			{
				_precalculatedTargetLanes.Add(lane);
			}
		}

		// Shuffle so passengers don't all go to lane 0 first
		for (int i = _precalculatedTargetLanes.Count - 1; i > 0; i--)
		{
			int j = _random.Next(i + 1);
			int tmp = _precalculatedTargetLanes[i];
			_precalculatedTargetLanes[i] = _precalculatedTargetLanes[j];
			_precalculatedTargetLanes[j] = tmp;
		}
	}

	private void ProcessPassengerSpawns(float delta)
	{
		if (_pendingPassengerSpawns <= 0)
		{
			return;
		}

		// Force spawn remaining passengers if time remaining <= 5.0s, but space them by at least 0.25s
		if (RoundTimeRemaining <= 5.0f)
		{
			float urgentInterval = 0.25f;
			_nextPassengerSpawnDelay -= delta;
			if (_nextPassengerSpawnDelay <= 0.0f)
			{
				SpawnPassenger();
				_pendingPassengerSpawns--;
				_nextPassengerSpawnDelay = urgentInterval;
			}
			return;
		}

		_nextPassengerSpawnDelay -= delta;

		if (_nextPassengerSpawnDelay <= 0.0f)
		{
			SpawnPassenger();
			_pendingPassengerSpawns--;
			_nextPassengerSpawnDelay = _spawnInterval;
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
		passenger.IsPickpocket = _random.NextDouble() < 0.10;
		passenger.LaneIndex = -1;

		passenger.LaneSlotIndex = -1;
		passenger.LegacyMovementEnabled = false;
		passenger.IsTrainPassenger = true;
		passenger.IsDragging = false;
		passenger.IsWalkingToLane = false;
		passenger.CurrentPerspective = Perspective.UNDER_STATION;

		// Spawn point in concourse (bottom-left entry region)
		passenger.Position = new Vector2(50.0f, 500.0f);

		ResolveSceneReferences();
		var tvm = passenger.FindShortestTVMQueue();
		if (tvm != null)
		{
			passenger.TargetTVM = tvm;
		}
		else if (TicketMachines.Count > 0)
		{
			passenger.TargetTVM = TicketMachines[0];
		}
		passenger.CurrentConcourseState = GodotCommuterAgent.ConcourseState.WalkingToTVM;

		AddPassengerToWorld(passenger);
		Passengers.Add(passenger);

		passenger.MovementSpeed = 240.0f;
	}

	public void TransitionPassengerToPlatform(GodotCommuterAgent passenger)
	{
		if (!IsInstanceValid(passenger)) return;

		ResolveSceneReferences();

		if (_concourseScreen != null && _platformScreen != null)
		{
			passenger.CallDeferred("reparent", _platformScreen, false);
		}

		passenger.CurrentPerspective = Perspective.PLATFORM;
		passenger.Visible = (ActivePerspective == Perspective.PLATFORM);

		passenger.IsTrainPassenger = true;
		passenger.IsDragging = false;
		
		int spawnIndex = passenger.SpawnOrder;
		passenger.TargetLaneIndex = (spawnIndex < _precalculatedTargetLanes.Count)
			? _precalculatedTargetLanes[spawnIndex]
			: _random.Next(LaneCount);

		passenger.LaneSlotIndex = -1;

		// Position where escalator comes up on the platform screen (below viewport for escalator look)
		float escX = (EscalatorDevice != null && IsInstanceValid(EscalatorDevice)) 
			? EscalatorDevice.Position.X 
			: 950.0f;
		UpdateViewportBounds();
		float escY = _viewportSize.Y - (SpawnOffset * 1.5f);
		Vector2 startPosition = new Vector2(escX, _viewportSize.Y + 80.0f);

		float laneX = GetLaneSlotPosition(passenger.TargetLaneIndex, 0).X;
		Vector2 targetPosition = new Vector2(laneX, escY);

		passenger.BeginLaneWalk(startPosition, targetPosition);
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
		float scale = CurrentRoundDuration / BaseRoundDuration;
		float minDelay = PassengerSpawnDelayMin * scale;
		float maxDelay = PassengerSpawnDelayMax * scale;
		return (float)(_random.NextDouble() * (maxDelay - minDelay) + minDelay);
	}

	private void StartNextBoardingStep(int laneIndex)
	{
		var lanePassengers = GetLanePassengers(laneIndex);
		if (lanePassengers.Count == 0)
		{
			return;
		}

		int initialCount = _initialLaneCounts[laneIndex];
		if (initialCount <= 0)
		{
			return;
		}

		float boardingInterval = 3.0f / initialCount;

		var frontPassenger = lanePassengers[0];
		if (IsInstanceValid(frontPassenger))
		{
			Vector2 doorPos = GetLaneSlotPosition(laneIndex, -1);
			float distance = frontPassenger.Position.DistanceTo(doorPos);
			frontPassenger.MovementSpeed = distance / boardingInterval;
			frontPassenger.BeginLaneWalk(frontPassenger.Position, doorPos);
		}
	}

	private void AddPassengerToWorld(GodotCommuterAgent passenger)
	{
		ResolveSceneReferences();
		Node parent = null;
		if (passenger.CurrentPerspective == Perspective.UNDER_STATION) parent = _concourseScreen;
		else if (passenger.CurrentPerspective == Perspective.PLATFORM) parent = _platformScreen;
		else if (passenger.CurrentPerspective == Perspective.INSIDE_CARS) parent = _trainScreen;

		if (parent == null) parent = GetTree().CurrentScene;
		parent?.AddChild(passenger);
	}

	private void ClearPassengers(bool forceAll = false)
	{
		ResolveSceneReferences();

		if (forceAll)
		{
			foreach (var passenger in Passengers)
			{
				if (IsInstanceValid(passenger))
				{
					passenger.QueueFree();
				}
			}
			Passengers.Clear();

			if (_concourseScreen != null)
			{
				foreach (var child in _concourseScreen.GetChildren())
				{
					if (child is GodotCommuterAgent agent)
					{
						agent.QueueFree();
					}
				}
			}
		}
		else
		{
			// Remove null/invalid instances from our active list
			Passengers.RemoveAll(p => !IsInstanceValid(p));
		}

		// Clean up the train passengers who boarded and departed in both cases
		if (_trainScreen != null)
		{
			foreach (var child in _trainScreen.GetChildren())
			{
				if (child is GodotCommuterAgent agent)
				{
					agent.QueueFree();
				}
			}
		}

		// Reset TVM and Escalator states only on full reset
		if (forceAll)
		{
			foreach (var tvm in TicketMachines)
			{
				if (IsInstanceValid(tvm))
				{
					tvm.ResetMachine();
				}
			}
			if (IsInstanceValid(EscalatorDevice))
			{
				EscalatorDevice.ResetEscalator();
			}
			if (IsInstanceValid(_wireMinigameNode))
			{
				_wireMinigameNode.Visible = false;
			}
		}

		// Recalculate lane counts based on surviving passengers
		Array.Clear(_laneCounts, 0, _laneCounts.Length);
		foreach (var passenger in Passengers)
		{
			if (passenger.LaneIndex >= 0 && passenger.LaneIndex < LaneCount)
			{
				_laneCounts[passenger.LaneIndex]++;
			}
		}
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
				// Walk into position instead of snapping, so late/missed passengers
				// visually stride into their lane slot rather than teleporting.
				if (passenger.Position.DistanceTo(slotPosition) > 2.0f)
				{
					passenger.BeginLaneWalk(passenger.Position, slotPosition);
				}
				else
				{
					passenger.Position = slotPosition;
				}
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

		if (CurrentState != TrainRoundState.WaitingForTrain && CurrentState != TrainRoundState.Arriving && !_isFixingStandby && !_isStandbyBufferActive)
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
		_concourseScreen ??= currentScene.FindChild("Concourse_Screen", true, false) as Node2D;
		_trainScreen ??= currentScene.FindChild("Train_Screen", true, false) as Node2D;
		_trainVehicle ??= currentScene.FindChild("TrainVehicle", true, false) as Sprite2D;
		CaptureTrainParkPosition();

		// 1. Resolve/Spawn Wire Minigame
		if (_wireMinigameNode == null)
		{
			_wireMinigameNode = currentScene.FindChild("WireMinigame", true, false) as WireMinigame;
			if (_wireMinigameNode == null)
			{
				try
				{
					var minigameScene = GD.Load<PackedScene>("res://scenes/WireMinigame.tscn");
					if (minigameScene != null)
					{
						_wireMinigameNode = minigameScene.Instantiate<WireMinigame>();
						_wireMinigameNode.Name = "WireMinigame";
						currentScene.AddChild(_wireMinigameNode);
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr("Failed to load WireMinigame scene: " + ex.Message);
				}
			}
		}

		// 2. Resolve/Spawn Escalator
		if (EscalatorDevice == null && _concourseScreen != null)
		{
			EscalatorDevice = currentScene.FindChild("Escalator", true, false) as GodotEscalator;
			if (EscalatorDevice == null)
			{
				foreach (var child in _concourseScreen.GetChildren())
				{
					if (child is GodotEscalator esc)
					{
						EscalatorDevice = esc;
						break;
					}
				}
			}
			if (EscalatorDevice == null)
			{
				EscalatorDevice = new GodotEscalator { Name = "Escalator", Position = new Vector2(950.0f, 350.0f) };
				_concourseScreen.AddChild(EscalatorDevice);
			}
		}

		// 3. Resolve/Spawn TVMs
		if (_concourseScreen != null)
		{
			var existingTvms = new List<GodotTVM>();
			foreach (var child in _concourseScreen.GetChildren())
			{
				if (child is GodotTVM tvm)
				{
					existingTvms.Add(tvm);
				}
			}

			TicketMachines.Clear();
			if (existingTvms.Count > 0)
			{
				TicketMachines.AddRange(existingTvms);
			}
			else
			{
				float[] xCoords = { 200.0f, 350.0f, 500.0f, 650.0f };
				for (int i = 0; i < 4; i++)
				{
					var tvm = new GodotTVM { 
						Name = $"TVM{i}", 
						Position = new Vector2(xCoords[i], 400.0f) 
					};
					_concourseScreen.AddChild(tvm);
					TicketMachines.Add(tvm);
				}
			}
		}
	}

	private void CaptureTrainParkPosition()
	{
		if (_trainVehicle == null || _trainParkPositionCaptured)
		{
			return;
		}

		_trainParkPosition = _trainVehicle.Position;
		_trainOffscreenLeft = new Vector2(_trainParkPosition.X - Math.Max(2500.0f, _viewportSize.X * 2.0f), _trainParkPosition.Y);
		_trainOffscreenRight = new Vector2(_trainParkPosition.X + Math.Max(2500.0f, _viewportSize.X * 2.0f), _trainParkPosition.Y);
		_trainVehicle.Position = _trainOffscreenLeft;
		_trainVehicle.Visible = (CurrentState != TrainRoundState.WaitingForTrain);
		_trainParkPositionCaptured = true;
	}

	private void PositionTrainForArrival()
	{
		if (_trainVehicle == null)
		{
			return;
		}

		_trainVehicle.Position = _trainOffscreenLeft;
		_trainVehicle.Visible = false;
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
			float easedProgress = progress * (2.0f - progress); // Deceleration
			_trainVehicle.Position = _trainOffscreenLeft.Lerp(_trainParkPosition, easedProgress);
			_trainVehicle.Visible = true;
		}
		else
		{
			float progress = Math.Clamp(_transitionTimer / TrainDepartureSeconds, 0.0f, 1.0f);
			float easedProgress = progress * progress; // Acceleration
			_trainVehicle.Position = _trainParkPosition.Lerp(_trainOffscreenRight, easedProgress);
			_trainVehicle.Visible = true;
		}
	}

	private void UpdateViewportBounds()
	{
		_viewportSize = GetViewport().GetVisibleRect().Size;
	}

	public void AdjustRage(float amount)
	{
		_currentRageScore = Math.Clamp(_currentRageScore - amount, 0.0f, 100.0f);
		_hasCompletedAnyRound = true;
	}

	public void EliminatePassenger(GodotCommuterAgent passenger)
	{
		if (passenger == null || !IsInstanceValid(passenger))
		{
			return;
		}

		if (passenger.IsPickpocket)
		{
			AdjustRage(-10.0f);
			OnFlashNotification?.Invoke("👮 PICKPOCKET ELIMINATED! Rage -10", Colors.Green);
		}
		else
		{
			AdjustRage(10.0f);
			OnFlashNotification?.Invoke("😠 INNOCENT COMMUTER ARRESTED! Rage +10", Colors.Red);
		}

		Passengers.Remove(passenger);

		if (passenger.DragSourceLaneIndex >= 0 && passenger.DragSourceLaneIndex < LaneCount)
		{
			RebuildLaneOrdering(passenger.DragSourceLaneIndex, false);
		}

		passenger.QueueFree();
		LayoutPassengers();
	}

	public void UpdateScreenVisibilities()
	{
		ResolveSceneReferences();

		Node2D concScreen = ConcourseScreen ?? _concourseScreen;
		Node2D platScreen = PlatformScreen ?? _platformScreen;
		Node2D trnScreen = TrainScreen ?? _trainScreen;

		if (concScreen != null) concScreen.Visible = (_activePerspective == Perspective.UNDER_STATION);
		if (platScreen != null) platScreen.Visible = (_activePerspective == Perspective.PLATFORM);
		if (trnScreen != null) trnScreen.Visible = (_activePerspective == Perspective.INSIDE_CARS);

		foreach (var passenger in Passengers)
		{
			if (IsInstanceValid(passenger))
			{
				passenger.Visible = (passenger.CurrentPerspective == _activePerspective);
			}
		}
	}
}
