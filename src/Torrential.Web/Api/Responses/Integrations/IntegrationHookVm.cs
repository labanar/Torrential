namespace Torrential.Web.Api.Responses.Integrations
{
    public class IntegrationHookVm
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required string Trigger { get; init; }
        public required string Configuration { get; init; }
        public required bool Enabled { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
