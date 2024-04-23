using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Telegram.Bot;
using Telegram.Bot.Types;
using TeleScarcy.Models;
using Newtonsoft.Json;

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

        var configRabbitSection = configBuilder.GetSection("RabbitSettings");
        var hostNameRabbit = configRabbitSection["HostName"].ToString();
        var portRabbit = int.Parse(configRabbitSection["Port"]); 
        var userNameRabbit =  configRabbitSection["UserName"].ToString(); 
        var passwordRabbit =  configRabbitSection["Password"].ToString();
        var queueEvent = configRabbitSection["EventQueue"].ToString();    

        _logger.LogInformation($"hostNameRabbit: {hostNameRabbit}, portRabbit: {portRabbit}, userNameRabbit: {userNameRabbit}, passwordRabbit: {passwordRabbit}, queueEvent: {queueEvent} ");

         factory = new ConnectionFactory() { HostName = hostNameRabbit, Port = portRabbit, UserName = userNameRabbit, Password = passwordRabbit, VirtualHost = "/" };
         connection = factory.CreateConnection();
         channel = connection.CreateModel();
           channel.QueueDeclare(queue: queueEvent,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            consumer = new EventingBasicConsumer(channel);
            
        }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            $"{nameof(QueuedHostedService)} is running.{Environment.NewLine}");

               consumer.Received += async (sender, ea) =>
                {
                    var body = ea.Body.ToArray();
                    TeleMessage receivedMessage = JsonConvert.DeserializeObject<TeleMessage>(Encoding.UTF8.GetString(body));
                    
                    var botClient = new TelegramBotClient(receivedMessage.TelegramAccessTocken);

                    Message teleMessage = await botClient.SendTextMessageAsync(
                    chatId: receivedMessage.TelegramChatId,
                    text: receivedMessage.Message);
                    
                      _logger.LogInformation(
                        $"Inviato messaggio telegram con il seguente testo: {receivedMessage.Message}");

                    // Note: it is possible to access the channel via
                    //       ((EventingBasicConsumer)sender).Model here
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };
              
                var configBuilder = new ConfigurationBuilder().
                    AddJsonFile("appsettings.json").Build();

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