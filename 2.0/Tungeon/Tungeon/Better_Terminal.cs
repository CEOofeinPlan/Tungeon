using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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

        public ASCIITerminal()
        {
            this.Text = "ASCII Terminal";
            this.StartPosition = FormStartPosition.CenterScreen;

            var font = new Font("Consolas", 11, FontStyle.Regular);
            this.Font = font;

            charWidth = 8;      // Standard Größe im Terminal
            charHeight = 16;    // Standard Größe

            this.ClientSize = new Size(cols * charWidth, rows * charHeight);
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.Cursor = Cursors.Default;

            buf = new char[cols, rows];
            table = new HashSet<Tuple<int, int, byte>>();

            ClearBuf();

            this.Paint += ASCIITerminal_Paint;      // Passt als Handler // Einfach so lassen
        }
        public void Build_gamma(int[,] xy)
        {
            // HashSet für sichtbare Zeichen (i, j, Helligkeit)
            var newSet = new HashSet<Tuple<int, int, byte>>();

            int rows = xy.GetLength(0); // Anzahl der Elemente in xy
            int cols = xy.GetLength(1); // sollte 3 sein: x, y, alpha

            if (cols < 3) throw new ArgumentException("xy muss mindestens 3 Spalten haben: x, y, alpha");

            for (int i = 0; i < rows; i++)
            {
                int x = xy[i, 0];
                int y = xy[i, 1];
                byte alpha = (byte)xy[i, 2];

                if (alpha == 0) continue; // Helligkeit 0 überspringen

                newSet.Add(Tuple.Create(x, y, alpha));
            }

            // Keine Zusammenstöße mit dem HashSet
            lock (table_Lock)
            {

                table.Clear();
                table.UnionWith(newSet); // fügt alle sichtbaren Werte hinzu
            }

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
                    if (alpha <= 7) // falls dein Cache nur 8 Helligkeitsstufen nutzt
                        scaledAlpha = (int)(alpha / 7.0 * 255);

                    char c = buf[x, y];
                    //if (c == '\0' || c == ' ') continue;

                    if (c == ' ') c = '·'; // Leerzeichen sichtbar machen

                    using var brush = new SolidBrush(Color.FromArgb(scaledAlpha, Color.LightGray));
                    e.Graphics.DrawString(c.ToString(), this.Font, brush, x * charWidth, y * charHeight);
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