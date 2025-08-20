using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class LanguageOptions : Form
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
    private System.ComponentModel.IContainer components = null;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.Windows.Forms.Label Label1;
            System.Windows.Forms.Label Label2;
            this.ButtonOK = new System.Windows.Forms.Button();
            this.TextAdmlLanguage = new System.Windows.Forms.TextBox();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 8);
            Label1.MaximumSize = new System.Drawing.Size(276, 0);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(274, 48);
            Label1.TabIndex = 1;
            Label1.Text = "Each ADMX policy definitions file may have multiple corresponding ADML language-s" +
    "pecific files. This setting controls which language\'s ADML file Policy Plus will" +
    " look for first.";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 62);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(164, 12);
            Label2.TabIndex = 3;
            Label2.Text = "Preferred ADML language code";
            // 
            // ButtonOK
            // 
            this.ButtonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonOK.Location = new System.Drawing.Point(213, 83);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 21);
            this.ButtonOK.TabIndex = 0;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            this.ButtonOK.Click += new System.EventHandler(this.ButtonOK_Click);
            // 
            // TextAdmlLanguage
            // 
            this.TextAdmlLanguage.Location = new System.Drawing.Point(175, 59);
            this.TextAdmlLanguage.Name = "TextAdmlLanguage";
            this.TextAdmlLanguage.Size = new System.Drawing.Size(113, 19);
            this.TextAdmlLanguage.TabIndex = 2;
            // 
            // LanguageOptions
            // 
            this.AcceptButton = this.ButtonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(300, 115);
            this.Controls.Add(Label2);
            this.Controls.Add(this.TextAdmlLanguage);
            this.Controls.Add(Label1);
            this.Controls.Add(this.ButtonOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LanguageOptions";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Language Options";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal Button ButtonOK;
        internal TextBox TextAdmlLanguage;
    }
}