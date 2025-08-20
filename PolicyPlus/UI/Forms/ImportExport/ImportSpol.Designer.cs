using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class ImportSpol : Form
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
            this.ButtonOpenFile = new System.Windows.Forms.Button();
            this.Label1 = new System.Windows.Forms.Label();
            this.ButtonApply = new System.Windows.Forms.Button();
            this.TextSpol = new System.Windows.Forms.TextBox();
            this.ButtonVerify = new System.Windows.Forms.Button();
            this.ButtonReset = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ButtonOpenFile
            // 
            this.ButtonOpenFile.Location = new System.Drawing.Point(157, 11);
            this.ButtonOpenFile.Name = "ButtonOpenFile";
            this.ButtonOpenFile.Size = new System.Drawing.Size(75, 21);
            this.ButtonOpenFile.TabIndex = 0;
            this.ButtonOpenFile.Text = "Open File";
            this.ButtonOpenFile.UseVisualStyleBackColor = true;
            this.ButtonOpenFile.Click += new System.EventHandler(this.ButtonOpenFile_Click);
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 16);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(151, 12);
            this.Label1.TabIndex = 1;
            this.Label1.Text = "Semantic Policy (SPOL) text";
            // 
            // ButtonApply
            // 
            this.ButtonApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonApply.Location = new System.Drawing.Point(303, 202);
            this.ButtonApply.Name = "ButtonApply";
            this.ButtonApply.Size = new System.Drawing.Size(75, 21);
            this.ButtonApply.TabIndex = 4;
            this.ButtonApply.Text = "Apply";
            this.ButtonApply.UseVisualStyleBackColor = true;
            this.ButtonApply.Click += new System.EventHandler(this.ButtonApply_Click);
            // 
            // TextSpol
            // 
            this.TextSpol.AcceptsReturn = true;
            this.TextSpol.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TextSpol.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TextSpol.Location = new System.Drawing.Point(12, 38);
            this.TextSpol.Multiline = true;
            this.TextSpol.Name = "TextSpol";
            this.TextSpol.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.TextSpol.Size = new System.Drawing.Size(366, 159);
            this.TextSpol.TabIndex = 2;
            this.TextSpol.Text = "Policy Plus Semantic Policy\r\n\r\n";
            this.TextSpol.WordWrap = false;
            this.TextSpol.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextSpol_KeyDown);
            // 
            // ButtonVerify
            // 
            this.ButtonVerify.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonVerify.Location = new System.Drawing.Point(222, 202);
            this.ButtonVerify.Name = "ButtonVerify";
            this.ButtonVerify.Size = new System.Drawing.Size(75, 21);
            this.ButtonVerify.TabIndex = 3;
            this.ButtonVerify.Text = "Verify";
            this.ButtonVerify.UseVisualStyleBackColor = true;
            this.ButtonVerify.Click += new System.EventHandler(this.ButtonVerify_Click);
            // 
            // ButtonReset
            // 
            this.ButtonReset.Location = new System.Drawing.Point(238, 11);
            this.ButtonReset.Name = "ButtonReset";
            this.ButtonReset.Size = new System.Drawing.Size(75, 21);
            this.ButtonReset.TabIndex = 1;
            this.ButtonReset.Text = "Reset";
            this.ButtonReset.UseVisualStyleBackColor = true;
            this.ButtonReset.Click += new System.EventHandler(this.ButtonReset_Click);
            // 
            // ImportSpol
            // 
            this.AcceptButton = this.ButtonApply;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(390, 234);
            this.Controls.Add(this.ButtonReset);
            this.Controls.Add(this.ButtonVerify);
            this.Controls.Add(this.TextSpol);
            this.Controls.Add(this.ButtonApply);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.ButtonOpenFile);
            this.KeyPreview = true;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(373, 249);
            this.Name = "ImportSpol";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import Semantic Policy";
            this.Shown += new System.EventHandler(this.ImportSpol_Shown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.ImportSpol_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal Button ButtonOpenFile;
        internal Label Label1;
        internal Button ButtonApply;
        internal TextBox TextSpol;
        internal Button ButtonVerify;
        internal Button ButtonReset;
    }
}