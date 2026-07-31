using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using System.Runtime.CompilerServices;

namespace MadWizard.Desomnia.Events
{
    /// <summary>
    /// The root catch-all of the action-resolution walk (§6.3): consulted exactly once,
    /// at the origin node, after the tree walk left an action or error unhandled.
    /// Implemented by <see cref="ActionManager"/>; phase 5 replaces the actor iteration
    /// with the ActionProvider abstraction.
    /// </summary>
    public interface IEventSystemRoot
    {
        Task<bool> TryHandleEventAction(Event @event, EventAction action);

        bool HandleActionError(ActionError error);
    }

    /// <summary>
    /// The engine hub: attachment marks container-created instances and wires their
    /// access to the root — the guarantee that makes action reachability independent
    /// of tree membership (§6.3/§7.2).
    /// </summary>
    public static class EventSystem
    {
        private static readonly ConditionalWeakTable<EventMetaObject, object> _attached = [];

        /// <summary>Attaches an instance to the event system. Called by the resolve
        /// middleware for every container-created EventMetaObject; direct use is
        /// reserved for test harnesses. Idempotent.</summary>
        public static void Attach(EventMetaObject instance) => Attach(instance, null);

        public static void Attach(EventMetaObject instance, Func<IEventSystemRoot?>? root)
        {
            ArgumentNullException.ThrowIfNull(instance);

            _attached.AddOrUpdate(instance, null!);

            if (root != null)
                instance.RootAccessor = root;
        }

        public static bool IsAttached(EventMetaObject instance) => _attached.TryGetValue(instance, out _);
    }

    /// <summary>
    /// Container-level service middleware: attaches every resolved
    /// <see cref="EventMetaObject"/> to the engine at activation — including instances
    /// from inline registrations in child lifetime scopes, which per-registration
    /// callbacks would miss. Runs before OnActivated callbacks (§7.1 ordering contract).
    /// </summary>
    public sealed class EventSystemMiddlewareSource : IServiceMiddlewareSource
    {
        public void ProvideMiddleware(Service service, IComponentRegistryServices availableServices, IResolvePipelineBuilder pipelineBuilder)
        {
            pipelineBuilder.Use(AttachMiddleware.Instance, MiddlewareInsertionMode.EndOfPhase);
        }

        private sealed class AttachMiddleware : IResolveMiddleware
        {
            public static readonly AttachMiddleware Instance = new();

            // service pipelines end at ServicePipelineEnd; the downstream registration
            // pipeline (incl. activation and OnActivating) runs inside next(), so the
            // instance is available afterwards — before OnActivated callbacks (§7.1)
            public PipelinePhase Phase => PipelinePhase.ServicePipelineEnd;

            public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
            {
                next(context);

                if (context.Instance is EventMetaObject instance)
                {
                    // root fallback resolves lazily at first dispatch (the SingleInstance
                    // ActionManager lives in the root scope; resolving it here, mid-resolve,
                    // could recurse through its own InjectableProviders collection)
                    var rootScope = context.ActivationScope.RootLifetimeScope;

                    EventSystem.Attach(instance, CreateRootAccessor(rootScope));
                }                                             // idempotent — the Registered event
            }                                                 // re-fires per child scope (§7.1)

            private static Func<IEventSystemRoot?> CreateRootAccessor(Autofac.ILifetimeScope scope)
            {
                IEventSystemRoot? cached = null;

                return () =>
                {
                    if (cached != null)
                        return cached;

                    try
                    {
                        Autofac.ResolutionExtensions.TryResolve(scope, out cached);
                    }
                    catch (ObjectDisposedException)
                    {
                        // application shutdown — no root to fall back to
                    }
                    catch (Autofac.Core.DependencyResolutionException)
                    {
                        // a broken/mid-activation root registration must degrade to
                        // "no root", never replace the error being routed
                    }

                    return cached;
                };
            }

            public override string ToString() => nameof(EventSystemMiddlewareSource);
        }
    }
}
