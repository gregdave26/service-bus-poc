using System.Text.Json.Nodes;
using ServiceBusPoc.DigitalSite.CommerceToolsStub.Querying;
using ServiceBusPoc.DigitalSite.Shared.CommerceTools;

namespace ServiceBusPoc.Tests.DigitalSite;

public sealed class QueryPredicateTests
{
    private static readonly JsonNode Cart = JsonNode.Parse("""
        { "anonymousId": "CRM-1", "cartState": "Active", "cart": { "id": "c-1" }, "note": "say \"hi\" and bye" }
        """)!;

    [Theory]
    [InlineData("", true)]
    [InlineData("anonymousId = \"CRM-1\"", true)]
    [InlineData("anonymousId = \"CRM-1\" and cartState = \"Active\"", true)]
    [InlineData("anonymousId = \"CRM-1\" AND cartState = \"Ordered\"", false)]
    [InlineData("cart(id = \"c-1\")", true)]
    [InlineData("missing.path = \"x\"", false)]
    public void Matches_EvaluatesSupportedSyntax(string where, bool expected)
    {
        Assert.Equal(expected, QueryPredicate.Parse(where).Matches(Cart));
    }

    [Fact]
    public void Parse_KeywordInsideQuotes_IsNotASeparator()
    {
        var predicate = QueryPredicate.Parse(CommercePredicate.EqualTo("note", "say \"hi\" and bye"));

        Assert.True(predicate.Matches(Cart));
    }

    [Theory]
    [InlineData("anonymousId > 1")]
    [InlineData("anonymousId = CRM-1")]
    [InlineData("anonymousId = \"CRM-1\" or cartState = \"Active\"")]
    public void Parse_UnsupportedSyntax_Throws(string where)
    {
        Assert.Throws<FormatException>(() => QueryPredicate.Parse(where));
    }

    [Fact]
    public void CommercePredicate_EscapesQuotesAndBackslashes()
    {
        Assert.Equal("a = \"x\\\"y\\\\z\"", CommercePredicate.EqualTo("a", "x\"y\\z"));
        Assert.Equal("a = \"1\" and b = \"2\"", CommercePredicate.And("a = \"1\"", "b = \"2\""));
    }
}
