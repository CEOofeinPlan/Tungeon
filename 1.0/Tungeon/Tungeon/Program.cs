using Better_Terminal;
using Microsoft.VisualBasic.Devices;
using MSG;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Hilfe;

// MORGEN: GAMMA einbauen
// MORGEN: Jedem Zeichen einen GAMMA Wert zuweisen
// MORGEN: Sichtfeld einbauen
// MORGEN: Lichtspiele einbauen


internal static class Program
{
    static bool running = true;
    static ASCIITerminal terminal = new ASCIITerminal();
    static Msg msg = new Msg();
    static short graphics_id;
    static short logic_id;
    static int width = terminal.Width;
    static int height = terminal.Height;

    // Interne Spiel Variablen
    static int player_x = 0;
    static int player_y = 0;
    static int old_player_x = 0;
    static int old_player_y = 0;

    // Gamma Radius
    static int gamma_radius = 3;

    // Dictionary für die Wände
    static Dictionary<Tuple<int, int>, char> walls = new Dictionary<Tuple<int, int>, char>();

    static void Compute_graphics()
    {
        while (running)
        {
            var packet = msg.receive(graphics_id);
            if (packet != null)
            {
                switch (packet)
                {
                    case m_s_g p when p.task == 0: // Clear Screen
                        terminal.ClearBuf();
                        break;

                    case m_s_g p when p.task == 1: // Write Map (.)
                                                   // 1. Nur Punkte
                                                   // 2. Spieler (@)
                        for (int i = 0; i < terminal.rows; ++i)
                        {
                            for (int j = 0; j < terminal.cols; ++j)
                            {
                                if (j == player_x && i == player_y)
                                {
                                    terminal.WriteAt("0", j, i); // Spieler
                                }
                                var key = Tuple.Create(j, i);
                                if (walls.ContainsKey(key))
                                {
                                    terminal.WriteAt(walls[key].ToString(), j, i); // Wand
                                }
                                else
                                {
                                    terminal.WriteAt(" ", j, i);
                                }
                            }
                        }
                        break;
                    case m_s_g p when p.task == 2:
                       // Alten Spielerplatz leeren
                        terminal.WriteAt(" ", old_player_x, old_player_y);
                        // Spieler an neuer Position zeichnen
                        terminal.WriteAt("0", player_x, player_y);
                        break;

                    case m_s_g p when p.task == 99: // Exit
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
        [DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);
        while (running)
        {
            old_player_x = player_x;
            old_player_y = player_y;

            bool up = (GetKeyState((int)Keys.W) & 0x8000) != 0;    // Up Arrow
            bool down = (GetKeyState((int)Keys.S) & 0x8000) != 0;  // Down Arrow
            bool left = (GetKeyState((int)Keys.A) & 0x8000) != 0;  // Left Arrow
            bool right = (GetKeyState((int)Keys.D) & 0x8000) != 0; // Right Arrow

            double dx = 0;
            double dy = 0;

            if (up) dy--;
            if (down) dy++;
            if (left) dx--;
            if (right) dx++;

            if (dx == 0 && dy == 0) continue; // Keine Bewegung

            var key = Tuple.Create((int)(player_x + dx), (int)(player_y + dy));
            if (walls.ContainsKey(key)) continue; // Wand im Weg

            if (dx != 0 && dy != 0)
            {
                dx *= 0.7071; // 1/√2 für gleiche Distanz wie horizontal/vertikal
                dy *= 0.7071;
            }

            // Anwenden und auf int runden
            player_x = Math.Clamp(player_x + (int)Math.Round(dx), 0, terminal.cols - 1);
            player_y = Math.Clamp(player_y + (int)Math.Round(dy), 0, terminal.rows - 1);

            // Nachricht senden, um die Grafik zu aktualisieren
            if (player_x != old_player_x || player_y != old_player_y) msg.send(graphics_id, 2, "");

            Thread.Sleep(100);
        }
    }

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        for (int i = 5; i < 20; ++i)
        {
            for (int j = 5; j < 55; ++j)
            {
                if (i == 5 || i == 19 || j == 5 || j == 54)
                {
                    // Temporäre Lösung für die Wände
                    var wall_key = Tuple.Create(j, i);
                    if (i == 5)
                    {
                        if (j == 5) walls[wall_key] = '┌';
                        else if (j == 54) walls[wall_key] = '┐';
                        else walls[wall_key] = '─';
                    }
                    else if (i == 19)
                    {
                        if (j == 5) walls[wall_key] = '└';
                        else if (j == 54) walls[wall_key] = '┘';
                        else walls[wall_key] = '─';
                    }
                    else if (j == 5 || j == 54)
                    {
                        walls[wall_key] = '│';
                    }
                }
            }
        }

                // IDs für Threads registrieren
                graphics_id = msg.register();
        logic_id = msg.register();

        msg.send(graphics_id, 1, ""); // Initiale Map zeichnen  

        terminal.cols = (short)(width / 8);     // Zuordnung der Größe des Fenster zu den Spalten
        terminal.rows = (short)(height / 16);   // Zuordnung der Größe des Fenster zu den Zeilen

        player_x = terminal.cols / 2;    // Spieler in der Mitte starten
        player_y = terminal.rows / 2;    // Spieler in der Mitte starten

        terminal.buf = new char[terminal.cols, terminal.rows]; // Puffer erneut erstellen 

        // Worker-Thread für Graphics starten
        Thread graphics = new Thread(Compute_graphics);
        graphics.IsBackground = true;
        graphics.Start();

        Thread logic = new Thread(Compute_logic);
        logic.IsBackground = true;
        logic.Start();

        // GUI starten
        Application.Run(terminal);
    }
}
