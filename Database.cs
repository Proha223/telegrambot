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

    public void RegisterUser(long userTelegramId, string firstName, string lastName, string username)
    {
        using var cmd = new MySqlCommand(
            "INSERT INTO users (first_name, last_name, username, role, register_date, userTelegramId) " +
            "VALUES (@firstName, @lastName, @username, 'user', NOW(), @userTelegramId)", _connection);

        cmd.Parameters.AddWithValue("@userTelegramId", userTelegramId);
        cmd.Parameters.AddWithValue("@firstName", firstName);
        cmd.Parameters.AddWithValue("@lastName", lastName);
        cmd.Parameters.AddWithValue("@username", username);

        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
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
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void RecordUserAnswer(int userId, int testId, int chosenAnswer, bool isCorrect)
    {
        // Сначала проверяем, есть ли уже ответ на этот вопрос
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
            // Обновляем существующий ответ
            using var updateCmd = new MySqlCommand(
                "UPDATE userAnswers SET chosen_answer = @chosenAnswer, is_correct = @isCorrect " +
                "WHERE id_answer = @answerId", _connection);
            updateCmd.Parameters.AddWithValue("@chosenAnswer", chosenAnswer);
            updateCmd.Parameters.AddWithValue("@isCorrect", isCorrect);
            updateCmd.Parameters.AddWithValue("@answerId", answerId);
            updateCmd.ExecuteNonQuery();

            // Корректируем баллы с учетом предыдущего результата
            int pointsDiff = (isCorrect ? 1 : 0) - (previousCorrect ?? 0);
            if (pointsDiff != 0)
            {
                UpdateUserScore(userId, pointsDiff);
            }
        }
        else
        {
            // Добавляем новый ответ
            using var insertCmd = new MySqlCommand(
                "INSERT INTO userAnswers (user_id, test_id, chosen_answer, is_correct) " +
                "VALUES (@userId, @testId, @chosenAnswer, @isCorrect)", _connection);
            insertCmd.Parameters.AddWithValue("@userId", userId);
            insertCmd.Parameters.AddWithValue("@testId", testId);
            insertCmd.Parameters.AddWithValue("@chosenAnswer", chosenAnswer);
            insertCmd.Parameters.AddWithValue("@isCorrect", isCorrect);
            insertCmd.ExecuteNonQuery();

            // Добавляем баллы за правильный ответ
            if (isCorrect)
            {
                UpdateUserScore(userId, 1);
            }
        }
    }

    public int GetUserTotalCorrectAnswers(int userId)
    {
        using var cmd = new MySqlCommand(
            "SELECT COUNT(DISTINCT test_id) FROM userAnswers WHERE user_id = @userId AND is_correct = TRUE",
            _connection);
        cmd.Parameters.AddWithValue("@userId", userId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void UpdateUserScore(int userId, int pointsToAdd)
    {
        // Проверяем, есть ли запись о баллах у пользователя
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
        return result == null ? 0 : Convert.ToInt32(result);
    }
    public int GetTotalQuestionsCount()
    {
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM tests", _connection);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}

public class TestQuestion
{
    public int TestId { get; set; }
    public string QuestionText { get; set; }
    public string Option1 { get; set; }
    public string Option2 { get; set; }
    public string Option3 { get; set; }
    public string Option4 { get; set; }
    public int CorrectAnswer { get; set; }
}
