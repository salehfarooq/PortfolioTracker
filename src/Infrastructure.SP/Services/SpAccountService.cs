using ApplicationCore.DTOs;
using ApplicationCore.Domain;
using ApplicationCore.Services;
using ApplicationCore.DataAccess;
using Infrastructure.SP.DataAccess;
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
            cmd.AddInt("@AccountID", accountId);

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
        PortfolioValidation.ValidateNewAccount(newAccount);

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
                cmd.AddString("@Username", newAccount.Username.Trim(), 64);
                cmd.AddBytes("@PasswordHash", RandomNumberGenerator.GetBytes(32), 256);
                cmd.AddBytes("@PasswordSalt", RandomNumberGenerator.GetBytes(16), 128);
                cmd.AddString("@FullName", newAccount.FullName.Trim(), 128);
                cmd.AddString("@Email", newAccount.Email.Trim(), 128);
                cmd.AddString("@Role", "User", 16);

                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                userId = Convert.ToInt32(result);
            }

            int accountId;
            using (var cmd = new SqlCommand(insertAccountSql, (SqlConnection)conn, (SqlTransaction)tx))
            {
                cmd.AddInt("@UserID", userId);
                cmd.AddString("@AccountType", newAccount.AccountType.Trim(), 32);
                cmd.AddString("@AccountName", newAccount.AccountName.Trim(), 64);

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

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync()
    {
        const string sql = @"
SELECT u.UserID, u.Username, u.FullName, u.Email,
       COUNT(a.AccountID) AS AccountCount,
       SUM(CASE WHEN a.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveAccountCount
FROM Users u
LEFT JOIN Accounts a ON a.UserID = u.UserID
GROUP BY u.UserID, u.Username, u.FullName, u.Email
ORDER BY u.Username;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var list = new List<UserSummaryDto>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new UserSummaryDto
                {
                    UserId = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    FullName = reader.GetString(2),
                    Email = reader.GetString(3),
                    AccountCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    ActiveAccountCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpAccountService.GetUsersAsync failed: {ex}");
            throw new InvalidOperationException("Failed to retrieve users via stored procedures.", ex);
        }
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        const string deactivateSql = @"
UPDATE Accounts
SET IsActive = 0
WHERE UserID = @UserID;
";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var tx = await ((SqlConnection)conn).BeginTransactionAsync().ConfigureAwait(false);

            var affectedRows = 0;
            using (var cmd = new SqlCommand(deactivateSql, (SqlConnection)conn, (SqlTransaction)tx))
            {
                cmd.AddInt("@UserID", userId);
                affectedRows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
            return affectedRows > 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpAccountService.DeleteUserAsync({userId}) failed: {ex}");
            throw new InvalidOperationException("Failed to delete user via stored procedures.", ex);
        }
    }
}
