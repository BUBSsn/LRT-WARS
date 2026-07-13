using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDSAStationManager
{
    // High-performance double-buffered visual cell
    public struct ConsoleCell
    {
        public char Char;
        public ConsoleColor Foreground;
        public ConsoleColor Background;

        public ConsoleCell(char c, ConsoleColor fg = ConsoleColor.White, ConsoleColor bg = ConsoleColor.Black)
        {
            Char = c;
            Foreground = fg;
            Background = bg;
        }
    }

    // Specification 1: PERSPECTIVE SYSTEM (THE THREE SCREENS) Enum
    public enum Perspective
    {
        PLATFORM,
        UNDER_STATION,
        INSIDE_CARS
    }

    // Week 2+3: COMMUTER AGENT DATA STRUCTURE
    public class CommuterAgent
    {
        public Vector2 Position { get; set; }
        public Vector2 TargetPosition { get; set; }
        public Perspective CurrentPerspective { get; set; }
        public float MovementSpeed { get; set; }
        public float IndividualRage { get; set; }
        public bool IsPriority { get; set; }     // Pink/magenta priority PWD/Women queue lines
        public bool IsPickpocket { get; set; }   // Week 3: Flagged as active pickpocket threat
        public bool IsFrozen { get; set; }       // Week 3: Frozen by ticket machine backlog
    }

    // GLOBAL STATE SINGLETON MAPS
    public class SimulationManager
    {
        private static readonly SimulationManager _instance = new SimulationManager();
        public static SimulationManager Instance => _instance;

        private SimulationManager() { }

        // Core State
        public float GlobalCommuterRage { get; set; } = 0.0f;    // 0.0 to 100.0
        public float DailyBudget { get; set; } = 5000.0f;        // Starting Budget: $5000
        public Perspective ActivePerspective { get; set; } = Perspective.PLATFORM;
        public bool RiotErupted { get; set; } = false;
        public bool IsRunning { get; set; } = true;

        // Platform View Stats
        public float PlatformDensity { get; set; } = 1.0f;
        public float TrainDelayTimer { get; set; } = 0.0f;
        public int PickpocketCount { get; set; } = 0;
        public float PriorityQueueViolationRate { get; set; } = 0.0f;

        // Under-Station View Stats
        public int TicketMachineFailures { get; set; } = 0;
        public float EscalatorWeightStrain { get; set; } = 0.0f;

        // Inside-Cars View Stats
        public float CarCrowdDensity { get; set; } = 1.0f;
        public float ACFailureChance { get; set; } = 0.0f;
        public bool ACFailed { get; set; } = false;

        // Week 2: Entity lists and spawning meters
        public List<CommuterAgent> Commuters { get; } = new List<CommuterAgent>();
        public float SpawnerTimer { get; set; } = 0.0f;

        // Week 3: Crisis Timers
        public float TicketMachineBreakTimer { get; set; } = 15.0f;  // 15-20s breakdown cycle
        public float ACBreakdownTimer { get; set; } = 25.0f;        // 25s AC failure cycle
        public float PickpocketSpawnTimer { get; set; } = 12.0f;    // Pickpocket flagging cycle

        // Week 3: Tactical Intervention Cooldowns
        public float FanCooldownTimer { get; set; } = 0.0f;         // Industrial Fan active seconds
        public bool FanActiveOnPlatform { get; set; } = false;
        public bool FanActiveOnCars { get; set; } = false;

        // Week 3: Revenue tracking
        public int TotalPassengersTransported { get; set; } = 0;
        public float TotalFareRevenue { get; set; } = 0.0f;
    }

    class Program
    {
        private const int Width = 80;
        private const int Height = 24;

        // Viewport layout bounds definitions (Viewport Canvas)
        private const int ViewX = 2;
        private const int ViewY = 5;
        private const int ViewW = Width - 4; // 76
        private const int ViewH = Height - 8; // 16

        // Visual Buffer
        private static ConsoleCell[,] _buffer = new ConsoleCell[Width, Height];
        private static readonly object _lock = new object();

        // Engine Clock
        private static Timer? _gameTimer;
        private const int TargetFps = 30;
        private const int LoopIntervalMs = 1000 / TargetFps;

        // Shaking properties
        private static Vector2 _screenOffset = Vector2.Zero;
        private static int _frameCount = 0;

        // User Alert Info Banner
        private static string _alertMessage = "OPERATIONS ACTIVE. CONTROL SWITCHBOARD SYSTEMS ON-LINE.";
        private static ConsoleColor _alertBg = ConsoleColor.DarkCyan;
        private static int _alertTicksRemaining = 60;

        // Keyboard Queue
        private static readonly Queue<ConsoleKeyInfo> _inputQueue = new Queue<ConsoleKeyInfo>();

        // Week 3: Crisis alert strings for dynamic HUD
        private static string _crisisAlert1 = "";
        private static string _crisisAlert2 = "";
        private static ConsoleColor _crisisColor1 = ConsoleColor.Red;
        private static ConsoleColor _crisisColor2 = ConsoleColor.Red;

        static async Task Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.Title = "EDSA Station Manager - Week 3 Crisis Engine";

            Initialize();

            // Run Keyboard scanner task thread in background
            var inputTask = Task.Run(InputReaderLoop);

            // Wait until session is quit
            while (SimulationManager.Instance.IsRunning)
            {
                await Task.Delay(100);
            }

            Cleanup();
        }

        private static void Initialize()
        {
            lock (_lock)
            {
                var sim = SimulationManager.Instance;
                sim.GlobalCommuterRage = 0.0f;
                sim.DailyBudget = 5000.0f;
                sim.ActivePerspective = Perspective.PLATFORM;
                sim.RiotErupted = false;
                sim.IsRunning = true;

                // Reset Platform stats
                sim.PlatformDensity = 1.0f;
                sim.TrainDelayTimer = 0.0f;
                sim.PickpocketCount = 0;
                sim.PriorityQueueViolationRate = 0.05f;

                // Reset Under-Station stats
                sim.TicketMachineFailures = 0;
                sim.EscalatorWeightStrain = 10.0f;

                // Reset Inside-Cars stats
                sim.CarCrowdDensity = 2.0f;
                sim.ACFailureChance = 0.05f;
                sim.ACFailed = false;

                // Week 2 resets
                sim.Commuters.Clear();
                sim.SpawnerTimer = 0.0f;

                // Week 3 resets
                sim.TicketMachineBreakTimer = 15.0f + (float)new Random().NextDouble() * 5.0f;
                sim.ACBreakdownTimer = 25.0f;
                sim.PickpocketSpawnTimer = 12.0f;
                sim.FanCooldownTimer = 0.0f;
                sim.FanActiveOnPlatform = false;
                sim.FanActiveOnCars = false;
                sim.TotalPassengersTransported = 0;
                sim.TotalFareRevenue = 0.0f;

                _screenOffset = Vector2.Zero;
                _crisisAlert1 = "";
                _crisisAlert2 = "";
                _alertMessage = "EDSA SYSTEM RESTORED. [1] Platform [2] Under-Station [3] Inside-Cars";
                _alertBg = ConsoleColor.DarkCyan;
                _alertTicksRemaining = 90;
            }

            // Start system thread timer
            _gameTimer?.Dispose();
            _gameTimer = new Timer(GameTickStep, null, 0, LoopIntervalMs);
        }

        private static void GameTickStep(object? state)
        {
            var sim = SimulationManager.Instance;
            if (!sim.IsRunning) return;

            lock (_lock)
            {
                float deltaTime = 1.0f / TargetFps;

                ProcessInputs();
                Update(deltaTime);
                Draw();
            }
        }

        private static void InputReaderLoop()
        {
            while (SimulationManager.Instance.IsRunning)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    lock (_lock)
                    {
                        _inputQueue.Enqueue(key);
                    }
                }
                Thread.Sleep(10);
            }
        }

        private static void ProcessInputs()
        {
            var sim = SimulationManager.Instance;

            while (_inputQueue.Count > 0)
            {
                var keyInfo = _inputQueue.Dequeue();

                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    sim.IsRunning = false;
                    return;
                }

                if (sim.RiotErupted)
                {
                    if (keyInfo.Key == ConsoleKey.R)
                    {
                        Initialize();
                    }
                    continue;
                }

                // PERSPECTIVE SWITCHING KEYS (1, 2, 3)
                if (keyInfo.Key == ConsoleKey.D1 || keyInfo.Key == ConsoleKey.NumPad1)
                {
                    sim.ActivePerspective = Perspective.PLATFORM;
                    FlashAlert("VIEW SWAPPED TO: PLATFORM CHAMBER", ConsoleColor.DarkBlue);
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.D2 || keyInfo.Key == ConsoleKey.NumPad2)
                {
                    sim.ActivePerspective = Perspective.UNDER_STATION;
                    FlashAlert("VIEW SWAPPED TO: UNDER-STATION COMMERCE", ConsoleColor.DarkYellow);
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.D3 || keyInfo.Key == ConsoleKey.NumPad3)
                {
                    sim.ActivePerspective = Perspective.INSIDE_CARS;
                    FlashAlert("VIEW SWAPPED TO: TRAIN CARRIAGE INTERIOR", ConsoleColor.DarkGray);
                    continue;
                }

                // ============================================================
                // Week 3 Spec 2: TACTICAL INTERVENTIONS (UNIVERSAL HOTKEYS)
                // ============================================================

                // [F] Deploy Industrial Fans - Costs $150 (PLATFORM or INSIDE_CARS only)
                if (keyInfo.Key == ConsoleKey.F)
                {
                    if (sim.ActivePerspective == Perspective.PLATFORM || sim.ActivePerspective == Perspective.INSIDE_CARS)
                    {
                        if (sim.DailyBudget >= 150.0f && sim.FanCooldownTimer <= 0.0f)
                        {
                            sim.DailyBudget -= 150.0f;
                            sim.FanCooldownTimer = 10.0f; // 10 seconds duration
                            if (sim.ActivePerspective == Perspective.PLATFORM)
                            {
                                sim.FanActiveOnPlatform = true;
                                FlashAlert("🌀 INDUSTRIAL FANS DEPLOYED ON PLATFORM! Rage -50% for 10s (-$150)", ConsoleColor.Green);
                            }
                            else
                            {
                                sim.FanActiveOnCars = true;
                                FlashAlert("🌀 INDUSTRIAL FANS DEPLOYED IN CARS! Rage -50% for 10s (-$150)", ConsoleColor.Green);
                            }
                        }
                        else if (sim.DailyBudget < 150.0f)
                        {
                            FlashAlert("⛔ INSUFFICIENT BUDGET FOR FAN DEPLOYMENT! Need $150", ConsoleColor.Red);
                        }
                        else
                        {
                            FlashAlert("⏳ FANS ALREADY ACTIVE! Wait for cooldown...", ConsoleColor.DarkYellow);
                        }
                    }
                    else
                    {
                        FlashAlert("⛔ FANS UNAVAILABLE: Switch to Platform or Cars view!", ConsoleColor.Red);
                    }
                    continue;
                }

                // [G] Deploy Security Guard - Costs $300 (ANY view)
                if (keyInfo.Key == ConsoleKey.G)
                {
                    if (sim.DailyBudget >= 300.0f)
                    {
                        sim.DailyBudget -= 300.0f;

                        switch (sim.ActivePerspective)
                        {
                            case Perspective.UNDER_STATION:
                                // Fix 1 broken ticket machine
                                if (sim.TicketMachineFailures > 0)
                                {
                                    sim.TicketMachineFailures--;
                                    // Unfreeze passengers
                                    foreach (var agent in sim.Commuters)
                                    {
                                        if (agent.IsFrozen && agent.CurrentPerspective == Perspective.UNDER_STATION)
                                        {
                                            agent.IsFrozen = false;
                                        }
                                    }
                                    FlashAlert("🛡️ SECURITY GUARD REPAIRED TICKET MACHINE! (-$300)", ConsoleColor.Green);
                                }
                                else
                                {
                                    FlashAlert("🛡️ SECURITY GUARD DEPLOYED - NO MACHINES DOWN (-$300)", ConsoleColor.DarkYellow);
                                }
                                break;

                            case Perspective.PLATFORM:
                                // Remove ALL active pickpockets
                                int removed = 0;
                                foreach (var agent in sim.Commuters)
                                {
                                    if (agent.IsPickpocket && agent.CurrentPerspective == Perspective.PLATFORM)
                                    {
                                        agent.IsPickpocket = false;
                                        removed++;
                                    }
                                }
                                sim.PickpocketCount = 0;
                                FlashAlert($"🛡️ SECURITY SWEEP! {removed} pickpockets neutralized! (-$300)", ConsoleColor.Green);
                                break;

                            case Perspective.INSIDE_CARS:
                                // Inside cars: reduce individual rage of all passengers
                                foreach (var agent in sim.Commuters)
                                {
                                    if (agent.CurrentPerspective == Perspective.INSIDE_CARS)
                                    {
                                        agent.IndividualRage = Math.Max(0.0f, agent.IndividualRage - 15.0f);
                                    }
                                }
                                FlashAlert("🛡️ SECURITY GUARD CALMED PASSENGERS IN TRAIN! (-$300)", ConsoleColor.Green);
                                break;
                        }
                    }
                    else
                    {
                        FlashAlert("⛔ INSUFFICIENT BUDGET FOR SECURITY GUARD! Need $300", ConsoleColor.Red);
                    }
                    continue;
                }

                // [E] Vanguard Security Push - Costs $50 (PLATFORM only, forces 5 extra onto train)
                if (keyInfo.Key == ConsoleKey.E)
                {
                    if (sim.ActivePerspective == Perspective.PLATFORM)
                    {
                        if (sim.DailyBudget >= 50.0f)
                        {
                            sim.DailyBudget -= 50.0f;
                            Random rng = new Random();
                            int pushed = 0;
                            for (int i = sim.Commuters.Count - 1; i >= 0 && pushed < 5; i--)
                            {
                                var agent = sim.Commuters[i];
                                if (agent.CurrentPerspective == Perspective.PLATFORM)
                                {
                                    agent.CurrentPerspective = Perspective.INSIDE_CARS;
                                    agent.Position = new Vector2(ViewX + 10 + rng.Next(ViewW - 20), ViewY + ViewH - 3);
                                    agent.TargetPosition = new Vector2(ViewX + 5 + rng.Next(ViewW - 10), ViewY + 11);
                                    agent.IndividualRage += 15.0f; // Forced push increases rage
                                    pushed++;
                                }
                            }
                            FlashAlert($"⚡ VANGUARD PUSH! {pushed} passengers forced into cars! (-$50)", ConsoleColor.DarkYellow);
                        }
                        else
                        {
                            FlashAlert("⛔ INSUFFICIENT BUDGET FOR SECURITY PUSH! Need $50", ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        FlashAlert("⛔ SECURITY PUSH ONLY AVAILABLE ON PLATFORM VIEW!", ConsoleColor.Red);
                    }
                    continue;
                }

                // EXISTING PERSPECTIVE-SPECIFIC CONTROLS
                switch (sim.ActivePerspective)
                {
                    case Perspective.PLATFORM:
                        if (keyInfo.Key == ConsoleKey.D)
                        {
                            // Deploy train
                            sim.TrainDelayTimer = 0.0f;

                            // Board passengers nearest the tracks from Platform to Inside Cars
                            int boarded = 0;
                            bool priorityBoarded = false;
                            bool normalBoardedFirst = false;
                            Random rng = new Random();

                            // Check for priority queue violations: find if priority passengers exist
                            bool hasPriorityWaiting = false;
                            foreach (var agent in sim.Commuters)
                            {
                                if (agent.CurrentPerspective == Perspective.PLATFORM && agent.IsPriority && agent.Position.Y <= ViewY + 9)
                                {
                                    hasPriorityWaiting = true;
                                    break;
                                }
                            }

                            for (int i = sim.Commuters.Count - 1; i >= 0; i--)
                            {
                                var agent = sim.Commuters[i];
                                if (agent.CurrentPerspective == Perspective.PLATFORM && agent.Position.Y <= ViewY + 9)
                                {
                                    // Week 3: Priority queue violation check
                                    if (!agent.IsPriority && hasPriorityWaiting && !priorityBoarded)
                                    {
                                        normalBoardedFirst = true;
                                    }
                                    if (agent.IsPriority) priorityBoarded = true;

                                    agent.CurrentPerspective = Perspective.INSIDE_CARS;
                                    agent.IsPickpocket = false; // Clear pickpocket status on boarding
                                    agent.Position = new Vector2(ViewX + 10 + rng.Next(ViewW - 20), ViewY + ViewH - 3);
                                    agent.TargetPosition = new Vector2(ViewX + 5 + rng.Next(ViewW - 10), ViewY + 11);
                                    boarded++;
                                    if (boarded >= 8) break;
                                }
                            }

                            // Week 3: Priority queue violation penalty
                            if (normalBoardedFirst && hasPriorityWaiting)
                            {
                                sim.GlobalCommuterRage = Math.Min(100.0f, sim.GlobalCommuterRage + 5.0f);
                                sim.PriorityQueueViolationRate = Math.Min(1.0f, sim.PriorityQueueViolationRate + 0.1f);
                            }

                            // Transport away passengers already seated inside cars
                            int transported = 0;
                            for (int i = sim.Commuters.Count - 1; i >= 0; i--)
                            {
                                var agent = sim.Commuters[i];
                                if (agent.CurrentPerspective == Perspective.INSIDE_CARS && agent.Position.Y <= ViewY + 11.5f)
                                {
                                    sim.Commuters.RemoveAt(i);
                                    transported++;
                                }
                            }

                            // Week 3 Spec 3: Revenue generation (+$15 per passenger transported)
                            float fareRevenue = transported * 15.0f;
                            sim.DailyBudget += fareRevenue;
                            sim.TotalPassengersTransported += transported;
                            sim.TotalFareRevenue += fareRevenue;

                            float netCost = 100.0f - fareRevenue;
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 100.0f);

                            FlashAlert($"🚄 MRT DEPLOYED! +{boarded} boarded, {transported} transported (+${fareRevenue:F0} fares, net {(netCost > 0 ? "-" : "+")}${Math.Abs(netCost):F0})", ConsoleColor.Green);
                        }
                        else if (keyInfo.Key == ConsoleKey.P)
                        {
                            // Bust individual pickpocket
                            bool found = false;
                            for (int i = 0; i < sim.Commuters.Count; i++)
                            {
                                if (sim.Commuters[i].IsPickpocket && sim.Commuters[i].CurrentPerspective == Perspective.PLATFORM)
                                {
                                    sim.Commuters[i].IsPickpocket = false;
                                    sim.PickpocketCount = Math.Max(0, sim.PickpocketCount - 1);
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                            {
                                FlashAlert("🔒 PICKPOCKET ARRESTED! (-$50)", ConsoleColor.Green);
                                sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 50.0f);
                            }
                            else
                            {
                                FlashAlert("No active pickpockets detected on platform.", ConsoleColor.DarkYellow);
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.Q)
                        {
                            sim.PriorityQueueViolationRate = Math.Max(0.0f, sim.PriorityQueueViolationRate - 0.15f);
                            FlashAlert("GUARDRADIUS ASSIGNED TO PRIORITY BOARDING GATE (-$30)", ConsoleColor.Green);
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 30.0f);
                        }
                        break;

                    case Perspective.UNDER_STATION:
                        if (keyInfo.Key == ConsoleKey.S)
                        {
                            sim.EscalatorWeightStrain = Math.Max(0.0f, sim.EscalatorWeightStrain - 25.0f);
                            FlashAlert("ESCALATOR LOAD REDISTRIBUTED / SPEED RESET (-$40)", ConsoleColor.Green);
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 40.0f);
                        }
                        break;

                    case Perspective.INSIDE_CARS:
                        if (keyInfo.Key == ConsoleKey.A)
                        {
                            sim.ACFailed = false;
                            sim.ACFailureChance = 0.05f;
                            sim.ACBreakdownTimer = 25.0f; // Reset AC timer
                            FlashAlert("❄️ CARRIAGE AC RESTORED! Temperature normalizing. (-$150)", ConsoleColor.Green);
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 150.0f);
                        }
                        break;
                }
            }
        }

        private static void Update(float deltaTime)
        {
            var sim = SimulationManager.Instance;

            if (sim.RiotErupted)
            {
                Random rng = new Random();
                _screenOffset = new Vector2(rng.Next(-4, 5), rng.Next(-2, 3));
                return;
            }

            _frameCount++;

            // ====================================================
            // ENTITY SPAWNING (1.5 to 3 seconds)
            // ====================================================
            sim.SpawnerTimer -= deltaTime;
            if (sim.SpawnerTimer <= 0.0f)
            {
                Random rng = new Random();
                var newAgent = new CommuterAgent
                {
                    Position = new Vector2(ViewX + 1, ViewY + 7 + rng.Next(-2, 3)),
                    CurrentPerspective = Perspective.UNDER_STATION,
                    MovementSpeed = 3.5f + (float)rng.NextDouble() * 3.5f,
                    IndividualRage = 0.0f,
                    IsPriority = rng.NextDouble() < 0.15,
                    IsPickpocket = false,
                    IsFrozen = false
                };
                newAgent.TargetPosition = new Vector2(ViewX + ViewW - 22, ViewY + 5 + rng.Next(-1, 2));
                sim.Commuters.Add(newAgent);
                sim.SpawnerTimer = 1.5f + (float)rng.NextDouble() * 1.5f;
            }

            // Count agents per perspective
            int underStationCount = 0;
            int platformCount = 0;
            int insideCarsCount = 0;
            foreach (var agent in sim.Commuters)
            {
                if (agent.CurrentPerspective == Perspective.UNDER_STATION) underStationCount++;
                else if (agent.CurrentPerspective == Perspective.PLATFORM) platformCount++;
                else if (agent.CurrentPerspective == Perspective.INSIDE_CARS) insideCarsCount++;
            }

            // Dynamic density scaling
            sim.PlatformDensity = 0.5f + platformCount * 0.35f;
            if (sim.PlatformDensity > 10.0f) sim.PlatformDensity = 10.0f;

            sim.CarCrowdDensity = 0.5f + insideCarsCount * 0.35f;
            if (sim.CarCrowdDensity > 10.0f) sim.CarCrowdDensity = 10.0f;

            // Throttling checks
            bool underStationOverloaded = underStationCount > 10;
            bool platformOverloaded = sim.PlatformDensity > 5.0f;
            bool insideCarsOverloaded = sim.CarCrowdDensity > 7.5f;

            // ====================================================
            // Week 3 Spec 1: PERSPECTIVE-SPECIFIC CRISES
            // ====================================================

            // --- UNDER_STATION: Ticket Machine Breakdown Cycle ---
            sim.TicketMachineBreakTimer -= deltaTime;
            if (sim.TicketMachineBreakTimer <= 0.0f && sim.TicketMachineFailures < 6)
            {
                sim.TicketMachineFailures++;
                Random rng = new Random();
                sim.TicketMachineBreakTimer = 15.0f + (float)rng.NextDouble() * 5.0f;

                // Freeze some concourse passengers
                int frozenCount = 0;
                foreach (var agent in sim.Commuters)
                {
                    if (agent.CurrentPerspective == Perspective.UNDER_STATION && !agent.IsFrozen && frozenCount < 3)
                    {
                        agent.IsFrozen = true;
                        frozenCount++;
                    }
                }

                FlashAlert($"⚠️ TICKET MACHINE #{sim.TicketMachineFailures} BROKE DOWN! {frozenCount} passengers stuck!", ConsoleColor.Red);
            }

            // --- PLATFORM: Pickpocket spawning from existing passengers ---
            sim.PickpocketSpawnTimer -= deltaTime;
            if (sim.PickpocketSpawnTimer <= 0.0f)
            {
                Random rng = new Random();
                sim.PickpocketSpawnTimer = 10.0f + (float)rng.NextDouble() * 5.0f;

                // Flag a random non-priority platform passenger as pickpocket
                List<int> candidates = new List<int>();
                for (int i = 0; i < sim.Commuters.Count; i++)
                {
                    var a = sim.Commuters[i];
                    if (a.CurrentPerspective == Perspective.PLATFORM && !a.IsPriority && !a.IsPickpocket)
                    {
                        candidates.Add(i);
                    }
                }
                if (candidates.Count > 0)
                {
                    int idx = candidates[rng.Next(candidates.Count)];
                    sim.Commuters[idx].IsPickpocket = true;
                    sim.PickpocketCount++;
                }
            }

            // Pickpocket proximity rage spike: check if pickpocket is near another passenger
            for (int i = 0; i < sim.Commuters.Count; i++)
            {
                var thief = sim.Commuters[i];
                if (!thief.IsPickpocket || thief.CurrentPerspective != Perspective.PLATFORM)
                    continue;

                for (int j = 0; j < sim.Commuters.Count; j++)
                {
                    if (i == j) continue;
                    var victim = sim.Commuters[j];
                    if (victim.CurrentPerspective != Perspective.PLATFORM) continue;

                    float dist = Vector2.Distance(thief.Position, victim.Position);
                    if (dist < 3.0f)
                    {
                        // Immediate +10 rage spike (scaled by deltaTime so it doesn't stack per-frame)
                        sim.GlobalCommuterRage = Math.Min(100.0f, sim.GlobalCommuterRage + 10.0f * deltaTime);
                        victim.IndividualRage += 5.0f * deltaTime;
                        break; // Only one victim per frame per thief
                    }
                }
            }

            // --- INSIDE_CARS: AC Failure Cycle (every ~25 seconds) ---
            if (!sim.ACFailed)
            {
                sim.ACBreakdownTimer -= deltaTime;
                if (sim.ACBreakdownTimer <= 0.0f)
                {
                    sim.ACFailed = true;
                    sim.ACBreakdownTimer = 25.0f;
                    FlashAlert("🔥 AC COMPRESSOR FAILURE! Temperature rising rapidly!", ConsoleColor.Red);
                }
            }

            // AC failed rage: 1.5x per second per agent trapped inside
            if (sim.ACFailed)
            {
                float acRage = 1.5f * insideCarsCount * deltaTime;
                sim.GlobalCommuterRage = Math.Min(100.0f, sim.GlobalCommuterRage + acRage);
            }

            // ====================================================
            // Week 3: Fan Cooldown Timer
            // ====================================================
            if (sim.FanCooldownTimer > 0.0f)
            {
                sim.FanCooldownTimer -= deltaTime;
                if (sim.FanCooldownTimer <= 0.0f)
                {
                    sim.FanActiveOnPlatform = false;
                    sim.FanActiveOnCars = false;
                    sim.FanCooldownTimer = 0.0f;
                }
            }

            // ====================================================
            // AGENT MOVEMENT with crisis integration
            // ====================================================
            for (int i = sim.Commuters.Count - 1; i >= 0; i--)
            {
                var agent = sim.Commuters[i];

                // Frozen agents don't move (ticket machine backlog)
                if (agent.IsFrozen)
                {
                    // If all machines are fixed, unfreeze
                    if (sim.TicketMachineFailures == 0)
                    {
                        agent.IsFrozen = false;
                    }
                    continue;
                }

                float multiplier = 1.0f;
                if (agent.CurrentPerspective == Perspective.UNDER_STATION && (underStationOverloaded || sim.TicketMachineFailures > 0))
                    multiplier = 0.30f;
                else if (agent.CurrentPerspective == Perspective.PLATFORM && platformOverloaded)
                    multiplier = 0.30f;
                else if (agent.CurrentPerspective == Perspective.INSIDE_CARS && insideCarsOverloaded)
                    multiplier = 0.30f;

                Vector2 dir = agent.TargetPosition - agent.Position;
                float dist = dir.Length();
                if (dist > 0.3f)
                {
                    Vector2 norm = Vector2.Normalize(dir);
                    agent.Position += norm * agent.MovementSpeed * multiplier * deltaTime;
                }
                else
                {
                    if (agent.CurrentPerspective == Perspective.UNDER_STATION)
                    {
                        agent.CurrentPerspective = Perspective.PLATFORM;
                        Random rng = new Random();
                        int targetX = ViewX + 4 + rng.Next(ViewW - 12);
                        int targetY = ViewY + 8;
                        agent.Position = new Vector2(targetX, ViewY + ViewH - 3);
                        agent.TargetPosition = new Vector2(targetX, targetY);
                    }
                }
            }

            // Passive metrics
            sim.TrainDelayTimer += deltaTime;
            sim.PriorityQueueViolationRate = Math.Min(1.0f, sim.PriorityQueueViolationRate + 0.015f * deltaTime);
            sim.EscalatorWeightStrain = Math.Min(100.0f, sim.EscalatorWeightStrain + (2.5f + sim.TicketMachineFailures * 1.5f) * deltaTime);

            // --- EXPONENTIAL RAGE CALCULATIONS ---
            float rageAddition = 0.0f;

            if (sim.TrainDelayTimer > 15.0f)
            {
                float excess = sim.TrainDelayTimer - 15.0f;
                float rageCalc = MathF.Exp(excess * 0.15f) * 0.35f * deltaTime;
                if (sim.FanActiveOnPlatform) rageCalc *= 0.5f; // Fan halves rage
                rageAddition += rageCalc;
            }

            if (sim.EscalatorWeightStrain > 75.0f)
            {
                float excess = sim.EscalatorWeightStrain - 75.0f;
                rageAddition += MathF.Exp(excess * 0.09f) * 0.45f * deltaTime;
            }

            if (sim.CarCrowdDensity > 8.0f)
            {
                float excess = sim.CarCrowdDensity - 8.0f;
                float rageCalc = MathF.Exp(excess * 0.5f) * 0.65f * deltaTime;
                if (sim.FanActiveOnCars) rageCalc *= 0.5f; // Fan halves rage
                rageAddition += rageCalc;
            }

            rageAddition += sim.PickpocketCount * 0.25f * deltaTime;
            rageAddition += sim.TicketMachineFailures * 0.4f * deltaTime;

            sim.GlobalCommuterRage += rageAddition;

            // Cooling if all systems nominal
            if (sim.TrainDelayTimer <= 15.0f && sim.EscalatorWeightStrain <= 75.0f
                && sim.CarCrowdDensity <= 8.0f && !sim.ACFailed && sim.PickpocketCount == 0)
            {
                sim.GlobalCommuterRage = Math.Max(0.0f, sim.GlobalCommuterRage - 1.5f * deltaTime);
            }

            if (sim.GlobalCommuterRage > 100.0f) sim.GlobalCommuterRage = 100.0f;
            if (sim.GlobalCommuterRage >= 100.0f)
            {
                sim.RiotErupted = true;
            }

            // Budget operational costs
            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 8.0f * deltaTime);

            // ====================================================
            // Week 3 Spec 4: DYNAMIC CRISIS ALERT STRINGS
            // ====================================================
            _crisisAlert1 = "";
            _crisisAlert2 = "";
            _crisisColor1 = ConsoleColor.Red;
            _crisisColor2 = ConsoleColor.Red;

            switch (sim.ActivePerspective)
            {
                case Perspective.PLATFORM:
                    if (sim.PickpocketCount > 0)
                    {
                        _crisisAlert1 = $"⚠️ {sim.PickpocketCount} PICKPOCKET(S) ACTIVE!";
                        _crisisColor1 = (_frameCount % 10 < 5) ? ConsoleColor.Red : ConsoleColor.Yellow;
                    }
                    if (sim.TrainDelayTimer > 15.0f)
                    {
                        _crisisAlert2 = "⚠️ TRAIN DELAY CRITICAL!";
                        _crisisColor2 = ConsoleColor.Red;
                    }
                    if (sim.FanActiveOnPlatform)
                    {
                        _crisisAlert2 = $"🌀 FANS ACTIVE ({sim.FanCooldownTimer:F0}s)";
                        _crisisColor2 = ConsoleColor.Cyan;
                    }
                    break;

                case Perspective.UNDER_STATION:
                    if (sim.TicketMachineFailures > 0)
                    {
                        _crisisAlert1 = $"⚠️ TICKET MACHINE DOWN: {sim.TicketMachineFailures}";
                        _crisisColor1 = (_frameCount % 10 < 5) ? ConsoleColor.Red : ConsoleColor.Yellow;
                    }
                    if (sim.EscalatorWeightStrain > 75.0f)
                    {
                        _crisisAlert2 = "⚠️ ESCALATOR OVERLOAD!";
                        _crisisColor2 = ConsoleColor.Red;
                    }
                    break;

                case Perspective.INSIDE_CARS:
                    if (sim.ACFailed)
                    {
                        _crisisAlert1 = "⚠️ AC SYSTEM FAILURE DETECTED!";
                        _crisisColor1 = (_frameCount % 8 < 4) ? ConsoleColor.Red : ConsoleColor.Yellow;
                    }
                    if (sim.CarCrowdDensity > 8.0f)
                    {
                        _crisisAlert2 = "⚠️ CAR OVERLOADED!";
                        _crisisColor2 = ConsoleColor.Red;
                    }
                    if (sim.FanActiveOnCars)
                    {
                        _crisisAlert2 = $"🌀 FANS ACTIVE ({sim.FanCooldownTimer:F0}s)";
                        _crisisColor2 = ConsoleColor.Cyan;
                    }
                    break;
            }

            // Notification clears
            if (_alertTicksRemaining > 0)
            {
                _alertTicksRemaining--;
                if (_alertTicksRemaining <= 0)
                {
                    _alertMessage = "[1]Platform [2]Under-Station [3]Cars | [F]Fan [G]Guard [E]Push [D]Train";
                    _alertBg = ConsoleColor.DarkGray;
                }
            }
        }

        private static void ClearBuffer()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    _buffer[x, y] = new ConsoleCell(' ', ConsoleColor.White, ConsoleColor.Black);
                }
            }
        }

        private static void Draw()
        {
            ClearBuffer();

            var sim = SimulationManager.Instance;

            // Draw Outer Panel borders
            DrawRect(0, 0, Width, Height, '▒', ConsoleColor.DarkGray, ConsoleColor.Black);

            // ==========================================
            // PERSISTENT GLOBAL HUD (4 rows)
            // ==========================================
            DrawRect(1, 1, Width - 2, 4, '═', ConsoleColor.DarkCyan, ConsoleColor.Black);
            
            string viewName = sim.ActivePerspective switch
            {
                Perspective.PLATFORM => "[PLATFORM]",
                Perspective.UNDER_STATION => "[UNDER-STATION]",
                Perspective.INSIDE_CARS => "[INSIDE CARS]",
                _ => "[SYSTEM]"
            };
            DrawString(3, 2, $"{viewName} Budget:${sim.DailyBudget:F0} Fares:${sim.TotalFareRevenue:F0} Transported:{sim.TotalPassengersTransported}", ConsoleColor.Yellow, ConsoleColor.Black);
            
            // Rage meter
            ConsoleColor rageColor = sim.GlobalCommuterRage > 80.0f ? ConsoleColor.Red : (sim.GlobalCommuterRage > 50.0f ? ConsoleColor.Yellow : ConsoleColor.Green);
            DrawString(3, 3, "Rage:", ConsoleColor.White, ConsoleColor.Black);
            DrawProgressBar(9, 3, 18, sim.GlobalCommuterRage / 100.0f, rageColor);
            DrawString(29, 3, $"{sim.GlobalCommuterRage:F1}%", rageColor, ConsoleColor.Black);

            // Week 3: Dynamic crisis alerts in HUD row
            if (!string.IsNullOrEmpty(_crisisAlert1))
            {
                DrawString(38, 3, _crisisAlert1, _crisisColor1, ConsoleColor.Black);
            }
            if (!string.IsNullOrEmpty(_crisisAlert2))
            {
                DrawString(38, 2, _crisisAlert2, _crisisColor2, ConsoleColor.Black);
            }

            // ==========================================
            // DYNAMIC VIEWPORT CANVAS
            // ==========================================
            switch (sim.ActivePerspective)
            {
                case Perspective.PLATFORM:
                    RenderPlatformView(ViewX, ViewY, ViewW, ViewH, sim);
                    break;
                case Perspective.UNDER_STATION:
                    RenderUnderStationView(ViewX, ViewY, ViewW, ViewH, sim);
                    break;
                case Perspective.INSIDE_CARS:
                    RenderInsideCarsView(ViewX, ViewY, ViewW, ViewH, sim);
                    break;
            }

            // ====================================================
            // GRAPHICAL REPRESENTATION OF AGENTS (Week 2+3)
            // ====================================================
            foreach (var agent in sim.Commuters)
            {
                if (agent.CurrentPerspective == sim.ActivePerspective)
                {
                    int ax = (int)Math.Round(agent.Position.X);
                    int ay = (int)Math.Round(agent.Position.Y);

                    if (ax > ViewX && ax < ViewX + ViewW - 1 && ay > ViewY && ay < ViewY + ViewH - 1)
                    {
                        char cSymbol;
                        ConsoleColor cCol;

                        if (agent.IsPickpocket)
                        {
                            cSymbol = '⚡';  // Week 3: Pickpocket shown as lightning bolt
                            cCol = ConsoleColor.Red;
                        }
                        else if (agent.IsFrozen)
                        {
                            cSymbol = '■';   // Week 3: Frozen passenger (stuck at ticket)
                            cCol = ConsoleColor.DarkYellow;
                        }
                        else if (agent.IsPriority)
                        {
                            cSymbol = '♀';
                            cCol = ConsoleColor.Magenta;
                        }
                        else
                        {
                            cSymbol = '☺';
                            cCol = ConsoleColor.Cyan;
                        }

                        // Rage-tinted coloring: passengers with high individual rage turn redder
                        if (agent.IndividualRage > 25.0f)
                        {
                            cCol = ConsoleColor.DarkRed;
                        }
                        else if (agent.IndividualRage > 10.0f)
                        {
                            cCol = ConsoleColor.DarkYellow;
                        }

                        DrawChar(ax, ay, cSymbol, cCol, GetViewportBg(sim.ActivePerspective));
                    }
                }
            }

            // Operations Alert Bar (bottom layer)
            FillRect(1, Height - 2, Width - 2, 1, ' ', ConsoleColor.White, _alertBg);
            string spacer = new string(' ', Math.Max(0, ((Width - 2) - _alertMessage.Length)/2));
            DrawString(1 + spacer.Length, Height - 2, _alertMessage, ConsoleColor.White, _alertBg);

            // Loss State Overlay
            if (sim.RiotErupted)
            {
                RenderGameOverPanel();
            }

            // Flush Grid
            RenderBufferToConsole();
        }

        private static ConsoleColor GetViewportBg(Perspective view)
        {
            return view switch
            {
                Perspective.PLATFORM => ConsoleColor.DarkGray,
                Perspective.UNDER_STATION => ConsoleColor.DarkCyan,
                Perspective.INSIDE_CARS => ConsoleColor.DarkBlue,
                _ => ConsoleColor.Black
            };
        }

        private static void RenderPlatformView(int x, int y, int w, int h, SimulationManager sim)
        {
            FillRect(x, y, w, h, ' ', ConsoleColor.White, ConsoleColor.DarkGray);
            DrawRect(x, y, w, h, '█', ConsoleColor.Gray, ConsoleColor.DarkGray);

            // Ceiling support columns
            for (int colX = x + 10; colX < x + w - 10; colX += 20)
            {
                FillRect(colX, y + 1, 3, 2, '█', ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                DrawString(colX - 1, y + 3, "[COL]", ConsoleColor.Black, ConsoleColor.DarkGray);
            }

            // Railway tracks
            int trackY = y + 4;
            for (int dx = x + 1; dx < x + w - 1; dx++)
            {
                DrawChar(dx, trackY, '=', ConsoleColor.White, ConsoleColor.DarkGray);
                DrawChar(dx, trackY + 2, '=', ConsoleColor.White, ConsoleColor.DarkGray);
            }
            for (int dx = x + 3; dx < x + w - 2; dx += 5)
            {
                DrawString(dx, trackY + 1, "|-|", ConsoleColor.DarkRed, ConsoleColor.DarkGray);
            }

            // Scrolling train
            int trainOffset = (_frameCount * 2) % (w - 15);
            int trainX = x + 1 + trainOffset;
            DrawString(trainX, trackY,     " _________________ ", ConsoleColor.Blue, ConsoleColor.DarkGray);
            DrawString(trainX, trackY + 1, "[= = = MRT-3 = = =]►", ConsoleColor.Blue, ConsoleColor.DarkGray);
            DrawString(trainX, trackY + 2, " O-O           O-O ", ConsoleColor.Black, ConsoleColor.DarkGray);

            // Yellow safety caution line
            int safetyY = trackY + 3;
            for (int dx = x + 1; dx < x + w - 1; dx++)
            {
                char c = (dx + _frameCount / 4) % 2 == 0 ? '▒' : '■';
                DrawChar(dx, safetyY, c, ConsoleColor.Yellow, ConsoleColor.DarkGray);
            }

            // Fan visual indicator
            if (sim.FanActiveOnPlatform)
            {
                char fanChar = (_frameCount % 4) switch { 0 => '/', 1 => '-', 2 => '\\', _ => '|' };
                for (int fx = x + 5; fx < x + w - 5; fx += 12)
                {
                    DrawChar(fx, y + 9, fanChar, ConsoleColor.Cyan, ConsoleColor.DarkGray);
                    DrawChar(fx + 1, y + 9, '~', ConsoleColor.Cyan, ConsoleColor.DarkGray);
                }
            }

            // Stats overlay
            DrawString(x + 3, y + 1, " ═ PLATFORM CAMERA ═ ", ConsoleColor.Yellow, ConsoleColor.DarkGray);

            ConsoleColor delayCol = sim.TrainDelayTimer > 15.0f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(x + 3, y + 10, $"Delay:{sim.TrainDelayTimer:F1}s", delayCol, ConsoleColor.DarkGray);

            ConsoleColor pickColor = sim.PickpocketCount > 0 ? ConsoleColor.Red : ConsoleColor.White;
            DrawString(x + 22, y + 10, $"Thieves:{sim.PickpocketCount}", pickColor, ConsoleColor.DarkGray);

            ConsoleColor viColor = sim.PriorityQueueViolationRate > 0.4f ? ConsoleColor.Yellow : ConsoleColor.White;
            DrawString(x + 38, y + 10, $"QueueViol:{(sim.PriorityQueueViolationRate * 100f):F0}%", viColor, ConsoleColor.DarkGray);

            DrawString(x + w - 20, y + 1, "Enforcer: ONLINE", ConsoleColor.Green, ConsoleColor.DarkGray);

            // Hotkeys (updated for Week 3)
            DrawString(x + 2, y + h - 2, "[D]Train [P]Bust [Q]Queue [F]Fan [G]Guard [E]Push", ConsoleColor.Cyan, ConsoleColor.DarkGray);
        }

        private static void RenderUnderStationView(int x, int y, int w, int h, SimulationManager sim)
        {
            FillRect(x, y, w, h, ' ', ConsoleColor.White, ConsoleColor.DarkCyan);
            DrawRect(x, y, w, h, '█', ConsoleColor.Cyan, ConsoleColor.DarkCyan);

            // Support pillars
            for (int dx = x + 8; dx < x + w - 5; dx += 18)
            {
                FillRect(dx, y + 1, 4, h - 4, '▒', ConsoleColor.Gray, ConsoleColor.DarkCyan);
            }

            // Ticket Vending Machines with crisis status
            int tvmX = x + 3;
            for (int k = 0; k < 4; k++)
            {
                int curTvmX = tvmX + (k * 10);
                DrawRect(curTvmX, y + 3, 7, 5, '█', ConsoleColor.Black, ConsoleColor.DarkCyan);
                DrawString(curTvmX + 2, y + 3, "[ ]", ConsoleColor.Green, ConsoleColor.DarkCyan);

                if (k < sim.TicketMachineFailures)
                {
                    // Week 3: Flashing FAIL indicator
                    ConsoleColor failCol = (_frameCount % 10 < 5) ? ConsoleColor.Red : ConsoleColor.DarkRed;
                    DrawString(curTvmX + 1, y + 4, "$$ERR$$", failCol, ConsoleColor.Black);
                    DrawString(curTvmX + 1, y + 6, " FAIL! ", ConsoleColor.Red, ConsoleColor.Black);
                }
                else
                {
                    DrawString(curTvmX + 1, y + 4, "$$TVM$$", ConsoleColor.Cyan, ConsoleColor.Black);
                    DrawString(curTvmX + 2, y + 6, "O K ", ConsoleColor.Green, ConsoleColor.Black);
                }
            }

            // Escalator with animated steps
            int escX = x + w - 24;
            DrawString(escX, y + 2, "=== Escalator ===", ConsoleColor.Yellow, ConsoleColor.DarkCyan);
            DrawString(escX, y + 3, " [Down]    [Up]  ", ConsoleColor.White, ConsoleColor.DarkCyan);

            int animOffset = _frameCount % 3;
            for (int k = 0; k < 6; k++)
            {
                int ly = y + 4 + k;
                int lx = escX + (k * 2);
                char stepCharL = (k + animOffset) % 3 == 0 ? '█' : '▒';
                DrawChar(lx, ly, stepCharL, ConsoleColor.Gray, ConsoleColor.DarkCyan);

                int ry = y + 9 - k;
                int rx = escX + 10 + (k * 2);
                char stepCharR = (k + (3 - animOffset)) % 3 == 0 ? '█' : '▒';
                DrawChar(rx, ry, stepCharR, ConsoleColor.Gray, ConsoleColor.DarkCyan);
            }

            // Frozen passenger queue indicator
            int frozenCount = 0;
            foreach (var agent in sim.Commuters)
            {
                if (agent.IsFrozen && agent.CurrentPerspective == Perspective.UNDER_STATION)
                    frozenCount++;
            }

            DrawString(x + 3, y + 1, " ═ UNDER-STATION CONCOURSE ═ ", ConsoleColor.Yellow, ConsoleColor.DarkCyan);

            ConsoleColor tColor = sim.TicketMachineFailures > 0 ? ConsoleColor.Red : ConsoleColor.White;
            DrawString(x + 3, y + 9, $"Machines Down: {sim.TicketMachineFailures}/4", tColor, ConsoleColor.DarkCyan);

            if (frozenCount > 0)
            {
                ConsoleColor frzCol = (_frameCount % 8 < 4) ? ConsoleColor.DarkYellow : ConsoleColor.Yellow;
                DrawString(x + 25, y + 9, $"Stuck: {frozenCount} commuters", frzCol, ConsoleColor.DarkCyan);
            }

            ConsoleColor strainColor = sim.EscalatorWeightStrain > 75.0f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(x + 3, y + 11, $"Escalator Strain: {sim.EscalatorWeightStrain:F1}%", strainColor, ConsoleColor.DarkCyan);

            if (sim.EscalatorWeightStrain > 75.0f)
            {
                DrawString(x + 40, y + 11, "🚨 OVERLOAD!", ConsoleColor.Red, ConsoleColor.DarkCyan);
            }

            // Hotkeys (updated for Week 3)
            DrawString(x + 2, y + h - 2, "[G]Guard(fix machine -$300) | [S]Escalator(-$40)", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
        }

        private static void RenderInsideCarsView(int x, int y, int w, int h, SimulationManager sim)
        {
            FillRect(x, y, w, h, ' ', ConsoleColor.White, ConsoleColor.DarkBlue);
            DrawRect(x, y, w, h, '█', ConsoleColor.Blue, ConsoleColor.DarkBlue);

            // Carriage windows with scrolling tunnel
            int scrollX = (_frameCount / 2) % 15;
            for (int wx = x + 4; wx < x + w - 24; wx += 16)
            {
                DrawRect(wx, y + 2, 10, 4, '█', ConsoleColor.Cyan, ConsoleColor.DarkBlue);
                int px = wx + 1 + ((scrollX) % 8);
                if (px < wx + 9)
                {
                    DrawChar(px, y + 3, '║', ConsoleColor.Black, ConsoleColor.Cyan);
                    DrawChar(px, y + 4, '║', ConsoleColor.Black, ConsoleColor.Cyan);
                }
            }

            // Overhead handhold straps
            for (int hx = x + 3; hx < x + w - 4; hx += 6)
            {
                DrawChar(hx, y + 7, 'T', ConsoleColor.Gray, ConsoleColor.DarkBlue);
                DrawChar(hx, y + 8, 'O', ConsoleColor.Yellow, ConsoleColor.DarkBlue);
            }

            // Seat rows
            int seatStartX = x + 4;
            int seatStartY = y + 10;
            DrawString(seatStartX, seatStartY,     "|____[Seat Row A]____|     |____[Seat Row B]____|", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            DrawString(seatStartX, seatStartY + 1, "|[ ]  [ ]   [ ]  [ ]|     |[ ]  [ ]   [ ]  [ ]|", ConsoleColor.White, ConsoleColor.DarkBlue);

            // Fan visual indicator
            if (sim.FanActiveOnCars)
            {
                char fanChar = (_frameCount % 4) switch { 0 => '/', 1 => '-', 2 => '\\', _ => '|' };
                for (int fx = x + 5; fx < x + w - 5; fx += 10)
                {
                    DrawChar(fx, y + 9, fanChar, ConsoleColor.Cyan, ConsoleColor.DarkBlue);
                    DrawChar(fx + 1, y + 9, '~', ConsoleColor.Cyan, ConsoleColor.DarkBlue);
                }
            }

            // Stats overlay
            DrawString(x + 3, y + 1, " ═ METRO CARRIAGE STATUS ═ ", ConsoleColor.Yellow, ConsoleColor.DarkBlue);

            ConsoleColor denColor = sim.CarCrowdDensity > 8.0f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(x + 46, y + 2, $"Density:{sim.CarCrowdDensity:F1}/10", denColor, ConsoleColor.DarkBlue);
            if (sim.CarCrowdDensity > 8.0f)
            {
                DrawString(x + 46, y + 3, "[OVERLOADED]", ConsoleColor.Red, ConsoleColor.DarkBlue);
            }

            DrawString(x + 46, y + 5, $"AC: {(sim.ACFailed ? "38C FAIL" : "21C OK")}", sim.ACFailed ? ConsoleColor.Red : ConsoleColor.Green, ConsoleColor.DarkBlue);

            // Week 3: Flashing AC failure warning panel
            if (sim.ACFailed)
            {
                ConsoleColor acFlash = (_frameCount % 6 < 3) ? ConsoleColor.Red : ConsoleColor.DarkRed;
                FillRect(x + 46, y + 7, 24, 4, ' ', ConsoleColor.White, acFlash);
                DrawString(x + 48, y + 8, "⚠️ AC COMPRESSOR ⚠️", ConsoleColor.Yellow, acFlash);
                DrawString(x + 48, y + 9, " SYSTEM FAILURE! ", ConsoleColor.White, acFlash);
            }
            else
            {
                DrawString(x + 46, y + 7, "AC Compressor: OK", ConsoleColor.Green, ConsoleColor.DarkBlue);
            }

            // Hotkeys (updated for Week 3)
            DrawString(x + 2, y + h - 2, "[A]Fix AC(-$150) [F]Fan(-$150) [G]Guard(-$300)", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
        }

        private static void DrawProgressBar(int x, int y, int width, float percentage, ConsoleColor color)
        {
            int filled = (int)(percentage * width);
            DrawString(x, y, "[", ConsoleColor.DarkGray);
            for (int i = 0; i < width; i++)
            {
                char symbol = i < filled ? '█' : '░';
                ConsoleColor c = i < filled ? color : ConsoleColor.DarkGray;
                DrawChar(x + 1 + i, y, symbol, c);
            }
            DrawString(x + 1 + width, y, "]", ConsoleColor.DarkGray);
        }

        private static void RenderGameOverPanel()
        {
            int panelW = 66;
            int panelH = 9;
            int panelX = (Width - panelW) / 2;
            int panelY = (Height - panelH) / 2;

            FillRect(panelX, panelY, panelW, panelH, ' ', ConsoleColor.White, ConsoleColor.DarkRed);
            DrawRect(panelX, panelY, panelW, panelH, '█', ConsoleColor.Yellow, ConsoleColor.DarkRed);

            DrawString(panelX + 5, panelY + 2, "💥 !!! STATION RIOT TRIGGERED! YOU ARE FIRED. !!! 💥", ConsoleColor.Yellow, ConsoleColor.DarkRed);
            DrawString(panelX + 3, panelY + 4, $"Transported: {SimulationManager.Instance.TotalPassengersTransported} | Fares: ${SimulationManager.Instance.TotalFareRevenue:F0}", ConsoleColor.White, ConsoleColor.DarkRed);
            DrawString(panelX + 3, panelY + 5, "EDSA commutes collapsed. Rage hit 100%.", ConsoleColor.White, ConsoleColor.DarkRed);
            
            DrawString(panelX + 18, panelY + 7, "Press [ R ] to Restart Simulation", ConsoleColor.Yellow, ConsoleColor.DarkRed);
        }

        private static void FlashAlert(string text, ConsoleColor color)
        {
            _alertMessage = text;
            _alertBg = color;
            _alertTicksRemaining = 60;
        }

        private static void DrawChar(int x, int y, char c, ConsoleColor fg = ConsoleColor.White, ConsoleColor bg = ConsoleColor.Black)
        {
            int rx = x + (int)Math.Round(_screenOffset.X);
            int ry = y + (int)Math.Round(_screenOffset.Y);

            if (rx >= 0 && rx < Width && ry >= 0 && ry < Height)
            {
                _buffer[rx, ry] = new ConsoleCell(c, fg, bg);
            }
        }

        private static void DrawString(int x, int y, string text, ConsoleColor fg = ConsoleColor.White, ConsoleColor bg = ConsoleColor.Black)
        {
            for (int i = 0; i < text.Length; i++)
            {
                DrawChar(x + i, y, text[i], fg, bg);
            }
        }

        private static void DrawRect(int x, int y, int w, int h, char borderChar, ConsoleColor fg = ConsoleColor.White, ConsoleColor bg = ConsoleColor.Black)
        {
            for (int i = 0; i < w; i++)
            {
                DrawChar(x + i, y, borderChar, fg, bg);
                DrawChar(x + i, y + h - 1, borderChar, fg, bg);
            }
            for (int j = 0; j < h; j++)
            {
                DrawChar(x, y + j, borderChar, fg, bg);
                DrawChar(x + w - 1, y + j, borderChar, fg, bg);
            }
        }

        private static void FillRect(int x, int y, int w, int h, char fillChar, ConsoleColor fg = ConsoleColor.White, ConsoleColor bg = ConsoleColor.Black)
        {
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    DrawChar(x + i, y + j, fillChar, fg, bg);
                }
            }
        }

        private static void RenderBufferToConsole()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\u001b[H");

            ConsoleColor activeFg = ConsoleColor.White;
            ConsoleColor activeBg = ConsoleColor.Black;

            sb.Append("\u001b[0m");
            sb.Append(GetAnsiFg(activeFg));
            sb.Append(GetAnsiBg(activeBg));

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = _buffer[x, y];

                    if (cell.Foreground != activeFg)
                    {
                        activeFg = cell.Foreground;
                        sb.Append(GetAnsiFg(activeFg));
                    }
                    if (cell.Background != activeBg)
                    {
                        activeBg = cell.Background;
                        sb.Append(GetAnsiBg(activeBg));
                    }
                    sb.Append(cell.Char);
                }

                if (y < Height - 1)
                {
                    sb.Append("\r\n");
                }
            }

            Console.Write(sb.ToString());
        }

        private static string GetAnsiFg(ConsoleColor c)
        {
            return c switch
            {
                ConsoleColor.Black => "\u001b[30m",
                ConsoleColor.DarkRed => "\u001b[31m",
                ConsoleColor.DarkGreen => "\u001b[32m",
                ConsoleColor.DarkYellow => "\u001b[33m",
                ConsoleColor.DarkBlue => "\u001b[34m",
                ConsoleColor.DarkMagenta => "\u001b[35m",
                ConsoleColor.DarkCyan => "\u001b[36m",
                ConsoleColor.Gray => "\u001b[37m",
                ConsoleColor.DarkGray => "\u001b[90m",
                ConsoleColor.Red => "\u001b[91m",
                ConsoleColor.Green => "\u001b[92m",
                ConsoleColor.Yellow => "\u001b[93m",
                ConsoleColor.Blue => "\u001b[94m",
                ConsoleColor.Magenta => "\u001b[95m",
                ConsoleColor.Cyan => "\u001b[96m",
                ConsoleColor.White => "\u001b[97m",
                _ => "\u001b[37m"
            };
        }

        private static string GetAnsiBg(ConsoleColor c)
        {
            return c switch
            {
                ConsoleColor.Black => "\u001b[40m",
                ConsoleColor.DarkRed => "\u001b[41m",
                ConsoleColor.DarkGreen => "\u001b[42m",
                ConsoleColor.DarkYellow => "\u001b[43m",
                ConsoleColor.DarkBlue => "\u001b[44m",
                ConsoleColor.DarkMagenta => "\u001b[45m",
                ConsoleColor.DarkCyan => "\u001b[46m",
                ConsoleColor.Gray => "\u001b[47m",
                ConsoleColor.DarkGray => "\u001b[100m",
                ConsoleColor.Red => "\u001b[101m",
                ConsoleColor.Green => "\u001b[102m",
                ConsoleColor.Yellow => "\u001b[103m",
                ConsoleColor.Blue => "\u001b[104m",
                ConsoleColor.Magenta => "\u001b[105m",
                ConsoleColor.Cyan => "\u001b[106m",
                ConsoleColor.White => "\u001b[107m",
                _ => "\u001b[40m"
            };
        }

        private static void Cleanup()
        {
            _gameTimer?.Dispose();
            Console.CursorVisible = true;
            Console.ResetColor();
            Console.Clear();
            Console.WriteLine("EDSA Station Manager shift simulation exited cleanly.");
        }
    }
}
