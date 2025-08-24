using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests
{
    // Detect dialogs which break when moving from 125%/100% monitor to 100%/125% monitor
    [Collection(UiTestCollection.Name)]
    public class DpiLayoutTests
    {
        public static IEnumerable<object[]> DialogFactories()
        {
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.UI.PolicyDetail.EditSetting()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.UI.PolicyDetail.EditPol()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.UI.PolicyDetail.DetailPolicyFormatted()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.ListEditor()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.UI.Find.FindByRegistry()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.UI.Find.FindById()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.LanguageOptions()) };
            yield return new object[] { (Func<Form>)(() => new PolicyPlus.UI.Find.FindByText()) };
        }

        [Theory]
        [MemberData(nameof(DialogFactories))]
        public void Dialog_Should_Not_Break_When_Moving_From_125_To_100(Func<Form> createDialog)
        {
            var report = DpiTestHelper.RunInSta(() =>
            {
                using var dlg = createDialog();
                DpiTestHelper.InitializeForm(dlg, initialDpiScale: 1.25f);
                DpiTestHelper.SimulateDpiChange(dlg, 96);
                return DpiTestHelper.AnalyzeLayout(dlg);
            });
            Assert.False(report.HasIssues, report.ToString());
        }

        [Theory]
        [MemberData(nameof(DialogFactories))]
        public void Dialog_Should_Not_Break_When_Moving_From_100_To_125(Func<Form> createDialog)
        {
            var report = DpiTestHelper.RunInSta(() =>
            {
                using var dlg = createDialog();
                DpiTestHelper.InitializeForm(dlg, initialDpiScale: 1.0f);
                DpiTestHelper.SimulateDpiChange(dlg, 120);
                return DpiTestHelper.AnalyzeLayout(dlg);
            });
            Assert.False(report.HasIssues, report.ToString());
        }

        [Theory]
        [MemberData(nameof(DialogFactories))]
        public void Dialog_Should_Not_Break_When_Moving_From_125_To_150(Func<Form> createDialog)
        {
            var report = DpiTestHelper.RunInSta(() =>
            {
                using var dlg = createDialog();
                DpiTestHelper.InitializeForm(dlg, initialDpiScale: 1.25f);
                DpiTestHelper.SimulateDpiChange(dlg, 144);
                return DpiTestHelper.AnalyzeLayout(dlg);
            });
            Assert.False(report.HasIssues, report.ToString());
        }
    }
}
