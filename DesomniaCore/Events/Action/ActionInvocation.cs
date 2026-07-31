using System.Reflection;

namespace MadWizard.Desomnia.Events
{
    internal readonly struct ActionInvocation(EventMetaObject actor, MethodInfo method, object?[] parameters)
    {
        internal EventMetaObject Actor { get; init; } = actor;

        internal MethodInfo Method { get; init; } = method;

        internal object?[] Parameters { get; init; } = parameters;

        public Task InvokeAsync()
        {
            if (Method.Invoke(Actor, Parameters) is Task task)
            {
                return task;
            }
            else
            {
                return Task.CompletedTask;
            }
        }
    }
}
