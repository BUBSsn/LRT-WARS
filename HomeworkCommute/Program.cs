using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDSAStationManager
{
    // Double buffered console cell representing character and color characteristics
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

    // Specification 1: GLOBAL STATE SINGLETON class
    public class SimulationManager
    {
        private static readonly SimulationManager _instance = new SimulationManager();
        public static SimulationManager Instance => _instance;

        private SimulationManager() { }

        // Core metric properties
        public float CommuterRage { get; set; } = 0.0f;       // Scale: 0.0 to 100.0
        public float DailyBudget { get; set; } = 5000.0f;     // Starting Budget: 5000.0
        public float PlatformDensity { get; set; } = 3.5f;    // Crowd Congestion Density

        public bool RiotErupted { get; set; } = false;         // Win/Loss flag
        public bool IsRunning { get; set; } = true;
    }

    class Program
    {
        private const int Width = 80;
        private const int Height = 24;

        // Double buffer visual grid
        private static ConsoleCell[,] _buffer = new ConsoleCell[Width, Height];
        private static readonly object _lock = new object();

        // Game Loop parameters
        private static Timer? _gameTimer;
        private const int TargetFps = 30;
        private const int LoopIntervalMs = 1000 / TargetFps;

        // Shaking offset displacement
        private static Vector2 _screenOffset = Vector2.Zero;

        // Input extraction buffer
        private static readonly Queue<ConsoleKeyInfo> _inputQueue = new Queue<ConsoleKeyInfo>();

        static async Task Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.Title = "EDSA Station Manager - Week 1 Scaffold";

            Initialize();

            // Run async input reader in background
            var inputTask = Task.Run(InputReaderLoop);

            // Wait until program terminates
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
                sim.CommuterRage = 0.0f;
                sim.DailyBudget = 5000.0f;
                sim.PlatformDensity = 3.5f; // Initial density below threshold 4.5
                sim.RiotErupted = false;
                sim.IsRunning = true;

                _screenOffset = Vector2.Zero;
            }

            // Specification requirement: high-performance thread timer game loop
            _gameTimer?.Dispose();
            _gameTimer = new Timer(GameLoopStep, null, 0, LoopIntervalMs);
        }

        private static void GameLoopStep(object? state)
        {
            var sim = SimulationManager.Instance;
            if (!sim.IsRunning) return;

            lock (_lock)
            {
                float deltaTime = 1.0f / TargetFps;

                // 1. Gather keypad inputs
                ProcessInputs();

                // 2. Perform updates and calculations
                Update(deltaTime);

                // 3. Render visuals directly onto double buffer
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

                // Interactive control inputs for manual simulation testing
                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        sim.PlatformDensity = Math.Min(10.0f, sim.PlatformDensity + 0.5f);
                        break;

                    case ConsoleKey.DownArrow:
                        sim.PlatformDensity = Math.Max(0.0f, sim.PlatformDensity - 0.5f);
                        break;

                    case ConsoleKey.RightArrow:
                        sim.DailyBudget += 250.0f;
                        break;

                    case ConsoleKey.LeftArrow:
                        sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 250.0f);
                        break;
                }
            }
        }

        private static void Update(float deltaTime)
        {
            var sim = SimulationManager.Instance;

            if (sim.RiotErupted)
            {
                // Visual chaotic shake on game over Loss state
                Random rng = new Random();
                _screenOffset = new Vector2(rng.Next(-2, 3), rng.Next(-1, 2));
                return;
            }

            // --- Specification 2: PASSIVE TICK SIMULATION ---
            // "if 'PlatformDensity' exceeds a threshold of 4.5, 'CommuterRage' passively increases over time."
            if (sim.PlatformDensity > 4.5f)
            {
                float excess = sim.PlatformDensity - 4.5f;

                // Rage rises proportional to the excess density
                float rageRiseRate = excess * 2.5f; 
                sim.CommuterRage += rageRiseRate * deltaTime;
            }
            else
            {
                // Slowly cool down rage passively when density stays below comfort threshold
                sim.CommuterRage = Math.Max(0.0f, sim.CommuterRage - 1.5f * deltaTime);
            }

            // Clamp commuter rage between boundaries
            if (sim.CommuterRage > 100.0f)
            {
                sim.CommuterRage = 100.0f;
            }

            // --- Specification 3: WIN/LOSS TRIGGERS ---
            // "triggers a Game Over sequence when 'CommuterRage' reaches 100.0"
            if (sim.CommuterRage >= 100.0f)
            {
                sim.RiotErupted = true;
            }

            // Slowly tick down budget on operational costs to simulate active shift
            sim.DailyBudget = Math.Max(0.0f, sim.DailyBudget - 5.0f * deltaTime);
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

            // Draw Tycoon Console Border Walls
            DrawRect(0, 0, Width, Height, '▒', ConsoleColor.DarkGray, ConsoleColor.Black);

            // Title Dashboard Header
            DrawString(3, 2, "=== EDSA STATION MANAGER ===", ConsoleColor.Yellow);
            DrawString(3, 3, "Week 1 Architecture Simulation Core Model", ConsoleColor.DarkCyan);
            DrawString(3, 4, "────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkGray);

            // --- Specification 4: BASELINE UI METRICS OVERLAY ---

            // 1. Daily Budget Card (Box Shape Canvas)
            DrawBox(3, 6, 22, 4, ConsoleColor.Green);
            DrawString(5, 7, "Daily Budget", ConsoleColor.Gray);
            DrawString(5, 8, $"${sim.DailyBudget:F2}", ConsoleColor.White);

            // 2. Active Platform Density Card (Box Shape Canvas)
            ConsoleColor densityColor = sim.PlatformDensity > 4.5f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawBox(28, 6, 22, 4, densityColor);
            DrawString(30, 7, "Platform Density", ConsoleColor.Gray);
            DrawString(30, 8, $"{sim.PlatformDensity:F1} / 10.0", ConsoleColor.White);
            
            // Comfort Threshold alert indicator
            if (sim.PlatformDensity > 4.5f)
            {
                DrawString(30, 9, "[ OVER OVERCROWD! ]", ConsoleColor.Red);
            }
            else
            {
                DrawString(30, 9, "[ COMFORT RANGE ]", ConsoleColor.DarkGreen);
            }

            // 3. Riot Meter / Commuter Rage Card (Box Shape Canvas)
            ConsoleColor rageColor = sim.CommuterRage > 80.0f ? ConsoleColor.Red : (sim.CommuterRage > 50.0f ? ConsoleColor.Yellow : ConsoleColor.Green);
            DrawBox(53, 6, 24, 4, rageColor);
            DrawString(55, 7, "Riot Meter (Rage)", ConsoleColor.Gray);
            DrawString(55, 8, $"{sim.CommuterRage:F1}%", ConsoleColor.White);

            // Progress bar mapping (Riot Meter)
            DrawProgressBar(55, 9, 20, sim.CommuterRage / 100.0f, rageColor);

            // Simulation Helper Information
            DrawString(3, 12, "--- SIMULATION PARAMETERS ---", ConsoleColor.DarkCyan);
            DrawString(3, 13, $"Threshold Platform Limit : 4.50", ConsoleColor.Gray);
            string passiveStatus = sim.PlatformDensity > 4.5f ? "RAGE INCREASING (Density > 4.5)" : "RAGE DISSIPATING (Density <= 4.5)";
            ConsoleColor statusColor = sim.PlatformDensity > 4.5f ? ConsoleColor.Red : ConsoleColor.Green;
            DrawString(3, 14, $"Passive Tick Status      : {passiveStatus}", statusColor);

            // Display Interactive Cheat Controls for manual simulation testing
            DrawString(3, 17, "────────────────────────────────────────────────────────────────────────────", ConsoleColor.DarkGray);
            DrawString(3, 18, "INTERACTIVE MANUAL TESTING INPUTS:", ConsoleColor.Magenta);
            DrawString(3, 19, "• Use [UP / DOWN] Arrows  : Increase / Decrease Platform Density (+/- 0.5)", ConsoleColor.White);
            DrawString(3, 20, "• Use [LEFT / RIGHT] Arrows: Decrease / Increase Daily Budget ($250.0)", ConsoleColor.White);
            DrawString(3, 21, "• Use [ESC] Key            : Exit simulation shift cleanly", ConsoleColor.White);

            // --- Specification 3: WIN/LOSS TRIGGERS ---
            if (sim.RiotErupted)
            {
                RenderRiotGameOverPanel();
            }

            RenderBufferToConsole();
        }

        private static void DrawBox(int x, int y, int w, int h, ConsoleColor color)
        {
            // Draw box outlines
            DrawRect(x, y, w, h, '█', color, ConsoleColor.Black);
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

        private static void RenderRiotGameOverPanel()
        {
            int panelW = 66;
            int panelH = 9;
            int panelX = (Width - panelW) / 2;
            int panelY = (Height - panelH) / 2;

            FillRect(panelX, panelY, panelW, panelH, ' ', ConsoleColor.White, ConsoleColor.DarkRed);
            DrawRect(panelX, panelY, panelW, panelH, '█', ConsoleColor.Yellow, ConsoleColor.DarkRed);

            DrawString(panelX + 17, panelY + 2, "⚠️  !!! PUBLIC RIOT ALERT !!! ⚠️", ConsoleColor.Yellow, ConsoleColor.DarkRed);
            DrawString(panelX + 5, panelY + 4, "THE STATION HAS OFFICIALLY ERUPTED INTO A DESTABILIZED PUBLIC RIOT!", ConsoleColor.White, ConsoleColor.DarkRed);
            DrawString(panelX + 10, panelY + 5, "Commuter rage hit index 100%. Security collapsed.", ConsoleColor.White, ConsoleColor.DarkRed);
            
            DrawString(panelX + 18, panelY + 7, "Press [ R ] to Restart Simulation", ConsoleColor.Yellow, ConsoleColor.DarkRed);
        }

        // Draw mappings coordinate screenOffset shakes
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
            Console.WriteLine("EDSA Station Manager shift simulation simulation exited cleanly.");
        }
    }
}
