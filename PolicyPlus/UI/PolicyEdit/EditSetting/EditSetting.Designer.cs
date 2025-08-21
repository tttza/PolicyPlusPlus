using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
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
    private System.ComponentModel.IContainer components = null;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            System.Windows.Forms.Label SectionLabel;
            System.Windows.Forms.Panel bottomButtonsPanel;
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
            this.SectionDropdown = new System.Windows.Forms.ComboBox();
            this.ApplyButton = new System.Windows.Forms.Button();
            this.ViewDetailFormattedBtn = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.panel4 = new System.Windows.Forms.Panel();
            this.splitContainer5 = new System.Windows.Forms.SplitContainer();
            this.panel6 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.splitContainer6 = new System.Windows.Forms.SplitContainer();
            this.panel5 = new System.Windows.Forms.Panel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.HelpTextbox = new System.Windows.Forms.RichTextBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.panel_table = new System.Windows.Forms.Panel();
            SectionLabel = new System.Windows.Forms.Label();
            bottomButtonsPanel = new System.Windows.Forms.Panel();
            System.Windows.Forms.TableLayoutPanel bottomButtonsLayout;
            System.Windows.Forms.TableLayoutPanel sectionTable;
            System.Windows.Forms.FlowLayoutPanel radiosFlow;
            bottomButtonsLayout = new System.Windows.Forms.TableLayoutPanel();
            this.ExtraOptionsPanel.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).BeginInit();
            this.splitContainer4.Panel1.SuspendLayout();
            this.splitContainer4.Panel2.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            this.panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer5)).BeginInit();
            this.splitContainer5.Panel1.SuspendLayout();
            this.splitContainer5.Panel2.SuspendLayout();
            this.splitContainer5.SuspendLayout();
            this.panel6.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer6)).BeginInit();
            this.splitContainer6.Panel1.SuspendLayout();
            this.splitContainer6.Panel2.SuspendLayout();
            this.splitContainer6.SuspendLayout();
            this.panel5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.panel_table.SuspendLayout();
            this.SuspendLayout();
            // 
            // SectionLabel
            // 
            SectionLabel.AutoSize = true;
            SectionLabel.Location = new System.Drawing.Point(8, 8);
            SectionLabel.Name = "SectionLabel";
            SectionLabel.Size = new System.Drawing.Size(58, 12);
            SectionLabel.TabIndex = 12;
            SectionLabel.Text = "Editing for";
            // 
            // CommentLabel
            // 
            this.CommentLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CommentLabel.Location = new System.Drawing.Point(0, 0);
            this.CommentLabel.Name = "CommentLabel";
            this.CommentLabel.Size = new System.Drawing.Size(100, 29);
            this.CommentLabel.TabIndex = 2;
            this.CommentLabel.Text = "Comment";
            this.CommentLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // SupportedLabel
            // 
            this.SupportedLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SupportedLabel.Location = new System.Drawing.Point(0, 0);
            this.SupportedLabel.Name = "SupportedLabel";
            this.SupportedLabel.Size = new System.Drawing.Size(100, 70);
            this.SupportedLabel.TabIndex = 4;
            this.SupportedLabel.Text = "Supported on";
            this.SupportedLabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.SupportedLabel.Click += SupportedLabel_Click;
            // 
            // SettingNameLabel
            // 
            this.SettingNameLabel.AutoEllipsis = true;
            this.SettingNameLabel.AutoSize = false;
            this.SettingNameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SettingNameLabel.Location = new System.Drawing.Point(0, 0);
            this.SettingNameLabel.Name = "SettingNameLabel";
            this.SettingNameLabel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.SettingNameLabel.Size = new System.Drawing.Size(626, 22);
            this.SettingNameLabel.TabIndex = 0;
            this.SettingNameLabel.Text = "Policy name";
            this.SettingNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CommentTextbox
            // 
            this.CommentTextbox.AcceptsReturn = true;
            this.CommentTextbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CommentTextbox.Location = new System.Drawing.Point(0, 0);
            this.CommentTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CommentTextbox.Multiline = true;
            this.CommentTextbox.Name = "CommentTextbox";
            this.CommentTextbox.Size = new System.Drawing.Size(312, 29);
            this.CommentTextbox.TabIndex = 100;
            this.CommentTextbox.TabStop = false;
            // 
            // SupportedTextbox
            // 
            this.SupportedTextbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SupportedTextbox.Location = new System.Drawing.Point(0, 0);
            this.SupportedTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.SupportedTextbox.Multiline = true;
            this.SupportedTextbox.Name = "SupportedTextbox";
            this.SupportedTextbox.ReadOnly = true;
            this.SupportedTextbox.Size = new System.Drawing.Size(312, 70);
            this.SupportedTextbox.TabIndex = 101;
            this.SupportedTextbox.TabStop = false;
            // 
            // NotConfiguredOption
            // 
            this.NotConfiguredOption.AutoSize = true;
            this.NotConfiguredOption.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.NotConfiguredOption.Name = "NotConfiguredOption";
            this.NotConfiguredOption.Size = new System.Drawing.Size(100, 16);
            this.NotConfiguredOption.TabIndex = 1;
            this.NotConfiguredOption.TabStop = true;
            this.NotConfiguredOption.Text = "Not Configured";
            this.NotConfiguredOption.UseVisualStyleBackColor = true;
            this.NotConfiguredOption.CheckedChanged += StateRadiosChanged;
            // 
            // EnabledOption
            // 
            this.EnabledOption.AutoSize = true;
            this.EnabledOption.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.EnabledOption.Name = "EnabledOption";
            this.EnabledOption.Size = new System.Drawing.Size(63, 16);
            this.EnabledOption.TabIndex = 2;
            this.EnabledOption.TabStop = true;
            this.EnabledOption.Text = "Enabled";
            this.EnabledOption.UseVisualStyleBackColor = true;
            this.EnabledOption.CheckedChanged += StateRadiosChanged;
            // 
            // DisabledOption
            // 
            this.DisabledOption.AutoSize = true;
            this.DisabledOption.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.DisabledOption.Name = "DisabledOption";
            this.DisabledOption.Size = new System.Drawing.Size(67, 16);
            this.DisabledOption.TabIndex = 3;
            this.DisabledOption.TabStop = true;
            this.DisabledOption.Text = "Disabled";
            this.DisabledOption.UseVisualStyleBackColor = true;
            this.DisabledOption.CheckedChanged += StateRadiosChanged;
            // 
            // ExtraOptionsPanel
            // 
            this.ExtraOptionsPanel.AutoScroll = true;
            this.ExtraOptionsPanel.AutoSize = false;
            this.ExtraOptionsPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            this.ExtraOptionsPanel.BackColor = System.Drawing.Color.White;
            this.ExtraOptionsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ExtraOptionsPanel.Controls.Add(this.ExtraOptionsTable);
            this.ExtraOptionsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ExtraOptionsPanel.Location = new System.Drawing.Point(0, 0);
            this.ExtraOptionsPanel.Margin = new System.Windows.Forms.Padding(0);
            this.ExtraOptionsPanel.Padding = new System.Windows.Forms.Padding(6,4,6,6); // add inner padding to reduce perceived width of controls
            this.ExtraOptionsPanel.MinimumSize = new System.Drawing.Size(100, 100);
            this.ExtraOptionsPanel.Name = "ExtraOptionsPanel";
            this.ExtraOptionsPanel.Size = new System.Drawing.Size(312, 228);
            this.ExtraOptionsPanel.TabIndex = 8;
            // 
            // ExtraOptionsTable
            // 
            this.ExtraOptionsTable.AutoSize = true;
            this.ExtraOptionsTable.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ExtraOptionsTable.ColumnCount = 1;
            this.ExtraOptionsTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.ExtraOptionsTable.Dock = System.Windows.Forms.DockStyle.Top;
            this.ExtraOptionsTable.Location = new System.Drawing.Point(0, 0);
            this.ExtraOptionsTable.Margin = new System.Windows.Forms.Padding(0);
            this.ExtraOptionsTable.Name = "ExtraOptionsTable";
            this.ExtraOptionsTable.Padding = new System.Windows.Forms.Padding(0, 0, 1, 0);
            this.ExtraOptionsTable.RowCount = 1;
            this.ExtraOptionsTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.ExtraOptionsTable.Size = new System.Drawing.Size(310, 0);
            this.ExtraOptionsTable.TabIndex = 0;
            this.ExtraOptionsTable.DoubleClick += new System.EventHandler(this.CopyToClipboard);
            // 
            // CloseButton
            // 
            this.CloseButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.CloseButton.Margin = new System.Windows.Forms.Padding(8, 8, 8, 8);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(75, 28);
            this.CloseButton.TabIndex = 104;
            this.CloseButton.Text = "Cancel";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += CancelButton_Click;
            // 
            // OkButton
            // 
            this.OkButton.Margin = new System.Windows.Forms.Padding(8);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 28);
            this.OkButton.TabIndex = 103;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += OkButton_Click;
            // 
            // SectionDropdown
            // 
            this.SectionDropdown.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.SectionDropdown.FormattingEnabled = true;
            this.SectionDropdown.Items.AddRange(new object[] {
            "User",
            "Computer"});
            this.SectionDropdown.Location = new System.Drawing.Point(68, 6);
            this.SectionDropdown.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.SectionDropdown.Name = "SectionDropdown";
            this.SectionDropdown.Size = new System.Drawing.Size(112, 20);
            this.SectionDropdown.TabIndex = 4;
            this.SectionDropdown.SelectedIndexChanged += SectionDropdown_SelectedIndexChanged;
            // 
            // ApplyButton
            // 
            this.ApplyButton.Margin = new System.Windows.Forms.Padding(8);
            this.ApplyButton.Name = "ApplyButton";
            this.ApplyButton.Size = new System.Drawing.Size(75, 28);
            this.ApplyButton.TabIndex = 105;
            this.ApplyButton.Text = "Apply";
            this.ApplyButton.UseVisualStyleBackColor = true;
            this.ApplyButton.Click += ApplyButton_Click;
            // 
            // ViewDetailFormattedBtn
            // 
            this.ViewDetailFormattedBtn.Margin = new System.Windows.Forms.Padding(8, 8, 8, 8);
            this.ViewDetailFormattedBtn.Name = "ViewDetailFormattedBtn";
            this.ViewDetailFormattedBtn.Size = new System.Drawing.Size(114, 28);
            this.ViewDetailFormattedBtn.TabIndex = 106;
            this.ViewDetailFormattedBtn.Text = "View Detail";
            this.ViewDetailFormattedBtn.UseVisualStyleBackColor = true;
            this.ViewDetailFormattedBtn.Click += ViewDetailFormattedBtn_Click;
            // 
            // panel1
            // 
            sectionTable = new System.Windows.Forms.TableLayoutPanel();
            sectionTable.AutoSize = true;
            sectionTable.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            sectionTable.ColumnCount = 2;
            sectionTable.RowCount = 1;
            sectionTable.Dock = System.Windows.Forms.DockStyle.Top;
            sectionTable.Padding = new System.Windows.Forms.Padding(8,4,8,0);
            sectionTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            sectionTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            SectionLabel.Margin = new System.Windows.Forms.Padding(0,4,8,0);
            SectionLabel.AutoSize = true;
            this.SectionDropdown.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SectionDropdown.Margin = new System.Windows.Forms.Padding(0,2,0,0);
            sectionTable.Controls.Add(SectionLabel,0,0);
            sectionTable.Controls.Add(this.SectionDropdown,1,0);
            radiosFlow = new System.Windows.Forms.FlowLayoutPanel();
            radiosFlow.AutoSize = true;
            radiosFlow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            radiosFlow.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            radiosFlow.WrapContents = false;
            radiosFlow.Dock = System.Windows.Forms.DockStyle.Top;
            radiosFlow.Padding = new System.Windows.Forms.Padding(8,4,8,4);
            radiosFlow.Controls.Add(this.NotConfiguredOption);
            radiosFlow.Controls.Add(this.EnabledOption);
            radiosFlow.Controls.Add(this.DisabledOption);
            this.panel1.Controls.Clear();
            this.panel1.Controls.Add(radiosFlow);
            this.panel1.Controls.Add(sectionTable);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(208, 102);
            this.panel1.TabIndex = 107;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.splitContainer4);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Margin = new System.Windows.Forms.Padding(0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(415, 102);
            this.panel2.TabIndex = 108;
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            this.splitContainer4.Panel1.Controls.Add(this.panel4);
            // 
            // splitContainer4.Panel2
            // 
            this.splitContainer4.Panel2.Controls.Add(this.panel3);
            this.splitContainer4.Panel2MinSize = 70;
            this.splitContainer4.Size = new System.Drawing.Size(415, 102);
            this.splitContainer4.SplitterDistance = 29;
            this.splitContainer4.SplitterWidth = 3;
            this.splitContainer4.TabIndex = 104;
            // 
            // panel4
            // 
            this.panel4.AutoSize = true;
            this.panel4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel4.Controls.Add(this.splitContainer5);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(0, 0);
            this.panel4.Margin = new System.Windows.Forms.Padding(2);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(415, 29);
            this.panel4.TabIndex = 103;
            // 
            // splitContainer5
            // 
            this.splitContainer5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer5.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer5.Location = new System.Drawing.Point(0, 0);
            this.splitContainer5.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer5.Name = "splitContainer5";
            // 
            // splitContainer5.Panel1
            // 
            this.splitContainer5.Panel1.Controls.Add(this.panel6);
            // 
            // splitContainer5.Panel2
            // 
            this.splitContainer5.Panel2.Controls.Add(this.CommentTextbox);
            this.splitContainer5.Size = new System.Drawing.Size(415, 29);
            this.splitContainer5.SplitterDistance = 100;
            this.splitContainer5.SplitterWidth = 3;
            this.splitContainer5.TabIndex = 102;
            // 
            // panel6
            // 
            this.panel6.Controls.Add(this.CommentLabel);
            this.panel6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel6.Location = new System.Drawing.Point(0, 0);
            this.panel6.Margin = new System.Windows.Forms.Padding(2);
            this.panel6.Name = "panel6";
            this.panel6.Size = new System.Drawing.Size(100, 29);
            this.panel6.TabIndex = 101;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.splitContainer6);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Margin = new System.Windows.Forms.Padding(2);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(415, 70);
            this.panel3.TabIndex = 102;
            // 
            // splitContainer6
            // 
            this.splitContainer6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer6.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer6.Location = new System.Drawing.Point(0, 0);
            this.splitContainer6.Margin = new System.Windows.Forms.Padding(2);
            this.splitContainer6.Name = "splitContainer6";
            // 
            // splitContainer6.Panel1
            // 
            this.splitContainer6.Panel1.Controls.Add(this.panel5);
            // 
            // splitContainer6.Panel2
            // 
            this.splitContainer6.Panel2.Controls.Add(this.SupportedTextbox);
            this.splitContainer6.Size = new System.Drawing.Size(415, 70);
            this.splitContainer6.SplitterDistance = 100;
            this.splitContainer6.SplitterWidth = 3;
            this.splitContainer6.TabIndex = 5;
            // 
            // panel5
            // 
            this.panel5.Controls.Add(this.SupportedLabel);
            this.panel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel5.Location = new System.Drawing.Point(0, 0);
            this.panel5.Margin = new System.Windows.Forms.Padding(2);
            this.panel5.Name = "panel5";
            this.panel5.Size = new System.Drawing.Size(100, 70);
            this.panel5.TabIndex = 102;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.panel_table);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.HelpTextbox);
            this.splitContainer1.Size = new System.Drawing.Size(626, 228);
            this.splitContainer1.SplitterDistance = this.splitContainer1.Width / 2;
            this.splitContainer1.SplitterWidth = 5;
            this.splitContainer1.TabIndex = 109;
            // 
            // HelpTextbox
            // 
            this.HelpTextbox.BackColor = System.Drawing.SystemColors.Control;
            this.HelpTextbox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.HelpTextbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.HelpTextbox.Location = new System.Drawing.Point(0, 0);
            this.HelpTextbox.Name = "HelpTextbox";
            this.HelpTextbox.ReadOnly = true;
            this.HelpTextbox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.HelpTextbox.Size = new System.Drawing.Size(309, 228);
            this.HelpTextbox.TabIndex = 0;
            this.HelpTextbox.Text = "";
            this.HelpTextbox.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.HelpTextbox_LinkClicked);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.panel2);
            this.splitContainer2.Size = new System.Drawing.Size(626, 102);
            this.splitContainer2.SplitterDistance = 208;
            this.splitContainer2.SplitterWidth = 3;
            this.splitContainer2.TabIndex = 110;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Margin = new System.Windows.Forms.Padding(0);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.SettingNameLabel);
            this.splitContainer3.Panel1MinSize = 24;
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer3.Size = new System.Drawing.Size(626, 150);
            this.splitContainer3.SplitterDistance = 26;
            this.splitContainer3.SplitterWidth = 3;
            this.splitContainer3.TabIndex = 111;
            // 
            // panel_table
            // 
            this.panel_table.AutoSize = false;
            this.panel_table.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            this.panel_table.Controls.Add(this.ExtraOptionsPanel);
            this.panel_table.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_table.Location = new System.Drawing.Point(0, 0);
            this.panel_table.MinimumSize = new System.Drawing.Size(100, 100);
            this.panel_table.Name = "panel_table";
            this.panel_table.Size = new System.Drawing.Size(312, 228);
            this.panel_table.TabIndex = 9;

            // 
            // bottomButtonsPanel
            // 
            bottomButtonsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            bottomButtonsPanel.Height = 40;
            bottomButtonsPanel.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            bottomButtonsPanel.Controls.Add(bottomButtonsLayout);
            // 
            // bottomButtonsLayout
            // 
            bottomButtonsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            bottomButtonsLayout.ColumnCount = 6;
            bottomButtonsLayout.RowCount = 1;
            bottomButtonsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            bottomButtonsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle()); // View Detail
            bottomButtonsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F)); // filler
            bottomButtonsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle()); // OK
            bottomButtonsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle()); // Cancel
            bottomButtonsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle()); // Apply
            bottomButtonsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 0F)); // spacer (unused)
            bottomButtonsLayout.Controls.Add(this.ViewDetailFormattedBtn, 0, 0);
            bottomButtonsLayout.Controls.Add(this.OkButton, 2, 0);
            bottomButtonsLayout.Controls.Add(this.CloseButton, 3, 0);
            bottomButtonsLayout.Controls.Add(this.ApplyButton, 4, 0);
            this.ViewDetailFormattedBtn.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.OkButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.CloseButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.ApplyButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.ViewDetailFormattedBtn.Margin = new System.Windows.Forms.Padding(0, 4, 12, 4);
            this.OkButton.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            this.CloseButton.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            this.ApplyButton.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            // 
            // EditSetting
            // 
            this.AcceptButton = this.OkButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.CloseButton;
            this.ClientSize = new System.Drawing.Size(639, 402);
            var mainContentPanel = new System.Windows.Forms.Panel();
            mainContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            mainContentPanel.Padding = new System.Windows.Forms.Padding(12, 4, 12, 0);
            mainContentPanel.BackColor = this.BackColor;
            this.splitContainer3.Parent = null;
            this.splitContainer1.Parent = null;
            mainContentPanel.Controls.Add(this.splitContainer1); // add fill after header so order reversed below
            mainContentPanel.Controls.SetChildIndex(this.splitContainer1, 1); // ensure header at index 0
            mainContentPanel.Controls.Add(this.splitContainer3);
            this.Controls.Add(mainContentPanel);
            this.Controls.Add(bottomButtonsPanel);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(654, 440);
            this.Name = "EditSetting";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Policy Setting";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.EditSetting_FormClosed);
            this.Shown += new System.EventHandler(this.EditSetting_Shown);
            this.ExtraOptionsPanel.ResumeLayout(false);
            this.ExtraOptionsPanel.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.splitContainer4.Panel1.ResumeLayout(false);
            this.splitContainer4.Panel1.PerformLayout();
            this.splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).EndInit();
            this.splitContainer4.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            this.splitContainer5.Panel1.ResumeLayout(false);
            this.splitContainer5.Panel2.ResumeLayout(false);
            this.splitContainer5.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer5)).EndInit();
            this.splitContainer5.ResumeLayout(false);
            this.panel6.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.splitContainer6.Panel1.ResumeLayout(false);
            this.splitContainer6.Panel2.ResumeLayout(false);
            this.splitContainer6.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer6)).EndInit();
            this.splitContainer6.ResumeLayout(false);
            this.panel5.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.panel_table.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        protected Label SettingNameLabel;
        protected TextBox CommentTextbox;
        protected TextBox SupportedTextbox;
        protected RadioButton NotConfiguredOption;
        protected RadioButton EnabledOption;
        protected RadioButton DisabledOption;
        protected Panel ExtraOptionsPanel;
        protected TableLayoutPanel ExtraOptionsTable;
        protected Button CloseButton;
        protected Button OkButton;
        protected ComboBox SectionDropdown;
        protected Button ApplyButton;
        protected Label CommentLabel;
        protected Label SupportedLabel;
        protected Button ViewDetailFormattedBtn;
        private Panel panel1;
        private Panel panel2;
        private SplitContainer splitContainer1;
        private SplitContainer splitContainer2;
        private SplitContainer splitContainer3;
        private Panel panel3;
        private Panel panel4;
        private SplitContainer splitContainer4;
        private Panel panel6;
        private Panel panel5;
        private SplitContainer splitContainer5;
        private SplitContainer splitContainer6;
        private RichTextBox HelpTextbox;
        private Panel panel_table;
    }
}