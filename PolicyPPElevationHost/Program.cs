namespace PolicyPPElevationHost
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                string? pipe = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (
                        string.Equals(
                            args[i],
                            "--elevation-host",
                            StringComparison.OrdinalIgnoreCase
                        )
                        && i + 1 < args.Length
                    )
                        pipe = args[i + 1];
                }
                if (string.IsNullOrEmpty(pipe))
                    return 2;
                return ElevationHost.Run(pipe);
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(),
                            "PolicyPlus_host_fatal.log"
                        ),
                        DateTime.Now + " " + ex + Environment.NewLine
                    );
                }
                catch { }
                return 3;
            }
        }
    }
}
