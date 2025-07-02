using VRageMath;

namespace Khjin.ResourceClaims
{
    public class BoundingBoxDIterator
    {
        private BoundingBoxD _box;
        private Vector3D _min, _max;
        private double _x, _y, _z, _stepX, _stepY, _stepZ;
        private bool _done;

        public BoundingBoxDIterator(ref BoundingBoxD box, double stepX = 3, double stepY = 3, double stepZ = 3)
        {
            _box = box;
            _min = _box.Min;
            _max = _box.Max;

            _x = _box.Min.X;
            _y = _box.Min.Y;
            _z = _box.Min.Z;

            _stepX = stepX;
            _stepY = stepY;
            _stepZ = stepZ;
            _done = false;
        }

        public Vector3D Current
        { get { return Vector3D.Clamp(new Vector3D(_x, _y, _z), _min, _max); } }

        public bool Next()
        {
            if (_done) { return false; }

            _y += _stepY;
            if (_y > _max.Y)
            { _y = _min.Y; _x += _stepX; }

            if (_x > _max.X)
            { _x = _min.X; _z += _stepZ; }

            if (_z > _max.Z)
            { _done = true; return false; }

            return true;
        }

        public bool IsDone { get { return _done; } }
    }
}
