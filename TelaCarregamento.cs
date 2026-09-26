using System;
using System.Drawing;
using System.Windows.Forms;

namespace YoutubeDownloaderCS
{
    public class TelaCarregamento : Form
    {
        private Label lblMensagem;
        public TelaCarregamento(string texto)
        {
            this.TopMost = true; this.Size = new Size(300, 100); this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen; this.BackColor = Color.White;
            this.Paint += (s, e) => e.Graphics.DrawRectangle(Pens.Black, 0, 0, Width - 1, Height - 1);
            lblMensagem = new Label { Text = texto, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI", 10) };
            var pb = new ProgressBar { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Bottom, Height = 20 };
            this.Controls.Add(lblMensagem); this.Controls.Add(pb);
            lblMensagem.Top = (this.ClientSize.Height - pb.Height - lblMensagem.Height) / 2;
        }
        public void AtualizarMensagem(string t) { if (lblMensagem != null) { lblMensagem.Text = t; Application.DoEvents(); } }
    }
}
