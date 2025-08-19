using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class OpenUserRegistry : Form
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
            this.SubfoldersListview = new System.Windows.Forms.ListView();
            this.ChUsername = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChAccess = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.OkButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // SubfoldersListview
            // 
            this.SubfoldersListview.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChUsername,
            this.ChAccess});
            this.SubfoldersListview.FullRowSelect = true;
            this.SubfoldersListview.HideSelection = false;
            this.SubfoldersListview.Location = new System.Drawing.Point(12, 11);
            this.SubfoldersListview.MultiSelect = false;
            this.SubfoldersListview.Name = "SubfoldersListview";
            this.SubfoldersListview.Size = new System.Drawing.Size(314, 103);
            this.SubfoldersListview.TabIndex = 0;
            this.SubfoldersListview.UseCompatibleStateImageBehavior = false;
            this.SubfoldersListview.View = System.Windows.Forms.View.Details;
            // 
            // ChUsername
            // 
            this.ChUsername.Text = "Folder Name";
            this.ChUsername.Width = 196;
            // 
            // ChAccess
            // 
            this.ChAccess.Text = "Accessible";
            this.ChAccess.Width = 95;
            // 
            // OkButton
            // 
            this.OkButton.Location = new System.Drawing.Point(251, 119);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 21);
            this.OkButton.TabIndex = 1;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += new System.EventHandler(this.OkButton_Click);
            // 
            // OpenUserRegistry
            // 
            this.AcceptButton = this.OkButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(338, 151);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(this.SubfoldersListview);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OpenUserRegistry";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Open User Hive";
            this.Shown += new System.EventHandler(this.OpenUserRegistry_Shown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OpenUserRegistry_KeyUp);
            this.ResumeLayout(false);

        }

        internal ListView SubfoldersListview;
        internal ColumnHeader ChUsername;
        internal ColumnHeader ChAccess;
        internal Button OkButton;
    }
}