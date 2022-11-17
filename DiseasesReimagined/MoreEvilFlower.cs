using Klei;
using UnityEngine;

namespace DiseasesReimagined
{
    // Makes zombie flowers even more evil if wild, causing them to spread spores into the
    // tile on which they stand.
    public sealed class MoreEvilFlower : KMonoBehaviour, ISim4000ms
    {
        // The cached disease index to use for infection.
        private byte disease = SimUtil.DiseaseInfo.Invalid.idx;

        // These are populated automatically
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
        [MyCmpGet]
        private WiltCondition isWilted;

        [MyCmpGet]
        private UprootedMonitor uprooted;
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier

        protected override void OnSpawn()
        {
            base.OnSpawn();
            disease = Db.Get().Diseases.GetIndex("ZombieSpores");
        }

        public void Sim4000ms(float dt)
        {
            var obj = gameObject;
            int cell;
            if (obj != null && Grid.IsValidCell(cell = Grid.PosToCell(obj)) && isWilted !=
                null && !isWilted.IsWilting() && disease != SimUtil.DiseaseInfo.Invalid.idx &&
                uprooted != null)
            {
                foreach (var monitoredCell in uprooted.monitorCells)
                {
                    cell = Grid.OffsetCell(cell, monitoredCell);
                    if (Grid.IsValidCell(cell) && Grid.Solid[cell])
                        // Flower is growing and on a solid cell, infect it!
                        SimMessages.ModifyDiseaseOnCell(cell, disease, Mathf.RoundToInt(
                            GermExposureTuning.SPORECHID_GERMS_PER_S * dt));
                }
            }
        }
    }
}
