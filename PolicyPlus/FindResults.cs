using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class FindResults
    {
        public FindResults()
        {
            InitializeComponent();
        }

        private AdmxBundle AdmxWorkspace;
        private Func<PolicyPlusPolicy, bool> SearchFunc;
        private bool CancelingSearch = false;
        private bool CancelDueToFormClose = false;
        private bool SearchPending = false;
        private bool HasSearched;
        private int LastSelectedIndex;
        public PolicyPlusPolicy SelectedPolicy;

        public DialogResult PresentDialogStartSearch(AdmxBundle Workspace, Func<PolicyPlusPolicy, bool> Searcher)
        {
            // Start running a search defined by one of the Find By windows
            AdmxWorkspace = Workspace;
            SearchFunc = Searcher;
            ResultsListview.Items.Clear();
            SearchProgress.Maximum = Workspace.Policies.Count;
            SearchProgress.Value = 0;
            StopButton.Enabled = true;
            CancelingSearch = false;
            CancelDueToFormClose = false;
            ProgressLabel.Text = "Starting search";
            SearchPending = true;
            HasSearched = true;
            LastSelectedIndex = -1;
            GoButton.Enabled = false;
            return ShowDialog();
        }

        public DialogResult PresentDialog()
        {
            // Open the dialog normally, like from the main form
            if (!HasSearched)
            {
                Interaction.MsgBox("No search has been run yet, so there are no results to display.", MsgBoxStyle.Information);
                return DialogResult.Cancel;
            }

            CancelingSearch = false;
            CancelDueToFormClose = false;
            SearchPending = false;
            return ShowDialog();
        }

        public void ClearSearch()
        {
            HasSearched = false;
            ResultsListview.Items.Clear();
        }

        public PolicyPlusPolicy NextPolicy()
        {
            if (LastSelectedIndex >= ResultsListview.Items.Count - 1 | !HasSearched)
                return null;
            LastSelectedIndex += 1;
            return (PolicyPlusPolicy)ResultsListview.Items[LastSelectedIndex].Tag;
        }

        private List<string> GetParentNames(PolicyPlusCategory category, List<string> namesList = null)
        {
            if (namesList == null)
            {
                namesList = new List<string>();
            }

            if (category.Parent is not null)
            {
                namesList = GetParentNames(category.Parent, namesList);
            }
            namesList.Add(category.DisplayName);
            return namesList;
        }

        public void SearchJob(AdmxBundle Workspace, Func<PolicyPlusPolicy, bool> Searcher)
        {
            // The long-running task that searches all the policies
            int searchedSoFar = 0;
            int results = 0;
            bool stoppedByCancel = false;
            var pendingInsertions = new List<PolicyPlusPolicy>();
            void addPendingInsertions()
            {
                ResultsListview.BeginUpdate();
                foreach (var insert in pendingInsertions)
                {
                    var directoryNames = GetParentNames(insert.Category);
                    var lsvi = ResultsListview.Items.Add(directoryNames[0]);
                    lsvi.Tag = insert;
                    lsvi.SubItems.Add(insert.DisplayName);
                    lsvi.SubItems.Add(insert.Category.DisplayName);
                    lsvi.SubItems.Add("" + string.Join(" - ", directoryNames) + "");
                }

                ResultsListview.EndUpdate();
                pendingInsertions.Clear();
            };
            foreach (var policy in Workspace.Policies)
            {
                object argaddress = CancelingSearch;
                if (Conversions.ToBoolean(System.Threading.Thread.VolatileRead(ref argaddress)))
                {
                    stoppedByCancel = true;
                    break;
                }

                searchedSoFar += 1;
                bool isHit = Searcher(policy.Value); // The potentially expensive check
                if (isHit)
                {
                    results += 1;
                    pendingInsertions.Add(policy.Value);
                }

                if (searchedSoFar % 100 == 0) // UI updating is costly
                {
                    Invoke(new Action(() =>
                    {
                        addPendingInsertions();
                        SearchProgress.Value = searchedSoFar;
                        ProgressLabel.Text = "Searching: checked " + searchedSoFar + " policies so far, found " + results + " hits";
                    }));
                }
            }

            object localVolatileRead() { object argaddress = CancelDueToFormClose; var ret = System.Threading.Thread.VolatileRead(ref argaddress); return ret; }

            object localVolatileRead1() { object argaddress = CancelDueToFormClose; var ret = System.Threading.Thread.VolatileRead(ref argaddress); return ret; }

            if (stoppedByCancel && Conversions.ToBoolean(localVolatileRead1()))
                return; // Avoid accessing a disposed object
            Invoke(new Action(() =>
            {
                addPendingInsertions();
                string status = stoppedByCancel ? "Search canceled" : "Finished searching";
                ProgressLabel.Text = status + ": checked " + searchedSoFar + " policies, found " + results + " hits";
                SearchProgress.Value = SearchProgress.Maximum;
                StopButton.Enabled = false;
                if (ResultsListview.Items.Count == 0)
                {
                    BackToRegSearchBtn.Focus();
                } 
                else
                {   
                    if (LastSelectedIndex == -1)
                    {
                        ResultsListview.Items[0].Focused = true;
                        ResultsListview.Items[0].Selected = true;
                    }
                    if (ResultsListview.Items.Count == 1)
                    {
                        ResultsListview.Items[0].Focused = true;
                        ResultsListview.Items[0].Selected = true;
                        GoButton.Enabled = true;
                        GoButton.Focus();
                    }
                }
            }));
        }

        public void StopSearch(bool Force)
        {
            object argaddress = CancelingSearch;
            System.Threading.Thread.VolatileWrite(ref argaddress, true);
            object argaddress1 = CancelDueToFormClose;
            System.Threading.Thread.VolatileWrite(ref argaddress1, Force);
        }

        private void FindResults_Shown(object sender, EventArgs e)
        {
            if (SearchPending)
            {
                Task.Factory.StartNew(() => SearchJob(AdmxWorkspace, SearchFunc));
            }
            else if (LastSelectedIndex >= 0 & LastSelectedIndex < ResultsListview.Items.Count)
            {
                // Restore the last selection
                var lastSelected = ResultsListview.Items[LastSelectedIndex];
                lastSelected.Selected = true;
                lastSelected.Focused = true;
                lastSelected.EnsureVisible();
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            StopSearch(false);
        }

        private void ResultsListview_SizeChanged(object sender, EventArgs e)
        {
            //ChTitle.Width = ResultsListview.ClientSize.Width - ChCategory.Width;
        }

        private void FindResults_Closing(object sender, CancelEventArgs e)
        {
            StopSearch(true);
            if (SearchProgress.Value != SearchProgress.Maximum)
            {
                ProgressLabel.Text = "Search canceled";
                SearchProgress.Maximum = 100;
                SearchProgress.Value = SearchProgress.Maximum;
            }
        }

        private void GoClicked(object sender, EventArgs e)
        {
            if (ResultsListview.SelectedItems.Count == 0)
                return;
            SelectedPolicy = (PolicyPlusPolicy)ResultsListview.SelectedItems[0].Tag;
            LastSelectedIndex = ResultsListview.SelectedIndices[0]; // Remember which item is selected
            DialogResult = DialogResult.OK;
        }

        private void FindResults_Load(object sender, EventArgs e)
        {
            // Enable double-buffering for the results view
            var doubleBufferProp = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            doubleBufferProp.SetValue(ResultsListview, true);
        }

        private void ResultsListview_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ResultsListview.SelectedItems.Count == 0)
            {
                GoButton.Enabled = false;
            } else
            {
                LastSelectedIndex = ResultsListview.SelectedIndices[0]; // Remember which item is selected
                GoButton.Enabled = true;
            }
        }
    }
}