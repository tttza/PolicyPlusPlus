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
            this.LsvPol = new System.Windows.Forms.ListView();
            this.ChItem = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ButtonSave = new System.Windows.Forms.Button();
            this.ButtonAddKey = new System.Windows.Forms.Button();
            this.ButtonAddValue = new System.Windows.Forms.Button();
            this.ButtonDeleteValue = new System.Windows.Forms.Button();
            this.ButtonForget = new System.Windows.Forms.Button();
            this.ButtonEdit = new System.Windows.Forms.Button();
            this.ButtonImport = new System.Windows.Forms.Button();
            this.ButtonExport = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // LsvPol
            // 
            this.LsvPol.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LsvPol.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChItem,
            this.ChValue});
            this.LsvPol.FullRowSelect = true;
            this.LsvPol.HideSelection = false;
            this.LsvPol.Location = new System.Drawing.Point(12, 38);
            this.LsvPol.MultiSelect = false;
            this.LsvPol.Name = "LsvPol";
            this.LsvPol.ShowItemToolTips = true;
            this.LsvPol.Size = new System.Drawing.Size(555, 217);
            this.LsvPol.TabIndex = 0;
            this.LsvPol.UseCompatibleStateImageBehavior = false;
            this.LsvPol.View = System.Windows.Forms.View.Details;
            this.LsvPol.SelectedIndexChanged += new System.EventHandler(this.LsvPol_SelectedIndexChanged);
            // 
            // ChItem
            // 
            this.ChItem.Text = "Name";
            this.ChItem.Width = 377;
            // 
            // ChValue
            // 
            this.ChValue.Text = "Value";
            this.ChValue.Width = 160;
            // 
            // ButtonSave
            // 
            this.ButtonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonSave.Location = new System.Drawing.Point(492, 260);
            this.ButtonSave.Name = "ButtonSave";
            this.ButtonSave.Size = new System.Drawing.Size(75, 21);
            this.ButtonSave.TabIndex = 11;
            this.ButtonSave.Text = "Done";
            this.ButtonSave.UseVisualStyleBackColor = true;
            this.ButtonSave.Click += new System.EventHandler(this.ButtonSave_Click);
            // 
            // ButtonAddKey
            // 
            this.ButtonAddKey.Location = new System.Drawing.Point(12, 11);
            this.ButtonAddKey.Name = "ButtonAddKey";
            this.ButtonAddKey.Size = new System.Drawing.Size(75, 21);
            this.ButtonAddKey.TabIndex = 3;
            this.ButtonAddKey.Text = "Add Key";
            this.ButtonAddKey.UseVisualStyleBackColor = true;
            this.ButtonAddKey.Click += new System.EventHandler(this.ButtonAddKey_Click);
            // 
            // ButtonAddValue
            // 
            this.ButtonAddValue.Location = new System.Drawing.Point(93, 11);
            this.ButtonAddValue.Name = "ButtonAddValue";
            this.ButtonAddValue.Size = new System.Drawing.Size(87, 21);
            this.ButtonAddValue.TabIndex = 4;
            this.ButtonAddValue.Text = "Add Value";
            this.ButtonAddValue.UseVisualStyleBackColor = true;
            this.ButtonAddValue.Click += new System.EventHandler(this.ButtonAddValue_Click);
            // 
            // ButtonDeleteValue
            // 
            this.ButtonDeleteValue.Location = new System.Drawing.Point(186, 11);
            this.ButtonDeleteValue.Name = "ButtonDeleteValue";
            this.ButtonDeleteValue.Size = new System.Drawing.Size(100, 21);
            this.ButtonDeleteValue.TabIndex = 5;
            this.ButtonDeleteValue.Text = "Delete Value(s)";
            this.ButtonDeleteValue.UseVisualStyleBackColor = true;
            this.ButtonDeleteValue.Click += new System.EventHandler(this.ButtonDeleteValue_Click);
            // 
            // ButtonForget
            // 
            this.ButtonForget.Location = new System.Drawing.Point(292, 11);
            this.ButtonForget.Name = "ButtonForget";
            this.ButtonForget.Size = new System.Drawing.Size(75, 21);
            this.ButtonForget.TabIndex = 6;
            this.ButtonForget.Text = "Forget";
            this.ButtonForget.UseVisualStyleBackColor = true;
            this.ButtonForget.Click += new System.EventHandler(this.ButtonForget_Click);
            // 
            // ButtonEdit
            // 
            this.ButtonEdit.Location = new System.Drawing.Point(373, 11);
            this.ButtonEdit.Name = "ButtonEdit";
            this.ButtonEdit.Size = new System.Drawing.Size(75, 21);
            this.ButtonEdit.TabIndex = 7;
            this.ButtonEdit.Text = "Edit";
            this.ButtonEdit.UseVisualStyleBackColor = true;
            this.ButtonEdit.Click += new System.EventHandler(this.ButtonEdit_Click);
            // 
            // ButtonImport
            // 
            this.ButtonImport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ButtonImport.Location = new System.Drawing.Point(12, 260);
            this.ButtonImport.Name = "ButtonImport";
            this.ButtonImport.Size = new System.Drawing.Size(75, 21);
            this.ButtonImport.TabIndex = 9;
            this.ButtonImport.Text = "Import REG";
            this.ButtonImport.UseVisualStyleBackColor = true;
            this.ButtonImport.Click += new System.EventHandler(this.ButtonImport_Click);
            // 
            // ButtonExport
            // 
            this.ButtonExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.ButtonExport.Location = new System.Drawing.Point(93, 260);
            this.ButtonExport.Name = "ButtonExport";
            this.ButtonExport.Size = new System.Drawing.Size(75, 21);
            this.ButtonExport.TabIndex = 10;
            this.ButtonExport.Text = "Export REG";
            this.ButtonExport.UseVisualStyleBackColor = true;
            this.ButtonExport.Click += new System.EventHandler(this.ButtonExport_Click);
            // 
            // EditPol
            // 
            this.AcceptButton = this.ButtonSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(579, 293);
            this.Controls.Add(this.ButtonExport);
            this.Controls.Add(this.ButtonImport);
            this.Controls.Add(this.ButtonEdit);
            this.Controls.Add(this.ButtonForget);
            this.Controls.Add(this.ButtonDeleteValue);
            this.Controls.Add(this.ButtonAddValue);
            this.Controls.Add(this.ButtonAddKey);
            this.Controls.Add(this.ButtonSave);
            this.Controls.Add(this.LsvPol);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPol";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Raw POL";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.EditPol_KeyDown);
            this.ResumeLayout(false);

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