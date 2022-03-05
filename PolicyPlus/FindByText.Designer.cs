using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class FindByText : Form
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
            this.StringTextbox = new System.Windows.Forms.TextBox();
            this.TitleCheckbox = new System.Windows.Forms.CheckBox();
            this.DescriptionCheckbox = new System.Windows.Forms.CheckBox();
            this.CommentCheckbox = new System.Windows.Forms.CheckBox();
            this.SearchButton = new System.Windows.Forms.Button();
            this.IdCheckbox = new System.Windows.Forms.CheckBox();
            this.partialCheckbox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // StringTextbox
            // 
            this.StringTextbox.Location = new System.Drawing.Point(16, 14);
            this.StringTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.StringTextbox.Name = "StringTextbox";
            this.StringTextbox.Size = new System.Drawing.Size(468, 22);
            this.StringTextbox.TabIndex = 0;
            // 
            // TitleCheckbox
            // 
            this.TitleCheckbox.AutoSize = true;
            this.TitleCheckbox.Checked = true;
            this.TitleCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.TitleCheckbox.Location = new System.Drawing.Point(16, 44);
            this.TitleCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.TitleCheckbox.Name = "TitleCheckbox";
            this.TitleCheckbox.Size = new System.Drawing.Size(70, 19);
            this.TitleCheckbox.TabIndex = 1;
            this.TitleCheckbox.Text = "In title";
            this.TitleCheckbox.UseVisualStyleBackColor = true;
            // 
            // DescriptionCheckbox
            // 
            this.DescriptionCheckbox.AutoSize = true;
            this.DescriptionCheckbox.Checked = true;
            this.DescriptionCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.DescriptionCheckbox.Location = new System.Drawing.Point(96, 44);
            this.DescriptionCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.DescriptionCheckbox.Name = "DescriptionCheckbox";
            this.DescriptionCheckbox.Size = new System.Drawing.Size(115, 19);
            this.DescriptionCheckbox.TabIndex = 2;
            this.DescriptionCheckbox.Text = "In description";
            this.DescriptionCheckbox.UseVisualStyleBackColor = true;
            // 
            // CommentCheckbox
            // 
            this.CommentCheckbox.AutoSize = true;
            this.CommentCheckbox.Checked = true;
            this.CommentCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.CommentCheckbox.Location = new System.Drawing.Point(223, 44);
            this.CommentCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.CommentCheckbox.Name = "CommentCheckbox";
            this.CommentCheckbox.Size = new System.Drawing.Size(105, 19);
            this.CommentCheckbox.TabIndex = 3;
            this.CommentCheckbox.Text = "In comment";
            this.CommentCheckbox.UseVisualStyleBackColor = true;
            // 
            // SearchButton
            // 
            this.SearchButton.Location = new System.Drawing.Point(385, 70);
            this.SearchButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.SearchButton.Name = "SearchButton";
            this.SearchButton.Size = new System.Drawing.Size(100, 27);
            this.SearchButton.TabIndex = 4;
            this.SearchButton.Text = "Search";
            this.SearchButton.UseVisualStyleBackColor = true;
            this.SearchButton.Click += new System.EventHandler(this.SearchButton_Click);
            // 
            // IdCheckbox
            // 
            this.IdCheckbox.AutoSize = true;
            this.IdCheckbox.Checked = true;
            this.IdCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.IdCheckbox.Location = new System.Drawing.Point(337, 44);
            this.IdCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.IdCheckbox.Name = "IdCheckbox";
            this.IdCheckbox.Size = new System.Drawing.Size(60, 19);
            this.IdCheckbox.TabIndex = 5;
            this.IdCheckbox.Text = "In ID";
            this.IdCheckbox.UseVisualStyleBackColor = true;
            // 
            // partialCheckbox
            // 
            this.partialCheckbox.AutoSize = true;
            this.partialCheckbox.Checked = true;
            this.partialCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.partialCheckbox.Location = new System.Drawing.Point(16, 75);
            this.partialCheckbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.partialCheckbox.Name = "partialCheckbox";
            this.partialCheckbox.Size = new System.Drawing.Size(117, 19);
            this.partialCheckbox.TabIndex = 6;
            this.partialCheckbox.Text = "Partial Match ";
            this.partialCheckbox.UseVisualStyleBackColor = true;
            // 
            // FindByText
            // 
            this.AcceptButton = this.SearchButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(501, 111);
            this.Controls.Add(this.partialCheckbox);
            this.Controls.Add(this.IdCheckbox);
            this.Controls.Add(this.SearchButton);
            this.Controls.Add(this.CommentCheckbox);
            this.Controls.Add(this.DescriptionCheckbox);
            this.Controls.Add(this.TitleCheckbox);
            this.Controls.Add(this.StringTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindByText";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find by Text";
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FindByText_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox StringTextbox;
        internal CheckBox TitleCheckbox;
        internal CheckBox DescriptionCheckbox;
        internal CheckBox CommentCheckbox;
        internal Button SearchButton;
        internal CheckBox IdCheckbox;
        internal CheckBox partialCheckbox;
    }
}