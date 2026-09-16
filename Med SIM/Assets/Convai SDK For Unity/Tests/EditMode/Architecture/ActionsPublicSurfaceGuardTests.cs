using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Convai.Runtime.Actions;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using NUnit.Framework;
using UnityEngine;

namespace Convai.Tests.EditMode.Architecture
{
    /// <summary>
    ///     Positive public-surface guard for the actions feature (mirrors
    ///     <c>EmbodimentPublicSurfaceGuardTests</c>). Everything public in a package runtime
    ///     assembly is customer API, so adding a public actions type is a deliberate review
    ///     gate: extend the approved list here in the same change, with docs and CHANGELOG.
    /// </summary>
    public sealed class ActionsPublicSurfaceGuardTests
    {
        private static readonly string[] ApprovedRuntimeActionTypes =
        {
            "Convai.Runtime.Actions.ConvaiActionBatchFailurePolicy",
            "Convai.Runtime.Actions.ConvaiActionBatchPolicy",
            "Convai.Runtime.Actions.ConvaiActionConfigDiagnostic",
            "Convai.Runtime.Actions.ConvaiActionConfigDiagnosticSeverity",
            "Convai.Runtime.Actions.ConvaiActionConfigValidator",
            "Convai.Runtime.Actions.ConvaiActionDebugProbe",
            "Convai.Runtime.Actions.ConvaiActionDefinition",
            "Convai.Runtime.Actions.ConvaiActionDispatcher",
            "Convai.Runtime.Actions.ConvaiActionExecutionResult",
            "Convai.Runtime.Actions.ConvaiActionExecutionStatus",
            "Convai.Runtime.Actions.ConvaiActionExecutor`1",
            "Convai.Runtime.Actions.ConvaiActionFailurePolicyOverride",
            "Convai.Runtime.Actions.ConvaiActionInvocation",
            "Convai.Runtime.Actions.ConvaiActionInvocationUnityEvent",
            "Convai.Runtime.Actions.ConvaiActionParameterAttribute",
            "Convai.Runtime.Actions.ConvaiActionParameterDefinition",
            "Convai.Runtime.Actions.ConvaiActionStepReport",
            "Convai.Runtime.Actions.ConvaiActionStepReportUnityEvent",
            "Convai.Runtime.Actions.ConvaiActionTargetRequirement",
            "Convai.Runtime.Actions.ConvaiResolvedActionTarget",
            "Convai.Runtime.Actions.IConvaiActionExecutor",
            "Convai.Runtime.Actions.IConvaiActionRuntimeSource",
            "Convai.Runtime.Actions.LookAtTargetActionExecutor",
            "Convai.Runtime.Actions.UnityEventActionExecutor"
        };

        private static readonly string[] ApprovedDomainActionTypes =
        {
            "Convai.Shared.Types.ConvaiActionCommand",
            "Convai.Shared.Types.ConvaiActionParameterReference",
            "Convai.Shared.Types.ConvaiActionParameterType",
            "Convai.Shared.Types.ConvaiActionParameterValue",
            "Convai.Shared.Types.ConvaiActionTargetKind"
        };

        private static readonly string[] ApprovedSharedUnityActionTypes =
        {
            "Convai.Shared.Actions.ConvaiActionCharacterDefinition",
            "Convai.Shared.Actions.ConvaiActionConfig",
            "Convai.Shared.Actions.ConvaiActionConfigPatch",
            "Convai.Shared.Actions.ConvaiActionObjectDefinition"
        };

        [Test]
        [Category("Architecture")]
        public void RuntimeActionsNamespace_PublicTypes_MatchApprovedList()
        {
            AssertPublicTypesMatch(
                typeof(ConvaiActionDispatcher).Assembly,
                type => type.Namespace != null && type.Namespace.StartsWith("Convai.Runtime.Actions", StringComparison.Ordinal),
                ApprovedRuntimeActionTypes);
        }

        [Test]
        [Category("Architecture")]
        public void DomainActionTypes_PublicTypes_MatchApprovedList()
        {
            AssertPublicTypesMatch(
                typeof(ConvaiActionCommand).Assembly,
                type => type.Namespace == "Convai.Shared.Types" &&
                        type.Name.Contains("Action"),
                ApprovedDomainActionTypes);
        }

        [Test]
        [Category("Architecture")]
        public void SharedUnityActionTypes_PublicTypes_MatchApprovedList()
        {
            AssertPublicTypesMatch(
                typeof(ConvaiActionConfig).Assembly,
                type => type.Namespace == "Convai.Shared.Actions",
                ApprovedSharedUnityActionTypes);
        }

        [Test]
        [Category("Architecture")]
        public void ActionWireFormatHelpers_StayInternal()
        {
            Assembly runtime = typeof(ConvaiActionDispatcher).Assembly;

            foreach (string typeName in new[]
                     {
                         "Convai.Runtime.Actions.ConvaiActionResponseParser",
                         "Convai.Runtime.Actions.ConvaiActionTemplateRenderer"
                     })
            {
                Type type = runtime.GetType(typeName);
                Assert.IsNotNull(type, $"{typeName} not found; update this guard if it moved.");
                Assert.IsFalse(type.IsPublic,
                    $"{typeName} must stay internal: enrichment runs automatically before dispatch and " +
                    "ConvaiActionDefinition.ToActionConfigString() is the public rendering entry point.");
            }
        }

        [Test]
        [Category("Architecture")]
        public void ShippedActionExecutors_UseConvaiActionsComponentMenu()
        {
            var violations = new List<string>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name == null || !(name == "Convai.Runtime" || name.StartsWith("Convai.Modules.", StringComparison.Ordinal)))
                    continue;

                foreach (Type type in SafeGetTypes(assembly))
                {
                    if (!type.IsPublic || type.IsAbstract || !typeof(IConvaiActionExecutor).IsAssignableFrom(type))
                        continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                        continue;

                    var menu = type.GetCustomAttribute<AddComponentMenu>();
                    if (menu == null || !menu.componentMenu.StartsWith("Convai/Actions/", StringComparison.Ordinal))
                        violations.Add($"{type.FullName}: AddComponentMenu '{menu?.componentMenu ?? "<missing>"}'");
                }
            }

            Assert.IsEmpty(violations,
                "Shipped action executors must live under the 'Convai/Actions/' component menu:\n" +
                string.Join(Environment.NewLine, violations));
        }

        private static void AssertPublicTypesMatch(Assembly assembly, Func<Type, bool> scope, string[] approved)
        {
            string[] actual = SafeGetTypes(assembly)
                .Where(type => type.IsPublic && !type.IsNested && scope(type))
                .Select(type => type.FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            string[] expected = approved.OrderBy(name => name, StringComparer.Ordinal).ToArray();

            string[] unexpected = actual.Except(expected).ToArray();
            string[] missing = expected.Except(actual).ToArray();

            Assert.IsEmpty(unexpected,
                "New public actions API detected. Public surface is customer API: if intentional, add the type " +
                "to the approved list here in the same change, with XML docs, ACTIONS.md, and a CHANGELOG entry:\n" +
                string.Join(Environment.NewLine, unexpected));
            Assert.IsEmpty(missing,
                "Approved public actions API is gone. Removals are breaking changes needing a CHANGELOG " +
                "Breaking Changes entry; update the approved list in the same change:\n" +
                string.Join(Environment.NewLine, missing));
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }
    }
}
