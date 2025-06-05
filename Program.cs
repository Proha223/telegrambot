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
    private static Dictionary<long, (string, Dictionary<string, object>)> adminStates = new();
    private static Dictionary<long, string> adminTableSelection = new();
    private static Dictionary<long, int> adminEditingId = new();
    private static Dictionary<long, string> adminEditingColumn = new();

    private static void Main()
    {
        try
        {
            string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
               ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN переменная окружения не задана в Railway");
            /*string token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
                         ?? ""; // Для локального тестирования */

            string connectionString;

            if (Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT") != null)
            {
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

        long userTelegramId = update.Message.From.Id;
        long chatId = update.Message.Chat.Id;
        string messageText = update.Message.Text;

        if (!_database.UserExists(userTelegramId))
        {
            string firstName = update.Message.From.FirstName ?? "";
            string lastName = update.Message.From.LastName ?? "";
            string username = update.Message.From.Username ?? string.Empty;

            _database.RegisterUser(userTelegramId, firstName, lastName, username);
        }

        if (userStates.TryGetValue(chatId, out string state))
        {
            switch (state)
            {
                case "ADMIN_PANEL":
                    if (messageText == "/exit")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Вы вышли из админ-панели!",
                            replyMarkup: new ReplyKeyboardRemove());
                        userStates.Remove(chatId);
                        adminStates.Remove(chatId);
                        adminTableSelection.Remove(chatId);
                        adminEditingId.Remove(chatId);
                        adminEditingColumn.Remove(chatId);
                        break;
                    }

                    if (messageText == "Просмотр таблиц")
                    {
                        var tables = _database.GetTableNames();
                        var tableButtons = tables.Select(t => new KeyboardButton(t))
                            .Concat(new[] { new KeyboardButton("Назад") })
                            .Chunk(2)
                            .ToArray();

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите таблицу для просмотра:",
                            replyMarkup: new ReplyKeyboardMarkup(tableButtons)
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            });

                        userStates[chatId] = "ADMIN_VIEW_TABLES";
                        break;
                    }
                    else if (messageText == "Изменение данных")
                    {
                        var tables = _database.GetTableNames();
                        var tableButtons = tables.Select(t => new KeyboardButton(t))
                            .Concat(new[] { new KeyboardButton("Назад") })
                            .Chunk(2)
                            .ToArray();

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите таблицу для изменения:",
                            replyMarkup: new ReplyKeyboardMarkup(tableButtons)
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            });

                        userStates[chatId] = "ADMIN_EDIT_TABLES";
                        break;
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Используйте кнопки для навигации или /exit для выхода");
                        break;
                    }

                case "ADMIN_VIEW_TABLES":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        else if (messageText == "Назад")
                        {
                            var adminKeyboard = new ReplyKeyboardMarkup(new[]
                            {
                                new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                new KeyboardButton[] { "/exit" }
                            })
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            };

                            await client.SendMessage(
                                chatId: chatId,
                                text: "Возврат в главное меню админ-панели:",
                                replyMarkup: adminKeyboard);

                            userStates[chatId] = "ADMIN_PANEL";
                            break;
                        }

                        var tables = _database.GetTableNames();
                        if (tables.Contains(messageText))
                        {
                            string structure = _database.GetTableStructure(messageText);
                            var data = _database.GetTableData(messageText);

                            var response = new System.Text.StringBuilder();
                            response.AppendLine(structure);
                            response.AppendLine("\nДанные:");

                            foreach (var row in data)
                            {
                                response.AppendLine(string.Join("; ", row.Select(kv => $"{kv.Key}: {kv.Value}")));
                                response.AppendLine();
                            }

                            await client.SendMessage(
                                chatId: chatId,
                                text: response.ToString(),
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                                    new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                    new KeyboardButton[] { "/exit" }
                                })
                                {
                                    ResizeKeyboard = true,
                                    OneTimeKeyboard = true
                                });

                            userStates[chatId] = "ADMIN_PANEL";
                        }
                        else
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Пожалуйста, выберите таблицу из списка или /exit для выхода");
                        }
                        break;
                    }

                case "ADMIN_EDIT_TABLES":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        else if (messageText == "Назад")
                        {
                            var adminKeyboard = new ReplyKeyboardMarkup(new[]
                            {
                                new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                new KeyboardButton[] { "/exit" }
                            })
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            };

                            await client.SendMessage(
                                chatId: chatId,
                                text: "Возврат в главное меню админ-панели:",
                                replyMarkup: adminKeyboard);

                            userStates[chatId] = "ADMIN_PANEL";
                            break;
                        }
                        var tables = _database.GetTableNames();
                        if (tables.Contains(messageText))
                        {
                            adminTableSelection[chatId] = messageText;

                            var editOptions = new ReplyKeyboardMarkup(new[]
                            {
                                new KeyboardButton[] { "Добавить данные", "Редактировать данные" },
                                new KeyboardButton[] { "Назад", "/exit" }
                            })
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            };

                            await client.SendMessage(
                                chatId: chatId,
                                text: $"Выбрана таблица: {messageText}. Выберите действие:",
                                replyMarkup: editOptions);

                            userStates[chatId] = "ADMIN_EDIT_OPTIONS";
                        }
                        else
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Пожалуйста, выберите таблицу из списка или /exit для выхода");
                        }
                        break;
                    }

                case "ADMIN_EDIT_OPTIONS":
                    if (messageText == "/exit")
                    {
                        await ExitAdminPanel(client, chatId);
                        break;
                    }
                    if (messageText == "Назад")
                    {
                        var tables = _database.GetTableNames();
                        var tableButtons = tables.Select(t => new KeyboardButton(t))
                            .Concat(new[] { new KeyboardButton("Назад") })
                            .Chunk(2)
                            .ToArray();

                        await client.SendMessage(
                            chatId: chatId,
                            text: "Выберите таблицу для изменения:",
                            replyMarkup: new ReplyKeyboardMarkup(tableButtons)
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            });

                        userStates[chatId] = "ADMIN_EDIT_TABLES";
                        adminTableSelection.Remove(chatId);
                        break;
                    }
                    else if (messageText == "Добавить данные")
                    {
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка выбора таблицы. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        var structure = _database.GetTableStructure(tableName);
                        var columns = structure.Split('\n')
                            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("Таблица"))
                            .Select(line => line.Split('-')[0].Trim())
                            .ToList();

                        adminStates[chatId] = ("ADD_DATA", new Dictionary<string, object>());

                        await client.SendMessage(
                            chatId: chatId,
                            text: $"Введите значение для {columns[0]} (для отмены введите /exit):",
                            replyMarkup: new ReplyKeyboardRemove());

                        userStates[chatId] = "ADMIN_ADD_DATA";
                        adminEditingColumn[chatId] = columns[0];
                        break;
                    }
                    else if (messageText == "Редактировать данные")
                    {
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка выбора таблицы. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        string idColumn = _database.GetPrimaryKeyColumn(tableName);
                        if (string.IsNullOrEmpty(idColumn))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Не удалось определить первичный ключ таблицы.");
                            break;
                        }

                        await client.SendMessage(
                            chatId: chatId,
                            text: $"Введите ID записи для редактирования (первичный ключ {idColumn}):",
                            replyMarkup: new ReplyKeyboardRemove());

                        userStates[chatId] = "ADMIN_EDIT_ID";
                        adminEditingColumn[chatId] = idColumn;
                        break;
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Используйте кнопки для навигации или /exit для выхода");
                        break;
                    }

                case "ADMIN_ADD_DATA":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName) ||
                            !adminStates.TryGetValue(chatId, out var adminState) ||
                            !adminEditingColumn.TryGetValue(chatId, out string currentColumn))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка состояния. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        var (action, data) = adminState;
                        var structure = _database.GetTableStructure(tableName);
                        var columns = structure.Split('\n')
                            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("Таблица"))
                            .Select(line => line.Split('-')[0].Trim())
                            .ToList();

                        int currentIndex = columns.IndexOf(currentColumn);

                        data[currentColumn] = messageText;

                        if (currentIndex == columns.Count - 1)
                        {
                            var response = new System.Text.StringBuilder();
                            response.AppendLine($"Вы ввели следующие данные для таблицы {tableName}:");

                            foreach (var column in columns)
                            {
                                response.AppendLine($"{column} - {data.GetValueOrDefault(column, "NULL")}");
                            }

                            var confirmKeyboard = new ReplyKeyboardMarkup(new[]
                            {
                                new KeyboardButton[] { "Отменить", "Добавить" },
                                new KeyboardButton[] { "/exit" }
                            })
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            };

                            await client.SendMessage(
                                chatId: chatId,
                                text: response.ToString(),
                                replyMarkup: confirmKeyboard);

                            userStates[chatId] = "ADMIN_CONFIRM_ADD";
                        }
                        else
                        {
                            string nextColumn = columns[currentIndex + 1];
                            adminEditingColumn[chatId] = nextColumn;

                            await client.SendMessage(
                                chatId: chatId,
                                text: $"Введите значение для {nextColumn} (для отмены введите /exit):");
                        }
                        break;
                    }

                case "ADMIN_CONFIRM_ADD":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName) ||
                            !adminStates.TryGetValue(chatId, out var adminState))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка состояния. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        var (action, data) = adminState;

                        if (messageText == "Добавить")
                        {
                            bool success = _database.InsertTableRow(tableName, data);

                            await client.SendMessage(
                                chatId: chatId,
                                text: success
                                    ? "Данные успешно добавлены!"
                                    : "Ошибка при добавлении данных. Проверьте введенные значения.",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                                    new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                    new KeyboardButton[] { "/exit" }
                                })
                                {
                                    ResizeKeyboard = true,
                                    OneTimeKeyboard = true
                                });

                            userStates[chatId] = "ADMIN_PANEL";
                            adminStates.Remove(chatId);
                            adminTableSelection.Remove(chatId);
                            adminEditingColumn.Remove(chatId);
                        }
                        else if (messageText == "Отменить")
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Добавление данных отменено.",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                                    new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                    new KeyboardButton[] { "/exit" }
                                })
                                {
                                    ResizeKeyboard = true,
                                    OneTimeKeyboard = true
                                });

                            userStates[chatId] = "ADMIN_PANEL";
                            adminStates.Remove(chatId);
                            adminTableSelection.Remove(chatId);
                            adminEditingColumn.Remove(chatId);
                        }
                        else
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Используйте кнопки для подтверждения или отмены");
                        }
                        break;
                    }

                case "ADMIN_EDIT_ID":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName) ||
                            !adminEditingColumn.TryGetValue(chatId, out string idColumn))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка состояния. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        if (!int.TryParse(messageText, out int id))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Пожалуйста, введите числовой ID.");
                            break;
                        }

                        var row = _database.GetTableRowById(tableName, idColumn, id);
                        if (row == null)
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Запись с таким ID не найдена. Попробуйте еще раз.");
                            break;
                        }

                        adminEditingId[chatId] = id;

                        var structure = _database.GetTableStructure(tableName);
                        var columns = structure.Split('\n')
                            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("Таблица"))
                            .Select(line => line.Split('-')[0].Trim())
                            .ToList();

                        var columnButtons = columns.Select(c => new KeyboardButton(c)).Chunk(2).ToArray();

                        await client.SendMessage(
                            chatId: chatId,
                            text: $"Выберите столбец для редактирования (текущие значения):\n{string.Join("\n", row.Select(kv => $"{kv.Key}: {kv.Value}"))}",
                            replyMarkup: new ReplyKeyboardMarkup(columnButtons)
                            {
                                ResizeKeyboard = true,
                                OneTimeKeyboard = true
                            });

                        userStates[chatId] = "ADMIN_EDIT_COLUMN";
                        break;
                    }

                case "ADMIN_EDIT_COLUMN":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName) ||
                            !adminEditingId.TryGetValue(chatId, out int id) ||
                            !adminEditingColumn.TryGetValue(chatId, out string idColumn))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка состояния. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        var structure = _database.GetTableStructure(tableName);
                        var columns = structure.Split('\n')
                            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("Таблица"))
                            .Select(line => line.Split('-')[0].Trim())
                            .ToList();

                        if (!columns.Contains(messageText))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Пожалуйста, выберите столбец из списка.");
                            break;
                        }

                        adminEditingColumn[chatId] = messageText;

                        await client.SendMessage(
                            chatId: chatId,
                            text: $"Введите новое значение для {messageText} (для отмены введите /exit):",
                            replyMarkup: new ReplyKeyboardRemove());

                        userStates[chatId] = "ADMIN_EDIT_VALUE";
                        break;
                    }

                case "ADMIN_EDIT_VALUE":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }
                        if (!adminTableSelection.TryGetValue(chatId, out string tableName) ||
                            !adminEditingId.TryGetValue(chatId, out int id) ||
                            !adminEditingColumn.TryGetValue(chatId, out string columnName))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка состояния. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        string idColumn = _database.GetPrimaryKeyColumn(tableName);
                        if (string.IsNullOrEmpty(idColumn))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Не удалось определить первичный ключ таблицы.");
                            userStates.Remove(chatId);
                            break;
                        }

                        var row = _database.GetTableRowById(tableName, idColumn, id);
                        if (row == null)
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Запись не найдена. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        row[columnName] = messageText;

                        var response = new System.Text.StringBuilder();
                        response.AppendLine($"Вы точно хотите изменить таблицу {tableName}?");
                        response.AppendLine("Новые значения:");

                        foreach (var item in row)
                        {
                            response.AppendLine($"{item.Key}: {item.Value}");
                        }

                        var confirmKeyboard = new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton[] { "Отменить", "Изменить" },
                            new KeyboardButton[] { "/exit" }
                        })
                        {
                            ResizeKeyboard = true,
                            OneTimeKeyboard = true
                        };

                        await client.SendMessage(
                            chatId: chatId,
                            text: response.ToString(),
                            replyMarkup: confirmKeyboard);

                        adminStates[chatId] = ("EDIT_DATA", row.ToDictionary(kv => kv.Key, kv => kv.Value));
                        userStates[chatId] = "ADMIN_CONFIRM_EDIT";
                        break;
                    }

                case "ADMIN_CONFIRM_EDIT":
                    {
                        if (messageText == "/exit")
                        {
                            await ExitAdminPanel(client, chatId);
                            break;
                        }

                        if (!adminTableSelection.TryGetValue(chatId, out string tableName) ||
                            !adminEditingId.TryGetValue(chatId, out int id) ||
                            !adminStates.TryGetValue(chatId, out var adminState))
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Ошибка состояния. Попробуйте снова.");
                            userStates.Remove(chatId);
                            break;
                        }

                        var (action, data) = adminState;

                        if (messageText == "Изменить")
                        {
                            string idColumn = _database.GetPrimaryKeyColumn(tableName);
                            if (string.IsNullOrEmpty(idColumn))
                            {
                                await client.SendMessage(
                                    chatId: chatId,
                                    text: "Не удалось определить первичный ключ таблицы.");
                                break;
                            }

                            bool success = _database.UpdateTableRow(tableName, idColumn, id, data);

                            if (success)
                            {
                                await client.SendMessage(
                                    chatId: chatId,
                                    text: "Данные успешно изменены!",
                                    replyMarkup: new ReplyKeyboardMarkup(new[]
                                    {
                                        new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                        new KeyboardButton[] { "/exit" }
                                    })
                                    {
                                        ResizeKeyboard = true,
                                        OneTimeKeyboard = true
                                    });
                            }
                            else
                            {
                                await client.SendMessage(
                                    chatId: chatId,
                                    text: "Ошибка при изменении данных. Проверьте:\n" +
                                         "1. Соответствие типов данных\n" +
                                         "2. Обязательные поля\n" +
                                         "3. Ограничения таблицы");
                            }

                            userStates[chatId] = "ADMIN_PANEL";
                            adminStates.Remove(chatId);
                            adminTableSelection.Remove(chatId);
                            adminEditingId.Remove(chatId);
                            adminEditingColumn.Remove(chatId);
                        }
                        else if (messageText == "Отменить")
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Изменение данных отменено.",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                                    new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                                    new KeyboardButton[] { "/exit" }
                                })
                                {
                                    ResizeKeyboard = true,
                                    OneTimeKeyboard = true
                                });

                            userStates[chatId] = "ADMIN_PANEL";
                            adminStates.Remove(chatId);
                            adminTableSelection.Remove(chatId);
                            adminEditingId.Remove(chatId);
                            adminEditingColumn.Remove(chatId);
                        }
                        else
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "Используйте кнопки для подтверждения или отмены");
                        }
                        break;
                    }

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

                    var allTopics = _database.GetTheoryTopics();
                    var selectedTopic = allTopics.FirstOrDefault(t => t.TopicName == messageText);

                    if (selectedTopic != default)
                    {
                        string description = _database.GetTheoryDescription(selectedTopic.Id);

                        await client.SendMessage(
                            chatId: chatId,
                            text: $"📚 {selectedTopic.TopicName}\n\n{description}");
                    }
                    else
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "Пожалуйста, выберите тему из предложенных вариантов!\nВыход из выбора тем - /exit");
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
                        var questions = _database.GetTestQuestions();
                        if (questions.Count == 0)
                        {
                            await client.SendMessage(
                                chatId: chatId,
                                text: "В базе данных нет вопросов для теста.");
                            return;
                        }

                        userTests[chatId] = (questions, 0);

                        await SendQuestion(client, chatId, questions[0], 1, questions.Count);
                        userStates[chatId] = "TEST_IN_PROGRESS";
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

                        if (currentIndex + 1 < questions.Count)
                        {
                            userTests[chatId] = (questions, currentIndex + 1);
                            await SendQuestion(client, chatId, questions[currentIndex + 1], currentIndex + 2, questions.Count);
                        }
                        else
                        {
                            int totalCorrect = _database.GetUserTotalCorrectAnswers(userId);
                            await client.SendMessage(
                                chatId: chatId,
                                text: $"Поздравляю! Вы прошли тест. Ваш результат: {totalCorrect} из {questions.Count}",
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
                    else if (messageText == "test1" || messageText == "test2") // Изменить на названия тестов в НАСТРАИМОВОМ ТИПЕ ТЕСТА или убрать
                    {
                        await client.SendMessage(
                        chatId: chatId,
                        text: $"Вы выбрали: {messageText}");
                        userStates.Remove(chatId);
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
            switch (messageText)
            {
                case "/admin":
                    if (_database.GetUserRole(userTelegramId) != "admin")
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "У вас нет прав доступа к админ-панели!");
                        break;
                    }

                    var adminKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton[] { "Просмотр таблиц", "Изменение данных" },
                        new KeyboardButton[] { "/exit" }
                    })
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };

                    await client.SendMessage(
                        chatId: chatId,
                        text: "Вы вошли в админ-панель. Выберите действие:",
                        replyMarkup: adminKeyboard);

                    userStates[chatId] = "ADMIN_PANEL";
                    break;

                case "/start":
                    await client.SendMessage(
                        chatId: chatId,
                        text: "Добро пожаловать!\n/theory - для изучения теории\n/test - создать новый тест",
                        replyParameters: update.Message.MessageId,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "/theory":
                    var topics = _database.GetTheoryTopics();
                    if (topics.Count == 0)
                    {
                        await client.SendMessage(
                            chatId: chatId,
                            text: "В базе данных пока нет доступных тем для изучения");
                        break;
                    }

                    var topicButtons = topics
                        .Select(t => new KeyboardButton(t.TopicName))
                        .Chunk(2)
                        .ToArray();

                    var replyKeyboardTheory = new ReplyKeyboardMarkup(topicButtons)
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };

                    await client.SendMessage(
                        chatId: chatId,
                        text: "Выберите тему для изучения:\n(Для выхода - /exit)",
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
                    int totalCorrect = _database.GetUserTotalCorrectAnswers(userIdResult);
                    int totalQuestions = _database.GetTotalQuestionsCount();

                    double percentage = totalQuestions > 0 ? Math.Round((double)totalCorrect / totalQuestions * 100, 1) : 0;

                    await client.SendMessage(
                        chatId: chatId,
                        text: $"Ваши баллы: {totalCorrect}/{totalQuestions}\n" +
                              $"Процент прохождения: {percentage}%");
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
        },
        scope: new BotCommandScopeDefault());

        if (_database.GetUserRole(update.Message.From.Id) == "admin")
        {
            await client.SetMyCommands(new[]
            {
                new BotCommand { Command = "/admin", Description = "Админ-панель" },
                new BotCommand { Command = "/start", Description = "Запуск бота" },
                new BotCommand { Command = "/theory", Description = "Изучение теории" },
                new BotCommand { Command = "/test", Description = "Создание нового теста" },
                new BotCommand { Command = "/results", Description = "Результаты тестов" },
                new BotCommand { Command = "/exit", Description = "Выход" },
                new BotCommand { Command = "/help", Description = "Помощь" }
            },
            scope: new BotCommandScopeChat()
            {
                ChatId = new ChatId(update.Message.Chat.Id)
            });
        }
    }

    private static async Task ExitAdminPanel(ITelegramBotClient client, long chatId)
    {
        await client.SendMessage(
            chatId: chatId,
            text: "Вы вышли из админ-панели!",
            replyMarkup: new ReplyKeyboardRemove());

        userStates.Remove(chatId);
        adminStates.Remove(chatId);
        adminTableSelection.Remove(chatId);
        adminEditingId.Remove(chatId);
        adminEditingColumn.Remove(chatId);
    }

    private static async Task SendQuestion(ITelegramBotClient client, long chatId, TestQuestion question, int questionNumber, int totalQuestions)
    {
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
    }
}
