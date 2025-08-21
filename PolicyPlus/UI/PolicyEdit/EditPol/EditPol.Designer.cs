using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    public partial class EditPol : Form
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
            LsvPol = new ListView();
            ChItem = new ColumnHeader();
            ChValue = new ColumnHeader();
            ButtonSave = new Button();
            ButtonAddKey = new Button();
            ButtonAddValue = new Button();
            ButtonDeleteValue = new Button();
            ButtonForget = new Button();
            ButtonEdit = new Button();
            ButtonImport = new Button();
            ButtonExport = new Button();
            SuspendLayout();
            // 
            // LsvPol
            // 
            LsvPol.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            LsvPol.Columns.AddRange(new ColumnHeader[] { ChItem, ChValue });
            LsvPol.FullRowSelect = true;
            LsvPol.Location = new Point(12, 38);
            LsvPol.MultiSelect = false;
            LsvPol.Name = "LsvPol";
            LsvPol.ShowItemToolTips = true;
            LsvPol.Size = new Size(566, 492);
            LsvPol.TabIndex = 0;
            LsvPol.UseCompatibleStateImageBehavior = false;
            LsvPol.View = View.Details;
            LsvPol.SelectedIndexChanged += LsvPol_SelectedIndexChanged;
            // 
            // ChItem
            // 
            ChItem.Text = "Name";
            ChItem.Width = 377;
            // 
            // ChValue
            // 
            ChValue.Text = "Value";
            ChValue.Width = 160;
            // 
            // ButtonSave
            // 
            ButtonSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ButtonSave.Location = new Point(562, 534);
            ButtonSave.Name = "ButtonSave";
            ButtonSave.Size = new Size(75, 21);
            ButtonSave.TabIndex = 11;
            ButtonSave.Text = "Done";
            ButtonSave.UseVisualStyleBackColor = true;
            ButtonSave.Click += ButtonSave_Click;
            // 
            // ButtonAddKey
            // 
            ButtonAddKey.Location = new Point(12, 11);
            ButtonAddKey.Name = "ButtonAddKey";
            ButtonAddKey.Size = new Size(75, 21);
            ButtonAddKey.TabIndex = 3;
            ButtonAddKey.Text = "Add Key";
            ButtonAddKey.UseVisualStyleBackColor = true;
            ButtonAddKey.Click += ButtonAddKey_Click;
            // 
            // ButtonAddValue
            // 
            ButtonAddValue.Location = new Point(93, 11);
            ButtonAddValue.Name = "ButtonAddValue";
            ButtonAddValue.Size = new Size(87, 21);
            ButtonAddValue.TabIndex = 4;
            ButtonAddValue.Text = "Add Value";
            ButtonAddValue.UseVisualStyleBackColor = true;
            ButtonAddValue.Click += ButtonAddValue_Click;
            // 
            // ButtonDeleteValue
            // 
            ButtonDeleteValue.Location = new Point(186, 11);
            ButtonDeleteValue.Name = "ButtonDeleteValue";
            ButtonDeleteValue.Size = new Size(100, 21);
            ButtonDeleteValue.TabIndex = 5;
            ButtonDeleteValue.Text = "Delete Value(s)";
            ButtonDeleteValue.UseVisualStyleBackColor = true;
            ButtonDeleteValue.Click += ButtonDeleteValue_Click;
            // 
            // ButtonForget
            // 
            ButtonForget.Location = new Point(292, 11);
            ButtonForget.Name = "ButtonForget";
            ButtonForget.Size = new Size(75, 21);
            ButtonForget.TabIndex = 6;
            ButtonForget.Text = "Forget";
            ButtonForget.UseVisualStyleBackColor = true;
            ButtonForget.Click += ButtonForget_Click;
            // 
            // ButtonEdit
            // 
            ButtonEdit.Location = new Point(373, 11);
            ButtonEdit.Name = "ButtonEdit";
            ButtonEdit.Size = new Size(75, 21);
            ButtonEdit.TabIndex = 7;
            ButtonEdit.Text = "Edit";
            ButtonEdit.UseVisualStyleBackColor = true;
            ButtonEdit.Click += ButtonEdit_Click;
            // 
            // ButtonImport
            // 
            ButtonImport.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ButtonImport.Location = new Point(12, 534);
            ButtonImport.Name = "ButtonImport";
            ButtonImport.Size = new Size(75, 21);
            ButtonImport.TabIndex = 9;
            ButtonImport.Text = "Import REG";
            ButtonImport.UseVisualStyleBackColor = true;
            ButtonImport.Click += ButtonImport_Click;
            // 
            // ButtonExport
            // 
            ButtonExport.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ButtonExport.Location = new Point(93, 534);
            ButtonExport.Name = "ButtonExport";
            ButtonExport.Size = new Size(75, 21);
            ButtonExport.TabIndex = 10;
            ButtonExport.Text = "Export REG";
            ButtonExport.UseVisualStyleBackColor = true;
            ButtonExport.Click += ButtonExport_Click;
            // 
            // EditPol
            // 
            AcceptButton = ButtonSave;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(579, 293);
            Controls.Add(ButtonExport);
            Controls.Add(ButtonImport);
            Controls.Add(ButtonEdit);
            Controls.Add(ButtonForget);
            Controls.Add(ButtonDeleteValue);
            Controls.Add(ButtonAddValue);
            Controls.Add(ButtonAddKey);
            Controls.Add(ButtonSave);
            Controls.Add(LsvPol);
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "EditPol";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Edit Raw POL";
            KeyDown += EditPol_KeyDown;
            ResumeLayout(false);

        }

        internal ListView LsvPol;
        internal Button ButtonSave;
        internal ColumnHeader ChItem;
        internal ColumnHeader ChValue;
        internal Button ButtonAddKey;
        internal Button ButtonAddValue;
        internal Button ButtonDeleteValue;
        internal Button ButtonForget;
        internal Button ButtonEdit;
        internal Button ButtonImport;
        internal Button ButtonExport;
    }
}