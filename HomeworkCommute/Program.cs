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

    // Specification 2: GLOBAL STATE SINGLETON MAPS
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
        public float TrainDelayTimer { get; set; } = 0.0f;       // Grows constantly (Seconds)
        public int PickpocketCount { get; set; } = 0;           // Passive growth
        public float PriorityQueueViolationRate { get; set; } = 0.0f; // Percentage 0.0 to 1.0

        // Under-Station View Stats
        public int TicketMachineFailures { get; set; } = 0;      // Ticks up randomly
        public float EscalatorWeightStrain { get; set; } = 0.0f;  // Percentage 0.0 to 100.0

        // Inside-Cars View Stats
        public float CarCrowdDensity { get; set; } = 1.0f;       // Density index 0.0 to 10.0
        public float ACFailureChance { get; set; } = 0.0f;       // Grows constantly
        public bool ACFailed { get; set; } = false;
    }

    class Program
    {
        private const int Width = 80;
        private const int Height = 24;

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
        private static int _alertTicksRemaining = 60; // 2 seconds

        // Keyboard Queue
        private static readonly Queue<ConsoleKeyInfo> _inputQueue = new Queue<ConsoleKeyInfo>();

        static async Task Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.Title = "EDSA Traffic Manager - Multi-Screen Central console";

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

                _screenOffset = Vector2.Zero;
                _alertMessage = "EDSA SYSTEM RESTORED. SWAP VEHICLE CHANNELS USING KEY 1, 2, 3.";
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

                // 1. Process keyboard buffers
                ProcessInputs();

                // 2. Compute background logic
                Update(deltaTime);

                // 3. Draw layout metrics
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

                // Specification 1: PERSPECTIVE SWITCHING KEYS (1, 2, 3)
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

                // ACTIVE PERSPECTIVE MITIGATION CONTROLS
                switch (sim.ActivePerspective)
                {
                    case Perspective.PLATFORM:
                        if (keyInfo.Key == ConsoleKey.D)
                        {
                            // Deploy train
                            sim.TrainDelayTimer = 0.0f;
                            // Clear platform crowding
                            sim.CarCrowdDensity = Math.Min(10.0f, sim.CarCrowdDensity + 2.0f);
                            FlashAlert("EXPRESS TRAIN DEPLOYED. DELAY CLOCK RESET! (-$100)", ConsoleColor.Green);
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 100.0f);
                        }
                        else if (keyInfo.Key == ConsoleKey.P)
                        {
                            // Bust pickpockets
                            if (sim.PickpocketCount > 0)
                            {
                                sim.PickpocketCount--;
                                FlashAlert("SECURITY ARRESTED PICKPOCKET! (-$50)", ConsoleColor.Green);
                                sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 50.0f);
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.Q)
                        {
                            // Enforce Priority Queue
                            sim.PriorityQueueViolationRate = Math.Max(0.0f, sim.PriorityQueueViolationRate - 0.15f);
                            FlashAlert("GUARDRADIUS ASSIGNED TO PRIORITY BOARDING GATE (-$30)", ConsoleColor.Green);
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 30.0f);
                        }
                        break;

                    case Perspective.UNDER_STATION:
                        if (keyInfo.Key == ConsoleKey.F)
                        {
                            // Fix ticket machine
                            if (sim.TicketMachineFailures > 0)
                            {
                                sim.TicketMachineFailures--;
                                FlashAlert("TICKET MACHINE SERVICED AND REPAIRED (-$120)", ConsoleColor.Green);
                                sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 120.0f);
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.S)
                        {
                            // Reduce escalator load weight
                            sim.EscalatorWeightStrain = Math.Max(0.0f, sim.EscalatorWeightStrain - 25.0f);
                            FlashAlert("ESCALATOR LOAD REDISTRIBUTED / SPEED RESET (-$40)", ConsoleColor.Green);
                            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 40.0f);
                        }
                        break;

                    case Perspective.INSIDE_CARS:
                        if (keyInfo.Key == ConsoleKey.A)
                        {
                            // Fix carriage AC units
                            sim.ACFailed = false;
                            sim.ACFailureChance = 0.05f;
                            FlashAlert("CARRIAGE AC FLUID REPLENISHED AND RESTORED (-$150)", ConsoleColor.Green);
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
                // Violent shake coordinates for Riot Eruptions
                Random rng = new Random();
                _screenOffset = new Vector2(rng.Next(-4, 5), rng.Next(-2, 3));
                return;
            }

            _frameCount++;

            // --- Specification 3: MULTI-SCREEN CHAOS LOGIC (PROCESS TICK) ---
            // Passive growth of metrics simultaneous across all channels:

            // 1. Platform updates
            sim.TrainDelayTimer += deltaTime;
            
            // Random chance: Pickpockets assemble
            if (_frameCount % 180 == 0 && sim.PickpocketCount < 10)
            {
                sim.PickpocketCount++;
            }
            // Violations rate increases
            sim.PriorityQueueViolationRate = Math.Min(1.0f, sim.PriorityQueueViolationRate + 0.015f * deltaTime);

            // 2. Under-Station Updates
            // Random chance: ticketing booth fails
            if (_frameCount % 300 == 0 && sim.TicketMachineFailures < 6)
            {
                sim.TicketMachineFailures++;
            }
            // Escalators accumulate weight strain
            sim.EscalatorWeightStrain = Math.Min(100.0f, sim.EscalatorWeightStrain + (2.5f + sim.TicketMachineFailures * 1.5f) * deltaTime);

            // 3. Inside-Cars Updates
            // Passengers get packed inside carriages
            sim.CarCrowdDensity = Math.Min(10.0f, sim.CarCrowdDensity + 0.15f * deltaTime);
            
            // AC fails chance climbs
            sim.ACFailureChance = Math.Min(1.0f, sim.ACFailureChance + 0.025f * sim.CarCrowdDensity * deltaTime);
            if (!sim.ACFailed && sim.ACFailureChance > 0.6f)
            {
                // Roll check
                Random rng = new Random();
                if (rng.NextDouble() < 0.005f) // random trip
                {
                    sim.ACFailed = true;
                    FlashAlert("💥 WARNING! TRAIN CAR 4 AC COMPRESSOR FAILED!", ConsoleColor.Red);
                }
            }

            // --- EXPONENTIAL RAGE METRICS ADDITIONS ---
            float rageAddition = 0.0f;

            // 1. Train delay impact
            if (sim.TrainDelayTimer > 15.0f)
            {
                float excess = sim.TrainDelayTimer - 15.0f;
                // Exponential rage growth: Exp(excess * 0.15)
                rageAddition += MathF.Exp(excess * 0.15f) * 0.35f * deltaTime;
            }

            // 2. Escalators weight strain impact
            if (sim.EscalatorWeightStrain > 75.0f)
            {
                float excess = sim.EscalatorWeightStrain - 75.0f;
                // Exponential rage growth: Exp(excess * 0.09)
                rageAddition += MathF.Exp(excess * 0.09f) * 0.45f * deltaTime;
            }

            // 3. Car overcrowding density impact
            if (sim.CarCrowdDensity > 8.0f)
            {
                float excess = sim.CarCrowdDensity - 8.0f;
                // Exponential rage growth: Exp(excess * 0.5)
                rageAddition += MathF.Exp(excess * 0.5f) * 0.65f * deltaTime;
            }

            // Pickpockets, ticket machine failures, and AC failures add flat passive rates
            rageAddition += sim.PickpocketCount * 0.25f * deltaTime;
            rageAddition += sim.TicketMachineFailures * 0.4f * deltaTime;
            if (sim.ACFailed)
            {
                rageAddition += 8.0f * deltaTime; // Extreme passive rage rise
            }

            // Apply calculated rage
            sim.GlobalCommuterRage += rageAddition;

            // Slowly decay rage if everything is normal and below thresholds
            if (sim.TrainDelayTimer <= 15.0f && sim.EscalatorWeightStrain <= 75.0f && sim.CarCrowdDensity <= 8.0f && !sim.ACFailed)
            {
                sim.GlobalCommuterRage = Math.Max(0.0f, sim.GlobalCommuterRage - 1.5f * deltaTime);
            }

            // Clamp rage
            if (sim.GlobalCommuterRage > 100.0f) sim.GlobalCommuterRage = 100.0f;

            // Specification 3 Win/Loss trigger check:
            // "triggers a Game Over sequence when 'CommuterRage' reaches 100.0, outputting a clear alert that the station has erupted into a public riot."
            if (sim.GlobalCommuterRage >= 100.0f)
            {
                sim.RiotErupted = true;
            }

            // Tick down budget passively
            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 8.0f * deltaTime);

            // Periodically clear active notification banners
            if (_alertTicksRemaining > 0)
            {
                _alertTicksRemaining--;
                if (_alertTicksRemaining <= 0)
                {
                    _alertMessage = "SWAP VIEW: [1] Platform | [2] Under-Station | [3] Inside-Cars";
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
            // 1. SPECIFICATION 4: PERSISTENT GLOBAL HUD
            // ==========================================
            DrawRect(1, 1, Width - 2, 4, '═', ConsoleColor.DarkCyan, ConsoleColor.Black);
            
            // Selected view indicator
            string viewName = sim.ActivePerspective switch
            {
                Perspective.PLATFORM => "[PLATFORM ACTION VIEW]",
                Perspective.UNDER_STATION => "[UNDER-STATION COMMERCE VIEW]",
                Perspective.INSIDE_CARS => "[INSIDE CARS PASSENGER VIEW]",
                _ => "[SYSTEM MONITOR]"
            };
            DrawString(3, 2, $"{viewName}   |   Daily Budget: ${sim.DailyBudget:F2}", ConsoleColor.Yellow, ConsoleColor.Black);
            
            // Riot Meter progress bar representing GlobalCommuterRage
            DrawString(3, 3, "Global Riot Meter (Rage):", ConsoleColor.White, ConsoleColor.Black);
            ConsoleColor rageColor = sim.GlobalCommuterRage > 80.0f ? ConsoleColor.Red : (sim.GlobalCommuterRage > 50.0f ? ConsoleColor.Yellow : ConsoleColor.Green);
            DrawProgressBar(30, 3, 20, sim.GlobalCommuterRage / 100.0f, rageColor);
            DrawString(52, 3, $"{sim.GlobalCommuterRage:F1}%", rageColor, ConsoleColor.Black);

            // ==========================================
            // 2. SPECIFICATION 4: DYNAMIC VIEWPORT CANVAS
            // ==========================================
            int viewX = 2;
            int viewY = 5;
            int viewW = Width - 4;
            int viewH = Height - 8;

            switch (sim.ActivePerspective)
            {
                case Perspective.PLATFORM:
                    RenderPlatformView(viewX, viewY, viewW, viewH, sim);
                    break;
                case Perspective.UNDER_STATION:
                    RenderUnderStationView(viewX, viewY, viewW, viewH, sim);
                    break;
                case Perspective.INSIDE_CARS:
                    RenderInsideCarsView(viewX, viewY, viewW, viewH, sim);
                    break;
            }

            // Operations Alert Bar (bottom layer)
            FillRect(1, Height - 2, Width - 2, 1, ' ', ConsoleColor.White, _alertBg);
            string spacer = new string(' ', Math.Max(0, ((Width - 2) - _alertMessage.Length)/2));
            DrawString(1 + spacer.Length, Height - 2, _alertMessage, ConsoleColor.White, _alertBg);

            // Loss State Overlay (Specification 3)
            if (sim.RiotErupted)
            {
                RenderGameOverPanel();
            }

            // Flush Grid
            RenderBufferToConsole();
        }

        private static void RenderPlatformView(int x, int y, int w, int h, SimulationManager sim)
        {
            // Paint gray viewport platform area using background cells
            FillRect(x, y, w, h, ' ', ConsoleColor.White, ConsoleColor.DarkGray);
            DrawRect(x, y, w, h, '█', ConsoleColor.Gray, ConsoleColor.DarkGray);

            // Ceiling support columns
            for (int colX = x + 10; colX < x + w - 10; colX += 20)
            {
                FillRect(colX, y + 1, 3, 2, '█', ConsoleColor.DarkGray, ConsoleColor.DarkGray);
                DrawString(colX - 1, y + 3, "[COL]", ConsoleColor.Black, ConsoleColor.DarkGray);
            }

            // Draw railways tracks
            int trackY = y + 4;
            for (int dx = x + 1; dx < x + w - 1; dx++)
            {
                DrawChar(dx, trackY, '=', ConsoleColor.White, ConsoleColor.DarkGray);
                DrawChar(dx, trackY + 2, '=', ConsoleColor.White, ConsoleColor.DarkGray);
            }
            // Sleepers
            for (int dx = x + 3; dx < x + w - 2; dx += 5)
            {
                DrawString(dx, trackY + 1, "|-|", ConsoleColor.DarkRed, ConsoleColor.DarkGray);
            }

            // Draw detailed train depending on the timer or scrolling
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

            // Commuters standing on platform
            int commutersCount = Math.Min(35, (int)(sim.TrainDelayTimer * 1.5f) + 5);
            Random rng = new Random(42);
            for (int i = 0; i < commutersCount; i++)
            {
                int px = rng.Next(x + 2, x + w - 3);
                int py = rng.Next(safetyY + 1, y + h - 3);
                char passChar = rng.Next(2) == 0 ? 'o' : 'x';
                ConsoleColor passCol = sim.PriorityQueueViolationRate > 0.4f ? ConsoleColor.Yellow : ConsoleColor.Green;
                DrawChar(px, py, passChar, passCol, ConsoleColor.DarkGray);
            }

            // Stats HUD overlay inside Platform view
            DrawString(x + 3, y + 1, " ═ PLATFORM OVERHEAD CAMERA ═ ", ConsoleColor.Yellow, ConsoleColor.DarkGray);

            ConsoleColor delayCol = sim.TrainDelayTimer > 15.0f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(x + 3, y + 10, $"Train Delay Timer  : {sim.TrainDelayTimer:F1}s (Threshold: 15.0s)", delayCol, ConsoleColor.DarkGray);
            if (sim.TrainDelayTimer > 15.0f)
            {
                DrawString(x + 46, y + 10, "🚨 RAGE SCALE INCOMING!", ConsoleColor.Red, ConsoleColor.DarkGray);
            }

            ConsoleColor pickColor = sim.PickpocketCount > 3 ? ConsoleColor.Yellow : ConsoleColor.White;
            DrawString(x + 3, y + 11, $"Pickpocket Active  : {sim.PickpocketCount} thieves detected", pickColor, ConsoleColor.DarkGray);

            ConsoleColor viColor = sim.PriorityQueueViolationRate > 0.4f ? ConsoleColor.Yellow : ConsoleColor.White;
            DrawString(x + 3, y + 12, $"Priority Queue Viol: {(sim.PriorityQueueViolationRate * 100f):F1}% rate", viColor, ConsoleColor.DarkGray);

            // Enforcers active
            DrawString(x + w - 24, y + 1, "Enforcer: ONLINE", ConsoleColor.Green, ConsoleColor.DarkGray);

            // Hotkey visual helps
            DrawString(x + 2, y + h - 2, "[D] Dispatch Train (-$100) | [P] Bust Thief (-$50) | [Q] Enforce Queue (-$30)", ConsoleColor.Cyan, ConsoleColor.DarkGray);
        }

        private static void RenderUnderStationView(int x, int y, int w, int h, SimulationManager sim)
        {
            // Concrete underground canvas (Dark Cyan background rectangle)
            FillRect(x, y, w, h, ' ', ConsoleColor.White, ConsoleColor.DarkCyan);
            DrawRect(x, y, w, h, '█', ConsoleColor.Cyan, ConsoleColor.DarkCyan);

            // Support pillars
            for (int dx = x + 8; dx < x + w - 5; dx += 18)
            {
                FillRect(dx, y + 1, 4, h - 4, '▒', ConsoleColor.Gray, ConsoleColor.DarkCyan);
            }

            // Draw Ticket Vending Machines (TVMs)
            int tvmX = x + 3;
            for (int k = 0; k < 4; k++)
            {
                int curTvmX = tvmX + (k * 10);
                DrawRect(curTvmX, y + 3, 7, 5, '█', ConsoleColor.Black, ConsoleColor.DarkCyan);
                DrawString(curTvmX + 2, y + 3, "[ ]", ConsoleColor.Green, ConsoleColor.DarkCyan);
                DrawString(curTvmX + 1, y + 4, "$$TVM$$", ConsoleColor.Cyan, ConsoleColor.Black);

                if (k < sim.TicketMachineFailures)
                {
                    DrawString(curTvmX + 2, y + 6, "FAIL", ConsoleColor.Red, ConsoleColor.Black);
                }
                else
                {
                    DrawString(curTvmX + 2, y + 6, "O K ", ConsoleColor.Green, ConsoleColor.Black);
                }
            }

            // Draw Escalator pathways with step animations
            int escX = x + w - 24;
            DrawString(escX, y + 2, "=== Escalator ===", ConsoleColor.Yellow, ConsoleColor.DarkCyan);
            DrawString(escX, y + 3, " [Down]    [Up]  ", ConsoleColor.White, ConsoleColor.DarkCyan);

            int animOffset = _frameCount % 3;
            for (int k = 0; k < 6; k++)
            {
                // Left descending steps
                int ly = y + 4 + k;
                int lx = escX + (k * 2);
                char stepCharL = (k + animOffset) % 3 == 0 ? '█' : '▒';
                DrawChar(lx, ly, stepCharL, ConsoleColor.Gray, ConsoleColor.DarkCyan);

                // Right ascending steps
                int ry = y + 9 - k;
                int rx = escX + 10 + (k * 2);
                char stepCharR = (k + (3 - animOffset)) % 3 == 0 ? '█' : '▒';
                DrawChar(rx, ry, stepCharR, ConsoleColor.Gray, ConsoleColor.DarkCyan);
            }

            // Stats HUD overlay inside Under-Station view
            DrawString(x + 3, y + 1, " ═ UNDER-STATION CONCOURSE HUD ═ ", ConsoleColor.Yellow, ConsoleColor.DarkCyan);

            ConsoleColor tColor = sim.TicketMachineFailures > 2 ? ConsoleColor.Red : ConsoleColor.White;
            DrawString(x + 3, y + 9, $"Ticket Machines Failed: {sim.TicketMachineFailures} booths offline", tColor, ConsoleColor.DarkCyan);

            ConsoleColor strainColor = sim.EscalatorWeightStrain > 75.0f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(x + 3, y + 11, $"Escalator Weight Strain: {sim.EscalatorWeightStrain:F1}% (Threshold: 75.0%)", strainColor, ConsoleColor.DarkCyan);

            // Alert triggers
            if (sim.EscalatorWeightStrain > 75.0f)
            {
                DrawString(x + 3, y + 12, "🚨 WARNING: WEIGHT LOAD EXCEEDED!", ConsoleColor.Red, ConsoleColor.DarkCyan);
            }

            // Control helps
            DrawString(x + 2, y + h - 2, "[F] Fix Ticket Booth (-$120)  |  [S] Cool Escalator Strain (-$40)", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
        }

        private static void RenderInsideCarsView(int x, int y, int w, int h, SimulationManager sim)
        {
            // cramped metallic train interior (Blue background panel)
            FillRect(x, y, w, h, ' ', ConsoleColor.White, ConsoleColor.DarkBlue);
            DrawRect(x, y, w, h, '█', ConsoleColor.Blue, ConsoleColor.DarkBlue);

            // Draw carriage windows looking out onto moving dark tunnel columns
            int scrollX = (_frameCount / 2) % 15;
            for (int wx = x + 4; wx < x + w - 24; wx += 16)
            {
                DrawRect(wx, y + 2, 10, 4, '█', ConsoleColor.Cyan, ConsoleColor.DarkBlue);
                // Scrolling tunnel pillar column lines
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

            // Draw seats rows
            int seatStartX = x + 4;
            int seatStartY = y + 10;
            DrawString(seatStartX, seatStartY,     "|____[Seat Row A]____|     |____[Seat Row B]____|", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
            DrawString(seatStartX, seatStartY + 1, "|[o]  [o]   [ ]  [x]|     |[x]  [ ]   [o]  [o]|", ConsoleColor.White, ConsoleColor.DarkBlue);

            // Stats overlay inside carriage
            DrawString(x + 3, y + 1, " ═ METRO DECK CARRIAGE STATUS ═ ", ConsoleColor.Yellow, ConsoleColor.DarkBlue);

            ConsoleColor denColor = sim.CarCrowdDensity > 8.0f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(x + 46, y + 2, $"Car Density: {sim.CarCrowdDensity:F1}/10.0", denColor, ConsoleColor.DarkBlue);
            if (sim.CarCrowdDensity > 8.0f)
            {
                DrawString(x + 46, y + 3, "[OVERLOADED]", ConsoleColor.Red, ConsoleColor.DarkBlue);
            }

            DrawString(x + 46, y + 5, $"AC Temp : {(sim.ACFailed ? "38C 🔥" : "21C ❄️")}", sim.ACFailed ? ConsoleColor.Red : ConsoleColor.Green, ConsoleColor.DarkBlue);

            // Flashing AC warnings (Specification 4)
            if (sim.ACFailed)
            {
                FillRect(x + 46, y + 7, 24, 4, ' ', ConsoleColor.White, ConsoleColor.Red);
                DrawString(x + 48, y + 8, "⚠️ AC COMPRESSOR ⚠️", ConsoleColor.Yellow, ConsoleColor.Red);
                DrawString(x + 48, y + 9, "  SYSTEM TRIP!  ", ConsoleColor.White, ConsoleColor.Red);
            }
            else
            {
                DrawString(x + 46, y + 7, "AC Compressor: OK", ConsoleColor.Green, ConsoleColor.DarkBlue);
            }

            // Controls help
            DrawString(x + 2, y + h - 2, "[A] Service carriage AC Units (-$150)", ConsoleColor.Cyan, ConsoleColor.DarkBlue);
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

            // Specification 3: Game Over text
            // "STATION RIOT TRIGGERED! YOU ARE FIRED."
            DrawString(panelX + 13, panelY + 2, "💥 !!! STATION RIOT TRIGGERED! YOU ARE FIRED. !!! 💥", ConsoleColor.Yellow, ConsoleColor.DarkRed);
            DrawString(panelX + 5, panelY + 4, "EDSA commutes collapsed. Rage hit 100%. Public riots occurred.", ConsoleColor.White, ConsoleColor.DarkRed);
            DrawString(panelX + 8, panelY + 5, "The operations agency terminated your coordinator shift.", ConsoleColor.White, ConsoleColor.DarkRed);
            
            DrawString(panelX + 18, panelY + 7, "Press [ R ] to Restart Simulation", ConsoleColor.Yellow, ConsoleColor.DarkRed);
        }

        private static void FlashAlert(string text, ConsoleColor color)
        {
            _alertMessage = text;
            _alertBg = color;
            _alertTicksRemaining = 60;
        }

        // Color and coordinate outputting mapping screenOffset shakes
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
            sb.Append("\u001b[H"); // Reset cursor home

            ConsoleColor activeFg = ConsoleColor.White;
            ConsoleColor activeBg = ConsoleColor.Black;

            sb.Append("\u001b[0m"); // clear attributes
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
