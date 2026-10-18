using System.Reflection;

namespace TheAppManager.Tests
{
    /// <summary>An <see cref="Assembly"/> whose type list is fully controlled by the test.</summary>
    internal sealed class StubAssembly(params Type[] types) : Assembly
    {
        public override string FullName => "Stub.Assembly";

        public override Type[] GetTypes() => types;
    }

    public class ModuleDiscoveryTests
    {
        [Fact]
        public void DiscoverModules_WhenSomeTypesFailToLoad_ReturnsLoadableModules()
        {
            var modules = ModuleDiscovery.DiscoverModules(new PartiallyLoadableAssembly());

            modules.ShouldHaveSingleItem().ShouldBeOfType<TestDiscoverableModule>();
        }

        private sealed class PartiallyLoadableAssembly : Assembly
        {
            public override Type[] GetTypes() =>
                throw new ReflectionTypeLoadException([typeof(TestDiscoverableModule), null], [null, new FileNotFoundException()]);
        }

        [Fact]
        public void DiscoverModules_KeepsOnlyReachableConcreteModules()
        {
            var modules = ModuleDiscovery.DiscoverModules(new StubAssembly(Fixtures.All));

            modules.Select(m => m.GetType()).ShouldBe(
                [typeof(Fixtures.ProtectedInternalNested), typeof(InternalModule), typeof(PublicModule)],
                ignoreOrder: true);
        }

        [Fact]
        public void DiscoverModules_DoesNotRunConstructorsOfFilteredTypes()
        {
            Fixtures.FilteredConstructions = 0;

            ModuleDiscovery.DiscoverModules(new StubAssembly(Fixtures.All));

            Fixtures.FilteredConstructions.ShouldBe(0);
        }

        [Fact]
        public void DiscoverModules_SortsByFullName()
        {
            var modules = ModuleDiscovery.DiscoverModules(new StubAssembly(typeof(B.Zeta.Alpha), typeof(A.Zeta.Beta)));

            modules.Select(m => m.GetType()).ShouldBe([typeof(A.Zeta.Beta), typeof(B.Zeta.Alpha)]);
        }

        [Fact]
        public void DiscoverModules_SubclassOfExcludedModule_IsDiscoverable()
        {
            var modules = ModuleDiscovery.DiscoverModules(new StubAssembly(typeof(ExcludedModule), typeof(ExcludedChild)));

            modules.ShouldHaveSingleItem().ShouldBeOfType<ExcludedChild>();
        }

        [Fact]
        public void DiscoverModules_ClosedGenericSubclass_IsDiscovered()
        {
            var modules = ModuleDiscovery.DiscoverModules(new StubAssembly(typeof(Fixtures.GenericModule<>), typeof(Fixtures.IntModule)));

            modules.ShouldHaveSingleItem().ShouldBeOfType<Fixtures.IntModule>();
        }

        [Fact]
        public void DiscoverModules_ThrowsOnNullAssembly()
        {
            Should.Throw<ArgumentNullException>(() => ModuleDiscovery.DiscoverModules(null!));
        }

        [Fact]
        public void ModuleDiscovery_IsNotPublic()
        {
            typeof(ModuleDiscovery).IsPublic.ShouldBeFalse();
        }

        internal class InternalModule : IAppModule;

        public class PublicModule : IAppModule;

        public class ExcludedChild : ExcludedModule;
    }

    [ExcludeFromDiscovery]
    public class ExcludedModule : IAppModule
    {
        public ExcludedModule() => Fixtures.FilteredConstructions++;
    }

    public class Fixtures
    {
        public static int FilteredConstructions;

        public static Type[] All =>
        [
            typeof(GenericModule<>), typeof(PrivateNested), typeof(ProtectedNested), typeof(PrivateProtectedNested),
            typeof(ProtectedInternalNested), typeof(PrivateOuter.InnerPublic), typeof(AbstractModule),
            typeof(CtorArgsModule), typeof(ExcludedModule),
            typeof(ModuleDiscoveryTests.InternalModule), typeof(ModuleDiscoveryTests.PublicModule),
        ];

        public class GenericModule<T> : IAppModule;

        public class IntModule : GenericModule<int>;

        public abstract class AbstractModule : IAppModule;

        public class CtorArgsModule(string name) : IAppModule
        {
            public string Name => name;
        }

        protected internal class ProtectedInternalNested : IAppModule;

        private class PrivateNested : IAppModule
        {
            public PrivateNested() => FilteredConstructions++;
        }

        protected class ProtectedNested : IAppModule
        {
            public ProtectedNested() => FilteredConstructions++;
        }

        private protected class PrivateProtectedNested : IAppModule
        {
            public PrivateProtectedNested() => FilteredConstructions++;
        }

        private class PrivateOuter
        {
            public class InnerPublic : IAppModule
            {
                public InnerPublic() => FilteredConstructions++;
            }
        }
    }
}

namespace A.Zeta
{
    public class Beta : TheAppManager.IAppModule;
}

namespace B.Zeta
{
    public class Alpha : TheAppManager.IAppModule;
}
