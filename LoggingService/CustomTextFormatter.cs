using System.IO;
using Serilog.Events;
using Serilog.Formatting;

namespace LoggingService
{
    public class CustomTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            if (logEvent.Level.ToString() != "Information")
            {
                output.WriteLine("--------------------------------------------------------");
                output.WriteLine($"Timestamp - {logEvent.Timestamp} | Level - {logEvent.Level}|");
                output.WriteLine("--------------------------------------------------------");
                foreach (var item in logEvent.Properties)
                {
                    output.WriteLine(item.Key + " : " + item.Value);
                }

                if (logEvent.Exception != null)
                {
                    output.WriteLine("-----------------------EXCEPTION DETAILS --------------");
                    output.Write($"Exception - {logEvent.Exception}");
                    output.Write($"StackTrace - {logEvent.Exception.StackTrace}");
                    output.Write($"Message - {logEvent.Exception.Message}");
                    output.Write($"Source - {logEvent.Exception.Source}");
                    output.Write($"InnerException - {logEvent.Exception.InnerException}");
                }

                output.WriteLine("--------------------------------------------------------");
            }

        }
    }
}
