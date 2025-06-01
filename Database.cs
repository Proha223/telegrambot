using MySql.Data.MySqlClient;
using System;

public class Database : IDisposable
{
    private readonly MySqlConnection _connection;
    private readonly object _lock = new object();

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

    public bool UserExists(string username)
    {
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", _connection);
        cmd.Parameters.AddWithValue("@username", username);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public int RegisterUser(string firstName, string lastName, string username)
    {
        lock (_lock) // Блокировка для потокобезопасности
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // Получаем максимальный ID
                int newId = 1;
                using (var cmd = new MySqlCommand("SELECT MAX(id) FROM users", _connection, transaction))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value)
                    {
                        newId = Convert.ToInt32(result) + 1;
                    }
                }

                // Вставляем нового пользователя
                using var insertCmd = new MySqlCommand(
                    "INSERT INTO users (id, first_name, last_name, username, role, register_date) " +
                    "VALUES (@id, @firstName, @lastName, @username, 'user', NOW())",
                    _connection, transaction);

                insertCmd.Parameters.AddWithValue("@id", newId);
                insertCmd.Parameters.AddWithValue("@firstName", firstName);
                insertCmd.Parameters.AddWithValue("@lastName", lastName);
                insertCmd.Parameters.AddWithValue("@username", username);

                insertCmd.ExecuteNonQuery();
                transaction.Commit();

                Console.WriteLine($"Зарегистрирован пользователь с ID: {newId}");
                return newId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
