using System.Text;
using LLMCommApi.Entities;
using LLMCommApi.Settings;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LLMCommApi.Repositories;

public class LLMEngineRepository : ILLMEngineRepository
{
    
    private readonly LLMCommSettings _commSettings;
    private IConnection _connection;
    private IModel _channel;
    
    
    public LLMEngineRepository(IOptions<LLMCommSettings> commSettings)
    {
        _commSettings = commSettings.Value;
        InitConnection();
    }

    
    private void InitConnection()
    {
        Console.WriteLine("InitConnection");
        
        var factory = new ConnectionFactory { HostName = _commSettings.Host };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        
        Console.WriteLine("connection initialized");
    }


    public Task<PromptReply> PostPromptAsync(Prompt prompt)
    {
        var taskCompletionSource = new TaskCompletionSource<PromptReply>();

        var queue = _commSettings.PromptQueue;
        var consumer = ConsumeRabbitQueue(queue);
        consumer.Received += (_, e) =>
        {
            var body = e.Body.ToArray();
            var reply = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received: {reply}");
            
            var promptReply = new PromptReply
            {
                Reply = reply,
                CreatedDate = DateTimeOffset.Now
            };
            
            _channel.BasicAck(deliveryTag: e.DeliveryTag, multiple: false);
            Dispose();
            
            taskCompletionSource.SetResult(promptReply);
        };
        
        _channel.BasicConsume(queue: queue,
            autoAck: false,
            consumer: consumer);
        Console.WriteLine("Consumer started");
        
        // todo may need to implement timeout to avoid infinite waiting
        Thread.Sleep(1000);
        SendRabbitMessage(prompt.PromptText, queue);

        return taskCompletionSource.Task;
    }

    
    public Task<DataUpdateStatus> RequestDataUpdateAsync()
    {
        throw new NotImplementedException();
    }


    private void SendRabbitMessage(string message, string queue)
    {
        if(!_connection.IsOpen) InitConnection();
        
        Console.WriteLine("SendPromptToLlm");
        
        // no need to declare queue cause it is already declared when listening
        
        var body = Encoding.UTF8.GetBytes(message);

        _channel.BasicPublish(exchange: string.Empty,
            routingKey: queue,
            basicProperties: null,
            body: body);
        
        Console.WriteLine($"Sent {message}");
    }


    private EventingBasicConsumer ConsumeRabbitQueue(string queue)
    {
        if(!_connection.IsOpen) InitConnection();
        
        Console.WriteLine("ListenFromLlmReply");
        
        _channel.QueueDeclare(queue: queue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        
        Console.WriteLine("Waiting for messages.");
        
        var consumer = new EventingBasicConsumer(_channel);
        return consumer;
    }
    
    
    private void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
    
    
}

