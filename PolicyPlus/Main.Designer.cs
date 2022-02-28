using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;

namespace PolicyPlus
{
    [DesignerGenerated()]
    public partial class Main : Form
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ColumnHeader ChSettingEnabled;
            System.Windows.Forms.ColumnHeader ChSettingCommented;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator1;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator2;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator3;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator4;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator5;
            System.Windows.Forms.ToolStripStatusLabel ToolStripStatusLabel1;
            System.Windows.Forms.ToolStripStatusLabel ToolStripStatusLabel2;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator6;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            this.MainMenu = new System.Windows.Forms.MenuStrip();
            this.FileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenADMXFolderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenADMXFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SetADMLLanguageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.CloseADMXWorkspaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenPolicyResourcesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.SavePoliciesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.EditRawPOLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ExitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ViewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.EmptyCategoriesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.OnlyFilteredObjectsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.FilterOptionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.DeduplicatePoliciesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.LoadedADMXFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AllProductsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AllSupportDefinitionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.FindToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ByIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ByTextToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ByRegistryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SearchResultsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.FindNextToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ShareToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ImportSemanticPolicyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ImportPOLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ImportREGToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ExportPOLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ExportREGToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.HelpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AcquireADMXFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.SplitContainer = new System.Windows.Forms.SplitContainer();
            this.ComboAppliesTo = new System.Windows.Forms.ComboBox();
            this.CategoriesTree = new System.Windows.Forms.TreeView();
            this.PolicyObjectContext = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.CmeCopyToClipboard = new System.Windows.Forms.ToolStripMenuItem();
            this.Cme2CopyId = new System.Windows.Forms.ToolStripMenuItem();
            this.Cme2CopyName = new System.Windows.Forms.ToolStripMenuItem();
            this.Cme2CopyRegPathLC = new System.Windows.Forms.ToolStripMenuItem();
            this.Cme2CopyRegPathCU = new System.Windows.Forms.ToolStripMenuItem();
            this.CmeCatOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.CmePolEdit = new System.Windows.Forms.ToolStripMenuItem();
            this.CmeAllDetails = new System.Windows.Forms.ToolStripMenuItem();
            this.CmeAllDetailsFormatted = new System.Windows.Forms.ToolStripMenuItem();
            this.CmePolInspectElements = new System.Windows.Forms.ToolStripMenuItem();
            this.CmePolSpolFragment = new System.Windows.Forms.ToolStripMenuItem();
            this.PolicyIcons = new System.Windows.Forms.ImageList(this.components);
            this.SettingInfoPanel = new System.Windows.Forms.Panel();
            this.PolicyInfoTable = new System.Windows.Forms.TableLayoutPanel();
            this.PolicyTitleLabel = new System.Windows.Forms.Label();
            this.PolicySupportedLabel = new System.Windows.Forms.Label();
            this.PolicyDescLabel = new System.Windows.Forms.Label();
            this.PolicyIsPrefTable = new System.Windows.Forms.TableLayoutPanel();
            this.PictureBox1 = new System.Windows.Forms.PictureBox();
            this.PolicyIsPrefLabel = new System.Windows.Forms.Label();
            this.PoliciesList = new System.Windows.Forms.ListView();
            this.ChSettingName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ChSettingID = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.InfoStrip = new System.Windows.Forms.StatusStrip();
            this.ComputerSourceLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.UserSourceLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            ChSettingEnabled = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ChSettingCommented = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ToolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            ToolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            ToolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            ToolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            ToolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            ToolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            ToolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            ToolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.MainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.SplitContainer)).BeginInit();
            this.SplitContainer.Panel1.SuspendLayout();
            this.SplitContainer.Panel2.SuspendLayout();
            this.SplitContainer.SuspendLayout();
            this.PolicyObjectContext.SuspendLayout();
            this.SettingInfoPanel.SuspendLayout();
            this.PolicyInfoTable.SuspendLayout();
            this.PolicyIsPrefTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).BeginInit();
            this.InfoStrip.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // ChSettingEnabled
            // 
            ChSettingEnabled.DisplayIndex = 0;
            ChSettingEnabled.Text = "State";
            ChSettingEnabled.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            ChSettingEnabled.Width = 140;
            // 
            // ChSettingCommented
            // 
            ChSettingCommented.DisplayIndex = 3;
            ChSettingCommented.Text = "Comment";
            ChSettingCommented.Width = 200;
            // 
            // ToolStripSeparator1
            // 
            ToolStripSeparator1.Name = "ToolStripSeparator1";
            ToolStripSeparator1.Size = new System.Drawing.Size(240, 6);
            // 
            // ToolStripSeparator2
            // 
            ToolStripSeparator2.Name = "ToolStripSeparator2";
            ToolStripSeparator2.Size = new System.Drawing.Size(291, 6);
            // 
            // ToolStripSeparator3
            // 
            ToolStripSeparator3.Name = "ToolStripSeparator3";
            ToolStripSeparator3.Size = new System.Drawing.Size(291, 6);
            // 
            // ToolStripSeparator4
            // 
            ToolStripSeparator4.Name = "ToolStripSeparator4";
            ToolStripSeparator4.Size = new System.Drawing.Size(247, 6);
            // 
            // ToolStripSeparator5
            // 
            ToolStripSeparator5.Name = "ToolStripSeparator5";
            ToolStripSeparator5.Size = new System.Drawing.Size(242, 6);
            // 
            // ToolStripStatusLabel1
            // 
            ToolStripStatusLabel1.Name = "ToolStripStatusLabel1";
            ToolStripStatusLabel1.Size = new System.Drawing.Size(125, 20);
            ToolStripStatusLabel1.Text = "Computer source:";
            // 
            // ToolStripStatusLabel2
            // 
            ToolStripStatusLabel2.Name = "ToolStripStatusLabel2";
            ToolStripStatusLabel2.Size = new System.Drawing.Size(88, 20);
            ToolStripStatusLabel2.Text = "User source:";
            // 
            // ToolStripSeparator6
            // 
            ToolStripSeparator6.Name = "ToolStripSeparator6";
            ToolStripSeparator6.Size = new System.Drawing.Size(240, 6);
            // 
            // MainMenu
            // 
            this.MainMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.FileToolStripMenuItem,
            this.ViewToolStripMenuItem,
            this.FindToolStripMenuItem,
            this.ShareToolStripMenuItem,
            this.HelpToolStripMenuItem});
            this.MainMenu.Location = new System.Drawing.Point(0, 0);
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(1483, 28);
            this.MainMenu.TabIndex = 0;
            this.MainMenu.Text = "MenuStrip1";
            // 
            // FileToolStripMenuItem
            // 
            this.FileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OpenADMXFolderToolStripMenuItem,
            this.OpenADMXFileToolStripMenuItem,
            this.SetADMLLanguageToolStripMenuItem,
            this.CloseADMXWorkspaceToolStripMenuItem,
            ToolStripSeparator2,
            this.OpenPolicyResourcesToolStripMenuItem,
            this.toolStripSeparator7,
            this.SavePoliciesToolStripMenuItem,
            this.EditRawPOLToolStripMenuItem,
            ToolStripSeparator3,
            this.ExitToolStripMenuItem});
            this.FileToolStripMenuItem.Name = "FileToolStripMenuItem";
            this.FileToolStripMenuItem.Size = new System.Drawing.Size(46, 24);
            this.FileToolStripMenuItem.Text = "File";
            // 
            // OpenADMXFolderToolStripMenuItem
            // 
            this.OpenADMXFolderToolStripMenuItem.Name = "OpenADMXFolderToolStripMenuItem";
            this.OpenADMXFolderToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.OpenADMXFolderToolStripMenuItem.Text = "Open ADMX Folder";
            this.OpenADMXFolderToolStripMenuItem.Click += new System.EventHandler(this.OpenADMXFolderToolStripMenuItem_Click);
            // 
            // OpenADMXFileToolStripMenuItem
            // 
            this.OpenADMXFileToolStripMenuItem.Name = "OpenADMXFileToolStripMenuItem";
            this.OpenADMXFileToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.OpenADMXFileToolStripMenuItem.Text = "Open ADMX File";
            this.OpenADMXFileToolStripMenuItem.Click += new System.EventHandler(this.OpenADMXFileToolStripMenuItem_Click);
            // 
            // SetADMLLanguageToolStripMenuItem
            // 
            this.SetADMLLanguageToolStripMenuItem.Name = "SetADMLLanguageToolStripMenuItem";
            this.SetADMLLanguageToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.SetADMLLanguageToolStripMenuItem.Text = "Set ADML Language";
            this.SetADMLLanguageToolStripMenuItem.Click += new System.EventHandler(this.SetADMLLanguageToolStripMenuItem_Click);
            // 
            // CloseADMXWorkspaceToolStripMenuItem
            // 
            this.CloseADMXWorkspaceToolStripMenuItem.Name = "CloseADMXWorkspaceToolStripMenuItem";
            this.CloseADMXWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.CloseADMXWorkspaceToolStripMenuItem.Text = "Close ADMX Workspace";
            this.CloseADMXWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.CloseADMXWorkspaceToolStripMenuItem_Click);
            // 
            // OpenPolicyResourcesToolStripMenuItem
            // 
            this.OpenPolicyResourcesToolStripMenuItem.Name = "OpenPolicyResourcesToolStripMenuItem";
            this.OpenPolicyResourcesToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.OpenPolicyResourcesToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.OpenPolicyResourcesToolStripMenuItem.Text = "Open Policy Resources";
            this.OpenPolicyResourcesToolStripMenuItem.Click += new System.EventHandler(this.OpenPolicyResourcesToolStripMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(291, 6);
            // 
            // SavePoliciesToolStripMenuItem
            // 
            this.SavePoliciesToolStripMenuItem.Name = "SavePoliciesToolStripMenuItem";
            this.SavePoliciesToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.SavePoliciesToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.SavePoliciesToolStripMenuItem.Text = "Save Policies";
            this.SavePoliciesToolStripMenuItem.Click += new System.EventHandler(this.SavePoliciesToolStripMenuItem_Click);
            // 
            // EditRawPOLToolStripMenuItem
            // 
            this.EditRawPOLToolStripMenuItem.Name = "EditRawPOLToolStripMenuItem";
            this.EditRawPOLToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.EditRawPOLToolStripMenuItem.Text = "Edit Raw POL";
            this.EditRawPOLToolStripMenuItem.Click += new System.EventHandler(this.EditRawPOLToolStripMenuItem_Click);
            // 
            // ExitToolStripMenuItem
            // 
            this.ExitToolStripMenuItem.Name = "ExitToolStripMenuItem";
            this.ExitToolStripMenuItem.Size = new System.Drawing.Size(294, 26);
            this.ExitToolStripMenuItem.Text = "Exit";
            this.ExitToolStripMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItem_Click);
            // 
            // ViewToolStripMenuItem
            // 
            this.ViewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.EmptyCategoriesToolStripMenuItem,
            this.OnlyFilteredObjectsToolStripMenuItem,
            ToolStripSeparator1,
            this.FilterOptionsToolStripMenuItem,
            this.DeduplicatePoliciesToolStripMenuItem,
            ToolStripSeparator6,
            this.LoadedADMXFilesToolStripMenuItem,
            this.AllProductsToolStripMenuItem,
            this.AllSupportDefinitionsToolStripMenuItem});
            this.ViewToolStripMenuItem.Name = "ViewToolStripMenuItem";
            this.ViewToolStripMenuItem.Size = new System.Drawing.Size(55, 24);
            this.ViewToolStripMenuItem.Text = "View";
            // 
            // EmptyCategoriesToolStripMenuItem
            // 
            this.EmptyCategoriesToolStripMenuItem.Name = "EmptyCategoriesToolStripMenuItem";
            this.EmptyCategoriesToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.EmptyCategoriesToolStripMenuItem.Text = "Empty Categories";
            this.EmptyCategoriesToolStripMenuItem.Click += new System.EventHandler(this.EmptyCategoriesToolStripMenuItem_Click);
            // 
            // OnlyFilteredObjectsToolStripMenuItem
            // 
            this.OnlyFilteredObjectsToolStripMenuItem.Name = "OnlyFilteredObjectsToolStripMenuItem";
            this.OnlyFilteredObjectsToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.OnlyFilteredObjectsToolStripMenuItem.Text = "Only Filtered Policies";
            this.OnlyFilteredObjectsToolStripMenuItem.Click += new System.EventHandler(this.OnlyFilteredObjectsToolStripMenuItem_Click);
            // 
            // FilterOptionsToolStripMenuItem
            // 
            this.FilterOptionsToolStripMenuItem.Name = "FilterOptionsToolStripMenuItem";
            this.FilterOptionsToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.FilterOptionsToolStripMenuItem.Text = "Filter Options";
            this.FilterOptionsToolStripMenuItem.Click += new System.EventHandler(this.FilterOptionsToolStripMenuItem_Click);
            // 
            // DeduplicatePoliciesToolStripMenuItem
            // 
            this.DeduplicatePoliciesToolStripMenuItem.Name = "DeduplicatePoliciesToolStripMenuItem";
            this.DeduplicatePoliciesToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.DeduplicatePoliciesToolStripMenuItem.Text = "Deduplicate Policies";
            this.DeduplicatePoliciesToolStripMenuItem.Visible = false;
            this.DeduplicatePoliciesToolStripMenuItem.Click += new System.EventHandler(this.DeduplicatePoliciesToolStripMenuItem_Click);
            // 
            // LoadedADMXFilesToolStripMenuItem
            // 
            this.LoadedADMXFilesToolStripMenuItem.Name = "LoadedADMXFilesToolStripMenuItem";
            this.LoadedADMXFilesToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.LoadedADMXFilesToolStripMenuItem.Text = "Loaded ADMX Files";
            this.LoadedADMXFilesToolStripMenuItem.Click += new System.EventHandler(this.LoadedADMXFilesToolStripMenuItem_Click);
            // 
            // AllProductsToolStripMenuItem
            // 
            this.AllProductsToolStripMenuItem.Name = "AllProductsToolStripMenuItem";
            this.AllProductsToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.AllProductsToolStripMenuItem.Text = "All Products";
            this.AllProductsToolStripMenuItem.Click += new System.EventHandler(this.AllProductsToolStripMenuItem_Click);
            // 
            // AllSupportDefinitionsToolStripMenuItem
            // 
            this.AllSupportDefinitionsToolStripMenuItem.Name = "AllSupportDefinitionsToolStripMenuItem";
            this.AllSupportDefinitionsToolStripMenuItem.Size = new System.Drawing.Size(243, 26);
            this.AllSupportDefinitionsToolStripMenuItem.Text = "All Support Definitions";
            this.AllSupportDefinitionsToolStripMenuItem.Click += new System.EventHandler(this.AllSupportDefinitionsToolStripMenuItem_Click);
            // 
            // FindToolStripMenuItem
            // 
            this.FindToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ByIDToolStripMenuItem,
            this.ByTextToolStripMenuItem,
            this.ByRegistryToolStripMenuItem,
            ToolStripSeparator4,
            this.SearchResultsToolStripMenuItem,
            this.FindNextToolStripMenuItem});
            this.FindToolStripMenuItem.Name = "FindToolStripMenuItem";
            this.FindToolStripMenuItem.Size = new System.Drawing.Size(51, 24);
            this.FindToolStripMenuItem.Text = "Find";
            // 
            // ByIDToolStripMenuItem
            // 
            this.ByIDToolStripMenuItem.Name = "ByIDToolStripMenuItem";
            this.ByIDToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.G)));
            this.ByIDToolStripMenuItem.Size = new System.Drawing.Size(250, 26);
            this.ByIDToolStripMenuItem.Text = "By ID";
            this.ByIDToolStripMenuItem.Click += new System.EventHandler(this.FindByIDToolStripMenuItem_Click);
            // 
            // ByTextToolStripMenuItem
            // 
            this.ByTextToolStripMenuItem.Name = "ByTextToolStripMenuItem";
            this.ByTextToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.ByTextToolStripMenuItem.Size = new System.Drawing.Size(250, 26);
            this.ByTextToolStripMenuItem.Text = "By Text";
            this.ByTextToolStripMenuItem.Click += new System.EventHandler(this.ByTextToolStripMenuItem_Click);
            // 
            // ByRegistryToolStripMenuItem
            // 
            this.ByRegistryToolStripMenuItem.Name = "ByRegistryToolStripMenuItem";
            this.ByRegistryToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.ByRegistryToolStripMenuItem.Size = new System.Drawing.Size(250, 26);
            this.ByRegistryToolStripMenuItem.Text = "By Registry";
            this.ByRegistryToolStripMenuItem.Click += new System.EventHandler(this.ByRegistryToolStripMenuItem_Click);
            // 
            // SearchResultsToolStripMenuItem
            // 
            this.SearchResultsToolStripMenuItem.Name = "SearchResultsToolStripMenuItem";
            this.SearchResultsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F3)));
            this.SearchResultsToolStripMenuItem.Size = new System.Drawing.Size(250, 26);
            this.SearchResultsToolStripMenuItem.Text = "Search Results";
            this.SearchResultsToolStripMenuItem.Click += new System.EventHandler(this.SearchResultsToolStripMenuItem_Click);
            // 
            // FindNextToolStripMenuItem
            // 
            this.FindNextToolStripMenuItem.Name = "FindNextToolStripMenuItem";
            this.FindNextToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.FindNextToolStripMenuItem.Size = new System.Drawing.Size(250, 26);
            this.FindNextToolStripMenuItem.Text = "Find Next";
            this.FindNextToolStripMenuItem.Click += new System.EventHandler(this.FindNextToolStripMenuItem_Click);
            // 
            // ShareToolStripMenuItem
            // 
            this.ShareToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ImportSemanticPolicyToolStripMenuItem,
            this.ImportPOLToolStripMenuItem,
            this.ImportREGToolStripMenuItem,
            ToolStripSeparator5,
            this.ExportPOLToolStripMenuItem,
            this.ExportREGToolStripMenuItem});
            this.ShareToolStripMenuItem.Name = "ShareToolStripMenuItem";
            this.ShareToolStripMenuItem.Size = new System.Drawing.Size(60, 24);
            this.ShareToolStripMenuItem.Text = "Share";
            // 
            // ImportSemanticPolicyToolStripMenuItem
            // 
            this.ImportSemanticPolicyToolStripMenuItem.Name = "ImportSemanticPolicyToolStripMenuItem";
            this.ImportSemanticPolicyToolStripMenuItem.Size = new System.Drawing.Size(245, 26);
            this.ImportSemanticPolicyToolStripMenuItem.Text = "Import Semantic Policy";
            this.ImportSemanticPolicyToolStripMenuItem.Click += new System.EventHandler(this.ImportSemanticPolicyToolStripMenuItem_Click);
            // 
            // ImportPOLToolStripMenuItem
            // 
            this.ImportPOLToolStripMenuItem.Name = "ImportPOLToolStripMenuItem";
            this.ImportPOLToolStripMenuItem.Size = new System.Drawing.Size(245, 26);
            this.ImportPOLToolStripMenuItem.Text = "Import POL";
            this.ImportPOLToolStripMenuItem.Click += new System.EventHandler(this.ImportPOLToolStripMenuItem_Click);
            // 
            // ImportREGToolStripMenuItem
            // 
            this.ImportREGToolStripMenuItem.Name = "ImportREGToolStripMenuItem";
            this.ImportREGToolStripMenuItem.Size = new System.Drawing.Size(245, 26);
            this.ImportREGToolStripMenuItem.Text = "Import REG";
            this.ImportREGToolStripMenuItem.Click += new System.EventHandler(this.ImportREGToolStripMenuItem_Click);
            // 
            // ExportPOLToolStripMenuItem
            // 
            this.ExportPOLToolStripMenuItem.Name = "ExportPOLToolStripMenuItem";
            this.ExportPOLToolStripMenuItem.Size = new System.Drawing.Size(245, 26);
            this.ExportPOLToolStripMenuItem.Text = "Export POL";
            this.ExportPOLToolStripMenuItem.Click += new System.EventHandler(this.ExportPOLToolStripMenuItem_Click);
            // 
            // ExportREGToolStripMenuItem
            // 
            this.ExportREGToolStripMenuItem.Name = "ExportREGToolStripMenuItem";
            this.ExportREGToolStripMenuItem.Size = new System.Drawing.Size(245, 26);
            this.ExportREGToolStripMenuItem.Text = "Export REG";
            this.ExportREGToolStripMenuItem.Click += new System.EventHandler(this.ExportREGToolStripMenuItem_Click);
            // 
            // HelpToolStripMenuItem
            // 
            this.HelpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.AboutToolStripMenuItem,
            this.AcquireADMXFilesToolStripMenuItem});
            this.HelpToolStripMenuItem.Name = "HelpToolStripMenuItem";
            this.HelpToolStripMenuItem.Size = new System.Drawing.Size(55, 24);
            this.HelpToolStripMenuItem.Text = "Help";
            // 
            // AboutToolStripMenuItem
            // 
            this.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem";
            this.AboutToolStripMenuItem.Size = new System.Drawing.Size(223, 26);
            this.AboutToolStripMenuItem.Text = "About";
            this.AboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            // 
            // AcquireADMXFilesToolStripMenuItem
            // 
            this.AcquireADMXFilesToolStripMenuItem.Name = "AcquireADMXFilesToolStripMenuItem";
            this.AcquireADMXFilesToolStripMenuItem.Size = new System.Drawing.Size(223, 26);
            this.AcquireADMXFilesToolStripMenuItem.Text = "Acquire ADMX Files";
            this.AcquireADMXFilesToolStripMenuItem.Click += new System.EventHandler(this.AcquireADMXFilesToolStripMenuItem_Click);
            // 
            // SplitContainer
            // 
            this.SplitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.SplitContainer.Location = new System.Drawing.Point(0, 28);
            this.SplitContainer.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.SplitContainer.Name = "SplitContainer";
            // 
            // SplitContainer.Panel1
            // 
            this.SplitContainer.Panel1.AutoScroll = true;
            this.SplitContainer.Panel1.Controls.Add(this.panel3);
            this.SplitContainer.Panel1.Controls.Add(this.panel2);
            // 
            // SplitContainer.Panel2
            // 
            this.SplitContainer.Panel2.BackColor = System.Drawing.Color.White;
            this.SplitContainer.Panel2.Controls.Add(this.panel1);
            this.SplitContainer.Panel2.Controls.Add(this.SettingInfoPanel);
            this.SplitContainer.Size = new System.Drawing.Size(1483, 732);
            this.SplitContainer.SplitterDistance = 300;
            this.SplitContainer.SplitterWidth = 5;
            this.SplitContainer.TabIndex = 1;
            this.SplitContainer.TabStop = false;
            // 
            // ComboAppliesTo
            // 
            this.ComboAppliesTo.Dock = System.Windows.Forms.DockStyle.Top;
            this.ComboAppliesTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboAppliesTo.Items.AddRange(new object[] {
            "User or Computer",
            "User",
            "Computer"});
            this.ComboAppliesTo.Location = new System.Drawing.Point(0, 0);
            this.ComboAppliesTo.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.ComboAppliesTo.Name = "ComboAppliesTo";
            this.ComboAppliesTo.Size = new System.Drawing.Size(300, 23);
            this.ComboAppliesTo.TabIndex = 1;
            this.ComboAppliesTo.SelectedIndexChanged += new System.EventHandler(this.ComboAppliesTo_SelectedIndexChanged);
            // 
            // CategoriesTree
            // 
            this.CategoriesTree.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.CategoriesTree.ContextMenuStrip = this.PolicyObjectContext;
            this.CategoriesTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.CategoriesTree.HideSelection = false;
            this.CategoriesTree.ImageIndex = 0;
            this.CategoriesTree.ImageList = this.PolicyIcons;
            this.CategoriesTree.Location = new System.Drawing.Point(0, 0);
            this.CategoriesTree.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.CategoriesTree.Name = "CategoriesTree";
            this.CategoriesTree.SelectedImageIndex = 0;
            this.CategoriesTree.ShowNodeToolTips = true;
            this.CategoriesTree.Size = new System.Drawing.Size(300, 709);
            this.CategoriesTree.TabIndex = 2;
            this.CategoriesTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.CategoriesTree_AfterSelect);
            this.CategoriesTree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.CategoriesTree_NodeMouseClick);
            // 
            // PolicyObjectContext
            // 
            this.PolicyObjectContext.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.PolicyObjectContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.CmeCopyToClipboard,
            this.CmeCatOpen,
            this.CmePolEdit,
            this.CmeAllDetails,
            this.CmeAllDetailsFormatted,
            this.CmePolInspectElements,
            this.CmePolSpolFragment});
            this.PolicyObjectContext.Name = "PolicyObjectContext";
            this.PolicyObjectContext.Size = new System.Drawing.Size(249, 172);
            this.PolicyObjectContext.Opening += new System.ComponentModel.CancelEventHandler(this.PolicyObjectContext_Opening);
            this.PolicyObjectContext.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.PolicyObjectContext_ItemClicked);
            // 
            // CmeCopyToClipboard
            // 
            this.CmeCopyToClipboard.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Cme2CopyId,
            this.Cme2CopyName,
            this.Cme2CopyRegPathLC,
            this.Cme2CopyRegPathCU});
            this.CmeCopyToClipboard.Name = "CmeCopyToClipboard";
            this.CmeCopyToClipboard.Size = new System.Drawing.Size(248, 24);
            this.CmeCopyToClipboard.Tag = "P";
            this.CmeCopyToClipboard.Text = "Copy value";
            this.CmeCopyToClipboard.DropDownOpening += new System.EventHandler(this.PolicyObjectContext_DropdownOpening);
            this.CmeCopyToClipboard.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.PolicyObjectContext_ItemClicked);
            // 
            // Cme2CopyId
            // 
            this.Cme2CopyId.Name = "Cme2CopyId";
            this.Cme2CopyId.Size = new System.Drawing.Size(212, 26);
            this.Cme2CopyId.Text = "ID";
            // 
            // Cme2CopyName
            // 
            this.Cme2CopyName.Name = "Cme2CopyName";
            this.Cme2CopyName.Size = new System.Drawing.Size(212, 26);
            this.Cme2CopyName.Text = "Name";
            // 
            // Cme2CopyRegPathLC
            // 
            this.Cme2CopyRegPathLC.Name = "Cme2CopyRegPathLC";
            this.Cme2CopyRegPathLC.Size = new System.Drawing.Size(212, 26);
            this.Cme2CopyRegPathLC.Tag = "P-LM";
            this.Cme2CopyRegPathLC.Text = "Registry Path - LM";
            // 
            // Cme2CopyRegPathCU
            // 
            this.Cme2CopyRegPathCU.Name = "Cme2CopyRegPathCU";
            this.Cme2CopyRegPathCU.Size = new System.Drawing.Size(212, 26);
            this.Cme2CopyRegPathCU.Tag = "P-CU";
            this.Cme2CopyRegPathCU.Text = "Registry Path - CU";
            // 
            // CmeCatOpen
            // 
            this.CmeCatOpen.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmeCatOpen.Name = "CmeCatOpen";
            this.CmeCatOpen.Size = new System.Drawing.Size(248, 24);
            this.CmeCatOpen.Tag = "C";
            this.CmeCatOpen.Text = "Open";
            // 
            // CmePolEdit
            // 
            this.CmePolEdit.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmePolEdit.Name = "CmePolEdit";
            this.CmePolEdit.Size = new System.Drawing.Size(248, 24);
            this.CmePolEdit.Tag = "P";
            this.CmePolEdit.Text = "Edit";
            // 
            // CmeAllDetails
            // 
            this.CmeAllDetails.Name = "CmeAllDetails";
            this.CmeAllDetails.Size = new System.Drawing.Size(248, 24);
            this.CmeAllDetails.Text = "Details";
            // 
            // CmeAllDetailsFormatted
            // 
            this.CmeAllDetailsFormatted.Name = "CmeAllDetailsFormatted";
            this.CmeAllDetailsFormatted.Size = new System.Drawing.Size(248, 24);
            this.CmeAllDetailsFormatted.Tag = "P";
            this.CmeAllDetailsFormatted.Text = "Details - Formatted";
            // 
            // CmePolInspectElements
            // 
            this.CmePolInspectElements.Name = "CmePolInspectElements";
            this.CmePolInspectElements.Size = new System.Drawing.Size(248, 24);
            this.CmePolInspectElements.Tag = "P";
            this.CmePolInspectElements.Text = "Element Inspector";
            // 
            // CmePolSpolFragment
            // 
            this.CmePolSpolFragment.Name = "CmePolSpolFragment";
            this.CmePolSpolFragment.Size = new System.Drawing.Size(248, 24);
            this.CmePolSpolFragment.Tag = "P";
            this.CmePolSpolFragment.Text = "Semantic Policy Fragment";
            // 
            // PolicyIcons
            // 
            this.PolicyIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("PolicyIcons.ImageStream")));
            this.PolicyIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.PolicyIcons.Images.SetKeyName(0, "folder.png");
            this.PolicyIcons.Images.SetKeyName(1, "folder_error.png");
            this.PolicyIcons.Images.SetKeyName(2, "folder_delete.png");
            this.PolicyIcons.Images.SetKeyName(3, "folder_go.png");
            this.PolicyIcons.Images.SetKeyName(4, "page_white.png");
            this.PolicyIcons.Images.SetKeyName(5, "page_white_gear.png");
            this.PolicyIcons.Images.SetKeyName(6, "arrow_up.png");
            this.PolicyIcons.Images.SetKeyName(7, "page_white_error.png");
            this.PolicyIcons.Images.SetKeyName(8, "delete.png");
            this.PolicyIcons.Images.SetKeyName(9, "arrow_right.png");
            this.PolicyIcons.Images.SetKeyName(10, "package.png");
            this.PolicyIcons.Images.SetKeyName(11, "computer.png");
            this.PolicyIcons.Images.SetKeyName(12, "database.png");
            this.PolicyIcons.Images.SetKeyName(13, "cog.png");
            this.PolicyIcons.Images.SetKeyName(14, "text_allcaps.png");
            this.PolicyIcons.Images.SetKeyName(15, "calculator.png");
            this.PolicyIcons.Images.SetKeyName(16, "cog_edit.png");
            this.PolicyIcons.Images.SetKeyName(17, "accept.png");
            this.PolicyIcons.Images.SetKeyName(18, "cross.png");
            this.PolicyIcons.Images.SetKeyName(19, "application_xp_terminal.png");
            this.PolicyIcons.Images.SetKeyName(20, "application_form.png");
            this.PolicyIcons.Images.SetKeyName(21, "text_align_left.png");
            this.PolicyIcons.Images.SetKeyName(22, "calculator_edit.png");
            this.PolicyIcons.Images.SetKeyName(23, "wrench.png");
            this.PolicyIcons.Images.SetKeyName(24, "textfield.png");
            this.PolicyIcons.Images.SetKeyName(25, "tick.png");
            this.PolicyIcons.Images.SetKeyName(26, "text_horizontalrule.png");
            this.PolicyIcons.Images.SetKeyName(27, "table.png");
            this.PolicyIcons.Images.SetKeyName(28, "table_sort.png");
            this.PolicyIcons.Images.SetKeyName(29, "font_go.png");
            this.PolicyIcons.Images.SetKeyName(30, "application_view_list.png");
            this.PolicyIcons.Images.SetKeyName(31, "brick.png");
            this.PolicyIcons.Images.SetKeyName(32, "error.png");
            this.PolicyIcons.Images.SetKeyName(33, "style.png");
            this.PolicyIcons.Images.SetKeyName(34, "sound_low.png");
            this.PolicyIcons.Images.SetKeyName(35, "arrow_down.png");
            this.PolicyIcons.Images.SetKeyName(36, "style_go.png");
            this.PolicyIcons.Images.SetKeyName(37, "exclamation.png");
            this.PolicyIcons.Images.SetKeyName(38, "application_cascade.png");
            this.PolicyIcons.Images.SetKeyName(39, "page_copy.png");
            this.PolicyIcons.Images.SetKeyName(40, "page.png");
            this.PolicyIcons.Images.SetKeyName(41, "calculator_add.png");
            this.PolicyIcons.Images.SetKeyName(42, "page_go.png");
            // 
            // SettingInfoPanel
            // 
            this.SettingInfoPanel.AutoScroll = true;
            this.SettingInfoPanel.Controls.Add(this.PolicyInfoTable);
            this.SettingInfoPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.SettingInfoPanel.Location = new System.Drawing.Point(878, 0);
            this.SettingInfoPanel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.SettingInfoPanel.Name = "SettingInfoPanel";
            this.SettingInfoPanel.Size = new System.Drawing.Size(300, 732);
            this.SettingInfoPanel.TabIndex = 0;
            this.SettingInfoPanel.ClientSizeChanged += new System.EventHandler(this.SettingInfoPanel_ClientSizeChanged);
            this.SettingInfoPanel.SizeChanged += new System.EventHandler(this.SettingInfoPanel_ClientSizeChanged);
            // 
            // PolicyInfoTable
            // 
            this.PolicyInfoTable.AutoSize = true;
            this.PolicyInfoTable.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.PolicyInfoTable.ColumnCount = 1;
            this.PolicyInfoTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 941F));
            this.PolicyInfoTable.Controls.Add(this.PolicyTitleLabel, 0, 0);
            this.PolicyInfoTable.Controls.Add(this.PolicySupportedLabel, 0, 1);
            this.PolicyInfoTable.Controls.Add(this.PolicyDescLabel, 0, 3);
            this.PolicyInfoTable.Controls.Add(this.PolicyIsPrefTable, 0, 2);
            this.PolicyInfoTable.Location = new System.Drawing.Point(4, 3);
            this.PolicyInfoTable.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.PolicyInfoTable.Name = "PolicyInfoTable";
            this.PolicyInfoTable.RowCount = 5;
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            this.PolicyInfoTable.Size = new System.Drawing.Size(941, 179);
            this.PolicyInfoTable.TabIndex = 0;
            // 
            // PolicyTitleLabel
            // 
            this.PolicyTitleLabel.AutoSize = true;
            this.PolicyTitleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PolicyTitleLabel.Location = new System.Drawing.Point(4, 0);
            this.PolicyTitleLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 28);
            this.PolicyTitleLabel.Name = "PolicyTitleLabel";
            this.PolicyTitleLabel.Size = new System.Drawing.Size(83, 17);
            this.PolicyTitleLabel.TabIndex = 0;
            this.PolicyTitleLabel.Text = "Policy title";
            this.PolicyTitleLabel.UseMnemonic = false;
            // 
            // PolicySupportedLabel
            // 
            this.PolicySupportedLabel.AutoSize = true;
            this.PolicySupportedLabel.Location = new System.Drawing.Point(4, 45);
            this.PolicySupportedLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 28);
            this.PolicySupportedLabel.Name = "PolicySupportedLabel";
            this.PolicySupportedLabel.Size = new System.Drawing.Size(94, 15);
            this.PolicySupportedLabel.TabIndex = 1;
            this.PolicySupportedLabel.Text = "Requirements";
            this.PolicySupportedLabel.UseMnemonic = false;
            // 
            // PolicyDescLabel
            // 
            this.PolicyDescLabel.AutoSize = true;
            this.PolicyDescLabel.Location = new System.Drawing.Point(4, 141);
            this.PolicyDescLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.PolicyDescLabel.Name = "PolicyDescLabel";
            this.PolicyDescLabel.Size = new System.Drawing.Size(119, 15);
            this.PolicyDescLabel.TabIndex = 2;
            this.PolicyDescLabel.Text = "Policy description";
            this.PolicyDescLabel.UseMnemonic = false;
            // 
            // PolicyIsPrefTable
            // 
            this.PolicyIsPrefTable.AutoSize = true;
            this.PolicyIsPrefTable.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.PolicyIsPrefTable.ColumnCount = 2;
            this.PolicyIsPrefTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.PolicyIsPrefTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.PolicyIsPrefTable.Controls.Add(this.PictureBox1, 0, 0);
            this.PolicyIsPrefTable.Controls.Add(this.PolicyIsPrefLabel, 1, 0);
            this.PolicyIsPrefTable.Location = new System.Drawing.Point(4, 91);
            this.PolicyIsPrefTable.Margin = new System.Windows.Forms.Padding(4, 3, 0, 28);
            this.PolicyIsPrefTable.Name = "PolicyIsPrefTable";
            this.PolicyIsPrefTable.RowCount = 1;
            this.PolicyIsPrefTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyIsPrefTable.Size = new System.Drawing.Size(937, 22);
            this.PolicyIsPrefTable.TabIndex = 4;
            // 
            // PictureBox1
            // 
            this.PictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("PictureBox1.Image")));
            this.PictureBox1.Location = new System.Drawing.Point(4, 3);
            this.PictureBox1.Margin = new System.Windows.Forms.Padding(4, 3, 0, 3);
            this.PictureBox1.Name = "PictureBox1";
            this.PictureBox1.Size = new System.Drawing.Size(16, 16);
            this.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.PictureBox1.TabIndex = 0;
            this.PictureBox1.TabStop = false;
            // 
            // PolicyIsPrefLabel
            // 
            this.PolicyIsPrefLabel.AutoSize = true;
            this.PolicyIsPrefLabel.Location = new System.Drawing.Point(24, 0);
            this.PolicyIsPrefLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.PolicyIsPrefLabel.Name = "PolicyIsPrefLabel";
            this.PolicyIsPrefLabel.Size = new System.Drawing.Size(970, 15);
            this.PolicyIsPrefLabel.TabIndex = 1;
            this.PolicyIsPrefLabel.Text = "Because it is not stored in a Policies section of the Registry, this policy is a " +
    "preference and will not be automatically undone if the setting is removed.";
            // 
            // PoliciesList
            // 
            this.PoliciesList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PoliciesList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ChSettingName,
            ChSettingEnabled,
            ChSettingCommented,
            this.ChSettingID});
            this.PoliciesList.ContextMenuStrip = this.PolicyObjectContext;
            this.PoliciesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PoliciesList.FullRowSelect = true;
            this.PoliciesList.HideSelection = false;
            this.PoliciesList.Location = new System.Drawing.Point(0, 0);
            this.PoliciesList.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.PoliciesList.MultiSelect = false;
            this.PoliciesList.Name = "PoliciesList";
            this.PoliciesList.ShowItemToolTips = true;
            this.PoliciesList.Size = new System.Drawing.Size(878, 732);
            this.PoliciesList.SmallImageList = this.PolicyIcons;
            this.PoliciesList.TabIndex = 3;
            this.PoliciesList.UseCompatibleStateImageBehavior = false;
            this.PoliciesList.View = System.Windows.Forms.View.Details;
            this.PoliciesList.SelectedIndexChanged += new System.EventHandler(this.PoliciesList_SelectedIndexChanged);
            this.PoliciesList.SizeChanged += new System.EventHandler(this.ResizePolicyNameColumn);
            this.PoliciesList.DoubleClick += new System.EventHandler(this.PoliciesList_DoubleClick);
            this.PoliciesList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PoliciesList_KeyDown);
            // 
            // ChSettingName
            // 
            this.ChSettingName.DisplayIndex = 1;
            this.ChSettingName.Text = "Name";
            this.ChSettingName.Width = 346;
            // 
            // ChSettingID
            // 
            this.ChSettingID.DisplayIndex = 2;
            this.ChSettingID.Text = "ID";
            this.ChSettingID.Width = 130;
            // 
            // InfoStrip
            // 
            this.InfoStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.InfoStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            ToolStripStatusLabel1,
            this.ComputerSourceLabel,
            ToolStripStatusLabel2,
            this.UserSourceLabel});
            this.InfoStrip.Location = new System.Drawing.Point(0, 763);
            this.InfoStrip.Name = "InfoStrip";
            this.InfoStrip.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.InfoStrip.Size = new System.Drawing.Size(1483, 26);
            this.InfoStrip.TabIndex = 2;
            this.InfoStrip.Text = "StatusStrip1";
            // 
            // ComputerSourceLabel
            // 
            this.ComputerSourceLabel.Name = "ComputerSourceLabel";
            this.ComputerSourceLabel.Size = new System.Drawing.Size(105, 20);
            this.ComputerSourceLabel.Text = "Computer info";
            // 
            // UserSourceLabel
            // 
            this.UserSourceLabel.Name = "UserSourceLabel";
            this.UserSourceLabel.Size = new System.Drawing.Size(68, 20);
            this.UserSourceLabel.Text = "User info";
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.PoliciesList);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(878, 732);
            this.panel1.TabIndex = 4;
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.Controls.Add(this.ComboAppliesTo);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(300, 23);
            this.panel2.TabIndex = 3;
            // 
            // panel3
            // 
            this.panel3.AutoSize = true;
            this.panel3.Controls.Add(this.CategoriesTree);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 23);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(300, 709);
            this.panel3.TabIndex = 4;
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1483, 789);
            this.Controls.Add(this.InfoStrip);
            this.Controls.Add(this.MainMenu);
            this.Controls.Add(this.SplitContainer);
            this.MainMenuStrip = this.MainMenu;
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MinimumSize = new System.Drawing.Size(794, 454);
            this.Name = "Main";
            this.ShowIcon = false;
            this.Text = "Policy Plus";
            this.Closed += new System.EventHandler(this.Main_Closed);
            this.Load += new System.EventHandler(this.Main_Load);
            this.Shown += new System.EventHandler(this.Main_Shown);
            this.SizeChanged += new System.EventHandler(this.ResizePolicyNameColumn);
            this.MainMenu.ResumeLayout(false);
            this.MainMenu.PerformLayout();
            this.SplitContainer.Panel1.ResumeLayout(false);
            this.SplitContainer.Panel1.PerformLayout();
            this.SplitContainer.Panel2.ResumeLayout(false);
            this.SplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.SplitContainer)).EndInit();
            this.SplitContainer.ResumeLayout(false);
            this.PolicyObjectContext.ResumeLayout(false);
            this.SettingInfoPanel.ResumeLayout(false);
            this.SettingInfoPanel.PerformLayout();
            this.PolicyInfoTable.ResumeLayout(false);
            this.PolicyInfoTable.PerformLayout();
            this.PolicyIsPrefTable.ResumeLayout(false);
            this.PolicyIsPrefTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).EndInit();
            this.InfoStrip.ResumeLayout(false);
            this.InfoStrip.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal MenuStrip MainMenu;
        internal ToolStripMenuItem FileToolStripMenuItem;
        internal ToolStripMenuItem OpenADMXFolderToolStripMenuItem;
        internal ToolStripMenuItem OpenADMXFileToolStripMenuItem;
        internal ToolStripMenuItem CloseADMXWorkspaceToolStripMenuItem;
        internal ToolStripMenuItem ExitToolStripMenuItem;
        internal SplitContainer SplitContainer;
        internal TreeView CategoriesTree;
        internal ListView PoliciesList;
        internal Panel SettingInfoPanel;
        internal ImageList PolicyIcons;
        internal TableLayoutPanel PolicyInfoTable;
        internal Label PolicyTitleLabel;
        internal Label PolicySupportedLabel;
        internal Label PolicyDescLabel;
        internal ColumnHeader ChSettingName;
        internal ToolStripMenuItem ViewToolStripMenuItem;
        internal ToolStripMenuItem EmptyCategoriesToolStripMenuItem;
        internal ComboBox ComboAppliesTo;
        internal ToolStripMenuItem DeduplicatePoliciesToolStripMenuItem;
        internal ToolStripMenuItem FindToolStripMenuItem;
        internal ToolStripMenuItem ByIDToolStripMenuItem;
        internal ToolStripMenuItem OpenPolicyResourcesToolStripMenuItem;
        internal ToolStripMenuItem SavePoliciesToolStripMenuItem;
        internal ToolStripMenuItem HelpToolStripMenuItem;
        internal ToolStripMenuItem AboutToolStripMenuItem;
        internal ToolStripMenuItem ByTextToolStripMenuItem;
        internal ToolStripMenuItem ByRegistryToolStripMenuItem;
        internal ToolStripMenuItem SearchResultsToolStripMenuItem;
        internal ToolStripMenuItem FindNextToolStripMenuItem;
        internal ContextMenuStrip PolicyObjectContext;
        internal ToolStripMenuItem CmeCatOpen;
        internal ToolStripMenuItem CmePolEdit;
        internal ToolStripMenuItem CmeAllDetails;
        internal ToolStripMenuItem CmePolInspectElements;
        internal ToolStripMenuItem OnlyFilteredObjectsToolStripMenuItem;
        internal ToolStripMenuItem FilterOptionsToolStripMenuItem;
        internal ToolStripMenuItem ShareToolStripMenuItem;
        internal ToolStripMenuItem ImportSemanticPolicyToolStripMenuItem;
        internal ToolStripMenuItem ImportPOLToolStripMenuItem;
        internal ToolStripMenuItem ExportPOLToolStripMenuItem;
        internal ToolStripMenuItem CmePolSpolFragment;
        internal ToolStripMenuItem AcquireADMXFilesToolStripMenuItem;
        internal StatusStrip InfoStrip;
        internal ToolStripStatusLabel ComputerSourceLabel;
        internal ToolStripStatusLabel UserSourceLabel;
        internal ToolStripMenuItem LoadedADMXFilesToolStripMenuItem;
        internal ToolStripMenuItem AllSupportDefinitionsToolStripMenuItem;
        internal ToolStripMenuItem AllProductsToolStripMenuItem;
        internal ToolStripMenuItem EditRawPOLToolStripMenuItem;
        internal ToolStripMenuItem ExportREGToolStripMenuItem;
        internal ToolStripMenuItem ImportREGToolStripMenuItem;
        internal ToolStripMenuItem SetADMLLanguageToolStripMenuItem;
        internal TableLayoutPanel PolicyIsPrefTable;
        internal PictureBox PictureBox1;
        internal Label PolicyIsPrefLabel;
        private ColumnHeader ChSettingID;
        internal ToolStripMenuItem CmeAllDetailsFormatted;
        private ToolStripSeparator toolStripSeparator7;
        internal ToolStripMenuItem CmeCopyToClipboard;
        internal ToolStripMenuItem Cme2CopyId;
        internal ToolStripMenuItem Cme2CopyName;
        internal ToolStripMenuItem Cme2CopyRegPathLC;
        internal ToolStripMenuItem Cme2CopyRegPathCU;
        private Panel panel1;
        private Panel panel3;
        private Panel panel2;
    }
}