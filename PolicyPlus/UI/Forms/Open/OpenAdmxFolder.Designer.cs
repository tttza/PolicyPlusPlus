using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class OpenAdmxFolder : Form
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
            System.Windows.Forms.Label LabelFromWhere;
            this.OptLocalFolder = new System.Windows.Forms.RadioButton();
            this.OptSysvol = new System.Windows.Forms.RadioButton();
            this.OptCustomFolder = new System.Windows.Forms.RadioButton();
            this.TextFolder = new System.Windows.Forms.TextBox();
            this.ButtonOK = new System.Windows.Forms.Button();
            this.ButtonBrowse = new System.Windows.Forms.Button();
            this.ClearWorkspaceCheckbox = new System.Windows.Forms.CheckBox();
            LabelFromWhere = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LabelFromWhere
            // 
            LabelFromWhere.AutoSize = true;
            LabelFromWhere.Location = new System.Drawing.Point(12, 8);
            LabelFromWhere.Name = "LabelFromWhere";
            LabelFromWhere.Size = new System.Drawing.Size(246, 12);
            LabelFromWhere.TabIndex = 0;
            LabelFromWhere.Text = "Where would you like to load ADMX files from?";
            // 
            // OptLocalFolder
            // 
            this.OptLocalFolder.AutoSize = true;
            this.OptLocalFolder.Location = new System.Drawing.Point(15, 23);
            this.OptLocalFolder.Name = "OptLocalFolder";
            this.OptLocalFolder.Size = new System.Drawing.Size(217, 16);
            this.OptLocalFolder.TabIndex = 1;
            this.OptLocalFolder.TabStop = true;
            this.OptLocalFolder.Text = "This system\'s PolicyDefinitions folder";
            this.OptLocalFolder.UseVisualStyleBackColor = true;
            this.OptLocalFolder.CheckedChanged += new System.EventHandler(this.Options_CheckedChanged);
            // 
            // OptSysvol
            // 
            this.OptSysvol.AutoSize = true;
            this.OptSysvol.Location = new System.Drawing.Point(15, 44);
            this.OptSysvol.Name = "OptSysvol";
            this.OptSysvol.Size = new System.Drawing.Size(137, 16);
            this.OptSysvol.TabIndex = 2;
            this.OptSysvol.TabStop = true;
            this.OptSysvol.Text = "The domain\'s SYSVOL";
            this.OptSysvol.UseVisualStyleBackColor = true;
            this.OptSysvol.CheckedChanged += new System.EventHandler(this.Options_CheckedChanged);
            // 
            // OptCustomFolder
            // 
            this.OptCustomFolder.AutoSize = true;
            this.OptCustomFolder.Location = new System.Drawing.Point(15, 66);
            this.OptCustomFolder.Name = "OptCustomFolder";
            this.OptCustomFolder.Size = new System.Drawing.Size(80, 16);
            this.OptCustomFolder.TabIndex = 3;
            this.OptCustomFolder.TabStop = true;
            this.OptCustomFolder.Text = "This folder:";
            this.OptCustomFolder.UseVisualStyleBackColor = true;
            this.OptCustomFolder.CheckedChanged += new System.EventHandler(this.Options_CheckedChanged);
            // 
            // TextFolder
            // 
            this.TextFolder.Location = new System.Drawing.Point(98, 65);
            this.TextFolder.Name = "TextFolder";
            this.TextFolder.Size = new System.Drawing.Size(265, 19);
            this.TextFolder.TabIndex = 4;
            // 
            // ButtonOK
            // 
            this.ButtonOK.Location = new System.Drawing.Point(354, 89);
            this.ButtonOK.Name = "ButtonOK";
            this.ButtonOK.Size = new System.Drawing.Size(75, 21);
            this.ButtonOK.TabIndex = 7;
            this.ButtonOK.Text = "OK";
            this.ButtonOK.UseVisualStyleBackColor = true;
            this.ButtonOK.Click += new System.EventHandler(this.ButtonOK_Click);
            // 
            // ButtonBrowse
            // 
            this.ButtonBrowse.Location = new System.Drawing.Point(369, 63);
            this.ButtonBrowse.Name = "ButtonBrowse";
            this.ButtonBrowse.Size = new System.Drawing.Size(60, 21);
            this.ButtonBrowse.TabIndex = 5;
            this.ButtonBrowse.Text = "Browse";
            this.ButtonBrowse.UseVisualStyleBackColor = true;
            this.ButtonBrowse.Click += new System.EventHandler(this.ButtonBrowse_Click);
            // 
            // ClearWorkspaceCheckbox
            // 
            this.ClearWorkspaceCheckbox.AutoSize = true;
            this.ClearWorkspaceCheckbox.Checked = true;
            this.ClearWorkspaceCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ClearWorkspaceCheckbox.Location = new System.Drawing.Point(15, 92);
            this.ClearWorkspaceCheckbox.Name = "ClearWorkspaceCheckbox";
            this.ClearWorkspaceCheckbox.Size = new System.Drawing.Size(258, 16);
            this.ClearWorkspaceCheckbox.TabIndex = 6;
            this.ClearWorkspaceCheckbox.Text = "Clear the workspace before adding this folder";
            this.ClearWorkspaceCheckbox.UseVisualStyleBackColor = true;
            // 
            // OpenAdmxFolder
            // 
            this.AcceptButton = this.ButtonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(441, 121);
            this.Controls.Add(this.ClearWorkspaceCheckbox);
            this.Controls.Add(this.ButtonBrowse);
            this.Controls.Add(this.ButtonOK);
            this.Controls.Add(this.TextFolder);
            this.Controls.Add(this.OptCustomFolder);
            this.Controls.Add(this.OptSysvol);
            this.Controls.Add(this.OptLocalFolder);
            this.Controls.Add(LabelFromWhere);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OpenAdmxFolder";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Open ADMX Folder";
            this.Shown += new System.EventHandler(this.OpenAdmxFolder_Shown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OpenAdmxFolder_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal RadioButton OptLocalFolder;
        internal RadioButton OptSysvol;
        internal RadioButton OptCustomFolder;
        internal TextBox TextFolder;
        internal Button ButtonOK;
        internal Button ButtonBrowse;
        internal CheckBox ClearWorkspaceCheckbox;
    }
}