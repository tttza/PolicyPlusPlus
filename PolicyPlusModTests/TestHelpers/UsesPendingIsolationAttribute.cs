using System;
using PolicyPlusPlus.Services;

namespace PolicyPlusModTests.TestHelpers;

// Removed marker attribute (was unused after base-class approach) to avoid accidental misuse.

// Base class that enforces per-test PendingChangesService isolation via AsyncLocal.
public abstract class PendingIsolationTestBase : IDisposable
{
    protected PendingIsolationTestBase()
    {
        PendingChangesService.EnableTestIsolation();
        PendingChangesService.ResetAmbientForTest();
        ViewNavigationService.EnableTestIsolation();
        ViewNavigationService.ResetAmbientForTest();
    }

    public void Dispose()
    {
        PendingChangesService.ResetAmbientForTest();
        ViewNavigationService.ResetAmbientForTest();
    }
}
