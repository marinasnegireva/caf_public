using CAF.Services.Conversation;

namespace CAF.Interfaces;

public interface IConversationPipeline
{
    Task<Turn> ProcessInputAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the conversation state and LLM request without executing it.
    /// Used for debugging and testing the pipeline.
    /// </summary>
    Task<(ConversationState State, Turn Turn)> BuildRequestAsync(string input, CancellationToken cancellationToken = default);
}