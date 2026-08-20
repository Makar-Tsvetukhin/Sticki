using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sticki.Upgrades
{
    [CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "Sticki/Upgrades/Upgrade Catalog")]
    public class UpgradeCatalog : ScriptableObject
    {
        [SerializeField] private List<UpgradeDefinition> characterUpgrades = new();
        [SerializeField] private List<UpgradeDefinition> weaponUpgrades = new();

        private Dictionary<string, UpgradeDefinition> byId;

        public IReadOnlyList<UpgradeDefinition> CharacterUpgrades => characterUpgrades;
        public IReadOnlyList<UpgradeDefinition> WeaponUpgrades => weaponUpgrades;

        public bool TryGetUpgradeById(string id, out UpgradeDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureIndex();
            return byId.TryGetValue(id, out definition);
        }

        private void OnEnable()
        {
            byId = null;
        }

        private void EnsureIndex()
        {
            if (byId != null)
            {
                return;
            }

            byId = new Dictionary<string, UpgradeDefinition>(StringComparer.OrdinalIgnoreCase);
            IndexUpgrades(characterUpgrades);
            IndexUpgrades(weaponUpgrades);
        }

        private void IndexUpgrades(List<UpgradeDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                UpgradeDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                byId[definition.Id] = definition;
            }
        }
    }
}
