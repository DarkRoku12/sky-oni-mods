﻿using TUNING;
using UnityEngine;

namespace BleachstoneRefinery
{
    class BleachstoneRefineryConfig : IBuildingConfig
    {
        public const string Id = "BleachstoneRefinery";
        public const string DisplayName = "Bleachstone Refinery";

        public const string Description =
            "Duplicants enamoured with Frost Burgers, yet stressed by the environmental implications of unchecked Bleach Stone mining operations, have devised a new, more sustainable process to refine Chlorine into Frost Burgers, with a temporary Bleach Stone stage in the middle.";

        public static string Effect = $"Synthesizes Bleach Stone using Chlorine and a small amount of Copper.";

        public const float EMIT_MASS = 10f;
        public const float INPUT_CHLORINE_PER_SECOND = 0.6f;
        public const float CHLORINE_PER_SECOND = 0.6f;
        public const float COPPER_PER_SECOND = 0.003f;
        public const float OUTPUT_TEMP = 303.15f;
        public const float REFILL_RATE = 2400f;
        public const float COPPER_STORAGE_AMOUNT = 7.2f;
        public const float CHLORINE_STORAGE_AMOUNT = 6f;
        public const float STORAGE_CAPACITY = 23.2f;

        public override BuildingDef CreateBuildingDef()
        {
            int width = 3;
            int height = 4;
            string anim = "bleachstone_refinery_kanim";
            int hitpoints = 100;
            float construction_time = 480f;
            string[] construction_materials = new string[2]
            {
                "RefinedMetal",
                "Plastic"
            };
            EffectorValues tieR5 = NOISE_POLLUTION.NOISY.TIER5;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(Id, width, height, anim, hitpoints,
                construction_time, new float[2]
                {
                    BUILDINGS.CONSTRUCTION_MASS_KG.TIER5[0],
                    BUILDINGS.CONSTRUCTION_MASS_KG.TIER2[0]
                }, construction_materials, 2400f, BuildLocationRule.OnFloor, BUILDINGS.DECOR.PENALTY.TIER1, tieR5,
                0.2f);
            buildingDef.Overheatable = false;
            buildingDef.RequiresPowerInput = true;
            buildingDef.PowerInputOffset = new CellOffset(0, 0);
            buildingDef.EnergyConsumptionWhenActive = 600f;
            buildingDef.ExhaustKilowattsWhenActive = 4f;
            buildingDef.SelfHeatKilowattsWhenActive = 2f;
            buildingDef.AudioCategory = "HollowMetal";
            buildingDef.InputConduitType = ConduitType.Gas;
            buildingDef.UtilityInputOffset = new CellOffset(1, 0);
            return buildingDef;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            Tag tag1 = SimHashes.ChlorineGas.CreateTag();
            Tag tag2 = SimHashes.Copper.CreateTag();
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery, false);
            OxyliteRefinery oxyliteRefinery = go.AddOrGet<OxyliteRefinery>();
            oxyliteRefinery.emitTag = SimHashes.BleachStone.CreateTag();
            oxyliteRefinery.emitMass = 10f;
            oxyliteRefinery.dropOffset = new Vector3(0.0f, 1f);
            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.conduitType = ConduitType.Gas;
            conduitConsumer.consumptionRate = 1.2f;
            conduitConsumer.capacityTag = tag1;
            conduitConsumer.capacityKG = 6f;
            conduitConsumer.forceAlwaysSatisfied = true;
            conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 23.2f;
            storage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            storage.showInUI = true;
            ManualDeliveryKG manualDeliveryKg = go.AddOrGet<ManualDeliveryKG>();
            manualDeliveryKg.SetStorage(storage);
            manualDeliveryKg.requestedItemTag = tag2;
            manualDeliveryKg.refillMass = 1.8f;
            manualDeliveryKg.capacity = 7.2f;
            manualDeliveryKg.choreTypeIDHash = Db.Get().ChoreTypes.MachineFetch.IdHash;
            ElementConverter elementConverter = go.AddOrGet<ElementConverter>();
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[2]
            {
                new ElementConverter.ConsumedElement(tag1, 0.6f),
                new ElementConverter.ConsumedElement(tag2, 3f / 1000f)
            };
            elementConverter.outputElements = new ElementConverter.OutputElement[1]
            {
                new ElementConverter.OutputElement(0.6f, SimHashes.BleachStone, 303.15f, false, true, 0.0f, 0.5f, 1f,
                    byte.MaxValue, 0)
            };
            Prioritizable.AddRef(go);
        }

        public override void DoPostConfigurePreview(BuildingDef def, GameObject go)
        {
            GeneratedBuildings.RegisterLogicPorts(go, LogicOperationalController.INPUT_PORTS_0_1);
        }

        public override void DoPostConfigureUnderConstruction(GameObject go)
        {
            GeneratedBuildings.RegisterLogicPorts(go, LogicOperationalController.INPUT_PORTS_0_1);
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            GeneratedBuildings.RegisterLogicPorts(go, LogicOperationalController.INPUT_PORTS_0_1);
            go.AddOrGet<LogicOperationalController>();
            go.AddOrGetDef<PoweredActiveController.Def>();
        }
    }
}