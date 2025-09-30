using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using MSG;
using Better_Terminal;
using System.Runtime.InteropServices;
using System.IO.Pipes;
using Hilfe;

internal static class Program

{
    static bool running = true; // Wenn false, dann beenden

    static ASCIITerminal terminal = new ASCIITerminal(); // Fenster Objekt
    static Msg msg = new Msg();                          // Nachrichten zwischen Threads

    static short graphics_id;                            // Postfach-ID der Grafik Einheit
    static short logic_id;                               // Postfach-ID der Logik Einheit

    // static int width = 800;                              // Aktuell nicht genutzt
    // static int height = 400;                             // Aktuell nicht genutzt

    static int player_x;                                 // AKTUELLE Spieler X Position
    static int player_y;                                 // AKTUELLE Spieler Y Position
    static int old_player_x;                             // VORHERIGE Spieler X Position
    static int old_player_y;                             // VORHERIGE Spieler Y Position

    // Position aller Wände auf der aktuellen Map und ein wenig darüber hinaus
    static Dictionary<Tuple<int, int>, char> walls = new Dictionary<Tuple<int, int>, char>();

    // Funktion zur Steuerung der Grafik 
    //
    // WICHTIG: Funktion malt NUR, wenn es ausdrücklich im Grafik Postfach steht!
    //
    static void Compute_graphics()
    {
        while (running)
        {
            // Durch die Postfach ID aktuelle Pakete abfragen
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
                                    terminal.WriteAt("0", j, i);    // Spieler Zeichen
                                else if (walls.ContainsKey(key))
                                    terminal.WriteAt(walls[key].ToString(), j, i);
                                else
                                    terminal.WriteAt(" ", j, i);    // Leere
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

    // Funktion zur Steuerung der Logik
    static void Compute_logic()
    {
        // DLL Import für Key State Abfrage
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);

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

            // Erkenne Wand und bestimme Position des Spielers
            var nextPos = Tuple.Create(player_x + dx, player_y + dy);
            if (!walls.ContainsKey(nextPos))
            {   
                // Prüft ob der Spieler eine Wand vor sich hat
                player_x = Math.Clamp(player_x + dx, 0, terminal.cols - 1);
                player_y = Math.Clamp(player_y + dy, 0, terminal.rows - 1);

                // Prüfen 
                if (dx != 0 || dy != 0)
                {
                    // Nachricht an Grafik Einheit senden 
                    msg.send(graphics_id, 2, "");
                    // Berechnung der veränderten Positionen, um das Gamma neu zu errechnen bzw. durch Wände zu blocken
                    terminal.ComputeFovGamma(terminal.cols, terminal.rows, player_x, player_y, 3, 1.0);
                    terminal.Build_gamma(); // Neues Gamma wird Buffer hinzugefügt
                }   
            }

            Thread.Sleep(100);
        }
    }

    [STAThread]
    static void Main()
    {   
        // DLL Import um die Konsole trz. WinForm aufrufen zu können 
        //
        // WICHTIG: Aktuell noch zum einfachen Debuggen, SPÄTER ENTFERNEN
        //
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        // Start Einstellungen für das Grafische Interface
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Schneller Debug Test und Initalisierung der Konsole, SPÄTER ENTFERNEN
        AllocConsole();
        Console.WriteLine("!");

        // Terminal Setup
        terminal.cols = 80; // Zeichen max. im Buffer (Später noch berechnen lassen)
        terminal.rows = 25; // Reihen max. im Buffer (Später noch berechnen lassen)

        // Erneute Initialisierung des Buffers auf Grund von mehr Kontrolle. 
        terminal.buf = new char[terminal.cols, terminal.rows];

        player_x = terminal.cols / 2; // Position des Spielers ca. in der Mitte
        player_y = terminal.rows / 2; // Position des Spielers ca. in der Mitte

        // Wände erstellen
        //
        // Vielleicht noch die Räume größer machen später
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

        // Initialisierung der Map
        msg.send(graphics_id, 1, "");

        // Threads starten
        //
        // Grafik Einheit
        //
        Thread graphics = new Thread(Compute_graphics) { IsBackground = true };
        graphics.Start();
        //
        // Logik Einheit
        //
        Thread logic = new Thread(Compute_logic) { IsBackground = true };
        logic.Start();

        // Erste schnelle Berechnung des Gamma FOV
        terminal.ComputeFovGamma(terminal.cols, terminal.rows, player_x, player_y, 3, 1.0);

        // Schnelle Auslegung des Gammas auf das HashSet in 'Better_Terminal'
        terminal.Build_gamma();

        // GUI starten
        Application.Run(terminal);
    }
};