using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class EditPolDelete : Form
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
            this.TextKey = new System.Windows.Forms.TextBox();
            this.OptPurge = new System.Windows.Forms.RadioButton();
            this.OptClearFirst = new System.Windows.Forms.RadioButton();
            this.OptDeleteOne = new System.Windows.Forms.RadioButton();
            this.TextValueName = new System.Windows.Forms.TextBox();
            this.ButtonOK = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(76, 12);
            Label1.TabIndex = 1;
            Label1.Text = "Container key";
            // 
            // TextKey
            // 
            this.TextKey.Location = new System.Drawing.Point(90, 11);
            this.TextKey.Name = "TextKey";
            this.TextKey.ReadOnly = true;
            this.TextKey.Size = new System.Drawing.Size(179, 19);
            this.TextKey.TabIndex = 0;
            // 
            // OptPurge
            // 
            this.OptPurge.AutoSize = true;
            this.OptPurge.Location = new System.Drawing.Point(15, 35);
            this.OptPurge.Name = "OptPurge";
            this.OptPurge.Size = new System.Drawing.Size(203, 16);
            this.OptPurge.TabIndex = 2;
            this.OptPurge.TabStop = true;
            this.OptPurge.Text = "Delete all the values under the key";
            this.OptPurge.UseVisualStyleBackColor = true;
            this.OptPurge.CheckedChanged += new System.EventHandler(this.ChoiceChanged);
            // 
            // OptClearFirst
            // 
            this.OptClearFirst.AutoSize = true;
            this.OptClearFirst.Location = new System.Drawing.Point(15, 56);
            this.OptClearFirst.Name = "OptClearFirst";
            this.OptClearFirst.Size = new System.Drawing.Size(202, 16);
            this.OptClearFirst.TabIndex = 3;
            this.OptClearFirst.TabStop = true;
            this.OptClearFirst.Text = "Clear the key before adding values";
            this.OptClearFirst.UseVisualStyleBackColor = true;
            this.OptClearFirst.CheckedChanged += new System.EventHandler(this.ChoiceChanged);
            // 
            // OptDeleteOne
            // 
            this.OptDeleteOne.AutoSize = true;
            this.OptDeleteOne.Location = new System.Drawing.Point(15, 78);
            this.OptDeleteOne.Name = "OptDeleteOne";
            this.OptDeleteOne.Size = new System.Drawing.Size(112, 16);
            this.OptDeleteOne.TabIndex = 4;
            this.OptDeleteOne.TabStop = true;
            this.OptDeleteOne.Text = "Delete this value:";
            this.OptDeleteOne.UseVisualStyleBackColor = true;
            this.OptDeleteOne.CheckedChanged += new System.EventHandler(this.ChoiceChanged);
            // 
            // TextValueName
            // 
            this.TextValueName.Enabled = false;
            this.TextValueName.Location = new System.Drawing.Point(128, 77);
            this.TextValueName.Name = "TextValueName";
            this.TextValueName.Size = new System.Drawing.Size(141, 19);
            this.TextValueName.TabIndex = 5;
            // 
            // ButtonOK
            // 
            this.ButtonOK.Location = new System.Drawing.Point(194, 101);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 21);
            this.ButtonOK.TabIndex = 6;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            this.ButtonOK.Click += new System.EventHandler(this.ButtonOK_Click);
            // 
            // EditPolDelete
            // 
            this.AcceptButton = this.ButtonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(281, 133);
            this.Controls.Add(this.ButtonOK);
            this.Controls.Add(this.TextValueName);
            this.Controls.Add(this.OptDeleteOne);
            this.Controls.Add(this.OptClearFirst);
            this.Controls.Add(this.OptPurge);
            this.Controls.Add(Label1);
            this.Controls.Add(this.TextKey);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPolDelete";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Delete Value(s)";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.EditPolDelete_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextKey;
        internal RadioButton OptPurge;
        internal RadioButton OptClearFirst;
        internal RadioButton OptDeleteOne;
        internal TextBox TextValueName;
        internal Button ButtonOK;
    }
}