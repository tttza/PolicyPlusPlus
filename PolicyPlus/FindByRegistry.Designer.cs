using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class FindByRegistry : Form
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
            System.Windows.Forms.Label KeyPathLabel;
            System.Windows.Forms.Label ValueLabel;
            this.KeyTextbox = new System.Windows.Forms.TextBox();
            this.ValueTextbox = new System.Windows.Forms.TextBox();
            this.SearchButton = new System.Windows.Forms.Button();
            KeyPathLabel = new System.Windows.Forms.Label();
            ValueLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // KeyPathLabel
            // 
            KeyPathLabel.AutoSize = true;
            KeyPathLabel.Location = new System.Drawing.Point(12, 14);
            KeyPathLabel.Name = "KeyPathLabel";
            KeyPathLabel.Size = new System.Drawing.Size(95, 12);
            KeyPathLabel.TabIndex = 0;
            KeyPathLabel.Text = "Key path or name";
            // 
            // ValueLabel
            // 
            ValueLabel.AutoSize = true;
            ValueLabel.Location = new System.Drawing.Point(12, 38);
            ValueLabel.Name = "ValueLabel";
            ValueLabel.Size = new System.Drawing.Size(65, 12);
            ValueLabel.TabIndex = 2;
            ValueLabel.Text = "Value name";
            // 
            // KeyTextbox
            // 
            this.KeyTextbox.Location = new System.Drawing.Point(108, 11);
            this.KeyTextbox.Name = "KeyTextbox";
            this.KeyTextbox.Size = new System.Drawing.Size(260, 19);
            this.KeyTextbox.TabIndex = 1;
            // 
            // ValueTextbox
            // 
            this.ValueTextbox.Location = new System.Drawing.Point(108, 35);
            this.ValueTextbox.Name = "ValueTextbox";
            this.ValueTextbox.Size = new System.Drawing.Size(260, 19);
            this.ValueTextbox.TabIndex = 2;
            // 
            // SearchButton
            // 
            this.SearchButton.Location = new System.Drawing.Point(293, 59);
            this.SearchButton.Name = "SearchButton";
            this.SearchButton.Size = new System.Drawing.Size(75, 21);
            this.SearchButton.TabIndex = 3;
            this.SearchButton.Text = "Search";
            this.SearchButton.UseVisualStyleBackColor = true;
            this.SearchButton.Click += new System.EventHandler(this.SearchButton_Click);
            // 
            // FindByRegistry
            // 
            this.AcceptButton = this.SearchButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(380, 91);
            this.Controls.Add(this.SearchButton);
            this.Controls.Add(ValueLabel);
            this.Controls.Add(this.ValueTextbox);
            this.Controls.Add(this.KeyTextbox);
            this.Controls.Add(KeyPathLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindByRegistry";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find by Registry";
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FindByRegistry_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox KeyTextbox;
        internal TextBox ValueTextbox;
        internal Button SearchButton;
    }
}