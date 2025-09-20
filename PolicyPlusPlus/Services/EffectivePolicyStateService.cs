using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.ViewModels;

namespace PolicyPlusPlus.Services
{
    internal sealed class EffectivePolicyStateService
    {
        public static EffectivePolicyStateService Instance { get; } = new();

        private EffectivePolicyStateService() { }

        private PendingChange? FindPending(string policyId, string scope)
        {
            try
            {
                return PendingChangesService.Instance.Pending.FirstOrDefault(p =>
                    string.Equals(p.PolicyId, policyId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Scope, scope, StringComparison.OrdinalIgnoreCase)
                );
            }
            catch
            {
                return null;
            }
        }

        private (PolicyState state, Dictionary<string, object>? options) GetBase(
            PolicyPlusCore.IO.IPolicySource source,
            PolicyPlusPolicy policy
        )
        {
            if (source == null)
                return (PolicyState.Unknown, null);
            try
            {
                var st = PolicyProcessing.GetPolicyState(source, policy);
                if (st == PolicyState.Enabled)
                {
                    try
                    {
                        var opts = PolicyProcessing.GetPolicyOptionStates(source, policy);
                        return (st, opts);
                    }
                    catch { }
                }
                return (st, null);
            }
            catch
            {
                return (PolicyState.Unknown, null);
            }
        }

        public void ApplyEffectiveToRow(
            QuickEditRow row,
            PolicyPlusCore.IO.IPolicySource compSource,
            PolicyPlusCore.IO.IPolicySource userSource
        )
        {
            if (row == null)
                return;
            if (row.SupportsComputer && compSource != null)
            {
                var (baseStateC, baseOptsC) = GetBase(compSource, row.Policy);
                var pendC = FindPending(row.Policy.UniqueID, "Computer");
                var stateC = pendC?.DesiredState ?? baseStateC;
                var optsC = pendC?.Options ?? baseOptsC;
                row.ApplyExternal("Computer", stateC, optsC);
            }
            if (row.SupportsUser && userSource != null)
            {
                var (baseStateU, baseOptsU) = GetBase(userSource, row.Policy);
                var pendU = FindPending(row.Policy.UniqueID, "User");
                var stateU = pendU?.DesiredState ?? baseStateU;
                var optsU = pendU?.Options ?? baseOptsU;
                row.ApplyExternal("User", stateU, optsU);
            }
        }

        public void ApplyPendingOverlay(
            IEnumerable<QuickEditRow> rows,
            PolicyPlusCore.IO.IPolicySource compSource,
            PolicyPlusCore.IO.IPolicySource userSource
        )
        {
            if (rows == null)
                return;
            foreach (var r in rows)
            {
                try
                {
                    ApplyEffectiveToRow(r, compSource, userSource);
                }
                catch { }
            }
        }
    }
}
