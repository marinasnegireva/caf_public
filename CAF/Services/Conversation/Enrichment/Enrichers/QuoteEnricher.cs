namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with Quote data.
/// Supports: AlwaysOn, Manual, Semantic (Semantic handled separately)
/// Does NOT support: Trigger
/// </summary>
public class QuoteEnricher(
IContextDataService contextDataService,
IDbContextFactory<GeneralDbContext> dbContextFactory,
ILogger<QuoteEnricher> logger)
: DataTypeEnricherBase<QuoteEnricher>(contextDataService, dbContextFactory, logger)
{
    protected override DataType DataType => DataType.Quote;
    protected override string EnricherName => nameof(QuoteEnricher);
    protected override bool SupportsManual => true;
    protected override bool SupportsSemantic => true; // Handled by SemanticDataEnricher
}