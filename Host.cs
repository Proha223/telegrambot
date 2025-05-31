using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class Host
{
    public event Action<ITelegramBotClient, Update>? OnMessage;
    private readonly TelegramBotClient _bot;

    public Host(string token)
    {
        _bot = new TelegramBotClient(token);
    }

    public async Task StartAsync()
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };
        
        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions
        );
        
        var me = await _bot.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен! ID: {me.Id}");
        
        await Task.Delay(-1); // Бесконечное ожидание
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        try 
        {
            if (update.Message is not { From: { } user })
                return;

            string fullName = $"{user.FirstName}{(string.IsNullOrEmpty(user.LastName) ? "" : " " + user.LastName)}";
            DateTime messageTime = update.Message.Date.ToLocalTime();
            
            Console.WriteLine($"[{messageTime:HH:mm:ss dd.MM.yyyy}] {fullName} (@{user.Username}): {update.Message.Text ?? "[не текст]"}");
            
            OnMessage?.Invoke(client, update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки сообщения: {ex}");
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}
