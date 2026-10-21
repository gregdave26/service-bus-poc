namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public interface IFilteredChannelsRoutingVerifier
{
    Task VerifyAsync(CancellationToken cancellationToken = default);
}
