namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with Memory data.
/// Supports: AlwaysOn, Manual, Trigger, Semantic (Semantic handled separately)
/// </summary>
public class MemoryDataEnricher(
IContextDataService contextDataService,
IDbContextFactory<GeneralDbContext> dbContextFactory,
ILogger<MemoryDataEnricher> logger)
: DataTypeEnricherBase<MemoryDataEnricher>(contextDataService, dbContextFactory, logger)
{
    protected override DataType DataType => DataType.Memory;
    protected override string EnricherName => nameof(MemoryDataEnricher);
    protected override bool SupportsManual => true;
    protected override bool SupportsSemantic => true; // Handled by SemanticDataEnricher
}