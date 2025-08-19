using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus.UI.Find
{
    [DesignerGenerated()]
    public partial class FindById : Form
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
            this.StatusImage = new System.Windows.Forms.PictureBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.GoButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.StatusImage)).BeginInit();
            this.SuspendLayout();
            // 
            // StatusImage
            // 
            this.StatusImage.Location = new System.Drawing.Point(12, 13);
            this.StatusImage.Name = "StatusImage";
            this.StatusImage.Size = new System.Drawing.Size(16, 15);
            this.StatusImage.TabIndex = 0;
            this.StatusImage.TabStop = false;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(34, 11);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.Size = new System.Drawing.Size(277, 19);
            this.IdTextbox.TabIndex = 1;
            this.IdTextbox.Text = " ";
            this.IdTextbox.TextChanged += new System.EventHandler(this.IdTextbox_TextChanged);
            // 
            // GoButton
            // 
            this.GoButton.Location = new System.Drawing.Point(236, 35);
            this.GoButton.Name = "GoButton";
            this.GoButton.Size = new System.Drawing.Size(75, 21);
            this.GoButton.TabIndex = 2;
            this.GoButton.Text = "Go";
            this.GoButton.UseVisualStyleBackColor = true;
            this.GoButton.Click += new System.EventHandler(this.GoButton_Click);
            // 
            // FindById
            // 
            this.AcceptButton = this.GoButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(323, 67);
            this.Controls.Add(this.GoButton);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(this.StatusImage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindById";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find by ID";
            this.Load += new System.EventHandler(this.FindById_Load);
            this.Shown += new System.EventHandler(this.FindById_Shown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FindById_KeyUp);
            ((System.ComponentModel.ISupportInitialize)(this.StatusImage)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal PictureBox StatusImage;
        internal TextBox IdTextbox;
        internal Button GoButton;
    }
}