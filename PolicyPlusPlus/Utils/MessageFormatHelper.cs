namespace PolicyPlusPlus.Utils
{
    /// <summary>
    /// Provides consistent message formatting across the application.
    /// </summary>
    public static class MessageFormatHelper
    {
        /// <summary>
        /// Formats a refresh failure message with consistent wording.
        /// </summary>
        /// <param name="changeCount">The number of changes that were saved.</param>
        /// <param name="error">The error message from the refresh failure.</param>
        /// <returns>A consistently formatted message string.</returns>
        public static string FormatRefreshFailureMessage(int changeCount, string error)
        {
            var baseMessage =
                changeCount == 1
                    ? "Saved 1 change, but refresh failed."
                    : $"Saved {changeCount} changes, but refresh failed.";
            return $"{baseMessage} ({error})";
        }

        /// <summary>
        /// Formats a reapply refresh failure message with consistent wording.
        /// </summary>
        /// <param name="error">The error message from the refresh failure.</param>
        /// <returns>A consistently formatted message string.</returns>
        public static string FormatReapplyRefreshFailureMessage(string error)
        {
            return $"Reapplied, but refresh failed. ({error})";
        }
    }
}
