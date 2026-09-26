using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YoutubeDownloaderCS
{
    // Janela de login do YouTube embutida no aplicativo.
    //
    // Por que existe: Chrome, Brave e Edge passaram a criptografar os cookies com
    // App-Bound Encryption, e o yt-dlp não consegue mais lê-los no Windows (o erro é
    // "Failed to decrypt with DPAPI"). Como o app já depende do WebView2, ele mantém a
    // própria sessão do YouTube e exporta os cookies em formato Netscape para o yt-dlp.
    public class LoginYoutube : Form
    {
        private readonly WebView2 navegador = new() { Dock = DockStyle.Fill };
        private readonly Label lblInstrucao = new();
        private bool exportou;

        public static string PastaPerfil => Util.CaminhoNaPastaApp("sessao-youtube");
        public static string CaminhoCookies => Util.CaminhoNaPastaApp("cookies.txt");

        // Cookies que só existem quando há uma sessão autenticada do Google/YouTube.
        private static readonly string[] CookiesDeSessao = { "SAPISID", "SID", "__Secure-3PSID" };

        public LoginYoutube()
        {
            Text = "Entrar no YouTube";
            Size = new Size(980, 760);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(32, 32, 32);

            lblInstrucao.Text = "Faça login na sua conta do YouTube nesta janela. Assim que entrar, ela fecha sozinha.";
            lblInstrucao.ForeColor = Color.WhiteSmoke;
            lblInstrucao.Font = new Font("Segoe UI", 9);
            lblInstrucao.Dock = DockStyle.Top;
            lblInstrucao.Height = 34;
            lblInstrucao.TextAlign = ContentAlignment.MiddleLeft;
            lblInstrucao.Padding = new Padding(10, 0, 0, 0);

            var btnConcluir = new Button
            {
                Text = "Já entrei, continuar",
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnConcluir.FlatAppearance.BorderSize = 0;
            btnConcluir.Click += async (s, e) => await ConcluirAsync(exigirLogin: false);

            Controls.Add(navegador);
            Controls.Add(lblInstrucao);
            Controls.Add(btnConcluir);
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                Directory.CreateDirectory(PastaPerfil);
                var ambiente = await CoreWebView2Environment.CreateAsync(null, PastaPerfil);
                await navegador.EnsureCoreWebView2Async(ambiente);

                // Se a sessão anterior ainda valer, fecha sozinha sem incomodar o usuário.
                navegador.CoreWebView2.NavigationCompleted += async (s, args) => await ConcluirAsync(exigirLogin: true);
                navegador.CoreWebView2.Navigate("https://www.youtube.com/");
            }
            catch (Exception ex)
            {
                Logger.Erro("Falha ao abrir a janela de login (WebView2)", ex);
                MessageBox.Show("Não foi possível abrir a janela de login: " + ex.Message, "Erro",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        // exigirLogin: no fechamento automático só aceita sair se realmente houver sessão;
        // no clique do botão, exporta o que houver e deixa o download decidir.
        private async Task ConcluirAsync(bool exigirLogin)
        {
            if (exportou) return;

            try
            {
                var cookies = await ObterCookiesAsync();
                bool temSessao = cookies.Any(c => CookiesDeSessao.Contains(c.Name, StringComparer.OrdinalIgnoreCase));

                if (exigirLogin && !temSessao)
                {
                    lblInstrucao.Text = "Entre na sua conta do YouTube nesta janela. Assim que entrar, ela fecha sozinha.";
                    return;
                }

                exportou = true;
                GravarNetscape(cookies, CaminhoCookies);
                Logger.Info($"Cookies do YouTube exportados ({cookies.Count} itens, sessão={temSessao}).");

                DialogResult = temSessao ? DialogResult.OK : DialogResult.Cancel;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Erro("Falha ao exportar cookies do WebView2", ex);
            }
        }

        private async Task<List<CoreWebView2Cookie>> ObterCookiesAsync()
        {
            var todos = new List<CoreWebView2Cookie>();
            foreach (var origem in new[] { "https://www.youtube.com", "https://www.google.com" })
                todos.AddRange(await navegador.CoreWebView2.CookieManager.GetCookiesAsync(origem));

            // O mesmo cookie aparece nas duas origens; manter um de cada nome+domínio.
            return todos
                .GroupBy(c => c.Name + "|" + c.Domain, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        // Formato Netscape (cookies.txt), que é o esperado pelo yt-dlp em --cookies.
        internal static void GravarNetscape(IEnumerable<CoreWebView2Cookie> cookies, string caminho)
        {
            var texto = new StringBuilder();
            texto.AppendLine("# Netscape HTTP Cookie File");
            texto.AppendLine("# Gerado pelo Youtube Downloader Pro");

            foreach (var c in cookies)
                texto.AppendLine(LinhaNetscape(c.Domain, c.Path, c.IsSecure, c.Expires, c.Name, c.Value));

            File.WriteAllText(caminho, texto.ToString(), new UTF8Encoding(false));
        }

        internal static string LinhaNetscape(string dominio, string caminho, bool seguro, DateTime expira, string nome, string valor)
        {
            bool incluiSubdominios = dominio.StartsWith(".", StringComparison.Ordinal);

            long expiraEm;
            try
            {
                // Cookies de sessão não têm validade: o yt-dlp aceita 0 nesse campo.
                expiraEm = expira <= new DateTime(1971, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    ? 0
                    : new DateTimeOffset(expira.ToUniversalTime()).ToUnixTimeSeconds();
            }
            catch { expiraEm = 0; }

            return string.Join("\t",
                dominio,
                incluiSubdominios ? "TRUE" : "FALSE",
                string.IsNullOrEmpty(caminho) ? "/" : caminho,
                seguro ? "TRUE" : "FALSE",
                expiraEm.ToString(CultureInfo.InvariantCulture),
                nome,
                valor);
        }
    }
}
