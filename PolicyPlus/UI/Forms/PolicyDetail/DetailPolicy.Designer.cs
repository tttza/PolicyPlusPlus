using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class DetailPolicy : Form
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
            System.Windows.Forms.Label DisplayLabel;
            System.Windows.Forms.Label InfoLabel;
            System.Windows.Forms.Label PresentLabel;
            System.Windows.Forms.Label SectionLabel;
            System.Windows.Forms.Label SupportLabel;
            System.Windows.Forms.Label CategoryLabel;
            this.NameTextbox = new System.Windows.Forms.TextBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.DefinedTextbox = new System.Windows.Forms.TextBox();
            this.DisplayCodeTextbox = new System.Windows.Forms.TextBox();
            this.InfoCodeTextbox = new System.Windows.Forms.TextBox();
            this.PresentCodeTextbox = new System.Windows.Forms.TextBox();
            this.SectionTextbox = new System.Windows.Forms.TextBox();
            this.SupportTextbox = new System.Windows.Forms.TextBox();
            this.CategoryTextbox = new System.Windows.Forms.TextBox();
            this.CategoryButton = new System.Windows.Forms.Button();
            this.SupportButton = new System.Windows.Forms.Button();
            this.CloseButton = new System.Windows.Forms.Button();
            NameLabel = new System.Windows.Forms.Label();
            IdLabel = new System.Windows.Forms.Label();
            DefinedLabel = new System.Windows.Forms.Label();
            DisplayLabel = new System.Windows.Forms.Label();
            InfoLabel = new System.Windows.Forms.Label();
            PresentLabel = new System.Windows.Forms.Label();
            SectionLabel = new System.Windows.Forms.Label();
            SupportLabel = new System.Windows.Forms.Label();
            CategoryLabel = new System.Windows.Forms.Label();
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
            // DisplayLabel
            // 
            DisplayLabel.AutoSize = true;
            DisplayLabel.Location = new System.Drawing.Point(12, 86);
            DisplayLabel.Name = "DisplayLabel";
            DisplayLabel.Size = new System.Drawing.Size(71, 12);
            DisplayLabel.TabIndex = 12;
            DisplayLabel.Text = "Display code";
            // 
            // InfoLabel
            // 
            InfoLabel.AutoSize = true;
            InfoLabel.Location = new System.Drawing.Point(12, 110);
            InfoLabel.Name = "InfoLabel";
            InfoLabel.Size = new System.Drawing.Size(52, 12);
            InfoLabel.TabIndex = 13;
            InfoLabel.Text = "Info code";
            // 
            // PresentLabel
            // 
            PresentLabel.AutoSize = true;
            PresentLabel.Location = new System.Drawing.Point(12, 134);
            PresentLabel.Name = "PresentLabel";
            PresentLabel.Size = new System.Drawing.Size(97, 12);
            PresentLabel.TabIndex = 14;
            PresentLabel.Text = "Presentation code";
            // 
            // SectionLabel
            // 
            SectionLabel.AutoSize = true;
            SectionLabel.Location = new System.Drawing.Point(12, 158);
            SectionLabel.Name = "SectionLabel";
            SectionLabel.Size = new System.Drawing.Size(43, 12);
            SectionLabel.TabIndex = 15;
            SectionLabel.Text = "Section";
            // 
            // SupportLabel
            // 
            SupportLabel.AutoSize = true;
            SupportLabel.Location = new System.Drawing.Point(12, 182);
            SupportLabel.Name = "SupportLabel";
            SupportLabel.Size = new System.Drawing.Size(72, 12);
            SupportLabel.TabIndex = 16;
            SupportLabel.Text = "Supported on";
            // 
            // CategoryLabel
            // 
            CategoryLabel.AutoSize = true;
            CategoryLabel.Location = new System.Drawing.Point(12, 206);
            CategoryLabel.Name = "CategoryLabel";
            CategoryLabel.Size = new System.Drawing.Size(51, 12);
            CategoryLabel.TabIndex = 17;
            CategoryLabel.Text = "Category";
            // 
            // NameTextbox
            // 
            this.NameTextbox.Location = new System.Drawing.Point(111, 11);
            this.NameTextbox.Name = "NameTextbox";
            this.NameTextbox.ReadOnly = true;
            this.NameTextbox.Size = new System.Drawing.Size(258, 19);
            this.NameTextbox.TabIndex = 0;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(111, 35);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.ReadOnly = true;
            this.IdTextbox.Size = new System.Drawing.Size(258, 19);
            this.IdTextbox.TabIndex = 1;
            // 
            // DefinedTextbox
            // 
            this.DefinedTextbox.Location = new System.Drawing.Point(111, 59);
            this.DefinedTextbox.Name = "DefinedTextbox";
            this.DefinedTextbox.ReadOnly = true;
            this.DefinedTextbox.Size = new System.Drawing.Size(258, 19);
            this.DefinedTextbox.TabIndex = 2;
            // 
            // DisplayCodeTextbox
            // 
            this.DisplayCodeTextbox.Location = new System.Drawing.Point(111, 83);
            this.DisplayCodeTextbox.Name = "DisplayCodeTextbox";
            this.DisplayCodeTextbox.ReadOnly = true;
            this.DisplayCodeTextbox.Size = new System.Drawing.Size(258, 19);
            this.DisplayCodeTextbox.TabIndex = 3;
            // 
            // InfoCodeTextbox
            // 
            this.InfoCodeTextbox.Location = new System.Drawing.Point(111, 107);
            this.InfoCodeTextbox.Name = "InfoCodeTextbox";
            this.InfoCodeTextbox.ReadOnly = true;
            this.InfoCodeTextbox.Size = new System.Drawing.Size(258, 19);
            this.InfoCodeTextbox.TabIndex = 4;
            // 
            // PresentCodeTextbox
            // 
            this.PresentCodeTextbox.Location = new System.Drawing.Point(111, 131);
            this.PresentCodeTextbox.Name = "PresentCodeTextbox";
            this.PresentCodeTextbox.ReadOnly = true;
            this.PresentCodeTextbox.Size = new System.Drawing.Size(258, 19);
            this.PresentCodeTextbox.TabIndex = 5;
            // 
            // SectionTextbox
            // 
            this.SectionTextbox.Location = new System.Drawing.Point(111, 155);
            this.SectionTextbox.Name = "SectionTextbox";
            this.SectionTextbox.ReadOnly = true;
            this.SectionTextbox.Size = new System.Drawing.Size(258, 19);
            this.SectionTextbox.TabIndex = 6;
            // 
            // SupportTextbox
            // 
            this.SupportTextbox.Location = new System.Drawing.Point(111, 179);
            this.SupportTextbox.Name = "SupportTextbox";
            this.SupportTextbox.ReadOnly = true;
            this.SupportTextbox.Size = new System.Drawing.Size(177, 19);
            this.SupportTextbox.TabIndex = 7;
            // 
            // CategoryTextbox
            // 
            this.CategoryTextbox.Location = new System.Drawing.Point(111, 203);
            this.CategoryTextbox.Name = "CategoryTextbox";
            this.CategoryTextbox.ReadOnly = true;
            this.CategoryTextbox.Size = new System.Drawing.Size(177, 19);
            this.CategoryTextbox.TabIndex = 8;
            // 
            // CategoryButton
            // 
            this.CategoryButton.Location = new System.Drawing.Point(294, 201);
            this.CategoryButton.Name = "CategoryButton";
            this.CategoryButton.Size = new System.Drawing.Size(75, 21);
            this.CategoryButton.TabIndex = 18;
            this.CategoryButton.Text = "Details";
            this.CategoryButton.UseVisualStyleBackColor = true;
            this.CategoryButton.Click += new System.EventHandler(this.CategoryButton_Click);
            // 
            // SupportButton
            // 
            this.SupportButton.Location = new System.Drawing.Point(294, 177);
            this.SupportButton.Name = "SupportButton";
            this.SupportButton.Size = new System.Drawing.Size(75, 21);
            this.SupportButton.TabIndex = 17;
            this.SupportButton.Text = "Details";
            this.SupportButton.UseVisualStyleBackColor = true;
            this.SupportButton.Click += new System.EventHandler(this.SupportButton_Click);
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseButton.Location = new System.Drawing.Point(294, 228);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 21);
            this.CloseButton.TabIndex = 19;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // DetailPolicy
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(381, 260);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.SupportButton);
            this.Controls.Add(this.CategoryButton);
            this.Controls.Add(CategoryLabel);
            this.Controls.Add(SupportLabel);
            this.Controls.Add(SectionLabel);
            this.Controls.Add(PresentLabel);
            this.Controls.Add(InfoLabel);
            this.Controls.Add(DisplayLabel);
            this.Controls.Add(DefinedLabel);
            this.Controls.Add(IdLabel);
            this.Controls.Add(NameLabel);
            this.Controls.Add(this.CategoryTextbox);
            this.Controls.Add(this.SupportTextbox);
            this.Controls.Add(this.SectionTextbox);
            this.Controls.Add(this.PresentCodeTextbox);
            this.Controls.Add(this.InfoCodeTextbox);
            this.Controls.Add(this.DisplayCodeTextbox);
            this.Controls.Add(this.DefinedTextbox);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(this.NameTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailPolicy";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Policy Details";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox NameTextbox;
        internal TextBox IdTextbox;
        internal TextBox DefinedTextbox;
        internal TextBox DisplayCodeTextbox;
        internal TextBox InfoCodeTextbox;
        internal TextBox PresentCodeTextbox;
        internal TextBox SectionTextbox;
        internal TextBox SupportTextbox;
        internal TextBox CategoryTextbox;
        internal Button CategoryButton;
        internal Button SupportButton;
        internal Button CloseButton;
    }
}