using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class InspectSpolFragment : Form
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
            this.TextPolicyName = new System.Windows.Forms.TextBox();
            this.LabelPolicy = new System.Windows.Forms.Label();
            this.TextSpol = new System.Windows.Forms.TextBox();
            this.ButtonClose = new System.Windows.Forms.Button();
            this.ButtonCopy = new System.Windows.Forms.Button();
            this.CheckHeader = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // TextPolicyName
            // 
            this.TextPolicyName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TextPolicyName.Location = new System.Drawing.Point(53, 11);
            this.TextPolicyName.Name = "TextPolicyName";
            this.TextPolicyName.ReadOnly = true;
            this.TextPolicyName.Size = new System.Drawing.Size(266, 19);
            this.TextPolicyName.TabIndex = 0;
            // 
            // LabelPolicy
            // 
            this.LabelPolicy.AutoSize = true;
            this.LabelPolicy.Location = new System.Drawing.Point(12, 14);
            this.LabelPolicy.Name = "LabelPolicy";
            this.LabelPolicy.Size = new System.Drawing.Size(36, 12);
            this.LabelPolicy.TabIndex = 1;
            this.LabelPolicy.Text = "Policy";
            // 
            // TextSpol
            // 
            this.TextSpol.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TextSpol.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TextSpol.Location = new System.Drawing.Point(12, 35);
            this.TextSpol.Multiline = true;
            this.TextSpol.Name = "TextSpol";
            this.TextSpol.ReadOnly = true;
            this.TextSpol.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.TextSpol.Size = new System.Drawing.Size(307, 159);
            this.TextSpol.TabIndex = 1;
            this.TextSpol.WordWrap = false;
            this.TextSpol.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TextSpol_KeyDown);
            // 
            // ButtonClose
            // 
            this.ButtonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonClose.Location = new System.Drawing.Point(244, 199);
            this.ButtonClose.Name = "ButtonClose";
            this.ButtonClose.Size = new System.Drawing.Size(75, 21);
            this.ButtonClose.TabIndex = 4;
            this.ButtonClose.Text = "Close";
            this.ButtonClose.UseVisualStyleBackColor = true;
            // 
            // ButtonCopy
            // 
            this.ButtonCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonCopy.Location = new System.Drawing.Point(163, 199);
            this.ButtonCopy.Name = "ButtonCopy";
            this.ButtonCopy.Size = new System.Drawing.Size(75, 21);
            this.ButtonCopy.TabIndex = 3;
            this.ButtonCopy.Text = "Copy";
            this.ButtonCopy.UseVisualStyleBackColor = true;
            this.ButtonCopy.Click += new System.EventHandler(this.ButtonCopy_Click);
            // 
            // CheckHeader
            // 
            this.CheckHeader.AutoSize = true;
            this.CheckHeader.Location = new System.Drawing.Point(12, 203);
            this.CheckHeader.Name = "CheckHeader";
            this.CheckHeader.Size = new System.Drawing.Size(130, 16);
            this.CheckHeader.TabIndex = 2;
            this.CheckHeader.Text = "Include SPOL header";
            this.CheckHeader.UseVisualStyleBackColor = true;
            this.CheckHeader.CheckedChanged += new System.EventHandler(this.CheckHeader_CheckedChanged);
            // 
            // InspectSpolFragment
            // 
            this.AcceptButton = this.ButtonClose;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.ButtonClose;
            this.ClientSize = new System.Drawing.Size(331, 232);
            this.Controls.Add(this.CheckHeader);
            this.Controls.Add(this.ButtonCopy);
            this.Controls.Add(this.ButtonClose);
            this.Controls.Add(this.TextSpol);
            this.Controls.Add(this.LabelPolicy);
            this.Controls.Add(this.TextPolicyName);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(347, 271);
            this.Name = "InspectSpolFragment";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Semantic Policy Fragment";
            this.Shown += new System.EventHandler(this.InspectSpolFragment_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextPolicyName;
        internal Label LabelPolicy;
        internal TextBox TextSpol;
        internal Button ButtonClose;
        internal Button ButtonCopy;
        internal CheckBox CheckHeader;
    }
}