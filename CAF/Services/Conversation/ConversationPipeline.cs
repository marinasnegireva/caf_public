namespace CAF.Services.Conversation;

/// <summary>
/// Main orchestrator for conversation processing pipeline.
/// Responsibilities:
/// 1. Manages conversation flow (session, turns)
/// 2. Builds initial conversation state via ConversationStateBuilder
/// 3. Triggers enrichment processes via ConversationEnrichmentOrchestrator
/// 4. Builds LLM request via ConversationRequestBuilder
/// 5. Delegates to LLM provider strategy for execution
/// 6. Updates turn with response
/// </summary>
public class ConversationPipeline(
ISessionService sessionService,
ITurnService turnService,
IConversationContextBuilder stateBuilder,
IConversationEnrichmentOrchestrator enrichmentOrchestrator,
IConversationRequestBuilder requestBuilder,
ILLMProviderFactory llmProviderFactory,
ILogger<ConversationPipeline> logger) : IConversationPipeline
{
    public async Task<Turn> ProcessInputAsync(string input, CancellationToken cancellationToken = default)
    {
        var (state, turn) = await BuildRequestAsync(input, cancellationToken);

        try
        {
            // Step 7: Execute the LLM request using the selected provider
            var provider = await llmProviderFactory.GetProviderAsync(state.CancellationToken);
            var (success, result) = await provider.ExecuteAsync(state, state.CancellationToken);

            // Step 8: Update turn with LLM response and mark all used context data
            state.CurrentTurn.Response = result;
            turn = await turnService.UpdateTurnAsync(
                state,
                accepted: !string.IsNullOrWhiteSpace(result) && success,
                cancellationToken: state.CancellationToken
            );

            state.CurrentTurn = turn;

            return turn;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing input for turn {TurnId}", turn.Id);

            try
            {
                var errorMessage = $"Error: {ex.Message}";
                turn = await turnService.UpdateTurnAsync(turn.Id, response: errorMessage, accepted: false);
                return turn;
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to persist error response for turn {TurnId}", turn.Id);
                throw;
            }
        }
    }

    public async Task<(ConversationState State, Turn Turn)> BuildRequestAsync(string input, CancellationToken cancellationToken = default)
    {
        // Step 1: Find or create active session
        var session = await sessionService.GetActiveSessionAsync() ?? throw new InvalidOperationException("No active session found. Please create and activate a session first.");

        // Step 2: Create new turn and save to database
        var turn = await turnService.CreateTurnAsync(session.Id, input);

        // Step 3: Build initial conversation state (history, persona, contexts, memories)
        var state = await stateBuilder.BuildContextAsync(turn, session);
        state.CurrentTurn = turn;

        // Ensure the pipeline can be cancelled via the supplied token
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(state.CancellationToken, cancellationToken);
        state.CancellationToken = linkedCts.Token;

        logger.LogInformation("Built conversation state for turn {TurnId} - User: {UserName}, Persona: {PersonaName}",
            turn.Id, state.UserName, state.PersonaName);

        // Step 4: Run all enrichment processes asynchronously (perceptions, context data, flags, etc.)
        await enrichmentOrchestrator.EnrichAsync(state, state.CancellationToken);

        logger.LogInformation("Enrichment complete for turn {TurnId}: {PerceptionCount} perceptions, {ContextDataCount} context data items",
            turn.Id, state.Perceptions.Count, state.GetAllContextData().Count());

        // Step 5: Get the appropriate LLM provider strategy
        var provider = await llmProviderFactory.GetProviderAsync(state.CancellationToken);

        // Step 6: Build the appropriate LLM request based on provider type
        if (provider.ProviderName.Equals(ConversationConstants.ClaudeProvider, StringComparison.OrdinalIgnoreCase))
        {
            await requestBuilder.BuildClaudeRequestAsync(state, state.CancellationToken);
        }
        else
        {
            await requestBuilder.BuildGeminiRequestAsync(state, state.CancellationToken);
        }


        return (state, turn);
    }
}