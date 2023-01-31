using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace TeleScarcy;
using Telegram.Bot;
using Telegram.Bot.Types;

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
        factory = new ConnectionFactory() { HostName = "scarcybox", Port = 5672, UserName = "scarcy", Password = "forzaJuve123" };
        connection = factory.CreateConnection();
        channel = connection.CreateModel();
           channel.QueueDeclare(queue: "TeleScarcy_queue",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            consumer = new EventingBasicConsumer(channel);
            TelegramAccessTocken = "5945035651:AAH_4sACRXJgOhqySEDatrE-ZBN2tH8Jbn0";
            TelegramChatId = "1234647619";
        }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            $"{nameof(QueuedHostedService)} is running.{Environment.NewLine}");

               consumer.Received += async (sender, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    
                    var botClient = new TelegramBotClient(TelegramAccessTocken);

                    Message teleMessage = await botClient.SendTextMessageAsync(
                    chatId: TelegramChatId,
                    text: message);
                    
                      _logger.LogInformation(
                        $"Inviato messaggio telegram con il seguente testo: {message}");

                    // Note: it is possible to access the channel via
                    //       ((EventingBasicConsumer)sender).Model here
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };
              
               
            
              channel.BasicConsume(queue: "TeleScarcy_queue",
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