# Spatial hashing for Unity using ECS 
A spatial hash is one way of indexing objects in space.
This package allows you to indexing struct manually or automatically ( via IComponentData) with high speed

![Spatial hashing image](https://zupimages.net/up/22/21/uw0s.png)

Source : https://www.researchgate.net/figure/Spatial-hashing-where-objects-are-mapped-into-uniformly-sized-cells_fig1_326306568

# Requirement
- Unity 2021.3.30
- Package Entities 0.51

# How to add to your project
- Open manifest.json at %RepertoryPath%/Packages
- Add ```"com.lennethproduction.spatialhashing": "https://github.com/Sylmerria/Spatial-Hashing.git"``` in dependencies section

# How to start

##Manually
1. Implement ISpatialHashingItem on a struct
2. Allocate a SpatialHash of this struct
3. Use it

##Automatically
1. Implement ISpatialHashingItem on a IComponentData struct
2. Implement a IComponentData struct to warn the system when an entity is dirty
3. Implement a child class inheritant SpatialHashingSystem ( T is 1. component, TY will be HMH.ECS.SpatialHashing.SpatialHashingMirror (or similar), TZ will be 2. component
4. Add that before the child class ( see un)
>  [assembly: RegisterGenericJobType(typeof({CHILDCLASSTYPE}.AddSpatialHashingJob))]  
   [assembly: RegisterGenericJobType(typeof({CHILDCLASSTYPE}.AddSpatialHashingEndJob))]  
   [assembly: RegisterGenericJobType(typeof({CHILDCLASSTYPE}.UpdateSpatialHashingRemoveFastJob))]  
   [assembly: RegisterGenericJobType(typeof({CHILDCLASSTYPE}.UpdateSpatialHashingAddFastJob))]  
   [assembly: RegisterGenericJobType(typeof({CHILDCLASSTYPE}.RemoveSpatialHashingJob))]  
5. good to go (See unit test to concrete example)
