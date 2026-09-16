using System.Runtime.CompilerServices;

// Modules and test assemblies need access to internal helpers declared in this contract layer.
[assembly: InternalsVisibleTo("Convai.Runtime")]
[assembly: InternalsVisibleTo("Convai.Modules.ConversationFlow")]
[assembly: InternalsVisibleTo("Convai.Modules.Attention")]
[assembly: InternalsVisibleTo("Convai.Modules.Emotion")]
[assembly: InternalsVisibleTo("Convai.Modules.Gaze")]
[assembly: InternalsVisibleTo("Convai.Modules.FacialAnimation")]
[assembly: InternalsVisibleTo("Convai.Tests.EditMode")]
[assembly: InternalsVisibleTo("Convai.Tests.PlayMode")]
