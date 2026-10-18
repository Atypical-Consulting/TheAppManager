namespace TheAppManager;

/// <summary>
/// Excludes a module class from <see cref="AppModuleCollection.AddFromAssembly"/> and
/// <see cref="AppModuleCollection.AddFromAssemblyOf{TAnchor}"/> scans. The module can still be added explicitly.
/// The attribute applies to the exact class only; subclasses stay discoverable.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ExcludeFromDiscoveryAttribute : Attribute;
