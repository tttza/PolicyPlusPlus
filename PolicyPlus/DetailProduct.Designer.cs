using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class DetailProduct : Form
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
            System.Windows.Forms.Label KindLabel;
            System.Windows.Forms.Label VersionLabel;
            System.Windows.Forms.Label ParentLabel;
            System.Windows.Forms.Label ChildrenLabel;
            System.Windows.Forms.Label DisplayCodeLabel;
            this.NameTextbox = new System.Windows.Forms.TextBox();
            this.IdTextbox = new System.Windows.Forms.TextBox();
            this.DefinedTextbox = new System.Windows.Forms.TextBox();
            this.DisplayCodeTextbox = new System.Windows.Forms.TextBox();
            this.KindTextbox = new System.Windows.Forms.TextBox();
            this.ParentButton = new System.Windows.Forms.Button();
            this.ParentTextbox = new System.Windows.Forms.TextBox();
            this.ChildrenListview = new System.Windows.Forms.ListView();
            this.ChVersion = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.CloseButton = new System.Windows.Forms.Button();
            this.VersionTextbox = new System.Windows.Forms.TextBox();
            NameLabel = new System.Windows.Forms.Label();
            IdLabel = new System.Windows.Forms.Label();
            DefinedLabel = new System.Windows.Forms.Label();
            KindLabel = new System.Windows.Forms.Label();
            VersionLabel = new System.Windows.Forms.Label();
            ParentLabel = new System.Windows.Forms.Label();
            ChildrenLabel = new System.Windows.Forms.Label();
            DisplayCodeLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // NameLabel
            // 
            NameLabel.AutoSize = true;
            NameLabel.Location = new System.Drawing.Point(12, 14);
            NameLabel.Name = "NameLabel";
            NameLabel.Size = new System.Drawing.Size(34, 12);
            NameLabel.TabIndex = 1;
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
            // KindLabel
            // 
            KindLabel.AutoSize = true;
            KindLabel.Location = new System.Drawing.Point(12, 110);
            KindLabel.Name = "KindLabel";
            KindLabel.Size = new System.Drawing.Size(27, 12);
            KindLabel.TabIndex = 8;
            KindLabel.Text = "Kind";
            // 
            // VersionLabel
            // 
            VersionLabel.AutoSize = true;
            VersionLabel.Location = new System.Drawing.Point(12, 134);
            VersionLabel.Name = "VersionLabel";
            VersionLabel.Size = new System.Drawing.Size(85, 12);
            VersionLabel.TabIndex = 9;
            VersionLabel.Text = "Version number";
            // 
            // ParentLabel
            // 
            ParentLabel.AutoSize = true;
            ParentLabel.Location = new System.Drawing.Point(12, 158);
            ParentLabel.Name = "ParentLabel";
            ParentLabel.Size = new System.Drawing.Size(38, 12);
            ParentLabel.TabIndex = 12;
            ParentLabel.Text = "Parent";
            // 
            // ChildrenLabel
            // 
            ChildrenLabel.AutoSize = true;
            ChildrenLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            ChildrenLabel.Location = new System.Drawing.Point(12, 182);
            ChildrenLabel.Name = "ChildrenLabel";
            ChildrenLabel.Size = new System.Drawing.Size(68, 12);
            ChildrenLabel.TabIndex = 14;
            ChildrenLabel.Text = "Subproducts";
            // 
            // DisplayCodeLabel
            // 
            DisplayCodeLabel.AutoSize = true;
            DisplayCodeLabel.Location = new System.Drawing.Point(12, 86);
            DisplayCodeLabel.Name = "DisplayCodeLabel";
            DisplayCodeLabel.Size = new System.Drawing.Size(71, 12);
            DisplayCodeLabel.TabIndex = 16;
            DisplayCodeLabel.Text = "Display code";
            // 
            // NameTextbox
            // 
            this.NameTextbox.Location = new System.Drawing.Point(98, 11);
            this.NameTextbox.Name = "NameTextbox";
            this.NameTextbox.ReadOnly = true;
            this.NameTextbox.Size = new System.Drawing.Size(256, 19);
            this.NameTextbox.TabIndex = 0;
            // 
            // IdTextbox
            // 
            this.IdTextbox.Location = new System.Drawing.Point(98, 35);
            this.IdTextbox.Name = "IdTextbox";
            this.IdTextbox.ReadOnly = true;
            this.IdTextbox.Size = new System.Drawing.Size(256, 19);
            this.IdTextbox.TabIndex = 2;
            // 
            // DefinedTextbox
            // 
            this.DefinedTextbox.Location = new System.Drawing.Point(98, 59);
            this.DefinedTextbox.Name = "DefinedTextbox";
            this.DefinedTextbox.ReadOnly = true;
            this.DefinedTextbox.Size = new System.Drawing.Size(256, 19);
            this.DefinedTextbox.TabIndex = 3;
            // 
            // DisplayCodeTextbox
            // 
            this.DisplayCodeTextbox.Location = new System.Drawing.Point(98, 83);
            this.DisplayCodeTextbox.Name = "DisplayCodeTextbox";
            this.DisplayCodeTextbox.ReadOnly = true;
            this.DisplayCodeTextbox.Size = new System.Drawing.Size(256, 19);
            this.DisplayCodeTextbox.TabIndex = 4;
            // 
            // KindTextbox
            // 
            this.KindTextbox.Location = new System.Drawing.Point(98, 107);
            this.KindTextbox.Name = "KindTextbox";
            this.KindTextbox.ReadOnly = true;
            this.KindTextbox.Size = new System.Drawing.Size(256, 19);
            this.KindTextbox.TabIndex = 5;
            // 
            // ParentButton
            // 
            this.ParentButton.Location = new System.Drawing.Point(279, 153);
            this.ParentButton.Name = "ParentButton";
            this.ParentButton.Size = new System.Drawing.Size(75, 21);
            this.ParentButton.TabIndex = 10;
            this.ParentButton.Text = "Details";
            this.ParentButton.UseVisualStyleBackColor = true;
            this.ParentButton.Click += new System.EventHandler(this.ParentButton_Click);
            // 
            // ParentTextbox
            // 
            this.ParentTextbox.Location = new System.Drawing.Point(98, 155);
            this.ParentTextbox.Name = "ParentTextbox";
            this.ParentTextbox.ReadOnly = true;
            this.ParentTextbox.Size = new System.Drawing.Size(175, 19);
            this.ParentTextbox.TabIndex = 7;
            // 
            // ChildrenListview
            // 
            this.ChildrenListview.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChVersion,
            this.ChName});
            this.ChildrenListview.FullRowSelect = true;
            this.ChildrenListview.HideSelection = false;
            this.ChildrenListview.Location = new System.Drawing.Point(98, 179);
            this.ChildrenListview.MultiSelect = false;
            this.ChildrenListview.Name = "ChildrenListview";
            this.ChildrenListview.Size = new System.Drawing.Size(256, 102);
            this.ChildrenListview.TabIndex = 13;
            this.ChildrenListview.UseCompatibleStateImageBehavior = false;
            this.ChildrenListview.View = System.Windows.Forms.View.Details;
            this.ChildrenListview.ClientSizeChanged += new System.EventHandler(this.ChildrenListview_ClientSizeChanged);
            this.ChildrenListview.DoubleClick += new System.EventHandler(this.ChildrenListview_DoubleClick);
            // 
            // ChVersion
            // 
            this.ChVersion.Text = "Version";
            this.ChVersion.Width = 51;
            // 
            // ChName
            // 
            this.ChName.Text = "Name";
            this.ChName.Width = 176;
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseButton.Location = new System.Drawing.Point(279, 286);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 21);
            this.CloseButton.TabIndex = 15;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            // 
            // VersionTextbox
            // 
            this.VersionTextbox.Location = new System.Drawing.Point(98, 131);
            this.VersionTextbox.Name = "VersionTextbox";
            this.VersionTextbox.ReadOnly = true;
            this.VersionTextbox.Size = new System.Drawing.Size(256, 19);
            this.VersionTextbox.TabIndex = 6;
            // 
            // DetailProduct
            // 
            this.AcceptButton = this.CloseButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(366, 318);
            this.Controls.Add(this.VersionTextbox);
            this.Controls.Add(DisplayCodeLabel);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(ChildrenLabel);
            this.Controls.Add(this.ChildrenListview);
            this.Controls.Add(ParentLabel);
            this.Controls.Add(this.ParentTextbox);
            this.Controls.Add(this.ParentButton);
            this.Controls.Add(VersionLabel);
            this.Controls.Add(KindLabel);
            this.Controls.Add(DefinedLabel);
            this.Controls.Add(IdLabel);
            this.Controls.Add(this.KindTextbox);
            this.Controls.Add(this.DisplayCodeTextbox);
            this.Controls.Add(this.DefinedTextbox);
            this.Controls.Add(this.IdTextbox);
            this.Controls.Add(NameLabel);
            this.Controls.Add(this.NameTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailProduct";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Product Details";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox NameTextbox;
        internal TextBox IdTextbox;
        internal TextBox DefinedTextbox;
        internal TextBox DisplayCodeTextbox;
        internal TextBox KindTextbox;
        internal Button ParentButton;
        internal TextBox ParentTextbox;
        internal ListView ChildrenListview;
        internal ColumnHeader ChVersion;
        internal ColumnHeader ChName;
        internal Button CloseButton;
        internal TextBox VersionTextbox;
    }
}