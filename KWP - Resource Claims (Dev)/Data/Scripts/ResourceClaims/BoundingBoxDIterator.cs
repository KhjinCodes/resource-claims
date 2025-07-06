using VRageMath;

namespace Khjin.ResourceClaims
{
    public class BoundingBoxDIterator : BoundingBoxIteratorBase
    {
        private BoundingBoxD _box;
        private Vector3D _min, _max;
        private double _x, _y, _z;

        public BoundingBoxDIterator(ref BoundingBoxD box, double stepX = 3, double stepY = 3, double stepZ = 3)
            : base(stepX, stepY, stepZ)
        {
            _box = box;
            _min = _box.Min;
            _max = _box.Max;

            _x = _min.X;
            _y = _min.Y;
            _z = _min.Z;

            var size = _max - _min;
            _totalCycles = (int)((size.X / _stepX) * (size.Y / _stepY) * (size.Z / _stepZ));
        }

        public override Vector3D Current => Vector3D.Clamp(new Vector3D(_x, _y, _z), _min, _max);

        public override bool Next()
        {
            if (_done) return false;

            _y += _stepY;
            if (_y > _max.Y)
            {
                _y = _min.Y;
                _x += _stepX;
            }

            if (_x > _max.X)
            {
                _x = _min.X;
                _z += _stepZ;
            }

            if (_z > _max.Z)
            {
                _done = true;
                return false;
            }

            _currentCycles++;
            return true;
        }
    }
}
