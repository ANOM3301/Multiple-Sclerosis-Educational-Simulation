namespace Convai.Domain.Emotion
{
    /// <summary>
    ///     Client-controlled emotion detection mode. Resolved at connect time to decide whether
    ///     and how the backend runs emotion detection for a character.
    /// </summary>
    /// <remarks>
    ///     The mode is authoritative: it fully determines the <c>emotion_config</c> sent on connect,
    ///     overriding any backend/playground emotion setting. Missing client-side mode is treated as
    ///     <see cref="Off" />.
    /// </remarks>
    public enum EmotionDetectionMode
    {
        /// <summary>
        ///     Emotion detection disabled. No <c>emotion_config</c> is sent on connect, so the
        ///     backend runs no detection and emits no <c>bot-emotion</c> frames.
        /// </summary>
        Off = 0,

        /// <summary>
        ///     LLM-based detection (backend Gemini provider). Context-aware but requires a valid
        ///     <c>GEMINI_API_KEY</c> server-side; without it the backend falls back to neutral.
        /// </summary>
        Llm = 1,

        /// <summary>
        ///     NRCLex keyword-based detection. Fast and deterministic with no API key, but less
        ///     context-aware. Skips turns shorter than the configured minimum word count.
        /// </summary>
        Nrclex = 2
    }
}
