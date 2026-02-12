namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with Insight data.
/// Supports: AlwaysOn, Manual, Trigger, Semantic (Semantic handled separately)
/// </summary>
public class InsightEnricher(
IContextDataService contextDataService,
IDbContextFactory<GeneralDbContext> dbContextFactory,
ILogger<InsightEnricher> logger)
: DataTypeEnricherBase<InsightEnricher>(contextDataService, dbContextFactory, logger)
{
    protected override DataType DataType => DataType.Insight;
    protected override string EnricherName => nameof(InsightEnricher);
    protected override bool SupportsManual => true;
    protected override bool SupportsSemantic => true; // Handled by SemanticDataEnricher
}