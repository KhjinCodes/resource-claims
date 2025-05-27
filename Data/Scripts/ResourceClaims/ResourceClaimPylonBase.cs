using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Voxels;
using VRageMath;
using InGame = Sandbox.ModAPI.Ingame;

namespace Khjin.ResourceClaims
{
    public abstract class ResourceClaimPylonBase : IMyEventProxy
    {
        // Base References
        private readonly IMyConveyorSorter _resourcePylon;
        private readonly ResourceClaimPylonLogic _logic;

        // Mining
        protected float _miningZoneRadius;
        private BoundingBoxD _boxMiningBounds;
        private BoundingSphereD _sphereMiningBounds;
        private BoundingBoxDIterator _boxIterator;
        private List<MyVoxelBase> _voxelMaps;
        private Queue<MyVoxelBase> _voxelMapQueue;
        private Dictionary<string, OreProfile> _detectedOres;
        private Dictionary<MyVoxelBase, BoundingBoxD> _detectedVoxelMaps;
        private int _maxScansPerCycle = 3000;

        // Production
        protected float _baseOreAmount;
        protected float _drillHeadFlatOffset;
        private float _effectiveness = 1.0f;
        private float _actualEffectiveness = 1.0f;
        private Random _randomizer = new Random();
        private IMyInventory _collectorInventory;
        private List<InGame.MyInventoryItemFilter> _filterList;
        private Queue<OreProfile> _oreQueue;
        private MyTuple<double, MyObjectBuilder_Ore> _oreForStorage;

        // Interference and Suppression
        protected float _interferenceZoneRadius;
        protected float _suppressionZoneRadius;
        protected float _suppressionPenaltyFactor;
        protected float _interferencePenaltyFactor;
        private float _interferrencePenalty;
        private float _suppressionPenalty;
        private List<long> _warnedPlayers = new List<long>();
        private static List<IMyConveyorSorter> _pylons;

        private PylonStatus _pylonStatus = PylonStatus.Idle;
        private PylonStatus _previousPylonStatus = PylonStatus.Idle;

        // Synced properties
        private PylonStatus _lastSyncedPylonStatus;
        private float _lastSyncedYield;
        private bool _lastSyncedIsInterferedStatus;
        private bool _lastSyncedIsSuppressedStatus;

        private class OreProfile
        {
            public string MinedOre;
            public float OreRatio;
        }

        public ResourceClaimPylonBase(IMyConveyorSorter resourcePylon, ResourceClaimPylonLogic logic)
        {
            _resourcePylon = resourcePylon;
            _logic = logic;
        }

        public void InitilizeAsServer()
        {
            if (_pylons == null)
            { _pylons = new List<IMyConveyorSorter>(); }

            _voxelMaps = new List<MyVoxelBase>();
            _voxelMapQueue = new Queue<MyVoxelBase>();
            _detectedOres = new Dictionary<string, OreProfile>();
            _detectedVoxelMaps = new Dictionary<MyVoxelBase, BoundingBoxD>();

            _oreQueue = new Queue<OreProfile>();
            _filterList = new List<InGame.MyInventoryItemFilter>();

            if (!_pylons.Contains(_resourcePylon))
            { _pylons.Add(_resourcePylon); }

            // Upgrades support
            _resourcePylon.UpgradeValues.Add("Effectiveness", 1f);

            // Inventory reference and constraints
            _collectorInventory = _resourcePylon.GetInventory(0);
            (_collectorInventory as MyInventory).Constraint =
                new MyInventoryConstraint(MySpaceTexts.ToolTipItemFilter_AnyOre, null, true)
                    .AddObjectBuilderType(typeof(MyObjectBuilder_Ore));

            // Synchronization Support
            _pylonStatus = PylonStatus.Idle;
            _lastSyncedPylonStatus = PylonStatus.Idle;
            _lastSyncedYield = 1.0f;
            _lastSyncedIsInterferedStatus = false;
            _lastSyncedIsSuppressedStatus = false;

            _logic._syncedPylonStatus.SetLocalValue(_lastSyncedPylonStatus);
            _logic._syncedYield.SetLocalValue(_lastSyncedYield);
            _logic._syncedIsInterfered.SetLocalValue(_lastSyncedIsInterferedStatus ? (short)1 : (short)0);
            _logic._syncedIsSuppressed.SetLocalValue(_lastSyncedIsSuppressedStatus ? (short)1 : (short)0);
        }

        public void DoWork()
        {
            if (_resourcePylon == null) { return; }

            if (!_resourcePylon.CubeGrid.IsStatic && _pylonStatus != PylonStatus.GridNotStatic)
            { 
                _pylonStatus = PylonStatus.GridNotStatic; 
            }

            if (!_resourcePylon.IsWorking && _pylonStatus != PylonStatus.NotWorking)
            {
                _previousPylonStatus = _pylonStatus;
                _pylonStatus = PylonStatus.NotWorking; 
            }

            CalculateEffectiveness();

            switch (_pylonStatus)
            {
                case PylonStatus.Idle:
                    _pylonStatus = PylonStatus.Initializing; break;
                case PylonStatus.Initializing:
                    {
                        SetupBounds();
                        GetVoxelMaps();
                        break;
                    }
                case PylonStatus.Scanning:
                    ScanVoxelMaps(); break;
                case PylonStatus.Mining:
                    GenerateOres(); break;
                case PylonStatus.Full:
                    WaitForInventorySpace(); break;
                case PylonStatus.OresBlocked:
                    WaitForOresAllowed(); break;
                case PylonStatus.NotWorking:
                    WaitUntilWorking(); break;
                case PylonStatus.NoOres:
                case PylonStatus.GridNotStatic:
                default: break;
            }

            SynchronizeStatus();
        }

        private void SynchronizeStatus()
        {
            if (_lastSyncedPylonStatus != _pylonStatus)
            {
                _logic._syncedPylonStatus.Value = _pylonStatus;
                _lastSyncedPylonStatus = _pylonStatus;
            }
            if (_lastSyncedYield != _actualEffectiveness)
            {
                _logic._syncedYield.Value = _actualEffectiveness;
                _lastSyncedYield = _actualEffectiveness;
            }
            if (_lastSyncedIsInterferedStatus != (_interferrencePenalty > 0))
            {
                _lastSyncedIsInterferedStatus = (_interferrencePenalty > 0);
                _logic._syncedIsInterfered.Value = _lastSyncedIsInterferedStatus ? (short)1 : (short)0;
            }
            if (_lastSyncedIsSuppressedStatus != (_suppressionPenalty > 0))
            {
                _lastSyncedIsSuppressedStatus = (_suppressionPenalty > 0);
                _logic._syncedIsSuppressed.Value = _lastSyncedIsSuppressedStatus ? (short)1 : (short)0;
            }
        }

        private void SetupBounds()
        {
            // Get model height (in meters)
            var blockDefinition = MyDefinitionManager.Static.GetCubeBlockDefinition(_resourcePylon.BlockDefinition);
            float height = blockDefinition.Size.Y * (_resourcePylon.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 2.5f : 0.5f);
            Vector3D flatOffset = _resourcePylon.PositionComp.WorldMatrixRef.Down * ((height / 2) + _drillHeadFlatOffset);
            Vector3D radiusOffset = _resourcePylon.PositionComp.WorldMatrixRef.Down * _miningZoneRadius;
            Vector3D center = _resourcePylon.GetPosition() + flatOffset + radiusOffset;
            _sphereMiningBounds = new BoundingSphereD(center, _miningZoneRadius);
            _boxMiningBounds = _sphereMiningBounds.GetBoundingBox();
        }

        private void GetVoxelMaps()
        {
            // Get all the voxels maps in the area
            _voxelMaps.Clear();
            _detectedOres.Clear();
            _detectedVoxelMaps.Clear();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref _sphereMiningBounds, _voxelMaps);

            if (_voxelMaps.Count == 0)
            { _pylonStatus = PylonStatus.NoOres; return; }

            for (int i = _voxelMaps.Count - 1; i >= 0; i--)
            {
                var voxelMap = _voxelMaps[i];
                if (voxelMap is MyVoxelMap)
                {
                    _detectedVoxelMaps.Add(voxelMap, (voxelMap as IMyVoxelBase).WorldAABB);
                    _voxelMapQueue.Enqueue(voxelMap);
                }
                else if (voxelMap is MyPlanet)
                {
                    _detectedVoxelMaps.Add(voxelMap, _boxMiningBounds);
                    _voxelMapQueue.Enqueue(voxelMap);
                }
            }

            _boxIterator = null;
            _pylonStatus = PylonStatus.Scanning;
        }

        private void ScanVoxelMaps()
        {
            if (_voxelMapQueue.Count > 0)
            {
                if (ScanVoxelMap(_voxelMapQueue.Peek()))
                {
                    _voxelMapQueue.Dequeue();
                    _boxIterator = null;
                }
            }

            if (_voxelMapQueue.Count == 0)
            {
                if (_detectedOres.Count > 0)
                {
                    QueueOresForMining();
                    _pylonStatus = PylonStatus.Mining;
                }
                else
                {
                    _pylonStatus = PylonStatus.NoOres;
                }
            }
        }

        private bool ScanVoxelMap(MyVoxelBase voxelMap)
        {
            if (_boxIterator == null)
            {
                var voxelMapBounds = _detectedVoxelMaps[voxelMap];
                _boxIterator = new BoundingBoxDIterator(ref voxelMapBounds, 7, 5, 3);
            }

            for (int scans = 0; scans < _maxScansPerCycle; scans++)
            {
                var worldPosition = _boxIterator.Current;
                GetOresAt(worldPosition, voxelMap);
                if (!_boxIterator.Next()) { break; }
            }

            return _boxIterator.IsDone;
        }

        private void GetOresAt(Vector3D worldPosition, MyVoxelBase voxelMap)
        {
            Vector3 localPosition;
            MyVoxelCoordSystems.WorldPositionToLocalPosition(
                worldPosition,
                voxelMap.PositionComp.WorldMatrixRef,
                voxelMap.PositionComp.WorldMatrixInvScaled,
                voxelMap.SizeInMetresHalf, out localPosition);

            var cache = new MyStorageData(MyStorageDataTypeFlags.ContentAndMaterial);
            cache.Resize(Vector3I.One);
            Vector3I scanPoint = new Vector3I(localPosition);

            voxelMap.Storage.ReadRange(cache, MyStorageDataTypeFlags.ContentAndMaterial, 0, scanPoint, scanPoint);
            byte materialId = cache.Material(0);

            if (materialId != 0 && materialId != 255)
            {
                var definition = MyDefinitionManager.Static.GetVoxelMaterialDefinition(materialId);
                if (definition != null
                && !string.IsNullOrEmpty(definition.MinedOre)
                && definition.CanBeHarvested
                && definition.AvailableInSurvival)
                {
                    if (!_detectedOres.ContainsKey(definition.MinedOre))
                    {
                        _detectedOres.Add(definition.MinedOre, new OreProfile
                        {
                            MinedOre = definition.MinedOre,
                            OreRatio = definition.MinedOreRatio
                        });
                    }
                }
            }
        }

        private void QueueOresForMining()
        {
            foreach (var oreProfile in _detectedOres.Values)
            {
                _oreQueue.Enqueue(oreProfile);
            }
        }

        private void GenerateOres()
        {
            while (_oreQueue.Count > 0 && IsFilteredOre(_oreQueue.Peek()))
            {
                _oreQueue.Dequeue();
            }

            if (_oreQueue.Count == 0)
            {
                _pylonStatus = PylonStatus.OresBlocked;
                return;
            }

            OreProfile oreProfile = _oreQueue.Peek();
            var currentOre = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>(oreProfile.MinedOre);

            double baseAmount = _baseOreAmount * MathHelper.Clamp(_randomizer.NextDouble(), 0.8f, 1.0f);
            double amount = baseAmount * oreProfile.OreRatio * _actualEffectiveness;

            if (_collectorInventory.CanItemsBeAdded((MyFixedPoint)amount, currentOre))
            {
                _collectorInventory.AddItems((MyFixedPoint)amount, currentOre);
                _oreQueue.Enqueue(_oreQueue.Dequeue());
            }
            else
            {
                _oreForStorage = new MyTuple<double, MyObjectBuilder_Ore>()
                {
                    Item1 = amount,
                    Item2 = currentOre
                };
                _pylonStatus = PylonStatus.Full;
            }
        }

        private void WaitForOresAllowed()
        {
            foreach (var oreProfile in _detectedOres.Values)
            {
                if (!IsFilteredOre(oreProfile))
                {
                    _oreQueue.Enqueue(oreProfile);
                    _pylonStatus = PylonStatus.Mining;
                }
            }
        }

        private void WaitForInventorySpace()
        {
            if (_collectorInventory.CanItemsBeAdded((MyFixedPoint)_oreForStorage.Item1, _oreForStorage.Item2))
            {
                _oreForStorage = new MyTuple<double, MyObjectBuilder_Ore>();
                _pylonStatus = PylonStatus.Mining;
            }
        }

        private void WaitUntilWorking()
        {
            if (_resourcePylon.IsWorking)
            {
                if (_previousPylonStatus <= PylonStatus.Scanning)
                { _pylonStatus = PylonStatus.Idle; }
                else
                { _pylonStatus = _previousPylonStatus; }
            }
        }

        private void CalculateEffectiveness()
        {
            // Get effects from upgrades
            if (_resourcePylon.UpgradeValues.ContainsKey("Effectiveness"))
            { _effectiveness = _resourcePylon.UpgradeValues["Effectiveness"]; }
            else
            { _effectiveness = 1.0f; }

            float suppressionSq = _suppressionZoneRadius * _suppressionZoneRadius;
            float interferenceSq = _interferenceZoneRadius * _interferenceZoneRadius;

            _interferrencePenalty = 0.0f;
            _suppressionPenalty = 0.0f;

            var claimPylonPosition = _resourcePylon.PositionComp.GetPosition();
            MyAPIGateway.Parallel.ForEach(_pylons, pylon =>
            {
                if (pylon == _resourcePylon) { return; } // continue

                var pylonPosition = pylon.PositionComp.GetPosition();

                if (Vector3D.DistanceSquared(pylonPosition, claimPylonPosition) <= suppressionSq)
                {
                    var relationship = GetRelationshipBetweenPlayers(pylon.OwnerId, _resourcePylon.OwnerId);
                    if (relationship == MyRelationsBetweenFactions.Enemies)
                    {
                        _suppressionPenalty += _suppressionPenaltyFactor;
                    }
                }
                if (Vector3D.DistanceSquared(pylonPosition, claimPylonPosition) <= interferenceSq)
                {
                    var relationship = GetRelationshipBetweenPlayers(pylon.OwnerId, _resourcePylon.OwnerId);
                    if (relationship != MyRelationsBetweenFactions.Enemies)
                    {
                        _interferrencePenalty += _interferencePenaltyFactor;
                    }
                }
            });

            _suppressionPenalty = MathHelper.Clamp(_suppressionPenalty, 0.0f, 0.6f);
            _interferrencePenalty = MathHelper.Clamp(_interferrencePenalty, 0.0f, 0.7f);

            // Calculate the actual final effectiveness
            _actualEffectiveness = (_effectiveness - _interferrencePenalty) * (1 - _suppressionPenalty);
            _actualEffectiveness = _actualEffectiveness < 0.10f ? 0.10f : _actualEffectiveness;
        }

        private MyRelationsBetweenFactions GetRelationshipBetweenPlayers(long playerIdA, long playerIdB)
        {
            if (playerIdA == playerIdB)
            { return MyRelationsBetweenFactions.Friends; }

            var factionA = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerIdA);
            var factionB = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerIdB);

            if (factionA != null && factionB != null)
            {
                return MyAPIGateway.Session.Factions.GetRelationBetweenFactions(factionA.FactionId, factionB.FactionId);
            }
            else
            {
                return MyRelationsBetweenFactions.Enemies;
            }
        }

        private bool IsFilteredOre(OreProfile oreProfile)
        {
            _filterList.Clear();
            _resourcePylon.GetFilterList(_filterList);
            bool whiteList = _resourcePylon.Mode == InGame.MyConveyorSorterMode.Whitelist ? true : false;
            if (whiteList && _filterList.Count == 0) { return true; }
            if (_filterList.Exists(i => i.ItemId.SubtypeName.Trim() == "" && i.ItemId.TypeId == typeof(MyObjectBuilder_Ore)))
            {
                return !whiteList;
            }
            else
            {
                if (whiteList != _filterList.Exists(i => i.ItemId.SubtypeName == oreProfile.MinedOre))
                {
                    return true;
                }
                return false;
            }
        }

        private bool IsInVoxelRange()
        {
            double offset = _resourcePylon.WorldAABB.Size.Z / 2 + 5;

            return false;
        }

        public abstract void AnimateSubparts();

        public abstract void PlaySounds();

        public long EntityId
        {
            get { return _resourcePylon.EntityId; }
        }
        
        public IMyConveyorSorter Block
        {
            get { return _resourcePylon; }
        }

        public PylonStatus Status
        {
            get
            {
                if (ResourceClaimPylonLogic.IsServer())
                {
                    return _pylonStatus;
                }
                else
                {
                    return _logic._syncedPylonStatus == null ? PylonStatus.Unknown : _logic._syncedPylonStatus.Value;
                }
            }
        }
        
        public float Yield
        {
            get 
            {
                if (ResourceClaimPylonLogic.IsServer())
                {
                    return _actualEffectiveness;
                }
                else
                {
                    return _logic._syncedYield == null ? 1.0f : _logic._syncedYield.Value;
                }
            }
        }

        public bool IsInterfered
        {
            get 
            {
                if (ResourceClaimPylonLogic.IsServer())
                {
                    return _interferrencePenalty > 0;
                }
                else
                {
                    return _logic._syncedIsInterfered == null ? false : _logic._syncedIsInterfered.Value == 1 ? true : false;
                }
            }
        }

        public bool IsSuppressed
        {
            get
            {
                if (ResourceClaimPylonLogic.IsServer())
                {
                    return _suppressionPenalty > 0;
                }
                else
                {
                    return _logic._syncedIsSuppressed == null ? false : _logic._syncedIsSuppressed.Value == 1 ? true : false;
                }
            }
        }
    }
}
