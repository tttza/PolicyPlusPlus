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
            components = new System.ComponentModel.Container();
            ToolStripSeparator ToolStripSeparator1;
            ToolStripSeparator ToolStripSeparator2;
            ToolStripSeparator ToolStripSeparator3;
            ToolStripSeparator ToolStripSeparator4;
            ToolStripSeparator ToolStripSeparator5;
            ToolStripStatusLabel ToolStripStatusLabel1;
            ToolStripStatusLabel ToolStripStatusLabel2;
            ToolStripSeparator ToolStripSeparator6;
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            SettingInfoSplitter = new Splitter();
            MainMenu = new MenuStrip();
            FileToolStripMenuItem = new ToolStripMenuItem();
            OpenADMXFolderToolStripMenuItem = new ToolStripMenuItem();
            OpenADMXFileToolStripMenuItem = new ToolStripMenuItem();
            SetADMLLanguageToolStripMenuItem = new ToolStripMenuItem();
            CloseADMXWorkspaceToolStripMenuItem = new ToolStripMenuItem();
            OpenPolicyResourcesToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator7 = new ToolStripSeparator();
            SavePoliciesToolStripMenuItem = new ToolStripMenuItem();
            EditRawPOLToolStripMenuItem = new ToolStripMenuItem();
            ExitToolStripMenuItem = new ToolStripMenuItem();
            ViewToolStripMenuItem = new ToolStripMenuItem();
            EmptyCategoriesToolStripMenuItem = new ToolStripMenuItem();
            OnlyFilteredObjectsToolStripMenuItem = new ToolStripMenuItem();
            FilterOptionsToolStripMenuItem = new ToolStripMenuItem();
            DeduplicatePoliciesToolStripMenuItem = new ToolStripMenuItem();
            LoadedADMXFilesToolStripMenuItem = new ToolStripMenuItem();
            AllProductsToolStripMenuItem = new ToolStripMenuItem();
            AllSupportDefinitionsToolStripMenuItem = new ToolStripMenuItem();
            FindToolStripMenuItem = new ToolStripMenuItem();
            ByIDToolStripMenuItem = new ToolStripMenuItem();
            ByTextToolStripMenuItem = new ToolStripMenuItem();
            ByRegistryToolStripMenuItem = new ToolStripMenuItem();
            SearchResultsToolStripMenuItem = new ToolStripMenuItem();
            FindNextToolStripMenuItem = new ToolStripMenuItem();
            ShareToolStripMenuItem = new ToolStripMenuItem();
            ImportSemanticPolicyToolStripMenuItem = new ToolStripMenuItem();
            ImportPOLToolStripMenuItem = new ToolStripMenuItem();
            ImportREGToolStripMenuItem = new ToolStripMenuItem();
            ExportPOLToolStripMenuItem = new ToolStripMenuItem();
            ExportREGToolStripMenuItem = new ToolStripMenuItem();
            HelpToolStripMenuItem = new ToolStripMenuItem();
            AboutToolStripMenuItem = new ToolStripMenuItem();
            AcquireADMXFilesToolStripMenuItem = new ToolStripMenuItem();
            SplitContainer = new SplitContainer();
            panel3 = new Panel();
            CategoriesTree = new TreeView();
            PolicyObjectContext = new ContextMenuStrip(components);
            CmeCopyToClipboard = new ToolStripMenuItem();
            Cme2CopyId = new ToolStripMenuItem();
            Cme2CopyName = new ToolStripMenuItem();
            Cme2CopyRegPathLC = new ToolStripMenuItem();
            Cme2CopyRegPathCU = new ToolStripMenuItem();
            CmeCatOpen = new ToolStripMenuItem();
            CmePolEdit = new ToolStripMenuItem();
            CmeAllDetails = new ToolStripMenuItem();
            CmeAllDetailsFormatted = new ToolStripMenuItem();
            CmePolInspectElements = new ToolStripMenuItem();
            CmePolSpolFragment = new ToolStripMenuItem();
            PolicyIcons = new ImageList(components);
            panel2 = new Panel();
            ComboAppliesTo = new ComboBox();
            panel1 = new Panel();
            PoliciesGrid = new DataGridView();
            State = new DataGridViewTextBoxColumn();
            Icon = new DataGridViewImageColumn();
            _Name = new DataGridViewTextBoxColumn();
            ID = new DataGridViewTextBoxColumn();
            Comment = new DataGridViewTextBoxColumn();
            SettingInfoPanel = new Panel();
            PolicyInfoTable = new TableLayoutPanel();
            PolicyIsPrefTable = new TableLayoutPanel();
            PictureBox1 = new PictureBox();
            PolicyIsPrefLabel = new TextBox();
            PolicySupportedLabel = new TextBox();
            PolicyTitleLabel = new TextBox();
            PolicyDescLabel = new RichTextBox();
            InfoStrip = new StatusStrip();
            ComputerSourceLabel = new ToolStripStatusLabel();
            UserSourceLabel = new ToolStripStatusLabel();
            AppVersion = new ToolStripStatusLabel();
            ToolStripSeparator1 = new ToolStripSeparator();
            ToolStripSeparator2 = new ToolStripSeparator();
            ToolStripSeparator3 = new ToolStripSeparator();
            ToolStripSeparator4 = new ToolStripSeparator();
            ToolStripSeparator5 = new ToolStripSeparator();
            ToolStripStatusLabel1 = new ToolStripStatusLabel();
            ToolStripStatusLabel2 = new ToolStripStatusLabel();
            ToolStripSeparator6 = new ToolStripSeparator();
            MainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)SplitContainer).BeginInit();
            SplitContainer.Panel1.SuspendLayout();
            SplitContainer.Panel2.SuspendLayout();
            SplitContainer.SuspendLayout();
            panel3.SuspendLayout();
            PolicyObjectContext.SuspendLayout();
            panel2.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PoliciesGrid).BeginInit();
            SettingInfoPanel.SuspendLayout();
            PolicyInfoTable.SuspendLayout();
            PolicyIsPrefTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox1).BeginInit();
            InfoStrip.SuspendLayout();
            SuspendLayout();
            // 
            // ToolStripSeparator1
            // 
            ToolStripSeparator1.Name = "ToolStripSeparator1";
            ToolStripSeparator1.Size = new Size(239, 6);
            // 
            // ToolStripSeparator2
            // 
            ToolStripSeparator2.Name = "ToolStripSeparator2";
            ToolStripSeparator2.Size = new Size(291, 6);
            // 
            // ToolStripSeparator3
            // 
            ToolStripSeparator3.Name = "ToolStripSeparator3";
            ToolStripSeparator3.Size = new Size(291, 6);
            // 
            // ToolStripSeparator4
            // 
            ToolStripSeparator4.Name = "ToolStripSeparator4";
            ToolStripSeparator4.Size = new Size(247, 6);
            // 
            // ToolStripSeparator5
            // 
            ToolStripSeparator5.Name = "ToolStripSeparator5";
            ToolStripSeparator5.Size = new Size(242, 6);
            // 
            // ToolStripStatusLabel1
            // 
            ToolStripStatusLabel1.Name = "ToolStripStatusLabel1";
            ToolStripStatusLabel1.Size = new Size(125, 20);
            ToolStripStatusLabel1.Text = "Computer source:";
            // 
            // ToolStripStatusLabel2
            // 
            ToolStripStatusLabel2.Name = "ToolStripStatusLabel2";
            ToolStripStatusLabel2.Size = new Size(88, 20);
            ToolStripStatusLabel2.Text = "User source:";
            // 
            // ToolStripSeparator6
            // 
            ToolStripSeparator6.Name = "ToolStripSeparator6";
            ToolStripSeparator6.Size = new Size(239, 6);
            // 
            // MainMenu
            // 
            MainMenu.ImageScalingSize = new Size(20, 20);
            MainMenu.Items.AddRange(new ToolStripItem[] { FileToolStripMenuItem, ViewToolStripMenuItem, FindToolStripMenuItem, ShareToolStripMenuItem, HelpToolStripMenuItem });
            MainMenu.Location = new Point(0, 0);
            MainMenu.Name = "MainMenu";
            MainMenu.Size = new Size(1852, 30);
            MainMenu.TabIndex = 0;
            MainMenu.Text = "MenuStrip1";
            // 
            // FileToolStripMenuItem
            // 
            FileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { OpenADMXFolderToolStripMenuItem, OpenADMXFileToolStripMenuItem, SetADMLLanguageToolStripMenuItem, CloseADMXWorkspaceToolStripMenuItem, ToolStripSeparator2, OpenPolicyResourcesToolStripMenuItem, toolStripSeparator7, SavePoliciesToolStripMenuItem, EditRawPOLToolStripMenuItem, ToolStripSeparator3, ExitToolStripMenuItem });
            FileToolStripMenuItem.Name = "FileToolStripMenuItem";
            FileToolStripMenuItem.Size = new Size(46, 26);
            FileToolStripMenuItem.Text = "File";
            // 
            // OpenADMXFolderToolStripMenuItem
            // 
            OpenADMXFolderToolStripMenuItem.Name = "OpenADMXFolderToolStripMenuItem";
            OpenADMXFolderToolStripMenuItem.Size = new Size(294, 26);
            OpenADMXFolderToolStripMenuItem.Text = "Open ADMX Folder";
            OpenADMXFolderToolStripMenuItem.Click += OpenADMXFolderToolStripMenuItem_Click;
            // 
            // OpenADMXFileToolStripMenuItem
            // 
            OpenADMXFileToolStripMenuItem.Name = "OpenADMXFileToolStripMenuItem";
            OpenADMXFileToolStripMenuItem.Size = new Size(294, 26);
            OpenADMXFileToolStripMenuItem.Text = "Open ADMX File";
            OpenADMXFileToolStripMenuItem.Click += OpenADMXFileToolStripMenuItem_Click;
            // 
            // SetADMLLanguageToolStripMenuItem
            // 
            SetADMLLanguageToolStripMenuItem.Name = "SetADMLLanguageToolStripMenuItem";
            SetADMLLanguageToolStripMenuItem.Size = new Size(294, 26);
            SetADMLLanguageToolStripMenuItem.Text = "Set ADML Language";
            SetADMLLanguageToolStripMenuItem.Click += SetADMLLanguageToolStripMenuItem_Click;
            // 
            // CloseADMXWorkspaceToolStripMenuItem
            // 
            CloseADMXWorkspaceToolStripMenuItem.Name = "CloseADMXWorkspaceToolStripMenuItem";
            CloseADMXWorkspaceToolStripMenuItem.Size = new Size(294, 26);
            CloseADMXWorkspaceToolStripMenuItem.Text = "Close ADMX Workspace";
            CloseADMXWorkspaceToolStripMenuItem.Click += CloseADMXWorkspaceToolStripMenuItem_Click;
            // 
            // OpenPolicyResourcesToolStripMenuItem
            // 
            OpenPolicyResourcesToolStripMenuItem.Name = "OpenPolicyResourcesToolStripMenuItem";
            OpenPolicyResourcesToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            OpenPolicyResourcesToolStripMenuItem.Size = new Size(294, 26);
            OpenPolicyResourcesToolStripMenuItem.Text = "Open Policy Resources";
            OpenPolicyResourcesToolStripMenuItem.Click += OpenPolicyResourcesToolStripMenuItem_Click;
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Name = "toolStripSeparator7";
            toolStripSeparator7.Size = new Size(291, 6);
            // 
            // SavePoliciesToolStripMenuItem
            // 
            SavePoliciesToolStripMenuItem.Name = "SavePoliciesToolStripMenuItem";
            SavePoliciesToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            SavePoliciesToolStripMenuItem.Size = new Size(294, 26);
            SavePoliciesToolStripMenuItem.Text = "Save Policies";
            SavePoliciesToolStripMenuItem.Click += SavePoliciesToolStripMenuItem_Click;
            // 
            // EditRawPOLToolStripMenuItem
            // 
            EditRawPOLToolStripMenuItem.Name = "EditRawPOLToolStripMenuItem";
            EditRawPOLToolStripMenuItem.Size = new Size(294, 26);
            EditRawPOLToolStripMenuItem.Text = "Edit Raw POL";
            EditRawPOLToolStripMenuItem.Click += EditRawPOLToolStripMenuItem_Click;
            // 
            // ExitToolStripMenuItem
            // 
            ExitToolStripMenuItem.Name = "ExitToolStripMenuItem";
            ExitToolStripMenuItem.Size = new Size(294, 26);
            ExitToolStripMenuItem.Text = "Exit";
            ExitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;
            // 
            // ViewToolStripMenuItem
            // 
            ViewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { EmptyCategoriesToolStripMenuItem, OnlyFilteredObjectsToolStripMenuItem, ToolStripSeparator1, FilterOptionsToolStripMenuItem, DeduplicatePoliciesToolStripMenuItem, ToolStripSeparator6, LoadedADMXFilesToolStripMenuItem, AllProductsToolStripMenuItem, AllSupportDefinitionsToolStripMenuItem });
            ViewToolStripMenuItem.Name = "ViewToolStripMenuItem";
            ViewToolStripMenuItem.Size = new Size(55, 26);
            ViewToolStripMenuItem.Text = "View";
            // 
            // EmptyCategoriesToolStripMenuItem
            // 
            EmptyCategoriesToolStripMenuItem.Name = "EmptyCategoriesToolStripMenuItem";
            EmptyCategoriesToolStripMenuItem.Size = new Size(242, 26);
            EmptyCategoriesToolStripMenuItem.Text = "Empty Categories";
            EmptyCategoriesToolStripMenuItem.Click += EmptyCategoriesToolStripMenuItem_Click;
            // 
            // OnlyFilteredObjectsToolStripMenuItem
            // 
            OnlyFilteredObjectsToolStripMenuItem.Name = "OnlyFilteredObjectsToolStripMenuItem";
            OnlyFilteredObjectsToolStripMenuItem.Size = new Size(242, 26);
            OnlyFilteredObjectsToolStripMenuItem.Text = "Only Filtered Policies";
            OnlyFilteredObjectsToolStripMenuItem.Click += OnlyFilteredObjectsToolStripMenuItem_Click;
            // 
            // FilterOptionsToolStripMenuItem
            // 
            FilterOptionsToolStripMenuItem.Name = "FilterOptionsToolStripMenuItem";
            FilterOptionsToolStripMenuItem.Size = new Size(242, 26);
            FilterOptionsToolStripMenuItem.Text = "Filter Options";
            FilterOptionsToolStripMenuItem.Click += FilterOptionsToolStripMenuItem_Click;
            // 
            // DeduplicatePoliciesToolStripMenuItem
            // 
            DeduplicatePoliciesToolStripMenuItem.Name = "DeduplicatePoliciesToolStripMenuItem";
            DeduplicatePoliciesToolStripMenuItem.Size = new Size(242, 26);
            DeduplicatePoliciesToolStripMenuItem.Text = "Deduplicate Policies";
            DeduplicatePoliciesToolStripMenuItem.Visible = false;
            DeduplicatePoliciesToolStripMenuItem.Click += DeduplicatePoliciesToolStripMenuItem_Click;
            // 
            // LoadedADMXFilesToolStripMenuItem
            // 
            LoadedADMXFilesToolStripMenuItem.Name = "LoadedADMXFilesToolStripMenuItem";
            LoadedADMXFilesToolStripMenuItem.Size = new Size(242, 26);
            LoadedADMXFilesToolStripMenuItem.Text = "Loaded ADMX Files";
            LoadedADMXFilesToolStripMenuItem.Click += LoadedADMXFilesToolStripMenuItem_Click;
            // 
            // AllProductsToolStripMenuItem
            // 
            AllProductsToolStripMenuItem.Name = "AllProductsToolStripMenuItem";
            AllProductsToolStripMenuItem.Size = new Size(242, 26);
            AllProductsToolStripMenuItem.Text = "All Products";
            AllProductsToolStripMenuItem.Click += AllProductsToolStripMenuItem_Click;
            // 
            // AllSupportDefinitionsToolStripMenuItem
            // 
            AllSupportDefinitionsToolStripMenuItem.Name = "AllSupportDefinitionsToolStripMenuItem";
            AllSupportDefinitionsToolStripMenuItem.Size = new Size(242, 26);
            AllSupportDefinitionsToolStripMenuItem.Text = "All Support Definitions";
            AllSupportDefinitionsToolStripMenuItem.Click += AllSupportDefinitionsToolStripMenuItem_Click;
            // 
            // FindToolStripMenuItem
            // 
            FindToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { ByIDToolStripMenuItem, ByTextToolStripMenuItem, ByRegistryToolStripMenuItem, ToolStripSeparator4, SearchResultsToolStripMenuItem, FindNextToolStripMenuItem });
            FindToolStripMenuItem.Name = "FindToolStripMenuItem";
            FindToolStripMenuItem.Size = new Size(51, 26);
            FindToolStripMenuItem.Text = "Find";
            // 
            // ByIDToolStripMenuItem
            // 
            ByIDToolStripMenuItem.Name = "ByIDToolStripMenuItem";
            ByIDToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.G;
            ByIDToolStripMenuItem.Size = new Size(250, 26);
            ByIDToolStripMenuItem.Text = "By ID";
            ByIDToolStripMenuItem.Click += FindByIDToolStripMenuItem_Click;
            // 
            // ByTextToolStripMenuItem
            // 
            ByTextToolStripMenuItem.Name = "ByTextToolStripMenuItem";
            ByTextToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.F;
            ByTextToolStripMenuItem.Size = new Size(250, 26);
            ByTextToolStripMenuItem.Text = "By Text";
            ByTextToolStripMenuItem.Click += ByTextToolStripMenuItem_Click;
            // 
            // ByRegistryToolStripMenuItem
            // 
            ByRegistryToolStripMenuItem.Name = "ByRegistryToolStripMenuItem";
            ByRegistryToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.R;
            ByRegistryToolStripMenuItem.Size = new Size(250, 26);
            ByRegistryToolStripMenuItem.Text = "By Registry";
            ByRegistryToolStripMenuItem.Click += ByRegistryToolStripMenuItem_Click;
            // 
            // SearchResultsToolStripMenuItem
            // 
            SearchResultsToolStripMenuItem.Name = "SearchResultsToolStripMenuItem";
            SearchResultsToolStripMenuItem.ShortcutKeys = Keys.Shift | Keys.F3;
            SearchResultsToolStripMenuItem.Size = new Size(250, 26);
            SearchResultsToolStripMenuItem.Text = "Search Results";
            SearchResultsToolStripMenuItem.Click += SearchResultsToolStripMenuItem_Click;
            // 
            // FindNextToolStripMenuItem
            // 
            FindNextToolStripMenuItem.Name = "FindNextToolStripMenuItem";
            FindNextToolStripMenuItem.ShortcutKeys = Keys.F3;
            FindNextToolStripMenuItem.Size = new Size(250, 26);
            FindNextToolStripMenuItem.Text = "Find Next";
            FindNextToolStripMenuItem.Click += FindNextToolStripMenuItem_Click;
            // 
            // ShareToolStripMenuItem
            // 
            ShareToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { ImportSemanticPolicyToolStripMenuItem, ImportPOLToolStripMenuItem, ImportREGToolStripMenuItem, ToolStripSeparator5, ExportPOLToolStripMenuItem, ExportREGToolStripMenuItem });
            ShareToolStripMenuItem.Name = "ShareToolStripMenuItem";
            ShareToolStripMenuItem.Size = new Size(60, 26);
            ShareToolStripMenuItem.Text = "Share";
            // 
            // ImportSemanticPolicyToolStripMenuItem
            // 
            ImportSemanticPolicyToolStripMenuItem.Name = "ImportSemanticPolicyToolStripMenuItem";
            ImportSemanticPolicyToolStripMenuItem.Size = new Size(245, 26);
            ImportSemanticPolicyToolStripMenuItem.Text = "Import Semantic Policy";
            ImportSemanticPolicyToolStripMenuItem.Click += ImportSemanticPolicyToolStripMenuItem_Click;
            // 
            // ImportPOLToolStripMenuItem
            // 
            ImportPOLToolStripMenuItem.Name = "ImportPOLToolStripMenuItem";
            ImportPOLToolStripMenuItem.Size = new Size(245, 26);
            ImportPOLToolStripMenuItem.Text = "Import POL";
            ImportPOLToolStripMenuItem.Click += ImportPOLToolStripMenuItem_Click;
            // 
            // ImportREGToolStripMenuItem
            // 
            ImportREGToolStripMenuItem.Name = "ImportREGToolStripMenuItem";
            ImportREGToolStripMenuItem.Size = new Size(245, 26);
            ImportREGToolStripMenuItem.Text = "Import REG";
            ImportREGToolStripMenuItem.Click += ImportREGToolStripMenuItem_Click;
            // 
            // ExportPOLToolStripMenuItem
            // 
            ExportPOLToolStripMenuItem.Name = "ExportPOLToolStripMenuItem";
            ExportPOLToolStripMenuItem.Size = new Size(245, 26);
            ExportPOLToolStripMenuItem.Text = "Export POL";
            ExportPOLToolStripMenuItem.Click += ExportPOLToolStripMenuItem_Click;
            // 
            // ExportREGToolStripMenuItem
            // 
            ExportREGToolStripMenuItem.Name = "ExportREGToolStripMenuItem";
            ExportREGToolStripMenuItem.Size = new Size(245, 26);
            ExportREGToolStripMenuItem.Text = "Export REG";
            ExportREGToolStripMenuItem.Click += ExportREGToolStripMenuItem_Click;
            // 
            // HelpToolStripMenuItem
            // 
            HelpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { AboutToolStripMenuItem, AcquireADMXFilesToolStripMenuItem });
            HelpToolStripMenuItem.Name = "HelpToolStripMenuItem";
            HelpToolStripMenuItem.Size = new Size(55, 26);
            HelpToolStripMenuItem.Text = "Help";
            // 
            // AboutToolStripMenuItem
            // 
            AboutToolStripMenuItem.Name = "AboutToolStripMenuItem";
            AboutToolStripMenuItem.Size = new Size(223, 26);
            AboutToolStripMenuItem.Text = "About";
            AboutToolStripMenuItem.Click += AboutToolStripMenuItem_Click;
            // 
            // AcquireADMXFilesToolStripMenuItem
            // 
            AcquireADMXFilesToolStripMenuItem.Name = "AcquireADMXFilesToolStripMenuItem";
            AcquireADMXFilesToolStripMenuItem.Size = new Size(223, 26);
            AcquireADMXFilesToolStripMenuItem.Text = "Acquire ADMX Files";
            AcquireADMXFilesToolStripMenuItem.Click += AcquireADMXFilesToolStripMenuItem_Click;
            // 
            // SplitContainer
            // 
            SplitContainer.Dock = DockStyle.Fill;
            // Keep left (Panel1) width constant when overall container width changes (right info panel resize)
            SplitContainer.FixedPanel = FixedPanel.Panel1;
            SplitContainer.Location = new Point(0, 0);
            SplitContainer.Margin = new Padding(5, 2, 5, 2);
            SplitContainer.Name = "SplitContainer";
            // 
            // SplitContainer.Panel1
            // 
            SplitContainer.Panel1.AutoScroll = true;
            SplitContainer.Panel1.Controls.Add(panel3);
            SplitContainer.Panel1.Controls.Add(panel2);
            // 
            // SplitContainer.Panel2
            SplitContainer.Panel2.BackColor = Color.White;
            SplitContainer.Panel2.Controls.Add(panel1);
            SplitContainer.Size = new Size(1477, 925); // width adjusted automatically at runtime; design-time value not critical
            SplitContainer.SplitterDistance = 469;
            SplitContainer.SplitterWidth = 6;
            SplitContainer.TabIndex = 1;
            SplitContainer.TabStop = false;
            // 
            // panel3
            // 
            panel3.AutoSize = true;
            panel3.Controls.Add(CategoriesTree);
            panel3.Dock = DockStyle.Fill;
            panel3.Location = new Point(0, 28);
            panel3.Margin = new Padding(2);
            panel3.Name = "panel3";
            panel3.Size = new Size(469, 887);
            panel3.TabIndex = 4;
            // 
            // CategoriesTree
            // 
            CategoriesTree.BorderStyle = BorderStyle.None;
            CategoriesTree.ContextMenuStrip = PolicyObjectContext;
            CategoriesTree.Dock = DockStyle.Fill;
            CategoriesTree.HideSelection = false;
            CategoriesTree.ImageIndex = 0;
            CategoriesTree.ImageList = PolicyIcons;
            CategoriesTree.Location = new Point(0, 0);
            CategoriesTree.Margin = new Padding(5, 2, 5, 2);
            CategoriesTree.Name = "CategoriesTree";
            CategoriesTree.SelectedImageIndex = 0;
            CategoriesTree.ShowNodeToolTips = true;
            CategoriesTree.Size = new Size(469, 887);
            CategoriesTree.TabIndex = 2;
            CategoriesTree.AfterSelect += CategoriesTree_AfterSelect;
            CategoriesTree.NodeMouseClick += CategoriesTree_NodeMouseClick;
            // 
            // PolicyObjectContext
            // 
            PolicyObjectContext.ImageScalingSize = new Size(20, 20);
            PolicyObjectContext.Items.AddRange(new ToolStripItem[] { CmeCopyToClipboard, CmeCatOpen, CmePolEdit, CmeAllDetails, CmeAllDetailsFormatted, CmePolInspectElements, CmePolSpolFragment });
            PolicyObjectContext.Name = "PolicyObjectContext";
            PolicyObjectContext.Size = new Size(249, 172);
            PolicyObjectContext.Opening += PolicyObjectContext_Opening;
            PolicyObjectContext.ItemClicked += PolicyObjectContext_ItemClicked;
            // 
            // CmeCopyToClipboard
            // 
            CmeCopyToClipboard.DropDownItems.AddRange(new ToolStripItem[] { Cme2CopyId, Cme2CopyName, Cme2CopyRegPathLC, Cme2CopyRegPathCU });
            CmeCopyToClipboard.Name = "CmeCopyToClipboard";
            CmeCopyToClipboard.Size = new Size(248, 24);
            CmeCopyToClipboard.Tag = "P";
            CmeCopyToClipboard.Text = "Copy value";
            CmeCopyToClipboard.DropDownOpening += PolicyObjectContext_DropdownOpening;
            CmeCopyToClipboard.DropDownItemClicked += PolicyObjectContext_ItemClicked;
            // 
            // Cme2CopyId
            // 
            Cme2CopyId.Name = "Cme2CopyId";
            Cme2CopyId.Size = new Size(212, 26);
            Cme2CopyId.Text = "ID";
            // 
            // Cme2CopyName
            // 
            Cme2CopyName.Name = "Cme2CopyName";
            Cme2CopyName.Size = new Size(212, 26);
            Cme2CopyName.Text = "Name";
            // 
            // Cme2CopyRegPathLC
            // 
            Cme2CopyRegPathLC.Name = "Cme2CopyRegPathLC";
            Cme2CopyRegPathLC.Size = new Size(212, 26);
            Cme2CopyRegPathLC.Tag = "P-LM";
            Cme2CopyRegPathLC.Text = "Registry Path - LM";
            // 
            // Cme2CopyRegPathCU
            // 
            Cme2CopyRegPathCU.Name = "Cme2CopyRegPathCU";
            Cme2CopyRegPathCU.Size = new Size(212, 26);
            Cme2CopyRegPathCU.Tag = "P-CU";
            Cme2CopyRegPathCU.Text = "Registry Path - CU";
            // 
            // CmeCatOpen
            // 
            CmeCatOpen.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            CmeCatOpen.Name = "CmeCatOpen";
            CmeCatOpen.Size = new Size(248, 24);
            CmeCatOpen.Tag = "C";
            CmeCatOpen.Text = "Open";
            // 
            // CmePolEdit
            // 
            CmePolEdit.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            CmePolEdit.Name = "CmePolEdit";
            CmePolEdit.Size = new Size(248, 24);
            CmePolEdit.Tag = "P";
            CmePolEdit.Text = "Edit";
            // 
            // CmeAllDetails
            // 
            CmeAllDetails.Name = "CmeAllDetails";
            CmeAllDetails.Size = new Size(248, 24);
            CmeAllDetails.Text = "Details";
            // 
            // CmeAllDetailsFormatted
            // 
            CmeAllDetailsFormatted.Name = "CmeAllDetailsFormatted";
            CmeAllDetailsFormatted.Size = new Size(248, 24);
            CmeAllDetailsFormatted.Tag = "P";
            CmeAllDetailsFormatted.Text = "Details - Formatted";
            // 
            // CmePolInspectElements
            // 
            CmePolInspectElements.Name = "CmePolInspectElements";
            CmePolInspectElements.Size = new Size(248, 24);
            CmePolInspectElements.Tag = "P";
            CmePolInspectElements.Text = "Element Inspector";
            // 
            // CmePolSpolFragment
            // 
            CmePolSpolFragment.Name = "CmePolSpolFragment";
            CmePolSpolFragment.Size = new Size(248, 24);
            CmePolSpolFragment.Tag = "P";
            CmePolSpolFragment.Text = "Semantic Policy Fragment";
            // 
            // PolicyIcons
            // 
            PolicyIcons.ColorDepth = ColorDepth.Depth32Bit;
            PolicyIcons.ImageSize = new Size(16, 16);
            PolicyIcons.TransparentColor = Color.Transparent;
            // 
            // panel2
            // 
            panel2.AutoSize = true;
            panel2.Controls.Add(ComboAppliesTo);
            panel2.Dock = DockStyle.Top;
            panel2.Location = new Point(0, 0);
            panel2.Margin = new Padding(0); // remove gap left/top
            panel2.Name = "panel2";
            panel2.Size = new Size(469, 28);
            panel2.TabIndex = 3;
            // 
            // ComboAppliesTo
            // 
            ComboAppliesTo.Dock = DockStyle.Top;
            ComboAppliesTo.DropDownStyle = ComboBoxStyle.DropDownList;
            ComboAppliesTo.Items.AddRange(new object[] { "User or Computer", "User", "Computer" });
            ComboAppliesTo.Location = new Point(0, 0);
            ComboAppliesTo.Margin = new Padding(5, 2, 5, 2);
            ComboAppliesTo.Name = "ComboAppliesTo";
            ComboAppliesTo.Size = new Size(469, 28);
            ComboAppliesTo.TabIndex = 1;
            ComboAppliesTo.SelectedIndexChanged += ComboAppliesTo_SelectedIndexChanged;
            // 
            // panel1
            // 
            panel1.AutoScroll = true;
            panel1.AutoSize = false; // Disable AutoSize to prevent cumulative shrink during DPI changes
            panel1.AutoSizeMode = AutoSizeMode.GrowOnly;
            panel1.Controls.Add(PoliciesGrid);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Margin = new Padding(0); // flush to splitter edges
            panel1.Padding = new Padding(0);
            panel1.Name = "panel1";
            panel1.Size = new Size(1002, 915);
            panel1.TabIndex = 4;
            // 
            // PoliciesGrid
            // 
            PoliciesGrid.AllowUserToAddRows = false;
            PoliciesGrid.AllowUserToDeleteRows = false;
            PoliciesGrid.AllowUserToResizeRows = false;
            PoliciesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            PoliciesGrid.BackgroundColor = SystemColors.Window;
            PoliciesGrid.BorderStyle = BorderStyle.None;
            PoliciesGrid.CellBorderStyle = DataGridViewCellBorderStyle.None;
            PoliciesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            PoliciesGrid.Columns.AddRange(new DataGridViewColumn[] { State, Icon, _Name, ID, Comment });
            PoliciesGrid.ContextMenuStrip = PolicyObjectContext;
            PoliciesGrid.Dock = DockStyle.Fill;
            PoliciesGrid.Location = new Point(0, 0);
            PoliciesGrid.Margin = new Padding(2);
            PoliciesGrid.MultiSelect = false;
            PoliciesGrid.Name = "PoliciesGrid";
            PoliciesGrid.ReadOnly = true;
            PoliciesGrid.RowHeadersVisible = false;
            PoliciesGrid.RowHeadersWidth = 51;
            PoliciesGrid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            PoliciesGrid.RowTemplate.Height = 22;
            PoliciesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            PoliciesGrid.Size = new Size(1002, 915);
            PoliciesGrid.TabIndex = 4;
            PoliciesGrid.CellClick += PoliciesGrid_CellContentClick;
            PoliciesGrid.CellContentClick += PoliciesGrid_CellContentClick;
            PoliciesGrid.CellContentDoubleClick += PoliciesGrid_CellContentDoubleClick;
            PoliciesGrid.CellDoubleClick += PoliciesGrid_CellContentDoubleClick;
            PoliciesGrid.SelectionChanged += PoliciesGrid_SelectionChanged;
            PoliciesGrid.EnableHeadersVisualStyles = false;
            DataGridViewCellStyle dataGridViewHeaderStyle = new DataGridViewCellStyle();
            dataGridViewHeaderStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewHeaderStyle.BackColor = Color.FromArgb(245, 245, 245);
            dataGridViewHeaderStyle.ForeColor = Color.Black;
            dataGridViewHeaderStyle.Padding = new Padding(6, 2, 6, 2); // tighter vertical padding to match combo height
            dataGridViewHeaderStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewHeaderStyle.Font = PoliciesGrid.ColumnHeadersDefaultCellStyle.Font;
            dataGridViewHeaderStyle.SelectionBackColor = dataGridViewHeaderStyle.BackColor; // suppress blue highlight
            dataGridViewHeaderStyle.SelectionForeColor = dataGridViewHeaderStyle.ForeColor;
            PoliciesGrid.ColumnHeadersDefaultCellStyle = dataGridViewHeaderStyle;
            PoliciesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            PoliciesGrid.ColumnHeadersHeight = 28; // align with ComboAppliesTo
            DataGridViewCellStyle dataGridViewAltStyle = new DataGridViewCellStyle();
            dataGridViewAltStyle.BackColor = Color.FromArgb(250, 250, 250);
            PoliciesGrid.AlternatingRowsDefaultCellStyle = dataGridViewAltStyle;
            // 
            // State
            // 
            State.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            State.ContextMenuStrip = PolicyObjectContext;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.Padding = new Padding(5, 0, 5, 0);
            State.DefaultCellStyle = dataGridViewCellStyle1;
            State.Frozen = true;
            State.HeaderText = "State";
            State.MinimumWidth = 6;
            State.Name = "State";
            State.ReadOnly = true;
            State.Width = 72;
            // 
            // Icon
            // 
            Icon.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            Icon.ContextMenuStrip = PolicyObjectContext;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.NullValue = "null";
            Icon.DefaultCellStyle = dataGridViewCellStyle2;
            Icon.HeaderText = " ";
            Icon.ImageLayout = DataGridViewImageCellLayout.Zoom;
            Icon.MinimumWidth = 6;
            Icon.Name = "Icon";
            Icon.ReadOnly = true;
            Icon.Resizable = DataGridViewTriState.True;
            Icon.SortMode = DataGridViewColumnSortMode.Automatic;
            Icon.Width = 42;
            // 
            // _Name
            // 
            _Name.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _Name.ContextMenuStrip = PolicyObjectContext;
            _Name.FillWeight = 26.31579F;
            _Name.HeaderText = "Name";
            _Name.MinimumWidth = 6;
            _Name.Name = "_Name";
            _Name.ReadOnly = true;
            // 
            // ID
            // 
            ID.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            ID.ContextMenuStrip = PolicyObjectContext;
            ID.FillWeight = 13.15789F;
            ID.HeaderText = "ID";
            ID.MinimumWidth = 6;
            ID.Name = "ID";
            ID.ReadOnly = true;
            // 
            // Comment
            // 
            Comment.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            Comment.ContextMenuStrip = PolicyObjectContext;
            Comment.FillWeight = 210.5263F;
            Comment.HeaderText = "Comment";
            Comment.MinimumWidth = 6;
            Comment.Name = "Comment";
            Comment.ReadOnly = true;
            Comment.Visible = false;
            Comment.Width = 125;
            // 
            // SettingInfoPanel (docked Right, resizable via SettingInfoSplitter)
            SettingInfoPanel.AutoScroll = true;
            SettingInfoPanel.Controls.Add(PolicyInfoTable);
            SettingInfoPanel.Dock = DockStyle.Right;
            SettingInfoPanel.Location = new Point(1225, 30);
            SettingInfoPanel.Margin = new Padding(0);
            SettingInfoPanel.Padding = new Padding(12, 10, 12, 10);
            SettingInfoPanel.Name = "SettingInfoPanel";
            SettingInfoPanel.Size = new Size(320, 925); // reduced default width
            SettingInfoPanel.TabIndex = 0;
            SettingInfoPanel.ClientSizeChanged += SettingInfoPanel_ClientSizeChanged;
            SettingInfoPanel.SizeChanged += SettingInfoPanel_ClientSizeChanged;

            // SettingInfoSplitter
            SettingInfoSplitter.Dock = DockStyle.Right;
            SettingInfoSplitter.MinExtra = 400;
            SettingInfoSplitter.MinSize = 220;
            SettingInfoSplitter.Width = 3; // slimmer
            SettingInfoSplitter.BackColor = Color.FromArgb(235, 235, 235); // lighter, less prominent
            SettingInfoSplitter.TabStop = false;
            SettingInfoSplitter.Cursor = Cursors.VSplit;
            SettingInfoSplitter.SplitterMoved += SettingInfoSplitter_SplitterMoved;
            // Removed MouseDown/Up handlers; left pane width now fixed via SplitContainer.FixedPanel
            // 
            // PolicyInfoTable
            // 
            PolicyInfoTable.AutoSize = true;
            PolicyInfoTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            PolicyInfoTable.ColumnCount = 1;
            PolicyInfoTable.ColumnStyles.Add(new ColumnStyle());
            PolicyInfoTable.Controls.Add(PolicyIsPrefTable, 0, 2);
            PolicyInfoTable.Controls.Add(PolicySupportedLabel, 0, 1);
            PolicyInfoTable.Controls.Add(PolicyTitleLabel, 0, 0);
            PolicyInfoTable.Controls.Add(PolicyDescLabel, 0, 4);
            PolicyInfoTable.Dock = DockStyle.Fill;
            PolicyInfoTable.Location = new Point(0, 0);
            PolicyInfoTable.Margin = new Padding(0);
            PolicyInfoTable.Name = "PolicyInfoTable";
            PolicyInfoTable.RowCount = 5;
            PolicyInfoTable.RowStyles.Add(new RowStyle());
            PolicyInfoTable.RowStyles.Add(new RowStyle());
            PolicyInfoTable.RowStyles.Add(new RowStyle());
            PolicyInfoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));
            PolicyInfoTable.RowStyles.Add(new RowStyle());
            PolicyInfoTable.Size = new Size(375, 915);
            PolicyInfoTable.TabIndex = 0;
            // 
            // PolicyIsPrefTable
            // 
            PolicyIsPrefTable.AutoSize = true;
            PolicyIsPrefTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            PolicyIsPrefTable.ColumnCount = 2;
            PolicyIsPrefTable.ColumnStyles.Add(new ColumnStyle());
            PolicyIsPrefTable.ColumnStyles.Add(new ColumnStyle());
            PolicyIsPrefTable.Controls.Add(PictureBox1, 0, 0);
            PolicyIsPrefTable.Controls.Add(PolicyIsPrefLabel, 1, 0);
            PolicyIsPrefTable.Dock = DockStyle.Fill;
            PolicyIsPrefTable.Location = new Point(5, 85);
            PolicyIsPrefTable.Margin = new Padding(0, 8, 0, 16);
            PolicyIsPrefTable.Name = "PolicyIsPrefTable";
            PolicyIsPrefTable.RowCount = 1;
            PolicyIsPrefTable.RowStyles.Add(new RowStyle());
            PolicyIsPrefTable.Size = new Size(374, 29);
            PolicyIsPrefTable.TabIndex = 4;
            // 
            // PictureBox1
            // 
            PictureBox1.Image = (Image)resources.GetObject("PictureBox1.Image");
            PictureBox1.Location = new Point(5, 2);
            PictureBox1.Margin = new Padding(5, 2, 0, 2);
            PictureBox1.Name = "PictureBox1";
            PictureBox1.Size = new Size(16, 16);
            PictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
            PictureBox1.TabIndex = 0;
            PictureBox1.TabStop = false;
            // 
            // PolicyIsPrefLabel
            // 
            PolicyIsPrefLabel.BackColor = SystemColors.Window;
            PolicyIsPrefLabel.BorderStyle = BorderStyle.None;
            PolicyIsPrefLabel.Dock = DockStyle.Fill;
            PolicyIsPrefLabel.Location = new Point(23, 2);
            PolicyIsPrefLabel.Margin = new Padding(2);
            PolicyIsPrefLabel.Multiline = true;
            PolicyIsPrefLabel.Name = "PolicyIsPrefLabel";
            PolicyIsPrefLabel.ReadOnly = true;
            PolicyIsPrefLabel.Size = new Size(349, 25);
            PolicyIsPrefLabel.TabIndex = 1;
            // 
            // PolicySupportedLabel
            // 
            PolicySupportedLabel.BackColor = SystemColors.Window;
            PolicySupportedLabel.BorderStyle = BorderStyle.None;
            PolicySupportedLabel.Dock = DockStyle.Fill;
            PolicySupportedLabel.Location = new Point(2, 19);
            PolicySupportedLabel.Margin = new Padding(2);
            PolicySupportedLabel.Multiline = true;
            PolicySupportedLabel.Name = "PolicySupportedLabel";
            PolicySupportedLabel.ReadOnly = true;
            PolicySupportedLabel.Size = new Size(375, 62);
            PolicySupportedLabel.TabIndex = 6;
            PolicySupportedLabel.Text = "Policy Supported";
            // 
            // PolicyTitleLabel
            // 
            PolicyTitleLabel.BackColor = SystemColors.Window;
            PolicyTitleLabel.BorderStyle = BorderStyle.None;
            PolicyTitleLabel.Dock = DockStyle.Fill;
            PolicyTitleLabel.Font = new Font("MS UI Gothic", 7.8F, FontStyle.Bold, GraphicsUnit.Point, 128);
            PolicyTitleLabel.Location = new Point(2, 2);
            PolicyTitleLabel.Margin = new Padding(2);
            PolicyTitleLabel.MinimumSize = new Size(0, 24);
            PolicyTitleLabel.Name = "PolicyTitleLabel";
            PolicyTitleLabel.ReadOnly = true;
            PolicyTitleLabel.Size = new Size(375, 24);
            PolicyTitleLabel.TabIndex = 7;
            PolicyTitleLabel.Text = "Policy Title";
            // 
            // PolicyDescLabel
            // 
            PolicyDescLabel.BackColor = SystemColors.Window;
            PolicyDescLabel.BorderStyle = BorderStyle.None;
            PolicyDescLabel.Dock = DockStyle.Fill;
            PolicyDescLabel.Location = new Point(5, 166);
            PolicyDescLabel.Margin = new Padding(2, 8, 2, 2);
            PolicyDescLabel.Name = "PolicyDescLabel";
            PolicyDescLabel.ReadOnly = true;
            PolicyDescLabel.Size = new Size(369, 744);
            PolicyDescLabel.TabIndex = 8;
            PolicyDescLabel.Text = "Policy Desc";
            PolicyDescLabel.LinkClicked += PolicyDescLabel_LinkClicked;
            // 
            // InfoStrip
            // 
            InfoStrip.ImageScalingSize = new Size(20, 20);
            InfoStrip.Items.AddRange(new ToolStripItem[] { ToolStripStatusLabel1, ComputerSourceLabel, ToolStripStatusLabel2, UserSourceLabel, AppVersion });
            InfoStrip.Location = new Point(0, 960);
            InfoStrip.Name = "InfoStrip";
            InfoStrip.Padding = new Padding(8, 0, 12, 0);
            InfoStrip.SizingGrip = false;
            InfoStrip.BackColor = Color.FromArgb(250, 250, 250);
            InfoStrip.Size = new Size(1852, 26);
            InfoStrip.TabIndex = 2;
            InfoStrip.Text = "StatusStrip1";
            // 
            // ComputerSourceLabel
            // 
            ComputerSourceLabel.Name = "ComputerSourceLabel";
            ComputerSourceLabel.Size = new Size(105, 20);
            ComputerSourceLabel.Text = "Computer info";
            // 
            // UserSourceLabel
            // 
            UserSourceLabel.Name = "UserSourceLabel";
            UserSourceLabel.Size = new Size(68, 20);
            UserSourceLabel.Text = "User info";
            // 
            // AppVersion
            // 
            AppVersion.Name = "AppVersion";
            AppVersion.Size = new Size(1441, 20);
            AppVersion.Spring = true;
            AppVersion.Text = "version";
            AppVersion.TextAlign = ContentAlignment.MiddleRight;
            AppVersion.Click += AboutToolStripMenuItem_Click;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1600, 900);
            Controls.Add(SplitContainer);
            Controls.Add(SettingInfoSplitter); // splitter sits to left of info panel
            Controls.Add(SettingInfoPanel);
            Controls.Add(InfoStrip);
            Controls.Add(MainMenu);
            MainMenuStrip = MainMenu;
            Margin = new Padding(5, 2, 5, 2);
            MinimumSize = new Size(987, 549);
            Name = "Main";
            BackColor = Color.White;
            ShowIcon = false;
            Text = "Policy Plus";
            Closed += Main_Closed;
            Load += Main_Load;
            Shown += Main_Shown;
            MainMenu.ResumeLayout(false);
            MainMenu.PerformLayout();
            SplitContainer.Panel1.ResumeLayout(false);
            SplitContainer.Panel1.PerformLayout();
            SplitContainer.Panel2.ResumeLayout(false);
            SplitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)SplitContainer).EndInit();
            SplitContainer.ResumeLayout(false);
            panel3.ResumeLayout(false);
            PolicyObjectContext.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)PoliciesGrid).EndInit();
            SettingInfoPanel.ResumeLayout(false);
            SettingInfoPanel.PerformLayout();
            PolicyInfoTable.ResumeLayout(false);
            PolicyInfoTable.PerformLayout();
            PolicyIsPrefTable.ResumeLayout(false);
            PolicyIsPrefTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)PictureBox1).EndInit();
            InfoStrip.ResumeLayout(false);
            InfoStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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
    private Splitter SettingInfoSplitter;
    }
}