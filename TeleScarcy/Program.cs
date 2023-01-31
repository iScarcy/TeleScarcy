using TeleScarcy;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
      
        services.AddHostedService<QueuedHostedService>();
        services.AddSingleton<IBackgroundTaskQueue>(_ => 
        {
            if (!int.TryParse(context.Configuration["QueueCapacity"], out var queueCapacity))
            {
                queueCapacity = 10;
            }

            return new DefaultBackgroundTaskQueue(queueCapacity);
        });
    })
    .Build();

await host.RunAsync();
