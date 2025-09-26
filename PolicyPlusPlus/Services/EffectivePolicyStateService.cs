using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Logging; // logging
using PolicyPlusPlus.ViewModels;

namespace PolicyPlusPlus.Services
{
    internal sealed class EffectivePolicyStateService
    {
        public static EffectivePolicyStateService Instance { get; } = new();
        private const string LogArea = "EffectiveState";

        private EffectivePolicyStateService() { }

        // Background evaluation control
        private CancellationTokenSource? _overlayCts;
        private readonly object _gate = new();
        private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(100);
        private readonly int _maxDegree = Math.Max(1, Environment.ProcessorCount - 1);

        private PendingChange? FindPending(string policyId, string scope)
        {
            try
            {
                return PendingChangesService.Instance.Pending.FirstOrDefault(p =>
                    string.Equals(p.PolicyId, policyId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Scope, scope, StringComparison.OrdinalIgnoreCase)
                );
            }
            catch (Exception ex)
            {
                Log.Debug(LogArea, "FindPending failed: " + ex.Message);
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
                    catch (Exception ex)
                    {
                        Log.Debug(LogArea, "GetPolicyOptionStates failed: " + ex.Message);
                    }
                }
                return (st, null);
            }
            catch (Exception ex)
            {
                Log.Debug(LogArea, "GetPolicyState failed: " + ex.Message);
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
            var snapshot = rows.ToList();
            CancellationToken token;
            lock (_gate)
            {
                try
                {
                    _overlayCts?.Cancel();
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "Cancel previous overlay failed: " + ex.Message);
                }
                _overlayCts = new CancellationTokenSource();
                token = _overlayCts.Token;
            }

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        // Debounce multiple rapid calls
                        await Task.Delay(_debounce, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(LogArea, "Debounce delay failed: " + ex.Message);
                        return;
                    }

                    var sem = new SemaphoreSlim(_maxDegree, _maxDegree);
                    var tasks = new List<Task>();
                    foreach (var row in snapshot)
                    {
                        if (row == null)
                            continue;
                        try
                        {
                            await sem.WaitAsync(token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(LogArea, "Semaphore wait aborted: " + ex.Message);
                            break;
                        }

                        tasks.Add(
                            Task.Run(
                                () =>
                                {
                                    try
                                    {
                                        if (token.IsCancellationRequested)
                                            return;
                                        // Compute off-UI-thread
                                        // Use local variables instead of tuple field access to avoid named tuple metadata loss across compilations.
                                        PolicyState baseCompState = PolicyState.Unknown;
                                        Dictionary<string, object>? baseCompOptions = null;
                                        PolicyState baseUserState = PolicyState.Unknown;
                                        Dictionary<string, object>? baseUserOptions = null;
                                        if (row.SupportsComputer && compSource != null)
                                        {
                                            var (stC, optC) = GetBase(compSource, row.Policy);
                                            baseCompState = stC;
                                            baseCompOptions = optC;
                                        }
                                        if (row.SupportsUser && userSource != null)
                                        {
                                            var (stU, optU) = GetBase(userSource, row.Policy);
                                            baseUserState = stU;
                                            baseUserOptions = optU;
                                        }

                                        // Overlay pending
                                        if (row.SupportsComputer)
                                        {
                                            var pendC = FindPending(
                                                row.Policy.UniqueID,
                                                "Computer"
                                            );
                                            var stateC = pendC?.DesiredState ?? baseCompState;
                                            var optsC = pendC?.Options ?? baseCompOptions;
                                            TryDispatch(() =>
                                                row.ApplyExternal("Computer", stateC, optsC)
                                            );
                                        }
                                        if (row.SupportsUser)
                                        {
                                            var pendU = FindPending(row.Policy.UniqueID, "User");
                                            var stateU = pendU?.DesiredState ?? baseUserState;
                                            var optsU = pendU?.Options ?? baseUserOptions;
                                            TryDispatch(() =>
                                                row.ApplyExternal("User", stateU, optsU)
                                            );
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Debug(LogArea, "Row evaluation failed: " + ex.Message);
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            sem.Release();
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Debug(
                                                LogArea,
                                                "Semaphore release failed: " + ex.Message
                                            );
                                        }
                                    }
                                },
                                token
                            )
                        );
                    }

                    try
                    {
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(LogArea, "WhenAll failed: " + ex.Message);
                    }
                    try
                    {
                        sem.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(LogArea, "Semaphore dispose failed: " + ex.Message);
                    }
                },
                token
            );
        }

        private static void TryDispatch(Action action)
        {
            try
            {
                var dq = App.Window?.DispatcherQueue;
                if (dq == null)
                {
                    action();
                    return;
                }
                // Avoid implicit Action -> DispatcherQueueHandler conversion issues by constructing the delegate explicitly.
                dq.TryEnqueue(new DispatcherQueueHandler(action));
            }
            catch (Exception ex)
            {
                Log.Debug(LogArea, "Dispatcher enqueue failed: " + ex.Message);
            }
        }
    }
}
