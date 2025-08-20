using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.Main
{
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
    private System.ComponentModel.IContainer components = null;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator1;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator2;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator3;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator4;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator5;
            System.Windows.Forms.ToolStripStatusLabel ToolStripStatusLabel1;
            System.Windows.Forms.ToolStripStatusLabel ToolStripStatusLabel2;
            System.Windows.Forms.ToolStripSeparator ToolStripSeparator6;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
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
            this.panel3 = new System.Windows.Forms.Panel();
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
            this.panel2 = new System.Windows.Forms.Panel();
            this.ComboAppliesTo = new System.Windows.Forms.ComboBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.PoliciesGrid = new System.Windows.Forms.DataGridView();
            this.State = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Icon = new System.Windows.Forms.DataGridViewImageColumn();
            this._Name = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Comment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SettingInfoPanel = new System.Windows.Forms.Panel();
            this.PolicyInfoTable = new System.Windows.Forms.TableLayoutPanel();
            this.PolicyIsPrefTable = new System.Windows.Forms.TableLayoutPanel();
            this.PictureBox1 = new System.Windows.Forms.PictureBox();
            this.PolicyIsPrefLabel = new System.Windows.Forms.TextBox();
            this.PolicySupportedLabel = new System.Windows.Forms.TextBox();
            this.PolicyTitleLabel = new System.Windows.Forms.TextBox();
            this.InfoStrip = new System.Windows.Forms.StatusStrip();
            this.ComputerSourceLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.UserSourceLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.AppVersion = new System.Windows.Forms.ToolStripStatusLabel();
            this.PolicyDescLabel = new System.Windows.Forms.RichTextBox();
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
            this.panel3.SuspendLayout();
            this.PolicyObjectContext.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PoliciesGrid)).BeginInit();
            this.SettingInfoPanel.SuspendLayout();
            this.PolicyInfoTable.SuspendLayout();
            this.PolicyIsPrefTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).BeginInit();
            this.InfoStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // ToolStripSeparator1
            // 
            ToolStripSeparator1.Name = "ToolStripSeparator1";
            ToolStripSeparator1.Size = new System.Drawing.Size(189, 6);
            // 
            // ToolStripSeparator2
            // 
            ToolStripSeparator2.Name = "ToolStripSeparator2";
            ToolStripSeparator2.Size = new System.Drawing.Size(233, 6);
            // 
            // ToolStripSeparator3
            // 
            ToolStripSeparator3.Name = "ToolStripSeparator3";
            ToolStripSeparator3.Size = new System.Drawing.Size(233, 6);
            // 
            // ToolStripSeparator4
            // 
            ToolStripSeparator4.Name = "ToolStripSeparator4";
            ToolStripSeparator4.Size = new System.Drawing.Size(197, 6);
            // 
            // ToolStripSeparator5
            // 
            ToolStripSeparator5.Name = "ToolStripSeparator5";
            ToolStripSeparator5.Size = new System.Drawing.Size(192, 6);
            // 
            // ToolStripStatusLabel1
            // 
            ToolStripStatusLabel1.Name = "ToolStripStatusLabel1";
            ToolStripStatusLabel1.Size = new System.Drawing.Size(100, 17);
            ToolStripStatusLabel1.Text = "Computer source:";
            // 
            // ToolStripStatusLabel2
            // 
            ToolStripStatusLabel2.Name = "ToolStripStatusLabel2";
            ToolStripStatusLabel2.Size = new System.Drawing.Size(71, 17);
            ToolStripStatusLabel2.Text = "User source:";
            // 
            // ToolStripSeparator6
            // 
            ToolStripSeparator6.Name = "ToolStripSeparator6";
            ToolStripSeparator6.Size = new System.Drawing.Size(189, 6);
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
            this.MainMenu.Padding = new System.Windows.Forms.Padding(5, 2, 0, 2);
            this.MainMenu.Size = new System.Drawing.Size(1186, 24);
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
            this.FileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.FileToolStripMenuItem.Text = "File";
            // 
            // OpenADMXFolderToolStripMenuItem
            // 
            this.OpenADMXFolderToolStripMenuItem.Name = "OpenADMXFolderToolStripMenuItem";
            this.OpenADMXFolderToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.OpenADMXFolderToolStripMenuItem.Text = "Open ADMX Folder";
            this.OpenADMXFolderToolStripMenuItem.Click += new System.EventHandler(this.OpenADMXFolderToolStripMenuItem_Click);
            // 
            // OpenADMXFileToolStripMenuItem
            // 
            this.OpenADMXFileToolStripMenuItem.Name = "OpenADMXFileToolStripMenuItem";
            this.OpenADMXFileToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.OpenADMXFileToolStripMenuItem.Text = "Open ADMX File";
            this.OpenADMXFileToolStripMenuItem.Click += new System.EventHandler(this.OpenADMXFileToolStripMenuItem_Click);
            // 
            // SetADMLLanguageToolStripMenuItem
            // 
            this.SetADMLLanguageToolStripMenuItem.Name = "SetADMLLanguageToolStripMenuItem";
            this.SetADMLLanguageToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.SetADMLLanguageToolStripMenuItem.Text = "Set ADML Language";
            this.SetADMLLanguageToolStripMenuItem.Click += new System.EventHandler(this.SetADMLLanguageToolStripMenuItem_Click);
            // 
            // CloseADMXWorkspaceToolStripMenuItem
            // 
            this.CloseADMXWorkspaceToolStripMenuItem.Name = "CloseADMXWorkspaceToolStripMenuItem";
            this.CloseADMXWorkspaceToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.CloseADMXWorkspaceToolStripMenuItem.Text = "Close ADMX Workspace";
            this.CloseADMXWorkspaceToolStripMenuItem.Click += new System.EventHandler(this.CloseADMXWorkspaceToolStripMenuItem_Click);
            // 
            // OpenPolicyResourcesToolStripMenuItem
            // 
            this.OpenPolicyResourcesToolStripMenuItem.Name = "OpenPolicyResourcesToolStripMenuItem";
            this.OpenPolicyResourcesToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.OpenPolicyResourcesToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.OpenPolicyResourcesToolStripMenuItem.Text = "Open Policy Resources";
            this.OpenPolicyResourcesToolStripMenuItem.Click += new System.EventHandler(this.OpenPolicyResourcesToolStripMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(233, 6);
            // 
            // SavePoliciesToolStripMenuItem
            // 
            this.SavePoliciesToolStripMenuItem.Name = "SavePoliciesToolStripMenuItem";
            this.SavePoliciesToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.SavePoliciesToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.SavePoliciesToolStripMenuItem.Text = "Save Policies";
            this.SavePoliciesToolStripMenuItem.Click += new System.EventHandler(this.SavePoliciesToolStripMenuItem_Click);
            // 
            // EditRawPOLToolStripMenuItem
            // 
            this.EditRawPOLToolStripMenuItem.Name = "EditRawPOLToolStripMenuItem";
            this.EditRawPOLToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
            this.EditRawPOLToolStripMenuItem.Text = "Edit Raw POL";
            this.EditRawPOLToolStripMenuItem.Click += new System.EventHandler(this.EditRawPOLToolStripMenuItem_Click);
            // 
            // ExitToolStripMenuItem
            // 
            this.ExitToolStripMenuItem.Name = "ExitToolStripMenuItem";
            this.ExitToolStripMenuItem.Size = new System.Drawing.Size(236, 22);
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
            this.ViewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.ViewToolStripMenuItem.Text = "View";
            // 
            // EmptyCategoriesToolStripMenuItem
            // 
            this.EmptyCategoriesToolStripMenuItem.Name = "EmptyCategoriesToolStripMenuItem";
            this.EmptyCategoriesToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.EmptyCategoriesToolStripMenuItem.Text = "Empty Categories";
            this.EmptyCategoriesToolStripMenuItem.Click += new System.EventHandler(this.EmptyCategoriesToolStripMenuItem_Click);
            // 
            // OnlyFilteredObjectsToolStripMenuItem
            // 
            this.OnlyFilteredObjectsToolStripMenuItem.Name = "OnlyFilteredObjectsToolStripMenuItem";
            this.OnlyFilteredObjectsToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.OnlyFilteredObjectsToolStripMenuItem.Text = "Only Filtered Policies";
            this.OnlyFilteredObjectsToolStripMenuItem.Click += new System.EventHandler(this.OnlyFilteredObjectsToolStripMenuItem_Click);
            // 
            // FilterOptionsToolStripMenuItem
            // 
            this.FilterOptionsToolStripMenuItem.Name = "FilterOptionsToolStripMenuItem";
            this.FilterOptionsToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.FilterOptionsToolStripMenuItem.Text = "Filter Options";
            this.FilterOptionsToolStripMenuItem.Click += new System.EventHandler(this.FilterOptionsToolStripMenuItem_Click);
            // 
            // DeduplicatePoliciesToolStripMenuItem
            // 
            this.DeduplicatePoliciesToolStripMenuItem.Name = "DeduplicatePoliciesToolStripMenuItem";
            this.DeduplicatePoliciesToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.DeduplicatePoliciesToolStripMenuItem.Text = "Deduplicate Policies";
            this.DeduplicatePoliciesToolStripMenuItem.Visible = false;
            this.DeduplicatePoliciesToolStripMenuItem.Click += new System.EventHandler(this.DeduplicatePoliciesToolStripMenuItem_Click);
            // 
            // LoadedADMXFilesToolStripMenuItem
            // 
            this.LoadedADMXFilesToolStripMenuItem.Name = "LoadedADMXFilesToolStripMenuItem";
            this.LoadedADMXFilesToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.LoadedADMXFilesToolStripMenuItem.Text = "Loaded ADMX Files";
            this.LoadedADMXFilesToolStripMenuItem.Click += new System.EventHandler(this.LoadedADMXFilesToolStripMenuItem_Click);
            // 
            // AllProductsToolStripMenuItem
            // 
            this.AllProductsToolStripMenuItem.Name = "AllProductsToolStripMenuItem";
            this.AllProductsToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
            this.AllProductsToolStripMenuItem.Text = "All Products";
            this.AllProductsToolStripMenuItem.Click += new System.EventHandler(this.AllProductsToolStripMenuItem_Click);
            // 
            // AllSupportDefinitionsToolStripMenuItem
            // 
            this.AllSupportDefinitionsToolStripMenuItem.Name = "AllSupportDefinitionsToolStripMenuItem";
            this.AllSupportDefinitionsToolStripMenuItem.Size = new System.Drawing.Size(192, 22);
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
            this.FindToolStripMenuItem.Size = new System.Drawing.Size(42, 20);
            this.FindToolStripMenuItem.Text = "Find";
            // 
            // ByIDToolStripMenuItem
            // 
            this.ByIDToolStripMenuItem.Name = "ByIDToolStripMenuItem";
            this.ByIDToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.G)));
            this.ByIDToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.ByIDToolStripMenuItem.Text = "By ID";
            this.ByIDToolStripMenuItem.Click += new System.EventHandler(this.FindByIDToolStripMenuItem_Click);
            // 
            // ByTextToolStripMenuItem
            // 
            this.ByTextToolStripMenuItem.Name = "ByTextToolStripMenuItem";
            this.ByTextToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.ByTextToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.ByTextToolStripMenuItem.Text = "By Text";
            this.ByTextToolStripMenuItem.Click += new System.EventHandler(this.ByTextToolStripMenuItem_Click);
            // 
            // ByRegistryToolStripMenuItem
            // 
            this.ByRegistryToolStripMenuItem.Name = "ByRegistryToolStripMenuItem";
            this.ByRegistryToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.ByRegistryToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.ByRegistryToolStripMenuItem.Text = "By Registry";
            this.ByRegistryToolStripMenuItem.Click += new System.EventHandler(this.ByRegistryToolStripMenuItem_Click);
            // 
            // SearchResultsToolStripMenuItem
            // 
            this.SearchResultsToolStripMenuItem.Name = "SearchResultsToolStripMenuItem";
            this.SearchResultsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F3)));
            this.SearchResultsToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
            this.SearchResultsToolStripMenuItem.Text = "Search Results";
            this.SearchResultsToolStripMenuItem.Click += new System.EventHandler(this.SearchResultsToolStripMenuItem_Click);
            // 
            // FindNextToolStripMenuItem
            // 
            this.FindNextToolStripMenuItem.Name = "FindNextToolStripMenuItem";
            this.FindNextToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F3;
            this.FindNextToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
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
            this.ShareToolStripMenuItem.Size = new System.Drawing.Size(48, 20);
            this.ShareToolStripMenuItem.Text = "Share";
            // 
            // ImportSemanticPolicyToolStripMenuItem
            // 
            this.ImportSemanticPolicyToolStripMenuItem.Name = "ImportSemanticPolicyToolStripMenuItem";
            this.ImportSemanticPolicyToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.ImportSemanticPolicyToolStripMenuItem.Text = "Import Semantic Policy";
            this.ImportSemanticPolicyToolStripMenuItem.Click += new System.EventHandler(this.ImportSemanticPolicyToolStripMenuItem_Click);
            // 
            // ImportPOLToolStripMenuItem
            // 
            this.ImportPOLToolStripMenuItem.Name = "ImportPOLToolStripMenuItem";
            this.ImportPOLToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.ImportPOLToolStripMenuItem.Text = "Import POL";
            this.ImportPOLToolStripMenuItem.Click += new System.EventHandler(this.ImportPOLToolStripMenuItem_Click);
            // 
            // ImportREGToolStripMenuItem
            // 
            this.ImportREGToolStripMenuItem.Name = "ImportREGToolStripMenuItem";
            this.ImportREGToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.ImportREGToolStripMenuItem.Text = "Import REG";
            this.ImportREGToolStripMenuItem.Click += new System.EventHandler(this.ImportREGToolStripMenuItem_Click);
            // 
            // ExportPOLToolStripMenuItem
            // 
            this.ExportPOLToolStripMenuItem.Name = "ExportPOLToolStripMenuItem";
            this.ExportPOLToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.ExportPOLToolStripMenuItem.Text = "Export POL";
            this.ExportPOLToolStripMenuItem.Click += new System.EventHandler(this.ExportPOLToolStripMenuItem_Click);
            // 
            // ExportREGToolStripMenuItem
            // 
            this.ExportREGToolStripMenuItem.Name = "ExportREGToolStripMenuItem";
            this.ExportREGToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.ExportREGToolStripMenuItem.Text = "Export REG";
            this.ExportREGToolStripMenuItem.Click += new System.EventHandler(this.ExportREGToolStripMenuItem_Click);
            // 
            // HelpToolStripMenuItem
            // 
            this.HelpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.AboutToolStripMenuItem,
            this.AcquireADMXFilesToolStripMenuItem});
            this.HelpToolStripMenuItem.Name = "HelpToolStripMenuItem";
            this.HelpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.HelpToolStripMenuItem.Text = "Help";
            // 
            // AboutToolStripMenuItem
            // 
            this.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem";
            this.AboutToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
            this.AboutToolStripMenuItem.Text = "About";
            this.AboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            // 
            // AcquireADMXFilesToolStripMenuItem
            // 
            this.AcquireADMXFilesToolStripMenuItem.Name = "AcquireADMXFilesToolStripMenuItem";
            this.AcquireADMXFilesToolStripMenuItem.Size = new System.Drawing.Size(178, 22);
            this.AcquireADMXFilesToolStripMenuItem.Text = "Acquire ADMX Files";
            this.AcquireADMXFilesToolStripMenuItem.Click += new System.EventHandler(this.AcquireADMXFilesToolStripMenuItem_Click);
            // 
            // SplitContainer
            // 
            this.SplitContainer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SplitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.SplitContainer.Location = new System.Drawing.Point(0, 22);
            this.SplitContainer.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
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
            this.SplitContainer.Size = new System.Drawing.Size(1186, 586);
            this.SplitContainer.SplitterDistance = 300;
            this.SplitContainer.TabIndex = 1;
            this.SplitContainer.TabStop = false;
            // 
            // panel3
            // 
            this.panel3.AutoSize = true;
            this.panel3.Controls.Add(this.CategoriesTree);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 20);
            this.panel3.Margin = new System.Windows.Forms.Padding(2);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(300, 566);
            this.panel3.TabIndex = 4;
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
            this.CategoriesTree.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.CategoriesTree.Name = "CategoriesTree";
            this.CategoriesTree.SelectedImageIndex = 0;
            this.CategoriesTree.ShowNodeToolTips = true;
            this.CategoriesTree.Size = new System.Drawing.Size(300, 566);
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
            this.PolicyObjectContext.Size = new System.Drawing.Size(210, 158);
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
            this.CmeCopyToClipboard.Size = new System.Drawing.Size(209, 22);
            this.CmeCopyToClipboard.Tag = "P";
            this.CmeCopyToClipboard.Text = "Copy value";
            this.CmeCopyToClipboard.DropDownOpening += new System.EventHandler(this.PolicyObjectContext_DropdownOpening);
            this.CmeCopyToClipboard.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.PolicyObjectContext_ItemClicked);
            // 
            // Cme2CopyId
            // 
            this.Cme2CopyId.Name = "Cme2CopyId";
            this.Cme2CopyId.Size = new System.Drawing.Size(171, 22);
            this.Cme2CopyId.Text = "ID";
            // 
            // Cme2CopyName
            // 
            this.Cme2CopyName.Name = "Cme2CopyName";
            this.Cme2CopyName.Size = new System.Drawing.Size(171, 22);
            this.Cme2CopyName.Text = "Name";
            // 
            // Cme2CopyRegPathLC
            // 
            this.Cme2CopyRegPathLC.Name = "Cme2CopyRegPathLC";
            this.Cme2CopyRegPathLC.Size = new System.Drawing.Size(171, 22);
            this.Cme2CopyRegPathLC.Tag = "P-LM";
            this.Cme2CopyRegPathLC.Text = "Registry Path - LM";
            // 
            // Cme2CopyRegPathCU
            // 
            this.Cme2CopyRegPathCU.Name = "Cme2CopyRegPathCU";
            this.Cme2CopyRegPathCU.Size = new System.Drawing.Size(171, 22);
            this.Cme2CopyRegPathCU.Tag = "P-CU";
            this.Cme2CopyRegPathCU.Text = "Registry Path - CU";
            // 
            // CmeCatOpen
            // 
            this.CmeCatOpen.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmeCatOpen.Name = "CmeCatOpen";
            this.CmeCatOpen.Size = new System.Drawing.Size(209, 22);
            this.CmeCatOpen.Tag = "C";
            this.CmeCatOpen.Text = "Open";
            // 
            // CmePolEdit
            // 
            this.CmePolEdit.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CmePolEdit.Name = "CmePolEdit";
            this.CmePolEdit.Size = new System.Drawing.Size(209, 22);
            this.CmePolEdit.Tag = "P";
            this.CmePolEdit.Text = "Edit";
            // 
            // CmeAllDetails
            // 
            this.CmeAllDetails.Name = "CmeAllDetails";
            this.CmeAllDetails.Size = new System.Drawing.Size(209, 22);
            this.CmeAllDetails.Text = "Details";
            // 
            // CmeAllDetailsFormatted
            // 
            this.CmeAllDetailsFormatted.Name = "CmeAllDetailsFormatted";
            this.CmeAllDetailsFormatted.Size = new System.Drawing.Size(209, 22);
            this.CmeAllDetailsFormatted.Tag = "P";
            this.CmeAllDetailsFormatted.Text = "Details - Formatted";
            // 
            // CmePolInspectElements
            // 
            this.CmePolInspectElements.Name = "CmePolInspectElements";
            this.CmePolInspectElements.Size = new System.Drawing.Size(209, 22);
            this.CmePolInspectElements.Tag = "P";
            this.CmePolInspectElements.Text = "Element Inspector";
            // 
            // CmePolSpolFragment
            // 
            this.CmePolSpolFragment.Name = "CmePolSpolFragment";
            this.CmePolSpolFragment.Size = new System.Drawing.Size(209, 22);
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
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.Controls.Add(this.ComboAppliesTo);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Margin = new System.Windows.Forms.Padding(2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(300, 20);
            this.panel2.TabIndex = 3;
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
            this.ComboAppliesTo.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.ComboAppliesTo.Name = "ComboAppliesTo";
            this.ComboAppliesTo.Size = new System.Drawing.Size(300, 20);
            this.ComboAppliesTo.TabIndex = 1;
            this.ComboAppliesTo.SelectedIndexChanged += new System.EventHandler(this.ComboAppliesTo_SelectedIndexChanged);
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.PoliciesGrid);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(642, 586);
            this.panel1.TabIndex = 4;
            // 
            // PoliciesGrid
            // 
            this.PoliciesGrid.AllowUserToAddRows = false;
            this.PoliciesGrid.AllowUserToDeleteRows = false;
            this.PoliciesGrid.AllowUserToResizeRows = false;
            this.PoliciesGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.PoliciesGrid.BackgroundColor = System.Drawing.SystemColors.Window;
            this.PoliciesGrid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PoliciesGrid.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.PoliciesGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.PoliciesGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.State,
            this.Icon,
            this._Name,
            this.ID,
            this.Comment});
            this.PoliciesGrid.ContextMenuStrip = this.PolicyObjectContext;
            this.PoliciesGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PoliciesGrid.Location = new System.Drawing.Point(0, 0);
            this.PoliciesGrid.Margin = new System.Windows.Forms.Padding(2);
            this.PoliciesGrid.MultiSelect = false;
            this.PoliciesGrid.Name = "PoliciesGrid";
            this.PoliciesGrid.ReadOnly = true;
            this.PoliciesGrid.RowHeadersVisible = false;
            this.PoliciesGrid.RowHeadersWidth = 51;
            this.PoliciesGrid.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.PoliciesGrid.RowTemplate.Height = 18;
            this.PoliciesGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.PoliciesGrid.Size = new System.Drawing.Size(642, 586);
            this.PoliciesGrid.TabIndex = 4;
            this.PoliciesGrid.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.PoliciesGrid_CellContentClick);
            this.PoliciesGrid.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.PoliciesGrid_CellContentClick);
            this.PoliciesGrid.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.PoliciesGrid_CellContentDoubleClick);
            this.PoliciesGrid.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.PoliciesGrid_CellContentDoubleClick);
            this.PoliciesGrid.SelectionChanged += new System.EventHandler(this.PoliciesGrid_SelectionChanged);
            // 
            // State
            // 
            this.State.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCells;
            this.State.ContextMenuStrip = this.PolicyObjectContext;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.Padding = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.State.DefaultCellStyle = dataGridViewCellStyle3;
            this.State.Frozen = true;
            this.State.HeaderText = "State";
            this.State.MinimumWidth = 6;
            this.State.Name = "State";
            this.State.ReadOnly = true;
            this.State.Width = 57;
            // 
            // Icon
            // 
            this.Icon.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCells;
            this.Icon.ContextMenuStrip = this.PolicyObjectContext;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.NullValue = "null";
            this.Icon.DefaultCellStyle = dataGridViewCellStyle1;
            this.Icon.HeaderText = " ";
            this.Icon.ImageLayout = System.Windows.Forms.DataGridViewImageCellLayout.Zoom;
            this.Icon.MinimumWidth = 6;
            this.Icon.Name = "Icon";
            this.Icon.ReadOnly = true;
            this.Icon.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Icon.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.Icon.Width = 34;
            // 
            // _Name
            // 
            this._Name.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this._Name.ContextMenuStrip = this.PolicyObjectContext;
            this._Name.FillWeight = 26.31579F;
            this._Name.HeaderText = "Name";
            this._Name.MinimumWidth = 6;
            this._Name.Name = "_Name";
            this._Name.ReadOnly = true;
            // 
            // ID
            // 
            this.ID.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.ID.ContextMenuStrip = this.PolicyObjectContext;
            this.ID.FillWeight = 13.15789F;
            this.ID.HeaderText = "ID";
            this.ID.MinimumWidth = 6;
            this.ID.Name = "ID";
            this.ID.ReadOnly = true;
            // 
            // Comment
            // 
            this.Comment.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.Comment.ContextMenuStrip = this.PolicyObjectContext;
            this.Comment.FillWeight = 210.5263F;
            this.Comment.HeaderText = "Comment";
            this.Comment.MinimumWidth = 6;
            this.Comment.Name = "Comment";
            this.Comment.ReadOnly = true;
            this.Comment.Visible = false;
            this.Comment.Width = 125;
            // 
            // SettingInfoPanel
            // 
            this.SettingInfoPanel.AutoScroll = true;
            this.SettingInfoPanel.Controls.Add(this.PolicyInfoTable);
            this.SettingInfoPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.SettingInfoPanel.Location = new System.Drawing.Point(642, 0);
            this.SettingInfoPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.SettingInfoPanel.Name = "SettingInfoPanel";
            this.SettingInfoPanel.Size = new System.Drawing.Size(240, 586);
            this.SettingInfoPanel.TabIndex = 0;
            this.SettingInfoPanel.ClientSizeChanged += new System.EventHandler(this.SettingInfoPanel_ClientSizeChanged);
            this.SettingInfoPanel.SizeChanged += new System.EventHandler(this.SettingInfoPanel_ClientSizeChanged);
            // 
            // PolicyInfoTable
            // 
            this.PolicyInfoTable.AutoSize = true;
            this.PolicyInfoTable.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.PolicyInfoTable.ColumnCount = 1;
            this.PolicyInfoTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.PolicyInfoTable.Controls.Add(this.PolicyIsPrefTable, 0, 2);
            this.PolicyInfoTable.Controls.Add(this.PolicySupportedLabel, 0, 1);
            this.PolicyInfoTable.Controls.Add(this.PolicyTitleLabel, 0, 0);
            this.PolicyInfoTable.Controls.Add(this.PolicyDescLabel, 0, 4);
            this.PolicyInfoTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PolicyInfoTable.Location = new System.Drawing.Point(0, 0);
            this.PolicyInfoTable.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.PolicyInfoTable.Name = "PolicyInfoTable";
            this.PolicyInfoTable.RowCount = 5;
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 8F));
            this.PolicyInfoTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyInfoTable.Size = new System.Drawing.Size(240, 586);
            this.PolicyInfoTable.TabIndex = 0;
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
            this.PolicyIsPrefTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PolicyIsPrefTable.Location = new System.Drawing.Point(3, 86);
            this.PolicyIsPrefTable.Margin = new System.Windows.Forms.Padding(3, 2, 0, 22);
            this.PolicyIsPrefTable.Name = "PolicyIsPrefTable";
            this.PolicyIsPrefTable.RowCount = 1;
            this.PolicyIsPrefTable.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.PolicyIsPrefTable.Size = new System.Drawing.Size(239, 20);
            this.PolicyIsPrefTable.TabIndex = 4;
            // 
            // PictureBox1
            // 
            this.PictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("PictureBox1.Image")));
            this.PictureBox1.Location = new System.Drawing.Point(3, 2);
            this.PictureBox1.Margin = new System.Windows.Forms.Padding(3, 2, 0, 2);
            this.PictureBox1.Name = "PictureBox1";
            this.PictureBox1.Size = new System.Drawing.Size(16, 16);
            this.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.PictureBox1.TabIndex = 0;
            this.PictureBox1.TabStop = false;
            // 
            // PolicyIsPrefLabel
            // 
            this.PolicyIsPrefLabel.BackColor = System.Drawing.SystemColors.Window;
            this.PolicyIsPrefLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PolicyIsPrefLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PolicyIsPrefLabel.Location = new System.Drawing.Point(21, 2);
            this.PolicyIsPrefLabel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.PolicyIsPrefLabel.Multiline = true;
            this.PolicyIsPrefLabel.Name = "PolicyIsPrefLabel";
            this.PolicyIsPrefLabel.ReadOnly = true;
            this.PolicyIsPrefLabel.Size = new System.Drawing.Size(216, 16);
            this.PolicyIsPrefLabel.TabIndex = 1;
            // 
            // PolicySupportedLabel
            // 
            this.PolicySupportedLabel.BackColor = System.Drawing.SystemColors.Window;
            this.PolicySupportedLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PolicySupportedLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PolicySupportedLabel.Location = new System.Drawing.Point(2, 46);
            this.PolicySupportedLabel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.PolicySupportedLabel.Multiline = true;
            this.PolicySupportedLabel.Name = "PolicySupportedLabel";
            this.PolicySupportedLabel.ReadOnly = true;
            this.PolicySupportedLabel.Size = new System.Drawing.Size(238, 36);
            this.PolicySupportedLabel.TabIndex = 6;
            this.PolicySupportedLabel.Text = "Policy Supported";
            // 
            // PolicyTitleLabel
            // 
            this.PolicyTitleLabel.BackColor = System.Drawing.SystemColors.Window;
            this.PolicyTitleLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PolicyTitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.PolicyTitleLabel.Font = new System.Drawing.Font("MS UI Gothic", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
            this.PolicyTitleLabel.Location = new System.Drawing.Point(2, 2);
            this.PolicyTitleLabel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.PolicyTitleLabel.MinimumSize = new System.Drawing.Size(0, 24);
            this.PolicyTitleLabel.Multiline = true;
            this.PolicyTitleLabel.Name = "PolicyTitleLabel";
            this.PolicyTitleLabel.ReadOnly = true;
            this.PolicyTitleLabel.Size = new System.Drawing.Size(238, 40);
            this.PolicyTitleLabel.TabIndex = 7;
            this.PolicyTitleLabel.Text = "Policy Title";
            // 
            // InfoStrip
            // 
            this.InfoStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.InfoStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            ToolStripStatusLabel1,
            this.ComputerSourceLabel,
            ToolStripStatusLabel2,
            this.UserSourceLabel,
            this.AppVersion});
            this.InfoStrip.Location = new System.Drawing.Point(0, 609);
            this.InfoStrip.Name = "InfoStrip";
            this.InfoStrip.Padding = new System.Windows.Forms.Padding(1, 0, 15, 0);
            this.InfoStrip.Size = new System.Drawing.Size(1186, 22);
            this.InfoStrip.TabIndex = 2;
            this.InfoStrip.Text = "StatusStrip1";
            // 
            // ComputerSourceLabel
            // 
            this.ComputerSourceLabel.Name = "ComputerSourceLabel";
            this.ComputerSourceLabel.Size = new System.Drawing.Size(83, 17);
            this.ComputerSourceLabel.Text = "Computer info";
            // 
            // UserSourceLabel
            // 
            this.UserSourceLabel.Name = "UserSourceLabel";
            this.UserSourceLabel.Size = new System.Drawing.Size(54, 17);
            this.UserSourceLabel.Text = "User info";
            // 
            // AppVersion
            // 
            this.AppVersion.Name = "AppVersion";
            this.AppVersion.Size = new System.Drawing.Size(862, 17);
            this.AppVersion.Spring = true;
            this.AppVersion.Text = "version";
            this.AppVersion.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.AppVersion.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            // 
            // PolicyDescLabel
            // 
            this.PolicyDescLabel.BackColor = System.Drawing.SystemColors.Window;
            this.PolicyDescLabel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.PolicyDescLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PolicyDescLabel.Location = new System.Drawing.Point(3, 139);
            this.PolicyDescLabel.Name = "PolicyDescLabel";
            this.PolicyDescLabel.ReadOnly = true;
            this.PolicyDescLabel.Size = new System.Drawing.Size(236, 444);
            this.PolicyDescLabel.TabIndex = 8;
            this.PolicyDescLabel.Text = "Policy Desc";
            this.PolicyDescLabel.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.PolicyDescLabel_LinkClicked);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1186, 631);
            this.Controls.Add(this.InfoStrip);
            this.Controls.Add(this.MainMenu);
            this.Controls.Add(this.SplitContainer);
            this.MainMenuStrip = this.MainMenu;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimumSize = new System.Drawing.Size(638, 369);
            this.Name = "Main";
            this.ShowIcon = false;
            this.Text = "Policy Plus";
            this.Closed += new System.EventHandler(this.Main_Closed);
            this.Load += new System.EventHandler(this.Main_Load);
            this.Shown += new System.EventHandler(this.Main_Shown);
            this.MainMenu.ResumeLayout(false);
            this.MainMenu.PerformLayout();
            this.SplitContainer.Panel1.ResumeLayout(false);
            this.SplitContainer.Panel1.PerformLayout();
            this.SplitContainer.Panel2.ResumeLayout(false);
            this.SplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.SplitContainer)).EndInit();
            this.SplitContainer.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.PolicyObjectContext.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.PoliciesGrid)).EndInit();
            this.SettingInfoPanel.ResumeLayout(false);
            this.SettingInfoPanel.PerformLayout();
            this.PolicyInfoTable.ResumeLayout(false);
            this.PolicyInfoTable.PerformLayout();
            this.PolicyIsPrefTable.ResumeLayout(false);
            this.PolicyIsPrefTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).EndInit();
            this.InfoStrip.ResumeLayout(false);
            this.InfoStrip.PerformLayout();
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
        internal Panel SettingInfoPanel;
        internal ImageList PolicyIcons;
        internal TableLayoutPanel PolicyInfoTable;
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
        private ToolStripStatusLabel AppVersion;
        private DataGridView PoliciesGrid;
    private DataGridViewTextBoxColumn State;
    private new DataGridViewImageColumn Icon; // 'new' to avoid CS0108 name hiding warning
        private DataGridViewTextBoxColumn _Name;
        private DataGridViewTextBoxColumn ID;
        private DataGridViewTextBoxColumn Comment;
        internal TableLayoutPanel PolicyIsPrefTable;
        internal PictureBox PictureBox1;
        private TextBox PolicyIsPrefLabel;
        private TextBox PolicySupportedLabel;
        private TextBox PolicyTitleLabel;
        private RichTextBox PolicyDescLabel;
    }
}