using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using System.Threading;
using Telegram.Bot.Types.Enums;

public class Host
{
    public Action<ITelegramBotClient, Update>? OnMessage;
    private readonly TelegramBotClient _bot;
    private readonly CancellationTokenSource _cts = new();

    public Host(string token)
    {
        _bot = new TelegramBotClient(token);
    }

    public void Start()
    {
        try
        {
            // В версии 22.5.1 просто запускаем получение сообщений без отключения вебхука
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // Получаем все типы обновлений
            };

            _bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _cts.Token
            );

            Console.WriteLine("Бот успешно запущен в режиме polling!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при запуске бота: {ex}");
            throw;
        }
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        try
        {
            var user = update.Message?.From;
            if (user == null) return;

            string fullName = $"{user.FirstName}{(string.IsNullOrEmpty(user.LastName) ? "" : " " + user.LastName)}";
            DateTime messageTime = update.Message!.Date.ToLocalTime();
            string formattedTime = messageTime.ToString("HH:mm:ss dd.MM.yyyy");

            Console.WriteLine($"[{formattedTime}] Сообщение от {fullName} (@{user.Username}): {update.Message.Text ?? "[не текст]"}");

            OnMessage?.Invoke(client, update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки обновления: {ex}");
        }
    }

    /*private Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка в работе бота: {exception}");
        return Task.CompletedTask;
    }*/
}
