using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class InspectPolicyElements : Form
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
            System.Windows.Forms.Label PolicyNameLabel;
            this.PolicyNameTextbox = new System.Windows.Forms.TextBox();
            this.PolicyDetailsButton = new System.Windows.Forms.Button();
            this.InfoTreeview = new System.Windows.Forms.TreeView();
            this.CloseButton = new System.Windows.Forms.Button();
            PolicyNameLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // PolicyNameLabel
            // 
            PolicyNameLabel.AutoSize = true;
            PolicyNameLabel.Location = new System.Drawing.Point(12, 14);
            PolicyNameLabel.Name = "PolicyNameLabel";
            PolicyNameLabel.Size = new System.Drawing.Size(36, 12);
            PolicyNameLabel.TabIndex = 2;
            PolicyNameLabel.Text = "Policy";
            // 
            // PolicyNameTextbox
            // 
            this.PolicyNameTextbox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PolicyNameTextbox.Location = new System.Drawing.Point(53, 11);
            this.PolicyNameTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.PolicyNameTextbox.Name = "PolicyNameTextbox";
            this.PolicyNameTextbox.ReadOnly = true;
            this.PolicyNameTextbox.Size = new System.Drawing.Size(450, 19);
            this.PolicyNameTextbox.TabIndex = 0;
            // 
            // PolicyDetailsButton
            // 
            this.PolicyDetailsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.PolicyDetailsButton.Location = new System.Drawing.Point(508, 10);
            this.PolicyDetailsButton.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.PolicyDetailsButton.Name = "PolicyDetailsButton";
            this.PolicyDetailsButton.Size = new System.Drawing.Size(75, 22);
            this.PolicyDetailsButton.TabIndex = 1;
            this.PolicyDetailsButton.Text = "Details";
            this.PolicyDetailsButton.UseVisualStyleBackColor = true;
            this.PolicyDetailsButton.Click += new System.EventHandler(this.PolicyDetailsButton_Click);
            // 
            // InfoTreeview
            // 
            this.InfoTreeview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.InfoTreeview.HideSelection = false;
            this.InfoTreeview.Location = new System.Drawing.Point(15, 35);
            this.InfoTreeview.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.InfoTreeview.Name = "InfoTreeview";
            this.InfoTreeview.ShowNodeToolTips = true;
            this.InfoTreeview.Size = new System.Drawing.Size(569, 178);
            this.InfoTreeview.TabIndex = 3;
            this.InfoTreeview.KeyDown += new System.Windows.Forms.KeyEventHandler(this.InfoTreeview_KeyDown);
            // 
            // CloseButton
            // 
            this.CloseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CloseButton.Location = new System.Drawing.Point(508, 218);
            this.CloseButton.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 22);
            this.CloseButton.TabIndex = 4;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // InspectPolicyElements
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(596, 251);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.InfoTreeview);
            this.Controls.Add(PolicyNameLabel);
            this.Controls.Add(this.PolicyDetailsButton);
            this.Controls.Add(this.PolicyNameTextbox);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(305, 204);
            this.Name = "InspectPolicyElements";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Element Inspector";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox PolicyNameTextbox;
        internal Button PolicyDetailsButton;
        internal TreeView InfoTreeview;
        internal Button CloseButton;
    }
}