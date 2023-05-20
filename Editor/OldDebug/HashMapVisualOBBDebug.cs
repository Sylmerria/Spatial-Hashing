using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HMH.ECS.SpatialHashing.Debug
{
    public class HashMapVisualOBBDebug : MonoBehaviour
    {
        private void Start()
        {
            Random.InitState(123456789);

            var copy = new Bounds(_worldBounds.Center, _worldBounds.Size);
            _spatialHashing = new SpatialHash<HashMapVisualDebug.ItemTest>(copy, new float3(10F), Allocator.Persistent);

        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying == false)
                return;

            var currentPosition     = transform.position;
            var targetBounds = new Bounds(currentPosition, _boundSize);

            var transformRotation = transform.rotation;
            var bounds2           = SpatialHash<HashMapVisualDebug.ItemTest>.TransformBounds(in targetBounds, transformRotation);
            bounds2.Clamp(_spatialHashing.WorldBounds);


            //********************** ligne raycast
            var startPosition = start.transform.position;
            Ray r         = new Ray(startPosition, end.transform.position - startPosition);

            var rr = new Ray(math.mul(math.inverse(transformRotation), (startPosition - currentPosition)) + (float3)currentPosition,
                             math.mul(math.inverse(transformRotation), r.direction));

            if (targetBounds.RayCastOBB(r.origin, r.direction, transformRotation, out var pp, math.length(end.transform.position - start.transform.position)))
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.red;

            Gizmos.DrawLine(start.transform.position, end.transform.position);

            //local recast
            Gizmos.color = Color.black;

            var localRotation       = math.inverse(transformRotation);
            var origin              = math.mul(localRotation, ((float3)r.origin - targetBounds.Center)) + targetBounds.Center;
            var directionNormalized = math.mul(localRotation, r.direction);
            Gizmos.DrawLine(origin, origin + directionNormalized * (math.length(start.transform.position - end.transform.position)));

            //*********************** interception point

            Gizmos.color = Color.cyan;
            Gizmos.DrawCube(pp, new Vector3(1F, 1F, 1F));



            //************ Debug
            var list = new NativeList<int3>(20, Allocator.Temp);
            _spatialHashing.Query(targetBounds, transformRotation, list);




            int3 cell = list[indexToDraw]; //new int3(9, 14, 16);

            var bounds = SpatialHash<HashMapVisualDebug.ItemTest>.TransformBounds(in targetBounds, transformRotation);
            bounds.Clamp(_spatialHashing.WorldBounds);

            targetBounds.Size += _spatialHashing.CellSize;
            var pos = _spatialHashing.GetPositionVoxel(cell, true);

            var r0 = new Ray(pos-new float3(_spatialHashing.CellSize.x*0.5F,0F,0F), Vector3.right);

            var r0Y = new Ray( pos-new float3(0F,_spatialHashing.CellSize.y*0.5F,0F), Vector3.up);

            var r0Z = new Ray(pos-new float3(0F,0F,_spatialHashing.CellSize.z*0.5F), Vector3.forward);

            UnityEngine.Debug.Log("index " + list[indexToDraw]);
            Color c;

            if (targetBounds.RayCastOBB(r0.origin, r0.direction, transformRotation, out var px,_spatialHashing.CellSize.x))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawCube(px, new Vector3(1F, 1F, 1F));
            }

            if (targetBounds.RayCastOBB(r0Y.origin, r0Y.direction, transformRotation, out var py,_spatialHashing.CellSize.y))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(py, new Vector3(1F, 1F, 1F));
            }

            if (targetBounds.RayCastOBB(r0Z.origin, r0Z.direction, transformRotation, out var pz,_spatialHashing.CellSize.z))
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(pz, new Vector3(1F, 1F, 1F));
            }

            if (targetBounds.RayCastOBB(r0.origin, r0.direction, transformRotation,_spatialHashing.CellSize.x) && targetBounds.RayCastOBB(r0Y.origin, r0Y.direction, transformRotation,_spatialHashing.CellSize.y) && targetBounds.RayCastOBB(r0Z.origin, r0Z.direction, transformRotation,_spatialHashing.CellSize.z))
                c = new Color(0F, 1F, 0F, 0.3F);
            else
                c = Gizmos.color = Color.red;

            DrawCell(cell, c);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(r0.origin, r0.GetPoint(_spatialHashing.CellSize.x));
            Gizmos.DrawLine(r0Y.origin, r0Y.GetPoint(_spatialHashing.CellSize.y));
            Gizmos.DrawLine(r0Z.origin, r0Z.GetPoint(_spatialHashing.CellSize.z));

            for (int i = 0; i < list.Length; i++)
                DrawCell(list[i], new Color(0F, 1F, 0F, 0.3F));

            list.Dispose();

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color  = new Color(0F, 0.5F, 0.5F, 0.3F);
            Gizmos.DrawCube(Vector3.zero, _boundSize);

            Gizmos.color  = new Color(0.2F, 0.5F, 0.95F, 0.3F);
            Gizmos.DrawCube(Vector3.zero, _boundSize+_spatialHashing.CellSize);
        }

        public int indexToDraw;

        private void DrawCell(int3 index, Color c)
        {
            var position = _spatialHashing.GetPositionVoxel(index, true);
            Gizmos.color = c;
            Gizmos.DrawCube(position, _spatialHashing.CellSize);
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(position, _spatialHashing.CellSize);
        }

        #region Variables

        [SerializeField]
        private Bounds _worldBounds;
        [SerializeField]
        private float3 _boundSize = new float3(5F);
        public  GameObject                               start;
        public  GameObject                               end;
        private SpatialHash<HashMapVisualDebug.ItemTest> _spatialHashing;

        #endregion
    }
}