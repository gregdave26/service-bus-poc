namespace ServiceBusPoc.Core.Utilities;

/// <summary>
/// Validates that the configured Service Bus topology is reachable.
/// </summary>
public interface ITopologyValidator
{
    Task<bool> ValidateAsync();
}
