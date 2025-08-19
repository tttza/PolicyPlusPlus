using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.CategoryDetail
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class DetailCategory : Form
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
            System.Windows.Forms.Label DisplayCode;
            System.Windows.Forms.Label InfoCodeLabel;
            System.Windows.Forms.Label ParentLabel;
            this.NameTextbox = new System.Windows.Forms.TextBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.DefinedTextbox = new System.Windows.Forms.TextBox();
            this.DisplayCodeTextbox = new System.Windows.Forms.TextBox();
            this.InfoCodeTextbox = new System.Windows.Forms.TextBox();
            this.ParentTextbox = new System.Windows.Forms.TextBox();
            this.ParentButton = new System.Windows.Forms.Button();
            this.CloseButton = new System.Windows.Forms.Button();
            NameLabel = new System.Windows.Forms.Label();
            IdLabel = new System.Windows.Forms.Label();
            DefinedLabel = new System.Windows.Forms.Label();
            DisplayCode = new System.Windows.Forms.Label();
            InfoCodeLabel = new System.Windows.Forms.Label();
            ParentLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Location = new System.Drawing.Point(12, 14);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(34, 12);
            NameLabel.TabIndex = 7;
            NameLabel.Text = "Name";
            // 
            // IdLabel
            // 
            IdLabel.AutoSize = true;
            IdLabel.Location = new System.Drawing.Point(12, 38);
            IdLabel.Name = "IdLabel";
            IdLabel.Size = new System.Drawing.Size(55, 12);
            IdLabel.TabIndex = 8;
            IdLabel.Text = "Unique ID";
            // 
            // DefinedLabel
            // 
            DefinedLabel.AutoSize = true;
            DefinedLabel.Location = new System.Drawing.Point(12, 62);
            DefinedLabel.Name = "DefinedLabel";
            DefinedLabel.Size = new System.Drawing.Size(57, 12);
            DefinedLabel.TabIndex = 9;
            DefinedLabel.Text = "Defined in";
            // 
            // DisplayCode
            // 
            DisplayCode.AutoSize = true;
            DisplayCode.Location = new System.Drawing.Point(12, 86);
            DisplayCode.Name = "DisplayCode";
            DisplayCode.Size = new System.Drawing.Size(71, 12);
            DisplayCode.TabIndex = 10;
            DisplayCode.Text = "Display code";
            // 
            // InfoCodeLabel
            // 
            InfoCodeLabel.AutoSize = true;
            InfoCodeLabel.Location = new System.Drawing.Point(12, 110);
            InfoCodeLabel.Name = "InfoCodeLabel";
            InfoCodeLabel.Size = new System.Drawing.Size(52, 12);
            InfoCodeLabel.TabIndex = 11;
            InfoCodeLabel.Text = "Info code";
            // 
            // ParentLabel
            // 
            ParentLabel.AutoSize = true;
            ParentLabel.Location = new System.Drawing.Point(12, 134);
            ParentLabel.Name = "ParentLabel";
            ParentLabel.Size = new System.Drawing.Size(38, 12);
            ParentLabel.TabIndex = 12;
            ParentLabel.Text = "Parent";
            // 
            // NameTextbox
            // 
            this.NameTextbox.Location = new System.Drawing.Point(86, 11);
            this.NameTextbox.Name = "NameTextbox";
            this.NameTextbox.ReadOnly = true;
            this.NameTextbox.Size = new System.Drawing.Size(225, 19);
            this.NameTextbox.TabIndex = 0;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(86, 35);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.ReadOnly = true;
            this.IdTextbox.Size = new System.Drawing.Size(225, 19);
            this.IdTextbox.TabIndex = 1;
            // 
            // DefinedTextbox
            // 
            this.DefinedTextbox.Location = new System.Drawing.Point(86, 59);
            this.DefinedTextbox.Name = "DefinedTextbox";
            this.DefinedTextbox.ReadOnly = true;
            this.DefinedTextbox.Size = new System.Drawing.Size(225, 19);
            this.DefinedTextbox.TabIndex = 2;
            // 
            // DisplayCodeTextbox
            // 
            this.DisplayCodeTextbox.Location = new System.Drawing.Point(86, 83);
            this.DisplayCodeTextbox.Name = "DisplayCodeTextbox";
            this.DisplayCodeTextbox.ReadOnly = true;
            this.DisplayCodeTextbox.Size = new System.Drawing.Size(225, 19);
            this.DisplayCodeTextbox.TabIndex = 3;
            // 
            // InfoCodeTextbox
            // 
            this.InfoCodeTextbox.Location = new System.Drawing.Point(86, 107);
            this.InfoCodeTextbox.Name = "InfoCodeTextbox";
            this.InfoCodeTextbox.ReadOnly = true;
            this.InfoCodeTextbox.Size = new System.Drawing.Size(225, 19);
            this.InfoCodeTextbox.TabIndex = 4;
            // 
            // ParentTextbox
            // 
            this.ParentTextbox.Location = new System.Drawing.Point(86, 131);
            this.ParentTextbox.Name = "ParentTextbox";
            this.ParentTextbox.ReadOnly = true;
            this.ParentTextbox.Size = new System.Drawing.Size(144, 19);
            this.ParentTextbox.TabIndex = 5;
            // 
            // ParentButton
            // 
            this.ParentButton.Location = new System.Drawing.Point(236, 129);
            this.ParentButton.Name = "ParentButton";
            this.ParentButton.Size = new System.Drawing.Size(75, 21);
            this.ParentButton.TabIndex = 6;
            this.ParentButton.Text = "Details";
            this.ParentButton.UseVisualStyleBackColor = true;
            this.ParentButton.Click += new System.EventHandler(this.ParentButton_Click);
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseButton.Location = new System.Drawing.Point(236, 156);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 21);
            this.CloseButton.TabIndex = 13;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // DetailCategory
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(323, 188);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(ParentLabel);
            this.Controls.Add(InfoCodeLabel);
            this.Controls.Add(DisplayCode);
            this.Controls.Add(DefinedLabel);
            this.Controls.Add(IdLabel);
            this.Controls.Add(NameLabel);
            this.Controls.Add(this.ParentButton);
            this.Controls.Add(this.ParentTextbox);
            this.Controls.Add(this.InfoCodeTextbox);
            this.Controls.Add(this.DisplayCodeTextbox);
            this.Controls.Add(this.DefinedTextbox);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(this.NameTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailCategory";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Category Details";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox NameTextbox;
        internal TextBox IdTextbox;
        internal TextBox DefinedTextbox;
        internal TextBox DisplayCodeTextbox;
        internal TextBox InfoCodeTextbox;
        internal TextBox ParentTextbox;
        internal Button ParentButton;
        internal Button CloseButton;
    }
}