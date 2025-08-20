using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.Admx
{
    public partial class DownloadAdmx : Form
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
            System.Windows.Forms.Label LabelWhatsThis;
            System.Windows.Forms.Label LabelDestFolder;
            this.TextDestFolder = new System.Windows.Forms.TextBox();
            this.ButtonBrowse = new System.Windows.Forms.Button();
            this.ProgressSpinner = new System.Windows.Forms.ProgressBar();
            this.LabelProgress = new System.Windows.Forms.Label();
            this.ButtonStart = new System.Windows.Forms.Button();
            this.ButtonClose = new System.Windows.Forms.Button();
            LabelWhatsThis = new System.Windows.Forms.Label();
            LabelDestFolder = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LabelWhatsThis
            // 
            LabelWhatsThis.AutoSize = true;
            LabelWhatsThis.Location = new System.Drawing.Point(12, 8);
            LabelWhatsThis.Name = "LabelWhatsThis";
            LabelWhatsThis.Size = new System.Drawing.Size(370, 12);
            LabelWhatsThis.TabIndex = 0;
            LabelWhatsThis.Text = "Download the full set of policy definitions (ADMX files) from Microsoft.";
            // 
            // LabelDestFolder
            // 
            LabelDestFolder.AutoSize = true;
            LabelDestFolder.Location = new System.Drawing.Point(12, 28);
            LabelDestFolder.Name = "LabelDestFolder";
            LabelDestFolder.Size = new System.Drawing.Size(96, 12);
            LabelDestFolder.TabIndex = 3;
            LabelDestFolder.Text = "Destination folder";
            // 
            // TextDestFolder
            // 
            this.TextDestFolder.Location = new System.Drawing.Point(107, 25);
            this.TextDestFolder.Name = "TextDestFolder";
            this.TextDestFolder.Size = new System.Drawing.Size(184, 19);
            this.TextDestFolder.TabIndex = 1;
            // 
            // ButtonBrowse
            // 
            this.ButtonBrowse.Location = new System.Drawing.Point(297, 23);
            this.ButtonBrowse.Name = "ButtonBrowse";
            this.ButtonBrowse.Size = new System.Drawing.Size(75, 21);
            this.ButtonBrowse.TabIndex = 2;
            this.ButtonBrowse.Text = "Browse";
            this.ButtonBrowse.UseVisualStyleBackColor = true;
            this.ButtonBrowse.Click += new System.EventHandler(this.ButtonBrowse_Click);
            // 
            // ProgressSpinner
            // 
            this.ProgressSpinner.Location = new System.Drawing.Point(12, 49);
            this.ProgressSpinner.Name = "ProgressSpinner";
            this.ProgressSpinner.Size = new System.Drawing.Size(360, 21);
            this.ProgressSpinner.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.ProgressSpinner.TabIndex = 4;
            // 
            // LabelProgress
            // 
            this.LabelProgress.AutoSize = true;
            this.LabelProgress.Location = new System.Drawing.Point(12, 80);
            this.LabelProgress.Name = "LabelProgress";
            this.LabelProgress.Size = new System.Drawing.Size(50, 12);
            this.LabelProgress.TabIndex = 5;
            this.LabelProgress.Text = "Progress";
            this.LabelProgress.Visible = false;
            // 
            // ButtonStart
            // 
            this.ButtonStart.Location = new System.Drawing.Point(297, 76);
            this.ButtonStart.Name = "ButtonStart";
            this.ButtonStart.Size = new System.Drawing.Size(75, 21);
            this.ButtonStart.TabIndex = 6;
            this.ButtonStart.Text = "Begin";
            this.ButtonStart.UseVisualStyleBackColor = true;
            this.ButtonStart.Click += new System.EventHandler(this.ButtonStart_Click);
            // 
            // ButtonClose
            // 
            this.ButtonClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.ButtonClose.Location = new System.Drawing.Point(216, 76);
            this.ButtonClose.Name = "ButtonClose";
            this.ButtonClose.Size = new System.Drawing.Size(75, 21);
            this.ButtonClose.TabIndex = 7;
            this.ButtonClose.Text = "Close";
            this.ButtonClose.UseVisualStyleBackColor = true;
            // 
            // DownloadAdmx
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(384, 108);
            this.Controls.Add(this.ButtonClose);
            this.Controls.Add(this.ButtonStart);
            this.Controls.Add(this.LabelProgress);
            this.Controls.Add(this.ProgressSpinner);
            this.Controls.Add(LabelDestFolder);
            this.Controls.Add(this.ButtonBrowse);
            this.Controls.Add(this.TextDestFolder);
            this.Controls.Add(LabelWhatsThis);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DownloadAdmx";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Acquire ADMX Files";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.DownloadAdmx_Closing);
            this.Shown += new System.EventHandler(this.DownloadAdmx_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextDestFolder;
        internal Button ButtonBrowse;
        internal ProgressBar ProgressSpinner;
        internal Label LabelProgress;
        internal Button ButtonStart;
        internal Button ButtonClose;
    }
}