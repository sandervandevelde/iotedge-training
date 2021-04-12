namespace WiseOpcUaClientModule
{
    public class LogLevelMessage
    {
        /// <summary>
        /// Trace       0 Logs that contain the most detailed messages.These messages may contain sensitive application data.These messages are disabled by default and should never be enabled in a production environment.
        /// Debug       1 Logs that are used for interactive investigation during development. These logs should primarily contain information useful for debugging and have no long-term value.
        /// Information 2 Logs that track the general flow of the application.These logs should have long-term value.
        /// Warning     3 Logs that highlight an abnormal or unexpected event in the application flow, but do not otherwise cause the application execution to stop.
        /// Error       4 Logs that highlight when the current flow of execution is stopped due to a failure.These should indicate a failure in the current activity, not an application-wide failure.
        /// None        6 Not used for writing log messages.Specifies that a logging category should not write any messages.
        /// Critical    5 Logs that describe an unrecoverable application or system crash, or a catastrophic failure that requires immediate attention.
        /// </summary>
        /// <remarks>
        /// See also https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel?view=dotnet-plat-ext-5.0
        /// </remarks>
        public LogLevel logLevel { get; set; }

        public string code { get; set; }

        public string message { get; set; }

        public enum LogLevel
        {
            Trace = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4,
            Critical = 5,
            None = 6,
        }
    }
}