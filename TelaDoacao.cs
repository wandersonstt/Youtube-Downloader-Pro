using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace YoutubeDownloaderCS
{
    public class TelaDoacao : Form
    {
        public TelaDoacao(string linkLivePix)
        {
            this.Text = "Apoie o Projeto ❤"; this.Size = new Size(350, 450); this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog; this.MaximizeBox = false; this.MinimizeBox = false; this.BackColor = Color.FromArgb(32, 32, 32);
            Label lblTitulo = new Label { Text = "Gostou do programa?", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(70, 20) };
            Label lblDesc = new Label { Text = "Escaneie o QR Code abaixo para doar\nqualquer valor via LivePix e apoiar o dev!", Font = new Font("Segoe UI", 9), ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, AutoSize = true, Location = new Point(50, 60) };
            PictureBox picQR = new PictureBox { Size = new Size(200, 200), Location = new Point(65, 110), SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.White, Padding = new Padding(5) };
            try { picQR.Load($"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={linkLivePix}"); } catch { picQR.BackColor = Color.Red; }
            Button btnAbrir = new Button { Text = "Abrir Link no Navegador", Size = new Size(200, 35), Location = new Point(65, 330), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnAbrir.FlatAppearance.BorderSize = 0;
            btnAbrir.Click += (s, e) => { Process.Start(new ProcessStartInfo { FileName = linkLivePix, UseShellExecute = true }); };
            this.Controls.Add(lblTitulo); this.Controls.Add(lblDesc); this.Controls.Add(picQR); this.Controls.Add(btnAbrir);
        }
    }
}
