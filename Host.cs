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
        _bot.StartReceiving(UpdateHandler, ErrorHandler);
        Console.WriteLine("Бот запущен!");
    }

    private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
    {
        Console.WriteLine("Ошибка: " + exception.Message);
        await Task.CompletedTask;
    }

    private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        var user = update.Message?.From;
        if (user == null) return;

        // Формируем имя пользователя с фамилией (если она есть)
        string fullName = $"{user.FirstName}{(string.IsNullOrEmpty(user.LastName) ? "" : " " + user.LastName)}";

        DateTime messageTime = update.Message!.Date.ToLocalTime();
        string formattedTime = messageTime.ToString("HH:mm:ss dd.MM.yyyy");

        Console.WriteLine($"[{formattedTime}] Пользователь {fullName} с id @{update.Message?.From?.Username} написал: {update.Message?.Text ?? "[не текст]"}");
        OnMessage?.Invoke(client, update);
        await Task.CompletedTask;
    }
}
