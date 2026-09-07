using Laraue.Apps.Identity.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Laraue.Apps.Identity.IntegrationTests.Infrastructure;

public static class DbExtensions
{
    /// <summary>
    /// Deletes every row written by a test (users, their Telegram links, their recorded service
    /// usage) without touching the static seed data (<see cref="DatabaseContext.Services"/>).
    /// </summary>
    public static void CleanDatabase(this DatabaseContext dbContext)
    {
        dbContext.UserServices.ExecuteDelete();
        dbContext.TelegramAccounts.ExecuteDelete();
        dbContext.Users.ExecuteDelete();
    }
}
