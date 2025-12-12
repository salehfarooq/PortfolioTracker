using System.Collections.Generic;
using System.Threading.Tasks;
using ApplicationCore.DTOs;

namespace ApplicationCore.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountSummaryDto>> GetAccountsAsync();
    Task<AccountSummaryDto?> GetAccountAsync(int accountId);
    Task<AccountSummaryDto> CreateAccountAsync(NewAccountDto newAccount);
    Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync();
    Task<bool> DeleteUserAsync(int userId);
}
