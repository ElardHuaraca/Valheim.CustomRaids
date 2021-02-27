﻿using System.Collections.Generic;

namespace Valheim.CustomRaids.ConfigurationCore
{
    public abstract class ConfigurationSection : IHaveEntries
    {
        public string SectionName { get; set; }

        public Dictionary<string, IConfigurationEntry> Entries { get; set; }
    }
}
