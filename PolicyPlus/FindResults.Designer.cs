using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class FindResults : Form
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
            this.SearchProgress = new System.Windows.Forms.ProgressBar();
            this.ResultsListview = new System.Windows.Forms.ListView();
            this.ChTitle = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChCategory = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ProgressLabel = new System.Windows.Forms.Label();
            this.CloseButton = new System.Windows.Forms.Button();
            this.GoButton = new System.Windows.Forms.Button();
            this.StopButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // SearchProgress
            // 
            this.SearchProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SearchProgress.Location = new System.Drawing.Point(16, 29);
            this.SearchProgress.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.SearchProgress.Name = "SearchProgress";
            this.SearchProgress.Size = new System.Drawing.Size(633, 27);
            this.SearchProgress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.SearchProgress.TabIndex = 0;
            // 
            // ResultsListview
            // 
            this.ResultsListview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ResultsListview.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChTitle,
            this.ChCategory});
            this.ResultsListview.FullRowSelect = true;
            this.ResultsListview.HideSelection = false;
            this.ResultsListview.Location = new System.Drawing.Point(16, 62);
            this.ResultsListview.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.ResultsListview.MultiSelect = false;
            this.ResultsListview.Name = "ResultsListview";
            this.ResultsListview.ShowItemToolTips = true;
            this.ResultsListview.Size = new System.Drawing.Size(707, 295);
            this.ResultsListview.TabIndex = 1;
            this.ResultsListview.UseCompatibleStateImageBehavior = false;
            this.ResultsListview.View = System.Windows.Forms.View.Details;
            this.ResultsListview.SizeChanged += new System.EventHandler(this.ResultsListview_SizeChanged);
            this.ResultsListview.DoubleClick += new System.EventHandler(this.GoClicked);
            // 
            // ChTitle
            // 
            this.ChTitle.Text = "Title";
            this.ChTitle.Width = 435;
            // 
            // ChCategory
            // 
            this.ChCategory.Text = "Category";
            this.ChCategory.Width = 259;
            // 
            // ProgressLabel
            // 
            this.ProgressLabel.AutoSize = true;
            this.ProgressLabel.Location = new System.Drawing.Point(16, 10);
            this.ProgressLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.ProgressLabel.Name = "ProgressLabel";
            this.ProgressLabel.Size = new System.Drawing.Size(146, 15);
            this.ProgressLabel.TabIndex = 2;
            this.ProgressLabel.Text = "Results: 0 (searching)";
            // 
            // CloseButton
            // 
            this.CloseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CloseButton.Location = new System.Drawing.Point(624, 364);
            this.CloseButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(100, 27);
            this.CloseButton.TabIndex = 4;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // GoButton
            // 
            this.GoButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.GoButton.Location = new System.Drawing.Point(516, 364);
            this.GoButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.GoButton.Name = "GoButton";
            this.GoButton.Size = new System.Drawing.Size(100, 27);
            this.GoButton.TabIndex = 3;
            this.GoButton.Text = "Go";
            this.GoButton.UseVisualStyleBackColor = true;
            this.GoButton.Click += new System.EventHandler(this.GoClicked);
            // 
            // StopButton
            // 
            this.StopButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.StopButton.Location = new System.Drawing.Point(657, 29);
            this.StopButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(67, 27);
            this.StopButton.TabIndex = 0;
            this.StopButton.Text = "Stop";
            this.StopButton.UseVisualStyleBackColor = true;
            this.StopButton.Click += new System.EventHandler(this.StopButton_Click);
            // 
            // FindResults
            // 
            this.AcceptButton = this.GoButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(740, 405);
            this.Controls.Add(this.StopButton);
            this.Controls.Add(this.GoButton);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.ProgressLabel);
            this.Controls.Add(this.ResultsListview);
            this.Controls.Add(this.SearchProgress);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindResults";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Search Results";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.FindResults_Closing);
            this.Load += new System.EventHandler(this.FindResults_Load);
            this.Shown += new System.EventHandler(this.FindResults_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal ProgressBar SearchProgress;
        internal ListView ResultsListview;
        internal ColumnHeader ChTitle;
        internal ColumnHeader ChCategory;
        internal Label ProgressLabel;
        internal Button CloseButton;
        internal Button GoButton;
        internal Button StopButton;
    }
}