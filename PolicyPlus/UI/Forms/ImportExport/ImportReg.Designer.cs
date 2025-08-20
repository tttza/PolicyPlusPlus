using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class ImportReg : Form
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
            this.TextReg = new System.Windows.Forms.TextBox();
            this.TextRoot = new System.Windows.Forms.TextBox();
            this.ButtonBrowse = new System.Windows.Forms.Button();
            this.ButtonImport = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(48, 12);
            Label1.TabIndex = 0;
            Label1.Text = "REG file";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 38);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(35, 12);
            Label2.TabIndex = 3;
            Label2.Text = "Prefix";
            // 
            // TextReg
            // 
            this.TextReg.Location = new System.Drawing.Point(64, 11);
            this.TextReg.Name = "TextReg";
            this.TextReg.Size = new System.Drawing.Size(195, 19);
            this.TextReg.TabIndex = 1;
            // 
            // TextRoot
            // 
            this.TextRoot.Location = new System.Drawing.Point(64, 35);
            this.TextRoot.Name = "TextRoot";
            this.TextRoot.Size = new System.Drawing.Size(276, 19);
            this.TextRoot.TabIndex = 3;
            // 
            // ButtonBrowse
            // 
            this.ButtonBrowse.Location = new System.Drawing.Point(265, 9);
            this.ButtonBrowse.Name = "ButtonBrowse";
            this.ButtonBrowse.Size = new System.Drawing.Size(75, 21);
            this.ButtonBrowse.TabIndex = 2;
            this.ButtonBrowse.Text = "Browse";
            this.ButtonBrowse.UseVisualStyleBackColor = true;
            this.ButtonBrowse.Click += new System.EventHandler(this.ButtonBrowse_Click);
            // 
            // ButtonImport
            // 
            this.ButtonImport.Location = new System.Drawing.Point(265, 59);
            this.ButtonImport.Name = "ButtonImport";
            this.ButtonImport.Size = new System.Drawing.Size(75, 21);
            this.ButtonImport.TabIndex = 4;
            this.ButtonImport.Text = "Import";
            this.ButtonImport.UseVisualStyleBackColor = true;
            this.ButtonImport.Click += new System.EventHandler(this.ButtonImport_Click);
            // 
            // ImportReg
            // 
            this.AcceptButton = this.ButtonImport;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(352, 91);
            this.Controls.Add(this.ButtonImport);
            this.Controls.Add(this.ButtonBrowse);
            this.Controls.Add(Label2);
            this.Controls.Add(this.TextRoot);
            this.Controls.Add(this.TextReg);
            this.Controls.Add(Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ImportReg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import REG";
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.ImportReg_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextReg;
        internal TextBox TextRoot;
        internal Button ButtonBrowse;
        internal Button ButtonImport;
    }
}