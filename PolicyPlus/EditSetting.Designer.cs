using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class EditSetting : Form
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
            System.Windows.Forms.Label SectionLabel;
            this.CommentLabel = new System.Windows.Forms.Label();
            this.SupportedLabel = new System.Windows.Forms.Label();
            this.SettingNameLabel = new System.Windows.Forms.Label();
            this.CommentTextbox = new System.Windows.Forms.TextBox();
            this.SupportedTextbox = new System.Windows.Forms.TextBox();
            this.NotConfiguredOption = new System.Windows.Forms.RadioButton();
            this.EnabledOption = new System.Windows.Forms.RadioButton();
            this.DisabledOption = new System.Windows.Forms.RadioButton();
            this.ExtraOptionsPanel = new System.Windows.Forms.Panel();
            this.ExtraOptionsTable = new System.Windows.Forms.TableLayoutPanel();
            this.CloseButton = new System.Windows.Forms.Button();
            this.OkButton = new System.Windows.Forms.Button();
            this.HelpTextbox = new System.Windows.Forms.TextBox();
            this.SectionDropdown = new System.Windows.Forms.ComboBox();
            this.ApplyButton = new System.Windows.Forms.Button();
            this.ViewDetailFormattedBtn = new System.Windows.Forms.Button();
            SectionLabel = new System.Windows.Forms.Label();
            this.ExtraOptionsPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // SectionLabel
            // 
            SectionLabel.AutoSize = true;
            SectionLabel.Location = new System.Drawing.Point(16, 32);
            SectionLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            SectionLabel.Name = "SectionLabel";
            SectionLabel.Size = new System.Drawing.Size(71, 15);
            SectionLabel.TabIndex = 12;
            SectionLabel.Text = "Editing for";
            // 
            // CommentLabel
            // 
            this.CommentLabel.AutoSize = true;
            this.CommentLabel.Location = new System.Drawing.Point(347, 32);
            this.CommentLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.CommentLabel.Name = "CommentLabel";
            this.CommentLabel.Size = new System.Drawing.Size(68, 15);
            this.CommentLabel.TabIndex = 2;
            this.CommentLabel.Text = "Comment";
            // 
            // SupportedLabel
            // 
            this.SupportedLabel.AutoSize = true;
            this.SupportedLabel.Location = new System.Drawing.Point(320, 119);
            this.SupportedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.SupportedLabel.Name = "SupportedLabel";
            this.SupportedLabel.Size = new System.Drawing.Size(92, 15);
            this.SupportedLabel.TabIndex = 4;
            this.SupportedLabel.Text = "Supported on";
            // 
            // SettingNameLabel
            // 
            this.SettingNameLabel.AutoEllipsis = true;
            this.SettingNameLabel.Location = new System.Drawing.Point(16, 10);
            this.SettingNameLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.SettingNameLabel.Name = "SettingNameLabel";
            this.SettingNameLabel.Size = new System.Drawing.Size(819, 15);
            this.SettingNameLabel.TabIndex = 0;
            this.SettingNameLabel.Text = "Policy name";
            // 
            // CommentTextbox
            // 
            this.CommentTextbox.AcceptsReturn = true;
            this.CommentTextbox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.CommentTextbox.Location = new System.Drawing.Point(423, 29);
            this.CommentTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.CommentTextbox.Multiline = true;
            this.CommentTextbox.Name = "CommentTextbox";
            this.CommentTextbox.Size = new System.Drawing.Size(411, 79);
            this.CommentTextbox.TabIndex = 100;
            // 
            // SupportedTextbox
            // 
            this.SupportedTextbox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SupportedTextbox.Location = new System.Drawing.Point(423, 115);
            this.SupportedTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.SupportedTextbox.Multiline = true;
            this.SupportedTextbox.Name = "SupportedTextbox";
            this.SupportedTextbox.ReadOnly = true;
            this.SupportedTextbox.Size = new System.Drawing.Size(411, 50);
            this.SupportedTextbox.TabIndex = 101;
            // 
            // NotConfiguredOption
            // 
            this.NotConfiguredOption.AutoSize = true;
            this.NotConfiguredOption.Location = new System.Drawing.Point(16, 60);
            this.NotConfiguredOption.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.NotConfiguredOption.Name = "NotConfiguredOption";
            this.NotConfiguredOption.Size = new System.Drawing.Size(125, 19);
            this.NotConfiguredOption.TabIndex = 1;
            this.NotConfiguredOption.TabStop = true;
            this.NotConfiguredOption.Text = "Not Configured";
            this.NotConfiguredOption.UseVisualStyleBackColor = true;
            this.NotConfiguredOption.CheckedChanged += new System.EventHandler(this.StateRadiosChanged);
            // 
            // EnabledOption
            // 
            this.EnabledOption.AutoSize = true;
            this.EnabledOption.Location = new System.Drawing.Point(16, 87);
            this.EnabledOption.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.EnabledOption.Name = "EnabledOption";
            this.EnabledOption.Size = new System.Drawing.Size(76, 19);
            this.EnabledOption.TabIndex = 2;
            this.EnabledOption.TabStop = true;
            this.EnabledOption.Text = "Enabled";
            this.EnabledOption.UseVisualStyleBackColor = true;
            this.EnabledOption.CheckedChanged += new System.EventHandler(this.StateRadiosChanged);
            // 
            // DisabledOption
            // 
            this.DisabledOption.AutoSize = true;
            this.DisabledOption.Location = new System.Drawing.Point(16, 113);
            this.DisabledOption.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.DisabledOption.Name = "DisabledOption";
            this.DisabledOption.Size = new System.Drawing.Size(80, 19);
            this.DisabledOption.TabIndex = 3;
            this.DisabledOption.TabStop = true;
            this.DisabledOption.Text = "Disabled";
            this.DisabledOption.UseVisualStyleBackColor = true;
            this.DisabledOption.CheckedChanged += new System.EventHandler(this.StateRadiosChanged);
            // 
            // ExtraOptionsPanel
            // 
            this.ExtraOptionsPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.ExtraOptionsPanel.BackColor = System.Drawing.Color.White;
            this.ExtraOptionsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ExtraOptionsPanel.Controls.Add(this.ExtraOptionsTable);
            this.ExtraOptionsPanel.Location = new System.Drawing.Point(16, 173);
            this.ExtraOptionsPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.ExtraOptionsPanel.Name = "ExtraOptionsPanel";
            this.ExtraOptionsPanel.Size = new System.Drawing.Size(398, 281);
            this.ExtraOptionsPanel.TabIndex = 8;
            // 
            // ExtraOptionsTable
            // 
            this.ExtraOptionsTable.AutoSize = true;
            this.ExtraOptionsTable.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ExtraOptionsTable.ColumnCount = 1;
            this.ExtraOptionsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 397F));
            this.ExtraOptionsTable.Location = new System.Drawing.Point(0, 0);
            this.ExtraOptionsTable.Margin = new System.Windows.Forms.Padding(0);
            this.ExtraOptionsTable.MaximumSize = new System.Drawing.Size(396, 0);
            this.ExtraOptionsTable.MinimumSize = new System.Drawing.Size(396, 0);
            this.ExtraOptionsTable.Name = "ExtraOptionsTable";
            this.ExtraOptionsTable.RowCount = 1;
            this.ExtraOptionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            this.ExtraOptionsTable.Size = new System.Drawing.Size(396, 23);
            this.ExtraOptionsTable.TabIndex = 0;
            // 
            // CloseButton
            // 
            this.CloseButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CloseButton.Location = new System.Drawing.Point(627, 462);
            this.CloseButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(100, 27);
            this.CloseButton.TabIndex = 104;
            this.CloseButton.Text = "Cancel";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // OkButton
            // 
            this.OkButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.OkButton.Location = new System.Drawing.Point(519, 462);
            this.OkButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(100, 27);
            this.OkButton.TabIndex = 103;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += new System.EventHandler(this.OkButton_Click);
            // 
            // HelpTextbox
            // 
            this.HelpTextbox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.HelpTextbox.Location = new System.Drawing.Point(423, 173);
            this.HelpTextbox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.HelpTextbox.Multiline = true;
            this.HelpTextbox.Name = "HelpTextbox";
            this.HelpTextbox.ReadOnly = true;
            this.HelpTextbox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.HelpTextbox.Size = new System.Drawing.Size(411, 281);
            this.HelpTextbox.TabIndex = 102;
            // 
            // SectionDropdown
            // 
            this.SectionDropdown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.SectionDropdown.FormattingEnabled = true;
            this.SectionDropdown.Items.AddRange(new object[] {
            "User",
            "Computer"});
            this.SectionDropdown.Location = new System.Drawing.Point(96, 29);
            this.SectionDropdown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.SectionDropdown.Name = "SectionDropdown";
            this.SectionDropdown.Size = new System.Drawing.Size(148, 23);
            this.SectionDropdown.TabIndex = 4;
            this.SectionDropdown.SelectedIndexChanged += new System.EventHandler(this.SectionDropdown_SelectedIndexChanged);
            // 
            // ApplyButton
            // 
            this.ApplyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ApplyButton.Location = new System.Drawing.Point(735, 462);
            this.ApplyButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.ApplyButton.Name = "ApplyButton";
            this.ApplyButton.Size = new System.Drawing.Size(100, 27);
            this.ApplyButton.TabIndex = 105;
            this.ApplyButton.Text = "Apply";
            this.ApplyButton.UseVisualStyleBackColor = true;
            this.ApplyButton.Click += new System.EventHandler(this.ApplyButton_Click);
            // 
            // ViewDetailFormattedBtn
            // 
            this.ViewDetailFormattedBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ViewDetailFormattedBtn.Location = new System.Drawing.Point(13, 461);
            this.ViewDetailFormattedBtn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.ViewDetailFormattedBtn.Name = "ViewDetailFormattedBtn";
            this.ViewDetailFormattedBtn.Size = new System.Drawing.Size(152, 27);
            this.ViewDetailFormattedBtn.TabIndex = 106;
            this.ViewDetailFormattedBtn.Text = "View Detail (Apply)";
            this.ViewDetailFormattedBtn.UseVisualStyleBackColor = true;
            this.ViewDetailFormattedBtn.Click += new System.EventHandler(this.ViewDetailFormattedBtn_Click);
            // 
            // EditSetting
            // 
            this.AcceptButton = this.OkButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(851, 502);
            this.Controls.Add(this.ViewDetailFormattedBtn);
            this.Controls.Add(this.ApplyButton);
            this.Controls.Add(SectionLabel);
            this.Controls.Add(this.SectionDropdown);
            this.Controls.Add(this.HelpTextbox);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.ExtraOptionsPanel);
            this.Controls.Add(this.DisabledOption);
            this.Controls.Add(this.EnabledOption);
            this.Controls.Add(this.NotConfiguredOption);
            this.Controls.Add(this.SupportedLabel);
            this.Controls.Add(this.SupportedTextbox);
            this.Controls.Add(this.CommentLabel);
            this.Controls.Add(this.CommentTextbox);
            this.Controls.Add(this.SettingNameLabel);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(866, 540);
            this.Name = "EditSetting";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Policy Setting";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.EditSetting_FormClosed);
            this.Shown += new System.EventHandler(this.EditSetting_Shown);
            this.Resize += new System.EventHandler(this.EditSetting_Resize);
            this.ExtraOptionsPanel.ResumeLayout(false);
            this.ExtraOptionsPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal Label SettingNameLabel;
        internal TextBox CommentTextbox;
        internal TextBox SupportedTextbox;
        internal RadioButton NotConfiguredOption;
        internal RadioButton EnabledOption;
        internal RadioButton DisabledOption;
        internal Panel ExtraOptionsPanel;
        internal TableLayoutPanel ExtraOptionsTable;
        internal Button CloseButton;
        internal Button OkButton;
        internal TextBox HelpTextbox;
        internal ComboBox SectionDropdown;
        internal Button ApplyButton;
        internal Label CommentLabel;
        internal Label SupportedLabel;
        internal Button ViewDetailFormattedBtn;
    }
}