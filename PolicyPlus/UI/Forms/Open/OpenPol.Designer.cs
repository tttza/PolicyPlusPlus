using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class OpenPol : Form
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
            this.ComputerGroup = new System.Windows.Forms.GroupBox();
            this.CompRegTextbox = new System.Windows.Forms.TextBox();
            this.CompNullOption = new System.Windows.Forms.RadioButton();
            this.CompFileBrowseButton = new System.Windows.Forms.Button();
            this.CompPolFilenameTextbox = new System.Windows.Forms.TextBox();
            this.CompFileOption = new System.Windows.Forms.RadioButton();
            this.CompRegistryOption = new System.Windows.Forms.RadioButton();
            this.CompLocalOption = new System.Windows.Forms.RadioButton();
            this.UserGroup = new System.Windows.Forms.GroupBox();
            this.UserRegTextbox = new System.Windows.Forms.TextBox();
            this.UserPerUserRegOption = new System.Windows.Forms.RadioButton();
            this.UserPerUserGpoOption = new System.Windows.Forms.RadioButton();
            this.UserBrowseHiveButton = new System.Windows.Forms.Button();
            this.UserNullOption = new System.Windows.Forms.RadioButton();
            this.UserBrowseGpoButton = new System.Windows.Forms.Button();
            this.UserHivePathTextbox = new System.Windows.Forms.TextBox();
            this.UserFileBrowseButton = new System.Windows.Forms.Button();
            this.UserGpoSidTextbox = new System.Windows.Forms.TextBox();
            this.UserPolFilenameTextbox = new System.Windows.Forms.TextBox();
            this.UserFileOption = new System.Windows.Forms.RadioButton();
            this.UserRegistryOption = new System.Windows.Forms.RadioButton();
            this.UserLocalOption = new System.Windows.Forms.RadioButton();
            this.OkButton = new System.Windows.Forms.Button();
            this.ComputerGroup.SuspendLayout();
            this.UserGroup.SuspendLayout();
            this.SuspendLayout();
            // 
            // ComputerGroup
            // 
            this.ComputerGroup.Controls.Add(this.CompRegTextbox);
            this.ComputerGroup.Controls.Add(this.CompNullOption);
            this.ComputerGroup.Controls.Add(this.CompFileBrowseButton);
            this.ComputerGroup.Controls.Add(this.CompPolFilenameTextbox);
            this.ComputerGroup.Controls.Add(this.CompFileOption);
            this.ComputerGroup.Controls.Add(this.CompRegistryOption);
            this.ComputerGroup.Controls.Add(this.CompLocalOption);
            this.ComputerGroup.Location = new System.Drawing.Point(12, 11);
            this.ComputerGroup.Name = "ComputerGroup";
            this.ComputerGroup.Size = new System.Drawing.Size(224, 102);
            this.ComputerGroup.TabIndex = 0;
            this.ComputerGroup.TabStop = false;
            this.ComputerGroup.Text = "Computer";
            // 
            // CompRegTextbox
            // 
            this.CompRegTextbox.Location = new System.Drawing.Point(104, 38);
            this.CompRegTextbox.Name = "CompRegTextbox";
            this.CompRegTextbox.Size = new System.Drawing.Size(114, 19);
            this.CompRegTextbox.TabIndex = 2;
            this.CompRegTextbox.Text = "HKLM";
            // 
            // CompNullOption
            // 
            this.CompNullOption.AutoSize = true;
            this.CompNullOption.Location = new System.Drawing.Point(6, 81);
            this.CompNullOption.Name = "CompNullOption";
            this.CompNullOption.Size = new System.Drawing.Size(126, 16);
            this.CompNullOption.TabIndex = 6;
            this.CompNullOption.TabStop = true;
            this.CompNullOption.Text = "Scratch space (null)";
            this.CompNullOption.UseVisualStyleBackColor = true;
            this.CompNullOption.CheckedChanged += new System.EventHandler(this.CompOptionsCheckedChanged);
            // 
            // CompFileBrowseButton
            // 
            this.CompFileBrowseButton.Location = new System.Drawing.Point(180, 57);
            this.CompFileBrowseButton.Name = "CompFileBrowseButton";
            this.CompFileBrowseButton.Size = new System.Drawing.Size(38, 21);
            this.CompFileBrowseButton.TabIndex = 5;
            this.CompFileBrowseButton.Text = "...";
            this.CompFileBrowseButton.UseVisualStyleBackColor = true;
            this.CompFileBrowseButton.Click += new System.EventHandler(this.CompFileBrowseButton_Click);
            // 
            // CompPolFilenameTextbox
            // 
            this.CompPolFilenameTextbox.Location = new System.Drawing.Point(74, 59);
            this.CompPolFilenameTextbox.Name = "CompPolFilenameTextbox";
            this.CompPolFilenameTextbox.Size = new System.Drawing.Size(100, 19);
            this.CompPolFilenameTextbox.TabIndex = 4;
            // 
            // CompFileOption
            // 
            this.CompFileOption.AutoSize = true;
            this.CompFileOption.Location = new System.Drawing.Point(6, 60);
            this.CompFileOption.Name = "CompFileOption";
            this.CompFileOption.Size = new System.Drawing.Size(64, 16);
            this.CompFileOption.TabIndex = 3;
            this.CompFileOption.TabStop = true;
            this.CompFileOption.Text = "POL file";
            this.CompFileOption.UseVisualStyleBackColor = true;
            this.CompFileOption.CheckedChanged += new System.EventHandler(this.CompOptionsCheckedChanged);
            // 
            // CompRegistryOption
            // 
            this.CompRegistryOption.AutoSize = true;
            this.CompRegistryOption.Location = new System.Drawing.Point(6, 39);
            this.CompRegistryOption.Name = "CompRegistryOption";
            this.CompRegistryOption.Size = new System.Drawing.Size(97, 16);
            this.CompRegistryOption.TabIndex = 1;
            this.CompRegistryOption.TabStop = true;
            this.CompRegistryOption.Text = "Local Registry";
            this.CompRegistryOption.UseVisualStyleBackColor = true;
            this.CompRegistryOption.CheckedChanged += new System.EventHandler(this.CompOptionsCheckedChanged);
            // 
            // CompLocalOption
            // 
            this.CompLocalOption.AutoSize = true;
            this.CompLocalOption.Location = new System.Drawing.Point(6, 18);
            this.CompLocalOption.Name = "CompLocalOption";
            this.CompLocalOption.Size = new System.Drawing.Size(77, 16);
            this.CompLocalOption.TabIndex = 0;
            this.CompLocalOption.TabStop = true;
            this.CompLocalOption.Text = "Local GPO";
            this.CompLocalOption.UseVisualStyleBackColor = true;
            this.CompLocalOption.CheckedChanged += new System.EventHandler(this.CompOptionsCheckedChanged);
            // 
            // UserGroup
            // 
            this.UserGroup.Controls.Add(this.UserRegTextbox);
            this.UserGroup.Controls.Add(this.UserPerUserRegOption);
            this.UserGroup.Controls.Add(this.UserPerUserGpoOption);
            this.UserGroup.Controls.Add(this.UserBrowseHiveButton);
            this.UserGroup.Controls.Add(this.UserNullOption);
            this.UserGroup.Controls.Add(this.UserBrowseGpoButton);
            this.UserGroup.Controls.Add(this.UserHivePathTextbox);
            this.UserGroup.Controls.Add(this.UserFileBrowseButton);
            this.UserGroup.Controls.Add(this.UserGpoSidTextbox);
            this.UserGroup.Controls.Add(this.UserPolFilenameTextbox);
            this.UserGroup.Controls.Add(this.UserFileOption);
            this.UserGroup.Controls.Add(this.UserRegistryOption);
            this.UserGroup.Controls.Add(this.UserLocalOption);
            this.UserGroup.Location = new System.Drawing.Point(242, 11);
            this.UserGroup.Name = "UserGroup";
            this.UserGroup.Size = new System.Drawing.Size(224, 145);
            this.UserGroup.TabIndex = 1;
            this.UserGroup.TabStop = false;
            this.UserGroup.Text = "User";
            // 
            // UserRegTextbox
            // 
            this.UserRegTextbox.Location = new System.Drawing.Point(104, 38);
            this.UserRegTextbox.Name = "UserRegTextbox";
            this.UserRegTextbox.Size = new System.Drawing.Size(114, 19);
            this.UserRegTextbox.TabIndex = 2;
            this.UserRegTextbox.Text = "HKCU";
            // 
            // UserPerUserRegOption
            // 
            this.UserPerUserRegOption.AutoSize = true;
            this.UserPerUserRegOption.Location = new System.Drawing.Point(6, 102);
            this.UserPerUserRegOption.Name = "UserPerUserRegOption";
            this.UserPerUserRegOption.Size = new System.Drawing.Size(72, 16);
            this.UserPerUserRegOption.TabIndex = 9;
            this.UserPerUserRegOption.TabStop = true;
            this.UserPerUserRegOption.Text = "User hive";
            this.UserPerUserRegOption.UseVisualStyleBackColor = true;
            this.UserPerUserRegOption.CheckedChanged += new System.EventHandler(this.UserOptionsCheckedChanged);
            // 
            // UserPerUserGpoOption
            // 
            this.UserPerUserGpoOption.AutoSize = true;
            this.UserPerUserGpoOption.Location = new System.Drawing.Point(6, 81);
            this.UserPerUserGpoOption.Name = "UserPerUserGpoOption";
            this.UserPerUserGpoOption.Size = new System.Drawing.Size(74, 16);
            this.UserPerUserGpoOption.TabIndex = 6;
            this.UserPerUserGpoOption.TabStop = true;
            this.UserPerUserGpoOption.Text = "User GPO";
            this.UserPerUserGpoOption.UseVisualStyleBackColor = true;
            this.UserPerUserGpoOption.CheckedChanged += new System.EventHandler(this.UserOptionsCheckedChanged);
            // 
            // UserBrowseHiveButton
            // 
            this.UserBrowseHiveButton.Location = new System.Drawing.Point(180, 100);
            this.UserBrowseHiveButton.Name = "UserBrowseHiveButton";
            this.UserBrowseHiveButton.Size = new System.Drawing.Size(38, 21);
            this.UserBrowseHiveButton.TabIndex = 11;
            this.UserBrowseHiveButton.Text = "...";
            this.UserBrowseHiveButton.UseVisualStyleBackColor = true;
            this.UserBrowseHiveButton.Click += new System.EventHandler(this.UserBrowseRegistryButton_Click);
            // 
            // UserNullOption
            // 
            this.UserNullOption.AutoSize = true;
            this.UserNullOption.Location = new System.Drawing.Point(6, 124);
            this.UserNullOption.Name = "UserNullOption";
            this.UserNullOption.Size = new System.Drawing.Size(126, 16);
            this.UserNullOption.TabIndex = 12;
            this.UserNullOption.TabStop = true;
            this.UserNullOption.Text = "Scratch space (null)";
            this.UserNullOption.UseVisualStyleBackColor = true;
            this.UserNullOption.CheckedChanged += new System.EventHandler(this.UserOptionsCheckedChanged);
            // 
            // UserBrowseGpoButton
            // 
            this.UserBrowseGpoButton.Location = new System.Drawing.Point(180, 78);
            this.UserBrowseGpoButton.Name = "UserBrowseGpoButton";
            this.UserBrowseGpoButton.Size = new System.Drawing.Size(38, 21);
            this.UserBrowseGpoButton.TabIndex = 8;
            this.UserBrowseGpoButton.Text = "...";
            this.UserBrowseGpoButton.UseVisualStyleBackColor = true;
            this.UserBrowseGpoButton.Click += new System.EventHandler(this.UserBrowseGpoButton_Click);
            // 
            // UserHivePathTextbox
            // 
            this.UserHivePathTextbox.Location = new System.Drawing.Point(85, 102);
            this.UserHivePathTextbox.Name = "UserHivePathTextbox";
            this.UserHivePathTextbox.Size = new System.Drawing.Size(89, 19);
            this.UserHivePathTextbox.TabIndex = 10;
            // 
            // UserFileBrowseButton
            // 
            this.UserFileBrowseButton.Location = new System.Drawing.Point(180, 57);
            this.UserFileBrowseButton.Name = "UserFileBrowseButton";
            this.UserFileBrowseButton.Size = new System.Drawing.Size(38, 21);
            this.UserFileBrowseButton.TabIndex = 5;
            this.UserFileBrowseButton.Text = "...";
            this.UserFileBrowseButton.UseVisualStyleBackColor = true;
            this.UserFileBrowseButton.Click += new System.EventHandler(this.UserFileBrowseButton_Click);
            // 
            // UserGpoSidTextbox
            // 
            this.UserGpoSidTextbox.Location = new System.Drawing.Point(85, 80);
            this.UserGpoSidTextbox.Name = "UserGpoSidTextbox";
            this.UserGpoSidTextbox.Size = new System.Drawing.Size(89, 19);
            this.UserGpoSidTextbox.TabIndex = 7;
            // 
            // UserPolFilenameTextbox
            // 
            this.UserPolFilenameTextbox.Location = new System.Drawing.Point(74, 59);
            this.UserPolFilenameTextbox.Name = "UserPolFilenameTextbox";
            this.UserPolFilenameTextbox.Size = new System.Drawing.Size(100, 19);
            this.UserPolFilenameTextbox.TabIndex = 4;
            // 
            // UserFileOption
            // 
            this.UserFileOption.AutoSize = true;
            this.UserFileOption.Location = new System.Drawing.Point(6, 60);
            this.UserFileOption.Name = "UserFileOption";
            this.UserFileOption.Size = new System.Drawing.Size(64, 16);
            this.UserFileOption.TabIndex = 3;
            this.UserFileOption.TabStop = true;
            this.UserFileOption.Text = "POL file";
            this.UserFileOption.UseVisualStyleBackColor = true;
            this.UserFileOption.CheckedChanged += new System.EventHandler(this.UserOptionsCheckedChanged);
            // 
            // UserRegistryOption
            // 
            this.UserRegistryOption.AutoSize = true;
            this.UserRegistryOption.Location = new System.Drawing.Point(6, 39);
            this.UserRegistryOption.Name = "UserRegistryOption";
            this.UserRegistryOption.Size = new System.Drawing.Size(97, 16);
            this.UserRegistryOption.TabIndex = 1;
            this.UserRegistryOption.TabStop = true;
            this.UserRegistryOption.Text = "Local Registry";
            this.UserRegistryOption.UseVisualStyleBackColor = true;
            this.UserRegistryOption.CheckedChanged += new System.EventHandler(this.UserOptionsCheckedChanged);
            // 
            // UserLocalOption
            // 
            this.UserLocalOption.AutoSize = true;
            this.UserLocalOption.Location = new System.Drawing.Point(6, 18);
            this.UserLocalOption.Name = "UserLocalOption";
            this.UserLocalOption.Size = new System.Drawing.Size(77, 16);
            this.UserLocalOption.TabIndex = 0;
            this.UserLocalOption.TabStop = true;
            this.UserLocalOption.Text = "Local GPO";
            this.UserLocalOption.UseVisualStyleBackColor = true;
            this.UserLocalOption.CheckedChanged += new System.EventHandler(this.UserOptionsCheckedChanged);
            // 
            // OkButton
            // 
            this.OkButton.Location = new System.Drawing.Point(391, 162);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 21);
            this.OkButton.TabIndex = 18;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += new System.EventHandler(this.OkButton_Click);
            // 
            // OpenPol
            // 
            this.AcceptButton = this.OkButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(478, 194);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(this.UserGroup);
            this.Controls.Add(this.ComputerGroup);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OpenPol";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Open Policy Resources";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OpenPol_KeyDown);
            this.ComputerGroup.ResumeLayout(false);
            this.ComputerGroup.PerformLayout();
            this.UserGroup.ResumeLayout(false);
            this.UserGroup.PerformLayout();
            this.ResumeLayout(false);

        }

        internal GroupBox ComputerGroup;
        internal RadioButton CompNullOption;
        internal Button CompFileBrowseButton;
        internal TextBox CompPolFilenameTextbox;
        internal RadioButton CompFileOption;
        internal RadioButton CompRegistryOption;
        internal RadioButton CompLocalOption;
        internal GroupBox UserGroup;
        internal RadioButton UserPerUserRegOption;
        internal RadioButton UserPerUserGpoOption;
        internal Button UserBrowseHiveButton;
        internal RadioButton UserNullOption;
        internal Button UserBrowseGpoButton;
        internal TextBox UserHivePathTextbox;
        internal Button UserFileBrowseButton;
        internal TextBox UserGpoSidTextbox;
        internal TextBox UserPolFilenameTextbox;
        internal RadioButton UserFileOption;
        internal RadioButton UserRegistryOption;
        internal RadioButton UserLocalOption;
        internal Button OkButton;
        internal TextBox CompRegTextbox;
        internal TextBox UserRegTextbox;
    }
}