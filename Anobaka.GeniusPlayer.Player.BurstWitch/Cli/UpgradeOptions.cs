using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace Anobaka.GeniusPlayer.Player.BurstWitch.Cli
{
    [Verb("upgrade", HelpText = "Parse all runes and upgrade them if needed and export the final data to excel.")]
    internal class UpgradeOptions : ListOptions
    {
    }
}