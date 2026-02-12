namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with generic Data entries.
/// Supports: AlwaysOn, Manual, Trigger
/// Does NOT support: Semantic
/// </summary>
public class GenericDataEnricher(
IContextDataService contextDataService,
IDbContextFactory<GeneralDbContext> dbContextFactory,
ILogger<GenericDataEnricher> logger)
: DataTypeEnricherBase<GenericDataEnricher>(contextDataService, dbContextFactory, logger)
{
    protected override DataType DataType => DataType.Generic;
    protected override string EnricherName => nameof(GenericDataEnricher);
    protected override bool SupportsManual => true;
    protected override bool SupportsSemantic => false;
}