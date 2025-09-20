using System;
using PolicyPlusCore.Core;

namespace PolicyPlusCore.Utilities;

public static class AdmxCacheFactory
{
    public static IAdmxCache CreateDefault() => new AdmxCache();
}
