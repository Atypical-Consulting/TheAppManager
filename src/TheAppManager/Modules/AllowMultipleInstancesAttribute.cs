namespace TheAppManager;

/// <summary>
/// Opts a module class out of type identity: an <see cref="AppModuleCollection"/> may then hold
/// several instances of it. Without this attribute, registering the same module type twice makes
/// <see cref="AppModuleCollection.Resolve"/> throw. The attribute applies to the exact class only;
/// subclasses do not inherit it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowMultipleInstancesAttribute : Attribute;
