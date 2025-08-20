using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.Admx
{
    public partial class DetailAdmx : Form
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
            System.Windows.Forms.Label Label2;
            System.Windows.Forms.Label Label3;
            System.Windows.Forms.Label Label4;
            System.Windows.Forms.Label Label5;
            System.Windows.Forms.Label Label6;
            System.Windows.Forms.Label Label7;
            this.TextPath = new System.Windows.Forms.TextBox();
            this.TextNamespace = new System.Windows.Forms.TextBox();
            this.TextSupersededAdm = new System.Windows.Forms.TextBox();
            this.LsvPolicies = new System.Windows.Forms.ListView();
            this.ChPolicyId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.LsvCategories = new System.Windows.Forms.ListView();
            this.ChCategoryId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChCategoryName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.LsvProducts = new System.Windows.Forms.ListView();
            this.ChProductId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChProductName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.LsvSupportDefinitions = new System.Windows.Forms.ListView();
            this.ChSupportId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChSupportName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ButtonClose = new System.Windows.Forms.Button();
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            Label3 = new System.Windows.Forms.Label();
            Label4 = new System.Windows.Forms.Label();
            Label5 = new System.Windows.Forms.Label();
            Label6 = new System.Windows.Forms.Label();
            Label7 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(28, 12);
            Label1.TabIndex = 1;
            Label1.Text = "Path";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 38);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(64, 12);
            Label2.TabIndex = 3;
            Label2.Text = "Namespace";
            // 
            // Label3
            // 
            Label3.AutoSize = true;
            Label3.Location = new System.Drawing.Point(12, 62);
            Label3.Name = "Label3";
            Label3.Size = new System.Drawing.Size(93, 12);
            Label3.TabIndex = 5;
            Label3.Text = "Superseded ADM";
            // 
            // Label4
            // 
            Label4.AutoSize = true;
            Label4.Location = new System.Drawing.Point(12, 86);
            Label4.Name = "Label4";
            Label4.Size = new System.Drawing.Size(45, 12);
            Label4.TabIndex = 7;
            Label4.Text = "Policies";
            // 
            // Label5
            // 
            Label5.AutoSize = true;
            Label5.Location = new System.Drawing.Point(12, 181);
            Label5.Name = "Label5";
            Label5.Size = new System.Drawing.Size(60, 12);
            Label5.TabIndex = 9;
            Label5.Text = "Categories";
            // 
            // Label6
            // 
            Label6.AutoSize = true;
            Label6.Location = new System.Drawing.Point(12, 276);
            Label6.Name = "Label6";
            Label6.Size = new System.Drawing.Size(50, 12);
            Label6.TabIndex = 11;
            Label6.Text = "Products";
            // 
            // Label7
            // 
            Label7.AutoSize = true;
            Label7.Location = new System.Drawing.Point(12, 371);
            Label7.Name = "Label7";
            Label7.Size = new System.Drawing.Size(73, 12);
            Label7.TabIndex = 13;
            Label7.Text = "Support rules";
            // 
            // TextPath
            // 
            this.TextPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TextPath.Location = new System.Drawing.Point(109, 11);
            this.TextPath.Name = "TextPath";
            this.TextPath.ReadOnly = true;
            this.TextPath.Size = new System.Drawing.Size(332, 19);
            this.TextPath.TabIndex = 0;
            // 
            // TextNamespace
            // 
            this.TextNamespace.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TextNamespace.Location = new System.Drawing.Point(109, 35);
            this.TextNamespace.Name = "TextNamespace";
            this.TextNamespace.ReadOnly = true;
            this.TextNamespace.Size = new System.Drawing.Size(332, 19);
            this.TextNamespace.TabIndex = 2;
            // 
            // TextSupersededAdm
            // 
            this.TextSupersededAdm.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TextSupersededAdm.Location = new System.Drawing.Point(109, 59);
            this.TextSupersededAdm.Name = "TextSupersededAdm";
            this.TextSupersededAdm.ReadOnly = true;
            this.TextSupersededAdm.Size = new System.Drawing.Size(332, 19);
            this.TextSupersededAdm.TabIndex = 4;
            // 
            // LsvPolicies
            // 
            this.LsvPolicies.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LsvPolicies.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChPolicyId,
            this.ChName});
            this.LsvPolicies.FullRowSelect = true;
            this.LsvPolicies.HideSelection = false;
            this.LsvPolicies.Location = new System.Drawing.Point(109, 83);
            this.LsvPolicies.MultiSelect = false;
            this.LsvPolicies.Name = "LsvPolicies";
            this.LsvPolicies.ShowItemToolTips = true;
            this.LsvPolicies.Size = new System.Drawing.Size(332, 90);
            this.LsvPolicies.TabIndex = 6;
            this.LsvPolicies.UseCompatibleStateImageBehavior = false;
            this.LsvPolicies.View = System.Windows.Forms.View.Details;
            this.LsvPolicies.DoubleClick += new System.EventHandler(this.LsvPolicies_DoubleClick);
            // 
            // ChPolicyId
            // 
            this.ChPolicyId.Text = "Local ID";
            this.ChPolicyId.Width = 73;
            // 
            // ChName
            // 
            this.ChName.Text = "Name";
            this.ChName.Width = 166;
            // 
            // LsvCategories
            // 
            this.LsvCategories.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LsvCategories.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChCategoryId,
            this.ChCategoryName});
            this.LsvCategories.FullRowSelect = true;
            this.LsvCategories.HideSelection = false;
            this.LsvCategories.Location = new System.Drawing.Point(109, 178);
            this.LsvCategories.MultiSelect = false;
            this.LsvCategories.Name = "LsvCategories";
            this.LsvCategories.ShowItemToolTips = true;
            this.LsvCategories.Size = new System.Drawing.Size(332, 90);
            this.LsvCategories.TabIndex = 8;
            this.LsvCategories.UseCompatibleStateImageBehavior = false;
            this.LsvCategories.View = System.Windows.Forms.View.Details;
            this.LsvCategories.DoubleClick += new System.EventHandler(this.LsvCategories_DoubleClick);
            // 
            // ChCategoryId
            // 
            this.ChCategoryId.Text = "Local ID";
            this.ChCategoryId.Width = 73;
            // 
            // ChCategoryName
            // 
            this.ChCategoryName.Text = "Name";
            this.ChCategoryName.Width = 166;
            // 
            // LsvProducts
            // 
            this.LsvProducts.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LsvProducts.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChProductId,
            this.ChProductName});
            this.LsvProducts.FullRowSelect = true;
            this.LsvProducts.HideSelection = false;
            this.LsvProducts.Location = new System.Drawing.Point(109, 273);
            this.LsvProducts.MultiSelect = false;
            this.LsvProducts.Name = "LsvProducts";
            this.LsvProducts.ShowItemToolTips = true;
            this.LsvProducts.Size = new System.Drawing.Size(332, 90);
            this.LsvProducts.TabIndex = 10;
            this.LsvProducts.UseCompatibleStateImageBehavior = false;
            this.LsvProducts.View = System.Windows.Forms.View.Details;
            this.LsvProducts.DoubleClick += new System.EventHandler(this.LsvProducts_DoubleClick);
            // 
            // ChProductId
            // 
            this.ChProductId.Text = "Local ID";
            this.ChProductId.Width = 73;
            // 
            // ChProductName
            // 
            this.ChProductName.Text = "Name";
            this.ChProductName.Width = 166;
            // 
            // LsvSupportDefinitions
            // 
            this.LsvSupportDefinitions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.LsvSupportDefinitions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChSupportId,
            this.ChSupportName});
            this.LsvSupportDefinitions.FullRowSelect = true;
            this.LsvSupportDefinitions.HideSelection = false;
            this.LsvSupportDefinitions.Location = new System.Drawing.Point(109, 368);
            this.LsvSupportDefinitions.MultiSelect = false;
            this.LsvSupportDefinitions.Name = "LsvSupportDefinitions";
            this.LsvSupportDefinitions.ShowItemToolTips = true;
            this.LsvSupportDefinitions.Size = new System.Drawing.Size(332, 90);
            this.LsvSupportDefinitions.TabIndex = 12;
            this.LsvSupportDefinitions.UseCompatibleStateImageBehavior = false;
            this.LsvSupportDefinitions.View = System.Windows.Forms.View.Details;
            this.LsvSupportDefinitions.DoubleClick += new System.EventHandler(this.LsvSupportDefinitions_DoubleClick);
            // 
            // ChSupportId
            // 
            this.ChSupportId.Text = "Local ID";
            this.ChSupportId.Width = 73;
            // 
            // ChSupportName
            // 
            this.ChSupportName.Text = "Name";
            this.ChSupportName.Width = 166;
            // 
            // ButtonClose
            // 
            this.ButtonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ButtonClose.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ButtonClose.Location = new System.Drawing.Point(366, 463);
            this.ButtonClose.Name = "ButtonClose";
            this.ButtonClose.Size = new System.Drawing.Size(75, 21);
            this.ButtonClose.TabIndex = 14;
            this.ButtonClose.Text = "Close";
            this.ButtonClose.UseVisualStyleBackColor = true;
            // 
            // DetailAdmx
            // 
            this.AcceptButton = this.ButtonClose;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.ButtonClose;
            this.ClientSize = new System.Drawing.Size(453, 496);
            this.Controls.Add(this.ButtonClose);
            this.Controls.Add(Label7);
            this.Controls.Add(this.LsvSupportDefinitions);
            this.Controls.Add(Label6);
            this.Controls.Add(this.LsvProducts);
            this.Controls.Add(Label5);
            this.Controls.Add(this.LsvCategories);
            this.Controls.Add(Label4);
            this.Controls.Add(this.LsvPolicies);
            this.Controls.Add(Label3);
            this.Controls.Add(this.TextSupersededAdm);
            this.Controls.Add(Label2);
            this.Controls.Add(this.TextNamespace);
            this.Controls.Add(Label1);
            this.Controls.Add(this.TextPath);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DetailAdmx";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ADMX Details";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal TextBox TextPath;
        internal TextBox TextNamespace;
        internal TextBox TextSupersededAdm;
        internal ListView LsvPolicies;
        internal ColumnHeader ChPolicyId;
        internal ColumnHeader ChName;
        internal ListView LsvCategories;
        internal ColumnHeader ChCategoryId;
        internal ColumnHeader ChCategoryName;
        internal ListView LsvProducts;
        internal ColumnHeader ChProductId;
        internal ColumnHeader ChProductName;
        internal ListView LsvSupportDefinitions;
        internal ColumnHeader ChSupportId;
        internal ColumnHeader ChSupportName;
        internal Button ButtonClose;
    }
}