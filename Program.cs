using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using MySql.Data.MySqlClient;
internal class Program
{
    private static Dictionary<long, string> userStates = new();
    private static Host? mybot;
    private static Database? _database;
    private static Dictionary<long, (List<TestQuestion>, int)> userTests = new();

    private static void Main()
    {
        try
        {
            string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
               ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN переменная окружения не задана в Railway");
            /*string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                         ?? "7476081986:AAFFHHi26MlxbRuCNAA4h5zyE9Nzlz4k_Tc"; // Для локального тестирования */

            string connectionString;

            // Проверяем, работаем ли мы на Railway
            if (Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT") != null)
            {
                // Получаем параметры подключения из переменных окружения Railway
                string dbHost = Environment.GetEnvironmentVariable("MYSQLHOST") ?? throw new Exception("MYSQLHOST не установлен");
                string dbPort = Environment.GetEnvironmentVariable("MYSQLPORT") ?? "3306";
                string dbUser = Environment.GetEnvironmentVariable("MYSQLUSER") ?? throw new Exception("MYSQLUSER не установлен");
                string dbPassword = Environment.GetEnvironmentVariable("MYSQLPASSWORD") ?? throw new Exception("MYSQLPASSWORD не установлен");
                string dbName = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? throw new Exception("MYSQLDATABASE не установлен");

                connectionString = $"server={dbHost};port={dbPort};database={dbName};user={dbUser};password={dbPassword};";
            }
            else
            {
                // Локальные настройки для тестирования
                connectionString = "server=localhost;database=telegrambot;user=root;password=root;";
            }

            _database = new Database(connectionString);

            mybot = new Host(token);
            mybot.Start();
            mybot.OnMessage += OnMessage;

            Thread.Sleep(Timeout.Infinite);
            //Console.ReadLine(); // Оставляем для локального тестирования => КОММЕНТИТЬ ПРЕД СТРОКУ
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex}");
        }
    }

    private static async void OnMessage(ITelegramBotClient client, Update update)
    {
        if (update.Message?.Text == null || update.Message?.Chat == null || update.Message.From == null)
            return;

        long userTelegramId = update.Message.From.Id; // Используем Telegram ID как внешний идентификатор
        long chatId = update.Message.Chat.Id;
        string messageText = update.Message.Text;

        // Автоматическая регистрация пользователя при первом сообщении
        if (!_database.UserExists(userTelegramId))
        {
            string firstName = update.Message.From.FirstName ?? "";
            string lastName = update.Message.From.LastName ?? "";
            string username = update.Message.From.Username ?? "";

            _database.RegisterUser(userTelegramId, firstName, lastName, username);
            //Console.WriteLine($"Зарегистрирован новый пользователь: {firstName} {lastName} (@{username})");
        }

        // Проверяем текущее состояние пользователя
        if (userStates.TryGetValue(chatId, out string state))
        {
            switch (state)
            {
                case "WAITING_TEST_TYPE":
                    switch (messageText)
                    {
                        case "Быстрый тест":
                            if (messageText == "/exit")
                            {
                                await client.SendMessage(
                                        chatId: chatId,
                                        text: "Вы успешно покинули тест!");
                                userStates.Remove(chatId);
                            }
                            var replyKeyboardFastTest = new ReplyKeyboardMarkup(new[]
                            {
                                new KeyboardButton[] { "Начать" }
                            })
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            };
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Тест создан. Для продолжения нажмите кнопку \"Начать\"",
                                replyMarkup: replyKeyboardFastTest);
                            userStates[chatId] = "FAST_TEST_READY";
                            break;

                        case "Настраиваемый":
                            var replyKeyboardCustom = new ReplyKeyboardMarkup(new[]
                                {
                                new KeyboardButton[] { "test1", "test2" }
                            })
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            };
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Выберите действие:",
                                replyMarkup: replyKeyboardCustom);
                            userStates[chatId] = "CUSTOM_TEST_OPTIONS";
                            break;

                        case "/exit":
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Вы успешно покинули выбор типа теста!",
                                replyMarkup: new ReplyKeyboardRemove());
                            userStates.Remove(chatId);
                            break;

                        default:
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Для выбора типа теста используйте кнопки или текст с кнопок!\nВыход из выбора типа теста - /exit");
                            break;
                    }
                    break;

                case "WAITING_THEORY_TYPE":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                                chatId: chatId,
                                text: "Вы успешно покинули выбор тем для изучения!",
                                replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                        break;
                    }
                    switch (messageText)
                    {
                        case "Теория1": // Заменить по названию темы
                            await client.SendMessage(
                                chatId: chatId,
                                text: "1"); // Текст теории по теме
                            userStates.Remove(chatId);
                            break;

                        case "Теория2": // Заменить по названию темы

                            await client.SendMessage(
                                chatId: chatId,
                                text: "2"); // Текст теории по теме
                            userStates.Remove(chatId);
                            break;

                        default:
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Для выбора темы используйте кнопки или текст с кнопок!\nВыход из выбора тем - /exit");
                            break;
                    }
                    break;

                case "FAST_TEST_READY":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вы успешно покинули запуск теста!",
                            replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                    }
                    else if (messageText == "Начать")
                    {
                        Console.WriteLine($"Пользователь {chatId} начал тест");

                        var questions = _database.GetTestQuestions();
                        Console.WriteLine($"Получено вопросов: {questions.Count}");

                        if (questions.Count == 0)
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Извините, в базе данных нет доступных вопросов для теста.");
                            return;
                        }

                        userTests[chatId] = (questions, 0);
                        Console.WriteLine($"Первый вопрос подготовлен для {chatId}");

                        await SendQuestion(client, chatId, questions[0], 1, questions.Count);
                        userStates[chatId] = "TEST_IN_PROGRESS";
                        Console.WriteLine($"Состояние пользователя {chatId} изменено на TEST_IN_PROGRESS");
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Для запуска теста нажмите кнопку 'Начать' или введите /exit для выхода");
                    }
                    break;

                case "TEST_IN_PROGRESS":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вы успешно покинули тест!",
                            replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                        userTests.Remove(chatId);
                    }
                    else if (int.TryParse(messageText, out int chosenAnswer) && chosenAnswer >= 1 && chosenAnswer <= 4)
                    {
                        var (questions, currentIndex) = userTests[chatId];
                        var currentQuestion = questions[currentIndex];

                        bool isCorrect = chosenAnswer == currentQuestion.CorrectAnswer;
                        int userId = _database.GetUserIdByTelegramId(userTelegramId);

                        _database.RecordUserAnswer(userId, currentQuestion.TestId, chosenAnswer, isCorrect);

                        await client.SendMessage(
                            chatId: chatId,
                            text: isCorrect ? "Верно ✅" : "Не правильно ❌");

                        // Переход к следующему вопросу или завершение теста
                        if (currentIndex + 1 < questions.Count)
                        {
                            userTests[chatId] = (questions, currentIndex + 1);
                            await SendQuestion(client, chatId, questions[currentIndex + 1], currentIndex + 2, questions.Count);
                        }
                        else
                        {
                            int totalPoints = _database.GetUserTotalPoints(userId);
                            await client.SendMessage(
                                chatId: chatId,
                                text: $"Поздравляю! Вы прошли тест. Ваш результат: {totalPoints} из {questions.Count}",
                                replyMarkup: new ReplyKeyboardRemove());
                            userStates.Remove(chatId);
                            userTests.Remove(chatId);
                        }
                    }
                    else
                    {
                        var (questions, currentIndex) = userTests[chatId];
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите вариант ответа от 1 до 4!");
                        await SendQuestion(client, chatId, questions[currentIndex], currentIndex + 1, questions.Count);
                    }
                    break;


                case "CUSTOM_TEST_OPTIONS":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                                chatId: chatId,
                                text: "Вы успешно покинули настройку теста!",
                                replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                    }
                    else if (messageText == "test1" || messageText == "test2") // Изменить на названия тестов в НАСТРАИМОВОМ ТИПЕ ТЕСТА
                    {
                        await client.SendMessage(
                        chatId: chatId,
                        text: $"Вы выбрали: {messageText}");
                        userStates.Remove(chatId); // Сбрасываем состояние
                    }
                    else
                    {
                        goto default;
                    }
                    break;

                default:
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Для настройки теста используйте кнопки или текст с кнопок!\nВыход из выбора типа теста - /exit");
                    break;
            }
        }
        else
        {
            // Если состояния нет, обрабатываем основные команды
            switch (messageText)
            {
                case "/start":
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Добро пожаловать!\n/theory - для изучения теории\n/test - создать новый тест",
                        replyParameters: update.Message.MessageId,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "/theory":
                    var replyKeyboardTheory = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "Теория1", "Теория2" }
                    })
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Выберите тему для изучения",
                        replyMarkup: replyKeyboardTheory);
                    userStates[chatId] = "WAITING_THEORY_TYPE";
                    break;

                case "/test":
                    var replyKeyboardNew = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "Быстрый тест", "Настраиваемый" }
                    })
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Выберите тип теста",
                        replyMarkup: replyKeyboardNew);
                    userStates[chatId] = "WAITING_TEST_TYPE";
                    break;

                case "/help":
                    await client.SendMessage(
                        chatId: chatId,
                        text: "По всем вопросам 👉🏻 @devGLoWie");
                    break;

                case "/results":
                    int userIdResult = _database.GetUserIdByTelegramId(userTelegramId);
                    int totalPointsResult = _database.GetUserTotalPoints(userIdResult);
                    await client.SendMessage(
                        chatId: chatId,
                        text: $"Ваши баллы: {totalPointsResult}");
                    break;

                default:
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Пиши /start для того, чтобы начать пользоваться ботом!");
                    break;
            }
        }

        await client.SetMyCommands(new[]
        {
            new BotCommand { Command = "/start", Description = "Запуск бота" },
            new BotCommand { Command = "/theory", Description = "Изучение теории" },
            new BotCommand { Command = "/test", Description = "Создание нового теста" },
            new BotCommand { Command = "/results", Description = "Результаты тестов" },
            new BotCommand { Command = "/exit", Description = "Выход" },
            new BotCommand { Command = "/help", Description = "Помощь" }
        });
    }
    private static async Task SendQuestion(ITelegramBotClient client, long chatId, TestQuestion question, int questionNumber, int totalQuestions)
    {
        try
        {
            Console.WriteLine($"Подготовка вопроса {questionNumber} для чата {chatId}");

            var options = new List<string>
        {
            $"1 - {question.Option1}",
            $"2 - {question.Option2}"
        };

            if (!string.IsNullOrEmpty(question.Option3))
                options.Add($"3 - {question.Option3}");
            if (!string.IsNullOrEmpty(question.Option4))
                options.Add($"4 - {question.Option4}");

            string questionText = $"Вопрос №{questionNumber} (из {totalQuestions}):\n" +
                                 $"{question.QuestionText}\n\n" +
                                 string.Join("\n", options) +
                                 "\n\nДля выхода из теста - /exit";

            Console.WriteLine($"Текст вопроса: {questionText}");

            var replyKeyboard = new ReplyKeyboardMarkup(
                Enumerable.Range(1, options.Count)
                    .Select(x => new KeyboardButton(x.ToString()))
                    .Chunk(2))
            {
                ResizeKeyboard = true
            };

            await client.SendMessage(
                chatId: chatId,
                text: questionText,
                replyMarkup: replyKeyboard);

            Console.WriteLine($"Вопрос отправлен пользователю {chatId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке вопроса: {ex.Message}");
        }
    }
}
