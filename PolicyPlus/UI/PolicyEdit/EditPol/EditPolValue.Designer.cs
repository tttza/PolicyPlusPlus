using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class EditPolValue : Form
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
            this.ComboKind = new System.Windows.Forms.ComboBox();
            this.ButtonOK = new System.Windows.Forms.Button();
            this.TextName = new System.Windows.Forms.TextBox();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 38);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(105, 12);
            Label1.TabIndex = 1;
            Label1.Text = "Registry value type";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 14);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(65, 12);
            Label2.TabIndex = 4;
            Label2.Text = "Value name";
            // 
            // ComboKind
            // 
            this.ComboKind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboKind.FormattingEnabled = true;
            this.ComboKind.Items.AddRange(new object[] {
            "String",
            "Expandable string",
            "List of strings",
            "32-bit DWord",
            "64-bit QWord"});
            this.ComboKind.Location = new System.Drawing.Point(115, 35);
            this.ComboKind.Name = "ComboKind";
            this.ComboKind.Size = new System.Drawing.Size(162, 20);
            this.ComboKind.TabIndex = 2;
            // 
            // ButtonOK
            // 
            this.ButtonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonOK.Location = new System.Drawing.Point(202, 60);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 21);
            this.ButtonOK.TabIndex = 3;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            // 
            // TextName
            // 
            this.TextName.Location = new System.Drawing.Point(115, 11);
            this.TextName.Name = "TextName";
            this.TextName.Size = new System.Drawing.Size(162, 19);
            this.TextName.TabIndex = 1;
            // 
            // EditPolValue
            // 
            this.AcceptButton = this.ButtonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(289, 92);
            this.Controls.Add(Label2);
            this.Controls.Add(this.TextName);
            this.Controls.Add(this.ButtonOK);
            this.Controls.Add(Label1);
            this.Controls.Add(this.ComboKind);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPolValue";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New Value";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.EditPolValueType_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal ComboBox ComboKind;
        internal Button ButtonOK;
        internal TextBox TextName;
    }
}