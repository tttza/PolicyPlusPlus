using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class LoadedProducts : Form
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
            System.Windows.Forms.ColumnHeader ColumnHeader1;
            System.Windows.Forms.ColumnHeader ColumnHeader2;
            System.Windows.Forms.ColumnHeader ColumnHeader3;
            System.Windows.Forms.ColumnHeader ColumnHeader4;
            System.Windows.Forms.ColumnHeader ColumnHeader5;
            System.Windows.Forms.ColumnHeader ColumnHeader6;
            System.Windows.Forms.ColumnHeader ColumnHeader7;
            this.LsvTopLevelProducts = new System.Windows.Forms.ListView();
            this.LabelMajorVersion = new System.Windows.Forms.Label();
            this.LsvMajorVersions = new System.Windows.Forms.ListView();
            this.LabelMinorVersion = new System.Windows.Forms.Label();
            this.LsvMinorVersions = new System.Windows.Forms.ListView();
            this.ButtonClose = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            ColumnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ColumnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ColumnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ColumnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ColumnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ColumnHeader6 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ColumnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 8);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(102, 12);
            Label1.TabIndex = 1;
            Label1.Text = "Top-level products";
            // 
            // ColumnHeader1
            // 
            ColumnHeader1.Text = "Name";
            ColumnHeader1.Width = 308;
            // 
            // ColumnHeader2
            // 
            ColumnHeader2.Text = "Children";
            ColumnHeader2.Width = 53;
            // 
            // ColumnHeader3
            // 
            ColumnHeader3.Text = "Name";
            ColumnHeader3.Width = 252;
            // 
            // ColumnHeader4
            // 
            ColumnHeader4.Text = "Version";
            ColumnHeader4.Width = 53;
            // 
            // ColumnHeader5
            // 
            ColumnHeader5.Text = "Name";
            ColumnHeader5.Width = 308;
            // 
            // ColumnHeader6
            // 
            ColumnHeader6.Text = "Version";
            ColumnHeader6.Width = 53;
            // 
            // ColumnHeader7
            // 
            ColumnHeader7.Text = "Children";
            // 
            // LsvTopLevelProducts
            // 
            this.LsvTopLevelProducts.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            ColumnHeader1,
            ColumnHeader2});
            this.LsvTopLevelProducts.FullRowSelect = true;
            this.LsvTopLevelProducts.HideSelection = false;
            this.LsvTopLevelProducts.Location = new System.Drawing.Point(12, 23);
            this.LsvTopLevelProducts.MultiSelect = false;
            this.LsvTopLevelProducts.Name = "LsvTopLevelProducts";
            this.LsvTopLevelProducts.ShowItemToolTips = true;
            this.LsvTopLevelProducts.Size = new System.Drawing.Size(385, 90);
            this.LsvTopLevelProducts.TabIndex = 0;
            this.LsvTopLevelProducts.UseCompatibleStateImageBehavior = false;
            this.LsvTopLevelProducts.View = System.Windows.Forms.View.Details;
            this.LsvTopLevelProducts.SelectedIndexChanged += new System.EventHandler(this.UpdateMajorList);
            this.LsvTopLevelProducts.DoubleClick += new System.EventHandler(this.OpenProductDetails);
            this.LsvTopLevelProducts.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ListKeyPressed);
            // 
            // LabelMajorVersion
            // 
            this.LabelMajorVersion.AutoSize = true;
            this.LabelMajorVersion.Location = new System.Drawing.Point(12, 115);
            this.LabelMajorVersion.Name = "LabelMajorVersion";
            this.LabelMajorVersion.Size = new System.Drawing.Size(80, 12);
            this.LabelMajorVersion.TabIndex = 3;
            this.LabelMajorVersion.Text = "Major versions";
            // 
            // LsvMajorVersions
            // 
            this.LsvMajorVersions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            ColumnHeader3,
            ColumnHeader4,
            ColumnHeader7});
            this.LsvMajorVersions.FullRowSelect = true;
            this.LsvMajorVersions.HideSelection = false;
            this.LsvMajorVersions.Location = new System.Drawing.Point(12, 130);
            this.LsvMajorVersions.MultiSelect = false;
            this.LsvMajorVersions.Name = "LsvMajorVersions";
            this.LsvMajorVersions.ShowItemToolTips = true;
            this.LsvMajorVersions.Size = new System.Drawing.Size(385, 90);
            this.LsvMajorVersions.TabIndex = 1;
            this.LsvMajorVersions.UseCompatibleStateImageBehavior = false;
            this.LsvMajorVersions.View = System.Windows.Forms.View.Details;
            this.LsvMajorVersions.SelectedIndexChanged += new System.EventHandler(this.UpdateMinorList);
            this.LsvMajorVersions.DoubleClick += new System.EventHandler(this.OpenProductDetails);
            this.LsvMajorVersions.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ListKeyPressed);
            // 
            // LabelMinorVersion
            // 
            this.LabelMinorVersion.AutoSize = true;
            this.LabelMinorVersion.Location = new System.Drawing.Point(12, 222);
            this.LabelMinorVersion.Name = "LabelMinorVersion";
            this.LabelMinorVersion.Size = new System.Drawing.Size(80, 12);
            this.LabelMinorVersion.TabIndex = 5;
            this.LabelMinorVersion.Text = "Minor versions";
            // 
            // LsvMinorVersions
            // 
            this.LsvMinorVersions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            ColumnHeader5,
            ColumnHeader6});
            this.LsvMinorVersions.FullRowSelect = true;
            this.LsvMinorVersions.HideSelection = false;
            this.LsvMinorVersions.Location = new System.Drawing.Point(12, 237);
            this.LsvMinorVersions.MultiSelect = false;
            this.LsvMinorVersions.Name = "LsvMinorVersions";
            this.LsvMinorVersions.ShowItemToolTips = true;
            this.LsvMinorVersions.Size = new System.Drawing.Size(385, 90);
            this.LsvMinorVersions.TabIndex = 2;
            this.LsvMinorVersions.UseCompatibleStateImageBehavior = false;
            this.LsvMinorVersions.View = System.Windows.Forms.View.Details;
            this.LsvMinorVersions.DoubleClick += new System.EventHandler(this.OpenProductDetails);
            this.LsvMinorVersions.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ListKeyPressed);
            // 
            // ButtonClose
            // 
            this.ButtonClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonClose.Location = new System.Drawing.Point(322, 332);
            this.ButtonClose.Name = "ButtonClose";
            this.ButtonClose.Size = new System.Drawing.Size(75, 21);
            this.ButtonClose.TabIndex = 3;
            this.ButtonClose.Text = "Close";
            this.ButtonClose.UseVisualStyleBackColor = true;
            // 
            // LoadedProducts
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.ButtonClose;
            this.ClientSize = new System.Drawing.Size(409, 365);
            this.Controls.Add(this.ButtonClose);
            this.Controls.Add(this.LabelMinorVersion);
            this.Controls.Add(this.LsvMinorVersions);
            this.Controls.Add(this.LabelMajorVersion);
            this.Controls.Add(this.LsvMajorVersions);
            this.Controls.Add(Label1);
            this.Controls.Add(this.LsvTopLevelProducts);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoadedProducts";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "All Products";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal ListView LsvTopLevelProducts;
        internal Label LabelMajorVersion;
        internal ListView LsvMajorVersions;
        internal Label LabelMinorVersion;
        internal ListView LsvMinorVersions;
        internal Button ButtonClose;
    }
}