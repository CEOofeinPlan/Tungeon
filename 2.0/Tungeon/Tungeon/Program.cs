using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using MSG;
using Better_Terminal;
using System.Runtime.InteropServices;

// Noch zu machen: Wände lassen kein Licht hindurch
// Spieler bekommt Renn-Fähigkeit (Shift halten = schneller bewegen) (weniger Zeit bei Thread.Sleep)

internal static class Program

{
    static bool running = true;
    static ASCIITerminal terminal = new ASCIITerminal();
    static Msg msg = new Msg();
    static short graphics_id;
    static short logic_id;

    static int width = 800;
    static int height = 400;

    static int player_x;
    static int player_y;
    static int old_player_x;
    static int old_player_y;

    static Dictionary<Tuple<int, int>, char> walls = new Dictionary<Tuple<int, int>, char>();

    static void Compute_graphics()
    {
        while (running)
        {
            var packet = msg.receive(graphics_id);
            if (packet != null)
            {
                switch (packet.Value.task)
                {
                    case 1: // Draw Map
                        for (int i = 0; i < terminal.rows; i++)
                        {
                            for (int j = 0; j < terminal.cols; j++)
                            {
                                var key = Tuple.Create(j, i);
                                if (j == player_x && i == player_y)
                                    terminal.WriteAt("0", j, i);
                                else if (walls.ContainsKey(key))
                                    terminal.WriteAt(walls[key].ToString(), j, i);
                                else
                                    terminal.WriteAt(" ", j, i);
                            }
                        }
                        break;
                    case 2: // Move player
                        terminal.WriteAt(" ", old_player_x, old_player_y);
                        terminal.WriteAt("0", player_x, player_y);
                        break;
                    case 99: // Exit
                        running = false;
                        Application.Exit();
                        break;
                }
            }
            Thread.Sleep(10);
        }
    }

    static void Compute_logic()
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);

        while (running)
        {
            old_player_x = player_x;
            old_player_y = player_y;

            bool up = (GetKeyState((int)Keys.W) & 0x8000) != 0;
            bool down = (GetKeyState((int)Keys.S) & 0x8000) != 0;
            bool left = (GetKeyState((int)Keys.A) & 0x8000) != 0;
            bool right = (GetKeyState((int)Keys.D) & 0x8000) != 0;

            int dx = 0, dy = 0;
            if (up) dy--;
            if (down) dy++;
            if (left) dx--;
            if (right) dx++;

            var nextPos = Tuple.Create(player_x + dx, player_y + dy);
            if (!walls.ContainsKey(nextPos))
            {
                player_x = Math.Clamp(player_x + dx, 0, terminal.cols - 1);
                player_y = Math.Clamp(player_y + dy, 0, terminal.rows - 1);
                int[,] gamma = new int[,]
                {
                    {player_x, player_y, 255},
                    {player_x, player_y-2, 51},
                    {player_x, player_y+2, 51 },
                    {player_x-2, player_y, 51},
                    {player_x+2, player_y, 51},
                    {player_x-1, player_y-1, 102},
                    {player_x+1, player_y+1, 102},
                    {player_x, player_y-1, 102},
                    {player_x, player_y+1, 102},
                    {player_x-1, player_y+1, 102},
                    {player_x+1, player_y-1, 102},
                    {player_x-1, player_y, 102},
                    {player_x+1, player_y, 102},
                };

                if (dx != 0 || dy != 0)
                {
                    msg.send(graphics_id, 2, "");
                    terminal.Build_gamma(gamma);
                }   
            }

            Thread.Sleep(100);
        }
    }

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        AllocConsole();
        Console.WriteLine("!");

        // Terminal Setup
        terminal.cols = 80;
        terminal.rows = 25;
        terminal.buf = new char[terminal.cols, terminal.rows];

        player_x = terminal.cols / 2;
        player_y = terminal.rows / 2;

        // Wände erstellen
        for (int i = 5; i < 20; i++)
        {
            for (int j = 5; j < 55; j++)
            {
                if (i == 5 || i == 19 || j == 5 || j == 54)
                {
                    var key = Tuple.Create(j, i);
                    if (i == 5) walls[key] = j == 5 ? '┌' : j == 54 ? '┐' : '─';
                    else if (i == 19) walls[key] = j == 5 ? '└' : j == 54 ? '┘' : '─';
                    else walls[key] = '│';
                }
            }
        }

        // Threads registrieren
        graphics_id = msg.register();
        logic_id = msg.register();

        msg.send(graphics_id, 1, ""); // Initial map

        // Cache (Beispiel)
        int[,] gamma = new int[,]
                {
                    {player_x, player_y, 255},
                    {player_x, player_y-2, 51},
                    {player_x, player_y+2, 51 },
                    {player_x-2, player_y, 51},
                    {player_x+2, player_y, 51},
                    {player_x-1, player_y-1, 102},
                    {player_x+1, player_y+1, 102},
                    {player_x, player_y-1, 102},
                    {player_x, player_y+1, 102},
                    {player_x-1, player_y+1, 102},
                    {player_x+1, player_y-1, 102},
                    {player_x-1, player_y, 102},
                    {player_x+1, player_y, 102},
        };
        terminal.Build_gamma(gamma);

        // Threads starten
        Thread graphics = new Thread(Compute_graphics) { IsBackground = true };
        graphics.Start();

        Thread logic = new Thread(Compute_logic) { IsBackground = true };
        logic.Start();

        // GUI starten
        Application.Run(terminal);
    }
}