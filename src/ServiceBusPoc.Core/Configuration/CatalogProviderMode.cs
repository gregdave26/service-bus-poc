namespace ServiceBusPoc.Core.Configuration;

/// <summary>Selects the backing implementation for the Digital Site catalog.</summary>
public enum CatalogProviderMode
{
    /// <summary>Reads the application-local canonical catalog.</summary>
    Local,

    /// <summary>Reads products and prices through commercetools.</summary>
    CommerceTools
}
