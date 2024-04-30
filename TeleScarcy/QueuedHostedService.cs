using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Telegram.Bot;
using Telegram.Bot.Types;
using TeleScarcy.Models;
using Newtonsoft.Json;
using TeleScarcy.Configurations;

namespace TeleScarcy;
public sealed class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<QueuedHostedService> _logger;

   
    private ConnectionFactory factory ;
    private IConnection connection;
    EventingBasicConsumer consumer ;
    private IModel channel;
    private string TelegramAccessTocken;
    private string TelegramChatId;
   
    public QueuedHostedService(
        IBackgroundTaskQueue taskQueue,
        ILogger<QueuedHostedService> logger) 
        {
         _taskQueue = taskQueue;
          _logger = logger;
         _logger = logger;

        var configBuilder = new ConfigurationBuilder().
        AddJsonFile("appsettings.json").Build();

        
        var rabbitSettings = new RabbitSettings();

        var configRabbitSection = configBuilder.GetSection("RabbitSettings");

        configRabbitSection.Bind(rabbitSettings);
    

        _logger.LogInformation($"hostNameRabbit: {rabbitSettings.HostName}, portRabbit: {rabbitSettings.Port}, userNameRabbit: {rabbitSettings.UserName}, passwordRabbit: {rabbitSettings.Password}, queueEvent: {rabbitSettings.EventQueue} ");

         factory = new ConnectionFactory() 
         { 
            HostName = rabbitSettings.HostName, 
            Port = rabbitSettings.Port, 
            UserName = rabbitSettings.UserName, 
            Password = rabbitSettings.Password, 
            VirtualHost = "/" 
        };

         connection = factory.CreateConnection();
         channel = connection.CreateModel();
           channel.QueueDeclare(queue: rabbitSettings.EventQueue,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            consumer = new EventingBasicConsumer(channel);
            
        }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configBuilder = new ConfigurationBuilder().
                    AddJsonFile("appsettings.json").Build();

        _logger.LogInformation(
            $"{nameof(QueuedHostedService)} is running.{Environment.NewLine}");

            var teleSettings = configBuilder.GetSection("TeleSettings").Get<List<TeleSettings>>();  
           
               consumer.Received += async (sender, ea) =>
                {
                    var body = ea.Body.ToArray();
                    TeleMessage receivedMessage = JsonConvert.DeserializeObject<TeleMessage>(Encoding.UTF8.GetString(body));
                    var ts = teleSettings.Where(x => x.Key == receivedMessage.Key);
                    if(ts.Any()){
                        
                    TeleSettings? tsSelected = ts.FirstOrDefault();    
                    var botClient = new TelegramBotClient(tsSelected.TelegramAccessTocken);

                    Message teleMessage = await botClient.SendTextMessageAsync(
                    chatId: tsSelected.TelegramChatId,
                    text: receivedMessage.Message);
                    
                      _logger.LogInformation(
                        $"Inviato messaggio telegram con il seguente testo: {receivedMessage.Message}");

                    // Note: it is possible to access the channel via
                    //       ((EventingBasicConsumer)sender).Model here
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                       
                    }else{
                     throw new Exception($"Non trovate chiave configurazioni per la chiave '{receivedMessage.Key}'");
                    }
                };
              
                

                var configRabbitSection = configBuilder.GetSection("RabbitSettings");
                var queueEvent = configRabbitSection["EventQueue"].ToString();    
            
              channel.BasicConsume(queue: queueEvent,
                                    autoAck: false,
                                    consumer: consumer); 

        return ProcessTaskQueueAsync(stoppingToken);
    }

    private async Task ProcessTaskQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Func<CancellationToken, ValueTask>? workItem =
                    await _taskQueue.DequeueAsync(stoppingToken);

                await workItem(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing task work item.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            $"{nameof(QueuedHostedService)} is stopping.");

        await base.StopAsync(stoppingToken);
    }
}