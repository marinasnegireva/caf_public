namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with PersonaVoiceSample data.
/// Supports: AlwaysOn, Semantic (Semantic handled separately)
/// Does NOT support: Manual, Trigger
/// </summary>
public class PersonaVoiceSampleEnricher(
IContextDataService contextDataService,
IDbContextFactory<GeneralDbContext> dbContextFactory,
ILogger<PersonaVoiceSampleEnricher> logger)
: DataTypeEnricherBase<PersonaVoiceSampleEnricher>(contextDataService, dbContextFactory, logger)
{
    protected override DataType DataType => DataType.PersonaVoiceSample;
    protected override string EnricherName => nameof(PersonaVoiceSampleEnricher);
    protected override bool SupportsManual => false;
    protected override bool SupportsSemantic => true; // Handled by SemanticDataEnricher
}