using VRageMath;

namespace Khjin.ResourceClaims
{
    public abstract class BoundingBoxIteratorBase
    {
        protected double _stepX, _stepY, _stepZ;
        protected int _currentCycles;
        protected int _totalCycles;
        protected bool _done;

        public BoundingBoxIteratorBase(double stepX = 3, double stepY = 3, double stepZ = 3)
        {
            _stepX = stepX;
            _stepY = stepY;
            _stepZ = stepZ;
            _currentCycles = 0;
            _done = false;
        }

        public abstract Vector3D Current { get; }

        public abstract bool Next();

        public bool IsDone => _done;
        public int TotalCycles => _totalCycles;
        public int CurrentCycles => _currentCycles;
    }
}
