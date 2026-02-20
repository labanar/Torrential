namespace Torrential.Web.Api.Requests.Integrations
{
    public class IntegrationHookCreateRequest
    {
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required string Trigger { get; init; }
        public required string Configuration { get; init; }
        public bool Enabled { get; init; } = true;
    }
}
