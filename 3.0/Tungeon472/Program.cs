using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Better_Terminal;
using MSG;
using ComputeSharp;

namespace Tungeon
{
	internal static class Program
	{
        static bool running = true; // Wenn false, dann beenden

        static ASCIITerminal terminal; // Fenster Objekt
        static Msg msg = new Msg();    // Nachrichten zwischen Threads
        
        static short graphics_id;      // Postfach-ID der Grafik Einheit
        static short logic_id;         // Postfach-ID der Logik Einheit

        // static int width = 800;        // Aktuell nicht genutzt
        // static int height = 400;       // Aktuell nicht genutzt

        static int player_x;           // AKTUELLE Spieler X Position
        static int player_y;           // AKTUELLE Spieler Y Position
        static int old_player_x;       // VORHERIGE Spieler X Position
        static int old_player_y;       // VORHERIGE Spieler Y Position

        // Position aller Wände der aktuellen Map und ein wenig darüber hinaus
        static Dictionary<Tuple<int, int>, char> walls = new Dictionary<Tuple<int, int>, char>();

        // Funktion zu Steuerung der Grafik
        //
        // WICHTIG: Funktion malt NUR, wenn es ausdrücklich im Grafik Postfach steht!
        //
        static void Compute_graphics()
        {
            while (running)
            {
                // Durch die Postfach ID aktuelle Paketet abfragen
                var packet = msg.receive(graphics_id);
                if (packet != null)
                {
                    switch (packet.Value.task)
                    {
                        case 1: // Draw Map
                            //walls.Clear(); // TESTWEISE
                            // Geht besser
                            // Erst alles in Leerzeichen machen
                            // Dann Spieler einsetzten und dann die Wände
                            terminal.ClearBuf();
                            foreach (var key in walls)
                            {
                                var k = key.Key;
                                terminal.WriteAt(walls.ToString(), k.Item1, k.Item2);
                            }
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
                            // Erste schnelle Berechnung des Gamma FOV
                            terminal.ComputeFovGamma(player_x+10, player_y+10, player_x, player_y, 10, 1.0);
            
                            // Schnelle Auslegung der Gammas auf das HashSet in 'Better_Terminal'
                            terminal.Build_gamma();
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

        // DLL Import für keys State Abfrage (Ältere Version: Es muss außerhalb jeglicher Methode stehen)
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);
        static void Compute_logic()
        {
            while (running)
            {
                // W A S D Steuerung
                bool up = (GetKeyState((int)Keys.W) & 0x8000) != 0;
                bool down = (GetKeyState((int)Keys.S) & 0x8000) != 0;
                bool left = (GetKeyState((int)Keys.A) & 0x8000) != 0;
                bool right = (GetKeyState((int)Keys.D) & 0x8000) != 0;

                int time = 100; // Normale Zeit; // Beim diagonalen Laufen 141

                // Bewegung berechnen
                int dx = 0, dy = 0;
                if (up) dy--;
                if (down) dy++;
                if (left) dx--;
                if (right) dx++;

                if (dx != 0 && dy != 0)
                {
                    time = 141;
                }

                // Erkenne Wand und bestimme Position des Spielers
                var nextPos = Tuple.Create(player_x + dx, player_y + dy);
                if (!walls.ContainsKey(nextPos))
                {
                    // Übergabe der alten Position
                    old_player_x = player_x;
                    old_player_y = player_y;
                    
                    // Prüft ob der Spieler eine Wand vor sich hat (Ältere Version: Math.Clamp existiet hier noch nicht)
                    player_x = Math.Max(Math.Min(player_x + dx, terminal.cols - 1), 0);
                    player_y = Math.Max(Math.Min(player_y + dy, terminal.rows - 1), 0);

                    // Prüfen
                    if (dx != 0 || dy != 0)
                    {   
                        Console.WriteLine("!");
                        // Nachricht an Grafik Einheit senden
                        terminal.WriteAt("0", player_x, player_y);
                        terminal.WriteAt(" ", old_player_x, old_player_y);
                        // Berechnung der veränderten Positionen, um das Gamma neu zu errechnen bzw. durch Wände zu blocken
                        terminal.ComputeFovGamma(player_x+10, player_y+10, player_x, player_y, 10, 1.0);
                        terminal.Build_gamma(); // Neues Gamma wird zum Buffer hinzugefügt
                    }   
                }

                Thread.Sleep(time);
            }
        }

        // DLL Import um die Konsole trz. WinForm aufrufen zu können (Ältere Version: Es muss außerhalb jeglicher Methode stehen)
        //
        // WICHTIG: Aktuell noch zum einfachen Debuggen, SPÄTER ENTFERNEN
        //
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [STAThread]
        static void Main()
        {
            // Start Einstellungen für das Grafische Interface
            //
            // ApplicationConfiguration.Initialize();
            //
            // Der Befehl oben darüber nicht benutzten, ältere Versionen kommen damit nicht klar
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Schneller Debug Test und Initalisierung der Konsole, SPÄTER ENTFERNEN
            AllocConsole();
            Console.WriteLine("!");

            // Initialisierung des Fenster Objektes (Ältere Version: Muss in einer Methode initialisiert werden)
            terminal = new ASCIITerminal();

            // Terminal Setup
            terminal.cols = 150; // Zeichen max. im Buffer (Später noch berechnen lassen)
            terminal.rows = 150; // Reihen max. im Buffer (Später noch berechnen lassen)

            // Erneute Initialisierung des Buffers auf Grund von mehr Kontrolle. 
            terminal.buf = new char[terminal.cols, terminal.rows];

            player_x = 20; // Position des Spielers oben links
            player_y = 20; // Position des Spielers oben links

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

            // Setzten des Spielers

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

            // GUI starten
            Application.Run(terminal);
        }
    }
}
