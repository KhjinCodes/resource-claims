using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;

namespace Khjin.ResourceClaims
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false,
        "KJN_KWP_RES_ResourceClaimPylon_Light",
        "KJN_KWP_RES_ResourceClaimPylon_Medium",
        "KJN_KWP_RES_ResourceClaimPylon_Heavy")]
    public class ResourceClaimPylonLogic : MyGameLogicComponent, IMyEventProxy
    {
        private IMyConveyorSorter _conveyorSorter;
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

        internal Guid DetectedOresKey = new Guid("4f3a6b6ae2ec481c97228b7d287ccc01");

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
            _conveyorSorter = (IMyConveyorSorter)Entity;
            string subtypeName = _conveyorSorter.BlockDefinition.SubtypeName;

            switch (subtypeName)
            {
                case "KJN_KWP_RES_ResourceClaimPylon_Light":
                    _resourcePylon = new ResourceClaimPylonLight(_conveyorSorter, this); break;
                case "KJN_KWP_RES_ResourceClaimPylon_Medium":
                    _resourcePylon = new ResourceClaimPylonMedium(_conveyorSorter, this); break;
                case "KJN_KWP_RES_ResourceClaimPylon_Heavy":
                    _resourcePylon = new ResourceClaimPylonHeavy(_conveyorSorter, this); break;
                default:
                    _conveyorSorter = null; return; // IDK where this came from
            }

            _conveyorSorter.OnMarkForClose += OnMarkForClose;
            _conveyorSorter.AppendingCustomInfo += AppendingCustomInfo;

            if (IsServer())
            {
                // Setup ore storage
                if (_conveyorSorter.Storage == null)
                { _conveyorSorter.Storage = new MyModStorageComponent(); }
                if (!_conveyorSorter.Storage.ContainsKey(DetectedOresKey))
                { _conveyorSorter.Storage.Add(DetectedOresKey, string.Empty); }

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
            if (IsNonCombatDamage(info.Type)) { return; }
            if (block == null || _resourcePylon == null) { return; }
            if ((block.FatBlock != null && block.FatBlock.EntityId == _resourcePylon.EntityId)
            || (block.CubeGrid != null && block.CubeGrid == _resourcePylon.Block.CubeGrid))
            {
                _resourcePylon.WarnOwners();
            }
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
            _statusLogs.AppendLine("--== Warnings ==--");
            if (_resourcePylon.IsInterfered)
            {
                _statusLogs.AppendLine("> Interfered: Friendly pylon in range, yield is slightly reduced.");
            }
            if (_resourcePylon.IsSuppressed)
            {
                _statusLogs.AppendLine("> Suppressed: Enemy pylon in range, yield is greatly reduced.");
            }
            _resourcePylon.Block.RefreshCustomInfo();
        }

        private string ToMinedOresList(MinedOres minedOres)
        {
            string minedOresList = "";
            if ((minedOres & MinedOres.Iron) != 0) { minedOresList += "- Iron\n"; }
            if ((minedOres & MinedOres.Cobalt) != 0) { minedOresList += "- Cobalt\n"; }
            if ((minedOres & MinedOres.Nickel) != 0) { minedOresList += "- Nickel\n"; }
            if ((minedOres & MinedOres.Silicon) != 0) { minedOresList += "- Silicon\n"; }
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

        internal string OresData
        {
            get { return _conveyorSorter.Storage[DetectedOresKey]; }
            set { _conveyorSorter.Storage[DetectedOresKey] = value; }
        }

        private bool IsNonCombatDamage(MyStringHash type)
        {
            if (type == MyDamageType.Bullet
            || type == MyDamageType.Explosion
            || type == MyDamageType.Rocket
            || type == MyDamageType.Mine
            || type == MyDamageType.Weapon
            || type == MyDamageType.Destruction
            || type == MyDamageType.Spider
            || type == MyDamageType.Wolf
            || type == MyDamageType.Unknown)
            { return false; }
            return true;
        }
    }
}
