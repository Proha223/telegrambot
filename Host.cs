using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

public class Host
{
    public Action<ITelegramBotClient, Update>? OnMessage;
    private TelegramBotClient _bot;

    public Host(string token)
    {
        _bot = new TelegramBotClient(token);
    }

    public void Start()
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };
        
        _bot.StartReceiving(
            updateHandler: UpdateHandler,
            pollingErrorHandler: ErrorHandler,
            receiverOptions: receiverOptions
        );
        
        Console.WriteLine("Бот запущен!");
    }

    private async Task ErrorHandler(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        Console.WriteLine("Ошибка: " + exception.Message);
        await Task.CompletedTask;
    }

    private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        if (update.Message is not { } message)
            return;
            
        if (message.Text is not { } messageText)
            return;

        var user = message.From;
        string fullName = $"{user.FirstName}{(string.IsNullOrEmpty(user.LastName) ? "" : " " + user.LastName)}";
        DateTime messageTime = message.Date.ToLocalTime();
        string formattedTime = messageTime.ToString("HH:mm:ss dd.MM.yyyy");

        Console.WriteLine($"[{formattedTime}] Пользователь {fullName} (@{user.Username}) написал: {messageText}");
        
        OnMessage?.Invoke(client, update);
        await Task.CompletedTask;
    }
}
