namespace PolicyPlus.WinUI3.Services
{
    // Central constants for update-related configuration.
    internal static partial class UpdateConfig
    {
#if USE_VELOPACK
        public const string VelopackUpdateUrl = @"C:\temp\Releases";
#endif
#if USE_STORE_UPDATE
        public const string StoreProductId = "9NJC1R1PGVF2";
#endif
    }
}
