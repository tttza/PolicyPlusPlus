using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.Export
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class ExportReg : Form
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
            System.Windows.Forms.Label Label3;
            System.Windows.Forms.Label Label4;
            this.TextReg = new System.Windows.Forms.TextBox();
            this.TextBranch = new System.Windows.Forms.TextBox();
            this.TextRoot = new System.Windows.Forms.TextBox();
            this.ButtonBrowse = new System.Windows.Forms.Button();
            this.ButtonExport = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            Label3 = new System.Windows.Forms.Label();
            Label4 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(78, 12);
            Label1.TabIndex = 0;
            Label1.Text = "Source branch";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 62);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(81, 12);
            Label2.TabIndex = 3;
            Label2.Text = "Registry prefix";
            // 
            // Label3
            // 
            Label3.AutoSize = true;
            Label3.Location = new System.Drawing.Point(12, 38);
            Label3.Name = "Label3";
            Label3.Size = new System.Drawing.Size(48, 12);
            Label3.TabIndex = 4;
            Label3.Text = "REG file";
            // 
            // Label4
            // 
            Label4.AutoSize = true;
            Label4.Location = new System.Drawing.Point(275, 14);
            Label4.Name = "Label4";
            Label4.Size = new System.Drawing.Size(106, 12);
            Label4.TabIndex = 8;
            Label4.Text = "(blank to export all)";
            // 
            // TextReg
            // 
            this.TextReg.Location = new System.Drawing.Point(95, 35);
            this.TextReg.Name = "TextReg";
            this.TextReg.Size = new System.Drawing.Size(195, 19);
            this.TextReg.TabIndex = 2;
            // 
            // TextBranch
            // 
            this.TextBranch.Location = new System.Drawing.Point(95, 11);
            this.TextBranch.Name = "TextBranch";
            this.TextBranch.Size = new System.Drawing.Size(174, 19);
            this.TextBranch.TabIndex = 1;
            // 
            // TextRoot
            // 
            this.TextRoot.Location = new System.Drawing.Point(95, 59);
            this.TextRoot.Name = "TextRoot";
            this.TextRoot.Size = new System.Drawing.Size(276, 19);
            this.TextRoot.TabIndex = 4;
            // 
            // ButtonBrowse
            // 
            this.ButtonBrowse.Location = new System.Drawing.Point(296, 33);
            this.ButtonBrowse.Name = "ButtonBrowse";
            this.ButtonBrowse.Size = new System.Drawing.Size(75, 21);
            this.ButtonBrowse.TabIndex = 3;
            this.ButtonBrowse.Text = "Browse";
            this.ButtonBrowse.UseVisualStyleBackColor = true;
            this.ButtonBrowse.Click += new System.EventHandler(this.ButtonBrowse_Click);
            // 
            // ButtonExport
            // 
            this.ButtonExport.Location = new System.Drawing.Point(296, 83);
            this.ButtonExport.Name = "ButtonExport";
            this.ButtonExport.Size = new System.Drawing.Size(75, 21);
            this.ButtonExport.TabIndex = 5;
            this.ButtonExport.Text = "Export";
            this.ButtonExport.UseVisualStyleBackColor = true;
            this.ButtonExport.Click += new System.EventHandler(this.ButtonExport_Click);
            // 
            // ExportReg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(383, 115);
            this.Controls.Add(Label4);
            this.Controls.Add(this.ButtonExport);
            this.Controls.Add(this.ButtonBrowse);
            this.Controls.Add(this.TextRoot);
            this.Controls.Add(Label3);
            this.Controls.Add(Label2);
            this.Controls.Add(this.TextBranch);
            this.Controls.Add(this.TextReg);
            this.Controls.Add(Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExportReg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export REG";
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.ExportReg_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextReg;
        internal TextBox TextBranch;
        internal TextBox TextRoot;
        internal Button ButtonBrowse;
        internal Button ButtonExport;
    }
}