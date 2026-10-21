namespace ServiceBusPoc.ContactEvents.Verifier.Services;

public interface IDigitalChannelsRoutingVerifier
{
    Task VerifyAsync(CancellationToken cancellationToken = default);
}
