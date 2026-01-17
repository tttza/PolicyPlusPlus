using System.Data;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Admx;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheApplyService
{
    public static async Task DiffAndApplyAsync(
        AdmxCacheStore store,
        AdmxBundle bundle,
        string culture,
        bool allowGlobalRebuild,
        CancellationToken ct,
        Func<AdmxPolicy, string> inferValueType,
        Func<PolicyPlusCategory?, string> buildCategoryPath,
        Func<string, string?, string?, string> getRegistryPath
    )
    {
        // Serialize writers across processes to avoid WAL write conflicts and cache deletion races.
        // Try a few short retries to reduce chances of missing a rebuild entirely during transient contention.
        var writerLock = await AdmxCacheWriterGate
            .TryAcquireWriterLockAsync(
                perAttemptTimeout: TimeSpan.FromSeconds(5),
                maxAttempts: 3,
                retryDelay: TimeSpan.FromMilliseconds(250),
                ct
            )
            .ConfigureAwait(false);
        if (writerLock is null)
        {
            // Could not acquire within budget; skip this pass to avoid blocking UI. A coalesced rerun should follow.
            return;
        }
        using var _writerLockScope = writerLock;

        using var conn = store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Phase 1: perform required deletes in a single transaction.
        using (
            var txDel = (SqliteTransaction)
                await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted).ConfigureAwait(false)
        )
        {
            try
            {
                if (allowGlobalRebuild)
                {
                    // For contentless FTS5, regular DELETE is not allowed; use special commands.
                    using (var purgeFts = conn.CreateCommand())
                    {
                        purgeFts.Transaction = txDel;
                        purgeFts.CommandText =
                            "INSERT INTO PolicyIndex(PolicyIndex) VALUES('delete-all');";
                        await purgeFts.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    using (var purgeRest = conn.CreateCommand())
                    {
                        purgeRest.Transaction = txDel;
                        purgeRest.CommandText =
                            "DELETE FROM PolicyIndexMap; DELETE FROM PolicyI18n; DELETE FROM Policies;";
                        await purgeRest.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    using (var delI18n = conn.CreateCommand())
                    {
                        delI18n.Transaction = txDel;
                        delI18n.CommandText = "DELETE FROM PolicyI18n WHERE culture=@c";
                        delI18n.Parameters.AddWithValue("@c", culture);
                        await delI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    // Delete FTS5 rows for the culture using special 'delete' per rowid, then clean up the map.
                    var rowIds = new List<long>(256);
                    using (var getRows = conn.CreateCommand())
                    {
                        getRows.Transaction = txDel;
                        getRows.CommandText = "SELECT rowid FROM PolicyIndexMap WHERE culture=@c;";
                        getRows.Parameters.AddWithValue("@c", culture);
                        using var r = await getRows.ExecuteReaderAsync(ct).ConfigureAwait(false);
                        while (await r.ReadAsync(ct).ConfigureAwait(false))
                        {
                            if (!r.IsDBNull(0))
                                rowIds.Add(r.GetInt64(0));
                        }
                    }
                    if (rowIds.Count > 0)
                    {
                        using var delFts = conn.CreateCommand();
                        delFts.Transaction = txDel;
                        delFts.CommandText =
                            "INSERT INTO PolicyIndex(PolicyIndex, rowid) VALUES('delete', @rid);";
                        var pRid = delFts.Parameters.Add("@rid", SqliteType.Integer);
                        foreach (var rid in rowIds)
                        {
                            pRid.Value = rid;
                            await delFts.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                        }
                    }
                    using (var delMap = conn.CreateCommand())
                    {
                        delMap.Transaction = txDel;
                        delMap.CommandText = "DELETE FROM PolicyIndexMap WHERE culture=@c;";
                        delMap.Parameters.AddWithValue("@c", culture);
                        await delMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
                await txDel.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await txDel.RollbackAsync(ct).ConfigureAwait(false);
                }
                catch { }
                // After rollback, continue without inserts to avoid partial state.
                return;
            }
        }

        // Prepare reusable commands to reduce per-policy command construction overhead.
        using var cmdPolicy = conn.CreateCommand();
        cmdPolicy.CommandText =
            @"INSERT INTO Policies(ns, policy_name, category_key, hive, reg_key, reg_value, value_type, supported_min, supported_max, deprecated, product_hint)
VALUES(@ns,@name,@cat,@hive,@rkey,@rval,@vtype,'','',0,@ph)
ON CONFLICT(ns,policy_name) DO UPDATE SET category_key=excluded.category_key, hive=excluded.hive, reg_key=excluded.reg_key, reg_value=excluded.reg_value, value_type=excluded.value_type, product_hint=excluded.product_hint;
SELECT id FROM Policies WHERE ns=@ns AND policy_name=@name;";
        var p_ns = cmdPolicy.Parameters.Add("@ns", SqliteType.Text);
        var p_name = cmdPolicy.Parameters.Add("@name", SqliteType.Text);
        var p_cat = cmdPolicy.Parameters.Add("@cat", SqliteType.Text);
        var p_hive = cmdPolicy.Parameters.Add("@hive", SqliteType.Text);
        var p_rkey = cmdPolicy.Parameters.Add("@rkey", SqliteType.Text);
        var p_rval = cmdPolicy.Parameters.Add("@rval", SqliteType.Text);
        var p_vtype = cmdPolicy.Parameters.Add("@vtype", SqliteType.Text);
        var p_ph = cmdPolicy.Parameters.Add("@ph", SqliteType.Text);
        try
        {
            cmdPolicy.Prepare();
        }
        catch { }

        using var cmdI18n = conn.CreateCommand();
        cmdI18n.CommandText =
            @"INSERT OR REPLACE INTO PolicyI18n(policy_id, culture, display_name, explain_text, category_path, reading_kana, presentation_json)
VALUES(@pid,@culture,@dname,@desc,@cat,@kana,@pres)";
        var s_pid = cmdI18n.Parameters.Add("@pid", SqliteType.Integer);
        var s_culture = cmdI18n.Parameters.Add("@culture", SqliteType.Text);
        var s_dname = cmdI18n.Parameters.Add("@dname", SqliteType.Text);
        var s_desc = cmdI18n.Parameters.Add("@desc", SqliteType.Text);
        var s_cat = cmdI18n.Parameters.Add("@cat", SqliteType.Text);
        var s_kana = cmdI18n.Parameters.Add("@kana", SqliteType.Text);
        var s_pres = cmdI18n.Parameters.Add("@pres", SqliteType.Blob);
        try
        {
            cmdI18n.Prepare();
        }
        catch { }

        using var cmdIdx = conn.CreateCommand();
        cmdIdx.CommandText =
            @"INSERT INTO PolicyIndex(title_norm,desc_norm,title_loose,desc_loose,registry_path,tags) VALUES(@tn,@dn,@tl,@dl,@rp,@tags); SELECT last_insert_rowid();";
        var i_tn = cmdIdx.Parameters.Add("@tn", SqliteType.Text);
        var i_dn = cmdIdx.Parameters.Add("@dn", SqliteType.Text);
        var i_tl = cmdIdx.Parameters.Add("@tl", SqliteType.Text);
        var i_dl = cmdIdx.Parameters.Add("@dl", SqliteType.Text);
        var i_rp = cmdIdx.Parameters.Add("@rp", SqliteType.Text);
        var i_tags = cmdIdx.Parameters.Add("@tags", SqliteType.Text);
        try
        {
            cmdIdx.Prepare();
        }
        catch { }

        using var cmdIdxMap = conn.CreateCommand();
        cmdIdxMap.CommandText =
            "INSERT INTO PolicyIndexMap(rowid,policy_id,culture) VALUES(@rowid,@pid,@culture)";
        var m_rowid = cmdIdxMap.Parameters.Add("@rowid", SqliteType.Integer);
        var m_pid = cmdIdxMap.Parameters.Add("@pid", SqliteType.Integer);
        var m_culture = cmdIdxMap.Parameters.Add("@culture", SqliteType.Text);
        try
        {
            cmdIdxMap.Prepare();
        }
        catch { }

        // Phase 2: insert/update in batches to yield and keep UI responsive.
        const int BatchSize = 500;
        var policies = bundle.Policies.Values.ToList();
        try
        {
            var capEnv = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES");
            if (
                !string.IsNullOrWhiteSpace(capEnv)
                && int.TryParse(capEnv, out var maxPol)
                && maxPol > 0
                && policies.Count > maxPol
            )
            {
                policies = policies.Take(maxPol).ToList();
            }
        }
        catch { }
        int total = policies.Count;
        int index = 0;
        while (index < total)
        {
            int take = Math.Min(BatchSize, total - index);
            using var txIns = (SqliteTransaction)
                await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted)
                    .ConfigureAwait(false);
            try
            {
                // Enlist prepared commands in this transaction.
                cmdPolicy.Transaction = txIns;
                cmdI18n.Transaction = txIns;
                cmdIdx.Transaction = txIns;
                cmdIdxMap.Transaction = txIns;

                for (int i0 = 0; i0 < take; i0++)
                {
                    ct.ThrowIfCancellationRequested();
                    var pol = policies[index + i0];
                    await AdmxCachePolicyUpsertService
                        .UpsertOnePolicyAsync(
                            conn,
                            pol,
                            culture,
                            ct,
                            cmdPolicy,
                            p_ns,
                            p_name,
                            p_cat,
                            p_hive,
                            p_rkey,
                            p_rval,
                            p_vtype,
                            p_ph,
                            cmdI18n,
                            s_pid,
                            s_culture,
                            s_dname,
                            s_desc,
                            s_cat,
                            s_kana,
                            s_pres,
                            cmdIdx,
                            i_tn,
                            i_dn,
                            i_tl,
                            i_dl,
                            i_rp,
                            i_tags,
                            cmdIdxMap,
                            m_rowid,
                            m_pid,
                            m_culture,
                            inferValueType,
                            buildCategoryPath,
                            getRegistryPath
                        )
                        .ConfigureAwait(false);
                }

                await txIns.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await txIns.RollbackAsync(ct).ConfigureAwait(false);
                }
                catch { }
                // Abort remaining batches on failure to avoid inconsistent partial state.
                break;
            }

            index += take;
            // Yield to allow UI thread and other tasks to run between batches.
            await Task.Yield();
        }
    }
}
