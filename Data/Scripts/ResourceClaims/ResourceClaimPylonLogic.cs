using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;

namespace Khjin.ResourceClaims
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "KJN_KWP_RES_ResourceClaimPylon")]
    public class ResourceClaimPylonLogic : MyGameLogicComponent, IMyEventProxy
    {
        private ResourceClaimPylonBase _resourcePylon;
        private StringBuilder _statusLogs;
        private int _customInfoUpdateCycle = 0;

        // !IMPORTANT! !IMPORTANT! !IMPORTANT! !IMPORTANT!
        // These are only for syncing, Do not access directly,
        // use the _resourcePylon properties
        internal MySync<PylonStatus, SyncDirection.FromServer> _syncedPylonStatus;
        internal MySync<float, SyncDirection.FromServer> _syncedYield;
        internal MySync<short, SyncDirection.FromServer> _syncedIsInterfered;
        internal MySync<short, SyncDirection.FromServer> _syncedIsSuppressed;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            _statusLogs = new StringBuilder();
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            // Create base instance, available for both client and server
            var pylonInstance = (IMyConveyorSorter)Entity;
            _resourcePylon = new ResourceClaimPylon(pylonInstance, this);

            pylonInstance.OnMarkForClose += OnMarkForClose;
            pylonInstance.AppendingCustomInfo += AppendingCustomInfo;

            if (IsServer())
            {
                // For KJN_KWP_RES_ResourceClaimPylon
                // TODO: Add more pylons!
                _resourcePylon.InitilizeAsServer();
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(int.MaxValue, OnDamage);

                NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            _resourcePylon.AnimateSubparts();

            if (CanUpdateCustomInfo())
            {
                UpdateCustomInfo();
            }
        }

        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
            if (!IsServer() || _resourcePylon.Status >= PylonStatus.Mining)
            { return; }
            else
            { _resourcePylon.DoWork(); }
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            if (!IsServer() || _resourcePylon.Status < PylonStatus.Mining) 
            { return; }
            else
            { _resourcePylon.DoWork(); }
        }

        private void OnDamage(object target, ref MyDamageInformation info)
        {
            IMySlimBlock block = target as IMySlimBlock;
            if (block == null || block.FatBlock == null || _resourcePylon == null) { return; }
            if (block.FatBlock.EntityId != _resourcePylon.EntityId) { return; }
        }

        private bool CanUpdateCustomInfo()
        {
            _customInfoUpdateCycle++;
            if (_customInfoUpdateCycle >= 100)
            {
                _customInfoUpdateCycle = 0;
                return true;
            }
            return false;
        }

        private void UpdateCustomInfo()
        {
            _statusLogs.Clear();
            _statusLogs.AppendLine($"Resource Claim Pylon Info");
            _statusLogs.AppendLine($"Pylon Status: {_resourcePylon.Status}");
            _statusLogs.AppendLine($"Yield: {_resourcePylon.Yield * 100:0.00}%");
            _statusLogs.AppendLine($"Interfered: {(_resourcePylon.IsInterfered ? "Yes" : "No")}");
            _statusLogs.AppendLine($"Suppressed: {(_resourcePylon.IsSuppressed ? "Yes" : "No")}");
            _resourcePylon.Block.RefreshCustomInfo();
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder customInfo)
        {
            customInfo.Clear();
            customInfo.AppendLine(_statusLogs.ToString());
        }

        private void OnMarkForClose(IMyEntity obj)
        {
            _resourcePylon.Block.AppendingCustomInfo -= AppendingCustomInfo;
            _resourcePylon.Block.OnMarkForClose -= OnMarkForClose;
        }

        public static bool IsServer()
        {
            return (MyAPIGateway.Session.IsServer || MyAPIGateway.Utilities.IsDedicated);
        }
    }
}
