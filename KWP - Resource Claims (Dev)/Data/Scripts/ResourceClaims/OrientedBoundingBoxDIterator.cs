using VRageMath;

namespace Khjin.ResourceClaims
{
    public class OrientedBoundingBoxDIterator : BoundingBoxIteratorBase
    {
        private Vector3D _localMin, _localMax;
        private Vector3D _currentLocal;
        private Vector3D _center;
        private MatrixD _transform;

        public OrientedBoundingBoxDIterator(ref MyOrientedBoundingBoxD obb, double stepX = 3, double stepY = 3, double stepZ = 3)
            : base(stepX, stepY, stepZ)
        {
            _center = obb.Center;
            _transform = MatrixD.CreateFromQuaternion(obb.Orientation);

            _localMin = -obb.HalfExtent;
            _localMax = obb.HalfExtent;

            _currentLocal = _localMin;

            Vector3D size = _localMax - _localMin;
            _totalCycles = (int)((size.X / _stepX) * (size.Y / _stepY) * (size.Z / _stepZ));
        }

        public override Vector3D Current => Vector3D.Transform(_currentLocal, _transform) + _center;

        public override bool Next()
        {
            if (_done) return false;

            _currentLocal.Y += _stepY;
            if (_currentLocal.Y > _localMax.Y)
            {
                _currentLocal.Y = _localMin.Y;
                _currentLocal.X += _stepX;
            }

            if (_currentLocal.X > _localMax.X)
            {
                _currentLocal.X = _localMin.X;
                _currentLocal.Z += _stepZ;
            }

            if (_currentLocal.Z > _localMax.Z)
            {
                _done = true;
                return false;
            }

            _currentCycles++;
            return true;
        }
    }
}
