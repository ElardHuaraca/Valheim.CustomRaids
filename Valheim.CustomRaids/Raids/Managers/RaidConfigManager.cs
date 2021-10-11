﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valheim.CustomRaids.Configuration;
using Valheim.CustomRaids.Configuration.ConfigTypes;
using Valheim.CustomRaids.Core;
using Valheim.CustomRaids.Debug;
using Valheim.CustomRaids.Raids.Conditions;
using Valheim.CustomRaids.Spawns.Caches;
using Valheim.CustomRaids.Utilities.Extensions;

namespace Valheim.CustomRaids.Raids.Managers
{
    public static class RaidConfigManager
    {
        public static void ApplyConfigs()
        {
            try
            {
                var eventSystem = RandEventSystem.instance;

                Log.LogDebug("Applying configurations to RandEventSystem.");

                if (ConfigurationManager.GeneralConfig is null)
                {
                    Log.LogWarning("No configuration loaded yet. Skipping application of raid changes.");
                    return;
                }

                if (ConfigurationManager.GeneralConfig?.WriteDefaultEventDataToDisk?.Value == true)
                {
                    EventsWriter.WriteToFile(eventSystem.m_events);
                }

                if (ConfigurationManager.GeneralConfig?.EventCheckInterval is not null)
                {
                    eventSystem.m_eventIntervalMin = ConfigurationManager.GeneralConfig.EventCheckInterval.Value;
                }

                if (ConfigurationManager.GeneralConfig?.EventTriggerChance is not null)
                {
                    eventSystem.m_eventChance = ConfigurationManager.GeneralConfig.EventTriggerChance.Value;
                }

                if (ConfigurationManager.GeneralConfig?.RemoveAllExistingRaids?.Value == true)
                {
                    Log.LogDebug("Removing default raids.");
                    eventSystem.m_events?.RemoveAll(x => x.m_random);
                }

                if (ConfigurationManager.RaidConfig?.Subsections is null)
                {
                    return;
                }

                Log.LogDebug($"Found {ConfigurationManager.RaidConfig.Subsections.Count} raid configurations to apply.");

                foreach (var raid in ConfigurationManager.RaidConfig.Subsections.Values)
                {
                    if (ConfigurationManager.GeneralConfig?.OverrideExisting?.Value == true)
                    {
                        //Check for overrides
                        if ((eventSystem.m_events?.Count ?? 0) > 0)
                        {
                            for (int i = 0; i < eventSystem.m_events.Count; ++i)
                            {
                                string cleanedEventName = eventSystem.m_events[i].m_name.ToUpperInvariant().Trim();
                                string cleanedRaidName = raid.Name.Value.ToUpperInvariant().Trim();
                                if (cleanedEventName == cleanedRaidName)
                                {
                                    Log.LogDebug($"Overriding existing event {eventSystem.m_events[i].m_name} with configured");
                                    eventSystem.m_events.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }

                    if ((raid.Enabled?.Value ?? false) == false)
                    {
                        continue;
                    }

                    Log.LogDebug($"Adding raid '{raid.Name}' to possible raids");

                    try
                    {
                        if (eventSystem.m_events is null)
                        {
                            eventSystem.m_events = new List<RandomEvent>();
                        }

                        if ((raid?.Subsections?.Count ?? 0) == 0)
                        {
                            continue;
                        }

                        var randomEvent = CreateEvent(raid);
                        RandomEventCache.Initialize(randomEvent, raid);
                        StoreRaid(randomEvent, raid);

                        eventSystem.m_events.Add(randomEvent);
                    }
                    catch (Exception e)
                    {
                        Log.LogWarning($"Failed to create possible raid {raid.Name}: " + e.Message);
                    }
                }

                if (ConfigurationManager.GeneralConfig.WritePostChangeEventDataToDisk.Value)
                {
                    EventsWriter.WriteToFile(eventSystem.m_events, "custom_random_events.txt");
                }
            }
            catch (Exception e)
            {
                Log.LogError("Error during application of raid configurations.", e);
            }
        }

        private static Heightmap.Biome GetBiome(RaidEventConfiguration config)
        {
            var biomeString = config.Biomes.Value;

            if (string.IsNullOrEmpty(biomeString))
            {
                return (Heightmap.Biome)1023;
            }

            List<Heightmap.Biome> biomes = new List<Heightmap.Biome>();

            var biomeRequest = Heightmap.Biome.None;

            foreach (var biome in biomeString.SplitByComma())
            {
                if (Enum.TryParse(biome.Trim(), true, out Heightmap.Biome biomeFlag))
                {
                    biomes.Add(biomeFlag);

                    biomeRequest = biomeRequest | biomeFlag;
                }
                else
                {
                    Log.LogWarning($"Unable to parse biome '{biome}'");
                }
            }

            return biomeRequest;
        }

        private static RandomEvent CreateEvent(RaidEventConfiguration raidEvent)
        {
            var spawnList = new List<SpawnSystem.SpawnData>();

            foreach (var spawnConfig in raidEvent.Subsections.Values)
            {
                var spawnObject = ZNetScene.instance.GetPrefab(spawnConfig.PrefabName.Value);

                if (spawnObject is null)
                {
                    Log.LogWarning($"Unable to find spawn {spawnConfig.PrefabName.Value}");
                    continue;
                }

                var requiredEnvironments = spawnConfig.RequiredEnvironments?.Value?.SplitByComma();

                SpawnSystem.SpawnData spawn = new SpawnSystem.SpawnData
                {
                    m_name = spawnConfig.SectionKey,
                    m_enabled = spawnConfig.Enabled.Value,
                    m_prefab = spawnObject,
                    m_maxSpawned = spawnConfig.MaxSpawned.Value,
                    m_spawnInterval = spawnConfig.SpawnInterval.Value,
                    m_spawnChance = spawnConfig.SpawnChancePerInterval.Value,
                    m_spawnDistance = spawnConfig.SpawnDistance.Value,
                    m_spawnRadiusMin = spawnConfig.SpawnRadiusMin.Value,
                    m_spawnRadiusMax = spawnConfig.SpawnRadiusMax.Value,
                    m_groupSizeMin = spawnConfig.GroupSizeMin.Value,
                    m_groupSizeMax = spawnConfig.GroupSizeMax.Value,
                    m_huntPlayer = spawnConfig.HuntPlayer.Value,
                    m_maxLevel = spawnConfig.MaxLevel.Value,
                    m_minLevel = spawnConfig.MinLevel.Value,
                    m_groundOffset = spawnConfig.GroundOffset.Value,
                    m_groupRadius = spawnConfig.GroupRadius.Value,
                    m_inForest = spawnConfig.InForest.Value,
                    m_maxAltitude = spawnConfig.AltitudeMax.Value,
                    m_minAltitude = spawnConfig.AltitudeMin.Value,
                    m_maxOceanDepth = spawnConfig.OceanDepthMax.Value,
                    m_minOceanDepth = spawnConfig.OceanDepthMin.Value,
                    m_minTilt = spawnConfig.TerrainTiltMin.Value,
                    m_maxTilt = spawnConfig.TerrainTiltMax.Value,
                    m_outsideForest = spawnConfig.OutsideForest.Value,
                    m_spawnAtDay = spawnConfig.SpawnAtDay.Value,
                    m_spawnAtNight = spawnConfig.SpawnAtNight.Value,
                    m_requiredGlobalKey = spawnConfig.RequiredGlobalKey.Value,
                    m_requiredEnvironments = requiredEnvironments,
                    m_biome = (Heightmap.Biome)1023,
                    m_biomeArea = (Heightmap.BiomeArea)7,
                };

                Log.LogDebug($"Adding {spawnConfig.Name} to {raidEvent.Name}");

                SpawnDataCache.GetOrCreate(spawn)
                    .SetSpawnConfig(spawnConfig)
                    .SetRaidConfig(raidEvent);

                spawnList.Add(spawn);
            }

            var notRequiredGlobalKeys = raidEvent.NotRequiredGlobalKeys?.Value?.SplitByComma();
            var requiredGlobalKeys = raidEvent.RequiredGlobalKeys?.Value?.SplitByComma();

            RandomEvent newEvent = new RandomEvent
            {
                m_name = raidEvent.Name.Value,
                m_duration = raidEvent.Duration.Value,
                m_forceEnvironment = raidEvent.ForceEnvironment.Value,
                m_forceMusic = raidEvent.ForceMusic.Value,
                m_nearBaseOnly = raidEvent.NearBaseOnly.Value,
                m_startMessage = raidEvent.StartMessage.Value,
                m_endMessage = raidEvent.EndMessage.Value,
                m_enabled = true,
                m_random = raidEvent.Random.Value,
                m_spawn = spawnList,
                m_biome = GetBiome(raidEvent),
                m_notRequiredGlobalKeys = notRequiredGlobalKeys,
                m_requiredGlobalKeys = requiredGlobalKeys,
                m_pauseIfNoPlayerInArea = raidEvent.PauseIfNoPlayerInArea.Value,
            };

            return newEvent;
        }

        private static void StoreRaid(RandomEvent randomEvent, RaidEventConfiguration config)
        {
            var raid = new Raid(randomEvent.m_name);

            // Set raid conditions.
            List<IRaidCondition> conditions = new List<IRaidCondition>();

            if (config.ConditionWorldAgeDaysMin?.Value > 0 || config.ConditionWorldAgeDaysMax?.Value > 0)
            {
                conditions.Add(new ConditionWorldAge()
                {
                    MinDays = config.ConditionWorldAgeDaysMin?.Value,
                    MaxDays = config.ConditionWorldAgeDaysMax?.Value,
                });
            }

            conditions.Add(new ConditionAltitude
            {
                MinAltitude = config.ConditionAltitudeMin?.Value,
                MaxAltitude = config.ConditionAltitudeMax?.Value
            });

            if (config.ConditionDistanceToCenterMin?.Value > 0 || config.ConditionDistanceToCenterMax?.Value > 0)
            {
                conditions.Add(new ConditionDistanceToCenter
                {
                    MinDistance = config.ConditionDistanceToCenterMin?.Value,
                    MaxDistance = config.ConditionDistanceToCenterMax?.Value,
                });
            }

            conditions.Add(new ConditionTimeOfDay
            {
                DuringDay = config.CanStartDuringDay?.Value,
                DuringNight = config.CanStartDuringNight?.Value,
            });

            if (config.ConditionMinPlayersNearby?.Value > 0 || config.ConditionMaxPlayersNearby?.Value > 0)
            {
                conditions.Add(new ConditionPlayersNearby
                {
                    MinPlayers = config.ConditionMinPlayersNearby?.Value,
                    MaxPlayers = config.ConditionMaxPlayersNearby?.Value
                });
            }

            if (config.ConditionMinPlayersOnline?.Value > 0 || config.ConditionMaxPlayersOnline?.Value > 0)
            {
                conditions.Add(new ConditionPlayersOnline
                {
                    MinPlayersOnline = config.ConditionMinPlayersOnline?.Value,
                    MaxPlayersOnline = config.ConditionMaxPlayersOnline?.Value
                });
            }

            RaidManager.RegisterRaid(randomEvent, raid);
        }
    }
}
