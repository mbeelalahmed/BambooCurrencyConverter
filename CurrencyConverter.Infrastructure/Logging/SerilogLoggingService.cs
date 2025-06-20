using CurrencyConverter.Application.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyConverter.Infrastructure.Logging
{
    public class SerilogLoggingService : ILoggingService
    {
        public void LogInfo(string message, params object[] args)
        {
            Log.Information(message, args);
        }

        public void LogError(Exception ex, string message, params object[] args)
        {
            Log.Error(ex, message, args);
        }
    }
}
