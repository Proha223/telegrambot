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
                TestId = reader.GetInt32("test_id"),
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
        using var cmd = new MySqlCommand(
            "INSERT INTO userAnswers (user_id, test_id, chosen_answer, is_correct) " +
            "VALUES (@userId, @testId, @chosenAnswer, @isCorrect)", _connection);
            
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@testId", testId);
        cmd.Parameters.AddWithValue("@chosenAnswer", chosenAnswer);
        cmd.Parameters.AddWithValue("@isCorrect", isCorrect);
        
        cmd.ExecuteNonQuery();
        
        // Обновляем общий счет
        UpdateUserScore(userId, isCorrect ? 1 : 0);
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
