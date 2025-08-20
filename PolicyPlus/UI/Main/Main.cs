using Microsoft.Win32;
using PolicyPlus.UI.Find;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.DirectoryServices;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace PolicyPlus.UI.Main
{
    public partial class Main
    {
        public Main()
        {
            Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            InitializeComponent();
            PolicyIconsLoader.Initialize(PolicyIcons); // Load icons according to selected mode (embedded / generated)
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5);
            if (!string.IsNullOrEmpty(version))
                AppVersion.Text = $"Version: {version}";
        }

        private ConfigurationStorage Configuration;
        private AdmxBundle AdmxWorkspace = new AdmxBundle();
        private IPolicySource UserPolicySource, CompPolicySource;
        private PolicyLoader UserPolicyLoader, CompPolicyLoader;
        private Dictionary<string, string> UserComments, CompComments;
        private PolicyPlusCategory CurrentCategory;
        private PolicyPlusPolicy CurrentSetting;
        private FilterConfiguration CurrentFilter = new FilterConfiguration();
        private PolicyPlusCategory HighlightCategory;
        private Dictionary<PolicyPlusCategory, TreeNode> CategoryNodes = new Dictionary<PolicyPlusCategory, TreeNode>();
        private bool ViewEmptyCategories = false;
        private AdmxPolicySection ViewPolicyTypes = AdmxPolicySection.Both;
        private bool ViewFilteredOnly = false;

        private void Main_Load(object sender, EventArgs e)
        {
            // Create the configuration manager (for the Registry)
            Configuration = new ConfigurationStorage(RegistryHive.CurrentUser, @"Software\Policy Plus");
            // Restore the last ADMX source and policy loaders
            OpenLastAdmxSource();
            PolicyLoaderSource compLoaderType = (PolicyLoaderSource)Configuration.GetValue("CompSourceType", 0);
            var compLoaderData = Configuration.GetValue("CompSourceData", "");
            PolicyLoaderSource userLoaderType = (PolicyLoaderSource)Configuration.GetValue("UserSourceType", 0);
            var userLoaderData = Configuration.GetValue("UserSourceData", "");
            try
            {
                OpenPolicyLoaders(new PolicyLoader(userLoaderType, Convert.ToString(userLoaderData), true), new PolicyLoader(compLoaderType, Convert.ToString(compLoaderData), false), true);
            }
            catch (Exception)
            {
                MessageBox.Show("The previous policy sources are not accessible. The defaults will be loaded.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Configuration.SetValue("CompSourceType", (int)PolicyLoaderSource.LocalGpo);
                Configuration.SetValue("UserSourceType", (int)PolicyLoaderSource.LocalGpo);
                OpenPolicyLoaders(new PolicyLoader(PolicyLoaderSource.LocalGpo, "", true), new PolicyLoader(PolicyLoaderSource.LocalGpo, "", false), true);
            }

            AppForms.OpenPol.SetLastSources(compLoaderType, Convert.ToString(compLoaderData), userLoaderType, Convert.ToString(userLoaderData));
            // Set up the UI
            ComboAppliesTo.Text = Convert.ToString(ComboAppliesTo.Items[0]);
            InfoStrip.Items.Insert(2, new ToolStripSeparator());
            PopulateAdmxUi();
        }

        private void Main_Shown(object sender, EventArgs e)
        {
            // Check whether ADMX files probably need to be downloaded
            if (Convert.ToInt32(Configuration.GetValue("CheckedPolicyDefinitions", 0)) == 0)
            {
                Configuration.SetValue("CheckedPolicyDefinitions", 1);
                if (!SystemInfo.HasGroupPolicyInfrastructure() && AdmxWorkspace.Categories.Values.Where(c => IsOrphanCategory(c) & !IsEmptyCategory(c)).Count() > 2)
                {
                    if (MessageBox.Show($"Welcome to Policy Plus!{Environment.NewLine}{Environment.NewLine}Home editions do not come with the full set of policy definitions. Would you like to download them now? This can also be done later with Help | Acquire ADMX Files.", "Policy Plus", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        AcquireADMXFilesToolStripMenuItem_Click(null, null);
                    }
                }
            }
        }

        public void OpenLastAdmxSource()
        {
            string defaultAdmxSource = Environment.ExpandEnvironmentVariables(@"%windir%\PolicyDefinitions");
            string admxSource = Convert.ToString(Configuration.GetValue("AdmxSource", defaultAdmxSource));
            try
            {
                var fails = AdmxWorkspace.LoadFolder(admxSource, GetPreferredLanguageCode());
                if (DisplayAdmxLoadErrorReport(fails, true) == DialogResult.No)
                    throw new Exception("You decided to not use the problematic ADMX bundle.");
            }
            catch (Exception ex)
            {
                AdmxWorkspace = new AdmxBundle();
                string loadFailReason = "";
                if ((admxSource ?? "") != (defaultAdmxSource ?? ""))
                {
                    if (MessageBox.Show("Policy definitions could not be loaded from \"" + admxSource + "\": " + ex.Message + Environment.NewLine + Environment.NewLine + "Load from the default location?", "Policy Plus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            Configuration.SetValue("AdmxSource", defaultAdmxSource);
                            AdmxWorkspace = new AdmxBundle();
                            DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFolder(defaultAdmxSource, GetPreferredLanguageCode()));
                        }
                        catch (Exception ex2)
                        {
                            loadFailReason = ex2.Message;
                        }
                    }
                }
                else
                {
                    loadFailReason = ex.Message;
                }

                if (!string.IsNullOrEmpty(loadFailReason))
                    MessageBox.Show("Policy definitions could not be loaded: " + loadFailReason, "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public void PopulateAdmxUi()
        {
            // Populate the left categories tree
            CategoriesTree.Nodes.Clear();
            CategoryNodes.Clear();
            void addCategory(IEnumerable<PolicyPlusCategory> CategoryList, TreeNodeCollection ParentNode)
            {
                foreach (var category in CategoryList.Where(ShouldShowCategory))
                {
                    var newNode = ParentNode.Add(category.UniqueID, category.DisplayName, GetImageIndexForCategory(category));
                    newNode.SelectedImageIndex = 3;
                    newNode.Tag = category;
                    CategoryNodes.Add(category, newNode);
                    addCategory(category.Children, newNode.Nodes);
                }
            }; // "Go" arrow
            addCategory(AdmxWorkspace.Categories.Values, CategoriesTree.Nodes);
            CategoriesTree.Sort();
            CurrentCategory = null;
            UpdateCategoryListing();
            ClearSelections();
            UpdatePolicyInfo();
        }

        private void ChangeColumnsAutoSize(bool shouldEnable)
        {
            var columnsList = new List<string>() { "State", "Icon" };
            if (shouldEnable)
            {
                columnsList.ForEach(column =>
                {
                    PoliciesGrid.Columns[column].AutoSizeMode = (DataGridViewAutoSizeColumnMode)DataGridViewAutoSizeColumnsMode.AllCells;
                });
            }
            else
            {
                columnsList.ForEach(column =>
                {
                    PoliciesGrid.Columns[column].AutoSizeMode = (DataGridViewAutoSizeColumnMode)DataGridViewAutoSizeColumnsMode.None;
                });
            }
        }

        public void UpdateCategoryListing(bool resetSort=false)
        {
            // Update the right pane to include the current category's children
            bool inSameCategory = false;
            var topItemIndex = PoliciesGrid.FirstDisplayedScrollingRowIndex;

            ChangeColumnsAutoSize(false);
            PoliciesGrid.Rows.Clear();

            PoliciesGrid.Columns["Icon"].DefaultCellStyle.NullValue = null;
            if (CurrentCategory is object)
            {
                if (CurrentSetting is object && ReferenceEquals(CurrentSetting.Category, CurrentCategory))
                    inSameCategory = true;
                if (CurrentCategory.Parent is object) // Add the parent
                {
                    var rowId = PoliciesGrid.Rows.Add();
                    DataGridViewRow row = PoliciesGrid.Rows[rowId];
                    row.Cells["Icon"].Value = PolicyIcons.Images[6];

                    row.Cells["ID"].Value = "";
                    row.Cells["State"].Value = "Parent";
                    row.Cells["_Name"].Value = "Up: " + CurrentCategory.Parent.DisplayName;
                    row.Cells["Comment"].Value = "";
                    row.Tag = CurrentCategory.Parent;
                }

                foreach (var category in CurrentCategory.Children.Where(ShouldShowCategory).OrderBy(c => c.DisplayName)) // Add subcategories
                {
                    var rowId = PoliciesGrid.Rows.Add();
                    DataGridViewRow row = PoliciesGrid.Rows[rowId];
                    row.Cells["Icon"].Value = PolicyIcons.Images[GetImageIndexForCategory(category)];
                    row.Cells["ID"].Value = "";
                    row.Cells["State"].Value = "";
                    row.Cells["_Name"].Value = category.DisplayName;
                    row.Cells["Comment"].Value = "";
                    row.Tag = category;
                }

                foreach (var policy in CurrentCategory.Policies.Where(ShouldShowPolicy).OrderBy(p => p.DisplayName)) // Add policies
                {
                    var rowId = PoliciesGrid.Rows.Add();
                    DataGridViewRow row = PoliciesGrid.Rows[rowId];
                    row.Cells["Icon"].Value = PolicyIcons.Images[GetImageIndexForSetting(policy)];
                    row.Cells["ID"].Value = GetPolicyID(policy);
                    row.Cells["State"].Value = GetPolicyState(policy);
                    row.Cells["_Name"].Value = policy.DisplayName;
                    row.Cells["Comment"].Value = GetPolicyCommentText(policy);
                    if (ReferenceEquals(policy, CurrentSetting)) // Keep the current policy selected
                    {
                        row.Selected = true;
                        PoliciesGrid.FirstDisplayedScrollingRowIndex = row.Index;
                    }
                    row.Tag = policy;
                }


                if (CategoriesTree.SelectedNode is null || !ReferenceEquals(CategoriesTree.SelectedNode.Tag, CurrentCategory)) // Update the tree view
                {
                    CategoriesTree.SelectedNode = CategoryNodes[CurrentCategory];
                }
            }
            ChangeColumnsAutoSize(true);
            if (inSameCategory) // Minimize the list view's jumping around when refreshing
            {
                if (PoliciesGrid.Rows.Count > topItemIndex)
                    PoliciesGrid.FirstDisplayedScrollingRowIndex = topItemIndex;
            }
            if (resetSort & PoliciesGrid.SortedColumn != null) {
                var sortedColumnIndex = PoliciesGrid.SortedColumn.Index;
                PoliciesGrid.Columns[sortedColumnIndex].SortMode = DataGridViewColumnSortMode.NotSortable;
                PoliciesGrid.Columns[sortedColumnIndex].SortMode = DataGridViewColumnSortMode.Automatic;
            }
            if (PoliciesGrid.SortOrder != SortOrder.None)
            {
                var order = PoliciesGrid.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
                PoliciesGrid.Sort(PoliciesGrid.SortedColumn, order);
            }
        }

        public void UpdatePolicyInfo()
        {
            // Update the middle pane with the selected object's information
            bool hasCurrentSetting = CurrentSetting is object | HighlightCategory is object | CurrentCategory is object;
            PolicyTitleLabel.Visible = hasCurrentSetting;
            PolicySupportedLabel.Visible = hasCurrentSetting;
            if (CurrentSetting is object)
            {
                PolicyTitleLabel.Text = CurrentSetting.DisplayName;
                if (CurrentSetting.SupportedOn is null)
                {
                    PolicySupportedLabel.Text = "(no requirements information)";
                }
                else
                {
                    PolicySupportedLabel.Text = "Requirements:" + Environment.NewLine + CurrentSetting.SupportedOn.DisplayName;
                }

                PolicyDescLabel.Text = PrettifyDescription(CurrentSetting.DisplayExplanation);
                PolicyIsPrefTable.Visible = IsPreference(CurrentSetting);
            }
            else if (HighlightCategory is object | CurrentCategory is object)
            {
                var shownCategory = HighlightCategory ?? CurrentCategory;
                PolicyTitleLabel.Text = shownCategory.DisplayName;
                PolicySupportedLabel.Text = (HighlightCategory is null ? "This" : "The selected") + " category contains " + shownCategory.Policies.Count + " policies and " + shownCategory.Children.Count + " subcategories.";
                PolicyDescLabel.Text = PrettifyDescription(shownCategory.DisplayExplanation);
                PolicyIsPrefTable.Visible = false;
            }
            else
            {
                PolicyDescLabel.Text = "Select an item to see its description.";
                PolicyIsPrefTable.Visible = false;
            }

            SettingInfoPanel_ClientSizeChanged(null, null);
        }

        public bool IsOrphanCategory(PolicyPlusCategory Category)
        {
            return Category.Parent is null & !string.IsNullOrEmpty(Category.RawCategory.ParentID);
        }

        public bool IsEmptyCategory(PolicyPlusCategory Category)
        {
            return Category.Children.Count == 0 & Category.Policies.Count == 0;
        }

        public int GetImageIndexForCategory(PolicyPlusCategory Category)
        {
            if (IsOrphanCategory(Category))
            {
                return 1; // Orphaned
            }
            else if (IsEmptyCategory(Category))
            {
                return 2; // Empty
            }
            else
            {
                return 0;
            } // Normal
        }

        public int GetImageIndexForSetting(PolicyPlusPolicy Setting)
        {
            if (IsPreference(Setting))
            {
                return 7; // Preference, not policy (exclamation mark)
            }
            else if (Setting.RawPolicy.Elements is null || Setting.RawPolicy.Elements.Count == 0)
            {
                return 4; // Normal
            }
            else
            {
                return 5;
            } // Extra configuration
        }

        public bool ShouldShowCategory(PolicyPlusCategory Category)
        {
            // Should this category be shown considering the current filter?
            if (ViewEmptyCategories)
            {
                return true;
            }
            else // Only if it has visible children
            {
                return Category.Policies.Any(ShouldShowPolicy) || Category.Children.Any(ShouldShowCategory);
            }
        }

        public bool ShouldShowPolicy(PolicyPlusPolicy Policy)
        {
            // Should this policy be shown considering the current filter and active sections?
            if (!PolicyVisibleInSection(Policy, ViewPolicyTypes))
                return false;
            if (ViewFilteredOnly)
            {
                bool visibleAfterFilter = false;
                if ((int)(ViewPolicyTypes & AdmxPolicySection.Machine) > 0 & PolicyVisibleInSection(Policy, AdmxPolicySection.Machine))
                {
                    if (IsPolicyVisibleAfterFilter(Policy, false))
                        visibleAfterFilter = true;
                }
                if (!visibleAfterFilter & (int)(ViewPolicyTypes & AdmxPolicySection.User) > 0 & PolicyVisibleInSection(Policy, AdmxPolicySection.User))
                {
                    if (IsPolicyVisibleAfterFilter(Policy, true))
                        visibleAfterFilter = true;
                }

                if (!visibleAfterFilter)
                    return false;
            }

            return true;
        }

        public void MoveToVisibleCategoryAndReload()
        {
            // Move up in the categories tree until a visible one is found
            var newFocusCategory = CurrentCategory;
            var newFocusPolicy = CurrentSetting;
            while (!(newFocusCategory is null) && !ShouldShowCategory(newFocusCategory))
            {
                newFocusCategory = newFocusCategory.Parent;
                newFocusPolicy = null;
            }

            if (newFocusPolicy is object && !ShouldShowPolicy(newFocusPolicy))
                newFocusPolicy = null;
            PopulateAdmxUi();
            CurrentCategory = newFocusCategory;
            UpdateCategoryListing(true);
            CurrentSetting = newFocusPolicy;
            UpdatePolicyInfo();
        }

        public string GetPolicyState(PolicyPlusPolicy Policy)
        {
            // Get a human-readable string describing the status of a policy, considering all active sections
            if (ViewPolicyTypes == AdmxPolicySection.Both)
            {
                string userState = GetPolicyState(Policy, AdmxPolicySection.User);
                string machState = GetPolicyState(Policy, AdmxPolicySection.Machine);
                var section = Policy.RawPolicy.Section;
                if (section == AdmxPolicySection.Both)
                {
                    if ((userState ?? "") == (machState ?? ""))
                    {
                        return userState + " (2)";
                    }
                    else if (userState == "Not Configured")
                    {
                        return machState + " (C)";
                    }
                    else if (machState == "Not Configured")
                    {
                        return userState + " (U)";
                    }
                    else
                    {
                        return "Mixed";
                    }
                }
                else if (section == AdmxPolicySection.Machine)
                    return machState + " (C)";
                else
                    return userState + " (U)";
            }
            else
            {
                return GetPolicyState(Policy, ViewPolicyTypes);
            }
        }

        public string GetPolicyState(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Get the human-readable status of a policy considering only one section
            switch (PolicyProcessing.GetPolicyState(Section == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource, Policy))
            {
                case PolicyState.Disabled:
                    {
                        return "Disabled";
                    }

                case PolicyState.Enabled:
                    {
                        return "Enabled";
                    }

                case PolicyState.NotConfigured:
                    {
                        return "Not Configured";
                    }

                default:
                    {
                        return "Unknown";
                    }
            }
        }
        public string GetPolicyID(PolicyPlusPolicy Policy)
        {
            return Policy.UniqueID.Split(':')[1];
        }

        public string GetPolicyCommentText(PolicyPlusPolicy Policy)
        {
            // Get the comment text to show in the Comment column, considering all active sections
            if (ViewPolicyTypes == AdmxPolicySection.Both)
            {
                string userComment = GetPolicyComment(Policy, AdmxPolicySection.User);
                string compComment = GetPolicyComment(Policy, AdmxPolicySection.Machine);
                if (string.IsNullOrEmpty(userComment) & string.IsNullOrEmpty(compComment))
                {
                    return "";
                }
                else if (!string.IsNullOrEmpty(userComment) & !string.IsNullOrEmpty(compComment))
                {
                    return "(multiple)";
                }
                else if (!string.IsNullOrEmpty(userComment))
                {
                    return userComment;
                }
                else
                {
                    return compComment;
                }
            }
            else
            {
                return GetPolicyComment(Policy, ViewPolicyTypes);
            }
        }

        public string GetPolicyComment(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Get a policy's comment in one section
            var commentSource = Section == AdmxPolicySection.Machine ? CompComments : UserComments;
            if (commentSource is null)
            {
                return "";
            }
            else if (commentSource.ContainsKey(Policy.UniqueID))
                return commentSource[Policy.UniqueID];
            else
                return "";
        }

        public bool IsPreference(PolicyPlusPolicy Policy)
        {
            return !string.IsNullOrEmpty(Policy.RawPolicy.RegistryKey) & !RegistryPolicyProxy.IsPolicyKey(Policy.RawPolicy.RegistryKey);
        }

        public void ShowSettingEditor(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Show the Edit Policy Setting dialog for a policy and reload if changes were made
            if (AppForms.EditSetting.PresentDialog(Policy, Section, AdmxWorkspace, CompPolicySource, UserPolicySource, CompPolicyLoader, UserPolicyLoader, CompComments, UserComments, GetPreferredLanguageCode()) == DialogResult.OK)
            {
                // Keep the selection where it is if possible
                if (CurrentCategory is null || ShouldShowCategory(CurrentCategory))
                    UpdateCategoryListing();
                else
                    MoveToVisibleCategoryAndReload();
            }
        }

        public void ClearSelections()
        {
            CurrentSetting = null;
            HighlightCategory = null;
        }

        public void OpenPolicyLoaders(PolicyLoader User, PolicyLoader Computer, bool Quiet)
        {
            // Create policy sources from the given loaders
            if (CompPolicyLoader is object | UserPolicyLoader is object)
                ClosePolicySources();
            UserPolicyLoader = User;
            UserPolicySource = User.OpenSource();
            CompPolicyLoader = Computer;
            CompPolicySource = Computer.OpenSource();
            bool allOk = true;
            string policyStatus(PolicyLoader Loader) { switch (Loader.GetWritability()) { case PolicySourceWritability.Writable: { return "is fully writable"; } case PolicySourceWritability.NoCommit: { allOk = false; return "cannot be saved"; } default: { allOk = false; return "cannot be modified"; } } }; // No writing
            Dictionary<string, string> loadComments(PolicyLoader Loader)
            {
                string cmtxPath = Loader.GetCmtxPath();
                if (string.IsNullOrEmpty(cmtxPath))
                {
                    return null;
                }
                else
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cmtxPath));
                        if (System.IO.File.Exists(cmtxPath))
                        {
                            return CmtxFile.Load(cmtxPath).ToCommentTable();
                        }
                        else
                        {
                            return new Dictionary<string, string>();
                        }
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            };
            string userStatus = policyStatus(User);
            string compStatus = policyStatus(Computer);
            UserComments = loadComments(User);
            CompComments = loadComments(Computer);
            UserSourceLabel.Text = UserPolicyLoader.GetDisplayInfo();
            ComputerSourceLabel.Text = CompPolicyLoader.GetDisplayInfo();
            if (allOk)
            {
                if (!Quiet)
                {
                    MessageBox.Show("Both the user and computer policy sources are loaded and writable.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                string msgText = "Not all policy sources are fully writable." + Environment.NewLine + Environment.NewLine + "The user source " + userStatus + "." + Environment.NewLine + Environment.NewLine + "The computer source " + compStatus + ".";
                MessageBox.Show(msgText, "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public void ClosePolicySources()
        {
            // Clean up the policy sources
            bool allOk = true;
            if (UserPolicyLoader is object)
            {
                if (!UserPolicyLoader.Close())
                    allOk = false;
            }

            if (CompPolicyLoader is object)
            {
                if (!CompPolicyLoader.Close())
                    allOk = false;
            }

            if (!allOk)
            {
                MessageBox.Show("Cleanup did not complete fully because the loaded resources are open in other programs.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public bool ShowSearchDialog(Func<PolicyPlusPolicy, bool> Searcher)
        {
            // Show the search dialog and make it start a search if appropriate
            DialogResult result;
            if (Searcher is null)
            {
                result = AppForms.FindResults.PresentDialog();
            }
            else
            {
                result = AppForms.FindResults.PresentDialogStartSearch(AdmxWorkspace, Searcher);
            }

            if (result == DialogResult.OK)
            {
                var selPol = AppForms.FindResults.SelectedPolicy;
                ShowSettingEditor(selPol, ViewPolicyTypes);
                FocusPolicy(selPol);
                return true;
            } else if (result == DialogResult.Retry) {
                return false;
            }
            return true;
        }

        public void ClearAdmxWorkspace()
        {
            // Clear out all the per-workspace bookkeeping
            AdmxWorkspace = new AdmxBundle();
            AppForms.FindResults.ClearSearch();
        }

        public void FocusPolicy(PolicyPlusPolicy Policy)
        {
            // Try to automatically select a policy in the list view
            if (CategoryNodes.ContainsKey(Policy.Category))
            {
                CurrentCategory = Policy.Category;
                UpdateCategoryListing();
                foreach (DataGridViewRow entry in PoliciesGrid.Rows)
                {
                    if (ReferenceEquals(entry.Tag, Policy))
                    {
                        entry.Selected = true;
                        PoliciesGrid.FirstDisplayedScrollingRowIndex = entry.Index;
                        break;
                    }
                }
            }
        }

        public bool IsPolicyVisibleAfterFilter(PolicyPlusPolicy Policy, bool IsUser)
        {
            // Calculate whether a policy is visible with the current filter
            if (CurrentFilter.ManagedPolicy.HasValue)
            {
                if (IsPreference(Policy) == CurrentFilter.ManagedPolicy.Value)
                    return false;
            }

            if (CurrentFilter.PolicyState.HasValue)
            {
                var policyState = PolicyProcessing.GetPolicyState(IsUser ? UserPolicySource : CompPolicySource, Policy);
                switch (CurrentFilter.PolicyState.Value)
                {
                    case FilterPolicyState.Configured:
                        {
                            if (policyState == PolicyState.NotConfigured)
                                return false;
                            break;
                        }

                    case FilterPolicyState.NotConfigured:
                        {
                            if (policyState != PolicyState.NotConfigured)
                                return false;
                            break;
                        }

                    case FilterPolicyState.Disabled:
                        {
                            if (policyState != PolicyState.Disabled)
                                return false;
                            break;
                        }

                    case FilterPolicyState.Enabled:
                        {
                            if (policyState != PolicyState.Enabled)
                                return false;
                            break;
                        }
                }
            }

            if (CurrentFilter.Commented.HasValue)
            {
                var commentDict = IsUser ? UserComments : CompComments;
                if ((commentDict.ContainsKey(Policy.UniqueID) && !string.IsNullOrEmpty(commentDict[Policy.UniqueID])) != CurrentFilter.Commented.Value)
                    return false;
            }

            if (CurrentFilter.AllowedProducts is object)
            {
                if (!PolicyProcessing.IsPolicySupported(Policy, CurrentFilter.AllowedProducts, CurrentFilter.AlwaysMatchAny, CurrentFilter.MatchBlankSupport))
                    return false;
            }

            return true;
        }

        public bool PolicyVisibleInSection(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Does this policy apply to the given section?
            return (int)(Policy.RawPolicy.Section & Section) > 0;
        }

        public PolFile GetOrCreatePolFromPolicySource(IPolicySource Source)
        {
            if (Source is PolFile)
            {
                // If it's already a POL, just save it
                return (PolFile)Source;
            }
            else if (Source is RegistryPolicyProxy)
            {
                // Recurse through the Registry branch and create a POL
                var regRoot = ((RegistryPolicyProxy)Source).EncapsulatedRegistry;
                var pol = new PolFile();
                void addSubtree(string PathRoot, RegistryKey Key)
                {
                    foreach (var valName in Key.GetValueNames())
                    {
                        var valData = Key.GetValue(valName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        pol.SetValue(PathRoot, valName, valData, Key.GetValueKind(valName));
                    }

                    foreach (var subkeyName in Key.GetSubKeyNames())
                    {
                        using (var subkey = Key.OpenSubKey(subkeyName, false))
                        {
                            addSubtree(PathRoot + @"\" + subkeyName, subkey);
                        }
                    }
                }
                foreach (var policyPath in RegistryPolicyProxy.PolicyKeys)
                {
                    using (var policyKey = regRoot.OpenSubKey(policyPath, false))
                    {
                        addSubtree(policyPath, policyKey);
                    }
                }

                return pol;
            }
            else
            {
                throw new InvalidOperationException("Policy source type not supported");
            }
        }

        public DialogResult DisplayAdmxLoadErrorReport(IEnumerable<AdmxLoadFailure> Failures, bool AskContinue = false)
        {
            var failureList = Failures?.ToList() ?? new List<AdmxLoadFailure>();
            if (failureList.Count == 0)
                return DialogResult.OK;
            string header = "Errors were encountered while adding administrative templates to the workspace.";
            var buttons = AskContinue ? MessageBoxButtons.YesNo : MessageBoxButtons.OK;
            return MessageBox.Show(header + (AskContinue ? " Continue trying to use this workspace?" : "") + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, failureList.Select(f => f.ToString())), "Policy Plus", buttons, MessageBoxIcon.Exclamation);
        }

        public string GetPreferredLanguageCode()
        {
            return Convert.ToString(Configuration.GetValue("LanguageCode", System.Globalization.CultureInfo.CurrentCulture.Name));
        }

        private void CategoriesTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // When the user selects a new category in the left pane
            CurrentCategory = (PolicyPlusCategory)e.Node.Tag;
            UpdateCategoryListing(true);
            ClearSelections();
            UpdatePolicyInfo();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void OpenADMXFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Open ADMX Folder dialog and load the policy definitions
            if (AppForms.OpenAdmxFolder.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (AppForms.OpenAdmxFolder.ClearWorkspace)
                        ClearAdmxWorkspace();
                    DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFolder(AppForms.OpenAdmxFolder.SelectedFolder, GetPreferredLanguageCode()));
                    // Only update the last source when successfully opening a complete source
                    if (AppForms.OpenAdmxFolder.ClearWorkspace)
                        Configuration.SetValue("AdmxSource", AppForms.OpenAdmxFolder.SelectedFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The folder could not be fully added to the workspace. " + ex.Message, "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

                PopulateAdmxUi();
            }
        }

        private void OpenADMXFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Open a single ADMX file
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Policy definitions files|*.admx";
                ofd.Title = "Open ADMX file";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;
                try
                {
                    DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFile(ofd.FileName, GetPreferredLanguageCode()));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The ADMX file could not be added to the workspace. " + ex.Message, "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

                PopulateAdmxUi();
            }
        }

        private void CloseADMXWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Close all policy definitions and clear the workspace
            ClearAdmxWorkspace();
            PopulateAdmxUi();
        }

        private void EmptyCategoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Toggle whether empty categories are visible
            ViewEmptyCategories = !ViewEmptyCategories;
            EmptyCategoriesToolStripMenuItem.Checked = ViewEmptyCategories;
            MoveToVisibleCategoryAndReload();
        }

        private void ComboAppliesTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            // When the user chooses a different section to work with
            switch (ComboAppliesTo.Text ?? "")
            {
                case "User":
                    {
                        ViewPolicyTypes = AdmxPolicySection.User;
                        break;
                    }

                case "Computer":
                    {
                        ViewPolicyTypes = AdmxPolicySection.Machine;
                        break;
                    }

                default:
                    {
                        ViewPolicyTypes = AdmxPolicySection.Both;
                        break;
                    }
            }

            MoveToVisibleCategoryAndReload();
        }

 
        private void DeduplicatePoliciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Make otherwise-identical pairs of user and computer policies into single dual-section policies
            ClearSelections();
            int deduped = PolicyProcessing.DeduplicatePolicies(AdmxWorkspace);
            MessageBox.Show("Deduplicated " + deduped + " policies.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateCategoryListing();
            UpdatePolicyInfo();
        }

        private void FindByIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Find By ID window and try to move to the selected object
            AppForms.FindById.AdmxWorkspace = AdmxWorkspace;
            if (AppForms.FindById.ShowDialog() == DialogResult.OK)
            {
                var selCat = AppForms.FindById.SelectedCategory;
                var selPol = AppForms.FindById.SelectedPolicy;
                var selPro = AppForms.FindById.SelectedProduct;
                var selSup = AppForms.FindById.SelectedSupport;
                if (selCat is object)
                {
                    if (CategoryNodes.ContainsKey(selCat))
                    {
                        CurrentCategory = selCat;
                        UpdateCategoryListing();
                    }
                    else
                    {
                        MessageBox.Show("The category is not currently visible. Change the view settings and try again.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                else if (selPol is object)
                {
                    ShowSettingEditor(selPol, (AdmxPolicySection)Math.Min((int)ViewPolicyTypes, (int)AppForms.FindById.SelectedSection));
                    FocusPolicy(selPol);
                }
                else if (selPro is object)
                {
                    AppForms.DetailProduct.PresentDialog(selPro);
                }
                else if (selSup is object)
                {
                    AppForms.DetailSupport.PresentDialog(selSup);
                }
                else
                {
                    MessageBox.Show("That object could not be found.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }

        private void OpenPolicyResourcesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Open Policy Resources dialog and open its loaders
            if (AppForms.OpenPol.ShowDialog() == DialogResult.OK)
            {
                OpenPolicyLoaders(AppForms.OpenPol.SelectedUser, AppForms.OpenPol.SelectedComputer, false);
                MoveToVisibleCategoryAndReload();
            }
        }

        private void SavePoliciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Save policy state and comments to disk
            // Doesn't matter, it's just comments
            void saveComments(Dictionary<string, string> Comments, PolicyLoader Loader) { try { if (Comments is object) CmtxFile.FromCommentTable(Comments).Save(Loader.GetCmtxPath()); } catch (Exception) { } };
            saveComments(UserComments, UserPolicyLoader);
            saveComments(CompComments, CompPolicyLoader);
            try
            {
                string compStatus = "not writable";
                string userStatus = "not writable";
                if (CompPolicyLoader.GetWritability() == PolicySourceWritability.Writable)
                    compStatus = CompPolicyLoader.Save();
                if (UserPolicyLoader.GetWritability() == PolicySourceWritability.Writable)
                    userStatus = UserPolicyLoader.Save();
                Configuration.SetValue("CompSourceType", (int)CompPolicyLoader.Source);
                Configuration.SetValue("UserSourceType", (int)UserPolicyLoader.Source);
                Configuration.SetValue("CompSourceData", CompPolicyLoader.LoaderData ?? "");
                Configuration.SetValue("UserSourceData", UserPolicyLoader.LoaderData ?? "");
                MessageBox.Show("Success." + Environment.NewLine + Environment.NewLine + "User policies: " + userStatus + "." + Environment.NewLine + Environment.NewLine + "Computer policies: " + compStatus + ".", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Saving failed!" + Environment.NewLine + Environment.NewLine + ex.Message, "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show author and version information if it was compiled into the program
            string about = $"Policy Plus by Ben Nordick.{System.Environment.NewLine}Modded by tttza.{System.Environment.NewLine}{System.Environment.NewLine}Available on GitHub: tttza/PolicyPlus.";
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5);
            if (!string.IsNullOrEmpty(version))
                about += $"{System.Environment.NewLine} Version: {version}";
            MessageBox.Show(about, "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ByTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var shouldClose = false;
            while (!shouldClose){
                // Show the Find By Text window and start the search
                if (AppForms.FindByText.PresentDialog(UserComments, CompComments) == DialogResult.OK)
                {
                    shouldClose = ShowSearchDialog(AppForms.FindByText.Searcher);
                }
                else
                {
                    break;
                }
            }
        }

        private void SearchResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the search results window but don't start a search
            ShowSearchDialog(null);
        }

        private void FindNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Move to the next policy in the search results
            do
            {
                var nextPol = AppForms.FindResults.NextPolicy();
                if (nextPol is null)
                {
                    MessageBox.Show("There are no more results that match the filter.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                }
                else if (ShouldShowPolicy(nextPol))
                {
                    FocusPolicy(nextPol);
                    break;
                }
            }
            while (true);
        }

        private void ByRegistryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var shouldClose = false;
            while (!shouldClose)
            {
                // Show the Find By Registry window and start the search
                if (AppForms.FindByRegistry.ShowDialog() == DialogResult.OK)
                {
                    shouldClose = ShowSearchDialog(AppForms.FindByRegistry.Searcher);
                }
                else
                {
                    break;
                }
            }
        }

        private void SettingInfoPanel_ClientSizeChanged(object sender, EventArgs e)
        {

        }

        private void Main_Closed(object sender, EventArgs e)
        {
            ClosePolicySources(); // Make sure everything is cleaned up before quitting
        }
        private void FilterOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Filter Options dialog and refresh if the filter changes
            if (AppForms.FilterOptions.PresentDialog(CurrentFilter, AdmxWorkspace) == DialogResult.OK)
            {
                CurrentFilter = AppForms.FilterOptions.CurrentFilter;
                ViewFilteredOnly = true;
                OnlyFilteredObjectsToolStripMenuItem.Checked = true;
                MoveToVisibleCategoryAndReload();
            }
        }

        private void OnlyFilteredObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Toggle whether the filter is used
            ViewFilteredOnly = !ViewFilteredOnly;
            OnlyFilteredObjectsToolStripMenuItem.Checked = ViewFilteredOnly;
            MoveToVisibleCategoryAndReload();
        }

        private void ImportSemanticPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Open the SPOL import dialog and apply the data
            if (AppForms.ImportSpol.ShowDialog() == DialogResult.OK)
            {
                var spol = AppForms.ImportSpol.Spol;
                int fails = spol.ApplyAll(AdmxWorkspace, UserPolicySource, CompPolicySource, UserComments, CompComments);
                MoveToVisibleCategoryAndReload();
                if (fails == 0)
                {
                    MessageBox.Show("Semantic Policy successfully applied.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(fails + " out of " + spol.Policies.Count + " could not be applied.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }

        private void ImportPOLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Open a POL file and write it to a policy source
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "POL files|*.pol";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    PolFile pol = null;
                    try
                    {
                        pol = PolFile.Load(ofd.FileName);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("The POL file could not be loaded.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }

                    if (AppForms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
                    {
                        var section = AppForms.OpenSection.SelectedSection == AdmxPolicySection.User ? UserPolicySource : CompPolicySource;
                        pol.Apply(section);
                        MoveToVisibleCategoryAndReload();
                        MessageBox.Show("POL import successful.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void ExportPOLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Create a POL file from a current policy source
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "POL files|*.pol";
                if (sfd.ShowDialog() == DialogResult.OK && AppForms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
                {
                    var section = AppForms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource;
                    try
                    {
                        GetOrCreatePolFromPolicySource(section).Save(sfd.FileName);
                        MessageBox.Show("POL exported successfully.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("The POL file could not be saved.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
        }

        private void AcquireADMXFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Acquire ADMX Files dialog and load the new ADMX files
            if (AppForms.DownloadAdmx.ShowDialog() == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(AppForms.DownloadAdmx.NewPolicySourceFolder))
                {
                    ClearAdmxWorkspace();
                    DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFolder(AppForms.DownloadAdmx.NewPolicySourceFolder, GetPreferredLanguageCode()));
                    Configuration.SetValue("AdmxSource", AppForms.DownloadAdmx.NewPolicySourceFolder);
                    PopulateAdmxUi();
                }
            }
        }

        private void LoadedADMXFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppForms.LoadedAdmx.PresentDialog(AdmxWorkspace);
        }

        private void AllSupportDefinitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppForms.LoadedSupportDefinitions.PresentDialog(AdmxWorkspace);
        }

        private void AllProductsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppForms.LoadedProducts.PresentDialog(AdmxWorkspace);
        }

        private void EditRawPOLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool userIsPol = UserPolicySource is PolFile;
            bool compIsPol = CompPolicySource is PolFile;
            if (!(userIsPol | compIsPol))
            {
                MessageBox.Show("Neither loaded source is backed by a POL file.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (Convert.ToInt32(Configuration.GetValue("EditPolDangerAcknowledged", 0)) == 0)
            {
                if (MessageBox.Show("Caution! This tool is for very advanced users. Improper modifications may result in inconsistencies in policies' states." + Environment.NewLine + Environment.NewLine + "Changes operate directly on the policy source, though they will not be committed to disk until you save. Are you sure you want to continue?", "Policy Plus", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                    return;
                Configuration.SetValue("EditPolDangerAcknowledged", 1);
            }

            if (AppForms.OpenSection.PresentDialog(userIsPol, compIsPol) == DialogResult.OK)
            {
                AppForms.EditPol.PresentDialog(PolicyIcons, (PolFile)(AppForms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource), AppForms.OpenSection.SelectedSection == AdmxPolicySection.User);
            }

            MoveToVisibleCategoryAndReload();
        }

        private void ExportREGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AppForms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
            {
                var source = AppForms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource;
                AppForms.ExportReg.PresentDialog("", GetOrCreatePolFromPolicySource(source), AppForms.OpenSection.SelectedSection == AdmxPolicySection.User);
            }
        }

        private void ImportREGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AppForms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
            {
                var source = AppForms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource;
                if (AppForms.ImportReg.PresentDialog(source) == DialogResult.OK)
                    MoveToVisibleCategoryAndReload();
            }
        }

        private void SetADMLLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (AppForms.LanguageOptions.PresentDialog(GetPreferredLanguageCode()) == DialogResult.OK)
            {
                Configuration.SetValue("LanguageCode", AppForms.LanguageOptions.NewLanguage);
                if (MessageBox.Show("Language changes will take effect when ADML files are next loaded. Would you like to reload the workspace now?", "Policy Plus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ClearAdmxWorkspace();
                    OpenLastAdmxSource();
                    PopulateAdmxUi();
                }
            }
        }

        private void PolicyObjectContext_DropdownOpening(object sender, EventArgs e)
        {

        }

        private void PolicyObjectContext_Opening(object sender, CancelEventArgs e)
        {
            // When the right-click menu is opened
            bool showingForCategory;
            if (ReferenceEquals(PolicyObjectContext.SourceControl, CategoriesTree))
            {
                showingForCategory = true;
                PolicyObjectContext.Tag = CategoriesTree.SelectedNode.Tag;
            }
            else if (PoliciesGrid.SelectedRows.Count > 0) // Shown from the main view
            {
                var selEntryTag = PoliciesGrid.SelectedRows[0].Tag;
                showingForCategory = selEntryTag is PolicyPlusCategory;
                PolicyObjectContext.Tag = selEntryTag;
            }
            else
            {
                e.Cancel = true;
                return;
            }

            var section = AdmxPolicySection.Both;
            if (PolicyObjectContext.Tag is PolicyPlusPolicy)
            {
                section = ((PolicyPlusPolicy)PolicyObjectContext.Tag).RawPolicy.Section;
            }
            else { section = 0; }
            // Items are tagged in the designer for the objects they apply to
            foreach (var item in PolicyObjectContext.Items.OfType<ToolStripMenuItem>())
            {
                if (item.HasDropDownItems)
                {
                    foreach (var item2 in item.DropDownItems.OfType<ToolStripDropDownItem>())
                    {
                        bool ok2 = true;
                        if (Convert.ToString(item2.Tag) == "P" & showingForCategory)
                            ok2 = false;
                        if (Convert.ToString(item2.Tag) == "C" & !showingForCategory)
                            ok2 = false;
                        if (ok2 & Convert.ToString(item2.Tag) == "P-LM" & section == AdmxPolicySection.User)
                            ok2 = false;
                        if (ok2 & Convert.ToString(item2.Tag) == "P-CU" & section == AdmxPolicySection.Machine)
                            ok2 = false;
                        item2.Visible = ok2;
                    }
                }
                bool ok = true;
                if (Convert.ToString(item.Tag) == "P" & showingForCategory)
                    ok = false;
                if (Convert.ToString(item.Tag) == "C" & !showingForCategory)
                    ok = false;
                if (ok & Convert.ToString(item.Tag) == "P-LM" & showingForCategory & section == AdmxPolicySection.User)
                    ok = false;
                if (ok & Convert.ToString(item.Tag) == "P-CU" & showingForCategory & section == AdmxPolicySection.Machine)
                    ok = false;
                item.Visible = ok;
            }
        }

        private void PolicyObjectContext_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // When the user clicks an item in the right-click menu
            var polObject = PolicyObjectContext.Tag; // The current policy object is in the Tag field
            if (ReferenceEquals(e.ClickedItem, CmeCatOpen))
            {
                CurrentCategory = (PolicyPlusCategory)polObject;
                UpdateCategoryListing();
            }
            else if (ReferenceEquals(e.ClickedItem, CmePolEdit))
            {
                ShowSettingEditor((PolicyPlusPolicy)polObject, ViewPolicyTypes);
            }
            else if (ReferenceEquals(e.ClickedItem, CmeAllDetails))
            {
                if (polObject is PolicyPlusCategory)
                {
                    AppForms.DetailCategory.PresentDialog((PolicyPlusCategory)polObject);
                }
                else
                {
                    AppForms.DetailPolicy.PresentDialog((PolicyPlusPolicy)polObject);
                }
            }
            else if (ReferenceEquals(e.ClickedItem, CmeAllDetailsFormatted))
            {
                AppForms.DetailPolicyFormatted.PresentDialog((PolicyPlusPolicy)polObject, CompPolicySource, UserPolicySource, GetPreferredLanguageCode());
            }
            else if (ReferenceEquals(e.ClickedItem, CmePolInspectElements))
            {
                AppForms.InspectPolicyElements.PresentDialog((PolicyPlusPolicy)polObject, PolicyIcons, AdmxWorkspace);
            }
            else if (ReferenceEquals(e.ClickedItem, CmePolSpolFragment))
            {
                AppForms.InspectSpolFragment.PresentDialog((PolicyPlusPolicy)polObject, AdmxWorkspace, CompPolicySource, UserPolicySource, CompComments, UserComments);
            }
            else
            {
                CopyToClipboard(polObject, e);
            }
        }

        private void UpdateSelectedRowInfo(int rowIndex)
        {
            // When the user highlights an item in the right pane
            if (rowIndex >= 0)
            {
                var row = PoliciesGrid.Rows[rowIndex];
                var selObject = row.Tag;
                if (selObject is PolicyPlusPolicy)
                {
                    CurrentSetting = (PolicyPlusPolicy)selObject;
                    HighlightCategory = null;
                }
                else if (selObject is PolicyPlusCategory)
                {
                    HighlightCategory = (PolicyPlusCategory)selObject;
                    CurrentSetting = null;
                }
            }
            else
            {
                ClearSelections();
            }

            Invoke(new Action(() => UpdatePolicyInfo()));
        }

        private void PoliciesGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            UpdateSelectedRowInfo(e.RowIndex);
        }

        private void PoliciesGrid_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) { return; }
            var clickedItem = PoliciesGrid.Rows[e.RowIndex];
            // When the user opens a policy object in the right pane
            var policyItem = clickedItem.Tag;
            if (policyItem is PolicyPlusCategory)
            {
                CurrentCategory = (PolicyPlusCategory)policyItem;
                UpdateCategoryListing();
            }
            else if (policyItem is PolicyPlusPolicy)
            {
                ShowSettingEditor((PolicyPlusPolicy)policyItem, ViewPolicyTypes);
            }
        }

        private void PoliciesGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (PoliciesGrid.SelectedRows.Count > 0)
            {
                UpdateSelectedRowInfo(PoliciesGrid.SelectedRows[0].Index);
            }
        }

        private void PolicyDescLabel_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start(e.LinkText);
        }

        private void CopyToClipboard(object polObject, ToolStripItemClickedEventArgs e)
        {
            if (polObject is PolicyPlusPolicy)
            {
                if (ReferenceEquals(e.ClickedItem, Cme2CopyId) | ReferenceEquals(e.ClickedItem, CmeCopyToClipboard))
                {
                    Clipboard.SetText(((PolicyPlusPolicy)polObject).UniqueID.Split(':')[1]);
                }
                else if (ReferenceEquals(e.ClickedItem, Cme2CopyName))
                {
                    Clipboard.SetText(((PolicyPlusPolicy)polObject).DisplayName);
                }
                else if (ReferenceEquals(e.ClickedItem, Cme2CopyRegPathLC))
                {
                    var rawPolicy = ((PolicyPlusPolicy)polObject).RawPolicy;
                    var text = @"HKEY_LOCAL_MACHINE\" + rawPolicy.RegistryKey;
                    if (rawPolicy.RegistryValue != null)
                    {
                        text += @"\" + rawPolicy.RegistryValue;
                    }
                    Clipboard.SetText(text);
                }
                else if (ReferenceEquals(e.ClickedItem, Cme2CopyRegPathCU))
                {
                    var rawPolicy = ((PolicyPlusPolicy)polObject).RawPolicy;
                    var text = @"HKEY_CURRENT_USER\" + rawPolicy.RegistryKey;
                    if (rawPolicy.RegistryValue != null)
                    {
                        text += @"\" + rawPolicy.RegistryValue;
                    }
                    Clipboard.SetText(text);
                }
            }
        }

        private void CategoriesTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // Right-clicking doesn't actually select the node by default
            if (e.Button == MouseButtons.Right)
                CategoriesTree.SelectedNode = e.Node;
        }

        public static string PrettifyDescription(string Description)
        {
            if (Description == null)
                return "";
            // Remove extra indentation from paragraphs
            var sb = new StringBuilder();
            foreach (var line in Description.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                sb.AppendLine(line.Trim());
            return sb.ToString().TrimEnd();
        }
    }
}