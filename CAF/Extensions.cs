namespace CAF
{
    public static partial class Extensions
    {

        // Compiled regex patterns to avoid recompilation on each call
        [GeneratedRegex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled)]
        private static partial Regex UnicodeEscapeRegex();

        [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Compiled)]
        private static partial Regex MarkdownBoldRegex();

        [GeneratedRegex(@"\*(.+?)\*", RegexOptions.Compiled)]
        private static partial Regex MarkdownItalicRegex();

        [GeneratedRegex(@"`(.+?)`", RegexOptions.Compiled)]
        private static partial Regex MarkdownCodeRegex();

        [GeneratedRegex(@"\[(.+?)\]\(.+?\)", RegexOptions.Compiled)]
        private static partial Regex MarkdownLinkRegex();

        [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
        private static partial Regex WhitespaceRegex();

        // Custom encoder that preserves all Unicode characters
        private static readonly JavaScriptEncoder CustomEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static JsonSerializerOptions settingsFlat = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        Encoder = CustomEncoder,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private static JsonSerializerOptions settings = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = CustomEncoder,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    // Separate settings for deserialization - no naming policy, just case-insensitive
    private static JsonSerializerOptions deserializerSettings = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = CustomEncoder,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public static readonly JsonSerializerOptions GeminiOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public static readonly JsonSerializerOptions ClaudeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Encoder = CustomEncoder,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

        public static string ToJsonFlat(this object input)
        {
            try
            {
                var json = JsonSerializer.Serialize(input, settingsFlat);

                // Handle any nested JSON strings that might still have escape sequences
                return json;
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"JSON serialization error: {ex.Message.Replace("\"", "\\\"")}\"}}";
            }
        }

        public static string ToJson(this object input)
        {
            try
            {
                return JsonSerializer.Serialize(input, settings);
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"JSON serialization error: {ex.Message.Replace("\"", "\\\"")}\"}}";
            }
        }

        public static string ExtractPrompt(this object request, string provider)
        {
            var prompt = string.Empty;
            try
            {
                if (request != null)
                {
                    if (provider == "DeepSeek")
                    {
                        var messages = (request as dynamic)?.messages;
                        if (messages != null)
                        {
                            foreach (var msg in messages)
                            {
                                if (msg.role == "user")
                                {
                                    prompt = msg.content ?? string.Empty;
                                }
                            }
                        }
                    }
                    else if (provider == "Gemini")
                    {
                        prompt = (request as GeminiRequest)?.Contents.LastOrDefault(m => m.Role == "user")?.Parts?.First().Text ?? string.Empty;
                    }
                    else if (provider == "ZAI")
                    {
                        var messages = (request as dynamic)?.Messages;
                        if (messages != null)
                        {
                            foreach (var msg in messages)
                            {
                                if (msg.Role == "user")
                                {
                                    prompt = msg.Content ?? string.Empty;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting prompt from {provider} request.", ex);
                throw;
            }
            return prompt;
        }

        /// <summary>
        /// Flattens markdown formatting to plain text
        /// </summary>
        public static string FlattenMarkdownToPlainString(this string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            // Remove markdown bold
            var result = MyRegex().Replace(markdown, "$1");
            // Remove markdown italic
            result = Regex.Replace(result, @"\*(.+?)\*", "$1");
            // Remove markdown code
            result = Regex.Replace(result, @"`(.+?)`", "$1");
            // Remove links [text](url)
            result = Regex.Replace(result, @"\[(.+?)\]\(.+?\)", "$1");
            // Normalize whitespace
            result = Regex.Replace(result, @"\s+", " ");

            return result.Trim();
        }

        /// <summary>
        /// Formats Quote as [sXX] S: (nonverbal) Line
        /// Where XX is session number (optional, omitted if not present), S is first character of Speaker (or empty if Multiple),
        /// nonverbal is optional behavior, and Line is the content
        /// </summary>
        public static string FormatAsQuote(this ContextData quote)
        {
            var plainText = quote.Content.FlattenMarkdownToPlainString();
            var nonverbal = quote.NonverbalBehavior?.FlattenMarkdownToPlainString();

            // Session prefix is optional - omit entirely if not present
            var sessionPrefix = quote.SourceSessionId.HasValue ? $"[s{quote.SourceSessionId}] " : "";

            // Build speaker part (empty if Multiple)
            var speakerPart = string.IsNullOrWhiteSpace(quote.Speaker) ||
                             quote.Speaker.Equals("Multiple", StringComparison.OrdinalIgnoreCase)
                ? ""
                : $"{quote.Speaker[0]}: ";

            // Build nonverbal part (only if present)
            var nonverbalPart = string.IsNullOrWhiteSpace(nonverbal) ? "" : $"({nonverbal}) ";

            return $"{sessionPrefix}{speakerPart}{nonverbalPart}{plainText}";
        }

        /// <summary>
        /// Formats PersonaVoiceSample as [sXX] S: (nonverbal) Line
        /// Where XX is session number (or empty if not from a session), S is first character of Speaker (or empty if Multiple),
        /// nonverbal is optional behavior, and Line is the content
        /// </summary>
        public static string FormatAsVoiceSampleQuote(this ContextData quote)
        {
            var plainText = quote.Content.FlattenMarkdownToPlainString();
            var nonverbal = quote.NonverbalBehavior?.FlattenMarkdownToPlainString();

            // Session prefix (optional for voice samples)
            var sessionPrefix = quote.SourceSessionId.HasValue ? $"[s{quote.SourceSessionId}] " : "";

            // Build speaker part (empty if Multiple or missing)
            var speakerPart = string.IsNullOrWhiteSpace(quote.Speaker) ||
                             quote.Speaker.Equals("Multiple", StringComparison.OrdinalIgnoreCase) ||
                             quote.Speaker.Equals("Speaker", StringComparison.OrdinalIgnoreCase)
                ? ""
                : $"{quote.Speaker[0]}: ";

            // Build nonverbal part (only if present)
            var nonverbalPart = string.IsNullOrWhiteSpace(nonverbal) ? "" : $"({nonverbal}) ";

            return $"{sessionPrefix}{speakerPart}{nonverbalPart}{plainText}";
        }

        /// <summary>
        /// Gets an element at the specified index from a ConcurrentBag (for testing/debugging purposes).
        /// Note: ConcurrentBag does not guarantee ordering, so use with caution.
        /// </summary>
        public static T ElementAt<T>(this ConcurrentBag<T> bag, int index)
        {
            return bag.ToArray()[index];
        }

        [GeneratedRegex(@"\*\*(.+?)\*\*")]
        private static partial Regex MyRegex();
    }
}