namespace YoutubeDownloaderCS
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            txtUrl = new TextBox();
            btnColar = new Button();
            btnBuscar = new Button();
            btnBaixar = new Button();
            picThumbnail = new PictureBox();
            label1 = new Label();
            cmbQualidade = new ComboBox();
            btnAtualizar = new Button();
            btnSobre = new Button();
            lblStatus = new Label();
            progressBar1 = new ProgressBar();
            lblPorcentagem = new Label();
            saveFileDialog1 = new SaveFileDialog();
            folderBrowserDialog1 = new FolderBrowserDialog();
            btnCancelar = new Button();
            btnDoar = new Button();
            ((System.ComponentModel.ISupportInitialize)picThumbnail).BeginInit();
            SuspendLayout();
            // 
            // txtUrl
            // 
            resources.ApplyResources(txtUrl, "txtUrl");
            txtUrl.BackColor = Color.FromArgb(45, 45, 48);
            txtUrl.BorderStyle = BorderStyle.FixedSingle;
            txtUrl.ForeColor = Color.WhiteSmoke;
            txtUrl.Name = "txtUrl";
            // 
            // btnColar
            // 
            resources.ApplyResources(btnColar, "btnColar");
            btnColar.BackColor = Color.FromArgb(60, 60, 60);
            btnColar.FlatAppearance.BorderSize = 0;
            btnColar.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnColar.ForeColor = Color.WhiteSmoke;
            btnColar.Name = "btnColar";
            btnColar.UseVisualStyleBackColor = false;
            btnColar.Click += btnColar_Click;
            // 
            // btnBuscar
            // 
            resources.ApplyResources(btnBuscar, "btnBuscar");
            btnBuscar.BackColor = Color.FromArgb(60, 60, 60);
            btnBuscar.FlatAppearance.BorderSize = 0;
            btnBuscar.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnBuscar.ForeColor = Color.WhiteSmoke;
            btnBuscar.Name = "btnBuscar";
            btnBuscar.UseVisualStyleBackColor = false;
            btnBuscar.Click += btnBuscar_Click;
            // 
            // btnBaixar
            // 
            resources.ApplyResources(btnBaixar, "btnBaixar");
            btnBaixar.BackColor = Color.FromArgb(0, 122, 204);
            btnBaixar.FlatAppearance.BorderSize = 0;
            btnBaixar.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 140, 230);
            btnBaixar.ForeColor = Color.White;
            btnBaixar.Name = "btnBaixar";
            btnBaixar.UseVisualStyleBackColor = false;
            btnBaixar.Click += btnBaixar_Click;
            // 
            // picThumbnail
            // 
            resources.ApplyResources(picThumbnail, "picThumbnail");
            picThumbnail.BackColor = Color.FromArgb(20, 20, 20);
            picThumbnail.BorderStyle = BorderStyle.FixedSingle;
            picThumbnail.Name = "picThumbnail";
            picThumbnail.TabStop = false;
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            label1.ForeColor = Color.WhiteSmoke;
            label1.Name = "label1";
            // 
            // cmbQualidade
            // 
            resources.ApplyResources(cmbQualidade, "cmbQualidade");
            cmbQualidade.BackColor = Color.FromArgb(45, 45, 48);
            cmbQualidade.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQualidade.ForeColor = Color.WhiteSmoke;
            cmbQualidade.FormattingEnabled = true;
            cmbQualidade.Name = "cmbQualidade";
            // 
            // btnAtualizar
            // 
            resources.ApplyResources(btnAtualizar, "btnAtualizar");
            btnAtualizar.BackColor = Color.FromArgb(60, 60, 60);
            btnAtualizar.FlatAppearance.BorderSize = 0;
            btnAtualizar.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnAtualizar.ForeColor = Color.WhiteSmoke;
            btnAtualizar.Name = "btnAtualizar";
            btnAtualizar.UseVisualStyleBackColor = false;
            btnAtualizar.Click += btnAtualizar_Click;
            // 
            // btnSobre
            // 
            resources.ApplyResources(btnSobre, "btnSobre");
            btnSobre.BackColor = Color.FromArgb(60, 60, 60);
            btnSobre.FlatAppearance.BorderSize = 0;
            btnSobre.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
            btnSobre.ForeColor = Color.WhiteSmoke;
            btnSobre.Name = "btnSobre";
            btnSobre.UseVisualStyleBackColor = false;
            btnSobre.Click += btnSobre_Click;
            // 
            // lblStatus
            // 
            resources.ApplyResources(lblStatus, "lblStatus");
            lblStatus.ForeColor = Color.Silver;
            lblStatus.Name = "lblStatus";
            // 
            // progressBar1
            // 
            resources.ApplyResources(progressBar1, "progressBar1");
            progressBar1.Name = "progressBar1";
            // 
            // lblPorcentagem
            // 
            resources.ApplyResources(lblPorcentagem, "lblPorcentagem");
            lblPorcentagem.ForeColor = Color.WhiteSmoke;
            lblPorcentagem.Name = "lblPorcentagem";
            // 
            // saveFileDialog1
            // 
            resources.ApplyResources(saveFileDialog1, "saveFileDialog1");
            // 
            // folderBrowserDialog1
            // 
            resources.ApplyResources(folderBrowserDialog1, "folderBrowserDialog1");
            // 
            // btnCancelar
            // 
            resources.ApplyResources(btnCancelar, "btnCancelar");
            btnCancelar.BackColor = Color.FromArgb(60, 60, 60);
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 40, 40);
            btnCancelar.ForeColor = Color.LightCoral;
            btnCancelar.Name = "btnCancelar";
            btnCancelar.UseVisualStyleBackColor = false;
            btnCancelar.Click += btnCancelar_Click;
            // 
            // btnDoar
            // 
            resources.ApplyResources(btnDoar, "btnDoar");
            btnDoar.BackColor = Color.FromArgb(255, 185, 0);
            btnDoar.FlatAppearance.BorderSize = 0;
            btnDoar.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 200, 50);
            btnDoar.ForeColor = Color.Black;
            btnDoar.Name = "btnDoar";
            btnDoar.UseVisualStyleBackColor = false;
            btnDoar.Click += btnDoar_Click;
            // 
            // Form1
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(32, 32, 32);
            Controls.Add(btnDoar);
            Controls.Add(btnCancelar);
            Controls.Add(lblPorcentagem);
            Controls.Add(progressBar1);
            Controls.Add(lblStatus);
            Controls.Add(btnSobre);
            Controls.Add(btnAtualizar);
            Controls.Add(cmbQualidade);
            Controls.Add(label1);
            Controls.Add(picThumbnail);
            Controls.Add(btnBaixar);
            Controls.Add(btnBuscar);
            Controls.Add(btnColar);
            Controls.Add(txtUrl);
            ForeColor = Color.WhiteSmoke;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            ((System.ComponentModel.ISupportInitialize)picThumbnail).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox txtUrl;
        private System.Windows.Forms.Button btnColar;
        private System.Windows.Forms.Button btnBuscar;
        private System.Windows.Forms.Button btnBaixar;
        private System.Windows.Forms.PictureBox picThumbnail;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbQualidade;
        private System.Windows.Forms.Button btnAtualizar;
        private System.Windows.Forms.Button btnSobre;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label lblPorcentagem;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button btnCancelar;
        private System.Windows.Forms.Button btnDoar; // Variável do novo botão
    }
}