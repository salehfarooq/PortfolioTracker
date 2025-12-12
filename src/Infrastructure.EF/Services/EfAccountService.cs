using System.Security.Cryptography;
using ApplicationCore.DTOs;
using ApplicationCore.Services;
using Infrastructure.EF.Generated;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EF.Services;

public class EfAccountService : IAccountService
{
    private readonly PortfolioDbContext _context;

    public EfAccountService(PortfolioDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetAccountsAsync()
    {
        try
        {
            return await _context.Accounts
                .Where(a => a.IsActive)
                .Join(
                    _context.Users,
                    a => a.UserID,
                    u => u.UserID,
                    (a, u) => new { Account = a, User = u })
                .OrderBy(x => x.User.UserID)
                .ThenBy(x => x.Account.AccountID)
                .Select(x => new AccountSummaryDto
                {
                    AccountId = x.Account.AccountID,
                    AccountName = x.Account.AccountName,
                    UserName = x.User.Username,
                    AccountType = x.Account.AccountType,
                    IsActive = x.Account.IsActive,
                    CreatedDate = x.Account.CreatedDate.ToDateTime(TimeOnly.MinValue)
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfAccountService.GetAccountsAsync failed: {ex}");
            throw new InvalidOperationException("Failed to retrieve accounts.", ex);
        }
    }

    public async Task<AccountSummaryDto?> GetAccountAsync(int accountId)
    {
        try
        {
            return await _context.Accounts
                .Where(a => a.AccountID == accountId && a.IsActive)
                .Join(
                    _context.Users,
                    a => a.UserID,
                    u => u.UserID,
                    (a, u) => new { Account = a, User = u })
                .Select(x => new AccountSummaryDto
                {
                    AccountId = x.Account.AccountID,
                    AccountName = x.Account.AccountName,
                    UserName = x.User.Username,
                    AccountType = x.Account.AccountType,
                    IsActive = x.Account.IsActive,
                    CreatedDate = x.Account.CreatedDate.ToDateTime(TimeOnly.MinValue)
                })
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfAccountService.GetAccountAsync({accountId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to retrieve account {accountId}.", ex);
        }
    }

    public async Task<AccountSummaryDto> CreateAccountAsync(NewAccountDto newAccount)
    {
        ArgumentNullException.ThrowIfNull(newAccount);
        try
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = RandomNumberGenerator.GetBytes(32);

            var user = new User
            {
                Username = newAccount.Username,
                FullName = newAccount.FullName,
                Email = newAccount.Email,
                Role = "User",
                PasswordHash = hash,
                PasswordSalt = salt
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            var account = new Account
            {
                UserID = user.UserID,
                AccountName = newAccount.AccountName,
                AccountType = newAccount.AccountType,
                IsActive = true,
                CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return new AccountSummaryDto
            {
                AccountId = account.AccountID,
                AccountName = account.AccountName,
                UserName = user.Username,
                AccountType = account.AccountType,
                IsActive = account.IsActive,
                CreatedDate = account.CreatedDate.ToDateTime(TimeOnly.MinValue)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfAccountService.CreateAccountAsync failed: {ex}");
            throw new InvalidOperationException("Failed to create account.", ex);
        }
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync()
    {
        try
        {
            var summaries = await _context.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.UserID,
                    u.Username,
                    u.FullName,
                    u.Email,
                    AccountCount = u.Accounts.Count,
                    ActiveAccountCount = u.Accounts.Count(a => a.IsActive)
                })
                .OrderBy(u => u.Username)
                .ToListAsync()
                .ConfigureAwait(false);

            return summaries.Select(u => new UserSummaryDto
            {
                UserId = u.UserID,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                AccountCount = u.AccountCount,
                ActiveAccountCount = u.ActiveAccountCount
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfAccountService.GetUsersAsync failed: {ex}");
            throw new InvalidOperationException("Failed to retrieve users.", ex);
        }
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Accounts)
                .SingleOrDefaultAsync(u => u.UserID == userId)
                .ConfigureAwait(false);

            if (user is null)
            {
                return false;
            }

            foreach (var acct in user.Accounts)
            {
                acct.IsActive = false;
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfAccountService.DeleteUserAsync({userId}) failed: {ex}");
            throw new InvalidOperationException("Failed to delete user.", ex);
        }
    }
}
