using Laraue.Apps.Identity.DataAccess.Entities;

namespace Laraue.Apps.Identity.DataAccess.Data;

public static class ServicesData
{
    private static readonly Service Boards = new()
    {
        Id = ServiceId.LaraueBoards,
        Name = "Laraue Boards",
        Code = "laraue_boards",
    };

    private static readonly Service LearnLanguage = new()
    {
        Id = ServiceId.LearnLanguage,
        Name = "Learn Language",
        Code = "learn_language",
    };

    public static readonly Service[] Services =
    [
        Boards,
        LearnLanguage,
    ];
}
