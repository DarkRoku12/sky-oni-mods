using System;
using System.Collections.Generic;
using SkyLib;
using STRINGS;

namespace Radiator
{
    public class Radiator : KMonoBehaviour, ISim200ms, IBridgedNetworkItem
    {
        protected static readonly Operational.Flag spaceExposureFlag =
            new Operational.Flag("InSpace", Operational.Flag.Type.Requirement);

        private static readonly double stefanBoltzmanConstant = 5.67e-8;
        private readonly float emissivity = 0.9f;
        private readonly float surface_area = 8f;
        public StatusItem _no_space_status;

        public StatusItem _radiating_status;

        private HandleVector<int>.Handle accumulator = HandleVector<int>.InvalidHandle;
        private int inputCell;
        [MyCmpReq] protected Operational operational;
        private int outputCell;
        private CellOffset[] radiatorOffsets; // the tiles that must be checked for vacuum
        [MyCmpReq] private KSelectable selectable; // does tooltip-related stuff
        private Guid statusHandle; // essentially a reference to a statusitem in particular

        private HandleVector<int>.Handle structureTemperature;
        public ConduitType type = ConduitType.Liquid;

        public float CurrentCooling { get; private set; }

        public void AddNetworks(ICollection<UtilityNetwork> networks)
        {
            var networkManager = Conduit.GetNetworkManager(type);
            var networkForCell1 = networkManager.GetNetworkForCell(inputCell);
            if (networkForCell1 != null)
                networks.Add(networkForCell1);
            var networkForCell2 = networkManager.GetNetworkForCell(outputCell);
            if (networkForCell2 == null)
                return;
            networks.Add(networkForCell2);
        }

        public bool IsConnectedToNetworks(ICollection<UtilityNetwork> networks)
        {
            var flag = false;
            var networkManager = Conduit.GetNetworkManager(type);
            return flag || networks.Contains(networkManager.GetNetworkForCell(inputCell)) ||
                   networks.Contains(networkManager.GetNetworkForCell(outputCell));
        }

        public int GetNetworkCell()
        {
            return inputCell;
        }

        public void Sim200ms(float dt)
        {
            var temp = gameObject.GetComponent<PrimaryElement>().Temperature;
            if (temp < 5) return;

            if (CheckInSpace())
            {
                var cooling = radiative_heat(temp) * 1;
                if (cooling > 1f)
                {
                    CurrentCooling = (float) cooling;
                    GameComps.StructureTemperatures.ProduceEnergy(structureTemperature, (float) -cooling / 1000,
                        BUILDING.STATUSITEMS.OPERATINGENERGY.PIPECONTENTS_TRANSFER, (float) -cooling / 1000);
                }

                UpdateStatusItem();
            }
            else
            {
                UpdateStatusItem(true);
            }
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            accumulator = Game.Instance.accumulators.Add("Flow", this);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            var building = GetComponent<Building>();
            inputCell = building.GetUtilityInputCell();
            outputCell = building.GetUtilityOutputCell();
            radiatorOffsets = new CellOffset[4]
            {
                new CellOffset(0, 0),
                new CellOffset(0, 1),
                new CellOffset(0, 2),
                new CellOffset(0, 3)
            };
            Conduit.GetFlowManager(type).AddConduitUpdater(ConduitUpdate);
            structureTemperature = GameComps.StructureTemperatures.GetHandle(gameObject);
        }

        protected override void OnCleanUp()
        {
            Conduit.GetFlowManager(type).RemoveConduitUpdater(ConduitUpdate);
            Game.Instance.accumulators.Remove(accumulator);
            base.OnCleanUp();
        }

        private void ConduitUpdate(float dt)
        {
            var flowManager = Conduit.GetFlowManager(type);
            if (!flowManager.HasConduit(inputCell)) return;
            var contents = flowManager.GetContents(inputCell);
            if (contents.mass <= 0f) return;
            var panel_mat = gameObject.GetComponent<PrimaryElement>();
            var element = ElementLoader.FindElementByHash(contents.element);
            var deltaheat = conductive_heat(element, contents.temperature, panel_mat.Element, panel_mat.Temperature,
                surface_area);
            // heat change = mass * specific heat capacity * temp change        
            var deltatemp_panel = deltaheat / RadiatorConfig.MASS[0] / panel_mat.Element.specificHeatCapacity * dt;
            var deltatemp_liquid = -deltaheat / contents.mass / element.specificHeatCapacity * dt;
            var panel_newtemp = panel_mat.Temperature + deltatemp_panel;
            var liquid_newtemp = contents.temperature + deltatemp_liquid;
            // In this case, the panel can at most be cooled to the content temperature
            if (panel_mat.Temperature > contents.temperature)
            {
                panel_newtemp = Math.Max(panel_newtemp, contents.temperature);
                liquid_newtemp = Math.Min(liquid_newtemp, panel_mat.Temperature);
            }
            else
            {
                panel_newtemp = Math.Min(panel_newtemp, contents.temperature);
                liquid_newtemp = Math.Max(liquid_newtemp, panel_mat.Temperature);
            }

            var delta = flowManager.AddElement(outputCell, contents.element, contents.mass, liquid_newtemp,
                contents.diseaseIdx, contents.diseaseCount);
            panel_mat.Temperature = panel_newtemp;
            if (delta <= 0f) return;
            flowManager.RemoveElement(inputCell, delta);
            Game.Instance.accumulators.Accumulate(accumulator, contents.mass);
        }

        // returns how much heat is transfered in W
        private static float conductive_heat(Element from, float from_temp, Element panel_material, float panel_temp,
            float area)
        {
            var conductivity = Math.Min(from.thermalConductivity, panel_material.thermalConductivity);
            return conductivity * area * (from_temp - panel_temp) * 1f;
        }

        private double radiative_heat(float temp)
        {
            return Math.Pow(temp, 4) * stefanBoltzmanConstant * emissivity * surface_area * 0.2f;
        }

        private bool CheckInSpace()
        {
            // Check whether in spaaace
            var root_cell = Grid.PosToCell(this);
            foreach (var offset in radiatorOffsets)
                if (!OniUtils.IsCellExposedToSpace(Grid.OffsetCell(root_cell, offset)))
                    return false;

            return true;
        }

        private static string _FormatStatusCallback(string formatstr, object data)
        {
            var radiator = (Radiator) data;
            var radiation_rate = GameUtil.GetFormattedHeatEnergyRate(radiator.CurrentCooling * 5);
            return string.Format(formatstr, radiation_rate);
        }

        private void UpdateStatusItem(bool no_more_space = false)
        {
            if (no_more_space && no_more_space != operational.IsOperational)
            {
                operational.SetFlag(spaceExposureFlag, false);
                // If there are no status items, just add a building disabled tooltip.
                _no_space_status = new StatusItem($"{RadiatorConfig.Id}_NOTINSPACE", "MISC", "",
                    StatusItem.IconType.Exclamation, NotificationType.BadMinor, false, OverlayModes.TileMode.ID);
                // if it's not empty we need to remove the radiating status
                if (statusHandle == Guid.Empty)
                {
                    statusHandle = selectable.AddStatusItem(_no_space_status, this);
                }
                else
                {
                    // no idea why this if statement is here but we're cargo culting it
                    if (!(statusHandle != Guid.Empty))
                        return;
                    selectable.ReplaceStatusItem(statusHandle, _no_space_status, this);
                }
            }
            else
            {
                operational.SetFlag(spaceExposureFlag, true);
                _radiating_status = new StatusItem($"{RadiatorConfig.Id}_RADIATING", "MISC", "",
                    StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.HeatFlow.ID);
                _radiating_status.resolveTooltipCallback = _FormatStatusCallback;
                _radiating_status.resolveStringCallback = _FormatStatusCallback;

                if (statusHandle == Guid.Empty)
                {
                    statusHandle = selectable.AddStatusItem(_radiating_status, this);
                }
                else
                {
                    if (!(statusHandle != Guid.Empty))
                        return;
                    GetComponent<KSelectable>().ReplaceStatusItem(statusHandle, _radiating_status, this);
                }
            }
        }
    }
}