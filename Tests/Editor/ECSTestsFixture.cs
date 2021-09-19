using NUnit.Framework;
using Unity.Entities;

namespace HMH.ECS.SpatialHashing.Test
{
    public class ECSTestsFixture
    {
        protected World PreviousWorld { get; private set; }
        protected World World { get; private set; }
        protected EntityManager EntityManager { get; private set; }
        protected EntityManager.EntityManagerDebug EntityManagerDebug { get; private set; }

        [SetUp]
        public virtual void Setup()
        {
            PreviousWorld = World.DefaultGameObjectInjectionWorld;
            World         = World.DefaultGameObjectInjectionWorld = new World("Test World");

            EntityManager      = World.EntityManager;
            EntityManagerDebug = new EntityManager.EntityManagerDebug(EntityManager);

            World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (World != null)
            {
                // Clean up systems before calling CheckInternalConsistency because we might have filters etc
                // holding on SharedComponentData making checks fail
                while (World.Systems.Count > 0)
                {
                    World.DestroySystem(World.Systems[0]);
                }

                EntityManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = PreviousWorld;
                PreviousWorld                         = null;
                EntityManager                         = default;
            }
        }

        public virtual Entity CreateEntity(int index, int version)
        {
            return new Entity { Index = index, Version = version };
        }
    }
}