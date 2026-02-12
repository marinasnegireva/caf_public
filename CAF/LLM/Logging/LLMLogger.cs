using SharpToken;

namespace CAF.LLM.Logging
{
    public class LLMLogger
    {
        private readonly LLMRequestLogEntity _log;
        private readonly IDbContextFactory<GeneralDbContext> _dbFactory;
        private readonly ILogger<LLMLogger> _logger;

        public LLMLogger(IDbContextFactory<GeneralDbContext> dbFactory, ILogger<LLMLogger> logger, string operation, string model, object request, string provider, int? turnId = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var prompt = request.ExtractPrompt(provider);

            _log = new LLMRequestLogEntity
            {
                Operation = operation,
                Provider = provider,
                Model = model,
                StartTime = DateTime.UtcNow,
                RawRequestJson = request.ToJsonFlat(),
                Prompt = prompt,
                RequestId = Guid.NewGuid().ToString(),
                TurnId = turnId
            };

            using var db = _dbFactory.CreateDbContext();
            db.LLMRequestLogs.Add(_log);
            db.SaveChanges();

            LogRequestStart();
        }

        public void SetPrompt(string? prompt)
        {
            _log.Prompt = prompt;
        }

        public void SetSystemInstruction(string? systemInstruction)
        {
            _log.SystemInstruction = systemInstruction;
        }

        public async Task FinalizeAndSaveAsync(
            HttpResponseMessage response,
            string body,
            CancellationToken ct = default)
        {
            _log.StatusCode = (int)response.StatusCode;
            _log.GeneratedText = body;
            _log.RawResponseJson = body;

            await FinalizeAndPersistAsync(ct);
        }

        public async Task FinalizeWithErrorAsync(
            string body,
            CancellationToken ct = default)
        {
            _log.StatusCode = -1;
            _log.GeneratedText = body;
            _log.RawResponseJson = body;

            await FinalizeAndPersistAsync(ct);
        }

        public async Task FinalizeResponseAsync(
            HttpResponseMessage response,
            string responseContent,
            string content,
            int totalTokens,
            int promptTokens,
            int completionTokens,
            int promptCacheCreationTokens,
            int promptCacheReadTokens,
            int reasoningTokens,
            CancellationToken ct = default)
        {
            _log.StatusCode = (int)response.StatusCode;
            _log.RawResponseJson = responseContent;
            _log.GeneratedText = content;
            _log.TotalTokens = totalTokens;
            _log.InputTokens = promptTokens;
            _log.OutputTokens = completionTokens;
            _log.CachedContentTokenCount = promptCacheReadTokens; // Only cache reads, not creation
            _log.ThinkingTokens = reasoningTokens;

            _log.TotalCost = CalculateTotalCost(promptTokens, promptCacheCreationTokens, promptCacheReadTokens, completionTokens);

            await FinalizeAndPersistAsync(ct, logTokenDetails: true);
        }

        private void FinalizeTiming()
        {
            _log.EndTime = DateTime.UtcNow;
            _log.DurationMs = (int)(_log.EndTime - _log.StartTime).Value.TotalMilliseconds;
        }

        private async Task FinalizeAndPersistAsync(CancellationToken ct, bool logTokenDetails = false)
        {
            FinalizeTiming();

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.LLMRequestLogs.Update(_log);
            await db.SaveChangesAsync(ct);

            if (logTokenDetails)
            {
                LogRequestCompletedWithTokens();
            }
            else
            {
                LogRequestCompleted();
            }
        }

        private void LogRequestStart()
        {
            _logger.LogInformation(
                "LLM request started {RequestId} {Provider} {Model} {Operation} TurnId:{TurnId}",
                _log.RequestId,
                _log.Provider,
                _log.Model,
                _log.Operation,
                _log.TurnId);
        }

        private void LogRequestCompleted()
        {
            _logger.LogInformation(
                "LLM request completed {RequestId} {Provider} {Model} {Operation} {StatusCode} {DurationMs}ms",
                _log.RequestId,
                _log.Provider,
                _log.Model,
                _log.Operation,
                _log.StatusCode,
                _log.DurationMs);
        }

        private void LogRequestCompletedWithTokens()
        {
            _logger.LogInformation(
                "LLM request completed {RequestId} {Provider} {Model} {Operation} {StatusCode} {DurationMs}ms {TotalTokens}t {InputTokens}in {OutputTokens}out {CachedTokens}cached {ThinkingTokens}thinking ${TotalCost}",
                _log.RequestId,
                _log.Provider,
                _log.Model,
                _log.Operation,
                _log.StatusCode,
                _log.DurationMs,
                _log.TotalTokens,
                _log.InputTokens,
                _log.OutputTokens,
                _log.CachedContentTokenCount,
                _log.ThinkingTokens,
                _log.TotalCost);
        }

        private decimal CalculateTotalCost(
            int regularInputTokens, 
            int cacheCreationTokens, 
            int cacheReadTokens, 
            int outputTokens)
        {
            // Claude pricing: regular input, cache creation (1.25x), cache read (0.1x), output
            var regularInputPrice = EstimatePriceOfTokens(regularInputTokens, isInput: true, isCached: false, cacheCreation: false, _log.Model);
            var cacheCreationPrice = EstimatePriceOfTokens(cacheCreationTokens, isInput: true, isCached: false, cacheCreation: true, _log.Model);
            var cacheReadPrice = EstimatePriceOfTokens(cacheReadTokens, isInput: true, isCached: true, cacheCreation: false, _log.Model);
            var outputPrice = EstimatePriceOfTokens(outputTokens, isInput: false, isCached: false, cacheCreation: false, _log.Model);

            return (decimal)(regularInputPrice + cacheCreationPrice + cacheReadPrice + outputPrice);
        }

        private static readonly Dictionary<string, (double input, double cacheCreation, double cacheRead, double output)> ModelPrices = new(StringComparer.OrdinalIgnoreCase)
        {
            // model: (input, cache creation, cache read, output) - prices per million tokens
            // Gemini: cache read = 0.1x input, no separate cache creation cost
            ["gemini-2.5-pro"] = (1.25, 1.25, 0.125, 10.00),
            ["gemini-2.5-flash"] = (0.3, 0.3, 0.03, 2.50),
            ["gemini-3-flash-preview"] = (0.50, 0.50, 0.05, 3.00),
            ["gemini-2.5-flash-lite"] = (0.1, 0.1, 0.01, 0.4),
            ["gemini-3-pro-preview"] = (2, 2, 0.4, 12.00),
            // Claude: cache creation = 1.25x input ($15 → $18.75), cache read = 0.1x input ($15 → $1.50)
            ["claude-opus-4-6"] = (15, 18.75, 1.50, 75),
            ["claude-opus-4-5"] = (15, 18.75, 1.50, 75),
            ["claude-sonnet-4-5"] = (3, 3.75, 0.30, 15),
            // DeepSeek: cache read pricing, assume no separate cache creation
            ["deepseek-chat"] = (0.27, 0.27, 0.07, 1.10),
            ["deepseek-reasoner"] = (0.55, 0.55, 0.14, 2.19)
        };

        public static int EstimateTokens(string text, string model)
        {
            var encoding = GptEncoding.GetEncodingForModel(model);
            return encoding.CountTokens(text);   // exact token count
        }

        /// <summary>
        /// Estimates the price for a given number of tokens for a model (per million tokens).
        /// </summary>
        /// <param name="tokens">Number of tokens</param>
        /// <param name="isInput">True for input, false for output</param>
        /// <param name="isCached">True for cache read tokens</param>
        /// <param name="cacheCreation">True for cache creation tokens (Claude only)</param>
        /// <param name="model">Model name</param>
        /// <returns>Estimated price in USD</returns>
        public static double EstimatePriceOfTokens(int tokens, bool isInput, bool isCached, bool cacheCreation, string model)
        {
            if (!ModelPrices.TryGetValue(model, out var prices))
                throw new ArgumentException($"Unknown model: {model}", nameof(model));

            var pricePerMillion = (isInput, isCached, cacheCreation) switch
            {
                (true, true, _) => prices.cacheRead,      // Cache read (0.1x)
                (true, false, true) => prices.cacheCreation, // Cache creation (1.25x for Claude)
                (true, false, false) => prices.input,      // Regular input
                (false, _, _) => prices.output,            // Output
            };

            if (pricePerMillion <= 0)
                throw new InvalidOperationException($"No price available for this operation for model: {model}");

            var price = tokens / 1000000.0 * pricePerMillion;

            return Math.Round(price, 6, MidpointRounding.AwayFromZero);
        }
    }
}