using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;

namespace Khjin.ResourceClaims
{
    public class ResourceClaimPylon : ResourceClaimPylonBase
    {
        private const float COVER_MAX_ROTATE_SPEED = -240f;
        private const float DRILL_MAX_ROTATE_SPEED = 480.0f;
        private const float MAX_CAMERA_DISTANCE = 400.0f;
        private MyEntitySubpart _coverSubpart;
        private MyEntitySubpart _drillSubpart;
        private Matrix _coverLocalMatrix;
        private Matrix _drillLocalMatrix;
        private float _coverSubpartSpeed;
        private float _drillSubpartSpeed;
        private bool pendingSubpartsInitialization;
        private PylonStatus _relayedStatus = PylonStatus.Mining;

        public ResourceClaimPylon(IMyConveyorSorter resourcePylon, ResourceClaimPylonLogic logic) 
            : base(resourcePylon, logic)
        {
            _miningZoneRadius = 150.0f;
            _interferenceZoneRadius = 10000.0f;
            _suppressionZoneRadius = 15000.0f;
            _drillHeadFlatOffset = 5.0f;

            _baseOreAmount = 10.0f;
            _suppressionPenaltyFactor = 0.20f;
            _interferencePenaltyFactor = 0.10f;

            pendingSubpartsInitialization = true;
        }

        public override void AnimateSubparts()
        {
            if (pendingSubpartsInitialization)
            {
                if(Block.TryGetSubpart("cover", out _coverSubpart))
                { _coverLocalMatrix = _coverSubpart.PositionComp.LocalMatrixRef; }

                if(Block.TryGetSubpart("drill", out _drillSubpart))
                { _drillLocalMatrix = _drillSubpart.PositionComp.LocalMatrixRef; }

                pendingSubpartsInitialization = false;
            }

            if (IsOutOfCameraRange()
            || (_coverSubpart == null || _drillSubpart == null)
            || pendingSubpartsInitialization)
            { return; }

            float coverSpeed = (Status == PylonStatus.Mining ? COVER_MAX_ROTATE_SPEED : 0) / 60;
            float drillSpeed = (Status == PylonStatus.Mining ? DRILL_MAX_ROTATE_SPEED : 0) / 60;

            _coverSubpartSpeed = MathHelper.Lerp(_coverSubpartSpeed, coverSpeed, 0.001f);
            _drillSubpartSpeed = MathHelper.Lerp(_drillSubpartSpeed, drillSpeed, 0.001f);

            if (_coverSubpartSpeed == 0 && _drillSubpartSpeed == 0)
            { return; }

            _coverLocalMatrix = Matrix.CreateFromAxisAngle(Vector3.Down, MathHelper.ToRadians(_coverSubpartSpeed)) * _coverLocalMatrix;
            _coverLocalMatrix = Matrix.Normalize(_coverLocalMatrix);

            _drillLocalMatrix = Matrix.CreateFromAxisAngle(Vector3.Down, MathHelper.ToRadians(_drillSubpartSpeed)) * _drillLocalMatrix;
            _drillLocalMatrix = Matrix.Normalize(_drillLocalMatrix);

            _coverSubpart.PositionComp.SetLocalMatrix(ref _coverLocalMatrix);
            _drillSubpart.PositionComp.SetLocalMatrix(ref _drillLocalMatrix);
        }

        public override void PlaySounds()
        {

        }

        public bool IsOutOfCameraRange()
        {
            return Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.Position, Block.GetPosition()) > (MAX_CAMERA_DISTANCE * MAX_CAMERA_DISTANCE);
        }

        public PylonStatus GetStatus()
        {
            return ResourceClaimPylonLogic.IsServer() ? Status : _relayedStatus;
        }
    
        public void SetRelayedStatus(PylonStatus relayedStatus)
        {
            _relayedStatus = relayedStatus;
        }
    }
}
