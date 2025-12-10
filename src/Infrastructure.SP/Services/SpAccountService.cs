using ApplicationCore.DTOs;
using ApplicationCore.Services;
using System.Data.Common;
using ApplicationCore.DataAccess;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace Infrastructure.SP.Services;

public class SpAccountService : IAccountService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SpAccountService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetAccountsAsync()
    {
        const string sql = @"
SELECT a.AccountID, a.AccountName, u.Username, a.AccountType, a.IsActive, a.CreatedDate
FROM Accounts a
INNER JOIN Users u ON a.UserID = u.UserID
WHERE a.IsActive = 1
ORDER BY u.UserID, a.AccountID;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            var results = new List<AccountSummaryDto>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new AccountSummaryDto
                {
                    AccountId = reader.GetInt32(0),
                    AccountName = reader.GetString(1),
                    UserName = reader.GetString(2),
                    AccountType = reader.GetString(3),
                    IsActive = reader.GetBoolean(4),
                    CreatedDate = reader.GetDateTime(5)
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpAccountService.GetAccountsAsync failed: {ex}");
            throw new InvalidOperationException("Failed to retrieve accounts via stored procedures.", ex);
        }
    }

    public async Task<AccountSummaryDto?> GetAccountAsync(int accountId)
    {
        const string sql = @"
SELECT a.AccountID, a.AccountName, u.Username, a.AccountType, a.IsActive, a.CreatedDate
FROM Accounts a
INNER JOIN Users u ON a.UserID = u.UserID
WHERE a.IsActive = 1 AND a.AccountID = @AccountID;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            cmd.Parameters.AddWithValue("@AccountID", accountId);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return new AccountSummaryDto
            {
                AccountId = reader.GetInt32(0),
                AccountName = reader.GetString(1),
            UserName = reader.GetString(2),
            AccountType = reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            CreatedDate = reader.GetDateTime(5)
        };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpAccountService.GetAccountAsync({accountId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to retrieve account {accountId} via stored procedures.", ex);
        }
    }

    public async Task<AccountSummaryDto> CreateAccountAsync(NewAccountDto newAccount)
    {
        ArgumentNullException.ThrowIfNull(newAccount);

        const string insertUserSql = @"
INSERT INTO Users (Username, PasswordHash, PasswordSalt, FullName, Email, Role)
OUTPUT INSERTED.UserID
VALUES (@Username, @PasswordHash, @PasswordSalt, @FullName, @Email, @Role);";

        const string insertAccountSql = @"
INSERT INTO Accounts (UserID, AccountType, AccountName, IsActive, CreatedDate)
OUTPUT INSERTED.AccountID
VALUES (@UserID, @AccountType, @AccountName, 1, CONVERT(date, GETDATE()));";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var tx = await ((SqlConnection)conn).BeginTransactionAsync().ConfigureAwait(false);

            int userId;
            using (var cmd = new SqlCommand(insertUserSql, (SqlConnection)conn, (SqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@Username", newAccount.Username);
                cmd.Parameters.AddWithValue("@PasswordHash", RandomNumberGenerator.GetBytes(32));
                cmd.Parameters.AddWithValue("@PasswordSalt", RandomNumberGenerator.GetBytes(16));
                cmd.Parameters.AddWithValue("@FullName", newAccount.FullName);
                cmd.Parameters.AddWithValue("@Email", newAccount.Email);
                cmd.Parameters.AddWithValue("@Role", "User");

                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                userId = Convert.ToInt32(result);
            }

            int accountId;
            using (var cmd = new SqlCommand(insertAccountSql, (SqlConnection)conn, (SqlTransaction)tx))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@AccountType", newAccount.AccountType);
                cmd.Parameters.AddWithValue("@AccountName", newAccount.AccountName);

                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                accountId = Convert.ToInt32(result);
            }

            await tx.CommitAsync().ConfigureAwait(false);

            return new AccountSummaryDto
            {
                AccountId = accountId,
                AccountName = newAccount.AccountName,
                UserName = newAccount.Username,
                AccountType = newAccount.AccountType,
                IsActive = true,
                CreatedDate = DateTime.UtcNow.Date
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpAccountService.CreateAccountAsync failed: {ex}");
            throw new InvalidOperationException("Failed to create account via stored procedures.", ex);
        }
    }
}
