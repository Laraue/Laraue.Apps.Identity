using System.Diagnostics.Metrics;
using Laraue.Apps.Identity.DataAccess.Entities;

namespace Laraue.Apps.Identity.Services.Metrics;

/// <summary>
/// Identity's event counters. Every label has a few values at most: never a user id or an account id. State that
/// must survive a restart (how many users there are now, how they split between accounts and products) is a gauge
/// read from the database, see <c>IdentityStateMetrics</c> in InternalApiServices. A counter is recorded after
/// the row is saved, and a lost insert race records nothing, so a user is counted once.
/// </summary>
public sealed class IdentityMetrics
{
    public const string MeterName = "Laraue.Apps.Identity";

    public const string MethodTelegram = "telegram";
    public const string MethodGoogle = "google";

    private readonly Counter<long> _usersRegistered;
    private readonly Counter<long> _serviceUsersAdded;
    private readonly Counter<long> _accountsLinked;

    public IdentityMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _usersRegistered = meter.CreateCounter<long>(
            "identity.users.registered",
            description: "New global users, by sign-in method (telegram, google) and the service they signed up from.");

        _serviceUsersAdded = meter.CreateCounter<long>(
            "identity.service.users.added",
            description: "Users that started using a service (their first call from it), registered now or earlier.");

        _accountsLinked = meter.CreateCounter<long>(
            "identity.accounts.linked",
            description: "Account link requests by provider and outcome (linked, moved, user_has_other_account, owner_used_by_another_service).");
    }

    public void RecordUserRegistered(string method, ServiceId serviceId)
        => _usersRegistered.Add(1, Tag("method", method), Tag("service", serviceId.ToString()));

    public void RecordServiceUserAdded(ServiceId serviceId)
        => _serviceUsersAdded.Add(1, Tag("service", serviceId.ToString()));

    public void RecordAccountLinked(string provider, LinkAccountOutcome outcome)
        => _accountsLinked.Add(1, Tag("provider", provider), Tag("outcome", OutcomeLabel(outcome)));

    private static string OutcomeLabel(LinkAccountOutcome outcome) => outcome switch
    {
        LinkAccountOutcome.Linked => "linked",
        LinkAccountOutcome.Moved => "moved",
        LinkAccountOutcome.OwnerUsedByAnotherService => "owner_used_by_another_service",
        LinkAccountOutcome.UserHasOtherAccount => "user_has_other_account",
        _ => "unknown",
    };

    private static KeyValuePair<string, object?> Tag(string name, object? value) => new(name, value);
}
