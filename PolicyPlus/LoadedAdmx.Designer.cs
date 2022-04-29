using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class LoadedAdmx : Form
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
            this.LsvAdmx = new System.Windows.Forms.ListView();
            this.ChFileTitle = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChFolder = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChNamespace = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ButtonClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // LsvAdmx
            // 
            this.LsvAdmx.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LsvAdmx.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChFileTitle,
            this.ChFolder,
            this.ChNamespace});
            this.LsvAdmx.FullRowSelect = true;
            this.LsvAdmx.HideSelection = false;
            this.LsvAdmx.Location = new System.Drawing.Point(12, 11);
            this.LsvAdmx.MultiSelect = false;
            this.LsvAdmx.Name = "LsvAdmx";
            this.LsvAdmx.ShowItemToolTips = true;
            this.LsvAdmx.Size = new System.Drawing.Size(487, 215);
            this.LsvAdmx.TabIndex = 0;
            this.LsvAdmx.UseCompatibleStateImageBehavior = false;
            this.LsvAdmx.View = System.Windows.Forms.View.Details;
            this.LsvAdmx.DoubleClick += new System.EventHandler(this.LsvAdmx_DoubleClick);
            this.LsvAdmx.KeyDown += new System.Windows.Forms.KeyEventHandler(this.LsvAdmx_KeyDown);
            // 
            // ChFileTitle
            // 
            this.ChFileTitle.Text = "File";
            this.ChFileTitle.Width = 88;
            // 
            // ChFolder
            // 
            this.ChFolder.Text = "Folder";
            this.ChFolder.Width = 203;
            // 
            // ChNamespace
            // 
            this.ChNamespace.Text = "Namespace";
            this.ChNamespace.Width = 172;
            // 
            // ButtonClose
            // 
            this.ButtonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonClose.Location = new System.Drawing.Point(424, 232);
            this.ButtonClose.Name = "ButtonClose";
            this.ButtonClose.Size = new System.Drawing.Size(75, 21);
            this.ButtonClose.TabIndex = 1;
            this.ButtonClose.Text = "Close";
            this.ButtonClose.UseVisualStyleBackColor = true;
            // 
            // LoadedAdmx
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.ButtonClose;
            this.ClientSize = new System.Drawing.Size(511, 264);
            this.Controls.Add(this.ButtonClose);
            this.Controls.Add(this.LsvAdmx);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoadedAdmx";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Loaded ADMX Files";
            this.SizeChanged += new System.EventHandler(this.LoadedAdmx_SizeChanged);
            this.ResumeLayout(false);

        }

        internal ListView LsvAdmx;
        internal ColumnHeader ChFileTitle;
        internal ColumnHeader ChFolder;
        internal ColumnHeader ChNamespace;
        internal Button ButtonClose;
    }
}