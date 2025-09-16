using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;

namespace Khjin.ResourceClaims
{
    public class ResourceClaimPylonLight : ResourceClaimPylonBase
    {
        private const float DRILL_MAX_ROTATE_SPEED = 480.0f;
        private const float MAX_CAMERA_DISTANCE = 400.0f;
        private MyEntitySubpart _drillSubpart;
        private Matrix _drillLocalMatrix;
        private float _drillSubpartSpeed;
        private bool pendingSubpartsInitialization;
        private PylonStatus _relayedStatus = PylonStatus.Mining;


        public ResourceClaimPylonLight(IMyConveyorSorter resourcePylon, ResourceClaimPylonLogic logic) 
            : base(resourcePylon, logic)
        {
            _miningZoneRadius = 300.0f;
            _interferenceZoneRadius = 2000.0f;
            _suppressionZoneRadius = 5000.0f;
            // _drillHeadFlatOffset = 2.0f;
            _drillHeadFlatOffset = -48.0f;

            // _baseOreAmount = 10.0f;
            _baseOreAmount = 5.0f;
            _interferencePenaltyFactor = 0.10f;
            _suppressionPenaltyFactor = 0.10f;

            pendingSubpartsInitialization = true;
        }

        public override void AnimateSubparts()
        {
            if (pendingSubpartsInitialization)
            {
                if(Block.TryGetSubpart("drill", out _drillSubpart))
                { _drillLocalMatrix = _drillSubpart.PositionComp.LocalMatrixRef; }

                pendingSubpartsInitialization = false;
            }

            if (IsOutOfCameraRange()
            || (_drillSubpart == null)
            || pendingSubpartsInitialization)
            { return; }

            float drillSpeed = (Status == PylonStatus.Mining ? DRILL_MAX_ROTATE_SPEED : 0) / 60;
            _drillSubpartSpeed = MathHelper.Lerp(_drillSubpartSpeed, drillSpeed, 0.001f);

            if (_drillSubpartSpeed == 0)
            { return; }

            _drillLocalMatrix = Matrix.CreateFromAxisAngle(Vector3.Down, MathHelper.ToRadians(_drillSubpartSpeed)) * _drillLocalMatrix;
            _drillLocalMatrix = Matrix.Normalize(_drillLocalMatrix);
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
