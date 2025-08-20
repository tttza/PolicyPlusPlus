using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    public partial class EditPolMultiStringData : Form
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
            this.TextName = new System.Windows.Forms.TextBox();
            this.TextData = new System.Windows.Forms.TextBox();
            this.ButtonOK = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(65, 12);
            Label1.TabIndex = 1;
            Label1.Text = "Value name";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 38);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(41, 12);
            Label2.TabIndex = 3;
            Label2.Text = "Entries";
            // 
            // TextName
            // 
            this.TextName.Location = new System.Drawing.Point(81, 11);
            this.TextName.Name = "TextName";
            this.TextName.ReadOnly = true;
            this.TextName.Size = new System.Drawing.Size(262, 19);
            this.TextName.TabIndex = 0;
            // 
            // TextData
            // 
            this.TextData.Location = new System.Drawing.Point(81, 35);
            this.TextData.Multiline = true;
            this.TextData.Name = "TextData";
            this.TextData.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.TextData.Size = new System.Drawing.Size(262, 118);
            this.TextData.TabIndex = 2;
            // 
            // ButtonOK
            // 
            this.ButtonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonOK.Location = new System.Drawing.Point(268, 159);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 21);
            this.ButtonOK.TabIndex = 4;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            // 
            // EditPolMultiStringData
            // 
            this.AcceptButton = this.ButtonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(355, 191);
            this.Controls.Add(this.ButtonOK);
            this.Controls.Add(Label2);
            this.Controls.Add(this.TextData);
            this.Controls.Add(Label1);
            this.Controls.Add(this.TextName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPolMultiStringData";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit String List";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.EditPolMultiStringData_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextName;
        internal TextBox TextData;
        internal Button ButtonOK;
    }
}