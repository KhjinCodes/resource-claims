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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, 
        "KJN_KWP_RES_ResourceClaimPylon_Light",
        "KJN_KWP_RES_ResourceClaimPylon_Medium",
        "KJN_KWP_RES_ResourceClaimPylon_Heavy")]
    public class ResourceClaimPylonLogic : MyGameLogicComponent, IMyEventProxy
    {
        private ResourceClaimPylonBase _resourcePylon;
        private StringBuilder _statusLogs;
        private int _customInfoUpdateCycle = 0;

        // !IMPORTANT! !IMPORTANT! !IMPORTANT! !IMPORTANT!
        // These are only for syncing, Do not access directly,
        // use the _resourcePylon properties
        internal MySync<PylonStatus, SyncDirection.FromServer> _syncedPylonStatus;
        internal MySync<MinedOres, SyncDirection.FromServer> _synchedMinedOres;
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
            string subtypeName = pylonInstance.BlockDefinition.SubtypeName;

            switch (subtypeName)
            {
                case "KJN_KWP_RES_ResourceClaimPylon_Light":
                    _resourcePylon = new ResourceClaimPylonLight(pylonInstance, this); break;
                case "KJN_KWP_RES_ResourceClaimPylon_Medium":
                    _resourcePylon = new ResourceClaimPylonMedium(pylonInstance, this); break;
                case "KJN_KWP_RES_ResourceClaimPylon_Heavy":
                    _resourcePylon = new ResourceClaimPylonHeavy(pylonInstance, this); break;
                default:
                    pylonInstance = null; return; // IDK where this came from
            }

            pylonInstance.OnMarkForClose += OnMarkForClose;
            pylonInstance.AppendingCustomInfo += AppendingCustomInfo;

            if (IsServer())
            {
                // For KJN_KWP_RES_ResourceClaimPylon_Light
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
            _resourcePylon.WarnOwners();
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
            _statusLogs.AppendLine($"Detected Ores:\n{ToMinedOresList(_resourcePylon.MinedOres)}");
            _resourcePylon.Block.RefreshCustomInfo();
        }

        private string ToMinedOresList(MinedOres minedOres)
        {
            string minedOresList = "";
            if ((minedOres & MinedOres.Iron) != 0) { minedOresList += "- Iron\n"; }
            if ((minedOres & MinedOres.Cobalt) != 0) { minedOresList += "- Cobalt\n"; }
            if ((minedOres & MinedOres.Nickel) != 0) { minedOresList += "- Nickel\n"; }
            if ((minedOres & MinedOres.Magnesium) != 0) { minedOresList += "- Magnesium\n"; }
            if ((minedOres & MinedOres.Silver) != 0) { minedOresList += "- Silver\n"; }
            if ((minedOres & MinedOres.Gold) != 0) { minedOresList += "- Gold\n"; }
            if ((minedOres & MinedOres.Uranium) != 0) { minedOresList += "- Uranium\n"; }
            if ((minedOres & MinedOres.Platinum) != 0) { minedOresList += "- Platinum\n"; }
            if ((minedOres & MinedOres.Stone) != 0) { minedOresList += "- Stone\n"; }
            if ((minedOres & MinedOres.Ice) != 0) { minedOresList += "- Ice\n"; }
            return minedOresList;
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
