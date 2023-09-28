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
    
    
    public LLMEngineRepository(IOptions<LLMCommSettings> commSettings)
    {
        _commSettings = commSettings.Value;
    }
    
    
    public Task<PromptReply> PostPromptAsync(Prompt prompt)
    {
        SendPromptToLLM(prompt);
        
        var tcs = new TaskCompletionSource<PromptReply>();
        var consumer = ListenFromLLMReply();
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var reply = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received {reply}");

            PromptReply promptReply = new()
            {
                Reply = reply,
                CreatedDate = DateTimeOffset.UtcNow
            };
            
            tcs.SetResult(promptReply);
        };
        
        return tcs.Task;
    }

    
    public Task<DataUpdateStatus> RequestDataUpdateAsync()
    {
        throw new NotImplementedException();
    }


    private void SendPromptToLLM(Prompt prompt)
    {
        var factory = new ConnectionFactory { HostName = _commSettings.Host };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        
        channel.QueueDeclare(queue: _commSettings.PromptQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        
        var body = Encoding.UTF8.GetBytes(prompt.PromptText);

        channel.BasicPublish(exchange: string.Empty,
            routingKey: _commSettings.PromptQueue,
            basicProperties: null,
            body: body);
        
        Console.WriteLine($"Sent {prompt.PromptText}");
    }


    private EventingBasicConsumer ListenFromLLMReply()
    {
        var factory = new ConnectionFactory { HostName = _commSettings.Host };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        
        channel.QueueDeclare(queue: _commSettings.PromptQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        
        Console.WriteLine("Waiting for messages.");

        // consume channel
        var consumer = new EventingBasicConsumer(channel);
        channel.BasicConsume(queue: _commSettings.PromptQueue,
            autoAck: true,
            consumer: consumer);
        
        return consumer;
    }
}