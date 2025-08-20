using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class LoadedSupportDefinitions : Form
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
            this.LsvSupport = new System.Windows.Forms.ListView();
            this.ChName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChDefinedIn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ButtonClose = new System.Windows.Forms.Button();
            this.TextFilter = new System.Windows.Forms.TextBox();
            Label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(89, 12);
            Label1.TabIndex = 3;
            Label1.Text = "Substring (filter)";
            // 
            // LsvSupport
            // 
            this.LsvSupport.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChName,
            this.ChDefinedIn});
            this.LsvSupport.FullRowSelect = true;
            this.LsvSupport.HideSelection = false;
            this.LsvSupport.Location = new System.Drawing.Point(12, 35);
            this.LsvSupport.MultiSelect = false;
            this.LsvSupport.Name = "LsvSupport";
            this.LsvSupport.ShowItemToolTips = true;
            this.LsvSupport.Size = new System.Drawing.Size(435, 176);
            this.LsvSupport.TabIndex = 2;
            this.LsvSupport.UseCompatibleStateImageBehavior = false;
            this.LsvSupport.View = System.Windows.Forms.View.Details;
            this.LsvSupport.DoubleClick += new System.EventHandler(this.LsvSupport_DoubleClick);
            this.LsvSupport.KeyDown += new System.Windows.Forms.KeyEventHandler(this.LsvSupport_KeyDown);
            // 
            // ChName
            // 
            this.ChName.Text = "Name";
            this.ChName.Width = 317;
            // 
            // ChDefinedIn
            // 
            this.ChDefinedIn.Text = "ADMX File";
            this.ChDefinedIn.Width = 97;
            // 
            // ButtonClose
            // 
            this.ButtonClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonClose.Location = new System.Drawing.Point(372, 216);
            this.ButtonClose.Name = "ButtonClose";
            this.ButtonClose.Size = new System.Drawing.Size(75, 21);
            this.ButtonClose.TabIndex = 3;
            this.ButtonClose.Text = "Close";
            this.ButtonClose.UseVisualStyleBackColor = true;
            // 
            // TextFilter
            // 
            this.TextFilter.Location = new System.Drawing.Point(97, 11);
            this.TextFilter.Name = "TextFilter";
            this.TextFilter.Size = new System.Drawing.Size(350, 19);
            this.TextFilter.TabIndex = 1;
            this.TextFilter.TextChanged += new System.EventHandler(this.TextFilter_TextChanged);
            // 
            // LoadedSupportDefinitions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.ButtonClose;
            this.ClientSize = new System.Drawing.Size(459, 248);
            this.Controls.Add(Label1);
            this.Controls.Add(this.TextFilter);
            this.Controls.Add(this.ButtonClose);
            this.Controls.Add(this.LsvSupport);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoadedSupportDefinitions";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "All Support Definitions";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal ListView LsvSupport;
        internal ColumnHeader ChName;
        internal ColumnHeader ChDefinedIn;
        internal Button ButtonClose;
        internal TextBox TextFilter;
    }
}