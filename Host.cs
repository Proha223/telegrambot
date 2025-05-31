using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

public class Host
{
    public event Action<ITelegramBotClient, Update>? OnMessage;
    private readonly TelegramBotClient _bot;

    public Host(string token)
    {
        _bot = new TelegramBotClient(token);
    }

    public void Start()
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Получаем все типы обновлений
        };
        
        _bot.StartReceiving(
            updateHandler: UpdateHandler,
            pollingErrorHandler: ErrorHandler,
            receiverOptions: receiverOptions
        );
        
        var me = _bot.GetMeAsync().Result;
        Console.WriteLine($"Бот @{me.Username} запущен! ID: {me.Id}");
        
        // Бесконечное ожидание
        Thread.Sleep(Timeout.Infinite);
    }

    private Task ErrorHandler(ITelegramBotClient client, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }

    private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
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
}
