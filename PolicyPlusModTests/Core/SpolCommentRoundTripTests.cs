using System;
using System.Linq;
using Xunit;

namespace PolicyPlusModTests.Core
{
    public class SpolCommentRoundTripTests
    {
        private static string BuildSinglePolicySpol(SpolPolicyState state)
        {
            return "Policy Plus Semantic Policy"
                + Environment.NewLine
                + SpolFile.GetFragment(state);
        }

        private static SpolPolicyState RoundTrip(SpolPolicyState original)
        {
            var text = BuildSinglePolicySpol(original);
            var parsed = SpolFile.FromText(text);
            return parsed.Policies.Single(p => p.UniqueID == original.UniqueID);
        }

        [Theory]
        [InlineData("a\\b")]
        [InlineData("a\\\\b")] // already double backslash sequences
        [InlineData("end\\")] // trailing backslash
        [InlineData("line1\r\nline2")] // newline sequence in comment
        [InlineData("\\")] // single backslash only
        public void Comment_With_Backslashes_RoundTrips(string comment)
        {
            var state = new SpolPolicyState
            {
                UniqueID = "TestPolicy",
                Section = AdmxPolicySection.User,
                BasicState = PolicyState.NotConfigured,
                Comment = comment,
            };
            var rt = RoundTrip(state);
            Assert.Equal(comment, rt.Comment);
        }
    }
}
