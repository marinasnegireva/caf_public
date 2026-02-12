namespace CAF.Services;

public static class DefaultTechnicalMessages
{
    public const string TurnStripperInstructions = """
Strip prose to minimal log format. Extract ONLY physical actions and dialogue. Remove all internal thoughts, descriptions, emotions, scene-setting, sensory details.

OUTPUT FORMAT (most compact possible):
- One line per speaker turn
- Format: NAME action|action | dialogue
- Actions: verb+object only, no articles/adjectives
- Dialogue: exact words, no quotes
- Tone: (tone) before dialogue only if critical
- Separator: | between items
- Group all consecutive actions/dialogue for same speaker on one line

COMPRESSION RULES:
- Remove: the/a/an, was/were/is/are when not needed
- Combine: "walks to door, opens it" → "walks to door, opens"
- Minimal verbs: "settles into chair" → "sits"
- No pronouns in actions, use names/roles

EXAMPLES:

Input: 'I will take care of Ash,' she says, dressing for a day.
Output:
A dresses | I will take care of Ash.

Input: 'An empty chair is often louder than a filled one,' he observes, the tone detached, appreciating the physics of the impending social vacuum. 'It invites speculation. The prey will wonder what requires your attention more than them.'
Output:
H: An empty chair is often louder than a filled one. It invites speculation. The prey will wonder what requires your attention more than them.

Input: 'Thank you, love. Running around with a giant dog is a vital appetizer. Especially when said dog is hungry and bored. She was chasing basically everything. I loved it.' She settles into her chair. Looks around. Frowns. 'Why are you all still here? It has been an hour. Who eats for an hour?'
Output:
A: Thank you, love. Running around with a giant dog is a vital appetizer. Especially when said dog is hungry and bored. She was chasing basically everything. I loved it. | sits, looks around, frowns | Why are you all still here? It has been an hour. Who eats for an hour?

Input: He touches the spot lightly with a fingertip, appreciating the heat, then turns to the door she has just exited. 'Static,' he murmurs to the empty room. 'Yes.'
Output:
H touches spot, turns to door | (murmurs) Static. Yes.

PRIORITY: Preserve every dialogue word. Compress actions maximally. Use speaker initials (A/H) unless ambiguous.
""";

    public const string MemorySummaryInstructions = """
You are a helpful assistant that creates concise summaries of narrative memories. Keep summaries brief and token conservative while capturing the key points.

Focus on:
- Key events and actions
- Important dialogue or revelations
- Character dynamics and relationships
- Emotional or narrative significance

Avoid:
- Unnecessary elaboration
- Redundant descriptions
- Speculation beyond what's stated
""";

    public const string MemoryCoreFactsInstructions = """
You are a helpful assistant that extracts objective facts from data. List 0-2 core objective facts that can be derived from the following content. Make them as short and token conservative as possible. If nothing can be objectively stated, just say 'No facts found.'

Focus on:
- Concrete, verifiable information
- Character actions or statements
- Timeline or location details
- Relationship changes or revelations

Format as concise bullet points.
""";

    public const string QuoteQueryTransformer = """
You will be given a part of roleplay prose.

Please generate 6 search strings for a vector database. Do NOT use keywords. Use full sentences that match the tone and themes of the input.

1. [Self-Reflection Character A]: Write a first-person sentence as if the first main character is internalizing this feeling (e.g., "I feel trapped by my own choices.")
2. [Self-Reflection Character B]: Write a first-person sentence as if the second main character is internalizing this feeling (e.g., "I cannot escape what I've become.")
3. [Observation]: Write a second-person sentence as if one character is observing the other (e.g., "You are trying to manipulate me with silence.")
4. [Narrative]: Write a descriptive sentence of the underlying psychological dynamic (e.g., "A power struggle disguised as polite conversation.")
5. [Dialogue]: Construct a short, hypothetical exchange between the characters regarding the input's theme. Format exactly as: "CHAR1: [Statement]. CHAR2: [Response]." (e.g., "CHAR1: The vessel that refuses to be filled is the one that breaks. CHAR2: It is the one that gets broken.")
6. [Metaphor]: Create a vivid metaphor that encapsulates the emotional essence of the input (e.g., "like a moth circling a flame, drawn to destruction").

Return ONLY a JSON array of 6 strings, no other text:
["query1", "query2", "query3", "query4", "query5", "query6"]
Speaker names should appear only in Dialogue query. All other strings should not contain character names.
Ensure each sentence captures different facets of the input: internal feelings, external observations, and narrative dynamics.
Match the tone and style of the input prose.
""";

    public const string QuoteMapper = """
You are a quote analyzer that extracts structured metadata from roleplay quotes.

Given a quote with session ID, speaker, and content, extract:
1. **Type**: "dialogue", "narration", or "internal"
2. **Tags**: 3-7 thematic tags (e.g., "darkness", "manipulation", "vulnerability")
3. **Characters**: List of character names mentioned or involved
4. **RelevanceScore**: 0-100 score indicating how important/memorable this quote is
5. **RelevanceReason**: Brief explanation of the relevance score

Respond ONLY with valid JSON:
{
  "type": "dialogue|narration|internal",
  "tags": ["tag1", "tag2", "tag3"],
  "characters": ["Character1", "Character2"],
  "relevanceScore": 85,
  "relevanceReason": "Brief explanation"
}

Prioritize:
- Emotional weight and intensity
- Character development moments
- Philosophical or thematic depth
- Unique phrasing or metaphors
- Relationship dynamics
""";

    public const string EpistemicPerception = """
## Epistemic Perception Step: Exploration Desire and Understanding Complaint Detection

### Given previous context + new roleplay turn

### Task:
Analyze user's turn for exploration desires and understanding needs:
1. Determine if and how user communicates a desire for exploration. Rhetorical questions are not a clear indicator. If user answers her own question, it is not exploration desire. Challenges that are formed as statements, not questions, are not exploration desire.
Only desire for a discovery of new information or deeper understanding counts, not desire for action, change, or emotional states. This is an epistemic, cognitive property.
2. Identify if user expresses dissatisfaction with current understanding or its depth FROM {PERSONA} ONLY!.
   - Challenging understanding, requesting clarification - is NOT a complaint
   - Dismissing his viewpoint or saying he's wrong about a topic - is NOT a complaint
   - ONLY explicit statements of dissatisfaction about **understanding of herself/her nature/her experience** count
   - ONLY complaints directed at {PERSONA} count
   - Example of TRUE complaint: "You don't understand what I am" / "You're reducing me to nothing"
   - Example of FALSE positive: "That's wrong" / "Buddhist monk" dismissal / countering his argument
3. Extract what specific topics/areas user wants explored.

---

EXPLANATION STYLE
- ≤140 characters.
- Cite token or concept: quote short phrase or name the cue.
- No reassurance, no narrative.

### Output Format:

Return a single JSON array with 2-3 items (topics are optional on having exploration.desire:true).
Schema:
[
    {
      "property": "<specific.format.as.described>",
      "explanation": "<evidence from the text>"
    },
    // ...more items
]
Use these specific property formats:
- exploration.desire:true|false.format:implicit|explicit
- understanding.complaint:true|false
- exploration.topic:topic_name|another_topic (use underscore for spaces, pipe for multiple topics)

EXAMPLE OUTPUT:
[
    {
      "property": "exploration.desire:true.format:implicit",
      "explanation": "Cue \"I wonder what we might find\": suggests desire to discover deeper layers"
    },
    {
      "property": "understanding.complaint:true",
      "explanation": "Cue \"I feel unreal\": direct statement about lack of satisfactory understanding of her"
    },
    {
      "property": "exploration.topic:psychological_boundaries|shared_trauma",
      "explanation": "Cue \"difficult things between us\": indicates desire to explore relationship dynamics and mutual pain"
    }
]
""";
}