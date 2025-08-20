using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class OpenUserGpo : Form
    {

        // Form overrides dispose to clean up the component list.
        [DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components is object)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.Windows.Forms.Label Label1;
            System.Windows.Forms.Label Label2;
            this.UsernameTextbox = new System.Windows.Forms.TextBox();
            this.SearchButton = new System.Windows.Forms.Button();
            this.SidTextbox = new System.Windows.Forms.TextBox();
            this.OkButton = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(98, 12);
            Label1.TabIndex = 0;
            Label1.Text = "Look up username";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 38);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(23, 12);
            Label2.TabIndex = 3;
            Label2.Text = "SID";
            // 
            // UsernameTextbox
            // 
            this.UsernameTextbox.Location = new System.Drawing.Point(113, 11);
            this.UsernameTextbox.Name = "UsernameTextbox";
            this.UsernameTextbox.Size = new System.Drawing.Size(153, 19);
            this.UsernameTextbox.TabIndex = 1;
            // 
            // SearchButton
            // 
            this.SearchButton.Location = new System.Drawing.Point(272, 9);
            this.SearchButton.Name = "SearchButton";
            this.SearchButton.Size = new System.Drawing.Size(57, 21);
            this.SearchButton.TabIndex = 2;
            this.SearchButton.Text = "Search";
            this.SearchButton.UseVisualStyleBackColor = true;
            this.SearchButton.Click += new System.EventHandler(this.SearchButton_Click);
            // 
            // SidTextbox
            // 
            this.SidTextbox.Location = new System.Drawing.Point(43, 35);
            this.SidTextbox.Name = "SidTextbox";
            this.SidTextbox.Size = new System.Drawing.Size(286, 19);
            this.SidTextbox.TabIndex = 3;
            // 
            // OkButton
            // 
            this.OkButton.Location = new System.Drawing.Point(254, 59);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 21);
            this.OkButton.TabIndex = 4;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += new System.EventHandler(this.OkButton_Click);
            // 
            // OpenUserGpo
            // 
            this.AcceptButton = this.OkButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(341, 91);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(this.SidTextbox);
            this.Controls.Add(Label2);
            this.Controls.Add(this.SearchButton);
            this.Controls.Add(this.UsernameTextbox);
            this.Controls.Add(Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OpenUserGpo";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select User SID";
            this.Shown += new System.EventHandler(this.OpenUserGpo_Shown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OpenUserGpo_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox UsernameTextbox;
        internal Button SearchButton;
        internal TextBox SidTextbox;
        internal Button OkButton;
    }
}