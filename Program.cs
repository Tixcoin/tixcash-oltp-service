using tix_OLTPservice;

IHost host = Host.CreateDefaultBuilder(args)
     .UseWindowsService(option =>
     {
         option.ServiceName = "Service name";
     })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
