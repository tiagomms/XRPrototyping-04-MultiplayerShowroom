using UnityEngine;

namespace CircuitProcessor
{
    /// <summary>
    /// Wire data to be attached to instantiated wire prefabs
    /// </summary>
    public class WireData : MonoBehaviour
    {
        private Wire wire;

        public string id => wire.id;
        public Vector2Int fromGrid => wire.fromGrid;
        public Vector2Int toGrid => wire.toGrid;
        public Vector2Int fromASCII => wire.fromASCII;
        public Vector2Int toASCII => wire.toASCII;
        public Vector2 fromRect => wire.fromRect;
        public Vector2 toRect => wire.toRect;
        public bool isHorizontal => wire.isHorizontal;
        public bool startTouchesComponent => wire.startTouchesComponent;
        public bool endTouchesComponent => wire.endTouchesComponent;
        public bool isPartOfFork => wire.isPartOfFork;
        public bool isPartOfMerge => wire.isPartOfMerge;

        public void Initialize(Wire wire)
        {
            this.wire = wire;
        }
    }
}