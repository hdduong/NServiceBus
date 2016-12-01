﻿namespace NServiceBus.Core.Tests.Features
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.Features;
    using Transport;
    using NUnit.Framework;
    using ObjectBuilder;
    using Settings;

    [TestFixture]
    public class FeatureStartupTests
    {
        private FeatureActivator featureSettings;
        private SettingsHolder settings;

        [SetUp]
        public void Init()
        {
            settings = new SettingsHolder();
            settings.Set<TransportDefinition>(new MsmqTransport());
            featureSettings = new FeatureActivator(settings);
        }

        [Test]
        public async Task Should_start_and_stop_features()
        {
            var feature = new FeatureWithStartupTask();

            featureSettings.Add(feature);

            featureSettings.SetupFeatures(null, null);

            await featureSettings.StartFeatures(null, null);
            await featureSettings.StopFeatures(null);

            Assert.True(feature.TaskStarted);
            Assert.True(feature.TaskStopped);
        }

        [Test]
        public async Task Should_dispose_feature_startup_tasks_when_they_implement_IDisposable()
        {
            var feature = new FeatureWithStartupTaskWhichIsDisposable();

            featureSettings.Add(feature);

            featureSettings.SetupFeatures(null, null);

            await featureSettings.StartFeatures(null, null);
            await featureSettings.StopFeatures(null);

            Assert.True(feature.TaskDisposed);
        }

        [Test]
        public void Should_not_throw_when_feature_task_fails_on_start_and_abort_starting()
        {
            var feature1 = new FeatureWithStartupTaskThatThrows(throwOnStart: true, throwOnStop: false);
            var feature2 = new FeatureWithStartupTaskThatThrows(throwOnStart: false, throwOnStop: false);
            featureSettings.Add(feature1);
            featureSettings.Add(feature2);

            featureSettings.SetupFeatures(null, null);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await featureSettings.StartFeatures(null, null));

            Assert.False(feature1.TaskStarted && feature1.TaskStopped);
            Assert.False(feature2.TaskStarted && feature2.TaskStopped);
        }

        [Test]
        public async Task Should_not_throw_when_feature_task_fails_on_stop_and_not_abort_stopping()
        {
            var feature1 = new FeatureWithStartupTaskThatThrows(throwOnStart: false, throwOnStop: false);
            var feature2 = new FeatureWithStartupTaskThatThrows(throwOnStart: false, throwOnStop: true);
            featureSettings.Add(feature1);
            featureSettings.Add(feature2);

            featureSettings.SetupFeatures(null, null);

            await featureSettings.StartFeatures(null, null);

            Assert.DoesNotThrowAsync(async () => await featureSettings.StopFeatures(null));
            Assert.True(feature1.TaskStarted && feature1.TaskStopped);
            Assert.True(feature2.TaskStarted && !feature2.TaskStopped);
        }

        [Test]
        public async Task Should_dispose_feature_task_even_when_stop_throws()
        {
            var feature = new FeatureWithStartupTaskThatThrows(throwOnStart: false, throwOnStop: true);
            featureSettings.Add(feature);

            featureSettings.SetupFeatures(null, null);

            await featureSettings.StartFeatures(null, null);

            await featureSettings.StopFeatures(null);
            Assert.True(feature.TaskDisposed);
        }

        class FeatureWithStartupTask : TestFeature
        {
            public FeatureWithStartupTask()
            {
                EnableByDefault();
            }

            protected internal override void Setup(FeatureConfigurationContext context)
            {
                context.RegisterStartupTask(new Runner(this));
            }

            public class Runner : FeatureStartupTask
            {
                FeatureWithStartupTask parentFeature;

                public Runner(FeatureWithStartupTask parentFeature)
                {
                    this.parentFeature = parentFeature;
                }

                protected override Task OnStart(IMessageSession session)
                {
                    parentFeature.TaskStarted = true;
                    return TaskEx.CompletedTask;
                }

                protected override Task OnStop(IMessageSession session)
                {
                    parentFeature.TaskStopped = true;
                    return TaskEx.CompletedTask;
                }
            }

            public bool TaskStarted { get; private set; }
            public bool TaskStopped { get; private set; }
        }

        class FeatureWithStartupTaskThatThrows : TestFeature
        {
            bool throwOnStart;
            bool throwOnStop;

            public FeatureWithStartupTaskThatThrows(bool throwOnStart = false, bool throwOnStop = false)
            {
                this.throwOnStart = throwOnStart;
                this.throwOnStop = throwOnStop;

                EnableByDefault();
            }

            protected internal override void Setup(FeatureConfigurationContext context)
            {
                context.RegisterStartupTask(new Runner(this));
            }

            public class Runner : FeatureStartupTask, IDisposable
            {
                FeatureWithStartupTaskThatThrows parentFeature;

                public Runner(FeatureWithStartupTaskThatThrows parenFeature)
                {
                    parentFeature = parenFeature;
                }

                protected override async Task OnStart(IMessageSession session)
                {
                    await Task.Yield();
                    if (parentFeature.throwOnStart)
                    {
                        throw new InvalidOperationException();
                    }
                    parentFeature.TaskStarted = true;
                }

                protected override async Task OnStop(IMessageSession session)
                {
                    await Task.Yield();
                    if (parentFeature.throwOnStop)
                    {
                        throw new InvalidOperationException();
                    }
                    parentFeature.TaskStopped = true;
                }

                public void Dispose()
                {
                    parentFeature.TaskDisposed = true;
                }
            }

            public bool TaskStarted { get; private set; }
            public bool TaskStopped { get; private set; }
            public bool TaskDisposed { get; private set; }
        }

        class FeatureWithStartupTaskWhichIsDisposable : TestFeature
        {
            public FeatureWithStartupTaskWhichIsDisposable()
            {
                EnableByDefault();
            }

            protected internal override void Setup(FeatureConfigurationContext context)
            {
                context.RegisterStartupTask(new Runner(this));
            }

            public class Runner : FeatureStartupTask, IDisposable
            {
                FeatureWithStartupTaskWhichIsDisposable parentFeature;

                public Runner(FeatureWithStartupTaskWhichIsDisposable parentFeature)
                {
                    this.parentFeature = parentFeature;
                }

                protected override Task OnStart(IMessageSession session)
                {
                    return TaskEx.CompletedTask;
                }

                protected override Task OnStop(IMessageSession session)
                {
                    return TaskEx.CompletedTask;
                }

                public void Dispose()
                {
                    parentFeature.TaskDisposed = true;
                }
            }

            public bool TaskDisposed { get; private set; }
        }
    }

    public class FakeBuilder : IBuilder
    {
        Type type;

        public FakeBuilder()
        {
        }

        public FakeBuilder(Type type)
        {
            this.type = type;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public object Build(Type typeToBuild)
        {
            if (typeToBuild != type)
            {
                throw new Exception("Not the expected type");
            }
            return Activator.CreateInstance(typeToBuild);
        }

        public IBuilder CreateChildBuilder()
        {
            throw new NotImplementedException();
        }

        public T Build<T>()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> BuildAll<T>()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> BuildAll(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public void Release(object instance)
        {
            throw new NotImplementedException();
        }

        public void BuildAndDispatch(Type typeToBuild, Action<object> action)
        {
            throw new NotImplementedException();
        }
    }
}