namespace tix_OLTPservice
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        private static readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!await _executionLock.WaitAsync(0, stoppingToken))
                return;
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    await _runBlockInvoker();
                    await Task.Delay(1000, stoppingToken);
                }
            }
            finally
            {
                _executionLock.Release();
            }
        }

        private Task PrintException(Exception ex)
        {
            if (ex != null)
                _logger.LogError(ex, "Worker encountered an unhandled error");
            return Task.CompletedTask;
        }

        //https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client?view=aspnetcore-8.0&tabs=visual-studio
        private async Task _runBlockInvoker()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                const string funName = nameof(Worker) + "." + nameof(_runBlockInvoker);
                DataUtility du = new DataUtility((string)_configuration["ConnectionString"]);
                var qry = "EXEC [Explorer_OLTP].[dbo].[sp_ProcessTxnReceipts] ";
                qry = "BEGIN TRANSACTION \"" + funName + "\" BEGIN TRY " + qry + " COMMIT TRANSACTION \"" + funName + "\" END TRY BEGIN CATCH ROLLBACK TRANSACTION \"" + funName + "\" END CATCH ";
                du.ExecuteSql(qry);
                _logger.LogDebug("Block invoker completed in {Elapsed}ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await PrintException(ex);
            }
        }
    }
}