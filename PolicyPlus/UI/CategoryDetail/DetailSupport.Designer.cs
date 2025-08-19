using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus.UI.CategoryDetail
{
    [DesignerGenerated()]
    public partial class DetailSupport : Form
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
            System.Windows.Forms.Label NameLabel;
            System.Windows.Forms.Label IdLabel;
            System.Windows.Forms.Label DefinedLabel;
            System.Windows.Forms.Label DisplayCodeLabel;
            System.Windows.Forms.Label LogicLabel;
            System.Windows.Forms.Label ProductsLabel;
            this.NameTextbox = new System.Windows.Forms.TextBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.DefinedTextbox = new System.Windows.Forms.TextBox();
            this.DisplayCodeTextbox = new System.Windows.Forms.TextBox();
            this.LogicTextbox = new System.Windows.Forms.TextBox();
            this.EntriesListview = new System.Windows.Forms.ListView();
            this.ChName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChMinVer = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChMaxVer = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.CloseButton = new System.Windows.Forms.Button();
            NameLabel = new System.Windows.Forms.Label();
            IdLabel = new System.Windows.Forms.Label();
            DefinedLabel = new System.Windows.Forms.Label();
            DisplayCodeLabel = new System.Windows.Forms.Label();
            LogicLabel = new System.Windows.Forms.Label();
            ProductsLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Location = new System.Drawing.Point(12, 14);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(34, 12);
            NameLabel.TabIndex = 5;
            NameLabel.Text = "Name";
            // 
            // IdLabel
            // 
            IdLabel.AutoSize = true;
            IdLabel.Location = new System.Drawing.Point(12, 38);
            IdLabel.Name = "IdLabel";
            IdLabel.Size = new System.Drawing.Size(55, 12);
            IdLabel.TabIndex = 6;
            IdLabel.Text = "Unique ID";
            // 
            // DefinedLabel
            // 
            DefinedLabel.AutoSize = true;
            DefinedLabel.Location = new System.Drawing.Point(12, 62);
            DefinedLabel.Name = "DefinedLabel";
            DefinedLabel.Size = new System.Drawing.Size(57, 12);
            DefinedLabel.TabIndex = 7;
            DefinedLabel.Text = "Defined in";
            // 
            // DisplayCodeLabel
            // 
            DisplayCodeLabel.AutoSize = true;
            DisplayCodeLabel.Location = new System.Drawing.Point(12, 86);
            DisplayCodeLabel.Name = "DisplayCodeLabel";
            DisplayCodeLabel.Size = new System.Drawing.Size(71, 12);
            DisplayCodeLabel.TabIndex = 8;
            DisplayCodeLabel.Text = "Display code";
            // 
            // LogicLabel
            // 
            LogicLabel.AutoSize = true;
            LogicLabel.Location = new System.Drawing.Point(12, 110);
            LogicLabel.Name = "LogicLabel";
            LogicLabel.Size = new System.Drawing.Size(68, 12);
            LogicLabel.TabIndex = 9;
            LogicLabel.Text = "Composition";
            // 
            // ProductsLabel
            // 
            ProductsLabel.AutoSize = true;
            ProductsLabel.Location = new System.Drawing.Point(12, 134);
            ProductsLabel.Name = "ProductsLabel";
            ProductsLabel.Size = new System.Drawing.Size(50, 12);
            ProductsLabel.TabIndex = 11;
            ProductsLabel.Text = "Products";
            // 
            // NameTextbox
            // 
            this.NameTextbox.Location = new System.Drawing.Point(86, 11);
            this.NameTextbox.Name = "NameTextbox";
            this.NameTextbox.ReadOnly = true;
            this.NameTextbox.Size = new System.Drawing.Size(268, 19);
            this.NameTextbox.TabIndex = 0;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(86, 35);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.ReadOnly = true;
            this.IdTextbox.Size = new System.Drawing.Size(268, 19);
            this.IdTextbox.TabIndex = 1;
            // 
            // DefinedTextbox
            // 
            this.DefinedTextbox.Location = new System.Drawing.Point(86, 59);
            this.DefinedTextbox.Name = "DefinedTextbox";
            this.DefinedTextbox.ReadOnly = true;
            this.DefinedTextbox.Size = new System.Drawing.Size(268, 19);
            this.DefinedTextbox.TabIndex = 2;
            // 
            // DisplayCodeTextbox
            // 
            this.DisplayCodeTextbox.Location = new System.Drawing.Point(86, 83);
            this.DisplayCodeTextbox.Name = "DisplayCodeTextbox";
            this.DisplayCodeTextbox.ReadOnly = true;
            this.DisplayCodeTextbox.Size = new System.Drawing.Size(268, 19);
            this.DisplayCodeTextbox.TabIndex = 3;
            // 
            // LogicTextbox
            // 
            this.LogicTextbox.Location = new System.Drawing.Point(86, 107);
            this.LogicTextbox.Name = "LogicTextbox";
            this.LogicTextbox.ReadOnly = true;
            this.LogicTextbox.Size = new System.Drawing.Size(268, 19);
            this.LogicTextbox.TabIndex = 4;
            // 
            // EntriesListview
            // 
            this.EntriesListview.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChName,
            this.ChMinVer,
            this.ChMaxVer});
            this.EntriesListview.FullRowSelect = true;
            this.EntriesListview.HideSelection = false;
            this.EntriesListview.Location = new System.Drawing.Point(86, 131);
            this.EntriesListview.MultiSelect = false;
            this.EntriesListview.Name = "EntriesListview";
            this.EntriesListview.ShowItemToolTips = true;
            this.EntriesListview.Size = new System.Drawing.Size(268, 81);
            this.EntriesListview.TabIndex = 12;
            this.EntriesListview.UseCompatibleStateImageBehavior = false;
            this.EntriesListview.View = System.Windows.Forms.View.Details;
            this.EntriesListview.ClientSizeChanged += new System.EventHandler(this.EntriesListview_ClientSizeChanged);
            this.EntriesListview.DoubleClick += new System.EventHandler(this.EntriesListview_DoubleClick);
            // 
            // ChName
            // 
            this.ChName.Text = "Name";
            this.ChName.Width = 158;
            // 
            // ChMinVer
            // 
            this.ChMinVer.Text = "Min";
            this.ChMinVer.Width = 40;
            // 
            // ChMaxVer
            // 
            this.ChMaxVer.Text = "Max";
            this.ChMaxVer.Width = 40;
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseButton.Location = new System.Drawing.Point(279, 217);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 21);
            this.CloseButton.TabIndex = 13;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // DetailSupport
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(366, 249);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.EntriesListview);
            this.Controls.Add(ProductsLabel);
            this.Controls.Add(LogicLabel);
            this.Controls.Add(DisplayCodeLabel);
            this.Controls.Add(DefinedLabel);
            this.Controls.Add(IdLabel);
            this.Controls.Add(NameLabel);
            this.Controls.Add(this.LogicTextbox);
            this.Controls.Add(this.DisplayCodeTextbox);
            this.Controls.Add(this.DefinedTextbox);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(this.NameTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailSupport";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Support Details";
            this.Shown += new System.EventHandler(this.EntriesListview_ClientSizeChanged);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox NameTextbox;
        internal TextBox IdTextbox;
        internal TextBox DefinedTextbox;
        internal TextBox DisplayCodeTextbox;
        internal TextBox LogicTextbox;
        internal ListView EntriesListview;
        internal ColumnHeader ChName;
        internal ColumnHeader ChMinVer;
        internal ColumnHeader ChMaxVer;
        internal Button CloseButton;
    }
}