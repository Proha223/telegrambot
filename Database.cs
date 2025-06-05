using MySql.Data.MySqlClient;
using System;

public class Database : IDisposable
{
    private readonly MySqlConnection _connection;

    public Database(string connectionString)
    {
        try
        {
            _connection = new MySqlConnection(connectionString);
            _connection.Open();
            Console.WriteLine("Успешное подключение к MySQL");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка подключения к MySQL: {ex.Message}");
            throw;
        }
    }

    public bool UserExists(long userTelegramId)
    {
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE userTelegramId = @userTelegramId", _connection);
        cmd.Parameters.AddWithValue("@userTelegramId", userTelegramId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public void RegisterUser(long userTelegramId, string firstName, string lastName, string? username)
    {
        using var cmd = new MySqlCommand(
            "INSERT INTO users (first_name, last_name, username, role, register_date, userTelegramId) " +
            "VALUES (@firstName, @lastName, @username, 'user', NOW(), @userTelegramId)", _connection);

        cmd.Parameters.AddWithValue("@userTelegramId", userTelegramId);
        cmd.Parameters.AddWithValue("@firstName", firstName);
        cmd.Parameters.AddWithValue("@lastName", lastName);
        cmd.Parameters.AddWithValue("@username", username ?? (object)DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }

    public List<TestQuestion> GetTestQuestions()
    {
        var questions = new List<TestQuestion>();

        using var cmd = new MySqlCommand("SELECT * FROM tests", _connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            questions.Add(new TestQuestion
            {
                TestId = reader.GetInt32("id_test"),
                QuestionText = reader.GetString("question_text"),
                Option1 = reader.GetString("option1"),
                Option2 = reader.GetString("option2"),
                Option3 = reader.IsDBNull(reader.GetOrdinal("option3")) ? null : reader.GetString("option3"),
                Option4 = reader.IsDBNull(reader.GetOrdinal("option4")) ? null : reader.GetString("option4"),
                CorrectAnswer = reader.GetInt32("correct_answer")
            });
        }

        return questions;
    }

    public int GetUserIdByTelegramId(long telegramId)
    {
        using var cmd = new MySqlCommand("SELECT id FROM users WHERE userTelegramId = @telegramId", _connection);
        cmd.Parameters.AddWithValue("@telegramId", telegramId);
        var result = cmd.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public void RecordUserAnswer(int userId, int testId, int chosenAnswer, bool isCorrect)
    {
        using var checkCmd = new MySqlCommand(
            "SELECT id_answer, is_correct FROM userAnswers WHERE user_id = @userId AND test_id = @testId",
            _connection);
        checkCmd.Parameters.AddWithValue("@userId", userId);
        checkCmd.Parameters.AddWithValue("@testId", testId);

        using var reader = checkCmd.ExecuteReader();
        bool exists = reader.Read();
        int? previousCorrect = exists ? (reader.GetBoolean("is_correct") ? 1 : 0) : null;
        int answerId = exists ? reader.GetInt32("id_answer") : 0;
        reader.Close();

        if (exists)
        {
            using var updateCmd = new MySqlCommand(
                "UPDATE userAnswers SET chosen_answer = @chosenAnswer, is_correct = @isCorrect " +
                "WHERE id_answer = @answerId", _connection);
            updateCmd.Parameters.AddWithValue("@chosenAnswer", chosenAnswer);
            updateCmd.Parameters.AddWithValue("@isCorrect", isCorrect);
            updateCmd.Parameters.AddWithValue("@answerId", answerId);
            updateCmd.ExecuteNonQuery();

            int pointsDiff = (isCorrect ? 1 : 0) - (previousCorrect ?? 0);
            if (pointsDiff != 0)
            {
                UpdateUserScore(userId, pointsDiff);
            }
        }
        else
        {
            using var insertCmd = new MySqlCommand(
                "INSERT INTO userAnswers (user_id, test_id, chosen_answer, is_correct) " +
                "VALUES (@userId, @testId, @chosenAnswer, @isCorrect)", _connection);
            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@testId", testId);
            insertCmd.Parameters.AddWithValue("@chosenAnswer", chosenAnswer);
            insertCmd.Parameters.AddWithValue("@isCorrect", isCorrect);
            insertCmd.ExecuteNonQuery();

            if (isCorrect)
            {
                UpdateUserScore(userId, 1);
            }
        }
    }

    private void UpdateUserScore(int userId, int pointsToAdd)
    {
        using var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM userScores WHERE user_id = @userId", _connection);
        checkCmd.Parameters.AddWithValue("@userId", userId);
        bool exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

        if (exists)
        {
            using var updateCmd = new MySqlCommand(
                "UPDATE userScores SET total_points = total_points + @points WHERE user_id = @userId", _connection);
            updateCmd.Parameters.AddWithValue("@userId", userId);
            updateCmd.Parameters.AddWithValue("@points", pointsToAdd);
            updateCmd.ExecuteNonQuery();
        }
        else
        {
            using var insertCmd = new MySqlCommand(
                "INSERT INTO userScores (user_id, total_points) VALUES (@userId, @points)", _connection);
            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@points", pointsToAdd);
            insertCmd.ExecuteNonQuery();
        }
    }

    public int GetUserTotalPoints(int userId)
    {
        using var cmd = new MySqlCommand("SELECT total_points FROM userScores WHERE user_id = @userId", _connection);
        cmd.Parameters.AddWithValue("@userId", userId);
        var result = cmd.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public int GetUserTotalCorrectAnswers(int userId)
    {
        using var cmd = new MySqlCommand(
            "SELECT COUNT(DISTINCT test_id) FROM userAnswers WHERE user_id = @userId AND is_correct = TRUE",
            _connection);
        cmd.Parameters.AddWithValue("@userId", userId);
        var result = cmd.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public int GetTotalQuestionsCount()
    {
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM tests", _connection);
        var result = cmd.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result);
    }

    public List<(int Id, string TopicName)> GetTheoryTopics()
    {
        var topics = new List<(int, string)>();

        using var cmd = new MySqlCommand("SELECT id_theory, topic_name FROM theory_materials", _connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            topics.Add((reader.GetInt32("id_theory"), reader.GetString("topic_name")));
        }

        return topics;
    }

    public string GetTheoryDescription(int theoryId)
    {
        using var cmd = new MySqlCommand("SELECT description FROM theory_materials WHERE id_theory = @theoryId", _connection);
        cmd.Parameters.AddWithValue("@theoryId", theoryId);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? "Теория по данной теме не найдена";
    }

    public string GetUserRole(long userTelegramId)
    {
        using var cmd = new MySqlCommand("SELECT role FROM users WHERE userTelegramId = @userTelegramId", _connection);
        cmd.Parameters.AddWithValue("@userTelegramId", userTelegramId);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? "user";
    }

    public List<string> GetTableNames()
    {
        var tables = new List<string>();
        using var cmd = new MySqlCommand("SHOW TABLES", _connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    public string GetTableStructure(string tableName)
    {
        using var cmd = new MySqlCommand($"DESCRIBE {tableName}", _connection);
        using var reader = cmd.ExecuteReader();

        var structure = new System.Text.StringBuilder();
        structure.AppendLine($"Таблица [{tableName}]:");

        while (reader.Read())
        {
            string field = reader.GetString("Field");
            string type = reader.GetString("Type");
            structure.AppendLine($"{field} - {type}");
        }

        return structure.ToString();
    }

    public List<Dictionary<string, object>> GetTableData(string tableName)
    {
        var data = new List<Dictionary<string, object>>();
        using var cmd = new MySqlCommand($"SELECT * FROM {tableName}", _connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row.Add(reader.GetName(i), reader.GetValue(i));
            }
            data.Add(row);
        }

        return data;
    }

    public Dictionary<string, object> GetTableRowById(string tableName, string idColumn, int id)
    {
        using var cmd = new MySqlCommand($"SELECT * FROM {tableName} WHERE {idColumn} = @id", _connection);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row.Add(reader.GetName(i), reader.GetValue(i));
            }
            return row;
        }

        return null;
    }

    public bool InsertTableRow(string tableName, Dictionary<string, object> data)
    {
        try
        {
            var columns = string.Join(", ", data.Keys);
            var parameters = string.Join(", ", data.Keys.Select(k => $"@{k}"));

            using var cmd = new MySqlCommand($"INSERT INTO {tableName} ({columns}) VALUES ({parameters})", _connection);

            foreach (var item in data)
            {
                cmd.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value);
            }

            return cmd.ExecuteNonQuery() > 0;
        }
        catch
        {
            return false;
        }
    }

    public bool UpdateTableRow(string tableName, string idColumn, int id, Dictionary<string, object> data)
    {
        try
        {
            var setClause = string.Join(", ", data.Keys.Select(k => $"{k} = @{k}"));

            using var cmd = new MySqlCommand($"UPDATE {tableName} SET {setClause} WHERE {idColumn} = @id", _connection);

            foreach (var item in data)
            {
                cmd.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value);
            }
            cmd.Parameters.AddWithValue("@id", id);

            return cmd.ExecuteNonQuery() > 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetPrimaryKeyColumn(string tableName)
    {
        using var cmd = new MySqlCommand(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_KEY = 'PRI'",
            _connection);

        cmd.Parameters.AddWithValue("@tableName", tableName);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }
}

public class TestQuestion
{
    public int TestId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Option1 { get; set; } = string.Empty;
    public string Option2 { get; set; } = string.Empty;
    public string? Option3 { get; set; }
    public string? Option4 { get; set; }
    public int CorrectAnswer { get; set; }
}
