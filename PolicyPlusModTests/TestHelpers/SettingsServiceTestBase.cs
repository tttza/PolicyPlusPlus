using System;
using System.IO;
using System.Threading;
using PolicyPlusPlus.Services;

namespace PolicyPlusModTests.TestHelpers;

// Ensures SettingsService uses an isolated directory per test and serializes access across tests.
public abstract class SettingsServiceTestBase : IDisposable
{
    private static readonly object SettingsLock = new();
    private readonly string _baseDir;
    private bool _lockTaken;

    protected SettingsServiceTestBase()
    {
        Monitor.Enter(SettingsLock, ref _lockTaken);
        _baseDir = Path.Combine(
            Path.GetTempPath(),
            "PPSettingsTests_" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_baseDir);
        SettingsService.Instance.InitializeForTests(_baseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures; isolation is best-effort for tests.
        }
        finally
        {
            if (_lockTaken)
            {
                Monitor.Exit(SettingsLock);
                _lockTaken = false;
            }
        }
    }
}
