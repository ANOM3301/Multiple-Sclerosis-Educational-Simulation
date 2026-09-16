using System.Runtime.CompilerServices;

// Allow Runtime (which now includes Application layer) to access internal members
[assembly: InternalsVisibleTo("Convai.Runtime")]
[assembly: InternalsVisibleTo("Convai.Modules.Emotion")]
[assembly: InternalsVisibleTo("Convai.Transport.Native")]
[assembly: InternalsVisibleTo("Convai.Transport.WebGL")]

// Allow test assemblies to access internal members for unit testing
[assembly: InternalsVisibleTo("Convai.Tests.EditMode")]
[assembly: InternalsVisibleTo("Convai.Tests.PlayMode")]
