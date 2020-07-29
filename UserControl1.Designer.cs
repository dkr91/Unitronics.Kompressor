namespace Unitronics.ComDriver
{
    partial class UserControl1
    {
        /// <summary> 
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Komponenten-Designer generierter Code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.login = new System.Windows.Forms.Button();
            this.Cancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.IP = new System.Windows.Forms.TextBox();
            this.Port_text = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // login
            // 
            this.login.Location = new System.Drawing.Point(30, 150);
            this.login.Name = "login";
            this.login.Size = new System.Drawing.Size(100, 35);
            this.login.TabIndex = 0;
            this.login.Text = "&Log in";
            this.login.UseVisualStyleBackColor = true;
            this.login.Click += new System.EventHandler(this.login_clicked);
            // 
            // Cancel
            // 
            this.Cancel.Location = new System.Drawing.Point(170, 150);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(100, 35);
            this.Cancel.TabIndex = 1;
            this.Cancel.Text = "&Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            this.Cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(30, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(17, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "IP";
            this.label1.Click += new System.EventHandler(this.IP_clicked);
            // 
            // IP
            // 
            this.IP.Location = new System.Drawing.Point(30, 35);
            this.IP.Name = "IP";
            this.IP.Size = new System.Drawing.Size(250, 20);
            this.IP.TabIndex = 4;
            this.IP.Text = " ";
            this.IP.TextChanged += new System.EventHandler(this.IP_Changed);
            // 
            // Port_text
            // 
            this.Port_text.Location = new System.Drawing.Point(30, 115);
            this.Port_text.Name = "Port_text";
            this.Port_text.Size = new System.Drawing.Size(250, 20);
            this.Port_text.TabIndex = 3;
            this.Port_text.Click += new System.EventHandler(this.Port_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(30, 99);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Port";
            // 
            // UserControl1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label2);
            this.Controls.Add(this.IP);
            this.Controls.Add(this.Port_text);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.login);
            this.Name = "UserControl1";
            this.Size = new System.Drawing.Size(300, 200);
            this.Load += new System.EventHandler(this.UserControl1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Button Cancel;
        private System.Windows.Forms.TextBox IP;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button login;
        private System.Windows.Forms.TextBox Port_text;

        #endregion

        private System.Windows.Forms.Label Port;
        private System.Windows.Forms.TextBox textBox2;
    }
}
