using Microsoft.Extensions.Options;
using System.Data;

namespace tix_OLTPservice
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        static bool _lock = false;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_lock)
                while (!stoppingToken.IsCancellationRequested)
                {
                    _lock = true;
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    await _runBlockInvoker();
                    await Task.Delay(1000, stoppingToken);
                    _lock = false;
                }
        }

        private async Task PrintException(Exception ex)
        {
            if (ex != null)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                foreach (var f in Directory.GetFiles(path, "*.txt"))
                {
                    DateTime fileCreatedDate = File.GetCreationTime(f);
                    if ((DateTime.Now - fileCreatedDate).TotalDays > 5)
                        File.Delete(f);
                }
                string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
                if (!File.Exists(filepath))
                {
                    using (StreamWriter sw = File.CreateText(filepath))
                    {
                        sw.WriteLine(ex.Message);
                        sw.WriteLine(ex.StackTrace);
                    }
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filepath))
                    {
                        sw.WriteLine(ex.Message);
                        sw.WriteLine(ex.StackTrace);
                    }
                }
            }
        }

        //https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-8.0&tabs=visual-studio
        private async Task _runBlockInvoker()
        {
            var methodInfo = System.Reflection.MethodBase.GetCurrentMethod();
            var funName = methodInfo.DeclaringType.Name + "." + methodInfo.Name;
            try
            {
                DataUtility du = new DataUtility((string)_configuration["ConnectionString"]);
                var qry = "EXEC [Explorer_OLTP].[dbo].[sp_ProcessTxnReceipts] ";
                qry = "BEGIN TRANSACTION \"" + funName + "\" BEGIN TRY " + qry + " COMMIT TRANSACTION \"" + funName + "\" END TRY BEGIN CATCH ROLLBACK TRANSACTION \"" + funName + "\" END CATCH ";
                du.ExecuteSql(qry);
            }
            catch (Exception ex)
            {
                await PrintException(ex);
            }
        }
    }
}