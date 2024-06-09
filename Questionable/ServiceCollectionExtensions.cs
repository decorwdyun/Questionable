using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps;

namespace Questionable;

internal static class ServiceCollectionExtensions
{
    public static void AddTaskWithFactory<
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TFactory,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask>(
        this IServiceCollection serviceCollection)
        where TFactory : class, ITaskFactory
        where TTask : class, ITask
    {
        serviceCollection.AddSingleton<ITaskFactory, TFactory>();
        serviceCollection.AddTransient<TTask>();
    }

    public static void AddTaskWithFactory<
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TFactory,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask1,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask2>(
        this IServiceCollection serviceCollection)
        where TFactory : class, ITaskFactory
        where TTask1 : class, ITask
        where TTask2 : class, ITask
    {
        serviceCollection.AddSingleton<ITaskFactory, TFactory>();
        serviceCollection.AddTransient<TTask1>();
        serviceCollection.AddTransient<TTask2>();
    }

    public static void AddTaskWithFactory<
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TFactory,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask1,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask2,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask3>(
        this IServiceCollection serviceCollection)
        where TFactory : class, ITaskFactory
        where TTask1 : class, ITask
        where TTask2 : class, ITask
        where TTask3 : class, ITask
    {
        serviceCollection.AddSingleton<ITaskFactory, TFactory>();
        serviceCollection.AddTransient<TTask1>();
        serviceCollection.AddTransient<TTask2>();
        serviceCollection.AddTransient<TTask3>();
    }

    public static void AddTaskWithFactory<
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TFactory,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask1,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask2,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask3,
        [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
        TTask4>(
        this IServiceCollection serviceCollection)
        where TFactory : class, ITaskFactory
        where TTask1 : class, ITask
        where TTask2 : class, ITask
        where TTask3 : class, ITask
        where TTask4 : class, ITask
    {
        serviceCollection.AddSingleton<ITaskFactory, TFactory>();
        serviceCollection.AddTransient<TTask1>();
        serviceCollection.AddTransient<TTask2>();
        serviceCollection.AddTransient<TTask3>();
        serviceCollection.AddTransient<TTask4>();
    }
}
