using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Khjin.ResourceClaims
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "KJN_KWP_RES_ResourceClaimPylon")]
    public class ResourceClaimPylonLogic : MyGameLogicComponent
    {
        private ResourceClaimPylonBase _resourcePylon;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            var pylonInstance = (IMyConveyorSorter)Entity;
            pylonInstance.OnMarkForClose += OnMarkForClose;
            pylonInstance.AppendingCustomInfo += AppendingCustomInfo;

            if (IsServer())
            {
                // For KJN_KWP_RES_ResourceClaimPylon
                // TODO: Add more pylons!
                _resourcePylon = new ResourceClaimPylon(pylonInstance);
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
            if (block == null || block.FatBlock.EntityId != _resourcePylon.EntityId) { return; }
        }

        private void OnMessageRecieved(ulong arg1, string arg2)
        {

        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder customInfo)
        {

        }

        private void OnMarkForClose(IMyEntity obj)
        {
            MyAPIGateway.Utilities.MessageRecieved -= OnMessageRecieved;
            _resourcePylon.Block.AppendingCustomInfo -= AppendingCustomInfo;
            _resourcePylon.Block.OnMarkForClose -= OnMarkForClose;
        }

        public static bool IsServer()
        {
            return (MyAPIGateway.Session.IsServer || MyAPIGateway.Utilities.IsDedicated);
        }
    }
}
