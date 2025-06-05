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
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
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
            if (update.Message?.From is null) return;

            string fullName = $"{update.Message.From.FirstName}{(string.IsNullOrEmpty(update.Message.From.LastName) ? "" : " " + update.Message.From.LastName)}";
            DateTime messageTime = update.Message.Date.ToLocalTime();
            string formattedTime = messageTime.ToString("HH:mm:ss dd.MM.yyyy");

            Console.WriteLine($"[{formattedTime}] Сообщение от {fullName} (@{update.Message.From.Username}): {update.Message.Text ?? "[не текст]"}");

            OnMessage?.Invoke(client, update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки обновления: {ex}");
        }
    }

    private async Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
    {
        await Task.Run(() => cancellationToken);
    }
}
