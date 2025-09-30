using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Better_Terminal
{
    // ASCII Terminal
    public class ASCIITerminal : Form
    {
        public short cols = 80;
        public short rows = 25;
        private int charWidth;
        private int charHeight;
        public char[,] buf;
        public HashSet<Tuple<int, int, byte>> table; // ASCII Zeichen -> Bitmap Index, sofern Helligkeit vorhanden ist
        private readonly object table_Lock = new object();    // Damit keine Fehler mit dem HashSet entstehen

        private bool IstWand(char c) =>
            c == '┌' || c == '┐' || c == '─' || c == '└' || c == '┘' || c == '│';

        public ASCIITerminal()
        {
            this.Text = "ASCII Terminal";
            this.StartPosition = FormStartPosition.CenterScreen;

            var font = new Font("Consolas", 11, FontStyle.Regular);
            this.Font = font;

            charWidth = 8;      // Standard Größe im Terminal
            charHeight = 8;   // Standard Größe

            this.ClientSize = new Size(1200, 800);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.Cursor = Cursors.Default;

            buf = new char[cols, rows];
            table = new HashSet<Tuple<int, int, byte>>();

            ClearBuf();

            this.Paint += ASCIITerminal_Paint;      // Passt als Handler // Einfach so lassen
        }

        private bool lineOfSight(int w, int h, int x0, int y0, int x1, int y1, HashSet<Tuple<int, int, byte>> newSet)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy; // Fehlerwert e_xy

            int x = x0, y = y0;

            while (true)
            {
                bool startCell = (x == x0 && y == y0);

                // Prüfen ob aktuelle Zelle eine Wand ist (außer Startzelle)
                if (!startCell && IstWand(buf[x, y]))
                {
                    // Wand selbst sichtbar machen (volle Helligkeit)
                    newSet.Add(Tuple.Create(x, y, (byte)255));
                    return false;      // Strahl endet hier
                }

                // Ziel erreicht
                if (x == x1 && y == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }

            return true; // kein Hindernis zwischen Start und Ziel
        }

        public void ComputeFovGamma(int w, int h, int px, int py, int radius, double gamma = 1.0)
        {
            var newSet = new HashSet<Tuple<int, int, byte>>();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int dx = x - px;
                    int dy = y - py;

                    // Innerhalb des Kreises?
                    int dist2 = dx * dx + dy * dy;
                    if (dist2 > radius * radius)
                        continue;

                    // Sichtlinie prüfen
                    if (lineOfSight(w, h, px, py, x, y, newSet))
                    {
                        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        double intensity = 1.0 - (dist / (double)radius);

                        if (intensity < 0) intensity = 0;

                        // Gamma-Korrektur
                        double corrected = Math.Pow(intensity, gamma);

                        byte alpha = (byte)(corrected * 255);

                        if (alpha > 0)
                            newSet.Add(Tuple.Create(x, y, alpha));
                    }
                }
            }

            // Thread-sicher aktualisieren
            lock (table_Lock)
            {
                table.Clear();
                table.UnionWith(newSet);
            }

            // Neu zeichnen
            this.Invalidate();
        }

        public void Build_gamma()
        {
            // Neuer Satz für gefilterte sichtbare Zeichen
            var newSet = new HashSet<Tuple<int, int, byte>>();

            // HashSet kann man nur mit foreach durchlaufen
            foreach (var t in table)
            {
                int x = t.Item1;
                int y = t.Item2;
                byte alpha = t.Item3;

                if (alpha == 0)
                    continue; // Helligkeit 0 überspringen

                newSet.Add(Tuple.Create(x, y, alpha));
            }

            // Thread-sicher ersetzen
            lock (table_Lock)
            {
                table.Clear();
                table.UnionWith(newSet);
            }

            // optional neu zeichnen
            this.Invalidate();
        }

        private void ASCIITerminal_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);

            // Keine Zusammenstöße mit dem HashSet

            lock (table_Lock)
            {
                foreach (var t in table)
                {
                    int x = t.Item1;
                    int y = t.Item2;
                    byte alpha = t.Item3; // aktuell 0..7 oder 0..255

                    // Alpha auf 0..255 skalieren, falls es kleiner ist
                    int scaledAlpha = alpha;
                    if (alpha <= 7) // 8 Helligkeitsstufen
                        scaledAlpha = (int)(alpha / 7.0 * 255);

                    char c = buf[x, y];
                    //if (c == '\0' || c == ' ') continue;
                    
                    if (c == ' ') c = '●'; // Leerzeichen sichtbar machen '█' '●'
                    if (c == '0') c = '0';

                    using (var brush = new SolidBrush(Color.FromArgb(scaledAlpha, Color.LightGray)))
                    {
                        e.Graphics.DrawString(c.ToString(), this.Font, brush, x * charWidth, y * charHeight);
                    }
                }
            }

            /*e.Graphics.Clear(Color.Black);
            using var brush = new SolidBrush(Color.FromArgb(40, Color.LightGray));
            e.Graphics.DrawString("TEST", this.Font, brush, 0, 0);*/
        }

        public void ClearBuf()
        {
            for (int y = 0; y < rows; ++y)
                for (int x = 0; x < cols; ++x)
                    buf[x, y] = ' ';
            this.Invalidate();
        }

        // Thread-sicherer Schreibzugriff
        public void WriteAt(string text, int x, int y)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => WriteAt(text, x, y)));
                return;
            }

            for (int i = 0; i < text.Length && (x + i) < cols; ++i)
            {
                buf[x + i, y] = text[i];
            }
            this.Invalidate();
        }
    }
}