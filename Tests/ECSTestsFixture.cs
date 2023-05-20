using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.LowLevel;

namespace HMH.ECS.SpatialHashing.Test
{
    public class ECSTestsCommonBase
    {
        [SetUp]
        public virtual void Setup()
        {
#if UNITY_DOTSRUNTIME
            Unity.Runtime.TempMemoryScope.EnterScope();
#endif
        }

        [TearDown]
        public virtual void TearDown()
        {
#if UNITY_DOTSRUNTIME
            Unity.Runtime.TempMemoryScope.ExitScope();
#endif
        }
    }

    public class ECSTestsFixture : ECSTestsCommonBase
    {
#if !UNITY_DOTSRUNTIME
        protected PlayerLoopSystem m_PreviousPlayerLoop;
#endif

        private bool JobsDebuggerWasEnabled;

        protected World PreviousWorld { get; private set; }
        protected World World { get; private set; }
        protected EntityManager EntityManager { get; private set; }
        protected EntityManager.EntityManagerDebug EntityManagerDebug { get; private set; }

        [SetUp]
        public override void Setup()
        {
            base.Setup();

#if !UNITY_DOTSRUNTIME

            // unit tests preserve the current player loop to restore later, and start from a blank slate.
            m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
#endif

            PreviousWorld = World.DefaultGameObjectInjectionWorld;
            World         = World.DefaultGameObjectInjectionWorld = new World("Test World");

            EntityManager      = World.EntityManager;
            EntityManagerDebug = new EntityManager.EntityManagerDebug(EntityManager);

            // Many ECS tests will only pass if the Jobs Debugger enabled;
            // force it enabled for all tests, and restore the original value at teardown.
            JobsDebuggerWasEnabled         = JobsUtility.JobDebuggerEnabled;
            JobsUtility.JobDebuggerEnabled = true;
#if !UNITY_DOTSRUNTIME
            //JobsUtility.ClearSystemIds();
#endif

            World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        [TearDown]
        public override void TearDown()
        {
            if (World != null && World.IsCreated)
            {
                // Clean up systems before calling CheckInternalConsistency because we might have filters etc
                // holding on SharedComponentData making checks fail
                while (World.Systems.Count > 0)
                {
                    World.DestroySystemManaged(World.Systems[0]);
                }

                EntityManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = PreviousWorld;
                PreviousWorld                         = null;
                EntityManager                         = default;
            }

            JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;
#if !UNITY_DOTSRUNTIME
            //JobsUtility.ClearSystemIds();
#endif

#if !UNITY_DOTSRUNTIME
            PlayerLoop.SetPlayerLoop(m_PreviousPlayerLoop);
#endif
            base.TearDown();
        }

        public virtual Entity CreateEntity(int index, int version)
        {
            return new Entity { Index = index, Version = version };
        }
    }
}