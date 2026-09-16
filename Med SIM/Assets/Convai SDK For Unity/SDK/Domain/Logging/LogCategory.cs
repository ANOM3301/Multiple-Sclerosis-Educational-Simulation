namespace Convai.Domain.Logging
{
    /// <summary>
    ///     Categories for filtering logs by subsystem.
    ///     Each category can have its own minimum log level.
    /// </summary>
    public enum LogCategory
    {
        /// <summary>General SDK operations.</summary>
        SDK = 0,

        /// <summary>Character/NPC related logs.</summary>
        Character,

        /// <summary>Audio system logs.</summary>
        Audio,

        /// <summary>UI component logs.</summary>
        UI,

        /// <summary>REST API communication logs.</summary>
        REST,

        /// <summary>Transport/connection logs.</summary>
        Transport,

        /// <summary>Event system logs.</summary>
        Events,

        /// <summary>Player/user related logs.</summary>
        Player,

        /// <summary>Editor-only logs.</summary>
        Editor,

        /// <summary>Vision capture and video publishing logs.</summary>
        Vision,

        /// <summary>Bootstrap and initialization logs.</summary>
        Bootstrap,

        /// <summary>Transcript processing and routing logs.</summary>
        Transcript,

        /// <summary>Narrative design and story system logs.</summary>
        Narrative,

        /// <summary>Lip sync processing and blendshape playback logs.</summary>
        LipSync
    }
}
