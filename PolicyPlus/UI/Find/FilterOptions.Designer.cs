using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.Find
{
    public partial class FilterOptions : Form
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
            System.Windows.Forms.Label PolicyTypeLabel;
            System.Windows.Forms.Label PolicyStateLabel;
            System.Windows.Forms.Label CommentedLabel;
            this.PolicyTypeCombobox = new System.Windows.Forms.ComboBox();
            this.PolicyStateCombobox = new System.Windows.Forms.ComboBox();
            this.CommentedCombobox = new System.Windows.Forms.ComboBox();
            this.OkButton = new System.Windows.Forms.Button();
            this.ResetButton = new System.Windows.Forms.Button();
            this.RequirementsBox = new System.Windows.Forms.GroupBox();
            this.AllowedProductsTreeview = new PolicyPlus.UI.Find.DoubleClickIgnoringTreeView();
            this.MatchBlankSupportCheckbox = new System.Windows.Forms.CheckBox();
            this.AlwaysMatchAnyCheckbox = new System.Windows.Forms.CheckBox();
            this.SupportedCheckbox = new System.Windows.Forms.CheckBox();
            PolicyTypeLabel = new System.Windows.Forms.Label();
            PolicyStateLabel = new System.Windows.Forms.Label();
            CommentedLabel = new System.Windows.Forms.Label();
            this.RequirementsBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // PolicyTypeLabel
            // 
            PolicyTypeLabel.AutoSize = true;
            PolicyTypeLabel.Location = new System.Drawing.Point(12, 8);
            PolicyTypeLabel.Name = "PolicyTypeLabel";
            PolicyTypeLabel.Size = new System.Drawing.Size(62, 12);
            PolicyTypeLabel.TabIndex = 1;
            PolicyTypeLabel.Text = "Policy type";
            // 
            // PolicyStateLabel
            // 
            PolicyStateLabel.AutoSize = true;
            PolicyStateLabel.Location = new System.Drawing.Point(121, 8);
            PolicyStateLabel.Name = "PolicyStateLabel";
            PolicyStateLabel.Size = new System.Drawing.Size(73, 12);
            PolicyStateLabel.TabIndex = 3;
            PolicyStateLabel.Text = "Current state";
            // 
            // CommentedLabel
            // 
            CommentedLabel.AutoSize = true;
            CommentedLabel.Location = new System.Drawing.Point(230, 8);
            CommentedLabel.Name = "CommentedLabel";
            CommentedLabel.Size = new System.Drawing.Size(65, 12);
            CommentedLabel.TabIndex = 5;
            CommentedLabel.Text = "Commented";
            // 
            // PolicyTypeCombobox
            // 
            this.PolicyTypeCombobox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.PolicyTypeCombobox.FormattingEnabled = true;
            this.PolicyTypeCombobox.Items.AddRange(new object[] {
            "Any",
            "Policy",
            "Preference"});
            this.PolicyTypeCombobox.Location = new System.Drawing.Point(12, 23);
            this.PolicyTypeCombobox.Name = "PolicyTypeCombobox";
            this.PolicyTypeCombobox.Size = new System.Drawing.Size(103, 20);
            this.PolicyTypeCombobox.TabIndex = 0;
            // 
            // PolicyStateCombobox
            // 
            this.PolicyStateCombobox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.PolicyStateCombobox.FormattingEnabled = true;
            this.PolicyStateCombobox.Items.AddRange(new object[] {
            "Any",
            "Not Configured",
            "Configured",
            "Enabled",
            "Disabled"});
            this.PolicyStateCombobox.Location = new System.Drawing.Point(121, 23);
            this.PolicyStateCombobox.Name = "PolicyStateCombobox";
            this.PolicyStateCombobox.Size = new System.Drawing.Size(103, 20);
            this.PolicyStateCombobox.TabIndex = 2;
            // 
            // CommentedCombobox
            // 
            this.CommentedCombobox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CommentedCombobox.FormattingEnabled = true;
            this.CommentedCombobox.Items.AddRange(new object[] {
            "Any",
            "Yes",
            "No"});
            this.CommentedCombobox.Location = new System.Drawing.Point(230, 23);
            this.CommentedCombobox.Name = "CommentedCombobox";
            this.CommentedCombobox.Size = new System.Drawing.Size(103, 20);
            this.CommentedCombobox.TabIndex = 4;
            // 
            // OkButton
            // 
            this.OkButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.OkButton.Location = new System.Drawing.Point(258, 287);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 21);
            this.OkButton.TabIndex = 6;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += new System.EventHandler(this.OkButton_Click);
            // 
            // ResetButton
            // 
            this.ResetButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ResetButton.Location = new System.Drawing.Point(12, 287);
            this.ResetButton.Name = "ResetButton";
            this.ResetButton.Size = new System.Drawing.Size(75, 21);
            this.ResetButton.TabIndex = 7;
            this.ResetButton.Text = "Reset";
            this.ResetButton.UseVisualStyleBackColor = true;
            this.ResetButton.Click += new System.EventHandler(this.ResetButton_Click);
            // 
            // RequirementsBox
            // 
            this.RequirementsBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.RequirementsBox.Controls.Add(this.AllowedProductsTreeview);
            this.RequirementsBox.Controls.Add(this.MatchBlankSupportCheckbox);
            this.RequirementsBox.Controls.Add(this.AlwaysMatchAnyCheckbox);
            this.RequirementsBox.Enabled = false;
            this.RequirementsBox.Location = new System.Drawing.Point(12, 48);
            this.RequirementsBox.Name = "RequirementsBox";
            this.RequirementsBox.Size = new System.Drawing.Size(321, 234);
            this.RequirementsBox.TabIndex = 8;
            this.RequirementsBox.TabStop = false;
            // 
            // AllowedProductsTreeview
            // 
            this.AllowedProductsTreeview.CheckBoxes = true;
            this.AllowedProductsTreeview.FullRowSelect = true;
            this.AllowedProductsTreeview.HideSelection = false;
            this.AllowedProductsTreeview.Location = new System.Drawing.Point(6, 64);
            this.AllowedProductsTreeview.Name = "AllowedProductsTreeview";
            this.AllowedProductsTreeview.ShowNodeToolTips = true;
            this.AllowedProductsTreeview.Size = new System.Drawing.Size(309, 165);
            this.AllowedProductsTreeview.TabIndex = 10;
            this.AllowedProductsTreeview.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.AllowedProductsTreeview_AfterCheck);
            this.AllowedProductsTreeview.EnabledChanged += new System.EventHandler(this.AllowedProductsTreeview_EnabledChanged);
            // 
            // MatchBlankSupportCheckbox
            // 
            this.MatchBlankSupportCheckbox.AutoSize = true;
            this.MatchBlankSupportCheckbox.Checked = true;
            this.MatchBlankSupportCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.MatchBlankSupportCheckbox.Location = new System.Drawing.Point(6, 42);
            this.MatchBlankSupportCheckbox.Name = "MatchBlankSupportCheckbox";
            this.MatchBlankSupportCheckbox.Size = new System.Drawing.Size(310, 16);
            this.MatchBlankSupportCheckbox.TabIndex = 0;
            this.MatchBlankSupportCheckbox.Text = "Match policies with missing or blank support definitions";
            this.MatchBlankSupportCheckbox.UseVisualStyleBackColor = true;
            // 
            // AlwaysMatchAnyCheckbox
            // 
            this.AlwaysMatchAnyCheckbox.AutoSize = true;
            this.AlwaysMatchAnyCheckbox.Checked = true;
            this.AlwaysMatchAnyCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.AlwaysMatchAnyCheckbox.Location = new System.Drawing.Point(6, 21);
            this.AlwaysMatchAnyCheckbox.Name = "AlwaysMatchAnyCheckbox";
            this.AlwaysMatchAnyCheckbox.Size = new System.Drawing.Size(331, 16);
            this.AlwaysMatchAnyCheckbox.TabIndex = 0;
            this.AlwaysMatchAnyCheckbox.Text = "Match a policy if at least one selected product is supported";
            this.AlwaysMatchAnyCheckbox.UseVisualStyleBackColor = true;
            // 
            // SupportedCheckbox
            // 
            this.SupportedCheckbox.AutoSize = true;
            this.SupportedCheckbox.Location = new System.Drawing.Point(18, 48);
            this.SupportedCheckbox.Name = "SupportedCheckbox";
            this.SupportedCheckbox.Size = new System.Drawing.Size(91, 16);
            this.SupportedCheckbox.TabIndex = 9;
            this.SupportedCheckbox.Text = "Supported on";
            this.SupportedCheckbox.UseVisualStyleBackColor = true;
            this.SupportedCheckbox.CheckedChanged += new System.EventHandler(this.SupportedCheckbox_CheckedChanged);
            // 
            // FilterOptions
            // 
            this.AcceptButton = this.OkButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(345, 319);
            this.Controls.Add(this.SupportedCheckbox);
            this.Controls.Add(this.RequirementsBox);
            this.Controls.Add(this.ResetButton);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(CommentedLabel);
            this.Controls.Add(this.CommentedCombobox);
            this.Controls.Add(PolicyStateLabel);
            this.Controls.Add(this.PolicyStateCombobox);
            this.Controls.Add(PolicyTypeLabel);
            this.Controls.Add(this.PolicyTypeCombobox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FilterOptions";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Filter Options";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FilterOptions_KeyDown);
            this.RequirementsBox.ResumeLayout(false);
            this.RequirementsBox.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal ComboBox PolicyTypeCombobox;
        internal ComboBox PolicyStateCombobox;
        internal ComboBox CommentedCombobox;
        internal Button OkButton;
        internal Button ResetButton;
        internal GroupBox RequirementsBox;
        internal CheckBox SupportedCheckbox;
        internal CheckBox MatchBlankSupportCheckbox;
        internal CheckBox AlwaysMatchAnyCheckbox;
        internal DoubleClickIgnoringTreeView AllowedProductsTreeview;
    }
}