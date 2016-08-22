// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Specification.Fakes;
using Xunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection.Ordered;

namespace Microsoft.Extensions.DependencyInjection.Specification
{
    public abstract partial class DependencyInjectionSpecificationTests
    {
        protected abstract IServiceProvider CreateServiceProvider(IServiceCollection serviceCollection);

        [Fact]
        public void ServicesRegisteredWithImplementationTypeCanBeResolved()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient(typeof(IFakeService), typeof(FakeService));
            var provider = CreateServiceProvider(collection);

            // Act
            var service = provider.GetService<IFakeService>();

            // Assert
            Assert.NotNull(service);
            Assert.IsType<FakeService>(service);
        }

        [Fact]
        public void ServicesRegisteredWithImplementationType_ReturnDifferentInstancesPerResolution_ForTransientServices()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient(typeof(IFakeService), typeof(FakeService));
            var provider = CreateServiceProvider(collection);

            // Act
            var service1 = provider.GetService<IFakeService>();
            var service2 = provider.GetService<IFakeService>();

            // Assert
            Assert.IsType<FakeService>(service1);
            Assert.IsType<FakeService>(service2);
            Assert.NotSame(service1, service2);
        }

        [Fact]
        public void ServicesRegisteredWithImplementationType_ReturnSameInstancesPerResolution_ForSingletons()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddSingleton(typeof(IFakeService), typeof(FakeService));
            var provider = CreateServiceProvider(collection);

            // Act
            var service1 = provider.GetService<IFakeService>();
            var service2 = provider.GetService<IFakeService>();

            // Assert
            Assert.IsType<FakeService>(service1);
            Assert.IsType<FakeService>(service2);
            Assert.Same(service1, service2);
        }

        [Fact]
        public void ServiceInstanceCanBeResolved()
        {
            // Arrange
            var collection = new ServiceCollection();
            var instance = new FakeService();
            collection.AddSingleton(typeof(IFakeServiceInstance), instance);
            var provider = CreateServiceProvider(collection);

            // Act
            var service = provider.GetService<IFakeServiceInstance>();

            // Assert
            Assert.Same(instance, service);
        }

        [Fact]
        public void TransientServiceCanBeResolvedFromProvider()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient(typeof(IFakeService), typeof(FakeService));
            var provider = CreateServiceProvider(collection);

            // Act
            var service1 = provider.GetService<IFakeService>();
            var service2 = provider.GetService<IFakeService>();

            // Assert
            Assert.NotNull(service1);
            Assert.NotSame(service1, service2);
        }

        [Fact]
        public void TransientServiceCanBeResolvedFromScope()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient(typeof(IFakeService), typeof(FakeService));
            var provider = CreateServiceProvider(collection);

            // Act
            var service1 = provider.GetService<IFakeService>();

            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                var scopedService1 = scope.ServiceProvider.GetService<IFakeService>();
                var scopedService2 = scope.ServiceProvider.GetService<IFakeService>();

                // Assert
                Assert.NotSame(service1, scopedService1);
                Assert.NotSame(service1, scopedService2);
                Assert.NotSame(scopedService1, scopedService2);
            }
        }

        [Fact]
        public void MultipleServiceCanBeIEnumerableResolved()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddEnumerable(typeof(IFakeMultipleService)).AddTransient(typeof(FakeOneMultipleService));
            collection.AddEnumerable(typeof(IFakeMultipleService)).AddTransient(typeof(FakeTwoMultipleService));
            var provider = CreateServiceProvider(collection);

            // Act
            var services = provider.GetService<IEnumerable<IFakeMultipleService>>();

            // Assert
            Assert.Collection(services.OrderBy(s => s.GetType().FullName),
                service => Assert.IsType<FakeOneMultipleService>(service),
                service => Assert.IsType<FakeTwoMultipleService>(service));
        }

        [Fact]
        public void OuterServiceCanHaveOtherServicesInjected()
        {
            // Arrange
            var collection = new ServiceCollection();
            var fakeService = new FakeService();
            collection.AddTransient<IFakeOuterService, FakeOuterService>();
            collection.AddSingleton<IFakeService>(fakeService);
            collection.AddEnumerable<IFakeMultipleService>().AddTransient<FakeOneMultipleService>();
            collection.AddEnumerable<IFakeMultipleService>().AddTransient<FakeTwoMultipleService>();
            var provider = CreateServiceProvider(collection);

            // Act
            var services = provider.GetService<IFakeOuterService>();

            // Assert
            Assert.Same(fakeService, services.SingleService);
            Assert.Collection(services.MultipleServices.OrderBy(s => s.GetType().FullName),
                service => Assert.IsType<FakeOneMultipleService>(service),
                service => Assert.IsType<FakeTwoMultipleService>(service));
        }

        [Fact]
        public void FactoryServicesCanBeCreatedByGetService()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient<IFakeService, FakeService>();
            collection.AddTransient<IFactoryService>(p =>
            {
                var fakeService = p.GetRequiredService<IFakeService>();
                return new TransientFactoryService
                {
                    FakeService = fakeService,
                    Value = 42
                };
            });
            var provider = CreateServiceProvider(collection);

            // Act
            var service = provider.GetService<IFactoryService>();

            // Assert
            Assert.NotNull(service);
            Assert.Equal(42, service.Value);
            Assert.NotNull(service.FakeService);
            Assert.IsType<FakeService>(service.FakeService);
        }

        [Fact]
        public void FactoryServicesAreCreatedAsPartOfCreatingObjectGraph()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient<IFakeService, FakeService>();
            collection.AddTransient<IFactoryService>(p =>
            {
                var fakeService = p.GetService<IFakeService>();
                return new TransientFactoryService
                {
                    FakeService = fakeService,
                    Value = 42
                };
            });
            collection.AddScoped(p =>
            {
                var fakeService = p.GetService<IFakeService>();
                return new ScopedFactoryService
                {
                    FakeService = fakeService,
                };
            });
            collection.AddTransient<ServiceAcceptingFactoryService>();
            var provider = CreateServiceProvider(collection);

            // Act
            var service1 = provider.GetService<ServiceAcceptingFactoryService>();
            var service2 = provider.GetService<ServiceAcceptingFactoryService>();

            // Assert
            Assert.Equal(42, service1.TransientService.Value);
            Assert.NotNull(service1.TransientService.FakeService);

            Assert.Equal(42, service2.TransientService.Value);
            Assert.NotNull(service2.TransientService.FakeService);

            Assert.NotNull(service1.ScopedService.FakeService);

            // Verify scoping works
            Assert.NotSame(service1.TransientService, service2.TransientService);
            Assert.Same(service1.ScopedService, service2.ScopedService);
        }

        [Fact]
        public void LastServiceReplacesPreviousServices()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient<IFakeMultipleService, FakeOneMultipleService>();
            collection.AddTransient<IFakeMultipleService, FakeTwoMultipleService>();
            var provider = CreateServiceProvider(collection);

            // Act
            var service = provider.GetService<IFakeMultipleService>();

            // Assert
            Assert.IsType<FakeTwoMultipleService>(service);
        }

        public static TheoryData<Action<IServiceCollection>, Func<IServiceProvider, IFakeService>> SingletonRegistrations
        {
            get
            {
                return new TheoryData<Action<IServiceCollection>, Func<IServiceProvider, IFakeService>>()
                {
                    {
                        c => c.AddSingleton<IFakeScopedService, FakeService>(),
                        p => p.GetService<IFakeScopedService>()
                    },
                    {
                        c => c.AddOrdered<IFakeScopedService>().AddSingleton<FakeService>(),
                        p => p.GetService<IOrdered<IFakeScopedService>>().First()
                    },
                    {
                        c => c.AddEnumerable<IFakeScopedService>().AddSingleton<FakeService>(),
                        p => p.GetService<IEnumerable<IFakeScopedService>>().First()
                    },
                };
            }
        }

        [Theory]
        [MemberData(nameof(SingletonRegistrations))]
        public void SingletonServiceCanBeResolved(Action<IServiceCollection> add, Func<IServiceProvider, IFakeService> get)
        {
            // Arrange
            var collection = new ServiceCollection();
            add(collection);
            var provider = CreateServiceProvider(collection);

            // Act
            var service1 = get(provider);
            var service2 = get(provider);

            // Assert
            Assert.NotNull(service1);
            Assert.Same(service1, service2);
        }

        [Fact]
        public void ServiceProviderRegistersServiceScopeFactory()
        {
            // Arrange
            var collection = new ServiceCollection();
            var provider = CreateServiceProvider(collection);

            // Act
            var scopeFactory = provider.GetService<IServiceScopeFactory>();

            // Assert
            Assert.NotNull(scopeFactory);
        }


        public static TheoryData<Action<IServiceCollection>, Func<IServiceProvider, IFakeService>> ScopedRegistrations
        {
            get
            {
                return new TheoryData<Action<IServiceCollection>, Func<IServiceProvider, IFakeService>>()
                {
                    {
                        c => c.AddScoped<IFakeScopedService, FakeService>(),
                        p => p.GetService<IFakeScopedService>()
                    },
                    {
                        c => c.AddOrdered<IFakeScopedService>().AddScoped<FakeService>(),
                        p => p.GetService<IOrdered<IFakeScopedService>>().First()
                    },
                    {
                        c => c.AddEnumerable<IFakeScopedService>().AddScoped<FakeService>(),
                        p => p.GetService<IEnumerable<IFakeScopedService>>().First()
                    },
                };
            }
        }

        [Theory]
        [MemberData(nameof(ScopedRegistrations))]
        public void ScopedServiceCanBeResolved(Action<IServiceCollection> add, Func<IServiceProvider, IFakeService> get)
        {
            // Arrange
            var collection = new ServiceCollection();
            add(collection);
            var provider = CreateServiceProvider(collection);

            // Act
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                var providerScopedService = get(provider);
                var scopedService1 = get(scope.ServiceProvider);
                var scopedService2 = get(scope.ServiceProvider);

                // Assert
                Assert.NotSame(providerScopedService, scopedService1);
                Assert.Same(scopedService1, scopedService2);
            }
        }

        [Theory]
        [MemberData(nameof(ScopedRegistrations))]
        public void NestedScopedServiceCanBeResolved(Action<IServiceCollection> add, Func<IServiceProvider, IFakeService> get)
        {
            // Arrange
            var collection = new ServiceCollection();
            add(collection);
            var provider = CreateServiceProvider(collection);

            // Act
            var outerScopeFactory = provider.GetService<IServiceScopeFactory>();
            using (var outerScope = outerScopeFactory.CreateScope())
            {
                var innerScopeFactory = outerScope.ServiceProvider.GetService<IServiceScopeFactory>();
                using (var innerScope = innerScopeFactory.CreateScope())
                {
                    var outerScopedService = get(outerScope.ServiceProvider);
                    var innerScopedService = get(innerScope.ServiceProvider);

                    // Assert
                    Assert.NotNull(outerScopedService);
                    Assert.NotNull(innerScopedService);
                    Assert.NotSame(outerScopedService, innerScopedService);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ScopedRegistrations))]
        public void ScopedServices_FromCachedScopeFactory_CanBeResolvedAndDisposed(Action<IServiceCollection> add, Func<IServiceProvider, IFakeService> get)
        {
            // Arrange
            var collection = new ServiceCollection();
            add(collection);
            var provider = CreateServiceProvider(collection);
            var cachedScopeFactory = provider.GetService<IServiceScopeFactory>();

            // Act
            for (var i = 0; i < 3; i++)
            {
                FakeService outerScopedService;
                using (var outerScope = cachedScopeFactory.CreateScope())
                {
                    var innerScopeFactory = outerScope.ServiceProvider.GetService<IServiceScopeFactory>();

                    FakeService innerScopedService;
                    using (var innerScope = innerScopeFactory.CreateScope())
                    {
                        outerScopedService = get(outerScope.ServiceProvider) as FakeService;
                        innerScopedService = get(innerScope.ServiceProvider) as FakeService;

                        // Assert
                        Assert.NotNull(outerScopedService);
                        Assert.NotNull(innerScopedService);
                        Assert.NotSame(outerScopedService, innerScopedService);
                    }

                    Assert.False(outerScopedService.Disposed);
                    Assert.True(innerScopedService.Disposed);
                }

                Assert.True(outerScopedService.Disposed);
            }
        }

        [Fact]
        public void DisposingScopeDisposesService()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddSingleton<IFakeSingletonService, FakeService>();
            collection.AddScoped<IFakeScopedService, FakeService>();
            collection.AddTransient<IFakeService, FakeService>();

            var provider = CreateServiceProvider(collection);
            FakeService disposableService;
            FakeService transient1;
            FakeService transient2;
            FakeService singleton;

            // Act and Assert
            var transient3 = Assert.IsType<FakeService>(provider.GetService<IFakeService>());
            var scopeFactory = provider.GetService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                disposableService = (FakeService)scope.ServiceProvider.GetService<IFakeScopedService>();
                transient1 = (FakeService)scope.ServiceProvider.GetService<IFakeService>();
                transient2 = (FakeService)scope.ServiceProvider.GetService<IFakeService>();
                singleton = (FakeService)scope.ServiceProvider.GetService<IFakeSingletonService>();

                Assert.False(disposableService.Disposed);
                Assert.False(transient1.Disposed);
                Assert.False(transient2.Disposed);
                Assert.False(singleton.Disposed);
            }

            Assert.True(disposableService.Disposed);
            Assert.True(transient1.Disposed);
            Assert.True(transient2.Disposed);
            Assert.False(singleton.Disposed);

            var disposableProvider = provider as IDisposable;
            if (disposableProvider != null)
            {
                disposableProvider.Dispose();
                Assert.True(singleton.Disposed);
                Assert.True(transient3.Disposed);
            }
        }

        [Fact]
        public void SelfResolveThenDispose()
        {
            // Arrange
            var collection = new ServiceCollection();
            var provider = CreateServiceProvider(collection);

            // Act
            var serviceProvider = provider.GetService<IServiceProvider>();

            // Assert
            Assert.NotNull(serviceProvider);
            (provider as IDisposable)?.Dispose();
        }

        [Fact]
        public void SafelyDisposeNestedProviderReferences()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient<ClassWithNestedReferencesToProvider>();
            var provider = CreateServiceProvider(collection);

            // Act
            var nester = provider.GetService<ClassWithNestedReferencesToProvider>();

            // Assert
            Assert.NotNull(nester);
            nester.Dispose();
        }

        [Theory]
        [MemberData(nameof(SingletonRegistrations))]
        public void SingletonServicesComeFromRootProvider(Action<IServiceCollection> add, Func<IServiceProvider, IFakeService> get)
        {
            // Arrange
            var collection = new ServiceCollection();
            add(collection);
            var provider = CreateServiceProvider(collection);
            FakeService disposableService1;
            FakeService disposableService2;

            // Act and Assert
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                var service = get(scope.ServiceProvider);
                disposableService1 = Assert.IsType<FakeService>(service);
                Assert.False(disposableService1.Disposed);
            }

            Assert.False(disposableService1.Disposed);

            using (var scope = scopeFactory.CreateScope())
            {
                var service = get(scope.ServiceProvider);
                disposableService2 = Assert.IsType<FakeService>(service);
                Assert.False(disposableService2.Disposed);
            }

            Assert.False(disposableService2.Disposed);
            Assert.Same(disposableService1, disposableService2);
        }

        [Fact]
        public void NestedScopedServiceCanBeResolvedWithNoFallbackProvider()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddScoped<IFakeScopedService, FakeService>();
            var provider = CreateServiceProvider(collection);

            // Act
            var outerScopeFactory = provider.GetService<IServiceScopeFactory>();
            using (var outerScope = outerScopeFactory.CreateScope())
            {
                var innerScopeFactory = outerScope.ServiceProvider.GetService<IServiceScopeFactory>();
                using (var innerScope = innerScopeFactory.CreateScope())
                {
                    var outerScopedService = outerScope.ServiceProvider.GetService<IFakeScopedService>();
                    var innerScopedService = innerScope.ServiceProvider.GetService<IFakeScopedService>();

                    // Assert
                    Assert.NotSame(outerScopedService, innerScopedService);
                }
            }
        }

        [Fact]
        public void OpenGenericServicesCanBeResolved()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient(typeof(IFakeOpenGenericService<>), typeof(FakeOpenGenericService<>));
            collection.AddSingleton<IFakeSingletonService, FakeService>();
            var provider = CreateServiceProvider(collection);

            // Act
            var genericService = provider.GetService<IFakeOpenGenericService<IFakeSingletonService>>();
            var singletonService = provider.GetService<IFakeSingletonService>();

            // Assert
            Assert.Same(singletonService, genericService.Value);
        }

        [Fact]
        public void ClosedServicesPreferredOverOpenGenericServices()
        {
            // Arrange
            var collection = new ServiceCollection();
            collection.AddTransient(typeof(IFakeOpenGenericService<PocoClass>), typeof(FakeService));
            collection.AddTransient(typeof(IFakeOpenGenericService<>), typeof(FakeOpenGenericService<>));
            collection.AddSingleton<PocoClass>();
            var provider = CreateServiceProvider(collection);

            // Act
            var service = provider.GetService<IFakeOpenGenericService<PocoClass>>();

            // Assert
            Assert.IsType<FakeService>(service);
        }

        [Fact]
        public void AttemptingToResolveNonexistentServiceReturnsNull()
        {
            // Arrange
            var collection = new ServiceCollection();
            var provider = CreateServiceProvider(collection);

            // Act
            var service = provider.GetService<INonexistentService>();

            // Assert
            Assert.Null(service);
        }

        public static TheoryData ServiceContainerPicksConstructorWithLongestMatchesData
        {
            get
            {
                var fakeService = new FakeService();
                var multipleService = new FakeService();
                var factoryService = new TransientFactoryService();
                var scopedService = new FakeService();

                return new TheoryData<IServiceCollection, TypeWithSupersetConstructors>
                {
                    {
                        new ServiceCollection()
                            .AddSingleton<IFakeService>(fakeService),
                        new TypeWithSupersetConstructors(fakeService)
                    },
                    {
                        new ServiceCollection()
                            .AddSingleton<IFactoryService>(factoryService),
                        new TypeWithSupersetConstructors(factoryService)
                    },
                    {
                        new ServiceCollection()
                            .AddSingleton<IFakeService>(fakeService)
                            .AddSingleton<IFactoryService>(factoryService),
                       new TypeWithSupersetConstructors(fakeService, factoryService)
                    },
                    {
                        new ServiceCollection()
                            .AddSingleton<IFakeService>(fakeService)
                            .AddSingleton<IFakeMultipleService>(multipleService)
                            .AddSingleton<IFactoryService>(factoryService),
                       new TypeWithSupersetConstructors(fakeService, multipleService, factoryService)
                    },
                    {
                        new ServiceCollection()
                            .AddSingleton<IFakeService>(fakeService)
                            .AddSingleton<IFakeMultipleService>(multipleService)
                            .AddSingleton<IFakeScopedService>(scopedService)
                            .AddSingleton<IFactoryService>(factoryService),
                       new TypeWithSupersetConstructors(multipleService, factoryService, fakeService, scopedService)
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(ServiceContainerPicksConstructorWithLongestMatchesData))]
        public void ServiceContainerPicksConstructorWithLongestMatches(
            IServiceCollection serviceCollection,
            TypeWithSupersetConstructors expected)
        {
            // Arrange
            serviceCollection.AddTransient<TypeWithSupersetConstructors>();
            var serviceProvider = CreateServiceProvider(serviceCollection);

            // Act
            var actual = serviceProvider.GetService<TypeWithSupersetConstructors>();

            // Assert
            Assert.NotNull(actual);
            Assert.Same(expected.Service, actual.Service);
            Assert.Same(expected.FactoryService, actual.FactoryService);
            Assert.Same(expected.MultipleService, actual.MultipleService);
            Assert.Same(expected.ScopedService, actual.ScopedService);
        }
    }
}
