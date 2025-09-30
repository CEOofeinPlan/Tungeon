using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Better_Terminal;
using MSG;

namespace Tungeon
{
	internal static class Program
	{
        static bool running = true;
        static ASCIITerminal terminal;
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);
        static void Compute_logic()
        {
            while (running)
            {
                // Übergabe der alten Position
                old_player_x = player_x;
                old_player_y = player_y;

                // W A S D Steuerung
                bool up = (GetKeyState((int)Keys.W) & 0x8000) != 0;
                bool down = (GetKeyState((int)Keys.S) & 0x8000) != 0;
                bool left = (GetKeyState((int)Keys.A) & 0x8000) != 0;
                bool right = (GetKeyState((int)Keys.D) & 0x8000) != 0;

                // Bewegung berechnen
                int dx = 0, dy = 0;
                if (up) dy--;
                if (down) dy++;
                if (left) dx--;
                if (right) dx++;

                var nextPos = Tuple.Create(player_x + dx, player_y + dy);
                if (!walls.ContainsKey(nextPos))
                {
                    player_x = Math.Max(Math.Min(player_x + dx, terminal.cols - 1), 0);
                    player_y = Math.Max(Math.Min(player_y + dy, terminal.rows - 1), 0);

                    if (dx != 0 || dy != 0)
                    {
                        msg.send(graphics_id, 2, "");
                        terminal.ComputeFovGamma(terminal.cols, terminal.rows, player_x, player_y, 3, 1.0);
                        terminal.Build_gamma();
                    }   
                }

                Thread.Sleep(100);
            }
        }

        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [STAThread]
        static void Main()
        {
            // ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            terminal = new ASCIITerminal();

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

            // Threads starten
            Thread graphics = new Thread(Compute_graphics) { IsBackground = true };
            graphics.Start();

            Thread logic = new Thread(Compute_logic) { IsBackground = true };
            logic.Start();

            terminal.ComputeFovGamma(terminal.cols, terminal.rows, player_x, player_y, 3, 1.0);
            terminal.Build_gamma();

            // GUI starten
            Application.Run(terminal);
        }
    }
}
