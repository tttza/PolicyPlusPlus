using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DetailPolicyFormatted));
            this.NameTextbox = new System.Windows.Forms.TextBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.DefinedTextbox = new System.Windows.Forms.TextBox();
            this.FormattedPathBox = new System.Windows.Forms.TextBox();
            this.CloseButton = new System.Windows.Forms.Button();
            this.PathCopyButton = new System.Windows.Forms.Button();
            NameLabel = new System.Windows.Forms.Label();
            IdLabel = new System.Windows.Forms.Label();
            DefinedLabel = new System.Windows.Forms.Label();
            FormattedPath = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Location = new System.Drawing.Point(16, 17);
            NameLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(43, 15);
            NameLabel.TabIndex = 9;
            NameLabel.Text = "Name";
            // 
            // IdLabel
            // 
            IdLabel.AutoSize = true;
            IdLabel.Location = new System.Drawing.Point(16, 47);
            IdLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            IdLabel.Name = "IdLabel";
            IdLabel.Size = new System.Drawing.Size(70, 15);
            IdLabel.TabIndex = 10;
            IdLabel.Text = "Unique ID";
            // 
            // DefinedLabel
            // 
            DefinedLabel.AutoSize = true;
            DefinedLabel.Location = new System.Drawing.Point(16, 77);
            DefinedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            DefinedLabel.Name = "DefinedLabel";
            DefinedLabel.Size = new System.Drawing.Size(72, 15);
            DefinedLabel.TabIndex = 11;
            DefinedLabel.Text = "Defined in";
            // 
            // FormattedPath
            // 
            FormattedPath.AutoSize = true;
            FormattedPath.Location = new System.Drawing.Point(16, 116);
            FormattedPath.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            FormattedPath.Name = "FormattedPath";
            FormattedPath.Size = new System.Drawing.Size(36, 15);
            FormattedPath.TabIndex = 15;
            FormattedPath.Text = "Path";
            FormattedPath.Click += new System.EventHandler(this.SectionLabel_Click);
            // 
            // NameTextbox
            // 
            this.NameTextbox.Location = new System.Drawing.Point(148, 14);
            this.NameTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.NameTextbox.Name = "NameTextbox";
            this.NameTextbox.ReadOnly = true;
            this.NameTextbox.Size = new System.Drawing.Size(505, 22);
            this.NameTextbox.TabIndex = 0;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(148, 44);
            this.IdTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.ReadOnly = true;
            this.IdTextbox.Size = new System.Drawing.Size(505, 22);
            this.IdTextbox.TabIndex = 1;
            // 
            // DefinedTextbox
            // 
            this.DefinedTextbox.Location = new System.Drawing.Point(148, 74);
            this.DefinedTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.DefinedTextbox.Name = "DefinedTextbox";
            this.DefinedTextbox.ReadOnly = true;
            this.DefinedTextbox.Size = new System.Drawing.Size(505, 22);
            this.DefinedTextbox.TabIndex = 2;
            // 
            // FormattedPathBox
            // 
            this.FormattedPathBox.Location = new System.Drawing.Point(148, 113);
            this.FormattedPathBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.FormattedPathBox.Multiline = true;
            this.FormattedPathBox.Name = "FormattedPathBox";
            this.FormattedPathBox.ReadOnly = true;
            this.FormattedPathBox.Size = new System.Drawing.Size(505, 166);
            this.FormattedPathBox.TabIndex = 6;
            this.FormattedPathBox.Tag = "Path";
            this.FormattedPathBox.TextChanged += new System.EventHandler(this.SectionTextbox_TextChanged);
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseButton.Location = new System.Drawing.Point(553, 286);
            this.CloseButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(100, 27);
            this.CloseButton.TabIndex = 19;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // PathCopyButton
            // 
            this.PathCopyButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("PathCopyButton.BackgroundImage")));
            this.PathCopyButton.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.PathCopyButton.FlatAppearance.BorderSize = 0;
            this.PathCopyButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.PathCopyButton.Location = new System.Drawing.Point(660, 113);
            this.PathCopyButton.Name = "PathCopyButton";
            this.PathCopyButton.Size = new System.Drawing.Size(36, 38);
            this.PathCopyButton.TabIndex = 20;
            this.PathCopyButton.Tag = "Path";
            this.PathCopyButton.UseVisualStyleBackColor = true;
            this.PathCopyButton.Click += new System.EventHandler(this.CopyToClipboard);
            // 
            // DetailPolicyFormatted
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(704, 325);
            this.Controls.Add(this.PathCopyButton);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(FormattedPath);
            this.Controls.Add(DefinedLabel);
            this.Controls.Add(IdLabel);
            this.Controls.Add(NameLabel);
            this.Controls.Add(this.FormattedPathBox);
            this.Controls.Add(this.DefinedTextbox);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(this.NameTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailPolicyFormatted";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Policy Details - Formatted";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox NameTextbox;
        internal TextBox IdTextbox;
        internal TextBox DefinedTextbox;
        internal TextBox FormattedPathBox;
        internal Button CloseButton;
        private Button PathCopyButton;
    }
}