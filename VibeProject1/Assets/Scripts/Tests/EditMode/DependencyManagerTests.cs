using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;

namespace Game.Core.Tests
{
    public class DependencyManagerTests
    {
        private interface ISampleDependency
        {
        }

        private class SampleDependency : ISampleDependency
        {
        }

        private GameObject gameObject;
        private DependencyManager dependencyManager;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject(nameof(DependencyManagerTests));
            dependencyManager = gameObject.AddComponent<DependencyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Resolve_ReturnsRegisteredInstance()
        {
            var dependency = new SampleDependency();
            dependencyManager.Register<ISampleDependency>(dependency);

            var resolved = dependencyManager.Resolve<ISampleDependency>();

            Assert.AreSame(dependency, resolved);
        }

        [Test]
        public void Resolve_ThrowsWhenNotRegistered()
        {
            Assert.Throws<DependencyNotRegisteredException>(() => dependencyManager.Resolve<ISampleDependency>());
        }

        [Test]
        public void TryResolve_ReturnsFalseWhenNotRegistered()
        {
            var result = dependencyManager.TryResolve<ISampleDependency>(out var instance);

            Assert.IsFalse(result);
            Assert.IsNull(instance);
        }

        [Test]
        public void TryResolve_ReturnsTrueWhenRegistered()
        {
            var dependency = new SampleDependency();
            dependencyManager.Register<ISampleDependency>(dependency);

            var result = dependencyManager.TryResolve<ISampleDependency>(out var instance);

            Assert.IsTrue(result);
            Assert.AreSame(dependency, instance);
        }

        [Test]
        public void Register_DuplicateLogsWarning()
        {
            dependencyManager.Register<ISampleDependency>(new SampleDependency());

            LogAssert.Expect(LogType.Warning, new Regex(".*이미 등록되어 있다.*"));
            dependencyManager.Register<ISampleDependency>(new SampleDependency());
        }
    }
}
