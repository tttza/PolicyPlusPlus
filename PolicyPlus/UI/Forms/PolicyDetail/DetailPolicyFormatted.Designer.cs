using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    public partial class DetailPolicyFormatted : Form
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
            System.Windows.Forms.Label NameLabel;
            System.Windows.Forms.Label IdLabel;
            System.Windows.Forms.Label DefinedLabel;
            System.Windows.Forms.Label FormattedPath;
            System.Windows.Forms.Label label1;
            System.Windows.Forms.Label label2;
            System.Windows.Forms.Label label3;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DetailPolicyFormatted));
            this.NameTextbox = new System.Windows.Forms.TextBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.DefinedTextbox = new System.Windows.Forms.TextBox();
            this.FormattedPolPathBox = new System.Windows.Forms.TextBox();
            this.CloseButton = new System.Windows.Forms.Button();
            this.PolPathCopyButton = new System.Windows.Forms.Button();
            this.FormattedRegPathBox = new System.Windows.Forms.TextBox();
            this.RegPathCopyButton = new System.Windows.Forms.Button();
            this.ToggleRegViewBtn = new System.Windows.Forms.Button();
            NameLabel = new System.Windows.Forms.Label();
            IdLabel = new System.Windows.Forms.Label();
            DefinedLabel = new System.Windows.Forms.Label();
            FormattedPath = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Location = new System.Drawing.Point(12, 14);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(34, 12);
            NameLabel.TabIndex = 9;
            NameLabel.Text = "Name";
            // 
            // IdLabel
            // 
            IdLabel.AutoSize = true;
            IdLabel.Location = new System.Drawing.Point(12, 38);
            IdLabel.Name = "IdLabel";
            IdLabel.Size = new System.Drawing.Size(55, 12);
            IdLabel.TabIndex = 10;
            IdLabel.Text = "Unique ID";
            // 
            // DefinedLabel
            // 
            DefinedLabel.AutoSize = true;
            DefinedLabel.Location = new System.Drawing.Point(12, 62);
            DefinedLabel.Name = "DefinedLabel";
            DefinedLabel.Size = new System.Drawing.Size(57, 12);
            DefinedLabel.TabIndex = 11;
            DefinedLabel.Text = "Defined in";
            // 
            // FormattedPath
            // 
            FormattedPath.AutoSize = true;
            FormattedPath.Location = new System.Drawing.Point(12, 93);
            FormattedPath.Name = "FormattedPath";
            FormattedPath.Size = new System.Drawing.Size(63, 12);
            FormattedPath.TabIndex = 15;
            FormattedPath.Text = "Policy Path";
            FormattedPath.Click += new System.EventHandler(this.SectionLabel_Click);
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(10, 240);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(81, 12);
            label1.TabIndex = 23;
            label1.Text = "Registry Value";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(10, 258);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(79, 12);
            label2.TabIndex = 24;
            label2.Text = "(Experimental)";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(10, 109);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(79, 12);
            label3.TabIndex = 25;
            label3.Text = "(Experimental)";
            // 
            // NameTextbox
            // 
            this.NameTextbox.Location = new System.Drawing.Point(111, 11);
            this.NameTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.NameTextbox.Name = "NameTextbox";
            this.NameTextbox.ReadOnly = true;
            this.NameTextbox.Size = new System.Drawing.Size(380, 19);
            this.NameTextbox.TabIndex = 0;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(111, 35);
            this.IdTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.ReadOnly = true;
            this.IdTextbox.Size = new System.Drawing.Size(380, 19);
            this.IdTextbox.TabIndex = 1;
            // 
            // DefinedTextbox
            // 
            this.DefinedTextbox.Location = new System.Drawing.Point(111, 59);
            this.DefinedTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.DefinedTextbox.Name = "DefinedTextbox";
            this.DefinedTextbox.ReadOnly = true;
            this.DefinedTextbox.Size = new System.Drawing.Size(380, 19);
            this.DefinedTextbox.TabIndex = 2;
            // 
            // FormattedPolPathBox
            // 
            this.FormattedPolPathBox.Location = new System.Drawing.Point(111, 90);
            this.FormattedPolPathBox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.FormattedPolPathBox.Multiline = true;
            this.FormattedPolPathBox.Name = "FormattedPolPathBox";
            this.FormattedPolPathBox.ReadOnly = true;
            this.FormattedPolPathBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.FormattedPolPathBox.Size = new System.Drawing.Size(380, 134);
            this.FormattedPolPathBox.TabIndex = 6;
            this.FormattedPolPathBox.Tag = "Path";
            this.FormattedPolPathBox.TextChanged += new System.EventHandler(this.SectionTextbox_TextChanged);
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseButton.Location = new System.Drawing.Point(415, 378);
            this.CloseButton.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 22);
            this.CloseButton.TabIndex = 19;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // PolPathCopyButton
            // 
            this.PolPathCopyButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("PolPathCopyButton.BackgroundImage")));
            this.PolPathCopyButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.PolPathCopyButton.FlatAppearance.BorderSize = 0;
            this.PolPathCopyButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.PolPathCopyButton.Location = new System.Drawing.Point(495, 90);
            this.PolPathCopyButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.PolPathCopyButton.Name = "PolPathCopyButton";
            this.PolPathCopyButton.Size = new System.Drawing.Size(27, 30);
            this.PolPathCopyButton.TabIndex = 20;
            this.PolPathCopyButton.Tag = "PolPath";
            this.PolPathCopyButton.UseVisualStyleBackColor = true;
            this.PolPathCopyButton.Click += new System.EventHandler(this.CopyToClipboard);
            // 
            // FormattedRegPathBox
            // 
            this.FormattedRegPathBox.Location = new System.Drawing.Point(111, 240);
            this.FormattedRegPathBox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.FormattedRegPathBox.Multiline = true;
            this.FormattedRegPathBox.Name = "FormattedRegPathBox";
            this.FormattedRegPathBox.ReadOnly = true;
            this.FormattedRegPathBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.FormattedRegPathBox.Size = new System.Drawing.Size(380, 134);
            this.FormattedRegPathBox.TabIndex = 21;
            this.FormattedRegPathBox.Tag = "Path";
            // 
            // RegPathCopyButton
            // 
            this.RegPathCopyButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("RegPathCopyButton.BackgroundImage")));
            this.RegPathCopyButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.RegPathCopyButton.FlatAppearance.BorderSize = 0;
            this.RegPathCopyButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.RegPathCopyButton.Location = new System.Drawing.Point(495, 240);
            this.RegPathCopyButton.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.RegPathCopyButton.Name = "RegPathCopyButton";
            this.RegPathCopyButton.Size = new System.Drawing.Size(27, 30);
            this.RegPathCopyButton.TabIndex = 22;
            this.RegPathCopyButton.Tag = "RegPath";
            this.RegPathCopyButton.UseVisualStyleBackColor = true;
            this.RegPathCopyButton.Click += new System.EventHandler(this.CopyToClipboard);
            // 
            // ToggleRegViewBtn
            // 
            this.ToggleRegViewBtn.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("ToggleRegViewBtn.BackgroundImage")));
            this.ToggleRegViewBtn.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.ToggleRegViewBtn.FlatAppearance.BorderSize = 0;
            this.ToggleRegViewBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.ToggleRegViewBtn.Location = new System.Drawing.Point(495, 275);
            this.ToggleRegViewBtn.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.ToggleRegViewBtn.Name = "ToggleRegViewBtn";
            this.ToggleRegViewBtn.Size = new System.Drawing.Size(27, 24);
            this.ToggleRegViewBtn.TabIndex = 26;
            this.ToggleRegViewBtn.UseVisualStyleBackColor = true;
            this.ToggleRegViewBtn.Click += new System.EventHandler(this.ToggleRegViewBtn_Click);
            // 
            // DetailPolicyFormatted
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(528, 409);
            this.Controls.Add(this.ToggleRegViewBtn);
            this.Controls.Add(label3);
            this.Controls.Add(label2);
            this.Controls.Add(label1);
            this.Controls.Add(this.RegPathCopyButton);
            this.Controls.Add(this.FormattedRegPathBox);
            this.Controls.Add(this.PolPathCopyButton);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(FormattedPath);
            this.Controls.Add(DefinedLabel);
            this.Controls.Add(IdLabel);
            this.Controls.Add(NameLabel);
            this.Controls.Add(this.FormattedPolPathBox);
            this.Controls.Add(this.DefinedTextbox);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(this.NameTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailPolicyFormatted";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Policy Details - Formatted";
            this.Load += new System.EventHandler(this.DetailPolicyFormatted_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox NameTextbox;
        internal TextBox IdTextbox;
        internal TextBox DefinedTextbox;
        internal TextBox FormattedPolPathBox;
        internal Button CloseButton;
        private Button PolPathCopyButton;
        internal TextBox FormattedRegPathBox;
        private Button RegPathCopyButton;
        private Button ToggleRegViewBtn;
    }
}