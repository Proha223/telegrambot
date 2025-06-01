using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using MySql.Data.MySqlClient;
internal class Program
{
    private static Dictionary<long, string> userStates = new();
    private static Host? mybot;
    private static Database? _database;

    private static void Main()
    {
        try
        {
            string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
               ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN переменная окружения не задана в Railway");
            /*string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                         ?? "Телеграм_Токен";*/ // Для локального тестирования 
            /*string connectionString = "server=localhost;database=telegrambot;user=root;password=root;";
            _database = new Database(connectionString); // Для локального тестирования*/

            // Получаем строку подключения к MySQL из переменных окружения
            string connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING") ??
                                    $"server={Environment.GetEnvironmentVariable("MYSQLHOST")};" +
                                    $"port={Environment.GetEnvironmentVariable("MYSQLPORT")};" +
                                    $"database={Environment.GetEnvironmentVariable("MYSQLDATABASE")};" +
                                    $"user={Environment.GetEnvironmentVariable("MYSQLUSER")};" +
                                    $"password={Environment.GetEnvironmentVariable("MYSQLPASSWORD")};";

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

        long userId = update.Message.From.Id;
        long chatId = update.Message.Chat.Id;
        string messageText = update.Message.Text;

        // Автоматическая регистрация пользователя при первом сообщении
        if (!_database.UserExists(userId))
        {
            string firstName = update.Message.From.FirstName ?? "";
            string lastName = update.Message.From.LastName ?? "";
            string username = update.Message.From.Username ?? "";

            _database.RegisterUser(userId, firstName, lastName, username);
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
                        var replyKeyboardTest1 = new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { "1", "2", "3", "4" }
                        })
                        {
                            ResizeKeyboard = true
                        };
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вопрос №1:\nКакой вариант верный?\n1 - Неверный\n2 - Верный\n3 - Неверный\n4 - Неверный\n\nДля выхода из теста - /exit",
                            replyMarkup: replyKeyboardTest1);
                        userStates[chatId] = "TEST_1_QUESTION_1";
                    }
                    else
                    {
                        await client.SendMessage(
                        chatId: chatId,
                        text: "Для запуска теста напишите или нажмите кнопку \"Начать\"!\nВыход - /exit");
                    }
                    break;

                case "TEST_1_QUESTION_1":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                                chatId: chatId,
                                text: "Вы успешно покинули тест!",
                                replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                    }
                    else if (messageText == "1" || messageText == "3" || messageText == "4")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Не правильно ❌"); // Добавить баллы пользователю

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вопрос №2:\nКакой вариант верный?\n1 - Верный\n2 - Неверный\n3 - Неверный\n4 - Неверный\n\nДля выхода из теста - /exit");
                        userStates[chatId] = "TEST_1_QUESTION_2";
                    }
                    else if (messageText == "2")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Верно ✅"); // Добавить баллы пользователю

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вопрос №2:\nКакой вариант верный?\n1 - Верный\n2 - Неверный\n3 - Неверный\n4 - Неверный\n\nДля выхода из теста - /exit");
                        userStates[chatId] = "TEST_1_QUESTION_2";
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите вариант ответа на вопрос из предложенного списка!\nВопрос №1:\nКакой вариант верный?\n1 - Неверный\n2 - Верный\n3 - Неверный\n4 - Неверный\n\nДля выхода из теста - /exit");
                    }
                    break;

                case "TEST_1_QUESTION_2":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                                chatId: chatId,
                                text: "Вы успешно покинули тест!",
                                replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                    }
                    else if (messageText == "2" || messageText == "3" || messageText == "4")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Не правильно ❌"); // Добавить баллы пользователю

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вопрос №3:\nКакой вариант верный?\n1 - Неверный\n2 - Неверный\n3 - Неверный\n4 - Верный\n\nДля выхода из теста - /exit");
                        userStates[chatId] = "TEST_1_QUESTION_3";
                    }
                    else if (messageText == "1")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Верно ✅"); // Добавить баллы пользователю

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вопрос №3:\nКакой вариант верный?\n1 - Неверный\n2 - Неверный\n3 - Неверный\n4 - Верный\n\nДля выхода из теста - /exit");
                        userStates[chatId] = "TEST_1_QUESTION_3";
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите вариант ответа на вопрос из предложенного списка!\nВопрос №2:\nКакой вариант верный?\n1 - Верный\n2 - Неверный\n3 - Неверный\n4 - Неверный\n\nДля выхода из теста - /exit");
                    }
                    break;

                case "TEST_1_QUESTION_3":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                                chatId: chatId,
                                text: "Вы успешно покинули тест!",
                                replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                    }
                    else if (messageText == "1" || messageText == "2" || messageText == "3")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Не правильно ❌"); // Добавить баллы пользователю

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Поздравляю! Вы прошли тест. Узнать баллы - /results",
                            replyMarkup: new ReplyKeyboardRemove()); // Добавить выгрузку баллов из бд
                        userStates.Remove(chatId); // Сбрасываем состояние
                    }
                    else if (messageText == "4")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Верно ✅"); // Добавить баллы пользователю

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Поздравляю! Вы прошли тест. Узнать баллы - /results",
                            replyMarkup: new ReplyKeyboardRemove()); // Добавить выгрузку баллов из бд
                        userStates.Remove(chatId); // Сбрасываем состояние
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите вариант ответа на вопрос из предложенного списка!\nВопрос №3:\nКакой вариант верный?\n1 - Неверный\n2 - Неверный\n3 - Неверный\n4 - Верный\n\nДля выхода из теста - /exit");
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
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Ваши баллы null/null"); // null - баллы из бд
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
}
