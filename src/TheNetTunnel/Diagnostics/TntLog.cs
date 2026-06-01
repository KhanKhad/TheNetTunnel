using System;

namespace TheNetTunnel.Diagnostics
{
    public enum TntLogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// Receives diagnostic messages emitted by the library. Assign an
    /// implementation to <see cref="TntLog.Logger"/> to observe internal
    /// errors that would otherwise be swallowed in background loops.
    /// </summary>
    public interface ITntLogger
    {
        void Log(TntLogLevel level, string source, string message, Exception exception);
    }

    /// <summary>
    /// Library-wide diagnostic sink. No-op until <see cref="Logger"/> is set.
    /// </summary>
    public static class TntLog
    {
        public static ITntLogger Logger { get; set; }

        public static void Warning(string source, string message, Exception exception = null)
            => Logger?.Log(TntLogLevel.Warning, source, message, exception);

        public static void Error(string source, string message, Exception exception = null)
            => Logger?.Log(TntLogLevel.Error, source, message, exception);
    }
}
