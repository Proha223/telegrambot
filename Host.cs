using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class Host
{
    public event Action<ITelegramBotClient, Update>? OnMessage;
    private readonly ITelegramBotClient _bot;

    public Host(string token)
    {
        _bot = new TelegramBotClient(token);
    }

    public async Task StartAsync()
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions
        );

        var me = await _bot.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен!");

        await Task.Delay(-1); // Бесконечное ожидание
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message is not { From: { } user })
                return;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {user.Username ?? user.FirstName}: {update.Message.Text ?? "[не текст]"}");
            OnMessage?.Invoke(botClient, update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки: {ex}");
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException 
                => $"Ошибка API: {apiRequestException.ErrorCode} - {apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}
