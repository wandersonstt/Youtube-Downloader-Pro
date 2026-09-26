using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YoutubeDownloaderCS
{
    // Aparência do app num lugar só. Tudo aqui usa desenho nativo do WinForms: os controles
    // padrão do Windows (ComboBox, ProgressBar, TabControl) ignoram BackColor no tema claro
    // do sistema e apareciam brancos no meio de uma janela escura.
    internal static class Tema
    {
        public static readonly Color Fundo = Color.FromArgb(23, 23, 26);
        public static readonly Color Superficie = Color.FromArgb(35, 35, 38);
        public static readonly Color SuperficieClara = Color.FromArgb(46, 46, 51);
        public static readonly Color Borda = Color.FromArgb(56, 56, 63);
        public static readonly Color Texto = Color.FromArgb(244, 244, 245);
        public static readonly Color TextoFraco = Color.FromArgb(160, 160, 171);
        public static readonly Color Destaque = Color.FromArgb(230, 33, 23);
        public static readonly Color DestaqueClaro = Color.FromArgb(255, 60, 48);
        public static readonly Color Perigo = Color.FromArgb(239, 68, 68);

        public static readonly Font Corpo = new("Segoe UI", 9.5f);
        public static readonly Font Rotulo = new("Segoe UI", 9f);
        public static readonly Font Botao = new("Segoe UI Semibold", 9.5f);
        public static readonly Font Numero = new("Segoe UI Semibold", 12f);

        // Windows 11 desenha a barra de título clara por padrão, o que destoa de uma janela
        // escura. Este atributo do DWM pinta a barra de escuro.
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valor, int tamanho);

        public static void BarraDeTituloEscura(Form form)
        {
            try
            {
                int ligado = 1;
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE; 19 era o número usado no Windows 10 inicial.
                if (DwmSetWindowAttribute(form.Handle, 20, ref ligado, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref ligado, sizeof(int));
            }
            catch { /* versões antigas do Windows simplesmente não têm o atributo */ }
        }

        public static GraphicsPath Cantos(Rectangle r, int raio)
        {
            var caminho = new GraphicsPath();
            if (raio <= 0 || r.Width <= 0 || r.Height <= 0) { caminho.AddRectangle(r); return caminho; }

            int d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
            caminho.AddArc(r.X, r.Y, d, d, 180, 90);
            caminho.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            caminho.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            caminho.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            caminho.CloseFigure();
            return caminho;
        }

        // Recorta o controle em cantos arredondados e mantém o recorte quando ele muda de tamanho.
        public static void Arredondar(Control c, int raio)
        {
            void Aplicar()
            {
                if (c.Width <= 0 || c.Height <= 0) return;
                using var caminho = Cantos(new Rectangle(0, 0, c.Width, c.Height), raio);
                c.Region?.Dispose();
                c.Region = new Region(caminho);
            }
            Aplicar();
            c.Resize += (s, e) => Aplicar();
        }

        public enum Estilo { Primario, Secundario, Fantasma, Aviso }

        public static void EstilizarBotao(Button b, Estilo estilo, int raio = 8)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.Font = Botao;
            b.Cursor = Cursors.Hand;

            switch (estilo)
            {
                case Estilo.Primario:
                    b.BackColor = Destaque;
                    b.ForeColor = Color.White;
                    b.FlatAppearance.MouseOverBackColor = DestaqueClaro;
                    b.FlatAppearance.MouseDownBackColor = Color.FromArgb(200, 28, 20);
                    break;
                case Estilo.Secundario:
                    b.BackColor = Superficie;
                    b.ForeColor = Texto;
                    b.FlatAppearance.MouseOverBackColor = SuperficieClara;
                    b.FlatAppearance.MouseDownBackColor = Borda;
                    break;
                case Estilo.Fantasma:
                    b.BackColor = Fundo;
                    b.ForeColor = TextoFraco;
                    b.FlatAppearance.MouseOverBackColor = Superficie;
                    b.FlatAppearance.MouseDownBackColor = Borda;
                    // Sem recorte por Region: o fundo do botão já é o da janela, então basta
                    // desenhar a borda curva por cima. Recortar deixava traços soltos de 1px
                    // onde o retângulo nativo não coincidia exatamente com a curva.
                    b.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using var contorno = Cantos(new Rectangle(0, 0, b.Width - 1, b.Height - 1), raio);
                        using var caneta = new Pen(Borda);
                        e.Graphics.DrawPath(caneta, contorno);
                    };
                    return;
                case Estilo.Aviso:
                    b.BackColor = Superficie;
                    b.ForeColor = Color.FromArgb(255, 196, 61);
                    b.FlatAppearance.MouseOverBackColor = SuperficieClara;
                    b.FlatAppearance.MouseDownBackColor = Borda;
                    break;
            }

            Arredondar(b, raio);
        }

        public static void DesenharChevron(Graphics g, Rectangle area, Color cor)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = area.X + area.Width / 2, cy = area.Y + area.Height / 2;
            using var caneta = new Pen(cor, 1.6f);
            g.DrawLines(caneta, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
        }
    }

    // O ComboBox nativo pinta o campo fechado e a seta com as cores do tema do Windows e
    // ignora BackColor, o que deixava um retângulo branco no meio da janela escura.
    internal sealed class ComboEscuro : ComboBox
    {
        public ComboEscuro()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            BackColor = Tema.Superficie;
            ForeColor = Tema.Texto;
            Font = Tema.Corpo;
            ItemHeight = 26;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sob = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            e.Graphics.FillRectangle(new SolidBrush(sob ? Tema.Destaque : Tema.Superficie), e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString() ?? "", Font,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                sob ? Color.White : Tema.Texto,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // 0x000F = WM_PAINT. Repinta por cima o que o controle nativo desenhou: a moldura
            // clara e o botão de seta, que nenhuma propriedade gerenciada alcança.
            if (m.Msg != 0x000F) return;

            using var g = Graphics.FromHwnd(Handle);
            var area = new Rectangle(0, 0, Width, Height);

            using (var moldura = new Pen(Tema.Borda))
                g.DrawRectangle(moldura, 0, 0, Width - 1, Height - 1);

            var botao = new Rectangle(Width - 26, 1, 25, Height - 2);
            g.FillRectangle(new SolidBrush(Tema.Superficie), botao);
            Tema.DesenharChevron(g, botao, Tema.TextoFraco);

            string texto = SelectedItem?.ToString() ?? "";
            if (texto.Length > 0)
            {
                g.FillRectangle(new SolidBrush(Tema.Superficie), new Rectangle(1, 1, Width - 28, Height - 2));
                TextRenderer.DrawText(g, texto, Font,
                    new Rectangle(10, 0, Width - 38, Height), Tema.Texto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    // Com UserPaint o TabControl para de desenhar as abas e a faixa ao lado delas com o tema
    // do Windows (que aparecia branca); tudo passa a ser desenhado aqui.
    internal sealed class AbasEscuras : TabControl
    {
        public AbasEscuras()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(112, 32);
            Font = Tema.Corpo;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Tema.Fundo);

            for (int i = 0; i < TabCount; i++)
            {
                var r = GetTabRect(i);
                bool ativa = SelectedIndex == i;

                e.Graphics.FillRectangle(new SolidBrush(ativa ? Tema.Superficie : Tema.Fundo), r);
                if (ativa)
                    e.Graphics.FillRectangle(new SolidBrush(Tema.Destaque),
                        new Rectangle(r.X + 10, r.Bottom - 2, r.Width - 20, 2));

                TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, r,
                    ativa ? Tema.Texto : Tema.TextoFraco,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Fundo da página, para não sobrar a cor do sistema em volta do conteúdo.
            if (TabCount > 0)
            {
                var pagina = GetTabRect(0);
                e.Graphics.FillRectangle(new SolidBrush(Tema.Superficie),
                    new Rectangle(0, pagina.Bottom, Width, Height - pagina.Bottom));
            }
        }
    }

    // Substitui a ProgressBar nativa, que é desenhada pelo tema do Windows (verde/azul claro)
    // e não aceita cor. Expõe só Value, que é tudo que o app usa.
    internal sealed class BarraProgresso : Control
    {
        private int valor;

        public BarraProgresso()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 8;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => valor;
            set { valor = Math.Clamp(value, 0, 100); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var area = new Rectangle(0, 0, Width - 1, Height - 1);
            int raio = Height / 2;

            using (var trilha = Tema.Cantos(area, raio))
                e.Graphics.FillPath(new SolidBrush(Tema.Superficie), trilha);

            int largura = (int)(area.Width * (valor / 100.0));
            if (largura <= 0) return;

            // Largura mínima para o arredondamento não virar um ponto deformado no início.
            largura = Math.Max(largura, Height);
            using var preenchido = Tema.Cantos(new Rectangle(0, 0, largura, area.Height), raio);
            using var gradiente = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(largura, 1), Height),
                Tema.Destaque, Tema.DestaqueClaro, LinearGradientMode.Horizontal);
            e.Graphics.FillPath(gradiente, preenchido);
        }
    }
}
