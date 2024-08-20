using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps;

namespace Questionable;

internal static class ServiceCollectionExtensions
{
    public static void AddTaskFactory<
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)] TFactory>(
        this IServiceCollection serviceCollection)
        where TFactory : class, ITaskFactory
    {
        serviceCollection.AddSingleton<ITaskFactory, TFactory>();
        serviceCollection.AddSingleton<TFactory>();
    }
}
