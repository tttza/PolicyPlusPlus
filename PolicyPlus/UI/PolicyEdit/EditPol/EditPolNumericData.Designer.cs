using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    public partial class EditPolNumericData : Form
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
            System.Windows.Forms.Label Label1;
            System.Windows.Forms.Label Label2;
            this.TextName = new System.Windows.Forms.TextBox();
            this.CheckHexadecimal = new System.Windows.Forms.CheckBox();
                this.NumData = new PolicyPlus.UI.PolicyDetail.WideRangeNumericUpDown();
            this.ButtonOK = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.NumData)).BeginInit();
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
            Label2.Size = new System.Drawing.Size(44, 12);
            Label2.TabIndex = 3;
            Label2.Text = "Number";
            // 
            // TextName
            // 
            this.TextName.Location = new System.Drawing.Point(81, 11);
            this.TextName.Name = "TextName";
            this.TextName.ReadOnly = true;
            this.TextName.Size = new System.Drawing.Size(230, 19);
            this.TextName.TabIndex = 0;
            // 
            // CheckHexadecimal
            // 
            this.CheckHexadecimal.AutoSize = true;
            this.CheckHexadecimal.Location = new System.Drawing.Point(224, 37);
            this.CheckHexadecimal.Name = "CheckHexadecimal";
            this.CheckHexadecimal.Size = new System.Drawing.Size(89, 16);
            this.CheckHexadecimal.TabIndex = 2;
            this.CheckHexadecimal.Text = "Hexadecimal";
            this.CheckHexadecimal.UseVisualStyleBackColor = true;
            this.CheckHexadecimal.CheckedChanged += new System.EventHandler(this.CheckHexadecimal_CheckedChanged);
            // 
            // NumData
            // 
            this.NumData.Location = new System.Drawing.Point(81, 34);
            this.NumData.Name = "NumData";
            this.NumData.Size = new System.Drawing.Size(137, 19);
            this.NumData.TabIndex = 1;
            // 
            // ButtonOK
            // 
            this.ButtonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonOK.Location = new System.Drawing.Point(236, 58);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 21);
            this.ButtonOK.TabIndex = 3;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            // 
            // EditPolNumericData
            // 
            this.AcceptButton = this.ButtonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(323, 90);
            this.Controls.Add(this.ButtonOK);
            this.Controls.Add(this.NumData);
            this.Controls.Add(this.CheckHexadecimal);
            this.Controls.Add(Label2);
            this.Controls.Add(Label1);
            this.Controls.Add(this.TextName);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPolNumericData";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Number";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.EditPolNumericData_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.NumData)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextName;
        internal CheckBox CheckHexadecimal;
        internal Button ButtonOK;
        internal WideRangeNumericUpDown NumData;
    }
}