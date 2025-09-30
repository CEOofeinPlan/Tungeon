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
        public float gamma = 1.0f; // @brief <1= dunkler, >1 = heller

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

            ClearBuf();

            this.Paint += ASCIITerminal_Paint;      // Passt als Handler // Einfach so lassen
        }

        private void ASCIITerminal_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);

            for (int y = 0; y < rows; ++y)
            {
                for (int x = 0; x < cols; ++x)
                {
                    char c = buf[x, y];
                    if (c != '\0' && c != ' ')
                    {
                        e.Graphics.DrawString(c.ToString(), this.Font, Brushes.LightGray, x * charWidth-3, y * charHeight); // 3px nach Links
                    }
                }
            }
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