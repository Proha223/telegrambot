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
}
